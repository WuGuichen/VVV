using MxFramework.Runtime;
using NUnit.Framework;

namespace MxFramework.PackageTests.Runtime
{
    public sealed class RuntimeHostPackageContractTests
    {
        private const int PackageContractTraceKind = 90200;
        [Test]
        public void Lifecycle_StartTickStop_DispatchesInOrder()
        {
            var module = new ProbeModule();
            using (var host = new RuntimeHost())
            {
                host.RegisterModule(module);
                host.Initialize();
                host.Start();
                host.Tick(12L, 0.016d, 0.192d);
                host.Stop();
            }

            Assert.That(module.Events, Is.EqualTo("initialize,start,tick:12,stop,dispose"));
        }

        [Test]
        public void CommandEnvelope_RoundTripsThroughBuffer()
        {
            var envelope = new RuntimeCommandEnvelope(
                700001,
                1,
                RuntimeCommandProvenanceKind.Derived,
                data0: 11L,
                data1: 22L,
                data7: 77L);
            var expected = new RuntimeCommand(
                new RuntimeFrame(3),
                sourceId: 9,
                commandId: 700101,
                targetId: 4,
                payload0: 5,
                payload1: 6,
                payload2: 7,
                traceId: new RuntimeTraceIdentity(PackageContractTraceKind),
                envelope: envelope);
            var buffer = new RuntimeCommandBuffer();

            Assert.That(buffer.Enqueue(expected).Success, Is.True);
            var drained = buffer.DrainForFrame(new RuntimeFrame(3));

            Assert.That(drained, Has.Count.EqualTo(1));
            Assert.That(drained[0], Is.EqualTo(expected));
            Assert.That(drained[0].Envelope, Is.EqualTo(envelope));
        }

        private sealed class ProbeModule : IRuntimeModule
        {
            public string ModuleId => "package-contract";
            public int Priority => 0;
            public RuntimeTickStage TickStage => RuntimeTickStage.Simulation;
            public string Events { get; private set; } = string.Empty;

            public void Initialize(RuntimeHostContext context) => Append("initialize");
            public void Start(RuntimeHostContext context) => Append("start");
            public void Tick(RuntimeTickContext context) => Append("tick:" + context.FrameIndex);
            public void Stop(RuntimeHostContext context) => Append("stop");
            public void Dispose() => Append("dispose");

            private void Append(string value)
            {
                Events = Events.Length == 0 ? value : Events + "," + value;
            }
        }
    }
}
