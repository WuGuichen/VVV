using System.Collections.Generic;
using MxFramework.Core.Math;
using MxFramework.Gameplay;
using MxFramework.Gameplay.Triggers;
using MxFramework.Runtime;
using NUnit.Framework;

namespace MxFramework.Tests.Gameplay.Triggers
{
    public sealed class GameplayTriggerAuthorityProjectionReferenceModelTests
    {
        private static readonly GameplayTriggerDefinitionId DefinitionId =
            new GameplayTriggerDefinitionId(1);
        private static readonly GameplayTriggerSourceDefinitionId SourceDefinitionId =
            new GameplayTriggerSourceDefinitionId(1);
        private static readonly GameplayTriggerRouteId RouteId =
            new GameplayTriggerRouteId(1);
        private static readonly GameplayTriggerBindingId BindingId =
            new GameplayTriggerBindingId(1);
        private static readonly GameplayTriggerSourceBindingId SourceBindingId =
            new GameplayTriggerSourceBindingId(100);
        private static readonly GameplayTriggerActionKindId ActionKindId =
            new GameplayTriggerActionKindId(1001);
        private static readonly GameplayEntityId Owner = new GameplayEntityId(99, 1);

        [Test]
        public void MultiFrameOccupancyProjection_PermutationsMatchIndependentAuthorityOracle()
        {
            var policy = new GameplayTriggerActivationPolicy(
                GameplayTriggerStateScope.PerSubject,
                maxActivations: 3,
                cooldownFrames: 2,
                stayIntervalFrames: 2);
            GameplayTriggerOccurrenceKind mask =
                GameplayTriggerOccurrenceKind.Entered |
                GameplayTriggerOccurrenceKind.Stayed;
            GameplayTriggerActionInstruction action = Action();
            GameplayTriggerRuntime first = Runtime(mask, policy, action, new ProjectionSink());
            GameplayTriggerRuntime second = Runtime(mask, policy, action, new ProjectionSink());
            GameplayTriggerInstanceId instanceId = first.GetInstanceId(0);
            var reference = new GameplayTriggerAuthorityProjectionReferenceModel(
                instanceId,
                DefinitionId,
                BindingId,
                Owner,
                RouteId,
                mask,
                policy,
                action);
            var firstProjection = new GameplayTriggerRuntimeAuthorityProjection();
            var secondProjection = new GameplayTriggerRuntimeAuthorityProjection();
            var a = new GameplayEntityId(1, 1);
            var b = new GameplayEntityId(2, 1);
            GameplayEntityId[][] firstInputs =
            {
                new[] { b, a, a },
                new[] { a, b },
                new[] { b },
                new GameplayEntityId[0],
                new[] { a }
            };
            GameplayEntityId[][] secondInputs =
            {
                new[] { a, b },
                new[] { b, a, b },
                new[] { b },
                new GameplayEntityId[0],
                new[] { a, a }
            };

            for (int frame = 0; frame < firstInputs.Length; frame++)
            {
                Submit(first, frame, firstInputs[frame]);
                Submit(second, frame, secondInputs[frame]);
                reference.EvaluateOccupancy(frame, firstInputs[frame]);
                firstProjection.Capture(first);
                secondProjection.Capture(second);

                AssertProjection(reference, firstProjection, "first frame=" + frame);
                AssertProjection(reference, secondProjection, "second frame=" + frame);
            }

            Assert.AreEqual(2L, reference.AggregatePresenceEpoch);
            Assert.AreEqual(5L, reference.NextActivationId);
        }

        [Test]
        public void ReservationReject_ProjectionKeepsSourceButNotPolicyOrAllocatorAuthority()
        {
            var policy = new GameplayTriggerActivationPolicy(
                GameplayTriggerStateScope.PerSubject,
                maxActivations: 1,
                cooldownFrames: 10);
            GameplayTriggerActionInstruction action = Action();
            var sink = new ProjectionSink { RejectPrepare = true };
            GameplayTriggerRuntime runtime = Runtime(
                GameplayTriggerOccurrenceKind.Entered,
                policy,
                action,
                sink);
            GameplayTriggerInstanceId instanceId = runtime.GetInstanceId(0);
            var reference = new GameplayTriggerAuthorityProjectionReferenceModel(
                instanceId,
                DefinitionId,
                BindingId,
                Owner,
                RouteId,
                GameplayTriggerOccurrenceKind.Entered,
                policy,
                action);
            var projection = new GameplayTriggerRuntimeAuthorityProjection();
            var subject = new GameplayEntityId(1, 1);

            Submit(runtime, 0, subject);
            reference.EvaluateOccupancy(0, new[] { subject }, reservationAccepted: false);
            projection.Capture(runtime);

            AssertProjection(reference, projection, "rejected Entered");
            Assert.AreEqual(1, sink.PrepareCount);
            Assert.AreEqual(0, projection.RouteStates.Count);
            Assert.AreEqual(1L, projection.NextActivationId);

            Submit(runtime, 1, subject);
            reference.EvaluateOccupancy(1, new[] { subject });
            projection.Capture(runtime);

            AssertProjection(reference, projection, "next-frame Stayed");
            Assert.AreEqual(1, sink.PrepareCount);
        }

        [Test]
        public void ThresholdDisable_ProjectionExposesPrimedScalarAndDisabledState()
        {
            var source = new GameplayTriggerSourceDefinition(
                SourceDefinitionId,
                GameplayTriggerSourceKind.Threshold,
                thresholdDirection: GameplayTriggerThresholdDirection.High,
                enterThreshold: Fix64.FromInt(10),
                exitThreshold: Fix64.FromInt(8),
                initialSamplePolicy: GameplayTriggerInitialSamplePolicy.EmitEnteredIfActive);
            GameplayTriggerRuntime runtime = Runtime(
                source,
                new[]
                {
                    Route(
                        RouteId,
                        GameplayTriggerOccurrenceKind.Entered,
                        default)
                },
                new GameplayTriggerBinding(
                    BindingId,
                    DefinitionId,
                    SourceBindingId,
                    Owner),
                new ProjectionSink());
            var projection = new GameplayTriggerRuntimeAuthorityProjection();
            var subject = new GameplayEntityId(7, 3);
            var frame = RuntimeFrame.Zero;
            Assert.IsTrue(runtime.TrySubmitScalarBatch(
                frame,
                SourceBindingId,
                new[]
                {
                    new GameplayTriggerScalarSample(subject, Fix64.FromInt(12))
                },
                out GameplayTriggerResult submit),
                submit.ErrorCode.ToString());
            Assert.IsTrue(runtime.Evaluate(frame).Success);
            projection.Capture(runtime);

            Assert.AreEqual(1, projection.Relations.Count);
            Assert.IsTrue(projection.Relations[0].Primed);
            Assert.IsTrue(projection.Relations[0].Active);
            Assert.AreEqual(Fix64.FromInt(12), projection.Relations[0].Scalar);
            Assert.AreEqual(1L, projection.Relations[0].PresenceEpoch);

            Assert.IsTrue(runtime.TrySetEnabled(runtime.GetInstanceId(0), false, out _));
            projection.Capture(runtime);

            Assert.IsFalse(projection.Instances[0].Enabled);
            Assert.IsFalse(projection.Relations[0].Primed);
            Assert.IsFalse(projection.Relations[0].Active);
            Assert.AreEqual(Fix64.FromInt(12), projection.Relations[0].Scalar);
        }

        [Test]
        public void SignalScopeProjection_ExposesPerInstigatorAndPerPairKeys()
        {
            var source = new GameplayTriggerSourceDefinition(
                SourceDefinitionId,
                GameplayTriggerSourceKind.Signal);
            GameplayTriggerRuntime runtime = Runtime(
                source,
                new[]
                {
                    Route(
                        new GameplayTriggerRouteId(20),
                        GameplayTriggerOccurrenceKind.Pulsed,
                        new GameplayTriggerActivationPolicy(
                            GameplayTriggerStateScope.PerPair)),
                    Route(
                        new GameplayTriggerRouteId(10),
                        GameplayTriggerOccurrenceKind.Pulsed,
                        new GameplayTriggerActivationPolicy(
                            GameplayTriggerStateScope.PerInstigator))
                },
                new GameplayTriggerBinding(
                    BindingId,
                    DefinitionId,
                    SourceBindingId,
                    Owner),
                new ProjectionSink());
            var subject = new GameplayEntityId(3, 2);
            var instigator = new GameplayEntityId(8, 4);
            Assert.IsTrue(runtime.TrySubmitSignals(
                RuntimeFrame.Zero,
                SourceBindingId,
                GameplayTriggerSignalOrdering.UnorderedMultiset,
                new[] { new GameplayTriggerSignal(5, subject, instigator) },
                out GameplayTriggerResult submit),
                submit.ErrorCode.ToString());
            Assert.IsTrue(runtime.Evaluate(RuntimeFrame.Zero).Success);
            var projection = new GameplayTriggerRuntimeAuthorityProjection();
            projection.Capture(runtime);

            Assert.AreEqual(2, projection.RouteStates.Count);
            Assert.AreEqual(new GameplayTriggerRouteId(10), projection.RouteStates[0].RouteId);
            Assert.AreEqual(default(GameplayEntityId), projection.RouteStates[0].Subject);
            Assert.AreEqual(instigator, projection.RouteStates[0].Instigator);
            Assert.AreEqual(new GameplayTriggerRouteId(20), projection.RouteStates[1].RouteId);
            Assert.AreEqual(subject, projection.RouteStates[1].Subject);
            Assert.AreEqual(instigator, projection.RouteStates[1].Instigator);
        }

        [Test]
        public void OncePerPresenceProjection_ExposesCommittedPresenceEpoch()
        {
            var policy = new GameplayTriggerActivationPolicy(
                GameplayTriggerStateScope.PerSubject,
                presenceLimit: GameplayTriggerPresenceLimit.OncePerPresence);
            GameplayTriggerRuntime runtime = Runtime(
                GameplayTriggerOccurrenceKind.Entered,
                policy,
                Action(),
                new ProjectionSink());
            var subject = new GameplayEntityId(4, 2);

            Submit(runtime, 0, subject);
            var projection = new GameplayTriggerRuntimeAuthorityProjection();
            projection.Capture(runtime);

            Assert.AreEqual(1, projection.RouteStates.Count);
            Assert.AreEqual(1L, projection.RouteStates[0].LastActivatedPresenceEpoch);
            Assert.AreEqual(1L, projection.Relations[0].PresenceEpoch);
        }

        [Test]
        public void CanonicalProjection_HandlesReverseHistoryAndMultipleIdentities()
        {
            GameplayTriggerRuntime reverseHistory = Runtime(
                GameplayTriggerOccurrenceKind.Entered,
                new GameplayTriggerActivationPolicy(
                    GameplayTriggerStateScope.PerSubject),
                Action(),
                new ProjectionSink());
            var a = new GameplayEntityId(1, 1);
            var b = new GameplayEntityId(2, 1);
            Submit(reverseHistory, 0, b);
            Submit(reverseHistory, 1, a, b);
            Assert.AreEqual(b, reverseHistory.GetRelationAuthorityState(0).Subject);
            Assert.AreEqual(a, reverseHistory.GetRelationAuthorityState(1).Subject);

            var reverseProjection = new GameplayTriggerRuntimeAuthorityProjection();
            reverseProjection.Capture(reverseHistory);
            Assert.AreEqual(a, reverseProjection.Relations[0].Subject);
            Assert.AreEqual(b, reverseProjection.Relations[1].Subject);
            Assert.AreEqual(a, reverseProjection.RouteStates[0].Subject);
            Assert.AreEqual(b, reverseProjection.RouteStates[1].Subject);

            GameplayTriggerRuntime multiple = MultipleIdentityRuntime();
            Assert.IsTrue(multiple.TrySubmitOccupancy(
                RuntimeFrame.Zero,
                new GameplayTriggerSourceBindingId(200),
                new[] { b },
                out _));
            Assert.IsTrue(multiple.TrySubmitOccupancy(
                RuntimeFrame.Zero,
                new GameplayTriggerSourceBindingId(100),
                new[] { a },
                out _));
            Assert.IsTrue(multiple.Evaluate(RuntimeFrame.Zero).Success);
            var multipleProjection = new GameplayTriggerRuntimeAuthorityProjection();
            multipleProjection.Capture(multiple);

            Assert.AreEqual(2, multipleProjection.Instances.Count);
            Assert.AreEqual(new GameplayTriggerBindingId(10), multipleProjection.Instances[0].BindingId);
            Assert.AreEqual(new GameplayTriggerBindingId(20), multipleProjection.Instances[1].BindingId);
            Assert.AreEqual(new GameplayTriggerInstanceId(1, 1), multipleProjection.Relations[0].InstanceId);
            Assert.AreEqual(new GameplayTriggerInstanceId(2, 1), multipleProjection.Relations[1].InstanceId);
            Assert.AreEqual(new GameplayTriggerRouteId(10), multipleProjection.RouteStates[0].RouteId);
            Assert.AreEqual(new GameplayTriggerRouteId(20), multipleProjection.RouteStates[1].RouteId);

            AssertSyntheticCanonicalSort();
        }

        private static GameplayTriggerActionInstruction Action() =>
            new GameplayTriggerActionInstruction(
                ActionKindId,
                GameplayTriggerTargetSelector.Subject,
                payload0: 11,
                payload1: 12,
                payload2: 13);

        private static GameplayTriggerRouteDefinition Route(
            GameplayTriggerRouteId routeId,
            GameplayTriggerOccurrenceKind occurrenceMask,
            GameplayTriggerActivationPolicy policy) =>
            new GameplayTriggerRouteDefinition(
                routeId,
                occurrenceMask,
                GameplayTriggerConditionProgram.Always,
                new[] { Action() },
                policy);

        private static GameplayTriggerRuntime Runtime(
            GameplayTriggerOccurrenceKind occurrenceMask,
            GameplayTriggerActivationPolicy policy,
            GameplayTriggerActionInstruction action,
            IGameplayTriggerActionSink sink)
        {
            var source = new GameplayTriggerSourceDefinition(
                SourceDefinitionId,
                GameplayTriggerSourceKind.Occupancy);
            var route = new GameplayTriggerRouteDefinition(
                RouteId,
                occurrenceMask,
                GameplayTriggerConditionProgram.Always,
                new[] { action },
                policy);
            return Runtime(
                source,
                new[] { route },
                new GameplayTriggerBinding(
                    BindingId,
                    DefinitionId,
                    SourceBindingId,
                    Owner),
                sink);
        }

        private static GameplayTriggerRuntime Runtime(
            GameplayTriggerSourceDefinition source,
            GameplayTriggerRouteDefinition[] routes,
            GameplayTriggerBinding binding,
            IGameplayTriggerActionSink sink)
        {
            var definition = new GameplayTriggerDefinition(
                DefinitionId,
                definitionVersion: 1,
                source.SourceDefinitionId,
                routes);
            Assert.IsTrue(GameplayTriggerCatalog.TryCreate(
                new[] { source },
                new[] { definition },
                new[] { binding },
                new[] { sink },
                out GameplayTriggerCatalog catalog,
                out GameplayTriggerCatalogValidationResult validation),
                validation.ErrorCode.ToString());
            return new GameplayTriggerRuntime(
                catalog,
                new GameplayTriggerRuntimeOptions(
                    4, 16, 16, 8, 16, 16, 16, 16, 4, 8));
        }

        private static GameplayTriggerRuntime MultipleIdentityRuntime()
        {
            var source10 = new GameplayTriggerSourceDefinition(
                new GameplayTriggerSourceDefinitionId(10),
                GameplayTriggerSourceKind.Occupancy);
            var source20 = new GameplayTriggerSourceDefinition(
                new GameplayTriggerSourceDefinitionId(20),
                GameplayTriggerSourceKind.Occupancy);
            var definition10 = new GameplayTriggerDefinition(
                new GameplayTriggerDefinitionId(10),
                1,
                source10.SourceDefinitionId,
                new[]
                {
                    Route(
                        new GameplayTriggerRouteId(10),
                        GameplayTriggerOccurrenceKind.Entered,
                        new GameplayTriggerActivationPolicy(
                            GameplayTriggerStateScope.PerSubject))
                });
            var definition20 = new GameplayTriggerDefinition(
                new GameplayTriggerDefinitionId(20),
                1,
                source20.SourceDefinitionId,
                new[]
                {
                    Route(
                        new GameplayTriggerRouteId(20),
                        GameplayTriggerOccurrenceKind.Entered,
                        new GameplayTriggerActivationPolicy(
                            GameplayTriggerStateScope.PerSubject))
                });
            var binding10 = new GameplayTriggerBinding(
                new GameplayTriggerBindingId(10),
                definition10.DefinitionId,
                new GameplayTriggerSourceBindingId(100),
                Owner);
            var binding20 = new GameplayTriggerBinding(
                new GameplayTriggerBindingId(20),
                definition20.DefinitionId,
                new GameplayTriggerSourceBindingId(200),
                Owner);
            Assert.IsTrue(GameplayTriggerCatalog.TryCreate(
                new[] { source20, source10 },
                new[] { definition20, definition10 },
                new[] { binding20, binding10 },
                new IGameplayTriggerActionSink[] { new ProjectionSink() },
                out GameplayTriggerCatalog catalog,
                out GameplayTriggerCatalogValidationResult validation),
                validation.ErrorCode.ToString());
            return new GameplayTriggerRuntime(
                catalog,
                new GameplayTriggerRuntimeOptions(
                    4, 16, 16, 8, 16, 16, 16, 16, 4, 8));
        }

        private static void AssertSyntheticCanonicalSort()
        {
            var instances = new List<GameplayTriggerInstanceAuthorityState>
            {
                new GameplayTriggerInstanceAuthorityState(
                    new GameplayTriggerInstanceId(2, 1),
                    new GameplayTriggerBindingId(20),
                    new GameplayTriggerSourceBindingId(200),
                    new GameplayTriggerDefinitionId(20),
                    1,
                    new GameplayTriggerSourceDefinitionId(20),
                    Owner,
                    true,
                    0,
                    0L),
                new GameplayTriggerInstanceAuthorityState(
                    new GameplayTriggerInstanceId(1, 1),
                    new GameplayTriggerBindingId(10),
                    new GameplayTriggerSourceBindingId(100),
                    new GameplayTriggerDefinitionId(10),
                    1,
                    new GameplayTriggerSourceDefinitionId(10),
                    Owner,
                    true,
                    0,
                    0L)
            };
            var relations = new List<GameplayTriggerRelationAuthorityState>
            {
                new GameplayTriggerRelationAuthorityState(
                    new GameplayTriggerInstanceId(2, 1),
                    new GameplayEntityId(2, 1),
                    true,
                    false,
                    default,
                    1L,
                    0L),
                new GameplayTriggerRelationAuthorityState(
                    new GameplayTriggerInstanceId(1, 1),
                    new GameplayEntityId(1, 1),
                    true,
                    false,
                    default,
                    1L,
                    0L)
            };
            var routeStates = new List<GameplayTriggerRouteAuthorityState>
            {
                new GameplayTriggerRouteAuthorityState(
                    new GameplayTriggerInstanceId(2, 1),
                    new GameplayTriggerRouteId(20),
                    new GameplayEntityId(2, 1),
                    default,
                    1,
                    true,
                    0L,
                    0L,
                    false,
                    0L),
                new GameplayTriggerRouteAuthorityState(
                    new GameplayTriggerInstanceId(1, 1),
                    new GameplayTriggerRouteId(10),
                    new GameplayEntityId(1, 1),
                    default,
                    1,
                    true,
                    0L,
                    0L,
                    false,
                    0L)
            };

            GameplayTriggerRuntimeAuthorityProjection.CanonicalizeInstances(instances);
            GameplayTriggerRuntimeAuthorityProjection.CanonicalizeRelations(relations);
            GameplayTriggerRuntimeAuthorityProjection.CanonicalizeRouteStates(routeStates);

            Assert.AreEqual(new GameplayTriggerInstanceId(1, 1), instances[0].InstanceId);
            Assert.AreEqual(new GameplayTriggerInstanceId(1, 1), relations[0].InstanceId);
            Assert.AreEqual(new GameplayTriggerInstanceId(1, 1), routeStates[0].InstanceId);
        }

        private static void Submit(
            GameplayTriggerRuntime runtime,
            long frame,
            params GameplayEntityId[] subjects)
        {
            var runtimeFrame = new RuntimeFrame(frame);
            Assert.IsTrue(runtime.TrySubmitOccupancy(
                runtimeFrame,
                SourceBindingId,
                subjects,
                out GameplayTriggerResult submit),
                submit.ErrorCode.ToString());
            Assert.IsTrue(runtime.Evaluate(runtimeFrame).Success);
        }

        private static void AssertProjection(
            GameplayTriggerAuthorityProjectionReferenceModel expected,
            GameplayTriggerRuntimeAuthorityProjection actual,
            string context)
        {
            Assert.AreEqual(1, actual.Instances.Count, context);
            GameplayTriggerInstanceAuthorityState instance = actual.Instances[0];
            Assert.AreEqual(new GameplayTriggerInstanceId(1, 1), instance.InstanceId, context);
            Assert.AreEqual(BindingId, instance.BindingId, context);
            Assert.IsTrue(instance.Enabled, context);
            Assert.AreEqual(expected.ActiveCount, instance.ActiveCount, context);
            Assert.AreEqual(
                expected.AggregatePresenceEpoch,
                instance.AggregatePresenceEpoch,
                context);
            AssertRelations(expected.Relations, actual.Relations, context);
            AssertRouteStates(expected.RouteStates, actual.RouteStates, context);
            AssertOccurrences(expected.Occurrences, actual.Occurrences, context);
            AssertActivations(expected.Activations, actual.Activations, context);
            AssertIntents(expected.Intents, actual.Intents, context);
            Assert.AreEqual(expected.NextActivationId, actual.NextActivationId, context);
        }

        private static void AssertRelations(
            IReadOnlyList<GameplayTriggerReferenceRelationState> expected,
            IReadOnlyList<GameplayTriggerRelationAuthorityState> actual,
            string context)
        {
            Assert.AreEqual(expected.Count, actual.Count, context);
            for (int i = 0; i < expected.Count; i++)
            {
                Assert.AreEqual(expected[i].InstanceId, actual[i].InstanceId, context);
                Assert.AreEqual(expected[i].Subject, actual[i].Subject, context);
                Assert.AreEqual(expected[i].Active, actual[i].Active, context);
                Assert.AreEqual(expected[i].Primed, actual[i].Primed, context);
                Assert.AreEqual(expected[i].Scalar, actual[i].Scalar, context);
                Assert.AreEqual(expected[i].PresenceEpoch, actual[i].PresenceEpoch, context);
                Assert.AreEqual(
                    expected[i].PresenceStartFrame,
                    actual[i].PresenceStartFrame,
                    context);
            }
        }

        private static void AssertRouteStates(
            IReadOnlyList<GameplayTriggerPolicyReferenceState> expected,
            IReadOnlyList<GameplayTriggerRouteAuthorityState> actual,
            string context)
        {
            Assert.AreEqual(expected.Count, actual.Count, context);
            for (int i = 0; i < expected.Count; i++)
            {
                Assert.AreEqual(expected[i].InstanceId, actual[i].InstanceId, context);
                Assert.AreEqual(expected[i].RouteId, actual[i].RouteId, context);
                Assert.AreEqual(expected[i].Subject, actual[i].Subject, context);
                Assert.AreEqual(expected[i].Instigator, actual[i].Instigator, context);
                Assert.AreEqual(expected[i].ActivationCount, actual[i].ActivationCount, context);
                Assert.AreEqual(expected[i].HasActivated, actual[i].HasActivated, context);
                Assert.AreEqual(
                    expected[i].LastActivationFrame,
                    actual[i].LastActivationFrame,
                    context);
                Assert.AreEqual(
                    expected[i].LastActivatedPresenceEpoch,
                    actual[i].LastActivatedPresenceEpoch,
                    context);
                Assert.AreEqual(expected[i].HasStaySchedule, actual[i].HasStaySchedule, context);
                Assert.AreEqual(expected[i].NextStayFrame, actual[i].NextStayFrame, context);
            }
        }

        private static void AssertOccurrences(
            IReadOnlyList<GameplayTriggerReferenceOccurrence> expected,
            IReadOnlyList<GameplayTriggerOccurrence> actual,
            string context)
        {
            Assert.AreEqual(expected.Count, actual.Count, context);
            for (int i = 0; i < expected.Count; i++)
                AssertOccurrence(expected[i], actual[i], context + " occurrence=" + i);
        }

        private static void AssertActivations(
            IReadOnlyList<GameplayTriggerAuthorityReferenceActivation> expected,
            IReadOnlyList<GameplayTriggerActivation> actual,
            string context)
        {
            Assert.AreEqual(expected.Count, actual.Count, context);
            for (int i = 0; i < expected.Count; i++)
            {
                Assert.AreEqual(expected[i].ActivationId, actual[i].ActivationId, context);
                Assert.AreEqual(expected[i].DefinitionId, actual[i].DefinitionId, context);
                Assert.AreEqual(expected[i].BindingId, actual[i].BindingId, context);
                Assert.AreEqual(expected[i].RouteId, actual[i].RouteId, context);
                AssertOccurrence(expected[i].Occurrence, actual[i].Occurrence, context);
            }
        }

        private static void AssertIntents(
            IReadOnlyList<GameplayTriggerAuthorityReferenceIntent> expected,
            IReadOnlyList<GameplayTriggerActionIntent> actual,
            string context)
        {
            Assert.AreEqual(expected.Count, actual.Count, context);
            for (int i = 0; i < expected.Count; i++)
            {
                Assert.AreEqual(expected[i].ActivationId, actual[i].ActivationId, context);
                Assert.AreEqual(expected[i].Occurrence.Frame, actual[i].Frame.Value, context);
                Assert.AreEqual(expected[i].BindingId, actual[i].BindingId, context);
                Assert.AreEqual(expected[i].Owner, actual[i].Owner, context);
                Assert.AreEqual(expected[i].Target, actual[i].Target, context);
                Assert.AreEqual(expected[i].RouteId, actual[i].RouteId, context);
                Assert.AreEqual(expected[i].ActionIndex, actual[i].ActionIndex, context);
                AssertOccurrence(expected[i].Occurrence, actual[i].Occurrence, context);
                AssertInstruction(expected[i].Instruction, actual[i].Instruction, context);
            }
        }

        private static void AssertOccurrence(
            in GameplayTriggerReferenceOccurrence expected,
            in GameplayTriggerOccurrence actual,
            string context)
        {
            Assert.AreEqual(expected.Frame, actual.Frame.Value, context);
            Assert.AreEqual(expected.InstanceId, actual.InstanceId, context);
            Assert.AreEqual(expected.Kind, actual.Kind, context);
            Assert.AreEqual(expected.Subject, actual.Subject, context);
            Assert.AreEqual(expected.Instigator, actual.Instigator, context);
            Assert.AreEqual(expected.SignalId, actual.SignalId, context);
            Assert.AreEqual(expected.PreviousScalar, actual.PreviousScalar, context);
            Assert.AreEqual(expected.CurrentScalar, actual.CurrentScalar, context);
            Assert.AreEqual(expected.Payload0, actual.Payload0, context);
            Assert.AreEqual(expected.Payload1, actual.Payload1, context);
            Assert.AreEqual(expected.Payload2, actual.Payload2, context);
            Assert.AreEqual(expected.OrderingValue, actual.OrderingValue, context);
            Assert.AreEqual(expected.PresenceEpoch, actual.PresenceEpoch, context);
            Assert.AreEqual(expected.PresenceStartFrame, actual.PresenceStartFrame, context);
        }

        private static void AssertInstruction(
            in GameplayTriggerActionInstruction expected,
            in GameplayTriggerActionInstruction actual,
            string context)
        {
            Assert.AreEqual(expected.ActionKindId, actual.ActionKindId, context);
            Assert.AreEqual(expected.TargetSelector, actual.TargetSelector, context);
            Assert.AreEqual(expected.TargetSlotId, actual.TargetSlotId, context);
            Assert.AreEqual(expected.Payload0, actual.Payload0, context);
            Assert.AreEqual(expected.Payload1, actual.Payload1, context);
            Assert.AreEqual(expected.Payload2, actual.Payload2, context);
        }

        private sealed class ProjectionSink : IGameplayTriggerActionSink
        {
            private int _nextToken = 1;

            public GameplayTriggerActionKindId ActionKindId =>
                GameplayTriggerAuthorityProjectionReferenceModelTests.ActionKindId;
            public GameplayTriggerDeliveryContract DeliveryContract =>
                GameplayTriggerDeliveryContract.SameFrameReserved;
            public bool RejectPrepare { get; set; }
            public int PrepareCount { get; private set; }

            public bool TryPrepare(
                in GameplayTriggerActionPreparation preparation,
                out int token)
            {
                PrepareCount++;
                token = RejectPrepare ? 0 : _nextToken++;
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
