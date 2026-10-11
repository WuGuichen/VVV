using System;
using System.Collections.Generic;

namespace MxFramework.Game.CharacterTest
{
    /// <summary>
    /// Verifies that an authored card still performs the responsibility promised by its core slot.
    /// </summary>
    /// <remarks>
    /// LowFreqAlloc: used only while editing, loading, migrating, or validating Runtime AI Lab
    /// drafts. Runtime AI Planner Tick consumes the already compiled program and never calls this
    /// policy.
    /// </remarks>
    internal static class CharacterTestRuntimeAiLabBehaviorCoreSlotCardPolicy
    {
        public static bool Fulfills(
            CharacterTestRuntimeAiPlannerBehaviorCoreSlotKind slotKind,
            CharacterTestRuntimeAiLabBehaviorCardDraft card,
            out string message)
        {
            if (IsNoOpPlaceholderForSlot(slotKind, card?.PublishedStableId))
            {
                CharacterTestRuntimeAiLabBehaviorCardLogicCompileResult placeholderCompile =
                    CharacterTestRuntimeAiLabBehaviorCardLogicCompiler.Compile(card.LogicPayload);
                if (placeholderCompile.Success && placeholderCompile.Program != null)
                {
                    message = string.Empty;
                    return true;
                }

                message = "基础槽占位行为无法编译。";
                return false;
            }
            return Fulfills(slotKind, card?.LogicPayload, out message);
        }

        public static bool Fulfills(
            CharacterTestRuntimeAiPlannerBehaviorCoreSlotKind slotKind,
            CharacterTestRuntimeAiLabBehaviorCardLogicPayload payload,
            out string message)
        {
            if (!CharacterTestRuntimeAiPlannerBehaviorCoreSlotPolicy.IsCore(slotKind))
            {
                message = string.Empty;
                return true;
            }

            CharacterTestRuntimeAiLabBehaviorCardLogicCompileResult compile =
                CharacterTestRuntimeAiLabBehaviorCardLogicCompiler.Compile(payload);
            if (!compile.Success || compile.Program == null)
            {
                message = "基础槽“"
                    + CharacterTestRuntimeAiPlannerBehaviorCoreSlotPolicy.GetDisplayName(slotKind)
                    + "”的行为卡无法编译。";
                return false;
            }

            // Target acquisition is supplied by the behavior definition and planner facts. Its card
            // may therefore remain logic-only. The persistent free fallback is represented by v5 as
            // an UntilEvent Logic action, while a finite fallback remains an explicit Wait action.
            if (slotKind == CharacterTestRuntimeAiPlannerBehaviorCoreSlotKind.Targeting)
            {
                message = string.Empty;
                return true;
            }

            IReadOnlyList<CharacterTestRuntimeAiLabCompiledLogicStage> stages =
                compile.Program.Stages;
            for (int i = 0; i < stages.Count; i++)
            {
                if (StageFulfills(slotKind, stages[i]))
                {
                    message = string.Empty;
                    return true;
                }
            }

            // v7 represents a persistent fallback explicitly: a conditionless WaitForCondition
            // marker freezes only the recipe playhead while the world and planner continue running.
            if (slotKind == CharacterTestRuntimeAiPlannerBehaviorCoreSlotKind.Fallback
                && IsPersistentV7Fallback(payload))
            {
                message = string.Empty;
                return true;
            }

            message = "基础槽“"
                + CharacterTestRuntimeAiPlannerBehaviorCoreSlotPolicy.GetDisplayName(slotKind)
                + "”缺少可执行的"
                + RequiredActionLabel(slotKind)
                + "步骤。";
            return false;
        }

        private static bool IsPersistentV7Fallback(
            CharacterTestRuntimeAiLabBehaviorCardLogicPayload payload)
        {
            if (payload == null
                || payload.BehaviorRecipeVersion < 7
                || payload.Timelines.Count == 0)
            {
                return false;
            }

            for (int timelineIndex = 0; timelineIndex < payload.Timelines.Count; timelineIndex++)
            {
                CharacterTestRuntimeAiLabBehaviorRecipeTimeline timeline =
                    payload.Timelines[timelineIndex];
                if (timeline == null || timeline.Events.Count != 1)
                    return false;
                CharacterTestRuntimeAiLabBehaviorRecipeEvent evt = timeline.Events[0];
                if (evt?.Operation?.Kind != CharacterTestRuntimeAiLabBehaviorRecipeEventOperationKind.WaitForCondition
                    || evt.ConditionRoot != null
                    || evt.DurationFrames != 1)
                    return false;
            }
            return payload.LegacySharedEvents.Count == 0;
        }

        /// <summary>
        /// Draft-time guard that preserves the responsibility promised by a core slot while still
        /// allowing an editor transaction to contain ordinary validation diagnostics (for example,
        /// a newly added Timeline that intentionally has no incoming Trigger yet). Save/publish
        /// continues to use <see cref="Fulfills"/> and the full compiler.
        /// </summary>
        internal static bool HasRequiredActionShape(
            CharacterTestRuntimeAiPlannerBehaviorCoreSlotKind slotKind,
            CharacterTestRuntimeAiLabBehaviorCardLogicPayload payload,
            out string message)
        {
            if (!CharacterTestRuntimeAiPlannerBehaviorCoreSlotPolicy.IsCore(slotKind)
                || slotKind == CharacterTestRuntimeAiPlannerBehaviorCoreSlotKind.Targeting)
            {
                message = string.Empty;
                return true;
            }

            for (int i = 0; payload != null && i < payload.Timelines.Count; i++)
            {
                CharacterTestRuntimeAiLabBehaviorRecipePrimaryActionEvent action =
                    payload.Timelines[i]?.PrimaryActionEvent;
                if (action != null && KindFulfills(slotKind, action.Kind))
                {
                    message = string.Empty;
                    return true;
                }
            }
            for (int i = 0; payload != null && i < payload.Stages.Count; i++)
            {
                CharacterTestRuntimeAiLabLogicStage stage = payload.Stages[i];
                if (stage != null && KindFulfills(slotKind, stage.Kind))
                {
                    message = string.Empty;
                    return true;
                }
            }

            message = "基础槽“"
                + CharacterTestRuntimeAiPlannerBehaviorCoreSlotPolicy.GetDisplayName(slotKind)
                + "”缺少可执行的"
                + RequiredActionLabel(slotKind)
                + "步骤。";
            return false;
        }

        private static bool StageFulfills(
            CharacterTestRuntimeAiPlannerBehaviorCoreSlotKind slotKind,
            CharacterTestRuntimeAiLabCompiledLogicStage stage)
        {
            if (stage == null)
                return false;

            if (slotKind == CharacterTestRuntimeAiPlannerBehaviorCoreSlotKind.Fallback
                && stage.Kind == CharacterTestRuntimeAiLabCardStepKind.Logic
                && stage.DurationMode == CharacterTestRuntimeAiLabLogicDurationMode.UntilEvent)
            {
                return true;
            }

            return KindFulfills(slotKind, stage.Kind);
        }

        private static bool KindFulfills(
            CharacterTestRuntimeAiPlannerBehaviorCoreSlotKind slotKind,
            CharacterTestRuntimeAiLabCardStepKind kind)
        {
            switch (slotKind)
            {
                case CharacterTestRuntimeAiPlannerBehaviorCoreSlotKind.EngageOrPressure:
                    return kind == CharacterTestRuntimeAiLabCardStepKind.MoveToTarget
                        || kind == CharacterTestRuntimeAiLabCardStepKind.UseAbility
                        || kind == CharacterTestRuntimeAiLabCardStepKind.CharacterAction;
                case CharacterTestRuntimeAiPlannerBehaviorCoreSlotKind.Damage:
                    return kind == CharacterTestRuntimeAiLabCardStepKind.UseAbility
                        || kind == CharacterTestRuntimeAiLabCardStepKind.CharacterAction;
                case CharacterTestRuntimeAiPlannerBehaviorCoreSlotKind.Fallback:
                    return kind == CharacterTestRuntimeAiLabCardStepKind.Wait;
                default:
                    return true;
            }
        }

        private static bool IsMovement(CharacterTestRuntimeAiPlannerActionKind actionKind)
        {
            return actionKind == CharacterTestRuntimeAiPlannerActionKind.MoveToTarget
                || actionKind == CharacterTestRuntimeAiPlannerActionKind.Strafe
                || actionKind == CharacterTestRuntimeAiPlannerActionKind.Retreat;
        }

        private static bool IsNoOpPlaceholderForSlot(
            CharacterTestRuntimeAiPlannerBehaviorCoreSlotKind slotKind,
            string behaviorId)
        {
            switch (slotKind)
            {
                case CharacterTestRuntimeAiPlannerBehaviorCoreSlotKind.EngageOrPressure:
                    return string.Equals(
                        behaviorId,
                        CharacterTestRuntimeAiPlannerBehaviorCompositionCatalog.FoundationIdle,
                        StringComparison.Ordinal);
                case CharacterTestRuntimeAiPlannerBehaviorCoreSlotKind.Damage:
                    return string.Equals(
                        behaviorId,
                        CharacterTestRuntimeAiPlannerBehaviorCompositionCatalog.FoundationIdle,
                        StringComparison.Ordinal);
                default:
                    return false;
            }
        }

        private static string RequiredActionLabel(
            CharacterTestRuntimeAiPlannerBehaviorCoreSlotKind slotKind)
        {
            switch (slotKind)
            {
                case CharacterTestRuntimeAiPlannerBehaviorCoreSlotKind.EngageOrPressure:
                    return "接战/压制";
                case CharacterTestRuntimeAiPlannerBehaviorCoreSlotKind.Damage:
                    return "输出";
                case CharacterTestRuntimeAiPlannerBehaviorCoreSlotKind.Fallback:
                    return "等待/保持";
                default:
                    return "职责";
            }
        }
    }
}
