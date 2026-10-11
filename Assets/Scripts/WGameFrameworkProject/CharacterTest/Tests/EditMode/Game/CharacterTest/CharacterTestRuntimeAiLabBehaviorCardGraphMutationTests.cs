using NUnit.Framework;

namespace MxFramework.Game.CharacterTest.Tests
{
    public sealed class CharacterTestRuntimeAiLabBehaviorCardGraphMutationTests
    {
        [Test]
        public void AddDetectorCreatesCanonicalDefaultsAndUniqueIds()
        {
            CharacterTestRuntimeAiLabBehaviorCardLogicPayload payload = CreatePayload();

            Assert.That(CharacterTestRuntimeAiLabBehaviorCardGraphMutation.TryAddDetector(
                payload,
                CharacterTestRuntimeAiLabLogicDetectorScope.Stage,
                "stage.a",
                out string firstId,
                out string diagnostic), Is.True, diagnostic);
            Assert.That(CharacterTestRuntimeAiLabBehaviorCardGraphMutation.TryAddDetector(
                payload,
                CharacterTestRuntimeAiLabLogicDetectorScope.Card,
                string.Empty,
                out string secondId,
                out diagnostic), Is.True, diagnostic);

            Assert.That(firstId, Is.Not.EqualTo(secondId));
            Assert.That(payload.Detectors[0].WindowEndMode,
                Is.EqualTo(CharacterTestRuntimeAiLabLogicDetectorWindowEndMode.StageEnd));
            Assert.That(payload.Detectors[1].WindowEndMode,
                Is.EqualTo(CharacterTestRuntimeAiLabLogicDetectorWindowEndMode.Infinite));
            Assert.That(payload.Detectors[0].Conditions, Has.Count.EqualTo(1));
            Assert.That(payload.Detectors[1].Priority, Is.EqualTo(payload.Detectors[0].Priority + 10));
        }

        [Test]
        public void AddDetectorRejectsPriorityExhaustionWithoutReordering()
        {
            CharacterTestRuntimeAiLabBehaviorCardLogicPayload payload = CreatePayloadWithDetector();
            payload.Detectors[0].Priority = int.MaxValue - 9;
            payload.Transitions.Add(CreateTransition(payload.Detectors[0], "transition.a", 37));

            Assert.That(CharacterTestRuntimeAiLabBehaviorCardGraphMutation.TryAddDetector(
                payload,
                CharacterTestRuntimeAiLabLogicDetectorScope.Card,
                string.Empty,
                out _,
                out string diagnostic), Is.False);

            StringAssert.Contains("DETECTOR_PRIORITY_EXHAUSTED", diagnostic);
            Assert.That(payload.Detectors, Has.Count.EqualTo(1));
            Assert.That(payload.Transitions[0].Priority, Is.EqualTo(37));
        }

        [Test]
        public void DuplicateDetectorCopiesConditionsButNotTransitions()
        {
            CharacterTestRuntimeAiLabBehaviorCardLogicPayload payload = CreatePayloadWithDetector();
            payload.Transitions.Add(CreateTransition(payload.Detectors[0], "transition.a", 100));

            Assert.That(CharacterTestRuntimeAiLabBehaviorCardGraphMutation.TryDuplicateDetector(
                payload,
                payload.Detectors[0].DetectorId,
                out string copyId,
                out string diagnostic), Is.True, diagnostic);

            Assert.That(payload.Detectors, Has.Count.EqualTo(2));
            Assert.That(payload.Transitions, Has.Count.EqualTo(1));
            Assert.That(payload.Detectors[1].Conditions[0].ConditionId,
                Does.StartWith(copyId + ".condition."));
        }

        [Test]
        public void DeleteDetectorRequiresCascadeConfirmation()
        {
            CharacterTestRuntimeAiLabBehaviorCardLogicPayload payload = CreatePayloadWithDetector();
            payload.Transitions.Add(CreateTransition(payload.Detectors[0], "transition.a", 100));

            Assert.That(CharacterTestRuntimeAiLabBehaviorCardGraphMutation.TryDeleteDetector(
                payload,
                payload.Detectors[0].DetectorId,
                false,
                out int linked,
                out string diagnostic), Is.False);
            Assert.That(linked, Is.EqualTo(1));
            Assert.That(payload.Detectors, Has.Count.EqualTo(1));

            Assert.That(CharacterTestRuntimeAiLabBehaviorCardGraphMutation.TryDeleteDetector(
                payload,
                payload.Detectors[0].DetectorId,
                true,
                out linked,
                out diagnostic), Is.True, diagnostic);
            Assert.That(payload.Detectors, Is.Empty);
            Assert.That(payload.Transitions, Is.Empty);
        }

        [Test]
        public void DeleteNativeV5DetectorRemovesAuthoritativeTriggerEvent()
        {
            CharacterTestRuntimeAiLabBehaviorCardLogicPayload payload = CreatePayload();
            CharacterTestRuntimeAiLabBehaviorRecipeV5Migration.RebuildV5FromLegacy(payload);
            CharacterTestRuntimeAiLabBehaviorRecipeTimeline timeline = payload.Timelines[0];
            timeline.TriggerEvents.Add(new CharacterTestRuntimeAiLabBehaviorRecipeTriggerEvent
            {
                EventId = "event.delete-me",
                TimelineId = timeline.TimelineId,
                Title = "删除我",
                SourceKind = CharacterTestRuntimeAiLabBehaviorRecipeTriggerSourceKind.LocalFrame,
                LocalFrameAnchor = CharacterTestRuntimeAiLabBehaviorRecipeLocalFrameAnchor.AbsoluteFrame,
                LocalFrame = 10,
                OutcomeKind = CharacterTestRuntimeAiLabBehaviorRecipeOutcomeKind.ReturnToPlanner
            });
            CharacterTestRuntimeAiLabBehaviorRecipeV5Migration.RebuildLegacyProjection(payload);
            CharacterTestRuntimeAiLabBehaviorRecipeV5Migration.SynchronizeForRead(payload);
            const string detectorId = "event.event.delete-me";

            Assert.That(CharacterTestRuntimeAiLabBehaviorCardGraphMutation.TryDeleteDetector(
                payload,
                detectorId,
                true,
                out int linked,
                out string diagnostic), Is.True, diagnostic);

            Assert.That(linked, Is.EqualTo(1));
            Assert.That(timeline.TriggerEvents, Is.Empty,
                "The native v5 Trigger Event is the deletion authority, not its Detector projection.");
            Assert.That(payload.Detectors.Exists(candidate => candidate?.DetectorId == detectorId), Is.False);
            Assert.That(payload.Transitions.Exists(candidate => candidate?.TransitionId == "event.delete-me"), Is.False);

            CharacterTestRuntimeAiLabBehaviorRecipeV5Migration.SynchronizeForWrite(payload);
            CharacterTestRuntimeAiLabBehaviorRecipeV5Migration.RebuildLegacyProjection(payload);
            Assert.That(payload.Detectors.Exists(candidate => candidate?.DetectorId == detectorId), Is.False,
                "A write/read projection rebuild must not resurrect a deleted Trigger Event.");
        }

        [Test]
        public void TriggerChangeClearsDormantConditionsAndRecreatesDefault()
        {
            CharacterTestRuntimeAiLabBehaviorCardLogicPayload payload = CreatePayloadWithDetector();
            string detectorId = payload.Detectors[0].DetectorId;

            Assert.That(ApplyDetector(payload, detectorId,
                CharacterTestRuntimeAiLabDetectorFieldKind.Trigger,
                new CharacterTestRuntimeAiLabDetectorFieldValue(
                    trigger: CharacterTestRuntimeAiLabLogicDetectorTrigger.ActionCompleted)), Is.True);
            Assert.That(payload.Detectors[0].Conditions, Is.Empty);

            Assert.That(ApplyDetector(payload, detectorId,
                CharacterTestRuntimeAiLabDetectorFieldKind.Trigger,
                new CharacterTestRuntimeAiLabDetectorFieldValue(
                    trigger: CharacterTestRuntimeAiLabLogicDetectorTrigger.ConditionEdge)), Is.True);
            Assert.That(payload.Detectors[0].Conditions, Has.Count.EqualTo(1));
            Assert.That(payload.Detectors[0].Conditions[0].Fact,
                Is.EqualTo(CharacterTestRuntimeAiLabCardConditionFact.TargetVisible));
        }

        [Test]
        public void NativeV5ActionCompletedAdapterCreatesPrimaryActionEndLocalFrame()
        {
            CharacterTestRuntimeAiLabBehaviorCardLogicPayload payload = CreatePayload();
            payload.Stages[0].Kind = CharacterTestRuntimeAiLabCardStepKind.Wait;
            payload.Stages[0].WaitFrames = 30;
            CharacterTestRuntimeAiLabBehaviorRecipeV5Migration.RebuildV5FromLegacy(payload);
            CharacterTestRuntimeAiLabBehaviorRecipeV5Migration.RebuildLegacyProjection(payload);
            Assert.That(CharacterTestRuntimeAiLabBehaviorCardGraphMutation.TryAddDetector(
                payload,
                CharacterTestRuntimeAiLabLogicDetectorScope.Stage,
                "stage.a",
                out string detectorId,
                out string addDiagnostic), Is.True, addDiagnostic);

            Assert.That(ApplyDetector(
                payload,
                detectorId,
                CharacterTestRuntimeAiLabDetectorFieldKind.Trigger,
                new CharacterTestRuntimeAiLabDetectorFieldValue(
                    trigger: CharacterTestRuntimeAiLabLogicDetectorTrigger.ActionCompleted)),
                Is.True);

            CharacterTestRuntimeAiLabLogicDetector detector = payload.Detectors.Find(
                candidate => candidate != null && candidate.DetectorId == detectorId);
            Assert.NotNull(detector);
            Assert.AreEqual(CharacterTestRuntimeAiLabLogicDetectorTrigger.LocalFrame, detector.Trigger);
            Assert.AreEqual(
                CharacterTestRuntimeAiLabBehaviorRecipeLocalFrameAnchor.PrimaryActionEnd,
                detector.LocalFrameAnchor);
            Assert.AreEqual(0, detector.LocalFrame);
            Assert.IsFalse(detector.LegacyUnfilteredSignal);
        }

        [Test]
        public void NativeV5ActionCompletedAdapterRejectsOpenEndedPrimaryAction()
        {
            CharacterTestRuntimeAiLabBehaviorCardLogicPayload payload = CreatePayload();
            payload.Stages[0].Kind = CharacterTestRuntimeAiLabCardStepKind.MoveToTarget;
            CharacterTestRuntimeAiLabBehaviorRecipeV5Migration.RebuildV5FromLegacy(payload);
            CharacterTestRuntimeAiLabBehaviorRecipeV5Migration.RebuildLegacyProjection(payload);
            Assert.That(CharacterTestRuntimeAiLabBehaviorCardGraphMutation.TryAddDetector(
                payload,
                CharacterTestRuntimeAiLabLogicDetectorScope.Stage,
                "stage.a",
                out string detectorId,
                out string addDiagnostic), Is.True, addDiagnostic);

            Assert.That(CharacterTestRuntimeAiLabBehaviorCardGraphMutation.TryApplyDetectorField(
                payload,
                detectorId,
                CharacterTestRuntimeAiLabDetectorFieldKind.Trigger,
                new CharacterTestRuntimeAiLabDetectorFieldValue(
                    trigger: CharacterTestRuntimeAiLabLogicDetectorTrigger.ActionCompleted),
                out bool changed,
                out string diagnostic), Is.False);

            Assert.IsFalse(changed);
            StringAssert.Contains("concrete predicate", diagnostic);
        }

        [Test]
        public void StageOwnerChangeRewritesEveryLinkedTransitionSource()
        {
            CharacterTestRuntimeAiLabBehaviorCardLogicPayload payload = CreatePayloadWithDetector();
            payload.Transitions.Add(CreateTransition(payload.Detectors[0], "transition.a", 90));
            payload.Transitions.Add(CreateTransition(payload.Detectors[0], "transition.b", 80));

            Assert.That(ApplyDetector(payload, payload.Detectors[0].DetectorId,
                CharacterTestRuntimeAiLabDetectorFieldKind.OwningStage,
                new CharacterTestRuntimeAiLabDetectorFieldValue(stringValue: "stage.b")), Is.True);

            Assert.That(payload.Transitions[0].SourceStageId, Is.EqualTo("stage.b"));
            Assert.That(payload.Transitions[1].SourceStageId, Is.EqualTo("stage.b"));
        }

        [Test]
        public void StageEndDetectorCannotBecomeCardScopeImplicitly()
        {
            CharacterTestRuntimeAiLabBehaviorCardLogicPayload payload = CreatePayloadWithDetector();

            Assert.That(ApplyDetector(payload, payload.Detectors[0].DetectorId,
                CharacterTestRuntimeAiLabDetectorFieldKind.Scope,
                new CharacterTestRuntimeAiLabDetectorFieldValue(
                    scope: CharacterTestRuntimeAiLabLogicDetectorScope.Card)), Is.False);

            Assert.That(payload.Detectors[0].Scope,
                Is.EqualTo(CharacterTestRuntimeAiLabLogicDetectorScope.Stage));
        }

        [Test]
        public void WindowModesClearDormantDurationAndFixedUsesExplicitDefault()
        {
            CharacterTestRuntimeAiLabBehaviorCardLogicPayload payload = CreatePayloadWithDetector();
            CharacterTestRuntimeAiLabLogicDetector detector = payload.Detectors[0];

            Assert.That(ApplyDetector(payload, detector.DetectorId,
                CharacterTestRuntimeAiLabDetectorFieldKind.WindowEndMode,
                new CharacterTestRuntimeAiLabDetectorFieldValue(
                    intValue: 45,
                    windowEndMode: CharacterTestRuntimeAiLabLogicDetectorWindowEndMode.Fixed)), Is.True);
            Assert.That(detector.ActivationDurationFrames, Is.EqualTo(45));

            Assert.That(ApplyDetector(payload, detector.DetectorId,
                CharacterTestRuntimeAiLabDetectorFieldKind.WindowEndMode,
                new CharacterTestRuntimeAiLabDetectorFieldValue(
                    windowEndMode: CharacterTestRuntimeAiLabLogicDetectorWindowEndMode.Infinite)), Is.True);
            Assert.That(detector.ActivationDurationFrames, Is.Zero);

            Assert.That(ApplyDetector(payload, detector.DetectorId,
                CharacterTestRuntimeAiLabDetectorFieldKind.WindowEndMode,
                new CharacterTestRuntimeAiLabDetectorFieldValue(
                    windowEndMode: CharacterTestRuntimeAiLabLogicDetectorWindowEndMode.Fixed)), Is.True);
            Assert.That(detector.ActivationDurationFrames, Is.EqualTo(60));
        }

        [Test]
        public void LegacyWindowsRequireExplicitCanonicalization()
        {
            CharacterTestRuntimeAiLabBehaviorCardLogicPayload payload = CreatePayloadWithDetector();
            payload.Detectors[0].WindowEndMode =
                CharacterTestRuntimeAiLabLogicDetectorWindowEndMode.UntilEvent;
            payload.Detectors[0].ActivationDurationFrames = 77;

            Assert.That(CharacterTestRuntimeAiLabBehaviorCardGraphMutation.RequiresWindowCanonicalization(payload),
                Is.True);
            Assert.That(CharacterTestRuntimeAiLabBehaviorCardGraphMutation.TryCanonicalizeDetectorWindows(
                payload, out bool changed, out string diagnostic), Is.True, diagnostic);

            Assert.That(changed, Is.True);
            Assert.That(payload.Detectors[0].WindowEndMode,
                Is.EqualTo(CharacterTestRuntimeAiLabLogicDetectorWindowEndMode.Infinite));
            Assert.That(payload.Detectors[0].ActivationDurationFrames, Is.Zero);
            Assert.That(CharacterTestRuntimeAiLabBehaviorCardGraphMutation.RequiresWindowCanonicalization(payload),
                Is.False);
        }

        [TestCase(float.NaN, 1f)]
        [TestCase(float.PositiveInfinity, 1f)]
        [TestCase(0f, float.NegativeInfinity)]
        public void EditConditionRejectsNonFiniteValueAndRange(float minimum, float maximum)
        {
            CharacterTestRuntimeAiLabBehaviorCardLogicPayload payload = CreatePayloadWithDetector();
            CharacterTestRuntimeAiLabLogicDetector detector = payload.Detectors[0];
            CharacterTestRuntimeAiLabCardCondition condition = detector.Conditions[0];
            condition.Fact = CharacterTestRuntimeAiLabCardConditionFact.TargetDistance;
            float originalValue = condition.Value;
            float originalMaximum = condition.MaxValue;

            Assert.That(CharacterTestRuntimeAiLabBehaviorCardGraphMutation.TryApplyConditionField(
                payload,
                detector.DetectorId,
                condition.ConditionId,
                CharacterTestRuntimeAiLabConditionFieldKind.Range,
                new CharacterTestRuntimeAiLabConditionFieldValue(
                    floatValue: minimum,
                    secondFloatValue: maximum),
                out bool changed,
                out _), Is.False);
            Assert.That(changed, Is.False);
            Assert.That(condition.Value, Is.EqualTo(originalValue));
            Assert.That(condition.MaxValue, Is.EqualTo(originalMaximum));
        }

        [Test]
        public void DetectorPriorityPropagatesDeltaAndRejectsOverflowAtomically()
        {
            CharacterTestRuntimeAiLabBehaviorCardLogicPayload payload = CreatePayloadWithDetector();
            payload.Detectors[0].Priority = 100;
            payload.Transitions.Add(CreateTransition(payload.Detectors[0], "transition.a", 80));
            payload.Transitions.Add(CreateTransition(payload.Detectors[0], "transition.b", int.MaxValue));

            Assert.That(ApplyDetector(payload, payload.Detectors[0].DetectorId,
                CharacterTestRuntimeAiLabDetectorFieldKind.Priority,
                new CharacterTestRuntimeAiLabDetectorFieldValue(intValue: 110)), Is.False);
            Assert.That(payload.Detectors[0].Priority, Is.EqualTo(100));
            Assert.That(payload.Transitions[0].Priority, Is.EqualTo(80));

            payload.Transitions[1].Priority = 70;
            Assert.That(ApplyDetector(payload, payload.Detectors[0].DetectorId,
                CharacterTestRuntimeAiLabDetectorFieldKind.Priority,
                new CharacterTestRuntimeAiLabDetectorFieldValue(intValue: 110)), Is.True);
            Assert.That(payload.Transitions[0].Priority, Is.EqualTo(90));
            Assert.That(payload.Transitions[1].Priority, Is.EqualTo(80));
        }

        [Test]
        public void DetectorPriorityRejectsExtremeDeltaWrapAtomically()
        {
            CharacterTestRuntimeAiLabBehaviorCardLogicPayload payload = CreatePayloadWithDetector();
            payload.Detectors[0].Priority = int.MinValue;
            payload.Transitions.Add(CreateTransition(payload.Detectors[0], "transition.a", 0));

            Assert.That(ApplyDetector(payload, payload.Detectors[0].DetectorId,
                CharacterTestRuntimeAiLabDetectorFieldKind.Priority,
                new CharacterTestRuntimeAiLabDetectorFieldValue(intValue: int.MaxValue)), Is.False);

            Assert.That(payload.Detectors[0].Priority, Is.EqualTo(int.MinValue));
            Assert.That(payload.Transitions[0].Priority, Is.Zero);
        }

        [Test]
        public void DeleteLastConditionKeepsCorrectableDraftInvalidPayload()
        {
            CharacterTestRuntimeAiLabBehaviorCardLogicPayload payload = CreatePayloadWithDetector();
            string detectorId = payload.Detectors[0].DetectorId;
            string conditionId = payload.Detectors[0].Conditions[0].ConditionId;

            Assert.That(CharacterTestRuntimeAiLabBehaviorCardGraphMutation.TryDeleteCondition(
                payload, detectorId, conditionId, out string diagnostic), Is.True, diagnostic);

            Assert.That(payload.Detectors[0].Conditions, Is.Empty);
            Assert.That(CharacterTestRuntimeAiLabBehaviorCardLogicValidator.HasErrors(
                CharacterTestRuntimeAiLabBehaviorCardLogicValidator.Validate(payload)), Is.True);
        }

        [Test]
        public void FactChangeCanonicalizesTypedDormantFields()
        {
            CharacterTestRuntimeAiLabBehaviorCardLogicPayload payload = CreatePayloadWithDetector();
            CharacterTestRuntimeAiLabCardCondition condition = payload.Detectors[0].Conditions[0];
            condition.Value = 3f;
            condition.MaxValue = 9f;
            condition.AbilityId = 8;
            condition.CharacterActionId = MxFramework.Tests.CharacterActionLegacyFixtureIds.From("Old");

            Assert.That(CharacterTestRuntimeAiLabBehaviorCardGraphMutation.TryApplyConditionField(
                payload,
                payload.Detectors[0].DetectorId,
                condition.ConditionId,
                CharacterTestRuntimeAiLabConditionFieldKind.Fact,
                new CharacterTestRuntimeAiLabConditionFieldValue(
                    fact: CharacterTestRuntimeAiLabCardConditionFact.AbilityAvailable),
                out bool changed,
                out string diagnostic), Is.True, diagnostic);

            Assert.That(changed, Is.True);
            Assert.That(condition.AbilityId, Is.EqualTo(1));
            Assert.That(condition.CharacterActionId, Is.Empty);
            Assert.That(condition.Value, Is.Zero);
            Assert.That(condition.Operator,
                Is.EqualTo(CharacterTestRuntimeAiLabCardConditionOperator.Equals));
        }

        [Test]
        public void StageScopedTransitionIgnoresRequestedSourceAndPreserveConflictRemainsInvalid()
        {
            CharacterTestRuntimeAiLabBehaviorCardLogicPayload payload = CreatePayloadWithDetector();

            Assert.That(CharacterTestRuntimeAiLabBehaviorCardGraphMutation.TryAddTransition(
                payload,
                payload.Detectors[0].DetectorId,
                "stage.b",
                "stage.b",
                out string transitionId,
                out string diagnostic), Is.True, diagnostic);
            Assert.That(payload.Transitions[0].SourceStageId, Is.EqualTo("stage.a"));

            Assert.That(CharacterTestRuntimeAiLabBehaviorCardGraphMutation.TryApplyTransitionField(
                payload,
                transitionId,
                CharacterTestRuntimeAiLabTransitionFieldKind.Policy,
                new CharacterTestRuntimeAiLabTransitionFieldValue(
                    policy: CharacterTestRuntimeAiLabLogicTransitionPolicy.PreserveAction),
                out bool changed,
                out diagnostic), Is.True, diagnostic);
            Assert.That(changed, Is.True);
            Assert.That(CharacterTestRuntimeAiLabBehaviorCardLogicValidator.HasErrors(
                CharacterTestRuntimeAiLabBehaviorCardLogicValidator.Validate(payload)), Is.True);
        }

        [Test]
        public void CardScopedTransitionAcceptsAnyOrStableStageSource()
        {
            CharacterTestRuntimeAiLabBehaviorCardLogicPayload payload = CreatePayload();
            Assert.That(CharacterTestRuntimeAiLabBehaviorCardGraphMutation.TryAddDetector(
                payload,
                CharacterTestRuntimeAiLabLogicDetectorScope.Card,
                string.Empty,
                out string detectorId,
                out string diagnostic), Is.True, diagnostic);
            Assert.That(CharacterTestRuntimeAiLabBehaviorCardGraphMutation.TryAddTransition(
                payload, detectorId, string.Empty, "stage.b", out string transitionId, out diagnostic),
                Is.True, diagnostic);

            Assert.That(CharacterTestRuntimeAiLabBehaviorCardGraphMutation.TryApplyTransitionField(
                payload,
                transitionId,
                CharacterTestRuntimeAiLabTransitionFieldKind.SourceStage,
                new CharacterTestRuntimeAiLabTransitionFieldValue(stringValue: "stage.a"),
                out bool changed,
                out diagnostic), Is.True, diagnostic);
            Assert.That(changed, Is.True);
            Assert.That(payload.Transitions[0].SourceStageId, Is.EqualTo("stage.a"));
        }

        private static bool ApplyDetector(
            CharacterTestRuntimeAiLabBehaviorCardLogicPayload payload,
            string detectorId,
            CharacterTestRuntimeAiLabDetectorFieldKind field,
            in CharacterTestRuntimeAiLabDetectorFieldValue value)
        {
            return CharacterTestRuntimeAiLabBehaviorCardGraphMutation.TryApplyDetectorField(
                payload, detectorId, field, value, out _, out _);
        }

        private static CharacterTestRuntimeAiLabBehaviorCardLogicPayload CreatePayloadWithDetector()
        {
            CharacterTestRuntimeAiLabBehaviorCardLogicPayload payload = CreatePayload();
            Assert.That(CharacterTestRuntimeAiLabBehaviorCardGraphMutation.TryAddDetector(
                payload,
                CharacterTestRuntimeAiLabLogicDetectorScope.Stage,
                "stage.a",
                out _,
                out string diagnostic), Is.True, diagnostic);
            return payload;
        }

        private static CharacterTestRuntimeAiLabBehaviorCardLogicPayload CreatePayload()
        {
            var payload = new CharacterTestRuntimeAiLabBehaviorCardLogicPayload
            {
                EntryStageId = "stage.a"
            };
            payload.Stages.Add(new CharacterTestRuntimeAiLabLogicStage
            {
                StageId = "stage.a",
                Order = 0,
                Kind = CharacterTestRuntimeAiLabCardStepKind.Logic,
                Title = "A",
                DurationMode = CharacterTestRuntimeAiLabLogicDurationMode.Fixed,
                PlannedDurationFrames = 60
            });
            payload.Stages.Add(new CharacterTestRuntimeAiLabLogicStage
            {
                StageId = "stage.b",
                Order = 1,
                Kind = CharacterTestRuntimeAiLabCardStepKind.MoveToTarget,
                Title = "B",
                MoveMaxDistance = 3f,
                PlannedStartFrame = 60,
                DurationMode = CharacterTestRuntimeAiLabLogicDurationMode.Fixed,
                PlannedDurationFrames = 60
            });
            return payload;
        }

        private static CharacterTestRuntimeAiLabLogicTransition CreateTransition(
            CharacterTestRuntimeAiLabLogicDetector detector,
            string transitionId,
            int priority)
        {
            return new CharacterTestRuntimeAiLabLogicTransition
            {
                TransitionId = transitionId,
                DetectorId = detector.DetectorId,
                SourceStageId = detector.StageId,
                TargetStageId = "stage.b",
                Priority = priority,
                Policy = CharacterTestRuntimeAiLabLogicTransitionPolicy.Interrupt
            };
        }
    }
}
