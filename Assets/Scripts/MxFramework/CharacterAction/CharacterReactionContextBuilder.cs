using System;
using System.Collections.Generic;
using MxFramework.Combat.Animation;
using MxFramework.Combat.Hit;
using MxFramework.Gameplay;
using MxFramework.Runtime;

namespace MxFramework.CharacterAction
{
    public readonly struct CharacterReactionHitSource : IEquatable<CharacterReactionHitSource>
    {
        public CharacterReactionHitSource(
            RuntimeFrame frame,
            GameplayEntityId entityId,
            string bodyPartId = "",
            string hitZoneId = "",
            string damageTypeId = "",
            CharacterHitDirection hitDirection = CharacterHitDirection.Unknown,
            int? impactForce = null,
            string reactionGroupId = "",
            int sourceId = 0,
            string reason = "",
            string traceId = "",
            PressureBand previousPressureBand = PressureBand.Stable,
            PressureBand currentPressureBand = PressureBand.Stable,
            int previousPressure = 0,
            int currentPressure = 0,
            int maxPressure = 0,
            int delta = 0,
            bool isDeath = false,
            string lifecycleState = "",
            bool isAirborne = false,
            string currentActionId = "",
            CharacterActionPhaseKind currentCharacterPhase = CharacterActionPhaseKind.None,
            CombatActionPhase currentCombatPhase = CombatActionPhase.None,
            bool currentActionCommitted = false,
            bool currentActionInterruptible = true)
        {
            if (!Enum.IsDefined(typeof(CharacterHitDirection), hitDirection))
                throw new ArgumentOutOfRangeException(nameof(hitDirection), "Hit direction is not defined.");
            if (impactForce.HasValue && impactForce.Value < 0)
                throw new ArgumentOutOfRangeException(nameof(impactForce), "Impact force cannot be negative.");
            if (!Enum.IsDefined(typeof(PressureBand), previousPressureBand))
                throw new ArgumentOutOfRangeException(nameof(previousPressureBand), "Previous pressure band is not defined.");
            if (!Enum.IsDefined(typeof(PressureBand), currentPressureBand))
                throw new ArgumentOutOfRangeException(nameof(currentPressureBand), "Current pressure band is not defined.");
            if (!Enum.IsDefined(typeof(CharacterActionPhaseKind), currentCharacterPhase))
                throw new ArgumentOutOfRangeException(nameof(currentCharacterPhase), "Current character phase is not defined.");
            if (!Enum.IsDefined(typeof(CombatActionPhase), currentCombatPhase))
                throw new ArgumentOutOfRangeException(nameof(currentCombatPhase), "Current combat phase is not defined.");

            Frame = frame;
            EntityId = entityId;
            BodyPartId = bodyPartId ?? string.Empty;
            HitZoneId = hitZoneId ?? string.Empty;
            DamageTypeId = damageTypeId ?? string.Empty;
            HitDirection = hitDirection;
            ImpactForce = impactForce;
            ReactionGroupId = reactionGroupId ?? string.Empty;
            SourceId = sourceId;
            Reason = reason ?? string.Empty;
            TraceId = traceId ?? string.Empty;
            PreviousPressureBand = previousPressureBand;
            CurrentPressureBand = currentPressureBand;
            PreviousPressure = previousPressure;
            CurrentPressure = currentPressure;
            MaxPressure = maxPressure;
            Delta = delta;
            IsDeath = isDeath;
            LifecycleState = lifecycleState ?? string.Empty;
            IsAirborne = isAirborne;
            CurrentActionId = currentActionId ?? string.Empty;
            CurrentCharacterPhase = currentCharacterPhase;
            CurrentCombatPhase = currentCombatPhase;
            CurrentActionCommitted = currentActionCommitted;
            CurrentActionInterruptible = currentActionInterruptible;
        }

        public RuntimeFrame Frame { get; }
        public GameplayEntityId EntityId { get; }
        public string BodyPartId { get; }
        public string HitZoneId { get; }
        public string DamageTypeId { get; }
        public CharacterHitDirection HitDirection { get; }
        public int? ImpactForce { get; }
        public string ReactionGroupId { get; }
        public int SourceId { get; }
        public string Reason { get; }
        public string TraceId { get; }
        public PressureBand PreviousPressureBand { get; }
        public PressureBand CurrentPressureBand { get; }
        public int PreviousPressure { get; }
        public int CurrentPressure { get; }
        public int MaxPressure { get; }
        public int Delta { get; }
        public bool IsDeath { get; }
        public string LifecycleState { get; }
        public bool IsAirborne { get; }
        public string CurrentActionId { get; }
        public CharacterActionPhaseKind CurrentCharacterPhase { get; }
        public CombatActionPhase CurrentCombatPhase { get; }
        public bool CurrentActionCommitted { get; }
        public bool CurrentActionInterruptible { get; }

        public bool HasFullHitFacts => !string.IsNullOrEmpty(BodyPartId)
            && !string.IsNullOrEmpty(HitZoneId)
            && !string.IsNullOrEmpty(DamageTypeId)
            && HitDirection != CharacterHitDirection.Unknown
            && ImpactForce.HasValue
            && !string.IsNullOrEmpty(ReactionGroupId);

        public bool Equals(CharacterReactionHitSource other)
        {
            return Frame.Equals(other.Frame)
                && EntityId.Equals(other.EntityId)
                && string.Equals(BodyPartId, other.BodyPartId, StringComparison.Ordinal)
                && string.Equals(HitZoneId, other.HitZoneId, StringComparison.Ordinal)
                && string.Equals(DamageTypeId, other.DamageTypeId, StringComparison.Ordinal)
                && HitDirection == other.HitDirection
                && ImpactForce == other.ImpactForce
                && string.Equals(ReactionGroupId, other.ReactionGroupId, StringComparison.Ordinal)
                && SourceId == other.SourceId
                && string.Equals(Reason, other.Reason, StringComparison.Ordinal)
                && string.Equals(TraceId, other.TraceId, StringComparison.Ordinal)
                && PreviousPressureBand == other.PreviousPressureBand
                && CurrentPressureBand == other.CurrentPressureBand
                && PreviousPressure == other.PreviousPressure
                && CurrentPressure == other.CurrentPressure
                && MaxPressure == other.MaxPressure
                && Delta == other.Delta
                && IsDeath == other.IsDeath
                && string.Equals(LifecycleState, other.LifecycleState, StringComparison.Ordinal)
                && IsAirborne == other.IsAirborne
                && string.Equals(CurrentActionId, other.CurrentActionId, StringComparison.Ordinal)
                && CurrentCharacterPhase == other.CurrentCharacterPhase
                && CurrentCombatPhase == other.CurrentCombatPhase
                && CurrentActionCommitted == other.CurrentActionCommitted
                && CurrentActionInterruptible == other.CurrentActionInterruptible;
        }

        public override bool Equals(object obj)
        {
            return obj is CharacterReactionHitSource other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = Frame.GetHashCode();
                hash = (hash * 397) ^ EntityId.GetHashCode();
                hash = (hash * 397) ^ (BodyPartId == null ? 0 : BodyPartId.GetHashCode());
                hash = (hash * 397) ^ (HitZoneId == null ? 0 : HitZoneId.GetHashCode());
                hash = (hash * 397) ^ (DamageTypeId == null ? 0 : DamageTypeId.GetHashCode());
                hash = (hash * 397) ^ (int)HitDirection;
                hash = (hash * 397) ^ ImpactForce.GetHashCode();
                hash = (hash * 397) ^ (ReactionGroupId == null ? 0 : ReactionGroupId.GetHashCode());
                hash = (hash * 397) ^ SourceId;
                hash = (hash * 397) ^ (Reason == null ? 0 : Reason.GetHashCode());
                hash = (hash * 397) ^ (TraceId == null ? 0 : TraceId.GetHashCode());
                hash = (hash * 397) ^ (int)PreviousPressureBand;
                hash = (hash * 397) ^ (int)CurrentPressureBand;
                hash = (hash * 397) ^ PreviousPressure;
                hash = (hash * 397) ^ CurrentPressure;
                hash = (hash * 397) ^ MaxPressure;
                hash = (hash * 397) ^ Delta;
                hash = (hash * 397) ^ IsDeath.GetHashCode();
                hash = (hash * 397) ^ (LifecycleState == null ? 0 : LifecycleState.GetHashCode());
                hash = (hash * 397) ^ IsAirborne.GetHashCode();
                hash = (hash * 397) ^ (CurrentActionId == null ? 0 : CurrentActionId.GetHashCode());
                hash = (hash * 397) ^ (int)CurrentCharacterPhase;
                hash = (hash * 397) ^ (int)CurrentCombatPhase;
                hash = (hash * 397) ^ CurrentActionCommitted.GetHashCode();
                hash = (hash * 397) ^ CurrentActionInterruptible.GetHashCode();
                return hash;
            }
        }

        public static CharacterReactionHitSource FromHitResolveResult(
            HitResolveResult result,
            GameplayEntityId targetEntityId,
            string bodyPartId = "",
            string hitZoneId = "",
            string damageTypeId = "",
            CharacterHitDirection hitDirection = CharacterHitDirection.Unknown,
            int? impactForce = null,
            string reactionGroupId = "",
            bool isAirborne = false,
            string currentActionId = "",
            CharacterActionPhaseKind currentCharacterPhase = CharacterActionPhaseKind.None,
            CombatActionPhase currentCombatPhase = CombatActionPhase.None,
            bool currentActionCommitted = false,
            bool currentActionInterruptible = true)
        {
            return new CharacterReactionHitSource(
                new RuntimeFrame(result.Frame.Value),
                targetEntityId,
                bodyPartId,
                hitZoneId,
                damageTypeId,
                hitDirection,
                impactForce,
                reactionGroupId,
                sourceId: result.AttackerId.Value,
                reason: result.Kind.ToString(),
                traceId: result.TraceId.ToString(),
                isAirborne: isAirborne,
                currentActionId: currentActionId,
                currentCharacterPhase: currentCharacterPhase,
                currentCombatPhase: currentCombatPhase,
                currentActionCommitted: currentActionCommitted,
                currentActionInterruptible: currentActionInterruptible);
        }
    }

    public readonly struct CharacterReactionContextBuildResult
    {
        public CharacterReactionContextBuildResult(
            CharacterReactionContext context,
            CharacterReactionContextCompleteness completeness,
            CharacterActionDiagnostic[] diagnostics)
        {
            Context = context;
            Completeness = completeness;
            Diagnostics = diagnostics ?? Array.Empty<CharacterActionDiagnostic>();
        }

        public CharacterReactionContext Context { get; }
        public CharacterReactionContextCompleteness Completeness { get; }
        public CharacterActionDiagnostic[] Diagnostics { get; }
        public bool Success => Completeness != CharacterReactionContextCompleteness.None;
    }

    public static class CharacterReactionContextBuilder
    {
        public static CharacterReactionContextBuildResult FromHitSource(CharacterReactionHitSource source)
        {
            List<string> missingFields = new List<string>();
            if (string.IsNullOrEmpty(source.BodyPartId))
                missingFields.Add(nameof(CharacterReactionHitSource.BodyPartId));
            if (string.IsNullOrEmpty(source.HitZoneId))
                missingFields.Add(nameof(CharacterReactionHitSource.HitZoneId));
            if (string.IsNullOrEmpty(source.DamageTypeId))
                missingFields.Add(nameof(CharacterReactionHitSource.DamageTypeId));
            if (source.HitDirection == CharacterHitDirection.Unknown)
                missingFields.Add(nameof(CharacterReactionHitSource.HitDirection));
            if (!source.ImpactForce.HasValue)
                missingFields.Add(nameof(CharacterReactionHitSource.ImpactForce));
            if (string.IsNullOrEmpty(source.ReactionGroupId))
                missingFields.Add(nameof(CharacterReactionHitSource.ReactionGroupId));

            CharacterReactionContextCompleteness completeness = missingFields.Count == 0
                ? CharacterReactionContextCompleteness.Full
                : CharacterReactionContextCompleteness.SourceOnly;
            var context = new CharacterReactionContext(
                CharacterReactionContextSourceKind.Hit,
                completeness,
                source.Frame,
                source.EntityId,
                source.PreviousPressureBand,
                source.CurrentPressureBand,
                source.PreviousPressure,
                source.CurrentPressure,
                source.MaxPressure,
                source.Delta,
                source.SourceId,
                source.Reason,
                source.TraceId,
                source.IsDeath,
                source.LifecycleState,
                source.BodyPartId,
                source.HitZoneId,
                source.DamageTypeId,
                source.HitDirection,
                source.ReactionGroupId,
                source.ImpactForce.GetValueOrDefault(),
                source.IsAirborne,
                source.CurrentActionId,
                source.CurrentCharacterPhase,
                source.CurrentCombatPhase,
                source.CurrentActionCommitted,
                source.CurrentActionInterruptible);

            if (missingFields.Count == 0)
            {
                return new CharacterReactionContextBuildResult(
                    context,
                    completeness,
                    Array.Empty<CharacterActionDiagnostic>());
            }

            return new CharacterReactionContextBuildResult(
                context,
                completeness,
                new[]
                {
                    CharacterActionDiagnostic.Error(
                        CharacterActionDiagnosticCodes.ReactionContextIncomplete,
                        "Hit reaction source is incomplete: missing " + string.Join(", ", missingFields) + ".")
                });
        }

        public static CharacterReactionContextBuildResult FromPostureBreak(PostureBreakEvent evt)
        {
            CharacterReactionContext context = CreatePressureOnly(
                CharacterReactionContextSourceKind.PostureBreak,
                evt.Frame,
                evt.EntityId,
                evt.PreviousBand,
                PressureBand.Broken,
                evt.PreviousValue,
                evt.CurrentPressure,
                evt.MaxPressure,
                evt.Delta,
                evt.SourceId,
                evt.Reason,
                evt.TraceId);
            return CompleteWithMissingHitDiagnostic(context);
        }

        public static CharacterReactionContextBuildResult FromGuardBreak(GuardBreakEvent evt)
        {
            CharacterReactionContext context = CreatePressureOnly(
                CharacterReactionContextSourceKind.GuardBreak,
                evt.Frame,
                evt.EntityId,
                evt.PreviousBand,
                PressureBand.Broken,
                evt.PreviousValue,
                evt.CurrentPressure,
                evt.MaxPressure,
                evt.Delta,
                evt.SourceId,
                evt.Reason,
                evt.TraceId);
            return CompleteWithMissingHitDiagnostic(context);
        }

        public static CharacterReactionContextBuildResult FromArmorBreak(ArmorBreakEvent evt)
        {
            CharacterReactionContext context = CreatePressureOnly(
                CharacterReactionContextSourceKind.ArmorBreak,
                evt.Frame,
                evt.EntityId,
                PressureBand.Stable,
                PressureBand.Broken,
                evt.PreviousIntegrity,
                evt.CurrentIntegrity,
                evt.MaxIntegrity,
                evt.CurrentIntegrity - evt.PreviousIntegrity,
                0,
                string.Empty,
                evt.TraceId);
            return CompleteWithMissingHitDiagnostic(context);
        }

        public static CharacterReactionContextBuildResult FromPressureBandChanged(PressureBandChangedEvent evt)
        {
            CharacterReactionContext context = CreatePressureOnly(
                CharacterReactionContextSourceKind.PressureBandChanged,
                evt.Frame,
                evt.EntityId,
                evt.PreviousBand,
                evt.NewBand,
                evt.PreviousValue,
                evt.NewValue,
                0,
                evt.Delta,
                evt.SourceId,
                evt.Reason,
                evt.TraceId);
            return CompleteWithMissingHitDiagnostic(context);
        }

        public static CharacterReactionContextBuildResult FromDeath(
            RuntimeFrame frame,
            GameplayEntityId entityId,
            string reason = "",
            string traceId = "")
        {
            CharacterReactionContext context = CreateDeathPressureOnly(
                CharacterReactionContextSourceKind.Death,
                frame,
                entityId,
                reason,
                traceId);
            return CompleteWithMissingHitDiagnostic(context);
        }

        public static CharacterReactionContextBuildResult FromLifecycle(
            RuntimeFrame frame,
            GameplayEntityId entityId,
            string lifecycleState,
            bool isDeath = false,
            string reason = "",
            string traceId = "")
        {
            CharacterReactionContextSourceKind sourceKind = isDeath
                ? CharacterReactionContextSourceKind.Death
                : CharacterReactionContextSourceKind.Lifecycle;
            CharacterReactionContext context = isDeath
                ? CreateDeathPressureOnly(sourceKind, frame, entityId, reason, traceId, lifecycleState)
                : CreateSourceOnly(sourceKind, frame, entityId, lifecycleState, reason, traceId);
            return CompleteWithMissingHitDiagnostic(context);
        }

        public static CharacterReactionContextBuildResult MissingSource(RuntimeFrame frame, GameplayEntityId entityId)
        {
            var context = new CharacterReactionContext(
                CharacterReactionContextSourceKind.Unknown,
                CharacterReactionContextCompleteness.None,
                frame,
                entityId,
                PressureBand.Stable,
                PressureBand.Stable,
                0,
                0,
                0,
                0,
                0,
                string.Empty,
                string.Empty,
                isDeath: false,
                lifecycleState: string.Empty,
                bodyPartId: string.Empty,
                hitZoneId: string.Empty,
                damageTypeId: string.Empty,
                hitDirection: CharacterHitDirection.Unknown,
                reactionGroupId: string.Empty);
            return new CharacterReactionContextBuildResult(
                context,
                CharacterReactionContextCompleteness.None,
                new[]
                {
                    CharacterActionDiagnostic.Error(
                        CharacterActionDiagnosticCodes.ReactionContextMissingSource,
                        "ReactionContext source was not provided.")
                });
        }

        private static CharacterReactionContext CreatePressureOnly(
            CharacterReactionContextSourceKind sourceKind,
            RuntimeFrame frame,
            GameplayEntityId entityId,
            PressureBand previousBand,
            PressureBand currentBand,
            int previousPressure,
            int currentPressure,
            int maxPressure,
            int delta,
            int sourceId,
            string reason,
            string traceId)
        {
            return new CharacterReactionContext(
                sourceKind,
                CharacterReactionContextCompleteness.PressureOnly,
                frame,
                entityId,
                previousBand,
                currentBand,
                previousPressure,
                currentPressure,
                maxPressure,
                delta,
                sourceId,
                reason,
                traceId,
                isDeath: sourceKind == CharacterReactionContextSourceKind.Death,
                lifecycleState: string.Empty,
                bodyPartId: string.Empty,
                hitZoneId: string.Empty,
                damageTypeId: string.Empty,
                hitDirection: CharacterHitDirection.Unknown,
                reactionGroupId: string.Empty);
        }

        private static CharacterReactionContext CreateSourceOnly(
            CharacterReactionContextSourceKind sourceKind,
            RuntimeFrame frame,
            GameplayEntityId entityId,
            string lifecycleState,
            string reason,
            string traceId)
        {
            return new CharacterReactionContext(
                sourceKind,
                CharacterReactionContextCompleteness.SourceOnly,
                frame,
                entityId,
                PressureBand.Stable,
                PressureBand.Stable,
                0,
                0,
                0,
                0,
                0,
                reason,
                traceId,
                isDeath: false,
                lifecycleState,
                bodyPartId: string.Empty,
                hitZoneId: string.Empty,
                damageTypeId: string.Empty,
                hitDirection: CharacterHitDirection.Unknown,
                reactionGroupId: string.Empty);
        }

        private static CharacterReactionContext CreateDeathPressureOnly(
            CharacterReactionContextSourceKind sourceKind,
            RuntimeFrame frame,
            GameplayEntityId entityId,
            string reason,
            string traceId,
            string lifecycleState = "Death")
        {
            return new CharacterReactionContext(
                sourceKind,
                CharacterReactionContextCompleteness.PressureOnly,
                frame,
                entityId,
                PressureBand.Stable,
                PressureBand.Stable,
                0,
                0,
                0,
                0,
                0,
                reason,
                traceId,
                isDeath: true,
                lifecycleState: lifecycleState,
                bodyPartId: string.Empty,
                hitZoneId: string.Empty,
                damageTypeId: string.Empty,
                hitDirection: CharacterHitDirection.Unknown,
                reactionGroupId: string.Empty);
        }

        private static CharacterReactionContextBuildResult CompleteWithMissingHitDiagnostic(CharacterReactionContext context)
        {
            return new CharacterReactionContextBuildResult(
                context,
                context.Completeness,
                new[]
                {
                    CharacterActionDiagnostic.Warning(
                        CharacterActionDiagnosticCodes.ReactionContextIncomplete,
                        "ReactionContext does not provide body part, hit zone, damage type, hit direction, or reaction group.")
                });
        }
    }
}
