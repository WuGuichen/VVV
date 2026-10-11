using System;
using System.Collections.Generic;
using MxFramework.Runtime;
using NUnit.Framework;

namespace MxFramework.Tests.Runtime
{
    public class RuntimeHostTests
    {
        private static readonly int[] LifecycleRegistrationOrder = { 2, 0, 1 };

        [Test]
        public void TickAttemptState_HasExplicitInitialAndSuccessfulValues()
        {
            var host = new RuntimeHost();

            Assert.IsFalse(host.IsTickInProgress);
            Assert.IsFalse(host.HasTickAttempt);
            Assert.AreEqual(-1L, host.LastTickAttemptFrame);
            Assert.IsFalse(host.LastTickAttemptSucceeded);

            host.Initialize();
            host.Start();
            host.Tick(12L, 0.016d, 0.192d);

            Assert.IsFalse(host.IsTickInProgress);
            Assert.IsTrue(host.HasTickAttempt);
            Assert.AreEqual(12L, host.LastTickAttemptFrame);
            Assert.IsTrue(host.LastTickAttemptSucceeded);
        }

        [Test]
        public void Tick_UsesStableStagePriorityAndModuleIdOrder()
        {
            var calls = new List<string>();
            var host = new RuntimeHost();
            host.RegisterModule(new RecordingModule("z-sim", calls, RuntimeTickStage.Simulation, 0));
            host.RegisterModule(new RecordingModule("b-pre", calls, RuntimeTickStage.PreSimulation, 10));
            host.RegisterModule(new RecordingModule("a-pre", calls, RuntimeTickStage.PreSimulation, 10));
            host.RegisterModule(new RecordingModule("diag", calls, RuntimeTickStage.Diagnostics, -100));
            host.RegisterModule(new RecordingModule("post", calls, RuntimeTickStage.PostSimulation, 0));
            host.RegisterModule(new RecordingModule("a-sim-fast", calls, RuntimeTickStage.Simulation, -1));

            host.Initialize();
            host.Start();
            calls.Clear();

            host.Tick(3, 0.016d, 0.048d);

            CollectionAssert.AreEqual(
                new[]
                {
                    "tick:a-pre:PreSimulation",
                    "tick:b-pre:PreSimulation",
                    "tick:a-sim-fast:Simulation",
                    "tick:z-sim:Simulation",
                    "tick:post:PostSimulation",
                    "tick:diag:Diagnostics"
                },
                calls);
        }

        [Test]
        public void Tick_DoesNotAllocateForNoOpModulesAfterWarmup()
        {
            var host = new RuntimeHost();
            for (int i = 0; i < 13; i++)
            {
                host.RegisterModule(new NoOpModule("noop." + i));
            }

            host.Initialize();
            host.Start();
            host.Tick(0, 0.016d, 0.016d);

            _ = GC.GetAllocatedBytesForCurrentThread();
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 128; i++)
            {
                host.Tick(i + 1, 0.016d, (i + 2) * 0.016d);
            }

            long allocatedBytes = GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.AreEqual(0L, allocatedBytes);
            Assert.AreEqual(129, host.TickCount);
            Assert.AreEqual(128L, host.LastTickAttemptFrame);
            Assert.IsTrue(host.LastTickAttemptSucceeded);
            Assert.IsFalse(host.IsTickInProgress);
        }

        [Test]
        public void RegisterModule_RejectsDuplicateModuleId()
        {
            var host = new RuntimeHost();
            host.RegisterModule(new RecordingModule("same", new List<string>()));

            Assert.Throws<InvalidOperationException>(() => host.RegisterModule(new RecordingModule("same", new List<string>())));
        }

        [Test]
        public void StartBeforeInitialize_ThrowsWithCurrentState()
        {
            var host = new RuntimeHost();

            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => host.Start());

            StringAssert.Contains("Created", exception.Message);
            StringAssert.Contains("Initialized", exception.Message);
        }

        [Test]
        public void TickAfterDispose_ThrowsWithCurrentState()
        {
            var host = new RuntimeHost();
            host.Initialize();
            host.Start();
            host.Dispose();

            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => host.Tick(0, 0d));

            StringAssert.Contains("Disposed", exception.Message);
            StringAssert.Contains("Started", exception.Message);
        }

        [Test]
        public void StopAndDispose_AreIdempotent()
        {
            var calls = new List<string>();
            var host = new RuntimeHost();
            var module = new CountingModule("module", calls);
            host.RegisterModule(module);
            host.Initialize();
            host.Start();

            host.Stop();
            host.Stop();
            host.Dispose();
            host.Dispose();

            Assert.AreEqual(RuntimeLifecycleState.Disposed, host.State);
            Assert.AreEqual(1, module.StopCount);
            Assert.AreEqual(1, module.DisposeCount);
            CollectionAssert.AreEqual(
                new[]
                {
                    "initialize:module",
                    "start:module",
                    "stop:module",
                    "dispose:module"
                },
                calls);
        }

        [Test]
        public void TickException_FailFastCollectsErrorAndThrows()
        {
            var calls = new List<string>();
            var host = CreateHost(RuntimeHostErrorPolicy.FailFast, calls);

            RuntimeHostException exception = Assert.Throws<RuntimeHostException>(() => host.Tick(7, 0.02d, 1d));

            Assert.AreEqual("bad", exception.Error.ModuleId);
            Assert.AreEqual(RuntimeHostOperation.Tick, exception.Error.Operation);
            Assert.AreEqual(RuntimeTickStage.Simulation, exception.Error.TickStage);
            Assert.AreEqual(7, exception.Error.FrameIndex);
            Assert.AreEqual(1, host.Errors.Count);
            Assert.AreEqual(0, host.TickCount);
            Assert.IsTrue(host.HasTickAttempt);
            Assert.AreEqual(7L, host.LastTickAttemptFrame);
            Assert.IsFalse(host.LastTickAttemptSucceeded);
            Assert.IsFalse(host.IsTickInProgress);
            CollectionAssert.AreEqual(new[] { "tick:before:Simulation", "tick:bad" }, calls);
        }

        [Test]
        public void TickException_CollectAndStopFrameStopsRemainingModules()
        {
            var calls = new List<string>();
            var host = CreateHost(RuntimeHostErrorPolicy.CollectAndStopFrame, calls);

            host.Tick(7, 0.02d, 1d);

            Assert.AreEqual(1, host.Errors.Count);
            Assert.AreEqual(1, host.TickCount);
            Assert.AreEqual(7L, host.LastTickAttemptFrame);
            Assert.IsFalse(host.LastTickAttemptSucceeded);
            Assert.IsFalse(host.IsTickInProgress);
            CollectionAssert.AreEqual(new[] { "tick:before:Simulation", "tick:bad" }, calls);
        }

        [Test]
        public void TickException_CollectAndContinueTicksRemainingModules()
        {
            var calls = new List<string>();
            var host = CreateHost(RuntimeHostErrorPolicy.CollectAndContinue, calls);

            host.Tick(7, 0.02d, 1d);

            Assert.AreEqual(1, host.Errors.Count);
            Assert.AreEqual(1, host.TickCount);
            Assert.AreEqual(7L, host.LastTickAttemptFrame);
            Assert.IsFalse(host.LastTickAttemptSucceeded);
            Assert.IsFalse(host.IsTickInProgress);
            CollectionAssert.AreEqual(new[] { "tick:before:Simulation", "tick:bad", "tick:after:Simulation" }, calls);
        }

        [TestCase(RuntimeHostErrorPolicy.FailFast)]
        [TestCase(RuntimeHostErrorPolicy.CollectAndStopFrame)]
        [TestCase(RuntimeHostErrorPolicy.CollectAndContinue)]
        public void TickAttemptState_FailedNewerAttemptReplacesEarlierSuccessfulFrame(RuntimeHostErrorPolicy policy)
        {
            var module = new ToggleThrowingModule("toggle");
            var host = new RuntimeHost(new RuntimeHostOptions { ErrorPolicy = policy });
            host.RegisterModule(module);
            host.Initialize();
            host.Start();
            host.Tick(20L, 0.016d);
            module.ShouldThrow = true;

            if (policy == RuntimeHostErrorPolicy.FailFast)
            {
                Assert.Throws<RuntimeHostException>(() => host.Tick(21L, 0.016d));
            }
            else
            {
                host.Tick(21L, 0.016d);
            }

            Assert.IsTrue(host.HasTickAttempt);
            Assert.AreEqual(21L, host.LastTickAttemptFrame);
            Assert.IsFalse(host.LastTickAttemptSucceeded);
            Assert.IsFalse(host.IsTickInProgress);
            Assert.AreEqual(policy == RuntimeHostErrorPolicy.FailFast ? 1L : 2L, host.TickCount);
        }

        [Test]
        public void TickAttemptState_NewSuccessIsNotBlockedByHistoricalErrors()
        {
            var module = new ToggleThrowingModule("toggle");
            var host = new RuntimeHost(new RuntimeHostOptions
            {
                ErrorPolicy = RuntimeHostErrorPolicy.CollectAndContinue
            });
            host.RegisterModule(module);
            host.Initialize();
            host.Start();
            module.ShouldThrow = true;
            host.Tick(1L, 0.016d);
            module.ShouldThrow = false;

            host.Tick(2L, 0.016d);

            Assert.AreEqual(1, host.Errors.Count);
            Assert.AreEqual(2L, host.LastTickAttemptFrame);
            Assert.IsTrue(host.LastTickAttemptSucceeded);
            Assert.IsFalse(host.IsTickInProgress);
        }

        [TestCase(RuntimeHostErrorPolicy.FailFast, 0)]
        [TestCase(RuntimeHostErrorPolicy.FailFast, 1)]
        [TestCase(RuntimeHostErrorPolicy.FailFast, 2)]
        [TestCase(RuntimeHostErrorPolicy.CollectAndStopFrame, 0)]
        [TestCase(RuntimeHostErrorPolicy.CollectAndStopFrame, 1)]
        [TestCase(RuntimeHostErrorPolicy.CollectAndStopFrame, 2)]
        [TestCase(RuntimeHostErrorPolicy.CollectAndContinue, 0)]
        [TestCase(RuntimeHostErrorPolicy.CollectAndContinue, 1)]
        [TestCase(RuntimeHostErrorPolicy.CollectAndContinue, 2)]
        public void InitializeFault_UnwindsEnteredThenDisposesUnenteredOwnership(
            RuntimeHostErrorPolicy policy,
            int faultIndex)
        {
            var calls = new List<string>();
            LifecycleFaultModule[] modules = CreateLifecycleModules(calls);
            modules[faultIndex].ThrowInitialize = true;
            RuntimeHost host = CreateLifecycleHost(policy, modules);

            RuntimeHostException exception = InvokeLifecycle(host.Initialize, policy);

            int enteredCount = policy == RuntimeHostErrorPolicy.CollectAndContinue
                ? modules.Length
                : faultIndex + 1;
            var expected = new List<string>();
            for (int i = 0; i < enteredCount; i++)
            {
                expected.Add("initialize:m" + i);
            }

            for (int i = enteredCount - 1; i >= 0; i--)
            {
                expected.Add("dispose:m" + i);
            }

            for (int i = LifecycleRegistrationOrder.Length - 1; i >= 0; i--)
            {
                int moduleIndex = LifecycleRegistrationOrder[i];
                if (moduleIndex >= enteredCount)
                {
                    expected.Add("dispose:m" + moduleIndex);
                }
            }

            AssertLifecycleRootError(host, exception, RuntimeHostOperation.Initialize, faultIndex);
            CollectionAssert.AreEqual(expected, calls);
            Assert.AreEqual(RuntimeLifecycleState.Faulted, host.State);
            AssertNoResidualOwnership(modules);
            for (int i = 0; i < modules.Length; i++)
            {
                Assert.AreEqual(1, modules[i].DisposeCount);
            }
        }

        [TestCase(RuntimeHostErrorPolicy.FailFast, 0)]
        [TestCase(RuntimeHostErrorPolicy.FailFast, 1)]
        [TestCase(RuntimeHostErrorPolicy.FailFast, 2)]
        [TestCase(RuntimeHostErrorPolicy.CollectAndStopFrame, 0)]
        [TestCase(RuntimeHostErrorPolicy.CollectAndStopFrame, 1)]
        [TestCase(RuntimeHostErrorPolicy.CollectAndStopFrame, 2)]
        [TestCase(RuntimeHostErrorPolicy.CollectAndContinue, 0)]
        [TestCase(RuntimeHostErrorPolicy.CollectAndContinue, 1)]
        [TestCase(RuntimeHostErrorPolicy.CollectAndContinue, 2)]
        public void StartFault_StopsEnteredAndDisposesInitializedModulesInReverseEntryOrder(
            RuntimeHostErrorPolicy policy,
            int faultIndex)
        {
            var calls = new List<string>();
            LifecycleFaultModule[] modules = CreateLifecycleModules(calls);
            modules[faultIndex].ThrowStart = true;
            RuntimeHost host = CreateLifecycleHost(policy, modules);
            host.Initialize();
            calls.Clear();

            RuntimeHostException exception = InvokeLifecycle(host.Start, policy);

            int enteredCount = policy == RuntimeHostErrorPolicy.CollectAndContinue
                ? modules.Length
                : faultIndex + 1;
            var expected = new List<string>();
            for (int i = 0; i < enteredCount; i++)
            {
                expected.Add("start:m" + i);
            }

            for (int i = enteredCount - 1; i >= 0; i--)
            {
                expected.Add("stop:m" + i);
            }

            for (int i = modules.Length - 1; i >= 0; i--)
            {
                expected.Add("dispose:m" + i);
            }

            AssertLifecycleRootError(host, exception, RuntimeHostOperation.Start, faultIndex);
            CollectionAssert.AreEqual(expected, calls);
            Assert.AreEqual(RuntimeLifecycleState.Faulted, host.State);
            AssertNoResidualOwnership(modules);
        }

        [TestCase(RuntimeHostErrorPolicy.FailFast, 0)]
        [TestCase(RuntimeHostErrorPolicy.FailFast, 1)]
        [TestCase(RuntimeHostErrorPolicy.FailFast, 2)]
        [TestCase(RuntimeHostErrorPolicy.CollectAndStopFrame, 0)]
        [TestCase(RuntimeHostErrorPolicy.CollectAndStopFrame, 1)]
        [TestCase(RuntimeHostErrorPolicy.CollectAndStopFrame, 2)]
        [TestCase(RuntimeHostErrorPolicy.CollectAndContinue, 0)]
        [TestCase(RuntimeHostErrorPolicy.CollectAndContinue, 1)]
        [TestCase(RuntimeHostErrorPolicy.CollectAndContinue, 2)]
        public void StopFault_AttemptsEveryEnteredModuleBeforeReporting(
            RuntimeHostErrorPolicy policy,
            int faultIndex)
        {
            var calls = new List<string>();
            LifecycleFaultModule[] modules = CreateLifecycleModules(calls);
            RuntimeHost host = CreateLifecycleHost(policy, modules);
            host.Initialize();
            host.Start();
            calls.Clear();
            modules[faultIndex].ThrowStop = true;

            RuntimeHostException exception = InvokeLifecycle(host.Stop, policy);

            AssertLifecycleRootError(host, exception, RuntimeHostOperation.Stop, faultIndex);
            CollectionAssert.AreEqual(new[] { "stop:m2", "stop:m1", "stop:m0" }, calls);
            Assert.AreEqual(RuntimeLifecycleState.Faulted, host.State);
            for (int i = 0; i < modules.Length; i++)
            {
                Assert.AreEqual(1, modules[i].StopCount);
                Assert.IsFalse(modules[i].StartSideEffectActive);
            }

            host.Dispose();

            Assert.AreEqual(RuntimeLifecycleState.Disposed, host.State);
            AssertNoResidualOwnership(modules);
        }

        [TestCase(RuntimeHostErrorPolicy.FailFast, 0)]
        [TestCase(RuntimeHostErrorPolicy.FailFast, 1)]
        [TestCase(RuntimeHostErrorPolicy.FailFast, 2)]
        [TestCase(RuntimeHostErrorPolicy.CollectAndStopFrame, 0)]
        [TestCase(RuntimeHostErrorPolicy.CollectAndStopFrame, 1)]
        [TestCase(RuntimeHostErrorPolicy.CollectAndStopFrame, 2)]
        [TestCase(RuntimeHostErrorPolicy.CollectAndContinue, 0)]
        [TestCase(RuntimeHostErrorPolicy.CollectAndContinue, 1)]
        [TestCase(RuntimeHostErrorPolicy.CollectAndContinue, 2)]
        public void DisposeFault_AttemptsEveryOwnedModuleAndRemainsIdempotent(
            RuntimeHostErrorPolicy policy,
            int faultIndex)
        {
            var calls = new List<string>();
            LifecycleFaultModule[] modules = CreateLifecycleModules(calls);
            RuntimeHost host = CreateLifecycleHost(policy, modules);
            host.Initialize();
            host.Start();
            host.Stop();
            calls.Clear();
            modules[faultIndex].ThrowDispose = true;

            RuntimeHostException exception = InvokeLifecycle(host.Dispose, policy);

            AssertLifecycleRootError(host, exception, RuntimeHostOperation.Dispose, faultIndex);
            CollectionAssert.AreEqual(new[] { "dispose:m2", "dispose:m1", "dispose:m0" }, calls);
            Assert.AreEqual(RuntimeLifecycleState.Disposed, host.State);
            AssertNoResidualOwnership(modules);

            host.Dispose();

            Assert.AreEqual(3, calls.Count);
            Assert.AreEqual(1, host.Errors.Count);
            for (int i = 0; i < modules.Length; i++)
            {
                Assert.AreEqual(1, modules[i].DisposeCount);
            }
        }

        [Test]
        public void CollectAndContinue_StartFaultsPreserveForwardThenCleanupErrorOrder()
        {
            var calls = new List<string>();
            LifecycleFaultModule[] modules = CreateLifecycleModules(calls);
            modules[0].ThrowStart = true;
            modules[2].ThrowStart = true;
            RuntimeHost host = CreateLifecycleHost(RuntimeHostErrorPolicy.CollectAndContinue, modules);
            host.Initialize();
            calls.Clear();

            host.Start();

            CollectionAssert.AreEqual(
                new[]
                {
                    "start:m0", "start:m1", "start:m2",
                    "stop:m2", "stop:m1", "stop:m0",
                    "dispose:m2", "dispose:m1", "dispose:m0"
                },
                calls);
            Assert.AreEqual(2, host.Errors.Count);
            Assert.AreEqual("m0", host.Errors[0].ModuleId);
            Assert.AreEqual("m2", host.Errors[1].ModuleId);
            Assert.AreEqual(RuntimeLifecycleState.Faulted, host.State);
            AssertNoResidualOwnership(modules);
        }

        [Test]
        public void FailFast_StartFaultRetainsRootAndAggregatesAllCleanupFaults()
        {
            var calls = new List<string>();
            LifecycleFaultModule[] modules = CreateLifecycleModules(calls);
            modules[1].ThrowStart = true;
            modules[0].ThrowStop = true;
            modules[1].ThrowStop = true;
            modules[0].ThrowDispose = true;
            modules[2].ThrowDispose = true;
            RuntimeHost host = CreateLifecycleHost(RuntimeHostErrorPolicy.FailFast, modules);
            host.Initialize();
            calls.Clear();

            RuntimeHostException exception = Assert.Throws<RuntimeHostException>(() => host.Start());

            CollectionAssert.AreEqual(
                new[]
                {
                    "start:m0", "start:m1",
                    "stop:m1", "stop:m0",
                    "dispose:m2", "dispose:m1", "dispose:m0"
                },
                calls);
            Assert.AreSame(host.Errors[0], exception.Error);
            AssertLifecycleError(host.Errors[0], RuntimeHostOperation.Start, 1);
            AssertLifecycleError(host.Errors[1], RuntimeHostOperation.Stop, 1);
            AssertLifecycleError(host.Errors[2], RuntimeHostOperation.Stop, 0);
            AssertLifecycleError(host.Errors[3], RuntimeHostOperation.Dispose, 2);
            AssertLifecycleError(host.Errors[4], RuntimeHostOperation.Dispose, 0);
            Assert.AreEqual(RuntimeLifecycleState.Faulted, host.State);
            AssertNoResidualOwnership(modules);

            host.Dispose();
            host.Dispose();

            Assert.AreEqual(RuntimeLifecycleState.Disposed, host.State);
            Assert.AreEqual(7, calls.Count);
            Assert.AreEqual(5, host.Errors.Count);
        }

        [Test]
        public void FaultedHost_RejectsReuseButAllowsFinalIdempotentDispose()
        {
            var calls = new List<string>();
            LifecycleFaultModule[] modules = CreateLifecycleModules(calls);
            modules[0].ThrowInitialize = true;
            RuntimeHost host = CreateLifecycleHost(RuntimeHostErrorPolicy.CollectAndStopFrame, modules);
            host.Initialize();
            int callCountAfterUnwind = calls.Count;

            Assert.AreEqual(RuntimeLifecycleState.Faulted, host.State);
            Assert.Throws<InvalidOperationException>(() => host.RegisterModule(new NoOpModule("late")));
            Assert.Throws<InvalidOperationException>(() => host.Initialize());
            Assert.Throws<InvalidOperationException>(() => host.Start());
            Assert.Throws<InvalidOperationException>(() => host.Tick(1L, 0.016d));

            host.Stop();
            Assert.AreEqual(callCountAfterUnwind, calls.Count);
            Assert.AreEqual(RuntimeLifecycleState.Faulted, host.State);

            host.Dispose();
            host.Dispose();

            Assert.AreEqual(RuntimeLifecycleState.Disposed, host.State);
            Assert.AreEqual(callCountAfterUnwind, calls.Count);
            AssertNoResidualOwnership(modules);
        }

        [Test]
        public void DisposeFromCreated_UsesReverseRegistrationOrderExactlyOnce()
        {
            var calls = new List<string>();
            LifecycleFaultModule[] modules = CreateLifecycleModules(calls);
            RuntimeHost host = CreateLifecycleHost(RuntimeHostErrorPolicy.FailFast, modules);

            host.Dispose();
            host.Dispose();

            CollectionAssert.AreEqual(new[] { "dispose:m1", "dispose:m0", "dispose:m2" }, calls);
            Assert.AreEqual(RuntimeLifecycleState.Disposed, host.State);
            AssertNoResidualOwnership(modules);
        }

        [Test]
        public void CaptureDiagnostics_ReturnsCopiesOfModuleAndErrorLists()
        {
            var calls = new List<string>();
            var host = CreateHost(RuntimeHostErrorPolicy.CollectAndStopFrame, calls);
            host.Tick(1, 0.01d, 0.01d);

            RuntimeHostDiagnostics diagnostics = host.CaptureDiagnostics();

            Assert.AreEqual(RuntimeLifecycleState.Started, diagnostics.State);
            Assert.AreEqual(1, diagnostics.TickCount);
            Assert.AreEqual(3, diagnostics.Modules.Count);
            Assert.AreEqual(1, diagnostics.Errors.Count);
            Assert.AreEqual("bad", diagnostics.LastError.ModuleId);
        }

        private static RuntimeHost CreateHost(RuntimeHostErrorPolicy policy, List<string> calls)
        {
            var host = new RuntimeHost(new RuntimeHostOptions { ErrorPolicy = policy });
            host.RegisterModule(new RecordingModule("before", calls, RuntimeTickStage.Simulation, 0));
            host.RegisterModule(new ThrowingModule("bad", calls, RuntimeTickStage.Simulation, 1));
            host.RegisterModule(new RecordingModule("after", calls, RuntimeTickStage.Simulation, 2));
            host.Initialize();
            host.Start();
            calls.Clear();
            return host;
        }

        private static LifecycleFaultModule[] CreateLifecycleModules(List<string> calls)
        {
            return new[]
            {
                new LifecycleFaultModule("m0", calls, 0),
                new LifecycleFaultModule("m1", calls, 1),
                new LifecycleFaultModule("m2", calls, 2)
            };
        }

        private static RuntimeHost CreateLifecycleHost(
            RuntimeHostErrorPolicy policy,
            LifecycleFaultModule[] modules)
        {
            var host = new RuntimeHost(new RuntimeHostOptions { ErrorPolicy = policy });
            for (int i = 0; i < LifecycleRegistrationOrder.Length; i++)
            {
                host.RegisterModule(modules[LifecycleRegistrationOrder[i]]);
            }

            return host;
        }

        private static RuntimeHostException InvokeLifecycle(Action callback, RuntimeHostErrorPolicy policy)
        {
            if (policy == RuntimeHostErrorPolicy.FailFast)
            {
                return Assert.Throws<RuntimeHostException>(() => callback());
            }

            callback();
            return null;
        }

        private static void AssertLifecycleRootError(
            RuntimeHost host,
            RuntimeHostException exception,
            RuntimeHostOperation operation,
            int faultIndex)
        {
            Assert.AreEqual(1, host.Errors.Count);
            AssertLifecycleError(host.Errors[0], operation, faultIndex);
            if (host.ErrorPolicy == RuntimeHostErrorPolicy.FailFast)
            {
                Assert.IsNotNull(exception);
                Assert.AreSame(host.Errors[0], exception.Error);
            }
            else
            {
                Assert.IsNull(exception);
            }
        }

        private static void AssertLifecycleError(
            RuntimeHostError error,
            RuntimeHostOperation operation,
            int faultIndex)
        {
            Assert.AreEqual("m" + faultIndex, error.ModuleId);
            Assert.AreEqual(operation, error.Operation);
        }

        private static void AssertNoResidualOwnership(LifecycleFaultModule[] modules)
        {
            for (int i = 0; i < modules.Length; i++)
            {
                Assert.IsFalse(modules[i].InitializeSideEffectActive, modules[i].ModuleId);
                Assert.IsFalse(modules[i].StartSideEffectActive, modules[i].ModuleId);
            }
        }

        private sealed class NoOpModule : RuntimeModule
        {
            public NoOpModule(string moduleId)
                : base(moduleId)
            {
            }
        }

        private class RecordingModule : RuntimeModule
        {
            protected readonly List<string> Calls;

            public RecordingModule(
                string moduleId,
                List<string> calls,
                RuntimeTickStage tickStage = RuntimeTickStage.Simulation,
                int priority = 0)
                : base(moduleId, tickStage, priority)
            {
                Calls = calls;
            }

            public override void Initialize(RuntimeHostContext context)
            {
                Calls.Add("initialize:" + ModuleId);
            }

            public override void Start(RuntimeHostContext context)
            {
                Calls.Add("start:" + ModuleId);
            }

            public override void Tick(RuntimeTickContext context)
            {
                Calls.Add("tick:" + ModuleId + ":" + context.Stage);
            }

            public override void Stop(RuntimeHostContext context)
            {
                Calls.Add("stop:" + ModuleId);
            }

            public override void Dispose()
            {
                Calls.Add("dispose:" + ModuleId);
            }
        }

        private sealed class CountingModule : RecordingModule
        {
            public CountingModule(string moduleId, List<string> calls)
                : base(moduleId, calls)
            {
            }

            public int StopCount { get; private set; }
            public int DisposeCount { get; private set; }

            public override void Stop(RuntimeHostContext context)
            {
                StopCount++;
                base.Stop(context);
            }

            public override void Dispose()
            {
                DisposeCount++;
                base.Dispose();
            }
        }

        private sealed class ThrowingModule : RecordingModule
        {
            public ThrowingModule(string moduleId, List<string> calls, RuntimeTickStage tickStage, int priority)
                : base(moduleId, calls, tickStage, priority)
            {
            }

            public override void Tick(RuntimeTickContext context)
            {
                Calls.Add("tick:" + ModuleId);
                throw new InvalidOperationException("boom");
            }
        }

        private sealed class ToggleThrowingModule : RuntimeModule
        {
            public ToggleThrowingModule(string moduleId)
                : base(moduleId)
            {
            }

            public bool ShouldThrow { get; set; }

            public override void Tick(RuntimeTickContext context)
            {
                if (ShouldThrow)
                {
                    throw new InvalidOperationException("toggle boom");
                }
            }
        }

        private sealed class LifecycleFaultModule : RuntimeModule
        {
            private readonly List<string> _calls;

            public LifecycleFaultModule(string moduleId, List<string> calls, int priority)
                : base(moduleId, RuntimeTickStage.Simulation, priority)
            {
                _calls = calls;
            }

            public bool ThrowInitialize { get; set; }
            public bool ThrowStart { get; set; }
            public bool ThrowStop { get; set; }
            public bool ThrowDispose { get; set; }
            public bool InitializeSideEffectActive { get; private set; }
            public bool StartSideEffectActive { get; private set; }
            public int StopCount { get; private set; }
            public int DisposeCount { get; private set; }

            public override void Initialize(RuntimeHostContext context)
            {
                _calls.Add("initialize:" + ModuleId);
                InitializeSideEffectActive = true;
                if (ThrowInitialize)
                {
                    throw new InvalidOperationException("initialize fault: " + ModuleId);
                }
            }

            public override void Start(RuntimeHostContext context)
            {
                _calls.Add("start:" + ModuleId);
                StartSideEffectActive = true;
                if (ThrowStart)
                {
                    throw new InvalidOperationException("start fault: " + ModuleId);
                }
            }

            public override void Stop(RuntimeHostContext context)
            {
                _calls.Add("stop:" + ModuleId);
                StopCount++;
                StartSideEffectActive = false;
                if (ThrowStop)
                {
                    throw new InvalidOperationException("stop fault: " + ModuleId);
                }
            }

            public override void Dispose()
            {
                _calls.Add("dispose:" + ModuleId);
                DisposeCount++;
                StartSideEffectActive = false;
                InitializeSideEffectActive = false;
                if (ThrowDispose)
                {
                    throw new InvalidOperationException("dispose fault: " + ModuleId);
                }
            }
        }
    }
}
