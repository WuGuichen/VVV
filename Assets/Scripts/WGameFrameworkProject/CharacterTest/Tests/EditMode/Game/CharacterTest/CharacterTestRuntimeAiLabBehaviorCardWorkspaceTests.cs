using System;
using System.Reflection;
using MxFramework.UI;
using NUnit.Framework;
using UnityEngine;

namespace MxFramework.Game.CharacterTest.Tests
{
    public sealed partial class CharacterTestRuntimeAiLabBehaviorCardWorkspaceTests
    {
        [Test]
        public void GameplayPlanProjection_UsesActionLocalProgressInsteadOfStageAge()
        {
            CharacterTestRuntimeAiLabBehaviorCardTimelineViewModel timeline = CreateProgressTimeline();
            CharacterTestRuntimeAiLabLogicStageViewModel stage = timeline.Stages[0];
            stage.ActualDurationFrames = 999;
            stage.ActionDurationFrames = 20;
            stage.ActionProgressFrames = 5;
            stage.ActionRuntimeState = (int)CharacterTestRuntimeAiLabLogicActionState.Running;

            var projection = new GameplayReadablePlanProjection();
            projection.Rebuild(timeline);

            GameplayReadablePlanStageProjection projected = projection.GetStage(0);
            Assert.AreEqual(GameplayReadablePlanProgressMode.Determinate, projected.ActionProgressMode);
            Assert.AreEqual(5, projected.ActionProgressFrames);
            Assert.AreEqual(20, projected.ActionDurationFrames);
            Assert.AreNotEqual(stage.ActualDurationFrames, projected.ActionProgressFrames);
        }

        [Test]
        public void GameplayPlanProjection_UsesAuthoredActionIdentityAndInstance()
        {
            CharacterTestRuntimeAiLabBehaviorCardTimelineViewModel timeline = CreateProgressTimeline();
            CharacterTestRuntimeAiLabLogicStageViewModel stage = timeline.Stages[0];
            stage.CharacterActionId = new MxFramework.CharacterAction.CharacterActionId(301);
            stage.CharacterActionDisplayName = "Light Attack";
            stage.ActionDurationFrames = 76;
            stage.ActionProgressFrames = 9;
            stage.ActionInstanceId = 42L;

            var projection = new GameplayReadablePlanProjection();
            projection.Rebuild(timeline);

            GameplayReadablePlanStageProjection projected = projection.GetStage(0);
            Assert.AreEqual("Light Attack（#301）", projected.ActionLabel);
            Assert.AreEqual(76, projected.ActionDurationFrames);
            Assert.AreEqual(9, projected.ActionProgressFrames);
            Assert.AreEqual(42L, projected.ActionInstanceId);
        }

        [Test]
        public void ActiveActionProgress_ConvertsZeroBasedLocalFrameToCoveredFrameCount()
        {
            var first = new CharacterTestRuntimeAiLabActiveActionProgress(
                17,
                41L,
                new MxFramework.CharacterAction.CharacterActionId(301),
                0,
                76);
            var last = new CharacterTestRuntimeAiLabActiveActionProgress(
                17,
                41L,
                new MxFramework.CharacterAction.CharacterActionId(301),
                75,
                76);

            Assert.AreEqual(1, first.CoveredFrames);
            Assert.AreEqual(76, last.CoveredFrames);
        }

        [Test]
        public void GameplayPlanProjection_ActionCanCompleteWhileExitKeepsListening()
        {
            CharacterTestRuntimeAiLabBehaviorCardTimelineViewModel timeline = CreateProgressTimeline();
            CharacterTestRuntimeAiLabLogicStageViewModel stage = timeline.Stages[0];
            stage.ActionDurationFrames = 20;
            stage.ActionProgressFrames = 20;
            stage.ActionRuntimeState = (int)CharacterTestRuntimeAiLabLogicActionState.Completed;
            CharacterTestRuntimeAiLabLogicDetectorViewModel detector = timeline.Detectors[0];
            detector.Active = true;
            detector.RuntimeElapsedFrames = 37;
            detector.WindowEndMode = CharacterTestRuntimeAiLabLogicDetectorWindowEndMode.StageEnd;

            var projection = new GameplayReadablePlanProjection();
            projection.Rebuild(timeline);

            Assert.AreEqual(
                GameplayReadablePlanProgressMode.Completed,
                projection.GetStage(0).ActionProgressMode);
            Assert.AreEqual(
                GameplayReadablePlanProgressMode.Indeterminate,
                projection.GetExit(0).ProgressMode);
            Assert.AreEqual("动作完成 · 监听中", projection.ExecutionStatusText);
        }

        [Test]
        public void GameplayPlanProjection_FixedDetectorUsesItsOwnWindowAndHistoricalHidesProgress()
        {
            CharacterTestRuntimeAiLabBehaviorCardTimelineViewModel timeline = CreateProgressTimeline();
            CharacterTestRuntimeAiLabLogicDetectorViewModel detector = timeline.Detectors[0];
            detector.Active = true;
            detector.DurationFrames = 30;
            detector.RuntimeElapsedFrames = 12;
            detector.WindowEndMode = CharacterTestRuntimeAiLabLogicDetectorWindowEndMode.Fixed;

            var projection = new GameplayReadablePlanProjection();
            projection.Rebuild(timeline);
            Assert.AreEqual(GameplayReadablePlanProgressMode.Determinate, projection.GetExit(0).ProgressMode);
            Assert.AreEqual(12, projection.GetExit(0).ProgressFrames);
            Assert.AreEqual(30, projection.GetExit(0).ProgressDurationFrames);

            detector.Triggered = true;
            projection.UpdateRuntime(timeline);
            Assert.AreEqual(GameplayReadablePlanProgressMode.Completed, projection.GetExit(0).ProgressMode);

            timeline.Historical = true;
            projection.UpdateRuntime(timeline);
            Assert.AreEqual(GameplayReadablePlanProgressMode.None, projection.GetExit(0).ProgressMode);
            Assert.AreEqual(GameplayReadablePlanProgressMode.None, projection.GetStage(0).ActionProgressMode);
            Assert.AreEqual("历史帧", projection.ExecutionStatusText);
        }

        [Test]
        public void GameplayPlanProjection_LogicStageRendersAsLocalJudgementEvent()
        {
            CharacterTestRuntimeAiLabBehaviorCardTimelineViewModel timeline = CreateProgressTimeline();
            CharacterTestRuntimeAiLabLogicStageViewModel stage = timeline.Stages[0];
            stage.Title = "Observe";
            stage.Kind = CharacterTestRuntimeAiLabCardStepKind.Logic;
            stage.OwnsPrimaryAction = false;
            stage.ActionDurationFrames = 30;
            stage.ActionProgressFrames = 9;
            stage.ActionRuntimeState = (int)CharacterTestRuntimeAiLabLogicActionState.Running;

            var projection = new GameplayReadablePlanProjection();
            projection.Rebuild(timeline);

            GameplayReadablePlanStageProjection projected = projection.GetStage(0);
            Assert.IsTrue(projected.OwnsPrimaryAction);
            Assert.AreEqual("观察", projected.StageLabel);
            Assert.AreEqual("观察目标", projected.ActionLabel);
            Assert.AreEqual(GameplayReadablePlanProgressMode.Determinate, projected.ActionProgressMode);
            Assert.AreEqual(9, projected.ActionProgressFrames);
            Assert.AreEqual(30, projected.ActionDurationFrames);
        }

        [Test]
        public void GameplayPlanProjection_OpenStagesUseGraphOrderWithoutOverlap()
        {
            var timeline = new CharacterTestRuntimeAiLabBehaviorCardTimelineViewModel
            {
                Available = true,
                RuntimeFrame = 3,
                HorizontalStartFrame = 0,
                HorizontalEndFrame = 60,
                StageCount = 3,
                Stages = new[]
                {
                    new CharacterTestRuntimeAiLabLogicStageViewModel
                    {
                        Index = 0,
                        Order = 0,
                        Column = 0,
                        StageId = "logic.observe",
                        Title = "Observe",
                        Kind = CharacterTestRuntimeAiLabCardStepKind.Logic,
                        DurationMode = CharacterTestRuntimeAiLabLogicDurationMode.UntilEvent,
                        StartFrame = 0,
                        DurationFrames = 60,
                        Current = true,
                        ActualDurationFrames = 3,
                        ActionProgressFrames = 3,
                        ActionRuntimeState = (int)CharacterTestRuntimeAiLabLogicActionState.Running
                    },
                    new CharacterTestRuntimeAiLabLogicStageViewModel
                    {
                        Index = 1,
                        Order = 1,
                        Column = 1,
                        StageId = "logic.wait",
                        Title = "Wait",
                        Kind = CharacterTestRuntimeAiLabCardStepKind.Logic,
                        DurationMode = CharacterTestRuntimeAiLabLogicDurationMode.UntilEvent,
                        StartFrame = 0,
                        DurationFrames = 60
                    },
                    new CharacterTestRuntimeAiLabLogicStageViewModel
                    {
                        Index = 2,
                        Order = 2,
                        Column = 2,
                        StageId = "logic.damage",
                        Title = "Damage Output",
                        Kind = CharacterTestRuntimeAiLabCardStepKind.CharacterAction,
                        CharacterActionId = new MxFramework.CharacterAction.CharacterActionId(301),
                        DurationMode = CharacterTestRuntimeAiLabLogicDurationMode.UntilEvent,
                        StartFrame = 0,
                        DurationFrames = 60,
                        ActionDurationFrames = 76,
                        OwnsPrimaryAction = true
                    }
                }
            };

            var projection = new GameplayReadablePlanProjection();
            projection.Rebuild(timeline);

            GameplayReadablePlanStageProjection observe = projection.GetStage(0);
            GameplayReadablePlanStageProjection wait = projection.GetStage(1);
            GameplayReadablePlanStageProjection attack = projection.GetStage(2);
            Assert.IsTrue(observe.Current);
            Assert.AreEqual("观察目标", observe.ActionLabel);
            Assert.AreEqual(GameplayReadablePlanProgressMode.Indeterminate, observe.ActionProgressMode);
            Assert.AreEqual(3, observe.ActionProgressFrames);
            Assert.LessOrEqual(observe.StartFrame + observe.DurationFrames, wait.StartFrame);
            Assert.LessOrEqual(wait.StartFrame + wait.DurationFrames, attack.StartFrame);
            Assert.AreEqual("动作 #301", attack.ActionLabel);
            Assert.AreEqual(GameplayReadablePlanProgressMode.None, attack.ActionProgressMode);
            Assert.Greater(projection.PresentationEndFrame, timeline.HorizontalEndFrame);
        }

        [Test]
        public void GameplayPlanProjection_WaitUsesCurrentStageFramesWithoutPrimaryActionIndex()
        {
            CharacterTestRuntimeAiLabBehaviorCardLogicPayload payload =
                CreateSingleStageProgressPayload(
                    CharacterTestRuntimeAiLabCardStepKind.Wait,
                    waitFrames: 300);
            CharacterTestRuntimeAiLabBehaviorCardLogicCompileResult compile =
                CharacterTestRuntimeAiLabBehaviorCardLogicCompiler.Compile(payload);
            Assert.IsTrue(compile.Success);
            var runtime = new CharacterTestRuntimeAiLabLogicAgentStateSnapshot
            {
                EntityId = 17,
                CurrentStageIndex = 0,
                StageEnteredFrame = 0,
                ActionEnteredFrame = 0,
                PendingTransitionIndex = -1,
                PrimaryActionState = (int)CharacterTestRuntimeAiLabLogicActionState.Running,
                PrimaryActionStageIndex = -1,
                DetectorArmed = Array.Empty<bool>()
            };
            var viewModel = new CharacterTestFairyGuiRuntimeAiPlannerDevPanelViewModel();

            CharacterTestRuntimeAiLabBehaviorCardWorkspaceViewModelBuilder.Build(
                viewModel,
                "等待测试",
                compile,
                payload,
                0,
                runtimeState: runtime,
                runtimeFrame: 9);

            CharacterTestRuntimeAiLabLogicStageViewModel stage =
                viewModel.BehaviorCardWorkspace.Timeline.Stages[0];
            Assert.AreEqual(300, stage.ActionDurationFrames);
            Assert.AreEqual(9, stage.ActionProgressFrames);
            Assert.AreEqual(
                (int)CharacterTestRuntimeAiLabLogicActionState.Running,
                stage.ActionRuntimeState);
            var projection = new GameplayReadablePlanProjection();
            projection.Rebuild(viewModel.BehaviorCardWorkspace.Timeline);
            Assert.IsTrue(projection.GetStage(0).OwnsPrimaryAction);
            Assert.AreEqual(
                GameplayReadablePlanProgressMode.Determinate,
                projection.GetStage(0).ActionProgressMode);
        }

        [Test]
        public void GameplayPlanBuilder_UseActionUsesMatchingRunnerInstanceFrames()
        {
            CharacterTestRuntimeAiLabBehaviorCardLogicPayload payload =
                CreateSingleStageProgressPayload(
                    CharacterTestRuntimeAiLabCardStepKind.CharacterAction,
                    characterActionId: "301");
            CharacterTestRuntimeAiLabBehaviorCardLogicCompileResult compile =
                CharacterTestRuntimeAiLabBehaviorCardLogicCompiler.Compile(payload);
            Assert.IsTrue(compile.Success);
            var runtime = new CharacterTestRuntimeAiLabLogicAgentStateSnapshot
            {
                EntityId = 17,
                CurrentStageIndex = 0,
                ActionEnteredFrame = 0,
                PrimaryActionState = (int)CharacterTestRuntimeAiLabLogicActionState.Running,
                PrimaryActionStageIndex = 0,
                PendingTransitionIndex = -1,
                DetectorArmed = Array.Empty<bool>()
            };
            var viewModel = new CharacterTestFairyGuiRuntimeAiPlannerDevPanelViewModel();

            CharacterTestRuntimeAiLabBehaviorCardWorkspaceViewModelBuilder.Build(
                viewModel,
                "动作测试",
                compile,
                payload,
                0,
                runtimeState: runtime,
                runtimeFrame: 9,
                activeActionProgress: new CharacterTestRuntimeAiLabActiveActionProgress(
                    17,
                    42L,
                    new MxFramework.CharacterAction.CharacterActionId(301),
                    8,
                    76));

            CharacterTestRuntimeAiLabLogicStageViewModel stage =
                viewModel.BehaviorCardWorkspace.Timeline.Stages[0];
            Assert.AreEqual(42L, stage.ActionInstanceId);
            Assert.AreEqual(9, stage.ActionProgressFrames);
            Assert.AreEqual(76, stage.ActionDurationFrames);
            Assert.IsFalse(stage.ActionProgressIndeterminate);
        }

        [Test]
        public void GameplayPlanProjection_OpenAndResolvedActionsUseTheirOwnFrameSemantics()
        {
            CharacterTestRuntimeAiLabBehaviorCardLogicPayload movePayload =
                CreateSingleStageProgressPayload(
                    CharacterTestRuntimeAiLabCardStepKind.MoveToTarget);
            CharacterTestRuntimeAiLabBehaviorCardLogicCompileResult moveCompile =
                CharacterTestRuntimeAiLabBehaviorCardLogicCompiler.Compile(movePayload);
            Assert.IsTrue(moveCompile.Success);
            var runtime = new CharacterTestRuntimeAiLabLogicAgentStateSnapshot
            {
                EntityId = 17,
                CurrentStageIndex = 0,
                StageEnteredFrame = 0,
                ActionEnteredFrame = 0,
                PendingTransitionIndex = -1,
                PrimaryActionState = (int)CharacterTestRuntimeAiLabLogicActionState.Running,
                PrimaryActionStageIndex = 0,
                DetectorArmed = Array.Empty<bool>()
            };
            var viewModel = new CharacterTestFairyGuiRuntimeAiPlannerDevPanelViewModel();

            CharacterTestRuntimeAiLabBehaviorCardWorkspaceViewModelBuilder.Build(
                viewModel,
                "移动测试",
                moveCompile,
                movePayload,
                0,
                runtimeState: runtime,
                runtimeFrame: 37);
            CharacterTestRuntimeAiLabLogicStageViewModel moveStage =
                viewModel.BehaviorCardWorkspace.Timeline.Stages[0];
            Assert.AreEqual(0, moveStage.ActionDurationFrames);
            Assert.AreEqual(37, moveStage.ActionProgressFrames);
            Assert.IsTrue(moveStage.ActionProgressIndeterminate);

            CharacterTestRuntimeAiLabBehaviorCardLogicPayload abilityPayload =
                CreateSingleStageProgressPayload(
                    CharacterTestRuntimeAiLabCardStepKind.UseAbility);
            CharacterTestRuntimeAiLabBehaviorCardLogicCompileResult abilityCompile =
                CharacterTestRuntimeAiLabBehaviorCardLogicCompiler.Compile(abilityPayload);
            Assert.IsTrue(abilityCompile.Success);
            CharacterTestRuntimeAiLabBehaviorCardWorkspaceViewModelBuilder.Build(
                viewModel,
                "能力测试",
                abilityCompile,
                abilityPayload,
                0,
                runtimeState: runtime,
                runtimeFrame: 9,
                activeActionProgress: new CharacterTestRuntimeAiLabActiveActionProgress(
                    17,
                    11,
                    MxFramework.Tests.CharacterActionLegacyFixtureIds.From("action.ability"),
                    8,
                    300));
            CharacterTestRuntimeAiLabLogicStageViewModel abilityStage =
                viewModel.BehaviorCardWorkspace.Timeline.Stages[0];
            Assert.AreEqual(300, abilityStage.ActionDurationFrames);
            Assert.AreEqual(9, abilityStage.ActionProgressFrames);
            Assert.IsFalse(abilityStage.ActionProgressIndeterminate);
        }

        [Test]
        public void GameplayPlanProjection_LegacyApproachUsesRuntimeProfileCompletionBoundary()
        {
            CharacterTestRuntimeAiLabBehaviorCardLogicPayload payload =
                CreateSingleStageProgressPayload(
                    CharacterTestRuntimeAiLabCardStepKind.MoveToTarget);
            payload.Stages[0].Title =
                CharacterTestRuntimeAiPlannerBehaviorCompositionCatalog.FoundationApproachAndAttack;
            payload.Stages[0].MoveMinDistance = 0f;
            payload.Stages[0].MoveMaxDistance = 0f;
            CharacterTestRuntimeAiLabBehaviorCardLogicCompileResult compile =
                CharacterTestRuntimeAiLabBehaviorCardLogicCompiler.Compile(payload);
            Assert.IsTrue(compile.Success);
            var viewModel = new CharacterTestFairyGuiRuntimeAiPlannerDevPanelViewModel();

            CharacterTestRuntimeAiLabBehaviorCardWorkspaceViewModelBuilder.Build(
                viewModel,
                "持续靠近目标",
                compile,
                payload,
                0,
                observedTargetDistance: 4f,
                movementCompletionDistance: 1.5f);

            CharacterTestRuntimeAiLabLogicStageViewModel stage =
                viewModel.BehaviorCardWorkspace.Timeline.Stages[0];
            Assert.AreEqual(1.5f, stage.MoveMinDistance);
            var projection = new GameplayReadablePlanProjection();
            projection.Rebuild(viewModel.BehaviorCardWorkspace.Timeline);
            Assert.AreEqual("持续靠近", projection.GetStage(0).StageLabel);
            Assert.AreEqual("距离 4.0m", projection.GetStage(0).MetricCurrentText);
            Assert.AreEqual("/ ≤1.5m", projection.GetStage(0).MetricTargetText);
        }

        [Test]
        public void GameplayPlanProjection_OpenCompletionAndFailureDoNotInventFixedTotals()
        {
            CharacterTestRuntimeAiLabBehaviorCardTimelineViewModel timeline = CreateProgressTimeline();
            CharacterTestRuntimeAiLabLogicStageViewModel stage = timeline.Stages[0];
            stage.ActionDurationFrames = 0;
            stage.ActionProgressFrames = 37;
            stage.ActionProgressIndeterminate = true;
            stage.ActionRuntimeState = (int)CharacterTestRuntimeAiLabLogicActionState.Completed;
            CharacterTestRuntimeAiLabLogicDetectorViewModel detector = timeline.Detectors[0];
            detector.WindowEndMode = CharacterTestRuntimeAiLabLogicDetectorWindowEndMode.StageEnd;
            detector.DurationFrames = 60;
            detector.RuntimeElapsedFrames = 37;
            detector.Triggered = true;

            var projection = new GameplayReadablePlanProjection();
            projection.Rebuild(timeline);
            Assert.AreEqual(
                GameplayReadablePlanProgressMode.Completed,
                projection.GetStage(0).ActionProgressMode);
            Assert.AreEqual(0, projection.GetStage(0).ActionDurationFrames);
            Assert.AreEqual(37, projection.GetStage(0).ActionProgressFrames);
            Assert.AreEqual(
                GameplayReadablePlanProgressMode.Completed,
                projection.GetExit(0).ProgressMode);
            Assert.AreEqual(0, projection.GetExit(0).ProgressDurationFrames);
            Assert.AreEqual(37, projection.GetExit(0).ProgressFrames);
            timeline.Transitions[0].Pending = true;
            projection.UpdateRuntime(timeline);
            Assert.AreEqual(
                GameplayReadablePlanExitRuntimeState.Pending,
                projection.GetExit(0).RuntimeState);
            Assert.AreEqual(
                "等待动作边界",
                GameplayReadablePlanProjection.GetRuntimeStateText(
                    projection.GetExit(0).RuntimeState));
            timeline.Transitions[0].Committed = true;
            projection.UpdateRuntime(timeline);
            Assert.AreEqual(
                GameplayReadablePlanExitRuntimeState.Committed,
                projection.GetExit(0).RuntimeState);
            Assert.AreEqual(
                "本帧已采用",
                GameplayReadablePlanProjection.GetRuntimeStateText(
                    projection.GetExit(0).RuntimeState));

            stage.ActionRuntimeState = (int)CharacterTestRuntimeAiLabLogicActionState.Failed;
            projection.UpdateRuntime(timeline);
            Assert.AreEqual(
                GameplayReadablePlanProgressMode.Failed,
                projection.GetStage(0).ActionProgressMode);
            Assert.AreEqual(0, projection.GetStage(0).ActionDurationFrames);
            Assert.AreEqual(37, projection.GetStage(0).ActionProgressFrames);

            stage.Current = false;
            stage.ActionRuntimeState = (int)CharacterTestRuntimeAiLabLogicActionState.None;
            stage.ActionProgressFrames = -1;
            detector.Active = false;
            detector.Triggered = false;
            timeline.Transitions[0].Pending = false;
            timeline.Transitions[0].Committed = false;
            projection.UpdateRuntime(timeline);
            Assert.AreEqual(
                GameplayReadablePlanProgressMode.None,
                projection.GetStage(0).ActionProgressMode);
            Assert.AreEqual(
                GameplayReadablePlanProgressMode.None,
                projection.GetExit(0).ProgressMode);
        }

        private static CharacterTestRuntimeAiLabBehaviorCardLogicPayload
            CreateSingleStageProgressPayload(
                CharacterTestRuntimeAiLabCardStepKind kind,
                int waitFrames = 0,
                string characterActionId = "")
        {
            var payload = new CharacterTestRuntimeAiLabBehaviorCardLogicPayload
            {
                EntryStageId = "stage.progress"
            };
            payload.Stages.Add(new CharacterTestRuntimeAiLabLogicStage
            {
                StageId = "stage.progress",
                Title = "进度测试",
                Kind = kind,
                CharacterActionId = int.TryParse(characterActionId, out int numericActionId)
                    ? new MxFramework.CharacterAction.CharacterActionId(numericActionId)
                    : MxFramework.Tests.CharacterActionLegacyFixtureIds.From(characterActionId),
                AbilityId = kind == CharacterTestRuntimeAiLabCardStepKind.UseAbility ? 1001 : 0,
                MoveMinDistance = 0f,
                MoveMaxDistance = 3f,
                WaitFrames = waitFrames,
                PreviewDurationFrames = kind == CharacterTestRuntimeAiLabCardStepKind.Wait
                    ? waitFrames
                    : 60,
                PlannedStartFrame = 0,
                PlannedDurationFrames = kind == CharacterTestRuntimeAiLabCardStepKind.Wait
                    ? waitFrames
                    : 0,
                DurationMode = kind == CharacterTestRuntimeAiLabCardStepKind.Wait
                    ? CharacterTestRuntimeAiLabLogicDurationMode.Fixed
                    : CharacterTestRuntimeAiLabLogicDurationMode.UntilEvent
            });
            payload.Layout.Stages.Add(new CharacterTestRuntimeAiLabLogicStageLayout
            {
                StageId = "stage.progress",
                Column = 0,
                Width = 1f
            });
            return payload;
        }

        private static CharacterTestRuntimeAiLabBehaviorCardTimelineViewModel CreateProgressTimeline()
        {
            var stage = new CharacterTestRuntimeAiLabLogicStageViewModel
            {
                Index = 0,
                StageId = "stage.approach",
                Title = "接近目标",
                Kind = CharacterTestRuntimeAiLabCardStepKind.CharacterAction,
                CharacterActionId = MxFramework.Tests.CharacterActionLegacyFixtureIds.From("action.test"),
                StartFrame = 0,
                DurationFrames = 60,
                OwnsPrimaryAction = true,
                Current = true,
                ActionRunning = true,
                ActualDurationFrames = 5,
                ActionDurationFrames = 20,
                ActionProgressFrames = 5,
                ActionRuntimeState = (int)CharacterTestRuntimeAiLabLogicActionState.Running
            };
            var detector = new CharacterTestRuntimeAiLabLogicDetectorViewModel
            {
                Index = 0,
                DetectorId = "detector.exit",
                Title = "到达目标",
                FactText = "距离目标不超过 1.5 米",
                StageIndex = 0,
                StartFrame = 0,
                DurationFrames = 60,
                Scope = CharacterTestRuntimeAiLabLogicDetectorScope.Stage,
                WindowEndMode = CharacterTestRuntimeAiLabLogicDetectorWindowEndMode.StageEnd,
                Active = true,
                Armed = true,
                RuntimeElapsedFrames = 5
            };
            var transition = new CharacterTestRuntimeAiLabLogicTransitionViewModel
            {
                Index = 0,
                TransitionId = "transition.success",
                SourceStageIndex = 0,
                TargetStageIndex = 0,
                DetectorIndex = 0,
                Label = "继续",
                TargetText = "接近目标"
            };
            return new CharacterTestRuntimeAiLabBehaviorCardTimelineViewModel
            {
                Available = true,
                RuntimeFrame = 15,
                HorizontalStartFrame = 0,
                HorizontalEndFrame = 90,
                StageCount = 1,
                DetectorCount = 1,
                TransitionCount = 1,
                Stages = new[] { stage },
                Detectors = new[] { detector },
                Transitions = new[] { transition }
            };
        }
    }
}
