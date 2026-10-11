using System;
using MxFramework.Combat.Animation;
using MxFramework.Gameplay;
using MxFramework.Runtime;

namespace MxFramework.CharacterAction
{
    public enum CharacterReactionContextSourceKind
    {
        Unknown = 0,
        CohesionBreak = 1,
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
            PressureBand currentScatterPressureBand,
            int previousPressure,
            int currentScatterPressure,
            int maxScatterPressure,
            int delta,
            int sourceId,
            string reason,
            RuntimeTraceIdentity traceId,
            bool isDeath,
            string lifecycleState,
            int bodyPartId,
            int hitZoneId,
            int damageTypeId,
            CharacterHitDirection hitDirection,
            int reactionGroupId,
            int impactForce = 0,
            bool isAirborne = false,
            CharacterActionId currentActionId = default,
            CharacterActionPhaseKind currentCharacterPhase = CharacterActionPhaseKind.None,
            CombatActionPhase currentCombatPhase = CombatActionPhase.None,
            bool currentActionCommitted = false,
            bool currentActionInterruptible = true,
            GameplayScatterPressureReason pressureReason = default,
            int traceNumericId = 0)
        {
            if (!CharacterReactionEnumGuards.IsDefined(sourceKind))
                throw new ArgumentOutOfRangeException(nameof(sourceKind), "Reaction context source kind is not defined.");
            if (!CharacterReactionEnumGuards.IsDefined(completeness))
                throw new ArgumentOutOfRangeException(nameof(completeness), "Reaction context completeness is not defined.");
            if (!CharacterReactionEnumGuards.IsDefined(previousPressureBand))
                throw new ArgumentOutOfRangeException(nameof(previousPressureBand), "Previous pressure band is not defined.");
            if (!CharacterReactionEnumGuards.IsDefined(currentScatterPressureBand))
                throw new ArgumentOutOfRangeException(nameof(currentScatterPressureBand), "Current pressure band is not defined.");
            if (!CharacterReactionEnumGuards.IsDefined(hitDirection))
                throw new ArgumentOutOfRangeException(nameof(hitDirection), "Hit direction is not defined.");
            if (!CharacterReactionEnumGuards.IsDefined(currentCharacterPhase))
                throw new ArgumentOutOfRangeException(nameof(currentCharacterPhase), "Current character phase is not defined.");
            if (!CharacterReactionEnumGuards.IsDefined(currentCombatPhase))
                throw new ArgumentOutOfRangeException(nameof(currentCombatPhase), "Current combat phase is not defined.");

            SourceKind = sourceKind;
            Completeness = completeness;
            Frame = frame;
            EntityId = entityId;
            PreviousPressureBand = previousPressureBand;
            CurrentScatterPressureBand = currentScatterPressureBand;
            PreviousPressure = previousPressure;
            CurrentScatterPressure = currentScatterPressure;
            MaxScatterPressure = maxScatterPressure;
            Delta = delta;
            SourceId = sourceId;
            _reason = reason ?? string.Empty;
            PressureReason = pressureReason;
            TraceId = traceId;
            IsDeath = isDeath;
            LifecycleState = lifecycleState ?? string.Empty;
            BodyPartId = bodyPartId;
            HitZoneId = hitZoneId;
            DamageTypeId = damageTypeId;
            HitDirection = hitDirection;
            ReactionGroupId = reactionGroupId;
            ImpactForce = impactForce;
            IsAirborne = isAirborne;
            CurrentActionId = currentActionId;
            CurrentCharacterPhase = currentCharacterPhase;
            CurrentCombatPhase = currentCombatPhase;
            CurrentActionCommitted = currentActionCommitted;
            CurrentActionInterruptible = currentActionInterruptible;
            TraceNumericId = traceNumericId;
        }

        public CharacterReactionContextSourceKind SourceKind { get; }
        public CharacterReactionContextCompleteness Completeness { get; }
        public RuntimeFrame Frame { get; }
        public GameplayEntityId EntityId { get; }
        public PressureBand PreviousPressureBand { get; }
        public PressureBand CurrentScatterPressureBand { get; }
        public int PreviousPressure { get; }
        public int CurrentScatterPressure { get; }
        public int MaxScatterPressure { get; }
        public int Delta { get; }
        public int SourceId { get; }
        private readonly string _reason;
        public GameplayScatterPressureReason PressureReason { get; }
        public int ReasonCode => PressureReason.Code;
        public int ReasonArgumentId => PressureReason.ArgumentId;
        public string Reason => PressureReason.IsNone ? _reason ?? string.Empty : PressureReason.ToString();
        public RuntimeTraceIdentity TraceId { get; }
        public bool IsDeath { get; }
        public string LifecycleState { get; }
        public int BodyPartId { get; }
        public int HitZoneId { get; }
        public int DamageTypeId { get; }
        public CharacterHitDirection HitDirection { get; }
        public int ReactionGroupId { get; }
        public int ImpactForce { get; }
        public bool IsAirborne { get; }
        public CharacterActionId CurrentActionId { get; }
        public CharacterActionPhaseKind CurrentCharacterPhase { get; }
        public CombatActionPhase CurrentCombatPhase { get; }
        public bool CurrentActionCommitted { get; }
        public bool CurrentActionInterruptible { get; }
        public int TraceNumericId { get; }
        public bool IsCohesionBreak => SourceKind == CharacterReactionContextSourceKind.CohesionBreak;
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
                && CurrentScatterPressureBand == other.CurrentScatterPressureBand
                && PreviousPressure == other.PreviousPressure
                && CurrentScatterPressure == other.CurrentScatterPressure
                && MaxScatterPressure == other.MaxScatterPressure
                && Delta == other.Delta
                && SourceId == other.SourceId
                && PressureReason == other.PressureReason
                && string.Equals(_reason, other._reason, StringComparison.Ordinal)
                && TraceId.Equals(other.TraceId)
                && IsDeath == other.IsDeath
                && string.Equals(LifecycleState, other.LifecycleState, StringComparison.Ordinal)
                && BodyPartId == other.BodyPartId
                && HitZoneId == other.HitZoneId
                && DamageTypeId == other.DamageTypeId
                && HitDirection == other.HitDirection
                && ReactionGroupId == other.ReactionGroupId
                && ImpactForce == other.ImpactForce
                && IsAirborne == other.IsAirborne
                && CurrentActionId == other.CurrentActionId
                && CurrentCharacterPhase == other.CurrentCharacterPhase
                && CurrentCombatPhase == other.CurrentCombatPhase
                && CurrentActionCommitted == other.CurrentActionCommitted
                && CurrentActionInterruptible == other.CurrentActionInterruptible
                && TraceNumericId == other.TraceNumericId;
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
                hash = (hash * 397) ^ (int)CurrentScatterPressureBand;
                hash = (hash * 397) ^ PreviousPressure;
                hash = (hash * 397) ^ CurrentScatterPressure;
                hash = (hash * 397) ^ MaxScatterPressure;
                hash = (hash * 397) ^ Delta;
                hash = (hash * 397) ^ SourceId;
                hash = (hash * 397) ^ PressureReason.GetHashCode()
                    ^ (_reason == null ? 0 : _reason.GetHashCode());
                hash = (hash * 397) ^ TraceId.GetHashCode();
                hash = (hash * 397) ^ IsDeath.GetHashCode();
                hash = (hash * 397) ^ (LifecycleState == null ? 0 : LifecycleState.GetHashCode());
                hash = (hash * 397) ^ BodyPartId.GetHashCode();
                hash = (hash * 397) ^ HitZoneId.GetHashCode();
                hash = (hash * 397) ^ DamageTypeId.GetHashCode();
                hash = (hash * 397) ^ (int)HitDirection;
                hash = (hash * 397) ^ ReactionGroupId.GetHashCode();
                hash = (hash * 397) ^ ImpactForce;
                hash = (hash * 397) ^ IsAirborne.GetHashCode();
                hash = (hash * 397) ^ CurrentActionId.GetHashCode();
                hash = (hash * 397) ^ (int)CurrentCharacterPhase;
                hash = (hash * 397) ^ (int)CurrentCombatPhase;
                hash = (hash * 397) ^ CurrentActionCommitted.GetHashCode();
                hash = (hash * 397) ^ CurrentActionInterruptible.GetHashCode();
                hash = (hash * 397) ^ TraceNumericId;
                return hash;
            }
        }
    }

    internal static class CharacterReactionEnumGuards
    {
        public static bool IsDefined(CharacterReactionContextSourceKind value)
        {
            switch (value)
            {
                case CharacterReactionContextSourceKind.Unknown:
                case CharacterReactionContextSourceKind.CohesionBreak:
                case CharacterReactionContextSourceKind.GuardBreak:
                case CharacterReactionContextSourceKind.ArmorBreak:
                case CharacterReactionContextSourceKind.PressureBandChanged:
                case CharacterReactionContextSourceKind.Death:
                case CharacterReactionContextSourceKind.Lifecycle:
                case CharacterReactionContextSourceKind.Hit:
                    return true;
                default:
                    return false;
            }
        }

        public static bool IsDefined(CharacterReactionContextCompleteness value)
        {
            switch (value)
            {
                case CharacterReactionContextCompleteness.None:
                case CharacterReactionContextCompleteness.SourceOnly:
                case CharacterReactionContextCompleteness.PressureOnly:
                case CharacterReactionContextCompleteness.Full:
                    return true;
                default:
                    return false;
            }
        }

        public static bool IsDefined(CharacterHitDirection value)
        {
            switch (value)
            {
                case CharacterHitDirection.Unknown:
                case CharacterHitDirection.Front:
                case CharacterHitDirection.Back:
                case CharacterHitDirection.Left:
                case CharacterHitDirection.Right:
                case CharacterHitDirection.Up:
                case CharacterHitDirection.Down:
                    return true;
                default:
                    return false;
            }
        }

        public static bool IsDefined(PressureBand value)
        {
            switch (value)
            {
                case PressureBand.Stable:
                case PressureBand.Pressed:
                case PressureBand.Cracked:
                case PressureBand.Critical:
                case PressureBand.Broken:
                    return true;
                default:
                    return false;
            }
        }

        public static bool IsDefined(CharacterActionPhaseKind value)
        {
            switch (value)
            {
                case CharacterActionPhaseKind.None:
                case CharacterActionPhaseKind.Startup:
                case CharacterActionPhaseKind.Active:
                case CharacterActionPhaseKind.Recovery:
                case CharacterActionPhaseKind.Loop:
                case CharacterActionPhaseKind.Airborne:
                case CharacterActionPhaseKind.Landing:
                case CharacterActionPhaseKind.Channel:
                case CharacterActionPhaseKind.Hold:
                case CharacterActionPhaseKind.Exit:
                case CharacterActionPhaseKind.Custom:
                    return true;
                default:
                    return false;
            }
        }

        public static bool IsDefined(CombatActionPhase value)
        {
            switch (value)
            {
                case CombatActionPhase.None:
                case CombatActionPhase.Startup:
                case CombatActionPhase.Active:
                case CombatActionPhase.Recovery:
                case CombatActionPhase.Finished:
                    return true;
                default:
                    return false;
            }
        }
    }
}
