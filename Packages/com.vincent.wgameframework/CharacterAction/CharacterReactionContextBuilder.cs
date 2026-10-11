using System;
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
            int bodyPartId = 0,
            int hitZoneId = 0,
            int damageTypeId = 0,
            CharacterHitDirection hitDirection = CharacterHitDirection.Unknown,
            int? impactForce = null,
            int reactionGroupId = 0,
            int sourceId = 0,
            string reason = "",
            RuntimeTraceIdentity traceId = default,
            PressureBand previousPressureBand = PressureBand.Stable,
            PressureBand currentScatterPressureBand = PressureBand.Stable,
            int previousPressure = 0,
            int currentScatterPressure = 0,
            int maxScatterPressure = 0,
            int delta = 0,
            bool isDeath = false,
            string lifecycleState = "",
            bool isAirborne = false,
            CharacterActionId currentActionId = default,
            CharacterActionPhaseKind currentCharacterPhase = CharacterActionPhaseKind.None,
            CombatActionPhase currentCombatPhase = CombatActionPhase.None,
            bool currentActionCommitted = false,
            bool currentActionInterruptible = true,
            int traceNumericId = 0)
        {
            if (!CharacterReactionEnumGuards.IsDefined(hitDirection))
                throw new ArgumentOutOfRangeException(nameof(hitDirection), "Hit direction is not defined.");
            if (impactForce.HasValue && impactForce.Value < 0)
                throw new ArgumentOutOfRangeException(nameof(impactForce), "Impact force cannot be negative.");
            if (!CharacterReactionEnumGuards.IsDefined(previousPressureBand))
                throw new ArgumentOutOfRangeException(nameof(previousPressureBand), "Previous pressure band is not defined.");
            if (!CharacterReactionEnumGuards.IsDefined(currentScatterPressureBand))
                throw new ArgumentOutOfRangeException(nameof(currentScatterPressureBand), "Current pressure band is not defined.");
            if (!CharacterReactionEnumGuards.IsDefined(currentCharacterPhase))
                throw new ArgumentOutOfRangeException(nameof(currentCharacterPhase), "Current character phase is not defined.");
            if (!CharacterReactionEnumGuards.IsDefined(currentCombatPhase))
                throw new ArgumentOutOfRangeException(nameof(currentCombatPhase), "Current combat phase is not defined.");

            Frame = frame;
            EntityId = entityId;
            BodyPartId = bodyPartId;
            HitZoneId = hitZoneId;
            DamageTypeId = damageTypeId;
            HitDirection = hitDirection;
            ImpactForce = impactForce;
            ReactionGroupId = reactionGroupId;
            SourceId = sourceId;
            Reason = reason ?? string.Empty;
            TraceId = traceId;
            PreviousPressureBand = previousPressureBand;
            CurrentScatterPressureBand = currentScatterPressureBand;
            PreviousPressure = previousPressure;
            CurrentScatterPressure = currentScatterPressure;
            MaxScatterPressure = maxScatterPressure;
            Delta = delta;
            IsDeath = isDeath;
            LifecycleState = lifecycleState ?? string.Empty;
            IsAirborne = isAirborne;
            CurrentActionId = currentActionId;
            CurrentCharacterPhase = currentCharacterPhase;
            CurrentCombatPhase = currentCombatPhase;
            CurrentActionCommitted = currentActionCommitted;
            CurrentActionInterruptible = currentActionInterruptible;
            TraceNumericId = traceNumericId;
        }

        public RuntimeFrame Frame { get; }
        public GameplayEntityId EntityId { get; }
        public int BodyPartId { get; }
        public int HitZoneId { get; }
        public int DamageTypeId { get; }
        public CharacterHitDirection HitDirection { get; }
        public int? ImpactForce { get; }
        public int ReactionGroupId { get; }
        public int SourceId { get; }
        public string Reason { get; }
        public RuntimeTraceIdentity TraceId { get; }
        public PressureBand PreviousPressureBand { get; }
        public PressureBand CurrentScatterPressureBand { get; }
        public int PreviousPressure { get; }
        public int CurrentScatterPressure { get; }
        public int MaxScatterPressure { get; }
        public int Delta { get; }
        public bool IsDeath { get; }
        public string LifecycleState { get; }
        public bool IsAirborne { get; }
        public CharacterActionId CurrentActionId { get; }
        public CharacterActionPhaseKind CurrentCharacterPhase { get; }
        public CombatActionPhase CurrentCombatPhase { get; }
        public bool CurrentActionCommitted { get; }
        public bool CurrentActionInterruptible { get; }
        public int TraceNumericId { get; }

        public bool HasFullHitFacts => (BodyPartId > 0)
            && (HitZoneId > 0)
            && (DamageTypeId > 0)
            && HitDirection != CharacterHitDirection.Unknown
            && ImpactForce.HasValue
            && !(ReactionGroupId <= 0);

        public bool Equals(CharacterReactionHitSource other)
        {
            return Frame.Equals(other.Frame)
                && EntityId.Equals(other.EntityId)
                && BodyPartId == other.BodyPartId
                && HitZoneId == other.HitZoneId
                && DamageTypeId == other.DamageTypeId
                && HitDirection == other.HitDirection
                && ImpactForce == other.ImpactForce
                && ReactionGroupId == other.ReactionGroupId
                && SourceId == other.SourceId
                && string.Equals(Reason, other.Reason, StringComparison.Ordinal)
                && TraceId.Equals(other.TraceId)
                && PreviousPressureBand == other.PreviousPressureBand
                && CurrentScatterPressureBand == other.CurrentScatterPressureBand
                && PreviousPressure == other.PreviousPressure
                && CurrentScatterPressure == other.CurrentScatterPressure
                && MaxScatterPressure == other.MaxScatterPressure
                && Delta == other.Delta
                && IsDeath == other.IsDeath
                && string.Equals(LifecycleState, other.LifecycleState, StringComparison.Ordinal)
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
            return obj is CharacterReactionHitSource other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = Frame.GetHashCode();
                hash = (hash * 397) ^ EntityId.GetHashCode();
                hash = (hash * 397) ^ BodyPartId.GetHashCode();
                hash = (hash * 397) ^ HitZoneId.GetHashCode();
                hash = (hash * 397) ^ DamageTypeId.GetHashCode();
                hash = (hash * 397) ^ (int)HitDirection;
                hash = (hash * 397) ^ ImpactForce.GetHashCode();
                hash = (hash * 397) ^ ReactionGroupId.GetHashCode();
                hash = (hash * 397) ^ SourceId;
                hash = (hash * 397) ^ (Reason == null ? 0 : Reason.GetHashCode());
                hash = (hash * 397) ^ TraceId.GetHashCode();
                hash = (hash * 397) ^ (int)PreviousPressureBand;
                hash = (hash * 397) ^ (int)CurrentScatterPressureBand;
                hash = (hash * 397) ^ PreviousPressure;
                hash = (hash * 397) ^ CurrentScatterPressure;
                hash = (hash * 397) ^ MaxScatterPressure;
                hash = (hash * 397) ^ Delta;
                hash = (hash * 397) ^ IsDeath.GetHashCode();
                hash = (hash * 397) ^ (LifecycleState == null ? 0 : LifecycleState.GetHashCode());
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

        public static CharacterReactionHitSource FromHitResolveResult(
            HitResolveResult result,
            GameplayEntityId targetEntityId,
            int bodyPartId = 0,
            int hitZoneId = 0,
            int damageTypeId = 0,
            CharacterHitDirection hitDirection = CharacterHitDirection.Unknown,
            int? impactForce = null,
            int reactionGroupId = 0,
            bool isAirborne = false,
            CharacterActionId currentActionId = default,
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
                traceId: new RuntimeTraceIdentity(1004, result.AttackerId.Value, result.TraceId, 0), traceNumericId: result.TraceId,
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
            MissingHitFieldFlags missingFields = GetMissingHitFields(source);
            CharacterReactionContextCompleteness completeness = missingFields == MissingHitFieldFlags.None
                ? CharacterReactionContextCompleteness.Full
                : CharacterReactionContextCompleteness.SourceOnly;
            CharacterReactionContext context = CreateHitContext(source, completeness);

            if (missingFields == MissingHitFieldFlags.None)
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
                        CharacterActionDiagnosticCode.ReactionContextIncomplete,
                        "Hit reaction source is incomplete: missing " + FormatMissingHitFields(missingFields) + ".")
                });
        }

        /// <summary>Builds a hit reaction context without allocating missing-field diagnostics.</summary>
        /// <remarks>NoAlloc after inputs are initialized; use <see cref="FromHitSource"/> for diagnostic text.</remarks>
        public static CharacterReactionContextBuildResult FromHitSourceNoAlloc(CharacterReactionHitSource source)
        {
            MissingHitFieldFlags missingFields = GetMissingHitFields(source);
            CharacterReactionContextCompleteness completeness = missingFields == MissingHitFieldFlags.None
                ? CharacterReactionContextCompleteness.Full
                : CharacterReactionContextCompleteness.SourceOnly;
            return new CharacterReactionContextBuildResult(
                CreateHitContext(source, completeness),
                completeness,
                Array.Empty<CharacterActionDiagnostic>());
        }

        public static CharacterReactionContextBuildResult FromCohesionBreak(CohesionBreakEvent evt)
        {
            CharacterReactionContext context = CreatePressureOnly(
                CharacterReactionContextSourceKind.CohesionBreak,
                evt.Frame,
                evt.EntityId,
                evt.PreviousBand,
                PressureBand.Broken,
                evt.PreviousValue,
                evt.CurrentScatterPressure,
                evt.MaxScatterPressure,
                evt.Delta,
                evt.SourceId,
                evt.Reason,
                evt.TraceId);
            return CompleteWithMissingHitDiagnostic(context);
        }

        /// <summary>Builds a cohesion-break reaction context without allocating warning diagnostics.</summary>
        /// <remarks>NoAlloc after inputs are initialized; use <see cref="FromCohesionBreak"/> for diagnostic text.</remarks>
        public static CharacterReactionContextBuildResult FromCohesionBreakNoAlloc(CohesionBreakEvent evt)
        {
            CharacterReactionContext context = CreatePressureOnly(
                CharacterReactionContextSourceKind.CohesionBreak,
                evt.Frame,
                evt.EntityId,
                evt.PreviousBand,
                PressureBand.Broken,
                evt.PreviousValue,
                evt.CurrentScatterPressure,
                evt.MaxScatterPressure,
                evt.Delta,
                evt.SourceId,
                evt.Reason,
                evt.TraceId);
            return CompleteNoDiagnostics(context);
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
                evt.CurrentLimit,
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
            RuntimeTraceIdentity traceId = default)
        {
            CharacterReactionContext context = CreateDeathPressureOnly(
                CharacterReactionContextSourceKind.Death,
                frame,
                entityId,
                reason,
                traceId);
            return CompleteWithMissingHitDiagnostic(context);
        }

        /// <summary>Builds a death reaction context without allocating warning diagnostics.</summary>
        /// <remarks>NoAlloc after inputs are initialized; use <see cref="FromDeath"/> for diagnostic text.</remarks>
        public static CharacterReactionContextBuildResult FromDeathNoAlloc(
            RuntimeFrame frame,
            GameplayEntityId entityId,
            string reason = "",
            RuntimeTraceIdentity traceId = default)
        {
            CharacterReactionContext context = CreateDeathPressureOnly(
                CharacterReactionContextSourceKind.Death,
                frame,
                entityId,
                reason,
                traceId);
            return CompleteNoDiagnostics(context);
        }

        public static CharacterReactionContextBuildResult FromLifecycle(
            RuntimeFrame frame,
            GameplayEntityId entityId,
            string lifecycleState,
            bool isDeath = false,
            string reason = "",
            RuntimeTraceIdentity traceId = default)
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
                default,
                isDeath: false,
                lifecycleState: string.Empty,
                bodyPartId: 0,
                hitZoneId: 0,
                damageTypeId: 0,
                hitDirection: CharacterHitDirection.Unknown,
                reactionGroupId: 0);
            return new CharacterReactionContextBuildResult(
                context,
                CharacterReactionContextCompleteness.None,
                new[]
                {
                    CharacterActionDiagnostic.Error(
                        CharacterActionDiagnosticCode.ReactionContextMissingSource,
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
            int currentScatterPressure,
            int maxScatterPressure,
            int delta,
            int sourceId,
            string reason,
            RuntimeTraceIdentity traceId)
        {
            return new CharacterReactionContext(
                sourceKind,
                CharacterReactionContextCompleteness.PressureOnly,
                frame,
                entityId,
                previousBand,
                currentBand,
                previousPressure,
                currentScatterPressure,
                maxScatterPressure,
                delta,
                sourceId,
                reason,
                traceId,
                isDeath: sourceKind == CharacterReactionContextSourceKind.Death,
                lifecycleState: string.Empty,
                bodyPartId: 0,
                hitZoneId: 0,
                damageTypeId: 0,
                hitDirection: CharacterHitDirection.Unknown,
                reactionGroupId: 0);
        }

        private static CharacterReactionContext CreatePressureOnly(
            CharacterReactionContextSourceKind sourceKind,
            RuntimeFrame frame,
            GameplayEntityId entityId,
            PressureBand previousBand,
            PressureBand currentBand,
            int previousPressure,
            int currentScatterPressure,
            int maxScatterPressure,
            int delta,
            int sourceId,
            GameplayScatterPressureReason reason,
            RuntimeTraceIdentity traceId)
        {
            return new CharacterReactionContext(
                sourceKind,
                CharacterReactionContextCompleteness.PressureOnly,
                frame,
                entityId,
                previousBand,
                currentBand,
                previousPressure,
                currentScatterPressure,
                maxScatterPressure,
                delta,
                sourceId,
                string.Empty,
                traceId,
                isDeath: sourceKind == CharacterReactionContextSourceKind.Death,
                lifecycleState: string.Empty,
                bodyPartId: 0,
                hitZoneId: 0,
                damageTypeId: 0,
                hitDirection: CharacterHitDirection.Unknown,
                reactionGroupId: 0,
                pressureReason: reason);
        }

        private static CharacterReactionContext CreateSourceOnly(
            CharacterReactionContextSourceKind sourceKind,
            RuntimeFrame frame,
            GameplayEntityId entityId,
            string lifecycleState,
            string reason,
            RuntimeTraceIdentity traceId)
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
                bodyPartId: 0,
                hitZoneId: 0,
                damageTypeId: 0,
                hitDirection: CharacterHitDirection.Unknown,
                reactionGroupId: 0);
        }

        private static CharacterReactionContext CreateDeathPressureOnly(
            CharacterReactionContextSourceKind sourceKind,
            RuntimeFrame frame,
            GameplayEntityId entityId,
            string reason,
            RuntimeTraceIdentity traceId,
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
                bodyPartId: 0,
                hitZoneId: 0,
                damageTypeId: 0,
                hitDirection: CharacterHitDirection.Unknown,
                reactionGroupId: 0);
        }

        private static CharacterReactionContext CreateHitContext(
            CharacterReactionHitSource source,
            CharacterReactionContextCompleteness completeness)
        {
            return new CharacterReactionContext(
                CharacterReactionContextSourceKind.Hit,
                completeness,
                source.Frame,
                source.EntityId,
                source.PreviousPressureBand,
                source.CurrentScatterPressureBand,
                source.PreviousPressure,
                source.CurrentScatterPressure,
                source.MaxScatterPressure,
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
                source.CurrentActionInterruptible,
                traceNumericId: source.TraceNumericId);
        }

        private static CharacterReactionContextBuildResult CompleteWithMissingHitDiagnostic(CharacterReactionContext context)
        {
            return new CharacterReactionContextBuildResult(
                context,
                context.Completeness,
                new[]
                {
                    CharacterActionDiagnostic.Warning(
                        CharacterActionDiagnosticCode.ReactionContextIncomplete,
                        "ReactionContext does not provide body part, hit zone, damage type, hit direction, or reaction group.")
                });
        }

        private static CharacterReactionContextBuildResult CompleteNoDiagnostics(CharacterReactionContext context)
        {
            return new CharacterReactionContextBuildResult(
                context,
                context.Completeness,
                Array.Empty<CharacterActionDiagnostic>());
        }

        [Flags]
        private enum MissingHitFieldFlags
        {
            None = 0,
            BodyPartId = 1 << 0,
            HitZoneId = 1 << 1,
            DamageTypeId = 1 << 2,
            HitDirection = 1 << 3,
            ImpactForce = 1 << 4,
            ReactionGroupId = 1 << 5
        }

        private static MissingHitFieldFlags GetMissingHitFields(CharacterReactionHitSource source)
        {
            MissingHitFieldFlags missing = MissingHitFieldFlags.None;
            if ((source.BodyPartId <= 0))
                missing |= MissingHitFieldFlags.BodyPartId;
            if ((source.HitZoneId <= 0))
                missing |= MissingHitFieldFlags.HitZoneId;
            if ((source.DamageTypeId <= 0))
                missing |= MissingHitFieldFlags.DamageTypeId;
            if (source.HitDirection == CharacterHitDirection.Unknown)
                missing |= MissingHitFieldFlags.HitDirection;
            if (!source.ImpactForce.HasValue)
                missing |= MissingHitFieldFlags.ImpactForce;
            if ((source.ReactionGroupId <= 0))
                missing |= MissingHitFieldFlags.ReactionGroupId;
            return missing;
        }

        private static string FormatMissingHitFields(MissingHitFieldFlags missing)
        {
            string fields = string.Empty;
            AppendMissingField(ref fields, missing, MissingHitFieldFlags.BodyPartId, nameof(CharacterReactionHitSource.BodyPartId));
            AppendMissingField(ref fields, missing, MissingHitFieldFlags.HitZoneId, nameof(CharacterReactionHitSource.HitZoneId));
            AppendMissingField(ref fields, missing, MissingHitFieldFlags.DamageTypeId, nameof(CharacterReactionHitSource.DamageTypeId));
            AppendMissingField(ref fields, missing, MissingHitFieldFlags.HitDirection, nameof(CharacterReactionHitSource.HitDirection));
            AppendMissingField(ref fields, missing, MissingHitFieldFlags.ImpactForce, nameof(CharacterReactionHitSource.ImpactForce));
            AppendMissingField(ref fields, missing, MissingHitFieldFlags.ReactionGroupId, nameof(CharacterReactionHitSource.ReactionGroupId));
            return fields;
        }

        private static void AppendMissingField(
            ref string fields,
            MissingHitFieldFlags missing,
            MissingHitFieldFlags flag,
            string fieldName)
        {
            if ((missing & flag) == 0)
                return;

            fields = string.IsNullOrEmpty(fields)
                ? fieldName
                : fields + ", " + fieldName;
        }
    }
}
