namespace MxFramework.Game.CharacterTest.TacticalCard
{
    /// <summary>Optional NoAlloc notification after the deck/time owner actually settles an execution.</summary>
    internal interface ITacticalExecutionRetirementPort
    {
        void RetireSettledExecution(ulong executionId, in ExecutionObservation observation, long completedFrame);
    }

    /// <summary>Read-only selection capability forwarded by the existing execution owner.</summary>
    internal interface ITacticalBehindPreviewPort
    {
        bool TryPrepareBehindPreview(TacticalCardInstance card, out CharacterTestBehaviorExecutionMovement movement, out string issue);
        bool TrySampleBehindPreview(TacticalCardInstance card, in CharacterTestBehaviorExecutionMovement movement,
            out TacticalWorldPoint origin, out CharacterTestRuntimeAiLabMovementTargetSample sample);
    }

    /// <summary>Optional per-request input extension. Existing execution ports and input-free cards stay compatible.</summary>
    internal interface ITacticalExecutionInputPort : ITacticalExecutionPort
    {
        TacticalExecutionInputKind GetRequiredInputKind(TacticalCardInstance card);
        TacticalInputStatus ValidateInput(TacticalCardInstance card, in TacticalExecutionInput input);
        bool TryPrepare(TacticalCardInstance card, in TacticalExecutionInput input, out string issue);
        bool TryAdmit(TacticalCardInstance card, in TacticalExecutionInput input, out ulong executionId, out string issue);
    }

    /// <summary>Optional project hit consumer; the driver supplies its existing ledger, never a second balance.</summary>
    internal interface ITacticalHitRewardPort
    {
        bool TryApplyHitReward(ulong executionId, long hitEventId, int momentumAmount, IMomentumLedger ledger);
    }

    /// <summary>Single-owner execution admission. Prepare/Admit may allocate; observation and pre-action cleanup are NoAlloc.</summary>
    public interface ITacticalExecutionPort
    {
        bool TryPrepare(TacticalCardInstance card, out string issue);
        /// <summary>Admits the entire request or nothing. Does not tick the action or world.</summary>
        bool TryAdmit(TacticalCardInstance card, out ulong executionId, out string issue);
        ExecutionObservation Observe(ulong executionId);
        /// <summary>Rejects cleanup if an action has started or an unacknowledged start may still execute.</summary>
        bool TryAbortPreAction(ulong executionId, string reason);
    }

    /// <summary>Facts for one admitted execution; an unknown ID returns the all-false default.</summary>
    public readonly struct ExecutionObservation
    {
        public readonly ulong ExecutionId;
        public readonly bool ActionStarted;
        public readonly bool ActionTimelineCompleted;
        public readonly bool BehaviorTerminal;
        public readonly bool OwnedIntentsReleased;
        public readonly bool CommandsAcknowledged;
        public readonly bool ExecutionStarted;
        public readonly long ExecutionStartedTick;
        public readonly string Issue;

        public ExecutionObservation(ulong executionId, bool actionStarted, bool actionTimelineCompleted,
            bool behaviorTerminal, bool ownedIntentsReleased, bool commandsAcknowledged, string issue = "")
            : this(executionId, actionStarted, actionTimelineCompleted, behaviorTerminal, ownedIntentsReleased,
                commandsAcknowledged, actionStarted, issue) { }

        internal ExecutionObservation(ulong executionId, bool actionStarted, bool actionTimelineCompleted,
            bool behaviorTerminal, bool ownedIntentsReleased, bool commandsAcknowledged, bool executionStarted, string issue = "", long executionStartedTick = -1)
        {
            ExecutionId = executionId;
            ActionStarted = actionStarted;
            ActionTimelineCompleted = actionTimelineCompleted;
            BehaviorTerminal = behaviorTerminal;
            OwnedIntentsReleased = ownedIntentsReleased;
            CommandsAcknowledged = commandsAcknowledged;
            ExecutionStarted = executionStarted;
            ExecutionStartedTick = executionStartedTick;
            Issue = issue;
        }
    }
}
