using System.Collections.Generic;
using MxFramework.Runtime;
using MxFramework.VoiceInput;
using NUnit.Framework;

namespace MxFramework.Tests.VoiceInput
{
    public class VoiceInputRuntimeModuleTests
    {
        [Test]
        public void Constructor_UsesPreSimulationByDefault()
        {
            var module = new VoiceInputRuntimeModule(new RecordingVoiceInputService());

            Assert.AreEqual(VoiceInputRuntimeModule.DefaultModuleId, module.ModuleId);
            Assert.AreEqual(RuntimeTickStage.PreSimulation, module.TickStage);
            Assert.AreEqual(0, module.Priority);
        }

        [Test]
        public void Tick_UsesRegisteredVoiceInputService()
        {
            var service = new RecordingVoiceInputService();
            var services = new RuntimeServiceRegistry();
            services.Register<IVoiceInputService>(service);
            var host = new RuntimeHost(new RuntimeHostOptions { Services = services });
            host.RegisterModule(new VoiceInputRuntimeModule());

            host.Initialize();
            host.Start();
            host.Tick(7, 0.25d, 1d);

            Assert.AreEqual(1, service.TickCount);
            Assert.AreEqual(new RuntimeFrame(7), service.LastFrame);
            Assert.AreEqual(0.25d, service.LastDeltaTime);
        }

        [Test]
        public void StartAndStop_CanBeOwnedByRuntimeModule()
        {
            var service = new RecordingVoiceInputService();
            var host = new RuntimeHost();
            host.RegisterModule(new VoiceInputRuntimeModule(service, startOnHostStart: true, stopOnHostStop: true));

            host.Initialize();
            host.Start();
            host.Stop();

            Assert.AreEqual(1, service.StartCount);
            Assert.AreEqual(1, service.StopCount);
        }

        [Test]
        public void Initialize_WithoutVoiceInputService_ThrowsRuntimeHostException()
        {
            var host = new RuntimeHost();
            host.RegisterModule(new VoiceInputRuntimeModule());

            RuntimeHostException exception = Assert.Throws<RuntimeHostException>(() => host.Initialize());

            Assert.AreEqual(VoiceInputRuntimeModule.DefaultModuleId, exception.Error.ModuleId);
            StringAssert.Contains("IVoiceInputService", exception.Error.Message);
        }

        private sealed class RecordingVoiceInputService : IVoiceInputService
        {
            public VoiceInputSnapshot Snapshot { get; private set; } = VoiceInputSnapshot.Idle;
            public int StartCount { get; private set; }
            public int StopCount { get; private set; }
            public int TickCount { get; private set; }
            public RuntimeFrame LastFrame { get; private set; }
            public double LastDeltaTime { get; private set; }

            public VoiceInputResult Start()
            {
                StartCount++;
                return VoiceInputResult.Ok();
            }

            public VoiceInputResult Stop()
            {
                StopCount++;
                return VoiceInputResult.Ok();
            }

            public void Tick(RuntimeFrame frame, double deltaTime)
            {
                TickCount++;
                LastFrame = frame;
                LastDeltaTime = deltaTime;
            }

            public int DrainEvents(RuntimeFrame frame, List<VoiceInputEvent> destination)
            {
                return 0;
            }

            public RuntimeEventQueueSnapshot CreateEventQueueSnapshot()
            {
                return new RuntimeEventQueueSnapshot(0, RuntimeFrame.Zero, RuntimeFrame.Zero, 0L, typeof(VoiceInputEvent).FullName);
            }

            public void ClearEvents()
            {
            }

            public void Dispose()
            {
            }
        }
    }
}
