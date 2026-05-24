using System;
using MxFramework.Combat.Animation;
using MxFramework.Gameplay;
using MxFramework.Runtime;

namespace MxFramework.CharacterAction
{
    public enum CharacterReactionContextSourceKind
    {
        Unknown = 0,
        PostureBreak = 1,
        GuardBreak = 2,
        ArmorBreak = 3,
        PressureBandChanged = 4,
        Death = 5,
        Lifecycle = 6,
        Hit = 7
    }

    public enum CharacterReactionContextCompleteness
    {
        None = 0,
        SourceOnly = 1,
        PressureOnly = 2,
        Full = 3
    }

    public enum CharacterHitDirection
    {
        Unknown = 0,
        Front = 1,
        Back = 2,
        Left = 3,
        Right = 4,
        Up = 5,
        Down = 6
    }

    public readonly struct CharacterReactionContext : IEquatable<CharacterReactionContext>
    {
        public CharacterReactionContext(
            CharacterReactionContextSourceKind sourceKind,
            CharacterReactionContextCompleteness completeness,
            RuntimeFrame frame,
            GameplayEntityId entityId,
            PressureBand previousPressureBand,
            PressureBand currentPressureBand,
            int previousPressure,
            int currentPressure,
            int maxPressure,
            int delta,
            int sourceId,
            string reason,
            string traceId,
            bool isDeath,
            string lifecycleState,
            string bodyPartId,
            string hitZoneId,
            string damageTypeId,
            CharacterHitDirection hitDirection,
            string reactionGroupId,
            int impactForce = 0,
            bool isAirborne = false,
            string currentActionId = "",
            CharacterActionPhaseKind currentCharacterPhase = CharacterActionPhaseKind.None,
            CombatActionPhase currentCombatPhase = CombatActionPhase.None,
            bool currentActionCommitted = false,
            bool currentActionInterruptible = true)
        {
            if (!Enum.IsDefined(typeof(CharacterReactionContextSourceKind), sourceKind))
                throw new ArgumentOutOfRangeException(nameof(sourceKind), "Reaction context source kind is not defined.");
            if (!Enum.IsDefined(typeof(CharacterReactionContextCompleteness), completeness))
                throw new ArgumentOutOfRangeException(nameof(completeness), "Reaction context completeness is not defined.");
            if (!Enum.IsDefined(typeof(PressureBand), previousPressureBand))
                throw new ArgumentOutOfRangeException(nameof(previousPressureBand), "Previous pressure band is not defined.");
            if (!Enum.IsDefined(typeof(PressureBand), currentPressureBand))
                throw new ArgumentOutOfRangeException(nameof(currentPressureBand), "Current pressure band is not defined.");
            if (!Enum.IsDefined(typeof(CharacterHitDirection), hitDirection))
                throw new ArgumentOutOfRangeException(nameof(hitDirection), "Hit direction is not defined.");
            if (!Enum.IsDefined(typeof(CharacterActionPhaseKind), currentCharacterPhase))
                throw new ArgumentOutOfRangeException(nameof(currentCharacterPhase), "Current character phase is not defined.");
            if (!Enum.IsDefined(typeof(CombatActionPhase), currentCombatPhase))
                throw new ArgumentOutOfRangeException(nameof(currentCombatPhase), "Current combat phase is not defined.");

            SourceKind = sourceKind;
            Completeness = completeness;
            Frame = frame;
            EntityId = entityId;
            PreviousPressureBand = previousPressureBand;
            CurrentPressureBand = currentPressureBand;
            PreviousPressure = previousPressure;
            CurrentPressure = currentPressure;
            MaxPressure = maxPressure;
            Delta = delta;
            SourceId = sourceId;
            Reason = reason ?? string.Empty;
            TraceId = traceId ?? string.Empty;
            IsDeath = isDeath;
            LifecycleState = lifecycleState ?? string.Empty;
            BodyPartId = bodyPartId ?? string.Empty;
            HitZoneId = hitZoneId ?? string.Empty;
            DamageTypeId = damageTypeId ?? string.Empty;
            HitDirection = hitDirection;
            ReactionGroupId = reactionGroupId ?? string.Empty;
            ImpactForce = impactForce;
            IsAirborne = isAirborne;
            CurrentActionId = currentActionId ?? string.Empty;
            CurrentCharacterPhase = currentCharacterPhase;
            CurrentCombatPhase = currentCombatPhase;
            CurrentActionCommitted = currentActionCommitted;
            CurrentActionInterruptible = currentActionInterruptible;
        }

        public CharacterReactionContextSourceKind SourceKind { get; }
        public CharacterReactionContextCompleteness Completeness { get; }
        public RuntimeFrame Frame { get; }
        public GameplayEntityId EntityId { get; }
        public PressureBand PreviousPressureBand { get; }
        public PressureBand CurrentPressureBand { get; }
        public int PreviousPressure { get; }
        public int CurrentPressure { get; }
        public int MaxPressure { get; }
        public int Delta { get; }
        public int SourceId { get; }
        public string Reason { get; }
        public string TraceId { get; }
        public bool IsDeath { get; }
        public string LifecycleState { get; }
        public string BodyPartId { get; }
        public string HitZoneId { get; }
        public string DamageTypeId { get; }
        public CharacterHitDirection HitDirection { get; }
        public string ReactionGroupId { get; }
        public int ImpactForce { get; }
        public bool IsAirborne { get; }
        public string CurrentActionId { get; }
        public CharacterActionPhaseKind CurrentCharacterPhase { get; }
        public CombatActionPhase CurrentCombatPhase { get; }
        public bool CurrentActionCommitted { get; }
        public bool CurrentActionInterruptible { get; }
        public bool IsPostureBreak => SourceKind == CharacterReactionContextSourceKind.PostureBreak;
        public bool IsGuardBreak => SourceKind == CharacterReactionContextSourceKind.GuardBreak;
        public bool IsArmorBreak => SourceKind == CharacterReactionContextSourceKind.ArmorBreak;
        public bool HasPressure => Completeness == CharacterReactionContextCompleteness.PressureOnly
            || Completeness == CharacterReactionContextCompleteness.Full;
        public bool HasFullHitContext => Completeness == CharacterReactionContextCompleteness.Full;

        public bool Equals(CharacterReactionContext other)
        {
            return SourceKind == other.SourceKind
                && Completeness == other.Completeness
                && Frame.Equals(other.Frame)
                && EntityId.Equals(other.EntityId)
                && PreviousPressureBand == other.PreviousPressureBand
                && CurrentPressureBand == other.CurrentPressureBand
                && PreviousPressure == other.PreviousPressure
                && CurrentPressure == other.CurrentPressure
                && MaxPressure == other.MaxPressure
                && Delta == other.Delta
                && SourceId == other.SourceId
                && string.Equals(Reason, other.Reason, StringComparison.Ordinal)
                && string.Equals(TraceId, other.TraceId, StringComparison.Ordinal)
                && IsDeath == other.IsDeath
                && string.Equals(LifecycleState, other.LifecycleState, StringComparison.Ordinal)
                && string.Equals(BodyPartId, other.BodyPartId, StringComparison.Ordinal)
                && string.Equals(HitZoneId, other.HitZoneId, StringComparison.Ordinal)
                && string.Equals(DamageTypeId, other.DamageTypeId, StringComparison.Ordinal)
                && HitDirection == other.HitDirection
                && string.Equals(ReactionGroupId, other.ReactionGroupId, StringComparison.Ordinal)
                && ImpactForce == other.ImpactForce
                && IsAirborne == other.IsAirborne
                && string.Equals(CurrentActionId, other.CurrentActionId, StringComparison.Ordinal)
                && CurrentCharacterPhase == other.CurrentCharacterPhase
                && CurrentCombatPhase == other.CurrentCombatPhase
                && CurrentActionCommitted == other.CurrentActionCommitted
                && CurrentActionInterruptible == other.CurrentActionInterruptible;
        }

        public override bool Equals(object obj)
        {
            return obj is CharacterReactionContext other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = (int)SourceKind;
                hash = (hash * 397) ^ (int)Completeness;
                hash = (hash * 397) ^ Frame.GetHashCode();
                hash = (hash * 397) ^ EntityId.GetHashCode();
                hash = (hash * 397) ^ (int)PreviousPressureBand;
                hash = (hash * 397) ^ (int)CurrentPressureBand;
                hash = (hash * 397) ^ PreviousPressure;
                hash = (hash * 397) ^ CurrentPressure;
                hash = (hash * 397) ^ MaxPressure;
                hash = (hash * 397) ^ Delta;
                hash = (hash * 397) ^ SourceId;
                hash = (hash * 397) ^ (Reason == null ? 0 : Reason.GetHashCode());
                hash = (hash * 397) ^ (TraceId == null ? 0 : TraceId.GetHashCode());
                hash = (hash * 397) ^ IsDeath.GetHashCode();
                hash = (hash * 397) ^ (LifecycleState == null ? 0 : LifecycleState.GetHashCode());
                hash = (hash * 397) ^ (BodyPartId == null ? 0 : BodyPartId.GetHashCode());
                hash = (hash * 397) ^ (HitZoneId == null ? 0 : HitZoneId.GetHashCode());
                hash = (hash * 397) ^ (DamageTypeId == null ? 0 : DamageTypeId.GetHashCode());
                hash = (hash * 397) ^ (int)HitDirection;
                hash = (hash * 397) ^ (ReactionGroupId == null ? 0 : ReactionGroupId.GetHashCode());
                hash = (hash * 397) ^ ImpactForce;
                hash = (hash * 397) ^ IsAirborne.GetHashCode();
                hash = (hash * 397) ^ (CurrentActionId == null ? 0 : CurrentActionId.GetHashCode());
                hash = (hash * 397) ^ (int)CurrentCharacterPhase;
                hash = (hash * 397) ^ (int)CurrentCombatPhase;
                hash = (hash * 397) ^ CurrentActionCommitted.GetHashCode();
                hash = (hash * 397) ^ CurrentActionInterruptible.GetHashCode();
                return hash;
            }
        }
    }
}
