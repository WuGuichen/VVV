using System;
using System.Collections.Generic;
using System.Linq;
using MxFramework.CharacterAction;
using MxFramework.Game.CharacterTest;
using NUnit.Framework;

namespace MxFramework.Tests.Game.CharacterTest
{
    public sealed partial class CharacterTestRuntimeAiLabBehaviorRecipeV5Tests
    {
        [Test]
        public void JsonRoundTrip_MigratesV5TimelineAndEventsIntoExplicitTimingV8Authority()
        {
            CharacterTestRuntimeAiLabBehaviorCardLogicPayload source = CreateTwoTimelineRecipe(3);

            string json = CharacterTestRuntimeAiLabBehaviorCardLogicJson.Serialize(source);
            CharacterTestRuntimeAiLabBehaviorCardLogicPayload restored =
                CharacterTestRuntimeAiLabBehaviorCardLogicJson.Deserialize(json);

            StringAssert.Contains("\"entryTimelineId\"", json);
            StringAssert.Contains("\"timelines\"", json);
            StringAssert.DoesNotContain("\"stages\"", json);
            StringAssert.DoesNotContain("\"detectors\"", json);
            StringAssert.DoesNotContain("\"transitions\"", json);
            Assert.AreEqual("timeline.a", restored.EntryTimelineId);
            Assert.AreEqual(2, restored.Timelines.Count);
            Assert.AreEqual(CharacterTestRuntimeAiLabBehaviorCardLogicPayload.CurrentBehaviorRecipeVersion,
                restored.BehaviorRecipeVersion);
            StringAssert.DoesNotContain("\"durationMode\"", json);
            Assert.AreEqual("primary.timeline.a", restored.Timelines[0].Events[0].EventId);
            Assert.AreEqual("event.a.to-b", restored.Timelines[0].Events[1].EventId);
            Assert.AreEqual(
                CharacterTestRuntimeAiLabBehaviorRecipeEventOperationKind.EnterTimeline,
                restored.Timelines[0].Events[1].Operation.Kind);
        }

        [Test]
        public void SemanticHash_IsCanonicalAndExcludesPresentationText()
        {
            CharacterTestRuntimeAiLabBehaviorCardLogicPayload first = CreateTwoTimelineRecipe(3);
            CharacterTestRuntimeAiLabBehaviorCardLogicPayload reordered = first.Clone();
            CharacterTestRuntimeAiLabBehaviorRecipeTimeline moved = reordered.Timelines[0];
            reordered.Timelines.RemoveAt(0);
            reordered.Timelines.Add(moved);
            reordered.Timelines[0].Title = "presentation-only title";
            reordered.Timelines[0].Note = "presentation-only note";
            reordered.Intent = "presentation-only intent";
            reordered.RiskSummary = "presentation-only risk";

            long firstHash = CharacterTestRuntimeAiLabBehaviorRecipeV5SemanticHash.Compute(first);
            long reorderedHash = CharacterTestRuntimeAiLabBehaviorRecipeV5SemanticHash.Compute(reordered);
            Assert.AreEqual(firstHash, reorderedHash);

            reordered.Timelines[1].TriggerEvents[0].LocalFrame = 4;
            Assert.AreNotEqual(
                firstHash,
                CharacterTestRuntimeAiLabBehaviorRecipeV5SemanticHash.Compute(reordered));

            CharacterTestRuntimeAiLabBehaviorCardLogicPayload fallbackDuration = CreateActionSignalRecipe();
            long fallbackDurationHash =
                CharacterTestRuntimeAiLabBehaviorRecipeV5SemanticHash.Compute(fallbackDuration);
            fallbackDuration.Timelines[0].PrimaryActionEvent.FallbackDurationFrames++;
            Assert.AreNotEqual(
                fallbackDurationHash,
                CharacterTestRuntimeAiLabBehaviorRecipeV5SemanticHash.Compute(fallbackDuration),
                "The compatibility duration can resolve PrimaryActionEnd and therefore remains runtime semantics.");
        }

        [Test]
        public void V4Migration_CollapsesStaticResultsWithoutSynthesizingRuntimeExits()
        {
            var legacy = new CharacterTestRuntimeAiLabBehaviorCardLogicPayload
            {
                EntryStageId = "move"
            };
            legacy.ActivationConditions.Add(new CharacterTestRuntimeAiLabCardCondition
            {
                ConditionId = "activation.distance",
                Fact = CharacterTestRuntimeAiLabCardConditionFact.TargetDistance,
                Operator = CharacterTestRuntimeAiLabCardConditionOperator.GreaterThan,
                Value = 1.5f
            });
            legacy.Stages.Add(CreateLegacyStage("move", 0, CharacterTestRuntimeAiLabCardStepKind.MoveToTarget));
            legacy.Stages.Add(CreateLegacyStage("fallback", 1, CharacterTestRuntimeAiLabCardStepKind.Logic));
            legacy.Detectors.Add(CreateLegacyPredicateDetector("detector.route", "move"));
            legacy.Transitions.Add(new CharacterTestRuntimeAiLabLogicTransition
            {
                TransitionId = "route.low",
                DetectorId = "detector.route",
                SourceStageId = "move",
                TargetStageId = "fallback",
                Priority = 10
            });
            legacy.Transitions.Add(new CharacterTestRuntimeAiLabLogicTransition
            {
                TransitionId = "route.high",
                DetectorId = "detector.route",
                SourceStageId = "move",
                TargetStageId = CharacterTestRuntimeAiLabLogicTransition.EndTarget,
                Priority = 20,
                EndReason = "Replan"
            });

            CharacterTestRuntimeAiLabBehaviorRecipeV5Migration.RebuildV5FromLegacy(legacy);

            Assert.AreEqual("move", legacy.EntryTimelineId);
            Assert.AreEqual(2, legacy.Timelines.Count);
            CharacterTestRuntimeAiLabBehaviorRecipeTriggerEvent route =
                FindEvent(legacy.Timelines[0], "route.high");
            Assert.NotNull(route);
            Assert.AreEqual(CharacterTestRuntimeAiLabBehaviorRecipeOutcomeKind.ReturnToPlanner, route.OutcomeKind);
            Assert.IsNull(FindEvent(legacy.Timelines[0], "guard.move.activation.distance"),
                "Entry eligibility must not synthesize a runtime exit Event.");
            Assert.AreEqual(1, legacy.ActivationConditions.Count,
                "Migration must preserve the entry condition itself.");
            Assert.IsTrue(ContainsDiagnostic(legacy.MigrationDiagnostics, "CARD_V5_MIGRATION_TRANSITIONS_COLLAPSED"));
        }

        [Test]
        public void V4Migration_PreservesAnExplicitActivationExitAsAnOrdinaryEvent()
        {
            var legacy = new CharacterTestRuntimeAiLabBehaviorCardLogicPayload
            {
                EntryStageId = "approach"
            };
            var activation = new CharacterTestRuntimeAiLabCardCondition
            {
                ConditionId = "activation.target",
                Fact = CharacterTestRuntimeAiLabCardConditionFact.TargetExists,
                Operator = CharacterTestRuntimeAiLabCardConditionOperator.Equals,
                BoolValue = true
            };
            legacy.ActivationConditions.Add(activation);
            legacy.Stages.Add(CreateLegacyStage(
                "approach",
                0,
                CharacterTestRuntimeAiLabCardStepKind.MoveToTarget));
            var explicitExit = new CharacterTestRuntimeAiLabLogicDetector
            {
                DetectorId = "event.target-missing",
                StageId = "approach",
                Scope = CharacterTestRuntimeAiLabLogicDetectorScope.Stage,
                Trigger = CharacterTestRuntimeAiLabLogicDetectorTrigger.ConditionEdge,
                WindowEndMode = CharacterTestRuntimeAiLabLogicDetectorWindowEndMode.Infinite,
                ExpectedResult = false,
                Priority = 1000
            };
            explicitExit.Conditions.Add(activation.Clone());
            legacy.Detectors.Add(explicitExit);
            legacy.Transitions.Add(new CharacterTestRuntimeAiLabLogicTransition
            {
                TransitionId = "event.target-missing",
                DetectorId = explicitExit.DetectorId,
                SourceStageId = "approach",
                TargetStageId = CharacterTestRuntimeAiLabLogicTransition.EndTarget,
                Priority = explicitExit.Priority
            });

            CharacterTestRuntimeAiLabBehaviorRecipeV5Migration.RebuildV5FromLegacy(legacy);

            Assert.AreEqual(1, legacy.Timelines[0].TriggerEvents.Count,
                "Only the explicitly authored runtime Event should exist.");
            Assert.NotNull(FindEvent(legacy.Timelines[0], "event.target-missing"));
            Assert.IsNull(FindEvent(legacy.Timelines[0], "guard.approach.activation.target"));
        }

        [Test]
        public void NativeV5Read_RemovesSupersededActivationGuardAndKeepsOrdinaryEvent()
        {
            CharacterTestRuntimeAiLabBehaviorCardLogicPayload payload = CreateTwoTimelineRecipe(3);
            CharacterTestRuntimeAiLabBehaviorRecipeTimeline timeline = payload.Timelines[0];
            var activation = new CharacterTestRuntimeAiLabCardCondition
            {
                ConditionId = "activation.target",
                Fact = CharacterTestRuntimeAiLabCardConditionFact.TargetExists,
                Operator = CharacterTestRuntimeAiLabCardConditionOperator.Equals,
                BoolValue = true
            };
            payload.ActivationConditions.Add(activation);
            timeline.TriggerEvents.Clear();
            timeline.TriggerEvents.Add(new CharacterTestRuntimeAiLabBehaviorRecipeTriggerEvent
            {
                EventId = "event.target-missing",
                TimelineId = timeline.TimelineId,
                SourceKind = CharacterTestRuntimeAiLabBehaviorRecipeTriggerSourceKind.Predicate,
                ExpectedResult = false,
                OutcomeKind = CharacterTestRuntimeAiLabBehaviorRecipeOutcomeKind.ReturnToPlanner
            });
            timeline.TriggerEvents[0].Conditions.Add(activation.Clone());
            timeline.TriggerEvents.Add(new CharacterTestRuntimeAiLabBehaviorRecipeTriggerEvent
            {
                EventId = "guard.timeline.a.activation.target",
                TimelineId = timeline.TimelineId,
                SourceKind = CharacterTestRuntimeAiLabBehaviorRecipeTriggerSourceKind.Predicate,
                ExpectedResult = false,
                OutcomeKind = CharacterTestRuntimeAiLabBehaviorRecipeOutcomeKind.ReturnToPlanner
            });
            timeline.TriggerEvents[1].Conditions.Add(activation.Clone());
            CharacterTestRuntimeAiLabBehaviorRecipeV5Migration.RebuildLegacyProjection(payload);

            CharacterTestRuntimeAiLabBehaviorRecipeV5Migration.SynchronizeForRead(payload);

            Assert.AreEqual(1, timeline.TriggerEvents.Count);
            Assert.AreEqual("event.target-missing", timeline.TriggerEvents[0].EventId);
            Assert.AreEqual(1, payload.Detectors.Count(candidate =>
                candidate != null && candidate.StageId == timeline.TimelineId));
        }

        [Test]
        public void V4Migration_CardScopeDetectorUsesRecipeClockCompatibilityEvents()
        {
            var legacy = new CharacterTestRuntimeAiLabBehaviorCardLogicPayload
            {
                EntryStageId = "a"
            };
            legacy.Stages.Add(CreateLegacyStage("a", 0, CharacterTestRuntimeAiLabCardStepKind.Logic));
            legacy.Stages.Add(CreateLegacyStage("b", 1, CharacterTestRuntimeAiLabCardStepKind.Logic));
            CharacterTestRuntimeAiLabLogicDetector shared = CreateLegacyPredicateDetector("shared", string.Empty);
            shared.Scope = CharacterTestRuntimeAiLabLogicDetectorScope.Card;
            legacy.Detectors.Add(shared);
            legacy.Transitions.Add(new CharacterTestRuntimeAiLabLogicTransition
            {
                TransitionId = "shared.a",
                DetectorId = "shared",
                SourceStageId = "a",
                TargetStageId = "b",
                Priority = 10
            });
            legacy.Transitions.Add(new CharacterTestRuntimeAiLabLogicTransition
            {
                TransitionId = "shared.b",
                DetectorId = "shared",
                SourceStageId = "b",
                TargetStageId = CharacterTestRuntimeAiLabLogicTransition.EndTarget,
                Priority = 10
            });

            CharacterTestRuntimeAiLabBehaviorRecipeV5Migration.RebuildV5FromLegacy(legacy);

            Assert.AreEqual(2, legacy.LegacySharedTriggerEvents.Count);
            Assert.AreEqual(
                CharacterTestRuntimeAiLabBehaviorRecipeClockOrigin.Recipe,
                legacy.LegacySharedTriggerEvents[0].ClockOrigin);
            Assert.AreEqual(
                CharacterTestRuntimeAiLabBehaviorRecipeClockOrigin.Recipe,
                legacy.LegacySharedTriggerEvents[1].ClockOrigin);
            Assert.AreEqual("shared", legacy.LegacySharedTriggerEvents[0].LegacySharedTriggerId);
            Assert.AreEqual(
                legacy.LegacySharedTriggerEvents[0].LegacySharedTriggerId,
                legacy.LegacySharedTriggerEvents[1].LegacySharedTriggerId);
        }

        [Test]
        public void Compiler_MigratesLegacySharedPredicateAsRecipeClockCompatibilityEvents()
        {
            var legacy = new CharacterTestRuntimeAiLabBehaviorCardLogicPayload
            {
                EntryStageId = "a"
            };
            legacy.Stages.Add(CreateLegacyStage("a", 0, CharacterTestRuntimeAiLabCardStepKind.Logic));
            legacy.Stages.Add(CreateLegacyStage("b", 1, CharacterTestRuntimeAiLabCardStepKind.Logic));
            CharacterTestRuntimeAiLabLogicDetector shared = CreateLegacyPredicateDetector("shared", string.Empty);
            shared.Scope = CharacterTestRuntimeAiLabLogicDetectorScope.Card;
            legacy.Detectors.Add(shared);
            legacy.Transitions.Add(new CharacterTestRuntimeAiLabLogicTransition
            {
                TransitionId = "shared.a",
                DetectorId = "shared",
                SourceStageId = "a",
                TargetStageId = "b",
                Priority = 10
            });
            legacy.Transitions.Add(new CharacterTestRuntimeAiLabLogicTransition
            {
                TransitionId = "shared.b",
                DetectorId = "shared",
                SourceStageId = "b",
                TargetStageId = CharacterTestRuntimeAiLabLogicTransition.EndTarget,
                Priority = 10
            });
            CharacterTestRuntimeAiLabBehaviorRecipeV5Migration.RebuildV5FromLegacy(legacy);
            ConfirmAllMigratedV7Intervals(legacy);
            CharacterTestRuntimeAiLabBehaviorCardLogicCompileResult compile =
                CompileReference(legacy);
            Assert.IsTrue(compile.Success, FormatDiagnostics(compile.Diagnostics));
            Assert.IsTrue(compile.Program.IsBehaviorRecipeV6);
            Assert.AreEqual(2, legacy.LegacySharedEvents.Count);
            for (int i = 0; i < legacy.LegacySharedEvents.Count; i++)
            {
                Assert.AreEqual(CharacterTestRuntimeAiLabBehaviorRecipeClockOrigin.Recipe,
                    legacy.LegacySharedEvents[i].ClockOrigin);
                Assert.AreEqual("shared", legacy.LegacySharedEvents[i].LegacySharedEventId);
            }
        }

        [Test]
        public void Validator_RejectsZeroFrameUnresolvedEndMissingReasonAndUnreachableTimeline()
        {
            CharacterTestRuntimeAiLabBehaviorCardLogicPayload payload = CreateTwoTimelineRecipe(3);
            CharacterTestRuntimeAiLabBehaviorRecipeTimeline entry = payload.Timelines[0];
            entry.TriggerEvents[0].LocalFrame = 0;
            entry.TriggerEvents.Add(new CharacterTestRuntimeAiLabBehaviorRecipeTriggerEvent
            {
                EventId = "event.unresolved-end",
                TimelineId = entry.TimelineId,
                SourceKind = CharacterTestRuntimeAiLabBehaviorRecipeTriggerSourceKind.LocalFrame,
                ClockOrigin = CharacterTestRuntimeAiLabBehaviorRecipeClockOrigin.Timeline,
                LocalFrameAnchor = CharacterTestRuntimeAiLabBehaviorRecipeLocalFrameAnchor.PrimaryActionEnd,
                WindowEndMode = CharacterTestRuntimeAiLabLogicDetectorWindowEndMode.Infinite,
                OutcomeKind = CharacterTestRuntimeAiLabBehaviorRecipeOutcomeKind.ReturnToPlanner
            });
            entry.PrimaryActionEvent.Kind = CharacterTestRuntimeAiLabCardStepKind.Logic;
            entry.TriggerEvents.Add(new CharacterTestRuntimeAiLabBehaviorRecipeTriggerEvent
            {
                EventId = "event.failed-without-reason",
                TimelineId = entry.TimelineId,
                SourceKind = CharacterTestRuntimeAiLabBehaviorRecipeTriggerSourceKind.PrimaryActionSignal,
                ClockOrigin = CharacterTestRuntimeAiLabBehaviorRecipeClockOrigin.Timeline,
                ActionSignal = CharacterTestRuntimeAiLabBehaviorRecipePrimaryActionSignal.Failed,
                WindowEndMode = CharacterTestRuntimeAiLabLogicDetectorWindowEndMode.Infinite,
                OutcomeKind = CharacterTestRuntimeAiLabBehaviorRecipeOutcomeKind.ReturnToPlanner
            });
            entry.TriggerEvents.Add(new CharacterTestRuntimeAiLabBehaviorRecipeTriggerEvent
            {
                EventId = "event.completed-without-legacy-marker",
                TimelineId = entry.TimelineId,
                SourceKind = CharacterTestRuntimeAiLabBehaviorRecipeTriggerSourceKind.PrimaryActionSignal,
                ClockOrigin = CharacterTestRuntimeAiLabBehaviorRecipeClockOrigin.Timeline,
                ActionSignal = CharacterTestRuntimeAiLabBehaviorRecipePrimaryActionSignal.Completed,
                WindowEndMode = CharacterTestRuntimeAiLabLogicDetectorWindowEndMode.Infinite,
                OutcomeKind = CharacterTestRuntimeAiLabBehaviorRecipeOutcomeKind.ReturnToPlanner
            });
            payload.Timelines.Add(CreateTimeline("timeline.unreachable", 9, CharacterTestRuntimeAiLabCardStepKind.Logic));

            IReadOnlyList<CharacterTestRuntimeAiLabCardDiagnostic> diagnostics =
                CharacterTestRuntimeAiLabBehaviorRecipeV5Validator.Validate(payload);

            Assert.IsTrue(ContainsDiagnostic(diagnostics, "CARD_V5_LOCAL_FRAME_ZERO"));
            Assert.IsTrue(ContainsDiagnostic(diagnostics, "CARD_V5_PRIMARY_END_UNRESOLVED"));
            Assert.IsTrue(ContainsDiagnostic(diagnostics, "CARD_V5_ACTION_SIGNAL_OWNER"));
            Assert.IsTrue(ContainsDiagnostic(diagnostics, "CARD_V5_ACTION_SIGNAL_REASON"));
            Assert.IsTrue(ContainsDiagnostic(diagnostics, "CARD_V5_ACTION_SIGNAL_COMPLETED"));
            Assert.IsTrue(ContainsDiagnostic(diagnostics, "CARD_V5_TIMELINE_UNREACHABLE"));
        }

        [Test]
        public void Evaluator_LocalFrameCrossingJumpsOnceAndStartsTargetAtZeroOnNextFrame()
        {
            CharacterTestRuntimeAiLabBehaviorCardLogicPayload payload = CreateTwoTimelineRecipe(3);
            ConfirmAllMigratedV7Intervals(payload);
            CharacterTestRuntimeAiLabBehaviorCardLogicCompileResult compile =
                CompileReference(payload);
            var runtime = new RecipeRuntimeStub();
            var evaluator = new CharacterTestBehaviorRecipeReferenceEvaluator(compile.Program, runtime);
            var events = new CharacterTestRuntimeAiLabLogicEventBuffer(32);

            Assert.IsTrue(compile.Success, FormatDiagnostics(compile.Diagnostics));
            Assert.IsTrue(compile.Program.IsBehaviorRecipeV6);
            Assert.IsTrue(evaluator.Start(10, events));
            evaluator.Tick(11, events);
            Assert.AreEqual(1, Count(events, CharacterTestRuntimeAiLabLogicEventKind.DetectorTriggered),
                "The ordinary 0f movement Event starts at the first local frame.");

            evaluator.Tick(12, events);
            evaluator.Tick(13, events);
            evaluator.Tick(14, events);
            Assert.AreEqual(1, Count(events, CharacterTestRuntimeAiLabLogicEventKind.TransitionPending));
            Assert.AreEqual(CharacterTestRuntimeAiLabLogicRuntimeState.Transitioning, evaluator.State);
            Assert.AreEqual(0, evaluator.CurrentTimelineIndex);
            CharacterTestRuntimeAiLabLogicEvent triggered = default;
            for (int i = 0; i < events.Count; i++)
            {
                if (events[i].Kind != CharacterTestRuntimeAiLabLogicEventKind.TransitionPending)
                    continue;
                triggered = events[i];
                break;
            }
            Assert.AreEqual("timeline.a", triggered.TimelineId);
            Assert.AreEqual("event.a.to-b", triggered.EventId);
            Assert.AreEqual("timeline.b", triggered.TargetTimelineId);
            Assert.AreEqual(3, triggered.LocalFrame);
            Assert.AreEqual("event.a.to-b", evaluator.LastTriggeredEventId);

            var index = new CharacterTestRuntimeAiLabTimelineEventIndex();
            index.Reset(10, 10);
            index.CaptureFrame(14, null, null, null, null, events);
            CharacterTestRuntimeAiLabTimelineEventSample sample = default;
            for (int i = 0; i < index.GetCount(14); i++)
            {
                CharacterTestRuntimeAiLabTimelineEventSample candidate = index.GetSample(14, i);
                if (candidate.Kind !=
                    CharacterTestRuntimeAiLabTimelineEventKind.BehaviorTransitionPending)
                {
                    continue;
                }
                sample = candidate;
                break;
            }
            Assert.AreEqual("event.a.to-b", sample.Label);
            Assert.AreEqual("timeline.b", sample.BehaviorTargetTimelineId);
            Assert.AreEqual(3, sample.BehaviorLocalFrame);

            evaluator.Tick(15, events);
            Assert.AreEqual(CharacterTestRuntimeAiLabLogicRuntimeState.Running, evaluator.State);
            Assert.AreEqual(1, evaluator.CurrentTimelineIndex);
            Assert.AreEqual(0, evaluator.LocalFrame);
            Assert.AreEqual(1, runtime.JumpRequests,
                $"Timeline control must submit exactly one orchestration jump; localFrame={evaluator.LocalFrame}.");
            Assert.AreEqual("timeline.b", runtime.CurrentTimelineId);
        }

        [Test]
        public void Evaluator_SameFrameChoosesPriorityThenOrdinalEventId()
        {
            CharacterTestRuntimeAiLabBehaviorCardLogicPayload payload = CreateThreeTimelineTieRecipe();
            ConfirmAllMigratedV7Intervals(payload);
            CharacterTestRuntimeAiLabBehaviorCardLogicCompileResult compile =
                CompileReference(payload);
            var runtime = new RecipeRuntimeStub();
            var evaluator = new CharacterTestBehaviorRecipeReferenceEvaluator(compile.Program, runtime);
            var events = new CharacterTestRuntimeAiLabLogicEventBuffer(32);

            Assert.IsTrue(compile.Success, FormatDiagnostics(compile.Diagnostics));
            Assert.IsTrue(evaluator.Start(0, events));
            evaluator.Tick(1, events);
            Assert.AreEqual(0, Count(events, CharacterTestRuntimeAiLabLogicEventKind.TransitionPending));
            evaluator.Tick(2, events);
            Assert.AreEqual(
                1,
                Count(events, CharacterTestRuntimeAiLabLogicEventKind.TransitionPending),
                $"localFrame={evaluator.LocalFrame}, jumpRequests={runtime.JumpRequests}, eventCount={events.Count}");
            Assert.AreEqual(1, runtime.JumpRequests,
                $"Tie winner must submit one timeline jump; localFrame={evaluator.LocalFrame}.");
            evaluator.Tick(3, events);
            Assert.AreEqual("timeline.b", runtime.CurrentTimelineId, "event.a must win the ordinal tie.");
            Assert.AreEqual(
                1,
                Count(events, CharacterTestRuntimeAiLabLogicEventKind.TransitionCommitted),
                $"Committed timeline jump must be recorded; state={evaluator.State}, eventCount={events.Count}.");
        }

        [Test]
        public void Evaluator_PrimaryActionSignalMatchesOwnedInstanceAndConcreteReason()
        {
            CharacterTestRuntimeAiLabBehaviorCardLogicPayload payload = CreateActionSignalRecipe();
            ConfirmAllMigratedV7Intervals(payload);
            CharacterTestRuntimeAiLabBehaviorCardLogicCompileResult compile =
                CompileReference(payload);
            var runtime = new RecipeRuntimeStub();
            var evaluator = new CharacterTestBehaviorRecipeReferenceEvaluator(compile.Program, runtime);
            var events = new CharacterTestRuntimeAiLabLogicEventBuffer(16);

            Assert.IsTrue(compile.Success, FormatDiagnostics(compile.Diagnostics));
            Assert.IsTrue(evaluator.Start(0, events));
            evaluator.Tick(1, events);
            long owned = evaluator.OwnedPrimaryActionInstanceId;
            Assert.Greater(owned, 0L);
            runtime.PrimaryActionState = CharacterTestRuntimeAiLabLogicActionState.Failed;
            runtime.PrimaryActionSignalReasonCode = 1001;
            runtime.PrimaryActionInstanceId = owned + 1L;
            evaluator.Tick(2, events);
            Assert.AreEqual(0, Count(events, CharacterTestRuntimeAiLabLogicEventKind.DetectorTriggered));

            runtime.PrimaryActionInstanceId = owned;
            evaluator.Tick(3, events);
            Assert.AreEqual(1, Count(events, CharacterTestRuntimeAiLabLogicEventKind.TransitionPending));
            Assert.AreEqual("event.action.failed", evaluator.LastTriggeredEventId);
        }

        [Test]
        public void Evaluator_SelfLoopReentersTimelineAndRestartsLocalClock()
        {
            CharacterTestRuntimeAiLabBehaviorCardLogicPayload payload = CreateSelfLoopRecipe();
            ConfirmAllMigratedV7Intervals(payload);
            CharacterTestRuntimeAiLabBehaviorCardLogicCompileResult compile =
                CompileReference(payload);
            var runtime = new RecipeRuntimeStub();
            var evaluator = new CharacterTestBehaviorRecipeReferenceEvaluator(compile.Program, runtime);
            var events = new CharacterTestRuntimeAiLabLogicEventBuffer(16);

            Assert.IsTrue(compile.Success, FormatDiagnostics(compile.Diagnostics));
            Assert.IsTrue(evaluator.Start(20, events));
            evaluator.Tick(21, events);
            evaluator.Tick(22, events);
            evaluator.Tick(23, events);
            Assert.AreEqual(0, evaluator.CurrentTimelineIndex);
            Assert.AreEqual(0, evaluator.LocalFrame);
            Assert.AreEqual(1, runtime.JumpRequests);
            Assert.AreEqual(3, runtime.EnterCount);

            evaluator.Tick(24, events);
            evaluator.Tick(25, events);
            Assert.AreEqual(2, runtime.JumpRequests, "the reset marker must fire again on the next loop.");
        }

        [Test]
        public void EvaluatorSnapshot_RestoresPendingJumpArmedAndFiredState()
        {
            CharacterTestRuntimeAiLabBehaviorCardLogicPayload payload = CreateTwoTimelineRecipe(2);
            ConfirmAllMigratedV7Intervals(payload);
            CharacterTestRuntimeAiLabBehaviorCardLogicCompileResult compile =
                CompileReference(payload);
            var firstRuntime = new RecipeRuntimeStub();
            var first = new CharacterTestBehaviorRecipeReferenceEvaluator(compile.Program, firstRuntime);
            var events = new CharacterTestRuntimeAiLabLogicEventBuffer(16);
            Assert.IsTrue(first.Start(30, events));
            first.Tick(31, events);
            first.Tick(32, events);
            first.Tick(33, events);
            CharacterTestRuntimeAiLabLogicEvaluatorSnapshot snapshot = first.CreateSnapshot();

            var restoredRuntime = new RecipeRuntimeStub
            {
                OrchestrationState = CharacterTestRuntimeAiLabBehaviorRecipeOrchestrationState.Succeeded
            };
            var restored = new CharacterTestBehaviorRecipeReferenceEvaluator(compile.Program, restoredRuntime);
            Assert.IsTrue(restored.TryRestoreSnapshot(snapshot));
            restored.Tick(34, events);

            Assert.AreEqual(1, restored.CurrentTimelineIndex);
            Assert.AreEqual(0, restored.LocalFrame);
            Assert.AreEqual(
                compile.Program.RecipeV6.Events.Single(item => item.EventId == "event.a.to-b").Index,
                snapshot.LastTriggeredEventIndex);
            Assert.Greater(snapshot.EventExecutionCounts.Sum(), 0);
        }

        [Test]
        public void TriggerEventInspector_ProjectsFinalBooleanMeaningDirectlyFromV5Event()
        {
            CharacterTestRuntimeAiLabBehaviorCardLogicPayload payload = CreatePredicateRecipe();
            var source = new CharacterTestRuntimeAiLabBehaviorCardTimelineViewModel { CanEdit = true };
            source.Presentation.SelectedRecipeEventKind = BehaviorRecipeEventKind.Branch;
            source.Presentation.SelectedRecipeEventStableKey =
                "event:timeline.a:detector:event.event.target-missing";

            BehaviorRecipeTriggerEventInspectorProjection.Rebuild(source, payload, null);

            Assert.NotNull(source.TriggerEventInspector);
            Assert.AreEqual("event.target-missing", source.TriggerEventInspector.EventId);
            Assert.AreEqual(0, source.TriggerEventInspector.Conditions[0].EffectiveOperator);
            Assert.IsFalse(source.TriggerEventInspector.Conditions[0].BoolValue);
            StringAssert.Contains("TargetExists = 否", source.TriggerEventInspector.EffectiveTriggerSummary);
        }

        [Test]
        public void TriggerEventMutation_EditsFinalBooleanAndRebuildsLegacyProjection()
        {
            CharacterTestRuntimeAiLabBehaviorCardLogicPayload payload = CreatePredicateRecipe();
            CharacterTestRuntimeAiLabBehaviorRecipeTimeline timeline = payload.Timelines[0];
            CharacterTestRuntimeAiLabBehaviorRecipeTriggerEvent trigger = timeline.TriggerEvents[0];

            bool accepted = CharacterTestRuntimeAiLabBehaviorRecipeTriggerEventMutation.TryApply(
                payload,
                timeline.TimelineId,
                trigger.EventId,
                CharacterTestRuntimeAiLabBehaviorRecipeTriggerEventField.ConditionBoolValue,
                0,
                new CharacterTestRuntimeAiLabBehaviorRecipeTriggerEventFieldValue(boolValue: true),
                out bool changed,
                out string diagnostic);

            Assert.IsTrue(accepted, diagnostic);
            Assert.IsTrue(changed);
            Assert.IsFalse(trigger.Conditions[0].BoolValue,
                "ExpectedResult=false stores the inverse raw value while the Inspector edits the final meaning.");
            Assert.Greater(payload.Detectors.Count, 0,
                "Read-only legacy consumers must be rebuilt after native v5 mutation.");
            Assert.AreEqual(1, timeline.TriggerEvents.Count,
                "Native mutation must not create guard or compatibility events.");
        }

        [Test]
        public void TriggerEventMutation_UpdatesTimelineGeometryWithoutLegacyFieldEditing()
        {
            CharacterTestRuntimeAiLabBehaviorCardLogicPayload payload = CreatePredicateRecipe();
            CharacterTestRuntimeAiLabBehaviorRecipeTimeline timeline = payload.Timelines[0];
            CharacterTestRuntimeAiLabBehaviorRecipeTriggerEvent trigger = timeline.TriggerEvents[0];

            Assert.IsTrue(CharacterTestRuntimeAiLabBehaviorRecipeTriggerEventMutation.TryApply(
                payload,
                timeline.TimelineId,
                trigger.EventId,
                CharacterTestRuntimeAiLabBehaviorRecipeTriggerEventField.ActivationStartFrame,
                -1,
                new CharacterTestRuntimeAiLabBehaviorRecipeTriggerEventFieldValue(intValue: 12),
                out bool changed,
                out string diagnostic), diagnostic);
            Assert.IsTrue(changed);
            Assert.AreEqual(12, trigger.ActivationStartFrame);

            Assert.AreEqual(
                CharacterTestRuntimeAiLabLogicDetectorWindowEndMode.Infinite,
                trigger.WindowEndMode);
            Assert.IsTrue(CharacterTestRuntimeAiLabBehaviorRecipeTriggerEventMutation.TryApply(
                payload,
                timeline.TimelineId,
                trigger.EventId,
                CharacterTestRuntimeAiLabBehaviorRecipeTriggerEventField.ActivationDurationFrames,
                -1,
                new CharacterTestRuntimeAiLabBehaviorRecipeTriggerEventFieldValue(intValue: 24),
                out changed,
                out diagnostic), diagnostic);
            Assert.IsTrue(changed);
            Assert.AreEqual(24, trigger.ActivationDurationFrames);
            Assert.AreEqual(
                CharacterTestRuntimeAiLabLogicDetectorWindowEndMode.Fixed,
                trigger.WindowEndMode,
                "Dragging an open span's right edge turns it into one fixed-width v5 event mutation.");
            Assert.AreEqual(12, payload.Detectors[0].ActivationStartFrame);
            Assert.AreEqual(24, payload.Detectors[0].ActivationDurationFrames);
        }

        [Test]
        public void TriggerEventMutation_RejectsPrimaryActionEndForLogicAndMovesAbsoluteFrame()
        {
            CharacterTestRuntimeAiLabBehaviorCardLogicPayload payload = CreateTwoTimelineRecipe(3);
            CharacterTestRuntimeAiLabBehaviorRecipeTimeline waitTimeline = payload.Timelines[0];
            CharacterTestRuntimeAiLabBehaviorRecipeTriggerEvent trigger = waitTimeline.TriggerEvents[0];

            Assert.IsTrue(CharacterTestRuntimeAiLabBehaviorRecipeTriggerEventMutation.TryApply(
                payload,
                waitTimeline.TimelineId,
                trigger.EventId,
                CharacterTestRuntimeAiLabBehaviorRecipeTriggerEventField.LocalFrame,
                -1,
                new CharacterTestRuntimeAiLabBehaviorRecipeTriggerEventFieldValue(intValue: 90),
                out bool changed,
                out string diagnostic), diagnostic);
            Assert.IsTrue(changed);
            Assert.AreEqual(90, trigger.LocalFrame);

            CharacterTestRuntimeAiLabBehaviorRecipeTimeline logicTimeline = payload.Timelines[1];
            CharacterTestRuntimeAiLabBehaviorRecipeTriggerEvent logicEvent = CreateLocalFrameEvent(
                "event.logic",
                logicTimeline.TimelineId,
                4,
                logicTimeline.TimelineId,
                1);
            logicTimeline.TriggerEvents.Add(logicEvent);
            Assert.IsFalse(CharacterTestRuntimeAiLabBehaviorRecipeTriggerEventMutation.TryApply(
                payload,
                logicTimeline.TimelineId,
                logicEvent.EventId,
                CharacterTestRuntimeAiLabBehaviorRecipeTriggerEventField.LocalFrameAnchor,
                -1,
                new CharacterTestRuntimeAiLabBehaviorRecipeTriggerEventFieldValue(
                    intValue: (int)CharacterTestRuntimeAiLabBehaviorRecipeLocalFrameAnchor.PrimaryActionEnd),
                out _,
                out diagnostic));
            StringAssert.Contains("没有可解析的结束帧", diagnostic);
        }

        private static CharacterTestRuntimeAiLabBehaviorCardLogicPayload CreatePredicateRecipe()
        {
            var payload = new CharacterTestRuntimeAiLabBehaviorCardLogicPayload
            {
                EntryTimelineId = "timeline.a"
            };
            CharacterTestRuntimeAiLabBehaviorRecipeTimeline timeline =
                CreateTimeline("timeline.a", 0, CharacterTestRuntimeAiLabCardStepKind.Logic);
            var trigger = new CharacterTestRuntimeAiLabBehaviorRecipeTriggerEvent
            {
                EventId = "event.target-missing",
                TimelineId = timeline.TimelineId,
                SourceKind = CharacterTestRuntimeAiLabBehaviorRecipeTriggerSourceKind.Predicate,
                ExpectedResult = false,
                ClockOrigin = CharacterTestRuntimeAiLabBehaviorRecipeClockOrigin.Timeline,
                WindowEndMode = CharacterTestRuntimeAiLabLogicDetectorWindowEndMode.Infinite,
                OutcomeKind = CharacterTestRuntimeAiLabBehaviorRecipeOutcomeKind.ReturnToPlanner
            };
            trigger.Conditions.Add(new CharacterTestRuntimeAiLabCardCondition
            {
                ConditionId = "condition.target-exists",
                Fact = CharacterTestRuntimeAiLabCardConditionFact.TargetExists,
                Operator = CharacterTestRuntimeAiLabCardConditionOperator.Equals,
                BoolValue = true
            });
            timeline.TriggerEvents.Add(trigger);
            payload.Timelines.Add(timeline);
            return payload;
        }

        private static CharacterTestRuntimeAiLabBehaviorCardLogicPayload CreateTwoTimelineRecipe(int frame)
        {
            var payload = new CharacterTestRuntimeAiLabBehaviorCardLogicPayload
            {
                EntryTimelineId = "timeline.a"
            };
            CharacterTestRuntimeAiLabBehaviorRecipeTimeline first =
                CreateTimeline("timeline.a", 0, CharacterTestRuntimeAiLabCardStepKind.MoveToTarget, waitFrames: 8);
            first.TriggerEvents.Add(CreateLocalFrameEvent(
                "event.a.to-b",
                first.TimelineId,
                frame,
                "timeline.b",
                priority: 100));
            payload.Timelines.Add(first);
            payload.Timelines.Add(CreateTimeline("timeline.b", 1, CharacterTestRuntimeAiLabCardStepKind.Logic));
            return payload;
        }

        private static CharacterTestRuntimeAiLabBehaviorCardLogicPayload CreateThreeTimelineTieRecipe()
        {
            var payload = new CharacterTestRuntimeAiLabBehaviorCardLogicPayload
            {
                EntryTimelineId = "timeline.a"
            };
            CharacterTestRuntimeAiLabBehaviorRecipeTimeline first =
                CreateTimeline("timeline.a", 0, CharacterTestRuntimeAiLabCardStepKind.MoveToTarget, 8);
            first.TriggerEvents.Add(CreateLocalFrameEvent("event.b", first.TimelineId, 1, "timeline.c", 50));
            first.TriggerEvents.Add(CreateLocalFrameEvent("event.a", first.TimelineId, 1, "timeline.b", 50));
            payload.Timelines.Add(first);
            payload.Timelines.Add(CreateTimeline("timeline.b", 1, CharacterTestRuntimeAiLabCardStepKind.Logic));
            payload.Timelines.Add(CreateTimeline("timeline.c", 2, CharacterTestRuntimeAiLabCardStepKind.Logic));
            return payload;
        }

        private static CharacterTestRuntimeAiLabBehaviorCardLogicPayload CreateActionSignalRecipe()
        {
            var payload = new CharacterTestRuntimeAiLabBehaviorCardLogicPayload
            {
                EntryTimelineId = "timeline.action"
            };
            CharacterTestRuntimeAiLabBehaviorRecipeTimeline timeline =
                CreateTimeline("timeline.action", 0, CharacterTestRuntimeAiLabCardStepKind.CharacterAction);
            timeline.PrimaryActionEvent.CharacterActionId =
                CharacterTestCharacterActionConfigSource.LightAttackActionId;
            timeline.PrimaryActionEvent.FallbackDurationFrames = 12;
            timeline.TriggerEvents.Add(new CharacterTestRuntimeAiLabBehaviorRecipeTriggerEvent
            {
                EventId = "event.action.failed",
                TimelineId = timeline.TimelineId,
                Priority = 100,
                SourceKind = CharacterTestRuntimeAiLabBehaviorRecipeTriggerSourceKind.PrimaryActionSignal,
                ClockOrigin = CharacterTestRuntimeAiLabBehaviorRecipeClockOrigin.Timeline,
                WindowEndMode = CharacterTestRuntimeAiLabLogicDetectorWindowEndMode.Infinite,
                ActionSignal = CharacterTestRuntimeAiLabBehaviorRecipePrimaryActionSignal.Failed,
                ActionSignalReasonCode = MigrationFixtureIds.Id0002,
                OutcomeKind = CharacterTestRuntimeAiLabBehaviorRecipeOutcomeKind.ReturnToPlanner
            });
            payload.Timelines.Add(timeline);
            return payload;
        }

        private static CharacterTestRuntimeAiLabBehaviorCardLogicPayload CreateSelfLoopRecipe()
        {
            var payload = new CharacterTestRuntimeAiLabBehaviorCardLogicPayload
            {
                EntryTimelineId = "timeline.loop"
            };
            CharacterTestRuntimeAiLabBehaviorRecipeTimeline timeline =
                CreateTimeline("timeline.loop", 0, CharacterTestRuntimeAiLabCardStepKind.MoveToTarget, 5);
            timeline.TriggerEvents.Add(CreateLocalFrameEvent(
                "event.loop",
                timeline.TimelineId,
                1,
                timeline.TimelineId,
                10));
            payload.Timelines.Add(timeline);
            return payload;
        }

        private static CharacterTestRuntimeAiLabBehaviorRecipeTimeline CreateTimeline(
            string id,
            int order,
            CharacterTestRuntimeAiLabCardStepKind kind,
            int waitFrames = 0)
        {
            return new CharacterTestRuntimeAiLabBehaviorRecipeTimeline
            {
                TimelineId = id,
                Order = order,
                Title = id,
                EntryEvent = new CharacterTestRuntimeAiLabBehaviorRecipeEntryEvent
                {
                    EventId = "entry." + id
                },
                PrimaryActionEvent = new CharacterTestRuntimeAiLabBehaviorRecipePrimaryActionEvent
                {
                    EventId = "primary." + id,
                    Kind = kind,
                    WaitFrames = waitFrames,
                    FallbackDurationFrames = waitFrames
                }
            };
        }

        private static CharacterTestRuntimeAiLabBehaviorRecipeTriggerEvent CreateLocalFrameEvent(
            string id,
            string timelineId,
            int frame,
            string targetTimelineId,
            int priority)
        {
            return new CharacterTestRuntimeAiLabBehaviorRecipeTriggerEvent
            {
                EventId = id,
                TimelineId = timelineId,
                Priority = priority,
                SourceKind = CharacterTestRuntimeAiLabBehaviorRecipeTriggerSourceKind.LocalFrame,
                ClockOrigin = CharacterTestRuntimeAiLabBehaviorRecipeClockOrigin.Timeline,
                WindowEndMode = CharacterTestRuntimeAiLabLogicDetectorWindowEndMode.Infinite,
                LocalFrameAnchor = CharacterTestRuntimeAiLabBehaviorRecipeLocalFrameAnchor.AbsoluteFrame,
                LocalFrame = frame,
                OutcomeKind = CharacterTestRuntimeAiLabBehaviorRecipeOutcomeKind.EnterTimeline,
                TargetTimelineId = targetTimelineId
            };
        }

        private static CharacterTestRuntimeAiLabLogicStage CreateLegacyStage(
            string id,
            int order,
            CharacterTestRuntimeAiLabCardStepKind kind)
        {
            return new CharacterTestRuntimeAiLabLogicStage
            {
                StageId = id,
                Order = order,
                Title = id,
                Kind = kind,
                MovementTargetKind = CharacterTestRuntimeAiLabMovementTargetKind.CurrentCombatTarget,
                MoveMinDistance = 0f,
                MoveMaxDistance = 1.5f,
                DurationMode = CharacterTestRuntimeAiLabLogicDurationMode.UntilEvent
            };
        }

        private static CharacterTestRuntimeAiLabLogicDetector CreateLegacyPredicateDetector(
            string id,
            string stageId)
        {
            var detector = new CharacterTestRuntimeAiLabLogicDetector
            {
                DetectorId = id,
                StageId = stageId,
                Scope = CharacterTestRuntimeAiLabLogicDetectorScope.Stage,
                Trigger = CharacterTestRuntimeAiLabLogicDetectorTrigger.ConditionEdge,
                WindowEndMode = CharacterTestRuntimeAiLabLogicDetectorWindowEndMode.Infinite,
                Priority = 10
            };
            detector.Conditions.Add(new CharacterTestRuntimeAiLabCardCondition
            {
                ConditionId = id + ".condition",
                Fact = CharacterTestRuntimeAiLabCardConditionFact.TargetVisible,
                Operator = CharacterTestRuntimeAiLabCardConditionOperator.Equals,
                BoolValue = true
            });
            return detector;
        }

        private static CharacterTestRuntimeAiLabBehaviorRecipeTriggerEvent FindEvent(
            CharacterTestRuntimeAiLabBehaviorRecipeTimeline timeline,
            string eventId)
        {
            for (int i = 0; timeline != null && i < timeline.TriggerEvents.Count; i++)
            {
                if (string.Equals(timeline.TriggerEvents[i]?.EventId, eventId, StringComparison.Ordinal))
                    return timeline.TriggerEvents[i];
            }
            return null;
        }

        private static int Count(
            CharacterTestRuntimeAiLabLogicEventBuffer events,
            CharacterTestRuntimeAiLabLogicEventKind kind)
        {
            int count = 0;
            for (int i = 0; i < events.Count; i++)
            {
                if (events[i].Kind == kind)
                    count++;
            }
            return count;
        }

        private static bool ContainsDiagnostic(
            IReadOnlyList<CharacterTestRuntimeAiLabCardDiagnostic> diagnostics,
            string code)
        {
            for (int i = 0; diagnostics != null && i < diagnostics.Count; i++)
            {
                if (string.Equals(diagnostics[i].Code, code, StringComparison.Ordinal))
                    return true;
            }
            return false;
        }

        private static string FormatDiagnostics(
            IReadOnlyList<CharacterTestRuntimeAiLabCardDiagnostic> diagnostics)
        {
            var lines = new string[diagnostics?.Count ?? 0];
            for (int i = 0; i < lines.Length; i++)
                lines[i] = diagnostics[i].Code + " " + diagnostics[i].Path + " " + diagnostics[i].Message;
            return string.Join("\n", lines);
        }

        [Test]
        public void V6JsonRoundTrip_UsesOnlyUnifiedEvents()
        {
            CharacterTestRuntimeAiLabBehaviorCardLogicPayload source = CreateV6Recipe();

            string json = CharacterTestRuntimeAiLabBehaviorCardLogicJson.Serialize(source);
            CharacterTestRuntimeAiLabBehaviorCardLogicPayload restored =
                CharacterTestRuntimeAiLabBehaviorCardLogicJson.Deserialize(json);

            StringAssert.Contains("\"behaviorRecipeVersion\": "
                + CharacterTestRuntimeAiLabBehaviorCardLogicPayload.CurrentBehaviorRecipeVersion, json);
            StringAssert.Contains("\"timingKind\"", json);
            StringAssert.DoesNotContain("\"durationMode\"", json);
            StringAssert.Contains("\"events\"", json);
            StringAssert.DoesNotContain("\"entryEvent\"", json);
            StringAssert.DoesNotContain("\"primaryActionEvent\"", json);
            StringAssert.DoesNotContain("\"triggerEvents\"", json);
            Assert.AreEqual(CharacterTestRuntimeAiLabBehaviorCardLogicPayload.CurrentBehaviorRecipeVersion,
                restored.BehaviorRecipeVersion);
            Assert.AreEqual(1, restored.Timelines.Count);
            Assert.AreEqual(3, restored.Timelines[0].Events.Count);
            Assert.AreEqual("event.move", restored.Timelines[0].Events[0].EventId);
        }

        [Test]
        public void V6SemanticHash_CanonicalizesAllAndAnyChildren()
        {
            CharacterTestRuntimeAiLabBehaviorCardLogicPayload first = CreateV6Recipe();
            CharacterTestRuntimeAiLabBehaviorCardLogicPayload reordered = first.Clone();
            CharacterTestRuntimeAiLabBehaviorRecipeConditionNode root =
                reordered.Timelines[0].Events[1].ConditionRoot;
            CharacterTestRuntimeAiLabBehaviorRecipeConditionNode child = root.Children[0];
            root.Children.RemoveAt(0);
            root.Children.Add(child);
            reordered.Timelines[0].Events[1].Title = "仅展示文案";

            Assert.AreEqual(
                CharacterTestRuntimeAiLabBehaviorRecipeV6SemanticHash.ComputeV8(first),
                CharacterTestRuntimeAiLabBehaviorRecipeV6SemanticHash.ComputeV8(reordered));

            reordered.Timelines[0].Events[1].CooldownFrames++;
            Assert.AreNotEqual(
                CharacterTestRuntimeAiLabBehaviorRecipeV6SemanticHash.ComputeV8(first),
                CharacterTestRuntimeAiLabBehaviorRecipeV6SemanticHash.ComputeV8(reordered));
        }

        [Test]
        public void V6Evaluator_ZeroFrameUnconditionalEventStartsActorControl()
        {
            CharacterTestRuntimeAiLabBehaviorCardLogicPayload payload = CreateV6Recipe();
            CharacterTestRuntimeAiLabBehaviorCardLogicCompileResult compile =
                CompileReference(payload);
            Assert.IsTrue(compile.Success, FormatDiagnostics(compile.Diagnostics));
            var runtime = new RecipeRuntimeStub();
            runtime.SetBool(CharacterTestRuntimeAiLabCardConditionFact.TargetExists, true);
            runtime.SetBool(CharacterTestRuntimeAiLabCardConditionFact.MovePathAvailable, true);
            runtime.SetNumber(CharacterTestRuntimeAiLabCardConditionFact.TargetDistance, 10f);
            var evaluator = new CharacterTestBehaviorRecipeReferenceEvaluator(compile.Program, runtime);
            var events = new CharacterTestRuntimeAiLabLogicEventBuffer();

            Assert.IsTrue(evaluator.Start(0, events));
            int entryCount = runtime.EnterCount;
            evaluator.Tick(1, events);

            Assert.Greater(runtime.EnterCount, entryCount);
            Assert.AreEqual(0, evaluator.CurrentTimelineIndex);
            Assert.AreEqual(1, evaluator.CreateSnapshot().EventExecutionCounts[0]);
        }

        [Test]
        public void V6Compiler_RejectsInvalidTreeAndSelfSignal()
        {
            CharacterTestRuntimeAiLabBehaviorCardLogicPayload payload = CreateV6Recipe();
            CharacterTestRuntimeAiLabBehaviorRecipeEvent evt = payload.Timelines[0].Events[1];
            evt.ConditionRoot = new CharacterTestRuntimeAiLabBehaviorRecipeConditionNode
            {
                NodeId = "self",
                Kind = CharacterTestRuntimeAiLabBehaviorRecipeConditionNodeKind.EventSignal,
                SourceEventId = evt.EventId,
                Signal = CharacterTestRuntimeAiLabBehaviorRecipeEventSignal.Completed
            };

            CharacterTestRuntimeAiLabBehaviorCardLogicCompileResult compile =
                CompileReference(payload);

            Assert.IsFalse(compile.Success);
            Assert.IsTrue(ContainsDiagnostic(compile.Diagnostics, "CARD_V6_EVENT_SIGNAL_SELF"));
        }

        [Test]
        public void V6Evaluator_RepeatsOnlyAfterCooldownAndExecutionLimit()
        {
            CharacterTestRuntimeAiLabBehaviorCardLogicPayload payload = CreateV6Recipe();
            CharacterTestRuntimeAiLabBehaviorRecipeTimeline timeline = payload.Timelines[0];
            timeline.Events.Clear();
            timeline.Events.Add(new CharacterTestRuntimeAiLabBehaviorRecipeEvent
            {
                EventId = "event.repeat",
                TimelineId = timeline.TimelineId,
                StartFrame = 0,
                DurationFrames = 10,
                MaxExecutionsPerPass = 2,
                CooldownFrames = 3,
                Operation = new CharacterTestRuntimeAiLabBehaviorRecipeEventOperation
                {
                    Kind = CharacterTestRuntimeAiLabBehaviorRecipeEventOperationKind.MoveToTarget
                }
            });
            CharacterTestRuntimeAiLabBehaviorCardLogicCompileResult compile =
                CompileReference(payload);
            Assert.IsTrue(compile.Success, FormatDiagnostics(compile.Diagnostics));
            var runtime = new RecipeRuntimeStub();
            var evaluator = new CharacterTestBehaviorRecipeReferenceEvaluator(compile.Program, runtime);
            var events = new CharacterTestRuntimeAiLabLogicEventBuffer();

            Assert.IsTrue(evaluator.Start(0, events));
            evaluator.Tick(1, events);
            Assert.AreEqual(1, evaluator.CreateSnapshot().EventExecutionCounts[0]);
            runtime.PrimaryActionState = CharacterTestRuntimeAiLabLogicActionState.Completed;
            evaluator.Tick(2, events);
            evaluator.Tick(3, events);
            Assert.AreEqual(1, evaluator.CreateSnapshot().EventExecutionCounts[0]);
            evaluator.Tick(4, events);
            Assert.AreEqual(2, evaluator.CreateSnapshot().EventExecutionCounts[0]);
            runtime.PrimaryActionState = CharacterTestRuntimeAiLabLogicActionState.Completed;
            evaluator.Tick(5, events);
            evaluator.Tick(7, events);
            Assert.AreEqual(2, evaluator.CreateSnapshot().EventExecutionCounts[0]);
        }

        [Test]
        public void V6Evaluator_EventSignalUsesCurrentConfirmedState()
        {
            CharacterTestRuntimeAiLabBehaviorCardLogicPayload payload = CreateV6Recipe();
            CharacterTestRuntimeAiLabBehaviorRecipeTimeline timeline = payload.Timelines[0];
            timeline.Events.Clear();
            timeline.Events.Add(new CharacterTestRuntimeAiLabBehaviorRecipeEvent
            {
                EventId = "event.action",
                TimelineId = timeline.TimelineId,
                StartFrame = 0,
                DurationFrames = 10,
                MaxExecutionsPerPass = 1,
                Operation = new CharacterTestRuntimeAiLabBehaviorRecipeEventOperation
                {
                    Kind = CharacterTestRuntimeAiLabBehaviorRecipeEventOperationKind.MoveToTarget
                }
            });
            timeline.Events.Add(new CharacterTestRuntimeAiLabBehaviorRecipeEvent
            {
                EventId = "event.after",
                TimelineId = timeline.TimelineId,
                StartFrame = 0,
                DurationFrames = 10,
                MaxExecutionsPerPass = 1,
                Priority = 100,
                ConditionRoot = new CharacterTestRuntimeAiLabBehaviorRecipeConditionNode
                {
                    NodeId = "condition.after",
                    Kind = CharacterTestRuntimeAiLabBehaviorRecipeConditionNodeKind.EventSignal,
                    SourceEventId = "event.action",
                    Signal = CharacterTestRuntimeAiLabBehaviorRecipeEventSignal.Completed
                },
                Operation = new CharacterTestRuntimeAiLabBehaviorRecipeEventOperation
                {
                    Kind = CharacterTestRuntimeAiLabBehaviorRecipeEventOperationKind.ReturnToPlanner
                }
            });
            CharacterTestRuntimeAiLabBehaviorCardLogicCompileResult compile =
                CompileReference(payload);
            Assert.IsTrue(compile.Success, FormatDiagnostics(compile.Diagnostics));
            var runtime = new RecipeRuntimeStub();
            var evaluator = new CharacterTestBehaviorRecipeReferenceEvaluator(compile.Program, runtime);
            var events = new CharacterTestRuntimeAiLabLogicEventBuffer();

            Assert.IsTrue(evaluator.Start(0, events));
            evaluator.Tick(1, events);
            runtime.PrimaryActionState = CharacterTestRuntimeAiLabLogicActionState.Completed;
            evaluator.Tick(2, events);
            Assert.AreEqual(1, evaluator.CreateSnapshot().EventExecutionCounts[1],
                "Later conditions observe state already confirmed this Tick.");
            evaluator.Tick(3, events);
            Assert.AreEqual(1, evaluator.CreateSnapshot().EventExecutionCounts[1]);
        }

        [Test]
        public void V6Compiler_MirrorsMigratedRecipeClockEventIntoEveryTimelineWithoutDuplicatingIdentity()
        {
            CharacterTestRuntimeAiLabBehaviorCardLogicPayload payload = CreateV6Recipe();
            payload.Timelines.Add(new CharacterTestRuntimeAiLabBehaviorRecipeTimeline
            {
                TimelineId = "second",
                Order = 1,
                Title = "Second"
            });
            payload.LegacySharedEvents.Add(new CharacterTestRuntimeAiLabBehaviorRecipeEvent
            {
                EventId = "event.shared",
                TimelineId = "approach",
                StartFrame = 10,
                DurationFrames = 100,
                MaxExecutionsPerPass = 1,
                ClockOrigin = CharacterTestRuntimeAiLabBehaviorRecipeClockOrigin.Recipe,
                LegacySharedEventId = "shared.original",
                Operation = new CharacterTestRuntimeAiLabBehaviorRecipeEventOperation
                {
                    Kind = CharacterTestRuntimeAiLabBehaviorRecipeEventOperationKind.EnterTimeline,
                    TargetTimelineId = "second"
                }
            });
            CharacterTestRuntimeAiLabBehaviorCardLogicCompileResult compile =
                CompileReference(payload);

            Assert.IsTrue(compile.Success, FormatDiagnostics(compile.Diagnostics));
            Assert.AreEqual(4, compile.Program.RecipeV6.Events.Count);
            Assert.AreEqual(4, compile.Program.RecipeV6.Timelines[0].Events.Count);
            Assert.AreEqual(1, compile.Program.RecipeV6.Timelines[1].Events.Count);
            Assert.AreSame(
                compile.Program.RecipeV6.Timelines[0].Events[3],
                compile.Program.RecipeV6.Timelines[1].Events[0],
                "Shared mirrors must resolve to one compiled Event identity and one recipe-clock state.");
        }

        [Test]
        public void V7Migration_OpenIntervalRequiresExplicitDurationConfirmation()
        {
            CharacterTestRuntimeAiLabBehaviorCardLogicPayload payload = CreateV6Recipe();
            payload.BehaviorRecipeVersion = 6;
            CharacterTestRuntimeAiLabBehaviorRecipeEvent evt = payload.Timelines[0].Events[0];
            evt.DurationMode = CharacterTestRuntimeAiLabBehaviorRecipeEventDurationMode.UntilTimelineExit;
            evt.MaxExecutionsPerEntry = 2;

            CharacterTestRuntimeAiLabBehaviorRecipeV7Migration.SynchronizeForRead(payload);
            int sourceVersion = payload.BehaviorRecipeVersion;
            CharacterTestRuntimeAiLabBehaviorCardLogicCompileResult compile =
                CompileReference(payload);

            Assert.AreEqual(sourceVersion, payload.BehaviorRecipeVersion,
                "A rejected compile must not upgrade the author's input object.");
            Assert.IsTrue(evt.DurationReviewRequired);
            Assert.AreEqual(2, evt.MaxExecutionsPerPass);
            Assert.IsFalse(compile.Success);
            Assert.IsTrue(ContainsDiagnostic(compile.Diagnostics, "CARD_V7_EVENT_DURATION_REVIEW_REQUIRED"));
        }

        [Test]
        public void V7Evaluator_WaitPointFreezesOnlyLocalPlayheadUntilConditionMatches()
        {
            CharacterTestRuntimeAiLabBehaviorCardLogicPayload payload = CreateV6Recipe();
            CharacterTestRuntimeAiLabBehaviorRecipeTimeline timeline = payload.Timelines[0];
            timeline.Events.Clear();
            timeline.Events.Add(new CharacterTestRuntimeAiLabBehaviorRecipeEvent
            {
                EventId = "event.wait-condition",
                TimelineId = timeline.TimelineId,
                StartFrame = 1,
                DurationFrames = 1,
                ConditionRoot = new CharacterTestRuntimeAiLabBehaviorRecipeConditionNode
                {
                    NodeId = "condition.continue",
                    Kind = CharacterTestRuntimeAiLabBehaviorRecipeConditionNodeKind.FactPredicate,
                    Predicate = new CharacterTestRuntimeAiLabCardCondition
                    {
                        ConditionId = "continue",
                        Fact = CharacterTestRuntimeAiLabCardConditionFact.TargetExists,
                        Operator = CharacterTestRuntimeAiLabCardConditionOperator.Equals,
                        BoolValue = true
                    }
                },
                Operation = new CharacterTestRuntimeAiLabBehaviorRecipeEventOperation
                {
                    Kind = CharacterTestRuntimeAiLabBehaviorRecipeEventOperationKind.WaitForCondition
                }
            });
            CharacterTestRuntimeAiLabBehaviorCardLogicCompileResult compile =
                CompileReference(payload);
            Assert.IsTrue(compile.Success, FormatDiagnostics(compile.Diagnostics));
            var runtime = new RecipeRuntimeStub();
            runtime.SetBool(CharacterTestRuntimeAiLabCardConditionFact.TargetExists, false);
            var evaluator = new CharacterTestBehaviorRecipeReferenceEvaluator(compile.Program, runtime);
            var events = new CharacterTestRuntimeAiLabLogicEventBuffer();

            Assert.IsTrue(evaluator.Start(0, events));
            evaluator.Tick(1, events);
            Assert.AreEqual(1, evaluator.LocalFrame);
            evaluator.Tick(2, events);
            Assert.AreEqual(1, evaluator.LocalFrame);
            Assert.AreEqual(0, evaluator.CreateSnapshot().ActiveWaitEventIndex);
            runtime.SetBool(CharacterTestRuntimeAiLabCardConditionFact.TargetExists, true);
            evaluator.Tick(3, events);
            Assert.AreEqual(2, evaluator.LocalFrame);
            Assert.AreEqual(-1, evaluator.CreateSnapshot().ActiveWaitEventIndex);
        }

        [Test]
        public void V7Evaluator_LoopRegionRewindsAndResetsPassGeneration()
        {
            CharacterTestRuntimeAiLabBehaviorCardLogicPayload payload = CreateV6Recipe();
            CharacterTestRuntimeAiLabBehaviorRecipeTimeline timeline = payload.Timelines[0];
            timeline.Events.Clear();
            timeline.Events.Add(new CharacterTestRuntimeAiLabBehaviorRecipeEvent
            {
                EventId = "event.loop",
                TimelineId = timeline.TimelineId,
                StartFrame = 0,
                DurationFrames = 3,
                ConditionRoot = new CharacterTestRuntimeAiLabBehaviorRecipeConditionNode
                {
                    NodeId = "condition.exit",
                    Kind = CharacterTestRuntimeAiLabBehaviorRecipeConditionNodeKind.FactPredicate,
                    Predicate = new CharacterTestRuntimeAiLabCardCondition
                    {
                        ConditionId = "exit",
                        Fact = CharacterTestRuntimeAiLabCardConditionFact.TargetExists,
                        Operator = CharacterTestRuntimeAiLabCardConditionOperator.Equals,
                        BoolValue = true
                    }
                },
                Operation = new CharacterTestRuntimeAiLabBehaviorRecipeEventOperation
                {
                    Kind = CharacterTestRuntimeAiLabBehaviorRecipeEventOperationKind.LoopRegion,
                    MaxLoopRepeats = 0
                }
            });
            CharacterTestRuntimeAiLabBehaviorCardLogicCompileResult compile =
                CompileReference(payload);
            Assert.IsTrue(compile.Success, FormatDiagnostics(compile.Diagnostics));
            var runtime = new RecipeRuntimeStub();
            runtime.SetBool(CharacterTestRuntimeAiLabCardConditionFact.TargetExists, false);
            var evaluator = new CharacterTestBehaviorRecipeReferenceEvaluator(compile.Program, runtime);
            var events = new CharacterTestRuntimeAiLabLogicEventBuffer();

            Assert.IsTrue(evaluator.Start(0, events));
            evaluator.Tick(1, events);
            evaluator.Tick(2, events);
            evaluator.Tick(3, events);
            CharacterTestRuntimeAiLabLogicEvaluatorSnapshot rewound = evaluator.CreateSnapshot();
            Assert.AreEqual(0, rewound.LocalPlayheadFrame);
            Assert.AreEqual(1, rewound.TimelinePassGeneration);
            Assert.AreEqual(1, rewound.LoopRepeatCounts[0]);

            runtime.SetBool(CharacterTestRuntimeAiLabCardConditionFact.TargetExists, true);
            evaluator.Tick(4, events);
            evaluator.Tick(5, events);
            evaluator.Tick(6, events);
            Assert.AreEqual(3, evaluator.LocalFrame);
        }

        [Test]
        public void V7Evaluator_EventCrossingLoopBoundaryKeepsOwnershipAcrossRewindAndTail()
        {
            CharacterTestRuntimeAiLabBehaviorCardLogicPayload payload = CreateV6Recipe();
            CharacterTestRuntimeAiLabBehaviorRecipeTimeline timeline = payload.Timelines[0];
            timeline.Events.Clear();
            CharacterTestRuntimeAiLabBehaviorRecipeEvent loop =
                CreateLoopEvent("event.loop", timeline.TimelineId, 0, 3);
            loop.ConditionRoot = new CharacterTestRuntimeAiLabBehaviorRecipeConditionNode
            {
                NodeId = "condition.loop.exit",
                Kind = CharacterTestRuntimeAiLabBehaviorRecipeConditionNodeKind.FactPredicate,
                Predicate = new CharacterTestRuntimeAiLabCardCondition
                {
                    ConditionId = "loop.exit",
                    Fact = CharacterTestRuntimeAiLabCardConditionFact.TargetExists,
                    Operator = CharacterTestRuntimeAiLabCardConditionOperator.Equals,
                    BoolValue = true
                }
            };
            timeline.Events.Add(loop);
            timeline.Events.Add(new CharacterTestRuntimeAiLabBehaviorRecipeEvent
            {
                EventId = "event.crossing",
                TimelineId = timeline.TimelineId,
                StartFrame = 0,
                DurationFrames = 5,
                MaxExecutionsPerPass = 1,
                Operation = new CharacterTestRuntimeAiLabBehaviorRecipeEventOperation
                {
                    Kind = CharacterTestRuntimeAiLabBehaviorRecipeEventOperationKind.MoveToTarget
                }
            });
            CharacterTestRuntimeAiLabBehaviorCardLogicCompileResult compile =
                CompileReference(payload);
            Assert.IsTrue(compile.Success, FormatDiagnostics(compile.Diagnostics));
            var runtime = new RecipeRuntimeStub();
            runtime.SetBool(CharacterTestRuntimeAiLabCardConditionFact.TargetExists, false);
            var evaluator = new CharacterTestBehaviorRecipeReferenceEvaluator(compile.Program, runtime);
            var events = new CharacterTestRuntimeAiLabLogicEventBuffer();

            Assert.IsTrue(evaluator.Start(0, events));
            evaluator.Tick(1, events);
            evaluator.Tick(2, events);
            evaluator.Tick(3, events);

            CharacterTestRuntimeAiLabLogicEvaluatorSnapshot rewound = evaluator.CreateSnapshot();
            Assert.AreEqual(0, rewound.LocalPlayheadFrame);
            Assert.AreEqual(1, rewound.TimelinePassGeneration);
            Assert.GreaterOrEqual(rewound.ActorOwnerEventIndex, 0);
            Assert.AreEqual(1, rewound.EventExecutionCounts.Sum());
            Assert.AreEqual(0, runtime.JumpRequests);

            runtime.SetBool(CharacterTestRuntimeAiLabCardConditionFact.TargetExists, true);
            evaluator.Tick(4, events);
            evaluator.Tick(5, events);
            evaluator.Tick(6, events);

            CharacterTestRuntimeAiLabLogicEvaluatorSnapshot exitedLoop = evaluator.CreateSnapshot();
            Assert.AreEqual(3, exitedLoop.LocalPlayheadFrame);
            Assert.GreaterOrEqual(exitedLoop.ActorOwnerEventIndex, 0);
            Assert.AreEqual(1, exitedLoop.EventExecutionCounts.Sum());
            Assert.AreEqual(0, runtime.JumpRequests);
        }

        [Test]
        public void V7Evaluator_EmptyTimelineCompletesImmediately()
        {
            CharacterTestRuntimeAiLabBehaviorCardLogicPayload payload = CreateV6Recipe();
            payload.Timelines[0].Events.Clear();
            CharacterTestRuntimeAiLabBehaviorCardLogicCompileResult compile =
                CompileReference(payload);
            Assert.IsTrue(compile.Success, FormatDiagnostics(compile.Diagnostics));
            var runtime = new RecipeRuntimeStub();
            var evaluator = new CharacterTestBehaviorRecipeReferenceEvaluator(compile.Program, runtime);
            var events = new CharacterTestRuntimeAiLabLogicEventBuffer();

            Assert.IsTrue(evaluator.Start(0, events));
            evaluator.Tick(1, events);
            Assert.AreEqual(CharacterTestRuntimeAiLabLogicRuntimeState.Transitioning, evaluator.State);
            evaluator.Tick(2, events);
            Assert.AreEqual(CharacterTestRuntimeAiLabLogicRuntimeState.Completed, evaluator.State);
        }

        [Test]
        public void V7Compiler_RejectsOverlappingLoopRegions()
        {
            CharacterTestRuntimeAiLabBehaviorCardLogicPayload payload = CreateV6Recipe();
            CharacterTestRuntimeAiLabBehaviorRecipeTimeline timeline = payload.Timelines[0];
            timeline.Events.Clear();
            timeline.Events.Add(CreateLoopEvent("event.loop-a", timeline.TimelineId, 0, 10));
            timeline.Events.Add(CreateLoopEvent("event.loop-b", timeline.TimelineId, 8, 10));

            CharacterTestRuntimeAiLabBehaviorCardLogicCompileResult compile =
                CompileReference(payload);

            Assert.IsFalse(compile.Success);
            Assert.IsTrue(ContainsDiagnostic(compile.Diagnostics, "CARD_V7_LOOP_OVERLAP"));
        }

        [Test]
        public void V7Compiler_AllowsEventCrossingLoopBoundary()
        {
            CharacterTestRuntimeAiLabBehaviorCardLogicPayload payload = CreateV6Recipe();
            CharacterTestRuntimeAiLabBehaviorRecipeTimeline timeline = payload.Timelines[0];
            timeline.Events.Clear();
            timeline.Events.Add(CreateLoopEvent("event.loop", timeline.TimelineId, 0, 10));
            timeline.Events.Add(new CharacterTestRuntimeAiLabBehaviorRecipeEvent
            {
                EventId = "event.crossing",
                TimelineId = timeline.TimelineId,
                StartFrame = 5,
                DurationFrames = 10,
                MaxExecutionsPerPass = 1,
                Operation = new CharacterTestRuntimeAiLabBehaviorRecipeEventOperation
                {
                    Kind = CharacterTestRuntimeAiLabBehaviorRecipeEventOperationKind.MoveToTarget
                }
            });

            CharacterTestRuntimeAiLabBehaviorCardLogicCompileResult compile =
                CompileReference(payload);

            Assert.IsTrue(compile.Success, FormatDiagnostics(compile.Diagnostics));
        }

        [Test]
        public void V7Compiler_RejectsTwoWaitPointsAtTheSameFrame()
        {
            CharacterTestRuntimeAiLabBehaviorCardLogicPayload payload = CreateV6Recipe();
            CharacterTestRuntimeAiLabBehaviorRecipeTimeline timeline = payload.Timelines[0];
            timeline.Events.Clear();
            timeline.Events.Add(CreateWaitEvent("event.wait-a", timeline.TimelineId, 4));
            timeline.Events.Add(CreateWaitEvent("event.wait-b", timeline.TimelineId, 4));

            CharacterTestRuntimeAiLabBehaviorCardLogicCompileResult compile =
                CompileReference(payload);

            Assert.IsFalse(compile.Success);
            Assert.IsTrue(ContainsDiagnostic(compile.Diagnostics, "CARD_V7_WAIT_FRAME_CONFLICT"));
        }

        [Test]
        public void V8JsonRoundTrip_PreservesExplicitTimingKind()
        {
            CharacterTestRuntimeAiLabBehaviorCardLogicPayload payload = CreateV6Recipe();
            CharacterTestRuntimeAiLabBehaviorRecipeEvent instant = payload.Timelines[0].Events[1];
            instant.TimingKind = CharacterTestRuntimeAiLabBehaviorRecipeEventTimingKind.Instant;
            instant.DurationFrames = 1;
            instant.MaxExecutionsPerPass = 1;
            instant.CooldownFrames = 0;

            string json = CharacterTestRuntimeAiLabBehaviorCardLogicJson.Serialize(payload);
            CharacterTestRuntimeAiLabBehaviorCardLogicPayload restored =
                CharacterTestRuntimeAiLabBehaviorCardLogicJson.Deserialize(json);

            StringAssert.Contains("\"behaviorRecipeVersion\": "
                + CharacterTestRuntimeAiLabBehaviorCardLogicPayload.CurrentBehaviorRecipeVersion, json);
            StringAssert.Contains("\"timingKind\": 0", json);
            Assert.AreEqual(CharacterTestRuntimeAiLabBehaviorCardLogicPayload.CurrentBehaviorRecipeVersion,
                restored.BehaviorRecipeVersion);
            Assert.AreEqual(
                CharacterTestRuntimeAiLabBehaviorRecipeEventTimingKind.Instant,
                restored.Timelines[0].Events[1].TimingKind);
        }

        [Test]
        public void V8SemanticHash_DistinguishesInstantAndOneFrameDuration()
        {
            CharacterTestRuntimeAiLabBehaviorCardLogicPayload instant = CreateV6Recipe();
            CharacterTestRuntimeAiLabBehaviorRecipeEvent instantEvent = instant.Timelines[0].Events[1];
            instantEvent.TimingKind = CharacterTestRuntimeAiLabBehaviorRecipeEventTimingKind.Instant;
            instantEvent.DurationFrames = 1;
            instantEvent.MaxExecutionsPerPass = 1;
            instantEvent.CooldownFrames = 0;
            CharacterTestRuntimeAiLabBehaviorCardLogicPayload duration = instant.Clone();
            duration.Timelines[0].Events[1].TimingKind =
                CharacterTestRuntimeAiLabBehaviorRecipeEventTimingKind.Duration;

            Assert.AreNotEqual(
                CharacterTestRuntimeAiLabBehaviorRecipeV6SemanticHash.ComputeV8(instant),
                CharacterTestRuntimeAiLabBehaviorRecipeV6SemanticHash.ComputeV8(duration));
        }

        [Test]
        public void V8MoveToTarget_SpeedMultiplierIsEditableCompiledAndHashed()
        {
            CharacterTestRuntimeAiLabBehaviorCardLogicPayload payload = CreateV6Recipe();
            CharacterTestRuntimeAiLabBehaviorRecipeEvent moveEvent = payload.Timelines[0].Events[0];
            moveEvent.Operation.Kind = CharacterTestRuntimeAiLabBehaviorRecipeEventOperationKind.MoveToTarget;
            moveEvent.Operation.MovementTargetKind =
                CharacterTestRuntimeAiLabMovementTargetKind.CurrentCombatTarget;
            moveEvent.Operation.MoveSpeedMultiplier = 1f;
            long fullSpeedHash = CharacterTestRuntimeAiLabBehaviorRecipeV6SemanticHash.ComputeV8(payload);

            Assert.IsTrue(CharacterTestRuntimeAiLabBehaviorRecipeTriggerEventMutation.TryApply(
                payload,
                payload.Timelines[0].TimelineId,
                moveEvent.EventId,
                CharacterTestRuntimeAiLabBehaviorRecipeTriggerEventField.OperationMoveSpeedMultiplier,
                -1,
                new CharacterTestRuntimeAiLabBehaviorRecipeTriggerEventFieldValue(floatValue: 0.5f),
                out bool changed,
                out string diagnostic), diagnostic);
            Assert.IsTrue(changed);
            Assert.AreEqual(0.5f, moveEvent.Operation.MoveSpeedMultiplier, 0.0001f);
            Assert.AreNotEqual(fullSpeedHash,
                CharacterTestRuntimeAiLabBehaviorRecipeV6SemanticHash.ComputeV8(payload));

            CharacterTestRuntimeAiLabBehaviorCardLogicCompileResult compile =
                CompileReference(payload);
            Assert.IsTrue(compile.Success, FormatDiagnostics(compile.Diagnostics));
            Assert.AreEqual(0.5f, compile.Program.RecipeV6.Events[0].ActionStage.MoveSpeedMultiplier, 0.0001f);
        }

        [Test]
        public void V8ApproachTarget_IgnoresLegacyStopDistanceAndLeavesExitToConditions()
        {
            CharacterTestRuntimeAiLabBehaviorCardLogicPayload payload = CreateV6Recipe();
            CharacterTestRuntimeAiLabBehaviorRecipeEvent moveEvent = payload.Timelines[0].Events[0];
            moveEvent.Operation.Kind = CharacterTestRuntimeAiLabBehaviorRecipeEventOperationKind.MoveToTarget;
            moveEvent.Operation.MovementStrategy = CharacterTestRuntimeAiLabMovementStrategy.ApproachTarget;
            moveEvent.Operation.MovementTargetKind =
                CharacterTestRuntimeAiLabMovementTargetKind.CurrentCombatTarget;
            moveEvent.Operation.MoveMinDistance = 1.5f;
            moveEvent.Operation.MoveMaxDistance = 3f;

            CharacterTestRuntimeAiLabBehaviorCardLogicCompileResult compile =
                CompileReference(payload);

            Assert.IsTrue(compile.Success, FormatDiagnostics(compile.Diagnostics));
            CharacterTestRuntimeAiLabCompiledLogicStage actionStage =
                compile.Program.RecipeV6.Events[0].ActionStage;
            Assert.AreEqual(0f, actionStage.MoveMinDistance);
            Assert.AreEqual(0f, actionStage.MoveMaxDistance);
        }

        [Test]
        public void V8MoveToTarget_SpeedMultiplierRejectsValuesOutsideSupportedRange()
        {
            CharacterTestRuntimeAiLabBehaviorCardLogicPayload payload = CreateV6Recipe();
            CharacterTestRuntimeAiLabBehaviorRecipeEvent moveEvent = payload.Timelines[0].Events[0];
            moveEvent.Operation.Kind = CharacterTestRuntimeAiLabBehaviorRecipeEventOperationKind.MoveToTarget;

            Assert.IsFalse(CharacterTestRuntimeAiLabBehaviorRecipeTriggerEventMutation.TryApply(
                payload,
                payload.Timelines[0].TimelineId,
                moveEvent.EventId,
                CharacterTestRuntimeAiLabBehaviorRecipeTriggerEventField.OperationMoveSpeedMultiplier,
                -1,
                new CharacterTestRuntimeAiLabBehaviorRecipeTriggerEventFieldValue(floatValue: 1.1f),
                out bool changed,
                out string diagnostic));
            Assert.IsFalse(changed);
            StringAssert.Contains("0.1", diagnostic);
        }

        [Test]
        public void V8Migration_UsesOperationSpecificTimingWithoutAddingEvents()
        {
            CharacterTestRuntimeAiLabBehaviorCardLogicPayload payload = CreateV6Recipe();
            payload.BehaviorRecipeVersion = 7;
            CharacterTestRuntimeAiLabBehaviorRecipeTimeline timeline = payload.Timelines[0];
            timeline.Events.Clear();
            timeline.Events.Add(CreateTimingMigrationEvent(
                "move",
                CharacterTestRuntimeAiLabBehaviorRecipeEventOperationKind.MoveToTarget,
                1));
            timeline.Events.Add(CreateTimingMigrationEvent(
                "return.instant",
                CharacterTestRuntimeAiLabBehaviorRecipeEventOperationKind.ReturnToPlanner,
                1));
            timeline.Events.Add(CreateTimingMigrationEvent(
                "return.duration",
                CharacterTestRuntimeAiLabBehaviorRecipeEventOperationKind.ReturnToPlanner,
                2));
            timeline.Events.Add(CreateTimingMigrationEvent(
                "wait",
                CharacterTestRuntimeAiLabBehaviorRecipeEventOperationKind.WaitForCondition,
                8));
            timeline.Events.Add(CreateTimingMigrationEvent(
                "loop",
                CharacterTestRuntimeAiLabBehaviorRecipeEventOperationKind.LoopRegion,
                4));

            CharacterTestRuntimeAiLabBehaviorRecipeV8Migration.SynchronizeForRead(payload);

            Assert.AreEqual(9, payload.BehaviorRecipeVersion,
                "The v8/v9 compatibility migrator must not impersonate the v10 SubjectScope migration.");
            Assert.AreEqual(5, timeline.Events.Count);
            Assert.AreEqual(CharacterTestRuntimeAiLabBehaviorRecipeEventTimingKind.Duration, timeline.Events[0].TimingKind);
            Assert.AreEqual(CharacterTestRuntimeAiLabBehaviorRecipeEventTimingKind.Instant, timeline.Events[1].TimingKind);
            Assert.AreEqual(CharacterTestRuntimeAiLabBehaviorRecipeEventTimingKind.Duration, timeline.Events[2].TimingKind);
            Assert.AreEqual(CharacterTestRuntimeAiLabBehaviorRecipeEventTimingKind.Instant, timeline.Events[3].TimingKind);
            Assert.AreEqual(1, timeline.Events[3].DurationFrames);
            Assert.AreEqual(CharacterTestRuntimeAiLabBehaviorRecipeEventTimingKind.Duration, timeline.Events[4].TimingKind);
        }

        [Test]
        public void V8Evaluator_InstantActorOperationWaitsAtTimelineEndUntilCompletion()
        {
            CharacterTestRuntimeAiLabBehaviorCardLogicPayload payload = CreateV6Recipe();
            CharacterTestRuntimeAiLabBehaviorRecipeTimeline timeline = payload.Timelines[0];
            timeline.Events.Clear();
            timeline.Events.Add(new CharacterTestRuntimeAiLabBehaviorRecipeEvent
            {
                EventId = "event.instant.move",
                TimelineId = timeline.TimelineId,
                TimingKind = CharacterTestRuntimeAiLabBehaviorRecipeEventTimingKind.Instant,
                StartFrame = 0,
                DurationFrames = 1,
                MaxExecutionsPerPass = 1,
                Operation = new CharacterTestRuntimeAiLabBehaviorRecipeEventOperation
                {
                    Kind = CharacterTestRuntimeAiLabBehaviorRecipeEventOperationKind.MoveToTarget,
                    MovementTargetKind = CharacterTestRuntimeAiLabMovementTargetKind.CurrentCombatTarget
                }
            });
            CharacterTestRuntimeAiLabBehaviorCardLogicCompileResult compile =
                CompileReference(payload);
            Assert.IsTrue(compile.Success, FormatDiagnostics(compile.Diagnostics));
            var runtime = new RecipeRuntimeStub();
            var evaluator = new CharacterTestBehaviorRecipeReferenceEvaluator(compile.Program, runtime);
            var events = new CharacterTestRuntimeAiLabLogicEventBuffer();

            Assert.IsTrue(evaluator.Start(0, events));
            evaluator.Tick(1, events);
            evaluator.Tick(2, events);
            evaluator.Tick(3, events);

            CharacterTestRuntimeAiLabLogicEvaluatorSnapshot waiting = evaluator.CreateSnapshot();
            Assert.AreEqual(1, waiting.LocalPlayheadFrame);
            Assert.IsTrue(waiting.WaitingForInstantOwnerAtTimelineEnd);
            Assert.AreEqual(CharacterTestRuntimeAiLabLogicRuntimeState.Running, evaluator.State);

            runtime.PrimaryActionState = CharacterTestRuntimeAiLabLogicActionState.Completed;
            evaluator.Tick(4, events);
            evaluator.Tick(5, events);
            Assert.AreEqual(CharacterTestRuntimeAiLabLogicRuntimeState.Completed, evaluator.State);
        }

        [Test]
        public void V8Evaluator_OneFrameDurationCancelsActorAtHardRightEdge()
        {
            CharacterTestRuntimeAiLabBehaviorCardLogicPayload payload = CreateV6Recipe();
            CharacterTestRuntimeAiLabBehaviorRecipeTimeline timeline = payload.Timelines[0];
            timeline.Events.Clear();
            timeline.Events.Add(new CharacterTestRuntimeAiLabBehaviorRecipeEvent
            {
                EventId = "event.duration.move",
                TimelineId = timeline.TimelineId,
                TimingKind = CharacterTestRuntimeAiLabBehaviorRecipeEventTimingKind.Duration,
                StartFrame = 0,
                DurationFrames = 1,
                MaxExecutionsPerPass = 1,
                Operation = new CharacterTestRuntimeAiLabBehaviorRecipeEventOperation
                {
                    Kind = CharacterTestRuntimeAiLabBehaviorRecipeEventOperationKind.MoveToTarget,
                    MovementTargetKind = CharacterTestRuntimeAiLabMovementTargetKind.CurrentCombatTarget
                }
            });
            CharacterTestRuntimeAiLabBehaviorCardLogicCompileResult compile =
                CompileReference(payload);
            Assert.IsTrue(compile.Success, FormatDiagnostics(compile.Diagnostics));
            var runtime = new RecipeRuntimeStub();
            var evaluator = new CharacterTestBehaviorRecipeReferenceEvaluator(compile.Program, runtime);
            var events = new CharacterTestRuntimeAiLabLogicEventBuffer();

            Assert.IsTrue(evaluator.Start(0, events));
            evaluator.Tick(1, events);
            evaluator.Tick(2, events);
            evaluator.Tick(3, events);
            evaluator.Tick(4, events);

            CharacterTestRuntimeAiLabLogicEvaluatorSnapshot cancelled = evaluator.CreateSnapshot();
            Assert.AreEqual(-1, cancelled.ActorOwnerEventIndex);
            Assert.AreEqual(
                (int)CharacterTestRuntimeAiLabBehaviorRecipeEventRuntimeResult.Cancelled,
                cancelled.EventResults[0]);
            Assert.IsFalse(cancelled.WaitingForInstantOwnerAtTimelineEnd);
        }

        [Test]
        public void V8Compiler_CharacterActionEventUsesAuthoritativeDurationAndCanonicalShape()
        {
            CharacterTestCharacterActionRuntime actionRuntime = CharacterTestCharacterActionConfigSource.Load();
            Assert.IsTrue(actionRuntime.Registry.TryGetAction(
                CharacterTestCharacterActionConfigSource.LightAttackActionId,
                out CharacterActionConfig action));
            Assert.IsTrue(action.DurationFrames.HasValue && action.DurationFrames.Value > 0);
            var references = new CharacterTestRuntimeAiLabBehaviorCardReferenceCatalog(
                CharacterTestAbilityCatalogSource.CreateRegistry(),
                actionRuntime.Registry);
            CharacterTestRuntimeAiLabBehaviorCardLogicPayload payload = CreateV6Recipe();
            CharacterTestRuntimeAiLabBehaviorRecipeTimeline timeline = payload.Timelines[0];
            timeline.Events.Clear();
            timeline.Events.Add(new CharacterTestRuntimeAiLabBehaviorRecipeEvent
            {
                EventId = "event.action.light-attack",
                TimelineId = timeline.TimelineId,
                TimingKind = CharacterTestRuntimeAiLabBehaviorRecipeEventTimingKind.Instant,
                StartFrame = 5,
                DurationFrames = 1,
                MaxExecutionsPerPass = 0,
                CooldownFrames = 99,
                Operation = new CharacterTestRuntimeAiLabBehaviorRecipeEventOperation
                {
                    Kind = CharacterTestRuntimeAiLabBehaviorRecipeEventOperationKind.CharacterAction,
                    CharacterActionId = CharacterTestCharacterActionConfigSource.LightAttackActionId
                }
            });

            CharacterTestRuntimeAiLabBehaviorCardLogicCompileResult compile =
                CompileReference(payload, references);

            Assert.IsTrue(compile.Success, FormatDiagnostics(compile.Diagnostics));
            CharacterTestRuntimeAiLabBehaviorRecipeEvent source = timeline.Events[0];
            Assert.AreEqual(CharacterTestRuntimeAiLabBehaviorRecipeEventTimingKind.Instant, source.TimingKind,
                "Compilation normalizes its private copy and preserves the authoring input.");
            Assert.AreEqual(1, source.DurationFrames);
            Assert.AreEqual(0, source.MaxExecutionsPerPass);
            Assert.AreEqual(99, source.CooldownFrames);
            Assert.AreEqual(CharacterTestRuntimeAiLabBehaviorRecipeEventTimingKind.Duration, compile.Program.RecipeV6.Events[0].TimingKind);
            Assert.AreEqual(1, compile.Program.RecipeV6.Events[0].MaxExecutionsPerPass);
            Assert.AreEqual(0, compile.Program.RecipeV6.Events[0].CooldownFrames);
            Assert.AreEqual(action.DurationFrames.Value, compile.Program.RecipeV6.Events[0].DurationFrames);
        }

        [Test]
        public void V8Evaluator_LaterCharacterActionAlwaysOverwritesPlayingActionAndCompletesPreviousEvent()
        {
            CharacterTestCharacterActionRuntime actionRuntime = CharacterTestCharacterActionConfigSource.Load();
            Assert.IsTrue(actionRuntime.Registry.TryGetAction(
                CharacterTestCharacterActionConfigSource.LightAttackActionId,
                out CharacterActionConfig action));
            Assert.IsTrue(action.DurationFrames.HasValue && action.DurationFrames.Value > 1);
            var references = new CharacterTestRuntimeAiLabBehaviorCardReferenceCatalog(
                CharacterTestAbilityCatalogSource.CreateRegistry(),
                actionRuntime.Registry);
            CharacterTestRuntimeAiLabBehaviorCardLogicPayload payload = CreateV6Recipe();
            CharacterTestRuntimeAiLabBehaviorRecipeTimeline timeline = payload.Timelines[0];
            timeline.Events.Clear();
            timeline.Events.Add(CreateCharacterActionEvent(
                "event.action.first",
                timeline.TimelineId,
                CharacterTestCharacterActionConfigSource.LightAttackActionId.Value.ToString(),
                startFrame: 0,
                durationFrames: action.DurationFrames.Value,
                priority: 1000));
            timeline.Events.Add(CreateCharacterActionEvent(
                "event.action.second",
                timeline.TimelineId,
                CharacterTestCharacterActionConfigSource.LightAttackActionId.Value.ToString(),
                startFrame: 1,
                durationFrames: action.DurationFrames.Value,
                priority: -1000));

            CharacterTestRuntimeAiLabBehaviorCardLogicCompileResult compile =
                CompileReference(payload, references);
            Assert.IsTrue(compile.Success, FormatDiagnostics(compile.Diagnostics));
            var runtime = new RecipeRuntimeStub();
            var evaluator = new CharacterTestBehaviorRecipeReferenceEvaluator(compile.Program, runtime);
            var events = new CharacterTestRuntimeAiLabLogicEventBuffer();

            Assert.IsTrue(evaluator.Start(0, events));
            evaluator.Tick(1, events);
            CharacterTestRuntimeAiLabLogicEvaluatorSnapshot firstRunning = evaluator.CreateSnapshot();
            Assert.AreEqual(0, firstRunning.ActorOwnerEventIndex);
            Assert.AreEqual(
                (int)CharacterTestRuntimeAiLabBehaviorRecipeEventRuntimeResult.Running,
                firstRunning.EventResults[0]);

            evaluator.Tick(2, events);
            Assert.AreEqual(1, runtime.JumpRequests,
                "The later action clip must request a forced replacement even with lower priority.");
            evaluator.Tick(3, events);

            CharacterTestRuntimeAiLabLogicEvaluatorSnapshot replaced = evaluator.CreateSnapshot();
            Assert.AreEqual(1, replaced.ActorOwnerEventIndex);
            Assert.AreEqual(
                (int)CharacterTestRuntimeAiLabBehaviorRecipeEventRuntimeResult.Completed,
                replaced.EventResults[0],
                "A timeline action overwrite exits the previous action as completed behavior flow.");
            Assert.AreEqual(
                (int)CharacterTestRuntimeAiLabBehaviorRecipeEventRuntimeResult.Running,
                replaced.EventResults[1]);
        }

        [Test]
        public void V8Compiler_AcceptsCharacterActionEventWithEntryCondition()
        {
            CharacterTestCharacterActionRuntime actionRuntime = CharacterTestCharacterActionConfigSource.Load();
            var references = new CharacterTestRuntimeAiLabBehaviorCardReferenceCatalog(
                CharacterTestAbilityCatalogSource.CreateRegistry(),
                actionRuntime.Registry);
            CharacterTestRuntimeAiLabBehaviorCardLogicPayload payload = CreateV6Recipe();
            CharacterTestRuntimeAiLabBehaviorRecipeTimeline timeline = payload.Timelines[0];
            timeline.Events.Clear();
            timeline.Events.Add(new CharacterTestRuntimeAiLabBehaviorRecipeEvent
            {
                EventId = "event.action.conditional",
                TimelineId = timeline.TimelineId,
                StartFrame = 0,
                DurationFrames = 1,
                ConditionRoot = new CharacterTestRuntimeAiLabBehaviorRecipeConditionNode
                {
                    NodeId = "condition.target",
                    Kind = CharacterTestRuntimeAiLabBehaviorRecipeConditionNodeKind.FactPredicate,
                    Predicate = new CharacterTestRuntimeAiLabCardCondition
                    {
                        ConditionId = "condition.target",
                        Fact = CharacterTestRuntimeAiLabCardConditionFact.TargetExists,
                        Operator = CharacterTestRuntimeAiLabCardConditionOperator.Equals,
                        BoolValue = true
                    }
                },
                Operation = new CharacterTestRuntimeAiLabBehaviorRecipeEventOperation
                {
                    Kind = CharacterTestRuntimeAiLabBehaviorRecipeEventOperationKind.CharacterAction,
                    CharacterActionId = CharacterTestCharacterActionConfigSource.LightAttackActionId
                }
            });

            CharacterTestRuntimeAiLabBehaviorCardLogicCompileResult compile =
                CompileReference(payload, references);

            Assert.IsTrue(compile.Success, FormatDiagnostics(compile.Diagnostics));
        }

        [Test]
        public void V8Evaluator_CharacterActionEventStartsOnlyAtItsLeftEdge()
        {
            CharacterTestRuntimeAiLabBehaviorCardLogicPayload payload = CreateV6Recipe();
            CharacterTestRuntimeAiLabBehaviorRecipeTimeline timeline = payload.Timelines[0];
            timeline.Events.Clear();
            timeline.Events.Add(new CharacterTestRuntimeAiLabBehaviorRecipeEvent
            {
                EventId = "event.action.once",
                TimelineId = timeline.TimelineId,
                TimingKind = CharacterTestRuntimeAiLabBehaviorRecipeEventTimingKind.Duration,
                StartFrame = 2,
                DurationFrames = 6,
                MaxExecutionsPerPass = 1,
                CooldownFrames = 0,
                Operation = new CharacterTestRuntimeAiLabBehaviorRecipeEventOperation
                {
                    Kind = CharacterTestRuntimeAiLabBehaviorRecipeEventOperationKind.CharacterAction,
                    CharacterActionId = MxFramework.Tests.CharacterActionLegacyFixtureIds.From("action.test")
                }
            });
            CharacterTestRuntimeAiLabBehaviorCardLogicCompileResult compile =
                CompileReference(payload);
            Assert.IsTrue(compile.Success, FormatDiagnostics(compile.Diagnostics));
            var runtime = new RecipeRuntimeStub();
            var evaluator = new CharacterTestBehaviorRecipeReferenceEvaluator(compile.Program, runtime);
            var events = new CharacterTestRuntimeAiLabLogicEventBuffer();

            Assert.IsTrue(evaluator.Start(0, events));
            Assert.AreEqual(1, runtime.EnterCount, "Entering the Timeline installs its header stage once.");
            evaluator.Tick(1, events);
            evaluator.Tick(2, events);
            Assert.AreEqual(1, runtime.EnterCount, "Frames before the left edge must not start the action.");
            evaluator.Tick(3, events);
            Assert.AreEqual(2, runtime.EnterCount, "The CharacterAction starts at its left edge.");
            evaluator.Tick(4, events);
            evaluator.Tick(5, events);

            Assert.AreEqual(2, runtime.EnterCount, "The duration body must not restart the CharacterAction.");
            Assert.AreEqual(CharacterTestRuntimeAiLabLogicRuntimeState.Running, evaluator.State);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void TimelineSampling_LastEligibleActionWinsWithoutIntermediateStarts(bool gateBlocked)
        {
            var payload = CreateV6Recipe();
            var timeline = payload.Timelines[0];
            timeline.Events.Clear();
            timeline.Events.Add(CreateCharacterActionEvent("event.a", timeline.TimelineId, "action.test", 0, 5, 100));
            timeline.Events.Add(CreateCharacterActionEvent("event.b", timeline.TimelineId, "action.test", 0, 5, 0));
            if (gateBlocked)
            {
                var gate = CreateWaitEvent("event.gate", timeline.TimelineId, 0);
                gate.ConditionRoot = SamplingCondition("gate");
                timeline.Events.Add(gate);
            }
            var compile = CompileReference(payload);
            Assert.IsTrue(compile.Success, FormatDiagnostics(compile.Diagnostics));
            var runtime = new RecipeRuntimeStub();
            var evaluator = new CharacterTestBehaviorRecipeReferenceEvaluator(compile.Program, runtime);
            var events = new CharacterTestRuntimeAiLabLogicEventBuffer();
            Assert.IsTrue(evaluator.Start(0, events));
            evaluator.Tick(1, events);
            CharacterTestRuntimeAiLabLogicEvaluatorSnapshot snapshot = evaluator.CreateSnapshot();
            Assert.AreEqual(
                compile.Program.RecipeV6.Events.Single(item => item.EventId == "event.b").Index,
                snapshot.LastTriggeredEventIndex);
            Assert.AreEqual(2, runtime.EnterCount, "Only the header and final selected action start.");
            evaluator.Tick(2, events);
            Assert.AreEqual(2, runtime.EnterCount, "A held start frame cannot restart the action.");
        }

        [Test]
        public void TimelineSampling_FailedEntryDoesNotRetryAfterSnapshotRestoreAtGate()
        {
            var payload = CreateV6Recipe();
            var timeline = payload.Timelines[0];
            timeline.Events.Clear();
            var action = CreateCharacterActionEvent("event.action", timeline.TimelineId, "action.test", 0, 5, 0);
            action.ConditionRoot = SamplingCondition("action");
            timeline.Events.Add(action);
            var gate = CreateWaitEvent("event.gate", timeline.TimelineId, 0);
            gate.ConditionRoot = SamplingCondition("gate");
            timeline.Events.Add(gate);
            var compile = CompileReference(payload);
            Assert.IsTrue(compile.Success, FormatDiagnostics(compile.Diagnostics));
            var runtime = new RecipeRuntimeStub();
            var evaluator = new CharacterTestBehaviorRecipeReferenceEvaluator(compile.Program, runtime);
            var events = new CharacterTestRuntimeAiLabLogicEventBuffer();
            Assert.IsTrue(evaluator.Start(0, events));
            evaluator.Tick(1, events);
            var snapshot = evaluator.CreateSnapshot();
            Assert.AreEqual(0, snapshot.LastSampledPlayheadFrame);
            var restored = new CharacterTestBehaviorRecipeReferenceEvaluator(compile.Program, runtime);
            Assert.IsTrue(restored.TryRestoreSnapshot(snapshot));
            runtime.SetBool(CharacterTestRuntimeAiLabCardConditionFact.TargetExists, true);
            restored.Tick(2, events);
            Assert.AreEqual(1, runtime.EnterCount, "A failed entry is consumed even when conditions later change.");
        }

        [TestCase(1, 1)]
        [TestCase(2, 2)]
        public void TimelineSampling_LoopRequiresActualPositionChangeToRestart(int loopLength, int expectedStarts)
        {
            var payload = CreateV6Recipe();
            var timeline = payload.Timelines[0];
            timeline.Events.Clear();
            timeline.Events.Add(CreateCharacterActionEvent("event.action", timeline.TimelineId, "action.test", 0, loopLength, 0));
            timeline.Events.Add(CreateLoopEvent("event.loop", timeline.TimelineId, 0, loopLength));
            var compile = CompileReference(payload);
            Assert.IsTrue(compile.Success, FormatDiagnostics(compile.Diagnostics));
            var runtime = new RecipeRuntimeStub();
            var evaluator = new CharacterTestBehaviorRecipeReferenceEvaluator(compile.Program, runtime);
            var events = new CharacterTestRuntimeAiLabLogicEventBuffer();
            Assert.IsTrue(evaluator.Start(0, events));
            evaluator.Tick(1, events);
            evaluator.Tick(2, events);
            evaluator.Tick(3, events);
            Assert.AreEqual(expectedStarts - 1, runtime.JumpRequests);
        }

        [Test]
        public void TimelineSampling_ReturnDiscardsEarlierActionCandidate()
        {
            var payload = CreateV6Recipe();
            var timeline = payload.Timelines[0];
            timeline.Events.Clear();
            timeline.Events.Add(CreateCharacterActionEvent("event.a", timeline.TimelineId, "action.test", 0, 5, 100));
            timeline.Events.Add(new CharacterTestRuntimeAiLabBehaviorRecipeEvent
            {
                EventId = "event.exit", TimelineId = timeline.TimelineId, StartFrame = 0, DurationFrames = 5,
                Operation = new CharacterTestRuntimeAiLabBehaviorRecipeEventOperation
                { Kind = CharacterTestRuntimeAiLabBehaviorRecipeEventOperationKind.ReturnToPlanner }
            });
            var compile = CompileReference(payload);
            Assert.IsTrue(compile.Success, FormatDiagnostics(compile.Diagnostics));
            var runtime = new RecipeRuntimeStub();
            var evaluator = new CharacterTestBehaviorRecipeReferenceEvaluator(compile.Program, runtime);
            var events = new CharacterTestRuntimeAiLabLogicEventBuffer();
            Assert.IsTrue(evaluator.Start(0, events));
            evaluator.Tick(1, events);
            Assert.AreEqual(1, runtime.EnterCount, "Exit discards the action before it starts.");
            evaluator.Tick(2, events);
            Assert.AreEqual(CharacterTestRuntimeAiLabLogicRuntimeState.Completed, evaluator.State);
        }

        [Test]
        public void TimelineSampling_ExpiredOwnerDoesNotDelayNextAction()
        {
            var payload = CreateV6Recipe();
            var timeline = payload.Timelines[0];
            timeline.Events.Clear();
            timeline.Events.Add(CreateCharacterActionEvent("event.a", timeline.TimelineId, "action.test", 0, 1, 0));
            timeline.Events.Add(CreateCharacterActionEvent("event.b", timeline.TimelineId, "action.test", 1, 5, 0));
            var compile = CompileReference(payload);
            Assert.IsTrue(compile.Success, FormatDiagnostics(compile.Diagnostics));
            var runtime = new RecipeRuntimeStub();
            var evaluator = new CharacterTestBehaviorRecipeReferenceEvaluator(compile.Program, runtime);
            var events = new CharacterTestRuntimeAiLabLogicEventBuffer();
            Assert.IsTrue(evaluator.Start(0, events));
            evaluator.Tick(1, events);
            evaluator.Tick(2, events);
            CharacterTestRuntimeAiLabLogicEvaluatorSnapshot snapshot = evaluator.CreateSnapshot();
            Assert.AreEqual(
                compile.Program.RecipeV6.Events.Single(item => item.EventId == "event.b").Index,
                snapshot.LastTriggeredEventIndex);
            Assert.AreEqual(1, runtime.JumpRequests, "Only replacement, no preceding cancellation frame.");
        }

        private static CharacterTestRuntimeAiLabBehaviorRecipeConditionNode SamplingCondition(string id)
        {
            return new CharacterTestRuntimeAiLabBehaviorRecipeConditionNode
            {
                NodeId = "condition." + id,
                Kind = CharacterTestRuntimeAiLabBehaviorRecipeConditionNodeKind.FactPredicate,
                Predicate = new CharacterTestRuntimeAiLabCardCondition
                {
                    ConditionId = id, Fact = CharacterTestRuntimeAiLabCardConditionFact.TargetExists,
                    Operator = CharacterTestRuntimeAiLabCardConditionOperator.Equals, BoolValue = true
                }
            };
        }
        private static CharacterTestRuntimeAiLabBehaviorRecipeEvent CreateLoopEvent(
            string eventId,
            string timelineId,
            int startFrame,
            int durationFrames)
        {
            return new CharacterTestRuntimeAiLabBehaviorRecipeEvent
            {
                EventId = eventId,
                TimelineId = timelineId,
                StartFrame = startFrame,
                DurationFrames = durationFrames,
                Operation = new CharacterTestRuntimeAiLabBehaviorRecipeEventOperation
                {
                    Kind = CharacterTestRuntimeAiLabBehaviorRecipeEventOperationKind.LoopRegion,
                    MaxLoopRepeats = 0
                }
            };
        }

        private static CharacterTestRuntimeAiLabBehaviorRecipeEvent CreateWaitEvent(
            string eventId,
            string timelineId,
            int startFrame)
        {
            return new CharacterTestRuntimeAiLabBehaviorRecipeEvent
            {
                EventId = eventId,
                TimelineId = timelineId,
                StartFrame = startFrame,
                DurationFrames = 1,
                Operation = new CharacterTestRuntimeAiLabBehaviorRecipeEventOperation
                {
                    Kind = CharacterTestRuntimeAiLabBehaviorRecipeEventOperationKind.WaitForCondition
                }
            };
        }

        private static CharacterTestRuntimeAiLabBehaviorRecipeEvent CreateTimingMigrationEvent(
            string eventId,
            CharacterTestRuntimeAiLabBehaviorRecipeEventOperationKind operationKind,
            int durationFrames)
        {
            return new CharacterTestRuntimeAiLabBehaviorRecipeEvent
            {
                EventId = eventId,
                TimelineId = "approach",
                StartFrame = 0,
                DurationFrames = durationFrames,
                MaxExecutionsPerPass = 1,
                Operation = new CharacterTestRuntimeAiLabBehaviorRecipeEventOperation
                {
                    Kind = operationKind
                }
            };
        }

        private static CharacterTestRuntimeAiLabBehaviorRecipeEvent CreateCharacterActionEvent(
            string eventId,
            string timelineId,
            string actionId,
            int startFrame,
            int durationFrames,
            int priority)
        {
            return new CharacterTestRuntimeAiLabBehaviorRecipeEvent
            {
                EventId = eventId,
                TimelineId = timelineId,
                Title = eventId,
                TimingKind = CharacterTestRuntimeAiLabBehaviorRecipeEventTimingKind.Duration,
                StartFrame = startFrame,
                DurationFrames = durationFrames,
                MaxExecutionsPerPass = 1,
                CooldownFrames = 0,
                Priority = priority,
                Operation = new CharacterTestRuntimeAiLabBehaviorRecipeEventOperation
                {
                    Kind = CharacterTestRuntimeAiLabBehaviorRecipeEventOperationKind.CharacterAction,
                    CharacterActionId = int.TryParse(actionId, out int numericId) ? new CharacterActionId(numericId) : CharacterActionLegacyFixtureIds.From(actionId)
                }
            };
        }

        private static void ConfirmAllMigratedV7Intervals(
            CharacterTestRuntimeAiLabBehaviorCardLogicPayload payload)
        {
            CharacterTestRuntimeAiLabBehaviorRecipeV7Migration.SynchronizeForRead(payload);
            for (int timelineIndex = 0; timelineIndex < payload.Timelines.Count; timelineIndex++)
            {
                CharacterTestRuntimeAiLabBehaviorRecipeTimeline timeline = payload.Timelines[timelineIndex];
                for (int eventIndex = 0; timeline != null && eventIndex < timeline.Events.Count; eventIndex++)
                {
                    CharacterTestRuntimeAiLabBehaviorRecipeEvent evt = timeline.Events[eventIndex];
                    if (evt == null)
                        continue;
                    if (evt.DurationReviewRequired)
                        evt.DurationFrames = Math.Max(30, evt.DurationFrames);
                    evt.DurationReviewRequired = false;
                    evt.DurationFrames = Math.Max(1, evt.DurationFrames);
                }
            }
            for (int eventIndex = 0; eventIndex < payload.LegacySharedEvents.Count; eventIndex++)
            {
                CharacterTestRuntimeAiLabBehaviorRecipeEvent evt = payload.LegacySharedEvents[eventIndex];
                if (evt == null)
                    continue;
                if (evt.DurationReviewRequired)
                    evt.DurationFrames = Math.Max(30, evt.DurationFrames);
                evt.DurationReviewRequired = false;
                evt.DurationFrames = Math.Max(1, evt.DurationFrames);
            }
            CharacterTestRuntimeAiLabBehaviorRecipeV8Migration.SynchronizeForWrite(payload);
        }

        private static CharacterTestRuntimeAiLabBehaviorCardLogicPayload CreateV6Recipe()
        {
            var payload = new CharacterTestRuntimeAiLabBehaviorCardLogicPayload
            {
                BehaviorRecipeVersion = 8,
                EntryTimelineId = "approach"
            };
            var timeline = new CharacterTestRuntimeAiLabBehaviorRecipeTimeline
            {
                TimelineId = "approach",
                Order = 0,
                Title = "持续靠近"
            };
            timeline.Events.Add(new CharacterTestRuntimeAiLabBehaviorRecipeEvent
            {
                EventId = "event.move",
                TimelineId = timeline.TimelineId,
                Title = "接近目标",
                StartFrame = 0,
                DurationFrames = 30,
                MaxExecutionsPerPass = 1,
                Priority = 100,
                Operation = new CharacterTestRuntimeAiLabBehaviorRecipeEventOperation
                {
                    Kind = CharacterTestRuntimeAiLabBehaviorRecipeEventOperationKind.MoveToTarget,
                    MovementStrategy = CharacterTestRuntimeAiLabMovementStrategy.ApproachTarget,
                    MovementTargetKind = CharacterTestRuntimeAiLabMovementTargetKind.CurrentCombatTarget,
                    MoveMinDistance = 0f,
                    MoveMaxDistance = 1.5f
                }
            });
            timeline.Events.Add(new CharacterTestRuntimeAiLabBehaviorRecipeEvent
            {
                EventId = "event.exit",
                TimelineId = timeline.TimelineId,
                Title = "满足任一退出条件",
                StartFrame = 0,
                DurationFrames = 30,
                MaxExecutionsPerPass = 1,
                Priority = 200,
                ConditionRoot = CreateAnyCondition(),
                Operation = new CharacterTestRuntimeAiLabBehaviorRecipeEventOperation
                {
                    Kind = CharacterTestRuntimeAiLabBehaviorRecipeEventOperationKind.ReturnToPlanner
                }
            });
            timeline.Events.Add(new CharacterTestRuntimeAiLabBehaviorRecipeEvent
            {
                EventId = "event.loop-tail",
                TimelineId = timeline.TimelineId,
                Title = "循环片段",
                StartFrame = 20,
                DurationFrames = 5,
                MaxExecutionsPerPass = 2,
                CooldownFrames = 2,
                Priority = 10,
                Operation = new CharacterTestRuntimeAiLabBehaviorRecipeEventOperation
                {
                    Kind = CharacterTestRuntimeAiLabBehaviorRecipeEventOperationKind.LoopRegion,
                    MaxLoopRepeats = 1
                }
            });
            payload.Timelines.Add(timeline);
            CharacterTestRuntimeAiLabBehaviorRecipeV8Migration.SynchronizeForWrite(payload);
            return payload;
        }

        private static CharacterTestRuntimeAiLabBehaviorRecipeConditionNode CreateAnyCondition()
        {
            var root = new CharacterTestRuntimeAiLabBehaviorRecipeConditionNode
            {
                NodeId = "condition.any",
                Kind = CharacterTestRuntimeAiLabBehaviorRecipeConditionNodeKind.Any
            };
            root.Children.Add(new CharacterTestRuntimeAiLabBehaviorRecipeConditionNode
            {
                NodeId = "condition.distance",
                Kind = CharacterTestRuntimeAiLabBehaviorRecipeConditionNodeKind.FactPredicate,
                Predicate = new CharacterTestRuntimeAiLabCardCondition
                {
                    ConditionId = "distance",
                    Fact = CharacterTestRuntimeAiLabCardConditionFact.TargetDistance,
                    Operator = CharacterTestRuntimeAiLabCardConditionOperator.LessOrEqual,
                    Value = 1.5f
                }
            });
            root.Children.Add(new CharacterTestRuntimeAiLabBehaviorRecipeConditionNode
            {
                NodeId = "condition.path",
                Kind = CharacterTestRuntimeAiLabBehaviorRecipeConditionNodeKind.FactPredicate,
                Predicate = new CharacterTestRuntimeAiLabCardCondition
                {
                    ConditionId = "path",
                    Fact = CharacterTestRuntimeAiLabCardConditionFact.MovePathAvailable,
                    Operator = CharacterTestRuntimeAiLabCardConditionOperator.Equals,
                    BoolValue = false
                }
            });
            return root;
        }

        private sealed class RecipeRuntimeStub :
            ICharacterTestRuntimeAiLabBehaviorCardLogicRuntime,
            ICharacterTestRuntimeAiLabBehaviorRecipeV5Runtime
        {
            private readonly Dictionary<CharacterTestRuntimeAiLabCardConditionFact, bool> _boolFacts =
                new Dictionary<CharacterTestRuntimeAiLabCardConditionFact, bool>();
            private readonly Dictionary<CharacterTestRuntimeAiLabCardConditionFact, float> _numberFacts =
                new Dictionary<CharacterTestRuntimeAiLabCardConditionFact, float>();
            private long _nextInstanceId = 100L;

            public CharacterTestRuntimeAiLabLogicActionState PrimaryActionState { get; set; }
            public long PrimaryActionInstanceId { get; set; }
            public int PrimaryActionSignalReasonCode { get; set; }
            public bool PrimaryActionRejected { get; set; }
            public CharacterTestRuntimeAiLabBehaviorRecipeOrchestrationState OrchestrationState { get; set; }
            public int OrchestrationFailureReasonCode { get; set; }
            public string CurrentTimelineId { get; private set; } = string.Empty;
            public int EnterCount { get; private set; }
            public int JumpRequests { get; private set; }
            public int EvaluateCount { get; private set; }

            public void SetBool(CharacterTestRuntimeAiLabCardConditionFact fact, bool value)
            {
                _boolFacts[fact] = value;
            }

            public void SetNumber(CharacterTestRuntimeAiLabCardConditionFact fact, float value)
            {
                _numberFacts[fact] = value;
            }

            public bool Evaluate(in CharacterTestRuntimeAiLabCompiledCondition condition)
            {
                EvaluateCount++;
                if (condition.Fact == CharacterTestRuntimeAiLabCardConditionFact.TargetState)
                {
                    bool exists = _boolFacts.TryGetValue(CharacterTestRuntimeAiLabCardConditionFact.TargetExists, out bool value) && value;
                    var mask = exists ? CharacterTestRuntimeAiPlannerTargetStateMask.Alive : CharacterTestRuntimeAiPlannerTargetStateMask.NoTarget;
                    if (_boolFacts.TryGetValue(CharacterTestRuntimeAiLabCardConditionFact.MovePathAvailable, out bool path) && path)
                        mask |= CharacterTestRuntimeAiPlannerTargetStateMask.MovePathAvailable;
                    return CharacterTestRuntimeAiPlannerTargetStatePolicy.Matches(mask, condition.AllowedTargetPhases,
                        condition.RequiredTargetFlags, condition.ForbiddenTargetFlags);
                }
                if (condition.Fact == CharacterTestRuntimeAiLabCardConditionFact.TargetDistance
                    || condition.Fact == CharacterTestRuntimeAiLabCardConditionFact.TargetAttackImpactFrames)
                {
                    float value = _numberFacts.TryGetValue(condition.Fact, out float current) ? current : 0f;
                    return condition.Operator switch
                    {
                        CharacterTestRuntimeAiLabCardConditionOperator.Equals => value == condition.Value,
                        CharacterTestRuntimeAiLabCardConditionOperator.NotEquals => value != condition.Value,
                        CharacterTestRuntimeAiLabCardConditionOperator.LessOrEqual => value <= condition.Value,
                        CharacterTestRuntimeAiLabCardConditionOperator.GreaterOrEqual => value >= condition.Value,
                        CharacterTestRuntimeAiLabCardConditionOperator.GreaterThan => value > condition.Value,
                        CharacterTestRuntimeAiLabCardConditionOperator.Between =>
                            value >= condition.Value && value <= condition.MaxValue,
                        _ => false
                    };
                }
                bool boolValue = _boolFacts.TryGetValue(condition.Fact, out bool currentBool) && currentBool;
                return condition.Operator == CharacterTestRuntimeAiLabCardConditionOperator.NotEquals
                    ? boolValue != condition.BoolValue
                    : boolValue == condition.BoolValue;
            }

            public bool TryEnterStage(CharacterTestRuntimeAiLabCompiledLogicStage stage)
            {
                CurrentTimelineId = stage?.TimelineId ?? string.Empty;
                EnterCount++;
                if (stage != null
                    && (stage.Kind == CharacterTestRuntimeAiLabCardStepKind.CharacterAction
                        || stage.Kind == CharacterTestRuntimeAiLabCardStepKind.UseAbility))
                {
                    PrimaryActionInstanceId = ++_nextInstanceId;
                    PrimaryActionState = CharacterTestRuntimeAiLabLogicActionState.Running;
                }
                else
                {
                    PrimaryActionInstanceId = 0L;
                    PrimaryActionState = stage?.Kind == CharacterTestRuntimeAiLabCardStepKind.Logic
                        ? CharacterTestRuntimeAiLabLogicActionState.None
                        : CharacterTestRuntimeAiLabLogicActionState.Running;
                }
                return true;
            }

            public bool TryInterruptPrimaryAction() => true;

            public bool TryBeginTimelineJump(CharacterTestRuntimeAiLabCompiledLogicStage target)
            {
                JumpRequests++;
                OrchestrationState = CharacterTestRuntimeAiLabBehaviorRecipeOrchestrationState.Succeeded;
                return true;
            }

            public bool TryBeginReturnToPlanner()
            {
                OrchestrationState = CharacterTestRuntimeAiLabBehaviorRecipeOrchestrationState.Succeeded;
                return true;
            }

            public void CompleteOrchestration()
            {
                OrchestrationState = CharacterTestRuntimeAiLabBehaviorRecipeOrchestrationState.None;
            }
        }
    }
}
