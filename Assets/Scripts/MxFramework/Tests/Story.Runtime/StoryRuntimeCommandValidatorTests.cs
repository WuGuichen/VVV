using MxFramework.Runtime;
using MxFramework.Story;
using MxFramework.Story.Runtime;
using MxFramework.Tests.Story;
using NUnit.Framework;

namespace MxFramework.Tests.Story.Runtime
{
    public class StoryRuntimeCommandValidatorTests
    {
        [Test]
        public void RegistersFiveAdr005Commands()
        {
            RuntimeCommandRegistrySnapshot snapshot = StoryRuntimeCommandRegistry.CreateDefault().CreateSnapshot();

            Assert.AreEqual(5, snapshot.Definitions.Count);
            Assert.AreEqual(StoryRuntimeCommandIds.RaiseTrigger, snapshot.Definitions[0].CommandId);
            Assert.AreEqual(StoryRuntimeCommandIds.SelectChoice, snapshot.Definitions[1].CommandId);
            Assert.AreEqual(StoryRuntimeCommandIds.CompletePresentation, snapshot.Definitions[2].CommandId);
            Assert.AreEqual(StoryRuntimeCommandIds.RequestEnterBeat, snapshot.Definitions[3].CommandId);
            Assert.AreEqual(StoryRuntimeCommandIds.AbortGraph, snapshot.Definitions[4].CommandId);
        }

        [Test]
        public void RejectsDebugCommandFromNonDebugSource()
        {
            StoryDirector director = LoadedDirector();
            var validator = new StoryRuntimeCommandValidator(director);
            RuntimeCommand command = StoryRuntimeCommandFactory.RequestEnterBeat(
                RuntimeFrame.Zero,
                StoryRuntimeCommandSources.Input,
                StoryTestGraphs.GraphId,
                StoryTestGraphs.EntryBeatId);

            RuntimeCommandValidationResult result = validator.Validate(command);

            Assert.IsFalse(result.Success);
            Assert.AreEqual(RuntimeCommandErrorCode.InvalidPayload, result.Error.Code);
        }

        [Test]
        public void RejectsUnknownCommandIdsAndInvalidPayloads()
        {
            StoryDirector director = LoadedDirector();
            var validator = new StoryRuntimeCommandValidator(director);

            RuntimeCommandValidationResult unknown = validator.Validate(new RuntimeCommand(
                RuntimeFrame.Zero,
                StoryRuntimeCommandSources.TestDriver,
                999999,
                0));
            RuntimeCommandValidationResult invalidPayload = validator.Validate(StoryRuntimeCommandFactory.RaiseTrigger(
                RuntimeFrame.Zero,
                StoryRuntimeCommandSources.TestDriver,
                triggerId: 0));

            Assert.IsFalse(unknown.Success);
            Assert.AreEqual(RuntimeCommandErrorCode.UnregisteredCommandId, unknown.Error.Code);
            Assert.IsFalse(invalidPayload.Success);
            Assert.AreEqual(RuntimeCommandErrorCode.InvalidPayload, invalidPayload.Error.Code);
        }

        [Test]
        public void RejectsStaleBeatInstancesAndUnknownChoices()
        {
            StoryDirector director = LoadedDirector();
            director.TryEnterBeat(StoryTestGraphs.GraphId, StoryTestGraphs.EntryBeatId, default);
            int beatInstanceId = director.CreateSnapshot().ActiveBeatInstances[0].BeatInstanceId;
            var validator = new StoryRuntimeCommandValidator(director);

            RuntimeCommandValidationResult unknownChoice = validator.Validate(StoryRuntimeCommandFactory.SelectChoice(
                RuntimeFrame.Zero,
                StoryRuntimeCommandSources.TestDriver,
                beatInstanceId,
                choiceId: 123456,
                graphId: StoryTestGraphs.GraphId));
            director.TryResolveChoice(beatInstanceId, StoryTestGraphs.SecondChoiceId);
            RuntimeCommandValidationResult stale = validator.Validate(StoryRuntimeCommandFactory.SelectChoice(
                RuntimeFrame.Zero,
                StoryRuntimeCommandSources.TestDriver,
                beatInstanceId,
                StoryTestGraphs.SecondChoiceId,
                StoryTestGraphs.GraphId));

            Assert.IsFalse(unknownChoice.Success);
            Assert.IsFalse(stale.Success);
            Assert.AreEqual(RuntimeCommandErrorCode.InvalidPayload, stale.Error.Code);
        }

        [Test]
        public void AcceptsCompletePresentationForWaitingStep()
        {
            var director = new StoryDirector();
            director.LoadGraph(StoryTestGraphs.MinimalChoiceGraph(waitForPresentation: true));
            director.TryEnterBeat(StoryTestGraphs.GraphId, StoryTestGraphs.EntryBeatId, default);
            int beatInstanceId = director.CreateSnapshot().ActiveBeatInstances[0].BeatInstanceId;
            var validator = new StoryRuntimeCommandValidator(director);

            RuntimeCommandValidationResult result = validator.Validate(StoryRuntimeCommandFactory.CompletePresentation(
                RuntimeFrame.Zero,
                StoryRuntimeCommandSources.PresentationAdapter,
                beatInstanceId,
                StoryTestGraphs.PresentationStepId,
                StoryTestGraphs.GraphId));

            Assert.IsTrue(result.Success);
        }

        private static StoryDirector LoadedDirector()
        {
            var director = new StoryDirector();
            director.LoadGraph(StoryTestGraphs.MinimalChoiceGraph());
            return director;
        }
    }
}
