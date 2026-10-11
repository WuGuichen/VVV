using System;
using System.Collections.Generic;
using MxFramework.Gameplay;
using MxFramework.Gameplay.Triggers;
using MxFramework.Runtime;
using NUnit.Framework;

namespace MxFramework.Tests.Gameplay.Triggers
{
    public sealed class GameplayTriggerTransactionReferenceModelTests
    {
        private static readonly GameplayTriggerSourceBindingId SourceBindingId =
            new GameplayTriggerSourceBindingId(100);
        private static readonly GameplayTriggerActionKindId FirstActionKind =
            new GameplayTriggerActionKindId(1001);
        private static readonly GameplayTriggerActionKindId SecondActionKind =
            new GameplayTriggerActionKindId(1002);
        private static readonly GameplayTriggerActionKindId ThirdActionKind =
            new GameplayTriggerActionKindId(1003);

        [Test]
        public void PrepareFailure_UsesStrictReverseAbortAndCommitsNoAuthority()
        {
            var trace = new List<TraceEvent>();
            var first = new TraceSink(FirstActionKind, trace);
            var second = new TraceSink(SecondActionKind, trace);
            var third = new TraceSink(ThirdActionKind, trace) { RejectPrepare = true };
            GameplayTriggerRuntime runtime = OccupancyRuntime(
                new[]
                {
                    Route(
                        1,
                        GameplayTriggerOccurrenceKind.Entered,
                        default,
                        GameplayTriggerConditionProgram.Always,
                        0,
                        new[]
                        {
                            Action(FirstActionKind),
                            Action(SecondActionKind),
                            Action(ThirdActionKind)
                        })
                },
                new IGameplayTriggerActionSink[] { third, first, second });
            var reference = new GameplayTriggerTransactionReferenceModel();
            GameplayTriggerTransactionReferenceDecision decision = reference.Execute(
                new GameplayTriggerTransactionReferencePlan(
                    3,
                    rejectPrepareActionIndex: 2,
                    actionSinkIds: new[]
                    {
                        FirstActionKind.Value,
                        SecondActionKind.Value,
                        ThirdActionKind.Value
                    }));
            var subject = new GameplayEntityId(1, 1);

            SubmitOccupancy(runtime, 0, subject);

            Assert.AreEqual(
                GameplayTriggerTransactionReferenceDecision.ReservationRejected,
                decision);
            AssertTransactionState(reference, runtime);
            AssertTrace(reference.Events, trace);
            AssertDiagnosticCount(runtime, GameplayTriggerErrorCode.ReservationRejected, 1);
            Assert.AreEqual(1, runtime.RelationCount);
        }

        [Test]
        public void TwoActivationsCompetingForLastSlot_DoNotOversubscribeOrConsumeLoserPolicy()
        {
            var trace = new List<TraceEvent>();
            var sink = new TraceSink(FirstActionKind, trace, capacity: 1);
            var policy = new GameplayTriggerActivationPolicy(
                GameplayTriggerStateScope.PerSubject,
                maxActivations: 1);
            GameplayTriggerRuntime runtime = SignalRuntime(
                new[]
                {
                    Route(
                        1,
                        GameplayTriggerOccurrenceKind.Pulsed,
                        policy,
                        GameplayTriggerConditionProgram.Always,
                        0,
                        new[] { Action(FirstActionKind) })
                },
                new IGameplayTriggerActionSink[] { sink });
            var a = new GameplayEntityId(1, 1);
            var b = new GameplayEntityId(2, 1);

            SubmitSignals(
                runtime,
                0,
                new GameplayTriggerSignal(1, b),
                new GameplayTriggerSignal(1, a));

            Assert.AreEqual(1, runtime.ActivationCount);
            Assert.AreEqual(a, runtime.GetActivation(0).Occurrence.Subject);
            Assert.AreEqual(1, runtime.IntentCount);
            Assert.AreEqual(2L, runtime.NextActivationId);
            Assert.AreEqual(1, sink.CommitCount);
            AssertDiagnosticCount(runtime, GameplayTriggerErrorCode.ReservationRejected, 1);

            sink.ReleaseAll();
            trace.Clear();
            SubmitSignals(runtime, 1, new GameplayTriggerSignal(1, b));

            Assert.AreEqual(1, runtime.ActivationCount);
            Assert.AreEqual(b, runtime.GetActivation(0).Occurrence.Subject);
            Assert.AreEqual(3L, runtime.NextActivationId);
            Assert.AreEqual(1, sink.CommitCount);
        }

        [Test]
        public void RejectedEnteredMatrix_PreservesSourceTruthAndCommitsNoActivationState()
        {
            VerifyRejectedEntered(
                GameplayTriggerTransactionReferenceDecision.ConditionRejected,
                new GameplayTriggerConditionProgram(
                    new[]
                    {
                        new GameplayTriggerConditionInstruction(
                            GameplayTriggerConditionOperation.PushFalse)
                    }),
                policy: default,
                exclusiveLoser: false,
                maxIntents: 8,
                rejectPrepare: false,
                GameplayTriggerErrorCode.ConditionRejected);
            VerifyRejectedEntered(
                GameplayTriggerTransactionReferenceDecision.PolicyRejected,
                GameplayTriggerConditionProgram.Always,
                new GameplayTriggerActivationPolicy(
                    GameplayTriggerStateScope.PerSubject,
                    maxActivations: 1),
                exclusiveLoser: false,
                maxIntents: 8,
                rejectPrepare: false,
                GameplayTriggerErrorCode.PolicyRejected,
                primePolicy: true);
            VerifyRejectedEntered(
                GameplayTriggerTransactionReferenceDecision.ArbitrationSuppressed,
                GameplayTriggerConditionProgram.Always,
                policy: default,
                exclusiveLoser: true,
                maxIntents: 8,
                rejectPrepare: false,
                GameplayTriggerErrorCode.ArbitrationSuppressed);
            VerifyRejectedEntered(
                GameplayTriggerTransactionReferenceDecision.CoreCapacityRejected,
                GameplayTriggerConditionProgram.Always,
                policy: default,
                exclusiveLoser: false,
                maxIntents: 1,
                rejectPrepare: false,
                GameplayTriggerErrorCode.IntentCapacityExceeded,
                actionCount: 2);
            VerifyRejectedEntered(
                GameplayTriggerTransactionReferenceDecision.ReservationRejected,
                GameplayTriggerConditionProgram.Always,
                policy: default,
                exclusiveLoser: false,
                maxIntents: 8,
                rejectPrepare: true,
                GameplayTriggerErrorCode.ReservationRejected);
        }

        [Test]
        public void CommitInvariantException_LeavesCommittedAuthorityForFailFastSession()
        {
            var trace = new List<TraceEvent>();
            var sink = new TraceSink(FirstActionKind, trace) { ThrowOnCommit = true };
            var reference = new GameplayTriggerTransactionReferenceModel();
            GameplayTriggerTransactionReferenceDecision decision = reference.Execute(
                new GameplayTriggerTransactionReferencePlan(
                    1,
                    actionSinkIds: new[] { FirstActionKind.Value }));
            var policy = new GameplayTriggerActivationPolicy(
                GameplayTriggerStateScope.PerSubject,
                maxActivations: 1);
            GameplayTriggerRuntime runtime = SignalRuntime(
                new[]
                {
                    Route(
                        1,
                        GameplayTriggerOccurrenceKind.Pulsed,
                        policy,
                        GameplayTriggerConditionProgram.Always,
                        0,
                        new[] { Action(FirstActionKind) })
                },
                new IGameplayTriggerActionSink[] { sink });
            var subject = new GameplayEntityId(1, 1);
            Assert.IsTrue(runtime.TrySubmitSignals(
                RuntimeFrame.Zero,
                SourceBindingId,
                GameplayTriggerSignalOrdering.UnorderedMultiset,
                new[] { new GameplayTriggerSignal(1, subject) },
                out _));

            Assert.Throws<TraceCommitException>(() => runtime.Evaluate(RuntimeFrame.Zero));

            Assert.AreEqual(GameplayTriggerTransactionReferenceDecision.Committed, decision);
            AssertTransactionState(reference, runtime);
            AssertTrace(reference.Events, trace);
            Assert.AreEqual(1, reference.PolicyCommitCount);
            Assert.AreEqual(1, sink.CommitAttemptCount);
            Assert.AreEqual(0, sink.CommitCount);

            sink.ThrowOnCommit = false;
            Assert.IsTrue(runtime.Evaluate(RuntimeFrame.Zero).Success);

            Assert.AreEqual(0, runtime.ActivationCount);
            Assert.AreEqual(0, runtime.IntentCount);
            Assert.AreEqual(reference.NextActivationId, runtime.NextActivationId);
            Assert.AreEqual(1, sink.CommitAttemptCount);
            Assert.AreEqual(0, sink.CommitCount);
            AssertDiagnosticCount(runtime, GameplayTriggerErrorCode.PolicyRejected, 1);
        }

        private static void VerifyRejectedEntered(
            GameplayTriggerTransactionReferenceDecision referenceDecision,
            GameplayTriggerConditionProgram condition,
            GameplayTriggerActivationPolicy policy,
            bool exclusiveLoser,
            int maxIntents,
            bool rejectPrepare,
            GameplayTriggerErrorCode expectedDiagnostic,
            bool primePolicy = false,
            int actionCount = 1)
        {
            var trace = new List<TraceEvent>();
            var sink = new TraceSink(FirstActionKind, trace) { RejectPrepare = rejectPrepare };
            var winnerTrace = new List<TraceEvent>();
            var winnerSink = new TraceSink(SecondActionKind, winnerTrace);
            GameplayTriggerActionInstruction[] actions = actionCount == 2
                ? new[] { Action(FirstActionKind), Action(FirstActionKind) }
                : new[] { Action(FirstActionKind) };
            var routes = new List<GameplayTriggerRouteDefinition>();
            if (exclusiveLoser)
            {
                routes.Add(Route(
                    1,
                    GameplayTriggerOccurrenceKind.Entered,
                    default,
                    GameplayTriggerConditionProgram.Always,
                    winnerPriority: 10,
                    actions: new[] { Action(SecondActionKind) },
                    exclusiveGroupId: 7));
                routes.Add(Route(
                    2,
                    GameplayTriggerOccurrenceKind.Entered,
                    policy,
                    condition,
                    winnerPriority: 0,
                    actions: actions,
                    exclusiveGroupId: 7));
            }
            else
            {
                routes.Add(Route(
                    1,
                    GameplayTriggerOccurrenceKind.Entered,
                    policy,
                    condition,
                    0,
                    actions));
            }
            GameplayTriggerRuntime runtime = OccupancyRuntime(
                routes.ToArray(),
                exclusiveLoser
                    ? new IGameplayTriggerActionSink[] { winnerSink, sink }
                    : new IGameplayTriggerActionSink[] { sink },
                maxIntents);
            var subject = new GameplayEntityId(1, 1);
            if (primePolicy)
            {
                SubmitOccupancy(runtime, 0, subject);
                Assert.AreEqual(1, runtime.ActivationCount);
                Assert.IsTrue(runtime.TrySetEnabled(runtime.GetInstanceId(0), false, out _));
                Assert.IsTrue(runtime.TrySetEnabled(runtime.GetInstanceId(0), true, out _));
                sink.ReleaseAll();
                trace.Clear();
            }
            long frame = primePolicy ? 1L : 0L;
            var reference = new GameplayTriggerTransactionReferenceModel();
            var actionSinkIds = new int[actionCount];
            for (int actionIndex = 0; actionIndex < actionSinkIds.Length; actionIndex++)
            {
                actionSinkIds[actionIndex] = FirstActionKind.Value;
            }
            GameplayTriggerTransactionReferencePlan referencePlan =
                referenceDecision == GameplayTriggerTransactionReferenceDecision.ReservationRejected
                    ? new GameplayTriggerTransactionReferencePlan(
                        actionCount,
                        rejectPrepareActionIndex: 0,
                        actionSinkIds: actionSinkIds)
                    : new GameplayTriggerTransactionReferencePlan(
                        actionCount,
                        referenceDecision,
                        actionSinkIds: actionSinkIds);
            GameplayTriggerTransactionReferenceDecision actualReferenceDecision =
                reference.Execute(referencePlan);
            Assert.AreEqual(referenceDecision, actualReferenceDecision);
            Assert.AreEqual(0, reference.ActivationCount);
            Assert.AreEqual(0, reference.IntentCount);
            Assert.AreEqual(1L, reference.NextActivationId);
            long expectedNextActivationId = runtime.NextActivationId;

            SubmitOccupancy(runtime, frame, subject);
            AssertTrace(reference.Events, trace);

            if (exclusiveLoser)
            {
                Assert.AreEqual(1, runtime.ActivationCount);
                Assert.AreEqual(new GameplayTriggerRouteId(1), runtime.GetActivation(0).RouteId);
                Assert.AreEqual(2, winnerTrace.Count);
            }
            else
            {
                Assert.AreEqual(0, runtime.ActivationCount);
                Assert.AreEqual(0, runtime.IntentCount);
            }
            Assert.AreEqual(expectedNextActivationId + (exclusiveLoser ? 1L : 0L), runtime.NextActivationId);
            Assert.AreEqual(1, runtime.RelationCount);
            AssertDiagnosticCount(runtime, expectedDiagnostic, 1);

            long nextFrame = frame + 1L;
            SubmitOccupancy(runtime, nextFrame, subject);
            Assert.AreEqual(GameplayTriggerOccurrenceKind.Stayed, runtime.GetOccurrence(0).Kind);
        }

        private static GameplayTriggerRuntime OccupancyRuntime(
            GameplayTriggerRouteDefinition[] routes,
            IGameplayTriggerActionSink[] sinks,
            int maxIntents = 16) =>
            Runtime(
                new GameplayTriggerSourceDefinition(
                    new GameplayTriggerSourceDefinitionId(1),
                    GameplayTriggerSourceKind.Occupancy),
                routes,
                sinks,
                maxIntents);

        private static GameplayTriggerRuntime SignalRuntime(
            GameplayTriggerRouteDefinition[] routes,
            IGameplayTriggerActionSink[] sinks) =>
            Runtime(
                new GameplayTriggerSourceDefinition(
                    new GameplayTriggerSourceDefinitionId(1),
                    GameplayTriggerSourceKind.Signal),
                routes,
                sinks,
                16);

        private static GameplayTriggerRuntime Runtime(
            GameplayTriggerSourceDefinition source,
            GameplayTriggerRouteDefinition[] routes,
            IGameplayTriggerActionSink[] sinks,
            int maxIntents)
        {
            Assert.IsTrue(GameplayTriggerCatalog.TryCreate(
                new[] { source },
                new[]
                {
                    new GameplayTriggerDefinition(
                        new GameplayTriggerDefinitionId(1),
                        1,
                        source.SourceDefinitionId,
                        routes)
                },
                new[]
                {
                    new GameplayTriggerBinding(
                        new GameplayTriggerBindingId(1),
                        new GameplayTriggerDefinitionId(1),
                        SourceBindingId,
                        new GameplayEntityId(99, 1))
                },
                sinks,
                out GameplayTriggerCatalog catalog,
                out GameplayTriggerCatalogValidationResult validation),
                validation.ErrorCode.ToString());
            return new GameplayTriggerRuntime(
                catalog,
                new GameplayTriggerRuntimeOptions(
                    8, 32, 32, 32, 64, 32, 32, maxIntents, 8, 16));
        }

        private static GameplayTriggerRouteDefinition Route(
            int routeId,
            GameplayTriggerOccurrenceKind mask,
            GameplayTriggerActivationPolicy policy,
            GameplayTriggerConditionProgram condition,
            int winnerPriority,
            GameplayTriggerActionInstruction[] actions,
            int exclusiveGroupId = 0) =>
            new GameplayTriggerRouteDefinition(
                new GameplayTriggerRouteId(routeId),
                mask,
                condition,
                actions,
                policy,
                exclusiveGroupId: exclusiveGroupId,
                winnerPriority: winnerPriority);

        private static GameplayTriggerActionInstruction Action(
            GameplayTriggerActionKindId kind) =>
            new GameplayTriggerActionInstruction(
                kind,
                GameplayTriggerTargetSelector.Subject);

        private static void SubmitOccupancy(
            GameplayTriggerRuntime runtime,
            long frame,
            params GameplayEntityId[] subjects)
        {
            var runtimeFrame = new RuntimeFrame(frame);
            Assert.IsTrue(runtime.TrySubmitOccupancy(
                runtimeFrame,
                SourceBindingId,
                subjects,
                out GameplayTriggerResult submit), submit.ErrorCode.ToString());
            Assert.IsTrue(runtime.Evaluate(runtimeFrame).Success);
        }

        private static void SubmitSignals(
            GameplayTriggerRuntime runtime,
            long frame,
            params GameplayTriggerSignal[] signals)
        {
            var runtimeFrame = new RuntimeFrame(frame);
            Assert.IsTrue(runtime.TrySubmitSignals(
                runtimeFrame,
                SourceBindingId,
                GameplayTriggerSignalOrdering.UnorderedMultiset,
                signals,
                out GameplayTriggerResult submit), submit.ErrorCode.ToString());
            Assert.IsTrue(runtime.Evaluate(runtimeFrame).Success);
        }

        private static void AssertTransactionState(
            GameplayTriggerTransactionReferenceModel reference,
            GameplayTriggerRuntime runtime)
        {
            Assert.AreEqual(reference.ActivationCount, runtime.ActivationCount);
            Assert.AreEqual(reference.IntentCount, runtime.IntentCount);
            Assert.AreEqual(reference.NextActivationId, runtime.NextActivationId);
        }

        private static void AssertTrace(
            IReadOnlyList<GameplayTriggerTransactionReferenceEvent> expected,
            IReadOnlyList<TraceEvent> actual)
        {
            Assert.AreEqual(expected.Count, actual.Count);
            for (int i = 0; i < expected.Count; i++)
            {
                Assert.AreEqual(expected[i].Kind, actual[i].Kind, "event=" + i);
                Assert.AreEqual(expected[i].SinkId, actual[i].SinkId, "event=" + i);
                Assert.AreEqual(expected[i].ActionIndex, actual[i].ActionIndex, "event=" + i);
                Assert.AreEqual(expected[i].Token, actual[i].Token, "event=" + i);
            }
        }

        private static void AssertDiagnosticCount(
            GameplayTriggerRuntime runtime,
            GameplayTriggerErrorCode code,
            int expected)
        {
            int actual = 0;
            for (int i = 0; i < runtime.DiagnosticCount; i++)
            {
                if (runtime.GetDiagnostic(i).Code == code)
                {
                    actual++;
                }
            }
            Assert.AreEqual(expected, actual, code.ToString());
        }

        private readonly struct TraceEvent
        {
            public TraceEvent(
                GameplayTriggerTransactionReferenceEventKind kind,
                int sinkId,
                int actionIndex,
                int token)
            {
                Kind = kind;
                SinkId = sinkId;
                ActionIndex = actionIndex;
                Token = token;
            }

            public GameplayTriggerTransactionReferenceEventKind Kind { get; }
            public int SinkId { get; }
            public int ActionIndex { get; }
            public int Token { get; }
        }

        private sealed class TraceCommitException : Exception
        {
        }

        private sealed class TraceSink : IGameplayTriggerActionSink
        {
            private readonly List<TraceEvent> _trace;
            private readonly Dictionary<int, int> _actionIndexByToken =
                new Dictionary<int, int>();
            private readonly int _capacity;
            private int _reserved;
            private int _nextToken = 1;

            public TraceSink(
                GameplayTriggerActionKindId actionKindId,
                List<TraceEvent> trace,
                int capacity = 32)
            {
                ActionKindId = actionKindId;
                _trace = trace;
                _capacity = capacity;
            }

            public GameplayTriggerActionKindId ActionKindId { get; }
            public GameplayTriggerDeliveryContract DeliveryContract =>
                GameplayTriggerDeliveryContract.SameFrameReserved;
            public bool RejectPrepare { get; set; }
            public bool ThrowOnCommit { get; set; }
            public int CommitAttemptCount { get; private set; }
            public int CommitCount { get; private set; }

            public bool TryPrepare(
                in GameplayTriggerActionPreparation preparation,
                out int token)
            {
                _trace.Add(new TraceEvent(
                    GameplayTriggerTransactionReferenceEventKind.Prepare,
                    ActionKindId.Value,
                    preparation.ActionIndex,
                    0));
                if (RejectPrepare || _reserved >= _capacity)
                {
                    token = 0;
                    return false;
                }
                token = _nextToken++;
                _actionIndexByToken.Add(token, preparation.ActionIndex);
                _reserved++;
                return true;
            }

            public void Abort(int token)
            {
                _reserved--;
                int actionIndex = _actionIndexByToken[token];
                _actionIndexByToken.Remove(token);
                _trace.Add(new TraceEvent(
                    GameplayTriggerTransactionReferenceEventKind.Abort,
                    ActionKindId.Value,
                    actionIndex,
                    token));
            }

            public void Commit(int token, in GameplayTriggerActionIntent intent)
            {
                CommitAttemptCount++;
                if (!_actionIndexByToken.TryGetValue(token, out int actionIndex))
                {
                    throw new AssertionException(
                        "Commit token does not belong to this sink: " + token);
                }
                if (actionIndex != intent.ActionIndex)
                {
                    throw new AssertionException(
                        "Commit token action mismatch: token=" + token
                        + ", expected=" + actionIndex
                        + ", actual=" + intent.ActionIndex);
                }
                _trace.Add(new TraceEvent(
                    GameplayTriggerTransactionReferenceEventKind.Commit,
                    ActionKindId.Value,
                    intent.ActionIndex,
                    token));
                if (ThrowOnCommit)
                {
                    throw new TraceCommitException();
                }
                _actionIndexByToken.Remove(token);
                CommitCount++;
            }

            public void ReleaseAll()
            {
                _reserved = 0;
                CommitCount = 0;
                _actionIndexByToken.Clear();
            }
        }
    }
}
