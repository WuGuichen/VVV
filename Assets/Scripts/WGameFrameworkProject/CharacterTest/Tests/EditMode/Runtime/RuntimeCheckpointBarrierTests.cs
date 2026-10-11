using System;
using System.Collections.Generic;
using MxFramework.Runtime;
using NUnit.Framework;

namespace MxFramework.Tests.Runtime
{
    public sealed class RuntimeCheckpointBarrierTests
    {
        private static readonly DateTime AuthorityCreatedAtUtc =
            new DateTime(2026, 7, 19, 0, 0, 0, DateTimeKind.Utc);

        [Test]
        public void CaptureCheckpoint_BeforeTickRejectsWithoutCheckingParticipantOrProvider()
        {
            var checker = new RecordingChecker("checker", null, RuntimeCheckpointQuiescenceResult.Ready());
            var provider = new RecordingProvider(CreateState(frame: 99L));
            RuntimeHost host = CreateStartedHost();
            RuntimeCheckpointBarrier barrier = CreateBarrier(host, provider, checker);

            RuntimeSaveStateCoordinatorResult<RuntimeSaveState> result = barrier.CaptureCheckpoint();

            AssertHostFailure(
                result,
                RuntimeCheckpointErrorDetailCode.HostHasNoTickAttempt);
            Assert.AreEqual(0, checker.CheckCount);
            Assert.AreEqual(0, provider.CaptureCount);
        }

        [Test]
        public void CaptureCheckpoint_DuringTickRejectsWithoutCheckingParticipantOrProvider()
        {
            var checker = new RecordingChecker("checker", null, RuntimeCheckpointQuiescenceResult.Ready());
            var provider = new RecordingProvider(CreateState(frame: 99L));
            var probe = new CheckpointProbeModule();
            var host = new RuntimeHost();
            host.RegisterModule(probe);
            host.Initialize();
            host.Start();
            RuntimeCheckpointBarrier barrier = CreateBarrier(host, provider, checker);
            probe.Barrier = barrier;

            host.Tick(7L, 0.016d);

            AssertHostFailure(
                probe.Result,
                RuntimeCheckpointErrorDetailCode.HostTickInProgress);
            Assert.AreEqual(0, checker.CheckCount);
            Assert.AreEqual(0, provider.CaptureCount);
            Assert.IsTrue(host.LastTickAttemptSucceeded);
        }

        [Test]
        public void CaptureCheckpoint_FailedNewerTickDoesNotFallBackToEarlierSuccess()
        {
            var module = new ToggleThrowingModule();
            var host = new RuntimeHost(new RuntimeHostOptions
            {
                ErrorPolicy = RuntimeHostErrorPolicy.CollectAndContinue
            });
            host.RegisterModule(module);
            host.Initialize();
            host.Start();
            host.Tick(10L, 0.016d);
            module.ShouldThrow = true;
            host.Tick(11L, 0.016d);
            var checker = new RecordingChecker("checker", null, RuntimeCheckpointQuiescenceResult.Ready());
            var provider = new RecordingProvider(CreateState(frame: 10L));
            RuntimeCheckpointBarrier barrier = CreateBarrier(host, provider, checker);

            RuntimeSaveStateCoordinatorResult<RuntimeSaveState> result = barrier.CaptureCheckpoint();

            AssertHostFailure(
                result,
                RuntimeCheckpointErrorDetailCode.HostLastTickAttemptFailed);
            Assert.AreEqual(11L, host.LastTickAttemptFrame);
            Assert.AreEqual(0, checker.CheckCount);
            Assert.AreEqual(0, provider.CaptureCount);
        }

        [Test]
        public void QuiescenceRegistry_RejectsInvalidAndDuplicateParticipants()
        {
            var checker = new RecordingChecker("checker", null, RuntimeCheckpointQuiescenceResult.Ready());
            var registry = new RuntimeCheckpointQuiescenceRegistry();

            Assert.Throws<ArgumentNullException>(() => registry.Register((RuntimeCheckpointQuiescenceParticipant)null));
            Assert.Throws<ArgumentException>(() => new RuntimeCheckpointQuiescenceParticipant(null, 0, checker));
            Assert.Throws<ArgumentException>(() => new RuntimeCheckpointQuiescenceParticipant(string.Empty, 0, checker));
            Assert.Throws<ArgumentException>(() => new RuntimeCheckpointQuiescenceParticipant("   ", 0, checker));
            Assert.Throws<ArgumentNullException>(() => new RuntimeCheckpointQuiescenceParticipant("participant", 0, null));

            RuntimeCheckpointQuiescenceRegistryResult first = registry.Register("duplicate", 0, checker);
            RuntimeCheckpointQuiescenceRegistryResult duplicate = registry.Register("duplicate", 1, checker);

            Assert.IsTrue(first.Success);
            Assert.IsFalse(duplicate.Success);
            Assert.AreEqual("duplicate", duplicate.Error.ParticipantId);
            Assert.AreEqual(1, registry.Count);
        }

        [Test]
        public void CaptureCheckpoint_AggregatesPendingAndCheckerExceptionInStableOrder()
        {
            var calls = new List<string>();
            var registry = new RuntimeCheckpointQuiescenceRegistry();
            registry.Register("b", 10, new RecordingChecker(
                "b",
                calls,
                RuntimeCheckpointQuiescenceResult.Pending(202)));
            registry.Register("z", 20, new RecordingChecker(
                "z",
                calls,
                RuntimeCheckpointQuiescenceResult.Ready()));
            registry.Register("a", 10, new RecordingChecker(
                "a",
                calls,
                RuntimeCheckpointQuiescenceResult.Ready(),
                new InvalidOperationException("checker failed")));
            registry.Register("c", 5, new RecordingChecker(
                "c",
                calls,
                RuntimeCheckpointQuiescenceResult.Pending(101)));
            var provider = new RecordingProvider(CreateState(frame: 99L));
            RuntimeHost host = CreateStartedHost();
            host.Tick(31L, 0.016d);
            RuntimeCheckpointBarrier barrier = CreateBarrier(host, provider, registry);

            RuntimeSaveStateCoordinatorResult<RuntimeSaveState> result = barrier.CaptureCheckpoint();

            Assert.IsFalse(result.Success);
            Assert.IsNull(result.Value);
            CollectionAssert.AreEqual(new[] { "c", "a", "b", "z" }, calls);
            Assert.AreEqual(3, result.Errors.Count);
            AssertCheckpointError(result.Errors[0], "c", RuntimeSaveStateErrorCode.PendingWork, 101);
            AssertCheckpointError(
                result.Errors[1],
                "a",
                RuntimeSaveStateErrorCode.InvalidDocument,
                (int)RuntimeCheckpointErrorDetailCode.QuiescenceCheckException);
            Assert.IsInstanceOf<InvalidOperationException>(result.Errors[1].Error.Exception);
            AssertCheckpointError(result.Errors[2], "b", RuntimeSaveStateErrorCode.PendingWork, 202);
            Assert.AreEqual(0, provider.CaptureCount);
        }

        [Test]
        public void CaptureCheckpoint_DefaultQuiescenceResultFailsClosedWithoutCallingProvider()
        {
            var checker = new RecordingChecker("default", null, default);
            var provider = new RecordingProvider(CreateState(frame: 99L));
            RuntimeHost host = CreateStartedHost();
            host.Tick(4L, 0.016d);
            RuntimeCheckpointBarrier barrier = CreateBarrier(host, provider, checker);

            RuntimeSaveStateCoordinatorResult<RuntimeSaveState> result = barrier.CaptureCheckpoint();

            Assert.IsFalse(result.Success);
            Assert.IsNull(result.Value);
            Assert.AreEqual(1, checker.CheckCount);
            Assert.AreEqual(4L, checker.LastCompletedFrame);
            Assert.AreEqual(0, provider.CaptureCount);
            Assert.AreEqual(1, result.Errors.Count);
            AssertCheckpointError(
                result.Errors[0],
                "checker",
                RuntimeSaveStateErrorCode.InvalidDocument,
                (int)RuntimeCheckpointErrorDetailCode.InvalidQuiescenceResult);
        }

        [Test]
        public void CaptureCheckpoint_NormalizesOnlyRootFrameAndAllowsRepeatedSlots()
        {
            RuntimeSaveState providerState = CreateState(
                frame: 999L,
                schemaVersion: 7,
                frameworkVersion: "legacy-framework",
                configVersion: "legacy-config",
                resourceCatalogVersion: "legacy-resources");
            var provider = new RecordingProvider(providerState);
            var calls = new List<string>();
            var registry = new RuntimeCheckpointQuiescenceRegistry();
            registry.Register("b", 10, new RecordingChecker(
                "b",
                calls,
                RuntimeCheckpointQuiescenceResult.Ready()));
            registry.Register("a", 10, new RecordingChecker(
                "a",
                calls,
                RuntimeCheckpointQuiescenceResult.Ready()));
            RuntimeHost host = CreateStartedHost();
            host.Tick(42L, 0.016d);
            RuntimeCheckpointBarrier barrier = CreateBarrier(host, provider, registry);

            RuntimeSaveStateCoordinatorResult<RuntimeSaveState> first = barrier.CaptureCheckpoint();
            RuntimeSaveStateCoordinatorResult<RuntimeSaveState> second = barrier.CaptureCheckpoint();

            Assert.IsTrue(first.Success, FirstError(first));
            Assert.IsTrue(second.Success, FirstError(second));
            Assert.AreEqual(42L, first.Value.Frame);
            Assert.AreEqual(42L, second.Value.Frame);
            Assert.AreEqual(RuntimeSaveState.CurrentSchemaVersion, first.Value.SchemaVersion);
            Assert.AreEqual(AuthorityCreatedAtUtc, first.Value.CreatedAtUtc);
            Assert.AreEqual("0.3", first.Value.FrameworkVersion);
            Assert.AreEqual("config-a", first.Value.ConfigVersion);
            Assert.AreEqual("resources-a", first.Value.ResourceCatalogVersion);
            Assert.AreEqual(providerState.Entities[0].EntityId, first.Value.Entities[0].EntityId);
            Assert.AreEqual(providerState.GlobalCounters[0].Value, first.Value.GlobalCounters[0].Value);
            Assert.AreEqual(providerState.ModuleStates[0].ModuleId, first.Value.ModuleStates[0].ModuleId);
            Assert.AreEqual(providerState.Metadata["slot"], first.Value.Metadata["slot"]);
            Assert.AreEqual(2, provider.CaptureCount);
            CollectionAssert.AreEqual(new[] { "a", "b", "a", "b" }, calls);
        }

        [Test]
        public void AuthoritativeFactoryFailure_PreventsBarrierAndProviderSideEffects()
        {
            var provider = new RecordingProvider(CreateState(frame: 99L));
            var registry = new RuntimeSaveStateRegistry();
            Assert.IsTrue(registry.Register("provider", 10, provider, null).Success);

            RuntimeSaveStateCoordinatorFactoryResult factory =
                RuntimeSaveStateCoordinator.CreateAuthoritative(registry);

            Assert.IsFalse(factory.Success);
            Assert.IsNull(factory.Coordinator);
            Assert.IsNotNull(factory.Error);
            Assert.AreEqual(RuntimeSaveStateCoordinatorPhase.Composition, factory.Error.Phase);
            Assert.AreEqual(0, provider.CaptureCount);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void CaptureCheckpoint_AuthorityFailureHidesValueAndSkipsBodyProvider(bool throwFromAuthority)
        {
            var authority = new RecordingAuthority(
                CreateAuthorityState(),
                new RuntimeSaveStateError(
                    RuntimeSaveStateErrorCode.MissingConfig,
                    "$.configVersion",
                    "authority failed"),
                throwFromAuthority);
            var provider = new RecordingProvider(CreateState(frame: 99L));
            var saveRegistry = new RuntimeSaveStateRegistry();
            Assert.IsTrue(saveRegistry.Register(
                RuntimeSaveStateRootHeaderProvider.ParticipantId,
                RuntimeSaveStateRootHeaderProvider.ParticipantOrder,
                authority,
                null).Success);
            Assert.IsTrue(saveRegistry.Register("provider", 10, provider, null).Success);
            RuntimeSaveStateCoordinatorFactoryResult factory =
                RuntimeSaveStateCoordinator.CreateAuthoritative(saveRegistry);
            Assert.IsTrue(factory.Success, factory.Error?.ToString());
            RuntimeHost host = CreateStartedHost();
            host.Tick(8L, 0.016d);
            var barrier = new RuntimeCheckpointBarrier(
                host,
                factory.Coordinator,
                new RuntimeCheckpointQuiescenceRegistry());

            RuntimeSaveStateCoordinatorResult<RuntimeSaveState> result = barrier.CaptureCheckpoint();

            Assert.IsFalse(result.Success);
            Assert.IsNull(result.Value);
            Assert.AreEqual(1, result.Errors.Count);
            Assert.AreEqual(RuntimeSaveStateRootHeaderProvider.ParticipantId, result.FirstError.ParticipantId);
            Assert.AreEqual(RuntimeSaveStateCoordinatorPhase.Capture, result.FirstError.Phase);
            Assert.AreEqual(
                throwFromAuthority ? RuntimeSaveStateErrorCode.InvalidDocument : RuntimeSaveStateErrorCode.MissingConfig,
                result.FirstError.Error.Code);
            Assert.AreEqual(1, authority.CaptureCount);
            Assert.AreEqual(0, provider.CaptureCount);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void CaptureCheckpoint_CoordinatorFailurePreservesErrorsButHidesPartialValue(bool throwFromProvider)
        {
            var expected = new RuntimeSaveStateError(
                RuntimeSaveStateErrorCode.MissingConfig,
                "$.configVersion",
                "missing config");
            var provider = new RecordingProvider(CreateState(frame: 999L), expected, throwFromProvider);
            RuntimeHost host = CreateStartedHost();
            host.Tick(5L, 0.016d);
            RuntimeCheckpointBarrier barrier = CreateBarrier(
                host,
                provider,
                new RecordingChecker("ready", null, RuntimeCheckpointQuiescenceResult.Ready()));

            RuntimeSaveStateCoordinatorResult<RuntimeSaveState> result = barrier.CaptureCheckpoint();

            Assert.IsFalse(result.Success);
            Assert.IsNull(result.Value);
            Assert.AreEqual(1, provider.CaptureCount);
            Assert.AreEqual(1, result.Errors.Count);
            Assert.AreEqual(RuntimeSaveStateCoordinatorPhase.Capture, result.Errors[0].Phase);
            if (throwFromProvider)
            {
                Assert.AreEqual(RuntimeSaveStateErrorCode.InvalidDocument, result.Errors[0].Error.Code);
                Assert.IsInstanceOf<InvalidOperationException>(result.Errors[0].Error.Exception);
            }
            else
            {
                Assert.AreSame(expected, result.Errors[0].Error);
            }
        }

        [Test]
        public void RuntimeSaveStateError_PreservesLegacyConstructorAndAddsConditionalDetailText()
        {
            var legacy = new RuntimeSaveStateError(
                RuntimeSaveStateErrorCode.InvalidDocument,
                "$.value",
                "legacy",
                1,
                2,
                null);
            var detailed = new RuntimeSaveStateError(
                RuntimeSaveStateErrorCode.PendingWork,
                "$.checkpoint",
                "pending",
                -1,
                -1,
                null,
                77);

            Assert.AreEqual(0, legacy.DetailCode);
            Assert.AreEqual(
                "InvalidDocument Path=$.value SourceSchema=1 TargetSchema=2 legacy",
                legacy.ToString());
            Assert.AreEqual(77, detailed.DetailCode);
            Assert.AreEqual(
                "PendingWork Path=$.checkpoint SourceSchema=-1 TargetSchema=-1 Detail=77 pending",
                detailed.ToString());
        }

        [Test]
        public void RuntimeCheckpointContracts_RejectInvalidNumericInputs()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new RuntimeCheckpointContext(-1L));
            Assert.Throws<ArgumentOutOfRangeException>(() => RuntimeCheckpointQuiescenceResult.Pending(0));
            Assert.Throws<ArgumentOutOfRangeException>(() => RuntimeCheckpointQuiescenceResult.Pending(-1));

            RuntimeHost host = CreateStartedHost();
            var coordinator = new RuntimeSaveStateCoordinator(new RuntimeSaveStateRegistry());
            var quiescence = new RuntimeCheckpointQuiescenceRegistry();
            Assert.Throws<ArgumentNullException>(() => new RuntimeCheckpointBarrier(null, coordinator, quiescence));
            Assert.Throws<ArgumentNullException>(() => new RuntimeCheckpointBarrier(host, null, quiescence));
            Assert.Throws<ArgumentNullException>(() => new RuntimeCheckpointBarrier(host, coordinator, null));
        }

        [Test]
        public void QuiescenceReadyCheck_DoesNotAllocateAfterWarmup()
        {
            var checker = new ReadyChecker();
            var context = new RuntimeCheckpointContext(1L);
            checker.CheckQuiescence(in context);

            _ = GC.GetAllocatedBytesForCurrentThread();
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 128; i++)
            {
                RuntimeCheckpointQuiescenceResult result = checker.CheckQuiescence(in context);
                if (!result.IsReady)
                {
                    throw new InvalidOperationException("Unexpected pending result.");
                }
            }

            long allocatedBytes = GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.AreEqual(0L, allocatedBytes);
            Assert.AreEqual(129, checker.CheckCount);
        }

        private static RuntimeHost CreateStartedHost()
        {
            var host = new RuntimeHost();
            host.Initialize();
            host.Start();
            return host;
        }

        private static RuntimeCheckpointBarrier CreateBarrier(
            RuntimeHost host,
            IRuntimeSaveStateProvider provider,
            IRuntimeCheckpointQuiescenceChecker checker)
        {
            var registry = new RuntimeCheckpointQuiescenceRegistry();
            registry.Register("checker", 0, checker);
            return CreateBarrier(host, provider, registry);
        }

        private static RuntimeCheckpointBarrier CreateBarrier(
            RuntimeHost host,
            IRuntimeSaveStateProvider provider,
            RuntimeCheckpointQuiescenceRegistry quiescenceRegistry)
        {
            var saveStateRegistry = new RuntimeSaveStateRegistry();
            saveStateRegistry.Register(
                RuntimeSaveStateRootHeaderProvider.ParticipantId,
                RuntimeSaveStateRootHeaderProvider.ParticipantOrder,
                new RecordingAuthority(CreateAuthorityState()),
                null);
            saveStateRegistry.Register("provider", 10, provider, null);
            RuntimeSaveStateCoordinatorFactoryResult factory =
                RuntimeSaveStateCoordinator.CreateAuthoritative(saveStateRegistry);
            Assert.IsTrue(factory.Success, factory.Error?.ToString());
            return new RuntimeCheckpointBarrier(
                host,
                factory.Coordinator,
                quiescenceRegistry);
        }

        private static RuntimeSaveState CreateAuthorityState()
        {
            return new RuntimeSaveState(
                RuntimeSaveState.CurrentSchemaVersion,
                AuthorityCreatedAtUtc,
                "0.3",
                "config-a",
                "resources-a",
                0L,
                null,
                null,
                null,
                null);
        }

        private static RuntimeSaveState CreateState(
            long frame,
            int schemaVersion = RuntimeSaveState.CurrentSchemaVersion,
            string frameworkVersion = "0.3",
            string configVersion = "config-a",
            string resourceCatalogVersion = "resources-a")
        {
            return new RuntimeSaveState(
                schemaVersion,
                new DateTime(2026, 7, 19, 1, 2, 3, DateTimeKind.Utc),
                frameworkVersion,
                configVersion,
                resourceCatalogVersion,
                frame,
                new[]
                {
                    new RuntimeEntitySaveState(10, 20, 1, true, null, null, null, null)
                },
                new[] { new RuntimeCounterSaveState(30, 40) },
                new[]
                {
                    new RuntimeModuleSaveState(
                        "module-a",
                        0,
                        new RuntimeCustomState(TestCustomStateTypeIds.ModuleA, "module-a.state", 0, "{}"))
                },
                new Dictionary<string, string> { { "slot", "auto" } });
        }

        private static void AssertHostFailure(
            RuntimeSaveStateCoordinatorResult<RuntimeSaveState> result,
            RuntimeCheckpointErrorDetailCode detailCode)
        {
            Assert.IsFalse(result.Success);
            Assert.IsNull(result.Value);
            Assert.AreEqual(1, result.Errors.Count);
            AssertCheckpointError(
                result.Errors[0],
                "mxframework.runtime.host",
                RuntimeSaveStateErrorCode.PendingWork,
                (int)detailCode);
        }

        private static void AssertCheckpointError(
            RuntimeSaveStateParticipantError participantError,
            string participantId,
            RuntimeSaveStateErrorCode errorCode,
            int detailCode)
        {
            Assert.AreEqual(participantId, participantError.ParticipantId);
            Assert.AreEqual(RuntimeSaveStateCoordinatorPhase.Checkpoint, participantError.Phase);
            Assert.AreEqual(errorCode, participantError.Error.Code);
            Assert.AreEqual(detailCode, participantError.Error.DetailCode);
        }

        private static string FirstError(RuntimeSaveStateCoordinatorResult<RuntimeSaveState> result)
        {
            return result.FirstError != null ? result.FirstError.ToString() : string.Empty;
        }

        private sealed class RecordingChecker : IRuntimeCheckpointQuiescenceChecker
        {
            private readonly string _name;
            private readonly List<string> _calls;
            private readonly RuntimeCheckpointQuiescenceResult _result;
            private readonly Exception _exception;

            public RecordingChecker(
                string name,
                List<string> calls,
                RuntimeCheckpointQuiescenceResult result,
                Exception exception = null)
            {
                _name = name;
                _calls = calls;
                _result = result;
                _exception = exception;
            }

            public int CheckCount { get; private set; }
            public long LastCompletedFrame { get; private set; }

            public RuntimeCheckpointQuiescenceResult CheckQuiescence(in RuntimeCheckpointContext context)
            {
                CheckCount++;
                LastCompletedFrame = context.CompletedFrame;
                _calls?.Add(_name);
                if (_exception != null)
                {
                    throw _exception;
                }

                return _result;
            }
        }

        private sealed class ReadyChecker : IRuntimeCheckpointQuiescenceChecker
        {
            public int CheckCount { get; private set; }

            public RuntimeCheckpointQuiescenceResult CheckQuiescence(in RuntimeCheckpointContext context)
            {
                CheckCount++;
                return RuntimeCheckpointQuiescenceResult.Ready();
            }
        }

        private sealed class RecordingProvider : IRuntimeSaveStateProvider
        {
            private readonly RuntimeSaveState _state;
            private readonly RuntimeSaveStateError _error;
            private readonly bool _throwOnCapture;

            public RecordingProvider(
                RuntimeSaveState state,
                RuntimeSaveStateError error = null,
                bool throwOnCapture = false)
            {
                _state = state;
                _error = error;
                _throwOnCapture = throwOnCapture;
            }

            public int CaptureCount { get; private set; }

            public RuntimeSaveStateResult<RuntimeSaveState> CaptureSaveState()
            {
                CaptureCount++;
                if (_throwOnCapture)
                {
                    throw new InvalidOperationException("provider failed");
                }

                return _error != null
                    ? RuntimeSaveStateResult<RuntimeSaveState>.Failed(_error)
                    : RuntimeSaveStateResult<RuntimeSaveState>.Succeeded(_state);
            }
        }

        private sealed class RecordingAuthority : IRuntimeSaveStateRootHeaderAuthority
        {
            private readonly RuntimeSaveState _state;
            private readonly RuntimeSaveStateError _error;
            private readonly bool _throwOnCapture;

            public RecordingAuthority(
                RuntimeSaveState state,
                RuntimeSaveStateError error = null,
                bool throwOnCapture = false)
            {
                _state = state;
                _error = error;
                _throwOnCapture = throwOnCapture;
                Descriptor = new RuntimeSaveStateRootHeaderDescriptor(
                    state.SchemaVersion,
                    state.FrameworkVersion,
                    state.ConfigVersion,
                    state.ResourceCatalogVersion);
            }

            public RuntimeSaveStateRootHeaderDescriptor Descriptor { get; }
            public int CaptureCount { get; private set; }

            public RuntimeSaveStateResult<RuntimeSaveState> CaptureSaveState()
            {
                CaptureCount++;
                if (_throwOnCapture)
                {
                    throw new InvalidOperationException("authority failed");
                }

                return _error != null
                    ? RuntimeSaveStateResult<RuntimeSaveState>.Failed(_error)
                    : RuntimeSaveStateResult<RuntimeSaveState>.Succeeded(_state);
            }
        }

        private sealed class CheckpointProbeModule : RuntimeModule
        {
            public CheckpointProbeModule()
                : base("checkpoint-probe")
            {
            }

            public RuntimeCheckpointBarrier Barrier { get; set; }
            public RuntimeSaveStateCoordinatorResult<RuntimeSaveState> Result { get; private set; }

            public override void Tick(RuntimeTickContext context)
            {
                Result = Barrier.CaptureCheckpoint();
            }
        }

        private sealed class ToggleThrowingModule : RuntimeModule
        {
            public ToggleThrowingModule()
                : base("toggle-throw")
            {
            }

            public bool ShouldThrow { get; set; }

            public override void Tick(RuntimeTickContext context)
            {
                if (ShouldThrow)
                {
                    throw new InvalidOperationException("tick failed");
                }
            }
        }
    }
}
