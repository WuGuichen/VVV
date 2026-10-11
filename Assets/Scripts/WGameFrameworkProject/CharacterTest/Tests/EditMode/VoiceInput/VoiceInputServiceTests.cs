using System.Collections.Generic;
using MxFramework.Runtime;
using MxFramework.VoiceInput;
using NUnit.Framework;

namespace MxFramework.Tests.VoiceInput
{
    public class VoiceInputServiceTests
    {
        [Test]
        public void StartAndStop_EmitSessionEvents()
        {
            var provider = new FakeVoiceInputProvider(sourceId: 42);
            var service = new VoiceInputService(provider, new VoiceInputOptions { InitialEventCapacity = 4 });

            VoiceInputResult start = service.Start();
            service.Tick(new RuntimeFrame(3), 0.016d);

            var events = new List<VoiceInputEvent>();
            service.DrainEvents(new RuntimeFrame(3), events);

            Assert.IsTrue(start.Success);
            Assert.AreEqual(1, events.Count);
            Assert.AreEqual(VoiceInputEventKind.SessionStarted, events[0].Kind);
            Assert.AreEqual(new RuntimeFrame(3), events[0].Frame);
            Assert.AreEqual(42, events[0].SourceId);
            Assert.AreEqual(VoiceInputStatus.Listening, service.Snapshot.Status);

            VoiceInputResult stop = service.Stop();
            service.Tick(new RuntimeFrame(4), 0.016d);
            service.DrainEvents(new RuntimeFrame(4), events);

            Assert.IsTrue(stop.Success);
            Assert.AreEqual(2, events.Count);
            Assert.AreEqual(VoiceInputEventKind.SessionStopped, events[1].Kind);
            Assert.AreEqual(VoiceInputStatus.Idle, service.Snapshot.Status);
        }

        [Test]
        public void ScriptedProvider_DrainsEventsInRuntimeFrameOrder()
        {
            var provider = new ScriptedVoiceInputProvider(
                new[]
                {
                    new VoiceInputScriptedEvent(new RuntimeFrame(2), VoiceInputEventKind.FinalTranscript, text: "two", confidence: 0.8f),
                    new VoiceInputScriptedEvent(RuntimeFrame.Zero, VoiceInputEventKind.FinalTranscript, text: "zero", confidence: 0.5f),
                    new VoiceInputScriptedEvent(new RuntimeFrame(1), VoiceInputEventKind.FinalTranscript, text: "one", confidence: 0.7f)
                });
            var service = new VoiceInputService(provider, new VoiceInputOptions { InitialEventCapacity = 4 });
            var output = new List<VoiceInputEvent>
            {
                new VoiceInputEvent(RuntimeFrame.Zero, 99, VoiceInputEventKind.Unknown)
            };

            service.Start();
            service.Tick(RuntimeFrame.Zero, 0.016d);
            service.Tick(new RuntimeFrame(1), 0.016d);
            service.Tick(new RuntimeFrame(2), 0.016d);

            int drained = service.DrainEvents(new RuntimeFrame(2), output);

            Assert.AreEqual(3, drained);
            Assert.AreEqual(4, output.Count);
            Assert.AreEqual("zero", output[1].Text);
            Assert.AreEqual("one", output[2].Text);
            Assert.AreEqual("two", output[3].Text);
            Assert.AreEqual(new RuntimeFrame(2), service.Snapshot.LastEventFrame);
            Assert.AreEqual(3, service.Snapshot.TotalEventCount);
            Assert.AreEqual("two", service.Snapshot.LastTranscript);
        }

        [Test]
        public void StartFailure_UpdatesSnapshotAndQueuesError()
        {
            var provider = new FakeVoiceInputProvider();
            provider.StartErrorCode = VoiceInputErrorCode.NoDevice;
            provider.StartErrorMessage = "missing mic";
            var service = new VoiceInputService(provider);

            VoiceInputResult result = service.Start();
            service.Tick(RuntimeFrame.Zero, 0.016d);

            var events = new List<VoiceInputEvent>();
            service.DrainEvents(RuntimeFrame.Zero, events);

            Assert.IsFalse(result.Success);
            Assert.AreEqual(VoiceInputErrorCode.NoDevice, result.ErrorCode);
            Assert.AreEqual(VoiceInputStatus.Error, service.Snapshot.Status);
            Assert.AreEqual(VoiceInputErrorCode.NoDevice, service.Snapshot.LastErrorCode);
            Assert.AreEqual("missing mic", service.Snapshot.LastErrorMessage);
            Assert.AreEqual(1, events.Count);
            Assert.AreEqual(VoiceInputEventKind.Error, events[0].Kind);
            Assert.AreEqual(VoiceInputErrorCode.NoDevice, events[0].ErrorCode);
        }

        [Test]
        public void Service_FillsDefaultSourceIdWhenProviderEventHasNoSource()
        {
            var provider = new ScriptedVoiceInputProvider(
                new[]
                {
                    new VoiceInputScriptedEvent(RuntimeFrame.Zero, VoiceInputEventKind.AudioLevel, level: 0.25f, sourceId: 0)
                },
                sourceId: 0);
            var service = new VoiceInputService(provider, new VoiceInputOptions { SourceId = 12345 });
            var events = new List<VoiceInputEvent>();

            service.Start();
            service.Tick(RuntimeFrame.Zero, 0.016d);
            service.DrainEvents(RuntimeFrame.Zero, events);

            Assert.AreEqual(1, events.Count);
            Assert.AreEqual(12345, events[0].SourceId);
        }
    }
}
