using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using MxFramework.CharacterAction;
using MxFramework.Combat.Physics;
using MxFramework.Diagnostics;
using MxFramework.Game.CharacterTest;
using MxFramework.Game.CharacterTest.Editor;
using MxFramework.Network;
using MxFramework.Runtime;
using MxFramework.UI;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace MxFramework.Tests.Game.CharacterTest
{
    public sealed partial class CharacterTestRuntimeAiPlannerFormalizationTests
    {

        [Test]
        public void RuntimeAiPlannerBehaviorCardStandardInspector_DashStrikeUsesTemplatesAndExits()
        {
            CharacterTestRuntimeAiPlannerBehaviorCardDraft draft =
                CharacterTestRuntimeAiPlannerBehaviorCardEditorModel.CreateDashStrikePursuitDraft();
            CharacterTestRuntimeAiPlannerBehaviorCardStandardStepView dash =
                CharacterTestRuntimeAiPlannerBehaviorCardStandardInspector.BuildStepView(
                    draft,
                    draft.Steps[0]);

            Assert.AreEqual(
                CharacterTestRuntimeAiPlannerBehaviorCardStepTemplateKind.DashAbility,
                dash.TemplateKind);
            Assert.AreEqual("突进能力", dash.TemplateName);
            Assert.AreEqual("Ability", dash.ActionLabel);
            Assert.AreEqual("DashStrikeAbilityId", dash.ActionValue);
            Assert.IsTrue(dash.EntryConditions.IsMainEditorCompatible);
            Assert.AreEqual("AND", dash.EntryConditions.OperatorText);
            Assert.GreaterOrEqual(dash.EntryConditions.Presets.Count, 5);
            Assert.AreEqual(1, dash.RunningExits.Count);
            Assert.AreEqual(
                CharacterTestRuntimeAiPlannerBehaviorCardStepExitKind.RunningMonitor,
                dash.RunningExits[0].Kind);
            Assert.AreEqual("light_attack", dash.RunningExits[0].TargetStepId);
            Assert.AreEqual(
                CharacterTestRuntimeAiPlannerBehaviorCardStepExitKind.Completion,
                dash.CompletionExit.Kind);
            Assert.AreEqual("second_dash", dash.CompletionExit.TargetStepId);
            Assert.AreEqual(
                CharacterTestRuntimeAiPlannerBehaviorCardStepExitKind.Failure,
                dash.FailureExit.Kind);
            Assert.AreEqual("End", dash.FailureExit.TargetStepId);

            var complexDraft = new CharacterTestRuntimeAiPlannerBehaviorCardDraft(
                "draft.complex",
                "Complex",
                "Complex condition");
            var complexStep = new CharacterTestRuntimeAiPlannerBehaviorCardStepDraft(
                "complex",
                CharacterTestRuntimeAiPlannerBehaviorCardStepKind.Wait)
            {
                ConditionSummary = "target.exists || target.visible"
            };
            complexDraft.Steps.Add(complexStep);
            CharacterTestRuntimeAiPlannerBehaviorCardStandardStepView complex =
                CharacterTestRuntimeAiPlannerBehaviorCardStandardInspector.BuildStepView(
                    complexDraft,
                    complexStep);
            Assert.IsFalse(complex.EntryConditions.IsMainEditorCompatible);
            StringAssert.Contains("AND", complex.EntryConditions.WarningText);
        }

        [Test]
        public void RuntimeAiPlannerBehaviorCardEditorModel_MigratesLegacyBranchesAndSortsPreview()
        {
            var draft = new CharacterTestRuntimeAiPlannerBehaviorCardDraft(
                "draft.test.legacy",
                "Test.Legacy",
                "Legacy branch test");
            draft.Steps.Add(new CharacterTestRuntimeAiPlannerBehaviorCardStepDraft(
                "step_a",
                CharacterTestRuntimeAiPlannerBehaviorCardStepKind.Wait)
            {
                OnSuccess = "step_b",
                OnFailure = "End",
                OnConditionMet = "step_b",
                ConditionWindowStartFrame = 2,
                ConditionWindowEndFrame = 4
            });
            draft.Steps.Add(new CharacterTestRuntimeAiPlannerBehaviorCardStepDraft(
                "step_b",
                CharacterTestRuntimeAiPlannerBehaviorCardStepKind.Wait)
            {
                OnSuccess = "End",
                OnFailure = "End",
                OnConditionMet = "End"
            });

            CharacterTestRuntimeAiPlannerBehaviorCardEditorModel.EnsureStructuredBranches(draft);
            draft.Steps[0].Branches.Add(
                CharacterTestRuntimeAiPlannerBehaviorCardEditorModel.CreateBranch(
                    "step_a",
                    "condition_early",
                    CharacterTestRuntimeAiPlannerBehaviorCardBranchTriggerType.Condition,
                    "End",
                    "Early condition",
                    "custom condition",
                    1,
                    3,
                    5));

            CharacterTestRuntimeAiPlannerBehaviorCardFlowTimePreview preview =
                CharacterTestRuntimeAiPlannerBehaviorCardEditorModel.BuildFlowTimePreview(draft);

            Assert.AreEqual(3, draft.Steps[1].Branches.Count);
            Assert.AreEqual("step_a.condition_early", preview.Branches[1].BranchId);
            Assert.AreEqual(
                CharacterTestRuntimeAiPlannerBehaviorCardBranchTriggerType.Condition,
                preview.Branches[1].TriggerType);
        }

        [Test]
        public void RuntimeAiPlannerBehaviorCardEditorModel_ValidatesStructuredBranchGraph()
        {
            var draft = new CharacterTestRuntimeAiPlannerBehaviorCardDraft(
                "draft.test.invalid_branches",
                "Test.InvalidBranches",
                "Invalid branches");
            var first = new CharacterTestRuntimeAiPlannerBehaviorCardStepDraft(
                "first",
                CharacterTestRuntimeAiPlannerBehaviorCardStepKind.Wait);
            first.Branches.Add(
                CharacterTestRuntimeAiPlannerBehaviorCardEditorModel.CreateBranch(
                    "first",
                    "condition_unknown",
                    CharacterTestRuntimeAiPlannerBehaviorCardBranchTriggerType.Condition,
                    "missing",
                    "Unknown",
                    "unknown target",
                    0,
                    0,
                    10));
            first.Branches.Add(
                CharacterTestRuntimeAiPlannerBehaviorCardEditorModel.CreateBranch(
                    "first",
                    "default",
                    CharacterTestRuntimeAiPlannerBehaviorCardBranchTriggerType.DefaultSuccess,
                    "second",
                    "Default",
                    "cycle",
                    0,
                    0,
                    100));
            draft.Steps.Add(first);

            var second = new CharacterTestRuntimeAiPlannerBehaviorCardStepDraft(
                "second",
                CharacterTestRuntimeAiPlannerBehaviorCardStepKind.Wait);
            second.Branches.Add(
                CharacterTestRuntimeAiPlannerBehaviorCardEditorModel.CreateBranch(
                    "second",
                    "default",
                    CharacterTestRuntimeAiPlannerBehaviorCardBranchTriggerType.DefaultSuccess,
                    "first",
                    "Default",
                    "cycle",
                    0,
                    0,
                    100));
            draft.Steps.Add(second);

            List<string> diagnostics =
                CharacterTestRuntimeAiPlannerBehaviorCardEditorModel.Validate(draft);

            AssertDiagnosticContains(diagnostics, "condition_unknown");
            AssertDiagnosticContains(diagnostics, "Branch cycle detected");
            AssertDiagnosticContains(diagnostics, "no reachable End path");
        }

        [Test]
        public void RuntimeAiPlannerBehaviorCardEditorModel_UnknownActionShowsDurationWarning()
        {
            var draft = new CharacterTestRuntimeAiPlannerBehaviorCardDraft(
                "draft.test.unknown_action",
                "Test.UnknownAction",
                "Unknown action test");
            draft.Steps.Add(new CharacterTestRuntimeAiPlannerBehaviorCardStepDraft(
                "missing",
                CharacterTestRuntimeAiPlannerBehaviorCardStepKind.UseAction)
            {
                CharacterActionId = MxFramework.Tests.CharacterActionLegacyFixtureIds.From("missing_action"),
                EstimatedDurationFrames = 12,
                OnSuccess = "End",
                OnFailure = "End",
                OnConditionMet = "End"
            });

            CharacterTestRuntimeAiPlannerBehaviorCardFlowTimePreview preview =
                CharacterTestRuntimeAiPlannerBehaviorCardEditorModel.BuildFlowTimePreview(draft);

            Assert.AreEqual(1, preview.Steps.Count);
            Assert.IsFalse(preview.Steps[0].DurationKnown);
            Assert.AreEqual(12, preview.Steps[0].DurationFrames);
            StringAssert.Contains("动作时长未知", preview.Steps[0].WarningText);
            StringAssert.Contains(
                "warning=动作时长未知",
                CharacterTestRuntimeAiPlannerBehaviorCardEditorModel.FormatTimelinePreview(draft.Steps[0]));
        }

        [Test]
        public void RuntimeAiPlannerBehaviorCardEditorModel_ReportsMissingBindingsAndStepLimit()
        {
            CharacterTestRuntimeAiPlannerBehaviorCardDraft draft =
                CharacterTestRuntimeAiPlannerBehaviorCardEditorModel.CreateDashStrikePursuitDraft();
            draft.Steps[0].AbilityId = 0;
            draft.Steps[2].CharacterActionId = MxFramework.CharacterAction.CharacterActionId.None;
            draft.Steps.Add(new CharacterTestRuntimeAiPlannerBehaviorCardStepDraft(
                "wait_a",
                CharacterTestRuntimeAiPlannerBehaviorCardStepKind.Wait));
            draft.Steps.Add(new CharacterTestRuntimeAiPlannerBehaviorCardStepDraft(
                "wait_b",
                CharacterTestRuntimeAiPlannerBehaviorCardStepKind.Wait));
            draft.Steps.Add(new CharacterTestRuntimeAiPlannerBehaviorCardStepDraft(
                "wait_c",
                CharacterTestRuntimeAiPlannerBehaviorCardStepKind.Wait));

            List<string> diagnostics =
                CharacterTestRuntimeAiPlannerBehaviorCardEditorModel.Validate(draft);

            AssertDiagnosticContains(diagnostics, "步骤超过上限");
            AssertDiagnosticContains(diagnostics, "未绑定 Ability");
            AssertDiagnosticContains(diagnostics, "未绑定 CharacterAction");
        }

        [Test]
        public void RuntimeAiPlannerWindow_BehaviorCardTabShowsFourEditorRegions()
        {
            CharacterTestRuntimeAiPlannerWindow window =
                EditorWindow.GetWindow<CharacterTestRuntimeAiPlannerWindow>();
            try
            {
                window.CreateGUI();
                SetWindowField(window, "_activeTab", 5);
                InvokeWindowMethod(window, "RefreshView");

                AssertTextPresent(window.rootVisualElement, "行为卡： 突进追击");
                AssertTextPresent(window.rootVisualElement, "Start");
                AssertTextPresent(window.rootVisualElement, "行为卡库");
                AssertTextPresent(window.rootVisualElement, "主执行链");
                AssertTextPresent(window.rootVisualElement, "分支流程图");
                AssertTextPresent(window.rootVisualElement, "动作 / 判断 / End");
                AssertTextPresent(window.rootVisualElement, "自动适配");
                AssertTextPresent(window.rootVisualElement, "无横向滚动");
                AssertTextPresent(window.rootVisualElement, "全局帧刻度");
                AssertTextPresent(window.rootVisualElement, "命中");
                AssertTextPresent(window.rootVisualElement, "判断");
                AssertTextPresent(window.rootVisualElement, "成功分支");
                AssertTextPresent(window.rootVisualElement, "失败分支");
                AssertTextPresent(window.rootVisualElement, "条件满足分支");
                AssertTextPresent(window.rootVisualElement, "已进 1m");
                AssertTextPresent(window.rootVisualElement, "未进 1m");
                AssertTextPresent(window.rootVisualElement, "失败/中断");
                AssertTextPresent(window.rootVisualElement, "二段后进 1m");
                AssertTextPresent(window.rootVisualElement, "End");
                AssertTextPresent(window.rootVisualElement, "交回行为组重新决策");
                AssertTextPresent(window.rootVisualElement, "情境预估");
                AssertTextPresent(window.rootVisualElement, "情境");
                AssertTextPresent(window.rootVisualElement, "路径");
                AssertTextPresent(window.rootVisualElement, "提示");
                AssertTextPresent(window.rootVisualElement, "高级诊断 / 选中步骤只读摘要");
                AssertTextPresent(window.rootVisualElement, "突进追击");
                AssertTextPresent(window.rootVisualElement, "DashStrikeAbilityId");
                AssertTextPresent(window.rootVisualElement, "second_dash");
                AssertTextPresent(window.rootVisualElement, "light_attack");
                AssertTextPresent(window.rootVisualElement, "light_attack_2");
                AssertTextPresent(window.rootVisualElement, "突进斩");
                AssertTextPresent(window.rootVisualElement, "轻攻击");
                AssertTextPresent(window.rootVisualElement, "第二段");
                AssertTextPresent(window.rootVisualElement, "时长未知");
                AssertTextPresent(window.rootVisualElement, "0-76f | 命中 18-24 | 切换 62-72");
                AssertTextPresent(window.rootVisualElement, "3-7m 看见目标时突进");
                AssertTextPresent(window.rootVisualElement, "Editor-only 草稿");
                AssertTextAbsent(window.rootVisualElement, "应用到 Play Mode");
                AssertTextAbsent(window.rootVisualElement, "编译覆盖底层 Profile 草稿");
                AssertTextAbsent(window.rootVisualElement, "行为流程时间图");
                AssertTextAbsent(window.rootVisualElement, "步骤卡工作台");
                AssertTextAbsent(window.rootVisualElement, "步骤列表 / 高级排查");
                AssertTextAbsent(window.rootVisualElement, "成功分支 : dash");

                Assert.NotNull(window.rootVisualElement.Q(
                    CharacterTestRuntimeAiPlannerWindow.BehaviorCardLibraryName));
                Assert.NotNull(window.rootVisualElement.Q(
                    CharacterTestRuntimeAiPlannerWindow.BehaviorCardFlowChainName));
                Assert.NotNull(window.rootVisualElement.Q(
                    CharacterTestRuntimeAiPlannerWindow.BehaviorCardFlowchartCanvasName));
                Assert.NotNull(window.rootVisualElement.Q(
                    CharacterTestRuntimeAiPlannerWindow.BehaviorCardFlowchartStartNodeName));
                Assert.NotNull(window.rootVisualElement.Q(
                    CharacterTestRuntimeAiPlannerWindow.BehaviorCardFlowchartActionNodeName));
                Assert.NotNull(window.rootVisualElement.Q(
                    CharacterTestRuntimeAiPlannerWindow.BehaviorCardFlowchartDecisionNodeName));
                Assert.NotNull(window.rootVisualElement.Q(
                    CharacterTestRuntimeAiPlannerWindow.BehaviorCardFlowchartEndNodeName));
                Assert.NotNull(window.rootVisualElement.Q(
                    CharacterTestRuntimeAiPlannerWindow.BehaviorCardFlowchartEdgeName));
                Assert.NotNull(window.rootVisualElement.Q(
                    CharacterTestRuntimeAiPlannerWindow.BehaviorCardFlowchartEdgeLabelName));
                Assert.NotNull(window.rootVisualElement.Q(
                    CharacterTestRuntimeAiPlannerWindow.BehaviorCardStepTimelineTrackName));
                Assert.NotNull(window.rootVisualElement.Q(
                    CharacterTestRuntimeAiPlannerWindow.BehaviorCardStepCompactTimelineName));
                Assert.NotNull(window.rootVisualElement.Q(
                    CharacterTestRuntimeAiPlannerWindow.BehaviorCardStepTimelineEventName));
                Assert.Null(window.rootVisualElement.Q(
                    CharacterTestRuntimeAiPlannerWindow.BehaviorCardStepBranchChipName));
                Assert.NotNull(window.rootVisualElement.Q(
                    CharacterTestRuntimeAiPlannerWindow.BehaviorCardStepBranchPanelName));
                Assert.Null(window.rootVisualElement.Q(
                    CharacterTestRuntimeAiPlannerWindow.BehaviorCardBranchRuleRowName));
                Assert.NotNull(window.rootVisualElement.Q(
                    CharacterTestRuntimeAiPlannerWindow.BehaviorCardStepWarningChipName));
                Assert.Null(window.rootVisualElement.Q(
                    CharacterTestRuntimeAiPlannerWindow.BehaviorCardFlowBoardName));
                Assert.Null(window.rootVisualElement.Q(
                    CharacterTestRuntimeAiPlannerWindow.BehaviorCardFlowBoardLeftLaneName));
                Assert.Null(window.rootVisualElement.Q(
                    CharacterTestRuntimeAiPlannerWindow.BehaviorCardFlowBoardMainLaneName));
                Assert.Null(window.rootVisualElement.Q(
                    CharacterTestRuntimeAiPlannerWindow.BehaviorCardFlowBoardRightLaneName));
                Assert.Null(window.rootVisualElement.Q(
                    CharacterTestRuntimeAiPlannerWindow.BehaviorCardFlowBoardBranchChipName));
                Assert.Null(window.rootVisualElement.Q(
                    CharacterTestRuntimeAiPlannerWindow.BehaviorCardFlowBoardConnectorName));
                Assert.Null(window.rootVisualElement.Q(
                    CharacterTestRuntimeAiPlannerWindow.BehaviorCardFlowBoardEndName));
                Assert.Null(window.rootVisualElement.Q(
                    CharacterTestRuntimeAiPlannerWindow.BehaviorCardGraphCanvasName));
                ScrollView editorScroll = window.rootVisualElement.Q<ScrollView>(
                    CharacterTestRuntimeAiPlannerWindow.BehaviorCardEditorScrollName);
                Assert.NotNull(editorScroll);
                Assert.AreEqual(ScrollViewMode.Vertical, editorScroll.mode);
                Assert.AreEqual(ScrollerVisibility.Hidden, editorScroll.horizontalScrollerVisibility);
                Assert.Null(window.rootVisualElement.Q(
                    CharacterTestRuntimeAiPlannerWindow.BehaviorCardGraphNodeLayerName));
                Assert.Null(window.rootVisualElement.Q(
                    CharacterTestRuntimeAiPlannerWindow.BehaviorCardGraphEdgeLayerName));
                Assert.Null(window.rootVisualElement.Q(
                    CharacterTestRuntimeAiPlannerWindow.BehaviorCardGraphEdgeName));
                Assert.Null(window.rootVisualElement.Q(
                    CharacterTestRuntimeAiPlannerWindow.BehaviorCardGraphEdgeLabelName));
                Assert.Null(window.rootVisualElement.Q(
                    CharacterTestRuntimeAiPlannerWindow.BehaviorCardMainChainContainerName));
                Assert.Null(window.rootVisualElement.Q(
                    CharacterTestRuntimeAiPlannerWindow.BehaviorCardChainConnectorName));
                Assert.NotNull(window.rootVisualElement.Q(
                    CharacterTestRuntimeAiPlannerWindow.BehaviorCardFlowTimeRulerName));
                Assert.NotNull(window.rootVisualElement.Q(
                    CharacterTestRuntimeAiPlannerWindow.BehaviorCardBranchLegendName));
                Assert.Null(window.rootVisualElement.Q(
                    CharacterTestRuntimeAiPlannerWindow.BehaviorCardTerminalPanelName));
                Assert.Null(window.rootVisualElement.Q(
                    CharacterTestRuntimeAiPlannerWindow.BehaviorCardConditionWindowFieldName));
                Assert.Null(window.rootVisualElement.Q(
                    CharacterTestRuntimeAiPlannerWindow.BehaviorCardStepInspectorName));
                Assert.NotNull(window.rootVisualElement.Q(
                    CharacterTestRuntimeAiPlannerWindow.BehaviorCardTimelinePreviewName));
                Assert.NotNull(window.rootVisualElement.Q(
                    CharacterTestRuntimeAiPlannerWindow.BehaviorCardForecastName));
                Assert.NotNull(window.rootVisualElement.Q(
                    CharacterTestRuntimeAiPlannerWindow.BehaviorCardForecastTableName));
                Assert.NotNull(window.rootVisualElement.Q(
                    CharacterTestRuntimeAiPlannerWindow.BehaviorCardPathForecastRowName));
                Assert.NotNull(window.rootVisualElement.Q<TextField>(
                    CharacterTestRuntimeAiPlannerWindow.BehaviorCardSearchFieldName));
                Assert.NotNull(window.rootVisualElement.Q<PopupField<string>>(
                    CharacterTestRuntimeAiPlannerWindow.BehaviorCardCapabilityFilterName));
                VisualElement rightPanel = GetWindowField<VisualElement>(window, "_rightPanel");
                Assert.NotNull(rightPanel);
                Assert.AreEqual(DisplayStyle.None, rightPanel.style.display.value);
                Assert.AreEqual(0f, rightPanel.style.width.value.value, 0.01f);
                Assert.AreEqual(
                    4,
                    CountNamedElements(
                        window.rootVisualElement,
                        CharacterTestRuntimeAiPlannerWindow.BehaviorCardFlowchartActionNodeName));
                Assert.AreEqual(
                    2,
                    CountNamedElements(
                        window.rootVisualElement,
                        CharacterTestRuntimeAiPlannerWindow.BehaviorCardStepCompactTimelineName));
                Assert.AreEqual(
                    9,
                    CountNamedElements(
                        window.rootVisualElement,
                        CharacterTestRuntimeAiPlannerWindow.BehaviorCardFlowchartEdgeLabelName));
                Assert.AreEqual(
                    0,
                    CountNamedElements(
                        window.rootVisualElement,
                        CharacterTestRuntimeAiPlannerWindow.BehaviorCardFlowBoardConnectorName));
            }
            finally
            {
                window.Close();
            }
        }

        [Test]
        public void RuntimeAiPlannerWindow_BehaviorCardTabPreservesScrollWhenSelectingStep()
        {
            CharacterTestRuntimeAiPlannerWindow window =
                EditorWindow.GetWindow<CharacterTestRuntimeAiPlannerWindow>();
            try
            {
                window.CreateGUI();
                SetWindowField(window, "_activeTab", 5);
                InvokeWindowMethod(window, "RefreshView");

                ScrollView editorScroll = window.rootVisualElement.Q<ScrollView>(
                    CharacterTestRuntimeAiPlannerWindow.BehaviorCardEditorScrollName);
                VisualElement flowchart = window.rootVisualElement.Q(
                    CharacterTestRuntimeAiPlannerWindow.BehaviorCardFlowchartCanvasName);
                Assert.NotNull(editorScroll);
                Assert.NotNull(flowchart);

                editorScroll.scrollOffset = new Vector2(0f, 240f);
                InvokeWindowMethod(window, "RefreshBehaviorCardViewPreservingScroll");

                ScrollView refreshedEditorScroll = window.rootVisualElement.Q<ScrollView>(
                    CharacterTestRuntimeAiPlannerWindow.BehaviorCardEditorScrollName);
                VisualElement refreshedFlowchart = window.rootVisualElement.Q(
                    CharacterTestRuntimeAiPlannerWindow.BehaviorCardFlowchartCanvasName);
                Assert.NotNull(refreshedEditorScroll);
                Assert.NotNull(refreshedFlowchart);
                Assert.AreEqual(240f, refreshedEditorScroll.scrollOffset.y, 0.01f);
            }
            finally
            {
                window.Close();
            }
        }

        [Test]
        public void RuntimeAiPlannerWindow_BehaviorCardMainChainUsesFlowchart()
        {
            CharacterTestRuntimeAiPlannerWindow window =
                EditorWindow.GetWindow<CharacterTestRuntimeAiPlannerWindow>();
            try
            {
                window.CreateGUI();
                SetWindowField(window, "_activeTab", 5);
                InvokeWindowMethod(window, "RefreshView");

                VisualElement flowchart = window.rootVisualElement.Q(
                    CharacterTestRuntimeAiPlannerWindow.BehaviorCardFlowchartCanvasName);
                VisualElement actionNode = window.rootVisualElement.Q(
                    CharacterTestRuntimeAiPlannerWindow.BehaviorCardFlowchartActionNodeName);
                VisualElement decisionNode = window.rootVisualElement.Q(
                    CharacterTestRuntimeAiPlannerWindow.BehaviorCardFlowchartDecisionNodeName);
                VisualElement endNode = window.rootVisualElement.Q(
                    CharacterTestRuntimeAiPlannerWindow.BehaviorCardFlowchartEndNodeName);
                ScrollView oldHorizontalScroll = window.rootVisualElement.Q<ScrollView>(
                    CharacterTestRuntimeAiPlannerWindow.BehaviorCardMainChainContainerName);
                Assert.NotNull(flowchart);
                Assert.NotNull(actionNode);
                Assert.NotNull(decisionNode);
                Assert.NotNull(endNode);
                Assert.IsNull(oldHorizontalScroll);
                Assert.AreEqual(
                    -1,
                    GetWindowField<int>(window, "_selectedBehaviorCardStepIndex"));
                Assert.NotNull(window.rootVisualElement.Q(
                    CharacterTestRuntimeAiPlannerWindow.BehaviorCardLibraryName));
                Assert.Null(window.rootVisualElement.Q(
                    CharacterTestRuntimeAiPlannerWindow.BehaviorCardStepInspectorName));
                Assert.Null(window.rootVisualElement.Q(
                    CharacterTestRuntimeAiPlannerWindow.BehaviorCardFlowBoardName));
                Assert.Null(window.rootVisualElement.Q(
                    CharacterTestRuntimeAiPlannerWindow.BehaviorCardGraphCanvasName));
                Assert.Null(window.rootVisualElement.Q(
                    CharacterTestRuntimeAiPlannerWindow.BehaviorCardGraphEdgeLayerName));

                VisualElement edgeLabel = window.rootVisualElement.Q(
                    CharacterTestRuntimeAiPlannerWindow.BehaviorCardFlowchartEdgeLabelName);
                Assert.NotNull(edgeLabel);
                Assert.IsInstanceOf<CharacterTestRuntimeAiPlannerBehaviorCardFlowchartEdgeViewModel>(
                    edgeLabel.userData);
                Assert.Null(window.rootVisualElement.Q(
                    CharacterTestRuntimeAiPlannerWindow.BehaviorCardChainConnectorName));

                Assert.IsInstanceOf<CharacterTestRuntimeAiPlannerBehaviorCardFlowchartNodeViewModel>(
                    actionNode.userData);
                AssertTextPresent(actionNode, "执行");
                AssertTextPresent(actionNode, "突进斩");
                AssertTextPresent(actionNode, "时长未知");
                AssertTextAbsent(actionNode, "UseAbility");
                AssertTextAbsent(actionNode, "DashStrikeAbilityId");
                AssertTextAbsent(actionNode, "? frames");
                AssertTextAbsent(actionNode, "监听");
                AssertTextAbsent(actionNode, "完成");
                AssertTextAbsent(actionNode, "失败");
                InvokeWindowMethod(window, "SelectBehaviorCardFlowchartNode", actionNode.userData);

                Assert.AreEqual(
                    0,
                    GetWindowField<int>(window, "_selectedBehaviorCardStepIndex"));
                Assert.NotNull(window.rootVisualElement.Q(
                    CharacterTestRuntimeAiPlannerWindow.BehaviorCardStepInspectorName));
                Assert.Null(window.rootVisualElement.Q(
                    CharacterTestRuntimeAiPlannerWindow.BehaviorCardLibraryName));
                Assert.Null(window.rootVisualElement.Q(
                    CharacterTestRuntimeAiPlannerWindow.BehaviorCardBranchRuleRowName));
                AssertTextPresent(window.rootVisualElement, "步骤 Inspector");
                AssertTextPresent(window.rootVisualElement, "当前步骤摘要");
                AssertTextPresent(window.rootVisualElement, "执行逻辑");
                AssertTextPresent(window.rootVisualElement, "模板参数");
                AssertTextPresent(window.rootVisualElement, "进入条件（模板生成）");
                AssertTextPresent(window.rootVisualElement, "执行中监听出口");
                AssertTextPresent(window.rootVisualElement, "完成出口");
                AssertTextPresent(window.rootVisualElement, "失败/中断出口");
                AssertTextPresent(window.rootVisualElement, "突进能力");
                AssertTextPresent(window.rootVisualElement, "目标步骤");
                AssertTextAbsent(window.rootVisualElement, "高级 / Raw");
                AssertTextAbsent(window.rootVisualElement, "兼容字段");
                AssertTextAbsent(window.rootVisualElement, "触发条件");
                AssertTextAbsent(window.rootVisualElement, "预计持续帧");
                AssertTextAbsent(window.rootVisualElement, "Branch Rules");

                edgeLabel = window.rootVisualElement.Q(
                    CharacterTestRuntimeAiPlannerWindow.BehaviorCardFlowchartEdgeLabelName);
                Assert.NotNull(edgeLabel);
                InvokeWindowMethod(window, "SelectBehaviorCardFlowchartEdge", edgeLabel.userData);

                AssertTextPresent(window.rootVisualElement, "当前选中");
                AssertTextPresent(window.rootVisualElement, "出口规则");
                Assert.NotNull(window.rootVisualElement.Q(
                    CharacterTestRuntimeAiPlannerWindow.BehaviorCardStepInspectorName));
                Assert.Null(window.rootVisualElement.Q(
                    CharacterTestRuntimeAiPlannerWindow.BehaviorCardLibraryName));

                InvokeWindowMethod(window, "ClearBehaviorCardFlowchartSelection");

                Assert.AreEqual(
                    -1,
                    GetWindowField<int>(window, "_selectedBehaviorCardStepIndex"));
                Assert.AreEqual(
                    string.Empty,
                    GetWindowField<string>(window, "_selectedBehaviorCardBranchId"));
                Assert.NotNull(window.rootVisualElement.Q(
                    CharacterTestRuntimeAiPlannerWindow.BehaviorCardLibraryName));
                Assert.Null(window.rootVisualElement.Q(
                    CharacterTestRuntimeAiPlannerWindow.BehaviorCardStepInspectorName));
            }
            finally
            {
                window.Close();
            }
        }

        [Test]
        public void RuntimeAiPlannerWindow_BehaviorCardTabEditsInMemoryDraft()
        {
            CharacterTestRuntimeAiPlannerWindow window =
                EditorWindow.GetWindow<CharacterTestRuntimeAiPlannerWindow>();
            try
            {
                window.CreateGUI();
                SetWindowField(window, "_activeTab", 5);
                InvokeWindowMethod(window, "RefreshView");

                List<CharacterTestRuntimeAiPlannerBehaviorCardDraft> drafts =
                    GetWindowField<List<CharacterTestRuntimeAiPlannerBehaviorCardDraft>>(
                        window,
                        "_behaviorCardDrafts");
                Assert.NotNull(drafts);
                Assert.AreEqual(1, drafts.Count);
                Assert.AreEqual(4, drafts[0].Steps.Count);

                Button add = window.rootVisualElement.Q<Button>(
                    CharacterTestRuntimeAiPlannerWindow.BehaviorCardAddStepButtonName);
                Assert.NotNull(add);
                PressButton(add);

                Assert.AreEqual(5, drafts[0].Steps.Count);
                Assert.AreEqual("step_5", drafts[0].Steps[4].StepId);
                Assert.AreEqual(
                    CharacterTestRuntimeAiPlannerBehaviorCardStepKind.Wait,
                    drafts[0].Steps[4].Kind);

                Button save = window.rootVisualElement.Q<Button>(
                    CharacterTestRuntimeAiPlannerWindow.BehaviorCardSaveDraftButtonName);
                Assert.NotNull(save);
                PressButton(save);

                Assert.AreEqual(1, drafts[0].SavedRevision);
                AssertTextPresent(window.rootVisualElement, "当前窗口内存 rev 1");
            }
            finally
            {
                window.Close();
            }
        }

        [Test]
        public void RuntimeAiPlannerWindow_BehaviorGroupFlowPreviewExpandOpensDetailWindow()
        {
            CharacterTestRuntimeAiPlannerWindow window =
                EditorWindow.GetWindow<CharacterTestRuntimeAiPlannerWindow>();
            CharacterTestRuntimeAiPlannerFlowPreviewWindow detailWindow = null;
            try
            {
                window.CreateGUI();
                SetWindowField(window, "_activeTab", 3);
                InvokeWindowMethod(window, "RefreshView");

                Button expand = window.rootVisualElement.Q<Button>(
                    CharacterTestRuntimeAiPlannerWindow.BehaviorGroupFlowExpandButtonName);
                Assert.NotNull(expand);
                PressButton(expand);

                CharacterTestRuntimeAiPlannerFlowPreviewWindow[] windows =
                    UnityEngine.Resources.FindObjectsOfTypeAll<CharacterTestRuntimeAiPlannerFlowPreviewWindow>();
                Assert.Greater(windows.Length, 0);
                detailWindow = windows[0];
                Assert.NotNull(detailWindow.rootVisualElement.Q<ScrollView>(
                    CharacterTestRuntimeAiPlannerFlowPreview.ScrollName));
                Assert.NotNull(detailWindow.rootVisualElement.Q(
                    CharacterTestRuntimeAiPlannerFlowPreview.StateNodeName("movement")));
                Assert.NotNull(detailWindow.rootVisualElement.Q(
                    CharacterTestRuntimeAiPlannerFlowPreview.StateNodeName("combat")));
            }
            finally
            {
                if (detailWindow != null)
                    detailWindow.Close();
                window.Close();
            }
        }

    }
}
