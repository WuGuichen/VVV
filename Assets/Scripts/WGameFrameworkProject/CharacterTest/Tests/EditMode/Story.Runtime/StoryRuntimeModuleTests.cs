using System.Collections.Generic;
using MxFramework.Runtime;
using MxFramework.Story;
using MxFramework.Story.Runtime;
using MxFramework.Tests.Story;
using NUnit.Framework;

namespace MxFramework.Tests.Story.Runtime
{
    public class StoryRuntimeModuleTests
    {
        [Test]
        public void DrainsOnlyStoryCommandBuffer()
        {
            var director = new StoryDirector();
            director.LoadGraph(StoryTestGraphs.MinimalChoiceGraph());
            var storyBuffer = new RuntimeCommandBuffer();
            var otherBuffer = new RuntimeCommandBuffer();
            var module = new StoryRuntimeModule(director, storyBuffer);

            storyBuffer.Enqueue(StoryRuntimeCommandFactory.RequestEnterBeat(
                RuntimeFrame.Zero,
                StoryRuntimeCommandSources.TestDriver,
                StoryTestGraphs.GraphId,
                StoryTestGraphs.EntryBeatId));
            otherBuffer.Enqueue(new RuntimeCommand(RuntimeFrame.Zero, 1, 77, 0));

            module.Tick(new RuntimeTickContext(0L, 0d, 0d, RuntimeTickStage.Simulation));

            Assert.AreEqual(0, storyBuffer.PendingCount);
            Assert.AreEqual(1, otherBuffer.PendingCount);
            Assert.AreEqual(1, module.LastDrainedCommands.Count);
        }

        [Test]
        public void RaiseTriggerCommandEntersGraph()
        {
            var director = new StoryDirector();
            director.LoadGraph(StoryTestGraphs.MinimalChoiceGraph());
            var module = new StoryRuntimeModule(director);
            module.CommandBuffer.Enqueue(StoryRuntimeCommandFactory.RaiseTrigger(
                RuntimeFrame.Zero,
                StoryRuntimeCommandSources.TestDriver,
                StoryTestGraphs.TriggerId));

            module.Tick(new RuntimeTickContext(0L, 0d, 0d, RuntimeTickStage.Simulation));

            Assert.AreEqual(0, module.LastCommandErrors.Count);
            StoryDirectorSnapshot snapshot = director.CreateSnapshot();
            Assert.AreEqual(1, snapshot.ActiveBeatInstances.Count);
            Assert.AreEqual(StoryTestGraphs.ChoiceBeatId, snapshot.ActiveBeatInstances[0].BeatId);
        }

        [Test]
        public void EmitsRuntimeEventsWithoutMergingSameFrameFactChanges()
        {
            var director = new StoryDirector();
            director.LoadGraph(FactChangeGraph());
            var module = new StoryRuntimeModule(director);
            module.CommandBuffer.Enqueue(StoryRuntimeCommandFactory.RequestEnterBeat(
                RuntimeFrame.Zero,
                StoryRuntimeCommandSources.TestDriver,
                graphId: 111,
                beatId: 222));

            module.Tick(new RuntimeTickContext(0L, 0d, 0d, RuntimeTickStage.Simulation));
            var output = new List<StoryRuntimeEvent>();
            module.Events.Drain(RuntimeFrame.Zero, output);

            int factChangedCount = 0;
            for (int i = 0; i < output.Count; i++)
            {
                if (output[i].Kind == StoryEventKind.FactChanged)
                {
                    factChangedCount++;
                    Assert.AreEqual(RuntimeFrame.Zero, output[i].Frame);
                    Assert.AreEqual(222, output[i].BeatId);
                }
            }

            Assert.AreEqual(2, factChangedCount);
        }

        private static StoryGraphDefinition FactChangeGraph()
        {
            return new StoryGraphDefinition(
                111,
                1,
                222,
                new[]
                {
                    new StoryBeatDefinition(
                        222,
                        new[]
                        {
                            new StoryStepDefinition(1, StoryStepKind.SetFact, factKey: new StoryFactKey(111, 1), factValue: StoryValue.FromInt32(1)),
                            new StoryStepDefinition(2, StoryStepKind.SetFact, factKey: new StoryFactKey(111, 1), factValue: StoryValue.FromInt32(2))
                        })
                });
        }
    }
}
