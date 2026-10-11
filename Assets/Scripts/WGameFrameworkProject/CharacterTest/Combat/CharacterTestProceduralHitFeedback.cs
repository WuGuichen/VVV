using System;
using System.Collections.Generic;
using System.Globalization;
using MxFramework.CharacterApplication;
using MxFramework.CharacterRuntimeSpawn;
using MxFramework.Combat.Damage;
using MxFramework.Combat.Hit;
using MxFramework.Core.Math;
using MxFramework.Runtime;

namespace MxFramework.Game.CharacterTest
{
    public sealed class CharacterTestProceduralHitFeedbackOptions
    {
        public static readonly CharacterTestProceduralHitFeedbackOptions Default =
            new CharacterTestProceduralHitFeedbackOptions();

        public CharacterTestProceduralHitFeedbackOptions(
            bool enabled = true,
            bool installDefaultSecondaryChain = false)
        {
            Enabled = enabled;
            InstallDefaultSecondaryChain = installDefaultSecondaryChain;
        }

        public bool Enabled { get; }
        public bool InstallDefaultSecondaryChain { get; }
    }

    internal sealed class CharacterTestProceduralHitFeedbackModule : RuntimeModule
    {
        private readonly Func<IReadOnlyList<DamageResolvedEvent>> _eventsProvider;
        private readonly Func<IReadOnlyList<CharacterTestCharacterEntity>> _entitiesProvider;
        private readonly CharacterTestCharacterRuntimeModule _characterRuntimeModule;
        private readonly Func<CharacterTestCharacterEntity, CharacterImportedPackage> _packageProvider;
        private readonly Dictionary<int, CharacterTestProceduralHitFeedbackImpulse> _bestImpulsesByTarget =
            new Dictionary<int, CharacterTestProceduralHitFeedbackImpulse>();
        private CharacterTestProceduralHitFeedbackResult _lastResult =
            CharacterTestProceduralHitFeedbackResult.Empty;
        private bool _enabled;

        public CharacterTestProceduralHitFeedbackModule(
            Func<IReadOnlyList<DamageResolvedEvent>> eventsProvider,
            Func<IReadOnlyList<CharacterTestCharacterEntity>> entitiesProvider,
            CharacterTestCharacterRuntimeModule characterRuntimeModule,
            CharacterTestProceduralHitFeedbackOptions options = null,
            Func<CharacterTestCharacterEntity, CharacterImportedPackage> packageProvider = null)
            : base("CharacterTest.ProceduralHitFeedback", RuntimeTickStage.PostSimulation, priority: 160)
        {
            _eventsProvider = eventsProvider ?? throw new ArgumentNullException(nameof(eventsProvider));
            _entitiesProvider = entitiesProvider ?? throw new ArgumentNullException(nameof(entitiesProvider));
            _characterRuntimeModule = characterRuntimeModule ?? throw new ArgumentNullException(nameof(characterRuntimeModule));
            _packageProvider = packageProvider;
            _enabled = (options ?? CharacterTestProceduralHitFeedbackOptions.Default).Enabled;
        }

        public bool Enabled => _enabled;
        public CharacterTestProceduralHitFeedbackResult LastResult => _lastResult;

        public void SetEnabled(bool enabled)
        {
            _enabled = enabled;
        }

        /// <remarks>NoAlloc on the steady-state path after registration/initialization; invalid-call and exception paths may allocate diagnostics.</remarks>
        public override void Tick(RuntimeTickContext context)
        {
            if (!_enabled)
                return;

            IReadOnlyList<DamageResolvedEvent> events = _eventsProvider();
            if (events == null || events.Count == 0)
                return;

            IReadOnlyList<CharacterTestCharacterEntity> entities = _entitiesProvider();
            if (entities == null || entities.Count == 0)
                return;

            _bestImpulsesByTarget.Clear();
            for (int i = 0; i < events.Count; i++)
            {
                if (!CharacterTestProceduralHitFeedbackMapper.TryCreateImpulse(
                        events[i],
                        entities,
                        _packageProvider,
                        out CharacterTestProceduralHitFeedbackImpulse impulse))
                {
                    continue;
                }

                CharacterTestProceduralHitFeedbackMapper.TryRecordBestImpulse(
                    _bestImpulsesByTarget,
                    impulse);
            }

            RuntimeFrame startFrame = new RuntimeFrame(context.FrameIndex).Next();
            foreach (KeyValuePair<int, CharacterTestProceduralHitFeedbackImpulse> pair in _bestImpulsesByTarget)
            {
                bool accepted = _characterRuntimeModule.TryApplyAuthorityPoseImpulse(
                    pair.Key,
                    pair.Value.Request,
                    startFrame,
                    out string issue);
                _lastResult = new CharacterTestProceduralHitFeedbackResult(
                    new RuntimeFrame(context.FrameIndex),
                    pair.Key,
                    accepted,
                    pair.Value.Request.RegionId,
                    pair.Value.Request.MatchSource,
                    string.IsNullOrWhiteSpace(issue) ? DescribeIssue(pair.Value.Request.FallbackIssue, pair.Value.Request.FallbackIssueArgumentId) : issue,
                    CharacterAuthorityPoseSnapshot.Empty());
            }

            _bestImpulsesByTarget.Clear();
        }
        private static string DescribeIssue(CharacterAuthorityPoseIssue issue, int argumentId)
        {
            switch (issue)
            {
                case CharacterAuthorityPoseIssue.None: return string.Empty;
                case CharacterAuthorityPoseIssue.MissingBodyPartOrHitZone: return "damage usage has no body part or hit zone";
                case CharacterAuthorityPoseIssue.UnmappedHitZone: return "hit zone was not mapped to a body region: " + argumentId;
                default: return issue.ToString();
            }
        }
    }

    internal readonly struct CharacterTestProceduralHitFeedbackImpulse
    {
        public CharacterTestProceduralHitFeedbackImpulse(
            int targetEntityId,
            CharacterAuthorityPoseImpulseRequest request,
            int impactScore)
        {
            TargetEntityId = targetEntityId < 0 ? 0 : targetEntityId;
            Request = request;
            ImpactScore = impactScore < 0 ? 0 : impactScore;
        }

        public int TargetEntityId { get; }
        public CharacterAuthorityPoseImpulseRequest Request { get; }
        public int ImpactScore { get; }
    }

    public readonly struct CharacterTestProceduralHitFeedbackResult
    {
        public CharacterTestProceduralHitFeedbackResult(
            RuntimeFrame frame,
            int targetEntityId,
            bool accepted,
            int regionId,
            CharacterAuthorityPoseMatchSource matchSource,
            string fallbackIssue,
            CharacterAuthorityPoseSnapshot snapshot)
        {
            Frame = frame;
            TargetEntityId = targetEntityId < 0 ? 0 : targetEntityId;
            Accepted = accepted;
            RegionId = regionId;
            MatchSource = matchSource;
            FallbackIssue = fallbackIssue ?? string.Empty;
            Snapshot = snapshot ?? CharacterAuthorityPoseSnapshot.Empty();
        }

        public RuntimeFrame Frame { get; }
        public int TargetEntityId { get; }
        public bool Accepted { get; }
        public int RegionId { get; }
        public CharacterAuthorityPoseMatchSource MatchSource { get; }
        public string FallbackIssue { get; }
        public CharacterAuthorityPoseSnapshot Snapshot { get; }
        public bool HasResult => TargetEntityId > 0 || RegionId > 0;

        public static CharacterTestProceduralHitFeedbackResult Empty =>
            new CharacterTestProceduralHitFeedbackResult(
                RuntimeFrame.Zero,
                0,
                accepted: false,
                0,
                CharacterAuthorityPoseMatchSource.None,
                string.Empty,
                CharacterAuthorityPoseSnapshot.Empty());
    }

    internal readonly struct CharacterTestProceduralHitFeedbackRegionResolution
    {
        public CharacterTestProceduralHitFeedbackRegionResolution(
            int regionId,
            CharacterAuthorityPoseMatchSource matchSource,
            CharacterAuthorityPoseIssue fallbackIssue,
            Fix64 strengthScale,
            int fallbackIssueArgumentId = 0)
        {
            RegionId = CharacterAuthorityPoseRegionIds.NormalizeBindingRegion(regionId);
            MatchSource = matchSource;
            FallbackIssue = fallbackIssue;
            FallbackIssueArgumentId = fallbackIssueArgumentId;
            StrengthScale = strengthScale > Fix64.Zero ? strengthScale : Fix64.One;
        }

        public int RegionId { get; }
        public CharacterAuthorityPoseMatchSource MatchSource { get; }
        public CharacterAuthorityPoseIssue FallbackIssue { get; }
        public int FallbackIssueArgumentId { get; }
        public Fix64 StrengthScale { get; }
    }

    internal static class CharacterTestProceduralHitFeedbackMapper
    {
        private static readonly Fix64 StrengthScale = Fix64.FromRatio(18, 100);
        private static readonly Fix64 MinStrength = Fix64.FromRatio(12, 10);
        private static readonly Fix64 MaxStrength = Fix64.FromInt(5);

        public static bool TryCreateImpulse(
            DamageResolvedEvent evt,
            IReadOnlyList<CharacterTestCharacterEntity> entities,
            out CharacterTestProceduralHitFeedbackImpulse impulse)
        {
            return TryCreateImpulse(evt, entities, null, out impulse);
        }

        public static bool TryCreateImpulse(
            DamageResolvedEvent evt,
            IReadOnlyList<CharacterTestCharacterEntity> entities,
            Func<CharacterTestCharacterEntity, CharacterImportedPackage> packageProvider,
            out CharacterTestProceduralHitFeedbackImpulse impulse)
        {
            impulse = default;
            if (entities == null
                || evt.FinalDamage <= 0
                || evt.HitResult.Kind != HitResolveKind.Damage)
            {
                return false;
            }

            int targetEntityId = CharacterTestCombatEntityLookup.ResolveCharacterEntityId(evt.HitResult.TargetId);
            if (targetEntityId <= 0)
                return false;

            CharacterTestCharacterEntity target = FindEntity(entities, targetEntityId);
            if (target == null)
                return false;

            CharacterTestCharacterEntity source = FindEntity(
                entities,
                CharacterTestCombatEntityLookup.ResolveCharacterEntityId(evt.HitResult.AttackerId));
            int impactScore = ResolveImpactScore(evt);
            if (impactScore <= 0)
                return false;

            FixVector3 direction = ResolveDirection(evt, source, target);
            CharacterTestProceduralHitFeedbackRegionResolution region =
                ResolveBodyRegion(evt.ProfileResult, packageProvider?.Invoke(target));
            Fix64 strength = Fix64.Clamp(
                    Fix64.FromInt(impactScore) * StrengthScale,
                    MinStrength,
                    MaxStrength)
                * region.StrengthScale;
            int durationFrames = ClampInt(6 + impactScore / 4, 6, 13);
            var request = new CharacterAuthorityPoseImpulseRequest(
                region.RegionId,
                direction,
                strength,
                durationFrames,
                CreateCorrelationId(evt, targetEntityId),
                region.MatchSource,
                region.FallbackIssue, region.FallbackIssueArgumentId);
            impulse = new CharacterTestProceduralHitFeedbackImpulse(
                targetEntityId,
                request,
                impactScore);
            return request.HasEffect;
        }

        public static bool TryRecordBestImpulse(
            Dictionary<int, CharacterTestProceduralHitFeedbackImpulse> impulsesByTarget,
            CharacterTestProceduralHitFeedbackImpulse impulse)
        {
            if (impulsesByTarget == null || impulse.TargetEntityId <= 0)
                return false;

            if (impulsesByTarget.TryGetValue(
                    impulse.TargetEntityId,
                    out CharacterTestProceduralHitFeedbackImpulse existing)
                && existing.ImpactScore >= impulse.ImpactScore)
            {
                return false;
            }

            impulsesByTarget[impulse.TargetEntityId] = impulse;
            return true;
        }

        private static CharacterTestCharacterEntity FindEntity(
            IReadOnlyList<CharacterTestCharacterEntity> entities,
            int entityId)
        {
            if (entities == null || entityId <= 0)
                return null;

            for (int i = 0; i < entities.Count; i++)
            {
                CharacterTestCharacterEntity entity = entities[i];
                if (entity != null && entity.Id.Value == entityId)
                    return entity;
            }

            return null;
        }

        private static int ResolveImpactScore(DamageResolvedEvent evt)
        {
            int impact = Math.Max(
                evt.ProfileResult.ImpactPower,
                Math.Max(evt.ProfileResult.ImpactForce, evt.FinalDamage));
            return impact < 0 ? 0 : impact;
        }

        private static FixVector3 ResolveDirection(
            DamageResolvedEvent evt,
            CharacterTestCharacterEntity source,
            CharacterTestCharacterEntity target)
        {
            if (source != null && target != null)
            {
                FixVector3 delta = ToFixPosition(target.Transform) - ToFixPosition(source.Transform);
                delta = new FixVector3(delta.X, Fix64.Zero, delta.Z);
                if (delta.TryNormalize(out FixVector3 normalized))
                    return normalized;
            }

            FixVector3 hitNormal = -evt.HitResult.HitNormalWorld;
            if (hitNormal.TryNormalize(out FixVector3 normalDirection))
                return normalDirection;

            return ResolveForward(target);
        }

        private static CharacterTestProceduralHitFeedbackRegionResolution ResolveBodyRegion(
            DamageProfileResolveResult profile,
            CharacterImportedPackage package)
        {
            if (!(profile.Usage.BodyPartId == 0))
            {
                int bodyPartId = profile.Usage.BodyPartId;
                if (package == null)
                {
                    return new CharacterTestProceduralHitFeedbackRegionResolution(
                        bodyPartId,
                        CharacterAuthorityPoseMatchSource.BodyPart,
                        CharacterAuthorityPoseIssue.None,
                        Fix64.One);
                }

                if (TryFindBodyPart(package, bodyPartId, out CharacterBodyPartConfig part))
                {
                    return new CharacterTestProceduralHitFeedbackRegionResolution(
                        bodyPartId,
                        CharacterAuthorityPoseMatchSource.BodyPart,
                        CharacterAuthorityPoseIssue.None,
                        ResolveImpulseScale(part));
                }
            }

            if (!(profile.Usage.HitZoneId == 0)
                && TryResolveHitZone(
                    package,
                    profile.Usage.HitZoneId,
                    out int regionId,
                    out Fix64 impulseScale))
            {
                return new CharacterTestProceduralHitFeedbackRegionResolution(
                    regionId,
                    CharacterAuthorityPoseMatchSource.HitZone,
                    CharacterAuthorityPoseIssue.None,
                    impulseScale);
            }

            CharacterAuthorityPoseIssue issue = profile.Usage.HitZoneId == 0
                ? CharacterAuthorityPoseIssue.MissingBodyPartOrHitZone
                : CharacterAuthorityPoseIssue.UnmappedHitZone;
            return new CharacterTestProceduralHitFeedbackRegionResolution(
                CharacterAuthorityPoseRegionIds.Default,
                CharacterAuthorityPoseMatchSource.Default,
                issue,
                Fix64.One, profile.Usage.HitZoneId);
        }

        private static bool TryResolveHitZone(
            CharacterImportedPackage package,
            int hitZoneId,
            out int regionId,
            out Fix64 impulseScale)
        {
            regionId = 0;
            impulseScale = Fix64.One;
            if (package == null || hitZoneId == 0)
                return false;

            CharacterBodyProfileConfig bodyProfile = ResolveDefaultBodyProfile(package);
            if (bodyProfile != null)
            {
                BodyPartHitZoneResolveResult result = BodyPartHitZoneResolver.Resolve(
                    bodyProfile,
                    package.Configs?.BodyParts ?? Array.Empty<CharacterBodyPartConfig>(),
                    hitZoneId);
                if (result.IsSuccess && !(result.PartId == 0))
                {
                    regionId = result.PartId;
                    impulseScale = ToFix64(result.ImpulseScale > 0f ? result.ImpulseScale : 1f);
                    return true;
                }
            }

            CharacterHitZoneRuntimeBinding[] hitZoneBindings =
                package.Geometry?.HitZoneBindings ?? Array.Empty<CharacterHitZoneRuntimeBinding>();
            for (int i = 0; i < hitZoneBindings.Length; i++)
            {
                CharacterHitZoneRuntimeBinding binding = hitZoneBindings[i];
                if (binding != null
                    && (binding.HitZoneId == hitZoneId)
                    && !(binding.PartId == 0))
                {
                    regionId = binding.PartId;
                    impulseScale = ResolvePartImpulseScale(package, regionId);
                    return true;
                }
            }

            CharacterBodyColliderRuntimeBinding[] colliders =
                package.Geometry?.BodyColliders ?? Array.Empty<CharacterBodyColliderRuntimeBinding>();
            CharacterBodyColliderRuntimeBinding selected = null;
            for (int i = 0; i < colliders.Length; i++)
            {
                CharacterBodyColliderRuntimeBinding collider = colliders[i];
                if (collider == null
                    || !(collider.HitZoneId == hitZoneId)
                    || (collider.PartId == 0))
                {
                    continue;
                }

                if (selected == null || collider.Priority > selected.Priority)
                    selected = collider;
            }

            if (selected == null)
                return false;

            regionId = selected.PartId;
            impulseScale = ResolvePartImpulseScale(package, regionId);
            return true;
        }

        private static CharacterBodyProfileConfig ResolveDefaultBodyProfile(CharacterImportedPackage package)
        {
            CharacterImportedConfigSet configs = package?.Configs;
            CharacterConfig character = configs?.Characters != null && configs.Characters.Length > 0
                ? configs.Characters[0]
                : null;
            return character == null
                ? (configs?.BodyProfiles != null && configs.BodyProfiles.Length > 0 ? configs.BodyProfiles[0] : null)
                : configs?.FindBodyProfile(character.BodyProfileId);
        }

        private static bool TryFindBodyPart(
            CharacterImportedPackage package,
            int partId,
            out CharacterBodyPartConfig part)
        {
            part = null;
            CharacterBodyPartConfig[] bodyParts =
                package?.Configs?.BodyParts ?? Array.Empty<CharacterBodyPartConfig>();
            for (int i = 0; i < bodyParts.Length; i++)
            {
                CharacterBodyPartConfig candidate = bodyParts[i];
                if (candidate != null
                    && (candidate.PartId == partId))
                {
                    part = candidate;
                    return true;
                }
            }

            return false;
        }

        private static Fix64 ResolvePartImpulseScale(CharacterImportedPackage package, int partId)
        {
            return TryFindBodyPart(package, partId, out CharacterBodyPartConfig part)
                ? ResolveImpulseScale(part)
                : Fix64.One;
        }

        private static Fix64 ResolveImpulseScale(CharacterBodyPartConfig part)
        {
            return ToFix64(part != null && part.ImpulseScale > 0f ? part.ImpulseScale : 1f);
        }

        private static FixVector3 ResolveForward(CharacterTestCharacterEntity entity)
        {
            if (entity == null)
                return new FixVector3(Fix64.Zero, Fix64.Zero, Fix64.One);

            double yaw = entity.Transform.YawDegrees * Math.PI / 180.0d;
            return new FixVector3(
                ToFix64(Math.Sin(yaw)),
                Fix64.Zero,
                ToFix64(Math.Cos(yaw)));
        }

        private static FixVector3 ToFixPosition(CharacterTestCharacterTransformComponent transform)
        {
            return new FixVector3(
                ToFix64(transform.X),
                ToFix64(transform.Y),
                ToFix64(transform.Z));
        }

        private static Fix64 ToFix64(double value)
        {
            return Fix64.FromRaw((long)Math.Round(value * Fix64.Scale));
        }

        private static int ClampInt(int value, int min, int max)
        {
            if (value < min)
                return min;
            return value > max ? max : value;
        }

        private static RuntimeTraceIdentity CreateCorrelationId(DamageResolvedEvent evt, int targetEntityId)
        {
            return new RuntimeTraceIdentity(556, targetEntityId, evt.FinalDamage, evt.HitResult.TraceId);
        }
    }
}
