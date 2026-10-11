using MxFramework.Gameplay;

namespace MxFramework.Game.CharacterTest.TacticalCard
{
    internal enum TacticalStagePhase
    {
        Booting = 0, AwaitingIngressSettle = 1, Planning = 2,
        Executing = 3, TurnSettling = 4, StageCompleted = 5
    }

    internal enum TacticalStageHealth { Running = 0, AwaitingStoryTransfer = 1, SuspendedByFault = 2 }

    internal enum TacticalStageCommandKind
    {
        SelectAction = 1, CancelPending = 2, PlayUtility = 3,
        ConfirmExecution = 4, RequestRecovery = 5, RequestRestart = 6, SetExecutionInput = 7, ConfirmAutomaticExecution = 8
    }

    internal readonly struct TacticalStageCommand
    {
        public readonly ulong StageRunId, TurnIndex, CardInstanceId, SnapshotRevision;
        public readonly uint CardVersion;
        public readonly TacticalStageCommandKind Kind;
        public readonly TacticalExecutionInput ExecutionInput;

        public TacticalStageCommand(ulong stageRunId, ulong turnIndex, ulong cardInstanceId,
            uint cardVersion, ulong snapshotRevision, TacticalStageCommandKind kind)
            : this(stageRunId, turnIndex, cardInstanceId, cardVersion, snapshotRevision, kind, default) { }

        public TacticalStageCommand(ulong stageRunId, ulong turnIndex, ulong cardInstanceId,
            uint cardVersion, ulong snapshotRevision, TacticalStageCommandKind kind, TacticalExecutionInput executionInput)
        {
            StageRunId = stageRunId; TurnIndex = turnIndex; CardInstanceId = cardInstanceId;
            CardVersion = cardVersion; SnapshotRevision = snapshotRevision; Kind = kind;
            ExecutionInput = executionInput;
        }
    }

    internal readonly struct TacticalStoryStepToken
    {
        public readonly ulong StageRunId;
        public readonly int GraphId, BeatInstanceId, StepId, ActionId;

        public TacticalStoryStepToken(ulong stageRunId, int graphId, int beatInstanceId, int stepId, int actionId)
        {
            StageRunId = stageRunId; GraphId = graphId; BeatInstanceId = beatInstanceId;
            StepId = stepId; ActionId = actionId;
        }
    }

    /// <summary>Published Story identity; a runtime beat instance is bound on its first ingress event.</summary>
    internal readonly struct TacticalStoryStepBinding
    {
        public readonly int GraphId, StepId, ActionId;
        public TacticalStoryStepBinding(int graphId, int stepId, int actionId)
        {
            if (graphId <= 0 || stepId <= 0 || actionId < 0)
                throw new System.ArgumentOutOfRangeException(nameof(stepId));
            GraphId = graphId; StepId = stepId; ActionId = actionId;
        }

        public bool Matches(in TacticalStoryStepToken step) => GraphId > 0 && step.GraphId == GraphId &&
            step.StepId == StepId && step.ActionId == ActionId && step.BeatInstanceId > 0;
    }

    internal enum TacticalStageRejection
    {
        None = 0, WrongStage = 1, WrongPhase = 2, StaleSnapshot = 3, InvalidCard = 4,
        ExecutionRejected = 5, CriticalFlowPending = 6, FaultSuspended = 7,
        WrongTurn = 8, StaleCardVersion = 9, InsufficientMomentum = 10,
        UnsupportedPayload = 11, NoPendingAction = 12, InvalidCommand = 13,
        InvalidSpawnBinding = 14, StaleFrame = 15, DuplicateStoryStep = 16,
        WrongStoryStep = 17, IngressTimeout = 18, SpawnTimeout = 19,
        RecoveryRejected = 20, RestartRejected = 21, QueueFull = 22,
        InvalidSequence = 23, StoryBufferOverflow = 24, InvalidBattleVitals = 25,
        CriticalTriggerRejected = 26, StoryTransferTimeout = 27, StoryTransferRejected = 28, InvalidExecutionInput = 29,
        PreparationAlreadyActive = 30
    }

    internal interface ITacticalStageFlowCoordinator
    {
        TacticalStagePhase Phase { get; }
        TacticalStageHealth Health { get; }
        TacticalStageRejection Apply(in TacticalStageCommand command);
        void OnSpawnBound(ulong stageRunId, int slotIndex, GameplayEntityId entityId, long completedFrame);
        void OnStoryStepStarted(in TacticalStoryStepToken step);
        void OnCompletedSimulationTick(long completedFrame, in SettleSample player, in SettleSample opponent);
    }
}
