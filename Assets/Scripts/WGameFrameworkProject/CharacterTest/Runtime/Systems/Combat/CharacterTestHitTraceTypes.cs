using System;
using System.Collections.Generic;
using System.Globalization;
using MxFramework.Animation;
using MxFramework.CharacterApplication;
using MxFramework.CharacterAction;
using MxFramework.CharacterRuntimeSpawn;
using MxFramework.Combat.Animation;
using MxFramework.Combat.Core;
using MxFramework.Combat.Damage;
using MxFramework.Combat.GameplayBridge;
using MxFramework.Combat.Hit;
using MxFramework.Combat.Physics;
using MxFramework.Core.Math;
using MxFramework.Gameplay;
using MxFramework.Resources;
using MxFramework.Runtime;
using UnityEngine;

namespace MxFramework.Game.CharacterTest
{
    public readonly struct CharacterTestAttackTraceDebugFrame
    {
        public CharacterTestAttackTraceDebugFrame(
            long runtimeFrame,
            int entityId,
            int hitWindowId,
            int localFrame,
            int equipmentSlotId,
            int traceProfileId,
            int traceId,
            Vector3 rootPrev,
            Vector3 tipPrev,
            Vector3 rootNow,
            Vector3 tipNow,
            float radius,
            float proximalExtension = 0f,
            float proximalRadius = 0f)
        {
            RuntimeFrame = runtimeFrame;
            EntityId = entityId;
            HitWindowId = hitWindowId;
            LocalFrame = localFrame;
            EquipmentSlotId = equipmentSlotId;
            TraceProfileId = traceProfileId;
            TraceId = traceId;
            RootPrev = rootPrev;
            TipPrev = tipPrev;
            RootNow = rootNow;
            TipNow = tipNow;
            Radius = radius;
            ProximalExtension = proximalExtension < 0f ? 0f : proximalExtension;
            ProximalRadius = proximalRadius < 0f ? 0f : proximalRadius;
        }

        public long RuntimeFrame { get; }
        public int EntityId { get; }
        public int HitWindowId { get; }
        public int LocalFrame { get; }
        public int EquipmentSlotId { get; }
        public int TraceProfileId { get; }
        public int TraceId { get; }
        public Vector3 RootPrev { get; }
        public Vector3 TipPrev { get; }
        public Vector3 RootNow { get; }
        public Vector3 TipNow { get; }
        public float Radius { get; }
        public float ProximalExtension { get; }
        public float ProximalRadius { get; }
        public bool HasProximalRange => ProximalExtension > 0f && ProximalRadius > 0f;

        internal static CharacterTestAttackTraceDebugFrame FromWeaponTrace(
            long runtimeFrame,
            CharacterTestActiveHitTrace activeTrace,
            int traceProfileId,
            int localFrame,
            WeaponTraceFrame weaponTrace)
        {
            return new CharacterTestAttackTraceDebugFrame(
                runtimeFrame,
                activeTrace.EntityId,
                activeTrace.HitWindowId,
                localFrame,
                activeTrace.EquipmentSlotId,
                traceProfileId,
                weaponTrace.TraceId,
                ToVector3(weaponTrace.RootPrev),
                ToVector3(weaponTrace.TipPrev),
                ToVector3(weaponTrace.RootNow),
                ToVector3(weaponTrace.TipNow),
                ToFloat(weaponTrace.Radius),
                ToFloat(weaponTrace.ProximalExtension),
                ToFloat(weaponTrace.ProximalRadius));
        }

        private static Vector3 ToVector3(FixVector3 value)
        {
            return new Vector3(ToFloat(value.X), ToFloat(value.Y), ToFloat(value.Z));
        }

        private static float ToFloat(Fix64 value)
        {
            return (float)value.RawValue / Fix64.Scale;
        }
    }

    public readonly struct CharacterTestHitPointDebugFrame
    {
        public static bool ShouldRecordKind(HitResolveKind kind)
        {
            return kind == HitResolveKind.Damage || kind == HitResolveKind.Blocked;
        }

        public CharacterTestHitPointDebugFrame(
            long runtimeFrame,
            int sourceEntityId,
            int targetEntityId,
            HitResolveKind kind,
            int traceId,
            FixVector3 hitPointWorld,
            FixVector3 hitNormalWorld,
            int hitZoneId,
            int bodyPartId)
        {
            RuntimeFrame = runtimeFrame;
            SourceEntityId = sourceEntityId < 0 ? 0 : sourceEntityId;
            TargetEntityId = targetEntityId < 0 ? 0 : targetEntityId;
            Kind = kind;
            TraceId = traceId < 0 ? 0 : traceId;
            HitPointWorld = hitPointWorld;
            HitNormalWorld = hitNormalWorld;
            HitZoneId = hitZoneId;
            BodyPartId = bodyPartId;
        }

        public long RuntimeFrame { get; }
        public int SourceEntityId { get; }
        public int TargetEntityId { get; }
        public HitResolveKind Kind { get; }
        public int TraceId { get; }
        public FixVector3 HitPointWorld { get; }
        public FixVector3 HitNormalWorld { get; }
        public int HitZoneId { get; }
        public int BodyPartId { get; }
    }

    internal readonly struct CharacterTestActiveHitTrace
    {
        public CharacterTestActiveHitTrace(
            int entityId,
            CharacterActionId actionId,
            long sourceInstanceId,
            int sourceActionInstanceId,
            int hitWindowId,
            int equipmentSlotId,
            int traceProfileId,
            CharacterActionDamagePayload damagePayload,
            CombatHitEventConfig hitEvent,
            CharacterActionHitVolumeConfig hitVolume,
            int stableEventId,
            int startFrame)
        {
            EntityId = entityId;
            ActionId = actionId;
            SourceInstanceId = sourceInstanceId;
            SourceActionInstanceId = sourceActionInstanceId;
            HitWindowId = hitWindowId <= 0 ? 0 : hitWindowId;
            EquipmentSlotId = equipmentSlotId;
            TraceProfileId = traceProfileId;
            DamagePayload = damagePayload;
            HitEvent = hitEvent.WithDefaults(stableEventId);
            HitVolume = hitVolume;
            StableEventId = stableEventId;
            StartFrame = startFrame < 0 ? 0 : startFrame;
        }

        public int EntityId { get; }
        public CharacterActionId ActionId { get; }
        public long SourceInstanceId { get; }
        public int SourceActionInstanceId { get; }
        public int HitWindowId { get; }
        public int EquipmentSlotId { get; }
        public int TraceProfileId { get; }
        public CharacterActionDamagePayload DamagePayload { get; }
        public CombatHitEventConfig HitEvent { get; }
        public CharacterActionHitVolumeConfig HitVolume { get; }
        public int StableEventId { get; }
        public int StartFrame { get; }

        public string DescribeSelector()
        {
            if (HitVolume.HasVolume)
            {
                return HitVolume.SourceKind == CharacterActionHitVolumeSourceKind.CasterBody
                    ? "casterBody"
                    : "hitVolume";
            }

            if ((TraceProfileId > 0))
                return TraceProfileId.ToString(CultureInfo.InvariantCulture);
            return (EquipmentSlotId <= 0) ? string.Empty : "slot:" + EquipmentSlotId;
        }
    }

    internal readonly struct CharacterTestTracePayloadKey : IEquatable<CharacterTestTracePayloadKey>
    {
        public CharacterTestTracePayloadKey(int actionInstanceId, int traceId)
        {
            ActionInstanceId = actionInstanceId < 0 ? 0 : actionInstanceId;
            TraceId = traceId < 0 ? 0 : traceId;
        }

        public int ActionInstanceId { get; }
        public int TraceId { get; }

        public bool Equals(CharacterTestTracePayloadKey other)
        {
            return ActionInstanceId == other.ActionInstanceId && TraceId == other.TraceId;
        }

        public override bool Equals(object obj)
        {
            return obj is CharacterTestTracePayloadKey other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return (ActionInstanceId * 397) ^ TraceId;
            }
        }
    }

    internal readonly struct CharacterTestTracePayload
    {
        public CharacterTestTracePayload(
            CharacterTestActiveHitTrace trace,
            CharacterTestWeaponTraceBinding binding,
            string clipKey,
            int localFrame)
        {
            Trace = trace;
            Binding = binding;
            ClipKey = clipKey ?? string.Empty;
            LocalFrame = localFrame < 0 ? 0 : localFrame;
        }

        public CharacterTestActiveHitTrace Trace { get; }
        public CharacterTestWeaponTraceBinding Binding { get; }
        public string ClipKey { get; }
        public int LocalFrame { get; }
    }

    internal readonly struct CharacterTestResolvedHitKey : IEquatable<CharacterTestResolvedHitKey>
    {
        public CharacterTestResolvedHitKey(HitCandidate candidate)
            : this(candidate.AttackerId, candidate.TargetId, candidate.ActionInstanceId, candidate.TraceId, candidate.Frame)
        {
        }

        public CharacterTestResolvedHitKey(HitResolveResult result)
            : this(result.AttackerId, result.TargetId, result.ActionInstanceId, result.TraceId, result.Frame)
        {
        }

        private CharacterTestResolvedHitKey(
            CombatEntityId attackerId,
            CombatEntityId targetId,
            int actionInstanceId,
            int traceId,
            CombatFrame frame)
        {
            AttackerId = attackerId;
            TargetId = targetId;
            ActionInstanceId = actionInstanceId < 0 ? 0 : actionInstanceId;
            TraceId = traceId < 0 ? 0 : traceId;
            Frame = frame;
        }

        public CombatEntityId AttackerId { get; }
        public CombatEntityId TargetId { get; }
        public int ActionInstanceId { get; }
        public int TraceId { get; }
        public CombatFrame Frame { get; }

        public bool Equals(CharacterTestResolvedHitKey other)
        {
            return AttackerId.Equals(other.AttackerId)
                && TargetId.Equals(other.TargetId)
                && ActionInstanceId == other.ActionInstanceId
                && TraceId == other.TraceId
                && Frame.Equals(other.Frame);
        }

        public override bool Equals(object obj)
        {
            return obj is CharacterTestResolvedHitKey other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = AttackerId.Value;
                hash = (hash * 397) ^ TargetId.Value;
                hash = (hash * 397) ^ ActionInstanceId;
                hash = (hash * 397) ^ TraceId;
                hash = (hash * 397) ^ Frame.Value;
                return hash;
            }
        }
    }

    internal readonly struct CharacterTestWeaponHitAudioMetadata
    {
        public CharacterTestWeaponHitAudioMetadata(
            CombatQueryResult physicsHit,
            DamageProfileResolveResult damage,
            CharacterTestHitZoneResolution hitZone,
            CombatSurfaceMaterial surfaceMaterial)
        {
            PhysicsHit = physicsHit;
            Damage = damage;
            HitZone = hitZone;
            SurfaceMaterial = surfaceMaterial;
        }

        public CombatQueryResult PhysicsHit { get; }
        public DamageProfileResolveResult Damage { get; }
        public CharacterTestHitZoneResolution HitZone { get; }
        public CombatSurfaceMaterial SurfaceMaterial { get; }

        public bool TryCreateEvent(
            RuntimeFrame frame,
            HitResolveResult result,
            int finalDamage,
            float effectiveImpact,
            out CharacterTestWeaponHitAudioEvent audioEvent)
        {
            audioEvent = default;
            if (!Damage.Success)
                return false;
            if (Damage.Usage.SourceKind != DamageProfileSourceKind.WeaponTrace)
                return false;
            if (result.Kind != HitResolveKind.Damage && result.Kind != HitResolveKind.Blocked)
                return false;

            DamageProfileUsage usage = Damage.Usage;
            audioEvent = new CharacterTestWeaponHitAudioEvent(
                frame,
                usage.SourceEntityId > 0
                    ? usage.SourceEntityId
                    : CharacterTestCombatEntityLookup.ResolveCharacterEntityId(result.AttackerId),
                usage.TargetEntityId > 0
                    ? usage.TargetEntityId
                    : CharacterTestCombatEntityLookup.ResolveCharacterEntityId(result.TargetId),
                result.ActionId,
                result.ActionInstanceId,
                result.TraceId,
                result.Kind,
                usage.WeaponId,
                usage.EquipmentSlotId,
                Damage.DamageTypeId,
                HitZone.HitZoneId,
                HitZone.BodyPartId,
                result.HitPointWorld,
                result.HitNormalWorld,
                PhysicsHit.TargetBodyId,
                PhysicsHit.TargetColliderId,
                PhysicsHit.TargetShapeKind,
                SurfaceMaterial,
                finalDamage,
                Damage.ImpactPower,
                effectiveImpact);
            return true;
        }
    }

    internal readonly struct CharacterTestWeaponHitAudioEvent
    {
        public CharacterTestWeaponHitAudioEvent(
            RuntimeFrame frame,
            int sourceEntityId,
            int targetEntityId,
            int actionId,
            int actionInstanceId,
            int traceId,
            HitResolveKind resultKind,
            int weaponId,
            int equipmentSlotId,
            int damageTypeId,
            int hitZoneId,
            int bodyPartId,
            FixVector3 hitPointWorld,
            FixVector3 hitNormalWorld,
            CombatBodyId targetBodyId,
            CombatColliderId targetColliderId,
            CombatPhysicsShapeKind targetShapeKind,
            CombatSurfaceMaterial surfaceMaterial,
            int finalDamage,
            int impactPower,
            float effectiveImpact)
        {
            Frame = frame;
            SourceEntityId = sourceEntityId < 0 ? 0 : sourceEntityId;
            TargetEntityId = targetEntityId < 0 ? 0 : targetEntityId;
            ActionId = actionId < 0 ? 0 : actionId;
            ActionInstanceId = actionInstanceId < 0 ? 0 : actionInstanceId;
            TraceId = traceId < 0 ? 0 : traceId;
            ResultKind = resultKind;
            WeaponId = weaponId;
            EquipmentSlotId = equipmentSlotId;
            DamageTypeId = damageTypeId;
            HitZoneId = hitZoneId;
            BodyPartId = bodyPartId;
            HitPointWorld = hitPointWorld;
            HitNormalWorld = hitNormalWorld;
            TargetBodyId = targetBodyId;
            TargetColliderId = targetColliderId;
            TargetShapeKind = targetShapeKind;
            SurfaceMaterial = surfaceMaterial;
            FinalDamage = finalDamage < 0 ? 0 : finalDamage;
            ImpactPower = impactPower < 0 ? 0 : impactPower;
            EffectiveImpact = effectiveImpact < 0f ? 0f : effectiveImpact;
        }

        public RuntimeFrame Frame { get; }
        public int SourceEntityId { get; }
        public int TargetEntityId { get; }
        public int ActionId { get; }
        public int ActionInstanceId { get; }
        public int TraceId { get; }
        public HitResolveKind ResultKind { get; }
        public int WeaponId { get; }
        public int EquipmentSlotId { get; }
        public int DamageTypeId { get; }
        public int HitZoneId { get; }
        public int BodyPartId { get; }
        public FixVector3 HitPointWorld { get; }
        public FixVector3 HitNormalWorld { get; }
        public CombatBodyId TargetBodyId { get; }
        public CombatColliderId TargetColliderId { get; }
        public CombatPhysicsShapeKind TargetShapeKind { get; }
        public CombatSurfaceMaterial SurfaceMaterial { get; }
        public int FinalDamage { get; }
        public int ImpactPower { get; }
        public float EffectiveImpact { get; }

        public int Intensity
        {
            get
            {
                int damageIntensity = FinalDamage * 100;
                int impactIntensity = ImpactPower * 100;
                int effectiveImpactIntensity = (int)(EffectiveImpact * 100f);
                int strongerPhysicalIntensity = impactIntensity > effectiveImpactIntensity
                    ? impactIntensity
                    : effectiveImpactIntensity;
                return damageIntensity > strongerPhysicalIntensity ? damageIntensity : strongerPhysicalIntensity;
            }
        }
    }

    internal static class CharacterTestReactionGroupIds
    {
        public const int HeightHigh = 1;
        public const int HeightMid = 2;
        public const int HeightLow = 3;
        public const int SpecialLaunch = 4;

        public static int ResolveHeightGroup(int hitZoneId, int bodyPartId, int sourceReactionGroupId)
        {
            if (sourceReactionGroupId >= HeightHigh && sourceReactionGroupId <= HeightLow)
                return sourceReactionGroupId;
            int flags = CharacterTestAuthoredContentIds.GetLegacyAnatomicalFlags(hitZoneId)
                | CharacterTestAuthoredContentIds.GetLegacyAnatomicalFlags(bodyPartId);
            if ((flags & 1) != 0) return HeightHigh;
            if ((flags & 2) != 0) return HeightLow;
            if ((flags & 4) != 0) return HeightMid;
            if ((flags & 8) != 0) return HeightHigh;
            if ((flags & 16) != 0) return HeightLow;
            return HeightMid;
        }
    }

    internal static class CharacterTestHitZoneDefaults
    {
        public const int HitZoneId = CharacterTestAuthoredContentIds.D400_Id34;
        public const int BodyPartId = CharacterTestAuthoredContentIds.D400_Id34;
    }

    internal readonly struct CharacterTestHitZoneResolution
    {
        public static readonly CharacterTestHitZoneResolution Default =
            new CharacterTestHitZoneResolution(
                CharacterTestHitZoneDefaults.HitZoneId,
                CharacterTestHitZoneDefaults.BodyPartId,
                CharacterTestReactionGroupIds.HeightMid,
                1f,
                1f,
                1f,
                1f);

        public CharacterTestHitZoneResolution(
            int hitZoneId,
            int bodyPartId,
            int reactionGroupId,
            float damageMultiplier,
            float staggerScale,
            float impulseScale,
            float impactPowerScale)
        {
            HitZoneId = hitZoneId;
            BodyPartId = bodyPartId;
            ReactionGroupId = reactionGroupId <= 0
                ? CharacterTestReactionGroupIds.ResolveHeightGroup(hitZoneId, bodyPartId, 0)
                : reactionGroupId;
            DamageMultiplier = damageMultiplier <= 0f ? 1f : damageMultiplier;
            StaggerScale = staggerScale <= 0f ? 1f : staggerScale;
            ImpulseScale = impulseScale <= 0f ? 1f : impulseScale;
            ImpactPowerScale = impactPowerScale <= 0f ? 1f : impactPowerScale;
        }

        public int HitZoneId { get; }
        public int BodyPartId { get; }
        public int ReactionGroupId { get; }
        public float DamageMultiplier { get; }
        public float StaggerScale { get; }
        public float ImpulseScale { get; }
        public float ImpactPowerScale { get; }
    }

    internal readonly struct CharacterTestWeaponTraceBinding
    {
        public CharacterTestWeaponTraceBinding(
            int traceProfileId,
            float length,
            float radius,
            CharacterWeaponTraceRuntimeBinding traceBinding = null,
            int equipmentSlotId = 0,
            int weaponId = 0,
            float proximalExtension = 0f,
            float proximalRadius = 0f)
        {
            TraceProfileId = traceProfileId;
            EquipmentSlotId = equipmentSlotId;
            WeaponId = weaponId;
            Length = length < 0f ? 0f : length;
            Radius = radius < 0f ? 0f : radius;
            ProximalExtension = proximalExtension < 0f ? 0f : proximalExtension;
            ProximalRadius = proximalRadius < 0f ? 0f : proximalRadius;
            TraceBinding = traceBinding;
        }

        public int TraceProfileId { get; }
        public int EquipmentSlotId { get; }
        public int WeaponId { get; }
        public float Length { get; }
        public float Radius { get; }
        public float ProximalExtension { get; }
        public float ProximalRadius { get; }
        public CharacterWeaponTraceRuntimeBinding TraceBinding { get; }
        public CombatPhysicsLayerMask TargetMask =>
            CombatPhysicsLayerMask.FromLayer(CharacterTestCharacterPhysicsRuntime.CharacterHurtboxLayer);
        public Fix64 LengthFix => ToFix64(Length);
        public Fix64 RadiusFix => ToFix64(Radius);
        public Fix64 ProximalExtensionFix => ToFix64(ProximalExtension);
        public Fix64 ProximalRadiusFix => ToFix64(ProximalRadius);

        private static Fix64 ToFix64(float value)
        {
            return Fix64.FromRaw((long)Math.Round(value * Fix64.Scale));
        }
    }

    internal readonly struct CharacterTestPreparedTraceKey : IEquatable<CharacterTestPreparedTraceKey>
    {
        public CharacterTestPreparedTraceKey(
            int actionInstanceId,
            int localFrame,
            int equipmentSlotId,
            int traceProfileId)
        {
            ActionInstanceId = actionInstanceId;
            LocalFrame = localFrame;
            EquipmentSlotId = equipmentSlotId;
            TraceProfileId = traceProfileId;
        }

        public int ActionInstanceId { get; }
        public int LocalFrame { get; }
        public int EquipmentSlotId { get; }
        public int TraceProfileId { get; }

        public bool Equals(CharacterTestPreparedTraceKey other)
        {
            return ActionInstanceId == other.ActionInstanceId
                && LocalFrame == other.LocalFrame
                && (EquipmentSlotId == other.EquipmentSlotId)
                && (TraceProfileId == other.TraceProfileId);
        }

        public override bool Equals(object obj)
        {
            return obj is CharacterTestPreparedTraceKey other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = ActionInstanceId;
                hash = (hash * 397) ^ LocalFrame;
                hash = (hash * 397) ^ EquipmentSlotId.GetHashCode();
                hash = (hash * 397) ^ TraceProfileId.GetHashCode();
                return hash;
            }
        }
    }

    internal readonly struct CharacterTestPreparedTrace
    {
        public CharacterTestPreparedTrace(
            string clipKey,
            MxAnimationBakedWeaponTraceFrame frame,
            CharacterTestWeaponTraceBinding binding)
        {
            ClipKey = clipKey ?? string.Empty;
            Frame = frame;
            Binding = binding;
        }

        public string ClipKey { get; }
        public MxAnimationBakedWeaponTraceFrame Frame { get; }
        public CharacterTestWeaponTraceBinding Binding { get; }
    }
}


