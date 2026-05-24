using MxFramework.Story;

namespace MxFramework.Tests.Story
{
    internal static class StoryTestGraphs
    {
        public const int GraphId = 101;
        public const int EntryBeatId = 201;
        public const int ChoiceBeatId = 202;
        public const int EndBeatId = 203;
        public const int TriggerId = 301;
        public const int EntryLineStepId = 401;
        public const int ChoiceLineStepId = 402;
        public const int EndStepId = 403;
        public const int PresentationStepId = 404;
        public const int ChoiceSetId = 501;
        public const int FirstChoiceId = 601;
        public const int SecondChoiceId = 602;
        public const int FactId = 701;

        public static StoryGraphDefinition MinimalChoiceGraph(bool waitForPresentation = false)
        {
            var entrySteps = waitForPresentation
                ? new[]
                {
                    new StoryStepDefinition(EntryLineStepId, StoryStepKind.Line),
                    new StoryStepDefinition(
                        PresentationStepId,
                        StoryStepKind.Presentation,
                        waitPolicy: StoryPresentationWaitPolicy.WaitForCommand)
                }
                : new[]
                {
                    new StoryStepDefinition(EntryLineStepId, StoryStepKind.Line)
                };

            return new StoryGraphDefinition(
                GraphId,
                version: 1,
                EntryBeatId,
                new[]
                {
                    new StoryBeatDefinition(
                        EntryBeatId,
                        entrySteps,
                        branches: new[]
                        {
                            new StoryBranchDefinition(1, ChoiceBeatId, isFallback: true)
                        },
                        triggerIds: new[] { TriggerId }),
                    new StoryBeatDefinition(
                        ChoiceBeatId,
                        new[]
                        {
                            new StoryStepDefinition(ChoiceLineStepId, StoryStepKind.Line)
                        },
                        choices: new[]
                        {
                            new StoryChoiceDefinition(FirstChoiceId, labelTextKey: 9001, targetBeatId: EndBeatId),
                            new StoryChoiceDefinition(SecondChoiceId, labelTextKey: 9002, targetBeatId: 0)
                        },
                        choiceSetId: ChoiceSetId),
                    new StoryBeatDefinition(
                        EndBeatId,
                        new[]
                        {
                            new StoryStepDefinition(
                                EndStepId,
                                StoryStepKind.SetFact,
                                factKey: new StoryFactKey(GraphId, FactId),
                                factValue: StoryValue.FromInt32(7))
                        })
                });
        }
    }
}
