using System;
using System.Globalization;
using MxFramework.Editor.CharacterAction;
using MxFramework.Combat.Core;
using MxFramework.Combat.Projectiles;
using MxFramework.Core.Math;
using Newtonsoft.Json.Linq;

namespace MxFramework.Editor.ProjectileAuthoring
{
    /// <summary>Authoring-only deterministic caster/socket/target fixture consumed by the formal spawn profile resolver.</summary>
    public readonly struct ProjectilePreviewSpawnFixture
    {
        public ProjectilePreviewSpawnFixture(
            FixVector3 casterRoot,
            FixVector3 casterForward,
            bool hasSocketPose,
            FixVector3 socketPosition,
            FixVector3 socketForward,
            bool hasLockedTarget,
            FixVector3 lockedTargetPoint,
            int lockedTargetEntityId = 2)
        {
            CasterRoot = casterRoot;
            CasterForward = casterForward;
            HasSocketPose = hasSocketPose;
            SocketPosition = socketPosition;
            SocketForward = socketForward;
            HasLockedTarget = hasLockedTarget;
            LockedTargetPoint = lockedTargetPoint;
            LockedTargetEntityId = lockedTargetEntityId;
        }

        public FixVector3 CasterRoot { get; }
        public FixVector3 CasterForward { get; }
        public bool HasSocketPose { get; }
        public FixVector3 SocketPosition { get; }
        public FixVector3 SocketForward { get; }
        public bool HasLockedTarget { get; }
        public FixVector3 LockedTargetPoint { get; }
        public int LockedTargetEntityId { get; }
    }

    public readonly struct ProjectilePreviewSpawnResolution
    {
        internal ProjectilePreviewSpawnResolution(
            bool success,
            FixVector3 origin,
            FixVector3 direction,
            FixVector3 targetPoint,
            int effectiveLifetimeFrames,
            bool hasTrackingTarget,
            CombatEntityId trackingTargetEntityId,
            bool syntheticProfile,
            bool socketFallback,
            bool lockedTargetFallback,
            bool ballisticFeasible,
            Fix64 resolvedRange,
            string diagnosticCode,
            string sceneLabel)
        {
            Success = success;
            Origin = origin;
            Direction = direction;
            TargetPoint = targetPoint;
            EffectiveLifetimeFrames = effectiveLifetimeFrames;
            HasTrackingTarget = hasTrackingTarget;
            TrackingTargetEntityId = trackingTargetEntityId;
            SyntheticProfile = syntheticProfile;
            SocketFallback = socketFallback;
            LockedTargetFallback = lockedTargetFallback;
            BallisticFeasible = ballisticFeasible;
            ResolvedRange = resolvedRange;
            DiagnosticCode = diagnosticCode ?? string.Empty;
            SceneLabel = sceneLabel ?? string.Empty;
        }

        public bool Success { get; }
        public FixVector3 Origin { get; }
        public FixVector3 Direction { get; }
        public FixVector3 TargetPoint { get; }
        public int EffectiveLifetimeFrames { get; }
        public bool HasTrackingTarget { get; }
        public CombatEntityId TrackingTargetEntityId { get; }
        public bool SyntheticProfile { get; }
        public bool SocketFallback { get; }
        public bool LockedTargetFallback { get; }
        public bool BallisticFeasible { get; }
        public Fix64 ResolvedRange { get; }
        public string DiagnosticCode { get; }
        public string SceneLabel { get; }
    }

    /// <summary>Projects compiled Request semantics into the formal projectile runtime spawn request.</summary>
    /// <remarks>Resolve is LowFreqAlloc for the cached Scene label. Runtime frame playback does not call this resolver.</remarks>
    public static class ProjectilePreviewSpawnResolver
    {
        // Reserved editor-only profile identity. It is never emitted into runtime content catalogs.
        private const int PreviewSyntheticProfileId = 990001;
        private const int SocketIdentityDomain = 400;

        public static CombatProjectileSpawnProfile CreateProfile(
            ProjectileDefinitionListItem definition,
            ProjectileRequestVariantListItem request,
            out bool synthetic)
        {
            if (definition == null || string.IsNullOrWhiteSpace(definition.stableId))
                throw new ArgumentException("Preview projectile definition is missing.", nameof(definition));

            ProjectileRequestDetailView detail = request?.detail;
            bool hasRequestProfileId = TryReadPositiveInt(request?.rawJson, "profileId", out int requestProfileId);
            bool hasDefinitionProfileId = TryReadPositiveInt(definition.rawJson, "profileId", out int definitionProfileId);
            if (!hasRequestProfileId && !hasDefinitionProfileId && detail != null)
            {
                hasRequestProfileId = TryReadGeneratedProfileId(
                    detail.projectileStableId,
                    definition.definitionId,
                    out requestProfileId);
            }
            synthetic = detail == null || (!hasRequestProfileId && !hasDefinitionProfileId);
            int profileId = detail == null
                ? PreviewSyntheticProfileId
                : hasRequestProfileId
                    ? requestProfileId
                    : hasDefinitionProfileId
                        ? definitionProfileId
                        : PreviewSyntheticProfileId;
            if (synthetic)
            {
                return new CombatProjectileSpawnProfile(
                    profileId,
                    "preview.synthetic." + definition.stableId,
                    definition.definitionId,
                    definition.stableId,
                    new CombatProjectileSpawnOriginConfig(
                        CombatProjectileSpawnOriginKind.Socket,
                        ResolveSocketId("mainHand"),
                        FixVector3.Zero,
                        FixVector3.Zero,
                        "mainHand"),
                    new CombatProjectileAimConfig(
                        CombatProjectileAimKind.LockedTargetOrCasterForwardRange,
                        Fix64.Zero,
                        CombatProjectileAimKind.CasterForwardRange,
                        Fix64.Zero));
            }

            if (!string.Equals(detail.projectileStableId, definition.stableId, StringComparison.Ordinal))
                throw new ArgumentException("Preview Request references a different projectile definition.", nameof(request));
            if (detail.origin == null || detail.aim == null)
                throw new ArgumentException("Preview Request spawn profile is incomplete.", nameof(request));
            if (!TryParseDefined(detail.origin.kind, out CombatProjectileSpawnOriginKind originKind)
                || !TryParseDefined(detail.aim.kind, out CombatProjectileAimKind aimKind)
                || !TryParseDefined(detail.aim.fallbackKind, out CombatProjectileAimKind fallbackKind))
                throw new ArgumentException("Preview Request spawn profile contains an unsupported enum value.", nameof(request));

            var origin = new CombatProjectileSpawnOriginConfig(
                originKind,
                ResolveSocketId(detail.origin.socketId),
                ToFix(detail.origin.localOffset),
                ToFix(detail.origin.fallbackRootOffset),
                detail.origin.socketId);
            var aim = new CombatProjectileAimConfig(
                aimKind,
                ToFix(detail.aim.targetHeightOffset),
                fallbackKind,
                ToFix(detail.aim.maxRange));
            return new CombatProjectileSpawnProfile(
                profileId,
                detail.stableId,
                definition.definitionId,
                detail.projectileStableId,
                origin,
                aim);
        }

        private static int ResolveSocketId(string value)
        {
            return CharacterActionAuthoringIds.Resolve(
                value,
                SocketIdentityDomain,
                allocate: false);
        }

        private static bool TryReadPositiveInt(string rawJson, string propertyName, out int value)
        {
            value = 0;
            if (string.IsNullOrWhiteSpace(rawJson))
                return false;
            try
            {
                JToken token = JObject.Parse(rawJson)[propertyName];
                return token != null
                    && token.Type == JTokenType.Integer
                    && (value = token.Value<int>()) > 0;
            }
            catch (Exception)
            {
                return false;
            }
        }

        private static bool TryReadGeneratedProfileId(
            string projectileStableId,
            int projectileDefinitionId,
            out int profileId)
        {
            profileId = 0;
            try
            {
                CombatProjectileSpawnProfileRegistry registry =
                    CombatProjectileSpawnProfileConfigJsonImporter.ImportFile(
                        CharacterActionProjectileAuthoringStore.DefaultGeneratedProjectileSpawnProfilesPath);
                CombatProjectileSpawnProfile[] profiles = registry.CreateSnapshot();
                for (int i = 0; i < profiles.Length; i++)
                {
                    CombatProjectileSpawnProfile profile = profiles[i];
                    if ((projectileDefinitionId > 0 && profile.ProjectileDefinitionId == projectileDefinitionId)
                        || string.Equals(profile.ProjectileStableId, projectileStableId, StringComparison.Ordinal))
                    {
                        profileId = profile.ProfileId;
                        return profileId > 0;
                    }
                }
            }
            catch (Exception)
            {
                // A missing generated catalog leaves the caller on the explicit synthetic preview identity.
            }

            return false;
        }

        public static ProjectilePreviewSpawnResolution Resolve(
            CombatProjectileSpawnProfile profile,
            CombatProjectileDefinition definition,
            in ProjectilePreviewSpawnFixture fixture,
            bool syntheticProfile)
        {
            if (profile == null || definition == null)
                return Failure(FixVector3.Zero, FixVector3.Zero, "PROJAUTH021", string.Empty, syntheticProfile, false, false, Fix64.Zero);

            FixVector3 casterForward = NormalizeOr(fixture.CasterForward, new FixVector3(Fix64.One, Fix64.Zero, Fix64.Zero));
            bool socketFallback = !fixture.HasSocketPose;
            FixVector3 origin = fixture.HasSocketPose
                ? fixture.SocketPosition + TransformLocal(NormalizeOr(fixture.SocketForward, casterForward), profile.Origin.LocalOffset)
                : fixture.CasterRoot + TransformLocal(casterForward, profile.Origin.FallbackRootOffset + profile.Origin.LocalOffset);

            CombatProjectileAimConfig aim = profile.Aim;
            Fix64 range = ResolveAimRange(aim, definition);
            bool wantsLockedTarget = aim.Kind == CombatProjectileAimKind.LockedTargetCenter
                || aim.Kind == CombatProjectileAimKind.LockedTargetOrCasterForwardRange;
            bool lockedFallback = false;
            FixVector3 targetPoint;
            CombatEntityId trackingId = default;
            bool resolvedLockedTarget = false;
            if (wantsLockedTarget && fixture.HasLockedTarget && fixture.LockedTargetEntityId > 0)
            {
                targetPoint = fixture.LockedTargetPoint
                    + new FixVector3(Fix64.Zero, aim.TargetHeightOffset, Fix64.Zero);
                FixVector3 delta = targetPoint - origin;
                bool reachable = delta.TryNormalize(out _);
                if (reachable && definition.Motion.Kind != CombatProjectileMotionKind.BallisticArcToTarget
                    && range > Fix64.Zero && delta.LengthSquared() > range * range)
                    reachable = false;
                if (reachable && definition.Motion.Kind == CombatProjectileMotionKind.BallisticArcToTarget)
                    reachable = TrySolveBallistic(definition, origin, targetPoint, out _);
                if (reachable)
                {
                    resolvedLockedTarget = true;
                    trackingId = new CombatEntityId(fixture.LockedTargetEntityId);
                }
                else if (aim.Kind == CombatProjectileAimKind.LockedTargetCenter)
                {
                    return Failure(origin, targetPoint, "PROJAUTH021", BallisticLabel(definition, origin, targetPoint, range, false),
                        syntheticProfile, socketFallback, false, range);
                }
                else
                {
                    lockedFallback = true;
                    targetPoint = origin + casterForward * range;
                }
            }
            else if (aim.Kind == CombatProjectileAimKind.LockedTargetCenter)
            {
                return Failure(origin, origin, "PROJAUTH021", string.Empty, syntheticProfile, socketFallback, false, range);
            }
            else
            {
                lockedFallback = wantsLockedTarget;
                targetPoint = origin + casterForward * range;
            }

            FixVector3 deltaToTarget = targetPoint - origin;
            FixVector3 direction = NormalizeOr(deltaToTarget, casterForward);
            bool ballistic = definition.Motion.Kind == CombatProjectileMotionKind.BallisticArcToTarget;
            int ballisticLifetime = 0;
            bool feasible = !ballistic || TrySolveBallistic(definition, origin, targetPoint, out ballisticLifetime);
            if (!feasible)
            {
                return Failure(origin, targetPoint, "PROJAUTH021", BallisticLabel(definition, origin, targetPoint, range, false),
                    syntheticProfile, socketFallback, lockedFallback, range);
            }

            int lifetime = ballistic ? ballisticLifetime : ResolveEffectiveLifetime(definition.Motion, origin, targetPoint);
            bool tracking = resolvedLockedTarget && definition.Motion.Kind == CombatProjectileMotionKind.HomingToTarget;
            string diagnostic = syntheticProfile || socketFallback ? "PROJAUTH022" : string.Empty;
            return new ProjectilePreviewSpawnResolution(
                true, origin, direction, targetPoint, lifetime, tracking, tracking ? trackingId : default,
                syntheticProfile, socketFallback, lockedFallback, feasible, range, diagnostic,
                ballistic ? BallisticLabel(definition, origin, targetPoint, range, true) : string.Empty);
        }

        private static ProjectilePreviewSpawnResolution Failure(
            FixVector3 origin,
            FixVector3 target,
            string code,
            string label,
            bool synthetic,
            bool socketFallback,
            bool lockedFallback,
            Fix64 range)
        {
            return new ProjectilePreviewSpawnResolution(false, origin, FixVector3.Zero, target, 0, false, default,
                synthetic, socketFallback, lockedFallback, false, range, code, label);
        }

        private static Fix64 ResolveAimRange(CombatProjectileAimConfig aim, CombatProjectileDefinition definition)
        {
            if (definition.Motion.Kind == CombatProjectileMotionKind.BallisticArcToTarget)
            {
                return CombatProjectileBallistics.CalculateSameHeightMaxRange(
                    definition.Motion.SpeedPerFrame,
                    definition.Motion.GravityPerFrameSquared,
                    definition.Motion.MaxLaunchAngleTangent);
            }
            if (aim.MaxRange > Fix64.Zero)
                return aim.MaxRange;
            return definition.Motion.SpeedPerFrame * Fix64.FromInt(definition.Motion.LifetimeFrames);
        }

        private static bool TrySolveBallistic(
            CombatProjectileDefinition definition,
            FixVector3 origin,
            FixVector3 target,
            out int lifetime)
        {
            return CombatProjectileBallistics.TrySolveLaunchArc(
                origin,
                target,
                definition.Motion.SpeedPerFrame,
                definition.Motion.GravityPerFrameSquared,
                definition.Motion.MaxLaunchAngleTangent,
                definition.Motion.LifetimeFrames,
                out _,
                out _,
                out lifetime);
        }

        private static int ResolveEffectiveLifetime(
            CombatProjectileMotionConfig motion,
            FixVector3 origin,
            FixVector3 target)
        {
            Fix64 distance = (target - origin).LengthSquared().Sqrt();
            if (distance <= Fix64.Zero || motion.SpeedPerFrame <= Fix64.Zero)
                return 1;
            Fix64 frames = distance / motion.SpeedPerFrame;
            long whole = frames.RawValue / Fix64.Scale;
            if (frames.RawValue % Fix64.Scale > 0)
                whole++;
            if (whole <= 0) return 1;
            return whole > int.MaxValue ? int.MaxValue : (int)whole;
        }

        private static string BallisticLabel(
            CombatProjectileDefinition definition,
            FixVector3 origin,
            FixVector3 target,
            Fix64 maximum,
            bool feasible)
        {
            if (definition.Motion.Kind != CombatProjectileMotionKind.BallisticArcToTarget)
                return string.Empty;
            double distanceSquared = (double)(target - origin).LengthSquared().RawValue / Fix64.Scale;
            double distance = Math.Sqrt(distanceSquared);
            return "Ballistic target  range "
                + distance.ToString("0.00", CultureInfo.InvariantCulture)
                + "/"
                + ((double)maximum.RawValue / Fix64.Scale).ToString("0.00", CultureInfo.InvariantCulture)
                + (feasible ? "  feasible" : "  rejected");
        }

        private static FixVector3 TransformLocal(FixVector3 forward, FixVector3 local)
        {
            var right = new FixVector3(forward.Z, Fix64.Zero, Fix64.Zero - forward.X);
            right = NormalizeOr(right, new FixVector3(Fix64.One, Fix64.Zero, Fix64.Zero));
            return right * local.X
                + new FixVector3(Fix64.Zero, local.Y, Fix64.Zero)
                + forward * local.Z;
        }

        private static FixVector3 NormalizeOr(FixVector3 value, FixVector3 fallback)
        {
            return value.TryNormalize(out FixVector3 normalized) ? normalized : fallback;
        }

        private static bool TryParseDefined<T>(string value, out T parsed) where T : struct
        {
            return Enum.TryParse(value, false, out parsed) && Enum.IsDefined(typeof(T), parsed);
        }

        private static Fix64 ToFix(float value) => Fix64.FromRaw((long)Math.Round(value * Fix64.Scale));
        private static FixVector3 ToFix(ProjectileVector3View value) => value == null
            ? FixVector3.Zero
            : new FixVector3(ToFix(value.x), ToFix(value.y), ToFix(value.z));
    }
}
