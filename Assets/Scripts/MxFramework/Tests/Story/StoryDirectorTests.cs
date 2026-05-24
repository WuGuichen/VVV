using System.Collections.Generic;
using MxFramework.Story;
using NUnit.Framework;

namespace MxFramework.Tests.Story
{
    public class StoryDirectorTests
    {
        [Test]
        public void MinimalGraphCompletes()
        {
            var director = new StoryDirector();
            var events = new List<StoryEvent>();
            director.Events.Subscribe(events.Add);

            Assert.IsTrue(director.LoadGraph(StoryTestGraphs.MinimalChoiceGraph()));
            StoryEnterBeatResult enter = director.TryEnterBeat(
                StoryTestGraphs.GraphId,
                StoryTestGraphs.EntryBeatId,
                default);

            Assert.IsTrue(enter.Success);
            StoryDirectorSnapshot offered = director.CreateSnapshot();
            Assert.AreEqual(1, offered.ActiveBeatInstances.Count);
            Assert.AreEqual(StoryTestGraphs.ChoiceBeatId, offered.ActiveBeatInstances[0].BeatId);
            Assert.AreEqual(StoryTestGraphs.ChoiceSetId, offered.ActiveBeatInstances[0].AwaitingChoiceSetId);

            StoryChoiceResult choice = director.TryResolveChoice(
                offered.ActiveBeatInstances[0].BeatInstanceId,
                StoryTestGraphs.FirstChoiceId);

            Assert.IsTrue(choice.Success);
            StoryValue value;
            Assert.IsTrue(director.Blackboard.TryGet(new StoryFactKey(StoryTestGraphs.GraphId, StoryTestGraphs.FactId), out value));
            Assert.AreEqual(StoryValue.FromInt32(7), value);
            CollectionAssert.Contains(EventKinds(events), StoryEventKind.GraphCompleted);
        }

        [Test]
        public void RaiseTriggerEntersMatchingBeat()
        {
            var director = new StoryDirector();
            director.LoadGraph(StoryTestGraphs.MinimalChoiceGraph());

            StoryTriggerResult trigger = director.TryRaiseTrigger(
                StoryTestGraphs.TriggerId,
                new StoryActivationContext(frame: 0L, sourceId: 42));

            Assert.IsTrue(trigger.Success);
            Assert.AreEqual(StoryTestGraphs.GraphId, trigger.GraphId);
            Assert.AreEqual(StoryTestGraphs.EntryBeatId, trigger.BeatId);

            StoryDirectorSnapshot snapshot = director.CreateSnapshot();
            Assert.AreEqual(1, snapshot.ActiveBeatInstances.Count);
            Assert.AreEqual(StoryTestGraphs.ChoiceBeatId, snapshot.ActiveBeatInstances[0].BeatId);
        }

        [Test]
        public void ChoiceUsesChoiceIdNotIndex()
        {
            var director = new StoryDirector();
            director.LoadGraph(StoryTestGraphs.MinimalChoiceGraph());
            director.TryEnterBeat(StoryTestGraphs.GraphId, StoryTestGraphs.EntryBeatId, default);
            int beatInstanceId = director.CreateSnapshot().ActiveBeatInstances[0].BeatInstanceId;

            StoryChoiceResult invalidIndex = director.TryResolveChoice(beatInstanceId, 0);
            StoryChoiceResult secondChoice = director.TryResolveChoice(beatInstanceId, StoryTestGraphs.SecondChoiceId);

            Assert.IsFalse(invalidIndex.Success);
            Assert.AreEqual(StoryDirectorResultCode.InvalidChoiceId, invalidIndex.Code);
            Assert.IsTrue(secondChoice.Success);
            Assert.AreEqual(StoryGraphRuntimeStatus.Completed, director.CreateSnapshot().Graphs[0].Status);
        }

        [Test]
        public void CompletePresentationAdvancesWaitingBeat()
        {
            var director = new StoryDirector();
            director.LoadGraph(StoryTestGraphs.MinimalChoiceGraph(waitForPresentation: true));

            StoryEnterBeatResult enter = director.TryEnterBeat(StoryTestGraphs.GraphId, StoryTestGraphs.EntryBeatId, default);
            StoryDirectorSnapshot waiting = director.CreateSnapshot();

            Assert.IsTrue(enter.Success);
            Assert.AreEqual(StoryTestGraphs.EntryBeatId, waiting.ActiveBeatInstances[0].BeatId);
            Assert.AreEqual(StoryTestGraphs.PresentationStepId, waiting.ActiveBeatInstances[0].PendingPresentationStepId);

            StoryPresentationResult complete = director.CompletePresentation(
                waiting.ActiveBeatInstances[0].BeatInstanceId,
                StoryTestGraphs.PresentationStepId);

            Assert.IsTrue(complete.Success);
            StoryDirectorSnapshot offered = director.CreateSnapshot();
            Assert.AreEqual(StoryTestGraphs.ChoiceBeatId, offered.ActiveBeatInstances[0].BeatId);
            Assert.AreEqual(StoryTestGraphs.ChoiceSetId, offered.ActiveBeatInstances[0].AwaitingChoiceSetId);
        }

        [Test]
        public void GetChoicesReportsRequiredCount()
        {
            var director = new StoryDirector();
            director.LoadGraph(StoryTestGraphs.MinimalChoiceGraph());
            director.TryEnterBeat(StoryTestGraphs.GraphId, StoryTestGraphs.EntryBeatId, default);
            StoryBeatInstanceSnapshot beat = director.CreateSnapshot().ActiveBeatInstances[0];
            var buffer = new StoryChoiceView[1];

            int required = director.GetChoices(beat.BeatInstanceId, StoryTestGraphs.ChoiceSetId, buffer);

            Assert.AreEqual(2, required);
            Assert.AreEqual(StoryTestGraphs.FirstChoiceId, buffer[0].ChoiceId);
        }

        private static StoryEventKind[] EventKinds(IReadOnlyList<StoryEvent> events)
        {
            var kinds = new StoryEventKind[events.Count];
            for (int i = 0; i < events.Count; i++)
            {
                kinds[i] = events[i].Kind;
            }

            return kinds;
        }
    }
}
