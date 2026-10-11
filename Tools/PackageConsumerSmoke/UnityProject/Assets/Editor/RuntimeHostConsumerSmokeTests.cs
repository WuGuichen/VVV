using MxFramework.Runtime;
using NUnit.Framework;

namespace MxFramework.PackageConsumerSmoke
{
    public sealed class RuntimeHostConsumerSmokeTests
    {
        [Test]
        public void BasePackage_CanRunRuntimeHostLifecycle()
        {
            var module = new ProbeModule();
            var host = new RuntimeHost();

            host.RegisterModule(module);
            host.Initialize();
            host.Start();
            host.Tick(12L, 0.016d, 0.192d);
            host.Stop();
            host.Dispose();

            Assert.That(module.Initialized, Is.True);
            Assert.That(module.Started, Is.True);
            Assert.That(module.TickCount, Is.EqualTo(1));
            Assert.That(module.LastFrameIndex, Is.EqualTo(12L));
            Assert.That(module.Stopped, Is.True);
            Assert.That(module.Disposed, Is.True);
        }

        private sealed class ProbeModule : IRuntimeModule
        {
            public string ModuleId => "package-consumer-smoke";
            public int Priority => 0;
            public RuntimeTickStage TickStage => RuntimeTickStage.Simulation;

            public bool Initialized { get; private set; }
            public bool Started { get; private set; }
            public int TickCount { get; private set; }
            public long LastFrameIndex { get; private set; }
            public bool Stopped { get; private set; }
            public bool Disposed { get; private set; }

            public void Initialize(RuntimeHostContext context)
            {
                Initialized = true;
            }

            public void Start(RuntimeHostContext context)
            {
                Started = true;
            }

            public void Tick(RuntimeTickContext context)
            {
                TickCount++;
                LastFrameIndex = context.FrameIndex;
            }

            public void Stop(RuntimeHostContext context)
            {
                Stopped = true;
            }

            public void Dispose()
            {
                Disposed = true;
            }
        }
    }
}
