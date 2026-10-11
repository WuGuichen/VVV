using System;
using System.Collections.Generic;
using MxFramework.Core.Math;
using MxFramework.Gameplay;
using MxFramework.Gameplay.Triggers;
using MxFramework.Runtime;
using NUnit.Framework;

namespace MxFramework.Tests.Gameplay.Triggers
{
    public sealed class GameplayTriggerHashTests
    {
        [Test]
        public void CatalogHashV2_GoldenValuesFreezeFieldSchema()
        {
            GameplayTriggerCatalog catalog = new CatalogVariant().Build();

            Assert.AreEqual(-5259361105955098294L, catalog.DefinitionCatalogHash);
            Assert.AreEqual(1094315143827898144L, catalog.BindingCatalogHash);
            Assert.AreEqual(-3362775511247608857L, catalog.GameplayTriggerCatalogHash);
            Assert.IsTrue(catalog.HasLegacyV1CatalogIdentity);
            Assert.AreEqual(-4705690926368793337L, catalog.LegacyV1DefinitionCatalogHash);
            Assert.AreEqual(6166229283828020143L, catalog.LegacyV1BindingCatalogHash);
            Assert.AreEqual(2417659894648601961L, catalog.LegacyV1GameplayTriggerCatalogHash);
        }

        [Test]
        public void CatalogCanonicalSetRegistrationOrder_DoesNotChangeHashes()
        {
            GameplayTriggerCatalog first = BuildPermutationCatalog(reverse: false);
            GameplayTriggerCatalog second = BuildPermutationCatalog(reverse: true);

            Assert.AreEqual(first.DefinitionCatalogHash, second.DefinitionCatalogHash);
            Assert.AreEqual(first.BindingCatalogHash, second.BindingCatalogHash);
            Assert.AreEqual(first.GameplayTriggerCatalogHash, second.GameplayTriggerCatalogHash);
        }

        [Test]
        public void ConditionAndActionDeclarationOrder_RemainSemantic()
        {
            var baseVariant = new CatalogVariant();
            GameplayTriggerCatalog baseline = baseVariant.Build();

            CatalogVariant conditionSwapped = baseVariant.Copy();
            conditionSwapped.ReverseConditionOperands = true;
            GameplayTriggerCatalog conditionCatalog = conditionSwapped.Build();
            CatalogVariant actionSwapped = baseVariant.Copy();
            actionSwapped.ReverseActions = true;
            GameplayTriggerCatalog actionCatalog = actionSwapped.Build();

            AssertDefinitionOnlyChanged(baseline, conditionCatalog, "condition order");
            AssertDefinitionOnlyChanged(baseline, actionCatalog, "action order");
        }

        [Test]
        public void DefinitionHashV2_CoversEverySemanticFieldGroup()
        {
            var baselineVariant = new CatalogVariant();
            GameplayTriggerCatalog baseline = baselineVariant.Build();
            Action<CatalogVariant>[] changes =
            {
                value => value.UnusedSourceDefinitionId = 151,
                value => value.SourceDefinitionId = 101,
                value => value.SourceKind = GameplayTriggerSourceKind.Occupancy,
                value => value.SignalOrdering = GameplayTriggerSignalOrdering.UnorderedMultiset,
                value => value.ThresholdDirection = GameplayTriggerThresholdDirection.Low,
                value => value.EnterThresholdRaw = 6L,
                value => value.ExitThresholdRaw = 4L,
                value => value.InitialSamplePolicy = GameplayTriggerInitialSamplePolicy.PrimeSilently,
                value => value.DeparturePolicy = GameplayTriggerSubjectDeparturePolicy.RemoveSilently,
                value => value.UnusedDefinitionId = 251,
                value => value.DefinitionVersion = 3,
                value => value.RouteId = 301,
                value => value.OccurrenceMask = GameplayTriggerOccurrenceKind.Exited,
                value => value.ConditionOperation = GameplayTriggerConditionOperation.Payload1,
                value => value.ConditionOperand = 6L,
                value => value.ConditionComparison = GameplayTriggerComparison.LessOrEqual,
                value => value.StateScope = GameplayTriggerStateScope.PerPair,
                value => value.MaxActivations = 4,
                value => value.CooldownFrames = 8L,
                value => value.PresenceLimit = GameplayTriggerPresenceLimit.OncePerPresence,
                value => value.StayIntervalFrames = 9L,
                value => value.ExecutionOrder = 11,
                value => value.ExclusiveGroupId = 12,
                value => value.WinnerPriority = 13,
                value => value.ArbitrationScope = GameplayTriggerArbitrationScope.PerSubject,
                value => value.Action1KindId = 403,
                value => value.Action1TargetSelector = GameplayTriggerTargetSelector.Subject,
                value => value.Action1TargetSlotId = 7,
                value => value.Action1Payload0 = 41L,
                value => value.Action1Payload1 = 42L,
                value => value.Action1Payload2 = 43L,
                value => value.Sink1Delivery = GameplayTriggerDeliveryContract.ReliableDeferred,
                value => value.UnusedSinkKindId = 404
            };

            for (int i = 0; i < changes.Length; i++)
            {
                CatalogVariant changed = baselineVariant.Copy();
                changes[i](changed);
                AssertDefinitionOnlyChanged(baseline, changed.Build(), "definition variant " + i);
            }
        }

        [Test]
        public void BindingHashV2_CoversEverySemanticFieldGroup()
        {
            var baselineVariant = new CatalogVariant();
            GameplayTriggerCatalog baseline = baselineVariant.Build();
            Action<CatalogVariant>[] changes =
            {
                value => value.BindingId = 501,
                value => value.SourceBindingId = 601,
                value => value.OwnerIndex = 701,
                value => value.OwnerGeneration = 4,
                value => value.InitiallyEnabled = false,
                value => value.Target1DomainId = 5,
                value => value.Target1Value = 801L,
                value => value.Target1Generation = 5,
                value => value.Target2SlotId = 11,
                value => value.Target2DomainId = 6,
                value => value.Target2Value = 802L,
                value => value.Target2Generation = 6,
                value => value.IncludeSecondTarget = false
            };

            for (int i = 0; i < changes.Length; i++)
            {
                CatalogVariant changed = baselineVariant.Copy();
                changes[i](changed);
                AssertBindingOnlyChanged(baseline, changed.Build(), "binding variant " + i);
            }
        }

        [Test]
        public void BindingDefinitionReference_ChangesOnlyBindingAndCombinedHashes()
        {
            GameplayTriggerCatalog first = BuildAlternateDefinitionCatalog(useAlternate: false);
            GameplayTriggerCatalog second = BuildAlternateDefinitionCatalog(useAlternate: true);

            AssertBindingOnlyChanged(first, second, "binding definition reference");
        }

        [Test]
        public void SinkObjectIdentity_IsExcludedButDeliveryContractIsIncluded()
        {
            var firstVariant = new CatalogVariant();
            var secondVariant = firstVariant.Copy();
            secondVariant.UseAlternateSinkImplementation = true;
            GameplayTriggerCatalog first = firstVariant.Build();
            GameplayTriggerCatalog second = secondVariant.Build();
            var deliveryChangedVariant = firstVariant.Copy();
            deliveryChangedVariant.Sink1Delivery = GameplayTriggerDeliveryContract.ReliableDeferred;
            GameplayTriggerCatalog deliveryChanged = deliveryChangedVariant.Build();

            Assert.AreEqual(first.DefinitionCatalogHash, second.DefinitionCatalogHash);
            Assert.AreEqual(first.BindingCatalogHash, second.BindingCatalogHash);
            Assert.AreEqual(first.GameplayTriggerCatalogHash, second.GameplayTriggerCatalogHash);
            AssertDefinitionOnlyChanged(first, deliveryChanged, "delivery contract");
        }

        [Test]
        public void MutableSinkMetadata_IsFrozenAtCatalogCreation()
        {
            var originalKind = new GameplayTriggerActionKindId(60);
            var changedKind = new GameplayTriggerActionKindId(61);
            var sink = new MutableTestSink(
                originalKind,
                GameplayTriggerDeliveryContract.SameFrameReserved);
            GameplayTriggerRuntime runtime = CreateRuntime(
                GameplayTriggerSourceKind.Signal,
                sink: sink);
            GameplayTriggerCatalog catalog = runtime.AuthorityCatalog;
            long definitionHash = catalog.DefinitionCatalogHash;
            long bindingHash = catalog.BindingCatalogHash;
            long combinedHash = catalog.GameplayTriggerCatalogHash;
            long authorityHash = ComputeModuleHash(runtime);

            sink.ActionKindId = changedKind;
            sink.DeliveryContract = GameplayTriggerDeliveryContract.ReliableDeferred;

            GameplayTriggerActionSinkEntry frozen = catalog.GetSinkEntry(0);
            Assert.AreEqual(originalKind, frozen.ActionKindId);
            Assert.AreEqual(
                GameplayTriggerDeliveryContract.SameFrameReserved,
                frozen.DeliveryContract);
            Assert.AreSame(sink, catalog.GetSink(0));
            Assert.AreEqual(0, catalog.FindSink(originalKind));
            Assert.AreEqual(-1, catalog.FindSink(changedKind));
            Assert.AreEqual(definitionHash, catalog.DefinitionCatalogHash);
            Assert.AreEqual(bindingHash, catalog.BindingCatalogHash);
            Assert.AreEqual(combinedHash, catalog.GameplayTriggerCatalogHash);
            Assert.AreEqual(authorityHash, ComputeModuleHash(runtime));

            SubmitSignals(
                runtime,
                0L,
                new[] { new GameplayTriggerSignal(1, new GameplayEntityId(1, 1)) });
            Assert.IsTrue(runtime.Evaluate(RuntimeFrame.Zero).Success);
            Assert.AreEqual(1, runtime.ActivationCount);
            Assert.AreEqual(1, runtime.IntentCount);
            Assert.AreEqual(1, sink.PrepareCount);
            Assert.AreEqual(1, sink.CommitCount);
        }

        [Test]
        public void AuthorityHashV3_InitialGoldenAndResolvedInstanceIdentity()
        {
            GameplayTriggerRuntime runtime = new GameplayTriggerRuntime(
                new CatalogVariant().Build(),
                Options());
            GameplayTriggerInstanceAuthorityState instance = runtime.GetInstanceAuthorityState(0);

            Assert.AreEqual(new GameplayTriggerSourceDefinitionId(100), instance.SourceDefinitionId);
            Assert.AreEqual(new GameplayTriggerDefinitionId(200), instance.DefinitionId);
            Assert.AreEqual(2, instance.DefinitionVersion);
            Assert.AreEqual(-44882057537873980L, ComputeModuleHash(runtime));
        }

        [Test]
        public void AuthorityHashV3_CoversAllRuntimeOptions()
        {
            GameplayTriggerCatalog catalog = new CatalogVariant().Build();
            long baseline = ComputeModuleHash(new GameplayTriggerRuntime(catalog, Options()));

            for (int optionIndex = 0; optionIndex < 12; optionIndex++)
            {
                GameplayTriggerRuntimeOptions changed = Options(optionIndex);
                long actual = ComputeModuleHash(new GameplayTriggerRuntime(catalog, changed));
                Assert.AreNotEqual(baseline, actual, "option index " + optionIndex);
            }
        }

        [Test]
        public void AuthorityHashV3_ChangesForDefinitionOrBindingCatalogMismatch()
        {
            var baselineVariant = new CatalogVariant();
            CatalogVariant definitionChanged = baselineVariant.Copy();
            definitionChanged.Sink1Delivery = GameplayTriggerDeliveryContract.ReliableDeferred;
            CatalogVariant bindingChanged = baselineVariant.Copy();
            bindingChanged.Target1DomainId++;

            long baseline = ComputeModuleHash(
                new GameplayTriggerRuntime(baselineVariant.Build(), Options(maxReliable: 16)));
            long definitionMismatch = ComputeModuleHash(
                new GameplayTriggerRuntime(definitionChanged.Build(), Options(maxReliable: 16)));
            long bindingMismatch = ComputeModuleHash(
                new GameplayTriggerRuntime(bindingChanged.Build(), Options(maxReliable: 16)));

            Assert.AreNotEqual(baseline, definitionMismatch);
            Assert.AreNotEqual(baseline, bindingMismatch);
        }

        [Test]
        public void AuthorityHashV3_CoversEveryInstanceField()
        {
            long baseline = ComputeInstanceStateHash(CreateInstanceAuthorityState());

            for (int fieldIndex = 0; fieldIndex < 12; fieldIndex++)
            {
                long changed = ComputeInstanceStateHash(CreateInstanceAuthorityState(fieldIndex));
                Assert.AreNotEqual(baseline, changed, "instance field index " + fieldIndex);
            }
        }

        [Test]
        public void AuthorityHashV3_CoversEveryRelationField()
        {
            long baseline = ComputeRelationStateHash(CreateRelationAuthorityState());

            for (int fieldIndex = 0; fieldIndex < 9; fieldIndex++)
            {
                long changed = ComputeRelationStateHash(CreateRelationAuthorityState(fieldIndex));
                Assert.AreNotEqual(baseline, changed, "relation field index " + fieldIndex);
            }
        }

        [Test]
        public void AuthorityHashV3_CoversEveryRouteStateField()
        {
            long baseline = ComputeRouteStateHash(CreateRouteAuthorityState());

            for (int fieldIndex = 0; fieldIndex < 13; fieldIndex++)
            {
                long changed = ComputeRouteStateHash(CreateRouteAuthorityState(fieldIndex));
                Assert.AreNotEqual(baseline, changed, "route field index " + fieldIndex);
            }
        }

        [Test]
        public void AuthorityHashV3_CoversEvaluatedMarkersEnabledAndNextActivationId()
        {
            GameplayTriggerRuntime initial = CreateRuntime(GameplayTriggerSourceKind.Signal);
            GameplayTriggerRuntime evaluated = CreateRuntime(GameplayTriggerSourceKind.Signal);
            SubmitSignals(evaluated, 5L, Array.Empty<GameplayTriggerSignal>());
            evaluated.Evaluate(new RuntimeFrame(5));
            Assert.AreNotEqual(ComputeModuleHash(initial), ComputeModuleHash(evaluated));

            GameplayTriggerRuntime otherFrame = CreateRuntime(GameplayTriggerSourceKind.Signal);
            SubmitSignals(otherFrame, 6L, Array.Empty<GameplayTriggerSignal>());
            otherFrame.Evaluate(new RuntimeFrame(6));
            Assert.AreNotEqual(ComputeModuleHash(evaluated), ComputeModuleHash(otherFrame));

            GameplayTriggerRuntime activatedThenReset = CreateRuntime(GameplayTriggerSourceKind.Signal);
            SubmitSignals(
                activatedThenReset,
                0L,
                new[] { new GameplayTriggerSignal(1, new GameplayEntityId(1, 1)) });
            activatedThenReset.Evaluate(RuntimeFrame.Zero);
            activatedThenReset.TryReset(activatedThenReset.GetInstanceId(0), out _);
            GameplayTriggerRuntime empty = CreateRuntime(GameplayTriggerSourceKind.Signal);
            SubmitSignals(empty, 0L, Array.Empty<GameplayTriggerSignal>());
            empty.Evaluate(RuntimeFrame.Zero);
            Assert.AreEqual(2L, activatedThenReset.NextActivationId);
            Assert.AreEqual(1L, empty.NextActivationId);
            Assert.AreNotEqual(ComputeModuleHash(activatedThenReset), ComputeModuleHash(empty));

            GameplayTriggerRuntime disabled = CreateRuntime(GameplayTriggerSourceKind.Signal);
            disabled.TrySetEnabled(disabled.GetInstanceId(0), false, out _);
            Assert.AreNotEqual(ComputeModuleHash(initial), ComputeModuleHash(disabled));
        }

        [Test]
        public void AuthorityHashV3_CoversRelationAndRoutePolicyFields()
        {
            GameplayTriggerRuntime first = CreateRuntime(
                GameplayTriggerSourceKind.Threshold,
                new GameplayTriggerActivationPolicy(
                    GameplayTriggerStateScope.PerSubject,
                    maxActivations: 4,
                    cooldownFrames: 1,
                    presenceLimit: GameplayTriggerPresenceLimit.OncePerPresence,
                    stayIntervalFrames: 2));
            GameplayTriggerRuntime second = CreateRuntime(
                GameplayTriggerSourceKind.Threshold,
                new GameplayTriggerActivationPolicy(
                    GameplayTriggerStateScope.PerSubject,
                    maxActivations: 4,
                    cooldownFrames: 1,
                    presenceLimit: GameplayTriggerPresenceLimit.OncePerPresence,
                    stayIntervalFrames: 2));
            var subject = new GameplayEntityId(7, 3);

            SubmitScalar(first, 0L, subject, Fix64.FromRaw(12L));
            first.Evaluate(RuntimeFrame.Zero);
            SubmitScalar(first, 1L, subject, Fix64.FromRaw(12L));
            first.Evaluate(new RuntimeFrame(1));

            SubmitScalar(second, 0L, subject, Fix64.FromRaw(8L));
            second.Evaluate(RuntimeFrame.Zero);
            SubmitScalar(second, 1L, subject, Fix64.FromRaw(12L));
            second.Evaluate(new RuntimeFrame(1));

            GameplayTriggerRelationAuthorityState firstRelation = first.GetRelationAuthorityState(0);
            GameplayTriggerRelationAuthorityState secondRelation = second.GetRelationAuthorityState(0);
            Assert.AreNotEqual(firstRelation.PresenceStartFrame, secondRelation.PresenceStartFrame);
            Assert.AreNotEqual(ComputeModuleHash(first), ComputeModuleHash(second));

            SubmitScalar(first, 2L, subject, Fix64.FromRaw(8L));
            first.Evaluate(new RuntimeFrame(2));
            SubmitScalar(first, 3L, subject, Fix64.FromRaw(12L));
            first.Evaluate(new RuntimeFrame(3));
            GameplayTriggerRelationAuthorityState reentered = first.GetRelationAuthorityState(0);
            GameplayTriggerRouteAuthorityState route = first.GetRouteAuthorityState(0);
            Assert.IsTrue(reentered.Active);
            Assert.IsTrue(reentered.Primed);
            Assert.AreEqual(2L, reentered.PresenceEpoch);
            Assert.Greater(route.ActivationCount, 0);
            Assert.IsTrue(route.HasActivated);
            Assert.GreaterOrEqual(route.LastActivationFrame, 0L);
            Assert.Greater(route.LastActivatedPresenceEpoch, 0L);
        }

        [Test]
        public void AuthorityHashV3_CanonicalizesNonEmptyRelationAndRouteBackingHistory()
        {
            var perSubject = new GameplayTriggerActivationPolicy(GameplayTriggerStateScope.PerSubject);
            GameplayTriggerRuntime first = CreateRuntime(GameplayTriggerSourceKind.Occupancy, perSubject);
            GameplayTriggerRuntime second = CreateRuntime(GameplayTriggerSourceKind.Occupancy, perSubject);
            var a = new GameplayEntityId(1, 1);
            var b = new GameplayEntityId(2, 1);
            EvaluateOccupancy(first, 0L, b);
            EvaluateOccupancy(first, 1L, a);
            EvaluateOccupancy(second, 0L, a);
            EvaluateOccupancy(second, 1L, b);
            EvaluateOccupancy(first, 2L);
            EvaluateOccupancy(second, 2L);
            EvaluateOccupancy(first, 3L, a, b);
            EvaluateOccupancy(second, 3L, b, a);

            Assert.GreaterOrEqual(first.RelationCount, 2);
            Assert.GreaterOrEqual(first.RouteStateCount, 2);
            Assert.AreNotEqual(
                first.GetRelationAuthorityState(0).Subject,
                second.GetRelationAuthorityState(0).Subject,
                "test must exercise different raw relation order");
            Assert.AreEqual(ComputeModuleHash(first), ComputeModuleHash(second));
        }

        [Test]
        public void AuthorityHashV3_ExcludesTransientOutputsDiagnosticsAndSinkIdentity()
        {
            GameplayTriggerConditionProgram never = new GameplayTriggerConditionProgram(
                new[] { new GameplayTriggerConditionInstruction(GameplayTriggerConditionOperation.PushFalse) });
            GameplayTriggerRuntime withOccurrence = CreateRuntime(
                GameplayTriggerSourceKind.Signal,
                condition: never);
            GameplayTriggerRuntime withoutOccurrence = CreateRuntime(
                GameplayTriggerSourceKind.Signal,
                condition: never);
            SubmitSignals(
                withOccurrence,
                0L,
                new[] { new GameplayTriggerSignal(9, payload0: 123L) });
            SubmitSignals(withoutOccurrence, 0L, Array.Empty<GameplayTriggerSignal>());
            withOccurrence.Evaluate(RuntimeFrame.Zero);
            withoutOccurrence.Evaluate(RuntimeFrame.Zero);
            Assert.Greater(withOccurrence.OccurrenceCount, withoutOccurrence.OccurrenceCount);
            Assert.Greater(withOccurrence.DiagnosticCount, withoutOccurrence.DiagnosticCount);
            Assert.AreEqual(ComputeModuleHash(withOccurrence), ComputeModuleHash(withoutOccurrence));

            GameplayTriggerRuntime firstPayload = CreateRuntime(GameplayTriggerSourceKind.Signal);
            GameplayTriggerRuntime secondPayload = CreateRuntime(GameplayTriggerSourceKind.Signal);
            var subject = new GameplayEntityId(1, 1);
            SubmitSignals(firstPayload, 0L, new[] { new GameplayTriggerSignal(9, subject, payload0: 1L) });
            SubmitSignals(secondPayload, 0L, new[] { new GameplayTriggerSignal(9, subject, payload0: 2L) });
            firstPayload.Evaluate(RuntimeFrame.Zero);
            secondPayload.Evaluate(RuntimeFrame.Zero);
            Assert.Greater(firstPayload.ActivationCount, 0);
            Assert.Greater(firstPayload.IntentCount, 0);
            Assert.AreNotEqual(
                firstPayload.GetOccurrence(0).Payload0,
                secondPayload.GetOccurrence(0).Payload0);
            Assert.AreEqual(ComputeModuleHash(firstPayload), ComputeModuleHash(secondPayload));

            var firstVariant = new CatalogVariant();
            var secondVariant = firstVariant.Copy();
            secondVariant.UseAlternateSinkImplementation = true;
            Assert.AreEqual(
                ComputeModuleHash(new GameplayTriggerRuntime(firstVariant.Build(), Options())),
                ComputeModuleHash(new GameplayTriggerRuntime(secondVariant.Build(), Options())));
        }

        [Test]
        public void RuntimeHashCombiner_OrdersTriggerContributorAndRejectsDuplicateId()
        {
            GameplayTriggerRuntime runtime = CreateRuntime(GameplayTriggerSourceKind.Signal);
            var trigger = new GameplayTriggerRuntimeHashContributor(runtime);
            var other = new ConstantContributor("aaa", 7);
            long first = RuntimeHashCombiner.ComputeHash(
                RuntimeFrame.Zero,
                new IRuntimeHashContributor[] { trigger, other });
            long second = RuntimeHashCombiner.ComputeHash(
                RuntimeFrame.Zero,
                new IRuntimeHashContributor[] { other, trigger });

            Assert.AreEqual(first, second);
            Assert.Throws<ArgumentException>(() => new RuntimeHashCombiner(
                new IRuntimeHashContributor[]
                {
                    trigger,
                    new ConstantContributor(GameplayTriggerRuntimeHashContributor.DefaultContributorId, 1)
                }));
        }

        [Test]
        public void Contributor_NonEmptyRelationAndRouteCanonicalSort_IsNoAllocAfterWarmup()
        {
            GameplayTriggerRuntime runtime = CreateRuntime(GameplayTriggerSourceKind.Occupancy);
            EvaluateOccupancy(
                runtime,
                0L,
                new GameplayEntityId(4, 2),
                new GameplayEntityId(2, 1),
                new GameplayEntityId(3, 3));
            Assert.Greater(runtime.RelationCount, 0);
            Assert.Greater(runtime.RouteStateCount, 0);
            var contributor = new GameplayTriggerRuntimeHashContributor(runtime);
            var accumulator = new RuntimeHashAccumulator(RuntimeHashSchemaIds.GameplayTriggers, RuntimeHashSchemaIds.Version1);
            for (int i = 0; i < 8; i++)
                contributor.Contribute(new RuntimeHashContext(RuntimeFrame.Zero), accumulator);

            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 64; i++)
                contributor.Contribute(new RuntimeHashContext(RuntimeFrame.Zero), accumulator);
            long after = GC.GetAllocatedBytesForCurrentThread();

            Assert.AreEqual(0L, after - before);
        }

        private static GameplayTriggerRuntimeOptions Options(
            int changedIndex = -1,
            int maxReliable = 0)
        {
            int[] values = { 8, 16, 16, 16, 16, 16, 16, 16, 16, 8, 0, maxReliable };
            if (changedIndex >= 0) values[changedIndex]++;
            return new GameplayTriggerRuntimeOptions(
                values[0],
                values[1],
                values[2],
                values[3],
                values[4],
                values[5],
                values[6],
                values[7],
                values[8],
                values[9],
                values[10],
                values[11]);
        }

        private static GameplayTriggerRuntime CreateRuntime(
            GameplayTriggerSourceKind kind,
            GameplayTriggerActivationPolicy policy = default,
            GameplayTriggerConditionProgram condition = null,
            IGameplayTriggerActionSink sink = null)
        {
            var sourceId = new GameplayTriggerSourceDefinitionId(10);
            var definitionId = new GameplayTriggerDefinitionId(20);
            var routeId = new GameplayTriggerRouteId(30);
            var bindingId = new GameplayTriggerBindingId(40);
            var sourceBindingId = new GameplayTriggerSourceBindingId(50);
            var actionKindId = new GameplayTriggerActionKindId(60);
            GameplayTriggerOccurrenceKind mask = kind == GameplayTriggerSourceKind.Signal
                ? GameplayTriggerOccurrenceKind.Pulsed
                : GameplayTriggerOccurrenceKind.Entered |
                  GameplayTriggerOccurrenceKind.Stayed |
                  GameplayTriggerOccurrenceKind.Exited;
            var source = kind == GameplayTriggerSourceKind.Threshold
                ? new GameplayTriggerSourceDefinition(
                    sourceId,
                    kind,
                    thresholdDirection: GameplayTriggerThresholdDirection.High,
                    enterThreshold: Fix64.FromRaw(10L),
                    exitThreshold: Fix64.FromRaw(9L),
                    initialSamplePolicy: GameplayTriggerInitialSamplePolicy.EmitEnteredIfActive,
                    departurePolicy: GameplayTriggerSubjectDeparturePolicy.EmitExited)
                : new GameplayTriggerSourceDefinition(sourceId, kind);
            var route = new GameplayTriggerRouteDefinition(
                routeId,
                mask,
                condition ?? GameplayTriggerConditionProgram.Always,
                new[]
                {
                    new GameplayTriggerActionInstruction(
                        actionKindId,
                        GameplayTriggerTargetSelector.Subject)
                },
                policy);
            var definition = new GameplayTriggerDefinition(
                definitionId,
                1,
                sourceId,
                new[] { route });
            var binding = new GameplayTriggerBinding(
                bindingId,
                definitionId,
                sourceBindingId,
                owner: new GameplayEntityId(99, 1));
            Assert.IsTrue(GameplayTriggerCatalog.TryCreate(
                new[] { source },
                new[] { definition },
                new[] { binding },
                new IGameplayTriggerActionSink[] { sink ?? new TestSink(actionKindId) },
                out GameplayTriggerCatalog catalog,
                out GameplayTriggerCatalogValidationResult result),
                result.ErrorCode.ToString());
            return new GameplayTriggerRuntime(catalog, Options());
        }

        private static void SubmitSignals(
            GameplayTriggerRuntime runtime,
            long frame,
            GameplayTriggerSignal[] signals)
        {
            Assert.IsTrue(runtime.TrySubmitSignals(
                new RuntimeFrame(frame),
                new GameplayTriggerSourceBindingId(50),
                GameplayTriggerSignalOrdering.UnorderedMultiset,
                signals,
                out GameplayTriggerResult result),
                result.ErrorCode.ToString());
        }

        private static void SubmitScalar(
            GameplayTriggerRuntime runtime,
            long frame,
            GameplayEntityId subject,
            Fix64 value)
        {
            Assert.IsTrue(runtime.TrySubmitScalarBatch(
                new RuntimeFrame(frame),
                new GameplayTriggerSourceBindingId(50),
                new[] { new GameplayTriggerScalarSample(subject, value) },
                out GameplayTriggerResult result),
                result.ErrorCode.ToString());
        }

        private static void EvaluateOccupancy(
            GameplayTriggerRuntime runtime,
            long frame,
            params GameplayEntityId[] subjects)
        {
            Assert.IsTrue(runtime.TrySubmitOccupancy(
                new RuntimeFrame(frame),
                new GameplayTriggerSourceBindingId(50),
                subjects,
                out GameplayTriggerResult result),
                result.ErrorCode.ToString());
            Assert.IsTrue(runtime.Evaluate(new RuntimeFrame(frame)).Success);
        }

        private static long ComputeModuleHash(GameplayTriggerRuntime runtime)
        {
            var accumulator = new RuntimeHashAccumulator(RuntimeHashSchemaIds.GameplayTriggers, RuntimeHashSchemaIds.Version1);
            new GameplayTriggerRuntimeHashContributor(runtime).Contribute(
                new RuntimeHashContext(RuntimeFrame.Zero),
                accumulator);
            return accumulator.ToHash();
        }

        private static GameplayTriggerInstanceAuthorityState CreateInstanceAuthorityState(
            int changedField = -1) =>
            new GameplayTriggerInstanceAuthorityState(
                new GameplayTriggerInstanceId(changedField == 0 ? 2 : 1, changedField == 1 ? 2 : 1),
                new GameplayTriggerBindingId(changedField == 2 ? 12 : 11),
                new GameplayTriggerSourceBindingId(changedField == 3 ? 22 : 21),
                new GameplayTriggerDefinitionId(changedField == 4 ? 32 : 31),
                changedField == 5 ? 42 : 41,
                new GameplayTriggerSourceDefinitionId(changedField == 6 ? 52 : 51),
                new GameplayEntityId(changedField == 7 ? 62 : 61, changedField == 8 ? 2 : 1),
                changedField == 9 ? false : true,
                changedField == 10 ? 72 : 71,
                changedField == 11 ? 82L : 81L);

        private static GameplayTriggerRelationAuthorityState CreateRelationAuthorityState(
            int changedField = -1) =>
            new GameplayTriggerRelationAuthorityState(
                new GameplayTriggerInstanceId(changedField == 0 ? 2 : 1, changedField == 1 ? 2 : 1),
                new GameplayEntityId(changedField == 2 ? 12 : 11, changedField == 3 ? 2 : 1),
                changedField == 4 ? false : true,
                changedField == 5 ? false : true,
                Fix64.FromRaw(changedField == 6 ? 22L : 21L),
                changedField == 7 ? 32L : 31L,
                changedField == 8 ? 42L : 41L);

        private static GameplayTriggerRouteAuthorityState CreateRouteAuthorityState(
            int changedField = -1) =>
            new GameplayTriggerRouteAuthorityState(
                new GameplayTriggerInstanceId(changedField == 0 ? 2 : 1, changedField == 1 ? 2 : 1),
                new GameplayTriggerRouteId(changedField == 2 ? 12 : 11),
                new GameplayEntityId(changedField == 3 ? 22 : 21, changedField == 4 ? 2 : 1),
                new GameplayEntityId(changedField == 5 ? 32 : 31, changedField == 6 ? 2 : 1),
                changedField == 7 ? 42 : 41,
                changedField == 8 ? false : true,
                changedField == 9 ? 52L : 51L,
                changedField == 10 ? 62L : 61L,
                changedField == 11 ? false : true,
                changedField == 12 ? 72L : 71L);

        private static long ComputeInstanceStateHash(GameplayTriggerInstanceAuthorityState state)
        {
            var accumulator = new RuntimeHashAccumulator(RuntimeHashSchemaIds.GameplayTriggers, RuntimeHashSchemaIds.Version1);
            GameplayTriggerRuntimeHashContributor.ContributeInstanceAuthorityState(
                accumulator,
                in state);
            return accumulator.ToHash();
        }

        private static long ComputeRelationStateHash(GameplayTriggerRelationAuthorityState state)
        {
            var accumulator = new RuntimeHashAccumulator(RuntimeHashSchemaIds.GameplayTriggers, RuntimeHashSchemaIds.Version1);
            GameplayTriggerRuntimeHashContributor.ContributeRelationAuthorityState(
                accumulator,
                in state);
            return accumulator.ToHash();
        }

        private static long ComputeRouteStateHash(GameplayTriggerRouteAuthorityState state)
        {
            var accumulator = new RuntimeHashAccumulator(RuntimeHashSchemaIds.GameplayTriggers, RuntimeHashSchemaIds.Version1);
            GameplayTriggerRuntimeHashContributor.ContributeRouteAuthorityState(
                accumulator,
                in state);
            return accumulator.ToHash();
        }

        private static void AssertDefinitionOnlyChanged(
            GameplayTriggerCatalog baseline,
            GameplayTriggerCatalog changed,
            string message)
        {
            Assert.AreNotEqual(baseline.DefinitionCatalogHash, changed.DefinitionCatalogHash, message);
            Assert.AreEqual(baseline.BindingCatalogHash, changed.BindingCatalogHash, message);
            Assert.AreNotEqual(baseline.GameplayTriggerCatalogHash, changed.GameplayTriggerCatalogHash, message);
        }

        private static void AssertBindingOnlyChanged(
            GameplayTriggerCatalog baseline,
            GameplayTriggerCatalog changed,
            string message)
        {
            Assert.AreEqual(baseline.DefinitionCatalogHash, changed.DefinitionCatalogHash, message);
            Assert.AreNotEqual(baseline.BindingCatalogHash, changed.BindingCatalogHash, message);
            Assert.AreNotEqual(baseline.GameplayTriggerCatalogHash, changed.GameplayTriggerCatalogHash, message);
        }

        private static GameplayTriggerCatalog BuildPermutationCatalog(bool reverse)
        {
            var source1 = new GameplayTriggerSourceDefinition(
                new GameplayTriggerSourceDefinitionId(1),
                GameplayTriggerSourceKind.Signal);
            var source2 = new GameplayTriggerSourceDefinition(
                new GameplayTriggerSourceDefinitionId(2),
                GameplayTriggerSourceKind.Occupancy);
            var kind1 = new GameplayTriggerActionKindId(1);
            var kind2 = new GameplayTriggerActionKindId(2);
            GameplayTriggerRouteDefinition Route(int id, GameplayTriggerActionKindId kind) =>
                new GameplayTriggerRouteDefinition(
                    new GameplayTriggerRouteId(id),
                    GameplayTriggerOccurrenceKind.Pulsed,
                    GameplayTriggerConditionProgram.Always,
                    new[]
                    {
                        new GameplayTriggerActionInstruction(kind, GameplayTriggerTargetSelector.Owner)
                    });
            var definition1 = new GameplayTriggerDefinition(
                new GameplayTriggerDefinitionId(1),
                1,
                source1.SourceDefinitionId,
                reverse
                    ? new[] { Route(2, kind2), Route(1, kind1) }
                    : new[] { Route(1, kind1), Route(2, kind2) });
            var definition2 = new GameplayTriggerDefinition(
                new GameplayTriggerDefinitionId(2),
                1,
                source2.SourceDefinitionId,
                new[] { Route(3, kind1) });
            GameplayTriggerBinding Binding(int id, GameplayTriggerDefinitionId definitionId, int sourceBinding) =>
                new GameplayTriggerBinding(
                    new GameplayTriggerBindingId(id),
                    definitionId,
                    new GameplayTriggerSourceBindingId(sourceBinding),
                    new GameplayEntityId(id, 1),
                    explicitTargets: reverse
                        ? new[]
                        {
                            new GameplayTriggerBindingTarget(2, new GameplayTriggerTargetRef(2, 2)),
                            new GameplayTriggerBindingTarget(1, new GameplayTriggerTargetRef(1, 1))
                        }
                        : new[]
                        {
                            new GameplayTriggerBindingTarget(1, new GameplayTriggerTargetRef(1, 1)),
                            new GameplayTriggerBindingTarget(2, new GameplayTriggerTargetRef(2, 2))
                        });
            GameplayTriggerSourceDefinition[] sources = reverse
                ? new[] { source2, source1 }
                : new[] { source1, source2 };
            GameplayTriggerDefinition[] definitions = reverse
                ? new[] { definition2, definition1 }
                : new[] { definition1, definition2 };
            GameplayTriggerBinding[] bindings = reverse
                ? new[] { Binding(2, definition2.DefinitionId, 2), Binding(1, definition1.DefinitionId, 1) }
                : new[] { Binding(1, definition1.DefinitionId, 1), Binding(2, definition2.DefinitionId, 2) };
            IGameplayTriggerActionSink[] sinks = reverse
                ? new IGameplayTriggerActionSink[] { new TestSink(kind2), new TestSink(kind1) }
                : new IGameplayTriggerActionSink[] { new TestSink(kind1), new TestSink(kind2) };
            Assert.IsTrue(GameplayTriggerCatalog.TryCreate(
                sources,
                definitions,
                bindings,
                sinks,
                out GameplayTriggerCatalog catalog,
                out GameplayTriggerCatalogValidationResult result),
                result.ErrorCode.ToString());
            return catalog;
        }

        private static GameplayTriggerCatalog BuildAlternateDefinitionCatalog(bool useAlternate)
        {
            var sourceId = new GameplayTriggerSourceDefinitionId(1);
            var kind = new GameplayTriggerActionKindId(1);
            GameplayTriggerDefinition Definition(int id) => new GameplayTriggerDefinition(
                new GameplayTriggerDefinitionId(id),
                1,
                sourceId,
                new[]
                {
                    new GameplayTriggerRouteDefinition(
                        new GameplayTriggerRouteId(id),
                        GameplayTriggerOccurrenceKind.Pulsed,
                        GameplayTriggerConditionProgram.Always,
                        new[]
                        {
                            new GameplayTriggerActionInstruction(kind, GameplayTriggerTargetSelector.Owner)
                        })
                });
            GameplayTriggerDefinition first = Definition(1);
            GameplayTriggerDefinition second = Definition(2);
            var binding = new GameplayTriggerBinding(
                new GameplayTriggerBindingId(1),
                useAlternate ? second.DefinitionId : first.DefinitionId,
                new GameplayTriggerSourceBindingId(1),
                new GameplayEntityId(1, 1));
            Assert.IsTrue(GameplayTriggerCatalog.TryCreate(
                new[] { new GameplayTriggerSourceDefinition(sourceId, GameplayTriggerSourceKind.Signal) },
                new[] { first, second },
                new[] { binding },
                new IGameplayTriggerActionSink[] { new TestSink(kind) },
                out GameplayTriggerCatalog catalog,
                out GameplayTriggerCatalogValidationResult result),
                result.ErrorCode.ToString());
            return catalog;
        }

        private sealed class CatalogVariant
        {
            public int SourceDefinitionId = 100;
            public int UnusedSourceDefinitionId = 150;
            public GameplayTriggerSourceKind SourceKind = GameplayTriggerSourceKind.Threshold;
            public GameplayTriggerSignalOrdering SignalOrdering = GameplayTriggerSignalOrdering.Ordered;
            public GameplayTriggerThresholdDirection ThresholdDirection = GameplayTriggerThresholdDirection.High;
            public long EnterThresholdRaw = 5L;
            public long ExitThresholdRaw = 5L;
            public GameplayTriggerInitialSamplePolicy InitialSamplePolicy =
                GameplayTriggerInitialSamplePolicy.EmitEnteredIfActive;
            public GameplayTriggerSubjectDeparturePolicy DeparturePolicy =
                GameplayTriggerSubjectDeparturePolicy.EmitExited;
            public int DefinitionId = 200;
            public int UnusedDefinitionId = 250;
            public int DefinitionVersion = 2;
            public int RouteId = 300;
            public GameplayTriggerOccurrenceKind OccurrenceMask =
                GameplayTriggerOccurrenceKind.Entered | GameplayTriggerOccurrenceKind.Stayed;
            public GameplayTriggerConditionOperation ConditionOperation =
                GameplayTriggerConditionOperation.Payload0;
            public long ConditionOperand = 5L;
            public GameplayTriggerComparison ConditionComparison = GameplayTriggerComparison.Greater;
            public bool ReverseConditionOperands;
            public GameplayTriggerStateScope StateScope = GameplayTriggerStateScope.PerSubject;
            public int MaxActivations = 3;
            public long CooldownFrames = 7L;
            public GameplayTriggerPresenceLimit PresenceLimit = GameplayTriggerPresenceLimit.Ignore;
            public long StayIntervalFrames = 8L;
            public int ExecutionOrder = 9;
            public int ExclusiveGroupId = 0;
            public int WinnerPriority = 10;
            public GameplayTriggerArbitrationScope ArbitrationScope =
                GameplayTriggerArbitrationScope.Global;
            public int Action1KindId = 401;
            public GameplayTriggerTargetSelector Action1TargetSelector =
                GameplayTriggerTargetSelector.Owner;
            public int Action1TargetSlotId;
            public long Action1Payload0 = 31L;
            public long Action1Payload1 = 32L;
            public long Action1Payload2 = 33L;
            public GameplayTriggerDeliveryContract Sink1Delivery =
                GameplayTriggerDeliveryContract.SameFrameReserved;
            public int UnusedSinkKindId = 403;
            public bool ReverseActions;
            public int BindingId = 500;
            public int SourceBindingId = 600;
            public int OwnerIndex = 700;
            public int OwnerGeneration = 3;
            public bool InitiallyEnabled = true;
            public int Target1DomainId = 2;
            public long Target1Value = 800L;
            public int Target1Generation = 4;
            public bool IncludeSecondTarget = true;
            public int Target2SlotId = 10;
            public int Target2DomainId = 3;
            public long Target2Value = 801L;
            public int Target2Generation = 5;
            public bool UseAlternateSinkImplementation;

            public CatalogVariant Copy() => (CatalogVariant)MemberwiseClone();

            public GameplayTriggerCatalog Build()
            {
                var sourceId = new GameplayTriggerSourceDefinitionId(SourceDefinitionId);
                var definitionId = new GameplayTriggerDefinitionId(DefinitionId);
                var action1Kind = new GameplayTriggerActionKindId(Action1KindId);
                var action2Kind = new GameplayTriggerActionKindId(402);
                var source = new GameplayTriggerSourceDefinition(
                    sourceId,
                    SourceKind,
                    SignalOrdering,
                    ThresholdDirection,
                    Fix64.FromRaw(EnterThresholdRaw),
                    Fix64.FromRaw(ExitThresholdRaw),
                    InitialSamplePolicy,
                    DeparturePolicy);
                var unusedSource = new GameplayTriggerSourceDefinition(
                    new GameplayTriggerSourceDefinitionId(UnusedSourceDefinitionId),
                    GameplayTriggerSourceKind.Signal);
                GameplayTriggerConditionInstruction first =
                    new GameplayTriggerConditionInstruction(
                        ConditionOperation,
                        ConditionOperand,
                        ConditionComparison);
                GameplayTriggerConditionInstruction second =
                    new GameplayTriggerConditionInstruction(GameplayTriggerConditionOperation.PushTrue);
                var condition = new GameplayTriggerConditionProgram(
                    ReverseConditionOperands
                        ? new[]
                        {
                            second,
                            first,
                            new GameplayTriggerConditionInstruction(GameplayTriggerConditionOperation.All)
                        }
                        : new[]
                        {
                            first,
                            second,
                            new GameplayTriggerConditionInstruction(GameplayTriggerConditionOperation.All)
                        });
                var action1 = new GameplayTriggerActionInstruction(
                    action1Kind,
                    Action1TargetSelector,
                    Action1TargetSlotId,
                    Action1Payload0,
                    Action1Payload1,
                    Action1Payload2);
                var action2 = new GameplayTriggerActionInstruction(
                    action2Kind,
                    GameplayTriggerTargetSelector.ExplicitBindingTarget,
                    targetSlotId: 9,
                    payload0: 34L,
                    payload1: 35L,
                    payload2: 36L);
                GameplayTriggerActionInstruction[] actions = ReverseActions
                    ? new[] { action2, action1 }
                    : new[] { action1, action2 };
                var route = new GameplayTriggerRouteDefinition(
                    new GameplayTriggerRouteId(RouteId),
                    OccurrenceMask,
                    condition,
                    actions,
                    new GameplayTriggerActivationPolicy(
                        StateScope,
                        MaxActivations,
                        CooldownFrames,
                        PresenceLimit,
                        StayIntervalFrames),
                    ExecutionOrder,
                    ExclusiveGroupId,
                    WinnerPriority,
                    ArbitrationScope);
                var definition = new GameplayTriggerDefinition(
                    definitionId,
                    DefinitionVersion,
                    sourceId,
                    new[] { route });
                var unusedDefinition = new GameplayTriggerDefinition(
                    new GameplayTriggerDefinitionId(UnusedDefinitionId),
                    1,
                    sourceId,
                    new[]
                    {
                        new GameplayTriggerRouteDefinition(
                            new GameplayTriggerRouteId(350),
                            GameplayTriggerOccurrenceKind.Pulsed,
                            GameplayTriggerConditionProgram.Always,
                            new[]
                            {
                                new GameplayTriggerActionInstruction(
                                    action2Kind,
                                    GameplayTriggerTargetSelector.Owner)
                            })
                    });
                var targets = new List<GameplayTriggerBindingTarget>
                {
                    new GameplayTriggerBindingTarget(
                        9,
                        new GameplayTriggerTargetRef(
                            Target1DomainId,
                            Target1Value,
                            Target1Generation))
                };
                if (IncludeSecondTarget)
                {
                    targets.Add(new GameplayTriggerBindingTarget(
                        Target2SlotId,
                        new GameplayTriggerTargetRef(
                            Target2DomainId,
                            Target2Value,
                            Target2Generation)));
                }
                var binding = new GameplayTriggerBinding(
                    new GameplayTriggerBindingId(BindingId),
                    definitionId,
                    new GameplayTriggerSourceBindingId(SourceBindingId),
                    new GameplayEntityId(OwnerIndex, OwnerGeneration),
                    InitiallyEnabled,
                    targets.ToArray());
                var firstRegisteredKind = new GameplayTriggerActionKindId(401);
                IGameplayTriggerActionSink firstSink = UseAlternateSinkImplementation
                    ? new AlternateTestSink(firstRegisteredKind, Sink1Delivery)
                    : new TestSink(firstRegisteredKind, Sink1Delivery);
                Assert.IsTrue(GameplayTriggerCatalog.TryCreate(
                    new[] { source, unusedSource },
                    new[] { definition, unusedDefinition },
                    new[] { binding },
                    new IGameplayTriggerActionSink[]
                    {
                        firstSink,
                        new TestSink(action2Kind, GameplayTriggerDeliveryContract.PresentationOnly),
                        new TestSink(
                            new GameplayTriggerActionKindId(UnusedSinkKindId),
                            GameplayTriggerDeliveryContract.SameFrameReserved)
                    },
                    out GameplayTriggerCatalog catalog,
                    out GameplayTriggerCatalogValidationResult result),
                    result.ErrorCode.ToString());
                return catalog;
            }
        }

        private class TestSink : IGameplayTriggerActionSink
        {
            public TestSink(
                GameplayTriggerActionKindId actionKindId,
                GameplayTriggerDeliveryContract deliveryContract =
                    GameplayTriggerDeliveryContract.SameFrameReserved)
            {
                ActionKindId = actionKindId;
                DeliveryContract = deliveryContract;
            }

            public GameplayTriggerActionKindId ActionKindId { get; }
            public GameplayTriggerDeliveryContract DeliveryContract { get; }
            public bool TryPrepare(in GameplayTriggerActionPreparation preparation, out int token)
            {
                token = 1;
                return true;
            }
            public void Abort(int token) { }
            public void Commit(int token, in GameplayTriggerActionIntent intent) { }
        }

        private sealed class AlternateTestSink : TestSink
        {
            public AlternateTestSink(
                GameplayTriggerActionKindId actionKindId,
                GameplayTriggerDeliveryContract deliveryContract)
                : base(actionKindId, deliveryContract)
            {
            }
        }

        private sealed class MutableTestSink : IGameplayTriggerActionSink
        {
            public MutableTestSink(
                GameplayTriggerActionKindId actionKindId,
                GameplayTriggerDeliveryContract deliveryContract)
            {
                ActionKindId = actionKindId;
                DeliveryContract = deliveryContract;
            }

            public GameplayTriggerActionKindId ActionKindId { get; set; }
            public GameplayTriggerDeliveryContract DeliveryContract { get; set; }
            public int PrepareCount { get; private set; }
            public int CommitCount { get; private set; }

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
                CommitCount++;
            }
        }

        private sealed class ConstantContributor : IRuntimeHashContributor
        {
            private readonly int _value;
            public ConstantContributor(string contributorId, int value)
            {
                DiagnosticContributorId = contributorId;
                _value = value;
            }
            public int ContributorId => DiagnosticContributorId.GetHashCode() & 0x7fffffff;
            public int HashSchemaId => 91000002;
            public int HashSchemaVersion => 1;
            public string DiagnosticContributorId { get; }
            public void Contribute(RuntimeHashContext context, RuntimeHashAccumulator accumulator) =>
                accumulator.AddInt(1, _value);
        }
    }
}
