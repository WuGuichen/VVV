using System.Collections.Generic;
using MxFramework.Runtime;
using MxFramework.VoiceInput;
using MxFramework.VoiceInput.Unity;
using NUnit.Framework;
using UnityEngine;

namespace MxFramework.Tests.VoiceInput
{
    public class UnityMicrophoneVoiceInputProviderTests
    {
        [Test]
        public void Start_WithNoDevices_ReturnsNoDeviceAndQueuesError()
        {
            var gateway = new StubMicrophoneGateway();
            gateway.DevicesValue = new string[0];
            var provider = new UnityMicrophoneVoiceInputProvider(gateway: gateway);
            var sink = new RecordingSink();

            VoiceInputResult result = provider.Start();
            provider.Tick(RuntimeFrame.Zero, 0.016d, sink);

            Assert.IsFalse(result.Success);
            Assert.AreEqual(VoiceInputErrorCode.NoDevice, result.ErrorCode);
            Assert.AreEqual(VoiceInputStatus.Unavailable, provider.Snapshot.Status);
            Assert.AreEqual(1, sink.Events.Count);
            Assert.AreEqual(VoiceInputEventKind.Error, sink.Events[0].Kind);
            Assert.AreEqual(VoiceInputErrorCode.NoDevice, sink.Events[0].ErrorCode);
        }

        [Test]
        public void Start_WaitsForPermissionAndReportsDenied()
        {
            var gateway = new StubMicrophoneGateway();
            gateway.DevicesValue = new[] { "Default" };
            gateway.HasAuthorization = false;
            gateway.AuthorizationRequestDone = false;
            var provider = new UnityMicrophoneVoiceInputProvider(gateway: gateway);
            var sink = new RecordingSink();

            VoiceInputResult start = provider.Start();
            gateway.AuthorizationRequestDone = true;
            provider.Tick(new RuntimeFrame(2), 0.016d, sink);

            Assert.IsTrue(start.Success);
            Assert.AreEqual(1, gateway.AuthorizationRequestCount);
            Assert.AreEqual(VoiceInputStatus.PermissionDenied, provider.Snapshot.Status);
            Assert.AreEqual(1, sink.Events.Count);
            Assert.AreEqual(new RuntimeFrame(2), sink.Events[0].Frame);
            Assert.AreEqual(VoiceInputErrorCode.PermissionDenied, sink.Events[0].ErrorCode);
        }

        [Test]
        public void Start_WithDefaultDevice_EmitsSessionAndActivity()
        {
            var gateway = new StubMicrophoneGateway();
            gateway.DevicesValue = new[] { "Default" };
            gateway.HasAuthorization = true;
            gateway.Position = 32;
            gateway.IsRecordingValue = true;
            gateway.ClipToReturn = AudioClip.Create("voice-test", 32, 1, 8000, false);
            var data = new float[32];
            for (int i = 0; i < data.Length; i++)
            {
                data[i] = 1f;
            }

            gateway.ClipToReturn.SetData(data, 0);
            var provider = new UnityMicrophoneVoiceInputProvider(
                new UnityMicrophoneVoiceInputOptions
                {
                    RequireUserAuthorization = true,
                    SampleRate = 8000,
                    SampleWindowSize = 8,
                    ActivityThreshold = 0.5f
                },
                gateway);
            var sink = new RecordingSink();

            VoiceInputResult start = provider.Start();
            provider.Tick(new RuntimeFrame(5), 0.016d, sink);

            Assert.IsTrue(start.Success);
            Assert.AreEqual("Default", gateway.StartDeviceName);
            Assert.AreEqual(VoiceInputStatus.Listening, provider.Snapshot.Status);
            Assert.IsTrue(provider.Snapshot.IsAudioActive);
            Assert.AreEqual(2, sink.Events.Count);
            Assert.AreEqual(VoiceInputEventKind.SessionStarted, sink.Events[0].Kind);
            Assert.AreEqual(VoiceInputEventKind.AudioActivityStarted, sink.Events[1].Kind);
        }

        private sealed class RecordingSink : IVoiceInputEventSink
        {
            public List<VoiceInputEvent> Events { get; } = new List<VoiceInputEvent>();

            public void Enqueue(in VoiceInputEvent evt)
            {
                Events.Add(evt);
            }
        }

        private sealed class StubMicrophoneGateway : IUnityMicrophoneGateway
        {
            public string[] DevicesValue = new string[0];
            public bool HasAuthorization = true;
            public bool AuthorizationRequestDone = true;
            public bool IsRecordingValue;
            public int Position;
            public AudioClip ClipToReturn;
            public int AuthorizationRequestCount;
            public string StartDeviceName;

            public string[] Devices => DevicesValue;
            public bool HasMicrophoneAuthorization => HasAuthorization;
            public bool IsMicrophoneAuthorizationRequestDone => AuthorizationRequestDone;

            public void RequestMicrophoneAuthorization()
            {
                AuthorizationRequestCount++;
            }

            public AudioClip Start(string deviceName, bool loop, int lengthSec, int frequency)
            {
                StartDeviceName = deviceName;
                return ClipToReturn;
            }

            public void End(string deviceName)
            {
                IsRecordingValue = false;
            }

            public bool IsRecording(string deviceName)
            {
                return IsRecordingValue;
            }

            public int GetPosition(string deviceName)
            {
                return Position;
            }
        }
    }
}
