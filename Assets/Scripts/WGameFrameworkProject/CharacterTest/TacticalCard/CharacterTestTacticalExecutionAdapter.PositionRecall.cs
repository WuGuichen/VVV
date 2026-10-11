using MxFramework.Runtime;

namespace MxFramework.Game.CharacterTest.TacticalCard
{
    internal sealed partial class CharacterTestTacticalExecutionAdapter
    {
        private bool _recallExecution, _recallSubmitted, _recallCancelled;
        private ulong _recallStage;
        private TacticalExecutionInputBinding _preparedRecallBinding, _recallBinding;
        internal void ConfigureRecallStage(ulong stage)
        {
            _recallStage = stage;
            if (_entity.PositionRecall.StageRun != stage || _entity.PositionRecall.Actor != _entity.GameplayEntityId)
                _entity.SetPositionRecall(CharacterTestPositionRecallState.Bind(stage, _entity.GameplayEntityId));
        }
        internal void ClearRecallStage()
        {
            if (_entity.PositionRecall.StageRun == _recallStage) _entity.SetPositionRecall(default);
            _recallStage = 0;
        }
        private bool RecallAvailable(TacticalPositionRecallRules rules)
        {
            var state = _entity.PositionRecall;
            return _recallStage != 0 && state.StageRun == _recallStage && state.Actor == _entity.GameplayEntityId
                && !state.Pending && _entity.CombatState.IsAlive && _entity.IsGrounded && !_entity.GroundPointTraction.IsActive
                && _entity.PendingMotionRequestCount == 0 && _entity.PendingCombatRequestCount == 0 && _entity.BattleState.CanStartNormalAction
                && _entity.BattleState.DominantReason == CharacterTestBattleStateDominantReason.None
                && _entity.Action.State != CharacterTestActionStateId.Running
                && (rules.Operation == CharacterTestPositionRecallOperation.Record || state.RecordValid(_frame.Value));
        }
        private bool PrepareRecall(TacticalCardInstance card, out string issue)
        {
            _prepared = null;
            var definition = (TacticalActionCardDefinition)card.Definition;
            if (_session.IsExecuting || _admitted?.Zone == CardZone.Resolving || card.Zone != CardZone.PendingAction
                || !_hasContext || _frame.Value < 0 || !ReferenceEquals(_runtime.FindEntityById(_entity.Id.Value), _entity)
                || _commands.PendingCommandCount != 0 || _pointBinding == null || !RecallAvailable(definition.PositionRecall))
                return Reject("execution:RecallActorBusyOrRecordUnavailable", out issue);
            if (definition.HasPublishedBehavior)
            {
                if (!TryResolvePublished(definition, out var published, out issue)) return false;
                if (!definition.PositionRecall.SameContract(published.TacticalSource.PositionRecall) || published.Metadata.Cost != definition.BaseMomentumCost)
                    return Reject("execution:PublishedRecallContractMismatch", out issue);
            }
            if (_deployment != null)
            {
                bool owned = false;
                for (int i = 0; i < _deployment.Cards.Count; i++) if (ReferenceEquals(_deployment.Cards[i], definition)) { owned = true; break; }
                if (!owned) return Reject("execution:CardNotInConfirmedDeployment", out issue);
            }
            _preparedRecallBinding = _pointBinding(card);
            if (!_preparedRecallBinding.IsValid || _preparedRecallBinding.StageRun != _recallStage
                || _preparedRecallBinding.Actor != _entity.GameplayEntityId) return Reject("execution:RecallStageUnavailable", out issue);
            _prepared = card; _preparedVersion = card.Version; _preparedEquipmentSnapshot = _entity.Equipment.ToSnapshot();
            issue = string.Empty; return true;
        }
        private bool AdmitRecall(TacticalCardInstance card, out ulong id, out string issue)
        {
            id = 0;
            if (!ReferenceEquals(card, _prepared) || card == null || card.Version != _preparedVersion
                || !_preparedRecallBinding.Equals(_pointBinding(card)) || !_preparedEquipmentSnapshot.Equals(_entity.Equipment.ToSnapshot())
                || !RecallAvailable(((TacticalActionCardDefinition)card.Definition).PositionRecall) || _commands.PendingCommandCount != 0)
                return Reject("execution:StaleRecallPreparation", out issue);
            if (_nextId >= long.MaxValue) return Reject("execution:IdentityExhausted", out issue);
            BeginExecutionIdentity(card); id = _executionId; _admitted = card; _recallBinding = _preparedRecallBinding;
            _recallExecution = true; _recallSubmitted = _recallCancelled = false;
            _tractionExecution = _pointExecution = _pointExecutionStarted = false; _pointStartedTick = -1;
            _actionStarted = _timelineCompleted = _faulted = _aborted = false;
            _lastTick = _frame.Value; _prepared = null; _actionInstance = null; _rewardedHitCount = 0;
            issue = LastIssue = string.Empty; return true;
        }
        private RuntimeCommand RecallCommand(RuntimeFrame frame, bool cancel)
        {
            var rules = ((TacticalActionCardDefinition)_admitted.Definition).PositionRecall;
            return CharacterTestRuntimeCommandFactory.PositionRecall(frame, _entity.Id.Value, _recallBinding.Actor,
                _recallBinding.StageRun, _executionId, rules.Operation, cancel ? 0 : rules.LifetimeFrames, cancel);
        }
        private bool TickRecall()
        {
            _lastTick = _frame.Value;
            if (_recallSubmitted || _recallCancelled) return true;
            if (!ReferenceEquals(_runtime.FindEntityById(_entity.Id.Value), _entity) || _entity.GameplayEntityId != _recallBinding.Actor)
            { _recallCancelled = true; LastIssue = "execution:RecallActorUnavailable"; return true; }
            var accepted = _commands.Enqueue(RecallCommand(_frame, false));
            if (!accepted.Success) { _recallCancelled = true; LastIssue = "execution:RecallCommandRejected"; return true; }
            _recallSubmitted = true; return true;
        }
        private ExecutionObservation ObserveRecall(ulong id)
        {
            bool current = ReferenceEquals(_runtime.FindEntityById(_entity.Id.Value), _entity) && _entity.GameplayEntityId == _recallBinding.Actor;
            var state = current ? _entity.PositionRecall : default;
            bool matching = current && state.Matches(_recallBinding.StageRun, id);
            if (matching && state.StartedTick >= 0) { _pointExecutionStarted = true; _pointStartedTick = state.StartedTick; }
            bool ack = _commands.PendingCommandCount == 0;
            bool terminal = !current || _recallCancelled && ack || matching && !state.Pending || _recallSubmitted && ack && !matching;
            if (matching && terminal && !state.Succeeded) LastIssue = "execution:PositionRecall:" + state.Result;
            return new ExecutionObservation(id, false, false, terminal, !matching || !state.Pending, ack,
                _pointExecutionStarted, LastIssue, _pointStartedTick);
        }
        private bool CancelRecall()
        {
            if (_recallCancelled) return true;
            if (!_recallSubmitted) { _recallCancelled = true; return true; }
            var next = _frame;
            if (_runtime.TryGetLastCompletedRuntimeFrame(out var completed) && completed.Value >= next.Value) next = new RuntimeFrame(completed.Value + 1);
            if (!_commands.Enqueue(RecallCommand(next, true)).Success) return false;
            _recallCancelled = true; return true;
        }
    }
}
