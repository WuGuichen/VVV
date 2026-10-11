using MxFramework.Combat.Damage;
using MxFramework.Combat.Hit;
using MxFramework.Core.Math;
using MxFramework.CharacterAction;
using MxFramework.CharacterRuntimeSpawn;
using MxFramework.Gameplay;
using UnityEngine;

namespace MxFramework.Game.CharacterTest
{
    internal static class CharacterTestHitReactionResolver
    {
        private const int HitResolveReactionGateFrames = 1;

        public static int CreateHitResolveReactionGate(DamageProfileResolveResult payload)
        {
            if (!payload.Success)
                return 0;

            return payload.IncomingDamage > 0 || payload.ImpactPower > 0
                ? HitResolveReactionGateFrames
                : 0;
        }

        public static FixVector3 CreateHitResolveKnockbackGate(DamageProfileResolveResult payload)
        {
            return FixVector3.Zero;
        }

        public static CharacterTestHitReactionResult Resolve(
            HitResolveResult result,
            DamageProfileResolveResult payload,
            CharacterTestCharacterCombatStateComponent targetCombatState)
        {
            if (result.Kind != HitResolveKind.Damage || result.Damage <= 0)
                return CharacterTestHitReactionResult.None;

            if (result.StaggerFrames <= 0)
                return CharacterTestHitReactionResult.None;
            if (!targetCombatState.IsCohesionBroken)
                return CharacterTestHitReactionResult.None;

            return new CharacterTestHitReactionResult(
                CharacterTestHitReactionKind.Hit,
                CharacterTestCharacterAttributeIds.DefaultCohesionBreakReactionFrames,
                FixVector3.Zero);
        }
    }

    internal enum CharacterTestHitReactionKind
    {
        None = 0,
        Hit = 1,
        Blocked = 2,
        Parried = 3,
        CohesionBreak = 4,
    }

    internal readonly struct CharacterTestHitReactionResult
    {
        public static readonly CharacterTestHitReactionResult None =
            new CharacterTestHitReactionResult(CharacterTestHitReactionKind.None, 0, FixVector3.Zero);

        public CharacterTestHitReactionResult(
            CharacterTestHitReactionKind kind,
            int reactionFrames,
            FixVector3 knockback)
        {
            Kind = kind;
            ReactionFrames = reactionFrames < 0 ? 0 : reactionFrames;
            Knockback = knockback;
        }

        public CharacterTestHitReactionKind Kind { get; }
        public int ReactionFrames { get; }
        public FixVector3 Knockback { get; }
        public bool ShouldReact => Kind != CharacterTestHitReactionKind.None && ReactionFrames > 0;
    }

    public static class CharacterTestImpactResponseResolver
    {
        public const float DefaultMassKg = 80f;
        public const float ReferenceMassKg = 80f;
        public const float ResistanceBase = 5f;
        public const float DistanceAsymptoteMeters = 10f;
        public const float DistanceDriveScale = 276f;

        private const float DirectionEpsilon = 0.0001f;

        public static CharacterTestImpactResponse Resolve(
            int impactPower,
            float hitZoneImpulseScale,
            CharacterTestCharacterEntity source,
            CharacterTestCharacterEntity target,
            CharacterMovementProfileConfig movementProfile = null)
        {
            return Resolve(
                impactPower,
                hitZoneImpulseScale,
                ResolveMassKg(source),
                ResolveMassKg(target),
                ResolveKnockbackDirection(source, target),
                ResolveEffectiveDeceleration(target, movementProfile));
        }

        public static CharacterTestImpactResponse Resolve(
            int impactPower,
            float hitZoneImpulseScale,
            float sourceMassKg,
            float targetMassKg,
            Vector3 knockbackDirection,
            float effectiveDeceleration)
        {
            int safeImpactPower = impactPower < 0 ? 0 : impactPower;
            float safeSourceMass = sourceMassKg > 0f ? sourceMassKg : DefaultMassKg;
            float safeTargetMass = targetMassKg > 0f ? targetMassKg : DefaultMassKg;
            float safeImpulseScale = hitZoneImpulseScale > 0f ? hitZoneImpulseScale : 1f;
            float massTransfer = 2f * safeSourceMass / (safeSourceMass + safeTargetMass);
            float effectiveImpact = safeImpactPower * massTransfer * safeImpulseScale;
            int scatterImpact = RoundAwayFromZero(effectiveImpact);
            float resistance = ResistanceBase * Mathf.Sqrt(safeTargetMass / ReferenceMassKg);
            float drive = Mathf.Max(0f, effectiveImpact - resistance);
            float previewDistanceMeters = DistanceAsymptoteMeters
                * (1f - Mathf.Exp(-drive / DistanceDriveScale));
            float safeDeceleration = effectiveDeceleration > 0f
                ? effectiveDeceleration
                : ResolveDefaultEffectiveDeceleration();
            float knockbackSpeed = previewDistanceMeters <= 0f
                ? 0f
                : Mathf.Sqrt(2f * safeDeceleration * previewDistanceMeters);
            Vector3 direction = NormalizeHorizontal(knockbackDirection);
            Vector3 knockbackVelocity = direction * knockbackSpeed;

            return new CharacterTestImpactResponse(
                safeImpactPower,
                safeSourceMass,
                safeTargetMass,
                safeImpulseScale,
                massTransfer,
                effectiveImpact,
                scatterImpact,
                resistance,
                drive,
                previewDistanceMeters,
                safeDeceleration,
                knockbackSpeed,
                knockbackVelocity);
        }

        public static float ResolveCharacterTestPackageMassKg()
        {
            CharacterImportedPackage package = CharacterTestGeneratedPackageSource.LoadDefaultCharacterPackage();
            float mass = package?.Geometry?.BodyProfile?.MassKg ?? 0f;
            return mass > 0f ? mass : DefaultMassKg;
        }

        private static float ResolveMassKg(CharacterTestCharacterEntity entity)
        {
            if (entity == null)
                return DefaultMassKg;

            float mass = entity.Physics.EffectiveMassKg;
            return mass > 0f ? mass : ResolveCharacterTestPackageMassKg();
        }

        private static float ResolveEffectiveDeceleration(
            CharacterTestCharacterEntity target,
            CharacterMovementProfileConfig movementProfile)
        {
            CharacterMovementProfileConfig profile = movementProfile
                ?? CharacterTestCharacterMovementProfileSource.DefaultProfile;
            float value = Mathf.Max(0f, profile.Deceleration)
                + Mathf.Max(0f, profile.GroundFriction)
                + Mathf.Max(0f, target != null ? target.SurfaceFriction : 0f);
            return value > 0f ? value : ResolveDefaultEffectiveDeceleration();
        }

        private static float ResolveDefaultEffectiveDeceleration()
        {
            CharacterMovementProfileConfig profile = CharacterTestCharacterMovementProfileSource.DefaultProfile;
            return Mathf.Max(0f, profile.Deceleration) + Mathf.Max(0f, profile.GroundFriction);
        }

        private static Vector3 ResolveKnockbackDirection(
            CharacterTestCharacterEntity source,
            CharacterTestCharacterEntity target)
        {
            if (source != null && target != null)
            {
                Vector3 delta = new Vector3(
                    target.Transform.X - source.Transform.X,
                    0f,
                    target.Transform.Z - source.Transform.Z);
                if (delta.sqrMagnitude > DirectionEpsilon)
                    return delta;
            }

            float yawDegrees = source != null ? source.Transform.YawDegrees : 0f;
            float yawRadians = yawDegrees * Mathf.Deg2Rad;
            return new Vector3(Mathf.Sin(yawRadians), 0f, Mathf.Cos(yawRadians));
        }

        private static Vector3 NormalizeHorizontal(Vector3 value)
        {
            value.y = 0f;
            return value.sqrMagnitude <= DirectionEpsilon
                ? Vector3.forward
                : value.normalized;
        }

        private static int RoundAwayFromZero(float value)
        {
            if (value > 0f)
                return Mathf.FloorToInt(value + 0.5f);
            if (value < 0f)
                return Mathf.CeilToInt(value - 0.5f);
            return 0;
        }
    }

    public readonly struct CharacterTestImpactResponse
    {
        public CharacterTestImpactResponse(
            int impactPower,
            float sourceMassKg,
            float targetMassKg,
            float hitZoneImpulseScale,
            float massTransfer,
            float effectiveImpact,
            int scatterImpact,
            float resistance,
            float drive,
            float previewDistanceMeters,
            float effectiveDeceleration,
            float knockbackSpeed,
            Vector3 knockbackVelocity)
        {
            ImpactPower = impactPower < 0 ? 0 : impactPower;
            SourceMassKg = sourceMassKg > 0f ? sourceMassKg : CharacterTestImpactResponseResolver.DefaultMassKg;
            TargetMassKg = targetMassKg > 0f ? targetMassKg : CharacterTestImpactResponseResolver.DefaultMassKg;
            HitZoneImpulseScale = hitZoneImpulseScale > 0f ? hitZoneImpulseScale : 1f;
            MassTransfer = massTransfer < 0f ? 0f : massTransfer;
            EffectiveImpact = effectiveImpact < 0f ? 0f : effectiveImpact;
            ScatterImpact = scatterImpact < 0 ? 0 : scatterImpact;
            Resistance = resistance < 0f ? 0f : resistance;
            Drive = drive < 0f ? 0f : drive;
            PreviewDistanceMeters = previewDistanceMeters < 0f ? 0f : previewDistanceMeters;
            EffectiveDeceleration = effectiveDeceleration < 0f ? 0f : effectiveDeceleration;
            KnockbackSpeed = knockbackSpeed < 0f ? 0f : knockbackSpeed;
            KnockbackVelocity = knockbackVelocity;
        }

        public int ImpactPower { get; }
        public float SourceMassKg { get; }
        public float TargetMassKg { get; }
        public float HitZoneImpulseScale { get; }
        public float MassTransfer { get; }
        public float EffectiveImpact { get; }
        public int ScatterImpact { get; }
        public float Resistance { get; }
        public float Drive { get; }
        public float PreviewDistanceMeters { get; }
        public float EffectiveDeceleration { get; }
        public float KnockbackSpeed { get; }
        public Vector3 KnockbackVelocity { get; }
        public bool HasKnockback => KnockbackSpeed > 0f && KnockbackVelocity.sqrMagnitude > 0.0001f;
    }
}
