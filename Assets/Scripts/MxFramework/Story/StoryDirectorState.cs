using System.Collections.Generic;

namespace MxFramework.Story
{
    public enum StoryGraphRuntimeStatus : byte
    {
        Loaded = 0,
        Active = 1,
        Completed = 2,
        Aborted = 3
    }

    public readonly struct StoryActivationContext
    {
        public StoryActivationContext(long frame, int sourceId = 0, int triggerId = 0, int param0 = 0, int param1 = 0, int targetId = 0)
        {
            Frame = frame;
            SourceId = sourceId;
            TriggerId = triggerId;
            Param0 = param0;
            Param1 = param1;
            TargetId = targetId;
        }

        public long Frame { get; }
        public int SourceId { get; }
        public int TriggerId { get; }
        public int Param0 { get; }
        public int Param1 { get; }
        public int TargetId { get; }
    }

    public readonly struct StoryTickContext
    {
        public StoryTickContext(long frame, double deltaTime)
        {
            Frame = frame;
            DeltaTime = deltaTime;
        }

        public long Frame { get; }
        public double DeltaTime { get; }
    }

    public sealed class StoryDirectorSnapshot
    {
        public StoryDirectorSnapshot(
            int schemaVersion,
            int nextBeatInstanceId,
            IReadOnlyList<StoryGraphRuntimeSnapshot> graphs,
            IReadOnlyList<StoryBeatInstanceSnapshot> activeBeatInstances,
            IReadOnlyList<StoryFactEntry> facts)
        {
            SchemaVersion = schemaVersion;
            NextBeatInstanceId = nextBeatInstanceId;
            Graphs = StoryGraphDefinition.CopyList(graphs);
            ActiveBeatInstances = StoryGraphDefinition.CopyList(activeBeatInstances);
            Facts = StoryGraphDefinition.CopyList(facts);
        }

        public int SchemaVersion { get; }
        public int NextBeatInstanceId { get; }
        public IReadOnlyList<StoryGraphRuntimeSnapshot> Graphs { get; }
        public IReadOnlyList<StoryBeatInstanceSnapshot> ActiveBeatInstances { get; }
        public IReadOnlyList<StoryFactEntry> Facts { get; }
    }

    public readonly struct StoryGraphRuntimeSnapshot
    {
        public StoryGraphRuntimeSnapshot(int graphId, int version, StoryGraphRuntimeStatus status)
        {
            GraphId = graphId;
            Version = version;
            Status = status;
        }

        public int GraphId { get; }
        public int Version { get; }
        public StoryGraphRuntimeStatus Status { get; }
    }

    public sealed class StoryBeatInstanceSnapshot
    {
        public StoryBeatInstanceSnapshot(
            int graphId,
            int beatId,
            int beatInstanceId,
            int currentStepIndex,
            int pendingPresentationStepId,
            StoryPresentationWaitPolicy pendingPresentationPolicy,
            int awaitingChoiceSetId)
        {
            GraphId = graphId;
            BeatId = beatId;
            BeatInstanceId = beatInstanceId;
            CurrentStepIndex = currentStepIndex;
            PendingPresentationStepId = pendingPresentationStepId;
            PendingPresentationPolicy = pendingPresentationPolicy;
            AwaitingChoiceSetId = awaitingChoiceSetId;
        }

        public int GraphId { get; }
        public int BeatId { get; }
        public int BeatInstanceId { get; }
        public int CurrentStepIndex { get; }
        public int PendingPresentationStepId { get; }
        public StoryPresentationWaitPolicy PendingPresentationPolicy { get; }
        public int AwaitingChoiceSetId { get; }
        public bool IsWaitingForPresentation => PendingPresentationStepId > 0;
        public bool IsAwaitingChoice => AwaitingChoiceSetId > 0;
    }

    public sealed class StoryDirectorSaveState
    {
        public const int CurrentSchemaVersion = 1;

        public StoryDirectorSaveState(
            int schemaVersion,
            int nextBeatInstanceId,
            IReadOnlyList<StoryGraphSaveState> graphs,
            IReadOnlyList<StoryBeatInstanceSaveState> activeBeatInstances,
            IReadOnlyList<StoryFactEntry> facts)
        {
            SchemaVersion = schemaVersion;
            NextBeatInstanceId = nextBeatInstanceId;
            Graphs = StoryGraphDefinition.CopyList(graphs);
            ActiveBeatInstances = StoryGraphDefinition.CopyList(activeBeatInstances);
            Facts = StoryGraphDefinition.CopyList(facts);
        }

        public int SchemaVersion { get; }
        public int NextBeatInstanceId { get; }
        public IReadOnlyList<StoryGraphSaveState> Graphs { get; }
        public IReadOnlyList<StoryBeatInstanceSaveState> ActiveBeatInstances { get; }
        public IReadOnlyList<StoryFactEntry> Facts { get; }
    }

    public sealed class StoryGraphSaveState
    {
        public StoryGraphSaveState(StoryGraphDefinition definition, StoryGraphRuntimeStatus status)
        {
            Definition = definition;
            Status = status;
        }

        public StoryGraphDefinition Definition { get; }
        public StoryGraphRuntimeStatus Status { get; }
    }

    public sealed class StoryBeatInstanceSaveState
    {
        public StoryBeatInstanceSaveState(
            int graphId,
            int beatId,
            int beatInstanceId,
            int currentStepIndex,
            int pendingPresentationStepId,
            StoryPresentationWaitPolicy pendingPresentationPolicy,
            int awaitingChoiceSetId)
        {
            GraphId = graphId;
            BeatId = beatId;
            BeatInstanceId = beatInstanceId;
            CurrentStepIndex = currentStepIndex;
            PendingPresentationStepId = pendingPresentationStepId;
            PendingPresentationPolicy = pendingPresentationPolicy;
            AwaitingChoiceSetId = awaitingChoiceSetId;
        }

        public int GraphId { get; }
        public int BeatId { get; }
        public int BeatInstanceId { get; }
        public int CurrentStepIndex { get; }
        public int PendingPresentationStepId { get; }
        public StoryPresentationWaitPolicy PendingPresentationPolicy { get; }
        public int AwaitingChoiceSetId { get; }
    }
}
