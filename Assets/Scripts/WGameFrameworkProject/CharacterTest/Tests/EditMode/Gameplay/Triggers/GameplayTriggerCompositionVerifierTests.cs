using System;
using MxFramework.Gameplay.Triggers;
using MxFramework.Gameplay.Triggers.Config;
using MxFramework.Runtime;
using NUnit.Framework;

namespace MxFramework.Tests.Gameplay.Triggers
{
    public sealed class GameplayTriggerCompositionVerifierTests
    {
        private static readonly GameplayTriggerActionKindId SameFrameActionKind = new GameplayTriggerActionKindId(2001);
        private static readonly GameplayTriggerActionKindId ReliableActionKind = new GameplayTriggerActionKindId(2002);

        [Test]
        public void TryVerify_NonReliableCreatedComposition_SucceedsWithoutOutbox()
        {
            CompiledFixture fixture = CreateFixture(SameFrameActionKind, GameplayTriggerDeliveryContract.SameFrameReserved);
            var host = new RuntimeHost();
            var runtime = new GameplayTriggerRuntime(fixture.Bootstrap.Catalog, fixture.Bootstrap.RuntimeOptions.Value);

            bool succeeded = GameplayTriggerCompositionVerifier.TryVerify(
                fixture.Plan,
                host,
                runtime,
                null,
                out GameplayTriggerCompositionVerificationResult result);

            Assert.That(succeeded, Is.True, Describe(result));
            Assert.That(result.Succeeded, Is.True);
            host.Dispose();
        }

        [Test]
        public void TryVerify_InitializedHost_IsRejectedBecauseGateIsCreatedOnly()
        {
            CompiledFixture fixture = CreateFixture(SameFrameActionKind, GameplayTriggerDeliveryContract.SameFrameReserved);
            var host = new RuntimeHost();
            var runtime = new GameplayTriggerRuntime(fixture.Bootstrap.Catalog, fixture.Bootstrap.RuntimeOptions.Value);
            host.Initialize();

            bool succeeded = GameplayTriggerCompositionVerifier.TryVerify(
                fixture.Plan,
                host,
                runtime,
                null,
                out GameplayTriggerCompositionVerificationResult result);

            Assert.That(succeeded, Is.False);
            Assert.That(Contains(result, GameplayTriggerConfigDiagnosticCode.BootstrapCapabilityMismatch), Is.True, Describe(result));
            host.Dispose();
        }

        [Test]
        public void TryVerify_ReliableActualHostOutboxTransferAndTuple_Succeeds()
        {
            ReliableComposition composition = CreateReliableComposition(RuntimeHostErrorPolicy.FailFast, true, true, RuntimeTickStage.Simulation, 0);

            bool succeeded = GameplayTriggerCompositionVerifier.TryVerify(
                composition.Fixture.Plan,
                composition.Host,
                composition.Runtime,
                composition.Outbox,
                out GameplayTriggerCompositionVerificationResult result);

            Assert.That(succeeded, Is.True, Describe(result));
            composition.Host.Dispose();
        }

        [Test]
        public void TryVerify_ReliableNonFailFastHost_IsRejected()
        {
            ReliableComposition composition = CreateReliableComposition(RuntimeHostErrorPolicy.CollectAndContinue, true, true, RuntimeTickStage.Simulation, 0);

            bool succeeded = GameplayTriggerCompositionVerifier.TryVerify(
                composition.Fixture.Plan,
                composition.Host,
                composition.Runtime,
                composition.Outbox,
                out GameplayTriggerCompositionVerificationResult result);

            Assert.That(succeeded, Is.False);
            Assert.That(Contains(result, GameplayTriggerConfigDiagnosticCode.ReliableRequiresFailFast), Is.True, Describe(result));
            composition.Host.Dispose();
        }

        [Test]
        public void TryVerify_UnregisteredOutbox_IsRejectedByInstanceIdentity()
        {
            ReliableComposition composition = CreateReliableComposition(RuntimeHostErrorPolicy.FailFast, false, true, RuntimeTickStage.Simulation, 0);

            bool succeeded = GameplayTriggerCompositionVerifier.TryVerify(
                composition.Fixture.Plan,
                composition.Host,
                composition.Runtime,
                composition.Outbox,
                out GameplayTriggerCompositionVerificationResult result);

            Assert.That(succeeded, Is.False);
            Assert.That(Contains(result, GameplayTriggerConfigDiagnosticCode.ReliableOutboxCapabilityMissing), Is.True, Describe(result));
            composition.Host.Dispose();
        }

        [Test]
        public void TryVerify_DisabledTransfer_IsRejectedFromActualTransferState()
        {
            ReliableComposition composition = CreateReliableComposition(RuntimeHostErrorPolicy.FailFast, true, true, RuntimeTickStage.Simulation, 0);
            composition.Transfer.Enabled = false;

            bool succeeded = GameplayTriggerCompositionVerifier.TryVerify(
                composition.Fixture.Plan,
                composition.Host,
                composition.Runtime,
                composition.Outbox,
                out GameplayTriggerCompositionVerificationResult result);

            Assert.That(succeeded, Is.False);
            Assert.That(Contains(result, GameplayTriggerConfigDiagnosticCode.BootstrapCapabilityMismatch), Is.True, Describe(result));
            composition.Host.Dispose();
        }

        [Test]
        public void TryVerify_MissingTargetModule_IsRejected()
        {
            ReliableComposition composition = CreateReliableComposition(RuntimeHostErrorPolicy.FailFast, true, false, RuntimeTickStage.Simulation, 0);

            bool succeeded = GameplayTriggerCompositionVerifier.TryVerify(
                composition.Fixture.Plan,
                composition.Host,
                composition.Runtime,
                composition.Outbox,
                out GameplayTriggerCompositionVerificationResult result);

            Assert.That(succeeded, Is.False);
            Assert.That(Contains(result, GameplayTriggerConfigDiagnosticCode.ReliableScheduleInvalid), Is.True, Describe(result));
            composition.Host.Dispose();
        }

        [Test]
        public void TryVerify_TargetNotAfterOutboxTuple_IsRejected()
        {
            ReliableComposition composition = CreateReliableComposition(RuntimeHostErrorPolicy.FailFast, true, true, RuntimeTickStage.PreSimulation, -1);

            bool succeeded = GameplayTriggerCompositionVerifier.TryVerify(
                composition.Fixture.Plan,
                composition.Host,
                composition.Runtime,
                composition.Outbox,
                out GameplayTriggerCompositionVerificationResult result);

            Assert.That(succeeded, Is.False);
            Assert.That(Contains(result, GameplayTriggerConfigDiagnosticCode.ReliableScheduleInvalid), Is.True, Describe(result));
            composition.Host.Dispose();
        }

        [Test]
        public void TryVerify_MutatedTransferTargetIdentity_IsRejected()
        {
            ReliableComposition composition = CreateReliableComposition(RuntimeHostErrorPolicy.FailFast, true, true, RuntimeTickStage.Simulation, 0);
            composition.Transfer.TargetModuleId = "target.changed";

            bool succeeded = GameplayTriggerCompositionVerifier.TryVerify(
                composition.Fixture.Plan,
                composition.Host,
                composition.Runtime,
                composition.Outbox,
                out GameplayTriggerCompositionVerificationResult result);

            Assert.That(succeeded, Is.False);
            Assert.That(Contains(result, GameplayTriggerConfigDiagnosticCode.BootstrapCapabilityMismatch), Is.True, Describe(result));
            composition.Host.Dispose();
        }

        [Test]
        public void TryVerify_WrongRuntimeIdentity_IsRejectedEvenWithSameCatalogHash()
        {
            ReliableComposition composition = CreateReliableComposition(RuntimeHostErrorPolicy.FailFast, true, true, RuntimeTickStage.Simulation, 0);
            var otherRuntime = new GameplayTriggerRuntime(
                composition.Fixture.Bootstrap.Catalog,
                composition.Fixture.Bootstrap.RuntimeOptions.Value);

            bool succeeded = GameplayTriggerCompositionVerifier.TryVerify(
                composition.Fixture.Plan,
                composition.Host,
                otherRuntime,
                composition.Outbox,
                out GameplayTriggerCompositionVerificationResult result);

            Assert.That(succeeded, Is.False);
            Assert.That(Contains(result, GameplayTriggerConfigDiagnosticCode.ReliableOutboxCapabilityMissing), Is.True, Describe(result));
            composition.Host.Dispose();
        }

        [Test]
        public void TryVerify_RuntimeCatalogHashMismatch_IsRejected()
        {
            CompiledFixture expected = CreateFixture(ReliableActionKind, GameplayTriggerDeliveryContract.ReliableDeferred);
            CompiledFixture actual = CreateFixture(SameFrameActionKind, GameplayTriggerDeliveryContract.SameFrameReserved);
            var runtime = new GameplayTriggerRuntime(actual.Bootstrap.Catalog, actual.Bootstrap.RuntimeOptions.Value);
            var host = new RuntimeHost();

            bool succeeded = GameplayTriggerCompositionVerifier.TryVerify(
                expected.Plan,
                host,
                runtime,
                null,
                out GameplayTriggerCompositionVerificationResult result);

            Assert.That(succeeded, Is.False);
            Assert.That(Contains(result, GameplayTriggerConfigDiagnosticCode.RuntimeCatalogRejected), Is.True, Describe(result));
            host.Dispose();
        }

        [Test]
        public void TryVerify_MissingTransferFromExactSet_IsRejected()
        {
            CompiledFixture fixture = CreateFixture(ReliableActionKind, GameplayTriggerDeliveryContract.ReliableDeferred);
            var runtime = new GameplayTriggerRuntime(fixture.Bootstrap.Catalog, fixture.Bootstrap.RuntimeOptions.Value);
            var outbox = new GameplayTriggerReliableOutboxModule(runtime);
            var host = new RuntimeHost();
            host.RegisterModule(outbox);
            host.RegisterModule(new TestModule("target.module", RuntimeTickStage.Simulation, 0));

            bool succeeded = GameplayTriggerCompositionVerifier.TryVerify(
                fixture.Plan,
                host,
                runtime,
                outbox,
                out GameplayTriggerCompositionVerificationResult result);

            Assert.That(succeeded, Is.False);
            Assert.That(Contains(result, GameplayTriggerConfigDiagnosticCode.BootstrapCapabilityMismatch), Is.True, Describe(result));
            host.Dispose();
        }

        [Test]
        public void TryVerify_DisabledOutbox_IsRejected()
        {
            ReliableComposition composition = CreateReliableComposition(RuntimeHostErrorPolicy.FailFast, true, true, RuntimeTickStage.Simulation, 0);
            composition.Outbox.SetEnabled(false);

            bool succeeded = GameplayTriggerCompositionVerifier.TryVerify(
                composition.Fixture.Plan,
                composition.Host,
                composition.Runtime,
                composition.Outbox,
                out GameplayTriggerCompositionVerificationResult result);

            Assert.That(succeeded, Is.False);
            Assert.That(Contains(result, GameplayTriggerConfigDiagnosticCode.ReliableOutboxCapabilityMissing), Is.True, Describe(result));
            composition.Host.Dispose();
        }

        [Test]
        public void TryVerify_NonReliablePlanRejectsExtraneousOutbox()
        {
            CompiledFixture fixture = CreateFixture(SameFrameActionKind, GameplayTriggerDeliveryContract.SameFrameReserved);
            var runtime = new GameplayTriggerRuntime(fixture.Bootstrap.Catalog, fixture.Bootstrap.RuntimeOptions.Value);
            var outbox = new GameplayTriggerReliableOutboxModule(runtime);
            var host = new RuntimeHost();
            host.RegisterModule(outbox);

            bool succeeded = GameplayTriggerCompositionVerifier.TryVerify(
                fixture.Plan,
                host,
                runtime,
                outbox,
                out GameplayTriggerCompositionVerificationResult result);

            Assert.That(succeeded, Is.False);
            Assert.That(Contains(result, GameplayTriggerConfigDiagnosticCode.BootstrapCapabilityMismatch), Is.True, Describe(result));
            host.Dispose();
        }

        private static ReliableComposition CreateReliableComposition(
            RuntimeHostErrorPolicy errorPolicy,
            bool registerOutbox,
            bool registerTarget,
            RuntimeTickStage targetStage,
            int targetPriority)
        {
            CompiledFixture fixture = CreateFixture(ReliableActionKind, GameplayTriggerDeliveryContract.ReliableDeferred);
            var runtime = new GameplayTriggerRuntime(fixture.Bootstrap.Catalog, fixture.Bootstrap.RuntimeOptions.Value);
            var transfer = new FakeTransfer(ReliableActionKind, "target.module");
            var outbox = new GameplayTriggerReliableOutboxModule(runtime, new IGameplayTriggerReliableTargetTransfer[] { transfer });
            var host = new RuntimeHost(new RuntimeHostOptions { ErrorPolicy = errorPolicy });
            if (registerOutbox) host.RegisterModule(outbox);
            if (registerTarget) host.RegisterModule(new TestModule("target.module", targetStage, targetPriority));
            return new ReliableComposition(fixture, host, runtime, outbox, transfer);
        }

        private static CompiledFixture CreateFixture(GameplayTriggerActionKindId actionKindId, GameplayTriggerDeliveryContract delivery)
        {
            GameplayTriggerCompilerEnvironment environment = CreateEnvironment(actionKindId, delivery);
            var document = new GameplayTriggerAuthoringDocument(
                GameplayTriggerConfigSchemas.AuthoringId,
                GameplayTriggerConfigSchemas.AuthoringVersion,
                new[] { new GameplayTriggerSourceAuthoring(1, GameplayTriggerSourceKind.Signal) },
                new[]
                {
                    new GameplayTriggerConditionAuthoring(
                        1,
                        new GameplayTriggerConditionNodeAuthoring(GameplayTriggerConditionOperation.PushTrue))
                },
                new[]
                {
                    new GameplayTriggerDefinitionAuthoring(
                        1,
                        1,
                        1,
                        new[]
                        {
                            new GameplayTriggerRouteAuthoring(
                                1,
                                GameplayTriggerOccurrenceKind.Pulsed,
                                1,
                                new[] { new GameplayTriggerActionAuthoring(actionKindId.Value, GameplayTriggerTargetSelector.Owner) })
                        })
                },
                new[] { new GameplayTriggerBindingAuthoring(1, 1, 1, 1, 1) });
            GameplayTriggerCompileResult compile = GameplayTriggerConfigCompiler.Compile(document, GameplayTriggerCompilerOptions.Default, environment);
            Assert.That(compile.Succeeded, Is.True, Describe(compile));
            GameplayTriggerRuntimeCapacityRequirements required = compile.CompiledCatalog.CapacityRequirements;
            var profile = new GameplayTriggerRuntimeCapacityProfile(
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
            GameplayTriggerCatalogBootstrapResult bootstrap = GameplayTriggerCatalogBootstrap.TryCreateCatalog(
                compile.CompiledCatalog,
                new[] { GameplayTriggerActionBootstrapBinding.FromSink(new FakeSink(actionKindId, delivery)) },
                profile);
            Assert.That(bootstrap.Succeeded, Is.True, Describe(bootstrap));
            return new CompiledFixture(compile.CompiledCatalog, bootstrap);
        }

        private static GameplayTriggerCompilerEnvironment CreateEnvironment(GameplayTriggerActionKindId actionKindId, GameplayTriggerDeliveryContract delivery)
        {
            Assert.That(GameplayTriggerRetiredIdRegistry.TryCreate(null, out GameplayTriggerRetiredIdRegistry retired, out _), Is.True);
            var contract = new GameplayTriggerSourceBindingContractDescriptor(
                new GameplayTriggerSourceBindingId(1),
                new GameplayTriggerSourceDefinitionId(1),
                GameplayTriggerSourceKind.Signal,
                new GameplayTriggerSourceProviderId(1),
                GameplayTriggerSourceEntityCapability.None,
                true,
                1,
                0,
                signalEnvelopeSchemaVersion: 1,
                signalOrdering: GameplayTriggerSignalOrdering.UnorderedMultiset);
            Assert.That(GameplayTriggerSourceBindingContractRegistry.TryCreate(new[] { contract }, out GameplayTriggerSourceBindingContractRegistry sources, out _), Is.True);
            Assert.That(GameplayTriggerActionRequirementRegistry.TryCreate(
                new[] { new GameplayTriggerActionRequirementDescriptor(actionKindId, delivery) },
                out GameplayTriggerActionRequirementRegistry actions,
                out _), Is.True);
            var expected = new GameplayTriggerExpectedCatalogIdentity(GameplayTriggerConfigSchemas.RuntimeCatalogId, GameplayTriggerConfigSchemas.RuntimeCatalogVersion);
            Assert.That(GameplayTriggerCompilerEnvironment.TryCreate(retired, sources, actions, expected, out GameplayTriggerCompilerEnvironment environment, out _), Is.True);
            return environment;
        }

        private static bool Contains(GameplayTriggerCompositionVerificationResult result, GameplayTriggerConfigDiagnosticCode code)
        {
            for (int i = 0; i < result.DiagnosticCount; i++) if (result.GetDiagnostic(i).Code == code) return true;
            return false;
        }

        private static string Describe(GameplayTriggerCompositionVerificationResult result)
        {
            string text = string.Empty;
            for (int i = 0; i < result.DiagnosticCount; i++) text += result.GetDiagnostic(i).Code + " " + result.GetDiagnostic(i).Path + "; ";
            return text;
        }

        private static string Describe(GameplayTriggerCompileResult result)
        {
            string text = string.Empty;
            for (int i = 0; i < result.DiagnosticCount; i++) text += result.GetDiagnostic(i).Code + " " + result.GetDiagnostic(i).Path + "; ";
            return text;
        }

        private static string Describe(GameplayTriggerCatalogBootstrapResult result)
        {
            string text = string.Empty;
            for (int i = 0; i < result.DiagnosticCount; i++) text += result.GetDiagnostic(i).Code + " " + result.GetDiagnostic(i).Path + "; ";
            return text;
        }

        private readonly struct CompiledFixture
        {
            public CompiledFixture(GameplayTriggerCompiledCatalogPlan plan, GameplayTriggerCatalogBootstrapResult bootstrap)
            {
                Plan = plan;
                Bootstrap = bootstrap;
            }

            public GameplayTriggerCompiledCatalogPlan Plan { get; }
            public GameplayTriggerCatalogBootstrapResult Bootstrap { get; }
        }

        private readonly struct ReliableComposition
        {
            public ReliableComposition(CompiledFixture fixture, RuntimeHost host, GameplayTriggerRuntime runtime, GameplayTriggerReliableOutboxModule outbox, FakeTransfer transfer)
            {
                Fixture = fixture;
                Host = host;
                Runtime = runtime;
                Outbox = outbox;
                Transfer = transfer;
            }

            public CompiledFixture Fixture { get; }
            public RuntimeHost Host { get; }
            public GameplayTriggerRuntime Runtime { get; }
            public GameplayTriggerReliableOutboxModule Outbox { get; }
            public FakeTransfer Transfer { get; }
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

        private sealed class FakeTransfer : IGameplayTriggerReliableTargetTransfer
        {
            public FakeTransfer(GameplayTriggerActionKindId actionKindId, string targetModuleId)
            {
                ActionKindId = actionKindId;
                TargetModuleId = targetModuleId;
            }

            public GameplayTriggerActionKindId ActionKindId { get; }
            public string TargetModuleId { get; set; }
            public bool IsEnabled => Enabled;
            public bool Enabled { get; set; } = true;

            public GameplayTriggerReliableTransferResult TryTransfer(RuntimeFrame currentFrame, in GameplayTriggerReliableOutboxRecord record) =>
                new GameplayTriggerReliableTransferResult(GameplayTriggerReliableTransferStatus.Accepted);
        }

        private sealed class TestModule : RuntimeModule
        {
            public TestModule(string moduleId, RuntimeTickStage stage, int priority)
                : base(moduleId, stage, priority)
            {
            }
        }
    }
}
