using System;
using MxFramework.Audio;
using NUnit.Framework;

namespace MxFramework.Tests.Audio
{
    public sealed class AudioPresentationDrainTests
    {
        [Test]
        public void FixedBuffer_WhenFull_DropsWithoutChangingOrder()
        {
            var buffer = new FixedAudioCommandBuffer(2);
            Assert.IsTrue(buffer.TryEnqueue(CreateCommand(1, frame: 1)));
            Assert.IsTrue(buffer.TryEnqueue(CreateCommand(1, frame: 2)));
            Assert.IsFalse(buffer.TryEnqueue(CreateCommand(1, frame: 3)));

            Assert.AreEqual(2, buffer.Count);
            Assert.AreEqual(1L, buffer[0].Request.Frame);
            Assert.AreEqual(2L, buffer[1].Request.Frame);
            Assert.AreEqual(1, buffer.DroppedCount);
        }

        [Test]
        public void Drain_ReplayAndConfirmationPolicies_AreDeterministic()
        {
            using (AudioService service = CreateService())
            {
                var drain = new AudioPresentationDrain();
                var buffer = new FixedAudioCommandBuffer(4);
                buffer.TryEnqueue(CreateCommand(1, frame: 10, policy: AudioPredictionPolicy.SuppressDuringReplay));
                drain.Drain(buffer, service, isReplay: true, isConfirmed: true);
                Assert.AreEqual(1, drain.Snapshot.SuppressedReplay);
                Assert.AreEqual(0, service.CaptureSnapshot().TotalPlayRequests);

                buffer.TryEnqueue(CreateCommand(1, frame: 11, policy: AudioPredictionPolicy.ConfirmedOnly));
                drain.Drain(buffer, service, isReplay: false, isConfirmed: false);
                Assert.AreEqual(1, drain.Snapshot.AwaitingConfirmation);

                buffer.TryEnqueue(CreateCommand(1, frame: 11, policy: AudioPredictionPolicy.ConfirmedOnly));
                drain.Drain(buffer, service, isReplay: false, isConfirmed: true);
                Assert.AreEqual(1, drain.Snapshot.Accepted);

                buffer.TryEnqueue(CreateCommand(1, frame: 11, policy: AudioPredictionPolicy.ConfirmedOnly));
                drain.Drain(buffer, service, isReplay: false, isConfirmed: true);
                Assert.AreEqual(1, drain.Snapshot.Deduped);
                Assert.AreEqual(1, service.CaptureSnapshot().TotalPlayRequests);
            }
        }

        [Test]
        public void Drain_OneShotFlood_RespectsGlobalBudget()
        {
            using (AudioService service = CreateService())
            {
                var drain = new AudioPresentationDrain(new AudioPresentationDrainOptions
                {
                    GlobalVoiceLimit = 2,
                    CategoryVoiceLimit = 8,
                    EventVoiceLimit = 8,
                    EmitterVoiceLimit = 8
                });
                var buffer = new FixedAudioCommandBuffer(4);
                buffer.TryEnqueue(CreateCommand(1, frame: 20, ordinal: 0));
                buffer.TryEnqueue(CreateCommand(1, frame: 20, ordinal: 1));
                buffer.TryEnqueue(CreateCommand(1, frame: 20, ordinal: 2));

                drain.Drain(buffer, service, isReplay: false, isConfirmed: true);

                Assert.AreEqual(2, drain.Snapshot.Accepted);
                Assert.AreEqual(1, drain.Snapshot.BudgetRejected);
                Assert.AreEqual(2, service.CaptureSnapshot().TotalPlayRequests);
            }
        }

        [Test]
        public void Drain_HigherPriorityControlledEvent_StealsAndScopeStops()
        {
            using (AudioService service = CreateService())
            {
                var drain = new AudioPresentationDrain(new AudioPresentationDrainOptions
                {
                    GlobalVoiceLimit = 1,
                    CategoryVoiceLimit = 8,
                    EventVoiceLimit = 8,
                    EmitterVoiceLimit = 8
                });
                var buffer = new FixedAudioCommandBuffer(2);
                buffer.TryEnqueue(CreateCommand(2, frame: 30, priority: 1, kind: AudioCommandKind.StartEvent, scope: AudioScope.Scene));
                drain.Drain(buffer, service, isReplay: false, isConfirmed: true);

                buffer.TryEnqueue(CreateCommand(2, frame: 31, priority: 10, kind: AudioCommandKind.StartEvent, scope: AudioScope.Scene));
                drain.Drain(buffer, service, isReplay: false, isConfirmed: true);

                Assert.AreEqual(1, drain.Snapshot.Stolen);
                Assert.AreEqual(1, drain.Snapshot.ControlledHandles);
                Assert.AreEqual(1, service.CaptureSnapshot().ActiveEventCount);
                Assert.AreEqual(1, drain.StopAllByScope(service, AudioScope.Scene, stopMode: AudioStopMode.Immediate));
                Assert.AreEqual(0, service.CaptureSnapshot().ActiveEventCount);
            }
        }

        [Test]
        public void Controlled3D_EmitterRegistry_UpdatesAndOwnerDestroyStops()
        {
            using (AudioService service = CreateService())
            {
                var emitters = new AudioEmitterRegistry(2);
                Assert.IsTrue(emitters.Register(42, new AudioTransform(1f, 2f, 3f)));
                var drain = new AudioPresentationDrain();
                var buffer = new FixedAudioCommandBuffer(1);
                AudioPlayRequest request = AudioPlayRequest.Create3D(3, new AudioTransform(1f, 2f, 3f), emitterId: 42, frame: 40);
                buffer.TryEnqueue(new AudioCommand(AudioCommandKind.StartEvent, request, AudioScope.Entity, ownerId: 99));

                drain.Drain(buffer, service, isReplay: false, isConfirmed: true);
                Assert.IsTrue(emitters.Update(42, new AudioTransform(4f, 5f, 6f, velocityX: 2f)));
                Assert.AreEqual(1, drain.UpdateEmitters(service, emitters));
                Assert.IsTrue(((IAudioSpatialControl)service).SetListenerAttributes(0, AudioTransform.Origin).Success);

                Assert.IsTrue(emitters.Unregister(42));
                Assert.AreEqual(1, drain.StopAllByEmitter(service, 42));
                Assert.AreEqual(0, service.CaptureSnapshot().ActiveEventCount);
            }
        }

        [Test]
        public void Lifecycle_PauseDoesNotOverwriteMix_AndSceneKeepsPersistentAudio()
        {
            using (AudioService service = CreateService())
            {
                var drain = new AudioPresentationDrain();
                var lifecycle = new AudioLifecycleController(service, drain, new[] { 10 });
                var buffer = new FixedAudioCommandBuffer(2);
                buffer.TryEnqueue(CreateCommand(2, frame: 50, ordinal: 0, kind: AudioCommandKind.StartEvent, scope: AudioScope.Scene));
                buffer.TryEnqueue(CreateCommand(2, frame: 50, ordinal: 1, kind: AudioCommandKind.StartEvent, scope: AudioScope.AppPersistent));
                drain.Drain(buffer, service, isReplay: false, isConfirmed: true);

                Assert.IsTrue(service.SetBusVolume(10, 0.4f).Success);
                Assert.IsTrue(lifecycle.SetPaused(true).Success);
                AudioDebugBusState paused = service.CaptureSnapshot().BusStates[0];
                Assert.AreEqual(0.4f, paused.Volume);
                Assert.IsTrue(paused.Paused);
                Assert.AreEqual(1, lifecycle.OnSceneExit());
                Assert.AreEqual(1, service.CaptureSnapshot().ActiveEventCount);
                Assert.AreEqual(1, lifecycle.OnSessionEnded());
                AudioDebugBusState resumed = service.CaptureSnapshot().BusStates[0];
                Assert.IsFalse(resumed.Paused);
                Assert.IsFalse(lifecycle.Paused);
                Assert.AreEqual(0, service.CaptureSnapshot().ActiveEventCount);
            }
        }

        [Test]
        public void Drain_WarmedSuccessfulPath_AllocatesZeroManagedBytes()
        {
            using (AudioService service = CreateService())
            {
                var drain = new AudioPresentationDrain();
                var buffer = new FixedAudioCommandBuffer(1);
                buffer.TryEnqueue(CreateCommand(1, frame: 60));
                drain.Drain(buffer, service, isReplay: false, isConfirmed: true);

                buffer.TryEnqueue(CreateCommand(1, frame: 61));
                long before = GC.GetAllocatedBytesForCurrentThread();
                drain.Drain(buffer, service, isReplay: false, isConfirmed: true);
                long allocated = GC.GetAllocatedBytesForCurrentThread() - before;

                Assert.AreEqual(0L, allocated);
            }
        }

        private static AudioCommand CreateCommand(
            int eventId,
            long frame,
            int ordinal = 0,
            int priority = 0,
            AudioCommandKind kind = AudioCommandKind.PlayOneShot,
            AudioScope scope = AudioScope.TransientFrame,
            AudioPredictionPolicy policy = AudioPredictionPolicy.Immediate)
        {
            AudioPlayRequest request = AudioPlayRequest.Create2D(eventId, priority: priority, frame: frame);
            return new AudioCommand(kind, request, scope, categoryId: 7, dedupeOrdinal: ordinal, predictionPolicy: policy);
        }

        private static AudioService CreateService()
        {
            var catalog = new AudioDefinitionCatalog(
                new[] { new AudioBusDefinition(10, "SFX", "bus:/SFX") },
                new[]
                {
                    new AudioEventDefinition(1, "one-shot", "event:/one-shot", string.Empty, AudioEventKind.Event, 10, false, false, 0f),
                    new AudioEventDefinition(2, "loop", "event:/loop", string.Empty, AudioEventKind.Event, 10, false, true, 0f),
                    new AudioEventDefinition(3, "moving-loop", "event:/moving-loop", string.Empty, AudioEventKind.Event, 10, true, true, 30f)
                },
                null,
                null);
            return new AudioService(catalog, new NullAudioBackend());
        }
    }
}
