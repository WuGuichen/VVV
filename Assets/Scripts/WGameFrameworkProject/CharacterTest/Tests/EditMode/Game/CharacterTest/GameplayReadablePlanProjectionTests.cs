using System;
using MxFramework.CharacterAction;
using NUnit.Framework;

namespace MxFramework.Game.CharacterTest.Tests
{
    public sealed class GameplayReadablePlanProjectionTests
    {
        [Test]
        public void Rebuild_ProjectsStableIdsAndGroupsMutuallyExclusiveOutcomes()
        {
            CharacterTestRuntimeAiLabBehaviorCardTimelineViewModel timeline =
                CreateTargetingTimeline();
            var projection = new GameplayReadablePlanProjection();

            projection.Rebuild(timeline);

            Assert.AreEqual(1, projection.StageCount);
            Assert.AreEqual(2, projection.ExitCount);
            Assert.AreEqual(1, projection.GroupCount);
            Assert.AreEqual("stage-target", projection.GetStage(0).StableId);
            Assert.AreEqual("选择最近敌人", projection.GetStage(0).ActionLabel);
            Assert.AreEqual("transition-success", projection.GetExit(0).StableId);
            Assert.AreEqual("detector-complete", projection.GetExit(0).DetectorStableId);
            Assert.AreEqual(GameplayReadablePlanExitTone.Success, projection.GetExit(0).Tone);
            Assert.AreEqual("完成 → 重新决策", projection.GetExit(0).Label);
            Assert.AreEqual("完成 → 重新决策", projection.GetExit(0).ListeningLabel);
            Assert.AreEqual("已触发 · 完成 → 重新决策", projection.GetExit(0).TriggeredLabel);
            Assert.AreEqual("已进入 · 重新决策", projection.GetExit(0).CommittedLabel);
            StringAssert.Contains("成功：已锁定有效目标", projection.GetExit(0).TooltipText);
            StringAssert.Contains("正常结束", projection.GetExit(0).TargetText);
            Assert.AreEqual("transition-failure", projection.GetExit(1).StableId);
            Assert.AreEqual(GameplayReadablePlanExitTone.Failure, projection.GetExit(1).Tone);
            Assert.AreEqual("失败 → 重新决策", projection.GetExit(1).Label);
            StringAssert.Contains("失败：没有有效目标或写入失败", projection.GetExit(1).TooltipText);
            StringAssert.Contains("失败结束", projection.GetExit(1).TargetText);
            Assert.AreEqual(0, projection.GetGroup(0).FirstExitIndex);
            Assert.AreEqual(2, projection.GetGroup(0).ExitCount);
            Assert.IsTrue(projection.SnapshotVisible);
            Assert.AreEqual(29, projection.SnapshotFrame);
            Assert.AreEqual(0, projection.SnapshotStageIndex);
        }

        [Test]
        public void Rebuild_ExplainsConditionGlobalBackSelfEndAndInvalidNodes()
        {
            CharacterTestRuntimeAiLabBehaviorCardTimelineViewModel timeline =
                CreateBranchCoverageTimeline();
            var projection = new GameplayReadablePlanProjection();

            projection.Rebuild(timeline);

            Assert.AreEqual(3, projection.GroupCount);
            Assert.AreEqual(5, projection.ExitCount);
            GameplayReadablePlanExitProjection self = projection.FindExit("transition-self");
            GameplayReadablePlanExitProjection missing = projection.FindExit("transition-missing");
            GameplayReadablePlanExitProjection back = projection.FindExit("transition-back");
            GameplayReadablePlanExitProjection noExit = projection.FindExit("detector-no-exit");
            GameplayReadablePlanExitProjection global = projection.FindExit("transition-global-end");
            Assert.NotNull(self);
            Assert.NotNull(missing);
            Assert.NotNull(back);
            Assert.NotNull(noExit);
            Assert.NotNull(global);
            Assert.AreEqual(GameplayReadablePlanExitTone.Condition, self.Tone);
            StringAssert.Contains("目标距离 ≤ 1.5", self.FactText);
            Assert.AreEqual("继续“准备” ↻", self.TargetText);
            Assert.AreEqual("目标距离 ≤ 1.5 → 继续“等待下一次决策” ↻", self.Label);
            Assert.AreEqual(GameplayReadablePlanExitTone.Invalid, missing.Tone);
            Assert.AreEqual("目标阶段缺失", missing.TargetText);
            Assert.AreEqual("回到“准备”", back.TargetText);
            Assert.AreEqual(GameplayReadablePlanExitTone.Invalid, noExit.Tone);
            StringAssert.Contains("监听条件没有出口", noExit.Label);
            Assert.IsTrue(global.CardScope);
            Assert.AreEqual("全局 · 目标可见 → 重新决策", global.Label);
            Assert.AreEqual("结束行为卡", global.TargetText);
            StringAssert.Contains("全局监听", global.WindowText);
        }

        [Test]
        public void Rebuild_ExplainsWaitForCharacterActionTransitionInGameplayLanguage()
        {
            var timeline = new CharacterTestRuntimeAiLabBehaviorCardTimelineViewModel
            {
                Available = true,
                StageCount = 1,
                DetectorCount = 1,
                Stages = new[]
                {
                    new CharacterTestRuntimeAiLabLogicStageViewModel
                    {
                        Index = 0,
                        StageId = "stage-wait-action",
                        Title = "Wait",
                        Kind = CharacterTestRuntimeAiLabCardStepKind.Logic,
                        DurationFrames = 60
                    }
                },
                Detectors = new[]
                {
                    new CharacterTestRuntimeAiLabLogicDetectorViewModel
                    {
                        Index = 0,
                        StageIndex = 0,
                        DetectorId = "detector-action-ready",
                        ConditionCount = 1,
                        Conditions = new[]
                        {
                            new CharacterTestRuntimeAiLabLogicConditionViewModel
                            {
                                Fact = CharacterTestRuntimeAiLabCardConditionFact
                                    .SelfCanTransitionToAction,
                                CharacterActionId = new CharacterActionId(301)
                            }
                        }
                    }
                }
            };
            var projection = new GameplayReadablePlanProjection();

            projection.Rebuild(timeline);

            Assert.AreEqual("等待动作可切换", projection.GetStage(0).ActionLabel);
            Assert.AreEqual("正在等待动作可切换", projection.GetStage(0).RunningLabel);
        }

        [Test]
        public void UpdateRuntime_ShowsSnapshotOnlyForLatestExactIdentity()
        {
            CharacterTestRuntimeAiLabBehaviorCardTimelineViewModel timeline =
                CreateTargetingTimeline();
            var projection = new GameplayReadablePlanProjection();
            projection.Rebuild(timeline);
            GameplayReadablePlanStageProjection stageSlot = projection.GetStage(0);
            GameplayReadablePlanExitProjection exitSlot = projection.GetExit(0);

            Assert.IsTrue(projection.SnapshotVisible);
            Assert.AreEqual(29, projection.SnapshotFrame);
            Assert.AreEqual("执行中", projection.ExecutionStatusText);

            timeline.Historical = true;
            projection.UpdateRuntime(timeline);
            Assert.IsFalse(projection.SnapshotVisible);
            Assert.AreEqual("历史帧", projection.ExecutionStatusText);

            timeline.Historical = false;
            timeline.Stages[0].Current = false;
            timeline.Stages[0].ActionRunning = false;
            timeline.Stages[0].ActualDurationFrames = -1;
            projection.UpdateRuntime(timeline);
            Assert.IsFalse(projection.SnapshotVisible);
            Assert.AreEqual("未执行", projection.ExecutionStatusText);
            Assert.AreSame(stageSlot, projection.GetStage(0));
            Assert.AreSame(exitSlot, projection.GetExit(0));

            timeline.RuntimeFrame = 42;
            projection.UpdateRuntime(timeline);
            Assert.IsFalse(projection.SnapshotVisible);
            Assert.AreEqual("未执行", projection.ExecutionStatusText);
        }

        private static CharacterTestRuntimeAiLabBehaviorCardTimelineViewModel
            CreateTargetingTimeline()
        {
            return new CharacterTestRuntimeAiLabBehaviorCardTimelineViewModel
            {
                Available = true,
                RuntimeFrame = 29,
                HorizontalStartFrame = 0,
                HorizontalEndFrame = 90,
                StageCount = 1,
                DetectorCount = 2,
                TransitionCount = 2,
                Stages = new[]
                {
                    new CharacterTestRuntimeAiLabLogicStageViewModel
                    {
                        Index = 0,
                        StageId = "stage-target",
                        Title = "Targeting.NearestEnemy",
                        Kind = CharacterTestRuntimeAiLabCardStepKind.Logic,
                        StartFrame = 0,
                        DurationFrames = 20,
                        ActualDurationFrames = 3,
                        OwnsPrimaryAction = true,
                        Current = true,
                        ActionRunning = true
                    }
                },
                Detectors = new[]
                {
                    new CharacterTestRuntimeAiLabLogicDetectorViewModel
                    {
                        Index = 0,
                        StageIndex = 0,
                        DetectorId = "detector-complete",
                        Title = "Completed",
                        Trigger = CharacterTestRuntimeAiLabLogicDetectorTrigger.ActionCompleted,
                        DurationFrames = 20,
                        LinkedTransitionCount = 1,
                        Active = true
                    },
                    new CharacterTestRuntimeAiLabLogicDetectorViewModel
                    {
                        Index = 1,
                        StageIndex = 0,
                        DetectorId = "detector-failed",
                        Title = "Failed",
                        Trigger = CharacterTestRuntimeAiLabLogicDetectorTrigger.ActionFailed,
                        DurationFrames = 20,
                        LinkedTransitionCount = 1,
                        Active = true
                    }
                },
                Transitions = new[]
                {
                    new CharacterTestRuntimeAiLabLogicTransitionViewModel
                    {
                        Index = 0,
                        TransitionId = "transition-success",
                        DetectorIndex = 0,
                        SourceStageIndex = 0,
                        TargetStageIndex = -1,
                        EndsCard = true
                    },
                    new CharacterTestRuntimeAiLabLogicTransitionViewModel
                    {
                        Index = 1,
                        TransitionId = "transition-failure",
                        DetectorIndex = 1,
                        SourceStageIndex = 0,
                        TargetStageIndex = -1,
                        EndsCard = true
                    }
                }
            };
        }

        private static CharacterTestRuntimeAiLabBehaviorCardTimelineViewModel
            CreateBranchCoverageTimeline()
        {
            return new CharacterTestRuntimeAiLabBehaviorCardTimelineViewModel
            {
                Available = true,
                HorizontalStartFrame = 0,
                HorizontalEndFrame = 120,
                StageCount = 2,
                DetectorCount = 4,
                TransitionCount = 4,
                Stages = new[]
                {
                    new CharacterTestRuntimeAiLabLogicStageViewModel
                    {
                        Index = 0,
                        StageId = "stage-ready",
                        Title = "准备",
                        Kind = CharacterTestRuntimeAiLabCardStepKind.Wait,
                        StartFrame = 0,
                        DurationFrames = 20,
                        OwnsPrimaryAction = true
                    },
                    new CharacterTestRuntimeAiLabLogicStageViewModel
                    {
                        Index = 1,
                        StageId = "stage-attack",
                        Title = "攻击",
                        Kind = CharacterTestRuntimeAiLabCardStepKind.UseAbility,
                        StartFrame = 20,
                        DurationFrames = 20,
                        OwnsPrimaryAction = true
                    }
                },
                Detectors = new[]
                {
                    new CharacterTestRuntimeAiLabLogicDetectorViewModel
                    {
                        Index = 0,
                        StageIndex = 0,
                        DetectorId = "detector-distance",
                        Trigger = CharacterTestRuntimeAiLabLogicDetectorTrigger.ConditionEdge,
                        DurationFrames = 20,
                        LinkedTransitionCount = 2,
                        ConditionCount = 1,
                        Conditions = new[]
                        {
                            new CharacterTestRuntimeAiLabLogicConditionViewModel
                            {
                                Fact = CharacterTestRuntimeAiLabCardConditionFact.TargetDistance,
                                Operator = CharacterTestRuntimeAiLabCardConditionOperator.LessOrEqual,
                                Value = 1.5f
                            }
                        }
                    },
                    new CharacterTestRuntimeAiLabLogicDetectorViewModel
                    {
                        Index = 1,
                        StageIndex = 1,
                        DetectorId = "detector-back",
                        Trigger = CharacterTestRuntimeAiLabLogicDetectorTrigger.ActionCompleted,
                        DurationFrames = 20,
                        LinkedTransitionCount = 1
                    },
                    new CharacterTestRuntimeAiLabLogicDetectorViewModel
                    {
                        Index = 2,
                        StageIndex = -1,
                        CardScope = true,
                        DetectorId = "detector-global",
                        Trigger = CharacterTestRuntimeAiLabLogicDetectorTrigger.ConditionEdge,
                        StartFrame = 5,
                        DurationFrames = 90,
                        LinkedTransitionCount = 1,
                        ConditionCount = 1,
                        Conditions = new[]
                        {
                            new CharacterTestRuntimeAiLabLogicConditionViewModel
                            {
                                Fact = CharacterTestRuntimeAiLabCardConditionFact.TargetVisible,
                                Operator = CharacterTestRuntimeAiLabCardConditionOperator.Equals,
                                BoolValue = true
                            }
                        }
                    },
                    new CharacterTestRuntimeAiLabLogicDetectorViewModel
                    {
                        Index = 3,
                        StageIndex = 1,
                        DetectorId = "detector-no-exit",
                        Title = "No route",
                        Trigger = CharacterTestRuntimeAiLabLogicDetectorTrigger.ConditionEdge,
                        DurationFrames = 20,
                        LinkedTransitionCount = 0
                    }
                },
                Transitions = new[]
                {
                    new CharacterTestRuntimeAiLabLogicTransitionViewModel
                    {
                        Index = 0,
                        TransitionId = "transition-self",
                        DetectorIndex = 0,
                        SourceStageIndex = 0,
                        TargetStageIndex = 0,
                        IsBackEdge = true
                    },
                    new CharacterTestRuntimeAiLabLogicTransitionViewModel
                    {
                        Index = 1,
                        TransitionId = "transition-missing",
                        DetectorIndex = 0,
                        SourceStageIndex = 0,
                        TargetStageIndex = 99,
                        Invalid = true
                    },
                    new CharacterTestRuntimeAiLabLogicTransitionViewModel
                    {
                        Index = 2,
                        TransitionId = "transition-back",
                        DetectorIndex = 1,
                        SourceStageIndex = 1,
                        TargetStageIndex = 0,
                        IsBackEdge = true
                    },
                    new CharacterTestRuntimeAiLabLogicTransitionViewModel
                    {
                        Index = 3,
                        TransitionId = "transition-global-end",
                        DetectorIndex = 2,
                        SourceStageIndex = 1,
                        TargetStageIndex = -1,
                        EndsCard = true
                    }
                }
            };
        }
    }
}
