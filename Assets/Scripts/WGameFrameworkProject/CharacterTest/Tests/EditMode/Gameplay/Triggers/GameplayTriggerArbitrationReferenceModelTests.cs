using System;
using System.Collections.Generic;
using MxFramework.Gameplay;
using MxFramework.Gameplay.Triggers;
using MxFramework.Runtime;
using NUnit.Framework;

namespace MxFramework.Tests.Gameplay.Triggers
{
    public sealed class GameplayTriggerArbitrationReferenceModelTests
    {
        private static readonly GameplayTriggerSourceBindingId FirstSourceBindingId =
            new GameplayTriggerSourceBindingId(100);
        private static readonly GameplayTriggerSourceBindingId SecondSourceBindingId =
            new GameplayTriggerSourceBindingId(101);
        private static readonly GameplayTriggerActionKindId ActionKind =
            new GameplayTriggerActionKindId(1000);

        [Test]
        public void IndependentCandidates_AllAcceptInExecutionOrder()
        {
            var sink = new ReferenceSink();
            GameplayTriggerRouteDefinition[] routes =
            {
                Route(3, executionOrder: 0),
                Route(1, executionOrder: 20),
                Route(2, executionOrder: -10)
            };
            GameplayTriggerRuntime runtime = Runtime(routes, sink);
            GameplayTriggerInstanceId instanceId = runtime.GetInstanceId(0);
            var signal = new GameplayTriggerSignal(
                1,
                new GameplayEntityId(1, 1),
                new GameplayEntityId(10, 1));
            GameplayTriggerReferenceOccurrence occurrence =
                GameplayTriggerReferenceOccurrence.Signal(0, instanceId, signal, 0);
            var candidates = new[]
            {
                Candidate(1, routes[0], occurrence),
                Candidate(1, routes[1], occurrence),
                Candidate(1, routes[2], occurrence)
            };
            var reference = new GameplayTriggerArbitrationReferenceModel();
            reference.Resolve(candidates);

            Submit(runtime, 0, FirstSourceBindingId, signal);

            AssertAccepted(reference.Accepted, runtime);
            Assert.AreEqual(new GameplayTriggerRouteId(2), runtime.GetActivation(0).RouteId);
            Assert.AreEqual(new GameplayTriggerRouteId(3), runtime.GetActivation(1).RouteId);
            Assert.AreEqual(new GameplayTriggerRouteId(1), runtime.GetActivation(2).RouteId);
            Assert.AreEqual(3, sink.PrepareCount);
        }

        [Test]
        public void ExclusiveWinner_UsesPriorityInsteadOfExecutionOrder()
        {
            var sink = new ReferenceSink();
            GameplayTriggerRouteDefinition highPriority = Route(
                2,
                executionOrder: 100,
                exclusiveGroupId: 7,
                winnerPriority: 10);
            GameplayTriggerRouteDefinition lowPriority = Route(
                1,
                executionOrder: -100,
                exclusiveGroupId: 7,
                winnerPriority: 0);
            GameplayTriggerRuntime runtime = Runtime(
                new[] { lowPriority, highPriority },
                sink);
            GameplayTriggerInstanceId instanceId = runtime.GetInstanceId(0);
            var signal = new GameplayTriggerSignal(
                1,
                new GameplayEntityId(1, 1),
                new GameplayEntityId(10, 1));
            GameplayTriggerReferenceOccurrence occurrence =
                GameplayTriggerReferenceOccurrence.Signal(0, instanceId, signal, 0);
            var reference = new GameplayTriggerArbitrationReferenceModel();
            reference.Resolve(new[]
            {
                Candidate(1, lowPriority, occurrence),
                Candidate(1, highPriority, occurrence)
            });

            Submit(runtime, 0, FirstSourceBindingId, signal);

            AssertAccepted(reference.Accepted, runtime);
            Assert.AreEqual(highPriority.RouteId, runtime.GetActivation(0).RouteId);
            AssertDiagnosticCount(runtime, GameplayTriggerErrorCode.ArbitrationSuppressed, 1);
            Assert.AreEqual(1, sink.PrepareCount);
        }

        [TestCase(GameplayTriggerArbitrationScope.Global, 1)]
        [TestCase(GameplayTriggerArbitrationScope.PerSubject, 2)]
        [TestCase(GameplayTriggerArbitrationScope.PerInstigator, 3)]
        [TestCase(GameplayTriggerArbitrationScope.PerPair, 4)]
        public void ExclusiveScopes_MatchIndependentReferenceAcrossPermutations(
            GameplayTriggerArbitrationScope scope,
            int expectedAccepted)
        {
            var sink = new ReferenceSink();
            GameplayTriggerRouteDefinition first = Route(
                1,
                exclusiveGroupId: 7,
                arbitrationScope: scope);
            GameplayTriggerRouteDefinition second = Route(
                2,
                exclusiveGroupId: 7,
                arbitrationScope: scope);
            GameplayTriggerRuntime runtime = Runtime(new[] { second, first }, sink);
            GameplayTriggerInstanceId instanceId = runtime.GetInstanceId(0);
            var a = new GameplayEntityId(1, 1);
            var b = new GameplayEntityId(2, 1);
            var x = new GameplayEntityId(10, 1);
            var y = new GameplayEntityId(11, 1);
            var z = new GameplayEntityId(12, 1);
            GameplayTriggerSignal[] submitted =
            {
                new GameplayTriggerSignal(1, b, x),
                new GameplayTriggerSignal(1, a, z),
                new GameplayTriggerSignal(1, a, x),
                new GameplayTriggerSignal(1, a, y)
            };
            GameplayTriggerSignal[] canonical =
            {
                new GameplayTriggerSignal(1, a, x),
                new GameplayTriggerSignal(1, a, y),
                new GameplayTriggerSignal(1, a, z),
                new GameplayTriggerSignal(1, b, x)
            };
            var candidates = new List<GameplayTriggerArbitrationReferenceCandidate>();
            for (int i = canonical.Length - 1; i >= 0; i--)
            {
                GameplayTriggerReferenceOccurrence occurrence =
                    GameplayTriggerReferenceOccurrence.Signal(0, instanceId, canonical[i], i);
                candidates.Add(Candidate(1, second, occurrence));
                candidates.Add(Candidate(1, first, occurrence));
            }
            var reference = new GameplayTriggerArbitrationReferenceModel();
            reference.Resolve(candidates);

            Assert.IsTrue(runtime.TrySubmitSignals(
                RuntimeFrame.Zero,
                FirstSourceBindingId,
                GameplayTriggerSignalOrdering.UnorderedMultiset,
                submitted,
                out GameplayTriggerResult submit), submit.ErrorCode.ToString());
            Assert.IsTrue(runtime.Evaluate(RuntimeFrame.Zero).Success);

            Assert.AreEqual(expectedAccepted, reference.Accepted.Count);
            AssertAccepted(reference.Accepted, runtime);
            AssertDiagnosticCount(
                runtime,
                GameplayTriggerErrorCode.ArbitrationSuppressed,
                canonical.Length * 2 - expectedAccepted);
            Assert.AreEqual(expectedAccepted, sink.PrepareCount);
        }

        [Test]
        public void WinnerTie_UsesDefinitionBeforeOccurrenceAndRegistryOrder()
        {
            var sink = new ReferenceSink();
            GameplayTriggerRouteDefinition route = Route(
                1,
                exclusiveGroupId: 7);
            GameplayTriggerRuntime runtime = Runtime(
                new[]
                {
                    new GameplayTriggerDefinition(
                        new GameplayTriggerDefinitionId(2),
                        1,
                        new GameplayTriggerSourceDefinitionId(1),
                        new[] { route }),
                    new GameplayTriggerDefinition(
                        new GameplayTriggerDefinitionId(1),
                        1,
                        new GameplayTriggerSourceDefinitionId(1),
                        new[] { route })
                },
                new[]
                {
                    Binding(2, 2, SecondSourceBindingId),
                    Binding(1, 1, FirstSourceBindingId)
                },
                sink);
            GameplayTriggerInstanceId definition1Instance = runtime.GetInstanceId(0);
            GameplayTriggerInstanceId definition2Instance = runtime.GetInstanceId(1);
            var lowOccurrenceSignal = new GameplayTriggerSignal(
                1,
                new GameplayEntityId(1, 1));
            var highOccurrenceSignal = new GameplayTriggerSignal(
                1,
                new GameplayEntityId(99, 1));
            var reference = new GameplayTriggerArbitrationReferenceModel();
            reference.Resolve(new[]
            {
                Candidate(
                    2,
                    route,
                    GameplayTriggerReferenceOccurrence.Signal(
                        0,
                        definition2Instance,
                        lowOccurrenceSignal,
                        0)),
                Candidate(
                    1,
                    route,
                    GameplayTriggerReferenceOccurrence.Signal(
                        0,
                        definition1Instance,
                        highOccurrenceSignal,
                        0))
            });

            Assert.IsTrue(runtime.TrySubmitSignals(
                RuntimeFrame.Zero,
                SecondSourceBindingId,
                GameplayTriggerSignalOrdering.UnorderedMultiset,
                new[] { lowOccurrenceSignal },
                out _));
            Assert.IsTrue(runtime.TrySubmitSignals(
                RuntimeFrame.Zero,
                FirstSourceBindingId,
                GameplayTriggerSignalOrdering.UnorderedMultiset,
                new[] { highOccurrenceSignal },
                out _));
            Assert.IsTrue(runtime.Evaluate(RuntimeFrame.Zero).Success);

            AssertAccepted(reference.Accepted, runtime);
            Assert.AreEqual(new GameplayTriggerDefinitionId(1), runtime.GetActivation(0).DefinitionId);
        }

        [Test]
        public void WinnerTie_UsesInstanceBeforeSubjectForSharedDefinitionAndRoute()
        {
            var sink = new ReferenceSink();
            GameplayTriggerRouteDefinition route = Route(
                1,
                exclusiveGroupId: 7);
            var definition = new GameplayTriggerDefinition(
                new GameplayTriggerDefinitionId(1),
                1,
                new GameplayTriggerSourceDefinitionId(1),
                new[] { route });
            GameplayTriggerRuntime runtime = Runtime(
                new[] { definition },
                new[]
                {
                    Binding(2, 1, SecondSourceBindingId),
                    Binding(1, 1, FirstSourceBindingId)
                },
                sink);
            GameplayTriggerInstanceId smallerInstance = runtime.GetInstanceId(0);
            GameplayTriggerInstanceId largerInstance = runtime.GetInstanceId(1);
            var largerSubjectSignal = new GameplayTriggerSignal(
                1,
                new GameplayEntityId(99, 1));
            var smallerSubjectSignal = new GameplayTriggerSignal(
                1,
                new GameplayEntityId(1, 1));
            var reference = new GameplayTriggerArbitrationReferenceModel();
            reference.Resolve(new[]
            {
                Candidate(
                    1,
                    route,
                    GameplayTriggerReferenceOccurrence.Signal(
                        0,
                        largerInstance,
                        smallerSubjectSignal,
                        0)),
                Candidate(
                    1,
                    route,
                    GameplayTriggerReferenceOccurrence.Signal(
                        0,
                        smallerInstance,
                        largerSubjectSignal,
                        0))
            });

            Assert.IsTrue(runtime.TrySubmitSignals(
                RuntimeFrame.Zero,
                SecondSourceBindingId,
                GameplayTriggerSignalOrdering.UnorderedMultiset,
                new[] { smallerSubjectSignal },
                out _));
            Assert.IsTrue(runtime.TrySubmitSignals(
                RuntimeFrame.Zero,
                FirstSourceBindingId,
                GameplayTriggerSignalOrdering.UnorderedMultiset,
                new[] { largerSubjectSignal },
                out _));
            Assert.IsTrue(runtime.Evaluate(RuntimeFrame.Zero).Success);

            AssertAccepted(reference.Accepted, runtime);
            Assert.AreEqual(smallerInstance, runtime.GetActivation(0).Occurrence.InstanceId);
            Assert.AreEqual(largerSubjectSignal.Subject, runtime.GetActivation(0).Occurrence.Subject);
            AssertDiagnosticCount(runtime, GameplayTriggerErrorCode.ArbitrationSuppressed, 1);
        }

        [Test]
        public void ConditionRejectedCandidate_DoesNotCompeteOrReserveSink()
        {
            var sink = new ReferenceSink();
            GameplayTriggerRouteDefinition eligible = Route(
                1,
                exclusiveGroupId: 7,
                winnerPriority: 0);
            GameplayTriggerRouteDefinition ineligible = Route(
                2,
                exclusiveGroupId: 7,
                winnerPriority: 100,
                condition: new GameplayTriggerConditionProgram(
                    new[]
                    {
                        new GameplayTriggerConditionInstruction(
                            GameplayTriggerConditionOperation.PushFalse)
                    }));
            GameplayTriggerRuntime runtime = Runtime(
                new[] { ineligible, eligible },
                sink);
            GameplayTriggerInstanceId instanceId = runtime.GetInstanceId(0);
            var signal = new GameplayTriggerSignal(
                1,
                new GameplayEntityId(1, 1));
            GameplayTriggerReferenceOccurrence occurrence =
                GameplayTriggerReferenceOccurrence.Signal(0, instanceId, signal, 0);
            var reference = new GameplayTriggerArbitrationReferenceModel();
            reference.Resolve(new[]
            {
                Candidate(1, ineligible, occurrence, eligible: false),
                Candidate(1, eligible, occurrence)
            });

            Submit(runtime, 0, FirstSourceBindingId, signal);

            AssertAccepted(reference.Accepted, runtime);
            Assert.AreEqual(eligible.RouteId, runtime.GetActivation(0).RouteId);
            AssertReferenceDecisionCount(
                reference,
                GameplayTriggerArbitrationReferenceDecision.Ineligible,
                1);
            AssertDiagnosticCount(runtime, GameplayTriggerErrorCode.ConditionRejected, 1);
            AssertDiagnosticCount(runtime, GameplayTriggerErrorCode.ArbitrationSuppressed, 0);
            Assert.AreEqual(1, sink.PrepareCount);
        }

        [Test]
        public void SuppressedLoser_DoesNotConsumePolicyOrReserveSink()
        {
            var sink = new ReferenceSink();
            var policy = new GameplayTriggerActivationPolicy(
                GameplayTriggerStateScope.PerInstance,
                maxActivations: 1);
            GameplayTriggerRouteDefinition winner = Route(
                1,
                exclusiveGroupId: 7,
                winnerPriority: 10,
                policy: policy);
            GameplayTriggerRouteDefinition loser = Route(
                2,
                exclusiveGroupId: 7,
                winnerPriority: 0,
                policy: policy);
            GameplayTriggerRuntime runtime = Runtime(new[] { loser, winner }, sink);
            var signal = new GameplayTriggerSignal(1);

            Submit(runtime, 0, FirstSourceBindingId, signal);
            Assert.AreEqual(1, runtime.ActivationCount);
            Assert.AreEqual(winner.RouteId, runtime.GetActivation(0).RouteId);
            Assert.AreEqual(1, sink.PrepareCount);

            Submit(runtime, 1, FirstSourceBindingId, signal);
            Assert.AreEqual(1, runtime.ActivationCount);
            Assert.AreEqual(loser.RouteId, runtime.GetActivation(0).RouteId);
            Assert.AreEqual(2, sink.PrepareCount);

            Submit(runtime, 2, FirstSourceBindingId, signal);
            Assert.AreEqual(0, runtime.ActivationCount);
            Assert.AreEqual(2, sink.PrepareCount);
        }

        [TestCase(GameplayTriggerArbitrationScope.PerSubject)]
        [TestCase(GameplayTriggerArbitrationScope.PerInstigator)]
        [TestCase(GameplayTriggerArbitrationScope.PerPair)]
        public void ExclusiveScopeMissingEntity_IsRejectedBeforeArbitration(
            GameplayTriggerArbitrationScope scope)
        {
            var sink = new ReferenceSink();
            GameplayTriggerRouteDefinition first = Route(
                1,
                exclusiveGroupId: 7,
                arbitrationScope: scope);
            GameplayTriggerRouteDefinition second = Route(
                2,
                exclusiveGroupId: 7,
                arbitrationScope: scope);
            GameplayTriggerRuntime runtime = Runtime(new[] { first, second }, sink);
            GameplayTriggerInstanceId instanceId = runtime.GetInstanceId(0);
            var signal = new GameplayTriggerSignal(1);
            GameplayTriggerReferenceOccurrence occurrence =
                GameplayTriggerReferenceOccurrence.Signal(0, instanceId, signal, 0);
            var reference = new GameplayTriggerArbitrationReferenceModel();
            reference.Resolve(new[]
            {
                Candidate(1, first, occurrence),
                Candidate(1, second, occurrence)
            });

            Submit(runtime, 0, FirstSourceBindingId, signal);

            Assert.AreEqual(0, reference.Accepted.Count);
            Assert.AreEqual(0, runtime.ActivationCount);
            AssertDiagnosticCount(runtime, GameplayTriggerErrorCode.MissingScopeEntity, 2);
            Assert.AreEqual(0, sink.PrepareCount);
        }

        private static GameplayTriggerArbitrationReferenceCandidate Candidate(
            int definitionId,
            GameplayTriggerRouteDefinition route,
            in GameplayTriggerReferenceOccurrence occurrence,
            bool eligible = true) =>
            new GameplayTriggerArbitrationReferenceCandidate(
                new GameplayTriggerDefinitionId(definitionId),
                route.RouteId,
                occurrence,
                route.ExecutionOrder,
                route.ExclusiveGroupId,
                route.WinnerPriority,
                route.ArbitrationScope,
                eligible);

        private static GameplayTriggerRouteDefinition Route(
            int routeId,
            int executionOrder = 0,
            int exclusiveGroupId = 0,
            int winnerPriority = 0,
            GameplayTriggerArbitrationScope arbitrationScope =
                GameplayTriggerArbitrationScope.Global,
            GameplayTriggerActivationPolicy policy = default,
            GameplayTriggerConditionProgram condition = null) =>
            new GameplayTriggerRouteDefinition(
                new GameplayTriggerRouteId(routeId),
                GameplayTriggerOccurrenceKind.Pulsed,
                condition ?? GameplayTriggerConditionProgram.Always,
                new[]
                {
                    new GameplayTriggerActionInstruction(
                        ActionKind,
                        GameplayTriggerTargetSelector.Owner)
                },
                policy,
                executionOrder,
                exclusiveGroupId,
                winnerPriority,
                arbitrationScope);

        private static GameplayTriggerRuntime Runtime(
            GameplayTriggerRouteDefinition[] routes,
            ReferenceSink sink) =>
            Runtime(
                new[]
                {
                    new GameplayTriggerDefinition(
                        new GameplayTriggerDefinitionId(1),
                        1,
                        new GameplayTriggerSourceDefinitionId(1),
                        routes)
                },
                new[] { Binding(1, 1, FirstSourceBindingId) },
                sink);

        private static GameplayTriggerRuntime Runtime(
            GameplayTriggerDefinition[] definitions,
            GameplayTriggerBinding[] bindings,
            ReferenceSink sink)
        {
            var source = new GameplayTriggerSourceDefinition(
                new GameplayTriggerSourceDefinitionId(1),
                GameplayTriggerSourceKind.Signal,
                signalOrdering: GameplayTriggerSignalOrdering.UnorderedMultiset);
            Assert.IsTrue(GameplayTriggerCatalog.TryCreate(
                new[] { source },
                definitions,
                bindings,
                new IGameplayTriggerActionSink[] { sink },
                out GameplayTriggerCatalog catalog,
                out GameplayTriggerCatalogValidationResult validation),
                validation.ErrorCode.ToString());
            return new GameplayTriggerRuntime(
                catalog,
                new GameplayTriggerRuntimeOptions(8, 32, 32, 32, 64, 32, 32, 32, 8, 16));
        }

        private static GameplayTriggerBinding Binding(
            int bindingId,
            int definitionId,
            GameplayTriggerSourceBindingId sourceBindingId) =>
            new GameplayTriggerBinding(
                new GameplayTriggerBindingId(bindingId),
                new GameplayTriggerDefinitionId(definitionId),
                sourceBindingId,
                new GameplayEntityId(900 + bindingId, 1));

        private static void Submit(
            GameplayTriggerRuntime runtime,
            long frame,
            GameplayTriggerSourceBindingId sourceBindingId,
            params GameplayTriggerSignal[] signals)
        {
            var runtimeFrame = new RuntimeFrame(frame);
            Assert.IsTrue(runtime.TrySubmitSignals(
                runtimeFrame,
                sourceBindingId,
                GameplayTriggerSignalOrdering.UnorderedMultiset,
                signals,
                out GameplayTriggerResult submit), submit.ErrorCode.ToString());
            Assert.IsTrue(runtime.Evaluate(runtimeFrame).Success);
        }

        private static void AssertAccepted(
            IReadOnlyList<GameplayTriggerArbitrationReferenceCandidate> expected,
            GameplayTriggerRuntime runtime)
        {
            Assert.AreEqual(expected.Count, runtime.ActivationCount);
            for (int i = 0; i < expected.Count; i++)
            {
                GameplayTriggerArbitrationReferenceCandidate left = expected[i];
                GameplayTriggerActivation right = runtime.GetActivation(i);
                string context = "activation=" + i;
                Assert.AreEqual(left.DefinitionId, right.DefinitionId, context);
                Assert.AreEqual(left.RouteId, right.RouteId, context);
                Assert.AreEqual(left.Occurrence.InstanceId, right.Occurrence.InstanceId, context);
                Assert.AreEqual(left.Occurrence.Subject, right.Occurrence.Subject, context);
                Assert.AreEqual(left.Occurrence.Instigator, right.Occurrence.Instigator, context);
                Assert.AreEqual(left.Occurrence.SignalId, right.Occurrence.SignalId, context);
                Assert.AreEqual(left.Occurrence.Payload0, right.Occurrence.Payload0, context);
                Assert.AreEqual(left.Occurrence.Payload1, right.Occurrence.Payload1, context);
                Assert.AreEqual(left.Occurrence.Payload2, right.Occurrence.Payload2, context);
                Assert.AreEqual(left.Occurrence.OrderingValue, right.Occurrence.OrderingValue, context);
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

        private static void AssertReferenceDecisionCount(
            GameplayTriggerArbitrationReferenceModel reference,
            GameplayTriggerArbitrationReferenceDecision decision,
            int expected)
        {
            int actual = 0;
            for (int i = 0; i < reference.Results.Count; i++)
            {
                if (reference.Results[i].Decision == decision)
                {
                    actual++;
                }
            }
            Assert.AreEqual(expected, actual, decision.ToString());
        }

        private sealed class ReferenceSink : IGameplayTriggerActionSink
        {
            public GameplayTriggerActionKindId ActionKindId => ActionKind;
            public GameplayTriggerDeliveryContract DeliveryContract =>
                GameplayTriggerDeliveryContract.SameFrameReserved;
            public int PrepareCount { get; private set; }

            public bool TryPrepare(
                in GameplayTriggerActionPreparation preparation,
                out int token)
            {
                PrepareCount++;
                token = PrepareCount;
                return true;
            }

            public void Abort(int token)
            {
            }

            public void Commit(int token, in GameplayTriggerActionIntent intent)
            {
            }
        }
    }
}
