using System;
using System.Globalization;
using System.Linq;
using MxFramework.Game.CharacterTest;
using MxFramework.Gameplay.Triggers;
using MxFramework.UI;
using UnityEngine;
using Object = UnityEngine.Object;

namespace MxFramework.Tests.Gameplay.TriggerAutomation
{
    internal sealed class TriggerTestRuntimeAiLabTrainingDummyCopyFlowArguments :
        ITriggerTestFunctionArguments
    {
        internal static readonly TriggerTestRuntimeAiLabTrainingDummyCopyFlowArguments Instance =
            new TriggerTestRuntimeAiLabTrainingDummyCopyFlowArguments();

        private TriggerTestRuntimeAiLabTrainingDummyCopyFlowArguments()
        {
        }
    }

    /// <summary>
    /// Selects an existing training-dummy copy through the live FairyGUI command
    /// route, starts the test, and probes the effective runtime loadout.
    /// </summary>
    /// <remarks>LowFreqAlloc: explicit manual Trigger only.</remarks>
    internal sealed class TriggerTestRuntimeAiLabTrainingDummyCopyFlowFunction :
        TriggerTestFunction<TriggerTestRuntimeAiLabTrainingDummyCopyFlowArguments>
    {
        internal static readonly TriggerTestFunctionId Id =
            new TriggerTestFunctionId(
                "mx.trigger.test.function.character-test.runtime-ai-lab-training-dummy-copy-flow");

        public override TriggerTestFunctionId FunctionId => Id;

        public override TriggerTestFunctionCapability Capability =>
            TriggerTestFunctionCapability.UnityAdapter;

        protected override TriggerTestOperationResult ExecuteTyped(
            in TriggerTestFunctionContext context,
            TriggerTestRuntimeAiLabTrainingDummyCopyFlowArguments arguments)
        {
            if (!Application.isPlaying)
                return TriggerTestOperationResult.Failed("Unity is not in Play Mode.");
            if (!context.Fixture.Services.TryGet(
                    out TriggerTestRuntimeAiLabTrainingDummyCopyFlowProbe probe))
            {
                return TriggerTestOperationResult.Failed(
                    "Training-dummy copy flow probe is unavailable.");
            }
            probe.InvocationCount++;

            if (!TriggerTestRuntimeAiLabCommandProbe.TryResolveController(
                    out CharacterTestRuntimeAiPlannerDevSceneController controller,
                    out string diagnostic))
                return Fail(probe, "resolve-controller", diagnostic);
            if (controller.Phase != CharacterTestRuntimeAiPlannerDevScenePhase.Setup)
                return Fail(probe, "require-setup", "该 Trigger 必须从实验设置阶段开始。");
            probe.Controller = controller;

            CharacterTestArenaSlotConfig slot = controller.MatchConfig.TryGetSlot(
                controller.SelectedSetupTeamId,
                controller.SelectedSetupSlotIndex);
            CharacterTestArenaBehaviorGroupDraft copy = controller.MatchConfig.BehaviorGroups.Drafts
                .FirstOrDefault(draft =>
                    !string.Equals(
                        draft.StableId,
                        CharacterTestArenaBehaviorGroupDraftStore.TrainingDummyStableId,
                        StringComparison.Ordinal)
                    && string.Equals(draft.DisplayName, "木桩 副本", StringComparison.Ordinal)
                    && HasTrainingDummyAuthoredBehaviors(draft));
            if (slot == null || copy == null)
            {
                return Fail(
                    probe,
                    "resolve-copy",
                    "当前项目没有保留作者明确配置行为的“木桩 副本”或可编辑位置。");
            }

            CharacterTestFairyGuiRuntimeAiPlannerDevPanelHost panelHost =
                Object.FindAnyObjectByType<CharacterTestFairyGuiRuntimeAiPlannerDevPanelHost>();
            CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandSink sink = panelHost?.CommandSink;
            if (sink == null)
                return Fail(probe, "resolve-panel", "Runtime AI Lab FairyGUI command sink is unavailable.");
            sink.Enqueue(new MxUiCommand(
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.ViewId,
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds
                    .SelectSetupBehaviorGroupPrefix
                + copy.DraftId.ToString(CultureInfo.InvariantCulture),
                null));

            slot = controller.MatchConfig.TryGetSlot(
                controller.SelectedSetupTeamId,
                controller.SelectedSetupSlotIndex);
            if (slot == null
                || slot.BehaviorGroupDraftId != copy.DraftId)
            {
                return Fail(probe, "select-copy", sink.LastStatusMessage);
            }
            probe.CopyStableGroupId = copy.StableId;
            probe.CopyDraftId = copy.DraftId;
            probe.ExpectedLoadoutId = CharacterTestArenaBehaviorGroupDraftStore.ToSessionLoadoutId(
                copy.DraftId);
            if (!HasTrainingDummyAuthoredBehaviors(copy))
                return Fail(probe, "verify-authored-behaviors", "木桩副本没有保留作者明确配置的行为。");
            if (!controller.DraftWorkspace.TryGetGroupForSlot(
                    controller.SelectedSetupTeamId,
                    controller.SelectedSetupSlotIndex,
                    out CharacterTestRuntimeAiLabBehaviorGroupDraft workspaceCopy)
                || !string.Equals(
                    workspaceCopy.PublishedStableId,
                    copy.StableId,
                    StringComparison.Ordinal))
            {
                return Fail(probe, "verify-workspace", "当前实验草稿没有采用选中的木桩副本。");
            }
            if (!controller.SaveBehaviorRecipeAndRun(
                    selectedEntryIndex: 0,
                    out CharacterTestRuntimeAiLabBehaviorRecipeSaveRunResult saveRun))
                return Fail(probe, "save-and-run", saveRun.Message);
            CharacterTestRuntimeAiPlannerDevSceneSnapshot snapshot = controller.CaptureSnapshot();
            if (snapshot.Phase != CharacterTestRuntimeAiPlannerDevScenePhase.Running
                || snapshot.Paused
                || snapshot.SelectedFrame == null)
            {
                return Fail(probe, "start-test", "保存并运行后没有立即进入连续播放。");
            }
            probe.SaveRunIdentityMatched = saveRun.CandidateAuthorityHash != 0L
                && saveRun.CandidateAuthorityHash == saveRun.InstalledAuthorityHash
                && saveRun.SelectedEntityId != 0
                && saveRun.RuntimeProfileSlot >= 0;
            if (!probe.SaveRunIdentityMatched)
                return Fail(probe, "verify-save-run-identity", "保存草稿与运行 Overlay 身份不一致。");
            probe.EffectiveLoadoutMatched = snapshot.SelectedFrame.Agents.Any(agent =>
                agent.RuntimeAiBehaviorLoadoutId == probe.ExpectedLoadoutId);
            if (!probe.EffectiveLoadoutMatched)
            {
                return Fail(
                    probe,
                    "verify-runtime-loadout",
                    "运行时首帧没有使用木桩副本对应的行为组 loadout。");
            }

            probe.FinalStage = "complete";
            Debug.Log(
                "[TriggerTest][RuntimeAiLabTrainingDummyCopy][PASS] " +
                probe.CopyStableGroupId +
                " loadout=" + probe.ExpectedLoadoutId +
                " operation=" + saveRun.OperationId +
                " authority=" + saveRun.InstalledAuthorityHash);
            return TriggerTestOperationResult.Succeeded(
                "已选择木桩副本，并通过保存并运行事务验证连续播放与 Overlay 身份。");
        }

        private static bool HasTrainingDummyAuthoredBehaviors(
            CharacterTestArenaBehaviorGroupDraft copy)
        {
            if (copy?.Profile?.Entries == null || copy.Profile.Entries.Count != 2)
                return false;
            string[] behaviors = copy.Profile.Entries.Select(entry => entry.BehaviorId).ToArray();
            return behaviors.SequenceEqual(new[]
            {
                CharacterTestRuntimeAiPlannerBehaviorCompositionCatalog.FoundationApproachAndAttack,
                CharacterTestRuntimeAiPlannerBehaviorCompositionCatalog.FoundationIdle
            });
        }

        private static TriggerTestOperationResult Fail(
            TriggerTestRuntimeAiLabTrainingDummyCopyFlowProbe probe,
            string stage,
            string diagnostic)
        {
            probe.FinalStage = stage;
            return TriggerTestOperationResult.Failed(
                string.IsNullOrWhiteSpace(diagnostic) ? stage : stage + ": " + diagnostic);
        }
    }

    internal sealed class TriggerTestRuntimeAiLabTrainingDummyCopyFlowProbe
    {
        internal CharacterTestRuntimeAiPlannerDevSceneController Controller;
        internal int InvocationCount;
        internal int CopyDraftId;
        internal int ExpectedLoadoutId;
        internal string CopyStableGroupId = string.Empty;
        internal bool EffectiveLoadoutMatched;
        internal bool SaveRunIdentityMatched;
        internal string FinalStage = string.Empty;
    }

    internal sealed class TriggerTestRuntimeAiLabTrainingDummyCopyFlowAssertion :
        ITriggerTestAssertion
    {
        public string AssertionId => "training-dummy-copy-flow-completed";

        public TriggerTestOperationResult Evaluate(
            in TriggerTestAssertionContext context)
        {
            if (!context.Fixture.Services.TryGet(
                    out TriggerTestRuntimeAiLabTrainingDummyCopyFlowProbe probe))
            {
                return TriggerTestOperationResult.Failed(
                    "Training-dummy copy flow probe is unavailable.");
            }
            CharacterTestArenaSlotConfig slot = probe.Controller?.MatchConfig?.TryGetSlot(
                probe.Controller.SelectedSetupTeamId,
                probe.Controller.SelectedSetupSlotIndex);
            if (probe.InvocationCount != 1
                || !string.Equals(probe.FinalStage, "complete", StringComparison.Ordinal)
                || slot == null
                || slot.BehaviorGroupDraftId != probe.CopyDraftId
                || !probe.EffectiveLoadoutMatched
                || !probe.SaveRunIdentityMatched)
            {
                return TriggerTestOperationResult.Failed(
                    "木桩副本流程没有完整保留选择并进入实际运行；最后阶段：" +
                    probe.FinalStage);
            }
            return TriggerTestOperationResult.Succeeded(
                "已从独立 Probe 验证木桩副本、连续播放、实际 loadout 与 Overlay authority 一致。");
        }
    }
}
