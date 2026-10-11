using System;
using MxFramework.Runtime;

namespace MxFramework.Game.CharacterTest.TacticalCard
{
    internal sealed partial class CharacterTestTacticalExecutionAdapter : ITacticalExecutionInputPort, ITacticalPointSelectionPort
    {
        private CharacterTestTacticalPointNavigation _pointNavigation;
        private Func<TacticalCardInstance, TacticalExecutionInputBinding> _pointBinding;
        private TacticalExecutionInput _preparedPoint, _activePoint;
        private bool _pointExecution, _pointExecutionStarted, _pointStartChecked;
        private long _pointOwnerToken;
        private long _pointStartedTick = -1;
        private bool _pointReceiptSubscribed;
        internal void ConfigurePointInput(CharacterTestTacticalPointNavigation navigation,
            Func<TacticalCardInstance, TacticalExecutionInputBinding> binding)
        {
            _pointNavigation = navigation; _pointBinding = binding;
            if (!_pointReceiptSubscribed) { _commands.BehaviorMoveApplied += OnPointMoveApplied; _pointReceiptSubscribed = true; }
        }

        private void OnPointMoveApplied(int entityId, long ownerToken, long frame)
        {
            if (!_pointExecution || _pointExecutionStarted || entityId != _entity.Id.Value || ownerToken != _pointOwnerToken) return;
            _pointExecutionStarted = true; _pointStartedTick = frame;
        }

        internal void DisposePointInput()
        {
            ClearRecallStage();
            ReleasePointExecution();
            if (_pointReceiptSubscribed) { _commands.BehaviorMoveApplied -= OnPointMoveApplied; _pointReceiptSubscribed = false; }
        }

        public TacticalExecutionInputKind GetRequiredInputKind(TacticalCardInstance card) =>
            (card?.Definition as TacticalActionCardDefinition)?.PointMove != null ? TacticalExecutionInputKind.WorldPoint : TacticalExecutionInputKind.None;
        public bool TryGetPointOrigin(out TacticalWorldPoint origin)
        { origin = default; return _pointNavigation != null && _pointNavigation.TryGetPointOrigin(out origin); }
        public TacticalInputStatus TryPickGround(TacticalWorldPoint rayOrigin, TacticalWorldPoint rayDirection, out TacticalWorldPoint point)
        { point = default; return _pointNavigation == null ? TacticalInputStatus.GroundUnavailable : _pointNavigation.TryPickGround(rayOrigin, rayDirection, out point); }
        public TacticalInputStatus ValidateInput(TacticalCardInstance card, in TacticalExecutionInput input)
        {
            var rules = (card?.Definition as TacticalActionCardDefinition)?.PointMove;
            if (rules == null || _pointBinding == null || !TryGetPointOrigin(out var origin)) return TacticalInputStatus.CharacterUnavailable;
            if (rules.IsGroundTraction)
            {
                var common = TacticalExecutionInputValidation.ValidatePoint(input, _pointBinding(card), rules.Input,
                    origin, origin, _hasContext && _entity.CombatState.IsAlive, _pointNavigation);
                if (common != TacticalInputStatus.Valid) return common;
                return _pointNavigation.ValidateTraction(input.Binding.Actor, origin, input.Point, rules.Input.RadiusMillimeters);
            }
            return TacticalExecutionInputValidation.ValidatePoint(input, _pointBinding(card), rules.Input,
                origin, origin,
                _hasContext && _entity.CombatState.IsAlive, _pointNavigation);
        }
        public bool TryPrepare(TacticalCardInstance card, in TacticalExecutionInput input, out string issue)
        {
            if (card?.Definition.FixedAssembly != null)
                return Reject("execution:FixedAssemblyJointCommitUnavailable", out issue);
            if (_session.IsExecuting || _admitted?.Zone == CardZone.Resolving || card?.Zone != CardZone.PendingAction)
                return Reject("execution:AlreadyAdmittedOrNotPending", out issue);
            _recallExecution = false;
            _prepared = null;
            var status = ValidateInput(card, input);
            if (status != TacticalInputStatus.Valid) return Reject("execution-input:" + status, out issue);
            var definition = (TacticalActionCardDefinition)card.Definition;
            if (definition.HasPublishedBehavior)
            {
                if (!TryResolvePublished(definition, out var published, out issue)) return false;
                var rules = published.TacticalSource.PointMove;
                if (rules == null || rules.Input.RadiusMillimeters != definition.PointMove.Input.RadiusMillimeters
                    || rules.ArrivalMillimeters != definition.PointMove.ArrivalMillimeters
                    || rules.MaximumFrames != definition.PointMove.MaximumFrames || rules.SpeedMilli != definition.PointMove.SpeedMilli
                    || rules.Input.RequireReachable != definition.PointMove.Input.RequireReachable
                    || rules.StartPolicy != definition.PointMove.StartPolicy
                    || rules.TractionSpeedMillimetersPerSecond != definition.PointMove.TractionSpeedMillimetersPerSecond
                    || rules.TractionAccelerationMillimetersPerSecondSquared != definition.PointMove.TractionAccelerationMillimetersPerSecondSquared
                    || published.Metadata.Cost != definition.BaseMomentumCost)
                    return Reject("execution:PublishedPointContractMismatch", out issue);
            }
            if (_deployment != null)
            {
                bool owned = false;
                for (int i = 0; i < _deployment.Cards.Count; i++)
                    if (ReferenceEquals(_deployment.Cards[i], card.Definition)) { owned = true; break; }
                if (!owned) return Reject("execution:CardNotInConfirmedDeployment", out issue);
            }
            if (definition.PointMove.IsGroundTraction)
            {
                if (!ReferenceEquals(_runtime.FindEntityById(_entity.Id.Value), _entity) || !_entity.IsGrounded
                    || !_entity.BattleState.CanStartNormalAction || _entity.BattleState.DominantReason != CharacterTestBattleStateDominantReason.None
                    || _entity.Action.State == CharacterTestActionStateId.Running || _entity.GroundPointTraction.IsActive || _commands.PendingCommandCount != 0)
                    return Reject("execution:ActorBusyOrNotGrounded", out issue);
                _prepared = card; _preparedVersion = card.Version; _preparedPoint = input;
                _preparedEquipmentSnapshot = _entity.Equipment.ToSnapshot();
                issue = string.Empty; return true;
            }
            _tractionExecution = false;
            // PointMove has no configured physical action and therefore no cooldown dependency.
            var source = CreatePointSource(definition, input.Point);
            var inputs = CharacterTestTacticalCardInputs.Unbound(_entity.Identity.CharacterId);
            if (!CharacterTestTacticalAuthoringCompiler.TryCompile(source, inputs, out var compiled, out var diagnostics))
                return Reject(diagnostics.Count > 0 ? diagnostics[0].Message : "execution:PointCompilationRejected", out issue);
            if (!_session.TryCompilePublished(definition.Name, definition.HasPublishedBehavior ? definition.PublishedContentHash : compiled.SourceFingerprint, compiled.Program, inputs, new PointInputs(inputs)))
                return Reject(_session.LastIssue, out issue);
            _prepared = card; _preparedVersion = card.Version; _preparedPoint = input;
            _preparedEquipmentSnapshot = _entity.Equipment.ToSnapshot();
            issue = string.Empty; return true;
        }
        public bool TryAdmit(TacticalCardInstance card, in TacticalExecutionInput input, out ulong executionId, out string issue)
        {
            executionId = 0;
            if (card?.Definition.FixedAssembly != null)
                return Reject("execution:FixedAssemblyJointCommitUnavailable", out issue);
            if (!ReferenceEquals(card, _prepared) || card.Version != _preparedVersion || !input.Binding.Equals(_preparedPoint.Binding)
                || !input.Point.Equals(_preparedPoint.Point)) return Reject("execution:StalePreparation", out issue);
            if (!TryGetPointOrigin(out var origin)) return Reject("execution:ActorUnavailable", out issue);
            var status = TacticalExecutionInputValidation.ValidatePoint(input, _pointBinding(card),
                ((TacticalActionCardDefinition)card.Definition).PointMove.Input, _preparedPoint.SelectionOrigin, origin,
                _hasContext && _entity.CombatState.IsAlive, _pointNavigation);
            if (status != TacticalInputStatus.Valid) return Reject("execution-input:" + status, out issue);
            if (!_entity.Equipment.ToSnapshot().Equals(_preparedEquipmentSnapshot)) return Reject("execution:EquipmentChangedAfterPrepare", out issue);
            if (_nextId == ulong.MaxValue) return Reject("execution:IdentityExhausted", out issue);
            if (((TacticalActionCardDefinition)card.Definition).PointMove.IsGroundTraction)
                return AdmitTraction(card, input, origin, out executionId, out issue);
            _resolvedFacts.SetNoTarget(false);
            _pointNavigation.BindDestination(input.Point);
            if (!_session.TryStartPublished(_resolvedFacts, _frame))
            { _pointNavigation.ClearDestination(); return Reject(_session.LastIssue, out issue); }
            BeginExecutionIdentity(card); executionId = _executionId; _admitted = card; _activePoint = input;
            _pointExecution = true; _pointExecutionStarted = false; _pointStartedTick = -1;
            _pointStartChecked = false; _pointOwnerToken = _session.Compilation.Program.Execution.Identity ^ ((long)_entity.Id.Value << 32) ^ _frame.Value;
            _actionStarted = _timelineCompleted = _faulted = _aborted = false; _lastTick = _frame.Value; _prepared = null;
            _actionInstance = null; _rewardedHitCount = 0;
            issue = LastIssue = string.Empty; return true;
        }
        private bool TickPoint()
        {
            _lastTick = _frame.Value; _pointNavigation.BeginFrame(_frame.Value);
            if (!_pointStartChecked)
            {
                _pointStartChecked = true;
                bool actorAvailable = TryGetPointOrigin(out var origin);
                var rules = ((TacticalActionCardDefinition)_admitted.Definition).PointMove;
                var status = TacticalExecutionInputValidation.ValidatePoint(_activePoint, _activePoint.Binding, rules.Input,
                    _activePoint.SelectionOrigin, origin, actorAvailable, _pointNavigation);
                if (status != TacticalInputStatus.Valid) { LastIssue = "execution-input:" + status; _aborted = true; _session.Stop(_resolvedFacts, _frame); }
            }
            var result = _session.Tick(_resolvedFacts, _frame);
            if (_session.State == CharacterTestRuntimeAiLabLogicRuntimeState.Failed)
                LastIssue = "execution:Point:" + _session.EndReason;
            if (_session.IsExecuting && _pointNavigation.LastStatus != TacticalInputStatus.Valid)
            {
                LastIssue = "execution-input:" + _pointNavigation.LastStatus;
                _aborted = true; _session.Stop(_resolvedFacts, _frame);
            }
            return result;
        }
        private ExecutionObservation ObservePoint(ulong executionId)
        {
            bool actor = ReferenceEquals(_runtime.FindEntityById(_entity.Id.Value), _entity);
            bool terminal = !_session.IsExecuting;
            bool acknowledged = actor && _commands.PendingCommandCount == 0 && _commands.LastCommandErrors.Count == 0;
            bool released = acknowledged && (!_entity.Input.BehaviorControl.IsPresent || _entity.Input.BehaviorControl.OwnerToken != _pointOwnerToken
                || _entity.Input.BehaviorControl.IsRelease);
            return new ExecutionObservation(executionId, false, false, terminal, released, acknowledged, _pointExecutionStarted, LastIssue, _pointStartedTick);
        }
        internal void ReleasePointExecution()
        {
            if (_tractionExecution) { CancelTraction(); return; }
            if (!_pointExecution) return;
            var stopFrame = _frame;
            if (_runtime.TryGetLastCompletedRuntimeFrame(out var completed) && completed.Value >= stopFrame.Value
                && completed.Value < int.MaxValue - 1) stopFrame = new RuntimeFrame(completed.Value + 1);
            _session.Stop(_resolvedFacts, stopFrame);
            _pointNavigation.ClearDestination();
            _aborted = true;
        }
        private static CharacterTestTacticalAuthoringSource CreatePointSource(TacticalActionCardDefinition card, TacticalWorldPoint point)
        {
            var source = CharacterTestTacticalAuthoringTemplates.CreateBlank(card.Name);
            source.Schema = CharacterTestTacticalAuthoringSource.TimingSchema; source.Version = 4; source.Slots.Clear();
            var block = new CharacterTestTacticalEventBlock { BlockId = "point-move", Title = card.Name,
                Start = new CharacterTestTacticalFrameReference { Kind = "Absolute", Frame = 0 }, Completion = "Arrived", CompletionEventId = "move",
                Timing = new CharacterTestTacticalBlockTiming { MinimumFrames = 1, DurationFrames = card.PointMove.MaximumFrames } };
            var move = new CharacterTestTacticalAuthoringEvent { EventId = "move", Title = card.Name,
                Start = CharacterTestTacticalBlockEdits.Local(0), End = CharacterTestTacticalAuthoringTemplates.VisualEnd(block.BlockId),
                Operation = new CharacterTestTacticalAuthoringOperation { Kind = "Movement", Speed = card.PointMove.SpeedMilli / 1000f,
                    Movement = new CharacterTestTacticalMovement { Target = CharacterTestRuntimeAiLabMovementTargetKind.WorldPosition,
                        Strategy = CharacterTestRuntimeAiLabMovementStrategy.ApproachTarget, X = point.X / 1000f, Z = point.Z / 1000f,
                        Minimum = card.PointMove.ArrivalMillimeters / 1000f, Maximum = card.PointMove.ArrivalMillimeters / 1000f } } };
            block.EventIds.Add(move.EventId); source.Timelines[0].Events.Add(move); source.Timelines[0].EventBlocks.Add(block);
            return source;
        }
        private sealed class PointInputs : ICharacterTestTacticalCardInputSource
        {
            private readonly CharacterTestTacticalCardInputs _inputs;
            internal PointInputs(CharacterTestTacticalCardInputs inputs) => _inputs = inputs;
            public bool TryPrepare(out CharacterTestTacticalCardInputs inputs, out string issue) { inputs = _inputs; issue = ""; return true; }
            public bool TryPrepare(int actionId, out CharacterTestTacticalCardInputs inputs, out string issue)
            { inputs = _inputs; issue = "point-input:NoPhysicalAction"; return actionId == 0; }
        }
    }
}
