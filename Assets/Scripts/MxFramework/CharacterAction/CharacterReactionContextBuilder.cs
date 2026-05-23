using System;
using MxFramework.Gameplay;
using MxFramework.Runtime;

namespace MxFramework.CharacterAction
{
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
