using MxFramework.Gameplay;
using MxFramework.Gameplay.Triggers;
using MxFramework.Runtime;
using NUnit.Framework;

namespace MxFramework.Tests.Gameplay.Triggers
{
    public sealed class GameplayTriggerLifecycleReferenceModelTests
    {
        private static readonly GameplayTriggerSourceDefinitionId SourceDefinitionId =
            new GameplayTriggerSourceDefinitionId(1);
        private static readonly GameplayTriggerDefinitionId DefinitionId =
            new GameplayTriggerDefinitionId(1);
        private static readonly GameplayTriggerBindingId BindingId =
            new GameplayTriggerBindingId(1);
        private static readonly GameplayTriggerSourceBindingId SourceBindingId =
            new GameplayTriggerSourceBindingId(1);
        private static readonly GameplayTriggerRouteId RouteId =
            new GameplayTriggerRouteId(1);
        private static readonly GameplayTriggerActionKindId ActionKindId =
            new GameplayTriggerActionKindId(1);
        private static readonly GameplayEntityId Owner = new GameplayEntityId(100, 1);

        [Test]
        public void DuplicateContacts_AreCanonicalAndIdempotentAcrossEnterStayExit()
        {
            GameplayTriggerRuntime runtime = Runtime(PerSubjectPolicy());
            var reference = new GameplayTriggerLifecycleReferenceModel();
            var a = new GameplayEntityId(1, 1);
            var b = new GameplayEntityId(2, 1);

            AssertFrame(runtime, reference, 0, b, a, b, a);
            AssertFrame(runtime, reference, 1, b, b, a);
            AssertFrame(runtime, reference, 2, b, b);

            Assert.AreEqual(2, reference.Relations.Count);
            Assert.AreEqual(a, reference.Relations[0].Subject);
            Assert.IsFalse(reference.Relations[0].Active);
            Assert.AreEqual(b, reference.Relations[1].Subject);
            Assert.IsTrue(reference.Relations[1].Active);
        }

        [Test]
        public void DisableClearSilently_PreservesPolicyAndReenableCreatesNewPresence()
        {
            var policy = new GameplayTriggerActivationPolicy(
                GameplayTriggerStateScope.PerSubject,
                maxActivations: 2,
                presenceLimit: GameplayTriggerPresenceLimit.OncePerPresence);
            GameplayTriggerRuntime runtime = Runtime(policy);
            var reference = new GameplayTriggerLifecycleReferenceModel(
                maxActivations: 2,
                oncePerPresence: true);
            GameplayTriggerInstanceId instanceId = runtime.GetInstanceId(0);
            var subject = new GameplayEntityId(1, 1);

            AssertFrame(runtime, reference, 0, subject);
            AssertSetEnabled(runtime, reference, instanceId, false);
            AssertSetEnabled(runtime, reference, instanceId, false);
            Assert.IsFalse(runtime.TrySubmitOccupancy(
                new RuntimeFrame(1),
                SourceBindingId,
                new[] { subject },
                out GameplayTriggerResult disabled));
            Assert.AreEqual(GameplayTriggerErrorCode.Disabled, disabled.ErrorCode);
            AssertSetEnabled(runtime, reference, instanceId, true);
            AssertSetEnabled(runtime, reference, instanceId, true);

            AssertFrame(runtime, reference, 1, subject);
            AssertSetEnabled(runtime, reference, instanceId, false);
            AssertSetEnabled(runtime, reference, instanceId, true);
            AssertFrame(runtime, reference, 2, subject);

            Assert.AreEqual(0, runtime.ActivationCount, "lifetime count must survive disable");
            Assert.AreEqual(3L, reference.Relations[0].PresenceEpoch);
            Assert.AreEqual(3L, reference.AggregatePresenceEpoch);
            Assert.AreEqual(3L, reference.NextActivationId);
        }

        [Test]
        public void Reset_ClearsSourceAndPolicyButPreservesEnabledAndActivationSequence()
        {
            var policy = new GameplayTriggerActivationPolicy(
                GameplayTriggerStateScope.PerSubject,
                maxActivations: 1,
                presenceLimit: GameplayTriggerPresenceLimit.OncePerPresence);
            GameplayTriggerRuntime runtime = Runtime(policy);
            var reference = new GameplayTriggerLifecycleReferenceModel(
                maxActivations: 1,
                oncePerPresence: true);
            GameplayTriggerInstanceId instanceId = runtime.GetInstanceId(0);
            var subject = new GameplayEntityId(1, 1);

            AssertFrame(runtime, reference, 0, subject);
            Assert.IsTrue(runtime.TryReset(instanceId, out GameplayTriggerResult reset));
            Assert.IsTrue(reset.Success);
            reference.Reset();
            AssertAuthority(runtime, reference);
            Assert.AreEqual(0, runtime.RouteStateCount);

            Assert.IsTrue(runtime.TryReset(instanceId, out reset));
            Assert.IsTrue(reset.Success);
            reference.Reset();
            AssertAuthority(runtime, reference);

            AssertFrame(runtime, reference, 1, subject);
            Assert.AreEqual(1, runtime.ActivationCount);
            Assert.AreEqual(2L, runtime.GetActivation(0).ActivationId);
            Assert.AreEqual(3L, runtime.NextActivationId);
            Assert.IsTrue(runtime.GetInstanceAuthorityState(0).Enabled);
        }

        [Test]
        public void InvalidOrStaleInstanceHandles_RejectWithoutAuthorityMutation()
        {
            GameplayTriggerRuntime runtime = Runtime(PerSubjectPolicy());
            var reference = new GameplayTriggerLifecycleReferenceModel();
            var subject = new GameplayEntityId(1, 1);
            AssertFrame(runtime, reference, 0, subject);

            GameplayTriggerInstanceId live = runtime.GetInstanceId(0);
            GameplayTriggerInstanceId[] invalidHandles =
            {
                default,
                new GameplayTriggerInstanceId(live.Index, live.Generation + 1),
                new GameplayTriggerInstanceId(live.Index + 1, live.Generation)
            };
            for (int i = 0; i < invalidHandles.Length; i++)
            {
                Assert.IsFalse(runtime.TrySetEnabled(
                    invalidHandles[i],
                    false,
                    out GameplayTriggerResult setEnabled));
                Assert.AreEqual(GameplayTriggerErrorCode.InvalidArgument, setEnabled.ErrorCode);
                AssertAuthority(runtime, reference);

                Assert.IsFalse(runtime.TryReset(
                    invalidHandles[i],
                    out GameplayTriggerResult reset));
                Assert.AreEqual(GameplayTriggerErrorCode.InvalidArgument, reset.ErrorCode);
                AssertAuthority(runtime, reference);
            }
        }

        [Test]
        public void InitiallyDisabled_RejectsInputUntilExplicitlyEnabled()
        {
            GameplayTriggerRuntime runtime = Runtime(
                PerSubjectPolicy(),
                initiallyEnabled: false);
            var reference = new GameplayTriggerLifecycleReferenceModel(initiallyEnabled: false);
            GameplayTriggerInstanceId instanceId = runtime.GetInstanceId(0);
            var subject = new GameplayEntityId(1, 1);

            Assert.IsFalse(runtime.TrySubmitOccupancy(
                RuntimeFrame.Zero,
                SourceBindingId,
                new[] { subject },
                out GameplayTriggerResult disabled));
            Assert.AreEqual(GameplayTriggerErrorCode.Disabled, disabled.ErrorCode);
            AssertAuthority(runtime, reference);

            AssertSetEnabled(runtime, reference, instanceId, true);
            AssertFrame(runtime, reference, 0, subject);
            Assert.AreEqual(GameplayTriggerOccurrenceKind.Entered, runtime.GetOccurrence(0).Kind);
        }

        [Test]
        public void Disable_DiscardsOnlyTargetInstancePendingInput()
        {
            GameplayTriggerRuntime runtime = TwoInstanceRuntime();
            var first = new GameplayTriggerLifecycleReferenceModel();
            var second = new GameplayTriggerLifecycleReferenceModel();
            GameplayTriggerInstanceId firstInstance = runtime.GetInstanceId(0);
            GameplayTriggerInstanceId secondInstance = runtime.GetInstanceId(1);
            var a = new GameplayEntityId(1, 1);
            var b = new GameplayEntityId(2, 1);

            AssertPendingSubmit(runtime, first, RuntimeFrame.Zero, SourceBindingId, a);
            AssertPendingSubmit(
                runtime,
                second,
                RuntimeFrame.Zero,
                new GameplayTriggerSourceBindingId(2),
                b);
            Assert.IsTrue(runtime.TrySetEnabled(firstInstance, false, out _));
            first.SetEnabled(false);

            Assert.IsTrue(runtime.Evaluate(RuntimeFrame.Zero).Success);
            Assert.IsFalse(first.EvaluatePending(0));
            Assert.IsTrue(second.EvaluatePending(0));
            AssertSingleSurvivingInstance(runtime, second, secondInstance);
        }

        [Test]
        public void Reset_DiscardsOnlyTargetInstancePendingInput()
        {
            GameplayTriggerRuntime runtime = TwoInstanceRuntime();
            var first = new GameplayTriggerLifecycleReferenceModel();
            var second = new GameplayTriggerLifecycleReferenceModel();
            GameplayTriggerInstanceId firstInstance = runtime.GetInstanceId(0);
            GameplayTriggerInstanceId secondInstance = runtime.GetInstanceId(1);
            var a = new GameplayEntityId(1, 1);
            var b = new GameplayEntityId(2, 1);

            AssertPendingSubmit(runtime, first, RuntimeFrame.Zero, SourceBindingId, a);
            AssertPendingSubmit(
                runtime,
                second,
                RuntimeFrame.Zero,
                new GameplayTriggerSourceBindingId(2),
                b);
            Assert.IsTrue(runtime.TryReset(firstInstance, out _));
            first.Reset();

            Assert.IsTrue(runtime.Evaluate(RuntimeFrame.Zero).Success);
            Assert.IsFalse(first.EvaluatePending(0));
            Assert.IsTrue(second.EvaluatePending(0));
            AssertSingleSurvivingInstance(runtime, second, secondInstance);

            Assert.IsTrue(first.EvaluateOccupancy(1, new[] { a }));
            Assert.IsTrue(runtime.TrySubmitOccupancy(
                new RuntimeFrame(1),
                SourceBindingId,
                new[] { a },
                out GameplayTriggerResult submit),
                submit.ErrorCode.ToString());
            Assert.IsTrue(runtime.Evaluate(new RuntimeFrame(1)).Success);
            Assert.AreEqual(1, runtime.OccurrenceCount);
            Assert.AreEqual(1, runtime.ActivationCount);
            Assert.AreEqual(firstInstance, runtime.GetOccurrence(0).InstanceId);
        }

        private static void AssertPendingSubmit(
            GameplayTriggerRuntime runtime,
            GameplayTriggerLifecycleReferenceModel reference,
            RuntimeFrame frame,
            GameplayTriggerSourceBindingId sourceBindingId,
            GameplayEntityId subject)
        {
            Assert.IsTrue(reference.SubmitOccupancy(new[] { subject }));
            Assert.IsTrue(runtime.TrySubmitOccupancy(
                frame,
                sourceBindingId,
                new[] { subject },
                out GameplayTriggerResult submit),
                submit.ErrorCode.ToString());
        }

        private static void AssertSingleSurvivingInstance(
            GameplayTriggerRuntime runtime,
            GameplayTriggerLifecycleReferenceModel reference,
            GameplayTriggerInstanceId instanceId)
        {
            Assert.AreEqual(1, runtime.OccurrenceCount);
            Assert.AreEqual(instanceId, runtime.GetOccurrence(0).InstanceId);
            Assert.AreEqual(reference.Occurrences[0].Subject, runtime.GetOccurrence(0).Subject);
            Assert.AreEqual(1, runtime.ActivationCount);
            Assert.AreEqual(instanceId, runtime.GetActivation(0).Occurrence.InstanceId);
        }

        private static void AssertSetEnabled(
            GameplayTriggerRuntime runtime,
            GameplayTriggerLifecycleReferenceModel reference,
            GameplayTriggerInstanceId instanceId,
            bool enabled)
        {
            Assert.IsTrue(runtime.TrySetEnabled(
                instanceId,
                enabled,
                out GameplayTriggerResult result));
            Assert.IsTrue(result.Success);
            reference.SetEnabled(enabled);
            AssertAuthority(runtime, reference);
            Assert.AreEqual(0, reference.Occurrences.Count);
            Assert.AreEqual(0, reference.Activations.Count);
        }

        private static void AssertFrame(
            GameplayTriggerRuntime runtime,
            GameplayTriggerLifecycleReferenceModel reference,
            long frame,
            params GameplayEntityId[] subjects)
        {
            Assert.IsTrue(reference.EvaluateOccupancy(frame, subjects));
            var runtimeFrame = new RuntimeFrame(frame);
            Assert.IsTrue(runtime.TrySubmitOccupancy(
                runtimeFrame,
                SourceBindingId,
                subjects,
                out GameplayTriggerResult submit),
                submit.ErrorCode.ToString());
            Assert.IsTrue(runtime.Evaluate(runtimeFrame).Success);

            Assert.AreEqual(reference.Occurrences.Count, runtime.OccurrenceCount);
            for (int i = 0; i < reference.Occurrences.Count; i++)
            {
                GameplayTriggerLifecycleReferenceOccurrence expected = reference.Occurrences[i];
                GameplayTriggerOccurrence actual = runtime.GetOccurrence(i);
                Assert.AreEqual(expected.Frame, actual.Frame.Value, "occurrence frame " + i);
                Assert.AreEqual(expected.Kind, actual.Kind, "occurrence kind " + i);
                Assert.AreEqual(expected.Subject, actual.Subject, "occurrence subject " + i);
                Assert.AreEqual(expected.PresenceEpoch, actual.PresenceEpoch, "occurrence epoch " + i);
                Assert.AreEqual(expected.PresenceStartFrame, actual.PresenceStartFrame, "occurrence start " + i);
            }

            Assert.AreEqual(reference.Activations.Count, runtime.ActivationCount);
            for (int i = 0; i < reference.Activations.Count; i++)
            {
                GameplayTriggerLifecycleReferenceActivation expected = reference.Activations[i];
                GameplayTriggerActivation actual = runtime.GetActivation(i);
                Assert.AreEqual(expected.ActivationId, actual.ActivationId, "activation id " + i);
                Assert.AreEqual(expected.Subject, actual.Occurrence.Subject, "activation subject " + i);
            }
            AssertAuthority(runtime, reference);
        }

        private static void AssertAuthority(
            GameplayTriggerRuntime runtime,
            GameplayTriggerLifecycleReferenceModel reference)
        {
            GameplayTriggerInstanceAuthorityState instance = runtime.GetInstanceAuthorityState(0);
            Assert.AreEqual(reference.Enabled, instance.Enabled, "enabled");
            Assert.AreEqual(reference.ActiveCount, instance.ActiveCount, "active count");
            Assert.AreEqual(
                reference.AggregatePresenceEpoch,
                instance.AggregatePresenceEpoch,
                "aggregate presence epoch");
            Assert.AreEqual(reference.NextActivationId, runtime.NextActivationId, "next activation id");

            Assert.AreEqual(reference.Relations.Count, runtime.RelationCount, "relation count");
            for (int i = 0; i < reference.Relations.Count; i++)
            {
                GameplayTriggerLifecycleReferenceRelation expected = reference.Relations[i];
                GameplayTriggerRelationAuthorityState actual = runtime.GetRelationAuthorityState(i);
                Assert.AreEqual(expected.Subject, actual.Subject, "relation subject " + i);
                Assert.AreEqual(expected.Active, actual.Active, "relation active " + i);
                Assert.AreEqual(expected.PresenceEpoch, actual.PresenceEpoch, "relation epoch " + i);
                Assert.AreEqual(
                    expected.PresenceStartFrame,
                    actual.PresenceStartFrame,
                    "relation start " + i);
            }

            Assert.AreEqual(reference.RouteStates.Count, runtime.RouteStateCount, "route state count");
            for (int i = 0; i < reference.RouteStates.Count; i++)
            {
                GameplayTriggerLifecycleReferenceRouteState expected = reference.RouteStates[i];
                GameplayTriggerRouteAuthorityState actual = runtime.GetRouteAuthorityState(i);
                Assert.AreEqual(expected.Subject, actual.Subject, "route subject " + i);
                Assert.AreEqual(expected.ActivationCount, actual.ActivationCount, "route count " + i);
                Assert.AreEqual(
                    expected.LastActivationFrame,
                    actual.LastActivationFrame,
                    "route frame " + i);
                Assert.AreEqual(
                    expected.LastActivatedPresenceEpoch,
                    actual.LastActivatedPresenceEpoch,
                    "route epoch " + i);
            }
        }

        private static GameplayTriggerRuntime Runtime(
            GameplayTriggerActivationPolicy policy,
            bool initiallyEnabled = true)
        {
            var source = new GameplayTriggerSourceDefinition(
                SourceDefinitionId,
                GameplayTriggerSourceKind.Occupancy);
            var route = new GameplayTriggerRouteDefinition(
                RouteId,
                GameplayTriggerOccurrenceKind.Entered,
                GameplayTriggerConditionProgram.Always,
                new[]
                {
                    new GameplayTriggerActionInstruction(
                        ActionKindId,
                        GameplayTriggerTargetSelector.Owner)
                },
                policy);
            var definition = new GameplayTriggerDefinition(
                DefinitionId,
                definitionVersion: 1,
                SourceDefinitionId,
                new[] { route });
            var binding = new GameplayTriggerBinding(
                BindingId,
                DefinitionId,
                SourceBindingId,
                Owner,
                initiallyEnabled);
            Assert.IsTrue(GameplayTriggerCatalog.TryCreate(
                new[] { source },
                new[] { definition },
                new[] { binding },
                new IGameplayTriggerActionSink[] { new LifecycleSink() },
                out GameplayTriggerCatalog catalog,
                out GameplayTriggerCatalogValidationResult validation),
                validation.ErrorCode.ToString());
            return new GameplayTriggerRuntime(
                catalog,
                new GameplayTriggerRuntimeOptions(
                    4, 16, 16, 8, 16, 16, 16, 16, 4, 8));
        }

        private static GameplayTriggerRuntime TwoInstanceRuntime()
        {
            var source = new GameplayTriggerSourceDefinition(
                SourceDefinitionId,
                GameplayTriggerSourceKind.Occupancy);
            var route = new GameplayTriggerRouteDefinition(
                RouteId,
                GameplayTriggerOccurrenceKind.Entered,
                GameplayTriggerConditionProgram.Always,
                new[]
                {
                    new GameplayTriggerActionInstruction(
                        ActionKindId,
                        GameplayTriggerTargetSelector.Owner)
                },
                PerSubjectPolicy());
            var definition = new GameplayTriggerDefinition(
                DefinitionId,
                definitionVersion: 1,
                SourceDefinitionId,
                new[] { route });
            GameplayTriggerBinding[] bindings =
            {
                new GameplayTriggerBinding(
                    BindingId,
                    DefinitionId,
                    SourceBindingId,
                    Owner),
                new GameplayTriggerBinding(
                    new GameplayTriggerBindingId(2),
                    DefinitionId,
                    new GameplayTriggerSourceBindingId(2),
                    new GameplayEntityId(200, 1))
            };
            Assert.IsTrue(GameplayTriggerCatalog.TryCreate(
                new[] { source },
                new[] { definition },
                bindings,
                new IGameplayTriggerActionSink[] { new LifecycleSink() },
                out GameplayTriggerCatalog catalog,
                out GameplayTriggerCatalogValidationResult validation),
                validation.ErrorCode.ToString());
            return new GameplayTriggerRuntime(
                catalog,
                new GameplayTriggerRuntimeOptions(
                    4, 16, 16, 8, 16, 16, 16, 16, 4, 8));
        }

        private static GameplayTriggerActivationPolicy PerSubjectPolicy() =>
            new GameplayTriggerActivationPolicy(GameplayTriggerStateScope.PerSubject);

        private sealed class LifecycleSink : IGameplayTriggerActionSink
        {
            public GameplayTriggerActionKindId ActionKindId =>
                GameplayTriggerLifecycleReferenceModelTests.ActionKindId;

            public GameplayTriggerDeliveryContract DeliveryContract =>
                GameplayTriggerDeliveryContract.SameFrameReserved;

            public bool TryPrepare(in GameplayTriggerActionPreparation preparation, out int token)
            {
                token = 1;
                return true;
            }

            public void Commit(int token, in GameplayTriggerActionIntent intent)
            {
            }

            public void Abort(int token)
            {
            }
        }
    }
}
