using System;
using System.Collections.Generic;
using MxFramework.Combat.Core;
using MxFramework.Core.Pooling;

namespace MxFramework.Combat.Animation
{
    public sealed class CombatActionRunner
    {
        private const string MissingTimelineReason = "Action timeline is not registered.";
        private const string NoRunningActionReason = "Entity has no running action.";
        private const string OutsideCancelWindowReason = "Current action is not in a cancel window.";
        private const string UncancelableActionReason = "Next action is not allowed by the current cancel window.";
        private const string ForceStartReason = "ForceStartAction replaced the running action.";
        private const string ForceCancelReason = "ForceCancel canceled the running action.";

        private readonly CombatActionRegistry _registry;
        private readonly Dictionary<CombatEntityId, RunningAction> _runningActions = new Dictionary<CombatEntityId, RunningAction>();
        private readonly List<CombatEntityId> _entityOrder = new List<CombatEntityId>();
        private int _nextActionInstanceId = 1;

        public CombatActionRunner(CombatActionRegistry registry)
        {
            _registry = registry ?? throw new ArgumentNullException(nameof(registry));
        }

        public event Action<ActionStartedEvent> ActionStarted;

        public event Action<ActionPhaseChangedEvent> ActionPhaseChanged;

        public event Action<ActionFrameEventRaisedEvent> ActionFrameEventRaised;

        public event Action<ActionFinishedEvent> ActionFinished;

        public event Action<ActionCanceledEvent> ActionCanceled;

        public event Action<ActionCancelRejectedEvent> ActionCancelRejected;

        public ActionResult StartAction(CombatEntityId entityId, int actionId, CombatFrame currentFrame)
        {
            if (!_registry.TryGetTimeline(actionId, out CombatActionTimeline timeline))
            {
                return Reject(entityId, 0, actionId, MissingTimelineReason);
            }

            if (_runningActions.TryGetValue(entityId, out RunningAction running))
            {
                if (!CanCancelTo(running, actionId, out string reason))
                {
                    return Reject(entityId, running.Timeline.ActionId, actionId, reason);
                }

                CancelRunning(entityId, running, "Canceled into next action.");
            }

            return StartResolvedAction(entityId, timeline, currentFrame);
        }

        public ActionResult ForceStartAction(CombatEntityId entityId, int actionId, CombatFrame currentFrame)
        {
            if (!_registry.TryGetTimeline(actionId, out CombatActionTimeline timeline))
            {
                return Reject(entityId, 0, actionId, MissingTimelineReason);
            }

            if (_runningActions.TryGetValue(entityId, out RunningAction running))
            {
                CancelRunning(entityId, running, ForceStartReason);
            }

            return StartResolvedAction(entityId, timeline, currentFrame);
        }

        public void TickActions(CombatFrame currentFrame)
        {
            for (int i = 0; i < _entityOrder.Count;)
            {
                CombatEntityId entityId = _entityOrder[i];
                RunningAction running = _runningActions[entityId];
                running.LocalFrame++;

                CombatActionPhase newPhase = running.Timeline.GetPhase(running.LocalFrame);
                if (newPhase != running.Phase)
                {
                    CombatActionPhase oldPhase = running.Phase;
                    running.Phase = newPhase;
                    ActionPhaseChanged?.Invoke(new ActionPhaseChangedEvent(entityId, oldPhase, newPhase, running.LocalFrame));
                }

                if (newPhase == CombatActionPhase.Finished)
                {
                    _runningActions.Remove(entityId);
                    _entityOrder.RemoveAt(i);
                    ActionFinished?.Invoke(new ActionFinishedEvent(entityId, running.Timeline.ActionId, running.ActionInstanceId));
                    continue;
                }

                _runningActions[entityId] = running;
                PublishFrameEvents(entityId, running, currentFrame);
                i++;
            }
        }

        public ActionResult TryCancel(CombatEntityId entityId, int nextActionId, CombatFrame currentFrame)
        {
            if (!_runningActions.TryGetValue(entityId, out RunningAction running))
            {
                return Reject(entityId, 0, nextActionId, NoRunningActionReason);
            }

            if (!_registry.TryGetTimeline(nextActionId, out CombatActionTimeline nextTimeline))
            {
                return Reject(entityId, running.Timeline.ActionId, nextActionId, MissingTimelineReason);
            }

            if (!CanCancelTo(running, nextActionId, out string reason))
            {
                return Reject(entityId, running.Timeline.ActionId, nextActionId, reason);
            }

            CancelRunning(entityId, running, "Canceled into next action.");
            return StartResolvedAction(entityId, nextTimeline, currentFrame);
        }

        public bool ForceCancel(CombatEntityId entityId)
        {
            if (!_runningActions.TryGetValue(entityId, out RunningAction running))
            {
                return false;
            }

            CancelRunning(entityId, running, ForceCancelReason);
            return true;
        }

        public CombatActionState[] GetRunningActions()
        {
            var results = new CombatActionState[_entityOrder.Count];
            for (int i = 0; i < _entityOrder.Count; i++)
            {
                CombatEntityId entityId = _entityOrder[i];
                results[i] = _runningActions[entityId].ToState(entityId);
            }

            return results;
        }

        public CombatActionState? GetActionState(CombatEntityId entityId)
        {
            if (!_runningActions.TryGetValue(entityId, out RunningAction running))
            {
                return null;
            }

            return running.ToState(entityId);
        }

        public bool IsInCancelWindow(CombatEntityId entityId, int nextActionId)
        {
            return _runningActions.TryGetValue(entityId, out RunningAction running)
                && IsInTargetWindow(running, CombatActionWindowKind.Cancel, nextActionId);
        }

        public bool IsInInvincibleWindow(CombatEntityId entityId)
        {
            return IsInWindow(entityId, CombatActionWindowKind.Invincible);
        }

        public bool IsInParryWindow(CombatEntityId entityId)
        {
            return IsInWindow(entityId, CombatActionWindowKind.Parry);
        }

        public bool IsInSuperArmorWindow(CombatEntityId entityId)
        {
            return IsInWindow(entityId, CombatActionWindowKind.SuperArmor);
        }

        public CombatActionPhase GetCurrentPhase(CombatEntityId entityId)
        {
            return _runningActions.TryGetValue(entityId, out RunningAction running)
                ? running.Phase
                : CombatActionPhase.None;
        }

        public CombatActionSupportProfile? GetCurrentSupportProfile(CombatEntityId entityId)
        {
            return _runningActions.TryGetValue(entityId, out RunningAction running)
                ? running.Timeline.SupportProfile
                : null;
        }

        public int GetCurrentLocalFrame(CombatEntityId entityId)
        {
            return _runningActions.TryGetValue(entityId, out RunningAction running)
                ? running.LocalFrame
                : -1;
        }

        public int GetActionInstanceId(CombatEntityId entityId)
        {
            return _runningActions.TryGetValue(entityId, out RunningAction running)
                ? running.ActionInstanceId
                : 0;
        }

        private ActionResult StartResolvedAction(CombatEntityId entityId, CombatActionTimeline timeline, CombatFrame currentFrame)
        {
            int instanceId = _nextActionInstanceId++;
            var instance = new CombatActionInstance(entityId, timeline, currentFrame);
            var running = new RunningAction(
                instance,
                instanceId,
                localFrame: 0,
                phase: timeline.GetPhase(0));

            _runningActions[entityId] = running;
            AddEntityOrder(entityId);
            ActionStarted?.Invoke(new ActionStartedEvent(entityId, timeline.ActionId, instanceId, currentFrame));
            PublishFrameEvents(entityId, running, currentFrame);
            return ActionResult.Succeeded(instanceId);
        }

        private void PublishFrameEvents(CombatEntityId entityId, RunningAction running, CombatFrame worldFrame)
        {
            Action<ActionFrameEventRaisedEvent> handler = ActionFrameEventRaised;
            if (handler == null)
            {
                return;
            }

            using (PooledList<CombatActionFrameEvent> pooled = ListPool<CombatActionFrameEvent>.Get(out List<CombatActionFrameEvent> frameEvents))
            {
                running.Timeline.CollectEvents(running.LocalFrame, frameEvents);
                for (int i = 0; i < frameEvents.Count; i++)
                {
                    handler(new ActionFrameEventRaisedEvent(
                        entityId,
                        running.Timeline.ActionId,
                        running.ActionInstanceId,
                        worldFrame,
                        running.LocalFrame,
                        frameEvents[i]));
                }
            }
        }

        private void CancelRunning(CombatEntityId entityId, RunningAction running, string reason)
        {
            _runningActions.Remove(entityId);
            RemoveEntityOrder(entityId);
            ActionCanceled?.Invoke(new ActionCanceledEvent(entityId, running.Timeline.ActionId, running.ActionInstanceId, reason));
        }

        private ActionResult Reject(CombatEntityId entityId, int actionId, int nextActionId, string reason)
        {
            ActionCancelRejected?.Invoke(new ActionCancelRejectedEvent(entityId, actionId, nextActionId, reason));
            return ActionResult.Failed(reason);
        }

        private bool CanCancelTo(RunningAction running, int nextActionId, out string reason)
        {
            bool hasCancelWindowAtFrame = false;
            for (int i = 0; i < running.Timeline.WindowCount; i++)
            {
                CombatActionWindow window = running.Timeline.GetWindow(i);
                if (window.Kind != CombatActionWindowKind.Cancel || !window.Contains(running.LocalFrame))
                {
                    continue;
                }

                hasCancelWindowAtFrame = true;
                if (window.TargetActionId == 0 || window.TargetActionId == nextActionId)
                {
                    reason = string.Empty;
                    return true;
                }
            }

            reason = hasCancelWindowAtFrame ? UncancelableActionReason : OutsideCancelWindowReason;
            return false;
        }

        private bool IsInWindow(CombatEntityId entityId, CombatActionWindowKind kind)
        {
            return _runningActions.TryGetValue(entityId, out RunningAction running)
                && running.Timeline.IsInWindow(kind, running.LocalFrame);
        }

        private bool IsInTargetWindow(RunningAction running, CombatActionWindowKind kind, int targetActionId)
        {
            for (int i = 0; i < running.Timeline.WindowCount; i++)
            {
                CombatActionWindow window = running.Timeline.GetWindow(i);
                if (window.Kind == kind
                    && window.Contains(running.LocalFrame)
                    && (window.TargetActionId == 0 || window.TargetActionId == targetActionId))
                {
                    return true;
                }
            }

            return false;
        }

        private void AddEntityOrder(CombatEntityId entityId)
        {
            int index = _entityOrder.BinarySearch(entityId);
            if (index < 0)
            {
                _entityOrder.Insert(~index, entityId);
            }
        }

        private void RemoveEntityOrder(CombatEntityId entityId)
        {
            int index = _entityOrder.BinarySearch(entityId);
            if (index >= 0)
            {
                _entityOrder.RemoveAt(index);
            }
        }

        private struct RunningAction
        {
            public RunningAction(
                CombatActionInstance instance,
                int actionInstanceId,
                int localFrame,
                CombatActionPhase phase)
            {
                Instance = instance;
                ActionInstanceId = actionInstanceId;
                LocalFrame = localFrame;
                Phase = phase;
            }

            public CombatActionInstance Instance { get; }

            public CombatActionTimeline Timeline => Instance.Timeline;

            public int ActionInstanceId { get; }

            public int LocalFrame { get; set; }

            public CombatActionPhase Phase { get; set; }

            public CombatActionState ToState(CombatEntityId entityId)
            {
                return new CombatActionState(
                    entityId,
                    Timeline.ActionId,
                    LocalFrame,
                    Instance.StartedAtFrame,
                    Phase,
                    ActionInstanceId);
            }
        }
    }
}
