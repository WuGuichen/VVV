using MxFramework.Runtime;

namespace MxFramework.Story.Runtime
{
    public readonly struct StoryRuntimeEvent
    {
        public StoryRuntimeEvent(
            RuntimeFrame frame,
            StoryEventKind kind,
            int graphId = 0,
            int beatId = 0,
            int beatInstanceId = 0,
            int stepId = 0,
            int choiceSetId = 0,
            int auxId = 0)
        {
            Frame = frame;
            Kind = kind;
            GraphId = graphId;
            BeatId = beatId;
            BeatInstanceId = beatInstanceId;
            StepId = stepId;
            ChoiceSetId = choiceSetId;
            AuxId = auxId;
        }

        public RuntimeFrame Frame { get; }
        public StoryEventKind Kind { get; }
        public int GraphId { get; }
        public int BeatId { get; }
        public int BeatInstanceId { get; }
        public int StepId { get; }
        public int ChoiceSetId { get; }
        public int AuxId { get; }

        public static StoryRuntimeEvent FromStoryEvent(RuntimeFrame frame, in StoryEvent evt)
        {
            return new StoryRuntimeEvent(
                frame,
                evt.Kind,
                evt.GraphId,
                evt.BeatId,
                evt.BeatInstanceId,
                evt.StepId,
                evt.ChoiceSetId,
                evt.AuxId);
        }
    }
}
