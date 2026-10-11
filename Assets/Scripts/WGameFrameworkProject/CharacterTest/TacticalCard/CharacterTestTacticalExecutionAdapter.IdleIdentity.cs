using System;
using System.Linq;
using MxFramework.CharacterAction;

namespace MxFramework.Game.CharacterTest.TacticalCard
{
    // Private paused-session transport only. This is historical evidence, never an executable request.
    internal sealed class TacticalIdleExecutionSnapshot
    {
        public ulong NextId, LastId, CardInstance;
        public uint CardVersion;
        public long CompletedFrame, StartedTick;
        public int Mode, SessionState, EndReason, ActionState, MotionResult;
        public bool ActionStarted, TimelineCompleted, Terminal, Released, Acknowledged, Started, Aborted;
        public string Issue, Warning, ObservationIssue;
        public long[] RewardedHits;
    }

    internal sealed partial class CharacterTestTacticalExecutionAdapter
    {
        private bool _retired;
        private bool _retiredReceiptSealed;
        private uint _admittedVersion;
        private long _retiredFrame;
        private int _retiredMode, _retiredSessionState, _retiredEndReason, _retiredActionState, _retiredMotionResult;
        private ExecutionObservation _retiredObservation;
        private string _retiredIssue, _retiredWarning;
        private bool _retiredAborted;

        private void BeginExecutionIdentity(TacticalCardInstance card)
        {
            _executionId = ++_nextId;
            _admitted = card;
            _admittedVersion = card.Version;
            _retired = false;
            _retiredReceiptSealed = false;
            _retiredObservation = default;
            _observedActionState = CharacterActionInstanceState.None;
        }

        /// <summary>NoAlloc. Only the existing deck/time owner calls this after successful real settlement.</summary>
        public void RetireSettledExecution(ulong executionId, in ExecutionObservation observation, long completedFrame)
        {
            SessionSnapshotCheck.Require(!_retired && !_faulted && executionId != 0 && executionId == _executionId &&
                observation.ExecutionId == executionId && observation.BehaviorTerminal && _admitted != null &&
                _admitted.Zone != CardZone.Resolving && completedFrame >= 0, "execution-retirement-owner");
            _retiredObservation = observation;
            _retiredFrame = completedFrame;
            _retiredMode = _recallExecution ? 3 : _tractionExecution ? 2 : _pointExecution ? 1 : 0;
            _retiredSessionState = _retiredMode < 2 ? (int)_session.State : (int)CharacterTestRuntimeAiLabLogicRuntimeState.Inactive;
            _retiredEndReason = _retiredMode < 2 ? (int)_session.EndReason : (int)CharacterTestBehaviorExecutionEndReason.None;
            _retiredActionState = _retiredMode == 0 ? (int)_observedActionState : (int)CharacterActionInstanceState.None;
            _retiredMotionResult = _recallExecution && _entity.PositionRecall.Matches(_recallBinding.StageRun, executionId)
                ? (int)_entity.PositionRecall.Result : _tractionExecution && _entity.GroundPointTraction.Matches(_tractionStage, executionId)
                ? (int)_entity.GroundPointTraction.End : 0;
            _retiredIssue = LastIssue; _retiredWarning = LastWarning;
            _retiredAborted = _aborted || _recallExecution && _recallCancelled || _tractionExecution && _tractionCancelled;
            _retired = true;
        }

        internal void ValidateIdleExecutionBoundary()
        {
            ReconcileRetiredReceipt();
            SessionSnapshotCheck.Require(!_faulted && !_session.IsExecuting && !_session.HasPendingActionCommand &&
                _commands.PendingCommandCount == 0 && _commands.LastCommandErrors.Count == 0 &&
                (_admitted == null || _admitted.Zone != CardZone.Resolving) &&
                (_nextId == 0 ? _executionId == 0 && !_retired : _retired && _executionId == _nextId &&
                    _retiredReceiptSealed &&
                    _retiredObservation.ExecutionId == _executionId && _retiredObservation.BehaviorTerminal &&
                    _retiredObservation.OwnedIntentsReleased && _retiredObservation.CommandsAcknowledged),
                "execution-idle-terminal-receipt-required");
        }

        // Settlement can precede free recovery and cleanup acknowledgements. Observe their real final facts;
        // never tick/restart a retired host, invent completion, or alter the gameplay pause detector.
        private void ReconcileRetiredReceipt()
        {
            if (!_retired || _retiredReceiptSealed || _entity.Action.State == CharacterTestActionStateId.Running) return;
            bool ack = _commands.PendingCommandCount == 0 && _commands.LastCommandErrors.Count == 0 && !_session.HasPendingActionCommand;
            bool released = ack && !_entity.Input.HasActiveMove &&
                (!_entity.Input.BehaviorControl.IsPresent || _entity.Input.BehaviorControl.IsRelease);
            if (!released || _session.IsExecuting) return;
            var previous = _retiredObservation;
            bool completed = previous.ActionTimelineCompleted;
            if (_retiredMode == 0 && _actionInstance != null) {
                _retiredActionState = (int)_actionInstance.State;
                completed = _actionInstance.State == CharacterActionInstanceState.Finished;
            }
            _retiredObservation = new ExecutionObservation(previous.ExecutionId, previous.ActionStarted, completed,
                previous.BehaviorTerminal, released, ack, previous.ExecutionStarted, previous.Issue, previous.ExecutionStartedTick);
            _retiredReceiptSealed = true;
        }

        internal TacticalIdleExecutionSnapshot CaptureIdleExecutionIdentity()
        {
            ValidateIdleExecutionBoundary();
            if (_nextId == 0) return null;
            var observation = _retiredObservation;
            var hits = new long[_rewardedHitCount];
            Array.Copy(_rewardedHits, hits, hits.Length);
            return new TacticalIdleExecutionSnapshot {
                NextId = _nextId, LastId = _executionId, CardInstance = _admitted.InstanceId, CardVersion = _admittedVersion,
                CompletedFrame = _retiredFrame, Mode = _retiredMode, SessionState = _retiredSessionState,
                EndReason = _retiredEndReason, ActionState = _retiredActionState, MotionResult = _retiredMotionResult,
                ActionStarted = observation.ActionStarted, TimelineCompleted = observation.ActionTimelineCompleted,
                Terminal = observation.BehaviorTerminal, Released = observation.OwnedIntentsReleased,
                Acknowledged = observation.CommandsAcknowledged, Started = observation.ExecutionStarted,
                StartedTick = observation.ExecutionStartedTick, Aborted = _retiredAborted,
                Issue = _retiredIssue, Warning = _retiredWarning, ObservationIssue = observation.Issue, RewardedHits = hits };
        }

        // Cold import into a fresh, unpublished adapter. No compile, action/command, payment or reward is replayed.
        internal void RestoreIdleExecutionIdentity(TacticalIdleExecutionSnapshot saved, TacticalDeckManager deck, long frame)
        {
            SessionSnapshotCheck.Require(_nextId == 0 && _executionId == 0 && !_session.IsExecuting, "fresh-idle-execution-owner");
            if (saved == null) return;
            var card = deck.Hand.Concat(deck.DrawPile).Concat(deck.DiscardPile).Concat(deck.ExhaustPile)
                .Concat(deck.PendingAction == null ? Array.Empty<TacticalCardInstance>() : new[] { deck.PendingAction })
                .SingleOrDefault(c => c.InstanceId == saved.CardInstance);
            SessionSnapshotCheck.Require(saved.NextId != 0 && saved.LastId == saved.NextId && saved.CardVersion != 0 &&
                card != null && card.Version >= saved.CardVersion && saved.CompletedFrame >= 0 && saved.CompletedFrame <= frame &&
                saved.Mode >= 0 && saved.Mode <= 3 && saved.Terminal && saved.Released && saved.Acknowledged &&
                saved.StartedTick >= -1 && saved.StartedTick <= saved.CompletedFrame && saved.RewardedHits != null &&
                saved.RewardedHits.Length <= _rewardedHits.Length && saved.RewardedHits.All(id => id > 0) &&
                saved.RewardedHits.Distinct().Count() == saved.RewardedHits.Length &&
                SessionSnapshotCheck.EnumValue((CharacterTestRuntimeAiLabLogicRuntimeState)saved.SessionState) &&
                SessionSnapshotCheck.EnumValue((CharacterTestBehaviorExecutionEndReason)saved.EndReason) &&
                SessionSnapshotCheck.EnumValue((CharacterActionInstanceState)saved.ActionState), "idle-execution-history-join");
            _nextId = saved.NextId; _executionId = saved.LastId; _admitted = card; _admittedVersion = saved.CardVersion;
            _retiredFrame = saved.CompletedFrame; _retiredMode = saved.Mode; _retiredSessionState = saved.SessionState;
            _retiredEndReason = saved.EndReason; _retiredActionState = saved.ActionState; _retiredMotionResult = saved.MotionResult;
            _retiredObservation = new ExecutionObservation(saved.LastId, saved.ActionStarted, saved.TimelineCompleted,
                saved.Terminal, saved.Released, saved.Acknowledged, saved.Started, saved.ObservationIssue, saved.StartedTick);
            _rewardedHitCount = saved.RewardedHits.Length; Array.Copy(saved.RewardedHits, _rewardedHits, _rewardedHitCount);
            _retiredAborted = _aborted = saved.Aborted; _retiredIssue = LastIssue = saved.Issue;
            _retiredWarning = LastWarning = saved.Warning; _retired = _retiredReceiptSealed = true;
            ValidateIdleExecutionBoundary();
        }
    }
}
