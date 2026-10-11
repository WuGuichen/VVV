namespace MxFramework.Story
{
    public enum StoryEventKind : byte
    {
        GraphLoaded = 1,
        GraphCompleted = 2,
        GraphAborted = 3,
        BeatEntered = 10,
        BeatExited = 11,
        StepStarted = 20,
        StepCompleted = 21,
        ChoiceOffered = 30,
        ChoiceResolved = 31,
        FactChanged = 40
    }

    public readonly struct StoryEvent
    {
        public StoryEvent(
            StoryEventKind kind,
            int graphId = 0,
            int beatId = 0,
            int beatInstanceId = 0,
            int stepId = 0,
            int choiceSetId = 0,
            int auxId = 0)
        {
            Kind = kind;
            GraphId = graphId;
            BeatId = beatId;
            BeatInstanceId = beatInstanceId;
            StepId = stepId;
            ChoiceSetId = choiceSetId;
            AuxId = auxId;
        }

        public StoryEventKind Kind { get; }
        public int GraphId { get; }
        public int BeatId { get; }
        public int BeatInstanceId { get; }
        public int StepId { get; }
        public int ChoiceSetId { get; }
        public int AuxId { get; }
    }
}
