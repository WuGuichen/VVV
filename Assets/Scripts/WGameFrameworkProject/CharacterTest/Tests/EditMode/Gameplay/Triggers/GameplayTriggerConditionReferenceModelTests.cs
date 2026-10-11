using System.Collections.Generic;
using MxFramework.Core.Math;
using MxFramework.Gameplay;
using MxFramework.Gameplay.Triggers;
using MxFramework.Runtime;
using NUnit.Framework;

namespace MxFramework.Tests.Gameplay.Triggers
{
    public sealed class GameplayTriggerConditionReferenceModelTests
    {
        private static readonly GameplayTriggerActionKindId ActionKind =
            new GameplayTriggerActionKindId(2001);
        private static readonly GameplayTriggerSourceBindingId SourceBinding =
            new GameplayTriggerSourceBindingId(200);

        [Test]
        public void RecursiveTreeMatrix_MatchesPostfixProgramForEveryLeafAndComparison()
        {
            List<GameplayTriggerConditionReferenceNode> nodes = ConditionMatrix();
            GameplayTriggerOccurrence[] occurrences = OccurrenceMatrix();
            for (int occurrenceIndex = 0; occurrenceIndex < occurrences.Length; occurrenceIndex++)
            {
                for (int nodeIndex = 0; nodeIndex < nodes.Count; nodeIndex++)
                {
                    GameplayTriggerConditionReferenceNode node = nodes[nodeIndex];
                    GameplayTriggerConditionProgram program = node.Compile();
                    bool expected = node.Evaluate(occurrences[occurrenceIndex]);
                    bool actual = program.Evaluate(
                        occurrences[occurrenceIndex],
                        new bool[program.MaxStackDepth]);
                    Assert.AreEqual(
                        expected,
                        actual,
                        "occurrence=" + occurrenceIndex + ", node=" + nodeIndex);
                }
            }
        }

        [Test]
        public void RuntimeRouteEligibility_MatchesRecursiveTreeWithoutMutatingOccurrence()
        {
            List<GameplayTriggerConditionReferenceNode> nodes = RuntimeConditions();
            var routes = new GameplayTriggerRouteDefinition[nodes.Count];
            for (int i = 0; i < routes.Length; i++)
            {
                routes[i] = new GameplayTriggerRouteDefinition(
                    new GameplayTriggerRouteId(i + 1),
                    GameplayTriggerOccurrenceKind.Pulsed,
                    nodes[i].Compile(),
                    new[]
                    {
                        new GameplayTriggerActionInstruction(
                            ActionKind,
                            GameplayTriggerTargetSelector.Owner)
                    });
            }
            GameplayTriggerRuntime runtime = Runtime(routes);
            var signal = new GameplayTriggerSignal(
                7,
                new GameplayEntityId(3, 2),
                new GameplayEntityId(8, 4),
                payload0: 5,
                payload1: -1,
                payload2: 41);
            Assert.IsTrue(runtime.TrySubmitSignals(
                RuntimeFrame.Zero,
                SourceBinding,
                GameplayTriggerSignalOrdering.UnorderedMultiset,
                new[] { signal },
                out GameplayTriggerResult submit),
                submit.ErrorCode.ToString());
            Assert.IsTrue(runtime.Evaluate(RuntimeFrame.Zero).Success);
            GameplayTriggerOccurrence occurrence = runtime.GetOccurrence(0);

            var expectedRoutes = new List<GameplayTriggerRouteId>();
            for (int i = 0; i < nodes.Count; i++)
            {
                if (nodes[i].Evaluate(occurrence))
                    expectedRoutes.Add(new GameplayTriggerRouteId(i + 1));
            }

            Assert.AreEqual(expectedRoutes.Count, runtime.ActivationCount);
            for (int i = 0; i < expectedRoutes.Count; i++)
                Assert.AreEqual(expectedRoutes[i], runtime.GetActivation(i).RouteId);
            AssertOccurrenceMatchesSignal(occurrence, signal);
        }

        [Test]
        public void BinaryOperators_ExplicitTruthTablesMatchRecursiveOracle()
        {
            bool[] values = { false, true };
            GameplayTriggerOccurrence occurrence = OccurrenceMatrix()[0];
            for (int leftIndex = 0; leftIndex < values.Length; leftIndex++)
            {
                for (int rightIndex = 0; rightIndex < values.Length; rightIndex++)
                {
                    bool left = values[leftIndex];
                    bool right = values[rightIndex];
                    GameplayTriggerConditionReferenceNode all =
                        GameplayTriggerConditionReferenceNode.All(
                            GameplayTriggerConditionReferenceNode.Literal(left),
                            GameplayTriggerConditionReferenceNode.Literal(right));
                    GameplayTriggerConditionReferenceNode any =
                        GameplayTriggerConditionReferenceNode.Any(
                            GameplayTriggerConditionReferenceNode.Literal(left),
                            GameplayTriggerConditionReferenceNode.Literal(right));
                    AssertProgram(all, occurrence, "All(" + left + "," + right + ")");
                    AssertProgram(any, occurrence, "Any(" + left + "," + right + ")");
                }
            }
        }

        [Test]
        public void ThresholdRuntimeEligibility_UsesDistinctNonZeroPreviousAndCurrentRawValues()
        {
            var nodes = new List<GameplayTriggerConditionReferenceNode>
            {
                GameplayTriggerConditionReferenceNode.Leaf(
                    GameplayTriggerConditionOperation.PreviousScalar,
                    0L,
                    GameplayTriggerComparison.Less),
                GameplayTriggerConditionReferenceNode.Leaf(
                    GameplayTriggerConditionOperation.PreviousScalar,
                    0L,
                    GameplayTriggerComparison.Greater),
                GameplayTriggerConditionReferenceNode.Leaf(
                    GameplayTriggerConditionOperation.CurrentScalar,
                    0L,
                    GameplayTriggerComparison.Greater),
                GameplayTriggerConditionReferenceNode.Leaf(
                    GameplayTriggerConditionOperation.CurrentScalar,
                    0L,
                    GameplayTriggerComparison.Less)
            };
            var routes = new GameplayTriggerRouteDefinition[nodes.Count];
            for (int i = 0; i < routes.Length; i++)
            {
                routes[i] = Route(i + 1, GameplayTriggerOccurrenceKind.Entered, nodes[i]);
            }
            var source = new GameplayTriggerSourceDefinition(
                new GameplayTriggerSourceDefinitionId(1),
                GameplayTriggerSourceKind.Threshold,
                thresholdDirection: GameplayTriggerThresholdDirection.High,
                enterThreshold: Fix64.FromRaw(0L),
                exitThreshold: Fix64.FromRaw(-5L),
                initialSamplePolicy: GameplayTriggerInitialSamplePolicy.PrimeSilently);
            GameplayTriggerRuntime runtime = Runtime(source, routes);
            var subject = new GameplayEntityId(4, 2);
            SubmitScalar(runtime, 0, subject, Fix64.FromRaw(-2L));
            Assert.AreEqual(0, runtime.OccurrenceCount);
            SubmitScalar(runtime, 1, subject, Fix64.FromRaw(3L));
            GameplayTriggerOccurrence occurrence = runtime.GetOccurrence(0);

            Assert.AreEqual(-2L, occurrence.PreviousScalar.RawValue);
            Assert.AreEqual(3L, occurrence.CurrentScalar.RawValue);
            var expectedRoutes = new List<GameplayTriggerRouteId>();
            for (int i = 0; i < nodes.Count; i++)
            {
                if (nodes[i].Evaluate(occurrence))
                    expectedRoutes.Add(new GameplayTriggerRouteId(i + 1));
            }
            Assert.AreEqual(2, expectedRoutes.Count);
            Assert.AreEqual(expectedRoutes.Count, runtime.ActivationCount);
            for (int i = 0; i < expectedRoutes.Count; i++)
                Assert.AreEqual(expectedRoutes[i], runtime.GetActivation(i).RouteId);
        }

        private static List<GameplayTriggerConditionReferenceNode> ConditionMatrix()
        {
            var nodes = new List<GameplayTriggerConditionReferenceNode>
            {
                GameplayTriggerConditionReferenceNode.Literal(true),
                GameplayTriggerConditionReferenceNode.Literal(false),
                GameplayTriggerConditionReferenceNode.Not(
                    GameplayTriggerConditionReferenceNode.Literal(false)),
                GameplayTriggerConditionReferenceNode.Not(
                    GameplayTriggerConditionReferenceNode.Literal(true)),
                GameplayTriggerConditionReferenceNode.Leaf(
                    GameplayTriggerConditionOperation.OccurrenceMask,
                    (long)(GameplayTriggerOccurrenceKind.Entered |
                           GameplayTriggerOccurrenceKind.Pulsed)),
                GameplayTriggerConditionReferenceNode.Leaf(
                    GameplayTriggerConditionOperation.SignalIdEquals,
                    7L),
                GameplayTriggerConditionReferenceNode.Leaf(
                    GameplayTriggerConditionOperation.HasSubject,
                    1L),
                GameplayTriggerConditionReferenceNode.Leaf(
                    GameplayTriggerConditionOperation.HasSubject,
                    0L),
                GameplayTriggerConditionReferenceNode.Leaf(
                    GameplayTriggerConditionOperation.HasInstigator,
                    1L),
                GameplayTriggerConditionReferenceNode.Leaf(
                    GameplayTriggerConditionOperation.HasInstigator,
                    0L),
                GameplayTriggerConditionReferenceNode.All(
                    GameplayTriggerConditionReferenceNode.Leaf(
                        GameplayTriggerConditionOperation.Payload0,
                        long.MinValue,
                        GameplayTriggerComparison.Equal),
                    GameplayTriggerConditionReferenceNode.Any(
                        GameplayTriggerConditionReferenceNode.Leaf(
                            GameplayTriggerConditionOperation.Payload1,
                            0L,
                            GameplayTriggerComparison.LessOrEqual),
                        GameplayTriggerConditionReferenceNode.Leaf(
                            GameplayTriggerConditionOperation.Payload2,
                            long.MaxValue,
                            GameplayTriggerComparison.GreaterOrEqual)))
            };
            GameplayTriggerComparison[] comparisons =
            {
                GameplayTriggerComparison.Equal,
                GameplayTriggerComparison.NotEqual,
                GameplayTriggerComparison.Less,
                GameplayTriggerComparison.LessOrEqual,
                GameplayTriggerComparison.Greater,
                GameplayTriggerComparison.GreaterOrEqual
            };
            for (int i = 0; i < comparisons.Length; i++)
            {
                nodes.Add(GameplayTriggerConditionReferenceNode.Leaf(
                    GameplayTriggerConditionOperation.PreviousScalar,
                    0L,
                    comparisons[i]));
                nodes.Add(GameplayTriggerConditionReferenceNode.Leaf(
                    GameplayTriggerConditionOperation.CurrentScalar,
                    0L,
                    comparisons[i]));
                nodes.Add(GameplayTriggerConditionReferenceNode.Leaf(
                    GameplayTriggerConditionOperation.Payload0,
                    0L,
                    comparisons[i]));
                nodes.Add(GameplayTriggerConditionReferenceNode.Leaf(
                    GameplayTriggerConditionOperation.Payload1,
                    0L,
                    comparisons[i]));
                nodes.Add(GameplayTriggerConditionReferenceNode.Leaf(
                    GameplayTriggerConditionOperation.Payload2,
                    0L,
                    comparisons[i]));
            }
            return nodes;
        }

        private static List<GameplayTriggerConditionReferenceNode> RuntimeConditions() =>
            new List<GameplayTriggerConditionReferenceNode>
            {
                GameplayTriggerConditionReferenceNode.Leaf(
                    GameplayTriggerConditionOperation.HasSubject,
                    1L),
                GameplayTriggerConditionReferenceNode.Leaf(
                    GameplayTriggerConditionOperation.HasInstigator,
                    1L),
                GameplayTriggerConditionReferenceNode.Not(
                    GameplayTriggerConditionReferenceNode.Leaf(
                        GameplayTriggerConditionOperation.SignalIdEquals,
                        8L)),
                GameplayTriggerConditionReferenceNode.All(
                    GameplayTriggerConditionReferenceNode.Leaf(
                        GameplayTriggerConditionOperation.Payload0,
                        0L,
                        GameplayTriggerComparison.Greater),
                    GameplayTriggerConditionReferenceNode.Leaf(
                        GameplayTriggerConditionOperation.Payload1,
                        0L,
                        GameplayTriggerComparison.Less)),
                GameplayTriggerConditionReferenceNode.Any(
                    GameplayTriggerConditionReferenceNode.Leaf(
                        GameplayTriggerConditionOperation.Payload2,
                        42L,
                        GameplayTriggerComparison.Equal),
                    GameplayTriggerConditionReferenceNode.Literal(false)),
                GameplayTriggerConditionReferenceNode.Leaf(
                    GameplayTriggerConditionOperation.OccurrenceMask,
                    (long)GameplayTriggerOccurrenceKind.Pulsed),
                GameplayTriggerConditionReferenceNode.Leaf(
                    GameplayTriggerConditionOperation.PreviousScalar,
                    0L,
                    GameplayTriggerComparison.Equal),
                GameplayTriggerConditionReferenceNode.Leaf(
                    GameplayTriggerConditionOperation.CurrentScalar,
                    0L,
                    GameplayTriggerComparison.Equal)
            };

        private static GameplayTriggerOccurrence[] OccurrenceMatrix() =>
            new[]
            {
                new GameplayTriggerOccurrence(
                    new RuntimeFrame(5),
                    new GameplayTriggerInstanceId(1, 1),
                    GameplayTriggerOccurrenceKind.Pulsed,
                    new GameplayEntityId(3, 2),
                    new GameplayEntityId(8, 4),
                    7,
                    Fix64.FromRaw(-1L),
                    Fix64.FromRaw(1L),
                    long.MinValue,
                    0L,
                    long.MaxValue,
                    0L,
                    0L,
                    0L),
                new GameplayTriggerOccurrence(
                    new RuntimeFrame(6),
                    new GameplayTriggerInstanceId(2, 1),
                    GameplayTriggerOccurrenceKind.Entered,
                    new GameplayEntityId(5, 1),
                    default,
                    0,
                    Fix64.Zero,
                    Fix64.Zero,
                    1L,
                    -1L,
                    0L,
                    0L,
                    2L,
                    6L),
                new GameplayTriggerOccurrence(
                    new RuntimeFrame(7),
                    new GameplayTriggerInstanceId(3, 1),
                    GameplayTriggerOccurrenceKind.Exited,
                    default,
                    default,
                    9,
                    Fix64.FromRaw(long.MinValue),
                    Fix64.FromRaw(long.MaxValue),
                    0L,
                    1L,
                    -1L,
                    0L,
                    3L,
                    4L)
            };

        private static GameplayTriggerRuntime Runtime(
            GameplayTriggerRouteDefinition[] routes)
        {
            var source = new GameplayTriggerSourceDefinition(
                new GameplayTriggerSourceDefinitionId(1),
                GameplayTriggerSourceKind.Signal);
            return Runtime(source, routes);
        }

        private static GameplayTriggerRuntime Runtime(
            GameplayTriggerSourceDefinition source,
            GameplayTriggerRouteDefinition[] routes)
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
                        SourceBinding,
                        new GameplayEntityId(99, 1))
                },
                new IGameplayTriggerActionSink[] { new ConditionSink() },
                out GameplayTriggerCatalog catalog,
                out GameplayTriggerCatalogValidationResult validation),
                validation.ErrorCode.ToString());
            return new GameplayTriggerRuntime(
                catalog,
                new GameplayTriggerRuntimeOptions(
                    4, 16, 4, 16, 32, 32, 32, 32, 8, 16));
        }

        private static GameplayTriggerRouteDefinition Route(
            int routeId,
            GameplayTriggerOccurrenceKind mask,
            GameplayTriggerConditionReferenceNode node) =>
            new GameplayTriggerRouteDefinition(
                new GameplayTriggerRouteId(routeId),
                mask,
                node.Compile(),
                new[]
                {
                    new GameplayTriggerActionInstruction(
                        ActionKind,
                        GameplayTriggerTargetSelector.Owner)
                });

        private static void SubmitScalar(
            GameplayTriggerRuntime runtime,
            long frame,
            GameplayEntityId subject,
            Fix64 value)
        {
            var runtimeFrame = new RuntimeFrame(frame);
            Assert.IsTrue(runtime.TrySubmitScalarBatch(
                runtimeFrame,
                SourceBinding,
                new[] { new GameplayTriggerScalarSample(subject, value) },
                out GameplayTriggerResult submit),
                submit.ErrorCode.ToString());
            Assert.IsTrue(runtime.Evaluate(runtimeFrame).Success);
        }

        private static void AssertProgram(
            GameplayTriggerConditionReferenceNode node,
            in GameplayTriggerOccurrence occurrence,
            string context)
        {
            GameplayTriggerConditionProgram program = node.Compile();
            Assert.AreEqual(
                node.Evaluate(occurrence),
                program.Evaluate(occurrence, new bool[program.MaxStackDepth]),
                context);
        }

        private static void AssertOccurrenceMatchesSignal(
            in GameplayTriggerOccurrence occurrence,
            in GameplayTriggerSignal signal)
        {
            Assert.AreEqual(GameplayTriggerOccurrenceKind.Pulsed, occurrence.Kind);
            Assert.AreEqual(signal.SignalId, occurrence.SignalId);
            Assert.AreEqual(signal.Subject, occurrence.Subject);
            Assert.AreEqual(signal.Instigator, occurrence.Instigator);
            Assert.AreEqual(signal.Payload0, occurrence.Payload0);
            Assert.AreEqual(signal.Payload1, occurrence.Payload1);
            Assert.AreEqual(signal.Payload2, occurrence.Payload2);
            Assert.AreEqual(default(Fix64), occurrence.PreviousScalar);
            Assert.AreEqual(default(Fix64), occurrence.CurrentScalar);
        }

        private sealed class ConditionSink : IGameplayTriggerActionSink
        {
            private int _nextToken = 1;

            public GameplayTriggerActionKindId ActionKindId => ActionKind;
            public GameplayTriggerDeliveryContract DeliveryContract =>
                GameplayTriggerDeliveryContract.SameFrameReserved;

            public bool TryPrepare(
                in GameplayTriggerActionPreparation preparation,
                out int token)
            {
                token = _nextToken++;
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
