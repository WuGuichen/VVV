using System;
using MxFramework.Audio;
using MxFramework.Audio.FMOD;
using MxFramework.Runtime;
using NUnit.Framework;

namespace MxFramework.Tests.Audio
{
    public class AudioRuntimeModuleTests
    {
        [Test]
        public void Constructor_UsesPostSimulationByDefault()
        {
            var module = new AudioRuntimeModule(new RecordingAudioService());

            Assert.AreEqual(AudioRuntimeModule.DefaultModuleId, module.ModuleId);
            Assert.AreEqual(RuntimeTickStage.PostSimulation, module.TickStage);
            Assert.AreEqual(0, module.Priority);
        }

        [Test]
        public void Tick_UsesRegisteredAudioService()
        {
            var service = new RecordingAudioService();
            var services = new RuntimeServiceRegistry();
            services.Register<IAudioService>(service);
            var host = new RuntimeHost(new RuntimeHostOptions { Services = services });
            host.RegisterModule(new AudioRuntimeModule());

            host.Initialize();
            host.Start();
            host.Tick(5, 0.25d, 1d);

            Assert.AreEqual(1, service.TickCount);
            Assert.AreEqual(0.25f, service.LastDeltaTime);
        }

        [Test]
        public void Initialize_WithoutAudioService_ThrowsRuntimeHostException()
        {
            var host = new RuntimeHost();
            host.RegisterModule(new AudioRuntimeModule());

            RuntimeHostException exception = Assert.Throws<RuntimeHostException>(() => host.Initialize());

            Assert.AreEqual(AudioRuntimeModule.DefaultModuleId, exception.Error.ModuleId);
            StringAssert.Contains("IAudioService", exception.Error.Message);
        }

        private sealed class RecordingAudioService : IAudioService, IDisposable
        {
            public int TickCount { get; private set; }
            public float LastDeltaTime { get; private set; }

            public AudioPlayResult PlayOneShot(in AudioPlayRequest request)
            {
                return AudioPlayResult.Ok(AudioHandle.Invalid);
            }

            public AudioPlayResult StartEvent(in AudioPlayRequest request, out AudioHandle handle)
            {
                handle = new AudioHandle(1, request.EventId, request.EmitterId, AudioHandleState.Playing);
                return AudioPlayResult.Ok(handle);
            }

            public AudioResult Stop(AudioHandle handle, AudioStopMode stopMode)
            {
                return AudioResult.Ok();
            }

            public AudioResult SetParameter(AudioHandle handle, int parameterId, float value)
            {
                return AudioResult.Ok();
            }

            public AudioResult SetBusVolume(int busId, float volume)
            {
                return AudioResult.Ok();
            }

            public AudioResult SetBusMuted(int busId, bool muted)
            {
                return AudioResult.Ok();
            }

            public AudioDebugSnapshot CaptureSnapshot()
            {
                return AudioDebugSnapshot.Empty;
            }

            public void Tick(float deltaTime)
            {
                TickCount++;
                LastDeltaTime = deltaTime;
            }

            public void Dispose()
            {
            }
        }
    }
}
