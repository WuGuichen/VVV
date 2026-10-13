using System;
using MxFramework.CharacterAction;
using MxFramework.Runtime;
using UnityEngine;

namespace MxFramework.Game.CharacterTest.TacticalCard
{
    /// <summary>
    /// Single-actor bridge to the existing single-primary Session. The composition root exclusively owns
    /// this Session and command-driven actor. Prepare/Admit are cold; Tick/Observe/sampling are NoAlloc.
    /// </summary>
    internal sealed partial class CharacterTestTacticalExecutionAdapter : ITacticalExecutionPort, ITacticalHitRewardPort, ITacticalBehindPreviewPort, ITacticalExecutionRetirementPort
    {
        private readonly CharacterTestCharacterRuntimeModule _runtime;
        private readonly CharacterTestRuntimeCommandModule _commands;
        private readonly CharacterTestTacticalCardSession _session;
        private readonly CharacterTestTacticalCardDefinition _source;
        private readonly TacticalDeployment _deployment;
        private readonly CharacterTestCharacterEntity _entity;
        private readonly ITacticalTimeAdapter _time;
        private readonly Rigidbody _body;
        private readonly bool _usesBody;
        private readonly Func<CharacterTestCharacterEntity> _targetResolver;
        private readonly TacticalCooldownTracker _cooldownTracker;
        private readonly Func<CharacterTestRuntimeAiLabBehaviorCardReferenceCatalog,
            CharacterTestRuntimeAiPlannerProjectCatalogState> _publishedCatalogLoader;
        private readonly CharacterTestRuntimeAiPlannerFactState _resolvedFacts = new CharacterTestRuntimeAiPlannerFactState();
        private readonly long[] _rewardedHits = new long[256];
        private int _rewardedHitCount;
        private CharacterTestCharacterEntity _preparedTarget, _target;
        private CharacterActionInstanceState _observedActionState;
        private bool _hasStableSupport, _usesSupportBody;
        private CharacterTestRuntimeAiPlannerFactState _facts;
        private bool _hasContext;
        private RuntimeFrame _frame;
        private TacticalCardInstance _prepared;
        private uint _preparedVersion;
        private EquipmentVersionSnapshot _preparedEquipmentSnapshot;
        private TacticalCardInstance _admitted;
        private ulong _nextId;
        private ulong _executionId;
        private long _lastTick = -1;
        private CharacterActionInstance _actionInstance;
        private ulong _acknowledgedActionRevision;
        private bool _actionStarted;
        private bool _timelineCompleted;
        private bool _faulted;
        private bool _aborted;
        private Rigidbody _support;
        private Vector3 _supportPoint;
        private long _physicsTick = -1;
        private bool _destabilizingContact;
        private bool _pendingImpulse;
        private int _preparedTargetId;
        private int _targetId;
        private readonly CharacterTestRuntimeAiPlannerFactState _unavailableFacts = new CharacterTestRuntimeAiPlannerFactState();
        private long _poseTick = -1;
        private int _poseYaw;
        private float _yawSpeed = float.PositiveInfinity;

        internal ulong CurrentExecutionId => _retired ? 0 : _executionId;
        internal string LastIssue { get; private set; } = string.Empty;
        internal string LastWarning { get; private set; } = string.Empty;
        internal bool HasExecutionFault => _faulted;
        private bool HasBlockingAction => _entity.Action.State == CharacterTestActionStateId.Running
            && !CharacterTestBehaviorExecutionHost.IsFreelyRecoveringAttack(_entity,
                _runtime.ActionRuntime.GetActionInstanceForObservation(_entity.Id));
        internal bool IsCardAvailable(TacticalCardDefinition definition) => EvaluateCardAvailability(definition).IsLegal;
        internal CardPlayEvaluation EvaluateCardAvailability(TacticalCardDefinition definition)
        {
            // The fixed Driver entry owns final joint admission; unsupported mechanism mains remain closed.
            if (definition?.FixedAssembly != null && (!(definition is TacticalActionCardDefinition fixedMain)
                || !fixedMain.HasPublishedBehavior || fixedMain.PointMove != null || fixedMain.PositionRecall != null))
                return new CardPlayEvaluation(CardPlayFailure.CharacterUnavailable, definition.BaseMomentumCost, 0);
            if (!ReferenceEquals(_runtime.FindEntityById(_entity.Id.Value), _entity)
                || !_entity.CombatState.IsAlive || !_entity.BattleState.CanStartNormalAction
                || _entity.BattleState.DominantReason != CharacterTestBattleStateDominantReason.None
                || HasBlockingAction || _commands.PendingCommandCount != 0)
                return new CardPlayEvaluation(CardPlayFailure.CharacterUnavailable, 0, 0);
            if (InteractionPreparationPayload.Matches(definition) && definition.HasPublishedBehavior)
            {
                bool exact = _deployment?.ContainsDefinition(definition) == true;
                return new CardPlayEvaluation(exact ? CardPlayFailure.None : CardPlayFailure.CharacterUnavailable, definition.BaseMomentumCost, 0);
            }
            // Point movement owns no physical action/Ability binding; its input port retains final admission.
            if (definition is TacticalActionCardDefinition point && point.PointMove != null)
                return new CardPlayEvaluation(CardPlayFailure.None, point.BaseMomentumCost, 0);
            if (definition is TacticalActionCardDefinition recall && recall.PositionRecall != null)
                return new CardPlayEvaluation(RecallAvailable(recall.PositionRecall) ? CardPlayFailure.None : CardPlayFailure.CharacterUnavailable, 0, 0);
            return definition is TacticalActionCardDefinition action
                ? EvaluateBoundActionCooldowns(action) : new CardPlayEvaluation(CardPlayFailure.None, 0, 0);
        }

        internal CardPlayEvaluation EvaluateCardAvailability(TacticalCardInstance instance,int reservationCredit=0)
        {
            if(instance==null)return new CardPlayEvaluation(CardPlayFailure.CardNotInHand,0,0);
            var gate=EvaluateCardAvailability(instance.Definition);if(!gate.IsLegal)return gate;
            if(!(instance.Definition is TacticalActionCardDefinition action) || action.PointMove!=null || action.PositionRecall!=null)return gate;
            return EvaluateBoundActionCosts(action,instance.Zone==CardZone.PendingAction?0:action.BaseMomentumCost,reservationCredit);
        }

        /// <summary>First observed authority state and subsequent changes, once per execution/state.</summary>
        /// <remarks>Observers must not throw, drive the world, or re-enter admission. Observe is safe.</remarks>
        internal event Action<ulong, CharacterActionInstanceState> ActionStateChanged;

        internal CharacterTestTacticalExecutionAdapter(CharacterTestCharacterRuntimeModule runtime,
            CharacterTestRuntimeCommandModule commands, CharacterTestTacticalCardSession session,
            CharacterTestTacticalCardDefinition source, CharacterTestCharacterEntity entity,
            ITacticalTimeAdapter time, Rigidbody authoritativeBody = null,
            Func<CharacterTestCharacterEntity> targetResolver = null,
            TacticalCooldownTracker cooldownTracker = null, TacticalDeployment deployment = null,
            Func<CharacterTestRuntimeAiLabBehaviorCardReferenceCatalog, CharacterTestRuntimeAiPlannerProjectCatalogState> publishedCatalogLoader = null)
        {
            _runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));
            _commands = commands ?? throw new ArgumentNullException(nameof(commands));
            _session = session ?? throw new ArgumentNullException(nameof(session));
            _source = source;
            _deployment = deployment;
            _entity = entity ?? throw new ArgumentNullException(nameof(entity));
            _time = time ?? throw new ArgumentNullException(nameof(time));
            _body = authoritativeBody;
            _usesBody = authoritativeBody != null;
            _targetResolver = targetResolver;
            _cooldownTracker = cooldownTracker;
            // Fixtures supply an isolated round-tripped catalog; shared ownership and identity checks still run.
            _publishedCatalogLoader = publishedCatalogLoader ?? CharacterTestRuntimeAiPlannerProjectCatalogLoader.Load;
        }

        [Obsolete("profileId is eliminated in Phase 5 (T-16). Use constructor without profileId.")]
        internal CharacterTestTacticalExecutionAdapter(CharacterTestCharacterRuntimeModule runtime,
            CharacterTestRuntimeCommandModule commands, CharacterTestTacticalCardSession session,
            CharacterTestTacticalCardDefinition source, string profileId, CharacterTestCharacterEntity entity,
            ITacticalTimeAdapter time, Rigidbody authoritativeBody = null,
            Func<CharacterTestCharacterEntity> targetResolver = null,
            TacticalCooldownTracker cooldownTracker = null)
            : this(runtime, commands, session, source, entity, time, authoritativeBody, targetResolver, cooldownTracker)
        {
        }

        /// <summary>NoAlloc. Supply the next formal world frame and current facts before admission/Tick.</summary>
        internal void SetContext(CharacterTestRuntimeAiPlannerFactState facts, RuntimeFrame frame)
        {
            _facts = facts;
            _frame = frame;
            _hasContext = true;
        }

        public bool TryPrepare(TacticalCardInstance card, out string issue)
        {
            if (card?.Definition.FixedAssembly != null)
                return Reject("execution:FixedAssemblyJointCommitUnavailable", out issue);
            if ((card?.Definition as TacticalActionCardDefinition)?.PositionRecall != null) return PrepareRecall(card, out issue);
            _recallExecution = false;
            if (_tractionExecution && _entity.GroundPointTraction.IsActive)
                return Reject("execution:TractionAlreadyActive", out issue);
            _tractionExecution = false;
            _pointNavigation?.ClearDestination(); _pointExecution = false;
            _prepared = null;
            if (!ValidateCard(card, out var facts, out issue)) return false;

            int actionId = 0;
            if (card?.Definition is TacticalActionCardDefinition actionCard)
            {
                if (actionCard.ActionId.IsValid)
                {
                    actionId = actionCard.ActionId.Value;
                }

            }

            if (card.Definition is TacticalActionCardDefinition published && published.HasPublishedBehavior)
            {
                if (!TryCompilePublished(published, out issue)) return false;
            }
            else if (!_session.TryCompile(_source, actionId)) return Reject(_session.LastIssue, out issue);
            _prepared = card;
            _preparedVersion = card.Version;
            _preparedEquipmentSnapshot = _entity.Equipment.ToSnapshot();
            _preparedTargetId = facts.TargetEntityId;
            _preparedTarget = _runtime.FindEntityById(_preparedTargetId);
            issue = string.Empty;
            return true;
        }

        public bool TryAdmit(TacticalCardInstance card, out ulong executionId, out string issue)
        {
            if (card?.Definition.FixedAssembly != null)
            { executionId = 0; return Reject("execution:FixedAssemblyJointCommitUnavailable", out issue); }
            if ((card?.Definition as TacticalActionCardDefinition)?.PositionRecall != null) return AdmitRecall(card, out executionId, out issue);
            executionId = 0;
            if (!ReferenceEquals(card, _prepared) || card == null || card.Version != _preparedVersion)
                return Reject("execution:StalePreparation", out issue);

            var currentEquipment = _entity.Equipment.ToSnapshot();
            if (!_preparedEquipmentSnapshot.Equals(currentEquipment))
                return Reject("execution:EquipmentChangedAfterPrepare", out issue);

            if (!ValidateCard(card, out var facts, out issue)) return false;
            if (facts.TargetEntityId != _preparedTargetId ||
                !ReferenceEquals(_runtime.FindEntityById(facts.TargetEntityId), _preparedTarget))
                return Reject("execution:TargetChanged", out issue);
            if (_nextId == ulong.MaxValue) return Reject("execution:IdentityExhausted", out issue);
            // Start creates the host and validates current dependencies, but submits no commands or world step.
            bool started = card.Definition is TacticalActionCardDefinition published && published.HasPublishedBehavior
                ? _session.TryStartPublished(facts, _frame) : _session.TryStart(_source, facts, _frame);
            if (!started) return Reject(_session.LastIssue, out issue);
            BeginExecutionIdentity(card);
            executionId = _executionId;
            _admitted = card;
            _targetId = _preparedTargetId;
            _target = _preparedTarget;
            _rewardedHitCount = 0;
            _observedActionState = CharacterActionInstanceState.None;
            _lastTick = _frame.Value;
            _actionInstance = null;
            _actionStarted = _timelineCompleted = _faulted = _aborted = false;
            _prepared = null;
            LastIssue = issue = string.Empty;
            return true;
        }

        private bool ValidateCard(TacticalCardInstance card, out CharacterTestRuntimeAiPlannerFactState facts, out string issue)
        {
            facts = null;
            if (_session.IsExecuting || (_admitted != null && _admitted.Zone == CardZone.Resolving))
                return Reject("execution:AlreadyAdmitted", out issue);
            if (card == null || card.Zone != CardZone.PendingAction || !(card.Definition is TacticalActionCardDefinition action))
                return Reject("execution:PendingActionRequired", out issue);
            if (action.HasPublishedBehavior && !ValidatePublishedOwnership(action, out issue)) return false;

            // Mutual exclusion and binding validation
            if (!action.HasPublishedBehavior && !action.ActionId.IsValid && action.ActionSlot == TacticalActionSlot.None)
                return Reject("Card:MissingActionOrSlotBinding", out issue);

            int targetActionId = 0;
            if (action.ActionId.IsValid)
            {
                targetActionId = action.ActionId.Value;
                if (action.ActionSlot != TacticalActionSlot.None)
                {
                    LastWarning = "FixedActionOverridesSlot";
                }
            }
            else if (!action.HasPublishedBehavior)
            {
                return Reject("Action:ExplicitSelectionRequired", out issue);
            }

            if (targetActionId > 0)
            {
                if (!CharacterTestTacticalCardInputs.TryResolveSelectedAction(null, _runtime.ActionRuntime.Registry.Actions,
                    targetActionId, out _, out string actionIssue)) return Reject(actionIssue, out issue);
            }
            if (action.TargetMode != TacticalTargetMode.Enemy
                || !action.HasPublishedBehavior && (_source == null || (int)action.Approach != (int)_source.Approach))
                return Reject("execution:UnsupportedSessionBinding", out issue);
            if (_cooldownTracker != null && _cooldownTracker.IsOnCooldown(_entity.Identity.CharacterId, action, _frame.Value))
                return Reject("execution:CardOnCooldown", out issue);
            if (!ReferenceEquals(_runtime.FindEntityById(_entity.Id.Value), _entity) || !_entity.CombatState.IsAlive
                || !_hasContext || _frame.Value < 0 || _frame.Value >= int.MaxValue)
                return Reject("execution:ContextUnavailable", out issue);
            if (!ValidateBoundActionCooldowns(action, out issue)) return Reject(issue, out issue);
            if(!EvaluateBoundActionCosts(action,0).IsLegal)return Reject("execution:CombinedCostUnavailable",out issue);
            if (!_entity.BattleState.CanStartNormalAction || _entity.BattleState.DominantReason != CharacterTestBattleStateDominantReason.None)
                return Reject("execution:ActorNotFree", out issue);
            if (HasBlockingAction || _commands.PendingCommandCount != 0)
                return Reject("execution:ActorBusy", out issue);
            var target = ResolveAdmissionTarget();
            if (target == null || !ReferenceEquals(_runtime.FindEntityById(target.Id.Value), target))
                return Reject("execution:TargetMissing", out issue);
            if (!target.CombatState.IsAlive) return Reject("execution:TargetDead", out issue);
            if (ReferenceEquals(target, _entity) || target.Team.TeamId == _entity.Team.TeamId)
                return Reject("execution:InvalidTarget", out issue);
            facts = _facts != null && _facts.TargetExists ? _facts : BuildTargetFacts(target);
            issue = string.Empty;
            return true;
        }

        private CharacterTestCharacterEntity ResolveAdmissionTarget() => _facts != null && _facts.TargetExists
            ? _runtime.FindEntityById(_facts.TargetEntityId) : ResolveTarget();

        private CharacterTestCharacterEntity ResolveTarget()
        {
            if (_targetResolver != null) return _targetResolver();
            CharacterTestCharacterEntity selected = null;
            var entities = _runtime.Entities;
            for (int i = 0; i < entities.Count; i++)
            {
                var candidate = entities[i];
                if (candidate.CombatState.IsAlive && candidate.Team.TeamId != _entity.Team.TeamId &&
                    (selected == null || candidate.Id.Value < selected.Id.Value)) selected = candidate;
            }
            return selected;
        }

        // Open-arena fallback reuses the existing direct navigation and hurtbox facts. The compiled
        // Session still owns measured range/facing admission; a root distance never fabricates a hit.
        private CharacterTestRuntimeAiPlannerFactState BuildTargetFacts(CharacterTestCharacterEntity target)
        {
            CharacterTestDirectAiNavigationService.Instance.TryResolveMove(_entity, target, 0f, out var navigation);
            bool hasDistance = _runtime.PerceptionFrameCache.TryAcquireNearestHurtbox(
                _frame, _entity, target, out _, out var distance) && distance.HasDistance;
            _resolvedFacts.SetTarget(target.Id.Value, hasDistance ? distance.SurfaceGapMeters : 0f,
                hasDistance, CharacterTestDirectAiNavigationService.ToTarget(_entity, target).magnitude,
                navigation, _entity.Action.State == CharacterTestActionStateId.Running, true, false, 0, 1f,
                targetDead: !target.CombatState.IsAlive);
            return _resolvedFacts;
        }

        /// <summary>NoAlloc. Call once before command consumption. Paused wall time is never accumulated.</summary>
        internal bool Tick()
        {
            if (_retired || _executionId == 0 || _time.IsSimulationPaused || _frame.Value <= _lastTick || _faulted) return false;
            if (_recallExecution) return TickRecall();
            if (_tractionExecution) return TickTraction();
            if (_pointExecution) return TickPoint();
            ReadActionFacts();
            if (_faulted) return false;
            _lastTick = _frame.Value;
            if (!_session.IsExecuting) return false;
            var facts = _facts;
            bool targetPresent = ReferenceEquals(_runtime.FindEntityById(_targetId), _target)
                && _target != null && _target.CombatState.IsAlive;
            if (targetPresent && (facts == null || !facts.TargetExists)) facts = BuildTargetFacts(_target);
            if (!targetPresent || facts == null || !facts.TargetExists || facts.TargetEntityId != _targetId)
            {
                if (!_actionStarted && TryAbortPreAction(_executionId, "execution:TargetLost")) return true;
                // Never redirect a committed action's movement/facing to a newly selected target.
                _unavailableFacts.SetNoTarget(_entity.Action.State == MxFramework.Game.CharacterTest.CharacterTestActionStateId.Running);
                facts = _unavailableFacts;
            }
            bool result = _session.Tick(facts, _frame);
            ReadActionFacts();
            if (!result && _session.State == CharacterTestRuntimeAiLabLogicRuntimeState.Failed)
                SetFault(_session.LastIssue);
            ReadBehaviorFacts();
            return result;
        }

        public ExecutionObservation Observe(ulong executionId)
        {
            if (executionId == 0 || executionId != _executionId) return default;
            if (_retired) return _retiredObservation;
            if (_recallExecution) return ObserveRecall(executionId);
            if (_tractionExecution) return ObserveTraction(executionId);
            if (_pointExecution) return ObservePoint(executionId);
            ReadActionFacts();
            ReadBehaviorFacts();
            bool present = ReferenceEquals(_runtime.FindEntityById(_entity.Id.Value), _entity);
            if (!present) SetFault("execution:ActorUnavailable");
            if (_faulted) return new ExecutionObservation(0, false, false, false, false, false, LastIssue);
            bool terminal = !_faulted && (_session.State == CharacterTestRuntimeAiLabLogicRuntimeState.Completed
                || _session.State == CharacterTestRuntimeAiLabLogicRuntimeState.Failed
                || (_aborted && !_session.IsExecuting));
            bool acknowledged = present && _commands.PendingCommandCount == 0
                && _commands.LastCommandErrors.Count == 0 && !_session.HasPendingActionCommand;
            bool released = terminal && acknowledged && !_entity.Input.HasActiveMove
                && (!_entity.Input.BehaviorControl.IsPresent || _entity.Input.BehaviorControl.IsRelease);
            return new ExecutionObservation(executionId, _actionStarted, _timelineCompleted,
                terminal, released, acknowledged);
        }

        public bool TryAbortPreAction(ulong executionId, string reason)
        {
            if (_retired || executionId == 0 || executionId != _executionId || _faulted) return false;
            if (_recallExecution) { LastIssue = reason ?? string.Empty; return CancelRecall(); }
            if (_tractionExecution) { LastIssue = reason ?? string.Empty; return CancelTraction(); }
            ReadActionFacts();
            // A submitted start can still be consumed this frame. Never turn it into a stop-action command.
            if (_actionStarted || _session.HasPendingActionCommand || _commands.PendingCommandCount != 0) return false;
            if (_aborted) return true;
            var stopFrame = _frame;
            if (_runtime.TryGetLastCompletedRuntimeFrame(out var completed) && completed.Value >= stopFrame.Value)
            {
                if (completed.Value >= int.MaxValue - 1) return false;
                stopFrame = new RuntimeFrame(completed.Value + 1);
            }
            _session.Stop(_facts, stopFrame);
            _aborted = true;
            LastIssue = reason ?? string.Empty;
            return true;
        }

        private void ReadBehaviorFacts()
        {
            if (_faulted || _session.State != CharacterTestRuntimeAiLabLogicRuntimeState.Failed) return;
            // A resolved failure is terminal too. Cleanup failures cannot safely complete a turn.
            switch (_session.EndReason)
            {
                case CharacterTestBehaviorExecutionEndReason.CleanupTimedOut:
                    SetFault("execution:BehaviorCleanupTimedOut");
                    break;
                case CharacterTestBehaviorExecutionEndReason.CleanupFailed:
                    SetFault("execution:BehaviorCleanupFailed");
                    break;
                case CharacterTestBehaviorExecutionEndReason.ConditionRejected:
                    LastIssue = "execution:BehaviorConditionRejected";
                    break;
                case CharacterTestBehaviorExecutionEndReason.TargetMissing:
                    LastIssue = "execution:BehaviorTargetMissing";
                    break;
                case CharacterTestBehaviorExecutionEndReason.TimedOut:
                    LastIssue = "execution:BehaviorTimedOut";
                    break;
                case CharacterTestBehaviorExecutionEndReason.ActionRejected:
                    LastIssue = "execution:BehaviorActionRejected";
                    break;
                default:
                    SetFault("execution:UnknownBehaviorFailure");
                    break;
            }
        }

        private void ReadActionFacts()
        {
            var owned = _session.AcknowledgedActionInstance;
            bool changed = owned != null && (!ReferenceEquals(owned, _actionInstance)
                || _acknowledgedActionRevision != _session.AcknowledgedActionRevision);
            if (changed)
            {
                _actionInstance = owned; _acknowledgedActionRevision = _session.AcknowledgedActionRevision;
                _actionStarted = true;
                _timelineCompleted = false;
            }
            // The receipt's terminal state survives gateway removal and pooled-object replacement.
            if (_actionInstance == null) return;
            var state = _session.AcknowledgedActionState;
            if (state == CharacterActionInstanceState.None && _admitted?.Zone == CardZone.Resolving)
                SetFault("execution:OwnedActionTerminalReceiptUnavailable");
            _timelineCompleted = state == CharacterActionInstanceState.Finished;
            if (state == CharacterActionInstanceState.Cancelled || state == CharacterActionInstanceState.Interrupted)
            {
                _timelineCompleted = false;
                if (_admitted?.Zone == CardZone.Resolving)
                    SetFault("execution:ActionInterrupted");
            }
            if (!changed && _observedActionState == state) return;
            _observedActionState = state;
            ActionStateChanged?.Invoke(_executionId, state);
        }

        /// <summary>NoAlloc. The authority hit consumer supplies an execution-correlated event and the driver's ledger.</summary>
        /// <remarks>Bounded to 256 distinct events per execution; overflow rejects without eviction or payment.</remarks>
        public bool TryApplyHitReward(ulong executionId, long hitEventId, int momentumAmount, IMomentumLedger ledger)
        {
            if (_retired || executionId == 0 || executionId != _executionId || hitEventId <= 0 || momentumAmount <= 0 ||
                ledger == null || _admitted == null || _admitted.Zone != CardZone.Resolving || _aborted) return false;
            ReadActionFacts();
            if (_faulted || !_actionStarted || !_session.IsExecuting || _time.IsSimulationPaused ||
                !ReferenceEquals(_runtime.FindEntityById(_entity.Id.Value), _entity) || !_entity.CombatState.IsAlive) return false;
            for (int i = 0; i < _rewardedHitCount; i++) if (_rewardedHits[i] == hitEventId) return false;
            if (_rewardedHitCount == _rewardedHits.Length) return false;
            _rewardedHits[_rewardedHitCount++] = hitEventId;
            ledger.Grant(momentumAmount);
            return true;
        }

        /// <summary>NoAlloc. Physics owner supplies support/contact facts for this exact completed tick.</summary>
        internal void SetPhysicsSample(long tick, Rigidbody support, Vector3 supportContactPoint,
            bool hasNewDestabilizingContact = false, bool hasPendingImpulse = false, bool hasStableSupport = true)
        {
            _physicsTick = tick;
            _support = support;
            _supportPoint = supportContactPoint;
            _destabilizingContact = hasNewDestabilizingContact;
            _pendingImpulse = hasPendingImpulse;
            _hasStableSupport = hasStableSupport && Finite(supportContactPoint.x)
                && Finite(supportContactPoint.y) && Finite(supportContactPoint.z);
            _usesSupportBody = support != null;
        }

        /// <summary>NoAlloc. Call only after commands, motion, contacts, rewards and receipts complete.</summary>
        internal SettleSample BuildSettleSample(long currentTick, CharacterTestCharacterEntity entity)
        {
            var observation = Observe(_executionId);
            if (entity == null || !ReferenceEquals(entity, _entity)
                || !ReferenceEquals(_runtime.FindEntityById(entity.Id.Value), entity))
                return new SettleSample(currentTick, false, false, false, false, true, true,
                    false, false, false, false, float.PositiveInfinity, float.PositiveInfinity);
            if (!_pointExecution && !_tractionExecution && !_recallExecution && !_faulted
                && _session.IsExecuting)
            {
                bool completed = _session.TryCompleteTerminalAction(_session.AcknowledgedActionInstance, _facts, new RuntimeFrame(currentTick));
                if (_session.State == CharacterTestRuntimeAiLabLogicRuntimeState.Failed) SetFault(_session.LastIssue);
                if (completed || _faulted) observation = Observe(_executionId);
            }
            bool physicsKnown = _physicsTick == currentTick;
            bool supported = physicsKnown && _hasStableSupport && (!_usesSupportBody || _support != null);
            Vector3 velocity = new Vector3(entity.HorizontalVelocity.x, entity.VerticalVelocity, entity.HorizontalVelocity.y);
            if (!_usesBody && currentTick > _poseTick)
            {
                int yaw = entity.Transform.YawMilliDegrees;
                _yawSpeed = _poseTick >= 0 && currentTick == _poseTick + 1
                    ? CharacterTestDeterministicMath.ShortestAngleDelta(_poseYaw, yaw) * (Mathf.Deg2Rad * 60f / 1000f)
                    : float.PositiveInfinity;
                _poseYaw = yaw;
                _poseTick = currentTick;
            }
            Vector3 angular = new Vector3(0f, _yawSpeed, 0f);
            if (_usesBody)
            {
                if (_body == null) physicsKnown = false;
                else { velocity = _body.linearVelocity; angular = _body.angularVelocity; }
            }
            if (physicsKnown && _support != null)
            {
                velocity -= _support.GetPointVelocity(_supportPoint);
                angular -= _support.angularVelocity;
            }
            bool reaction = entity.CombatState.IsReacting || !entity.CombatState.IsAlive;
            // Entity.MovementMode retains the selected locomotion profile (e.g. Run) at rest.
            // The action/control authority, rather than that profile or an Animator state, defines neutral.
            bool neutral = entity.Action.State != MxFramework.Game.CharacterTest.CharacterTestActionStateId.Running && entity.Action.MovementMode == CharacterMovementMode.Idle
                && !entity.Input.HasActiveMove && !entity.MovementLocked && !entity.Action.MovementLocked && !reaction;
            bool pending = !physicsKnown || _pendingImpulse || entity.PendingMotionRequestCount != 0
                || entity.JumpBufferFramesRemaining != 0 || entity.Action.JumpImpulseYMillimetersPerSecond != 0;
            var battleState = entity.BattleState;
            bool canActFreely = entity.CombatState.IsAlive && !reaction && battleState.IsValid
                && battleState.DominantReason == CharacterTestBattleStateDominantReason.None
                && battleState.CanStartNormalAction && battleState.CanMoveByInput;
            return new SettleSample(currentTick, observation.ActionTimelineCompleted, observation.BehaviorTerminal,
                neutral, reaction, !entity.IsGrounded, pending, observation.OwnedIntentsReleased,
                observation.CommandsAcknowledged, physicsKnown && supported && entity.IsGrounded,
                physicsKnown && _destabilizingContact, velocity.magnitude, angular.magnitude,
                observation.BehaviorTerminal && !observation.ActionStarted && observation.CommandsAcknowledged,
                playerCanActFreely: canActFreely);
        }

        private bool Reject(string issue, out string result) { LastIssue = result = issue; return false; }
        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        private void SetFault(string issue) { _faulted = true; LastIssue = issue; }
    }
}
