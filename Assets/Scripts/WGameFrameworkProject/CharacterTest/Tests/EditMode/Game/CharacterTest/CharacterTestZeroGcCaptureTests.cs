using System;
using System.Collections.Generic;
using System.Reflection;
using MxFramework.Game.CharacterTest;
using MxFramework.Runtime;
using NUnit.Framework;
using Unity.Profiling;

namespace MxFramework.Tests.Game.CharacterTest
{
    public sealed class CharacterTestZeroGcCaptureTests
    {
        private const ProfilerRecorderOptions Options = ProfilerRecorderOptions.StartImmediately
            | ProfilerRecorderOptions.CollectOnlyOnCurrentThread;

        [Test]
        public void ResetReusesTheExactProtocolSeedAndFieldCount()
        {
            var reused = new RuntimeHashAccumulator(RuntimeHashSchemaIds.NetworkSnapshot, RuntimeHashSchemaIds.Version1);
            for (int i = 0; i < 32; i++)
            {
                var independent = new RuntimeHashAccumulator(RuntimeHashSchemaIds.NetworkSnapshot, RuntimeHashSchemaIds.Version1);
                reused.Reset();
                independent.AddInt(1, i); reused.AddInt(1, i);
                independent.AddDigest(2, RuntimeHashDigest.ComputeContentDigest(i % 2 == 0 ? null : "😀"));
                reused.AddDigest(2, RuntimeHashDigest.ComputeContentDigest(i % 2 == 0 ? null : "😀"));
                Assert.AreEqual(independent.ToHash(), reused.ToHash());
            }
        }

        [Test]
        public void PreparedHistoryIsAllocationFreeAndPublishedFramesRemainIndependent()
        {
            using var runner = new CharacterTestNetworkLoopbackRunner(NullRuntimeLogger.Instance, createServerViews: false);
            for (int i = 0; i < 20; i++) runner.Tick(1d / 60d);
            var slice = runner.ServerSlice;
            var module = slice.RuntimeAiPlannerCommands;
            var participant = slice.Services.Network.CreateSnapshotParticipant();
            var timeline = new CharacterTestRuntimeAiPlannerDevTimeline(64);
            var frames = new CharacterTestRuntimeAiPlannerDevFrameBuffer(64);
            int start = (int)slice.CurrentFrame.Value;
            timeline.BeginSession(new CharacterTestRuntimeAiLabTimelineSessionIdentity(1, start, 64));
            var slots = new CharacterTestRuntimeAiLabFrameCaptureStorage[64];
            for (int i = 0; i < slots.Length; i++)
                slots[i] = new CharacterTestRuntimeAiLabFrameCaptureStorage(module, slice.CharacterRuntime.Entities.Count, 64);
            participant.ComputeStateHash(slice.CurrentFrame);
            CharacterTestRuntimeAiPlannerDevSceneController.ComputeTimelineRecordContentHash(
                new CharacterTestRuntimeAiPlannerDevTimelineRecord(0, 0, 0, null, null, null, false, 0));
            AssertRecorderDetectsAllocations();
            var expectedStates = new long[64];
            var observedStates = new long[64];
            var historyHashes = new long[64];
            var eventsPerFrame = new int[64];
            using var recorder = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC.Alloc", 65536, Options);
            recorder.Stop();
            for (int i = 0; i < slots.Length; i++)
            {
                if (i > 0) runner.Tick(1d / 60d);
                var owned = module.CreateStateSnapshot();
                expectedStates[i] = participant.ComputeCapturedStateHash(slice.CurrentFrame, module, owned);
                recorder.Reset(); recorder.Start();
                var slot = slots[i];
                int frame = (int)slice.CurrentFrame.Value;
                slot.Capture(module, slice.CharacterRuntime, frame, default);
                long stateHash = participant.ComputeCapturedStateHash(slice.CurrentFrame, module, slot.State.Snapshot);
                observedStates[i] = stateHash;
                frames.Capture(frame, slot.Frame.RealtimeSeconds, slot.Display, runtimeAiPlannerStateSnapshot: slot.State.Snapshot,
                    preparedSnapshot: slot.Frame);
                var record = timeline.AddRecord(frame, slot.Frame.RealtimeSeconds, slice.Services.ElapsedSeconds,
                    slot.Display, slice.Services.Modules.RuntimeCommands.LastDrainedCommands, null, stateHash,
                    animationInputs: slot.AnimationInputs, runtimeAiPlannerStateSnapshot: slot.State.Snapshot, prepared: slot);
                historyHashes[i] = CharacterTestRuntimeAiPlannerDevSceneController.ComputeTimelineRecordContentHash(record);
                slot.Bundle.Initialize(timeline.SessionIdentity, record, historyHashes[i], 0, 0, 0, 0, false);
                recorder.Stop(); eventsPerFrame[i] = recorder.Count;
            }
            CollectionAssert.AreEqual(expectedStates, observedStates, "Prepared state must hash exactly like owned rollback exports.");
            for (int i = 0; i < slots.Length; i++)
            {
                Assert.AreEqual(0, eventsPerFrame[i], "Capture allocations at offset " + i);
                Assert.AreEqual(historyHashes[i], CharacterTestRuntimeAiPlannerDevSceneController.ComputeTimelineRecordContentHash(slots[i].Record));
                Assert.Throws<InvalidOperationException>(() => slots[i].Frame.Initialize(999, 0, slots[i].Display));
            }
            // Replacing history must not recycle a record still held by another consumer.
            timeline.SelectFrame(start + 10);
            var replacement = timeline.AddRecord(start + 10, 0, 0, module.CreateSnapshot(), null, null, 456);
            Assert.AreNotSame(slots[10].Record, replacement);
            Assert.AreEqual(historyHashes[10], CharacterTestRuntimeAiPlannerDevSceneController.ComputeTimelineRecordContentHash(slots[10].Record));
            Assert.Throws<InvalidOperationException>(() => slots[10].Capture(module, slice.CharacterRuntime, 999, default));
            Assert.AreEqual(historyHashes[10], CharacterTestRuntimeAiPlannerDevSceneController.ComputeTimelineRecordContentHash(slots[10].Record));
        }

        [Test]
        public void WarmLiveNetworkHashDoesNotAllocate()
        {
            using var runner = new CharacterTestNetworkLoopbackRunner(NullRuntimeLogger.Instance, createServerViews: false);
            for (int i = 0; i < 20; i++) runner.Tick(1d / 60d);
            var slice = runner.ServerSlice;
            var participant = slice.Services.Network.CreateSnapshotParticipant();
            for (int i = 0; i < 10; i++) participant.ComputeStateHash(slice.CurrentFrame);
            AssertRecorderDetectsAllocations();
            using var recorder = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC.Alloc", 65536, Options);
            for (int i = 0; i < 100; i++) participant.ComputeStateHash(slice.CurrentFrame);
            recorder.Stop(); Assert.AreEqual(0, recorder.Count);
        }

        [TestCase(0)] [TestCase(930100)] [TestCase(int.MaxValue)]
        public void NumericEventLabelsPreserveTheirOriginalContentHash(int actionId)
        {
            var mix = typeof(CharacterTestRuntimeAiLabTimelineEventIndex).GetMethod("MixLabel", BindingFlags.NonPublic | BindingFlags.Static);
            var text = new CharacterTestRuntimeAiLabTimelineEventSample(1, default, 1, 0, "ActionStarted", default,
                new MxFramework.CharacterAction.CharacterActionId(actionId).ToString(), default, 0);
            var numeric = new CharacterTestRuntimeAiLabTimelineEventSample(1, default, 1, 0, "ActionStarted", default,
                "", default, 0, numericLabel: actionId);
            object[] expected = { 1469598103934665603UL, text };
            object[] actual = { 1469598103934665603UL, numeric };
            mix.Invoke(null, expected); mix.Invoke(null, actual);
            Assert.AreEqual(expected[0], actual[0]);
            Assert.AreEqual(text.Label, numeric.Label);
            Assert.IsNull(default(CharacterTestRuntimeAiLabTimelineEventSample).Label);
        }

        private static void AssertRecorderDetectsAllocations()
        {
            using var recorder = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC.Alloc", 1024, Options);
            GC.KeepAlive(new byte[8192]); recorder.Stop();
            Assert.Greater(recorder.Count, 0, "A positive allocation control must be observed before accepting zero events.");
        }

        [Test]
        public void PreparedReplayRetainsCommandsAndDeferredTextAcrossClearAndNewRecording()
        {
            var recorder = new RuntimeReplayRecorder(new RuntimeReplayHeader(1, "test", "config", "resources", RuntimeFrame.Zero));
            recorder.PrepareCapacity(32, 4);
            var commands = new List<RuntimeCommand>(4);
            var retained = new RuntimeReplayFrameRecord[32];
            Func<RuntimeReplayFrameRecord, string> formatter = record => "frame=" + record.Frame.Value + " count=" + record.Commands.Count;
            AssertRecorderDetectsAllocations();
            using var allocations = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC.Alloc", 65536, Options);
            for (int i = 0; i < retained.Length; i++)
            {
                commands.Clear();
                commands.Add(new RuntimeCommand(new RuntimeFrame(i), 1, 901020, 1, i));
                retained[i] = recorder.RecordFrameDeferred(new RuntimeFrame(i), commands, i, formatter);
            }
            allocations.Stop();
            Assert.AreEqual(0, allocations.Count);
            commands.Clear(); recorder.Clear(); recorder.PrepareCapacity(1, 4);
            recorder.RecordFrame(RuntimeFrame.Zero, commands, 999, "replacement");
            for (int i = 0; i < retained.Length; i++)
            {
                Assert.AreEqual(i, retained[i].Commands[0].Payload0);
                Assert.AreEqual("frame=" + i + " count=1", retained[i].DiagnosticsSummary);
            }
        }
    }
}
