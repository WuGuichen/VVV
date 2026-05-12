using System;
using System.Collections.Generic;
using MxFramework.Runtime;
using NUnit.Framework;

namespace MxFramework.Tests.Runtime
{
    public class RuntimeHostTests
    {
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
            CollectionAssert.AreEqual(new[] { "tick:before:Simulation", "tick:bad", "tick:after:Simulation" }, calls);
        }

        [Test]
        public void InitializeException_CollectAndStopFrameKeepsCreatedState()
        {
            var calls = new List<string>();
            var host = new RuntimeHost(new RuntimeHostOptions { ErrorPolicy = RuntimeHostErrorPolicy.CollectAndStopFrame });
            host.RegisterModule(new ThrowingInitializeModule("bad-init", calls));
            host.RegisterModule(new RecordingModule("z-after", calls));

            host.Initialize();

            Assert.AreEqual(RuntimeLifecycleState.Created, host.State);
            Assert.AreEqual(1, host.Errors.Count);
            CollectionAssert.AreEqual(new[] { "initialize:bad-init" }, calls);
        }

        [Test]
        public void StartException_CollectAndContinueKeepsInitializedState()
        {
            var calls = new List<string>();
            var host = new RuntimeHost(new RuntimeHostOptions { ErrorPolicy = RuntimeHostErrorPolicy.CollectAndContinue });
            host.RegisterModule(new ThrowingStartModule("bad-start", calls, RuntimeTickStage.Simulation, 0));
            host.RegisterModule(new RecordingModule("after", calls, RuntimeTickStage.Simulation, 1));
            host.Initialize();
            calls.Clear();

            host.Start();

            Assert.AreEqual(RuntimeLifecycleState.Initialized, host.State);
            Assert.AreEqual(1, host.Errors.Count);
            CollectionAssert.AreEqual(new[] { "start:bad-start", "start:after" }, calls);
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

        private sealed class ThrowingInitializeModule : RecordingModule
        {
            public ThrowingInitializeModule(string moduleId, List<string> calls)
                : base(moduleId, calls)
            {
            }

            public override void Initialize(RuntimeHostContext context)
            {
                Calls.Add("initialize:" + ModuleId);
                throw new InvalidOperationException("init boom");
            }
        }

        private sealed class ThrowingStartModule : RecordingModule
        {
            public ThrowingStartModule(string moduleId, List<string> calls, RuntimeTickStage tickStage, int priority)
                : base(moduleId, calls, tickStage, priority)
            {
            }

            public override void Start(RuntimeHostContext context)
            {
                Calls.Add("start:" + ModuleId);
                throw new InvalidOperationException("start boom");
            }
        }
    }
}
