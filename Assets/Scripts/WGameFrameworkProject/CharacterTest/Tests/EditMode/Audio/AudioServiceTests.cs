using System.Collections.Generic;
using MxFramework.Audio;
using NUnit.Framework;

namespace MxFramework.Tests.Audio
{
    public class AudioServiceTests
    {
        [Test]
        public void PlayOneShot_WithKnownEvent_DoesNotExposeHandle()
        {
            MemoryAudioDefinitions definitions = CreateDefinitions();
            using (var service = new AudioService(definitions, new NullAudioBackend()))
            {
                AudioPlayResult result = service.PlayOneShot(AudioPlayRequest.Create2D(1001));

                Assert.IsTrue(result.Success, result.Message);
                Assert.AreEqual(AudioHandle.Invalid, result.Handle);
                AudioDebugSnapshot snapshot = service.CaptureSnapshot();
                Assert.AreEqual(1, snapshot.TotalPlayRequests);
                Assert.AreEqual(0, snapshot.ActiveEventCount);
            }
        }

        [Test]
        public void StartEvent_WithLoopEvent_ReturnsActiveHandle()
        {
            MemoryAudioDefinitions definitions = CreateDefinitions();
            using (var service = new AudioService(definitions, new NullAudioBackend()))
            {
                AudioPlayResult result = service.StartEvent(AudioPlayRequest.Create3D(2001, AudioTransform.Origin), out AudioHandle handle);

                Assert.IsTrue(result.Success, result.Message);
                Assert.IsTrue(handle.IsValid);
                Assert.AreEqual(2001, handle.EventId);
                AudioDebugSnapshot snapshot = service.CaptureSnapshot();
                Assert.AreEqual(1, snapshot.ActiveEventCount);
                Assert.AreEqual(1, snapshot.ActiveEvents.Count);
                Assert.AreEqual(handle.Id, snapshot.ActiveEvents[0].Handle.Id);
            }
        }

        [Test]
        public void PlayOneShot_WithLoopEvent_IsRejected()
        {
            MemoryAudioDefinitions definitions = CreateDefinitions();
            using (var service = new AudioService(definitions, new NullAudioBackend()))
            {
                AudioPlayResult result = service.PlayOneShot(AudioPlayRequest.Create2D(2001));

                Assert.IsFalse(result.Success);
                Assert.AreEqual(AudioErrorCode.RequestRejected, result.ErrorCode);
                Assert.AreEqual(0, service.CaptureSnapshot().TotalPlayRequests);
            }
        }

        [Test]
        public void SetParameter_WhenParameterIsMissing_ReturnsInvalidParameter()
        {
            MemoryAudioDefinitions definitions = CreateDefinitions();
            using (var service = new AudioService(definitions, new NullAudioBackend()))
            {
                AudioPlayResult start = service.StartEvent(AudioPlayRequest.Create3D(2001, AudioTransform.Origin), out AudioHandle handle);
                Assert.IsTrue(start.Success, start.Message);

                AudioResult result = service.SetParameter(handle, 9999, 1f);

                Assert.IsFalse(result.Success);
                Assert.AreEqual(AudioErrorCode.InvalidParameter, result.ErrorCode);
            }
        }

        [Test]
        public void SetBusVolume_ClampsAndKeepsMutedStateSeparate()
        {
            MemoryAudioDefinitions definitions = CreateDefinitions();
            using (var service = new AudioService(definitions, new NullAudioBackend()))
            {
                Assert.IsTrue(service.SetBusMuted(10, true).Success);
                Assert.IsTrue(service.SetBusVolume(10, 2f).Success);

                AudioDebugSnapshot snapshot = service.CaptureSnapshot();
                Assert.AreEqual(1, snapshot.BusStates.Count);
                Assert.AreEqual(1f, snapshot.BusStates[0].Volume);
                Assert.IsTrue(snapshot.BusStates[0].Muted);
            }
        }

        [Test]
        public void Stop_WithInvalidHandle_ReturnsInvalidHandleAndRecordsError()
        {
            MemoryAudioDefinitions definitions = CreateDefinitions();
            using (var service = new AudioService(definitions, new NullAudioBackend()))
            {
                AudioResult result = service.Stop(AudioHandle.Invalid, AudioStopMode.Immediate);

                Assert.IsFalse(result.Success);
                Assert.AreEqual(AudioErrorCode.InvalidHandle, result.ErrorCode);
            }
        }

        [Test]
        public void PlayOneShot_3DEventWith2DRequest_IsRejected()
        {
            MemoryAudioDefinitions definitions = CreateDefinitions();
            using (var service = new AudioService(definitions, new NullAudioBackend()))
            {
                AudioPlayResult result = service.PlayOneShot(AudioPlayRequest.Create2D(3001));

                Assert.IsFalse(result.Success);
                Assert.AreEqual(AudioErrorCode.RequestRejected, result.ErrorCode);
                Assert.AreEqual(0, service.CaptureSnapshot().TotalPlayRequests);
            }
        }

        [Test]
        public void PlayOneShot_2DEventWith3DRequest_IsRejected()
        {
            MemoryAudioDefinitions definitions = CreateDefinitions();
            using (var service = new AudioService(definitions, new NullAudioBackend()))
            {
                AudioPlayResult result = service.PlayOneShot(AudioPlayRequest.Create3D(1001, AudioTransform.Origin));

                Assert.IsFalse(result.Success);
                Assert.AreEqual(AudioErrorCode.RequestRejected, result.ErrorCode);
            }
        }

        [Test]
        public void PlayOneShot_WithOutOfRangeOrDuplicateParameter_IsRejected()
        {
            MemoryAudioDefinitions definitions = CreateDefinitions();
            using (var service = new AudioService(definitions, new NullAudioBackend()))
            {
                AudioPlayResult outOfRange = service.PlayOneShot(AudioPlayRequest.Create3D(
                    3001,
                    AudioTransform.Origin,
                    parameters: new[] { new AudioParameterValue(1, 2f) }));
                AudioPlayResult duplicate = service.PlayOneShot(AudioPlayRequest.Create3D(
                    3001,
                    AudioTransform.Origin,
                    parameters: new[]
                    {
                        new AudioParameterValue(1, 0.25f),
                        new AudioParameterValue(1, 0.5f)
                    }));

                Assert.AreEqual(AudioErrorCode.InvalidParameter, outOfRange.ErrorCode);
                Assert.AreEqual(AudioErrorCode.InvalidParameter, duplicate.ErrorCode);
            }
        }

        [Test]
        public void SetParameter_WithNonFiniteValue_IsRejectedBeforeBackend()
        {
            MemoryAudioDefinitions definitions = CreateDefinitions();
            using (var service = new AudioService(definitions, new NullAudioBackend()))
            {
                Assert.IsTrue(service.StartEvent(
                    AudioPlayRequest.Create3D(2001, AudioTransform.Origin),
                    out AudioHandle handle).Success);

                AudioResult result = service.SetParameter(handle, 1, float.NaN);

                Assert.AreEqual(AudioErrorCode.InvalidParameter, result.ErrorCode);
            }
        }

        private static MemoryAudioDefinitions CreateDefinitions()
        {
            var definitions = new MemoryAudioDefinitions();
            definitions.AddBus(new AudioBusDefinition(10, "SFX", "bus:/SFX"));
            definitions.AddEvent(new AudioEventDefinition(
                1001,
                "ui.click",
                "event:/ui/click",
                string.Empty,
                AudioEventKind.Event,
                10,
                false,
                false,
                0f));
            definitions.AddEvent(new AudioEventDefinition(
                2001,
                "combat.aura",
                "event:/combat/aura",
                string.Empty,
                AudioEventKind.Event,
                10,
                true,
                true,
                30f,
                new[] { new AudioParameterDefinition(1, "Intensity") }));
            definitions.AddEvent(new AudioEventDefinition(
                3001,
                "combat.impact",
                "event:/combat/impact",
                string.Empty,
                AudioEventKind.Event,
                10,
                true,
                false,
                30f,
                new[] { new AudioParameterDefinition(1, "Intensity") }));
            return definitions;
        }

        private sealed class MemoryAudioDefinitions : IAudioDefinitionProvider
        {
            private readonly Dictionary<int, AudioEventDefinition> _events = new Dictionary<int, AudioEventDefinition>();
            private readonly Dictionary<int, AudioBusDefinition> _buses = new Dictionary<int, AudioBusDefinition>();

            public void AddEvent(AudioEventDefinition definition)
            {
                _events[definition.Id] = definition;
            }

            public void AddBus(AudioBusDefinition definition)
            {
                _buses[definition.Id] = definition;
            }

            public bool TryGetEvent(int eventId, out AudioEventDefinition definition)
            {
                return _events.TryGetValue(eventId, out definition);
            }

            public bool TryGetBus(int busId, out AudioBusDefinition definition)
            {
                return _buses.TryGetValue(busId, out definition);
            }

            public bool TryGetParameter(int eventId, int parameterId, out AudioParameterDefinition definition)
            {
                definition = default;
                if (!_events.TryGetValue(eventId, out AudioEventDefinition audioEvent))
                    return false;

                IReadOnlyList<AudioParameterDefinition> parameters = audioEvent.Parameters ?? AudioEventDefinition.EmptyParameters;
                for (int i = 0; i < parameters.Count; i++)
                {
                    if (parameters[i].Id == parameterId)
                    {
                        definition = parameters[i];
                        return true;
                    }
                }

                return false;
            }
        }
    }
}
