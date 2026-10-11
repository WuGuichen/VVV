using System;
using MxFramework.CharacterAction;
using MxFramework.CharacterControl;
using MxFramework.Runtime;
using UnityEngine;

namespace MxFramework.Game.CharacterTest
{
    public sealed class CharacterTestBehaviorExecutionRuntimeState
    {
        public const int CurrentVersion = 2;
        public int Version { get; set; } = CurrentVersion;
        public int EntityId { get; set; }
        public long ProgramIdentity { get; set; }
        public int ActionCommandState { get; set; }
        public int Operation { get; set; }
        public int ResourceId { get; set; }
        public int ExpectedActionId { get; set; }
        public bool CommandPending { get; set; }
        public bool CommandSubmitted { get; set; }
        public bool RejectAfterCleanup { get; set; }
        public long PreviousInstanceId { get; set; }
        public long OwnedInstanceId { get; set; }
        public long ReceiptInstanceId { get; set; }
        public long SubmittedFrame { get; set; } = -1;
        public int FailureReasonId { get; set; }
        public int FailureDetailId { get; set; }
        public CharacterTestActionUseFailure FailureDetails { get; set; }
        public long ContinuationFrame { get; set; } = -1;
        public bool FailureContinuation { get; set; }
        public bool ControlPending { get; set; }
        public bool ControlRejected { get; set; }
        public short MoveXMilli { get; set; }
        public short MoveZMilli { get; set; }
        public float MovementSpeed { get; set; } = 1f;
        public CharacterTestBehaviorControlInput Control { get; set; }
        public CharacterTestBehaviorExecutionRuntimeState Clone() => (CharacterTestBehaviorExecutionRuntimeState)MemberwiseClone();
        public void AddHash(RuntimeHashAccumulator hash, int key = 1)
        {
            hash.BeginObject(key, BehaviorExecutionRuntimeHashFields.SchemaId, BehaviorExecutionRuntimeHashFields.SchemaVersion);
            hash.AddInt(BehaviorExecutionRuntimeHashFields.Version, Version);
            hash.AddInt(BehaviorExecutionRuntimeHashFields.ExecutionRuntimeEntity, EntityId); hash.AddLong(BehaviorExecutionRuntimeHashFields.ExecutionRuntimeIdentity, ProgramIdentity);
            hash.AddInt(BehaviorExecutionRuntimeHashFields.ExecutionRuntimeCommandState, ActionCommandState); hash.AddInt(BehaviorExecutionRuntimeHashFields.Operation, Operation);
            hash.AddInt(BehaviorExecutionRuntimeHashFields.Resource, ResourceId); hash.AddInt(BehaviorExecutionRuntimeHashFields.Expected, ExpectedActionId);
            hash.AddInt(BehaviorExecutionRuntimeHashFields.Pending, CommandPending ? 1 : 0); hash.AddInt(BehaviorExecutionRuntimeHashFields.Submitted, CommandSubmitted ? 1 : 0);
            hash.AddInt(BehaviorExecutionRuntimeHashFields.RejectCleanup, RejectAfterCleanup ? 1 : 0); hash.AddLong(BehaviorExecutionRuntimeHashFields.Previous, PreviousInstanceId);
            hash.AddLong(BehaviorExecutionRuntimeHashFields.Owned, OwnedInstanceId); hash.AddLong(BehaviorExecutionRuntimeHashFields.SubmittedFrame, SubmittedFrame);
            hash.AddLong(BehaviorExecutionRuntimeHashFields.ReceiptInstance, ReceiptInstanceId);
            hash.AddLong(BehaviorExecutionRuntimeHashFields.ContinuationFrame, ContinuationFrame); hash.AddInt(BehaviorExecutionRuntimeHashFields.FailureContinuation, FailureContinuation ? 1 : 0);
            hash.AddInt(BehaviorExecutionRuntimeHashFields.CommitReason, FailureReasonId);
            hash.AddInt(BehaviorExecutionRuntimeHashFields.CommitDetailId, FailureDetailId);
            FailureDetails.ContributeHash(hash, BehaviorExecutionRuntimeHashFields.CommitDetails);
            hash.AddInt(BehaviorExecutionRuntimeHashFields.ControlPending, ControlPending ? 1 : 0); hash.AddInt(BehaviorExecutionRuntimeHashFields.MoveX, MoveXMilli); hash.AddInt(BehaviorExecutionRuntimeHashFields.MoveZ, MoveZMilli);
            hash.AddInt(BehaviorExecutionRuntimeHashFields.ControlRejected, ControlRejected ? 1 : 0);
            hash.AddInt(BehaviorExecutionRuntimeHashFields.MoveSpeed, BitConverter.SingleToInt32Bits(MovementSpeed)); Control.AddHash(ref hash, BehaviorExecutionRuntimeHashFields.Control);
            hash.EndObject();
        }
    }

    internal sealed partial class CharacterTestRuntimeAiLabBehaviorCardRuntimeOverlay
    {
        internal sealed partial class RuntimeBridge : ICharacterTestBehaviorExecutionRuntime, ICharacterTestBehaviorReceiptObserver,
            ICharacterTestBehaviorActionContext, ICharacterTestBehaviorActionCommitGuard, ICharacterTestBehaviorContinuationObserver,
            ICharacterTestBehaviorExecutionContinuationRuntime, ICharacterTestBehaviorExecutionActionClock
        {
            private readonly CharacterTestCharacterRuntimeModule _executionCharacterRuntime;
            private readonly CharacterTestRuntimeAiPlannerAttackOpportunityAdapter _executionAttackOpportunity;
            private bool _executionControlPending;
            private Vector2 _executionMove;
            private float _executionMoveSpeed = 1f;
            private CharacterTestBehaviorControlInput _executionControl;
            private long _executionOwnedInstance;
            private long _executionSubmittedFrame = -1;
            private long _executionReceiptInstance;
            internal CharacterActionInstance LastAcknowledgedActionInstance { get; private set; }
            private CharacterTestBehaviorExecutionEvaluator _executionEvaluator;
            private long _executionContinuationFrame = -1;
            private bool _executionFailureContinuation;
            public int ActionCommitFailureReasonId => _orchestrationFailureReasonId;
            public int ActionCommitFailureDetailId => _orchestrationFailureDetailId;
            public CharacterTestActionUseFailure ActionCommitFailureDetails => _orchestrationFailureDetails;
            public void AttachExecutionEvaluator(CharacterTestBehaviorExecutionEvaluator evaluator) => _executionEvaluator = evaluator;
            public bool CanCommitAction(int actionId) => _executionEvaluator?.CanCommitPendingAction(actionId) ?? true;
            private string[] _executionActionNames;
            private CharacterTestRuntimeAiLabLogicActionState ExecutionPrimaryActionState
            {
                get
                {
                    long owned = _orchestrationState == CharacterTestRuntimeAiLabBehaviorRecipeOrchestrationState.Succeeded
                        && _orchestrationOperation == CharacterTestBehaviorRecipeOrchestrationOperation.ReplaceCharacterAction
                        ? _executionReceiptInstance : _executionOwnedInstance;
                    return owned <= 0 ? CharacterTestRuntimeAiLabLogicActionState.None
                        : ActualActionInstanceId == owned ? CharacterTestRuntimeAiLabLogicActionState.Running : CharacterTestRuntimeAiLabLogicActionState.Completed;
                }
            }
            private void InitializeExecutionObservation()
            {
                if (_program.Execution == null) return;
                _executionActionNames = new string[_program.Execution.Actions.Length];
                for (int i = 0; i < _executionActionNames.Length; i++) _executionActionNames[i] = "character-action:" + _program.Execution.Actions[i].ActionId;
            }
            private string ExecutionActionStableId()
            {
                if (_actionRuntime == null || !_actionRuntime.TryGetActiveActionPresentationState(new CharacterTestCharacterEntityId(_entityId), out _, out var action, out _, out _)) return string.Empty;
                for (int i = 0; _executionActionNames != null && i < _executionActionNames.Length; i++)
                    if (_program.Execution.Actions[i].ActionId == action.Value) return _executionActionNames[i];
                return string.Empty;
            }
            public int EntityId => _entityId;
            public int ActualActionFrame => _entity?.Action.LocalFrame ?? 0;
            public long StartedActionInstanceId => _executionReceiptInstance > 0 ? _executionReceiptInstance : ActualActionInstanceId;
            public CharacterTestRuntimeAiLabBehaviorRecipeOrchestrationState ActionCommandState => _orchestrationState;
            public long ActualActionInstanceId => _actionRuntime != null && _actionRuntime.TryGetActiveActionPresentationState(
                new CharacterTestCharacterEntityId(_entityId), out long id, out _, out _, out _) ? id : 0L;
            public CharacterTestRuntimeAiLabLogicActionState ActualActionState => _commandRejected ? CharacterTestRuntimeAiLabLogicActionState.Failed : ActualActionInstanceId > 0
                ? CharacterTestRuntimeAiLabLogicActionState.Running : CharacterTestRuntimeAiLabLogicActionState.Completed;
            public void ObserveOwnedAction(long instanceId) { _executionOwnedInstance = instanceId; _observedActionInstanceId = instanceId; }
            public void CompleteActionCommand() { CompleteOrchestration(); _executionSubmittedFrame = -1; _executionReceiptInstance = 0; }
            private void ResetExecutionRuntime()
            {
                LastAcknowledgedActionInstance = null;
                _executionControlPending = false; _executionMove = Vector2.zero; _executionMoveSpeed = 1f;
                _executionControl = default; _executionOwnedInstance = 0; _executionSubmittedFrame = -1;
                _executionReceiptInstance = 0;
                _executionContinuationFrame = -1; _executionFailureContinuation = false;
            }
            private void RefreshExecutionActionState()
            {
                RefreshOrchestrationState();
                _actionState = _executionOwnedInstance <= 0 ? CharacterTestRuntimeAiLabLogicActionState.None
                    : ActualActionInstanceId == _executionOwnedInstance ? CharacterTestRuntimeAiLabLogicActionState.Running
                    : CharacterTestRuntimeAiLabLogicActionState.Completed;
            }
            public bool IsSubjectAvailable(CharacterTestRuntimeAiLabBehaviorRecipeConditionSubjectKind subject)
            {
                switch (subject)
                {
                    case CharacterTestRuntimeAiLabBehaviorRecipeConditionSubjectKind.CurrentCombatTarget: return CurrentExecutionTarget() != null;
                    case CharacterTestRuntimeAiLabBehaviorRecipeConditionSubjectKind.Self: return _entity != null;
                    case CharacterTestRuntimeAiLabBehaviorRecipeConditionSubjectKind.Environment: return true;
                    default: return false;
                }
            }
            public bool Evaluate(in CharacterTestBehaviorExecutionPredicate predicate)
            {
                if (predicate.Kind == CharacterTestBehaviorExecutionPredicateKind.Fact)
                {
                    if (predicate.Fact == CharacterTestRuntimeAiLabCardConditionFact.TargetFacingAngle)
                    {
                        var facingTarget = CurrentExecutionTarget();
                        if (_entity == null || facingTarget == null || !facingTarget.CombatState.IsAlive) return false;
                        Vector2 toward = DirectionTo(facingTarget, out float separation);
                        if (toward.sqrMagnitude <= 0.000001f || float.IsNaN(separation) || float.IsInfinity(separation)) return false;
                        int yaw = CharacterKinematicMotionMath.Atan2MilliDegrees((int)Math.Round(toward.x * 1000000d), (int)Math.Round(toward.y * 1000000d));
                        float angle = Math.Abs(CharacterTestDeterministicMath.ShortestAngleDelta(_entity.Transform.YawMilliDegrees, yaw)) / 1000f;
                        return predicate.Operator switch {
                            CharacterTestRuntimeAiLabCardConditionOperator.LessOrEqual => angle <= predicate.Value,
                            CharacterTestRuntimeAiLabCardConditionOperator.GreaterOrEqual => angle >= predicate.Value,
                            CharacterTestRuntimeAiLabCardConditionOperator.Between => angle >= predicate.Value && angle <= predicate.Maximum,
                            CharacterTestRuntimeAiLabCardConditionOperator.GreaterThan => angle > predicate.Value,
                            CharacterTestRuntimeAiLabCardConditionOperator.NotEquals => angle != predicate.Value,
                            _ => angle == predicate.Value };
                    }
                    if (predicate.Fact == CharacterTestRuntimeAiLabCardConditionFact.TargetDistance
                        && (CurrentExecutionTarget() == null || _facts == null || !_facts.TargetDistanceAvailable)) return false;
                    if (predicate.Fact == CharacterTestRuntimeAiLabCardConditionFact.TargetRootDistance && CurrentExecutionTarget() == null) return false;
                    return Evaluate(predicate.AsFactRequest());
                }
                CharacterTestCharacterEntity target = CurrentExecutionTarget();
                if (_entity == null || target == null) return false;
                if (predicate.Kind == CharacterTestBehaviorExecutionPredicateKind.BoundActionOpportunity)
                {
                    if (_executionAttackOpportunity == null || !_executionAttackOpportunity.TryPreviewPrimaryAttack(
                        _entity, target, new CharacterActionId(predicate.ActionId), _frame, out CharacterTestAttackOpportunityResult opportunity)) return false;
                    return opportunity.Status == CharacterTestAttackOpportunityStatus.Valid
                        && opportunity.Level == CharacterTestAttackOpportunityLevel.CurrentPose && opportunity.CanHitFromCurrentPose;
                }
                Vector2 direction = DirectionTo(target, out float distance);
                if (float.IsNaN(distance) || float.IsInfinity(distance)) return false;
                if (predicate.Kind == CharacterTestBehaviorExecutionPredicateKind.RootDistanceRange)
                    return distance >= predicate.Value && distance <= predicate.Maximum;
                if (predicate.Kind == CharacterTestBehaviorExecutionPredicateKind.TargetFacing)
                {
                    if (direction.sqrMagnitude <= 0.000001f) return false;
                    int x = (int)Math.Round(direction.x * 1000000d), z = (int)Math.Round(direction.y * 1000000d);
                    int yaw = CharacterKinematicMotionMath.Atan2MilliDegrees(x, z);
                    int difference = CharacterTestDeterministicMath.ShortestAngleDelta(
                        _entity.Transform.YawMilliDegrees + (int)Math.Round(predicate.Value * 1000d), yaw);
                    return Math.Abs(difference) <= (int)Math.Round(predicate.Maximum * 1000d);
                }
                return false;
            }
            private CharacterTestCharacterEntity CurrentExecutionTarget() => _facts != null && _facts.TargetExists && _facts.TargetEntityId > 0
                ? (_executionCharacterRuntime != null ? _executionCharacterRuntime.FindEntityById(_facts.TargetEntityId) : FindEntity(_facts.TargetEntityId)) : null;
            private bool TryExecutionTarget(CharacterTestRuntimeAiLabMovementTargetKind kind, int entityId, float x, float z,
                out CharacterTestCharacterEntity target, out Vector2 toward, out float distance)
            {
                target = null; toward = Vector2.zero; distance = 0;
                if (_entity == null) return false;
                if (kind == CharacterTestRuntimeAiLabMovementTargetKind.WorldPosition)
                {
                    Vector2 delta = new Vector2(x - _entity.Transform.X, z - _entity.Transform.Z);
                    distance = delta.magnitude; toward = distance > 0.0001f ? delta / distance : Vector2.zero;
                    return true;
                }
                target = kind == CharacterTestRuntimeAiLabMovementTargetKind.CurrentCombatTarget ? CurrentExecutionTarget()
                    : entityId > 0 ? (_executionCharacterRuntime != null ? _executionCharacterRuntime.FindEntityById(entityId) : FindEntity(entityId)) : FindAutomaticAlly();
                if (target == null) return false;
                toward = DirectionTo(target, out distance);
                return !float.IsNaN(distance) && !float.IsInfinity(distance);
            }
            public bool TrySampleMovement(in CharacterTestBehaviorExecutionMovement movement, out CharacterTestRuntimeAiLabMovementTargetSample sample)
            {
                sample = default;
                if (!TryExecutionTarget(movement.Target, movement.EntityId, movement.X, movement.Z, out var target, out var toward, out float distance)) return false;
                if (movement.RequireLivingTarget && target != null && !target.CombatState.IsAlive) return false;
                float rootDistance = distance;
                bool needsDistance = movement.ExplicitDistance && movement.Strategy != CharacterTestRuntimeAiLabMovementStrategy.AvoidHazard
                    && movement.Strategy != CharacterTestRuntimeAiLabMovementStrategy.StrafeWhenBlocked
                    || movement.IsOptimalRange || movement.Strategy == CharacterTestRuntimeAiLabMovementStrategy.MaintainDistance
                    || movement.Strategy == CharacterTestRuntimeAiLabMovementStrategy.RetreatWhenClose
                    || (movement.Strategy == CharacterTestRuntimeAiLabMovementStrategy.ApproachTarget && movement.Minimum > 0)
                    || (movement.Strategy == CharacterTestRuntimeAiLabMovementStrategy.OrbitTarget && (movement.Minimum > 0 || movement.Maximum > 0));
                if (movement.Metric == CharacterTestBehaviorDistanceMetric.HurtboxSurfaceClearance && needsDistance)
                {
                    if (_facts == null || !_facts.TargetDistanceAvailable) return false;
                    distance = _facts.TargetDistance;
                }
                Vector2 navigation = toward;
                if (movement.Target == CharacterTestRuntimeAiLabMovementTargetKind.WorldPosition
                    && _navigationService is MxFramework.Game.CharacterTest.TacticalCard.ICharacterTestWorldPointNavigationService points)
                {
                    if (!points.TryResolvePointMove(_entity, movement.X, movement.Z, out navigation)) return false;
                }
                else if (movement.Target == CharacterTestRuntimeAiLabMovementTargetKind.CurrentCombatTarget && _facts != null)
                    navigation = _facts.MoveDirection;
                else if (target != null && _navigationService != null && _navigationService.TryResolveMove(
                    _entity, target, movement.IsOptimalRange ? movement.Optimal : movement.Minimum, out CharacterTestAiNavigationResult path)) navigation = path.Move;
                sample = new CharacterTestRuntimeAiLabMovementTargetSample(true, distance, toward, navigation,
                    target != null, target?.Transform.YawMilliDegrees ?? 0, rootDistance, target?.Id.Value ?? 0);
                return true;
            }
            public bool TrySampleTurn(in CharacterTestBehaviorExecutionTurn turn, out int targetXMillimeters, out int targetZMillimeters)
            {
                targetXMillimeters = 0; targetZMillimeters = 0;
                if (!TryExecutionTarget(turn.Target, turn.EntityId, turn.X, turn.Z, out _, out var toward, out float distance)
                    || toward.sqrMagnitude <= 0.000001f) return false;
                targetXMillimeters = _entity.Transform.XMillimeters + CharacterTestDeterministicUnits.MetersToMillimeters(toward.x * distance);
                targetZMillimeters = _entity.Transform.ZMillimeters + CharacterTestDeterministicUnits.MetersToMillimeters(toward.y * distance);
                return true;
            }
            public bool TryStartAction(int actionId)
            {
                if (_entity == null || _actionRuntime == null || _orchestrationState != CharacterTestRuntimeAiLabBehaviorRecipeOrchestrationState.None
                    || !_actionRuntime.Registry.TryGetAction(new CharacterActionId(actionId), out var action)) return false;
                BeginOrchestration(CharacterTestBehaviorRecipeOrchestrationOperation.ReplaceCharacterAction, actionId, null, action.ActionId, false);
                _executionReceiptInstance = 0;
                return true;
            }
            public void OnBehaviorOrchestrationReceipt(in CharacterTestBehaviorOrchestrationReceipt receipt)
            {
                if (_orchestrationState != CharacterTestRuntimeAiLabBehaviorRecipeOrchestrationState.Pending || !_orchestrationCommandSubmitted
                    || receipt.EntityId != _entityId || receipt.Frame.Value != _executionSubmittedFrame
                    || receipt.Operation != _orchestrationOperation || receipt.ResourceId != _orchestrationResourceId) return;
                _executionReceiptInstance = receipt.ActionInstanceId;
                if (receipt.Success && receipt.Operation == CharacterTestBehaviorRecipeOrchestrationOperation.ReplaceCharacterAction)
                {
                    var instance = _actionRuntime?.GetActionInstanceForObservation(new CharacterTestCharacterEntityId(_entityId));
                    if (instance != null && instance.InstanceId == receipt.ActionInstanceId)
                        LastAcknowledgedActionInstance = instance;
                }
                _orchestrationState = receipt.Success ? CharacterTestRuntimeAiLabBehaviorRecipeOrchestrationState.Succeeded
                    : CharacterTestRuntimeAiLabBehaviorRecipeOrchestrationState.Rejected;
                if (!receipt.Success)
                {
                    _orchestrationFailureText = receipt.Reason;
                    _orchestrationFailureReasonId = (int)CharacterTestBehaviorFailureCode.ActionReceiptDomain + (int)receipt.ReasonCode;
                    _orchestrationFailureDetailId = 0;
                    _orchestrationFailureDetails = receipt.FailureDetails;
                }
                if (!receipt.Success && receipt.Operation == CharacterTestBehaviorRecipeOrchestrationOperation.ReplaceCharacterAction)
                    _executionFailureContinuation = true;
                if (_executionFailureContinuation) _executionContinuationFrame = receipt.Frame.Value;
            }
            public bool ContinueAfterCommand(RuntimeFrame frame, Func<RuntimeCommand, RuntimeCommandValidationResult> apply)
            {
                if (_executionContinuationFrame != frame.Value || _executionEvaluator == null) return false;
                _executionContinuationFrame = -1;
                _executionEvaluator.ContinueAfterActionCommit(checked((int)frame.Value));
                if (_executionEvaluator.HasReadyCommandReceipt) _executionContinuationFrame = frame.Value;
                if (TryBuildExecutionIntent(out var intent))
                {
                    var result = CharacterTestBehaviorExecutionCommandWriter.Submit(frame, _entityId, intent, apply);
                    RecordCommandResult(result.Success);
                }
                if (_executionContinuationFrame < 0 && _orchestrationState != CharacterTestRuntimeAiLabBehaviorRecipeOrchestrationState.Pending)
                    _executionFailureContinuation = false;
                return true;
            }
            public bool TryGetActionFacts(RuntimeFrame frame, int entity, int resource, out CharacterTestRuntimeAiPlannerFactState facts)
            {
                facts = _facts;
                return _orchestrationState == CharacterTestRuntimeAiLabBehaviorRecipeOrchestrationState.Pending && _orchestrationCommandSubmitted
                    && entity == _entityId && frame.Value == _executionSubmittedFrame && resource == _orchestrationResourceId;
            }
            public bool TryStopAction(long ownedInstanceId)
            {
                if (_orchestrationState != CharacterTestRuntimeAiLabBehaviorRecipeOrchestrationState.None) return false;
                if (ownedInstanceId <= 0 || ActualActionInstanceId != ownedInstanceId)
                { _orchestrationState = CharacterTestRuntimeAiLabBehaviorRecipeOrchestrationState.Succeeded; return true; }
                BeginOrchestration(CharacterTestBehaviorRecipeOrchestrationOperation.TerminateForReturnToPlanner, 0, null, CharacterActionId.None, false);
                return true;
            }
            public void SetExecutionControl(Vector2 move, float movementSpeed, in CharacterTestBehaviorControlInput control)
            {
                _executionMove = move; _executionMoveSpeed = movementSpeed; _executionControl = control; _executionControlPending = true;
            }
            private bool TryBuildExecutionIntent(out CharacterTestRuntimeAiLabRuntimeIntent intent)
            {
                bool action = TryBuildOrchestrationIntent(out CharacterTestRuntimeAiLabRuntimeIntent actionIntent);
                bool control = _executionControlPending; _executionControlPending = false;
                if (!action && !control) { intent = default; return false; }
                if (action) _executionSubmittedFrame = _frame.Value;
                intent = new CharacterTestRuntimeAiLabRuntimeIntent(action ? actionIntent.Kind : CharacterTestRuntimeAiLabRuntimeIntentKind.Control,
                    control ? _executionMove : Vector2.zero, orchestrationOperation: actionIntent.OrchestrationOperation,
                    orchestrationResourceId: actionIntent.OrchestrationResourceId, movementSpeedMultiplier: _executionMoveSpeed,
                    hasExecutionControl: control, executionControl: _executionControl,
                    orchestrationExpectedInstanceId: action && actionIntent.OrchestrationOperation == CharacterTestBehaviorRecipeOrchestrationOperation.TerminateForReturnToPlanner
                        ? _orchestrationPreviousInstanceId : 0L);
                LastIntentKind = intent.Kind; return true;
            }
            private bool InterruptExecutionAction()
            {
                RefreshOrchestrationState();
                if (_orchestrationState == CharacterTestRuntimeAiLabBehaviorRecipeOrchestrationState.Succeeded)
                {
                    if (_orchestrationOperation == CharacterTestBehaviorRecipeOrchestrationOperation.ReplaceCharacterAction)
                        ObserveOwnedAction(ActualActionInstanceId);
                    CompleteActionCommand();
                }
                SetExecutionControl(Vector2.zero, 1f, new CharacterTestBehaviorControlInput(1, 0, 0, ownerToken: _executionControl.OwnerToken, isRelease: true));
                return _orchestrationState != CharacterTestRuntimeAiLabBehaviorRecipeOrchestrationState.None || TryStopAction(_executionOwnedInstance);
            }
            public CharacterTestBehaviorExecutionRuntimeState CreateExecutionRuntimeSnapshot(CharacterTestBehaviorExecutionRuntimeState target = null)
            {
                target ??= new CharacterTestBehaviorExecutionRuntimeState();
                target.EntityId = _entityId;
                target.ProgramIdentity = _program.Execution.Identity;
                target.ActionCommandState = (int)_orchestrationState;
                target.Operation = (int)_orchestrationOperation;
                target.ResourceId = _orchestrationResourceId;
                target.ExpectedActionId = _orchestrationExpectedActionId.Value;
                target.CommandPending = _orchestrationCommandPending;
                target.CommandSubmitted = _orchestrationCommandSubmitted;
                target.RejectAfterCleanup = _orchestrationRejectAfterCleanup;
                target.PreviousInstanceId = _orchestrationPreviousInstanceId;
                target.OwnedInstanceId = _executionOwnedInstance;
                target.SubmittedFrame = _executionSubmittedFrame;
                target.ReceiptInstanceId = _executionReceiptInstance;
                target.ContinuationFrame = _executionContinuationFrame;
                target.FailureContinuation = _executionFailureContinuation;
                target.FailureReasonId = _orchestrationFailureReasonId;
                target.FailureDetailId = _orchestrationFailureDetailId;
                target.Version = CharacterTestBehaviorExecutionRuntimeState.CurrentVersion;
                target.FailureDetails = _orchestrationFailureDetails;
                target.ControlPending = _executionControlPending;
                target.ControlRejected = _commandRejected;
                target.MoveXMilli = CharacterTestDeterministicUnits.AxisToMilli(_executionMove.x);
                target.MoveZMilli = CharacterTestDeterministicUnits.AxisToMilli(_executionMove.y);
                target.MovementSpeed = _executionMoveSpeed;
                target.Control = _executionControl;
                return target;
            }
            public bool CanRestoreExecutionRuntimeSnapshot(CharacterTestBehaviorExecutionRuntimeState source) => source != null
                && source.Version == CharacterTestBehaviorExecutionRuntimeState.CurrentVersion && source.FailureDetails.IsValid
                && source.EntityId == _entityId && source.ProgramIdentity == _program.Execution.Identity
                && Enum.IsDefined(typeof(CharacterTestRuntimeAiLabBehaviorRecipeOrchestrationState), source.ActionCommandState)
                && CharacterTestBehaviorFailureCodes.IsValid(source.FailureReasonId, source.FailureDetailId)
                && source.OwnedInstanceId >= 0 && source.PreviousInstanceId >= 0 && source.ExpectedActionId >= 0
                && source.MovementSpeed > 0 && !float.IsNaN(source.MovementSpeed) && !float.IsInfinity(source.MovementSpeed);
            public bool TryRestoreExecutionRuntimeSnapshot(CharacterTestBehaviorExecutionRuntimeState source)
            {
                if (!CanRestoreExecutionRuntimeSnapshot(source)) return false;
                _orchestrationState = (CharacterTestRuntimeAiLabBehaviorRecipeOrchestrationState)source.ActionCommandState;
                _orchestrationOperation = (CharacterTestBehaviorRecipeOrchestrationOperation)source.Operation;
                _orchestrationResourceId = source.ResourceId; _orchestrationExpectedActionId = new CharacterActionId(source.ExpectedActionId);
                _orchestrationCommandPending = source.CommandPending; _orchestrationCommandSubmitted = source.CommandSubmitted;
                _orchestrationRejectAfterCleanup = source.RejectAfterCleanup; _orchestrationPreviousInstanceId = source.PreviousInstanceId;
                ObserveOwnedAction(source.OwnedInstanceId); _executionSubmittedFrame = source.SubmittedFrame;
                _executionReceiptInstance = source.ReceiptInstanceId;
                _orchestrationFailureReasonId = source.FailureReasonId;
                _orchestrationFailureDetailId = source.FailureDetailId;
                _orchestrationFailureDetails = source.FailureDetails;
                _orchestrationFailureText = string.Empty;
                _executionContinuationFrame = source.ContinuationFrame; _executionFailureContinuation = source.FailureContinuation;
                _executionControlPending = source.ControlPending; _executionMove = new Vector2(CharacterTestDeterministicUnits.MilliToAxis(source.MoveXMilli), CharacterTestDeterministicUnits.MilliToAxis(source.MoveZMilli));
                _commandRejected = source.ControlRejected;
                _executionMoveSpeed = source.MovementSpeed; _executionControl = source.Control;
                return true;
            }
            public void AddExecutionRuntimeHash(RuntimeHashAccumulator hash, int key = 1)
            {
                hash.BeginObject(key, BehaviorExecutionRuntimeHashFields.SchemaId, BehaviorExecutionRuntimeHashFields.SchemaVersion);
            hash.AddInt(BehaviorExecutionRuntimeHashFields.Version, CharacterTestBehaviorExecutionRuntimeState.CurrentVersion);
                hash.AddInt(BehaviorExecutionRuntimeHashFields.ExecutionRuntimeEntity, _entityId); hash.AddLong(BehaviorExecutionRuntimeHashFields.ExecutionRuntimeIdentity, _program.Execution.Identity);
                hash.AddInt(BehaviorExecutionRuntimeHashFields.ExecutionRuntimeCommandState, (int)_orchestrationState); hash.AddInt(BehaviorExecutionRuntimeHashFields.Operation, (int)_orchestrationOperation);
                hash.AddInt(BehaviorExecutionRuntimeHashFields.Resource, _orchestrationResourceId); hash.AddInt(BehaviorExecutionRuntimeHashFields.Expected, _orchestrationExpectedActionId.Value);
                hash.AddInt(BehaviorExecutionRuntimeHashFields.Pending, _orchestrationCommandPending ? 1 : 0); hash.AddInt(BehaviorExecutionRuntimeHashFields.Submitted, _orchestrationCommandSubmitted ? 1 : 0);
                hash.AddInt(BehaviorExecutionRuntimeHashFields.RejectCleanup, _orchestrationRejectAfterCleanup ? 1 : 0); hash.AddLong(BehaviorExecutionRuntimeHashFields.Previous, _orchestrationPreviousInstanceId);
                hash.AddLong(BehaviorExecutionRuntimeHashFields.Owned, _executionOwnedInstance); hash.AddLong(BehaviorExecutionRuntimeHashFields.SubmittedFrame, _executionSubmittedFrame);
                hash.AddLong(BehaviorExecutionRuntimeHashFields.ReceiptInstance, _executionReceiptInstance);
                hash.AddLong(BehaviorExecutionRuntimeHashFields.ContinuationFrame, _executionContinuationFrame); hash.AddInt(BehaviorExecutionRuntimeHashFields.FailureContinuation, _executionFailureContinuation ? 1 : 0);
                hash.AddInt(BehaviorExecutionRuntimeHashFields.CommitReason, _orchestrationFailureReasonId);
                hash.AddInt(BehaviorExecutionRuntimeHashFields.CommitDetailId, _orchestrationFailureDetailId);
                _orchestrationFailureDetails.ContributeHash(hash, BehaviorExecutionRuntimeHashFields.CommitDetails);
                hash.AddInt(BehaviorExecutionRuntimeHashFields.ControlPending, _executionControlPending ? 1 : 0);
                hash.AddInt(BehaviorExecutionRuntimeHashFields.MoveX, CharacterTestDeterministicUnits.AxisToMilli(_executionMove.x)); hash.AddInt(BehaviorExecutionRuntimeHashFields.MoveZ, CharacterTestDeterministicUnits.AxisToMilli(_executionMove.y));
                hash.AddInt(BehaviorExecutionRuntimeHashFields.ControlRejected, _commandRejected ? 1 : 0);
                hash.AddInt(BehaviorExecutionRuntimeHashFields.MoveSpeed, BitConverter.SingleToInt32Bits(_executionMoveSpeed)); _executionControl.AddHash(ref hash, BehaviorExecutionRuntimeHashFields.Control);
                hash.EndObject();
            }
        }
    }

    internal static class BehaviorExecutionRuntimeHashFields
    {
        internal const int CommitDetails = 22;
        internal const int Control = 23;
        internal const int CommitDetailId = 24;
        internal const int Version = 25;
        internal const int SchemaId = RuntimeHashSchemaIds.RuntimeAiPlannerBehaviorExecutionRuntime;
        internal const int SchemaVersion = 2;
        internal const int ExecutionRuntimeEntity = 1;
        internal const int ExecutionRuntimeIdentity = 2;
        internal const int ExecutionRuntimeCommandState = 3;
        internal const int Operation = 4;
        internal const int Resource = 5;
        internal const int Expected = 6;
        internal const int Pending = 7;
        internal const int Submitted = 8;
        internal const int RejectCleanup = 9;
        internal const int Previous = 10;
        internal const int Owned = 11;
        internal const int SubmittedFrame = 12;
        internal const int ReceiptInstance = 13;
        internal const int ContinuationFrame = 14;
        internal const int FailureContinuation = 15;
        internal const int CommitReason = 16;
        internal const int ControlPending = 17;
        internal const int MoveX = 18;
        internal const int MoveZ = 19;
        internal const int ControlRejected = 20;
        internal const int MoveSpeed = 21;
    }
}
