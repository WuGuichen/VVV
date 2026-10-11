using System.Collections.Generic;

namespace MxFramework.Tests.Gameplay.Triggers
{
    internal enum GameplayTriggerTransactionReferenceDecision
    {
        Committed = 0,
        ConditionRejected = 1,
        PolicyRejected = 2,
        ArbitrationSuppressed = 3,
        CoreCapacityRejected = 4,
        ReservationRejected = 5
    }

    internal enum GameplayTriggerTransactionReferenceEventKind
    {
        Prepare = 0,
        Abort = 1,
        Commit = 2
    }

    internal readonly struct GameplayTriggerTransactionReferenceEvent
    {
        public GameplayTriggerTransactionReferenceEvent(
            GameplayTriggerTransactionReferenceEventKind kind,
            int sinkId,
            int actionIndex,
            int token)
        {
            Kind = kind;
            SinkId = sinkId;
            ActionIndex = actionIndex;
            Token = token;
        }

        public GameplayTriggerTransactionReferenceEventKind Kind { get; }
        public int SinkId { get; }
        public int ActionIndex { get; }
        public int Token { get; }
    }

    internal readonly struct GameplayTriggerTransactionReferencePlan
    {
        public GameplayTriggerTransactionReferencePlan(
            int actionCount,
            GameplayTriggerTransactionReferenceDecision preflightDecision =
                GameplayTriggerTransactionReferenceDecision.Committed,
            int rejectPrepareActionIndex = -1,
            int[] actionSinkIds = null)
        {
            ActionCount = actionCount;
            PreflightDecision = preflightDecision;
            RejectPrepareActionIndex = rejectPrepareActionIndex;
            ActionSinkIds = actionSinkIds;
        }

        public int ActionCount { get; }
        public GameplayTriggerTransactionReferenceDecision PreflightDecision { get; }
        public int RejectPrepareActionIndex { get; }
        public int[] ActionSinkIds { get; }

        public int GetActionSinkId(int actionIndex) =>
            ActionSinkIds == null ? 0 : ActionSinkIds[actionIndex];
    }

    /// <summary>
    /// Test-only activation transaction oracle. It models preflight, reservation,
    /// reverse abort, and commit deltas without using runtime buffers or sinks.
    /// </summary>
    internal sealed class GameplayTriggerTransactionReferenceModel
    {
        private readonly List<GameplayTriggerTransactionReferenceEvent> _events =
            new List<GameplayTriggerTransactionReferenceEvent>();
        private readonly Dictionary<int, int> _nextTokenBySink =
            new Dictionary<int, int>();

        public IReadOnlyList<GameplayTriggerTransactionReferenceEvent> Events => _events;
        public long NextActivationId { get; private set; } = 1L;
        public int PolicyCommitCount { get; private set; }
        public int ActivationCount { get; private set; }
        public int IntentCount { get; private set; }

        public GameplayTriggerTransactionReferenceDecision Execute(
            in GameplayTriggerTransactionReferencePlan plan)
        {
            _events.Clear();
            if (plan.PreflightDecision != GameplayTriggerTransactionReferenceDecision.Committed)
            {
                return plan.PreflightDecision;
            }

            int[] tokens = new int[plan.ActionCount];
            for (int actionIndex = 0; actionIndex < plan.ActionCount; actionIndex++)
            {
                int sinkId = plan.GetActionSinkId(actionIndex);
                _events.Add(new GameplayTriggerTransactionReferenceEvent(
                    GameplayTriggerTransactionReferenceEventKind.Prepare,
                    sinkId,
                    actionIndex,
                    0));
                if (actionIndex == plan.RejectPrepareActionIndex)
                {
                    for (int preparedIndex = actionIndex - 1; preparedIndex >= 0; preparedIndex--)
                    {
                        _events.Add(new GameplayTriggerTransactionReferenceEvent(
                            GameplayTriggerTransactionReferenceEventKind.Abort,
                            plan.GetActionSinkId(preparedIndex),
                            preparedIndex,
                            tokens[preparedIndex]));
                    }
                    return GameplayTriggerTransactionReferenceDecision.ReservationRejected;
                }
                if (!_nextTokenBySink.TryGetValue(sinkId, out int nextToken))
                {
                    nextToken = 1;
                }
                tokens[actionIndex] = nextToken;
                _nextTokenBySink[sinkId] = nextToken + 1;
            }

            NextActivationId++;
            PolicyCommitCount++;
            ActivationCount++;
            IntentCount += plan.ActionCount;
            for (int actionIndex = 0; actionIndex < plan.ActionCount; actionIndex++)
            {
                _events.Add(new GameplayTriggerTransactionReferenceEvent(
                    GameplayTriggerTransactionReferenceEventKind.Commit,
                    plan.GetActionSinkId(actionIndex),
                    actionIndex,
                    tokens[actionIndex]));
            }
            return GameplayTriggerTransactionReferenceDecision.Committed;
        }
    }
}
