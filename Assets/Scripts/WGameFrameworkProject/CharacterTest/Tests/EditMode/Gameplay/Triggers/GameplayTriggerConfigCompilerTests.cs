using System;
using MxFramework.Gameplay.Triggers;
using MxFramework.Gameplay.Triggers.Config;
using NUnit.Framework;

namespace MxFramework.Tests.Gameplay.Triggers
{
    public sealed class GameplayTriggerConfigCompilerTests
    {
        private static readonly GameplayTriggerActionKindId SameFrameActionKind = new GameplayTriggerActionKindId(1001);
        private static readonly GameplayTriggerActionKindId ReliableActionKind = new GameplayTriggerActionKindId(1002);
        private static readonly GameplayTriggerActionKindId InternalSignalActionKind = new GameplayTriggerActionKindId(1003);

        private static readonly string[] CapacityFields =
        {
            "MaxInstances",
            "MaxInputBatches",
            "MaxInputItems",
            "MaxRelations",
            "MaxOccurrences",
            "MaxCandidates",
            "MaxRouteStates",
            "MaxActivations",
            "MaxIntents",
            "MaxPreparedActions",
            "MaxConditionStackDepth",
            "MaxDeferredRecords",
            "MaxReliableOutboxRecords"
        };

        [Test]
        public void Compile_CanonicalSignalDocument_SucceedsWithConstructibleMinimums()
        {
            GameplayTriggerCompileResult result = Compile(CreateDocument(), CreateEnvironment());

            Assert.That(result.Succeeded, Is.True, Describe(result));
            Assert.That(result.CompiledCatalog.SourceDefinitionCount, Is.EqualTo(1));
            Assert.That(result.CompiledCatalog.DefinitionCount, Is.EqualTo(1));
            Assert.That(result.CompiledCatalog.BindingCount, Is.EqualTo(1));
            Assert.That(result.CompiledCatalog.CapacityShape.MaxRelations, Is.Zero);
            Assert.That(result.CompiledCatalog.CapacityShape.MaxDeferredRecords, Is.Zero);
            Assert.That(result.CompiledCatalog.CapacityShape.MaxReliableOutboxRecords, Is.Zero);
            Assert.That(result.CompiledCatalog.CapacityRequirements.MaxRelations, Is.EqualTo(1));
            Assert.That(result.CompiledCatalog.CapacityRequirements.MaxDeferredRecords, Is.Zero);
            Assert.That(result.CompiledCatalog.CapacityRequirements.MaxReliableOutboxRecords, Is.Zero);
        }

        [Test]
        public void Compile_ThresholdAndOrderedSignal_PositiveCasesSucceed()
        {
            var thresholdSource = new GameplayTriggerSourceAuthoring(
                1,
                GameplayTriggerSourceKind.Threshold,
                thresholdDirection: GameplayTriggerThresholdDirection.High,
                enterThreshold: GameplayTriggerFix64Authoring.FromRaw(10),
                exitThreshold: GameplayTriggerFix64Authoring.FromRaw(5));
            var thresholdContract = new GameplayTriggerSourceBindingContractDescriptor(
                new GameplayTriggerSourceBindingId(1),
                new GameplayTriggerSourceDefinitionId(1),
                GameplayTriggerSourceKind.Threshold,
                new GameplayTriggerSourceProviderId(1),
                GameplayTriggerSourceEntityCapability.None,
                true,
                2,
                2);
            GameplayTriggerCompileResult threshold = Compile(
                CreateDocument(
                    source: thresholdSource,
                    route: new GameplayTriggerRouteAuthoring(
                        1,
                        GameplayTriggerOccurrenceKind.Entered,
                        1,
                        new[] { new GameplayTriggerActionAuthoring(SameFrameActionKind.Value, GameplayTriggerTargetSelector.Owner) })),
                CreateEnvironment(sourceContracts: new[] { thresholdContract }));

            var orderedSource = new GameplayTriggerSourceAuthoring(
                1,
                GameplayTriggerSourceKind.Signal,
                signalOrdering: GameplayTriggerSignalOrdering.Ordered);
            var orderedContract = new GameplayTriggerSourceBindingContractDescriptor(
                new GameplayTriggerSourceBindingId(1),
                new GameplayTriggerSourceDefinitionId(1),
                GameplayTriggerSourceKind.Signal,
                new GameplayTriggerSourceProviderId(1),
                GameplayTriggerSourceEntityCapability.None,
                true,
                2,
                0,
                signalEnvelopeSchemaVersion: 1,
                signalOrdering: GameplayTriggerSignalOrdering.Ordered,
                preservesOrderedSequence: true);
            GameplayTriggerCompileResult ordered = Compile(
                CreateDocument(source: orderedSource),
                CreateEnvironment(sourceContracts: new[] { orderedContract }));

            Assert.That(threshold.Succeeded, Is.True, Describe(threshold));
            Assert.That(ordered.Succeeded, Is.True, Describe(ordered));
        }

        [Test]
        public void Compile_PermutationsProduceSameHashesWithoutMutatingInputs()
        {
            GameplayTriggerSourceAuthoring source1 = CreateSignalSource(1);
            GameplayTriggerSourceAuthoring source2 = CreateSignalSource(2);
            GameplayTriggerConditionAuthoring condition1 = TrueCondition(1);
            GameplayTriggerConditionAuthoring condition2 = TrueCondition(2);
            GameplayTriggerDefinitionAuthoring definition1 = CreateDefinition(1, 1, 1, 1);
            GameplayTriggerDefinitionAuthoring definition2 = CreateDefinition(2, 2, 2, 2);
            GameplayTriggerBindingAuthoring binding1 = new GameplayTriggerBindingAuthoring(1, 1, 1, 1, 1);
            GameplayTriggerBindingAuthoring binding2 = new GameplayTriggerBindingAuthoring(2, 2, 2, 2, 1);
            var sources = new[] { source2, source1 };
            var conditions = new[] { condition2, condition1 };
            var definitions = new[] { definition2, definition1 };
            var bindings = new[] { binding2, binding1 };
            var document = new GameplayTriggerAuthoringDocument(
                GameplayTriggerConfigSchemas.AuthoringId,
                GameplayTriggerConfigSchemas.AuthoringVersion,
                sources,
                conditions,
                definitions,
                bindings);
            GameplayTriggerCompilerEnvironment environment = CreateEnvironment(
                sourceContracts: new[]
                {
                    CreateSignalContract(2, 2, 9),
                    CreateSignalContract(1, 1, 9)
                });

            GameplayTriggerCompileResult first = Compile(document, environment);
            GameplayTriggerCompileResult second = Compile(
                new GameplayTriggerAuthoringDocument(
                    GameplayTriggerConfigSchemas.AuthoringId,
                    GameplayTriggerConfigSchemas.AuthoringVersion,
                    new[] { source1, source2 },
                    new[] { condition1, condition2 },
                    new[] { definition1, definition2 },
                    new[] { binding1, binding2 }),
                environment);

            Assert.That(first.Succeeded, Is.True, Describe(first));
            Assert.That(second.Succeeded, Is.True, Describe(second));
            Assert.That(first.CompiledCatalog.DefinitionCatalogHash, Is.EqualTo(second.CompiledCatalog.DefinitionCatalogHash));
            Assert.That(first.CompiledCatalog.BindingCatalogHash, Is.EqualTo(second.CompiledCatalog.BindingCatalogHash));
            Assert.That(first.CompiledCatalog.GameplayTriggerCatalogHash, Is.EqualTo(second.CompiledCatalog.GameplayTriggerCatalogHash));
            AssertCanonicalProjectionEqual(first.CompiledCatalog, second.CompiledCatalog);
            Assert.That(sources[0], Is.SameAs(source2));
            Assert.That(conditions[0], Is.SameAs(condition2));
            Assert.That(definitions[0], Is.SameAs(definition2));
            Assert.That(bindings[0], Is.SameAs(binding2));
        }

        [Test]
        public void CompiledPlan_PublicProjectionDoesNotExposeCompositionOrUnityIdentity()
        {
            GameplayTriggerCompileResult compile = Compile(CreateDocument(), CreateEnvironment());
            Assert.That(compile.Succeeded, Is.True, Describe(compile));

            Type planType = typeof(GameplayTriggerCompiledCatalogPlan);
            foreach (System.Reflection.PropertyInfo property in planType.GetProperties(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public))
                AssertPublicProjectionType(property.PropertyType, property.Name);
            foreach (System.Reflection.MethodInfo method in planType.GetMethods(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.DeclaredOnly))
                AssertPublicProjectionType(method.ReturnType, method.Name);
        }

        [Test]
        public void SourceRegistry_RepeatedProviderIdAcrossSourceBindings_IsAccepted()
        {
            bool succeeded = GameplayTriggerSourceBindingContractRegistry.TryCreate(
                new[]
                {
                    CreateSignalContract(2, 2, 7),
                    CreateSignalContract(1, 1, 7)
                },
                out GameplayTriggerSourceBindingContractRegistry registry,
                out GameplayTriggerConfigRegistryResult result);

            Assert.That(succeeded, Is.True, Describe(result));
            Assert.That(registry.Count, Is.EqualTo(2));
            Assert.That(registry.Get(0).SourceBindingId.Value, Is.EqualTo(1));
            Assert.That(registry.Get(1).SourceBindingId.Value, Is.EqualTo(2));
            Assert.That(registry.Get(0).ProviderId, Is.EqualTo(registry.Get(1).ProviderId));
        }

        [Test]
        public void SourceRegistry_DuplicateSourceBindingId_IsRejected()
        {
            bool succeeded = GameplayTriggerSourceBindingContractRegistry.TryCreate(
                new[]
                {
                    CreateSignalContract(1, 1, 7),
                    CreateSignalContract(1, 1, 8)
                },
                out _,
                out GameplayTriggerConfigRegistryResult result);

            Assert.That(succeeded, Is.False);
            Assert.That(Contains(result, GameplayTriggerConfigDiagnosticCode.DuplicateSourceBindingId), Is.True, Describe(result));
        }

        [Test]
        public void SourceRegistry_NonSignalReplayMetadataMustUseCanonicalSentinel()
        {
            var descriptor = new GameplayTriggerSourceBindingContractDescriptor(
                new GameplayTriggerSourceBindingId(1),
                new GameplayTriggerSourceDefinitionId(1),
                GameplayTriggerSourceKind.Occupancy,
                new GameplayTriggerSourceProviderId(1),
                GameplayTriggerSourceEntityCapability.None,
                true,
                1,
                1,
                signalEnvelopeSchemaVersion: 0,
                signalOrdering: GameplayTriggerSignalOrdering.Ordered,
                preservesOrderedSequence: false);

            bool succeeded = GameplayTriggerSourceBindingContractRegistry.TryCreate(
                new[] { descriptor },
                out _,
                out GameplayTriggerConfigRegistryResult result);

            Assert.That(succeeded, Is.False);
            Assert.That(Contains(result, GameplayTriggerConfigDiagnosticCode.InvalidSignalReplayMetadata), Is.True, Describe(result));
        }

        [Test]
        public void SourceRegistry_SignalPresenceEpochCapability_IsRejected()
        {
            GameplayTriggerSourceBindingContractDescriptor descriptor = CreateSignalContract(
                1,
                1,
                1,
                GameplayTriggerSourceEntityCapability.PresenceEpoch);

            bool succeeded = GameplayTriggerSourceBindingContractRegistry.TryCreate(
                new[] { descriptor },
                out _,
                out GameplayTriggerConfigRegistryResult result);

            Assert.That(succeeded, Is.False);
            Assert.That(Contains(result, GameplayTriggerConfigDiagnosticCode.SourceCapabilityMismatch), Is.True, Describe(result));
        }

        [Test]
        public void SourceRegistry_UnorderedSignalCannotClaimOrderedPreservation()
        {
            var descriptor = new GameplayTriggerSourceBindingContractDescriptor(
                new GameplayTriggerSourceBindingId(1),
                new GameplayTriggerSourceDefinitionId(1),
                GameplayTriggerSourceKind.Signal,
                new GameplayTriggerSourceProviderId(1),
                GameplayTriggerSourceEntityCapability.None,
                true,
                1,
                0,
                signalEnvelopeSchemaVersion: 1,
                signalOrdering: GameplayTriggerSignalOrdering.UnorderedMultiset,
                preservesOrderedSequence: true);

            bool succeeded = GameplayTriggerSourceBindingContractRegistry.TryCreate(
                new[] { descriptor },
                out _,
                out GameplayTriggerConfigRegistryResult result);

            Assert.That(succeeded, Is.False);
            Assert.That(Contains(result, GameplayTriggerConfigDiagnosticCode.InvalidSignalReplayMetadata), Is.True, Describe(result));
        }

        [TestCase(GameplayTriggerSourceKind.Signal, 0, true)]
        [TestCase(GameplayTriggerSourceKind.Signal, 1, false)]
        [TestCase(GameplayTriggerSourceKind.Occupancy, 0, false)]
        [TestCase(GameplayTriggerSourceKind.Occupancy, 1, true)]
        [TestCase(GameplayTriggerSourceKind.Threshold, 0, false)]
        [TestCase(GameplayTriggerSourceKind.Threshold, 1, true)]
        public void SourceRegistry_ValidatesRelationBoundsBySourceKind(GameplayTriggerSourceKind kind, int maxRelations, bool expected)
        {
            var descriptor = new GameplayTriggerSourceBindingContractDescriptor(
                new GameplayTriggerSourceBindingId(1),
                new GameplayTriggerSourceDefinitionId(1),
                kind,
                new GameplayTriggerSourceProviderId(1),
                GameplayTriggerSourceEntityCapability.None,
                true,
                1,
                maxRelations,
                kind == GameplayTriggerSourceKind.Signal ? 1 : 0,
                GameplayTriggerSignalOrdering.UnorderedMultiset);

            bool succeeded = GameplayTriggerSourceBindingContractRegistry.TryCreate(
                new[] { descriptor },
                out _,
                out GameplayTriggerConfigRegistryResult result);

            Assert.That(succeeded, Is.EqualTo(expected), Describe(result));
        }

        [Test]
        public void Compile_RetiredSourceId_IsRejected()
        {
            GameplayTriggerCompilerEnvironment environment = CreateEnvironment(
                retired: new[] { new GameplayTriggerRetiredIdDescriptor(GameplayTriggerConfigIdKind.SourceDefinition, 1) });

            GameplayTriggerCompileResult result = Compile(CreateDocument(), environment);

            AssertDiagnostic(result, GameplayTriggerConfigDiagnosticCode.RetiredId);
        }

        [Test]
        public void Compile_ZeroAndRetiredNestedActionIds_AreTypedBeforeUnknownContract()
        {
            GameplayTriggerCompileResult zero = Compile(
                CreateDocument(action: new GameplayTriggerActionAuthoring(0, GameplayTriggerTargetSelector.Owner)),
                CreateEnvironment());
            GameplayTriggerCompileResult retired = Compile(
                CreateDocument(action: new GameplayTriggerActionAuthoring(2222, GameplayTriggerTargetSelector.Owner)),
                CreateEnvironment(
                    retired: new[]
                    {
                        new GameplayTriggerRetiredIdDescriptor(GameplayTriggerConfigIdKind.ActionKind, 2222)
                    }));

            AssertDiagnostic(zero, GameplayTriggerConfigDiagnosticCode.InvalidId);
            Assert.That(Contains(zero, GameplayTriggerConfigDiagnosticCode.UnknownActionContract), Is.False, Describe(zero));
            AssertDiagnostic(retired, GameplayTriggerConfigDiagnosticCode.RetiredId);
            bool hasNestedPath = false;
            for (int i = 0; i < retired.DiagnosticCount; i++)
                if (retired.GetDiagnostic(i).Code == GameplayTriggerConfigDiagnosticCode.RetiredId && retired.GetDiagnostic(i).Path.IndexOf(".actions[0].actionKindId", StringComparison.Ordinal) >= 0) hasNestedPath = true;
            Assert.That(hasNestedPath, Is.True, Describe(retired));
        }

        [Test]
        public void Compile_ExpectedHashMismatch_IsReachableAndTyped()
        {
            GameplayTriggerCompilerEnvironment environment = CreateEnvironment(expectedHash: 123456789L);

            GameplayTriggerCompileResult result = Compile(CreateDocument(), environment);

            AssertDiagnostic(result, GameplayTriggerConfigDiagnosticCode.CompiledCatalogHashMismatch);
        }

        [Test]
        public void Compile_SubjectSelectorWithoutGuaranteedCapability_IsRejected()
        {
            GameplayTriggerSourceBindingContractDescriptor contract = CreateSignalContract(
                1,
                1,
                1,
                GameplayTriggerSourceEntityCapability.SubjectPossible);
            GameplayTriggerAuthoringDocument document = CreateDocument(
                action: new GameplayTriggerActionAuthoring(SameFrameActionKind.Value, GameplayTriggerTargetSelector.Subject));

            GameplayTriggerCompileResult result = Compile(
                document,
                CreateEnvironment(sourceContracts: new[] { contract }));

            AssertDiagnostic(result, GameplayTriggerConfigDiagnosticCode.MissingSubjectCapability);
        }

        [Test]
        public void Compile_ConditionCycle_IsRejectedWithoutRecursion()
        {
            var children = new GameplayTriggerConditionNodeAuthoring[1];
            var root = new GameplayTriggerConditionNodeAuthoring(GameplayTriggerConditionOperation.Not, children: children);
            children[0] = root;
            GameplayTriggerAuthoringDocument document = CreateDocument(condition: new GameplayTriggerConditionAuthoring(1, root));

            GameplayTriggerCompileResult result = Compile(document, CreateEnvironment());

            AssertDiagnostic(result, GameplayTriggerConfigDiagnosticCode.ConditionCycle);
        }

        [Test]
        public void Compile_ConditionInstructionLimit_HasExactBoundary()
        {
            var root = new GameplayTriggerConditionNodeAuthoring(
                GameplayTriggerConditionOperation.All,
                children: new[]
                {
                    new GameplayTriggerConditionNodeAuthoring(GameplayTriggerConditionOperation.PushTrue),
                    new GameplayTriggerConditionNodeAuthoring(GameplayTriggerConditionOperation.PushTrue)
                });
            GameplayTriggerAuthoringDocument document = CreateDocument(condition: new GameplayTriggerConditionAuthoring(1, root));
            var atLimit = new GameplayTriggerCompilerOptions(4, 3, 2, 4, 4, 16);
            var belowLimit = new GameplayTriggerCompilerOptions(4, 2, 2, 4, 4, 16);

            GameplayTriggerCompileResult succeeds = GameplayTriggerConfigCompiler.Compile(document, atLimit, CreateEnvironment());
            GameplayTriggerCompileResult fails = GameplayTriggerConfigCompiler.Compile(document, belowLimit, CreateEnvironment());

            Assert.That(succeeds.Succeeded, Is.True, Describe(succeeds));
            AssertDiagnostic(fails, GameplayTriggerConfigDiagnosticCode.ConditionInstructionExceeded);
        }

        [Test]
        public void Compile_NotAnyConditionLowersAndChildOrderChangesDefinitionHash()
        {
            GameplayTriggerConditionNodeAuthoring firstAny = new GameplayTriggerConditionNodeAuthoring(
                GameplayTriggerConditionOperation.Any,
                children: new[]
                {
                    new GameplayTriggerConditionNodeAuthoring(GameplayTriggerConditionOperation.PushTrue),
                    new GameplayTriggerConditionNodeAuthoring(GameplayTriggerConditionOperation.PushFalse)
                });
            GameplayTriggerConditionNodeAuthoring secondAny = new GameplayTriggerConditionNodeAuthoring(
                GameplayTriggerConditionOperation.Any,
                children: new[]
                {
                    new GameplayTriggerConditionNodeAuthoring(GameplayTriggerConditionOperation.PushFalse),
                    new GameplayTriggerConditionNodeAuthoring(GameplayTriggerConditionOperation.PushTrue)
                });
            GameplayTriggerConditionAuthoring firstCondition = new GameplayTriggerConditionAuthoring(
                1,
                new GameplayTriggerConditionNodeAuthoring(GameplayTriggerConditionOperation.Not, children: new[] { firstAny }));
            GameplayTriggerConditionAuthoring secondCondition = new GameplayTriggerConditionAuthoring(
                1,
                new GameplayTriggerConditionNodeAuthoring(GameplayTriggerConditionOperation.Not, children: new[] { secondAny }));

            GameplayTriggerCompileResult first = Compile(CreateDocument(condition: firstCondition), CreateEnvironment());
            GameplayTriggerCompileResult second = Compile(CreateDocument(condition: secondCondition), CreateEnvironment());

            Assert.That(first.Succeeded, Is.True, Describe(first));
            Assert.That(second.Succeeded, Is.True, Describe(second));
            Assert.That(second.CompiledCatalog.DefinitionCatalogHash, Is.Not.EqualTo(first.CompiledCatalog.DefinitionCatalogHash));
        }

        [Test]
        public void Compile_MaxIntInstructionOption_DoesNotOverflowOrMisclassifySmallTree()
        {
            var options = new GameplayTriggerCompilerOptions(4, int.MaxValue, 2, 4, 4, 16);

            GameplayTriggerCompileResult result = GameplayTriggerConfigCompiler.Compile(
                CreateDocument(),
                options,
                CreateEnvironment());

            Assert.That(result.Succeeded, Is.True, Describe(result));
        }

        [Test]
        public void Compile_NonAdjacentDuplicateSourceBinding_IsCanonicalAcrossPermutations()
        {
            GameplayTriggerBindingAuthoring binding1 = new GameplayTriggerBindingAuthoring(1, 1, 1, 1, 1);
            GameplayTriggerBindingAuthoring binding2 = new GameplayTriggerBindingAuthoring(2, 1, 2, 2, 1);
            GameplayTriggerBindingAuthoring binding3 = new GameplayTriggerBindingAuthoring(3, 1, 1, 3, 1);
            GameplayTriggerCompilerEnvironment environment = CreateEnvironment(
                sourceContracts: new[]
                {
                    CreateSignalContract(1, 1, 1),
                    CreateSignalContract(2, 1, 1)
                });
            GameplayTriggerAuthoringDocument firstDocument = CreateDocumentWithBindings(new[] { binding1, binding2, binding3 });
            GameplayTriggerAuthoringDocument secondDocument = CreateDocumentWithBindings(new[] { binding3, binding1, binding2 });

            GameplayTriggerCompileResult first = Compile(firstDocument, environment);
            GameplayTriggerCompileResult second = Compile(secondDocument, environment);

            AssertDiagnostic(first, GameplayTriggerConfigDiagnosticCode.DuplicateSourceBindingId);
            AssertDiagnostic(second, GameplayTriggerConfigDiagnosticCode.DuplicateSourceBindingId);
            GameplayTriggerConfigDiagnostic firstDiagnostic = Find(first, GameplayTriggerConfigDiagnosticCode.DuplicateSourceBindingId);
            GameplayTriggerConfigDiagnostic secondDiagnostic = Find(second, GameplayTriggerConfigDiagnosticCode.DuplicateSourceBindingId);
            Assert.That(firstDiagnostic.Path, Is.EqualTo("$.bindings[id=3].sourceBindingId"));
            Assert.That(secondDiagnostic.Path, Is.EqualTo(firstDiagnostic.Path));
            Assert.That(secondDiagnostic.PrimaryId, Is.EqualTo(firstDiagnostic.PrimaryId));
            Assert.That(secondDiagnostic.SecondaryId, Is.EqualTo(firstDiagnostic.SecondaryId));
        }

        [Test]
        public void Compile_InvariantDecimalExponent_IsRejected()
        {
            var source = new GameplayTriggerSourceAuthoring(
                1,
                GameplayTriggerSourceKind.Threshold,
                enterThreshold: GameplayTriggerFix64Authoring.FromInvariantDecimal("1e0"),
                exitThreshold: GameplayTriggerFix64Authoring.FromRaw(0));
            GameplayTriggerAuthoringDocument document = CreateDocument(source: source);
            var contract = new GameplayTriggerSourceBindingContractDescriptor(
                new GameplayTriggerSourceBindingId(1),
                new GameplayTriggerSourceDefinitionId(1),
                GameplayTriggerSourceKind.Threshold,
                new GameplayTriggerSourceProviderId(1),
                GameplayTriggerSourceEntityCapability.None,
                true,
                1,
                1);

            GameplayTriggerCompileResult result = Compile(document, CreateEnvironment(sourceContracts: new[] { contract }));

            AssertDiagnostic(result, GameplayTriggerConfigDiagnosticCode.InvalidHysteresis);
        }

        [TestCase("79228162514264337593543950335")]
        [TestCase("-79228162514264337593543950335")]
        public void Compile_InvariantDecimalExtreme_FailsClosedWithoutOverflow(string value)
        {
            var source = new GameplayTriggerSourceAuthoring(
                1,
                GameplayTriggerSourceKind.Threshold,
                enterThreshold: GameplayTriggerFix64Authoring.FromInvariantDecimal(value),
                exitThreshold: GameplayTriggerFix64Authoring.FromRaw(0));
            var contract = new GameplayTriggerSourceBindingContractDescriptor(
                new GameplayTriggerSourceBindingId(1),
                new GameplayTriggerSourceDefinitionId(1),
                GameplayTriggerSourceKind.Threshold,
                new GameplayTriggerSourceProviderId(1),
                GameplayTriggerSourceEntityCapability.None,
                true,
                1,
                1);

            GameplayTriggerCompileResult result = Compile(
                CreateDocument(source: source),
                CreateEnvironment(sourceContracts: new[] { contract }));

            AssertDiagnostic(result, GameplayTriggerConfigDiagnosticCode.InvalidHysteresis);
        }

        [TestCase("1.000001", true)]
        [TestCase("1.0000000", false)]
        [TestCase("0.1234560", false)]
        [TestCase("0.1234567", false)]
        public void Compile_InvariantDecimalScale_IsStrictlyLimitedToSixTextDigits(string value, bool expectedSuccess)
        {
            GameplayTriggerCompileResult result = Compile(
                CreateThresholdDecimalDocument(value),
                CreateEnvironment(sourceContracts: new[] { CreateStatefulContract(1, 1, GameplayTriggerSourceKind.Threshold) }));

            Assert.That(result.Succeeded, Is.EqualTo(expectedSuccess), Describe(result));
            if (expectedSuccess)
                Assert.That(result.CompiledCatalog.GetSourceDefinition(0).EnterThreshold.RawValue, Is.EqualTo(1000001L));
            else
                AssertDiagnostic(result, GameplayTriggerConfigDiagnosticCode.InvalidHysteresis);
        }

        [Test]
        public void Compile_InvalidInvariantDecimalScale_DoesNotPolluteFollowingValidCompile()
        {
            GameplayTriggerCompilerEnvironment environment = CreateEnvironment(
                sourceContracts: new[] { CreateStatefulContract(1, 1, GameplayTriggerSourceKind.Threshold) });

            GameplayTriggerCompileResult failed = Compile(CreateThresholdDecimalDocument("1.0000000"), environment);
            GameplayTriggerCompileResult recovered = Compile(CreateThresholdDecimalDocument("1.000001"), environment);

            AssertDiagnostic(failed, GameplayTriggerConfigDiagnosticCode.InvalidHysteresis);
            Assert.That(recovered.Succeeded, Is.True, Describe(recovered));
            Assert.That(recovered.CompiledCatalog.GetSourceDefinition(0).EnterThreshold.RawValue, Is.EqualTo(1000001L));
        }

        [Test]
        public void Compile_MissingExplicitTargetSlot_IsTypedBeforeRuntimeCatalogValidation()
        {
            GameplayTriggerAuthoringDocument document = CreateDocument(
                action: new GameplayTriggerActionAuthoring(
                    SameFrameActionKind.Value,
                    GameplayTriggerTargetSelector.ExplicitBindingTarget,
                    targetSlotId: 9));

            GameplayTriggerCompileResult result = Compile(document, CreateEnvironment());

            AssertDiagnostic(result, GameplayTriggerConfigDiagnosticCode.MissingExplicitTargetSlot);
            Assert.That(Contains(result, GameplayTriggerConfigDiagnosticCode.RuntimeCatalogRejected), Is.False, Describe(result));
        }

        [Test]
        public void Compile_StatefulPresenceEpochCapability_AllowsOncePerPresence()
        {
            var source = new GameplayTriggerSourceAuthoring(1, GameplayTriggerSourceKind.Occupancy);
            var route = new GameplayTriggerRouteAuthoring(
                1,
                GameplayTriggerOccurrenceKind.Entered,
                1,
                new[] { new GameplayTriggerActionAuthoring(SameFrameActionKind.Value, GameplayTriggerTargetSelector.Owner) },
                presenceLimit: GameplayTriggerPresenceLimit.OncePerPresence);
            var contract = new GameplayTriggerSourceBindingContractDescriptor(
                new GameplayTriggerSourceBindingId(1),
                new GameplayTriggerSourceDefinitionId(1),
                GameplayTriggerSourceKind.Occupancy,
                new GameplayTriggerSourceProviderId(1),
                GameplayTriggerSourceEntityCapability.PresenceEpoch,
                true,
                1,
                1);

            GameplayTriggerCompileResult result = Compile(
                CreateDocument(source: source, route: route),
                CreateEnvironment(sourceContracts: new[] { contract }));

            Assert.That(result.Succeeded, Is.True, Describe(result));
        }

        [Test]
        public void Compile_UnboundedInternalSignalSelfCycle_IsRejected()
        {
            GameplayTriggerAuthoringDocument document = CreateDocument(
                action: new GameplayTriggerActionAuthoring(
                    InternalSignalActionKind.Value,
                    GameplayTriggerTargetSelector.ExplicitBindingTarget,
                    targetSlotId: 1),
                targets: new[]
                {
                    new GameplayTriggerBindingTargetAuthoring(
                        1,
                        GameplayTriggerTargetRef.GameplayTriggerSourceBindingDomainId,
                        1)
                });
            GameplayTriggerCompilerEnvironment environment = CreateEnvironment(
                actionRequirements: new[]
                {
                    new GameplayTriggerActionRequirementDescriptor(
                        InternalSignalActionKind,
                        GameplayTriggerDeliveryContract.InternalDeferred,
                        GameplayTriggerInternalDeferredOperationKind.Signal,
                        signalId: 7)
                });

            GameplayTriggerCompileResult result = Compile(document, environment);

            AssertDiagnostic(result, GameplayTriggerConfigDiagnosticCode.UnboundedSignalCycle);
        }

        [TestCase(GameplayTriggerConditionOperation.Not)]
        [TestCase(GameplayTriggerConditionOperation.All)]
        [TestCase(GameplayTriggerConditionOperation.Any)]
        public void Compile_ComposedAlwaysTrueCondition_RejectsInternalSignalSelfCycle(GameplayTriggerConditionOperation operation)
        {
            GameplayTriggerCompileResult result = Compile(
                CreateInternalSignalSelfCycleDocument(CreateComposedConstantCondition(1, operation)),
                CreateInternalSignalEnvironment());

            AssertDiagnostic(result, GameplayTriggerConfigDiagnosticCode.UnboundedSignalCycle);
        }

        [Test]
        public void Compile_AlwaysFalseAndRuntimeDependentConditions_DoNotCreateSignalCycle()
        {
            var alwaysFalse = new GameplayTriggerConditionAuthoring(
                1,
                new GameplayTriggerConditionNodeAuthoring(
                    GameplayTriggerConditionOperation.Not,
                    children: new[] { new GameplayTriggerConditionNodeAuthoring(GameplayTriggerConditionOperation.PushTrue) }));
            var runtimeDependent = new GameplayTriggerConditionAuthoring(
                1,
                new GameplayTriggerConditionNodeAuthoring(
                    GameplayTriggerConditionOperation.All,
                    children: new[]
                    {
                        new GameplayTriggerConditionNodeAuthoring(GameplayTriggerConditionOperation.PushTrue),
                        new GameplayTriggerConditionNodeAuthoring(
                            GameplayTriggerConditionOperation.OccurrenceMask,
                            operand: (long)GameplayTriggerOccurrenceKind.Pulsed)
                    }));
            GameplayTriggerCompilerEnvironment environment = CreateInternalSignalEnvironment();

            GameplayTriggerCompileResult falseResult = Compile(CreateInternalSignalSelfCycleDocument(alwaysFalse), environment);
            GameplayTriggerCompileResult dynamicResult = Compile(CreateInternalSignalSelfCycleDocument(runtimeDependent), environment);

            Assert.That(falseResult.Succeeded, Is.True, Describe(falseResult));
            Assert.That(dynamicResult.Succeeded, Is.True, Describe(dynamicResult));
            Assert.That(Contains(falseResult, GameplayTriggerConfigDiagnosticCode.UnboundedSignalCycle), Is.False, Describe(falseResult));
            Assert.That(Contains(dynamicResult, GameplayTriggerConfigDiagnosticCode.UnboundedSignalCycle), Is.False, Describe(dynamicResult));
        }

        [Test]
        public void Compile_BoundedInternalSignalSelfCycle_IsAccepted()
        {
            var route = new GameplayTriggerRouteAuthoring(
                1,
                GameplayTriggerOccurrenceKind.Pulsed,
                1,
                new[]
                {
                    new GameplayTriggerActionAuthoring(
                        InternalSignalActionKind.Value,
                        GameplayTriggerTargetSelector.ExplicitBindingTarget,
                        targetSlotId: 1)
                },
                maxActivations: 1);
            GameplayTriggerAuthoringDocument document = CreateDocument(
                route: route,
                targets: new[]
                {
                    new GameplayTriggerBindingTargetAuthoring(
                        1,
                        GameplayTriggerTargetRef.GameplayTriggerSourceBindingDomainId,
                        1)
                });
            GameplayTriggerCompilerEnvironment environment = CreateEnvironment(
                actionRequirements: new[]
                {
                    new GameplayTriggerActionRequirementDescriptor(
                        InternalSignalActionKind,
                        GameplayTriggerDeliveryContract.InternalDeferred,
                        GameplayTriggerInternalDeferredOperationKind.Signal,
                        signalId: 7)
                });

            GameplayTriggerCompileResult result = Compile(document, environment);

            Assert.That(result.Succeeded, Is.True, Describe(result));
            Assert.That(result.CompiledCatalog.CapacityShape.MaxDeferredRecords, Is.EqualTo(1));
        }

        [Test]
        public void Compile_MultiNodeSignalScc_SelectsCanonicalMinimumEdgeAcrossPermutations()
        {
            GameplayTriggerCompilerEnvironment environment = CreateEnvironment(
                sourceContracts: new[]
                {
                    CreateSignalContract(3, 1, 1),
                    CreateSignalContract(1, 1, 1),
                    CreateSignalContract(2, 1, 1)
                },
                actionRequirements: new[]
                {
                    new GameplayTriggerActionRequirementDescriptor(
                        InternalSignalActionKind,
                        GameplayTriggerDeliveryContract.InternalDeferred,
                        GameplayTriggerInternalDeferredOperationKind.Signal,
                        signalId: 7)
                });
            GameplayTriggerConditionAuthoring composedTrue = CreateComposedConstantCondition(1, GameplayTriggerConditionOperation.Any);
            GameplayTriggerAuthoringDocument firstDocument = CreateThreeNodeCycleDocument(false, composedTrue);
            GameplayTriggerAuthoringDocument secondDocument = CreateThreeNodeCycleDocument(true, composedTrue);

            GameplayTriggerCompileResult first = Compile(firstDocument, environment);
            GameplayTriggerCompileResult second = Compile(secondDocument, environment);

            AssertDiagnostic(first, GameplayTriggerConfigDiagnosticCode.UnboundedSignalCycle);
            AssertDiagnostic(second, GameplayTriggerConfigDiagnosticCode.UnboundedSignalCycle);
            GameplayTriggerConfigDiagnostic firstDiagnostic = Find(first, GameplayTriggerConfigDiagnosticCode.UnboundedSignalCycle);
            GameplayTriggerConfigDiagnostic secondDiagnostic = Find(second, GameplayTriggerConfigDiagnosticCode.UnboundedSignalCycle);
            Assert.That(firstDiagnostic.PrimaryId, Is.EqualTo(1));
            Assert.That(firstDiagnostic.SecondaryId, Is.EqualTo(2));
            Assert.That(firstDiagnostic.Path, Is.EqualTo("$.bindings[id=30].definition.routes[id=1].actions[0]"));
            Assert.That(secondDiagnostic.Path, Is.EqualTo(firstDiagnostic.Path));
            Assert.That(secondDiagnostic.PrimaryId, Is.EqualTo(firstDiagnostic.PrimaryId));
            Assert.That(secondDiagnostic.SecondaryId, Is.EqualTo(firstDiagnostic.SecondaryId));
        }

        [Test]
        public void Compile_RepeatingIntervalCycle_IsAccepted()
        {
            var route = new GameplayTriggerRouteAuthoring(
                1,
                GameplayTriggerOccurrenceKind.Pulsed,
                1,
                new[]
                {
                    new GameplayTriggerActionAuthoring(
                        InternalSignalActionKind.Value,
                        GameplayTriggerTargetSelector.ExplicitBindingTarget,
                        targetSlotId: 1)
                },
                cooldownFrames: 2,
                allowRepeatingCycle: true,
                minIntervalFrames: 2);
            GameplayTriggerAuthoringDocument document = CreateDocument(
                route: route,
                targets: new[]
                {
                    new GameplayTriggerBindingTargetAuthoring(
                        1,
                        GameplayTriggerTargetRef.GameplayTriggerSourceBindingDomainId,
                        1)
                });
            GameplayTriggerCompilerEnvironment environment = CreateEnvironment(
                actionRequirements: new[]
                {
                    new GameplayTriggerActionRequirementDescriptor(
                        InternalSignalActionKind,
                        GameplayTriggerDeliveryContract.InternalDeferred,
                        GameplayTriggerInternalDeferredOperationKind.Signal,
                        signalId: 7)
                });

            GameplayTriggerCompileResult result = Compile(document, environment);

            Assert.That(result.Succeeded, Is.True, Describe(result));
        }

        [Test]
        public void Compile_SemanticActionOrderChangesCatalogHash()
        {
            GameplayTriggerActionKindId other = new GameplayTriggerActionKindId(1010);
            GameplayTriggerCompilerEnvironment environment = CreateEnvironment(
                actionRequirements: new[]
                {
                    new GameplayTriggerActionRequirementDescriptor(SameFrameActionKind, GameplayTriggerDeliveryContract.SameFrameReserved),
                    new GameplayTriggerActionRequirementDescriptor(other, GameplayTriggerDeliveryContract.SameFrameReserved)
                });
            GameplayTriggerAuthoringDocument firstDocument = CreateDocument(
                route: new GameplayTriggerRouteAuthoring(
                    1,
                    GameplayTriggerOccurrenceKind.Pulsed,
                    1,
                    new[]
                    {
                        new GameplayTriggerActionAuthoring(SameFrameActionKind.Value, GameplayTriggerTargetSelector.Owner),
                        new GameplayTriggerActionAuthoring(other.Value, GameplayTriggerTargetSelector.Owner)
                    }));
            GameplayTriggerAuthoringDocument secondDocument = CreateDocument(
                route: new GameplayTriggerRouteAuthoring(
                    1,
                    GameplayTriggerOccurrenceKind.Pulsed,
                    1,
                    new[]
                    {
                        new GameplayTriggerActionAuthoring(other.Value, GameplayTriggerTargetSelector.Owner),
                        new GameplayTriggerActionAuthoring(SameFrameActionKind.Value, GameplayTriggerTargetSelector.Owner)
                    }));

            GameplayTriggerCompileResult first = Compile(firstDocument, environment);
            GameplayTriggerCompileResult second = Compile(secondDocument, environment);

            Assert.That(first.Succeeded, Is.True, Describe(first));
            Assert.That(second.Succeeded, Is.True, Describe(second));
            Assert.That(second.CompiledCatalog.GameplayTriggerCatalogHash, Is.Not.EqualTo(first.CompiledCatalog.GameplayTriggerCatalogHash));
        }

        [Test]
        public void Bootstrap_ExactProfileAndSink_CreateHashIdenticalCatalog()
        {
            GameplayTriggerCompileResult compile = Compile(CreateDocument(), CreateEnvironment());
            Assert.That(compile.Succeeded, Is.True, Describe(compile));
            GameplayTriggerRuntimeCapacityProfile profile = ExactProfile(compile.CompiledCatalog.CapacityRequirements);

            GameplayTriggerCatalogBootstrapResult bootstrap = GameplayTriggerCatalogBootstrap.TryCreateCatalog(
                compile.CompiledCatalog,
                new[] { GameplayTriggerActionBootstrapBinding.FromSink(new FakeSink(SameFrameActionKind, GameplayTriggerDeliveryContract.SameFrameReserved)) },
                profile);

            Assert.That(bootstrap.Succeeded, Is.True, Describe(bootstrap));
            Assert.That(bootstrap.Catalog.GameplayTriggerCatalogHash, Is.EqualTo(compile.CompiledCatalog.GameplayTriggerCatalogHash));
            Assert.That(bootstrap.RuntimeOptions.HasValue, Is.True);
            Assert.That(bootstrap.CompositionRequirements.SourceRequirementCount, Is.EqualTo(1));
            Assert.That(bootstrap.CompositionRequirements.ReliableActionCount, Is.Zero);
        }

        [Test]
        public void Bootstrap_UndersizedProfileAndDeliveryMismatch_AreTypedFailures()
        {
            GameplayTriggerCompileResult compile = Compile(CreateDocument(), CreateEnvironment());
            Assert.That(compile.Succeeded, Is.True, Describe(compile));
            GameplayTriggerRuntimeCapacityRequirements required = compile.CompiledCatalog.CapacityRequirements;
            GameplayTriggerRuntimeCapacityProfile undersized = new GameplayTriggerRuntimeCapacityProfile(
                required.RequiredInstances,
                required.MaxInputBatches,
                required.MaxInputItems,
                required.MaxRelations,
                required.MaxOccurrences,
                required.MaxCandidates,
                required.MaxRouteStates,
                required.MaxActivations,
                required.MaxIntents,
                required.MaxPreparedActions - 1,
                required.MaxConditionStackDepth,
                required.MaxDeferredRecords,
                required.MaxReliableOutboxRecords);

            GameplayTriggerCatalogBootstrapResult result = GameplayTriggerCatalogBootstrap.TryCreateCatalog(
                compile.CompiledCatalog,
                new[] { GameplayTriggerActionBootstrapBinding.FromSink(new FakeSink(SameFrameActionKind, GameplayTriggerDeliveryContract.ReliableDeferred)) },
                undersized);

            Assert.That(result.Succeeded, Is.False);
            Assert.That(Contains(result, GameplayTriggerConfigDiagnosticCode.RuntimeProfileUndersized), Is.True, Describe(result));
            Assert.That(Contains(result, GameplayTriggerConfigDiagnosticCode.BootstrapDeliveryMismatch), Is.True, Describe(result));
        }

        [Test]
        public void Bootstrap_CapacityBoundary_NMinusOneRejects_NAndNPlusOneAccept()
        {
            GameplayTriggerCompileResult compile = Compile(CreateDocument(), CreateEnvironment());
            Assert.That(compile.Succeeded, Is.True, Describe(compile));
            GameplayTriggerRuntimeCapacityRequirements required = compile.CompiledCatalog.CapacityRequirements;
            GameplayTriggerRuntimeCapacityProfile atLimit = ExactProfile(required);
            GameplayTriggerRuntimeCapacityProfile aboveLimit = ProfileWithPrepared(required, required.MaxPreparedActions + 1);
            GameplayTriggerRuntimeCapacityProfile belowLimit = ProfileWithPrepared(required, required.MaxPreparedActions - 1);
            GameplayTriggerActionBootstrapBinding[] actions =
            {
                GameplayTriggerActionBootstrapBinding.FromSink(new FakeSink(SameFrameActionKind, GameplayTriggerDeliveryContract.SameFrameReserved))
            };

            GameplayTriggerCatalogBootstrapResult below = GameplayTriggerCatalogBootstrap.TryCreateCatalog(compile.CompiledCatalog, actions, belowLimit);
            GameplayTriggerCatalogBootstrapResult exact = GameplayTriggerCatalogBootstrap.TryCreateCatalog(compile.CompiledCatalog, actions, atLimit);
            GameplayTriggerCatalogBootstrapResult above = GameplayTriggerCatalogBootstrap.TryCreateCatalog(compile.CompiledCatalog, actions, aboveLimit);

            Assert.That(below.Succeeded, Is.False);
            Assert.That(Contains(below, GameplayTriggerConfigDiagnosticCode.RuntimeProfileUndersized), Is.True, Describe(below));
            Assert.That(exact.Succeeded, Is.True, Describe(exact));
            Assert.That(above.Succeeded, Is.True, Describe(above));
        }

        [TestCaseSource(nameof(CapacityFields))]
        public void Bootstrap_EachCapacityField_HasNMinusOneNAndNPlusOneBoundary(string field)
        {
            GameplayTriggerCompileResult compile = Compile(CreateDocument(), CreateEnvironment());
            Assert.That(compile.Succeeded, Is.True, Describe(compile));
            GameplayTriggerRuntimeCapacityRequirements required = compile.CompiledCatalog.CapacityRequirements;
            GameplayTriggerActionBootstrapBinding[] actions =
            {
                GameplayTriggerActionBootstrapBinding.FromSink(new FakeSink(SameFrameActionKind, GameplayTriggerDeliveryContract.SameFrameReserved))
            };

            GameplayTriggerCatalogBootstrapResult below = GameplayTriggerCatalogBootstrap.TryCreateCatalog(
                compile.CompiledCatalog,
                actions,
                ProfileWithDelta(required, field, -1));
            GameplayTriggerCatalogBootstrapResult exact = GameplayTriggerCatalogBootstrap.TryCreateCatalog(
                compile.CompiledCatalog,
                actions,
                ProfileWithDelta(required, field, 0));
            GameplayTriggerCatalogBootstrapResult above = GameplayTriggerCatalogBootstrap.TryCreateCatalog(
                compile.CompiledCatalog,
                actions,
                ProfileWithDelta(required, field, 1));

            Assert.That(below.Succeeded, Is.False, field);
            Assert.That(Contains(below, GameplayTriggerConfigDiagnosticCode.RuntimeProfileUndersized), Is.True, field + ": " + Describe(below));
            Assert.That(exact.Succeeded, Is.True, field + ": " + Describe(exact));
            Assert.That(above.Succeeded, Is.True, field + ": " + Describe(above));
        }

        [Test]
        public void Bootstrap_MissingDuplicateAndInternalDescriptorMismatch_AreTyped()
        {
            GameplayTriggerCompileResult externalCompile = Compile(CreateDocument(), CreateEnvironment());
            Assert.That(externalCompile.Succeeded, Is.True, Describe(externalCompile));
            GameplayTriggerRuntimeCapacityProfile externalProfile = ExactProfile(externalCompile.CompiledCatalog.CapacityRequirements);
            GameplayTriggerCatalogBootstrapResult missing = GameplayTriggerCatalogBootstrap.TryCreateCatalog(
                externalCompile.CompiledCatalog,
                Array.Empty<GameplayTriggerActionBootstrapBinding>(),
                externalProfile);
            GameplayTriggerActionBootstrapBinding actual = GameplayTriggerActionBootstrapBinding.FromSink(
                new FakeSink(SameFrameActionKind, GameplayTriggerDeliveryContract.SameFrameReserved));
            GameplayTriggerCatalogBootstrapResult duplicate = GameplayTriggerCatalogBootstrap.TryCreateCatalog(
                externalCompile.CompiledCatalog,
                new[] { actual, actual },
                externalProfile);

            GameplayTriggerCompileResult internalCompile = Compile(
                CreateDocument(
                    route: new GameplayTriggerRouteAuthoring(
                        1,
                        GameplayTriggerOccurrenceKind.Pulsed,
                        1,
                        new[]
                        {
                            new GameplayTriggerActionAuthoring(
                                InternalSignalActionKind.Value,
                                GameplayTriggerTargetSelector.ExplicitBindingTarget,
                                targetSlotId: 1)
                        },
                        maxActivations: 1),
                    targets: new[]
                    {
                        new GameplayTriggerBindingTargetAuthoring(
                            1,
                            GameplayTriggerTargetRef.GameplayTriggerSourceBindingDomainId,
                            1)
                    }),
                CreateEnvironment(
                    actionRequirements: new[]
                    {
                        new GameplayTriggerActionRequirementDescriptor(
                            InternalSignalActionKind,
                            GameplayTriggerDeliveryContract.InternalDeferred,
                            GameplayTriggerInternalDeferredOperationKind.Signal,
                            signalId: 7)
                    }));
            Assert.That(internalCompile.Succeeded, Is.True, Describe(internalCompile));
            GameplayTriggerCatalogBootstrapResult internalMismatch = GameplayTriggerCatalogBootstrap.TryCreateCatalog(
                internalCompile.CompiledCatalog,
                new[]
                {
                    GameplayTriggerActionBootstrapBinding.FromInternal(
                        new GameplayTriggerInternalDeferredActionDescriptor(
                            InternalSignalActionKind,
                            GameplayTriggerInternalDeferredOperationKind.Reset))
                },
                ExactProfile(internalCompile.CompiledCatalog.CapacityRequirements));

            Assert.That(Contains(missing, GameplayTriggerConfigDiagnosticCode.BootstrapActionMissing), Is.True, Describe(missing));
            Assert.That(Contains(duplicate, GameplayTriggerConfigDiagnosticCode.BootstrapActionDuplicate), Is.True, Describe(duplicate));
            Assert.That(Contains(internalMismatch, GameplayTriggerConfigDiagnosticCode.InternalDescriptorMismatch), Is.True, Describe(internalMismatch));
        }

        [Test]
        public void Compile_FailureDoesNotPolluteFollowingValidCompile()
        {
            GameplayTriggerCompilerEnvironment environment = CreateEnvironment();
            GameplayTriggerAuthoringDocument invalid = CreateDocument(
                condition: new GameplayTriggerConditionAuthoring(1, null));

            GameplayTriggerCompileResult failed = Compile(invalid, environment);
            GameplayTriggerCompileResult recovered = Compile(CreateDocument(), environment);

            AssertDiagnostic(failed, GameplayTriggerConfigDiagnosticCode.InvalidConditionNode);
            Assert.That(recovered.Succeeded, Is.True, Describe(recovered));
        }

        [TestCase("null-document", GameplayTriggerConfigDiagnosticCode.NullDocument, "$")]
        [TestCase("authoring-schema", GameplayTriggerConfigDiagnosticCode.AuthoringSchemaMismatch, "$.schemaId")]
        [TestCase("authoring-version", GameplayTriggerConfigDiagnosticCode.AuthoringVersionMismatch, "$.schemaVersion")]
        [TestCase("runtime-schema", GameplayTriggerConfigDiagnosticCode.RuntimeCatalogSchemaMismatch, "expectedCatalogIdentity.schemaId")]
        [TestCase("runtime-version", GameplayTriggerConfigDiagnosticCode.RuntimeCatalogVersionMismatch, "expectedCatalogIdentity.schemaVersion")]
        [TestCase("null-row", GameplayTriggerConfigDiagnosticCode.NullRow, "$.sources[null]")]
        [TestCase("duplicate-source", GameplayTriggerConfigDiagnosticCode.DuplicateSourceId, "$.sources[id=1]")]
        [TestCase("duplicate-condition", GameplayTriggerConfigDiagnosticCode.DuplicateConditionId, "$.conditions[id=1]")]
        [TestCase("duplicate-definition", GameplayTriggerConfigDiagnosticCode.DuplicateDefinitionId, "$.definitions[id=1]")]
        [TestCase("duplicate-route", GameplayTriggerConfigDiagnosticCode.DuplicateRouteId, ".routes[id=1]")]
        [TestCase("duplicate-binding", GameplayTriggerConfigDiagnosticCode.DuplicateBindingId, "$.bindings[id=1]")]
        [TestCase("duplicate-action-registry", GameplayTriggerConfigDiagnosticCode.DuplicateActionKind, "actionRequirements[kind=1001]")]
        [TestCase("missing-source", GameplayTriggerConfigDiagnosticCode.MissingSourceReference, ".sourceDefinitionId")]
        [TestCase("missing-condition", GameplayTriggerConfigDiagnosticCode.MissingConditionReference, ".conditionId")]
        [TestCase("missing-definition", GameplayTriggerConfigDiagnosticCode.MissingDefinitionReference, ".definitionId")]
        [TestCase("invalid-source-kind", GameplayTriggerConfigDiagnosticCode.InvalidSourceKind, ".sourceKind")]
        [TestCase("invalid-signal-ordering", GameplayTriggerConfigDiagnosticCode.InvalidSignalOrdering, ".signalOrdering")]
        [TestCase("invalid-threshold-direction", GameplayTriggerConfigDiagnosticCode.InvalidThresholdDirection, ".thresholdDirection")]
        [TestCase("invalid-initial-policy", GameplayTriggerConfigDiagnosticCode.InvalidInitialSamplePolicy, ".initialSamplePolicy")]
        [TestCase("invalid-departure-policy", GameplayTriggerConfigDiagnosticCode.InvalidDeparturePolicy, ".departurePolicy")]
        [TestCase("invalid-binding-capacity", GameplayTriggerConfigDiagnosticCode.InvalidBindingCapacity, ".maxStateKeys")]
        [TestCase("missing-source-contract", GameplayTriggerConfigDiagnosticCode.MissingSourceBindingContract, "sourceBindings[id=1]")]
        [TestCase("source-definition-mismatch", GameplayTriggerConfigDiagnosticCode.SourceDefinitionContractMismatch, ".sourceDefinitionId")]
        [TestCase("source-kind-mismatch", GameplayTriggerConfigDiagnosticCode.SourceKindContractMismatch, ".sourceKind")]
        [TestCase("source-binding-zero", GameplayTriggerConfigDiagnosticCode.InvalidId, ".sourceBindingId")]
        [TestCase("source-definition-zero", GameplayTriggerConfigDiagnosticCode.InvalidId, ".sourceDefinitionId")]
        [TestCase("source-provider-missing", GameplayTriggerConfigDiagnosticCode.SourceProviderMissing, ".provider")]
        [TestCase("source-capability-invalid", GameplayTriggerConfigDiagnosticCode.SourceCapabilityMismatch, ".entityCapabilities")]
        [TestCase("source-evaluation-invalid", GameplayTriggerConfigDiagnosticCode.SourceEvaluationContractInvalid, ".evaluation")]
        [TestCase("empty-occurrence", GameplayTriggerConfigDiagnosticCode.EmptyOccurrenceMask, ".occurrenceMask")]
        [TestCase("invalid-occurrence", GameplayTriggerConfigDiagnosticCode.InvalidOccurrenceMask, ".occurrenceMask")]
        [TestCase("invalid-state-scope", GameplayTriggerConfigDiagnosticCode.InvalidStateScope, ".stateScope")]
        [TestCase("negative-activations", GameplayTriggerConfigDiagnosticCode.NegativeMaxActivations, ".maxActivations")]
        [TestCase("negative-cooldown", GameplayTriggerConfigDiagnosticCode.NegativeCooldown, ".cooldownFrames")]
        [TestCase("invalid-presence", GameplayTriggerConfigDiagnosticCode.InvalidPresenceLimit, ".presenceLimit")]
        [TestCase("negative-stay", GameplayTriggerConfigDiagnosticCode.NegativeStayInterval, ".stayIntervalFrames")]
        [TestCase("invalid-exclusive", GameplayTriggerConfigDiagnosticCode.InvalidExclusiveGroup, ".exclusiveGroupId")]
        [TestCase("invalid-arbitration", GameplayTriggerConfigDiagnosticCode.InvalidArbitrationScope, ".arbitrationScope")]
        [TestCase("once-presence", GameplayTriggerConfigDiagnosticCode.OncePerPresenceUnsupported, ".entityCapabilities")]
        [TestCase("missing-subject", GameplayTriggerConfigDiagnosticCode.MissingSubjectCapability, ".entityCapabilities")]
        [TestCase("missing-instigator", GameplayTriggerConfigDiagnosticCode.MissingInstigatorCapability, ".entityCapabilities")]
        [TestCase("exclusive-scope", GameplayTriggerConfigDiagnosticCode.ExclusiveScopeMismatch, "$.exclusiveGroups[id=1]")]
        [TestCase("route-count", GameplayTriggerConfigDiagnosticCode.RouteCountExceeded, ".routes")]
        [TestCase("action-count", GameplayTriggerConfigDiagnosticCode.ActionCountExceeded, ".actions")]
        [TestCase("condition-arity", GameplayTriggerConfigDiagnosticCode.InvalidConditionArity, ".children")]
        [TestCase("condition-operand", GameplayTriggerConfigDiagnosticCode.InvalidConditionOperand, ".operand")]
        [TestCase("condition-comparison", GameplayTriggerConfigDiagnosticCode.InvalidComparison, ".comparison")]
        [TestCase("condition-depth", GameplayTriggerConfigDiagnosticCode.ConditionDepthExceeded, ".root")]
        [TestCase("condition-stack", GameplayTriggerConfigDiagnosticCode.ConditionStackExceeded, ".root")]
        [TestCase("invalid-target-selector", GameplayTriggerConfigDiagnosticCode.InvalidTargetSelector, ".targetSelector")]
        [TestCase("duplicate-target-slot", GameplayTriggerConfigDiagnosticCode.DuplicateExplicitTargetSlot, ".explicitTargets[slot=1]")]
        [TestCase("unknown-action", GameplayTriggerConfigDiagnosticCode.UnknownActionContract, ".actionKindId")]
        [TestCase("action-kind-zero", GameplayTriggerConfigDiagnosticCode.InvalidId, ".actionKindId")]
        [TestCase("invalid-action-registry", GameplayTriggerConfigDiagnosticCode.InvalidActionContract, ".internalDescriptor")]
        [TestCase("invalid-options", GameplayTriggerConfigDiagnosticCode.InvalidCompilerOption, "$.compilerOptions.maxConditionTreeDepth")]
        [TestCase("capacity-overflow", GameplayTriggerConfigDiagnosticCode.CapacityArithmeticOverflow, "$.capacityRequirements")]
        [TestCase("profile-shape", GameplayTriggerConfigDiagnosticCode.RuntimeProfileShapeInvalid, "$.runtimeProfile")]
        [TestCase("invalid-environment", GameplayTriggerConfigDiagnosticCode.RegistryInvalid, "$.environment")]
        public void DiagnosticMatrix_ReachableFailuresHaveTypedCodeAndStablePath(string scenario, GameplayTriggerConfigDiagnosticCode expectedCode, string expectedPath)
        {
            GameplayTriggerConfigDiagnostic diagnostic = CreateMatrixDiagnostic(scenario, expectedCode);

            Assert.That(diagnostic.Code, Is.EqualTo(expectedCode), scenario);
            Assert.That(diagnostic.Path, Does.Contain(expectedPath), scenario);
        }

        [Test]
        public void ReservedInvariantDiagnosticCodes_RemainNumericallyFrozen()
        {
            Assert.That((int)GameplayTriggerConfigDiagnosticCode.InvalidPostfixStack, Is.EqualTo(408));
            Assert.That((int)GameplayTriggerConfigDiagnosticCode.SameFrameReservationCapabilityMissing, Is.EqualTo(506));
            Assert.That((int)GameplayTriggerConfigDiagnosticCode.ZeroRequiredCapacity, Is.EqualTo(602));
        }

        [Test]
        public void Compile_MultipleInvalidOptions_ReturnCanonicalFieldPaths()
        {
            var invalid = new GameplayTriggerCompilerOptions(0, 0, 0, 0, 0, 0);

            GameplayTriggerCompileResult first = GameplayTriggerConfigCompiler.Compile(CreateDocument(), invalid, CreateEnvironment());
            GameplayTriggerCompileResult second = GameplayTriggerConfigCompiler.Compile(CreateDocument(), invalid, CreateEnvironment());

            string[] expected =
            {
                "$.compilerOptions.maxActionsPerRoute",
                "$.compilerOptions.maxConditionInstructions",
                "$.compilerOptions.maxConditionStackDepth",
                "$.compilerOptions.maxConditionTreeDepth",
                "$.compilerOptions.maxRoutesPerDefinition",
                "$.compilerOptions.maxTotalRows"
            };
            Assert.That(first.DiagnosticCount, Is.EqualTo(expected.Length));
            Assert.That(second.DiagnosticCount, Is.EqualTo(expected.Length));
            for (int i = 0; i < expected.Length; i++)
            {
                Assert.That(first.GetDiagnostic(i).Code, Is.EqualTo(GameplayTriggerConfigDiagnosticCode.InvalidCompilerOption));
                Assert.That(first.GetDiagnostic(i).Path, Is.EqualTo(expected[i]));
                Assert.That(second.GetDiagnostic(i).Path, Is.EqualTo(expected[i]));
            }
        }

        [Test]
        public void Compile_ReliableActionProducesOnlyReliableCapacity()
        {
            GameplayTriggerAuthoringDocument document = CreateDocument(
                action: new GameplayTriggerActionAuthoring(ReliableActionKind.Value, GameplayTriggerTargetSelector.Owner));
            GameplayTriggerCompilerEnvironment environment = CreateEnvironment(
                actionRequirements: new[]
                {
                    new GameplayTriggerActionRequirementDescriptor(ReliableActionKind, GameplayTriggerDeliveryContract.ReliableDeferred)
                });

            GameplayTriggerCompileResult result = Compile(document, environment);

            Assert.That(result.Succeeded, Is.True, Describe(result));
            Assert.That(result.CompiledCatalog.CapacityShape.MaxDeferredRecords, Is.Zero);
            Assert.That(result.CompiledCatalog.CapacityShape.MaxReliableOutboxRecords, Is.EqualTo(1));
        }

        [Test]
        public void Compile_UnusedGlobalActionRequirements_DoNotAffectPlanHashOrCapacity()
        {
            GameplayTriggerCompileResult baseline = Compile(CreateDocument(), CreateEnvironment());
            GameplayTriggerCompilerEnvironment expandedEnvironment = CreateEnvironment(
                actionRequirements: new[]
                {
                    new GameplayTriggerActionRequirementDescriptor(SameFrameActionKind, GameplayTriggerDeliveryContract.SameFrameReserved),
                    new GameplayTriggerActionRequirementDescriptor(ReliableActionKind, GameplayTriggerDeliveryContract.ReliableDeferred),
                    new GameplayTriggerActionRequirementDescriptor(
                        InternalSignalActionKind,
                        GameplayTriggerDeliveryContract.InternalDeferred,
                        GameplayTriggerInternalDeferredOperationKind.Reset)
                });

            GameplayTriggerCompileResult expanded = Compile(CreateDocument(), expandedEnvironment);

            Assert.That(baseline.Succeeded, Is.True, Describe(baseline));
            Assert.That(expanded.Succeeded, Is.True, Describe(expanded));
            Assert.That(expanded.CompiledCatalog.ActionRequirementCount, Is.EqualTo(1));
            Assert.That(expanded.CompiledCatalog.GameplayTriggerCatalogHash, Is.EqualTo(baseline.CompiledCatalog.GameplayTriggerCatalogHash));
            Assert.That(expanded.CompiledCatalog.CapacityShape.MaxDeferredRecords, Is.Zero);
            Assert.That(expanded.CompiledCatalog.CapacityShape.MaxReliableOutboxRecords, Is.Zero);
            Assert.That(expanded.CompiledCatalog.CapacityRequirements.MaxDeferredRecords, Is.Zero);
            Assert.That(expanded.CompiledCatalog.CapacityRequirements.MaxReliableOutboxRecords, Is.Zero);
            GameplayTriggerCatalogBootstrapResult bootstrap = GameplayTriggerCatalogBootstrap.TryCreateCatalog(
                expanded.CompiledCatalog,
                new[] { GameplayTriggerActionBootstrapBinding.FromSink(new FakeSink(SameFrameActionKind, GameplayTriggerDeliveryContract.SameFrameReserved)) },
                ExactProfile(expanded.CompiledCatalog.CapacityRequirements));
            Assert.That(bootstrap.Succeeded, Is.True, Describe(bootstrap));
            Assert.DoesNotThrow(() => new GameplayTriggerRuntime(bootstrap.Catalog, bootstrap.RuntimeOptions.Value));
        }

        [Test]
        public void Compile_UnboundDefinitionDelivery_IsProjectedAndClampedForRuntimeConstruction()
        {
            var document = new GameplayTriggerAuthoringDocument(
                GameplayTriggerConfigSchemas.AuthoringId,
                GameplayTriggerConfigSchemas.AuthoringVersion,
                new[] { CreateSignalSource(1) },
                new[] { TrueCondition(1) },
                new[]
                {
                    CreateDefinition(1, 1, 1, 1),
                    new GameplayTriggerDefinitionAuthoring(
                        2,
                        1,
                        1,
                        new[]
                        {
                            new GameplayTriggerRouteAuthoring(
                                2,
                                GameplayTriggerOccurrenceKind.Pulsed,
                                1,
                                new[] { new GameplayTriggerActionAuthoring(ReliableActionKind.Value, GameplayTriggerTargetSelector.Owner) })
                        })
                },
                new[] { new GameplayTriggerBindingAuthoring(1, 1, 1, 1, 1) });
            GameplayTriggerCompilerEnvironment environment = CreateEnvironment(
                actionRequirements: new[]
                {
                    new GameplayTriggerActionRequirementDescriptor(SameFrameActionKind, GameplayTriggerDeliveryContract.SameFrameReserved),
                    new GameplayTriggerActionRequirementDescriptor(ReliableActionKind, GameplayTriggerDeliveryContract.ReliableDeferred)
                });

            GameplayTriggerCompileResult compile = Compile(document, environment);

            Assert.That(compile.Succeeded, Is.True, Describe(compile));
            Assert.That(compile.CompiledCatalog.ActionRequirementCount, Is.EqualTo(2));
            Assert.That(compile.CompiledCatalog.CapacityShape.MaxReliableOutboxRecords, Is.Zero);
            Assert.That(compile.CompiledCatalog.CapacityRequirements.MaxReliableOutboxRecords, Is.EqualTo(1));
            GameplayTriggerCatalogBootstrapResult bootstrap = GameplayTriggerCatalogBootstrap.TryCreateCatalog(
                compile.CompiledCatalog,
                new[]
                {
                    GameplayTriggerActionBootstrapBinding.FromSink(new FakeSink(SameFrameActionKind, GameplayTriggerDeliveryContract.SameFrameReserved)),
                    GameplayTriggerActionBootstrapBinding.FromSink(new FakeSink(ReliableActionKind, GameplayTriggerDeliveryContract.ReliableDeferred))
                },
                ExactProfile(compile.CompiledCatalog.CapacityRequirements));
            Assert.That(bootstrap.Succeeded, Is.True, Describe(bootstrap));
            Assert.DoesNotThrow(() => new GameplayTriggerRuntime(bootstrap.Catalog, bootstrap.RuntimeOptions.Value));
        }

        [Test]
        public void Compile_InternalSignalTargetMustBeInstantiatedInCompiledBindings()
        {
            GameplayTriggerAuthoringDocument document = CreateDocument(
                action: new GameplayTriggerActionAuthoring(
                    InternalSignalActionKind.Value,
                    GameplayTriggerTargetSelector.ExplicitBindingTarget,
                    targetSlotId: 2),
                targets: new[]
                {
                    new GameplayTriggerBindingTargetAuthoring(
                        2,
                        GameplayTriggerTargetRef.GameplayTriggerSourceBindingDomainId,
                        2)
                });
            GameplayTriggerCompilerEnvironment environment = CreateEnvironment(
                sourceContracts: new[]
                {
                    CreateSignalContract(1, 1, 1),
                    CreateSignalContract(2, 1, 1)
                },
                actionRequirements: new[]
                {
                    new GameplayTriggerActionRequirementDescriptor(
                        InternalSignalActionKind,
                        GameplayTriggerDeliveryContract.InternalDeferred,
                        GameplayTriggerInternalDeferredOperationKind.Signal,
                        signalId: 7)
                });

            GameplayTriggerCompileResult result = Compile(document, environment);

            AssertDiagnostic(result, GameplayTriggerConfigDiagnosticCode.InternalDescriptorMismatch);
            Assert.That(Contains(result, GameplayTriggerConfigDiagnosticCode.RuntimeCatalogRejected), Is.False, Describe(result));
        }

        [TestCase(GameplayTriggerInternalDeferredOperationKind.SetEnabled, 2L, 0L, 0L)]
        [TestCase(GameplayTriggerInternalDeferredOperationKind.SetEnabled, 1L, 1L, 0L)]
        [TestCase(GameplayTriggerInternalDeferredOperationKind.Reset, 1L, 0L, 0L)]
        public void Compile_InternalPayloadContract_IsTypedBeforeRuntimeCatalog(
            GameplayTriggerInternalDeferredOperationKind operation,
            long payload0,
            long payload1,
            long payload2)
        {
            var actionKindId = new GameplayTriggerActionKindId(1020 + (int)operation);
            GameplayTriggerAuthoringDocument document = CreateDocument(
                action: new GameplayTriggerActionAuthoring(
                    actionKindId.Value,
                    GameplayTriggerTargetSelector.ExplicitBindingTarget,
                    targetSlotId: 1,
                    payload0: payload0,
                    payload1: payload1,
                    payload2: payload2),
                targets: new[]
                {
                    new GameplayTriggerBindingTargetAuthoring(
                        1,
                        GameplayTriggerTargetRef.GameplayTriggerSourceBindingDomainId,
                        1)
                });
            GameplayTriggerCompilerEnvironment environment = CreateEnvironment(
                actionRequirements: new[]
                {
                    new GameplayTriggerActionRequirementDescriptor(
                        actionKindId,
                        GameplayTriggerDeliveryContract.InternalDeferred,
                        operation)
                });

            GameplayTriggerCompileResult result = Compile(document, environment);

            AssertDiagnostic(result, GameplayTriggerConfigDiagnosticCode.InternalDescriptorMismatch);
            Assert.That(Contains(result, GameplayTriggerConfigDiagnosticCode.RuntimeCatalogRejected), Is.False, Describe(result));
        }

        [Test]
        public void Compile_InternalResetMustUseActualDomainTwoTarget()
        {
            var actionKindId = new GameplayTriggerActionKindId(1030);
            GameplayTriggerAuthoringDocument document = CreateDocument(
                action: new GameplayTriggerActionAuthoring(actionKindId.Value, GameplayTriggerTargetSelector.Owner));
            GameplayTriggerCompilerEnvironment environment = CreateEnvironment(
                actionRequirements: new[]
                {
                    new GameplayTriggerActionRequirementDescriptor(
                        actionKindId,
                        GameplayTriggerDeliveryContract.InternalDeferred,
                        GameplayTriggerInternalDeferredOperationKind.Reset)
                });

            GameplayTriggerCompileResult result = Compile(document, environment);

            AssertDiagnostic(result, GameplayTriggerConfigDiagnosticCode.InternalDescriptorMismatch);
        }

        [Test]
        public void Compile_OwnerSelectorRequiresValidBindingOwner()
        {
            GameplayTriggerAuthoringDocument document = CreateDocumentWithBindings(
                new[] { new GameplayTriggerBindingAuthoring(1, 1, 1) });

            GameplayTriggerCompileResult result = Compile(document, CreateEnvironment());

            AssertDiagnostic(result, GameplayTriggerConfigDiagnosticCode.InvalidTargetReference);
            Assert.That(Contains(result, GameplayTriggerConfigDiagnosticCode.RuntimeCatalogRejected), Is.False, Describe(result));
        }

        [Test]
        public void Compile_SignalAuthoringAndContractOrderingMustMatch()
        {
            var source = new GameplayTriggerSourceAuthoring(
                1,
                GameplayTriggerSourceKind.Signal,
                signalOrdering: GameplayTriggerSignalOrdering.Ordered);
            GameplayTriggerSourceBindingContractDescriptor contract = CreateSignalContract(1, 1, 1);

            GameplayTriggerCompileResult result = Compile(
                CreateDocument(source: source),
                CreateEnvironment(sourceContracts: new[] { contract }));

            AssertDiagnostic(result, GameplayTriggerConfigDiagnosticCode.InvalidSignalReplayMetadata);
        }

        [Test]
        public void Compile_NonSignalAuthoringMustUseCanonicalReplaySentinel()
        {
            var source = new GameplayTriggerSourceAuthoring(
                1,
                GameplayTriggerSourceKind.Occupancy,
                signalOrdering: GameplayTriggerSignalOrdering.Ordered);

            GameplayTriggerCompileResult result = Compile(
                CreateDocument(source: source),
                CreateEnvironment(sourceContracts: new[] { CreateStatefulContract(1, 1, GameplayTriggerSourceKind.Occupancy) }));

            AssertDiagnostic(result, GameplayTriggerConfigDiagnosticCode.InvalidSignalReplayMetadata);
        }

        private static GameplayTriggerConfigDiagnostic CreateMatrixDiagnostic(string scenario, GameplayTriggerConfigDiagnosticCode expectedCode)
        {
            GameplayTriggerCompileResult compile;
            GameplayTriggerAuthoringDocument baseline = CreateDocument();
            switch (scenario)
            {
                case "null-document":
                    compile = GameplayTriggerConfigCompiler.Compile(null, GameplayTriggerCompilerOptions.Default, CreateEnvironment());
                    break;
                case "authoring-schema":
                    compile = Compile(new GameplayTriggerAuthoringDocument("wrong", GameplayTriggerConfigSchemas.AuthoringVersion, baseline.Sources, baseline.Conditions, baseline.Definitions, baseline.Bindings), CreateEnvironment());
                    break;
                case "authoring-version":
                    compile = Compile(new GameplayTriggerAuthoringDocument(GameplayTriggerConfigSchemas.AuthoringId, 2, baseline.Sources, baseline.Conditions, baseline.Definitions, baseline.Bindings), CreateEnvironment());
                    break;
                case "runtime-schema":
                    return InvalidEnvironmentDiagnostic("wrong", GameplayTriggerConfigSchemas.RuntimeCatalogVersion, expectedCode);
                case "runtime-version":
                    return InvalidEnvironmentDiagnostic(GameplayTriggerConfigSchemas.RuntimeCatalogId, 2, expectedCode);
                case "null-row":
                    compile = Compile(new GameplayTriggerAuthoringDocument(GameplayTriggerConfigSchemas.AuthoringId, GameplayTriggerConfigSchemas.AuthoringVersion, new GameplayTriggerSourceAuthoring[] { null }, baseline.Conditions, baseline.Definitions, baseline.Bindings), CreateEnvironment());
                    break;
                case "duplicate-source":
                    compile = Compile(new GameplayTriggerAuthoringDocument(GameplayTriggerConfigSchemas.AuthoringId, GameplayTriggerConfigSchemas.AuthoringVersion, new[] { baseline.Sources[0], baseline.Sources[0] }, baseline.Conditions, baseline.Definitions, baseline.Bindings), CreateEnvironment());
                    break;
                case "duplicate-condition":
                    compile = Compile(new GameplayTriggerAuthoringDocument(GameplayTriggerConfigSchemas.AuthoringId, GameplayTriggerConfigSchemas.AuthoringVersion, baseline.Sources, new[] { baseline.Conditions[0], baseline.Conditions[0] }, baseline.Definitions, baseline.Bindings), CreateEnvironment());
                    break;
                case "duplicate-definition":
                    compile = Compile(new GameplayTriggerAuthoringDocument(GameplayTriggerConfigSchemas.AuthoringId, GameplayTriggerConfigSchemas.AuthoringVersion, baseline.Sources, baseline.Conditions, new[] { baseline.Definitions[0], baseline.Definitions[0] }, baseline.Bindings), CreateEnvironment());
                    break;
                case "duplicate-route":
                {
                    GameplayTriggerRouteAuthoring route = baseline.Definitions[0].Routes[0];
                    var definition = new GameplayTriggerDefinitionAuthoring(1, 1, 1, new[] { route, route });
                    compile = Compile(MatrixDocument(definitions: new[] { definition }), CreateEnvironment());
                    break;
                }
                case "duplicate-binding":
                    compile = Compile(MatrixDocument(bindings: new[] { baseline.Bindings[0], baseline.Bindings[0] }), CreateEnvironment());
                    break;
                case "duplicate-action-registry":
                {
                    GameplayTriggerActionRequirementDescriptor row = new GameplayTriggerActionRequirementDescriptor(SameFrameActionKind, GameplayTriggerDeliveryContract.SameFrameReserved);
                    GameplayTriggerActionRequirementRegistry.TryCreate(new[] { row, row }, out _, out GameplayTriggerConfigRegistryResult result);
                    return Find(result, expectedCode);
                }
                case "missing-source":
                    compile = Compile(MatrixDocument(definitions: new[] { new GameplayTriggerDefinitionAuthoring(1, 1, 2, baseline.Definitions[0].Routes) }), CreateEnvironment());
                    break;
                case "missing-condition":
                    compile = Compile(CreateDocument(route: MatrixRoute(conditionId: 2)), CreateEnvironment());
                    break;
                case "missing-definition":
                    compile = Compile(MatrixDocument(bindings: new[] { new GameplayTriggerBindingAuthoring(1, 2, 1, 1, 1) }), CreateEnvironment());
                    break;
                case "invalid-source-kind":
                    compile = Compile(CreateDocument(source: new GameplayTriggerSourceAuthoring(1, (GameplayTriggerSourceKind)99)), CreateEnvironment());
                    break;
                case "invalid-signal-ordering":
                    compile = Compile(CreateDocument(source: new GameplayTriggerSourceAuthoring(1, GameplayTriggerSourceKind.Signal, (GameplayTriggerSignalOrdering)99)), CreateEnvironment());
                    break;
                case "invalid-threshold-direction":
                    compile = Compile(CreateDocument(source: new GameplayTriggerSourceAuthoring(1, GameplayTriggerSourceKind.Signal, thresholdDirection: (GameplayTriggerThresholdDirection)99)), CreateEnvironment());
                    break;
                case "invalid-initial-policy":
                    compile = Compile(CreateDocument(source: new GameplayTriggerSourceAuthoring(1, GameplayTriggerSourceKind.Signal, initialSamplePolicy: (GameplayTriggerInitialSamplePolicy)99)), CreateEnvironment());
                    break;
                case "invalid-departure-policy":
                    compile = Compile(CreateDocument(source: new GameplayTriggerSourceAuthoring(1, GameplayTriggerSourceKind.Signal, departurePolicy: (GameplayTriggerSubjectDeparturePolicy)99)), CreateEnvironment());
                    break;
                case "invalid-binding-capacity":
                    compile = Compile(CreateDocument(route: MatrixRoute(maxStateKeys: 2)), CreateEnvironment());
                    break;
                case "missing-source-contract":
                    compile = Compile(baseline, CreateEnvironment(sourceContracts: new[] { CreateSignalContract(2, 1, 1) }));
                    break;
                case "source-definition-mismatch":
                    compile = Compile(baseline, CreateEnvironment(sourceContracts: new[] { CreateSignalContract(1, 2, 1) }));
                    break;
                case "source-kind-mismatch":
                    compile = Compile(baseline, CreateEnvironment(sourceContracts: new[] { CreateStatefulContract(1, 1, GameplayTriggerSourceKind.Occupancy) }));
                    break;
                case "source-binding-zero":
                {
                    var row = new GameplayTriggerSourceBindingContractDescriptor(default, new GameplayTriggerSourceDefinitionId(1), GameplayTriggerSourceKind.Signal, new GameplayTriggerSourceProviderId(1), GameplayTriggerSourceEntityCapability.None, true, 1, 0, 1, GameplayTriggerSignalOrdering.UnorderedMultiset);
                    GameplayTriggerSourceBindingContractRegistry.TryCreate(new[] { row }, out _, out GameplayTriggerConfigRegistryResult result);
                    return Find(result, expectedCode);
                }
                case "source-definition-zero":
                {
                    var row = new GameplayTriggerSourceBindingContractDescriptor(new GameplayTriggerSourceBindingId(1), default, GameplayTriggerSourceKind.Signal, new GameplayTriggerSourceProviderId(1), GameplayTriggerSourceEntityCapability.None, true, 1, 0, 1, GameplayTriggerSignalOrdering.UnorderedMultiset);
                    GameplayTriggerSourceBindingContractRegistry.TryCreate(new[] { row }, out _, out GameplayTriggerConfigRegistryResult result);
                    return Find(result, expectedCode);
                }
                case "source-provider-missing":
                {
                    GameplayTriggerSourceBindingContractDescriptor row = new GameplayTriggerSourceBindingContractDescriptor(new GameplayTriggerSourceBindingId(1), new GameplayTriggerSourceDefinitionId(1), GameplayTriggerSourceKind.Signal, default, GameplayTriggerSourceEntityCapability.None, true, 1, 0, 1, GameplayTriggerSignalOrdering.UnorderedMultiset);
                    GameplayTriggerSourceBindingContractRegistry.TryCreate(new[] { row }, out _, out GameplayTriggerConfigRegistryResult result);
                    return Find(result, expectedCode);
                }
                case "source-capability-invalid":
                {
                    GameplayTriggerSourceBindingContractDescriptor row = CreateSignalContract(1, 1, 1, GameplayTriggerSourceEntityCapability.SubjectGuaranteed);
                    GameplayTriggerSourceBindingContractRegistry.TryCreate(new[] { row }, out _, out GameplayTriggerConfigRegistryResult result);
                    return Find(result, expectedCode);
                }
                case "source-evaluation-invalid":
                {
                    GameplayTriggerSourceBindingContractDescriptor row = new GameplayTriggerSourceBindingContractDescriptor(new GameplayTriggerSourceBindingId(1), new GameplayTriggerSourceDefinitionId(1), GameplayTriggerSourceKind.Signal, new GameplayTriggerSourceProviderId(1), GameplayTriggerSourceEntityCapability.None, false, 0, 0, 1, GameplayTriggerSignalOrdering.UnorderedMultiset);
                    GameplayTriggerSourceBindingContractRegistry.TryCreate(new[] { row }, out _, out GameplayTriggerConfigRegistryResult result);
                    return Find(result, expectedCode);
                }
                case "empty-occurrence":
                    compile = Compile(CreateDocument(route: MatrixRoute(occurrence: GameplayTriggerOccurrenceKind.None)), CreateEnvironment());
                    break;
                case "invalid-occurrence":
                    compile = Compile(CreateDocument(route: MatrixRoute(occurrence: (GameplayTriggerOccurrenceKind)128)), CreateEnvironment());
                    break;
                case "invalid-state-scope":
                    compile = Compile(CreateDocument(route: MatrixRoute(stateScope: (GameplayTriggerStateScope)99)), CreateEnvironment());
                    break;
                case "negative-activations":
                    compile = Compile(CreateDocument(route: MatrixRoute(maxActivations: -1)), CreateEnvironment());
                    break;
                case "negative-cooldown":
                    compile = Compile(CreateDocument(route: MatrixRoute(cooldown: -1)), CreateEnvironment());
                    break;
                case "invalid-presence":
                    compile = Compile(CreateDocument(route: MatrixRoute(presence: (GameplayTriggerPresenceLimit)99)), CreateEnvironment());
                    break;
                case "negative-stay":
                    compile = Compile(CreateDocument(route: MatrixRoute(stay: -1)), CreateEnvironment());
                    break;
                case "invalid-exclusive":
                    compile = Compile(CreateDocument(route: MatrixRoute(exclusiveGroupId: -1)), CreateEnvironment());
                    break;
                case "invalid-arbitration":
                    compile = Compile(CreateDocument(route: MatrixRoute(arbitrationScope: (GameplayTriggerArbitrationScope)99)), CreateEnvironment());
                    break;
                case "once-presence":
                    compile = Compile(CreateDocument(route: MatrixRoute(presence: GameplayTriggerPresenceLimit.OncePerPresence)), CreateEnvironment());
                    break;
                case "missing-subject":
                    compile = Compile(CreateDocument(route: MatrixRoute(stateScope: GameplayTriggerStateScope.PerSubject)), CreateEnvironment());
                    break;
                case "missing-instigator":
                    compile = Compile(CreateDocument(route: MatrixRoute(stateScope: GameplayTriggerStateScope.PerInstigator)), CreateEnvironment());
                    break;
                case "exclusive-scope":
                    compile = Compile(CreateExclusiveScopeMismatchDocument(), CreateEnvironment(sourceContracts: new[] { CreateSignalContract(1, 1, 1, GameplayTriggerSourceEntityCapability.SubjectPossible | GameplayTriggerSourceEntityCapability.SubjectGuaranteed), CreateSignalContract(2, 1, 1, GameplayTriggerSourceEntityCapability.SubjectPossible | GameplayTriggerSourceEntityCapability.SubjectGuaranteed) }));
                    break;
                case "route-count":
                {
                    GameplayTriggerRouteAuthoring route = MatrixRoute();
                    var definition = new GameplayTriggerDefinitionAuthoring(1, 1, 1, new[] { route, new GameplayTriggerRouteAuthoring(2, GameplayTriggerOccurrenceKind.Pulsed, 1, route.Actions) });
                    compile = GameplayTriggerConfigCompiler.Compile(MatrixDocument(definitions: new[] { definition }), new GameplayTriggerCompilerOptions(4, 8, 4, 1, 4, 32), CreateEnvironment());
                    break;
                }
                case "action-count":
                {
                    var route = new GameplayTriggerRouteAuthoring(1, GameplayTriggerOccurrenceKind.Pulsed, 1, new[] { new GameplayTriggerActionAuthoring(SameFrameActionKind.Value, GameplayTriggerTargetSelector.Owner), new GameplayTriggerActionAuthoring(SameFrameActionKind.Value, GameplayTriggerTargetSelector.Owner) });
                    compile = GameplayTriggerConfigCompiler.Compile(CreateDocument(route: route), new GameplayTriggerCompilerOptions(4, 8, 4, 4, 1, 32), CreateEnvironment());
                    break;
                }
                case "condition-arity":
                    compile = Compile(CreateDocument(condition: new GameplayTriggerConditionAuthoring(1, new GameplayTriggerConditionNodeAuthoring(GameplayTriggerConditionOperation.Not))), CreateEnvironment());
                    break;
                case "condition-operand":
                    compile = Compile(CreateDocument(condition: new GameplayTriggerConditionAuthoring(1, new GameplayTriggerConditionNodeAuthoring(GameplayTriggerConditionOperation.PushTrue, operand: 1))), CreateEnvironment());
                    break;
                case "condition-comparison":
                    compile = Compile(CreateDocument(condition: new GameplayTriggerConditionAuthoring(1, new GameplayTriggerConditionNodeAuthoring(GameplayTriggerConditionOperation.PushTrue, comparison: GameplayTriggerComparison.Greater))), CreateEnvironment());
                    break;
                case "condition-depth":
                {
                    var root = new GameplayTriggerConditionNodeAuthoring(GameplayTriggerConditionOperation.Not, children: new[] { new GameplayTriggerConditionNodeAuthoring(GameplayTriggerConditionOperation.PushTrue) });
                    compile = GameplayTriggerConfigCompiler.Compile(CreateDocument(condition: new GameplayTriggerConditionAuthoring(1, root)), new GameplayTriggerCompilerOptions(1, 8, 4, 4, 4, 32), CreateEnvironment());
                    break;
                }
                case "condition-stack":
                {
                    var root = new GameplayTriggerConditionNodeAuthoring(GameplayTriggerConditionOperation.All, children: new[] { new GameplayTriggerConditionNodeAuthoring(GameplayTriggerConditionOperation.PushTrue), new GameplayTriggerConditionNodeAuthoring(GameplayTriggerConditionOperation.PushTrue) });
                    compile = GameplayTriggerConfigCompiler.Compile(CreateDocument(condition: new GameplayTriggerConditionAuthoring(1, root)), new GameplayTriggerCompilerOptions(4, 8, 1, 4, 4, 32), CreateEnvironment());
                    break;
                }
                case "invalid-target-selector":
                    compile = Compile(CreateDocument(action: new GameplayTriggerActionAuthoring(SameFrameActionKind.Value, (GameplayTriggerTargetSelector)99)), CreateEnvironment());
                    break;
                case "duplicate-target-slot":
                    compile = Compile(CreateDocument(targets: new[] { new GameplayTriggerBindingTargetAuthoring(1, 1, 1), new GameplayTriggerBindingTargetAuthoring(1, 1, 2) }), CreateEnvironment());
                    break;
                case "unknown-action":
                    compile = Compile(CreateDocument(action: new GameplayTriggerActionAuthoring(2222, GameplayTriggerTargetSelector.Owner)), CreateEnvironment());
                    break;
                case "action-kind-zero":
                {
                    var row = new GameplayTriggerActionRequirementDescriptor(default, GameplayTriggerDeliveryContract.SameFrameReserved);
                    GameplayTriggerActionRequirementRegistry.TryCreate(new[] { row }, out _, out GameplayTriggerConfigRegistryResult result);
                    return Find(result, expectedCode);
                }
                case "invalid-action-registry":
                {
                    var row = new GameplayTriggerActionRequirementDescriptor(SameFrameActionKind, GameplayTriggerDeliveryContract.SameFrameReserved, GameplayTriggerInternalDeferredOperationKind.Reset);
                    GameplayTriggerActionRequirementRegistry.TryCreate(new[] { row }, out _, out GameplayTriggerConfigRegistryResult result);
                    return Find(result, expectedCode);
                }
                case "invalid-options":
                    compile = GameplayTriggerConfigCompiler.Compile(baseline, new GameplayTriggerCompilerOptions(0, 1, 1, 1, 1, 1), CreateEnvironment());
                    break;
                case "capacity-overflow":
                    compile = Compile(CreateCapacityOverflowDocument(), CreateEnvironment(sourceContracts: new[] { CreateSignalContractWithInput(1, int.MaxValue), CreateSignalContractWithInput(2, int.MaxValue) }));
                    break;
                case "profile-shape":
                {
                    GameplayTriggerCompileResult valid = Compile(baseline, CreateEnvironment());
                    GameplayTriggerRuntimeCapacityRequirements required = valid.CompiledCatalog.CapacityRequirements;
                    GameplayTriggerRuntimeCapacityProfile profile = new GameplayTriggerRuntimeCapacityProfile(required.RequiredInstances, required.MaxInputBatches, required.MaxInputItems, required.MaxRelations, required.MaxOccurrences, required.MaxCandidates, required.MaxRouteStates, required.MaxActivations, required.MaxIntents, required.MaxPreparedActions, required.MaxConditionStackDepth, -1, required.MaxReliableOutboxRecords);
                    GameplayTriggerCatalogBootstrapResult result = GameplayTriggerCatalogBootstrap.TryCreateCatalog(valid.CompiledCatalog, new[] { GameplayTriggerActionBootstrapBinding.FromSink(new FakeSink(SameFrameActionKind, GameplayTriggerDeliveryContract.SameFrameReserved)) }, profile);
                    return Find(result, expectedCode);
                }
                case "invalid-environment":
                    compile = GameplayTriggerConfigCompiler.Compile(baseline, GameplayTriggerCompilerOptions.Default, null);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(scenario), scenario, null);
            }
            return Find(compile, expectedCode);
        }

        private static GameplayTriggerCompileResult Compile(GameplayTriggerAuthoringDocument document, GameplayTriggerCompilerEnvironment environment) =>
            GameplayTriggerConfigCompiler.Compile(document, GameplayTriggerCompilerOptions.Default, environment);

        private static GameplayTriggerAuthoringDocument CreateDocument(
            GameplayTriggerSourceAuthoring source = null,
            GameplayTriggerConditionAuthoring condition = null,
            GameplayTriggerRouteAuthoring route = null,
            GameplayTriggerActionAuthoring? action = null,
            GameplayTriggerBindingTargetAuthoring[] targets = null)
        {
            GameplayTriggerRouteAuthoring actualRoute = route ?? new GameplayTriggerRouteAuthoring(
                1,
                GameplayTriggerOccurrenceKind.Pulsed,
                1,
                new[] { action ?? new GameplayTriggerActionAuthoring(SameFrameActionKind.Value, GameplayTriggerTargetSelector.Owner) });
            return new GameplayTriggerAuthoringDocument(
                GameplayTriggerConfigSchemas.AuthoringId,
                GameplayTriggerConfigSchemas.AuthoringVersion,
                new[] { source ?? CreateSignalSource(1) },
                new[] { condition ?? TrueCondition(1) },
                new[] { new GameplayTriggerDefinitionAuthoring(1, 1, 1, new[] { actualRoute }) },
                new[] { new GameplayTriggerBindingAuthoring(1, 1, 1, 1, 1, explicitTargets: targets) });
        }

        private static GameplayTriggerConfigDiagnostic InvalidEnvironmentDiagnostic(string schemaId, int schemaVersion, GameplayTriggerConfigDiagnosticCode expectedCode)
        {
            GameplayTriggerRetiredIdRegistry.TryCreate(null, out GameplayTriggerRetiredIdRegistry retired, out _);
            GameplayTriggerSourceBindingContractRegistry.TryCreate(new[] { CreateSignalContract(1, 1, 1) }, out GameplayTriggerSourceBindingContractRegistry sources, out _);
            GameplayTriggerActionRequirementRegistry.TryCreate(new[] { new GameplayTriggerActionRequirementDescriptor(SameFrameActionKind, GameplayTriggerDeliveryContract.SameFrameReserved) }, out GameplayTriggerActionRequirementRegistry actions, out _);
            GameplayTriggerCompilerEnvironment.TryCreate(retired, sources, actions, new GameplayTriggerExpectedCatalogIdentity(schemaId, schemaVersion), out _, out GameplayTriggerConfigRegistryResult result);
            return Find(result, expectedCode);
        }

        private static GameplayTriggerAuthoringDocument MatrixDocument(
            GameplayTriggerSourceAuthoring[] sources = null,
            GameplayTriggerConditionAuthoring[] conditions = null,
            GameplayTriggerDefinitionAuthoring[] definitions = null,
            GameplayTriggerBindingAuthoring[] bindings = null)
        {
            GameplayTriggerAuthoringDocument baseline = CreateDocument();
            return new GameplayTriggerAuthoringDocument(
                GameplayTriggerConfigSchemas.AuthoringId,
                GameplayTriggerConfigSchemas.AuthoringVersion,
                sources ?? baseline.Sources,
                conditions ?? baseline.Conditions,
                definitions ?? baseline.Definitions,
                bindings ?? baseline.Bindings);
        }

        private static GameplayTriggerRouteAuthoring MatrixRoute(
            int conditionId = 1,
            GameplayTriggerOccurrenceKind occurrence = GameplayTriggerOccurrenceKind.Pulsed,
            GameplayTriggerStateScope stateScope = GameplayTriggerStateScope.PerInstance,
            int maxActivations = 0,
            long cooldown = 0,
            GameplayTriggerPresenceLimit presence = GameplayTriggerPresenceLimit.Ignore,
            long stay = 0,
            int exclusiveGroupId = 0,
            GameplayTriggerArbitrationScope arbitrationScope = GameplayTriggerArbitrationScope.Global,
            int maxStateKeys = 1) =>
            new GameplayTriggerRouteAuthoring(
                1,
                occurrence,
                conditionId,
                new[] { new GameplayTriggerActionAuthoring(SameFrameActionKind.Value, GameplayTriggerTargetSelector.Owner) },
                stateScope,
                maxActivations,
                cooldown,
                presence,
                stay,
                exclusiveGroupId: exclusiveGroupId,
                arbitrationScope: arbitrationScope,
                maxStateKeys: maxStateKeys);

        private static GameplayTriggerSourceBindingContractDescriptor CreateStatefulContract(int sourceBindingId, int sourceDefinitionId, GameplayTriggerSourceKind sourceKind) =>
            new GameplayTriggerSourceBindingContractDescriptor(
                new GameplayTriggerSourceBindingId(sourceBindingId),
                new GameplayTriggerSourceDefinitionId(sourceDefinitionId),
                sourceKind,
                new GameplayTriggerSourceProviderId(1),
                GameplayTriggerSourceEntityCapability.None,
                true,
                1,
                1,
                0,
                GameplayTriggerSignalOrdering.UnorderedMultiset);

        private static GameplayTriggerAuthoringDocument CreateExclusiveScopeMismatchDocument()
        {
            GameplayTriggerRouteAuthoring global = MatrixRoute(exclusiveGroupId: 1, arbitrationScope: GameplayTriggerArbitrationScope.Global);
            GameplayTriggerRouteAuthoring subject = new GameplayTriggerRouteAuthoring(
                2,
                GameplayTriggerOccurrenceKind.Pulsed,
                1,
                new[] { new GameplayTriggerActionAuthoring(SameFrameActionKind.Value, GameplayTriggerTargetSelector.Owner) },
                exclusiveGroupId: 1,
                arbitrationScope: GameplayTriggerArbitrationScope.PerSubject);
            return MatrixDocument(
                definitions: new[]
                {
                    new GameplayTriggerDefinitionAuthoring(1, 1, 1, new[] { global }),
                    new GameplayTriggerDefinitionAuthoring(2, 1, 1, new[] { subject })
                },
                bindings: new[]
                {
                    new GameplayTriggerBindingAuthoring(1, 1, 1, 1, 1),
                    new GameplayTriggerBindingAuthoring(2, 2, 2, 2, 1)
                });
        }

        private static GameplayTriggerAuthoringDocument CreateCapacityOverflowDocument() =>
            MatrixDocument(
                bindings: new[]
                {
                    new GameplayTriggerBindingAuthoring(1, 1, 1, 1, 1),
                    new GameplayTriggerBindingAuthoring(2, 1, 2, 2, 1)
                });

        private static GameplayTriggerSourceBindingContractDescriptor CreateSignalContractWithInput(int sourceBindingId, int maxInputItems) =>
            new GameplayTriggerSourceBindingContractDescriptor(
                new GameplayTriggerSourceBindingId(sourceBindingId),
                new GameplayTriggerSourceDefinitionId(1),
                GameplayTriggerSourceKind.Signal,
                new GameplayTriggerSourceProviderId(1),
                GameplayTriggerSourceEntityCapability.None,
                true,
                maxInputItems,
                0,
                1,
                GameplayTriggerSignalOrdering.UnorderedMultiset);

        private static GameplayTriggerAuthoringDocument CreateDocumentWithBindings(GameplayTriggerBindingAuthoring[] bindings) =>
            new GameplayTriggerAuthoringDocument(
                GameplayTriggerConfigSchemas.AuthoringId,
                GameplayTriggerConfigSchemas.AuthoringVersion,
                new[] { CreateSignalSource(1) },
                new[] { TrueCondition(1) },
                new[] { CreateDefinition(1, 1, 1, 1) },
                bindings);

        private static GameplayTriggerAuthoringDocument CreateThreeNodeCycleDocument(bool permuted, GameplayTriggerConditionAuthoring condition)
        {
            GameplayTriggerDefinitionAuthoring definition10 = CreateSignalEdgeDefinition(10, 2);
            GameplayTriggerDefinitionAuthoring definition20 = CreateSignalEdgeDefinition(20, 3);
            GameplayTriggerDefinitionAuthoring definition30 = CreateSignalEdgeDefinition(30, 1);
            GameplayTriggerBindingAuthoring binding30 = CreateSignalEdgeBinding(30, 10, 1, 2);
            GameplayTriggerBindingAuthoring binding10 = CreateSignalEdgeBinding(10, 20, 2, 3);
            GameplayTriggerBindingAuthoring binding20 = CreateSignalEdgeBinding(20, 30, 3, 1);
            return new GameplayTriggerAuthoringDocument(
                GameplayTriggerConfigSchemas.AuthoringId,
                GameplayTriggerConfigSchemas.AuthoringVersion,
                new[] { CreateSignalSource(1) },
                new[] { condition },
                permuted
                    ? new[] { definition30, definition10, definition20 }
                    : new[] { definition20, definition30, definition10 },
                permuted
                    ? new[] { binding20, binding30, binding10 }
                    : new[] { binding10, binding20, binding30 });
        }

        private static GameplayTriggerDefinitionAuthoring CreateSignalEdgeDefinition(int definitionId, int targetSourceBindingId) =>
            new GameplayTriggerDefinitionAuthoring(
                definitionId,
                1,
                1,
                new[]
                {
                    new GameplayTriggerRouteAuthoring(
                        1,
                        GameplayTriggerOccurrenceKind.Pulsed,
                        1,
                        new[]
                        {
                            new GameplayTriggerActionAuthoring(
                                InternalSignalActionKind.Value,
                                GameplayTriggerTargetSelector.ExplicitBindingTarget,
                                targetSlotId: targetSourceBindingId)
                        })
                });

        private static GameplayTriggerAuthoringDocument CreateThresholdDecimalDocument(string value) =>
            CreateDocument(
                source: new GameplayTriggerSourceAuthoring(
                    1,
                    GameplayTriggerSourceKind.Threshold,
                    enterThreshold: GameplayTriggerFix64Authoring.FromInvariantDecimal(value),
                    exitThreshold: GameplayTriggerFix64Authoring.FromRaw(0)));

        private static GameplayTriggerAuthoringDocument CreateInternalSignalSelfCycleDocument(GameplayTriggerConditionAuthoring condition) =>
            CreateDocument(
                condition: condition,
                action: new GameplayTriggerActionAuthoring(
                    InternalSignalActionKind.Value,
                    GameplayTriggerTargetSelector.ExplicitBindingTarget,
                    targetSlotId: 1),
                targets: new[]
                {
                    new GameplayTriggerBindingTargetAuthoring(
                        1,
                        GameplayTriggerTargetRef.GameplayTriggerSourceBindingDomainId,
                        1)
                });

        private static GameplayTriggerCompilerEnvironment CreateInternalSignalEnvironment() =>
            CreateEnvironment(
                actionRequirements: new[]
                {
                    new GameplayTriggerActionRequirementDescriptor(
                        InternalSignalActionKind,
                        GameplayTriggerDeliveryContract.InternalDeferred,
                        GameplayTriggerInternalDeferredOperationKind.Signal,
                        signalId: 7)
                });

        private static GameplayTriggerConditionAuthoring CreateComposedConstantCondition(int id, GameplayTriggerConditionOperation operation)
        {
            GameplayTriggerConditionNodeAuthoring root;
            if (operation == GameplayTriggerConditionOperation.Not)
            {
                root = new GameplayTriggerConditionNodeAuthoring(
                    operation,
                    children: new[] { new GameplayTriggerConditionNodeAuthoring(GameplayTriggerConditionOperation.PushFalse) });
            }
            else
            {
                root = new GameplayTriggerConditionNodeAuthoring(
                    operation,
                    children: new[]
                    {
                        new GameplayTriggerConditionNodeAuthoring(GameplayTriggerConditionOperation.PushTrue),
                        new GameplayTriggerConditionNodeAuthoring(operation == GameplayTriggerConditionOperation.All
                            ? GameplayTriggerConditionOperation.PushTrue
                            : GameplayTriggerConditionOperation.PushFalse)
                    });
            }
            return new GameplayTriggerConditionAuthoring(id, root);
        }

        private static GameplayTriggerBindingAuthoring CreateSignalEdgeBinding(int bindingId, int definitionId, int sourceBindingId, int targetSourceBindingId) =>
            new GameplayTriggerBindingAuthoring(
                bindingId,
                definitionId,
                sourceBindingId,
                bindingId,
                1,
                explicitTargets: new[]
                {
                    new GameplayTriggerBindingTargetAuthoring(
                        targetSourceBindingId,
                        GameplayTriggerTargetRef.GameplayTriggerSourceBindingDomainId,
                        targetSourceBindingId)
                });

        private static GameplayTriggerSourceAuthoring CreateSignalSource(int id) =>
            new GameplayTriggerSourceAuthoring(id, GameplayTriggerSourceKind.Signal);

        private static GameplayTriggerConditionAuthoring TrueCondition(int id) =>
            new GameplayTriggerConditionAuthoring(
                id,
                new GameplayTriggerConditionNodeAuthoring(GameplayTriggerConditionOperation.PushTrue));

        private static GameplayTriggerDefinitionAuthoring CreateDefinition(int definitionId, int sourceId, int routeId, int conditionId) =>
            new GameplayTriggerDefinitionAuthoring(
                definitionId,
                1,
                sourceId,
                new[]
                {
                    new GameplayTriggerRouteAuthoring(
                        routeId,
                        GameplayTriggerOccurrenceKind.Pulsed,
                        conditionId,
                        new[] { new GameplayTriggerActionAuthoring(SameFrameActionKind.Value, GameplayTriggerTargetSelector.Owner) })
                });

        private static GameplayTriggerSourceBindingContractDescriptor CreateSignalContract(
            int sourceBindingId,
            int sourceDefinitionId,
            int providerId,
            GameplayTriggerSourceEntityCapability capabilities = GameplayTriggerSourceEntityCapability.None) =>
            new GameplayTriggerSourceBindingContractDescriptor(
                new GameplayTriggerSourceBindingId(sourceBindingId),
                new GameplayTriggerSourceDefinitionId(sourceDefinitionId),
                GameplayTriggerSourceKind.Signal,
                new GameplayTriggerSourceProviderId(providerId),
                capabilities,
                true,
                1,
                0,
                signalEnvelopeSchemaVersion: 1,
                signalOrdering: GameplayTriggerSignalOrdering.UnorderedMultiset);

        private static GameplayTriggerCompilerEnvironment CreateEnvironment(
            GameplayTriggerRetiredIdDescriptor[] retired = null,
            GameplayTriggerSourceBindingContractDescriptor[] sourceContracts = null,
            GameplayTriggerActionRequirementDescriptor[] actionRequirements = null,
            long? expectedHash = null)
        {
            Assert.That(GameplayTriggerRetiredIdRegistry.TryCreate(retired, out GameplayTriggerRetiredIdRegistry retiredRegistry, out GameplayTriggerConfigRegistryResult retiredResult), Is.True, Describe(retiredResult));
            Assert.That(GameplayTriggerSourceBindingContractRegistry.TryCreate(
                sourceContracts ?? new[] { CreateSignalContract(1, 1, 1) },
                out GameplayTriggerSourceBindingContractRegistry sourceRegistry,
                out GameplayTriggerConfigRegistryResult sourceResult), Is.True, Describe(sourceResult));
            Assert.That(GameplayTriggerActionRequirementRegistry.TryCreate(
                actionRequirements ?? new[] { new GameplayTriggerActionRequirementDescriptor(SameFrameActionKind, GameplayTriggerDeliveryContract.SameFrameReserved) },
                out GameplayTriggerActionRequirementRegistry actionRegistry,
                out GameplayTriggerConfigRegistryResult actionResult), Is.True, Describe(actionResult));
            var expected = new GameplayTriggerExpectedCatalogIdentity(
                GameplayTriggerConfigSchemas.RuntimeCatalogId,
                GameplayTriggerConfigSchemas.RuntimeCatalogVersion,
                expectedHash.HasValue,
                expectedHash.GetValueOrDefault());
            Assert.That(GameplayTriggerCompilerEnvironment.TryCreate(
                retiredRegistry,
                sourceRegistry,
                actionRegistry,
                expected,
                out GameplayTriggerCompilerEnvironment environment,
                out GameplayTriggerConfigRegistryResult environmentResult), Is.True, Describe(environmentResult));
            return environment;
        }

        private static GameplayTriggerRuntimeCapacityProfile ExactProfile(GameplayTriggerRuntimeCapacityRequirements required) =>
            new GameplayTriggerRuntimeCapacityProfile(
                required.RequiredInstances,
                required.MaxInputBatches,
                required.MaxInputItems,
                required.MaxRelations,
                required.MaxOccurrences,
                required.MaxCandidates,
                required.MaxRouteStates,
                required.MaxActivations,
                required.MaxIntents,
                required.MaxPreparedActions,
                required.MaxConditionStackDepth,
                required.MaxDeferredRecords,
                required.MaxReliableOutboxRecords);

        private static GameplayTriggerRuntimeCapacityProfile ProfileWithPrepared(GameplayTriggerRuntimeCapacityRequirements required, int maxPreparedActions) =>
            new GameplayTriggerRuntimeCapacityProfile(
                required.RequiredInstances,
                required.MaxInputBatches,
                required.MaxInputItems,
                required.MaxRelations,
                required.MaxOccurrences,
                required.MaxCandidates,
                required.MaxRouteStates,
                required.MaxActivations,
                required.MaxIntents,
                maxPreparedActions,
                required.MaxConditionStackDepth,
                required.MaxDeferredRecords,
                required.MaxReliableOutboxRecords);

        private static GameplayTriggerRuntimeCapacityProfile ProfileWithDelta(GameplayTriggerRuntimeCapacityRequirements required, string field, int delta)
        {
            int Adjust(string current, int value) => string.Equals(current, field, StringComparison.Ordinal) ? value + delta : value;
            return new GameplayTriggerRuntimeCapacityProfile(
                Adjust("MaxInstances", required.RequiredInstances),
                Adjust("MaxInputBatches", required.MaxInputBatches),
                Adjust("MaxInputItems", required.MaxInputItems),
                Adjust("MaxRelations", required.MaxRelations),
                Adjust("MaxOccurrences", required.MaxOccurrences),
                Adjust("MaxCandidates", required.MaxCandidates),
                Adjust("MaxRouteStates", required.MaxRouteStates),
                Adjust("MaxActivations", required.MaxActivations),
                Adjust("MaxIntents", required.MaxIntents),
                Adjust("MaxPreparedActions", required.MaxPreparedActions),
                Adjust("MaxConditionStackDepth", required.MaxConditionStackDepth),
                Adjust("MaxDeferredRecords", required.MaxDeferredRecords),
                Adjust("MaxReliableOutboxRecords", required.MaxReliableOutboxRecords));
        }

        private static void AssertDiagnostic(GameplayTriggerCompileResult result, GameplayTriggerConfigDiagnosticCode code)
        {
            Assert.That(result.Succeeded, Is.False);
            Assert.That(Contains(result, code), Is.True, Describe(result));
        }

        private static void AssertCanonicalProjectionEqual(GameplayTriggerCompiledCatalogPlan first, GameplayTriggerCompiledCatalogPlan second)
        {
            Assert.That(second.SourceDefinitionCount, Is.EqualTo(first.SourceDefinitionCount));
            Assert.That(second.DefinitionCount, Is.EqualTo(first.DefinitionCount));
            Assert.That(second.BindingCount, Is.EqualTo(first.BindingCount));
            for (int i = 0; i < first.SourceDefinitionCount; i++)
            {
                GameplayTriggerSourceDefinition left = first.GetSourceDefinition(i);
                GameplayTriggerSourceDefinition right = second.GetSourceDefinition(i);
                Assert.That(right.SourceDefinitionId, Is.EqualTo(left.SourceDefinitionId));
                Assert.That(right.SourceKind, Is.EqualTo(left.SourceKind));
                Assert.That(right.SignalOrdering, Is.EqualTo(left.SignalOrdering));
            }
            for (int i = 0; i < first.DefinitionCount; i++)
            {
                GameplayTriggerDefinition left = first.GetDefinition(i);
                GameplayTriggerDefinition right = second.GetDefinition(i);
                Assert.That(right.DefinitionId, Is.EqualTo(left.DefinitionId));
                Assert.That(right.DefinitionVersion, Is.EqualTo(left.DefinitionVersion));
                Assert.That(right.SourceDefinitionId, Is.EqualTo(left.SourceDefinitionId));
                Assert.That(right.RouteCount, Is.EqualTo(left.RouteCount));
                for (int routeIndex = 0; routeIndex < left.RouteCount; routeIndex++)
                {
                    GameplayTriggerRouteDefinition leftRoute = left.GetRoute(routeIndex);
                    GameplayTriggerRouteDefinition rightRoute = right.GetRoute(routeIndex);
                    Assert.That(rightRoute.RouteId, Is.EqualTo(leftRoute.RouteId));
                    Assert.That(rightRoute.ActionCount, Is.EqualTo(leftRoute.ActionCount));
                    for (int actionIndex = 0; actionIndex < leftRoute.ActionCount; actionIndex++)
                        Assert.That(rightRoute.GetAction(actionIndex).ActionKindId, Is.EqualTo(leftRoute.GetAction(actionIndex).ActionKindId));
                }
            }
            for (int i = 0; i < first.BindingCount; i++)
            {
                GameplayTriggerBinding left = first.GetBinding(i);
                GameplayTriggerBinding right = second.GetBinding(i);
                Assert.That(right.BindingId, Is.EqualTo(left.BindingId));
                Assert.That(right.DefinitionId, Is.EqualTo(left.DefinitionId));
                Assert.That(right.SourceBindingId, Is.EqualTo(left.SourceBindingId));
                Assert.That(right.Owner, Is.EqualTo(left.Owner));
            }
        }

        private static void AssertPublicProjectionType(Type type, string member)
        {
            Assert.That(typeof(IGameplayTriggerActionSink).IsAssignableFrom(type), Is.False, member);
            Assert.That(typeof(Delegate).IsAssignableFrom(type), Is.False, member);
            Assert.That(type.FullName == null || type.FullName.IndexOf("UnityEngine", StringComparison.Ordinal) < 0, Is.True, member);
            Assert.That(type.FullName == null || type.FullName.IndexOf("UnityEditor", StringComparison.Ordinal) < 0, Is.True, member);
        }

        private static bool Contains(GameplayTriggerCompileResult result, GameplayTriggerConfigDiagnosticCode code)
        {
            for (int i = 0; i < result.DiagnosticCount; i++) if (result.GetDiagnostic(i).Code == code) return true;
            return false;
        }

        private static GameplayTriggerConfigDiagnostic Find(GameplayTriggerCompileResult result, GameplayTriggerConfigDiagnosticCode code)
        {
            for (int i = 0; i < result.DiagnosticCount; i++) if (result.GetDiagnostic(i).Code == code) return result.GetDiagnostic(i);
            Assert.Fail("Missing diagnostic " + code + ": " + Describe(result));
            return default;
        }

        private static GameplayTriggerConfigDiagnostic Find(GameplayTriggerConfigRegistryResult result, GameplayTriggerConfigDiagnosticCode code)
        {
            for (int i = 0; i < result.DiagnosticCount; i++) if (result.GetDiagnostic(i).Code == code) return result.GetDiagnostic(i);
            Assert.Fail("Missing registry diagnostic " + code + ": " + Describe(result));
            return default;
        }

        private static GameplayTriggerConfigDiagnostic Find(GameplayTriggerCatalogBootstrapResult result, GameplayTriggerConfigDiagnosticCode code)
        {
            for (int i = 0; i < result.DiagnosticCount; i++) if (result.GetDiagnostic(i).Code == code) return result.GetDiagnostic(i);
            Assert.Fail("Missing bootstrap diagnostic " + code + ": " + Describe(result));
            return default;
        }

        private static bool Contains(GameplayTriggerConfigRegistryResult result, GameplayTriggerConfigDiagnosticCode code)
        {
            for (int i = 0; i < result.DiagnosticCount; i++) if (result.GetDiagnostic(i).Code == code) return true;
            return false;
        }

        private static bool Contains(GameplayTriggerCatalogBootstrapResult result, GameplayTriggerConfigDiagnosticCode code)
        {
            for (int i = 0; i < result.DiagnosticCount; i++) if (result.GetDiagnostic(i).Code == code) return true;
            return false;
        }

        private static string Describe(GameplayTriggerCompileResult result)
        {
            string text = string.Empty;
            for (int i = 0; i < result.DiagnosticCount; i++)
            {
                GameplayTriggerConfigDiagnostic diagnostic = result.GetDiagnostic(i);
                text += diagnostic.Code + " " + diagnostic.Path + "; ";
            }
            return text;
        }

        private static string Describe(GameplayTriggerConfigRegistryResult result)
        {
            string text = string.Empty;
            for (int i = 0; i < result.DiagnosticCount; i++)
            {
                GameplayTriggerConfigDiagnostic diagnostic = result.GetDiagnostic(i);
                text += diagnostic.Code + " " + diagnostic.Path + "; ";
            }
            return text;
        }

        private static string Describe(GameplayTriggerCatalogBootstrapResult result)
        {
            string text = string.Empty;
            for (int i = 0; i < result.DiagnosticCount; i++)
            {
                GameplayTriggerConfigDiagnostic diagnostic = result.GetDiagnostic(i);
                text += diagnostic.Code + " " + diagnostic.Path + "; ";
            }
            return text;
        }

        private sealed class FakeSink : IGameplayTriggerActionSink
        {
            public FakeSink(GameplayTriggerActionKindId actionKindId, GameplayTriggerDeliveryContract deliveryContract)
            {
                ActionKindId = actionKindId;
                DeliveryContract = deliveryContract;
            }

            public GameplayTriggerActionKindId ActionKindId { get; }
            public GameplayTriggerDeliveryContract DeliveryContract { get; }
            public bool TryPrepare(in GameplayTriggerActionPreparation preparation, out int token) { token = 1; return true; }
            public void Abort(int token) { }
            public void Commit(int token, in GameplayTriggerActionIntent intent) { }
        }
    }
}
