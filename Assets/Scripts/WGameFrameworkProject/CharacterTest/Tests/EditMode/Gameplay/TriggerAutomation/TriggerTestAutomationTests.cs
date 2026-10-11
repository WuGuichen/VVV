using System;
using System.Collections.Generic;
using System.IO;
using MxFramework.Diagnostics;
using MxFramework.Gameplay;
using MxFramework.Gameplay.Triggers;
using MxFramework.Runtime;
using MxFramework.Tests.Gameplay.TriggerAutomation.Scenarios.CharacterTest;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace MxFramework.Tests.Gameplay.TriggerAutomation
{
    public sealed class TriggerTestAutomationTests
    {
        private const string AutomationRoot =
            "Assets/Scripts/WGameFrameworkProject/CharacterTest/Tests/EditMode/Gameplay/TriggerAutomation";

        [SetUp]
        public void SetUp()
        {
            TriggerTestRuntimeAiLabEnterPlayModeWorkflow.ClearForTests();
            TriggerTestRuntimeAiLabOpenRunningBehaviorLogicWorkflow
                .ClearForTests();
            TriggerTestRuntimeAiLabCharacterActionAuthoringWorkflow
                .ClearForTests();
        }

        [TearDown]
        public void TearDown()
        {
            TriggerTestRuntimeAiLabEnterPlayModeWorkflow.ClearForTests();
            TriggerTestRuntimeAiLabOpenRunningBehaviorLogicWorkflow
                .ClearForTests();
        }

        [Test]
        public void Discovery_FindsAttributedScenarioAndWritesStableIndex()
        {
            TriggerTestDiscoveryResult discovery =
                TriggerTestScenarioDiscovery.Discover(
                    typeof(TriggerTestAutomationTests).Assembly);

            Assert.That(discovery.Succeeded, Is.True, Describe(discovery));
            Assert.That(discovery.Scenarios.Count, Is.EqualTo(32));
            Assert.That(
                ScenarioIds(discovery),
                Is.EquivalentTo(new[]
                {
                    RuntimeAiLabOpenSceneScenario.ScenarioId,
                    RuntimeAiLabOpenRunningBehaviorLogicScenario.ScenarioId,
                    RuntimeAiLabCharacterActionAuthoringScenario.ScenarioId,
                    RuntimeAiLabEnterPlayModeScenario.ScenarioId,
                    RuntimeAiLabStartTestScenario.ScenarioId,
                    RuntimeAiLabPlayerPublishFlowScenario.ScenarioId,
                    RuntimeAiLabPlayModeStepScenario.ScenarioId,
                    RuntimeAiLabResumeSimulationScenario.ScenarioId,
                    RuntimeAiLabPauseSimulationScenario.ScenarioId,
                    RuntimeAiLabResetTestScenario.ScenarioId,
                    RuntimeAiLabSelectHistoricalFrameScenario.ScenarioId,
                    RuntimeAiLabSelectLatestFrameScenario.ScenarioId,
                    RuntimeAiLabRequestBranchRecomputeScenario.ScenarioId,
                    RuntimeAiLabConfirmBranchRecomputeScenario.ScenarioId,
                    RuntimeAiLabCancelBranchRecomputeScenario.ScenarioId,
                    RuntimeAiLabZoomInTimelineScenario.ScenarioId,
                    RuntimeAiLabZoomOutTimelineScenario.ScenarioId,
                    RuntimeAiLabFitTimelineScenario.ScenarioId,
                    RuntimeAiLabSelectTimelineEventScenario.ScenarioId,
                    RuntimeAiLabLocateEventStartScenario.ScenarioId,
                    RuntimeAiLabLocateEventEndScenario.ScenarioId,
                    RuntimeAiLabCloseEventDetailsScenario.ScenarioId,
                    RuntimeAiLabSwitchTimelineModeScenario.ScenarioId,
                    RuntimeAiLabOpenBehaviorLogicTabScenario.ScenarioId,
                    RuntimeAiLabSelectSetupSlotScenario.ScenarioId,
                    RuntimeAiLabSelectSetupCharacterScenario.ScenarioId,
                    RuntimeAiLabSelectSetupBehaviorGroupScenario.ScenarioId,
                    RuntimeAiLabEnableSetupSlotScenario.ScenarioId,
                    RuntimeAiLabDisableSetupSlotScenario.ScenarioId,
                    RuntimeAiLabTrainingDummyCopyFlowScenario.ScenarioId,
                    RuntimeAiLabAuthoringUxFlowScenario.ScenarioId,
                    TacticalUiOverviewInspectScenario.ScenarioId
                }));

            string indexPath =
                TriggerTestReportWriter.WriteIndex(discovery.Scenarios);
            Assert.That(File.Exists(indexPath), Is.True);
            JObject index = JObject.Parse(File.ReadAllText(indexPath));
            Assert.That(
                (string)index["schema"],
                Is.EqualTo("mx.trigger.test.index.v1"));
            Assert.That(((JArray)index["scenarios"]).Count, Is.EqualTo(32));
            Assert.That(
                IndexScenarioIds(index),
                Is.EquivalentTo(new[]
                {
                    RuntimeAiLabOpenSceneScenario.ScenarioId,
                    RuntimeAiLabOpenRunningBehaviorLogicScenario.ScenarioId,
                    RuntimeAiLabCharacterActionAuthoringScenario.ScenarioId,
                    RuntimeAiLabEnterPlayModeScenario.ScenarioId,
                    RuntimeAiLabStartTestScenario.ScenarioId,
                    RuntimeAiLabPlayerPublishFlowScenario.ScenarioId,
                    RuntimeAiLabPlayModeStepScenario.ScenarioId,
                    RuntimeAiLabResumeSimulationScenario.ScenarioId,
                    RuntimeAiLabPauseSimulationScenario.ScenarioId,
                    RuntimeAiLabResetTestScenario.ScenarioId,
                    RuntimeAiLabSelectHistoricalFrameScenario.ScenarioId,
                    RuntimeAiLabSelectLatestFrameScenario.ScenarioId,
                    RuntimeAiLabRequestBranchRecomputeScenario.ScenarioId,
                    RuntimeAiLabConfirmBranchRecomputeScenario.ScenarioId,
                    RuntimeAiLabCancelBranchRecomputeScenario.ScenarioId,
                    RuntimeAiLabZoomInTimelineScenario.ScenarioId,
                    RuntimeAiLabZoomOutTimelineScenario.ScenarioId,
                    RuntimeAiLabFitTimelineScenario.ScenarioId,
                    RuntimeAiLabSelectTimelineEventScenario.ScenarioId,
                    RuntimeAiLabLocateEventStartScenario.ScenarioId,
                    RuntimeAiLabLocateEventEndScenario.ScenarioId,
                    RuntimeAiLabCloseEventDetailsScenario.ScenarioId,
                    RuntimeAiLabSwitchTimelineModeScenario.ScenarioId,
                    RuntimeAiLabOpenBehaviorLogicTabScenario.ScenarioId,
                    RuntimeAiLabSelectSetupSlotScenario.ScenarioId,
                    RuntimeAiLabSelectSetupCharacterScenario.ScenarioId,
                    RuntimeAiLabSelectSetupBehaviorGroupScenario.ScenarioId,
                    RuntimeAiLabEnableSetupSlotScenario.ScenarioId,
                    RuntimeAiLabDisableSetupSlotScenario.ScenarioId,
                    RuntimeAiLabTrainingDummyCopyFlowScenario.ScenarioId,
                    RuntimeAiLabAuthoringUxFlowScenario.ScenarioId,
                    TacticalUiOverviewInspectScenario.ScenarioId
                }));
        }

        [Test]
        public void Discovery_ReportsDuplicateIdAndMissingConstructor()
        {
            TriggerTestDiscoveryResult discovery =
                TriggerTestScenarioDiscovery.DiscoverTypes(
                    new[]
                    {
                        typeof(DuplicateScenarioA),
                        typeof(DuplicateScenarioB),
                        typeof(MissingConstructorScenario)
                    },
                    requireAttribute: false);

            Assert.That(discovery.Succeeded, Is.False);
            Assert.That(discovery.Errors.Count, Is.EqualTo(2));
            Assert.That(
                Describe(discovery),
                Does.Contain("Duplicate scenario id"));
            Assert.That(
                Describe(discovery),
                Does.Contain("parameterless constructor"));
        }

        [Test]
        public void Discovery_SortsCurrentTypesAndKeepsNoRemovedRegistration()
        {
            TriggerTestDiscoveryResult first =
                TriggerTestScenarioDiscovery.DiscoverTypes(
                    new[]
                    {
                        typeof(ZetaScenario),
                        typeof(AlphaScenario)
                    },
                    requireAttribute: false);

            Assert.That(first.Succeeded, Is.True, Describe(first));
            Assert.That(
                first.Scenarios[0].Descriptor.ScenarioId.Value,
                Is.EqualTo("mx.trigger.test.test.alpha"));
            Assert.That(
                first.Scenarios[1].Descriptor.ScenarioId.Value,
                Is.EqualTo("mx.trigger.test.test.zeta"));

            TriggerTestDiscoveryResult afterRemoval =
                TriggerTestScenarioDiscovery.DiscoverTypes(
                    new[] { typeof(ZetaScenario) },
                    requireAttribute: false);

            Assert.That(afterRemoval.Succeeded, Is.True, Describe(afterRemoval));
            Assert.That(afterRemoval.Scenarios.Count, Is.EqualTo(1));
            Assert.That(
                afterRemoval.Scenarios[0].Descriptor.ScenarioId.Value,
                Is.EqualTo("mx.trigger.test.test.zeta"));
        }

        [Test]
        public void ManualScenarioDisplayMap_CentralizesKnownAndFallbackMetadata()
        {
            Assert.That(
                TriggerTestManualScenarioDisplayMap.Count,
                Is.EqualTo(30));

            TriggerTestManualScenarioDisplay environment =
                TriggerTestManualScenarioDisplayMap.Resolve(
                    new RuntimeAiLabOpenSceneScenario().Descriptor);
            Assert.That(
                environment.DisplayName,
                Is.EqualTo("打开 Runtime AI Lab 场景"));
            Assert.That(environment.GroupName, Is.EqualTo("环境准备"));
            Assert.That(environment.Order, Is.EqualTo(0));

            TriggerTestManualScenarioDisplay timeline =
                TriggerTestManualScenarioDisplayMap.Resolve(
                    new RuntimeAiLabSelectTimelineEventScenario().Descriptor);
            Assert.That(
                timeline.DisplayName,
                Is.EqualTo("选择指定时间轴事件"));
            Assert.That(timeline.GroupName, Is.EqualTo("时间轴观察"));
            Assert.That(timeline.Order, Is.EqualTo(15));

            TriggerTestManualScenarioDisplay trainingDummyCopy =
                TriggerTestManualScenarioDisplayMap.Resolve(
                    new RuntimeAiLabTrainingDummyCopyFlowScenario().Descriptor);
            Assert.That(
                trainingDummyCopy.DisplayName,
                Is.EqualTo("使用木桩副本开始测试"));
            Assert.That(trainingDummyCopy.GroupName, Is.EqualTo("实验设置"));
            Assert.That(trainingDummyCopy.Order, Is.EqualTo(26));

            var unmappedDescriptor = new TriggerTestDescriptor(
                "mx.trigger.test.test.unmapped-display",
                "Unmapped display",
                "test",
                TriggerTestExecutionProfile.NoEngine,
                maxFrames: 1);
            TriggerTestManualScenarioDisplay fallback =
                TriggerTestManualScenarioDisplayMap.Resolve(
                    unmappedDescriptor);
            Assert.That(fallback.DisplayName, Is.EqualTo("Unmapped display"));
            Assert.That(fallback.GroupName, Is.EqualTo("其他"));
            Assert.That(fallback.Order, Is.EqualTo(100));
        }

        [Test]
        public void SourceLayout_MatchesScenarioModuleSlugAndClass()
        {
            TriggerTestDiscoveryResult discovery =
                TriggerTestScenarioDiscovery.Discover(
                    typeof(TriggerTestAutomationTests).Assembly);
            Assert.That(discovery.Succeeded, Is.True, Describe(discovery));

            string[] guids = AssetDatabase.FindAssets(
                "t:MonoScript",
                new[] { AutomationRoot + "/Scenarios" });
            for (int scenarioIndex = 0;
                 scenarioIndex < discovery.Scenarios.Count;
                 scenarioIndex++)
            {
                ITriggerTestScenario scenario =
                    discovery.Scenarios[scenarioIndex];
                string actualPath = FindScriptPath(
                    guids,
                    scenario.GetType());
                string expectedSegment =
                    "/Scenarios/" +
                    scenario.Descriptor.Module +
                    "/" +
                    TriggerTestIdentity.GetScenarioSlug(
                        scenario.Descriptor.ScenarioId) +
                    "/";
                Assert.That(
                    actualPath,
                    Does.Contain(expectedSegment),
                    scenario.Descriptor.ScenarioId.Value);
                Assert.That(
                    Path.GetFileNameWithoutExtension(actualPath),
                    Is.EqualTo(scenario.GetType().Name));
            }
        }

        [Test]
        public void Builder_RejectsDuplicateUnknownAndUnityOnlyFunctions()
        {
            var duplicate = new TriggerTestScenarioBuilder(
                Descriptor("mx.trigger.test.test.duplicate-function"));
            duplicate.RegisterFunction(
                new TriggerTestIncrementCounterFunction());
            Assert.Throws<InvalidOperationException>(
                () => duplicate.RegisterFunction(
                    new TriggerTestIncrementCounterFunction()));

            var unknown = new TriggerTestScenarioBuilder(
                Descriptor("mx.trigger.test.test.unknown-function"));
            unknown.UseFixture(_ => null);
            unknown.RegisterCall(
                1,
                new TriggerTestFunctionId(
                    "mx.trigger.test.function.test.missing"),
                EmptyArguments.Instance);
            Assert.Throws<InvalidOperationException>(
                () => unknown.BuildPlan());

            var noEngine = new TriggerTestScenarioBuilder(
                Descriptor("mx.trigger.test.test.unity-function"));
            Assert.Throws<InvalidOperationException>(
                () => noEngine.RegisterFunction(
                    new UnityOnlyFunction()));

            var playMode = new TriggerTestScenarioBuilder(
                Descriptor(
                    "mx.trigger.test.test.playmode-unity-function",
                    TriggerTestExecutionProfile.PlayMode));
            Assert.DoesNotThrow(
                () => playMode.RegisterFunction(
                    new UnityOnlyFunction()));
        }

        [Test]
        public void Builder_RejectsWorkOutsideMaximumFrameRange()
        {
            var builder = new TriggerTestScenarioBuilder(
                Descriptor("mx.trigger.test.test.frame-limit"));

            Assert.Throws<ArgumentOutOfRangeException>(
                () => builder.AtFrame(1L, NoOpStep.Instance));
            Assert.Throws<ArgumentOutOfRangeException>(
                () => builder.AssertAtFrame(1L, AlwaysFailAssertion.Instance));
        }

        [Test]
        public void Runner_ExecutesTypedScenarioAndAdaptsSimulationReport()
        {
            var scenario = new SignalFunctionScenario();
            TriggerTestRunResult result =
                new TriggerTestRunner(writeLatestReport: false).Run(scenario);

            Assert.That(result.Status, Is.EqualTo(TriggerTestRunStatus.Passed));
            Assert.That(result.InternalSucceeded, Is.True);
            Assert.That(result.SimulationResult.FrameCount, Is.EqualTo(1));
            Assert.That(result.SimulationResult.Failures.Count, Is.Zero);
            Assert.That(
                result.SimulationResult.Events.Count,
                Is.GreaterThanOrEqualTo(5));

            var adapter = new TriggerTestSimulationScenarioAdapter(
                scenario,
                writeLatestReport: false);
            FrameworkSimulationReport report =
                new FrameworkSimulationBatchRunner().Run(
                    new IFrameworkSimulationScenario[] { adapter });
            Assert.That(report.Success, Is.True);
            Assert.That(report.ScenarioCount, Is.EqualTo(1));
        }

        [Test]
        public void ManualExecutionService_RequiresPlayModeForLiveSceneScenario()
        {
            var editModeService = new TriggerTestManualExecutionService(
                typeof(TriggerTestAutomationTests).Assembly,
                () => false);
            TriggerTestDiscoveryResult discovery =
                editModeService.Discover();
            ITriggerTestScenario liveScenario = null;
            for (int i = 0; i < discovery.Scenarios.Count; i++)
            {
                if (string.Equals(
                        discovery.Scenarios[i].Descriptor.ScenarioId.Value,
                        RuntimeAiLabPlayModeStepScenario.ScenarioId,
                        StringComparison.Ordinal))
                {
                    liveScenario = discovery.Scenarios[i];
                    break;
                }
            }

            Assert.That(liveScenario, Is.Not.Null);
            Assert.That(
                liveScenario.Descriptor.ExecutionProfile,
                Is.EqualTo(TriggerTestExecutionProfile.PlayMode));
            Assert.That(
                editModeService.CanRun(
                    liveScenario.Descriptor,
                    out string editModeReason),
                Is.False);
            Assert.That(editModeReason, Does.Contain("Play Mode"));

            TriggerTestManualExecutionResult blocked =
                editModeService.Run(
                    RuntimeAiLabPlayModeStepScenario.ScenarioId);
            Assert.That(blocked.HasResults, Is.False);
            Assert.That(blocked.ErrorMessage, Does.Contain("Play Mode"));

            var playModeService = new TriggerTestManualExecutionService(
                typeof(TriggerTestAutomationTests).Assembly,
                () => true);
            Assert.That(
                playModeService.CanRun(
                    liveScenario.Descriptor,
                    out string playModeReason),
                Is.True);
            Assert.That(playModeReason, Is.Empty);
        }

        [Test]
        public void RuntimeAiLabManualScenarios_BuildOneCommandAndThreeAssertions()
        {
            ITriggerTestScenario[] scenarios =
            {
                new RuntimeAiLabStartTestScenario(),
                new RuntimeAiLabResumeSimulationScenario(),
                new RuntimeAiLabPauseSimulationScenario(),
                new RuntimeAiLabResetTestScenario(),
                new RuntimeAiLabSelectHistoricalFrameScenario(),
                new RuntimeAiLabSelectLatestFrameScenario(),
                new RuntimeAiLabRequestBranchRecomputeScenario(),
                new RuntimeAiLabConfirmBranchRecomputeScenario(),
                new RuntimeAiLabCancelBranchRecomputeScenario(),
                new RuntimeAiLabZoomInTimelineScenario(),
                new RuntimeAiLabZoomOutTimelineScenario(),
                new RuntimeAiLabFitTimelineScenario(),
                new RuntimeAiLabSelectTimelineEventScenario(),
                new RuntimeAiLabLocateEventStartScenario(),
                new RuntimeAiLabLocateEventEndScenario(),
                new RuntimeAiLabCloseEventDetailsScenario(),
                new RuntimeAiLabSelectSetupSlotScenario(),
                new RuntimeAiLabSelectSetupCharacterScenario(),
                new RuntimeAiLabSelectSetupBehaviorGroupScenario(),
                new RuntimeAiLabEnableSetupSlotScenario(),
                new RuntimeAiLabDisableSetupSlotScenario(),
                new RuntimeAiLabTrainingDummyCopyFlowScenario()
            };

            for (int i = 0; i < scenarios.Length; i++)
            {
                ITriggerTestScenario scenario = scenarios[i];
                var builder =
                    new TriggerTestScenarioBuilder(scenario.Descriptor);
                scenario.Build(builder);
                TriggerTestScenarioPlan plan = builder.BuildPlan();

                Assert.That(
                    plan.StepCount,
                    Is.EqualTo(1),
                    scenario.Descriptor.ScenarioId.Value);
                Assert.That(
                    plan.AssertionCount,
                    Is.EqualTo(3),
                    scenario.Descriptor.ScenarioId.Value);
                Assert.That(
                    plan.CopyCallIds(),
                    Is.EqualTo(new[] { 1 }),
                    scenario.Descriptor.ScenarioId.Value);
            }
        }

        [Test]
        public void ManualWindow_BuildsCompactRunnerAndShowsSelectedResult()
        {
            TriggerTestAutomationWindow window =
                ScriptableObject.CreateInstance<TriggerTestAutomationWindow>();
            try
            {
                window.CreateGUI();
                VisualElement root = window.rootVisualElement;
                Assert.That(
                    root.Q<TriggerTestScenarioPicker>(
                        "trigger-test-scenario-selector"),
                    Is.Not.Null);
                Assert.That(
                    root.Q<Button>("trigger-test-run-selected"),
                    Is.Not.Null);
                Assert.That(
                    root.Q<VisualElement>("trigger-test-chain"),
                    Is.Not.Null);
                Assert.That(
                    root.Q<Button>("trigger-test-open-report"),
                    Is.Not.Null);
                Assert.That(window.minSize.x, Is.EqualTo(360f));

                TriggerTestScenarioPicker selector =
                    root.Q<TriggerTestScenarioPicker>(
                        "trigger-test-scenario-selector");
                Assert.That(
                    selector.Value,
                    Is.EqualTo(
                        RuntimeAiLabOpenSceneScenario.ScenarioId));
                Assert.That(
                    root.Q<Label>(
                        "trigger-test-scenario-selected-name").text,
                    Is.EqualTo("打开 Runtime AI Lab 场景"));
                window.OpenScenarioPickerForTests();
                Assert.That(selector.IsOpen, Is.True);
                VisualElement pickerOverlay =
                    selector.panel?.visualTree ?? root;
                Assert.That(
                    selector.VisibleOptionCount,
                    Is.EqualTo(32));
                Assert.That(
                    pickerOverlay.Q<VisualElement>(
                        "trigger-test-scenario-group-environment"),
                    Is.Not.Null);
                Assert.That(
                    pickerOverlay.Q<VisualElement>(
                        "trigger-test-scenario-group-control"),
                    Is.Not.Null);
                Assert.That(
                    pickerOverlay.Q<VisualElement>(
                        "trigger-test-scenario-group-timeline"),
                    Is.Not.Null);
                Assert.That(
                    pickerOverlay.Q<VisualElement>(
                        "trigger-test-scenario-group-setup"),
                    Is.Not.Null);
                window.SetScenarioSearchForTests("时间轴");
                Assert.That(selector.VisibleOptionCount, Is.EqualTo(14));
                window.SetScenarioSearchForTests(string.Empty);
                window.SetScenarioFilterForTests("Available");
                Assert.That(selector.VisibleOptionCount, Is.EqualTo(4));
                window.SetScenarioFilterForTests("All");
                selector.Close();
                selector.SelectForTests(
                    RuntimeAiLabEnterPlayModeScenario.ScenarioId);
                selector.Open();
                Assert.That(
                    pickerOverlay.Q<VisualElement>(
                        "trigger-test-scenario-group-recent"),
                    Is.Not.Null);
                selector.Close();
                window.SelectScenarioForTests(
                    RuntimeAiLabOpenSceneScenario.ScenarioId);
                Assert.That(
                    root.Q<Label>("trigger-test-profile-hint").text,
                    Does.Contain("Editor 操作"));
                Assert.That(
                    root.Q<Label>("trigger-test-chain-title").text,
                    Is.EqualTo("Trigger 定义"));
                VisualElement sourcePreview =
                    root.Q<VisualElement>("trigger-test-operation-0");
                Assert.That(
                    sourcePreview.Q<Label>(
                        className:
                        "trigger-test-operation-heading").text,
                    Is.EqualTo("事件 · Signal"));
                Assert.That(
                    sourcePreview.Q<Label>(
                        className:
                        "trigger-test-operation-result"),
                    Is.Null);
                Assert.That(
                    sourcePreview.tooltip,
                    Does.Contain("SourceDefinitionId=1"));
                VisualElement conditionPreview =
                    root.Q<VisualElement>("trigger-test-operation-1");
                Assert.That(
                    conditionPreview.Q<Label>(
                        className:
                        "trigger-test-operation-heading").text,
                    Is.EqualTo("触发过滤 · 无"));
                Assert.That(
                    conditionPreview.Q<Label>(
                        className:
                        "trigger-test-operation-result"),
                    Is.Null);
                Assert.That(
                    conditionPreview.tooltip,
                    Does.Contain("ConditionId=1"));
                VisualElement policyPreview =
                    root.Q<VisualElement>("trigger-test-operation-2");
                Assert.That(
                    policyPreview.Q<Label>(
                        className:
                        "trigger-test-operation-heading").text,
                    Is.EqualTo("触发方式 · 每次 Signal 脉冲"));
                Assert.That(
                    policyPreview.Q<Label>(
                        className:
                        "trigger-test-operation-result"),
                    Is.Null);
                Assert.That(
                    policyPreview.tooltip,
                    Does.Contain("Scope=PerInstance"));
                VisualElement behaviorConditionPreview =
                    root.Q<VisualElement>("trigger-test-operation-3");
                Assert.That(
                    behaviorConditionPreview.Q<Label>(
                        className:
                        "trigger-test-operation-heading").text,
                    Is.EqualTo(
                        "执行前提 · 打开 Runtime AI Lab 场景"));
                Assert.That(
                    behaviorConditionPreview.Q<Label>(
                        className:
                        "trigger-test-operation-result").text,
                    Does.Contain("Unity 必须处于 Edit Mode"));
                VisualElement actionPreview =
                    root.Q<VisualElement>("trigger-test-operation-4");
                Assert.That(
                    actionPreview.Q<Label>(
                        className:
                        "trigger-test-operation-heading").text,
                    Is.EqualTo(
                        "行为 · 打开 Runtime AI Lab 场景"));
                Assert.That(
                    actionPreview.Q<Label>(
                        className:
                        "trigger-test-operation-result"),
                    Is.Null);
                Assert.That(
                    actionPreview.tooltip,
                    Does.Contain("runtime-ai-lab-open-scene"));
                Assert.That(actionPreview.tooltip, Does.Contain("CallId=1"));
                Assert.That(
                    root.Q<Button>(
                        "trigger-test-run-selected").enabledSelf,
                    Is.True);

                window.SelectScenarioForTests(
                    RuntimeAiLabSelectHistoricalFrameScenario.ScenarioId);
                IntegerField historicalFrame =
                    root.Q<IntegerField>(
                        "trigger-test-historical-frame");
                Assert.That(historicalFrame, Is.Not.Null);
                Assert.That(
                    historicalFrame.style.display.value,
                    Is.EqualTo(DisplayStyle.Flex));
                window.SetHistoricalFrameForTests(37);
                Assert.That(historicalFrame.value, Is.EqualTo(37));
                actionPreview =
                    root.Q<VisualElement>("trigger-test-operation-4");
                Assert.That(
                    actionPreview.Q<Label>(
                        className:
                        "trigger-test-operation-result").text,
                    Does.Contain("参数："));
                Assert.That(
                    actionPreview.tooltip,
                    Does.Contain("runtime-ai-lab-timeline"));
                Assert.That(
                    actionPreview.Q<Label>(
                        className:
                        "trigger-test-operation-result").text,
                    Does.Contain("TargetFrame=37"));

                window.SelectScenarioForTests(
                    RuntimeAiLabEnterPlayModeScenario.ScenarioId);
                behaviorConditionPreview =
                    root.Q<VisualElement>("trigger-test-operation-3");
                Assert.That(
                    behaviorConditionPreview.Q<Label>(
                        className:
                        "trigger-test-operation-heading").text,
                    Is.EqualTo("执行前提 · 请求进入 Play Mode"));
                Assert.That(
                    behaviorConditionPreview.Q<Label>(
                        className:
                        "trigger-test-operation-result").text,
                    Does.Contain("活动场景为 Runtime AI Lab"));
                Assert.That(
                    behaviorConditionPreview.Q<Label>(
                        className:
                        "trigger-test-operation-result").text,
                    Does.Contain("恰好 1 个活动 Controller"));
                Assert.That(
                    behaviorConditionPreview.tooltip,
                    Does.Contain(
                        TriggerTestRuntimeAiLabEditorActions.TargetScenePath));
                actionPreview =
                    root.Q<VisualElement>("trigger-test-operation-4");
                Assert.That(
                    actionPreview.Q<Label>(
                        className:
                        "trigger-test-operation-heading").text,
                    Is.EqualTo("行为 · 请求进入 Play Mode"));
                VisualElement acceptancePreview =
                    root.Q<VisualElement>("trigger-test-operation-5");
                Assert.That(
                    acceptancePreview.Q<Label>(
                        className:
                        "trigger-test-operation-heading").text,
                    Is.EqualTo("验收 · 场景状态"));
                Assert.That(
                    acceptancePreview.Q<Label>(
                        className:
                        "trigger-test-operation-result").text,
                    Does.Contain("1 个 Ready Controller"));

                window.SelectScenarioForTests(
                    RuntimeAiLabSelectTimelineEventScenario.ScenarioId);
                IntegerField timelineEvent =
                    root.Q<IntegerField>(
                        "trigger-test-timeline-event");
                Assert.That(timelineEvent, Is.Not.Null);
                Assert.That(
                    timelineEvent.style.display.value,
                    Is.EqualTo(DisplayStyle.Flex));
                window.SetTimelineEventForTests(5);
                Assert.That(timelineEvent.value, Is.EqualTo(5));

                window.SelectScenarioForTests(
                    RuntimeAiLabOpenSceneScenario.ScenarioId);
                window.SelectScenarioForTests(
                    SignalFunctionScenario.ScenarioId);
                Assert.That(
                    selector.Value,
                    Is.EqualTo(
                        RuntimeAiLabOpenSceneScenario.ScenarioId));
                Assert.That(
                    root.Q<Button>("trigger-test-run-selected").enabledSelf,
                    Is.True);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(window);
            }
        }

        [Test]
        public void Runner_ReportsPendingExternalEvidenceWithoutClaimingPass()
        {
            TriggerTestRunResult result =
                new TriggerTestRunner(writeLatestReport: false).Run(
                    new ExternalEvidenceScenario());

            Assert.That(result.InternalSucceeded, Is.True);
            Assert.That(
                result.Status,
                Is.EqualTo(TriggerTestRunStatus.PendingExternalEvidence));
            Assert.That(result.ExternalChecks.Count, Is.EqualTo(1));
        }

        [Test]
        public void Runner_CapturesFunctionExceptionAsStructuredFailure()
        {
            TriggerTestRunResult result =
                new TriggerTestRunner(writeLatestReport: false).Run(
                    new ThrowingFunctionScenario());

            Assert.That(result.Status, Is.EqualTo(TriggerTestRunStatus.Failed));
            Assert.That(result.InternalSucceeded, Is.False);
            Assert.That(result.SimulationResult.Failures.Count, Is.EqualTo(1));
            Assert.That(
                result.SimulationResult.Failures[0].Message,
                Does.Contain("controlled failure"));
            Assert.That(
                result.SimulationResult.Failures[0].Frame,
                Is.EqualTo(0L));
        }

        [Test]
        public void Runner_StopsAtFirstFailedAssertionFrame()
        {
            TriggerTestRunResult result =
                new TriggerTestRunner(writeLatestReport: false).Run(
                    new FailAtFrameScenario());

            Assert.That(result.Status, Is.EqualTo(TriggerTestRunStatus.Failed));
            Assert.That(result.SimulationResult.FrameCount, Is.EqualTo(2));
            Assert.That(result.SimulationResult.Failures.Count, Is.EqualTo(1));
            Assert.That(
                result.SimulationResult.Failures[0].Frame,
                Is.EqualTo(1L));
        }

        [Test]
        public void Runner_CreatesAndDisposesFreshFixtureForEveryRun()
        {
            var scenario = new TrackingFixtureScenario();
            var runner = new TriggerTestRunner(writeLatestReport: false);

            Assert.That(runner.Run(scenario).Status,
                Is.EqualTo(TriggerTestRunStatus.Passed));
            Assert.That(runner.Run(scenario).Status,
                Is.EqualTo(TriggerTestRunStatus.Passed));
            Assert.That(scenario.CreatedFixtureCount, Is.EqualTo(2));
            Assert.That(scenario.DisposedFixtureCount, Is.EqualTo(2));
        }

        [Test]
        public void FunctionSink_DoesNotExecuteHandlerInsideTriggerTick()
        {
            var sink = new TriggerTestFunctionActionSink(
                new[]
                {
                    SignalFunctionScenario.FunctionCallId,
                    SignalFunctionScenario.ScenarioLocalFunctionCallId
                },
                capacity: 2);
            ITriggerTestFixture fixture =
                SignalFunctionScenario.CreateFixture(sink);
            fixture.Initialize();
            try
            {
                var signals = new[] { new GameplayTriggerSignal(1001) };
                Assert.That(
                    fixture.TriggerRuntime.TrySubmitSignals(
                        RuntimeFrame.Zero,
                        new GameplayTriggerSourceBindingId(
                            SignalFunctionScenario.SourceBindingId),
                        GameplayTriggerSignalOrdering.UnorderedMultiset,
                        signals,
                        out _),
                    Is.True);

                fixture.Tick(0L, 0d);

                Assert.That(sink.PendingCount, Is.EqualTo(2));
                Assert.That(
                    fixture.Services.TryGet(out TriggerTestCounter counter),
                    Is.True);
                Assert.That(counter.Value, Is.Zero);
                Assert.That(
                    fixture.Services.TryGet(
                        out SignalFunctionScenarioMarker marker),
                    Is.True);
                Assert.That(marker.InvocationCount, Is.Zero);
                Assert.That(sink.TryDequeue(out _), Is.True);
                Assert.That(sink.TryDequeue(out _), Is.True);
                Assert.That(counter.Value, Is.Zero);
                Assert.That(marker.InvocationCount, Is.Zero);
            }
            finally
            {
                fixture.Dispose();
            }
        }

        [Test]
        public void FunctionSink_AbortsPreparedCallWhenRouteReservationFails()
        {
            var sink = new TriggerTestFunctionActionSink(
                new[]
                {
                    SignalFunctionScenario.FunctionCallId,
                    SignalFunctionScenario.ScenarioLocalFunctionCallId
                },
                capacity: 1);
            ITriggerTestFixture fixture =
                SignalFunctionScenario.CreateFixture(
                    sink,
                    new[]
                    {
                        SignalFunctionScenario.FunctionCallId,
                        SignalFunctionScenario.ScenarioLocalFunctionCallId
                    });
            fixture.Initialize();
            try
            {
                Assert.That(
                    fixture.TriggerRuntime.TrySubmitSignals(
                        RuntimeFrame.Zero,
                        new GameplayTriggerSourceBindingId(
                            SignalFunctionScenario.SourceBindingId),
                        GameplayTriggerSignalOrdering.UnorderedMultiset,
                        new[] { new GameplayTriggerSignal(1001) },
                        out _),
                    Is.True);

                fixture.Tick(0L, 0d);

                Assert.That(fixture.TriggerRuntime.ActivationCount, Is.Zero);
                Assert.That(fixture.TriggerRuntime.DiagnosticCount, Is.EqualTo(1));
                Assert.That(sink.PendingCount, Is.Zero);
                Assert.That(sink.ReservedCount, Is.Zero);
            }
            finally
            {
                fixture.Dispose();
            }
        }

        [Test]
        public void FunctionSink_RejectsUnknownCallAndDrainsByReservationOrder()
        {
            var sink = new TriggerTestFunctionActionSink(
                new[]
                {
                    SignalFunctionScenario.FunctionCallId,
                    SignalFunctionScenario.ScenarioLocalFunctionCallId
                },
                capacity: 2);

            Assert.That(
                sink.TryPrepare(Preparation(999), out _),
                Is.False);
            Assert.That(
                sink.TryPrepare(
                    Preparation(
                        SignalFunctionScenario.ScenarioLocalFunctionCallId),
                    out int localToken),
                Is.True);
            Assert.That(
                sink.TryPrepare(
                    Preparation(SignalFunctionScenario.FunctionCallId),
                    out int commonToken),
                Is.True);

            sink.Commit(commonToken, default);
            sink.Commit(localToken, default);

            Assert.That(sink.TryDequeue(out TriggerTestFunctionCallRecord first),
                Is.True);
            Assert.That(
                first.CallId,
                Is.EqualTo(SignalFunctionScenario.ScenarioLocalFunctionCallId));
            Assert.That(sink.TryDequeue(out TriggerTestFunctionCallRecord second),
                Is.True);
            Assert.That(
                second.CallId,
                Is.EqualTo(SignalFunctionScenario.FunctionCallId));
            Assert.That(first.ReservationSequence,
                Is.LessThan(second.ReservationSequence));
            Assert.That(sink.TryDequeue(out _), Is.False);
        }

        [Test]
        public void FunctionSink_WarmCommitAndDequeuePathAllocatesZeroBytes()
        {
            var sink = new TriggerTestFunctionActionSink(
                new[]
                {
                    SignalFunctionScenario.FunctionCallId,
                    SignalFunctionScenario.ScenarioLocalFunctionCallId
                },
                capacity: 2);
            ITriggerTestFixture fixture =
                SignalFunctionScenario.CreateFixture(sink);
            fixture.Initialize();
            try
            {
                var source = new GameplayTriggerSourceBindingId(
                    SignalFunctionScenario.SourceBindingId);
                var signals = new[] { new GameplayTriggerSignal(1001) };
                Assert.That(
                    fixture.TriggerRuntime.TrySubmitSignals(
                        RuntimeFrame.Zero,
                        source,
                        GameplayTriggerSignalOrdering.UnorderedMultiset,
                        signals,
                        out _),
                    Is.True);
                fixture.Tick(0L, 0d);
                Assert.That(sink.TryDequeue(out _), Is.True);
                Assert.That(sink.TryDequeue(out _), Is.True);

                bool succeeded = true;
                long before = GC.GetAllocatedBytesForCurrentThread();
                for (int frame = 1; frame <= 64; frame++)
                {
                    succeeded &= fixture.TriggerRuntime.TrySubmitSignals(
                        new RuntimeFrame(frame),
                        source,
                        GameplayTriggerSignalOrdering.UnorderedMultiset,
                        signals,
                        out _);
                    fixture.Tick(frame, 0d);
                    succeeded &= sink.TryDequeue(out _);
                    succeeded &= sink.TryDequeue(out _);
                }
                long allocated =
                    GC.GetAllocatedBytesForCurrentThread() - before;

                Assert.That(succeeded, Is.True);
                Assert.That(allocated, Is.Zero);
            }
            finally
            {
                fixture.Dispose();
            }
        }

        [Test]
        public void ReportWriter_EmitsVerificationStatusAndSimulationPayload()
        {
            TriggerTestRunResult result =
                new TriggerTestRunner(writeLatestReport: true).Run(
                    new SignalFunctionScenario());
            string path = Path.GetFullPath(
                Path.Combine(
                    "Temp",
                    "MxFramework",
                    "TriggerTests",
                    SignalFunctionScenario.ScenarioId,
                    "latest.json"));

            Assert.That(File.Exists(path), Is.True);
            JObject report = JObject.Parse(File.ReadAllText(path));
            Assert.That(
                (string)report["schema"],
                Is.EqualTo("mx.trigger.test.report.v1"));
            Assert.That(
                (string)report["status"],
                Is.EqualTo(TriggerTestRunStatus.Passed.ToString()));
            Assert.That(
                (bool)report["simulation"]["success"],
                Is.True);
        }

        [Test]
        public void AutomationSource_RemainsInsideEditorTestAssembly()
        {
            string asmdefPath =
                "Assets/Scripts/WGameFrameworkProject/CharacterTest/Tests/EditMode/MxFramework.Tests.asmdef";
            string asmdef = File.ReadAllText(asmdefPath);

            Assert.That(asmdef, Does.Contain("\"Editor\""));
            Assert.That(asmdef, Does.Contain("\"UNITY_INCLUDE_TESTS\""));
            Assert.That(
                Directory.Exists(AutomationRoot),
                Is.True);
        }

        private static TriggerTestDescriptor Descriptor(
            string scenarioId,
            TriggerTestExecutionProfile executionProfile =
                TriggerTestExecutionProfile.NoEngine) =>
            new TriggerTestDescriptor(
                scenarioId,
                scenarioId,
                "test",
                executionProfile,
                maxFrames: 1);

        private static GameplayTriggerActionPreparation Preparation(int callId)
        {
            var instruction = new GameplayTriggerActionInstruction(
                TriggerTestActionKindIds.FunctionCall,
                GameplayTriggerTargetSelector.Owner,
                payload0: callId);
            return new GameplayTriggerActionPreparation(
                RuntimeFrame.Zero,
                new GameplayTriggerBindingId(1),
                new GameplayEntityId(1, 1),
                default,
                new GameplayTriggerRouteId(1),
                0,
                default,
                instruction);
        }

        private static string FindScriptPath(string[] guids, Type type)
        {
            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                MonoScript script = AssetDatabase.LoadAssetAtPath<MonoScript>(path);
                if (script != null && script.GetClass() == type)
                    return path;
            }

            return string.Empty;
        }

        private static string Describe(TriggerTestDiscoveryResult discovery)
        {
            var text = new System.Text.StringBuilder();
            for (int i = 0; i < discovery.Errors.Count; i++)
            {
                if (i > 0)
                    text.Append(" | ");
                text.Append(discovery.Errors[i].TypeName);
                text.Append(": ");
                text.Append(discovery.Errors[i].Message);
            }

            return text.ToString();
        }

        private static string[] ScenarioIds(
            TriggerTestDiscoveryResult discovery)
        {
            var ids = new string[discovery.Scenarios.Count];
            for (int i = 0; i < discovery.Scenarios.Count; i++)
            {
                ids[i] =
                    discovery.Scenarios[i].Descriptor.ScenarioId.Value;
            }

            return ids;
        }

        private static string[] IndexScenarioIds(JObject index)
        {
            JArray scenarios = (JArray)index["scenarios"];
            var ids = new string[scenarios.Count];
            for (int i = 0; i < scenarios.Count; i++)
                ids[i] = (string)scenarios[i]["id"];
            return ids;
        }

        private sealed class DuplicateScenarioA : ITriggerTestScenario
        {
            public TriggerTestDescriptor Descriptor =>
                TriggerTestAutomationTests.Descriptor(
                    "mx.trigger.test.test.duplicate");

            public void Build(TriggerTestScenarioBuilder builder)
            {
            }
        }

        private sealed class DuplicateScenarioB : ITriggerTestScenario
        {
            public TriggerTestDescriptor Descriptor =>
                TriggerTestAutomationTests.Descriptor(
                    "mx.trigger.test.test.duplicate");

            public void Build(TriggerTestScenarioBuilder builder)
            {
            }
        }

        private sealed class MissingConstructorScenario : ITriggerTestScenario
        {
            public MissingConstructorScenario(int value)
            {
            }

            public TriggerTestDescriptor Descriptor =>
                TriggerTestAutomationTests.Descriptor(
                    "mx.trigger.test.test.missing-constructor");

            public void Build(TriggerTestScenarioBuilder builder)
            {
            }
        }

        private sealed class AlphaScenario : ITriggerTestScenario
        {
            public TriggerTestDescriptor Descriptor =>
                TriggerTestAutomationTests.Descriptor(
                    "mx.trigger.test.test.alpha");

            public void Build(TriggerTestScenarioBuilder builder)
            {
            }
        }

        private sealed class ZetaScenario : ITriggerTestScenario
        {
            public TriggerTestDescriptor Descriptor =>
                TriggerTestAutomationTests.Descriptor(
                    "mx.trigger.test.test.zeta");

            public void Build(TriggerTestScenarioBuilder builder)
            {
            }
        }

        private sealed class ExternalEvidenceScenario : ITriggerTestScenario
        {
            private static readonly TriggerTestDescriptor ScenarioDescriptor =
                new TriggerTestDescriptor(
                    "mx.trigger.test.framework.external-evidence",
                    "External evidence state",
                    "framework",
                    TriggerTestExecutionProfile.NoEngine,
                    maxFrames: 1,
                    externalChecks: new[]
                    {
                        new TriggerTestExternalCheck(
                            "console-clean",
                            TriggerTestExternalCheckKind.ConsoleNoErrors,
                            "Unity Console contains no task-related errors.")
                    });

            public TriggerTestDescriptor Descriptor => ScenarioDescriptor;

            public void Build(TriggerTestScenarioBuilder builder)
            {
                new SignalFunctionScenario().Build(builder);
            }
        }

        private sealed class ThrowingFunctionScenario : ITriggerTestScenario
        {
            private static readonly TriggerTestDescriptor ScenarioDescriptor =
                new TriggerTestDescriptor(
                    "mx.trigger.test.framework.throwing-function",
                    "Throwing function is captured",
                    "framework",
                    TriggerTestExecutionProfile.NoEngine,
                    maxFrames: 1);

            public TriggerTestDescriptor Descriptor => ScenarioDescriptor;

            public void Build(TriggerTestScenarioBuilder builder)
            {
                builder.RegisterFunction(new ThrowingFunction());
                builder.RegisterCall(
                    SignalFunctionScenario.FunctionCallId,
                    ThrowingFunction.Id,
                    EmptyArguments.Instance);
                builder.UseFixture(
                    sink => SignalFunctionScenario.CreateFixture(
                        sink,
                        actionCount: 1));
                builder.AtFrame(
                    0L,
                    new TriggerTestSubmitSignalsStep(
                        "submit-signal",
                        new GameplayTriggerSourceBindingId(
                            SignalFunctionScenario.SourceBindingId),
                        GameplayTriggerSignalOrdering.UnorderedMultiset,
                        new[] { new GameplayTriggerSignal(1001) }));
            }
        }

        private sealed class FailAtFrameScenario : ITriggerTestScenario
        {
            private static readonly TriggerTestDescriptor ScenarioDescriptor =
                new TriggerTestDescriptor(
                    "mx.trigger.test.framework.fail-at-frame",
                    "First failed assertion frame",
                    "framework",
                    TriggerTestExecutionProfile.NoEngine,
                    maxFrames: 3);

            public TriggerTestDescriptor Descriptor => ScenarioDescriptor;

            public void Build(TriggerTestScenarioBuilder builder)
            {
                builder.UseFixture(SignalFunctionScenario.CreateFixture);
                builder.AssertAtFrame(1L, AlwaysFailAssertion.Instance);
            }
        }

        private sealed class TrackingFixtureScenario : ITriggerTestScenario
        {
            private static readonly TriggerTestDescriptor ScenarioDescriptor =
                new TriggerTestDescriptor(
                    "mx.trigger.test.framework.fixture-isolation",
                    "Fresh fixture isolation",
                    "framework",
                    TriggerTestExecutionProfile.NoEngine,
                    maxFrames: 1);

            internal int CreatedFixtureCount { get; private set; }
            internal int DisposedFixtureCount { get; private set; }

            public TriggerTestDescriptor Descriptor => ScenarioDescriptor;

            public void Build(TriggerTestScenarioBuilder builder)
            {
                builder.UseFixture(sink =>
                {
                    CreatedFixtureCount++;
                    return new TrackingFixture(
                        SignalFunctionScenario.CreateFixture(sink),
                        this);
                });
            }

            internal void RecordDispose()
            {
                DisposedFixtureCount++;
            }
        }

        private sealed class TrackingFixture : ITriggerTestFixture
        {
            private readonly ITriggerTestFixture _inner;
            private readonly TrackingFixtureScenario _owner;

            internal TrackingFixture(
                ITriggerTestFixture inner,
                TrackingFixtureScenario owner)
            {
                _inner = inner;
                _owner = owner;
            }

            public GameplayTriggerRuntime TriggerRuntime =>
                _inner.TriggerRuntime;

            public TriggerTestServiceRegistry Services => _inner.Services;

            public void Initialize()
            {
                _inner.Initialize();
            }

            public void Tick(long frame, double deltaTime)
            {
                _inner.Tick(frame, deltaTime);
            }

            public void Dispose()
            {
                _inner.Dispose();
                _owner.RecordDispose();
            }
        }

        private sealed class NoOpStep : ITriggerTestFrameStep
        {
            internal static readonly NoOpStep Instance = new NoOpStep();

            private NoOpStep()
            {
            }

            public string StepId => "no-op";

            public TriggerTestOperationResult Execute(
                in TriggerTestStepContext context) =>
                TriggerTestOperationResult.Succeeded();
        }

        private sealed class AlwaysFailAssertion : ITriggerTestAssertion
        {
            internal static readonly AlwaysFailAssertion Instance =
                new AlwaysFailAssertion();

            private AlwaysFailAssertion()
            {
            }

            public string AssertionId => "always-fail";

            public TriggerTestOperationResult Evaluate(
                in TriggerTestAssertionContext context) =>
                TriggerTestOperationResult.Failed(
                    "Controlled assertion failure.");
        }

        private sealed class ThrowingFunction : ITriggerTestFunction
        {
            internal static readonly TriggerTestFunctionId Id =
                new TriggerTestFunctionId(
                    "mx.trigger.test.function.test.throw");

            public TriggerTestFunctionId FunctionId => Id;

            public TriggerTestFunctionCapability Capability =>
                TriggerTestFunctionCapability.PureRead;

            public TriggerTestOperationResult Execute(
                in TriggerTestFunctionContext context,
                ITriggerTestFunctionArguments arguments)
            {
                throw new InvalidOperationException("controlled failure");
            }
        }

        private sealed class UnityOnlyFunction : ITriggerTestFunction
        {
            public TriggerTestFunctionId FunctionId =>
                new TriggerTestFunctionId(
                    "mx.trigger.test.function.test.unity-only");

            public TriggerTestFunctionCapability Capability =>
                TriggerTestFunctionCapability.UnityAdapter;

            public TriggerTestOperationResult Execute(
                in TriggerTestFunctionContext context,
                ITriggerTestFunctionArguments arguments) =>
                TriggerTestOperationResult.Succeeded();
        }

        private sealed class EmptyArguments : ITriggerTestFunctionArguments
        {
            internal static readonly EmptyArguments Instance =
                new EmptyArguments();

            private EmptyArguments()
            {
            }
        }
    }
}
