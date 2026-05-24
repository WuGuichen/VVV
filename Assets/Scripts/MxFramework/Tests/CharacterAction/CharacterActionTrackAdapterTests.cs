using MxFramework.CharacterAction;
using MxFramework.Combat.Core;
using MxFramework.Gameplay;
using MxFramework.Runtime;
using NUnit.Framework;

namespace MxFramework.Tests.CharacterAction
{
    public sealed class CharacterActionTrackAdapterTests
    {
        [Test]
        public void Adapter_RoutesRunnerDispatchToMotionCombatAndGameplayRequestRecords()
        {
            CharacterActionConfig action = CreateAction(
                motionTrack: new MotionTrackConfig(false, new[]
                {
                    new MotionTrackEvent(
                        1,
                        CharacterActionTrackEventKind.SetMovementMode,
                        CharacterMovementMode.Run,
                        1f,
                        0f,
                        0f,
                        "motion.run"),
                }),
                combatTrack: new CombatTrackConfig("combat.light", new[]
                {
                    new CombatTrackEvent(
                        1,
                        CharacterActionTrackEventKind.StartCombatAction,
                        "combat.light",
                        stableEventId: "combat.start"),
                    new CombatTrackEvent(
                        1,
                        CharacterActionTrackEventKind.StartHitTrace,
                        traceProfileId: "trace.light",
                        stableEventId: "combat.trace.start"),
                }),
                gameplayTrack: new GameplayTrackConfig(new[]
                {
                    new GameplayTrackEvent(
                        1,
                        CharacterActionTrackEventKind.ApplyGameplayEffect,
                        requestId: "pressure.posture.light",
                        abilityStableId: "ability.light",
                        stableEventId: "gameplay.pressure"),
                }));
            CharacterActionRunner runner = StartRunner(action);
            CharacterActionRunnerOperationResult tick = runner.Tick();
            var collector = new CharacterActionAdapterRequestCollector();
            var adapter = new CharacterActionTrackAdapter(collector, collector, collector, collector);

            CharacterActionAdapterResult result = adapter.AdaptMany(tick.Events, CreateContext());

            Assert.IsTrue(result.Accepted);
            Assert.AreEqual(1, result.MotionRequestCount);
            Assert.AreEqual(2, result.CombatRequestCount);
            Assert.AreEqual(1, result.GameplayRequestCount);
            Assert.AreEqual(1, collector.MotionRequests.Count);
            Assert.AreEqual(2, collector.CombatRequests.Count);
            Assert.AreEqual(1, collector.GameplayRequests.Count);
            Assert.AreEqual(CharacterMovementMode.Run, collector.MotionRequests[0].MovementMode);
            Assert.AreEqual(new CombatBodyId(13), collector.MotionRequests[0].CombatBodyId);
            Assert.AreEqual(CharacterActionTrackEventKind.StartCombatAction, collector.CombatRequests[0].EventKind);
            Assert.AreEqual("combat.light", collector.CombatRequests[0].CombatActionId);
            Assert.AreEqual(CharacterActionTrackEventKind.StartHitTrace, collector.CombatRequests[1].EventKind);
            Assert.AreEqual("trace.light", collector.CombatRequests[1].TraceProfileId);
            Assert.AreEqual(CharacterActionTrackEventKind.ApplyGameplayEffect, collector.GameplayRequests[0].EventKind);
            Assert.AreEqual("pressure.posture.light", collector.GameplayRequests[0].RequestId);
            Assert.AreEqual(new GameplayEntityId(7, 1), collector.GameplayRequests[0].GameplayEntityId);
            Assert.AreEqual(new RuntimeFrame(42), collector.GameplayRequests[0].Metadata.Frame);
            Assert.AreEqual("trace.light_attack", collector.GameplayRequests[0].Metadata.TraceId);
        }

        [Test]
        public void Adapter_MissingCombatPayloadEmitsDiagnosticWithoutStartingCombatAuthority()
        {
            CharacterActionConfig action = CreateAction(
                combatTrack: new CombatTrackConfig(string.Empty, new[]
                {
                    new CombatTrackEvent(0, CharacterActionTrackEventKind.StartCombatAction, stableEventId: "combat.missing"),
                }));
            CharacterActionRunner runner = StartRunner(action);
            var collector = new CharacterActionAdapterRequestCollector();
            var adapter = new CharacterActionTrackAdapter(combatSink: collector);

            CharacterActionAdapterResult result = adapter.AdaptMany(runner.DrainEvents(), CreateContext());

            Assert.IsFalse(result.Accepted);
            Assert.AreEqual(0, result.CombatRequestCount);
            Assert.AreEqual(0, collector.CombatRequests.Count);
            AssertHasDiagnostic(result.Diagnostics, CharacterActionDiagnosticCodes.AdapterPayloadMissing);
        }

        [Test]
        public void Adapter_MissingGameplayRequestPayloadEmitsDiagnosticWithoutMutatingGameplayAuthority()
        {
            CharacterActionConfig action = CreateAction(
                gameplayTrack: new GameplayTrackConfig(new[]
                {
                    new GameplayTrackEvent(0, CharacterActionTrackEventKind.SendGameplayRequest, stableEventId: "gameplay.missing"),
                }));
            CharacterActionRunner runner = StartRunner(action);
            var collector = new CharacterActionAdapterRequestCollector();
            var adapter = new CharacterActionTrackAdapter(gameplaySink: collector);

            CharacterActionAdapterResult result = adapter.AdaptMany(runner.DrainEvents(), CreateContext());

            Assert.IsFalse(result.Accepted);
            Assert.AreEqual(0, result.GameplayRequestCount);
            Assert.AreEqual(0, collector.GameplayRequests.Count);
            AssertHasDiagnostic(result.Diagnostics, CharacterActionDiagnosticCodes.AdapterPayloadMissing);
        }

        [Test]
        public void Adapter_MissingSinkEmitsDiagnosticWithoutRequestRecord()
        {
            CharacterActionConfig action = CreateAction(
                motionTrack: new MotionTrackConfig(false, new[]
                {
                    new MotionTrackEvent(0, CharacterActionTrackEventKind.LockMovement, CharacterMovementMode.ControlLocked),
                }));
            CharacterActionRunner runner = StartRunner(action);
            var adapter = new CharacterActionTrackAdapter();

            CharacterActionAdapterResult result = adapter.AdaptMany(runner.DrainEvents(), CreateContext());

            Assert.IsFalse(result.Accepted);
            Assert.AreEqual(0, result.MotionRequestCount);
            AssertHasDiagnostic(result.Diagnostics, CharacterActionDiagnosticCodes.AdapterSinkMissing);
        }

        [Test]
        public void Adapter_SinkFailureEmitsDiagnosticWithoutCountingRequest()
        {
            CharacterActionConfig action = CreateAction(
                motionTrack: new MotionTrackConfig(false, new[]
                {
                    new MotionTrackEvent(0, CharacterActionTrackEventKind.LockMovement, CharacterMovementMode.ControlLocked),
                }));
            CharacterActionRunner runner = StartRunner(action);
            var adapter = new CharacterActionTrackAdapter(motionSink: new ThrowingMotionSink());

            CharacterActionAdapterResult result = adapter.AdaptMany(runner.DrainEvents(), CreateContext());

            Assert.IsFalse(result.Accepted);
            Assert.AreEqual(0, result.MotionRequestCount);
            AssertHasDiagnostic(result.Diagnostics, CharacterActionDiagnosticCodes.AdapterSinkFailure);
        }

        [Test]
        public void Adapter_RecordsPressureOnlyReactionRequestSeam()
        {
            CharacterReactionContext context = CharacterReactionContextBuilder.FromPostureBreak(
                new PostureBreakEvent(
                    new RuntimeFrame(9),
                    new GameplayEntityId(7, 1),
                    PressureBand.Critical,
                    previousValue: 90,
                    currentPressure: 100,
                    maxPressure: 100,
                    delta: 10,
                    traceId: "trace.pressure")).Context;
            var collector = new CharacterActionAdapterRequestCollector();
            var adapter = new CharacterActionTrackAdapter(pressureOnlyReactionSink: collector);

            CharacterActionAdapterResult result = adapter.SubmitPressureOnlyReaction(context, "posture_break_react");

            Assert.IsTrue(result.Accepted);
            Assert.AreEqual(1, result.PressureOnlyReactionRequestCount);
            Assert.AreEqual(1, collector.PressureOnlyReactionRequests.Count);
            Assert.AreEqual(CharacterReactionContextCompleteness.PressureOnly, collector.PressureOnlyReactionRequests[0].Context.Completeness);
            Assert.AreEqual("posture_break_react", collector.PressureOnlyReactionRequests[0].RequestedActionId);
            Assert.IsFalse(collector.PressureOnlyReactionRequests[0].Context.HasFullHitContext);
        }

        private static CharacterActionRunner StartRunner(CharacterActionConfig action)
        {
            var runner = new CharacterActionRunner();
            CharacterActionRunnerOperationResult start = runner.Start(
                CharacterActionResolveResult.Success(CreatePlan(action)),
                CharacterActionRunnerActionDefinition.FromConfig(action));
            Assert.IsTrue(start.Accepted);
            return runner;
        }

        private static CharacterActionPlan CreatePlan(CharacterActionConfig action)
        {
            return new CharacterActionPlan(
                planId: action.Id,
                actionId: action.StableId,
                category: action.Category,
                priority: action.Priority,
                durationFrames: action.DurationFrames.Value,
                phases: action.Phases,
                tracks: CharacterActionTrackPlan.FromConfig(action),
                traceId: "trace." + action.StableId);
        }

        private static CharacterActionConfig CreateAction(
            MotionTrackConfig motionTrack = null,
            CombatTrackConfig combatTrack = null,
            GameplayTrackConfig gameplayTrack = null)
        {
            return new CharacterActionConfig(
                id: 100,
                stableId: "light_attack",
                displayName: "Light Attack",
                category: CharacterActionCategory.BasicAttack,
                timelineAuthority: CharacterActionTimelineAuthority.CharacterAuthored,
                tags: null,
                priority: 10,
                durationFrames: 3,
                requirements: null,
                phases: new[]
                {
                    new CharacterActionPhase(CharacterActionPhaseKind.Startup, 0, 0),
                    new CharacterActionPhase(CharacterActionPhaseKind.Active, 1, 1),
                    new CharacterActionPhase(CharacterActionPhaseKind.Recovery, 2, 2),
                },
                cancelRules: null,
                interruptRules: null,
                motionTrack: motionTrack,
                combatTrack: combatTrack,
                gameplayTrack: gameplayTrack);
        }

        private static CharacterActionAdapterContext CreateContext()
        {
            return new CharacterActionAdapterContext(
                new RuntimeFrame(42),
                new GameplayEntityId(7, 1),
                new CombatEntityId(11),
                new CombatBodyId(13),
                sourceId: 5);
        }

        private static void AssertHasDiagnostic(CharacterActionDiagnostic[] diagnostics, string code)
        {
            for (int i = 0; i < diagnostics.Length; i++)
            {
                if (diagnostics[i].Code == code)
                    return;
            }

            Assert.Fail("Expected diagnostic code " + code + ".");
        }

        private sealed class ThrowingMotionSink : ICharacterActionMotionRequestSink
        {
            public CharacterActionAdapterSinkResult SubmitMotionRequest(CharacterActionMotionRequest request)
            {
                throw new System.InvalidOperationException("motion backend unavailable");
            }
        }
    }
}
