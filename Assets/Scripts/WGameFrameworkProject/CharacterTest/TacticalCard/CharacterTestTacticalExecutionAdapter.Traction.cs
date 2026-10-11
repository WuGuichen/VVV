using MxFramework.Runtime;

namespace MxFramework.Game.CharacterTest.TacticalCard
{
    internal sealed partial class CharacterTestTacticalExecutionAdapter
    {
        private bool _tractionExecution, _tractionSubmitted, _tractionCancelled;
        private ulong _tractionStage;
        private bool AdmitTraction(TacticalCardInstance card, in TacticalExecutionInput input, TacticalWorldPoint origin,
            out ulong executionId, out string issue)
        {
            executionId = 0;
            var rules = ((TacticalActionCardDefinition)card.Definition).PointMove;
            var status = _pointNavigation.ValidateTraction(input.Binding.Actor, origin, input.Point, rules.Input.RadiusMillimeters);
            if (status != TacticalInputStatus.Valid || input.Binding.Actor != _entity.GameplayEntityId || !_entity.IsGrounded
                || !_entity.BattleState.CanStartNormalAction || _entity.GroundPointTraction.IsActive || _commands.PendingCommandCount != 0)
                return Reject("execution:TractionAdmission:" + status, out issue);
            if (_nextId >= long.MaxValue) return Reject("execution:IdentityExhausted", out issue);
            BeginExecutionIdentity(card); executionId = _executionId;
            _admitted = card; _activePoint = input; _tractionStage = input.Binding.StageRun;
            _tractionExecution = true; _tractionSubmitted = _tractionCancelled = false;
            _pointExecution = false; _actionStarted = _timelineCompleted = _faulted = _aborted = false;
            _pointExecutionStarted = false; _pointStartedTick = -1; _lastTick = _frame.Value;
            _prepared = null; _actionInstance = null; _rewardedHitCount = 0;
            issue = LastIssue = string.Empty; return true;
        }
        private RuntimeCommand TractionCommand(RuntimeFrame frame, bool cancel)
        {
            var r = ((TacticalActionCardDefinition)_admitted.Definition).PointMove;
            return CharacterTestRuntimeCommandFactory.GroundPointTraction(frame, _entity.Id.Value, _activePoint.Binding.Actor,
                _tractionStage, _executionId, _activePoint.Point, r.Input.RadiusMillimeters, r.ArrivalMillimeters,
                r.MaximumFrames, r.TractionSpeedMillimetersPerSecond, r.TractionAccelerationMillimetersPerSecondSquared, cancel);
        }
        private bool TickTraction()
        {
            _lastTick = _frame.Value;
            if (_tractionCancelled) return true;
            if (_tractionSubmitted)
            {
                if (!ReferenceEquals(_runtime.FindEntityById(_entity.Id.Value), _entity) || !_entity.CombatState.IsAlive
                    || _entity.GameplayEntityId != _activePoint.Binding.Actor) CancelTraction();
                return true;
            }
            if (!ReferenceEquals(_runtime.FindEntityById(_entity.Id.Value), _entity) || _entity.GameplayEntityId != _activePoint.Binding.Actor
                || !_entity.CombatState.IsAlive)
            { _tractionCancelled = true; LastIssue = "execution:TractionActorUnavailable"; return true; }
            // The formal consumer repeats range, ground, clearance, actor and generation admission.
            var accepted = _commands.Enqueue(TractionCommand(_frame, false));
            if (!accepted.Success) { _tractionCancelled = true; LastIssue = "execution:TractionCommandRejected"; return true; }
            _tractionSubmitted = true; return true;
        }
        private ExecutionObservation ObserveTraction(ulong executionId)
        {
            bool actor = ReferenceEquals(_runtime.FindEntityById(_entity.Id.Value), _entity)
                && _entity.GameplayEntityId == _activePoint.Binding.Actor;
            var state = actor ? _entity.GroundPointTraction : default;
            bool matching = actor && state.Matches(_tractionStage, executionId);
            if (matching && state.StartedTick >= 0)
            { _pointExecutionStarted = true; _pointStartedTick = state.StartedTick; }
            bool ack = _commands.PendingCommandCount == 0;
            bool terminal = !actor || _tractionCancelled && ack || matching && !state.IsActive
                || _tractionSubmitted && ack && !matching;
            if (matching && terminal && state.End != CharacterTestGroundPointTractionEnd.Arrived)
                LastIssue = "execution:Traction:" + state.End;
            bool released = !matching || !state.IsActive;
            return new ExecutionObservation(executionId, false, false, terminal, released, ack,
                _pointExecutionStarted, LastIssue, _pointStartedTick);
        }
        private bool CancelTraction()
        {
            if (!_tractionExecution) return false;
            if (_tractionCancelled) return true;
            if (!_tractionSubmitted) { _tractionCancelled = true; return true; }
            var next = _frame;
            if (_runtime.TryGetLastCompletedRuntimeFrame(out var completed) && completed.Value >= next.Value)
                next = new RuntimeFrame(completed.Value + 1);
            var result = _commands.Enqueue(TractionCommand(next, true));
            if (!result.Success) return false;
            _tractionCancelled = true; return true;
        }
    }
}
