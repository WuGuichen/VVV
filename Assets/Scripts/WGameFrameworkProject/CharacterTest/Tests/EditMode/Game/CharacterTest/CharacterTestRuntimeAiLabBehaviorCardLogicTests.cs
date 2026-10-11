using System;
using System.Collections.Generic;
using System.Linq;
using MxFramework.CharacterAction;
using MxFramework.Game.CharacterTest;
using MxFramework.Runtime;
using Newtonsoft.Json;
using NUnit.Framework;
using UnityEngine;

namespace MxFramework.Tests.Game.CharacterTest
{
    public sealed class CharacterTestRuntimeAiLabBehaviorCardLogicTests
    {
        private const int TestBehaviorCardNumericId = 1001;
        private const int TestPendingEventIndex = 5;
        private const int TestLastTriggeredEventIndex = 3;
        private const int TestOrchestrationFailureReasonCode = 1004;

        [Test]
        public void RuntimeIntent_ExposesOneDiscreteCharacterActionAndNoInputButtons()
        {
            Assert.IsNull(typeof(CharacterTestRuntimeAiLabRuntimeIntent).GetProperty("AttackPrimaryPressed"));
            Assert.IsNull(typeof(CharacterTestRuntimeAiLabRuntimeIntent).GetProperty("DodgePressed"));
            Assert.AreEqual(
                3,
                (int)CharacterTestRuntimeAiLabRuntimeIntentKind.CharacterAction);
        }

        [Test]
        public void V1Migration_ProducesStagesDetectorsAndTransitions()
        {
            CharacterTestRuntimeAiLabBehaviorCardPayload v1 =
                CharacterTestRuntimeAiLabBehaviorCardFactory.CreateActionChainFixture();

            CharacterTestRuntimeAiLabBehaviorCardLogicPayload v2 =
                CharacterTestRuntimeAiLabBehaviorCardV1Migration.Convert(v1);
            CharacterTestRuntimeAiLabBehaviorCardLogicCompileResult compile =
                CharacterTestRuntimeAiLabBehaviorCardLogicCompiler.Compile(v2);

            Assert.IsTrue(compile.Success, FormatDiagnostics(compile.Diagnostics));
            Assert.AreEqual("dash", v2.EntryStageId);
            Assert.AreEqual(v1.Steps.Count, v2.Stages.Count);
            Assert.AreEqual(10, v2.Detectors.Count);
            Assert.AreEqual(v2.Detectors.Count, v2.Transitions.Count);
            Assert.AreEqual(CharacterTestRuntimeAiLabLogicDetectorTrigger.ConditionEdge, v2.Detectors[0].Trigger);
            Assert.AreEqual(CharacterTestRuntimeAiLabLogicDetectorTrigger.ActionCompleted, v2.Detectors[1].Trigger);
            Assert.AreEqual(CharacterTestRuntimeAiLabLogicTransitionPolicy.Interrupt, v2.Transitions[0].Policy);
            Assert.AreEqual(CharacterTestRuntimeAiLabLogicTransitionPolicy.AfterActionEnd, v2.Transitions[1].Policy);
        }

        [Test]
        public void V1Migration_AtomicCardCompilesWithoutChangingSource()
        {
            CharacterTestRuntimeAiLabBehaviorCardPayload source =
                CharacterTestRuntimeAiLabBehaviorCardFactory.CreateLegacyAtomic(
                    "atomic.approach",
                    CharacterTestRuntimeAiPlannerActionKind.MoveToTarget);
            string before = CharacterTestRuntimeAiLabBehaviorCardJson.Serialize(source);

            CharacterTestRuntimeAiLabBehaviorCardLogicPayload migrated =
                CharacterTestRuntimeAiLabBehaviorCardV1Migration.Convert(source);
            CharacterTestRuntimeAiLabBehaviorCardLogicCompileResult compile =
                CharacterTestRuntimeAiLabBehaviorCardLogicCompiler.Compile(migrated);

            Assert.IsTrue(compile.Success, FormatDiagnostics(compile.Diagnostics));
            Assert.AreEqual(before, CharacterTestRuntimeAiLabBehaviorCardJson.Serialize(source));
            Assert.AreEqual(1, compile.Program.Stages.Count);
            Assert.AreEqual(CharacterTestRuntimeAiLabCardStepKind.MoveToTarget, compile.Program.Stages[0].Kind);
        }

        [Test]
        public void V1Migration_FallbackWaitBecomesPersistentHold()
        {
            CharacterTestRuntimeAiLabBehaviorCardPayload source =
                CharacterTestRuntimeAiLabBehaviorCardFactory.CreateLegacyAtomic(
                    CharacterTestRuntimeAiPlannerBehaviorCompositionCatalog.FoundationIdle,
                    CharacterTestRuntimeAiPlannerActionKind.Wait);

            CharacterTestRuntimeAiLabBehaviorCardLogicPayload migrated =
                CharacterTestRuntimeAiLabBehaviorCardV1Migration.Convert(source);
            CharacterTestRuntimeAiLabBehaviorCardLogicCompileResult compile =
                CharacterTestRuntimeAiLabBehaviorCardLogicCompiler.Compile(migrated);

            Assert.IsTrue(compile.Success, FormatDiagnostics(compile.Diagnostics));
            Assert.AreEqual(1, compile.Program.Stages.Count);
            Assert.AreEqual(CharacterTestRuntimeAiLabCardStepKind.Wait, compile.Program.Stages[0].Kind);
            Assert.AreEqual(
                CharacterTestRuntimeAiLabLogicDurationMode.Infinite,
                compile.Program.Stages[0].DurationMode);
            Assert.AreEqual(0, compile.Program.Detectors.Count);
            Assert.AreEqual(0, compile.Program.Transitions.Count);
        }

        [Test]
        public void V1Migration_ReactiveDodgeCompilesWithTypedThreatFacts()
        {
            CharacterTestRuntimeAiLabBehaviorCardPayload source =
                CharacterTestRuntimeAiLabBehaviorCardFactory.CreateReactiveDodgeAtomic(6, 12);

            CharacterTestRuntimeAiLabBehaviorCardLogicPayload migrated =
                CharacterTestRuntimeAiLabBehaviorCardV1Migration.Convert(source);
            CharacterTestRuntimeAiLabBehaviorCardLogicCompileResult compile =
                CharacterTestRuntimeAiLabBehaviorCardLogicCompiler.Compile(migrated);

            Assert.IsTrue(compile.Success, FormatDiagnostics(compile.Diagnostics));
            Assert.AreEqual(source.Steps.Count, migrated.Stages.Count);
            Assert.IsTrue(migrated.ActivationConditions.Exists(
                condition => condition.Fact == CharacterTestRuntimeAiLabCardConditionFact.TargetAttackWillHitSelf));
            Assert.AreEqual(CharacterTestRuntimeAiLabCardStepKind.CharacterAction, migrated.Stages[0].Kind);
            Assert.AreEqual(
                CharacterTestCharacterActionConfigSource.DefaultEvadeActionId,
                migrated.Stages[0].CharacterActionId);
            Assert.AreEqual(1, migrated.Stages[0].PlannedDurationFrames);
            Assert.IsFalse(
                CharacterTestRuntimeAiLabBehaviorCardGraphMutation.RequiresWindowCanonicalization(migrated),
                "Fresh v3 projections must not carry dormant fixed-window durations.");
        }

        [Test]
        public void Compiler_LayoutDoesNotChangeSemanticHash()
        {
            CharacterTestRuntimeAiLabBehaviorCardLogicPayload first = CreatePriorityCard();
            CharacterTestRuntimeAiLabBehaviorCardLogicPayload second = first.Clone();
            second.Layout.Stages[0].Column = 12;
            second.Layout.Stages[0].Offset = 0.75f;
            second.Layout.Stages[0].Width = 3f;

            CharacterTestRuntimeAiLabBehaviorCardLogicCompileResult firstCompile =
                CharacterTestRuntimeAiLabBehaviorCardLogicCompiler.Compile(first);
            CharacterTestRuntimeAiLabBehaviorCardLogicCompileResult secondCompile =
                CharacterTestRuntimeAiLabBehaviorCardLogicCompiler.Compile(second);

            Assert.IsTrue(firstCompile.Success, FormatDiagnostics(firstCompile.Diagnostics));
            Assert.IsTrue(secondCompile.Success, FormatDiagnostics(secondCompile.Diagnostics));
            Assert.AreEqual(firstCompile.Program.SemanticHash, secondCompile.Program.SemanticHash);
        }

        [Test]
        public void Compiler_AuthoredTimingChangesSemanticHash()
        {
            CharacterTestRuntimeAiLabBehaviorCardLogicPayload first = CreatePriorityCard();
            CharacterTestRuntimeAiLabBehaviorCardLogicPayload second = first.Clone();
            second.Stages[0].PlannedStartFrame = 15;
            second.Stages[0].PlannedDurationFrames = 90;
            second.Stages[0].DurationMode = CharacterTestRuntimeAiLabLogicDurationMode.Fixed;

            CharacterTestRuntimeAiLabBehaviorCardLogicCompileResult firstCompile =
                CharacterTestRuntimeAiLabBehaviorCardLogicCompiler.Compile(first);
            CharacterTestRuntimeAiLabBehaviorCardLogicCompileResult secondCompile =
                CharacterTestRuntimeAiLabBehaviorCardLogicCompiler.Compile(second);

            Assert.IsTrue(firstCompile.Success, FormatDiagnostics(firstCompile.Diagnostics));
            Assert.IsTrue(secondCompile.Success, FormatDiagnostics(secondCompile.Diagnostics));
            Assert.AreNotEqual(firstCompile.Program.SemanticHash, secondCompile.Program.SemanticHash);
        }

        [Test]
        public void Validator_RejectsOverlappingFixedPrimaryActions()
        {
            CharacterTestRuntimeAiLabBehaviorCardLogicPayload payload = CreatePriorityCard();
            payload.Stages[0].PlannedStartFrame = 0;
            payload.Stages[0].PlannedDurationFrames = 60;
            payload.Stages[0].DurationMode = CharacterTestRuntimeAiLabLogicDurationMode.Fixed;
            payload.Stages[1].PlannedStartFrame = 30;
            payload.Stages[1].PlannedDurationFrames = 60;
            payload.Stages[1].DurationMode = CharacterTestRuntimeAiLabLogicDurationMode.Fixed;

            IReadOnlyList<CharacterTestRuntimeAiLabCardDiagnostic> diagnostics =
                CharacterTestRuntimeAiLabBehaviorCardLogicValidator.Validate(payload);

            Assert.IsTrue(ContainsCode(diagnostics, "CARD_V2_PRIMARY_ACTION_TIME_CONFLICT"));
        }

        [Test]
        public void Validator_RejectsConditionIdsReusedAcrossOwners()
        {
            CharacterTestRuntimeAiLabBehaviorCardLogicPayload payload = CreatePriorityCard();
            payload.Stages[0].EntryConditions.Add(new CharacterTestRuntimeAiLabCardCondition
            {
                ConditionId = payload.Detectors[0].Conditions[0].ConditionId,
                Fact = CharacterTestRuntimeAiLabCardConditionFact.TargetVisible,
                Operator = CharacterTestRuntimeAiLabCardConditionOperator.Equals,
                BoolValue = true
            });

            IReadOnlyList<CharacterTestRuntimeAiLabCardDiagnostic> diagnostics =
                CharacterTestRuntimeAiLabBehaviorCardLogicValidator.Validate(payload);

            Assert.IsTrue(ContainsCode(diagnostics, "CARD_V2_CONDITION_DUPLICATE"));
        }

        [Test]
        public void Evaluator_DetectorOnlyParticipatesInsideAuthoredWindow()
        {
            CharacterTestRuntimeAiLabBehaviorCardLogicPayload payload = CreateLoopCard();
            payload.Detectors[0].ActivationStartFrame = 3;
            payload.Detectors[0].ActivationDurationFrames = 2;
            payload.Detectors[0].WindowEndMode =
                CharacterTestRuntimeAiLabLogicDetectorWindowEndMode.Fixed;
            CharacterTestRuntimeAiLabBehaviorCardLogicCompileResult compile =
                CharacterTestRuntimeAiLabBehaviorCardLogicCompiler.Compile(payload);
            var runtime = new LogicRuntimeStub();
            runtime.Set(CharacterTestRuntimeAiLabCardConditionFact.TargetVisible, true);
            var evaluator = new CharacterTestRuntimeAiLabBehaviorCardLogicEvaluator(compile.Program, runtime);
            var events = new CharacterTestRuntimeAiLabLogicEventBuffer();

            Assert.IsTrue(evaluator.Start(10, events));
            evaluator.Tick(11, events);
            Assert.AreEqual(0, Count(events, CharacterTestRuntimeAiLabLogicEventKind.DetectorTriggered));
            evaluator.Tick(12, events);
            Assert.AreEqual(0, Count(events, CharacterTestRuntimeAiLabLogicEventKind.DetectorTriggered));
            evaluator.Tick(13, events);
            Assert.AreEqual(1, Count(events, CharacterTestRuntimeAiLabLogicEventKind.DetectorTriggered));
        }

        [Test]
        public void Compiler_PureLogicStage_DoesNotOwnPrimaryAction()
        {
            CharacterTestRuntimeAiLabBehaviorCardLogicPayload payload = CreateLoopCard();
            payload.Stages[0].Kind = CharacterTestRuntimeAiLabCardStepKind.Logic;
            payload.Stages[0].WaitFrames = 0;

            CharacterTestRuntimeAiLabBehaviorCardLogicCompileResult compile =
                CharacterTestRuntimeAiLabBehaviorCardLogicCompiler.Compile(payload);

            Assert.IsTrue(compile.Success, FormatDiagnostics(compile.Diagnostics));
            Assert.IsFalse(compile.Program.Stages[0].OwnsPrimaryAction);
        }

        [Test]
        public void TimelineEventIndex_CapturesTypedBehaviorLogicMarkers()
        {
            var index = new CharacterTestRuntimeAiLabTimelineEventIndex();
            index.Reset(20, 10);
            var events = new CharacterTestRuntimeAiLabLogicEventBuffer();
            var evt = new CharacterTestRuntimeAiLabLogicEvent(
                21,
                CharacterTestRuntimeAiLabLogicEventKind.TransitionCommitted,
                "approach",
                "threat",
                "threat.to-dodge",
                entityId: 7,
                behaviorEntryId: "entry.dodge",
                programSemanticHash: 42L,
                targetTimelineId: "dodge",
                localFrame: 7,
                primaryActionInstanceId: 99L,
                reasonCode: global::MxFramework.Tests.Game.CharacterTest.MigrationFixtureIds.Id0002,
                triggerSourceKind:
                    CharacterTestRuntimeAiLabBehaviorRecipeTriggerSourceKind.PrimaryActionSignal,
                outcomeKind: CharacterTestRuntimeAiLabBehaviorRecipeOutcomeKind.EnterTimeline);
            events.Add(evt);

            index.CaptureFrame(21, null, null, null, null, events);

            Assert.AreEqual(1, index.GetCount(21));
            Assert.AreEqual(
                CharacterTestRuntimeAiLabTimelineEventKind.BehaviorTransitionCommitted,
                index.GetSample(21, 0).Kind);
            Assert.AreEqual("threat.to-dodge", index.GetSample(21, 0).Label);
            Assert.AreEqual(7, index.GetSample(21, 0).EntityId);
            Assert.AreEqual("entry.dodge", index.GetSample(21, 0).BehaviorEntryId);
            Assert.AreEqual("approach", index.GetSample(21, 0).BehaviorStepId);
            Assert.AreEqual(42L, index.GetSample(21, 0).InstanceId);
            Assert.AreEqual(
                CharacterTestRuntimeAiLabBehaviorRecipeTriggerSourceKind.PrimaryActionSignal,
                index.GetSample(21, 0).BehaviorTriggerSourceKind);
            Assert.AreEqual(
                CharacterTestRuntimeAiLabBehaviorRecipeOutcomeKind.EnterTimeline,
                index.GetSample(21, 0).BehaviorOutcomeKind);
            Assert.AreEqual("dodge", index.GetSample(21, 0).BehaviorTargetTimelineId);
            Assert.AreEqual(7, index.GetSample(21, 0).BehaviorLocalFrame);
            Assert.AreEqual(99L, index.GetSample(21, 0).BehaviorPrimaryActionInstanceId);
            Assert.AreEqual(MigrationFixtureIds.Id0002, index.GetSample(21, 0).BehaviorReasonCode);

            Assert.IsTrue(CharacterTestRuntimeAiPlannerDevSceneController.TryConvertLogicTimelineSample(
                index.GetSample(21, 0),
                out CharacterTestRuntimeAiLabLogicEvent restored));
            Assert.AreEqual("dodge", restored.TargetTimelineId);
            Assert.AreEqual(7, restored.LocalFrame);
            Assert.AreEqual(99L, restored.PrimaryActionInstanceId);
            Assert.AreEqual(MigrationFixtureIds.Id0002, restored.ReasonCode);
            Assert.AreEqual(
                CharacterTestRuntimeAiLabBehaviorRecipeTriggerSourceKind.PrimaryActionSignal,
                restored.TriggerSourceKind);
            Assert.AreEqual(
                CharacterTestRuntimeAiLabBehaviorRecipeOutcomeKind.EnterTimeline,
                restored.OutcomeKind);
        }

        [Test]
        public void TimelineEventIndex_MapsEvaluatedLogicFrameToPostTickSnapshotFrame()
        {
            var index = new CharacterTestRuntimeAiLabTimelineEventIndex();
            index.Reset(21, 10);
            var events = new CharacterTestRuntimeAiLabLogicEventBuffer();
            events.Add(new CharacterTestRuntimeAiLabLogicEvent(
                20,
                CharacterTestRuntimeAiLabLogicEventKind.StageEntered,
                "approach",
                string.Empty,
                string.Empty,
                entityId: 7,
                behaviorEntryId: "entry.approach",
                programSemanticHash: 42L));

            index.CaptureFrame(
                21,
                null,
                null,
                null,
                null,
                events,
                logicEventFrame: 20);

            Assert.AreEqual(1, index.GetCount(21));
            CharacterTestRuntimeAiLabTimelineEventSample sample = index.GetSample(21, 0);
            Assert.AreEqual(21, sample.Frame);
            Assert.AreEqual(
                CharacterTestRuntimeAiLabTimelineEventKind.BehaviorStageEntered,
                sample.Kind);
            Assert.AreEqual("approach", sample.BehaviorStepId);
        }

        [Test]
        public void HistoricalProjectionUsesExactCommitAndDoesNotRepeatItAsPreviousPath()
        {
            CharacterTestRuntimeAiLabBehaviorCardLogicCompileResult compile =
                CharacterTestRuntimeAiLabBehaviorCardLogicCompiler.Compile(CreatePriorityCard());
            CharacterTestRuntimeAiLabBehaviorCardRuntimeIdentityResolution runtime =
                CreateHistoricalRuntime(compile, selectedFrame: 12, cardStartedFrame: 10);
            var index = new CharacterTestRuntimeAiLabTimelineEventIndex();
            index.Reset(new CharacterTestRuntimeAiLabTimelineSessionIdentity(7, 10, 16));
            index.CaptureFrame(10, null, null, null, null);
            index.CaptureFrame(11, null, null, null, null);
            var exact = new CharacterTestRuntimeAiLabLogicEventBuffer();
            exact.Add(CreateCommittedEvent(
                compile,
                runtime.CommittedCard.RuntimeEntryId,
                frame: 12,
                profileSlot: 2,
                cardStartedFrame: 10));
            index.CaptureFrame(12, null, null, null, null, exact);
            var projection = new CharacterTestRuntimeAiLabHistoricalBehaviorProjection();

            CharacterTestRuntimeAiLabHistoricalBehaviorProjector.ResolveInto(
                projection,
                index,
                7,
                10,
                12,
                13,
                runtime);

            Assert.That(
                projection.Status,
                Is.EqualTo(CharacterTestRuntimeAiLabHistoricalBehaviorProjectionStatus.Resolved),
                projection.DiagnosticCode + ": " + projection.Diagnostic);
            Assert.That(projection.ExactCommittedTransitionIndex, Is.GreaterThanOrEqualTo(0));
            Assert.That(projection.PreviousCommittedTransitionIndex, Is.EqualTo(-1));
            Assert.That(projection.ExactFrameEvents.Count, Is.EqualTo(1));
        }

        [Test]
        public void HistoricalProjectionFiltersOtherSlotAndOtherCardInstanceEvents()
        {
            CharacterTestRuntimeAiLabBehaviorCardLogicCompileResult compile =
                CharacterTestRuntimeAiLabBehaviorCardLogicCompiler.Compile(CreatePriorityCard());
            CharacterTestRuntimeAiLabBehaviorCardRuntimeIdentityResolution runtime =
                CreateHistoricalRuntime(compile, selectedFrame: 13, cardStartedFrame: 10);
            var index = new CharacterTestRuntimeAiLabTimelineEventIndex();
            index.Reset(new CharacterTestRuntimeAiLabTimelineSessionIdentity(8, 10, 16));
            index.CaptureFrame(10, null, null, null, null);
            var previous = new CharacterTestRuntimeAiLabLogicEventBuffer();
            previous.Add(CreateCommittedEvent(
                compile,
                runtime.CommittedCard.RuntimeEntryId,
                frame: 11,
                profileSlot: 2,
                cardStartedFrame: 10));
            index.CaptureFrame(11, null, null, null, null, previous);
            var wrongInstance = new CharacterTestRuntimeAiLabLogicEventBuffer();
            wrongInstance.Add(CreateCommittedEvent(
                compile,
                runtime.CommittedCard.RuntimeEntryId,
                frame: 12,
                profileSlot: 2,
                cardStartedFrame: 12));
            wrongInstance.Add(CreateCommittedEvent(
                compile,
                runtime.CommittedCard.RuntimeEntryId,
                frame: 12,
                profileSlot: 1,
                cardStartedFrame: 10));
            index.CaptureFrame(12, null, null, null, null, wrongInstance);
            index.CaptureFrame(13, null, null, null, null);
            var projection = new CharacterTestRuntimeAiLabHistoricalBehaviorProjection();

            CharacterTestRuntimeAiLabHistoricalBehaviorProjector.ResolveInto(
                projection,
                index,
                8,
                10,
                13,
                14,
                runtime);

            Assert.That(projection.PreviousCommittedRuntimeFrame, Is.EqualTo(11));
            Assert.That(projection.PreviousCommittedTransitionIndex, Is.GreaterThanOrEqualTo(0));
        }

        [Test]
        public void HistoricalProjectionTreatsSelectedAndReverseScanOverflowAsEvidenceBarrier()
        {
            CharacterTestRuntimeAiLabBehaviorCardLogicCompileResult compile =
                CharacterTestRuntimeAiLabBehaviorCardLogicCompiler.Compile(CreatePriorityCard());
            CharacterTestRuntimeAiLabBehaviorCardRuntimeIdentityResolution runtime =
                CreateHistoricalRuntime(compile, selectedFrame: 13, cardStartedFrame: 10);
            var index = new CharacterTestRuntimeAiLabTimelineEventIndex();
            index.Reset(new CharacterTestRuntimeAiLabTimelineSessionIdentity(9, 10, 16));
            index.CaptureFrame(10, null, null, null, null);
            var oldCommit = new CharacterTestRuntimeAiLabLogicEventBuffer();
            oldCommit.Add(CreateCommittedEvent(
                compile,
                runtime.CommittedCard.RuntimeEntryId,
                frame: 11,
                profileSlot: 2,
                cardStartedFrame: 10));
            index.CaptureFrame(11, null, null, null, null, oldCommit);
            var overflow = new CharacterTestRuntimeAiLabLogicEventBuffer(1);
            overflow.Add(CreateCommittedEvent(
                compile,
                runtime.CommittedCard.RuntimeEntryId,
                frame: 12,
                profileSlot: 1,
                cardStartedFrame: 10));
            overflow.Add(CreateCommittedEvent(
                compile,
                runtime.CommittedCard.RuntimeEntryId,
                frame: 12,
                profileSlot: 3,
                cardStartedFrame: 10));
            index.CaptureFrame(12, null, null, null, null, overflow);
            index.CaptureFrame(13, null, null, null, null);
            var projection = new CharacterTestRuntimeAiLabHistoricalBehaviorProjection();

            CharacterTestRuntimeAiLabHistoricalBehaviorProjector.ResolveInto(
                projection,
                index,
                9,
                10,
                13,
                14,
                runtime);

            Assert.That(
                projection.EventEvidenceState,
                Is.EqualTo(CharacterTestRuntimeAiLabHistoricalEventEvidenceState.PreviousScanOverflow));
            Assert.That(projection.PreviousCommittedTransitionIndex, Is.EqualTo(-1));
            Assert.That(projection.DiagnosticCode, Is.EqualTo("HISTORY_PREVIOUS_PATH_OVERFLOW"));

            runtime.Frame = 12;
            CharacterTestRuntimeAiLabHistoricalBehaviorProjector.ResolveInto(
                projection,
                index,
                9,
                10,
                12,
                14,
                runtime);
            Assert.That(
                projection.EventEvidenceState,
                Is.EqualTo(CharacterTestRuntimeAiLabHistoricalEventEvidenceState.SelectedFrameOverflow));
            Assert.That(projection.RuntimeState, Is.SameAs(runtime.LogicAgent));
            Assert.That(projection.ExactFrameEvents.Count, Is.EqualTo(0));
            Assert.That(projection.ExactCommittedTransitionIndex, Is.EqualTo(-1));
            Assert.That(projection.PreviousCommittedTransitionIndex, Is.EqualTo(-1));
        }

        [Test]
        public void DetectorTriggered_RoundTripsDetectorAndChosenRouteIntoWorkspaceOverlay()
        {
            CharacterTestRuntimeAiLabBehaviorCardLogicPayload payload = CreatePriorityCard();
            payload.Transitions.Add(new CharacterTestRuntimeAiLabLogicTransition
            {
                TransitionId = "threat.to-attack.lower",
                DetectorId = "threat",
                SourceStageId = "approach",
                TargetStageId = "attack",
                Priority = 50,
                Policy = CharacterTestRuntimeAiLabLogicTransitionPolicy.Interrupt
            });
            CharacterTestRuntimeAiLabBehaviorCardLogicCompileResult compile =
                CharacterTestRuntimeAiLabBehaviorCardLogicCompiler.Compile(payload);
            Assert.IsTrue(compile.Success, FormatDiagnostics(compile.Diagnostics));
            var runtime = new LogicRuntimeStub();
            runtime.Set(CharacterTestRuntimeAiLabCardConditionFact.TargetVisible, true);
            runtime.Set(CharacterTestRuntimeAiLabCardConditionFact.TargetAttackWillHitSelf, true);
            var evaluator = new CharacterTestRuntimeAiLabBehaviorCardLogicEvaluator(compile.Program, runtime);
            var evaluated = new CharacterTestRuntimeAiLabLogicEventBuffer();
            Assert.IsTrue(evaluator.Start(10, evaluated));
            runtime.PrimaryActionState = CharacterTestRuntimeAiLabLogicActionState.None;
            evaluator.Tick(11, evaluated);
            CollectionAssert.Contains(runtime.BegunLogicCandidates, "threat.to-dodge");
            CollectionAssert.Contains(runtime.BegunLogicCandidates, "threat.to-attack.lower");
            CollectionAssert.Contains(runtime.SelectedLogicCandidates, "threat.to-dodge");

            var authoritative = new CharacterTestRuntimeAiLabLogicEventBuffer();
            for (int i = 0; i < evaluated.Count; i++)
            {
                CharacterTestRuntimeAiLabLogicEvent evt = evaluated[i];
                authoritative.Add(new CharacterTestRuntimeAiLabLogicEvent(
                    evt.Frame,
                    evt.Kind,
                    evt.StageId,
                    evt.DetectorId,
                    evt.TransitionId,
                    entityId: 7,
                    behaviorEntryId: "entry.priority",
                    programSemanticHash: compile.Program.SemanticHash,
                    targetTimelineId: evt.TargetTimelineId,
                    localFrame: evt.LocalFrame,
                    primaryActionInstanceId: evt.PrimaryActionInstanceId,
                    reasonCode: evt.ReasonCode,
                    triggerSourceKind: evt.TriggerSourceKind,
                    outcomeKind: evt.OutcomeKind,
                    behaviorEntryNumericId: TestBehaviorCardNumericId,
                    transitionIndex: 0));
            }
            var index = new CharacterTestRuntimeAiLabTimelineEventIndex();
            index.Reset(10, 4);
            index.CaptureFrame(11, null, null, null, null, authoritative);

            CharacterTestRuntimeAiLabTimelineEventSample triggeredSample = default;
            bool found = false;
            for (int i = 0; i < index.GetCount(11); i++)
            {
                CharacterTestRuntimeAiLabTimelineEventSample sample = index.GetSample(11, i);
                if (sample.Kind == CharacterTestRuntimeAiLabTimelineEventKind.BehaviorDetectorTriggered
                    && string.Equals(sample.Label, "threat", StringComparison.Ordinal))
                {
                    triggeredSample = sample;
                    found = true;
                    break;
                }
            }
            Assert.IsTrue(found);
            Assert.AreEqual(
                CharacterTestAiEvidenceIds.Index(
                    CharacterTestAiEvidenceIds.TransitionKind,
                    0,
                    TestBehaviorCardNumericId),
                triggeredSample.SourceTraceId);
            Assert.IsTrue(CharacterTestRuntimeAiPlannerDevSceneController.TryConvertLogicTimelineSample(
                triggeredSample,
                out CharacterTestRuntimeAiLabLogicEvent restored));
            Assert.AreEqual("threat", restored.DetectorId);
            Assert.AreEqual("threat.to-dodge", restored.TransitionId);

            var restoredEvents = new CharacterTestRuntimeAiLabLogicEventBuffer();
            restoredEvents.Add(restored);
            var viewModel = new CharacterTestFairyGuiRuntimeAiPlannerDevPanelViewModel();
            CharacterTestRuntimeAiLabBehaviorCardWorkspaceViewModelBuilder.Build(
                viewModel,
                "Priority",
                compile,
                payload,
                selectedStageIndex: 0,
                runtimeState: new CharacterTestRuntimeAiLabLogicAgentStateSnapshot
                {
                    EntityId = 7,
                    ProgramSemanticHash = compile.Program.SemanticHash,
                    CurrentStageIndex = 0,
                    CardStartedFrame = 10,
                    StageEnteredFrame = 10,
                    DetectorArmed = new[] { true, true }
                },
                latestEvents: restoredEvents,
                runtimeFrame: 11,
                behaviorEntryId: "entry.priority");

            CharacterTestRuntimeAiLabBehaviorCardTimelineViewModel timeline =
                viewModel.BehaviorCardWorkspace.Timeline;
            Assert.IsTrue(timeline.Detectors[1].Triggered);
            Assert.IsTrue(timeline.Transitions[1].DetectorHit);
            Assert.IsFalse(timeline.Transitions[2].DetectorHit);
        }

        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        [TestCase(float.NegativeInfinity)]
        public void Validator_RejectsNonFiniteConditionValues(float invalid)
        {
            CharacterTestRuntimeAiLabBehaviorCardLogicPayload payload = CreatePriorityCard();
            payload.Detectors[0].Conditions[0].Fact =
                CharacterTestRuntimeAiLabCardConditionFact.TargetDistance;
            payload.Detectors[0].Conditions[0].Value = invalid;

            IReadOnlyList<CharacterTestRuntimeAiLabCardDiagnostic> diagnostics =
                CharacterTestRuntimeAiLabBehaviorCardLogicValidator.Validate(payload);

            Assert.IsTrue(ContainsCode(diagnostics, "CARD_V2_CONDITION_NON_FINITE"));
        }

        [Test]
        public void WorkspaceOverlayRejectsLocalIdsFromAnotherEntryOrProgram()
        {
            CharacterTestRuntimeAiLabBehaviorCardLogicPayload payload = CreatePriorityCard();
            CharacterTestRuntimeAiLabBehaviorCardLogicCompileResult compile =
                CharacterTestRuntimeAiLabBehaviorCardLogicCompiler.Compile(payload);
            var runtimeState = new CharacterTestRuntimeAiLabLogicAgentStateSnapshot
            {
                EntityId = 7,
                ProgramSemanticHash = compile.Program.SemanticHash,
                CurrentStageIndex = 0,
                CardStartedFrame = 10,
                StageEnteredFrame = 10,
                DetectorArmed = new[] { true, true }
            };
            var events = new CharacterTestRuntimeAiLabLogicEventBuffer();
            events.Add(new CharacterTestRuntimeAiLabLogicEvent(
                11,
                CharacterTestRuntimeAiLabLogicEventKind.DetectorTriggered,
                "approach",
                "threat",
                "threat.to-dodge",
                entityId: 7,
                behaviorEntryId: "entry.other",
                programSemanticHash: compile.Program.SemanticHash));
            var viewModel = new CharacterTestFairyGuiRuntimeAiPlannerDevPanelViewModel();

            CharacterTestRuntimeAiLabBehaviorCardWorkspaceViewModelBuilder.Build(
                viewModel,
                "Priority",
                compile,
                payload,
                selectedStageIndex: 0,
                runtimeState: runtimeState,
                latestEvents: events,
                runtimeFrame: 11,
                behaviorEntryId: "entry.priority");
            Assert.IsFalse(viewModel.BehaviorCardWorkspace.Timeline.Detectors[1].Triggered);
            Assert.IsFalse(viewModel.BehaviorCardWorkspace.Timeline.Transitions[1].DetectorHit);

            events.Clear();
            events.Add(new CharacterTestRuntimeAiLabLogicEvent(
                11,
                CharacterTestRuntimeAiLabLogicEventKind.DetectorTriggered,
                "approach",
                "threat",
                "threat.to-dodge",
                entityId: 7,
                behaviorEntryId: "entry.priority",
                programSemanticHash: compile.Program.SemanticHash + 1));
            CharacterTestRuntimeAiLabBehaviorCardWorkspaceViewModelBuilder.Build(
                viewModel,
                "Priority",
                compile,
                payload,
                selectedStageIndex: 0,
                runtimeState: runtimeState,
                latestEvents: events,
                runtimeFrame: 11,
                behaviorEntryId: "entry.priority");
            Assert.IsFalse(viewModel.BehaviorCardWorkspace.Timeline.Detectors[1].Triggered);
            Assert.IsFalse(viewModel.BehaviorCardWorkspace.Timeline.Transitions[1].DetectorHit);
        }

        [Test]
        public void CharacterActionRequestCommand_RequiresSchemaV2StartPayload()
        {
            var validator = new RuntimeCommandRegistryValidator(CharacterTestRuntimeCommandRegistry.Create());
            RuntimeCommand start = CharacterTestRuntimeCommandFactory.RequestCharacterAction(
                new RuntimeFrame(20),
                CharacterTestRuntimeCommandSources.RuntimeAiPlanner,
                targetEntityId: 7,
                characterActionNumericId: 1001,
                traceId: new RuntimeTraceIdentity(917653));
            var legacyCancel = new RuntimeCommand(
                new RuntimeFrame(21),
                CharacterTestRuntimeCommandSources.RuntimeAiPlanner,
                CharacterTestRuntimeCommandIds.RequestCharacterAction,
                targetId: 7,
                payload0: 0,
                payload1: 0,
                payload2: 1,
                traceId: new RuntimeTraceIdentity(943558));
            var invalid = new RuntimeCommand(
                new RuntimeFrame(22),
                CharacterTestRuntimeCommandSources.RuntimeAiPlanner,
                CharacterTestRuntimeCommandIds.RequestCharacterAction,
                targetId: 7,
                payload0: 1,
                payload1: 0,
                payload2: CharacterTestRuntimeCommandFactory.CharacterActionRequestSchemaVersion);

            Assert.IsTrue(validator.Validate(start).Success);
            Assert.IsFalse(validator.Validate(legacyCancel).Success);
            Assert.IsFalse(validator.Validate(invalid).Success);
        }

        [Test]
        public void BehaviorLogicSnapshot_ClonesAndContributesToAuthorityHash()
        {
            var source = new CharacterTestRuntimeAiPlannerStateSnapshot
            {
                SchemaVersion = CharacterTestRuntimeAiPlannerStateSnapshot.CurrentSchemaVersion,
                Slots = Array.Empty<CharacterTestRuntimeAiPlannerSlotStateSnapshot>(),
                BehaviorLogicSlots = new[]
                {
                    new CharacterTestRuntimeAiLabLogicSlotStateSnapshot
                    {
                        Slot = 2,
                        AuthorityHash = 1234L,
                        Agents = new[]
                        {
                            new CharacterTestRuntimeAiLabLogicAgentStateSnapshot
                            {
                                EntityId = 17,
                                ActiveBindingIndex = 1,
                                PendingBindingIndex = 2,
                                BindingReleasePending = true,
                                BehaviorCardStableId = TestBehaviorCardNumericId,
                                ProgramSemanticHash = 42L,
                                RuntimeState = (int)CharacterTestRuntimeAiLabLogicRuntimeState.Transitioning,
                                CurrentStageIndex = 2,
                                CurrentTimelineIndex = 3,
                                CardStartedFrame = 20,
                                StageEnteredFrame = 30,
                                TimelineEnteredFrame = 31,
                                LocalFrame = 9,
                                PendingTransitionIndex = 4,
                                PendingTimelineIndex = 5,
                                PendingEventId = TestPendingEventIndex,
                                LastTriggeredEventId = TestLastTriggeredEventIndex,
                                DetectorArmed = new[] { true, false },
                                TriggerArmed = new[] { true, false, true },
                                TriggerFired = new[] { false, true, false },
                                PrimaryActionState = (int)CharacterTestRuntimeAiLabLogicActionState.Running,
                                PrimaryActionStageIndex = 1,
                                PrimaryActionTimelineIndex = 3,
                                PrimaryActionInstanceId = 7701L,
                                OrchestrationState = 2,
                                OrchestrationOperation = 1,
                                OrchestrationTargetTimelineIndex = 5,
                                OrchestrationResourceId = 88,
                                OrchestrationCommandPending = true,
                                OrchestrationCommandSubmitted = true,
                                OrchestrationRejectAfterCleanup = true,
                                OrchestrationPreviousInstanceId = 7601L,
                                OrchestrationFailureReasonCode = TestOrchestrationFailureReasonCode,
                                CancelRequested = true,
                                CommandPending = true,
                                ActionEnteredFrame = 30
                            }
                        }
                    }
                }
            };
            CharacterTestRuntimeAiPlannerStateSnapshot clone = source.CloneForRollback();
            clone.BehaviorLogicSlots[0].Agents[0].TriggerArmed[0] = false;

            Assert.IsTrue(source.BehaviorLogicSlots[0].Agents[0].DetectorArmed[0]);
            Assert.IsTrue(source.BehaviorLogicSlots[0].Agents[0].TriggerArmed[0]);
            Assert.AreEqual(2, clone.BehaviorLogicSlots[0].Agents[0].PendingBindingIndex);
            Assert.IsTrue(clone.BehaviorLogicSlots[0].Agents[0].BindingReleasePending);
            Assert.AreEqual(3, clone.BehaviorLogicSlots[0].Agents[0].CurrentTimelineIndex);
            Assert.AreEqual(31, clone.BehaviorLogicSlots[0].Agents[0].TimelineEnteredFrame);
            Assert.AreEqual(9, clone.BehaviorLogicSlots[0].Agents[0].LocalFrame);
            Assert.AreEqual(5, clone.BehaviorLogicSlots[0].Agents[0].PendingTimelineIndex);
            Assert.AreEqual(TestPendingEventIndex, clone.BehaviorLogicSlots[0].Agents[0].PendingEventId);
            Assert.AreEqual(TestBehaviorCardNumericId, clone.BehaviorLogicSlots[0].Agents[0].BehaviorCardStableId);
            Assert.AreEqual(7701L, clone.BehaviorLogicSlots[0].Agents[0].PrimaryActionInstanceId);
            Assert.AreEqual(5, clone.BehaviorLogicSlots[0].Agents[0].OrchestrationTargetTimelineIndex);
            Assert.AreEqual(TestOrchestrationFailureReasonCode, clone.BehaviorLogicSlots[0].Agents[0].OrchestrationFailureReasonCode);
            var sourceHash = new RuntimeHashAccumulator(RuntimeHashSchemaIds.RuntimeAiPlannerState, 2);
            var cloneHash = new RuntimeHashAccumulator(RuntimeHashSchemaIds.RuntimeAiPlannerState, 2);
            CharacterTestRuntimeAiPlannerStateSnapshot.AddStateHash(sourceHash, source);
            CharacterTestRuntimeAiPlannerStateSnapshot.AddStateHash(cloneHash, clone);
            Assert.AreNotEqual(sourceHash.ToHash(), cloneHash.ToHash());
        }

        [Test]
        public void Validator_RejectsPreserveActionTargetingAnotherAction()
        {
            CharacterTestRuntimeAiLabBehaviorCardLogicPayload payload = CreatePriorityCard();
            payload.Transitions[0].Policy = CharacterTestRuntimeAiLabLogicTransitionPolicy.PreserveAction;

            IReadOnlyList<CharacterTestRuntimeAiLabCardDiagnostic> diagnostics =
                CharacterTestRuntimeAiLabBehaviorCardLogicValidator.Validate(payload);

            Assert.IsTrue(CharacterTestRuntimeAiLabBehaviorCardLogicValidator.HasErrors(diagnostics));
            Assert.IsTrue(ContainsCode(diagnostics, "CARD_V2_PRESERVE_ACTION_CONFLICT"));
        }

        [Test]
        public void Validator_RejectsDuplicateStableIdAndInvalidTargetWithPaths()
        {
            CharacterTestRuntimeAiLabBehaviorCardLogicPayload payload = CreatePriorityCard();
            payload.Stages.Add(Stage("attack", CharacterTestRuntimeAiLabCardStepKind.Logic));
            payload.Transitions[0].TargetStageId = "missing";

            IReadOnlyList<CharacterTestRuntimeAiLabCardDiagnostic> diagnostics =
                CharacterTestRuntimeAiLabBehaviorCardLogicValidator.Validate(payload);

            Assert.IsTrue(ContainsCode(diagnostics, "CARD_V2_STAGE_DUPLICATE"));
            Assert.IsTrue(ContainsCode(diagnostics, "CARD_V2_TRANSITION_TARGET"));
        }

        [Test]
        public void Evaluator_SameFrameChoosesHighestPriorityAndOnlyOneTransition()
        {
            CharacterTestRuntimeAiLabBehaviorCardLogicCompileResult compile =
                CharacterTestRuntimeAiLabBehaviorCardLogicCompiler.Compile(CreatePriorityCard());
            var runtime = new LogicRuntimeStub();
            runtime.Set(CharacterTestRuntimeAiLabCardConditionFact.TargetVisible, true);
            runtime.Set(CharacterTestRuntimeAiLabCardConditionFact.TargetAttackWillHitSelf, true);
            var evaluator = new CharacterTestRuntimeAiLabBehaviorCardLogicEvaluator(compile.Program, runtime);
            var events = new CharacterTestRuntimeAiLabLogicEventBuffer();

            Assert.IsTrue(evaluator.Start(10, events));
            runtime.PrimaryActionState = CharacterTestRuntimeAiLabLogicActionState.None;
            evaluator.Tick(11, events);

            Assert.AreEqual("dodge", compile.Program.Stages[evaluator.CurrentStageIndex].StageId);
            Assert.AreEqual(2, Count(events, CharacterTestRuntimeAiLabLogicEventKind.DetectorTriggered));
            Assert.AreEqual(1, Count(events, CharacterTestRuntimeAiLabLogicEventKind.TransitionCommitted));
            Assert.IsTrue(ContainsTransition(events, "threat.to-dodge"));
        }

        [Test]
        public void Evaluator_SameInputProducesIdenticalStateEventsAndSemanticHash()
        {
            CharacterTestRuntimeAiLabBehaviorCardLogicPayload firstPayload = CreatePriorityCard();
            CharacterTestRuntimeAiLabBehaviorCardLogicCompileResult firstCompile =
                CharacterTestRuntimeAiLabBehaviorCardLogicCompiler.Compile(firstPayload);
            CharacterTestRuntimeAiLabBehaviorCardLogicCompileResult secondCompile =
                CharacterTestRuntimeAiLabBehaviorCardLogicCompiler.Compile(firstPayload.Clone());
            var firstRuntime = new LogicRuntimeStub();
            var secondRuntime = new LogicRuntimeStub();
            firstRuntime.Set(CharacterTestRuntimeAiLabCardConditionFact.TargetVisible, true);
            firstRuntime.Set(CharacterTestRuntimeAiLabCardConditionFact.TargetAttackWillHitSelf, true);
            secondRuntime.Set(CharacterTestRuntimeAiLabCardConditionFact.TargetVisible, true);
            secondRuntime.Set(CharacterTestRuntimeAiLabCardConditionFact.TargetAttackWillHitSelf, true);
            var first = new CharacterTestRuntimeAiLabBehaviorCardLogicEvaluator(firstCompile.Program, firstRuntime);
            var second = new CharacterTestRuntimeAiLabBehaviorCardLogicEvaluator(secondCompile.Program, secondRuntime);
            var firstEvents = new CharacterTestRuntimeAiLabLogicEventBuffer();
            var secondEvents = new CharacterTestRuntimeAiLabLogicEventBuffer();

            first.Start(20, firstEvents);
            second.Start(20, secondEvents);
            firstRuntime.PrimaryActionState = CharacterTestRuntimeAiLabLogicActionState.None;
            secondRuntime.PrimaryActionState = CharacterTestRuntimeAiLabLogicActionState.None;
            first.Tick(21, firstEvents);
            second.Tick(21, secondEvents);

            Assert.AreEqual(firstCompile.Program.SemanticHash, secondCompile.Program.SemanticHash);
            Assert.AreEqual(first.CurrentStageIndex, second.CurrentStageIndex);
            Assert.AreEqual(first.State, second.State);
            Assert.AreEqual(firstRuntime.PrimaryActionState, secondRuntime.PrimaryActionState);
            Assert.AreEqual(firstEvents.Count, secondEvents.Count);
            for (int i = 0; i < firstEvents.Count; i++)
            {
                Assert.AreEqual(firstEvents[i].Kind, secondEvents[i].Kind);
                Assert.AreEqual(firstEvents[i].StageId, secondEvents[i].StageId);
                Assert.AreEqual(firstEvents[i].DetectorId, secondEvents[i].DetectorId);
                Assert.AreEqual(firstEvents[i].TransitionId, secondEvents[i].TransitionId);
            }
        }

        [Test]
        public void Evaluator_ConditionEdgeRearmsOnlyAfterFalse()
        {
            CharacterTestRuntimeAiLabBehaviorCardLogicPayload payload = CreateLoopCard();
            CharacterTestRuntimeAiLabBehaviorCardLogicCompileResult compile =
                CharacterTestRuntimeAiLabBehaviorCardLogicCompiler.Compile(payload);
            var runtime = new LogicRuntimeStub();
            runtime.Set(CharacterTestRuntimeAiLabCardConditionFact.TargetVisible, true);
            var evaluator = new CharacterTestRuntimeAiLabBehaviorCardLogicEvaluator(compile.Program, runtime);
            var events = new CharacterTestRuntimeAiLabLogicEventBuffer();

            evaluator.Start(1, events);
            evaluator.Tick(2, events);
            Assert.AreEqual("observe", compile.Program.Stages[evaluator.CurrentStageIndex].StageId);
            evaluator.Tick(3, events);
            Assert.AreEqual(0, Count(events, CharacterTestRuntimeAiLabLogicEventKind.TransitionCommitted));
            runtime.Set(CharacterTestRuntimeAiLabCardConditionFact.TargetVisible, false);
            evaluator.Tick(4, events);
            runtime.Set(CharacterTestRuntimeAiLabCardConditionFact.TargetVisible, true);
            evaluator.Tick(5, events);
            Assert.AreEqual("observe", compile.Program.Stages[evaluator.CurrentStageIndex].StageId);
            Assert.AreEqual(1, Count(events, CharacterTestRuntimeAiLabLogicEventKind.TransitionCommitted));
        }

        [Test]
        public void Evaluator_NewStageDetectorStartsOnFollowingRuntimeFrame()
        {
            var payload = new CharacterTestRuntimeAiLabBehaviorCardLogicPayload { EntryStageId = "a" };
            payload.Stages.Add(Stage("a", CharacterTestRuntimeAiLabCardStepKind.Logic));
            payload.Stages.Add(Stage("b", CharacterTestRuntimeAiLabCardStepKind.Logic));
            payload.Detectors.Add(Detector("a.visible", "a", 10, CharacterTestRuntimeAiLabCardConditionFact.TargetVisible));
            payload.Detectors.Add(Detector("b.threat", "b", 10, CharacterTestRuntimeAiLabCardConditionFact.TargetAttackWillHitSelf));
            payload.Transitions.Add(new CharacterTestRuntimeAiLabLogicTransition
            {
                TransitionId = "a.to-b",
                DetectorId = "a.visible",
                SourceStageId = "a",
                TargetStageId = "b",
                Priority = 10,
                Policy = CharacterTestRuntimeAiLabLogicTransitionPolicy.PreserveAction
            });
            payload.Transitions.Add(new CharacterTestRuntimeAiLabLogicTransition
            {
                TransitionId = "b.to-a",
                DetectorId = "b.threat",
                SourceStageId = "b",
                TargetStageId = "a",
                Priority = 10,
                Policy = CharacterTestRuntimeAiLabLogicTransitionPolicy.PreserveAction
            });
            CharacterTestRuntimeAiLabBehaviorCardLogicCompileResult compile =
                CharacterTestRuntimeAiLabBehaviorCardLogicCompiler.Compile(payload);
            Assert.IsTrue(compile.Success, FormatDiagnostics(compile.Diagnostics));
            var runtime = new LogicRuntimeStub();
            runtime.Set(CharacterTestRuntimeAiLabCardConditionFact.TargetVisible, true);
            runtime.Set(CharacterTestRuntimeAiLabCardConditionFact.TargetAttackWillHitSelf, true);
            var evaluator = new CharacterTestRuntimeAiLabBehaviorCardLogicEvaluator(compile.Program, runtime);
            var events = new CharacterTestRuntimeAiLabLogicEventBuffer();

            evaluator.Start(1, events);
            evaluator.Tick(2, events);
            Assert.AreEqual("b", compile.Program.Stages[evaluator.CurrentStageIndex].StageId);
            Assert.AreEqual(1, Count(events, CharacterTestRuntimeAiLabLogicEventKind.TransitionCommitted));
            evaluator.Tick(3, events);
            Assert.AreEqual("a", compile.Program.Stages[evaluator.CurrentStageIndex].StageId);
            Assert.AreEqual(1, Count(events, CharacterTestRuntimeAiLabLogicEventKind.TransitionCommitted));
        }

        [Test]
        public void Evaluator_InterruptWaitsForActionReleaseBeforeEnteringTarget()
        {
            CharacterTestRuntimeAiLabBehaviorCardLogicCompileResult compile =
                CharacterTestRuntimeAiLabBehaviorCardLogicCompiler.Compile(CreatePriorityCard());
            var runtime = new LogicRuntimeStub();
            runtime.Set(CharacterTestRuntimeAiLabCardConditionFact.TargetAttackWillHitSelf, true);
            var evaluator = new CharacterTestRuntimeAiLabBehaviorCardLogicEvaluator(compile.Program, runtime);
            var events = new CharacterTestRuntimeAiLabLogicEventBuffer();

            evaluator.Start(1, events);
            evaluator.Tick(2, events);

            Assert.AreEqual(CharacterTestRuntimeAiLabLogicRuntimeState.Transitioning, evaluator.State);
            Assert.AreEqual("approach", compile.Program.Stages[evaluator.CurrentStageIndex].StageId);
            Assert.AreEqual(1, runtime.InterruptRequests);
            runtime.PrimaryActionState = CharacterTestRuntimeAiLabLogicActionState.None;
            evaluator.Tick(3, events);
            Assert.AreEqual(CharacterTestRuntimeAiLabLogicRuntimeState.Running, evaluator.State);
            Assert.AreEqual("dodge", compile.Program.Stages[evaluator.CurrentStageIndex].StageId);
            Assert.AreEqual(0, runtime.ConcurrentActionStarts);
        }

        [Test]
        public void CoreScenario_DangerWinsThenDodgeBackJumpsAndEveryCommitHasRuntimeFrameMarker()
        {
            CharacterTestRuntimeAiLabBehaviorCardLogicPayload payload = CreatePriorityCard();
            payload.Detectors.Add(new CharacterTestRuntimeAiLabLogicDetector
            {
                DetectorId = "dodge.complete",
                Title = "闪避结束",
                Scope = CharacterTestRuntimeAiLabLogicDetectorScope.Stage,
                StageId = "dodge",
                Trigger = CharacterTestRuntimeAiLabLogicDetectorTrigger.ActionCompleted,
                Priority = 90
            });
            payload.Transitions.Add(new CharacterTestRuntimeAiLabLogicTransition
            {
                TransitionId = "dodge.back-to-approach",
                DetectorId = "dodge.complete",
                SourceStageId = "dodge",
                TargetStageId = "approach",
                Priority = 90,
                Policy = CharacterTestRuntimeAiLabLogicTransitionPolicy.AfterActionEnd
            });
            CharacterTestRuntimeAiLabBehaviorCardLogicCompileResult compile =
                CharacterTestRuntimeAiLabBehaviorCardLogicCompiler.Compile(payload);
            Assert.IsTrue(compile.Success, FormatDiagnostics(compile.Diagnostics));
            var runtime = new LogicRuntimeStub();
            runtime.Set(CharacterTestRuntimeAiLabCardConditionFact.TargetVisible, true);
            runtime.Set(CharacterTestRuntimeAiLabCardConditionFact.TargetAttackWillHitSelf, true);
            var evaluator = new CharacterTestRuntimeAiLabBehaviorCardLogicEvaluator(compile.Program, runtime);
            var events = new CharacterTestRuntimeAiLabLogicEventBuffer();
            var index = new CharacterTestRuntimeAiLabTimelineEventIndex();
            index.Reset(1, 16);

            Assert.IsTrue(evaluator.Start(1, events));
            evaluator.Tick(2, events);
            index.CaptureFrame(2, null, null, null, null, events);
            Assert.AreEqual(CharacterTestRuntimeAiLabLogicRuntimeState.Transitioning, evaluator.State);
            Assert.AreEqual(2, Count(events, CharacterTestRuntimeAiLabLogicEventKind.DetectorTriggered));
            Assert.AreEqual(1, runtime.InterruptRequests);

            runtime.PrimaryActionState = CharacterTestRuntimeAiLabLogicActionState.None;
            evaluator.Tick(3, events);
            index.CaptureFrame(3, null, null, null, null, events);
            Assert.AreEqual("dodge", compile.Program.Stages[evaluator.CurrentStageIndex].StageId);
            Assert.IsTrue(ContainsTransition(events, "threat.to-dodge"));
            Assert.AreEqual(0, runtime.ConcurrentActionStarts);

            runtime.Set(CharacterTestRuntimeAiLabCardConditionFact.TargetVisible, false);
            runtime.Set(CharacterTestRuntimeAiLabCardConditionFact.TargetAttackWillHitSelf, false);
            runtime.PrimaryActionState = CharacterTestRuntimeAiLabLogicActionState.Completed;
            evaluator.Tick(4, events);
            index.CaptureFrame(4, null, null, null, null, events);
            Assert.AreEqual("approach", compile.Program.Stages[evaluator.CurrentStageIndex].StageId);
            Assert.IsTrue(ContainsTransition(events, "dodge.back-to-approach"));
            Assert.IsTrue(ContainsTimelineTransition(index, 3, "threat.to-dodge"));
            Assert.IsTrue(ContainsTimelineTransition(index, 4, "dodge.back-to-approach"));
        }

        [Test]
        public void Evaluator_SamePriorityUsesStableTransitionIdAsTieBreaker()
        {
            CharacterTestRuntimeAiLabBehaviorCardLogicPayload payload = CreatePriorityCard();
            payload.Detectors[1].Priority = 20;
            payload.Transitions[1].Priority = 20;
            CharacterTestRuntimeAiLabBehaviorCardLogicCompileResult compile =
                CharacterTestRuntimeAiLabBehaviorCardLogicCompiler.Compile(payload);
            var runtime = new LogicRuntimeStub();
            runtime.Set(CharacterTestRuntimeAiLabCardConditionFact.TargetVisible, true);
            runtime.Set(CharacterTestRuntimeAiLabCardConditionFact.TargetAttackWillHitSelf, true);
            var evaluator = new CharacterTestRuntimeAiLabBehaviorCardLogicEvaluator(compile.Program, runtime);
            var events = new CharacterTestRuntimeAiLabLogicEventBuffer();

            evaluator.Start(1, events);
            runtime.PrimaryActionState = CharacterTestRuntimeAiLabLogicActionState.None;
            evaluator.Tick(2, events);

            Assert.AreEqual("attack", compile.Program.Stages[evaluator.CurrentStageIndex].StageId);
            Assert.IsTrue(ContainsTransition(events, "distance.to-attack"));
            Assert.AreEqual(1, Count(events, CharacterTestRuntimeAiLabLogicEventKind.TransitionCommitted));
        }

        [Test]
        public void Evaluator_AfterActionEndKeepsPendingUntilPrimaryActionCompletes()
        {
            CharacterTestRuntimeAiLabBehaviorCardLogicPayload payload = CreatePriorityCard();
            payload.Transitions[1].Policy = CharacterTestRuntimeAiLabLogicTransitionPolicy.AfterActionEnd;
            CharacterTestRuntimeAiLabBehaviorCardLogicCompileResult compile =
                CharacterTestRuntimeAiLabBehaviorCardLogicCompiler.Compile(payload);
            var runtime = new LogicRuntimeStub();
            runtime.Set(CharacterTestRuntimeAiLabCardConditionFact.TargetAttackWillHitSelf, true);
            var evaluator = new CharacterTestRuntimeAiLabBehaviorCardLogicEvaluator(compile.Program, runtime);
            var events = new CharacterTestRuntimeAiLabLogicEventBuffer();

            evaluator.Start(4, events);
            evaluator.Tick(5, events);
            Assert.AreEqual(CharacterTestRuntimeAiLabLogicRuntimeState.Transitioning, evaluator.State);
            Assert.AreEqual(0, runtime.InterruptRequests);
            Assert.AreEqual("approach", compile.Program.Stages[evaluator.CurrentStageIndex].StageId);
            runtime.PrimaryActionState = CharacterTestRuntimeAiLabLogicActionState.Completed;
            evaluator.Tick(6, events);

            Assert.AreEqual("dodge", compile.Program.Stages[evaluator.CurrentStageIndex].StageId);
            Assert.AreEqual(CharacterTestRuntimeAiLabLogicRuntimeState.Running, evaluator.State);
        }

        [Test]
        public void Evaluator_PreserveActionChangesLogicalStageWithoutStartingAnotherAction()
        {
            CharacterTestRuntimeAiLabBehaviorCardLogicPayload payload = CreatePriorityCard();
            payload.Stages[1].Kind = CharacterTestRuntimeAiLabCardStepKind.Wait;
            payload.Stages[1].CharacterActionId = MxFramework.CharacterAction.CharacterActionId.None;
            payload.Stages[1].WaitFrames = 1;
            payload.Transitions[0].Policy = CharacterTestRuntimeAiLabLogicTransitionPolicy.PreserveAction;
            payload.Detectors.RemoveAt(1);
            payload.Transitions.RemoveAt(1);
            payload.Stages.RemoveAt(2);
            CharacterTestRuntimeAiLabBehaviorCardLogicCompileResult compile =
                CharacterTestRuntimeAiLabBehaviorCardLogicCompiler.Compile(payload);
            Assert.IsTrue(compile.Success, FormatDiagnostics(compile.Diagnostics));
            var runtime = new LogicRuntimeStub();
            runtime.Set(CharacterTestRuntimeAiLabCardConditionFact.TargetVisible, true);
            var evaluator = new CharacterTestRuntimeAiLabBehaviorCardLogicEvaluator(compile.Program, runtime);
            var events = new CharacterTestRuntimeAiLabLogicEventBuffer();

            evaluator.Start(7, events);
            evaluator.Tick(8, events);

            Assert.AreEqual("attack", compile.Program.Stages[evaluator.CurrentStageIndex].StageId);
            Assert.AreEqual(CharacterTestRuntimeAiLabLogicActionState.Running, runtime.PrimaryActionState);
            Assert.AreEqual(0, runtime.ConcurrentActionStarts);
            Assert.AreEqual(0, runtime.InterruptRequests);
        }

        [Test]
        public void Validator_RejectsUnreachableStageButAllowsBackEdgeLoop()
        {
            CharacterTestRuntimeAiLabBehaviorCardLogicPayload payload = CreateLoopCard();
            payload.Stages.Add(Stage("orphan", CharacterTestRuntimeAiLabCardStepKind.Wait));

            IReadOnlyList<CharacterTestRuntimeAiLabCardDiagnostic> diagnostics =
                CharacterTestRuntimeAiLabBehaviorCardLogicValidator.Validate(payload);

            Assert.IsTrue(ContainsCode(diagnostics, "CARD_V2_STAGE_UNREACHABLE"));
            payload.Stages.RemoveAt(payload.Stages.Count - 1);
            diagnostics = CharacterTestRuntimeAiLabBehaviorCardLogicValidator.Validate(payload);
            Assert.IsFalse(CharacterTestRuntimeAiLabBehaviorCardLogicValidator.HasErrors(diagnostics));
        }

        [Test]
        public void EvaluatorSnapshot_RestoresPendingTransitionAndDetectorArming()
        {
            CharacterTestRuntimeAiLabBehaviorCardLogicCompileResult compile =
                CharacterTestRuntimeAiLabBehaviorCardLogicCompiler.Compile(CreatePriorityCard());
            var firstRuntime = new LogicRuntimeStub();
            firstRuntime.Set(CharacterTestRuntimeAiLabCardConditionFact.TargetAttackWillHitSelf, true);
            var first = new CharacterTestRuntimeAiLabBehaviorCardLogicEvaluator(compile.Program, firstRuntime);
            var events = new CharacterTestRuntimeAiLabLogicEventBuffer();
            first.Start(10, events);
            first.Tick(11, events);
            CharacterTestRuntimeAiLabLogicEvaluatorSnapshot snapshot = first.CreateSnapshot();

            var restoredRuntime = new LogicRuntimeStub
            {
                PrimaryActionState = CharacterTestRuntimeAiLabLogicActionState.None
            };
            restoredRuntime.Set(CharacterTestRuntimeAiLabCardConditionFact.TargetAttackWillHitSelf, true);
            var restored = new CharacterTestRuntimeAiLabBehaviorCardLogicEvaluator(compile.Program, restoredRuntime);

            Assert.IsTrue(restored.TryRestoreSnapshot(snapshot));
            restored.Tick(12, events);
            Assert.AreEqual("dodge", compile.Program.Stages[restored.CurrentStageIndex].StageId);
            Assert.AreEqual(1, Count(events, CharacterTestRuntimeAiLabLogicEventKind.TransitionCommitted));
        }

        [Test]
        public void LogicTimelineView_OrdersTransitionsByPriorityAndShowsLogicalAxis()
        {
            CharacterTestRuntimeAiLabBehaviorCardLogicPayload payload = CreatePriorityCard();
            CharacterTestRuntimeAiLabBehaviorCardLogicCompileResult compile =
                CharacterTestRuntimeAiLabBehaviorCardLogicCompiler.Compile(payload);
            var viewModel = new CharacterTestFairyGuiRuntimeAiPlannerDevPanelViewModel();

            CharacterTestRuntimeAiLabBehaviorCardWorkspaceViewModelBuilder.Build(
                viewModel,
                "Priority",
                compile,
                payload,
                selectedStageIndex: 0);

            CharacterTestRuntimeAiLabBehaviorCardTimelineViewModel timeline =
                viewModel.BehaviorCardWorkspace.Timeline;
            StringAssert.Contains("横轴按秒/帧表达计划时长", timeline.SummaryText);
            StringAssert.Contains("P100", timeline.Stages[0].TransitionSummaryText);
            StringAssert.Contains("P20", timeline.Stages[0].TransitionSummaryText);
            StringAssert.Contains("唯一主动作", timeline.Stages[0].ActionText);
        }

        [Test]
        public void LogicTimelineView_ProjectsAllDynamicTracksAndRuntimeState()
        {
            CharacterTestRuntimeAiLabBehaviorCardLogicPayload payload = CreatePriorityCard();
            CharacterTestRuntimeAiLabBehaviorCardLogicCompileResult compile =
                CharacterTestRuntimeAiLabBehaviorCardLogicCompiler.Compile(payload);
            var viewModel = new CharacterTestFairyGuiRuntimeAiPlannerDevPanelViewModel();
            var runtime = new CharacterTestRuntimeAiLabLogicAgentStateSnapshot
            {
                EntityId = 7,
                ActiveBindingIndex = 0,
                ProgramSemanticHash = compile.Program.SemanticHash,
                CurrentStageIndex = 1,
                PendingTransitionIndex = 0,
                DetectorArmed = new[] { true, false },
                PrimaryActionState = (int)CharacterTestRuntimeAiLabLogicActionState.Running,
                PrimaryActionStageIndex = 0
            };
            var events = new CharacterTestRuntimeAiLabLogicEventBuffer();
            events.Add(new CharacterTestRuntimeAiLabLogicEvent(
                12,
                CharacterTestRuntimeAiLabLogicEventKind.DetectorTriggered,
                "approach",
                "threat",
                "threat.to-dodge",
                entityId: 7,
                behaviorEntryId: "entry.priority",
                programSemanticHash: compile.Program.SemanticHash));
            events.Add(new CharacterTestRuntimeAiLabLogicEvent(
                12,
                CharacterTestRuntimeAiLabLogicEventKind.TransitionCommitted,
                "approach",
                "threat",
                "threat.to-dodge",
                entityId: 7,
                behaviorEntryId: "entry.priority",
                programSemanticHash: compile.Program.SemanticHash));

            CharacterTestRuntimeAiLabBehaviorCardWorkspaceViewModelBuilder.Build(
                viewModel,
                "Priority",
                compile,
                payload,
                selectedStageIndex: 1,
                selectedElementKind: CharacterTestRuntimeAiLabLogicElementKind.Detector,
                runtimeState: runtime,
                latestEvents: events,
                runtimeFrame: 12,
                behaviorEntryId: "entry.priority");

            CharacterTestRuntimeAiLabBehaviorCardTimelineViewModel timeline =
                viewModel.BehaviorCardWorkspace.Timeline;
            Assert.IsTrue(timeline.Available);
            Assert.AreEqual(compile.Program.Stages.Count, timeline.StageCount);
            Assert.AreEqual(compile.Program.Detectors.Count, timeline.DetectorCount);
            Assert.AreEqual(compile.Program.Transitions.Count, timeline.TransitionCount);
            // A typed commit on the selected RuntimeFrame is authoritative over the
            // pre-commit snapshot captured at that frame boundary.
            Assert.IsTrue(timeline.Stages[2].Current);
            Assert.IsTrue(timeline.Stages[0].ActionRunning);
            Assert.AreEqual(0, timeline.Detectors[1].PriorityRank);
            Assert.IsTrue(timeline.Detectors[1].Triggered);
            Assert.IsTrue(timeline.Transitions[0].Pending);
            Assert.AreEqual(12, timeline.Transitions[1].RuntimeFrame);
            Assert.AreEqual(CharacterTestRuntimeAiLabLogicElementKind.Detector, timeline.SelectedElementKind);
        }

        [Test]
        public void LogicTimelineView_InvalidPreserveActionDraft_RemainsVisibleWithConflictSpan()
        {
            CharacterTestRuntimeAiLabBehaviorCardLogicPayload payload = CreatePriorityCard();
            payload.Transitions[0].Policy = CharacterTestRuntimeAiLabLogicTransitionPolicy.PreserveAction;
            CharacterTestRuntimeAiLabBehaviorCardLogicCompileResult compile =
                CharacterTestRuntimeAiLabBehaviorCardLogicCompiler.Compile(payload);
            var viewModel = new CharacterTestFairyGuiRuntimeAiPlannerDevPanelViewModel();

            CharacterTestRuntimeAiLabBehaviorCardWorkspaceViewModelBuilder.Build(
                viewModel,
                "Invalid",
                compile,
                payload,
                selectedStageIndex: 0);

            Assert.IsFalse(compile.Success);
            CharacterTestRuntimeAiLabBehaviorCardTimelineViewModel timeline =
                viewModel.BehaviorCardWorkspace.Timeline;
            Assert.IsTrue(timeline.Available);
            Assert.IsTrue(timeline.HasBlockingDiagnostics);
            Assert.IsTrue(timeline.Stages[0].ActionConflict);
            Assert.IsTrue(timeline.Stages[1].ActionConflict);
            StringAssert.Contains("主动作冲突", timeline.Transitions[0].PolicyText);
            Assert.IsTrue(timeline.Transitions[0].Invalid);
            Assert.IsNotEmpty(timeline.DiagnosticsText);
        }

        [Test]
        public void LogicTimelineView_MissingDetectorCondition_AutoFocusesRepairOnce()
        {
            CharacterTestRuntimeAiLabBehaviorCardLogicPayload payload = CreatePriorityCard();
            payload.Detectors[0].Trigger = CharacterTestRuntimeAiLabLogicDetectorTrigger.ConditionEdge;
            payload.Detectors[0].Conditions.Clear();
            CharacterTestRuntimeAiLabBehaviorCardLogicCompileResult compile =
                CharacterTestRuntimeAiLabBehaviorCardLogicCompiler.Compile(payload);
            var viewModel = new CharacterTestFairyGuiRuntimeAiPlannerDevPanelViewModel();

            CharacterTestRuntimeAiLabBehaviorCardWorkspaceViewModelBuilder.Build(
                viewModel,
                "Missing condition",
                compile,
                payload,
                selectedStageIndex: 0);

            CharacterTestRuntimeAiLabBehaviorCardTimelineViewModel timeline =
                viewModel.BehaviorCardWorkspace.Timeline;
            Assert.That(compile.Success, Is.False);
            Assert.That(timeline.SelectedElementKind,
                Is.EqualTo(CharacterTestRuntimeAiLabLogicElementKind.Detector));
            Assert.That(timeline.SelectedElementIndex, Is.EqualTo(0));
            Assert.That(timeline.Detectors[0].Selected, Is.True);
            StringAssert.Contains("添加条件", timeline.RuntimeStatusText);

            CharacterTestRuntimeAiLabBehaviorCardWorkspaceViewModelBuilder.Build(
                viewModel,
                "Missing condition",
                compile,
                payload,
                selectedStageIndex: 0,
                selectedElementKind: CharacterTestRuntimeAiLabLogicElementKind.Stage);

            Assert.That(timeline.SelectedElementKind,
                Is.EqualTo(CharacterTestRuntimeAiLabLogicElementKind.Stage),
                "Once the diagnostic has been focused, an explicit user selection must be preserved.");
            Assert.That(timeline.Stages[0].Selected, Is.True);
        }

        [Test]
        public void MovementResolver_RetreatWhenClose_StopsAtThreshold()
        {
            CharacterTestRuntimeAiLabCompiledLogicStage stage = CompileMovementStage(
                CharacterTestRuntimeAiLabMovementStrategy.RetreatWhenClose,
                2.5f,
                4f);
            var close = new CharacterTestRuntimeAiLabMovementTargetSample(
                true,
                2f,
                Vector2.right,
                Vector2.right);
            var safe = new CharacterTestRuntimeAiLabMovementTargetSample(
                true,
                3f,
                Vector2.right,
                Vector2.right);

            Assert.IsTrue(CharacterTestRuntimeAiLabMovementResolver.IsEligible(stage, close));
            Assert.AreEqual(Vector2.left, CharacterTestRuntimeAiLabMovementResolver.Resolve(stage, close, 1));
            Assert.IsFalse(CharacterTestRuntimeAiLabMovementResolver.IsEligible(stage, safe));
            Assert.AreEqual(Vector2.zero, CharacterTestRuntimeAiLabMovementResolver.Resolve(stage, safe, 1));
        }

        [Test]
        public void MovementResolver_DistanceBand_ApproachesRetreatsOrYieldsExclusively()
        {
            CharacterTestRuntimeAiLabCompiledLogicStage stage = CompileMovementStage(
                CharacterTestRuntimeAiLabMovementStrategy.MaintainDistance,
                2.5f,
                4f);
            var near = new CharacterTestRuntimeAiLabMovementTargetSample(
                true,
                2f,
                Vector2.right,
                Vector2.right);
            var inside = new CharacterTestRuntimeAiLabMovementTargetSample(
                true,
                3f,
                Vector2.right,
                Vector2.right);
            var far = new CharacterTestRuntimeAiLabMovementTargetSample(
                true,
                5f,
                Vector2.right,
                Vector2.right);

            Assert.AreEqual(Vector2.left, CharacterTestRuntimeAiLabMovementResolver.Resolve(stage, near, 1));
            Assert.AreEqual(Vector2.zero, CharacterTestRuntimeAiLabMovementResolver.Resolve(stage, inside, 1));
            Assert.AreEqual(Vector2.right, CharacterTestRuntimeAiLabMovementResolver.Resolve(stage, far, 1));
            Assert.IsFalse(CharacterTestRuntimeAiLabMovementResolver.IsEligible(stage, inside));
        }

        [Test]
        public void MovementResolver_StrafeWhenBlocked_IsTrulyLateralAndDeterministic()
        {
            CharacterTestRuntimeAiLabCompiledLogicStage stage = CompileMovementStage(
                CharacterTestRuntimeAiLabMovementStrategy.StrafeWhenBlocked,
                0f,
                999f);
            var target = new CharacterTestRuntimeAiLabMovementTargetSample(
                true,
                5f,
                Vector2.right,
                Vector2.right);

            Vector2 first = CharacterTestRuntimeAiLabMovementResolver.Resolve(stage, target, 17);
            Vector2 repeated = CharacterTestRuntimeAiLabMovementResolver.Resolve(stage, target, 17);

            Assert.That(Mathf.Abs(first.x), Is.LessThan(0.0001f));
            Assert.That(Mathf.Abs(first.y), Is.EqualTo(1f).Within(0.0001f));
            Assert.AreEqual(first, repeated);
        }

        [Test]
        public void MovementResolver_OrbitTarget_AddsRadialCorrectionOutsideBand()
        {
            CharacterTestRuntimeAiLabCompiledLogicStage stage = CompileMovementStage(
                CharacterTestRuntimeAiLabMovementStrategy.OrbitTarget,
                2.5f,
                4f);
            var inside = new CharacterTestRuntimeAiLabMovementTargetSample(
                true,
                3f,
                Vector2.right,
                Vector2.right);
            var far = new CharacterTestRuntimeAiLabMovementTargetSample(
                true,
                5f,
                Vector2.right,
                Vector2.right);

            Vector2 tangent = CharacterTestRuntimeAiLabMovementResolver.Resolve(stage, inside, 1);
            Vector2 corrected = CharacterTestRuntimeAiLabMovementResolver.Resolve(stage, far, 1);

            Assert.That(Mathf.Abs(tangent.x), Is.LessThan(0.0001f));
            Assert.That(corrected.x, Is.GreaterThan(0f));
            Assert.That(Mathf.Abs(corrected.y), Is.GreaterThan(0f));
            Assert.That(corrected.magnitude, Is.LessThanOrEqualTo(1.0001f));
        }

        [Test]
        public void MovementTargetContext_ChangesSemanticIdentityAndRejectsNegativeEntityId()
        {
            CharacterTestRuntimeAiLabBehaviorCardLogicPayload current = CreateMovementPayload(
                CharacterTestRuntimeAiLabMovementStrategy.ApproachTarget,
                0.5f,
                0.5f);
            CharacterTestRuntimeAiLabBehaviorCardLogicPayload position = current.Clone();
            position.Stages[0].MovementTargetKind = CharacterTestRuntimeAiLabMovementTargetKind.WorldPosition;
            position.Stages[0].MoveTargetPositionX = 3f;
            position.Stages[0].MoveTargetPositionZ = -2f;
            CharacterTestRuntimeAiLabBehaviorCardLogicPayload entity = current.Clone();
            entity.Stages[0].MovementTargetKind = CharacterTestRuntimeAiLabMovementTargetKind.Entity;
            entity.Stages[0].MoveTargetEntityId = 0;

            Assert.AreNotEqual(
                CharacterTestRuntimeAiLabBehaviorCardLogicCompiler.ComputeSemanticHash(current),
                CharacterTestRuntimeAiLabBehaviorCardLogicCompiler.ComputeSemanticHash(position));
            Assert.AreNotEqual(
                CharacterTestRuntimeAiLabBehaviorCardLogicCompiler.ComputeSemanticHash(current),
                CharacterTestRuntimeAiLabBehaviorCardLogicCompiler.ComputeSemanticHash(entity));

            entity.Stages[0].MoveTargetEntityId = -1;
            Assert.IsTrue(ContainsCode(
                CharacterTestRuntimeAiLabBehaviorCardLogicValidator.Validate(entity),
                "CARD_V4_MOVEMENT_TARGET_ENTITY"));
        }

        [Test]
        public void CurrentMovementJsonRoundTrip_PreservesAuthoredStrategy()
        {
            CharacterTestRuntimeAiLabBehaviorCardLogicPayload source = CreateCurrentMovementRecipePayload(
                CharacterTestRuntimeAiLabMovementStrategy.OrbitTarget,
                2.5f,
                4f);

            CharacterTestRuntimeAiLabBehaviorCardLogicPayload restored =
                CharacterTestRuntimeAiLabBehaviorCardLogicJson.Deserialize(
                    CharacterTestRuntimeAiLabBehaviorCardLogicJson.Serialize(source));

            Assert.AreEqual(
                CharacterTestRuntimeAiLabMovementStrategy.OrbitTarget,
                restored.Timelines[0].Events[0].Operation.MovementStrategy);
        }

        private static CharacterTestRuntimeAiLabCompiledLogicStage CompileMovementStage(
            CharacterTestRuntimeAiLabMovementStrategy strategy,
            float minimum,
            float maximum)
        {
            CharacterTestRuntimeAiLabBehaviorCardLogicCompileResult compile =
                CharacterTestRuntimeAiLabBehaviorCardLogicCompiler.Compile(
                    CreateMovementPayload(strategy, minimum, maximum));
            Assert.IsTrue(compile.Success, FormatDiagnostics(compile.Diagnostics));
            return compile.Program.Stages[0];
        }

        private static CharacterTestRuntimeAiLabBehaviorCardLogicPayload CreateMovementPayload(
            CharacterTestRuntimeAiLabMovementStrategy strategy,
            float minimum,
            float maximum)
        {
            var payload = new CharacterTestRuntimeAiLabBehaviorCardLogicPayload
            {
                EntryStageId = "move"
            };
            payload.Stages.Add(new CharacterTestRuntimeAiLabLogicStage
            {
                StageId = "move",
                Order = 10,
                Kind = CharacterTestRuntimeAiLabCardStepKind.MoveToTarget,
                Title = "Move",
                MovementStrategy = strategy,
                MoveMinDistance = minimum,
                MoveMaxDistance = maximum,
                DurationMode = CharacterTestRuntimeAiLabLogicDurationMode.Infinite
            });
            return payload;
        }

        private static CharacterTestRuntimeAiLabBehaviorCardLogicPayload CreateCurrentMovementRecipePayload(
            CharacterTestRuntimeAiLabMovementStrategy strategy,
            float minimum,
            float maximum)
        {
            var payload = new CharacterTestRuntimeAiLabBehaviorCardLogicPayload
            {
                BehaviorRecipeVersion = 9,
                EntryTimelineId = "timeline.move"
            };
            var timeline = new CharacterTestRuntimeAiLabBehaviorRecipeTimeline
            {
                TimelineId = "timeline.move",
                Order = 0,
                Title = "Move",
            };
            timeline.Events.Add(new CharacterTestRuntimeAiLabBehaviorRecipeEvent
            {
                EventId = "event.move",
                TimelineId = timeline.TimelineId,
                Title = "Move",
                StartFrame = 0,
                DurationFrames = 60,
                MaxExecutionsPerPass = 1,
                Priority = 100,
                Operation = new CharacterTestRuntimeAiLabBehaviorRecipeEventOperation
                {
                    Kind = CharacterTestRuntimeAiLabBehaviorRecipeEventOperationKind.MoveToTarget,
                    MovementStrategy = strategy,
                    MovementTargetKind = CharacterTestRuntimeAiLabMovementTargetKind.CurrentCombatTarget,
                    MoveMinDistance = minimum,
                    MoveMaxDistance = maximum,
                }
            });
            payload.Timelines.Add(timeline);
            return payload;
        }

        private static CharacterTestRuntimeAiLabBehaviorCardRuntimeIdentityResolution CreateHistoricalRuntime(
            CharacterTestRuntimeAiLabBehaviorCardLogicCompileResult compile,
            int selectedFrame,
            int cardStartedFrame)
        {
            var card = new CharacterTestRuntimeAiLabBehaviorCardDraft(
                null,
                "behavior-card.test.history",
                "History",
                "Test",
                new CharacterTestRuntimeAiLabBehaviorCardPayload())
            {
                LogicPayload = CreatePriorityCard(),
                LogicPayloadMigratedFromV1 = false
            };
            var runtimeEntry = new CharacterTestRuntimeAiPlannerBehaviorModuleEntry(
                CharacterTestRuntimeAiPlannerBehaviorCompositionCatalog.FoundationIdle,
                enabled: true,
                order: 0);
            var entry = new CharacterTestRuntimeAiLabBehaviorEntryDraft(
                null,
                card.CardDraftId,
                runtimeEntry.BehaviorId,
                0,
                disabled: false,
                runtimeEntry);
            Assert.That(
                CharacterTestRuntimeAiLabRevisionCardIdentity.TryCreate(
                    0,
                    entry,
                    card,
                    compile.Program,
                    out CharacterTestRuntimeAiLabRevisionCardIdentity identity,
                    out string diagnostic),
                Is.True,
                diagnostic);
            return new CharacterTestRuntimeAiLabBehaviorCardRuntimeIdentityResolution
            {
                Status = CharacterTestRuntimeAiLabIdentityResolutionStatus.Resolved,
                SelectedEntityId = 7,
                Frame = selectedFrame,
                ProfileSlot = 2,
                CommittedCard = identity,
                CommittedProgram = compile.Program,
                CurrentStageId = compile.Program.Stages[0].StageId,
                LogicAgent = new CharacterTestRuntimeAiLabLogicAgentStateSnapshot
                {
                    EntityId = 7,
                    ActiveBindingIndex = 0,
                    ProgramSemanticHash = compile.Program.SemanticHash,
                    CurrentStageIndex = 0,
                    CardStartedFrame = cardStartedFrame,
                    StageEnteredFrame = cardStartedFrame,
                    DetectorArmed = new bool[compile.Program.Detectors.Count]
                }
            };
        }

        private static CharacterTestRuntimeAiLabLogicEvent CreateCommittedEvent(
            CharacterTestRuntimeAiLabBehaviorCardLogicCompileResult compile,
            string behaviorEntryId,
            int frame,
            int profileSlot,
            int cardStartedFrame)
        {
            return new CharacterTestRuntimeAiLabLogicEvent(
                frame,
                CharacterTestRuntimeAiLabLogicEventKind.TransitionCommitted,
                "approach",
                "threat",
                "threat.to-dodge",
                entityId: 7,
                behaviorEntryId: behaviorEntryId,
                programSemanticHash: compile.Program.SemanticHash,
                profileSlot: profileSlot,
                cardInstanceStartFrame: cardStartedFrame);
        }

        private static CharacterTestRuntimeAiLabBehaviorCardLogicPayload CreatePriorityCard()
        {
            var payload = new CharacterTestRuntimeAiLabBehaviorCardLogicPayload
            {
                EntryStageId = "approach"
            };
            payload.Stages.Add(Stage("approach", CharacterTestRuntimeAiLabCardStepKind.MoveToTarget));
            payload.Stages.Add(Stage("attack", CharacterTestRuntimeAiLabCardStepKind.CharacterAction));
            payload.Stages.Add(Stage("dodge", CharacterTestRuntimeAiLabCardStepKind.CharacterAction));
            payload.Layout.Stages.Add(new CharacterTestRuntimeAiLabLogicStageLayout { StageId = "approach" });

            payload.Detectors.Add(Detector(
                "distance",
                "approach",
                20,
                CharacterTestRuntimeAiLabCardConditionFact.TargetVisible));
            payload.Detectors.Add(Detector(
                "threat",
                "approach",
                100,
                CharacterTestRuntimeAiLabCardConditionFact.TargetAttackWillHitSelf));
            payload.Transitions.Add(new CharacterTestRuntimeAiLabLogicTransition
            {
                TransitionId = "distance.to-attack",
                DetectorId = "distance",
                SourceStageId = "approach",
                TargetStageId = "attack",
                Priority = 20,
                Policy = CharacterTestRuntimeAiLabLogicTransitionPolicy.Interrupt
            });
            payload.Transitions.Add(new CharacterTestRuntimeAiLabLogicTransition
            {
                TransitionId = "threat.to-dodge",
                DetectorId = "threat",
                SourceStageId = "approach",
                TargetStageId = "dodge",
                Priority = 100,
                Policy = CharacterTestRuntimeAiLabLogicTransitionPolicy.Interrupt
            });
            return payload;
        }

        private static CharacterTestRuntimeAiLabBehaviorCardLogicPayload CreateLoopCard()
        {
            var payload = new CharacterTestRuntimeAiLabBehaviorCardLogicPayload
            {
                EntryStageId = "observe"
            };
            payload.Stages.Add(Stage("observe", CharacterTestRuntimeAiLabCardStepKind.Wait));
            payload.Detectors.Add(Detector(
                "visible",
                "observe",
                10,
                CharacterTestRuntimeAiLabCardConditionFact.TargetVisible));
            payload.Transitions.Add(new CharacterTestRuntimeAiLabLogicTransition
            {
                TransitionId = "observe.loop",
                DetectorId = "visible",
                SourceStageId = "observe",
                TargetStageId = "observe",
                Priority = 10,
                Policy = CharacterTestRuntimeAiLabLogicTransitionPolicy.PreserveAction
            });
            return payload;
        }

        private static CharacterTestRuntimeAiLabLogicStage Stage(
            string id,
            CharacterTestRuntimeAiLabCardStepKind kind)
        {
            return new CharacterTestRuntimeAiLabLogicStage
            {
                StageId = id,
                Title = id,
                Kind = kind,
                CharacterActionId = kind == CharacterTestRuntimeAiLabCardStepKind.CharacterAction
                    ? CharacterActionLegacyFixtureIds.From(id)
                    : CharacterActionId.None,
                WaitFrames = kind == CharacterTestRuntimeAiLabCardStepKind.Wait ? 1 : 0
            };
        }

        private static CharacterTestRuntimeAiLabLogicDetector Detector(
            string id,
            string stageId,
            int priority,
            CharacterTestRuntimeAiLabCardConditionFact fact)
        {
            var detector = new CharacterTestRuntimeAiLabLogicDetector
            {
                DetectorId = id,
                Title = id,
                Scope = CharacterTestRuntimeAiLabLogicDetectorScope.Stage,
                StageId = stageId,
                Trigger = CharacterTestRuntimeAiLabLogicDetectorTrigger.ConditionEdge,
                Priority = priority
            };
            detector.Conditions.Add(new CharacterTestRuntimeAiLabCardCondition
            {
                ConditionId = id + ".condition",
                Fact = fact,
                Operator = CharacterTestRuntimeAiLabCardConditionOperator.Equals,
                BoolValue = true
            });
            return detector;
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

        private static bool ContainsTransition(
            CharacterTestRuntimeAiLabLogicEventBuffer events,
            string transitionId)
        {
            for (int i = 0; i < events.Count; i++)
            {
                if (string.Equals(events[i].TransitionId, transitionId, StringComparison.Ordinal))
                    return true;
            }
            return false;
        }

        private static bool ContainsTimelineTransition(
            CharacterTestRuntimeAiLabTimelineEventIndex index,
            int frame,
            string transitionId)
        {
            for (int i = 0; i < index.GetCount(frame); i++)
            {
                CharacterTestRuntimeAiLabTimelineEventSample sample = index.GetSample(frame, i);
                if (sample.Kind == CharacterTestRuntimeAiLabTimelineEventKind.BehaviorTransitionCommitted
                    && string.Equals(sample.Label, transitionId, StringComparison.Ordinal))
                {
                    return true;
                }
            }
            return false;
        }

        private static bool ContainsCode(
            IReadOnlyList<CharacterTestRuntimeAiLabCardDiagnostic> diagnostics,
            string code)
        {
            for (int i = 0; i < diagnostics.Count; i++)
            {
                if (string.Equals(diagnostics[i].Code, code, StringComparison.Ordinal))
                    return true;
            }
            return false;
        }

        private static string FormatDiagnostics(
            IReadOnlyList<CharacterTestRuntimeAiLabCardDiagnostic> diagnostics)
        {
            var lines = new string[diagnostics.Count];
            for (int i = 0; i < diagnostics.Count; i++)
                lines[i] = diagnostics[i].Code + " " + diagnostics[i].Path + " " + diagnostics[i].Message;
            return string.Join("\n", lines);
        }

        private sealed class LogicRuntimeStub :
            ICharacterTestRuntimeAiLabBehaviorCardLogicRuntime,
            ICharacterTestRuntimeAiLabBehaviorCardLogicTraceSink
        {
            private readonly Dictionary<CharacterTestRuntimeAiLabCardConditionFact, bool> _facts =
                new Dictionary<CharacterTestRuntimeAiLabCardConditionFact, bool>();

            public CharacterTestRuntimeAiLabLogicActionState PrimaryActionState { get; set; }
            public int InterruptRequests { get; private set; }
            public int ConcurrentActionStarts { get; private set; }
            public List<string> BegunLogicCandidates { get; } = new List<string>();
            public List<string> SelectedLogicCandidates { get; } = new List<string>();

            public void BeginLogicCandidate(CharacterTestRuntimeAiLabLogicCandidateKind kind, int index, int stableRank)
            {
                BegunLogicCandidates.Add(kind + ":" + index);
            }

            public void EndLogicCandidate(
                CharacterTestRuntimeAiLabDecisionTraceCandidateState state,
                int priority,
                int reasonCode)
            {
            }

            public void MarkLogicCandidateSelected(CharacterTestRuntimeAiLabLogicCandidateKind kind, int index, int reasonCode)
            {
                SelectedLogicCandidates.Add(kind + ":" + index);
            }

            public void RecordStageActionCandidate(int stageIndex, bool selected, int reasonCode)
            {
            }

            public void Set(CharacterTestRuntimeAiLabCardConditionFact fact, bool value)
            {
                _facts[fact] = value;
            }

            public bool Evaluate(in CharacterTestRuntimeAiLabCompiledCondition condition)
            {
                bool value = _facts.TryGetValue(condition.Fact, out bool current) && current;
                return condition.Operator == CharacterTestRuntimeAiLabCardConditionOperator.NotEquals
                    ? value != condition.BoolValue
                    : value == condition.BoolValue;
            }

            public bool TryEnterStage(CharacterTestRuntimeAiLabCompiledLogicStage stage)
            {
                if (!stage.OwnsPrimaryAction)
                    return true;
                if (PrimaryActionState == CharacterTestRuntimeAiLabLogicActionState.Running)
                {
                    ConcurrentActionStarts++;
                    return false;
                }
                PrimaryActionState = CharacterTestRuntimeAiLabLogicActionState.Running;
                return true;
            }

            public bool TryInterruptPrimaryAction()
            {
                InterruptRequests++;
                return true;
            }
        }
    }
}
