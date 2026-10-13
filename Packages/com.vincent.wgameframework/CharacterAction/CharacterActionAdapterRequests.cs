using System;
using MxFramework.Combat.Core;
using MxFramework.Combat.Hit;
using MxFramework.Gameplay;

namespace MxFramework.CharacterAction
{
    public readonly struct CharacterActionMotionRequest : IEquatable<CharacterActionMotionRequest>
    {
        public CharacterActionMotionRequest(
            CharacterActionAdapterDispatchMetadata metadata,
            GameplayEntityId gameplayEntityId,
            CombatBodyId combatBodyId,
            CharacterActionTrackEventKind eventKind,
            CharacterMovementMode movementMode,
            float x,
            float y,
            float z,
            float movementControlScale = 1f,
            float rotationControlScale = 1f,
            CharacterActionMotionVectorSpace motionVectorSpace = CharacterActionMotionVectorSpace.World,
            bool hasMovementControlScale = true,
            bool hasRotationControlScale = true,
            int collisionProfileId = 0,
            CharacterActionCollisionProfileRestorePolicy collisionRestorePolicy =
                CharacterActionCollisionProfileRestorePolicy.OnActionEnd,
            int collisionTransitionFrames = 0)
        {
            EnsureMotionKind(eventKind);
            if (!Enum.IsDefined(typeof(CharacterMovementMode), movementMode))
                throw new ArgumentOutOfRangeException(nameof(movementMode), "Movement mode is not defined.");
            if (!Enum.IsDefined(typeof(CharacterActionMotionVectorSpace), motionVectorSpace))
                throw new ArgumentOutOfRangeException(nameof(motionVectorSpace), "Motion vector space is not defined.");
            if (!Enum.IsDefined(typeof(CharacterActionCollisionProfileRestorePolicy), collisionRestorePolicy))
                throw new ArgumentOutOfRangeException(nameof(collisionRestorePolicy), "Collision profile restore policy is not defined.");

            Metadata = metadata;
            GameplayEntityId = gameplayEntityId;
            CombatBodyId = combatBodyId;
            EventKind = eventKind;
            MovementMode = movementMode;
            X = x;
            Y = y;
            Z = z;
            MovementControlScale = CharacterActionControlScaleJsonProjection.ValidateScale(movementControlScale);
            RotationControlScale = CharacterActionControlScaleJsonProjection.ValidateScale(rotationControlScale);
            MotionVectorSpace = motionVectorSpace;
            HasMovementControlScale = hasMovementControlScale;
            HasRotationControlScale = hasRotationControlScale;
            CollisionProfileId = collisionProfileId;
            CollisionRestorePolicy = collisionRestorePolicy;
            CollisionTransitionFrames = collisionTransitionFrames < 0 ? 0 : collisionTransitionFrames;
        }

        public CharacterActionAdapterDispatchMetadata Metadata { get; }
        public GameplayEntityId GameplayEntityId { get; }
        public CombatBodyId CombatBodyId { get; }
        public CharacterActionTrackEventKind EventKind { get; }
        public CharacterMovementMode MovementMode { get; }
        public float X { get; }
        public float Y { get; }
        public float Z { get; }
        public float MovementControlScale { get; }
        public float RotationControlScale { get; }
        public CharacterActionMotionVectorSpace MotionVectorSpace { get; }
        public bool HasMovementControlScale { get; }
        public bool HasRotationControlScale { get; }
        public int CollisionProfileId { get; }
        public CharacterActionCollisionProfileRestorePolicy CollisionRestorePolicy { get; }
        public int CollisionTransitionFrames { get; }

        public bool Equals(CharacterActionMotionRequest other)
        {
            return Metadata.Equals(other.Metadata)
                && GameplayEntityId.Equals(other.GameplayEntityId)
                && CombatBodyId.Equals(other.CombatBodyId)
                && EventKind == other.EventKind
                && MovementMode == other.MovementMode
                && X.Equals(other.X)
                && Y.Equals(other.Y)
                && Z.Equals(other.Z)
                && MovementControlScale.Equals(other.MovementControlScale)
                && RotationControlScale.Equals(other.RotationControlScale)
                && MotionVectorSpace == other.MotionVectorSpace
                && HasMovementControlScale == other.HasMovementControlScale
                && HasRotationControlScale == other.HasRotationControlScale
                && (CollisionProfileId == other.CollisionProfileId)
                && CollisionRestorePolicy == other.CollisionRestorePolicy
                && CollisionTransitionFrames == other.CollisionTransitionFrames;
        }

        public override bool Equals(object obj)
        {
            return obj is CharacterActionMotionRequest other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = Metadata.GetHashCode();
                hash = (hash * 397) ^ GameplayEntityId.GetHashCode();
                hash = (hash * 397) ^ CombatBodyId.GetHashCode();
                hash = (hash * 397) ^ (int)EventKind;
                hash = (hash * 397) ^ (int)MovementMode;
                hash = (hash * 397) ^ X.GetHashCode();
                hash = (hash * 397) ^ Y.GetHashCode();
                hash = (hash * 397) ^ Z.GetHashCode();
                hash = (hash * 397) ^ MovementControlScale.GetHashCode();
                hash = (hash * 397) ^ RotationControlScale.GetHashCode();
                hash = (hash * 397) ^ (int)MotionVectorSpace;
                hash = (hash * 397) ^ HasMovementControlScale.GetHashCode();
                hash = (hash * 397) ^ HasRotationControlScale.GetHashCode();
                hash = (hash * 397) ^ CollisionProfileId;
                hash = (hash * 397) ^ (int)CollisionRestorePolicy;
                hash = (hash * 397) ^ CollisionTransitionFrames;
                return hash;
            }
        }

        private static void EnsureMotionKind(CharacterActionTrackEventKind eventKind)
        {
            if (eventKind != CharacterActionTrackEventKind.SetMovementMode
                && eventKind != CharacterActionTrackEventKind.ApplyImpulse
                && eventKind != CharacterActionTrackEventKind.LockMovement
                && eventKind != CharacterActionTrackEventKind.SetControlScale
                && eventKind != CharacterActionTrackEventKind.SetCollisionProfile
                && eventKind != CharacterActionTrackEventKind.SetVerticalVelocity
                && eventKind != CharacterActionTrackEventKind.IgnoreGravity
                && eventKind != CharacterActionTrackEventKind.SampleRootMotionY)
            {
                throw new ArgumentOutOfRangeException(nameof(eventKind), "Event kind is not a MotionTrack request.");
            }
        }

        private static float Clamp01(float value)
        {
            if (value <= 0f)
                return 0f;
            return value >= 1f ? 1f : value;
        }
    }

    public readonly struct CharacterActionRootMotionRequest : IEquatable<CharacterActionRootMotionRequest>
    {
        public CharacterActionRootMotionRequest(
            CharacterActionAdapterDispatchMetadata metadata,
            float x,
            float y,
            float z,
            CharacterActionRootMotionSpace space,
            CharacterActionRootMotionObstaclePolicy obstaclePolicy)
        {
            if (!Enum.IsDefined(typeof(CharacterActionRootMotionSpace), space))
                throw new ArgumentOutOfRangeException(nameof(space), "Root motion space is not defined.");
            if (!Enum.IsDefined(typeof(CharacterActionRootMotionObstaclePolicy), obstaclePolicy))
                throw new ArgumentOutOfRangeException(nameof(obstaclePolicy), "Root motion obstacle policy is not defined.");

            Metadata = metadata;
            X = x;
            Y = y;
            Z = z;
            Space = space;
            ObstaclePolicy = obstaclePolicy;
        }

        public CharacterActionAdapterDispatchMetadata Metadata { get; }
        public float X { get; }
        public float Y { get; }
        public float Z { get; }
        public CharacterActionRootMotionSpace Space { get; }
        public CharacterActionRootMotionObstaclePolicy ObstaclePolicy { get; }

        public bool Equals(CharacterActionRootMotionRequest other)
        {
            return Metadata.Equals(other.Metadata)
                && X.Equals(other.X)
                && Y.Equals(other.Y)
                && Z.Equals(other.Z)
                && Space == other.Space
                && ObstaclePolicy == other.ObstaclePolicy;
        }

        public override bool Equals(object obj)
        {
            return obj is CharacterActionRootMotionRequest other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = Metadata.GetHashCode();
                hash = (hash * 397) ^ X.GetHashCode();
                hash = (hash * 397) ^ Y.GetHashCode();
                hash = (hash * 397) ^ Z.GetHashCode();
                hash = (hash * 397) ^ (int)Space;
                hash = (hash * 397) ^ (int)ObstaclePolicy;
                return hash;
            }
        }
    }

    public readonly struct CharacterActionDebugRequest : IEquatable<CharacterActionDebugRequest>
    {
        public CharacterActionDebugRequest(
            CharacterActionAdapterDispatchMetadata metadata,
            CharacterActionTrackEventKind eventKind,
            string markerId)
        {
            if (eventKind != CharacterActionTrackEventKind.EmitDebugMarker)
                throw new ArgumentOutOfRangeException(nameof(eventKind), "Event kind is not a DebugTrack request.");

            Metadata = metadata;
            EventKind = eventKind;
            MarkerId = markerId ?? string.Empty;
        }

        public CharacterActionAdapterDispatchMetadata Metadata { get; }
        public CharacterActionTrackEventKind EventKind { get; }
        public string MarkerId { get; }

        public bool Equals(CharacterActionDebugRequest other)
        {
            return Metadata.Equals(other.Metadata)
                && EventKind == other.EventKind
                && string.Equals(MarkerId, other.MarkerId, StringComparison.Ordinal);
        }

        public override bool Equals(object obj)
        {
            return obj is CharacterActionDebugRequest other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = Metadata.GetHashCode();
                hash = (hash * 397) ^ (int)EventKind;
                hash = (hash * 397) ^ (MarkerId == null ? 0 : StringComparer.Ordinal.GetHashCode(MarkerId));
                return hash;
            }
        }
    }

    public readonly struct CharacterActionCombatRequest : IEquatable<CharacterActionCombatRequest>
    {
        public CharacterActionCombatRequest(
            CharacterActionAdapterDispatchMetadata metadata,
            CombatEntityId combatEntityId,
            CharacterActionTrackEventKind eventKind,
            int combatActionId,
            int traceProfileId,
            int equipmentSlotId = 0,
            CharacterActionDamagePayload damagePayload = default,
            CombatHitEventConfig hitEvent = default,
            CharacterActionHitVolumeConfig hitVolume = default)
        {
            EnsureCombatKind(eventKind);
            Metadata = metadata;
            CombatEntityId = combatEntityId;
            EventKind = eventKind;
            CombatActionId = combatActionId;
            TraceProfileId = traceProfileId;
            EquipmentSlotId = equipmentSlotId;
            DamagePayload = damagePayload;
            HitEvent = hitEvent.WithDefaults(Metadata.StableEventId);
            HitVolume = hitVolume;
            TargetStateFlags = HitTargetStateFlags.None;
        }

        public CharacterActionCombatRequest(
            CharacterActionAdapterDispatchMetadata metadata,
            CombatEntityId combatEntityId,
            CharacterActionTrackEventKind eventKind,
            HitTargetStateFlags targetStateFlags)
            : this(metadata, combatEntityId, eventKind, 0, 0)
        {
            TargetStateFlags = targetStateFlags;
            HitTargetStateContributionStore.ValidateFlags(targetStateFlags);
        }

        public CharacterActionAdapterDispatchMetadata Metadata { get; }
        public CombatEntityId CombatEntityId { get; }
        public CharacterActionTrackEventKind EventKind { get; }
        public int CombatActionId { get; }
        public int TraceProfileId { get; }
        public int EquipmentSlotId { get; }
        public CharacterActionDamagePayload DamagePayload { get; }
        public CombatHitEventConfig HitEvent { get; }
        public CharacterActionHitVolumeConfig HitVolume { get; }
        public HitTargetStateFlags TargetStateFlags { get; }

        public bool Equals(CharacterActionCombatRequest other)
        {
            return Metadata.Equals(other.Metadata)
                && CombatEntityId.Equals(other.CombatEntityId)
                && EventKind == other.EventKind
                && (CombatActionId == other.CombatActionId)
                && (TraceProfileId == other.TraceProfileId)
                && (EquipmentSlotId == other.EquipmentSlotId)
                && DamagePayload.Equals(other.DamagePayload)
                && HitEvent.Equals(other.HitEvent)
                && HitVolume.Equals(other.HitVolume)
                && TargetStateFlags == other.TargetStateFlags;
        }

        public override bool Equals(object obj)
        {
            return obj is CharacterActionCombatRequest other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = Metadata.GetHashCode();
                hash = (hash * 397) ^ CombatEntityId.GetHashCode();
                hash = (hash * 397) ^ (int)EventKind;
                hash = (hash * 397) ^ CombatActionId;
                hash = (hash * 397) ^ TraceProfileId;
                hash = (hash * 397) ^ EquipmentSlotId;
                hash = (hash * 397) ^ DamagePayload.GetHashCode();
                hash = (hash * 397) ^ HitEvent.GetHashCode();
                hash = (hash * 397) ^ HitVolume.GetHashCode();
                hash = (hash * 397) ^ (int)TargetStateFlags;
                return hash;
            }
        }

        private static void EnsureCombatKind(CharacterActionTrackEventKind eventKind)
        {
            if (eventKind != CharacterActionTrackEventKind.StartCombatAction
                && eventKind != CharacterActionTrackEventKind.HitTraceWindow
                && eventKind != CharacterActionTrackEventKind.SetHitTargetState)
            {
                throw new ArgumentOutOfRangeException(nameof(eventKind), "Event kind is not a CombatTrack request.");
            }
        }
    }

    public readonly struct CharacterActionGameplayRequest : IEquatable<CharacterActionGameplayRequest>
    {
        public CharacterActionGameplayRequest(
            CharacterActionAdapterDispatchMetadata metadata,
            GameplayEntityId gameplayEntityId,
            CharacterActionTrackEventKind eventKind,
            int requestId,
            int abilityStableId,
            int targetPolicyId = 0,
            int effectTimelineId = 0,
            CharacterActionDamagePayload damagePayload = default,
            CombatHitEventConfig hitEvent = default)
        {
            EnsureGameplayKind(eventKind);
            Metadata = metadata;
            GameplayEntityId = gameplayEntityId;
            EventKind = eventKind;
            RequestId = requestId;
            AbilityStableId = abilityStableId;
            TargetPolicyId = targetPolicyId;
            EffectTimelineId = effectTimelineId;
            DamagePayload = damagePayload;
            HitEvent = hitEvent.WithDefaults(Metadata.StableEventId);
        }

        public CharacterActionAdapterDispatchMetadata Metadata { get; }
        public GameplayEntityId GameplayEntityId { get; }
        public CharacterActionTrackEventKind EventKind { get; }
        public int RequestId { get; }
        public int AbilityStableId { get; }
        public int TargetPolicyId { get; }
        public int EffectTimelineId { get; }
        public CharacterActionDamagePayload DamagePayload { get; }
        public CombatHitEventConfig HitEvent { get; }

        public bool Equals(CharacterActionGameplayRequest other)
        {
            return Metadata.Equals(other.Metadata)
                && GameplayEntityId.Equals(other.GameplayEntityId)
                && EventKind == other.EventKind
                && RequestId == other.RequestId
                && (AbilityStableId == other.AbilityStableId)
                && (TargetPolicyId == other.TargetPolicyId)
                && EffectTimelineId == other.EffectTimelineId
                && DamagePayload.Equals(other.DamagePayload)
                && HitEvent.Equals(other.HitEvent);
        }

        public override bool Equals(object obj)
        {
            return obj is CharacterActionGameplayRequest other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = Metadata.GetHashCode();
                hash = (hash * 397) ^ GameplayEntityId.GetHashCode();
                hash = (hash * 397) ^ (int)EventKind;
                hash = (hash * 397) ^ RequestId.GetHashCode();
                hash = (hash * 397) ^ AbilityStableId;
                hash = (hash * 397) ^ TargetPolicyId;
                hash = (hash * 397) ^ EffectTimelineId.GetHashCode();
                hash = (hash * 397) ^ DamagePayload.GetHashCode();
                hash = (hash * 397) ^ HitEvent.GetHashCode();
                return hash;
            }
        }

        private static void EnsureGameplayKind(CharacterActionTrackEventKind eventKind)
        {
            if (eventKind != CharacterActionTrackEventKind.SendGameplayRequest
                && eventKind != CharacterActionTrackEventKind.CastAbility
                && eventKind != CharacterActionTrackEventKind.ApplyGameplayEffect
                && eventKind != CharacterActionTrackEventKind.StartEffectTimeline)
            {
                throw new ArgumentOutOfRangeException(nameof(eventKind), "Event kind is not a GameplayTrack request.");
            }
        }
    }

    public readonly struct CharacterActionPressureOnlyReactionRequest : IEquatable<CharacterActionPressureOnlyReactionRequest>
    {
        public CharacterActionPressureOnlyReactionRequest(
            CharacterReactionContext context,
            CharacterActionId requestedActionId = default)
        {
            Context = context;
            RequestedActionId = requestedActionId;
        }

        public CharacterReactionContext Context { get; }
        public CharacterActionId RequestedActionId { get; }

        public bool Equals(CharacterActionPressureOnlyReactionRequest other)
        {
            return Context.Equals(other.Context)
                && RequestedActionId == other.RequestedActionId;
        }

        public override bool Equals(object obj)
        {
            return obj is CharacterActionPressureOnlyReactionRequest other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return (Context.GetHashCode() * 397) ^ RequestedActionId.GetHashCode();
            }
        }
    }
}
