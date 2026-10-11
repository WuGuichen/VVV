using System;
using System.Collections.Generic;
using MxFramework.Gameplay;
using NUnit.Framework;

namespace MxFramework.Tests.Ability
{
    public sealed class AbilityRuntimeGraphPhaseTimelineTests
    {
        [Test]
        public void Advance_ByFrameSequence_IsDeterministic()
        {
            AbilityGraphTimelineDefinition timeline = CreateStandardTimeline();
            int[] ticks = { 1, 1, 2, 1, 1 };

            string[] firstRun = RunTimeline(timeline, ticks);
            string[] secondRun = RunTimeline(timeline, ticks);

            CollectionAssert.AreEqual(firstRun, secondRun);
            Assert.AreEqual("prepare:1:1:0:0", firstRun[0]);
            Assert.AreEqual("active:0:2:0:1", firstRun[1]);
            Assert.AreEqual("active:2:4:0:0", firstRun[2]);
            Assert.AreEqual("recovery:0:5:0:1", firstRun[3]);
            Assert.AreEqual("recovery:1:6:1:0", firstRun[4]);
        }

        [Test]
        public void Advance_ZeroDurationPhase_TransitionsWithoutConsumingFrame()
        {
            var timeline = new AbilityGraphTimelineDefinition(
                "zero",
                "prepare",
                new[]
                {
                    new AbilityGraphPhaseDefinition("prepare", 0, "active"),
                    new AbilityGraphPhaseDefinition("active", 2),
                });
            AbilityGraphTimelineState state = AbilityGraphTimelineScheduler.CreateInitialState(timeline);

            AbilityGraphTimelineAdvanceResult result = AbilityGraphTimelineScheduler.Advance(timeline, state, 0);

            Assert.AreEqual(AbilityGraphTimelineAdvanceStatus.Advanced, result.Status);
            Assert.AreEqual(0, result.ConsumedFrames);
            Assert.AreEqual(1, result.TransitionCount);
            Assert.AreEqual("prepare", result.Transitions[0].FromPhaseId.Value);
            Assert.AreEqual("active", result.Transitions[0].ToPhaseId.Value);
            Assert.AreEqual(0, result.Transitions[0].ConsumedFrames);
            Assert.AreEqual("active", result.EndState.CurrentPhaseId.Value);
            Assert.AreEqual(0, result.EndState.TotalElapsedFrames);
            Assert.IsTrue(AbilityGraphPhaseGate.HasReachedPhase(timeline, result.EndState, "prepare"));
            Assert.IsTrue(AbilityGraphPhaseGate.HasReachedPhase(timeline, result.EndState, "active"));
        }

        [Test]
        public void Validate_InvalidPhaseReference_ReturnsStructuredError()
        {
            var timeline = new AbilityGraphTimelineDefinition(
                "invalid-reference",
                "prepare",
                new[]
                {
                    new AbilityGraphPhaseDefinition("prepare", 1, "missing"),
                });

            AbilityGraphTimelineValidationResult result = timeline.Validate();

            AssertSingleError(result, AbilityGraphTimelineValidationErrorCode.InvalidPhaseReference);
            Assert.AreEqual("prepare", result.Errors[0].PhaseId);
            Assert.AreEqual("missing", result.Errors[0].ReferencedPhaseId);
            Assert.AreEqual(0, result.Errors[0].PhaseIndex);
            Assert.AreEqual("Phases[prepare].NextPhaseId", result.Errors[0].FieldPath);
        }

        [Test]
        public void Validate_DuplicatePhase_ReturnsStructuredError()
        {
            var timeline = new AbilityGraphTimelineDefinition(
                "duplicate",
                "prepare",
                new[]
                {
                    new AbilityGraphPhaseDefinition("prepare", 1),
                    new AbilityGraphPhaseDefinition("prepare", 2),
                });

            AbilityGraphTimelineValidationResult result = timeline.Validate();

            AssertSingleError(result, AbilityGraphTimelineValidationErrorCode.DuplicatePhaseId);
            Assert.AreEqual("prepare", result.Errors[0].PhaseId);
            Assert.AreEqual(1, result.Errors[0].PhaseIndex);
            Assert.AreEqual("Phases[1].PhaseId", result.Errors[0].FieldPath);
        }

        [Test]
        public void Validate_MissingEntry_ReturnsStructuredError()
        {
            var timeline = new AbilityGraphTimelineDefinition(
                "missing-entry",
                "missing",
                new[]
                {
                    new AbilityGraphPhaseDefinition("prepare", 1),
                });

            AbilityGraphTimelineValidationResult result = timeline.Validate();

            AssertSingleError(result, AbilityGraphTimelineValidationErrorCode.MissingEntryPhase);
            Assert.AreEqual("missing", result.Errors[0].PhaseId);
            Assert.AreEqual("EntryPhaseId", result.Errors[0].FieldPath);
        }

        [Test]
        public void Validate_NegativeDuration_ReturnsStructuredError()
        {
            var timeline = new AbilityGraphTimelineDefinition(
                "negative-duration",
                "prepare",
                new[]
                {
                    new AbilityGraphPhaseDefinition("prepare", -1),
                });

            AbilityGraphTimelineValidationResult result = timeline.Validate();

            AssertSingleError(result, AbilityGraphTimelineValidationErrorCode.NegativePhaseDuration);
            Assert.AreEqual("prepare", result.Errors[0].PhaseId);
            Assert.AreEqual(0, result.Errors[0].PhaseIndex);
            Assert.AreEqual("Phases[prepare].DurationFrames", result.Errors[0].FieldPath);
        }

        [Test]
        public void Validate_Cycle_ReturnsStructuredErrorAndSchedulerRejectsTimeline()
        {
            var timeline = new AbilityGraphTimelineDefinition(
                "cycle",
                "prepare",
                new[]
                {
                    new AbilityGraphPhaseDefinition("prepare", 1, "active"),
                    new AbilityGraphPhaseDefinition("active", 1, "prepare"),
                });

            AbilityGraphTimelineValidationResult validation = timeline.Validate();
            AbilityGraphTimelineState state = AbilityGraphTimelineScheduler.CreateInitialState(timeline);
            AbilityGraphTimelineAdvanceResult advance = AbilityGraphTimelineScheduler.Advance(timeline, state, 10, transitionBudget: 2);

            AssertSingleError(validation, AbilityGraphTimelineValidationErrorCode.CycleDetected);
            Assert.AreEqual("prepare", validation.Errors[0].PhaseId);
            Assert.AreEqual("active", validation.Errors[0].ReferencedPhaseId);
            Assert.AreEqual(AbilityGraphTimelineAdvanceStatus.InvalidTimeline, advance.Status);
        }

        [Test]
        public void PhaseGate_OpensOnlyAfterTargetPhaseReached()
        {
            var timeline = new AbilityGraphTimelineDefinition(
                "gate",
                "prepare",
                new[]
                {
                    new AbilityGraphPhaseDefinition("prepare", 1, "active"),
                    new AbilityGraphPhaseDefinition("active", 2, "recovery"),
                    new AbilityGraphPhaseDefinition("recovery", 1),
                });
            AbilityGraphTimelineState state = AbilityGraphTimelineScheduler.CreateInitialState(timeline);
            var payload = new AbilityGraphPhaseGatePayload("active");

            Assert.IsFalse(AbilityGraphPhaseGate.IsOpen(timeline, state, payload));

            AbilityGraphTimelineAdvanceResult reachedActive = AbilityGraphTimelineScheduler.Advance(timeline, state, 1);

            Assert.AreEqual("active", reachedActive.EndState.CurrentPhaseId.Value);
            Assert.IsTrue(AbilityGraphPhaseGate.IsOpen(timeline, reachedActive.EndState, payload));
            Assert.IsFalse(AbilityGraphPhaseGate.IsOpen(timeline, reachedActive.EndState, new AbilityGraphPhaseGatePayload("recovery")));
        }

        [Test]
        public void TimelinePhaseGate_ImplementsExecutorPhaseGateInterface()
        {
            var timeline = new AbilityGraphTimelineDefinition(
                "gate-adapter",
                "prepare",
                new[]
                {
                    new AbilityGraphPhaseDefinition("prepare", 1, "active"),
                    new AbilityGraphPhaseDefinition("active", 1),
                });
            AbilityGraphTimelineState state = AbilityGraphTimelineScheduler.CreateInitialState(timeline);
            AbilityGraphTimelineAdvanceResult reachedActive = AbilityGraphTimelineScheduler.Advance(timeline, state, 1);

            IAbilityGraphPhaseGate initialGate = new AbilityGraphTimelinePhaseGate(timeline, state);
            IAbilityGraphPhaseGate activeGate = new AbilityGraphTimelinePhaseGate(timeline, reachedActive.EndState);

            Assert.IsFalse(initialGate.IsPhaseActive("active"));
            Assert.IsTrue(activeGate.IsPhaseActive("active"));
        }

        private static AbilityGraphTimelineDefinition CreateStandardTimeline()
        {
            return new AbilityGraphTimelineDefinition(
                "standard",
                "prepare",
                new[]
                {
                    new AbilityGraphPhaseDefinition("prepare", 2, "active"),
                    new AbilityGraphPhaseDefinition("active", 3, "recovery"),
                    new AbilityGraphPhaseDefinition("recovery", 1),
                });
        }

        private static string[] RunTimeline(AbilityGraphTimelineDefinition timeline, IReadOnlyList<int> ticks)
        {
            var states = new List<string>();
            AbilityGraphTimelineState state = AbilityGraphTimelineScheduler.CreateInitialState(timeline);
            for (int i = 0; i < ticks.Count; i++)
            {
                AbilityGraphTimelineAdvanceResult result = AbilityGraphTimelineScheduler.Advance(timeline, state, ticks[i]);
                state = result.EndState;
                states.Add(state.CurrentPhaseId.Value
                    + ":"
                    + state.ElapsedFramesInPhase
                    + ":"
                    + state.TotalElapsedFrames
                    + ":"
                    + (state.IsCompleted ? 1 : 0)
                    + ":"
                    + result.TransitionCount);
            }

            return states.ToArray();
        }

        private static void AssertSingleError(
            AbilityGraphTimelineValidationResult result,
            AbilityGraphTimelineValidationErrorCode code)
        {
            Assert.IsFalse(result.IsValid);
            Assert.AreEqual(1, result.ErrorCount);
            Assert.AreEqual(code, result.Errors[0].Code);
        }
    }
}
