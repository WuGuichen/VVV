using System.Collections.Generic;
using MxFramework.Gameplay;
using MxFramework.Gameplay.Triggers;
using MxFramework.Runtime;
using NUnit.Framework;

namespace MxFramework.Tests.Gameplay.Triggers
{
    public sealed class GameplayTriggerPolicyReferenceModelTests
    {
        private static readonly GameplayTriggerSourceBindingId SourceBindingId = new GameplayTriggerSourceBindingId(100);
        private static readonly GameplayTriggerSourceBindingId SecondSourceBindingId = new GameplayTriggerSourceBindingId(101);
        private static readonly GameplayTriggerRouteId RouteId = new GameplayTriggerRouteId(1);
        private static readonly GameplayTriggerActionKindId ActionKind = new GameplayTriggerActionKindId(1000);

        [TestCase(GameplayTriggerStateScope.PerInstance, 1)]
        [TestCase(GameplayTriggerStateScope.PerSubject, 2)]
        [TestCase(GameplayTriggerStateScope.PerInstigator, 3)]
        [TestCase(GameplayTriggerStateScope.PerPair, 4)]
        public void CountScope_FixedTraceMatchesIndependentReference(
            GameplayTriggerStateScope scope,
            int expectedAccepted)
        {
            var policy = new GameplayTriggerActivationPolicy(scope, maxActivations: 1);
            GameplayTriggerRuntime runtime = CreateSignalRuntime(policy, new ReferenceSink());
            GameplayTriggerInstanceId instanceId = runtime.GetInstanceId(0);
            var reference = new GameplayTriggerPolicyReferenceModel();
            var a = new GameplayEntityId(1, 1);
            var b = new GameplayEntityId(2, 1);
            var x = new GameplayEntityId(10, 1);
            var y = new GameplayEntityId(11, 1);
            var z = new GameplayEntityId(12, 1);
            var signals = new[]
            {
                new GameplayTriggerSignal(1, a, x),
                new GameplayTriggerSignal(1, a, x),
                new GameplayTriggerSignal(1, a, y),
                new GameplayTriggerSignal(1, a, z),
                new GameplayTriggerSignal(1, b, x)
            };
            var expected = new List<ReferenceActivation>();

            for (int i = 0; i < signals.Length; i++)
            {
                GameplayTriggerSignal signal = signals[i];
                var occurrence = new GameplayTriggerPolicyReferenceOccurrence(
                    0L,
                    instanceId,
                    GameplayTriggerOccurrenceKind.Pulsed,
                    signal.Subject,
                    signal.Instigator);
                if (reference.Check(RouteId, policy, occurrence) == GameplayTriggerPolicyReferenceDecision.Accepted)
                {
                    reference.Commit(RouteId, policy, occurrence);
                    expected.Add(new ReferenceActivation(RouteId, signal.Subject, signal.Instigator));
                }
            }

            Assert.IsTrue(runtime.TrySubmitSignals(
                RuntimeFrame.Zero,
                SourceBindingId,
                GameplayTriggerSignalOrdering.UnorderedMultiset,
                signals,
                out GameplayTriggerResult submit), submit.ErrorCode.ToString());
            Assert.IsTrue(runtime.Evaluate(RuntimeFrame.Zero).Success);
            Assert.AreEqual(expectedAccepted, expected.Count);
            AssertActivations(expected, runtime);
        }

        [Test]
        public void PolicyStateKey_IsolatesMultipleRoutesAndInstances()
        {
            var policy = new GameplayTriggerActivationPolicy(
                GameplayTriggerStateScope.PerInstance,
                maxActivations: 1);
            var source = new GameplayTriggerSourceDefinition(
                new GameplayTriggerSourceDefinitionId(1),
                GameplayTriggerSourceKind.Signal,
                signalOrdering: GameplayTriggerSignalOrdering.UnorderedMultiset);
            var firstRouteId = new GameplayTriggerRouteId(1);
            var secondRouteId = new GameplayTriggerRouteId(2);
            var routes = new[]
            {
                CreateRoute(firstRouteId, GameplayTriggerOccurrenceKind.Pulsed, policy),
                CreateRoute(secondRouteId, GameplayTriggerOccurrenceKind.Pulsed, policy)
            };
            GameplayTriggerRuntime runtime = CreateRuntime(
                source,
                routes,
                new[]
                {
                    new GameplayTriggerBinding(
                        new GameplayTriggerBindingId(1),
                        new GameplayTriggerDefinitionId(1),
                        SourceBindingId,
                        new GameplayEntityId(99, 1)),
                    new GameplayTriggerBinding(
                        new GameplayTriggerBindingId(2),
                        new GameplayTriggerDefinitionId(1),
                        SecondSourceBindingId,
                        new GameplayEntityId(100, 1))
                },
                new ReferenceSink());
            GameplayTriggerInstanceId firstInstance = runtime.GetInstanceId(0);
            GameplayTriggerInstanceId secondInstance = runtime.GetInstanceId(1);
            var subject = new GameplayEntityId(1, 1);
            var instigator = new GameplayEntityId(10, 1);
            var signals = new[] { new GameplayTriggerSignal(1, subject, instigator) };

            Assert.IsTrue(runtime.TrySubmitSignals(
                RuntimeFrame.Zero,
                SecondSourceBindingId,
                GameplayTriggerSignalOrdering.UnorderedMultiset,
                signals,
                out _));
            Assert.IsTrue(runtime.TrySubmitSignals(
                RuntimeFrame.Zero,
                SourceBindingId,
                GameplayTriggerSignalOrdering.UnorderedMultiset,
                signals,
                out _));
            Assert.IsTrue(runtime.Evaluate(RuntimeFrame.Zero).Success);

            Assert.AreEqual(4, runtime.ActivationCount);
            AssertActivation(runtime.GetActivation(0), firstRouteId, firstInstance, subject, instigator);
            AssertActivation(runtime.GetActivation(1), firstRouteId, secondInstance, subject, instigator);
            AssertActivation(runtime.GetActivation(2), secondRouteId, firstInstance, subject, instigator);
            AssertActivation(runtime.GetActivation(3), secondRouteId, secondInstance, subject, instigator);

            var nextFrame = new RuntimeFrame(1);
            Assert.IsTrue(runtime.TrySubmitSignals(
                nextFrame,
                SourceBindingId,
                GameplayTriggerSignalOrdering.UnorderedMultiset,
                signals,
                out _));
            Assert.IsTrue(runtime.TrySubmitSignals(
                nextFrame,
                SecondSourceBindingId,
                GameplayTriggerSignalOrdering.UnorderedMultiset,
                signals,
                out _));
            Assert.IsTrue(runtime.Evaluate(nextFrame).Success);
            Assert.AreEqual(0, runtime.ActivationCount);
        }

        [Test]
        public void CooldownAndLifetimeCount_InterleavedSubjectsMatchIndependentReference()
        {
            var policy = new GameplayTriggerActivationPolicy(
                GameplayTriggerStateScope.PerSubject,
                maxActivations: 2,
                cooldownFrames: 2);
            GameplayTriggerRuntime runtime = CreateSignalRuntime(policy, new ReferenceSink());
            GameplayTriggerInstanceId instanceId = runtime.GetInstanceId(0);
            var reference = new GameplayTriggerPolicyReferenceModel();
            var a = new GameplayEntityId(1, 1);
            var b = new GameplayEntityId(2, 1);
            long[] frames = { 0L, 1L, 2L, 4L };
            int[] expected = { 2, 0, 2, 0 };

            for (int frameIndex = 0; frameIndex < frames.Length; frameIndex++)
            {
                long frameValue = frames[frameIndex];
                var signals = new[]
                {
                    new GameplayTriggerSignal(1, a),
                    new GameplayTriggerSignal(1, b)
                };
                int referenceAccepted = CountAndCommit(
                    reference,
                    policy,
                    instanceId,
                    frameValue,
                    signals);

                var frame = new RuntimeFrame(frameValue);
                Assert.IsTrue(runtime.TrySubmitSignals(
                    frame,
                    SourceBindingId,
                    GameplayTriggerSignalOrdering.UnorderedMultiset,
                    signals,
                    out GameplayTriggerResult submit), submit.ErrorCode.ToString());
                Assert.IsTrue(runtime.Evaluate(frame).Success);
                Assert.AreEqual(expected[frameIndex], referenceAccepted, "frame=" + frameValue);
                Assert.AreEqual(referenceAccepted, runtime.ActivationCount, "frame=" + frameValue);
            }
        }

        [Test]
        public void CooldownZero_AllowsMultipleAcceptedOccurrencesInSameFrame()
        {
            var policy = new GameplayTriggerActivationPolicy(
                GameplayTriggerStateScope.PerSubject,
                cooldownFrames: 0L);
            GameplayTriggerRuntime runtime = CreateSignalRuntime(policy, new ReferenceSink());
            GameplayTriggerInstanceId instanceId = runtime.GetInstanceId(0);
            var reference = new GameplayTriggerPolicyReferenceModel();
            var subject = new GameplayEntityId(1, 1);
            var signals = new[]
            {
                new GameplayTriggerSignal(1, subject),
                new GameplayTriggerSignal(1, subject),
                new GameplayTriggerSignal(1, subject)
            };

            int referenceAccepted = CountAndCommit(reference, policy, instanceId, 7L, signals);
            var frame = new RuntimeFrame(7);
            Assert.IsTrue(runtime.TrySubmitSignals(
                frame,
                SourceBindingId,
                GameplayTriggerSignalOrdering.UnorderedMultiset,
                signals,
                out _));
            Assert.IsTrue(runtime.Evaluate(frame).Success);
            Assert.AreEqual(3, referenceAccepted);
            Assert.AreEqual(referenceAccepted, runtime.ActivationCount);
        }

        [Test]
        public void StayInterval_ReservationRejectDoesNotAdvanceReferenceOrRuntimeSchedule()
        {
            var policy = new GameplayTriggerActivationPolicy(
                GameplayTriggerStateScope.PerSubject,
                stayIntervalFrames: 2L);
            var sink = new ReferenceSink();
            GameplayTriggerRuntime runtime = CreateOccupancyRuntime(
                GameplayTriggerOccurrenceKind.Stayed,
                policy,
                sink);
            GameplayTriggerInstanceId instanceId = runtime.GetInstanceId(0);
            var reference = new GameplayTriggerPolicyReferenceModel();
            var subject = new GameplayEntityId(1, 1);
            int[] expected = { 0, 0, 0, 1, 0, 1 };

            for (int frameValue = 0; frameValue < expected.Length; frameValue++)
            {
                sink.RejectPrepare = frameValue == 2;
                int referenceAccepted = 0;
                if (frameValue > 0)
                {
                    var occurrence = new GameplayTriggerPolicyReferenceOccurrence(
                        frameValue,
                        instanceId,
                        GameplayTriggerOccurrenceKind.Stayed,
                        subject,
                        presenceEpoch: 1L,
                        presenceStartFrame: 0L,
                        aggregatePresenceEpoch: 1L);
                    if (reference.Check(RouteId, policy, occurrence) == GameplayTriggerPolicyReferenceDecision.Accepted &&
                        !sink.RejectPrepare)
                    {
                        reference.Commit(RouteId, policy, occurrence);
                        referenceAccepted = 1;
                    }
                }

                var frame = new RuntimeFrame(frameValue);
                Assert.IsTrue(runtime.TrySubmitOccupancy(frame, SourceBindingId, new[] { subject }, out _));
                Assert.IsTrue(runtime.Evaluate(frame).Success);
                Assert.AreEqual(expected[frameValue], referenceAccepted, "frame=" + frameValue);
                Assert.AreEqual(referenceAccepted, runtime.ActivationCount, "frame=" + frameValue);
            }
        }

        [Test]
        public void OncePerPresence_RearmsOnReentryButLifetimeCountDoesNotReset()
        {
            var policy = new GameplayTriggerActivationPolicy(
                GameplayTriggerStateScope.PerSubject,
                maxActivations: 2,
                presenceLimit: GameplayTriggerPresenceLimit.OncePerPresence);
            GameplayTriggerRuntime runtime = CreateOccupancyRuntime(
                GameplayTriggerOccurrenceKind.Entered |
                GameplayTriggerOccurrenceKind.Stayed |
                GameplayTriggerOccurrenceKind.Exited,
                policy,
                new ReferenceSink());
            GameplayTriggerInstanceId instanceId = runtime.GetInstanceId(0);
            var reference = new GameplayTriggerPolicyReferenceModel();
            var subject = new GameplayEntityId(1, 1);
            GameplayEntityId[][] submitted =
            {
                new[] { subject },
                new[] { subject },
                System.Array.Empty<GameplayEntityId>(),
                new[] { subject },
                System.Array.Empty<GameplayEntityId>(),
                new[] { subject }
            };
            GameplayTriggerOccurrenceKind[] kinds =
            {
                GameplayTriggerOccurrenceKind.Entered,
                GameplayTriggerOccurrenceKind.Stayed,
                GameplayTriggerOccurrenceKind.Exited,
                GameplayTriggerOccurrenceKind.Entered,
                GameplayTriggerOccurrenceKind.Exited,
                GameplayTriggerOccurrenceKind.Entered
            };
            long[] epochs = { 1L, 1L, 1L, 2L, 2L, 3L };
            long[] starts = { 0L, 0L, 0L, 3L, 3L, 5L };
            int[] expected = { 1, 0, 0, 1, 0, 0 };

            for (int frameValue = 0; frameValue < submitted.Length; frameValue++)
            {
                var occurrence = new GameplayTriggerPolicyReferenceOccurrence(
                    frameValue,
                    instanceId,
                    kinds[frameValue],
                    subject,
                    presenceEpoch: epochs[frameValue],
                    presenceStartFrame: starts[frameValue],
                    aggregatePresenceEpoch: epochs[frameValue]);
                int referenceAccepted = 0;
                if (reference.Check(RouteId, policy, occurrence) == GameplayTriggerPolicyReferenceDecision.Accepted)
                {
                    reference.Commit(RouteId, policy, occurrence);
                    referenceAccepted = 1;
                }

                var frame = new RuntimeFrame(frameValue);
                Assert.IsTrue(runtime.TrySubmitOccupancy(frame, SourceBindingId, submitted[frameValue], out _));
                Assert.IsTrue(runtime.Evaluate(frame).Success);
                Assert.AreEqual(expected[frameValue], referenceAccepted, "frame=" + frameValue);
                Assert.AreEqual(referenceAccepted, runtime.ActivationCount, "frame=" + frameValue);
            }
        }

        [Test]
        public void PerInstanceOncePerPresence_UsesAggregateEpochAcrossOverlappingSubjects()
        {
            var policy = new GameplayTriggerActivationPolicy(
                GameplayTriggerStateScope.PerInstance,
                presenceLimit: GameplayTriggerPresenceLimit.OncePerPresence);
            GameplayTriggerRuntime runtime = CreateOccupancyRuntime(
                GameplayTriggerOccurrenceKind.Entered |
                GameplayTriggerOccurrenceKind.Stayed |
                GameplayTriggerOccurrenceKind.Exited,
                policy,
                new ReferenceSink());
            GameplayTriggerInstanceId instanceId = runtime.GetInstanceId(0);
            var reference = new GameplayTriggerPolicyReferenceModel();
            var a = new GameplayEntityId(1, 1);
            var b = new GameplayEntityId(2, 1);
            GameplayEntityId[][] submitted =
            {
                new[] { a },
                new[] { a, b },
                new[] { a },
                new[] { a, b },
                System.Array.Empty<GameplayEntityId>(),
                new[] { b }
            };
            GameplayTriggerPolicyReferenceOccurrence[][] occurrences =
            {
                new[] { PolicyOccurrence(0, instanceId, GameplayTriggerOccurrenceKind.Entered, a, 1, 0, 1) },
                new[]
                {
                    PolicyOccurrence(1, instanceId, GameplayTriggerOccurrenceKind.Stayed, a, 1, 0, 1),
                    PolicyOccurrence(1, instanceId, GameplayTriggerOccurrenceKind.Entered, b, 1, 1, 1)
                },
                new[]
                {
                    PolicyOccurrence(2, instanceId, GameplayTriggerOccurrenceKind.Stayed, a, 1, 0, 1),
                    PolicyOccurrence(2, instanceId, GameplayTriggerOccurrenceKind.Exited, b, 1, 1, 1)
                },
                new[]
                {
                    PolicyOccurrence(3, instanceId, GameplayTriggerOccurrenceKind.Stayed, a, 1, 0, 1),
                    PolicyOccurrence(3, instanceId, GameplayTriggerOccurrenceKind.Entered, b, 2, 3, 1)
                },
                new[]
                {
                    PolicyOccurrence(4, instanceId, GameplayTriggerOccurrenceKind.Exited, a, 1, 0, 1),
                    PolicyOccurrence(4, instanceId, GameplayTriggerOccurrenceKind.Exited, b, 2, 3, 1)
                },
                new[] { PolicyOccurrence(5, instanceId, GameplayTriggerOccurrenceKind.Entered, b, 3, 5, 2) }
            };
            int[] expected = { 1, 0, 0, 0, 0, 1 };

            for (int frameValue = 0; frameValue < submitted.Length; frameValue++)
            {
                int referenceAccepted = 0;
                for (int i = 0; i < occurrences[frameValue].Length; i++)
                {
                    GameplayTriggerPolicyReferenceOccurrence occurrence = occurrences[frameValue][i];
                    if (reference.Check(RouteId, policy, occurrence) != GameplayTriggerPolicyReferenceDecision.Accepted)
                    {
                        continue;
                    }
                    reference.Commit(RouteId, policy, occurrence);
                    referenceAccepted++;
                }

                var frame = new RuntimeFrame(frameValue);
                Assert.IsTrue(runtime.TrySubmitOccupancy(frame, SourceBindingId, submitted[frameValue], out _));
                Assert.IsTrue(runtime.Evaluate(frame).Success);
                Assert.AreEqual(expected[frameValue], referenceAccepted, "frame=" + frameValue);
                Assert.AreEqual(referenceAccepted, runtime.ActivationCount, "frame=" + frameValue);
            }
        }

        private static int CountAndCommit(
            GameplayTriggerPolicyReferenceModel reference,
            in GameplayTriggerActivationPolicy policy,
            GameplayTriggerInstanceId instanceId,
            long frame,
            IReadOnlyList<GameplayTriggerSignal> signals)
        {
            int accepted = 0;
            for (int i = 0; i < signals.Count; i++)
            {
                GameplayTriggerSignal signal = signals[i];
                var occurrence = new GameplayTriggerPolicyReferenceOccurrence(
                    frame,
                    instanceId,
                    GameplayTriggerOccurrenceKind.Pulsed,
                    signal.Subject,
                    signal.Instigator);
                if (reference.Check(RouteId, policy, occurrence) != GameplayTriggerPolicyReferenceDecision.Accepted)
                {
                    continue;
                }
                reference.Commit(RouteId, policy, occurrence);
                accepted++;
            }
            return accepted;
        }

        private static GameplayTriggerRuntime CreateSignalRuntime(
            in GameplayTriggerActivationPolicy policy,
            ReferenceSink sink)
        {
            var source = new GameplayTriggerSourceDefinition(
                new GameplayTriggerSourceDefinitionId(1),
                GameplayTriggerSourceKind.Signal,
                signalOrdering: GameplayTriggerSignalOrdering.UnorderedMultiset);
            return CreateRuntime(source, GameplayTriggerOccurrenceKind.Pulsed, policy, sink);
        }

        private static GameplayTriggerRuntime CreateOccupancyRuntime(
            GameplayTriggerOccurrenceKind occurrenceMask,
            in GameplayTriggerActivationPolicy policy,
            ReferenceSink sink)
        {
            var source = new GameplayTriggerSourceDefinition(
                new GameplayTriggerSourceDefinitionId(1),
                GameplayTriggerSourceKind.Occupancy);
            return CreateRuntime(source, occurrenceMask, policy, sink);
        }

        private static GameplayTriggerRuntime CreateRuntime(
            GameplayTriggerSourceDefinition source,
            GameplayTriggerOccurrenceKind occurrenceMask,
            in GameplayTriggerActivationPolicy policy,
            ReferenceSink sink)
        {
            GameplayTriggerRouteDefinition route = CreateRoute(RouteId, occurrenceMask, policy);
            return CreateRuntime(
                source,
                new[] { route },
                new[]
                {
                    new GameplayTriggerBinding(
                        new GameplayTriggerBindingId(1),
                        new GameplayTriggerDefinitionId(1),
                        SourceBindingId,
                        new GameplayEntityId(99, 1))
                },
                sink);
        }

        private static GameplayTriggerRuntime CreateRuntime(
            GameplayTriggerSourceDefinition source,
            GameplayTriggerRouteDefinition[] routes,
            GameplayTriggerBinding[] bindings,
            ReferenceSink sink)
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
                bindings,
                new IGameplayTriggerActionSink[] { sink },
                out GameplayTriggerCatalog catalog,
                out GameplayTriggerCatalogValidationResult validation),
                validation.ErrorCode.ToString());
            return new GameplayTriggerRuntime(
                catalog,
                new GameplayTriggerRuntimeOptions(8, 32, 32, 32, 64, 32, 32, 32, 8, 16));
        }

        private static GameplayTriggerRouteDefinition CreateRoute(
            GameplayTriggerRouteId routeId,
            GameplayTriggerOccurrenceKind occurrenceMask,
            in GameplayTriggerActivationPolicy policy) =>
            new GameplayTriggerRouteDefinition(
                routeId,
                occurrenceMask,
                GameplayTriggerConditionProgram.Always,
                new[]
                {
                    new GameplayTriggerActionInstruction(
                        ActionKind,
                        GameplayTriggerTargetSelector.Owner)
                },
                policy);

        private static GameplayTriggerPolicyReferenceOccurrence PolicyOccurrence(
            long frame,
            GameplayTriggerInstanceId instanceId,
            GameplayTriggerOccurrenceKind kind,
            GameplayEntityId subject,
            long presenceEpoch,
            long presenceStartFrame,
            long aggregatePresenceEpoch) =>
            new GameplayTriggerPolicyReferenceOccurrence(
                frame,
                instanceId,
                kind,
                subject,
                presenceEpoch: presenceEpoch,
                presenceStartFrame: presenceStartFrame,
                aggregatePresenceEpoch: aggregatePresenceEpoch);

        private static void AssertActivations(
            IReadOnlyList<ReferenceActivation> expected,
            GameplayTriggerRuntime runtime)
        {
            Assert.AreEqual(expected.Count, runtime.ActivationCount);
            for (int i = 0; i < expected.Count; i++)
            {
                ReferenceActivation left = expected[i];
                GameplayTriggerActivation right = runtime.GetActivation(i);
                Assert.AreEqual(left.RouteId, right.RouteId, "activation=" + i);
                Assert.AreEqual(left.Subject, right.Occurrence.Subject, "activation=" + i);
                Assert.AreEqual(left.Instigator, right.Occurrence.Instigator, "activation=" + i);
            }
        }

        private static void AssertActivation(
            GameplayTriggerActivation activation,
            GameplayTriggerRouteId routeId,
            GameplayTriggerInstanceId instanceId,
            GameplayEntityId subject,
            GameplayEntityId instigator)
        {
            Assert.AreEqual(routeId, activation.RouteId);
            Assert.AreEqual(instanceId, activation.Occurrence.InstanceId);
            Assert.AreEqual(subject, activation.Occurrence.Subject);
            Assert.AreEqual(instigator, activation.Occurrence.Instigator);
        }

        private readonly struct ReferenceActivation
        {
            public ReferenceActivation(
                GameplayTriggerRouteId routeId,
                GameplayEntityId subject,
                GameplayEntityId instigator)
            {
                RouteId = routeId;
                Subject = subject;
                Instigator = instigator;
            }

            public GameplayTriggerRouteId RouteId { get; }
            public GameplayEntityId Subject { get; }
            public GameplayEntityId Instigator { get; }
        }

        private sealed class ReferenceSink : IGameplayTriggerActionSink
        {
            public GameplayTriggerActionKindId ActionKindId => ActionKind;
            public GameplayTriggerDeliveryContract DeliveryContract =>
                GameplayTriggerDeliveryContract.SameFrameReserved;
            public bool RejectPrepare { get; set; }

            public bool TryPrepare(
                in GameplayTriggerActionPreparation preparation,
                out int token)
            {
                token = preparation.ActionIndex + 1;
                return !RejectPrepare;
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
