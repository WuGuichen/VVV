using MxFramework.Story.Runtime;
using UnityEngine;

namespace MxFramework.Story.Unity
{
    [DisallowMultipleComponent]
    public sealed class StoryPresentationCompletionAdapter : StoryUnityCommandAdapter
    {
        [SerializeField] private int _beatInstanceId;
        [SerializeField] private int _stepId;
        [SerializeField] private int _graphId;

        public int BeatInstanceId
        {
            get => _beatInstanceId;
            set => _beatInstanceId = value;
        }

        public int StepId
        {
            get => _stepId;
            set => _stepId = value;
        }

        public int GraphId
        {
            get => _graphId;
            set => _graphId = value;
        }

        protected override int DefaultSourceId => StoryRuntimeCommandSources.PresentationAdapter;

        public StoryUnityCommandResult CompletePresentation()
        {
            return CompletePresentation(_beatInstanceId, _stepId, _graphId);
        }

        public StoryUnityCommandResult CompletePresentation(int beatInstanceId, int stepId, int graphId = 0)
        {
            return Enqueue(StoryRuntimeCommandFactory.CompletePresentation(
                ResolveFrame(),
                SourceId,
                beatInstanceId,
                stepId,
                graphId,
                TraceId));
        }
    }
}
