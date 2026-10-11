using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using MxFramework.UI;
using UnityEngine;

namespace MxFramework.Game.CharacterTest
{
    internal static partial class CharacterTestRuntimeAiLabBehaviorCardWorkspaceViewModelBuilder
    {
        private static void PopulateCharacterActionOptions(
            CharacterTestRuntimeAiLabBehaviorCardTimelineViewModel timeline,
            CharacterTestRuntimeAiPlannerDevSceneController controller)
        {
            if (timeline == null || controller == null
                || !controller.TryGetBehaviorRecipeCharacterActionCatalog(
                    out IReadOnlyList<MxFramework.CharacterAction.CharacterActionConfig> actions,
                    out long stableHash))
            {
                return;
            }
            if (timeline.CharacterActionCatalogHash == stableHash
                && timeline.CharacterActionOptions != null)
                return;
            timeline.CharacterActionOptions =
                BehaviorRecipeTriggerEventInspectorProjection.BuildCharacterActionOptions(actions);
            timeline.CharacterActionCatalogHash = stableHash;
        }

        private static string FormatStageRuntimeStatus(bool current, bool actionRunning, int actualDurationFrames)
        {
            if (current && actualDurationFrames >= 0 && actualDurationFrames <= RuntimeDurationCacheLimit)
                return actionRunning
                    ? CurrentActionDurationText[actualDurationFrames]
                    : CurrentStageDurationText[actualDurationFrames];
            if (current)
                return actionRunning ? "当前阶段 · 主动作执行中" : "当前阶段";
            return actionRunning ? "主动作仍在执行" : "等待进入";
        }

        private static void ConfigureActionGeometry(
            CharacterTestRuntimeAiLabLogicStageViewModel row,
            CharacterTestRuntimeAiPlannerDevSceneController controller)
        {
            row.ActionDurationFrames = 0;
            row.CharacterActionDisplayName = string.Empty;
            row.ActionProgressIndeterminate = row.OwnsPrimaryAction;
            switch (row.Kind)
            {
                case CharacterTestRuntimeAiLabCardStepKind.Logic:
                    if (row.DurationMode == CharacterTestRuntimeAiLabLogicDurationMode.Fixed)
                    {
                        row.ActionDurationFrames = Math.Max(1, row.DurationFrames);
                        row.ActionProgressIndeterminate = false;
                    }
                    break;
                case CharacterTestRuntimeAiLabCardStepKind.Wait:
                    row.ActionDurationFrames = Math.Max(1, row.WaitFrames);
                    row.ActionProgressIndeterminate = false;
                    break;
                case CharacterTestRuntimeAiLabCardStepKind.CharacterAction:
                    if (controller != null
                        && controller.TryResolveCharacterActionPresentation(
                            row.CharacterActionId,
                            out string displayName,
                            out int durationFrames))
                    {
                        row.CharacterActionDisplayName = displayName;
                        row.ActionDurationFrames = durationFrames;
                        row.ActionProgressIndeterminate = durationFrames <= 0;
                    }
                    break;
            }
        }

        /// <remarks>NoAlloc.</remarks>
        private static void UpdateActionProgress(
            CharacterTestRuntimeAiLabLogicStageViewModel row,
            int stageIndex,
            CharacterTestRuntimeAiLabLogicAgentStateSnapshot runtimeState,
            int runtimeFrame,
            CharacterTestRuntimeAiLabActiveActionProgress activeActionProgress)
        {
            row.ActionProgressFrames = -1;
            row.ActionInstanceId = 0L;
            row.ActionProgressOverrun = false;
            row.ActionRuntimeState = (int)CharacterTestRuntimeAiLabLogicActionState.None;
            if (row.Kind == CharacterTestRuntimeAiLabCardStepKind.Logic
                && row.Current
                && row.ActualDurationFrames >= 0)
            {
                row.ActionProgressFrames = row.ActualDurationFrames;
                row.ActionRuntimeState = (int)CharacterTestRuntimeAiLabLogicActionState.Running;
                row.ActionProgressOverrun = row.ActionDurationFrames > 0
                    && row.ActionProgressFrames > row.ActionDurationFrames;
                return;
            }
            bool currentWait = row.Kind == CharacterTestRuntimeAiLabCardStepKind.Wait
                && runtimeState?.CurrentStageIndex == stageIndex;
            if (runtimeState == null
                || (!currentWait && runtimeState.PrimaryActionStageIndex != stageIndex)
                || runtimeState.PrimaryActionState < (int)CharacterTestRuntimeAiLabLogicActionState.Running)
            {
                return;
            }

            row.ActionRuntimeState = runtimeState.PrimaryActionState;
            CharacterTestRuntimeAiLabLogicActionState state =
                (CharacterTestRuntimeAiLabLogicActionState)runtimeState.PrimaryActionState;
            var runtimeTime = new BehaviorRuntimeTimeSnapshot(
                runtimeFrame,
                runtimeState.CardStartedFrame,
                runtimeState.StageEnteredFrame,
                runtimeState.ActionEnteredFrame);
            int actionElapsedFrame = Math.Max(0, runtimeTime.GetActionElapsedFrame());
            bool matchingActiveAction = activeActionProgress.Available
                && (row.Kind == CharacterTestRuntimeAiLabCardStepKind.UseAbility
                    || activeActionProgress.ActionId == row.CharacterActionId);
            if (matchingActiveAction)
            {
                row.ActionInstanceId = activeActionProgress.InstanceId;
                if (activeActionProgress.Determinate)
                {
                    row.ActionDurationFrames = activeActionProgress.DurationFrames;
                    row.ActionProgressIndeterminate = false;
                }
            }

            if (state == CharacterTestRuntimeAiLabLogicActionState.Completed)
            {
                row.ActionProgressFrames = row.ActionProgressIndeterminate
                    ? actionElapsedFrame
                    : Math.Max(1, row.ActionDurationFrames);
                return;
            }
            if (state == CharacterTestRuntimeAiLabLogicActionState.Failed)
            {
                row.ActionProgressFrames = row.ActionDurationFrames > 0
                    ? Math.Min(row.ActionDurationFrames, actionElapsedFrame)
                    : actionElapsedFrame;
                return;
            }

            if (row.ActionProgressIndeterminate)
            {
                row.ActionProgressFrames = actionElapsedFrame;
                return;
            }

            int progressFrames = row.Kind == CharacterTestRuntimeAiLabCardStepKind.Wait
                ? actionElapsedFrame
                : matchingActiveAction
                    ? activeActionProgress.CoveredFrames
                    : row.ActionProgressIndeterminate
                        ? actionElapsedFrame
                        : 0;
            row.ActionProgressFrames = progressFrames;
            row.ActionProgressOverrun = row.ActionDurationFrames > 0
                && progressFrames > row.ActionDurationFrames;
        }

        /// <remarks>NoAlloc.</remarks>
        private static int ResolveDetectorElapsedFrames(
            bool active,
            bool triggered,
            CharacterTestRuntimeAiLabCompiledLogicDetector detector,
            CharacterTestRuntimeAiLabLogicAgentStateSnapshot runtimeState,
            int runtimeFrame)
        {
            if (detector == null || runtimeState == null)
                return -1;
            if (triggered && detector.WindowEndMode == CharacterTestRuntimeAiLabLogicDetectorWindowEndMode.Fixed)
                return Math.Max(1, detector.ActivationDurationFrames);
            if (!active)
                return -1;
            var runtimeTime = new BehaviorRuntimeTimeSnapshot(
                runtimeFrame,
                runtimeState.CardStartedFrame,
                runtimeState.StageEnteredFrame,
                runtimeState.ActionEnteredFrame);
            int scopeElapsedFrame = detector.Scope == CharacterTestRuntimeAiLabLogicDetectorScope.Card
                ? runtimeTime.GetCardElapsedFrame()
                : runtimeTime.GetStageElapsedFrame();
            if (scopeElapsedFrame < 0)
                return -1;
            return Math.Max(0, scopeElapsedFrame - detector.ActivationStartFrame);
        }

        private static int CountTransitionsForDetector(
            CharacterTestRuntimeAiLabCompiledLogicProgram program,
            int detectorIndex)
        {
            int count = 0;
            for (int i = 0; program != null && i < program.Transitions.Count; i++)
            {
                if (program.Transitions[i].DetectorIndex == detectorIndex)
                    count++;
            }
            return count;
        }

        private static int CountDraftTransitionsForDetector(
            CharacterTestRuntimeAiLabBehaviorCardLogicPayload payload,
            string detectorId)
        {
            int count = 0;
            for (int i = 0; payload != null && i < payload.Transitions.Count; i++)
            {
                if (string.Equals(
                        payload.Transitions[i]?.DetectorId,
                        detectorId,
                        StringComparison.Ordinal))
                {
                    count++;
                }
            }
            return count;
        }

        private static string[] CreateRuntimeDurationText(string prefix)
        {
            var cache = new string[RuntimeDurationCacheLimit + 1];
            for (int i = 0; i < cache.Length; i++)
                cache[i] = prefix + i.ToString(CultureInfo.InvariantCulture) + "f";
            return cache;
        }

        private static CharacterTestRuntimeAiLabLogicStageLayout FindLogicLayout(
            CharacterTestRuntimeAiLabBehaviorCardLogicPayload payload,
            string stageId)
        {
            IReadOnlyList<CharacterTestRuntimeAiLabLogicStageLayout> layouts = payload?.Layout?.Stages;
            for (int i = 0; layouts != null && i < layouts.Count; i++)
            {
                CharacterTestRuntimeAiLabLogicStageLayout layout = layouts[i];
                if (layout != null
                    && string.Equals(layout.StageId, stageId, StringComparison.Ordinal))
                    return layout;
            }
            return null;
        }

    }
}
