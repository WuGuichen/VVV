using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using MxFramework.Animation;
using MxFramework.Audio;
using MxFramework.CharacterAction;
using MxFramework.CharacterRuntimeSpawn.Unity;
using MxFramework.Combat.Core;
using MxFramework.Combat.GameplayBridge;
using MxFramework.Combat.Physics;
using MxFramework.Combat.Projectiles;
using MxFramework.Combat.Shields;
using MxFramework.Core.Math;
using MxFramework.Game.CharacterTest;
using MxFramework.Gameplay;
using MxFramework.Network;
using MxFramework.Resources;
using MxFramework.Runtime;
using MxFramework.UI;
using MxFramework.UI.FairyGui;
using MxFramework.Vfx;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace MxFramework.Tests.Game.CharacterTest
{
    public sealed partial class CharacterTestRuntimeAiPlannerDevTests
    {

        [Test]
        public void RuntimeAiLabViewModel_NullControllerShowsUnavailableState()
        {
            CharacterTestFairyGuiRuntimeAiPlannerDevPanelViewModel viewModel =
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelViewModelBuilder.Build(
                    controller: null,
                    commandStatus: null);

            Assert.AreEqual("运行时智能实验室", viewModel.Title);
            Assert.AreEqual("不可用", viewModel.PhaseText);
            Assert.AreEqual("当前没有测试会话。", viewModel.FrameSummary);
            Assert.IsFalse(viewModel.CanStart);
            Assert.IsFalse(viewModel.CanStep);
            StringAssert.Contains("不可用", viewModel.StatusText);
            StringAssert.Contains("暂停并推进一帧", viewModel.PreviewInstructionsText);
            StringAssert.Contains("场景标记不可用", viewModel.PreviewStatusText);
            StringAssert.Contains("CharacterTestRuntimeAiLabScene", viewModel.DiagnosticsText);
        }

        [Test]
        public void RuntimeAiLabViewModel_SetupStateAllowsStartOnly()
        {
            var host = new GameObject("Runtime AI Lab ViewModel Test");
            try
            {
                CharacterTestRuntimeAiPlannerDevSceneController controller =
                    host.AddComponent<CharacterTestRuntimeAiPlannerDevSceneController>();
                controller.PrepareInitialSetupState();

                Assert.IsTrue(controller.UsesTransientDraftPersistence);
                StringAssert.StartsWith(
                    Path.GetFullPath(Path.GetTempPath()),
                    controller.DraftPersistenceRoot);
                Assert.AreNotEqual(
                    Path.GetFullPath(Path.Combine("UserSettings", "RuntimeAiLab", "Drafts")),
                    controller.DraftPersistenceRoot);

                CharacterTestFairyGuiRuntimeAiPlannerDevPanelViewModel viewModel =
                    CharacterTestFairyGuiRuntimeAiPlannerDevPanelViewModelBuilder.Build(
                        controller,
                        commandStatus: null);

                Assert.AreEqual("运行时智能实验室", viewModel.Title);
                Assert.IsTrue(viewModel.CanStart);
                Assert.IsTrue(viewModel.CanReset);
                Assert.IsFalse(viewModel.CanPauseResume);
                Assert.IsFalse(viewModel.CanStep);
                Assert.IsFalse(viewModel.CanToggleEditGate);
                Assert.IsTrue(viewModel.CanTogglePreviewOverlay);
                Assert.IsTrue(viewModel.PreviewOverlaySelected);
                Assert.IsTrue(viewModel.CanToggleDetails);
                Assert.IsTrue(viewModel.CanEditSetupPlacement);
                Assert.IsTrue(viewModel.CanFitSetupView);
                Assert.IsTrue(viewModel.SetupSnapSelected);
                Assert.IsFalse(viewModel.DetailsExpanded);
                Assert.IsFalse(viewModel.BehaviorCardRuntimeControl.Available);
                Assert.IsNull(viewModel.TimelineProjection);
                Assert.IsFalse(viewModel.ShowWorkbench);
                Assert.AreEqual(
                    CharacterTestRuntimeAiPlannerDevPresentationContext.Setup,
                    viewModel.PresentationContext);
                Assert.IsTrue(viewModel.ShowSetupToolbar);
                Assert.IsFalse(viewModel.ShowLiveToolbar);
                Assert.IsFalse(viewModel.ShowPausedToolbar);
                Assert.IsFalse(viewModel.ShowTimeline);
                Assert.AreEqual(
                    CharacterTestRuntimeAiLabWorkspaceMode.Setup,
                    viewModel.WorkspaceMode);
                Assert.IsTrue(viewModel.Workspace.ShowSetup);
                Assert.IsFalse(viewModel.Workspace.ShowBehaviorGroup);
                Assert.IsFalse(viewModel.Workspace.ShowBehaviorCard);
                Assert.IsFalse(viewModel.Workspace.ShowDiagnostics);
                Assert.IsNull(viewModel.TimelineProjection);
                Assert.AreEqual(6, viewModel.SetupSlotRows.Length);
                Assert.IsTrue(viewModel.SetupSlotRows[0].Selected);
                Assert.GreaterOrEqual(viewModel.SetupCharacterOptions.Length, 1);
                Assert.IsTrue(viewModel.SetupCharacterOptions[0].Selected);
                Assert.AreEqual(
                    CharacterTestCharacterDefinitionRegistry.DefaultPlayableCharacterId,
                    viewModel.SetupCharacterOptions[0].CharacterId);
                Assert.AreEqual(
                    "Codex Flow Test",
                    viewModel.SetupCharacterOptions[0].DisplayName);
                Assert.AreEqual("C", viewModel.SetupCharacterOptions[0].MonogramText);
                Assert.GreaterOrEqual(viewModel.SetupBehaviorGroupOptions.Length, 1);
                Assert.IsTrue(viewModel.SetupBehaviorGroupOptions[0].Selected);
                Assert.IsTrue(viewModel.SetupBehaviorGroupOptions[0].Available);
                Assert.IsFalse(string.IsNullOrWhiteSpace(
                    viewModel.SetupBehaviorGroupOptions[0].GroupDraftId));
                Assert.IsFalse(viewModel.SetupBehaviorGroupOptions[0].IsLocalDraft);
                Assert.Greater(viewModel.SetupBehaviorGroupOptions[0].ArenaBehaviorGroupDraftId, 0);
                Assert.AreEqual(
                    string.Empty,
                    viewModel.SetupBehaviorGroupOptions[0].UnavailableReasonText);
                Assert.AreEqual("行为编辑", viewModel.ToggleDetailsLabel);
                StringAssert.Contains("WASD", viewModel.SetupInstructionsText);
                StringAssert.Contains("适配视野", viewModel.SetupInstructionsText);
                StringAssert.Contains("拖动角色预览", viewModel.SetupInstructionsText);
                StringAssert.Contains("点击地面", viewModel.SetupInstructionsText);
                StringAssert.Contains("Shift+右键", viewModel.SetupInstructionsText);
                Assert.AreEqual("适配视野", viewModel.FitSetupViewLabel);
                Assert.AreEqual(string.Empty, viewModel.SetupSelectedSlotText);
                StringAssert.Contains("Codex Flow Test", viewModel.SetupCharacterText);
                Assert.AreEqual(
                    viewModel.SetupBehaviorGroupOptions[0].DisplayName,
                    viewModel.SetupBehaviorGroupText);
                StringAssert.Contains("位置", viewModel.SetupPositionText);
                Assert.AreEqual(string.Empty, viewModel.SetupValidationText);
                Assert.AreEqual("● 配置有效", viewModel.SetupTopValidationText);
                Assert.AreEqual("队伍 1      1/3", viewModel.SetupTeam1CountText);
                Assert.AreEqual("队伍 2      1/3", viewModel.SetupTeam2CountText);
                Assert.AreEqual("● 配置有效", viewModel.SetupTeam1ValidationText);
                Assert.AreEqual("● 配置有效", viewModel.SetupTeam2ValidationText);
                Assert.AreEqual("T1·1", viewModel.SetupAgentBadgeText);
                Assert.AreEqual("● 已准备", viewModel.SetupReadyTitleText);
                Assert.AreEqual(string.Empty, viewModel.SetupReadyDescriptionText);
                Assert.AreEqual("Arena · 2 智能体 · 900 帧", viewModel.SetupSceneText);
                Assert.AreEqual("智能体  2", viewModel.SetupAgentCountText);
                Assert.AreEqual("帧上限  900", viewModel.SetupFrameLimitStatusText);
                StringAssert.Contains("配置", viewModel.PhaseText);
                StringAssert.Contains("尚未捕获运行时帧", viewModel.FrameSummary);
                StringAssert.Contains("场景标记已开启", viewModel.PreviewStatusText);
                Assert.AreEqual(string.Empty, viewModel.TimelineText);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void RuntimeAiLabViewModel_SetupPickerIncludesSelectedLocalGroupDraft()
        {
            var host = new GameObject("Runtime AI Lab Local Group Picker Test");
            try
            {
                CharacterTestRuntimeAiPlannerDevSceneController controller =
                    host.AddComponent<CharacterTestRuntimeAiPlannerDevSceneController>();
                controller.PrepareInitialSetupState();
                Assert.IsTrue(controller.DraftWorkspace.TryCreateLocalGroupCopyForSlot(
                    1,
                    0,
                    out CharacterTestRuntimeAiLabBehaviorGroupDraft copy,
                    out string message), message);

                CharacterTestFairyGuiRuntimeAiPlannerDevPanelViewModel viewModel =
                    CharacterTestFairyGuiRuntimeAiPlannerDevPanelViewModelBuilder.Build(
                        controller,
                        commandStatus: null);

                CharacterTestRuntimeAiLabSetupBehaviorGroupOptionViewModel option =
                    viewModel.SetupBehaviorGroupOptions.FirstOrDefault(candidate =>
                        candidate != null
                        && string.Equals(candidate.GroupDraftId, copy.GroupDraftId, StringComparison.Ordinal));
                Assert.NotNull(option);
                Assert.IsTrue(option.IsLocalDraft);
                Assert.IsTrue(option.Selected);
                StringAssert.Contains("本地草稿", option.DisplayName);
                StringAssert.Contains("本地草稿", viewModel.SetupBehaviorGroupText);
                Assert.IsTrue(viewModel.SetupBehaviorGroupEditorIsLocalDraft);
                Assert.AreEqual("确认为正式行为组", viewModel.SetupBehaviorGroupEditorSaveLabel);
            }
            finally
            {
                Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void RuntimeAiLabViewModel_InvalidSetupProjectsBlockingChineseFeedback()
        {
            var host = new GameObject("Runtime AI Lab Invalid Setup ViewModel Test");
            try
            {
                CharacterTestRuntimeAiPlannerDevSceneController controller =
                    host.AddComponent<CharacterTestRuntimeAiPlannerDevSceneController>();
                controller.PrepareInitialSetupState();
                Assert.IsTrue(controller.SelectSetupSpawnSlot(CharacterTestArenaMatchConfig.Team2Id, 0));
                Assert.IsTrue(controller.ToggleSelectedSetupSlotEnabled());

                CharacterTestFairyGuiRuntimeAiPlannerDevPanelViewModel viewModel =
                    CharacterTestFairyGuiRuntimeAiPlannerDevPanelViewModelBuilder.Build(
                        controller,
                        commandStatus: null);

                Assert.IsFalse(viewModel.CanStart);
                Assert.AreEqual("● 配置待修复", viewModel.SetupTopValidationText);
                Assert.AreEqual("队伍 2      0/3", viewModel.SetupTeam2CountText);
                Assert.AreEqual("● 需要检查", viewModel.SetupTeam2ValidationText);
                Assert.AreEqual("● 待修复", viewModel.SetupReadyTitleText);
                StringAssert.Contains("至少各启用一个", viewModel.SetupReadyDescriptionText);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void RuntimeAiLabViewModel_InvalidBehaviorCardBlocksStartWithActionableFeedback()
        {
            var host = new GameObject("Runtime AI Lab Invalid Behavior Card ViewModel Test");
            try
            {
                CharacterTestRuntimeAiPlannerDevSceneController controller =
                    host.AddComponent<CharacterTestRuntimeAiPlannerDevSceneController>();
                controller.PrepareInitialSetupState();
                Assert.IsTrue(controller.DraftWorkspace.TryGetGroupForSlot(
                    CharacterTestArenaMatchConfig.Team1Id,
                    0,
                    out CharacterTestRuntimeAiLabBehaviorGroupDraft group));
                Assert.Greater(group.Entries.Count, 0);
                Assert.IsTrue(controller.DraftWorkspace.Cards.TryGetValue(
                    group.Entries[0].CardDraftId,
                    out CharacterTestRuntimeAiLabBehaviorCardDraft card));
                card.DisplayName = "缺少条件的测试卡";
                card.LogicPayload.Detectors.Add(new CharacterTestRuntimeAiLabLogicDetector
                {
                    DetectorId = "detector-missing-condition",
                    Title = "缺少条件",
                    Scope = CharacterTestRuntimeAiLabLogicDetectorScope.Card,
                    Trigger = CharacterTestRuntimeAiLabLogicDetectorTrigger.ConditionEdge,
                    WindowEndMode = CharacterTestRuntimeAiLabLogicDetectorWindowEndMode.Infinite
                });

                CharacterTestFairyGuiRuntimeAiPlannerDevPanelViewModel viewModel =
                    CharacterTestFairyGuiRuntimeAiPlannerDevPanelViewModelBuilder.Build(
                        controller,
                        commandStatus: null);

                Assert.IsFalse(viewModel.CanStart);
                Assert.AreEqual("● 配置待修复", viewModel.SetupTopValidationText);
                Assert.AreEqual("● 待修复", viewModel.SetupReadyTitleText);
                StringAssert.Contains("缺少条件的测试卡", viewModel.SetupValidationText);
                StringAssert.Contains("缺少触发条件", viewModel.SetupValidationText);
                StringAssert.Contains("$.detectors", viewModel.SetupValidationText);
                Assert.AreEqual(viewModel.SetupValidationText, viewModel.SetupReadyDescriptionText);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void RuntimeAiLabSessionStart_BlankBehaviorGroupIsAllowed()
        {
            var host = new GameObject("Runtime AI Lab Blank Session Start Test");
            try
            {
                CharacterTestRuntimeAiPlannerDevSceneController controller =
                    host.AddComponent<CharacterTestRuntimeAiPlannerDevSceneController>();
                controller.PrepareInitialSetupState();
                MethodInfo validate = typeof(CharacterTestRuntimeAiPlannerDevSceneController).GetMethod(
                    "TryValidateSessionStartConfiguration",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.That(validate, Is.Not.Null);

                var arguments = new object[] { null };
                bool allowed = (bool)validate.Invoke(controller, arguments);

                Assert.IsTrue(allowed, arguments[0] as string);
                Assert.AreEqual(string.Empty, arguments[0] as string);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void RuntimeAiLabSessionStartRecovery_PreservesFailureStatus()
        {
            var host = new GameObject("Runtime AI Lab Session Start Recovery Test");
            try
            {
                CharacterTestRuntimeAiPlannerDevSceneController controller =
                    host.AddComponent<CharacterTestRuntimeAiPlannerDevSceneController>();
                controller.PrepareInitialSetupState();
                Type controllerType = typeof(CharacterTestRuntimeAiPlannerDevSceneController);
                MethodInfo capture = controllerType.GetMethod(
                    "CaptureSessionStartRecoverySnapshot",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                MethodInfo recover = controllerType.GetMethod(
                    "RecoverFailedSessionStart",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                FieldInfo status = controllerType.GetField(
                    "_statusMessage",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.That(capture, Is.Not.Null);
                Assert.That(recover, Is.Not.Null);
                Assert.That(status, Is.Not.Null);

                object snapshot = capture.Invoke(controller, null);
                const string failureMessage = "行为卡“测试卡”无法用于测试：缺少触发条件";
                status.SetValue(controller, failureMessage);

                recover.Invoke(controller, new[] { snapshot });

                StringAssert.StartsWith(failureMessage, controller.StatusMessage);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void RuntimeAiLabViewModel_BehaviorGroupProjectsEveryRowForNativeList()
        {
            var host = new GameObject("Runtime AI Lab Native Workbench List Test");
            try
            {
                CharacterTestRuntimeAiPlannerDevSceneController controller =
                    host.AddComponent<CharacterTestRuntimeAiPlannerDevSceneController>();
                controller.PrepareInitialSetupState();

                CharacterTestFairyGuiRuntimeAiPlannerDevPanelViewModel viewModel =
                    CharacterTestFairyGuiRuntimeAiPlannerDevPanelViewModelBuilder.Build(
                        controller,
                        commandStatus: null,
                        textProvider: null,
                        CharacterTestRuntimeAiLabWorkspaceMode.BehaviorGroup,
                        selectedWorkbenchEntryIndex: 0);

                Assert.IsTrue(viewModel.ShowWorkbench);
                Assert.IsTrue(viewModel.CanEditWorkbench);
                Assert.AreEqual(viewModel.WorkbenchRows.Length, viewModel.WorkbenchRowCount);
                Assert.Greater(
                    viewModel.WorkbenchRowCount,
                    3,
                    "The native list must receive the complete behavior group instead of a three-row window.");
                Assert.AreEqual(0, viewModel.WorkbenchScrollOffset);
                Assert.AreEqual(0, viewModel.WorkbenchMaxScrollOffset);
                Assert.IsTrue(viewModel.WorkbenchRows[0].Visible);
                Assert.IsTrue(viewModel.WorkbenchRows[0].IsHeader);
                Assert.IsTrue(viewModel.WorkbenchRows.Any(row => !row.IsHeader && row.Selected));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void RuntimeAiLabViewModel_TrainingDummyShowsResponsibilityContainersWithoutPlaceholders()
        {
            var host = new GameObject("Runtime AI Lab Training Dummy Workbench Test");
            try
            {
                CharacterTestRuntimeAiPlannerDevSceneController controller =
                    host.AddComponent<CharacterTestRuntimeAiPlannerDevSceneController>();
                controller.PrepareInitialSetupState(restorePersistedDraft: false);
                Assert.IsTrue(controller.SelectSetupSpawnSlot(
                    CharacterTestArenaMatchConfig.Team2Id,
                    0));
                Assert.IsTrue(controller.SelectSetupBehaviorGroupDraft(4));

                CharacterTestFairyGuiRuntimeAiPlannerDevPanelViewModel viewModel =
                    CharacterTestFairyGuiRuntimeAiPlannerDevPanelViewModelBuilder.Build(
                        controller,
                        commandStatus: null,
                        textProvider: null,
                        CharacterTestRuntimeAiLabWorkspaceMode.BehaviorGroup,
                        selectedWorkbenchEntryIndex: 0);

                Assert.AreEqual("木桩", viewModel.WorkbenchTitleText);
                Assert.AreEqual(4, viewModel.WorkbenchRows.Count(row => row.IsHeader));
                Assert.AreEqual(2, viewModel.WorkbenchRows.Count(row => !row.IsHeader));
                Assert.IsTrue(viewModel.WorkbenchRows.Any(row =>
                    row.IsHeader
                    && row.ResponsibilityKind
                        == CharacterTestRuntimeAiPlannerBehaviorResponsibilityKind.EngageOrPressure
                    && row.ResponsibilityWarning));
                Assert.IsTrue(viewModel.WorkbenchRows.Any(row =>
                    row.IsHeader
                    && row.ResponsibilityKind
                        == CharacterTestRuntimeAiPlannerBehaviorResponsibilityKind.Damage
                    && row.ResponsibilityWarning));
                Assert.IsFalse(viewModel.WorkbenchRows.Any(row =>
                    row.Label.Contains("占位", StringComparison.Ordinal)));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void RuntimeAiLabViewModel_SetupBehaviorWorkbenchUsesSelectedDraft()
        {
            var host = new GameObject("Runtime AI Lab Workbench ViewModel Test");
            try
            {
                CharacterTestRuntimeAiPlannerDevSceneController controller =
                    host.AddComponent<CharacterTestRuntimeAiPlannerDevSceneController>();
                controller.PrepareInitialSetupState(restorePersistedDraft: false);
                controller.DraftWorkspace.LastSavedRevision =
                    controller.DraftWorkspace.CurrentRevision;

                CharacterTestFairyGuiRuntimeAiPlannerDevPanelViewModel groupViewModel =
                    CharacterTestFairyGuiRuntimeAiPlannerDevPanelViewModelBuilder.Build(
                        controller,
                        commandStatus: null,
                        textProvider: null,
                        CharacterTestRuntimeAiLabWorkspaceMode.BehaviorGroup,
                        selectedWorkbenchEntryIndex: 0);

                Assert.IsTrue(groupViewModel.ShowWorkbench);
                Assert.IsTrue(groupViewModel.CanEditWorkbench);
                Assert.Greater(groupViewModel.WorkbenchRows.Length, 0);
                Assert.AreEqual(groupViewModel.WorkbenchRows.Length, groupViewModel.WorkbenchRowCount);
                Assert.Greater(
                    groupViewModel.WorkbenchRowCount,
                    3,
                    "The native list must receive the complete behavior group instead of a three-row window.");
                Assert.IsTrue(groupViewModel.WorkbenchRows[0].Visible);
                Assert.IsTrue(groupViewModel.WorkbenchRows.Any(row => !row.IsHeader && row.Selected));
                Assert.IsTrue(groupViewModel.Workspace.ShowBehaviorGroup);
                Assert.IsTrue(groupViewModel.Workspace.ShowSetup);
                Assert.Greater(groupViewModel.WorkbenchLibraryRows.Length, 0);
                Assert.IsTrue(Array.Exists(
                    groupViewModel.WorkbenchLibraryRows,
                    row => row != null && row.Installed));
                Assert.IsNull(groupViewModel.TimelineProjection);
                Assert.IsTrue(groupViewModel.WorkbenchRows[0].IsHeader);
                Assert.AreEqual(
                    CharacterTestRuntimeAiPlannerBehaviorResponsibilityKind.Targeting,
                    groupViewModel.WorkbenchRows[0].ResponsibilityKind);
                Assert.AreEqual("默认近战", groupViewModel.WorkbenchTitleText);
                Assert.IsEmpty(groupViewModel.WorkbenchSummaryText);
                Assert.AreEqual("配置有效", groupViewModel.DraftStatusText);

                CharacterTestFairyGuiRuntimeAiPlannerDevPanelViewModel cardViewModel =
                    CharacterTestFairyGuiRuntimeAiPlannerDevPanelViewModelBuilder.Build(
                        controller,
                        commandStatus: null,
                        textProvider: null,
                        CharacterTestRuntimeAiLabWorkspaceMode.BehaviorCard,
                        selectedWorkbenchEntryIndex: 0);

                Assert.AreEqual(
                    CharacterTestRuntimeAiLabWorkspaceMode.BehaviorCard,
                    cardViewModel.WorkspaceMode);
                Assert.IsTrue(cardViewModel.IsSelectedCardExpanded);
                Assert.IsTrue(cardViewModel.Workspace.ShowBehaviorCard);
                CharacterTestRuntimeAiLabBehaviorCardTimelineViewModel cardTimeline =
                    cardViewModel.BehaviorCardWorkspace.Timeline;
                Assert.IsTrue(cardTimeline.Available);
                StringAssert.StartsWith("hash ", cardTimeline.SemanticHashText);
                Assert.Greater(
                    cardTimeline.StageCount,
                    0,
                    "The selected behavior card must project its current Logic v2 stages.");
                Assert.GreaterOrEqual(cardTimeline.Stages.Length, cardTimeline.StageCount);
                StringAssert.Contains("等待 · 不占用主动作", cardTimeline.Stages[0].ActionText);
                StringAssert.Contains("结束", cardTimeline.Stages[0].TransitionSummaryText);
                Assert.AreEqual("配置有效", cardViewModel.DraftStatusText);
                Assert.IsFalse(
                    cardViewModel.CanApplyDraft,
                    "草稿没有修改时，保存按钮必须禁用。");
                Assert.AreEqual("保存", cardViewModel.ApplyDraftLabel);

            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void RuntimeAiLabViewModel_BehaviorGroupUsesLogicV2Validity()
        {
            var host = new GameObject("Runtime AI Lab Logic V2 Validity Test");
            try
            {
                CharacterTestRuntimeAiPlannerDevSceneController controller =
                    host.AddComponent<CharacterTestRuntimeAiPlannerDevSceneController>();
                controller.PrepareInitialSetupState();
                Assert.IsTrue(controller.DraftWorkspace.TryGetGroupForSlot(
                    CharacterTestArenaMatchConfig.Team1Id,
                    0,
                    out CharacterTestRuntimeAiLabBehaviorGroupDraft group));
                Assert.Greater(group.Entries.Count, 0);
                Assert.IsTrue(controller.DraftWorkspace.Cards.TryGetValue(
                    group.Entries[0].CardDraftId,
                    out CharacterTestRuntimeAiLabBehaviorCardDraft card));

                card.Payload = new CharacterTestRuntimeAiLabBehaviorCardPayload();
                Assert.IsFalse(controller.CompileBehaviorCard(card).Success);
                Assert.IsTrue(controller.CompileBehaviorCardLogic(card).Success);

                CharacterTestFairyGuiRuntimeAiPlannerDevPanelViewModel viewModel =
                    CharacterTestFairyGuiRuntimeAiPlannerDevPanelViewModelBuilder.Build(
                        controller,
                        commandStatus: null,
                        textProvider: null,
                        CharacterTestRuntimeAiLabWorkspaceMode.BehaviorGroup,
                        selectedWorkbenchEntryIndex: 0);

                Assert.IsFalse(viewModel.WorkbenchRows[0].Invalid);
                StringAssert.StartsWith("索敌", viewModel.WorkbenchRows[0].BadgeText);
                Assert.IsEmpty(viewModel.WorkbenchSummaryText);
                Assert.IsEmpty(viewModel.WorkbenchDiagnosticText);
                Assert.AreEqual("配置有效", viewModel.DraftStatusText);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void RuntimeAiLabWorkbenchCommandSinkMutatesSetupDraftOnly()
        {
            var host = new GameObject("Runtime AI Lab Workbench Command Test");
            try
            {
                CharacterTestRuntimeAiPlannerDevSceneController controller =
                    host.AddComponent<CharacterTestRuntimeAiPlannerDevSceneController>();
                controller.PrepareInitialSetupState();
                var sink = new CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandSink(controller);
                CharacterTestArenaSlotConfig slot = controller.MatchConfig.Team1Slots[0];
                Assert.IsTrue(controller.MatchConfig.BehaviorGroups.TryGetDraft(
                    slot.BehaviorGroupDraftId,
                    out CharacterTestArenaBehaviorGroupDraft draft));
                int originalCount = draft.Profile.Entries.Count;
                Assert.IsTrue(controller.DraftWorkspace.TryGetGroupForSlot(
                    CharacterTestArenaMatchConfig.Team1Id,
                    0,
                    out CharacterTestRuntimeAiLabBehaviorGroupDraft workspaceGroup));
                int workspaceCount = workspaceGroup.Entries.Count;

                sink.Enqueue(new MxUiCommand(
                    CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.ViewId,
                    CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.OpenBehaviorGroup,
                    null));
                Assert.AreEqual(CharacterTestRuntimeAiLabWorkspaceMode.BehaviorGroup, sink.WorkspaceMode);
                sink.Enqueue(new MxUiCommand(
                    CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.ViewId,
                    CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.SelectWorkbenchEntry(1),
                    null));

                sink.Enqueue(new MxUiCommand(
                    CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.ViewId,
                    CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.DuplicateEntry,
                    null));

                Assert.AreEqual(originalCount, draft.Profile.Entries.Count, "Arena source must remain unchanged.");
                Assert.IsTrue(controller.DraftWorkspace.TryGetGroupForSlot(
                    CharacterTestArenaMatchConfig.Team1Id,
                    0,
                    out workspaceGroup));
                Assert.AreEqual(workspaceCount + 1, workspaceGroup.Entries.Count);
                Assert.AreEqual(1, sink.SelectedWorkbenchEntryIndex);
                Assert.IsTrue(controller.DraftWorkspace.Dirty);
                Assert.AreEqual(
                    CharacterTestRuntimeAiLabWorkspaceMode.BehaviorGroup,
                    sink.WorkspaceMode);

                sink.Enqueue(new MxUiCommand(
                    CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.ViewId,
                    CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.DeleteEntry,
                    2));
                Assert.IsTrue(controller.DraftWorkspace.TryGetGroupForSlot(
                    CharacterTestArenaMatchConfig.Team1Id,
                    0,
                    out workspaceGroup));
                Assert.AreEqual(workspaceCount, workspaceGroup.Entries.Count);
                Assert.AreEqual(1, sink.SelectedWorkbenchEntryIndex);
                StringAssert.Contains("已从当前行为组移除", sink.LastStatusMessage);

                sink.Enqueue(new MxUiCommand(
                    CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.ViewId,
                    CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.SelectWorkbenchEntry(0),
                    null));
                Assert.AreEqual(
                    CharacterTestRuntimeAiLabWorkspaceMode.BehaviorGroup,
                    sink.WorkspaceMode);
                Assert.IsFalse(sink.IsSelectedCardExpanded);

                sink.Enqueue(new MxUiCommand(
                    CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.ViewId,
                    CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.BehaviorCardTab,
                    null));
                Assert.AreEqual(CharacterTestRuntimeAiLabWorkspaceMode.BehaviorCard, sink.WorkspaceMode);
                Assert.IsTrue(sink.IsSelectedCardExpanded);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void RuntimeAiLabInvalidSetup_BlocksStartButKeepsRepairEditorReachable()
        {
            var host = new GameObject("Runtime AI Lab Invalid Card Repair Navigation Test");
            try
            {
                CharacterTestRuntimeAiPlannerDevSceneController controller =
                    host.AddComponent<CharacterTestRuntimeAiPlannerDevSceneController>();
                controller.PrepareInitialSetupState(restorePersistedDraft: false);
                Assert.IsTrue(controller.DraftWorkspace.TryGetGroupForSlot(
                    CharacterTestArenaMatchConfig.Team1Id,
                    0,
                    out CharacterTestRuntimeAiLabBehaviorGroupDraft group));
                Assert.That(group.Entries.Count, Is.GreaterThan(0));

                for (int i = 0; i < controller.MatchConfig.Team2Slots.Count; i++)
                    controller.MatchConfig.Team2Slots[i].Enabled = false;

                CharacterTestFairyGuiRuntimeAiPlannerDevPanelViewModel viewModel =
                    CharacterTestFairyGuiRuntimeAiPlannerDevPanelViewModelBuilder.Build(
                        controller,
                        commandStatus: string.Empty);
                Assert.IsFalse(viewModel.CanStart, "Invalid test data must still block Start.");
                Assert.IsTrue(
                    viewModel.CanOpenBehaviorGroup,
                    "The repair editor must remain reachable before a session can start.");

                var sink = new CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandSink(controller);
                sink.Enqueue(new MxUiCommand(
                    CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.ViewId,
                    CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.OpenBehaviorGroup,
                    null));
                Assert.AreEqual(
                    CharacterTestRuntimeAiLabWorkspaceMode.BehaviorGroup,
                    sink.WorkspaceMode);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void RuntimeAiLabWorkbench_EnteringBehaviorLogicUsesVisibleRecoveredRevisionAsSaveBaseline()
        {
            var host = new GameObject("Runtime AI Lab Save Revision Authority Test");
            try
            {
                CharacterTestRuntimeAiPlannerDevSceneController controller =
                    host.AddComponent<CharacterTestRuntimeAiPlannerDevSceneController>();
                controller.PrepareInitialSetupState(restorePersistedDraft: false);

                CharacterTestRuntimeAiLabDraftWorkspace workspace = controller.DraftWorkspace;
                workspace.CurrentRevision = 7;
                workspace.LastSavedRevision = 7;
                workspace.LastExplicitSaveRevision = 3;
                workspace.LastAppliedRevision = 5;
                MethodInfo resetCheckpoint = typeof(CharacterTestRuntimeAiPlannerDevSceneController).GetMethod(
                    "ResetBehaviorRecipeSaveCheckpoint",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.IsNotNull(resetCheckpoint);
                resetCheckpoint.Invoke(controller, new object[] { workspace });

                Assert.IsFalse(
                    controller.HasBehaviorRecipeChangesToSave,
                    "Entering Behavior Logic must treat the currently visible recovered revision as the new session baseline.");
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelViewModel viewModel =
                    CharacterTestFairyGuiRuntimeAiPlannerDevPanelViewModelBuilder.Build(
                        controller,
                        commandStatus: null,
                        textProvider: null,
                        CharacterTestRuntimeAiLabWorkspaceMode.BehaviorCard,
                        selectedWorkbenchEntryIndex: 0);
                Assert.IsFalse(viewModel.CanApplyDraft);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void RuntimeAiLabWorkbench_GroupChangesDoNotEnableCardSave_AndSaveFollowsSelectedSharedCard()
        {
            var host = new GameObject("Runtime AI Lab Explicit Save Checkpoint Test");
            try
            {
                CharacterTestRuntimeAiPlannerDevSceneController controller =
                    host.AddComponent<CharacterTestRuntimeAiPlannerDevSceneController>();
                controller.PrepareInitialSetupState(restorePersistedDraft: false);
                var sink = new CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandSink(controller);

                Assert.IsTrue(controller.TryDuplicateDraftEntry(1, out string duplicateMessage), duplicateMessage);
                Assert.IsFalse(
                    controller.HasBehaviorRecipeChangesToSave,
                    "Installing or duplicating a group entry is group data, not a behavior-card edit.");
                Assert.IsTrue(controller.DraftWorkspace.TryGetGroupForSlot(
                    CharacterTestArenaMatchConfig.Team1Id,
                    0,
                    out CharacterTestRuntimeAiLabBehaviorGroupDraft group));
                CharacterTestRuntimeAiLabBehaviorCardLogicPayload edited =
                    controller.DraftWorkspace.Cards[group.Entries[1].CardDraftId].LogicPayload.Clone();
                edited.Timelines[0].Title = "Shared card edit";
                Assert.IsTrue(controller.DraftWorkspace.TryReplaceCardLogicPayload(
                    CharacterTestArenaMatchConfig.Team1Id,
                    0,
                    1,
                    edited,
                    out string editMessage), editMessage);
                Assert.IsTrue(controller.HasBehaviorRecipeChangesToSave);
                int changedRevision = controller.DraftWorkspace.CurrentRevision;

                // Recovery autosave only protects the draft on disk. It must not acknowledge
                // the user's explicit Save command or dim its button on the next UI refresh.
                controller.DraftWorkspace.LastSavedRevision = changedRevision;
                Assert.IsFalse(controller.DraftWorkspace.Dirty);
                Assert.IsTrue(controller.HasBehaviorRecipeChangesToSave);

                sink.Enqueue(new MxUiCommand(
                    CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.ViewId,
                    CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.OpenBehaviorGroup,
                    null));
                sink.Enqueue(new MxUiCommand(
                    CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.ViewId,
                    CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.SelectWorkbenchEntry(0),
                    null));

                CharacterTestFairyGuiRuntimeAiPlannerDevPanelViewModel viewModel =
                    CharacterTestFairyGuiRuntimeAiPlannerDevPanelViewModelBuilder.Build(
                        controller,
                        sink.LastStatusMessage,
                        textProvider: null,
                        sink.WorkspaceMode,
                        sink.SelectedWorkbenchEntryIndex);
                Assert.AreEqual(0, sink.SelectedWorkbenchEntryIndex);
                Assert.IsFalse(
                    viewModel.CanApplyDraft,
                    "Save must follow the selected shared card, not unrelated card edits or group changes.");

                sink.Enqueue(new MxUiCommand(
                    CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.ViewId,
                    CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.SelectWorkbenchEntry(1),
                    null));
                viewModel = CharacterTestFairyGuiRuntimeAiPlannerDevPanelViewModelBuilder.Build(
                    controller,
                    sink.LastStatusMessage,
                    textProvider: null,
                    sink.WorkspaceMode,
                    sink.SelectedWorkbenchEntryIndex);
                Assert.IsTrue(viewModel.CanApplyDraft);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void RuntimeAiLabWorkbenchCommandSink_DeletesOpenBehaviorCardInOneClick()
        {
            var host = new GameObject("Runtime AI Lab Delete Open Card Command Test");
            try
            {
                CharacterTestRuntimeAiPlannerDevSceneController controller =
                    host.AddComponent<CharacterTestRuntimeAiPlannerDevSceneController>();
                controller.PrepareInitialSetupState();
                var sink = new CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandSink(controller);
                sink.Enqueue(new MxUiCommand(
                    CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.ViewId,
                    CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.OpenBehaviorGroup,
                    null));
                sink.Enqueue(new MxUiCommand(
                    CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.ViewId,
                    CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.SelectWorkbenchEntry(1),
                    null));
                sink.Enqueue(new MxUiCommand(
                    CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.ViewId,
                    CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.BehaviorCardTab,
                    null));
                Assert.AreEqual(
                    CharacterTestRuntimeAiLabWorkspaceMode.BehaviorCard,
                    sink.WorkspaceMode,
                    "Selecting an editable card must open its timeline before the inline delete is used.");
                Assert.IsTrue(controller.DraftWorkspace.TryGetGroupForSlot(
                    CharacterTestArenaMatchConfig.Team1Id,
                    0,
                    out CharacterTestRuntimeAiLabBehaviorGroupDraft workspaceGroup));
                int countBeforeDelete = workspaceGroup.Entries.Count;

                sink.Enqueue(new MxUiCommand(
                    CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.ViewId,
                    CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.DeleteEntry,
                    1));

                Assert.IsTrue(controller.DraftWorkspace.TryGetGroupForSlot(
                    CharacterTestArenaMatchConfig.Team1Id,
                    0,
                    out workspaceGroup));
                Assert.AreEqual(
                    countBeforeDelete - 1,
                    workspaceGroup.Entries.Count,
                    "The open card must be removed by one inline-delete click.");
                Assert.AreEqual(1, sink.SelectedWorkbenchEntryIndex);
                Assert.AreEqual(
                    CharacterTestRuntimeAiLabWorkspaceMode.BehaviorCard,
                    sink.WorkspaceMode,
                    "After deletion the timeline must reopen on the adjacent editable card instead of retaining a stale route.");
                StringAssert.Contains("已从当前行为组移除", sink.LastStatusMessage);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(host);
            }
        }


        [Test]
        public void RuntimeAiLabWorkbenchCommandSink_CoreReplacementFiltersLibraryAndStaysOpen()
        {
            var host = new GameObject("Runtime AI Lab Core Slot Replacement Test");
            try
            {
                CharacterTestRuntimeAiPlannerDevSceneController controller =
                    host.AddComponent<CharacterTestRuntimeAiPlannerDevSceneController>();
                controller.PrepareInitialSetupState();
                var sink = new CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandSink(controller);
                sink.Enqueue(new MxUiCommand(
                    CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.ViewId,
                    CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.OpenBehaviorGroup,
                    null));
                sink.Enqueue(new MxUiCommand(
                    CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.ViewId,
                    CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.SelectWorkbenchEntry(0),
                    null));
                sink.Enqueue(new MxUiCommand(
                    CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.ViewId,
                    CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.ReplaceCoreSlot,
                    null));

                Assert.IsTrue(controller.IsCoreSlotReplacementActive);
                Assert.AreEqual(
                    CharacterTestRuntimeAiPlannerBehaviorCoreSlotKind.Targeting,
                    controller.CoreSlotReplacementKind);
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelViewModel viewModel =
                    CharacterTestFairyGuiRuntimeAiPlannerDevPanelViewModelBuilder.Build(
                        controller,
                        sink.LastStatusMessage,
                        textProvider: null,
                        CharacterTestRuntimeAiLabWorkspaceMode.BehaviorGroup,
                        selectedWorkbenchEntryIndex: 0);
                CharacterTestRuntimeAiLabWorkbenchLibraryRowViewModel available =
                    Array.Find(viewModel.WorkbenchLibraryRows, row => row != null && row.Available);
                Assert.IsNotNull(available);
                Assert.IsTrue(viewModel.WorkbenchCoreReplacementActive);
                var projectedBehaviorIds = new HashSet<string>(StringComparer.Ordinal);
                for (int rowIndex = 0;
                    rowIndex < viewModel.WorkbenchLibraryRows.Length;
                    rowIndex++)
                {
                    CharacterTestRuntimeAiLabWorkbenchLibraryRowViewModel row =
                        viewModel.WorkbenchLibraryRows[rowIndex];
                    Assert.IsNotNull(row);
                    if (!string.IsNullOrWhiteSpace(row.BehaviorId))
                    {
                        Assert.IsTrue(
                            projectedBehaviorIds.Add(row.BehaviorId),
                            "Core replacement library must show each behavior definition once.");
                    }
                }
                Assert.IsTrue(Array.Exists(
                    viewModel.WorkbenchLibraryRows,
                    row => row != null && !row.Available && row.StatusText == "槽位不兼容"));

                sink.Enqueue(new MxUiCommand(
                    CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.ViewId,
                    CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.AddWorkbenchCard,
                    available.CardDraftId));
                Assert.AreEqual(
                    CharacterTestRuntimeAiLabWorkspaceMode.BehaviorGroup,
                    sink.WorkspaceMode);
                Assert.IsTrue(controller.IsCoreSlotReplacementActive);
                Assert.AreEqual(0, sink.SelectedWorkbenchEntryIndex);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void RuntimeAiLabWorkspaceNavigation_UsesExplicitPagesAndDiagnosticsTabs()
        {
            var host = new GameObject("Runtime AI Lab Workspace Navigation Test");
            try
            {
                CharacterTestRuntimeAiPlannerDevSceneController controller =
                    host.AddComponent<CharacterTestRuntimeAiPlannerDevSceneController>();
                controller.PrepareInitialSetupState();
                var sink = new CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandSink(controller);

                Assert.AreEqual(CharacterTestRuntimeAiLabWorkspaceMode.Setup, sink.WorkspaceMode);
                Assert.AreEqual(CharacterTestRuntimeAiLabDiagnosticsTab.Facts, sink.DiagnosticsTab);

                sink.Enqueue(new MxUiCommand(
                    CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.ViewId,
                    CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.OpenBehaviorGroup,
                    null));
                Assert.AreEqual(CharacterTestRuntimeAiLabWorkspaceMode.BehaviorGroup, sink.WorkspaceMode);

                sink.Enqueue(new MxUiCommand(
                    CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.ViewId,
                    CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.SelectWorkbenchEntry(0),
                    null));
                Assert.AreEqual(CharacterTestRuntimeAiLabWorkspaceMode.BehaviorGroup, sink.WorkspaceMode);
                sink.Enqueue(new MxUiCommand(
                    CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.ViewId,
                    CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.BehaviorCardTab,
                    null));
                Assert.AreEqual(CharacterTestRuntimeAiLabWorkspaceMode.BehaviorCard, sink.WorkspaceMode);

                sink.Enqueue(new MxUiCommand(
                    CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.ViewId,
                    CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.WorkspaceBack,
                    null));
                Assert.AreEqual(CharacterTestRuntimeAiLabWorkspaceMode.BehaviorGroup, sink.WorkspaceMode);

                sink.Enqueue(new MxUiCommand(
                    CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.ViewId,
                    CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.WorkspaceBack,
                    null));
                Assert.AreEqual(CharacterTestRuntimeAiLabWorkspaceMode.Setup, sink.WorkspaceMode);

                sink.Enqueue(new MxUiCommand(
                    CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.ViewId,
                    CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.DiagnosticsRuntimeFrameTab,
                    null));
                Assert.AreEqual(CharacterTestRuntimeAiLabWorkspaceMode.Diagnostics, sink.WorkspaceMode);
                Assert.AreEqual(CharacterTestRuntimeAiLabDiagnosticsTab.RuntimeFrame, sink.DiagnosticsTab);

                sink.Enqueue(new MxUiCommand(
                    CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.ViewId,
                    CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.DiagnosticsReplayTab,
                    null));
                Assert.AreEqual(CharacterTestRuntimeAiLabDiagnosticsTab.Replay, sink.DiagnosticsTab);
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelViewModel replayViewModel =
                    CharacterTestFairyGuiRuntimeAiPlannerDevPanelViewModelBuilder.Build(
                        controller,
                        commandStatus: null,
                        textProvider: null,
                        workspaceMode: sink.WorkspaceMode,
                        selectedWorkbenchEntryIndex: sink.SelectedWorkbenchEntryIndex,
                        diagnosticsTab: sink.DiagnosticsTab);
                Assert.IsTrue(replayViewModel.Workspace.ShowDiagnostics);
                Assert.AreEqual(CharacterTestReplayValidationState.Empty, replayViewModel.Replay.State);
                Assert.IsFalse(replayViewModel.Replay.HasCapture);
                Assert.IsFalse(replayViewModel.Replay.CanFreeze);
                Assert.IsFalse(replayViewModel.Replay.CanValidate);
                Assert.IsNull(replayViewModel.Replay.TimelineProjection);

                sink.Enqueue(new MxUiCommand(
                    CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.ViewId,
                    CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.DiagnosticsFactsTab,
                    null));
                Assert.AreEqual(CharacterTestRuntimeAiLabDiagnosticsTab.Facts, sink.DiagnosticsTab);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void RuntimeAiLabCompiledStepSelection_RefreshesReadOnlyUiOnly()
        {
            var host = new GameObject("Runtime AI Lab Compiled Step Selection Test");
            try
            {
                CharacterTestRuntimeAiPlannerDevSceneController controller =
                    host.AddComponent<CharacterTestRuntimeAiPlannerDevSceneController>();
                controller.PrepareInitialSetupState();
                int refreshCount = 0;
                var sink = new CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandSink(
                    controller,
                    refreshPanel: () =>
                    {
                        refreshCount++;
                        return true;
                    });
                int revisionBefore = controller.DraftWorkspace.CurrentRevision;
                Assert.IsTrue(controller.DraftWorkspace.TryGetGroupForSlot(
                    CharacterTestArenaMatchConfig.Team1Id,
                    0,
                    out CharacterTestRuntimeAiLabBehaviorGroupDraft group));
                CharacterTestRuntimeAiLabBehaviorCardDraft card =
                    controller.DraftWorkspace.Cards[group.Entries[0].CardDraftId];
                string payloadBefore = CharacterTestRuntimeAiLabBehaviorCardJson.Serialize(card.Payload);

                sink.Enqueue(new MxUiCommand(
                    CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.ViewId,
                    CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.SelectCompiledStep(3),
                    null));

                Assert.AreEqual(3, sink.SelectedCompiledStepIndex);
                Assert.AreEqual("已选择编译步骤。", sink.LastStatusMessage);
                Assert.AreEqual(1, refreshCount);
                Assert.AreEqual(revisionBefore, controller.DraftWorkspace.CurrentRevision);
                Assert.AreEqual(
                    payloadBefore,
                    CharacterTestRuntimeAiLabBehaviorCardJson.Serialize(card.Payload));
                Assert.AreEqual(CharacterTestRuntimeAiPlannerDevScenePhase.Setup, controller.Phase);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(host);
            }
        }


        [Test]
        public void RuntimeAiLabDraftWorkspace_FirstGroupEditUsesCopyOnWrite()
        {
            CharacterTestArenaMatchConfig arena = CharacterTestArenaMatchConfig.CreateDefault();
            CharacterTestRuntimeAiLabDraftWorkspace workspace =
                CharacterTestRuntimeAiLabDraftWorkspace.Import(arena);
            Assert.IsTrue(workspace.TryGetGroupForSlot(1, 0, out CharacterTestRuntimeAiLabBehaviorGroupDraft first));
            Assert.IsTrue(workspace.TryGetGroupForSlot(1, 1, out CharacterTestRuntimeAiLabBehaviorGroupDraft shared));
            Assert.AreEqual(first.GroupDraftId, shared.GroupDraftId);
            int sourceCount = first.Entries.Count;

            Assert.IsTrue(workspace.TryDuplicateEntry(1, 0, 0, out string message), message);
            Assert.IsTrue(workspace.TryGetGroupForSlot(1, 0, out CharacterTestRuntimeAiLabBehaviorGroupDraft edited));
            Assert.IsTrue(workspace.TryGetGroupForSlot(1, 1, out CharacterTestRuntimeAiLabBehaviorGroupDraft untouched));
            Assert.AreNotEqual(edited.GroupDraftId, untouched.GroupDraftId);
            Assert.AreEqual(sourceCount + 1, edited.Entries.Count);
            Assert.AreEqual(sourceCount, untouched.Entries.Count);
            Assert.AreNotEqual(edited.Entries[0].EntryId, edited.Entries[1].EntryId);
            Assert.AreEqual(edited.Entries[0].EntryId, edited.Entries[0].RuntimeEntry.EntryId);
            Assert.AreEqual(edited.Entries[1].EntryId, edited.Entries[1].RuntimeEntry.EntryId);
        }

        [Test]
        public void RuntimeAiLabDraftWorkspace_SetupSyncPreservesCurrentCopyOnWriteGroup()
        {
            CharacterTestArenaMatchConfig arena = CharacterTestArenaMatchConfig.CreateDefault();
            CharacterTestRuntimeAiLabDraftWorkspace workspace =
                CharacterTestRuntimeAiLabDraftWorkspace.Import(arena);
            Assert.IsTrue(workspace.TryDuplicateEntry(1, 0, 0, out string editMessage), editMessage);
            Assert.IsTrue(workspace.TryGetGroupForSlot(1, 0, out CharacterTestRuntimeAiLabBehaviorGroupDraft edited));
            string editedGroupId = edited.GroupDraftId;
            int editedEntryCount = edited.Entries.Count;

            Assert.IsTrue(arena.SetSpawnPosition(1, 0, new Vector2(-3f, 1f)));
            Assert.IsTrue(workspace.SyncSlotFromArena(arena, 1, 0));
            Assert.AreEqual(editedGroupId, workspace.TryGetSlot(1, 0).GroupDraftId);
            Assert.IsTrue(workspace.TryGetGroupForSlot(1, 0, out CharacterTestRuntimeAiLabBehaviorGroupDraft retained));
            Assert.AreSame(edited, retained);
            Assert.AreEqual(editedEntryCount, retained.Entries.Count);
        }

        [Test]
        public void RuntimeAiLabDraftWorkspace_LocalGroupCopyRemainsSelectableWithoutProjectIdentity()
        {
            CharacterTestArenaMatchConfig arena = CharacterTestArenaMatchConfig.CreateDefault();
            CharacterTestRuntimeAiLabDraftWorkspace workspace =
                CharacterTestRuntimeAiLabDraftWorkspace.Import(arena);
            Assert.IsTrue(workspace.TryGetGroupForSlot(
                1,
                0,
                out CharacterTestRuntimeAiLabBehaviorGroupDraft source));

            Assert.IsTrue(workspace.TryCreateLocalGroupCopyForSlot(
                1,
                0,
                out CharacterTestRuntimeAiLabBehaviorGroupDraft copy,
                out string createMessage), createMessage);

            Assert.AreNotEqual(source.GroupDraftId, copy.GroupDraftId);
            Assert.IsFalse(copy.HasProjectSource);
            Assert.AreEqual(string.Empty, copy.PublishedStableId);
            StringAssert.Contains("副本", copy.DisplayName);
            StringAssert.StartsWith("runtime-ai-lab.draft.", copy.Profile.GroupId);
            Assert.AreEqual(0, workspace.TryGetSlot(1, 0).ArenaBehaviorGroupDraftId);
            Assert.IsTrue(workspace.TryGetGroupForSlot(
                1,
                0,
                out CharacterTestRuntimeAiLabBehaviorGroupDraft selected));
            Assert.AreSame(copy, selected);

            Assert.IsTrue(workspace.TrySelectGroupForSlot(
                1,
                0,
                source.GroupDraftId,
                arena.TryGetSlot(1, 0).BehaviorGroupDraftId,
                out string selectMessage), selectMessage);
            Assert.IsTrue(workspace.TryGetGroupForSlot(1, 0, out selected));
            Assert.AreSame(source, selected);
        }

        [Test]
        public void RuntimeAiLabDraftWorkspace_FirstSharedCardEditUsesCopyOnWrite()
        {
            CharacterTestRuntimeAiLabDraftWorkspace workspace =
                CharacterTestRuntimeAiLabDraftWorkspace.Import(CharacterTestArenaMatchConfig.CreateDefault());
            Assert.IsTrue(workspace.TryGetGroupForSlot(
                1,
                0,
                out CharacterTestRuntimeAiLabBehaviorGroupDraft sourceGroup));
            string sourceEntryId = sourceGroup.Entries[0].EntryId;
            string sharedCardDraftId = sourceGroup.Entries[0].CardDraftId;
            Assert.IsTrue(workspace.TryDuplicateEntry(1, 0, 0, out string duplicateMessage), duplicateMessage);
            Assert.IsTrue(workspace.TryGetGroupForSlot(1, 0, out CharacterTestRuntimeAiLabBehaviorGroupDraft group));
            int sourceEntryIndex = -1;
            int duplicateEntryIndex = -1;
            string duplicateEntryId = string.Empty;
            for (int entryIndex = 0; entryIndex < group.Entries.Count; entryIndex++)
            {
                CharacterTestRuntimeAiLabBehaviorEntryDraft entry = group.Entries[entryIndex];
                if (string.Equals(entry.EntryId, sourceEntryId, StringComparison.Ordinal))
                    sourceEntryIndex = entryIndex;
                else if (string.Equals(entry.CardDraftId, sharedCardDraftId, StringComparison.Ordinal))
                {
                    duplicateEntryIndex = entryIndex;
                    duplicateEntryId = entry.EntryId;
                }
            }
            Assert.GreaterOrEqual(sourceEntryIndex, 0);
            Assert.GreaterOrEqual(duplicateEntryIndex, 0);
            Assert.AreEqual(
                group.Entries[sourceEntryIndex].CardDraftId,
                group.Entries[duplicateEntryIndex].CardDraftId);

            Assert.IsTrue(workspace.TryReplaceCardPayload(
                1,
                0,
                sourceEntryIndex,
                CharacterTestRuntimeAiLabBehaviorCardFactory.CreateActionChainFixture(),
                out string editMessage), editMessage);
            Assert.IsTrue(workspace.TryGetGroupForSlot(
                1,
                0,
                out CharacterTestRuntimeAiLabBehaviorGroupDraft editedGroup));
            CharacterTestRuntimeAiLabBehaviorEntryDraft editedSource = null;
            CharacterTestRuntimeAiLabBehaviorEntryDraft retainedDuplicate = null;
            for (int entryIndex = 0; entryIndex < editedGroup.Entries.Count; entryIndex++)
            {
                CharacterTestRuntimeAiLabBehaviorEntryDraft entry = editedGroup.Entries[entryIndex];
                if (string.Equals(entry.EntryId, sourceEntryId, StringComparison.Ordinal))
                    editedSource = entry;
                else if (string.Equals(entry.EntryId, duplicateEntryId, StringComparison.Ordinal))
                    retainedDuplicate = entry;
            }
            Assert.IsNotNull(editedSource);
            Assert.IsNotNull(retainedDuplicate);
            Assert.AreNotEqual(editedSource.CardDraftId, retainedDuplicate.CardDraftId);
            Assert.AreEqual(4, workspace.Cards[editedSource.CardDraftId].Payload.Steps.Count);
            Assert.AreEqual(1, workspace.Cards[retainedDuplicate.CardDraftId].Payload.Steps.Count);
        }

        [Test]
        public void RuntimeAiLabDraftWorkspace_UndoRestoresReferencesAndRemainsDirty()
        {
            CharacterTestRuntimeAiLabDraftWorkspace workspace =
                CharacterTestRuntimeAiLabDraftWorkspace.Import(CharacterTestArenaMatchConfig.CreateDefault());
            string originalGroupId = workspace.TryGetSlot(1, 0).GroupDraftId;
            Assert.IsTrue(workspace.TryGetGroupForSlot(1, 0, out CharacterTestRuntimeAiLabBehaviorGroupDraft original));
            int originalCount = original.Entries.Count;
            Assert.IsTrue(workspace.TryDuplicateEntry(1, 0, 0, out string mutationMessage), mutationMessage);
            Assert.AreNotEqual(originalGroupId, workspace.TryGetSlot(1, 0).GroupDraftId);

            Assert.IsTrue(workspace.TryUndo(out string undoMessage), undoMessage);
            Assert.AreEqual(originalGroupId, workspace.TryGetSlot(1, 0).GroupDraftId);
            Assert.IsTrue(workspace.TryGetGroupForSlot(1, 0, out CharacterTestRuntimeAiLabBehaviorGroupDraft restored));
            Assert.AreEqual(originalCount, restored.Entries.Count);
            Assert.IsTrue(workspace.Dirty);
        }

        [Test]
        [Ignore("Replaced by behavior responsibilities v3 coverage.")]
        public void BehaviorGroupCoreSlots_DefaultProfileOwnsCompleteMinimumExecutionChain()
        {
            CharacterTestRuntimeAiPlannerBehaviorCompositionProfile profile =
                CharacterTestRuntimeAiPlannerBehaviorCompositionCatalog.CreateDefaultProfile();
            CharacterTestRuntimeAiPlannerBehaviorCoreSlotReport report =
                CharacterTestRuntimeAiPlannerBehaviorCoreSlotPolicy.Validate(
                    profile.Entries,
                    CharacterTestRuntimeAiPlannerBehaviorDefinitionCatalog.CreateBuiltIn());

            Assert.IsTrue(report.Success, report.Message);
            Assert.AreEqual(
                CharacterTestRuntimeAiPlannerBehaviorCoreSlotKind.Targeting,
                profile.Entries[0].CoreSlotKind);
            Assert.AreEqual(
                CharacterTestRuntimeAiPlannerBehaviorCoreSlotKind.EngageOrPressure,
                profile.Entries[1].CoreSlotKind);
            Assert.AreEqual(
                CharacterTestRuntimeAiPlannerBehaviorCoreSlotKind.Damage,
                profile.Entries[2].CoreSlotKind);
            Assert.AreEqual(
                CharacterTestRuntimeAiPlannerBehaviorCoreSlotKind.Fallback,
                profile.Entries[3].CoreSlotKind);
            Assert.IsTrue(CharacterTestRuntimeAiPlannerBehaviorCoreSlotPolicy.IsCore(
                CharacterTestRuntimeAiPlannerBehaviorCoreSlotKind.EngageOrPressure));
            Assert.IsTrue(CharacterTestRuntimeAiPlannerBehaviorCoreSlotPolicy.IsCore(
                CharacterTestRuntimeAiPlannerBehaviorCoreSlotKind.Damage));
            Assert.AreEqual(4, CharacterTestRuntimeAiPlannerBehaviorCoreSlotPolicy.OrderedCoreSlots.Count);
        }

        [Test]
        [Ignore("Replaced by behavior responsibilities v3 coverage.")]
        public void RuntimeAiLabDraftWorkspace_CoreSlotsRejectDestructiveMutationsAndCopiesBecomeFlexible()
        {
            CharacterTestRuntimeAiLabDraftWorkspace workspace =
                CharacterTestRuntimeAiLabDraftWorkspace.Import(CharacterTestArenaMatchConfig.CreateDefault());
            Assert.IsTrue(workspace.TryGetGroupForSlot(1, 0, out CharacterTestRuntimeAiLabBehaviorGroupDraft group));

            Assert.IsFalse(workspace.TryRemoveEntry(1, 0, 0, out string removeMessage));
            StringAssert.Contains("只能替换", removeMessage);
            Assert.IsFalse(workspace.TryToggleEntry(1, 0, 0, out string toggleMessage));
            StringAssert.Contains("始终启用", toggleMessage);
            Assert.IsFalse(workspace.TryMoveEntry(1, 0, 0, 1, out string moveMessage));
            StringAssert.Contains("不能移动", moveMessage);

            Assert.IsTrue(workspace.TryDuplicateEntry(1, 0, 0, out string duplicateMessage), duplicateMessage);
            Assert.IsTrue(workspace.TryGetGroupForSlot(1, 0, out group));
            Assert.AreEqual(2, group.Entries.Count(entry =>
                string.Equals(
                    entry.BehaviorId,
                    CharacterTestRuntimeAiPlannerBehaviorCompositionCatalog.FoundationApproachAndAttack,
                    StringComparison.Ordinal)));
            Assert.AreEqual(1, group.Entries.Count(entry =>
                entry.CoreSlotKind == CharacterTestRuntimeAiPlannerBehaviorCoreSlotKind.Flexible
                && string.Equals(
                    entry.BehaviorId,
                    CharacterTestRuntimeAiPlannerBehaviorCompositionCatalog.FoundationApproachAndAttack,
                    StringComparison.Ordinal)));
        }

        [Test]
        [Ignore("Replaced by behavior responsibilities v3 coverage.")]
        public void RuntimeAiLabDraftWorkspace_CoreSlotReplacementIsAtomicAndCapabilityFiltered()
        {
            CharacterTestRuntimeAiLabDraftWorkspace workspace =
                CharacterTestRuntimeAiLabDraftWorkspace.Import(CharacterTestArenaMatchConfig.CreateDefault());
            Assert.IsTrue(workspace.TryGetGroupForSlot(1, 0, out CharacterTestRuntimeAiLabBehaviorGroupDraft group));
            string targetingCard = group.Entries[0].CardDraftId;
            string engageCard = group.Entries[1].CardDraftId;
            string originalBehavior = group.Entries[2].BehaviorId;

            Assert.IsFalse(workspace.TryReplaceCoreSlot(
                1,
                0,
                CharacterTestRuntimeAiPlannerBehaviorCoreSlotKind.Damage,
                targetingCard,
                out _,
                out string incompatibleMessage));
            StringAssert.Contains("不兼容", incompatibleMessage);
            Assert.AreEqual(originalBehavior, group.Entries[2].BehaviorId);

            Assert.IsTrue(workspace.TryReplaceCoreSlot(
                1,
                0,
                CharacterTestRuntimeAiPlannerBehaviorCoreSlotKind.EngageOrPressure,
                engageCard,
                out int replacedIndex,
                out string replaceMessage), replaceMessage);
            Assert.AreEqual(1, replacedIndex);
            Assert.AreEqual(
                CharacterTestRuntimeAiPlannerBehaviorCoreSlotKind.EngageOrPressure,
                group.Entries[replacedIndex].CoreSlotKind);
        }

        [Test]
        [Ignore("v3 responsibilities never repair or synthesize installed cards.")]
        public void RuntimeAiLabDraftWorkspace_RepairsLockedFallbackCardThatNoLongerPerformsItsRole()
        {
            CharacterTestRuntimeAiLabDraftWorkspace workspace =
                CharacterTestRuntimeAiLabDraftWorkspace.Import(
                    CharacterTestArenaMatchConfig.CreateDefault());
            Assert.IsTrue(
                workspace.TryGetGroupForSlot(
                    1,
                    0,
                    out CharacterTestRuntimeAiLabBehaviorGroupDraft group));

            CharacterTestRuntimeAiLabBehaviorEntryDraft originalFallback = null;
            for (int i = 0; i < group.Entries.Count; i++)
            {
                if (group.Entries[i].CoreSlotKind
                    == CharacterTestRuntimeAiPlannerBehaviorCoreSlotKind.Fallback)
                {
                    originalFallback = group.Entries[i];
                    break;
                }
            }
            Assert.IsNotNull(originalFallback);
            string originalCardId = originalFallback.CardDraftId;
            CharacterTestRuntimeAiLabBehaviorCardDraft originalCard =
                workspace.Cards[originalCardId];
            originalCard.LogicPayload = CharacterTestRuntimeAiLabBehaviorCardV1Migration.Convert(
                CharacterTestRuntimeAiLabBehaviorCardFactory.CreateLegacyAtomic(
                    "test.primary-attack-only",
                    CharacterTestRuntimeAiPlannerActionKind.PrimaryAttack));
            originalCard.LogicPayloadMigratedFromV1 = false;

            Assert.IsTrue(workspace.EnsureCoreSlotContracts(markMigrated: true));
            Assert.IsTrue(workspace.TryGetGroupForSlot(1, 0, out group));

            CharacterTestRuntimeAiLabBehaviorEntryDraft repairedFallback = null;
            CharacterTestRuntimeAiLabBehaviorEntryDraft preservedOriginal = null;
            for (int i = 0; i < group.Entries.Count; i++)
            {
                CharacterTestRuntimeAiLabBehaviorEntryDraft entry = group.Entries[i];
                if (entry.CoreSlotKind
                    == CharacterTestRuntimeAiPlannerBehaviorCoreSlotKind.Fallback)
                {
                    repairedFallback = entry;
                }
                if (entry.CardDraftId == originalCardId)
                    preservedOriginal = entry;
            }

            Assert.IsNotNull(repairedFallback);
            Assert.AreNotEqual(originalCardId, repairedFallback.CardDraftId);
            Assert.IsTrue(
                CharacterTestRuntimeAiLabBehaviorCoreSlotCardPolicy.Fulfills(
                    CharacterTestRuntimeAiPlannerBehaviorCoreSlotKind.Fallback,
                    workspace.Cards[repairedFallback.CardDraftId],
                    out string roleMessage),
                roleMessage);
            Assert.IsNotNull(preservedOriginal);
            Assert.AreEqual(
                CharacterTestRuntimeAiPlannerBehaviorCoreSlotKind.Flexible,
                preservedOriginal.CoreSlotKind);
            Assert.IsTrue(preservedOriginal.Disabled);
        }

        [Test]
        [Ignore("v3 responsibilities never remove authored duplicate cards.")]
        public void RuntimeAiLabDraftWorkspace_V6PersistentFallbackIsIdempotentAndRemovesGeneratedDuplicates()
        {
            CharacterTestRuntimeAiLabDraftWorkspace workspace =
                CharacterTestRuntimeAiLabDraftWorkspace.Import(
                    CharacterTestArenaMatchConfig.CreateDefault());
            Assert.IsTrue(
                workspace.TryGetGroupForSlot(
                    1,
                    0,
                    out CharacterTestRuntimeAiLabBehaviorGroupDraft group));

            CharacterTestRuntimeAiLabBehaviorEntryDraft fallback = group.Entries.Single(entry =>
                entry.CoreSlotKind == CharacterTestRuntimeAiPlannerBehaviorCoreSlotKind.Fallback);
            Assert.IsTrue(
                CharacterTestRuntimeAiLabBehaviorCoreSlotCardPolicy.Fulfills(
                    CharacterTestRuntimeAiPlannerBehaviorCoreSlotKind.Fallback,
                    workspace.Cards[fallback.CardDraftId],
                    out string roleMessage),
                roleMessage);

            CharacterTestRuntimeAiPlannerBehaviorModuleEntry duplicateRuntime =
                CharacterTestRuntimeAiPlannerBehaviorCoreSlotPolicy.CreateDefaultEntry(
                    CharacterTestRuntimeAiPlannerBehaviorCoreSlotKind.Fallback);
            duplicateRuntime.CoreSlotKind =
                CharacterTestRuntimeAiPlannerBehaviorCoreSlotKind.Flexible;
            duplicateRuntime.Disabled = true;
            group.Entries.Add(new CharacterTestRuntimeAiLabBehaviorEntryDraft(
                duplicateRuntime.EntryId,
                fallback.CardDraftId,
                duplicateRuntime.BehaviorId,
                group.Entries.Count,
                disabled: true,
                duplicateRuntime));
            group.NormalizeOrders();

            Assert.IsTrue(workspace.EnsureCoreSlotContracts(markMigrated: true));
            Assert.AreEqual(
                1,
                group.Entries.Count(entry => string.Equals(
                    entry.BehaviorId,
                    CharacterTestRuntimeAiPlannerBehaviorCompositionCatalog.FoundationIdle,
                    StringComparison.Ordinal)));
            Assert.IsFalse(workspace.EnsureCoreSlotContracts(markMigrated: true));
        }

        [Test]
        [Ignore("v3 migration preserves authored cards and does not create placeholders.")]
        public void RuntimeAiLabDraftWorkspace_TrainingDummyReplacesLegacyCombatWithNoOpPlaceholders()
        {
            CharacterTestRuntimeAiLabDraftWorkspace workspace =
                CharacterTestRuntimeAiLabDraftWorkspace.Import(
                    CharacterTestArenaMatchConfig.CreateDefault());
            CharacterTestRuntimeAiLabBehaviorGroupDraft dummy = workspace.Groups.Values.Single(
                group => string.Equals(
                    group.PublishedStableId,
                    CharacterTestArenaBehaviorGroupDraftStore.TrainingDummyStableId,
                    StringComparison.Ordinal));
            CharacterTestRuntimeAiLabBehaviorGroupDraft melee = workspace.Groups.Values.Single(
                group => string.Equals(
                    group.PublishedStableId,
                    "arena.default-melee",
                    StringComparison.Ordinal));

            for (int i = dummy.Entries.Count - 1; i >= 0; i--)
            {
                CharacterTestRuntimeAiLabBehaviorEntryDraft entry = dummy.Entries[i];
                if (entry.CoreSlotKind != CharacterTestRuntimeAiPlannerBehaviorCoreSlotKind.EngageOrPressure
                    && entry.CoreSlotKind != CharacterTestRuntimeAiPlannerBehaviorCoreSlotKind.Damage)
                {
                    continue;
                }
                dummy.Profile.Entries.Remove(entry.RuntimeEntry);
                dummy.Entries.RemoveAt(i);
            }
            AddLegacyCoreEntry(
                dummy,
                melee,
                CharacterTestRuntimeAiPlannerBehaviorCoreSlotKind.EngageOrPressure);
            AddLegacyCoreEntry(
                dummy,
                melee,
                CharacterTestRuntimeAiPlannerBehaviorCoreSlotKind.Damage);
            dummy.NormalizeOrders();
            Assert.AreEqual(
                4,
                dummy.Entries.Count,
                string.Join(", ", dummy.Entries.Select(entry =>
                    entry.EntryId + "=" + entry.BehaviorId + "/" + entry.CoreSlotKind)));

            Assert.IsTrue(workspace.EnsureCoreSlotContracts(markMigrated: true));
            Assert.AreEqual(4, dummy.Entries.Count);
            CollectionAssert.AreEqual(
                new[]
                {
                    CharacterTestRuntimeAiPlannerBehaviorCompositionCatalog.FoundationApproachAndAttack,
                    CharacterTestRuntimeAiPlannerBehaviorCompositionCatalog.FoundationIdle,
                    CharacterTestRuntimeAiPlannerBehaviorCompositionCatalog.FoundationIdle,
                    CharacterTestRuntimeAiPlannerBehaviorCompositionCatalog.FoundationIdle
                },
                dummy.Entries.Select(entry => entry.BehaviorId).ToArray());
        }

        private static void AddLegacyCoreEntry(
            CharacterTestRuntimeAiLabBehaviorGroupDraft target,
            CharacterTestRuntimeAiLabBehaviorGroupDraft cardSource,
            CharacterTestRuntimeAiPlannerBehaviorCoreSlotKind slotKind)
        {
            CharacterTestRuntimeAiPlannerBehaviorModuleEntry runtimeEntry =
                CharacterTestRuntimeAiPlannerBehaviorCoreSlotPolicy.CreateDefaultEntry(slotKind);
            CharacterTestRuntimeAiLabBehaviorEntryDraft source = cardSource.Entries.First(entry =>
                string.Equals(entry.BehaviorId, runtimeEntry.BehaviorId, StringComparison.Ordinal));
            target.Entries.Add(new CharacterTestRuntimeAiLabBehaviorEntryDraft(
                runtimeEntry.EntryId,
                source.CardDraftId,
                runtimeEntry.BehaviorId,
                runtimeEntry.Order,
                disabled: false,
                runtimeEntry));
        }
    }
}
