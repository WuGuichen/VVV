using MxFramework.Combat.Core;

namespace MxFramework.Combat.Animation
{
    public readonly struct ActionStartedEvent
    {
        public ActionStartedEvent(CombatEntityId entityId, int actionId, int actionInstanceId, CombatFrame frame)
        {
            EntityId = entityId;
            ActionId = actionId;
            ActionInstanceId = actionInstanceId;
            Frame = frame;
        }

        public CombatEntityId EntityId { get; }

        public int ActionId { get; }

        public int ActionInstanceId { get; }

        public CombatFrame Frame { get; }
    }

    public readonly struct ActionPhaseChangedEvent
    {
        public ActionPhaseChangedEvent(CombatEntityId entityId, CombatActionPhase oldPhase, CombatActionPhase newPhase, int localFrame)
        {
            EntityId = entityId;
            OldPhase = oldPhase;
            NewPhase = newPhase;
            LocalFrame = localFrame;
        }

        public CombatEntityId EntityId { get; }

        public CombatActionPhase OldPhase { get; }

        public CombatActionPhase NewPhase { get; }

        public int LocalFrame { get; }
    }

    public readonly struct ActionFrameEventRaisedEvent
    {
        public ActionFrameEventRaisedEvent(
            CombatEntityId entityId,
            int actionId,
            int actionInstanceId,
            CombatFrame worldFrame,
            int localFrame,
            CombatActionFrameEvent frameEvent)
        {
            EntityId = entityId;
            ActionId = actionId;
            ActionInstanceId = actionInstanceId;
            WorldFrame = worldFrame;
            LocalFrame = localFrame;
            FrameEvent = frameEvent;
        }

        public CombatEntityId EntityId { get; }

        public int ActionId { get; }

        public int ActionInstanceId { get; }

        public CombatFrame WorldFrame { get; }

        public int LocalFrame { get; }

        public CombatActionFrameEvent FrameEvent { get; }
    }

    public readonly struct ActionFinishedEvent
    {
        public ActionFinishedEvent(CombatEntityId entityId, int actionId, int actionInstanceId)
        {
            EntityId = entityId;
            ActionId = actionId;
            ActionInstanceId = actionInstanceId;
        }

        public CombatEntityId EntityId { get; }

        public int ActionId { get; }

        public int ActionInstanceId { get; }
    }

    public readonly struct ActionCanceledEvent
    {
        public ActionCanceledEvent(CombatEntityId entityId, int actionId, int actionInstanceId, string reason)
        {
            EntityId = entityId;
            ActionId = actionId;
            ActionInstanceId = actionInstanceId;
            Reason = reason ?? string.Empty;
        }

        public CombatEntityId EntityId { get; }

        public int ActionId { get; }

        public int ActionInstanceId { get; }

        public string Reason { get; }
    }

    public readonly struct ActionCancelRejectedEvent
    {
        public ActionCancelRejectedEvent(CombatEntityId entityId, int actionId, int nextActionId, string reason)
        {
            EntityId = entityId;
            ActionId = actionId;
            NextActionId = nextActionId;
            Reason = reason ?? string.Empty;
        }

        public CombatEntityId EntityId { get; }

        public int ActionId { get; }

        public int NextActionId { get; }

        public string Reason { get; }
    }
}
