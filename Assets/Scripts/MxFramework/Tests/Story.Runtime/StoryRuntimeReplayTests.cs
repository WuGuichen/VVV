using System.Collections.Generic;
using MxFramework.Runtime;
using MxFramework.Story;
using MxFramework.Story.Runtime;
using MxFramework.Tests.Story;
using NUnit.Framework;

namespace MxFramework.Tests.Story.Runtime
{
    public class StoryRuntimeReplayTests
    {
        [Test]
        public void PlaybackCommandsRecreateHash()
        {
            RuntimeReplaySnapshot snapshot = RecordSnapshot();
            var result = new RuntimeReplayPlaybackRunner().Play(snapshot, new StoryReplayDriver());

            Assert.IsTrue(result.Success);
            Assert.AreEqual(2, result.FramesPlayed);
        }

        private static RuntimeReplaySnapshot RecordSnapshot()
        {
            var driver = new StoryReplayDriver();
            driver.Reset(Header());
            var recorder = new RuntimeReplayRecorder(Header());

            RuntimeCommand enter = StoryRuntimeCommandFactory.RequestEnterBeat(
                RuntimeFrame.Zero,
                StoryRuntimeCommandSources.TestDriver,
                StoryTestGraphs.GraphId,
                StoryTestGraphs.EntryBeatId);
            RuntimeReplayPlaybackFrameResult frame0 = driver.RunFrame(new RuntimeReplayFrameRecord(RuntimeFrame.Zero, new[] { enter }, 0L, string.Empty));
            recorder.RecordFrame(RuntimeFrame.Zero, new[] { enter }, frame0.ActualResultHash, frame0.DiagnosticsSummary);

            int beatInstanceId = driver.Director.CreateSnapshot().ActiveBeatInstances[0].BeatInstanceId;
            RuntimeCommand choice = StoryRuntimeCommandFactory.SelectChoice(
                new RuntimeFrame(1),
                StoryRuntimeCommandSources.TestDriver,
                beatInstanceId,
                StoryTestGraphs.FirstChoiceId,
                StoryTestGraphs.GraphId);
            RuntimeReplayPlaybackFrameResult frame1 = driver.RunFrame(new RuntimeReplayFrameRecord(new RuntimeFrame(1), new[] { choice }, 0L, string.Empty));
            recorder.RecordFrame(new RuntimeFrame(1), new[] { choice }, frame1.ActualResultHash, frame1.DiagnosticsSummary);

            return recorder.CreateSnapshot();
        }

        private static RuntimeReplayHeader Header()
        {
            return new RuntimeReplayHeader(
                schemaVersion: 1,
                frameworkVersion: "story-runtime-s1",
                configHash: "story-test",
                resourceCatalogHash: "none",
                startFrame: RuntimeFrame.Zero);
        }

        private sealed class StoryReplayDriver : IRuntimeReplayFrameDriver
        {
            private StoryRuntimeModule _module;

            public StoryDirector Director { get; private set; }

            public void Reset(RuntimeReplayHeader header)
            {
                Director = new StoryDirector();
                Director.LoadGraph(StoryTestGraphs.MinimalChoiceGraph());
                _module = new StoryRuntimeModule(Director);
            }

            public RuntimeReplayPlaybackFrameResult RunFrame(RuntimeReplayFrameRecord record)
            {
                for (int i = 0; i < record.Commands.Count; i++)
                {
                    RuntimeCommandValidationResult enqueue = _module.CommandBuffer.Enqueue(record.Commands[i]);
                    if (!enqueue.Success)
                    {
                        return new RuntimeReplayPlaybackFrameResult(
                            record.Frame,
                            Hash(record.Frame),
                            "enqueue-error",
                            new[] { enqueue.Error });
                    }
                }

                _module.Tick(new RuntimeTickContext(record.Frame.Value, 0d, 0d, RuntimeTickStage.Simulation));
                var errors = new List<RuntimeCommandError>(_module.LastCommandErrors);
                return new RuntimeReplayPlaybackFrameResult(
                    record.Frame,
                    Hash(record.Frame),
                    "story-events=" + _module.Events.CreateSnapshot().PendingCount,
                    errors);
            }

            private long Hash(RuntimeFrame frame)
            {
                return RuntimeHashCombiner.ComputeHash(
                    frame,
                    new IRuntimeHashContributor[] { new StoryRuntimeHashContributor(Director) });
            }
        }
    }
}
