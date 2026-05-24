using MxFramework.Runtime;
using MxFramework.Story;
using MxFramework.Story.Runtime;
using MxFramework.Tests.Story;
using NUnit.Framework;

namespace MxFramework.Tests.Story.Runtime
{
    public class StoryRuntimeSaveStateTests
    {
        [Test]
        public void JsonRoundtripRestoresHash()
        {
            var source = new StoryDirector();
            source.LoadGraph(StoryTestGraphs.MinimalChoiceGraph(waitForPresentation: true));
            source.TryEnterBeat(StoryTestGraphs.GraphId, StoryTestGraphs.EntryBeatId, default);
            StoryBeatInstanceSnapshot waiting = source.CreateSnapshot().ActiveBeatInstances[0];
            source.CompletePresentation(waiting.BeatInstanceId, StoryTestGraphs.PresentationStepId);
            int choiceBeatInstanceId = source.CreateSnapshot().ActiveBeatInstances[0].BeatInstanceId;

            long before = Hash(source);
            RuntimeSaveStateResult<RuntimeSaveState> captured = new StoryRuntimeSaveStateProvider(source, () => 9L).CaptureSaveState();
            string json = RuntimeSaveStateJson.SaveToJson(captured.Value);
            RuntimeSaveStateResult<RuntimeSaveState> loaded = RuntimeSaveStateJson.LoadFromJson(json);
            var restored = new StoryDirector();
            RuntimeSaveStateResult<bool> restore = new StoryRuntimeSaveStateProvider(restored).RestoreSaveState(loaded.Value);

            Assert.IsTrue(captured.Success);
            Assert.IsTrue(loaded.Success);
            Assert.IsTrue(restore.Success);
            Assert.AreEqual(before, Hash(restored));

            StoryChoiceResult result = restored.TryResolveChoice(choiceBeatInstanceId, StoryTestGraphs.FirstChoiceId);
            Assert.IsTrue(result.Success);
        }

        private static long Hash(StoryDirector director)
        {
            return RuntimeHashCombiner.ComputeHash(
                RuntimeFrame.Zero,
                new IRuntimeHashContributor[] { new StoryRuntimeHashContributor(director) });
        }
    }
}
