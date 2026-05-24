using MxFramework.Runtime;
using MxFramework.Story;
using MxFramework.Story.Runtime;
using MxFramework.Tests.Story;
using NUnit.Framework;

namespace MxFramework.Tests.Story.Runtime
{
    public class StoryRuntimeHashTests
    {
        [Test]
        public void HashStableAcrossEquivalentProgression()
        {
            long first = ProgressAndHash();
            long second = ProgressAndHash();

            Assert.AreEqual(first, second);
        }

        private static long ProgressAndHash()
        {
            var director = new StoryDirector();
            director.LoadGraph(StoryTestGraphs.MinimalChoiceGraph());
            director.TryEnterBeat(StoryTestGraphs.GraphId, StoryTestGraphs.EntryBeatId, default);
            int beatInstanceId = director.CreateSnapshot().ActiveBeatInstances[0].BeatInstanceId;
            director.TryResolveChoice(beatInstanceId, StoryTestGraphs.FirstChoiceId);

            return RuntimeHashCombiner.ComputeHash(
                new RuntimeFrame(3),
                new IRuntimeHashContributor[] { new StoryRuntimeHashContributor(director) });
        }
    }
}
