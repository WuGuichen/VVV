using System;
using System.Collections.Generic;
using System.Globalization;
using MxFramework.CharacterAction;
using MxFramework.Gameplay;
using MxFramework.Runtime;

namespace MxFramework.Game.CharacterTest
{
    internal sealed class CharacterTestCharacterActionSystem
    {
        private readonly CharacterTestCharacterActionConfigSourceOptions _options;
        private readonly AbilityRuntimeModule _abilityRuntime;
        private readonly CharacterTestSkillSlotRuntime _skillSlots;
        private readonly IRuntimeLogger _logger;
        private CharacterTestCharacterActionRuntime _runtime;
        private CharacterTestCharacterDefinitionRegistry _animationDefinitions;
        private CharacterTestCharacterActionReloadResult? _lastReloadResult;
        private bool _loggedValidation;
        private CharacterTestTraceRecorder _traceRecorder;
        private ICharacterTestRuntimeAiPlannerDodgeOutcomeSink _runtimeAiPlannerDodgeOutcomeSink;
        private Func<CharacterTestCharacterEntity, RuntimeFrame, bool> _primaryAttackOpportunityGuard;
        private readonly HashSet<int> _loggedMissingActionSetEntityIds = new HashSet<int>();
        private readonly Dictionary<int, CombatEvadeState> _combatEvadeStates =
            new Dictionary<int, CombatEvadeState>(16);
        private CharacterTestCharacterRuntimeWorld _currentWorld;
        private readonly CharacterTestCombatEvadeTuning _combatEvadeTuning;

        private const CharacterTestActionStateId RunningActionState = CharacterTestActionStateId.Running;
        private const CharacterTestActionStateId CompletedLifecycleActionState = CharacterTestActionStateId.Completed;
        private const CharacterTestActionPhaseId DeathLifecyclePhase = CharacterTestActionPhaseId.Death;
        private static class ActionTraceKinds
        {
            internal const int ConfigurationReload = 90810;
            internal const int MovementCancel = 90811;
            internal const int DeadCleanup = 90812;
            internal const int Jump = 90813;
            internal const int AbilitySlot = 90814;
            internal const int SkillSlot = 90815;
            internal const int AbilityInput = 90816;
            internal const int JumpLanded = 90817;
            internal const int Landing = 90818;
        }

        private enum BufferedIntentResolveStatus
        {
            None = 0,
            Started = 1,
            TemporaryBlocked = 2,
            PermanentRejected = 3
        }

        public CharacterTestCharacterActionSystem(
            IRuntimeLogger logger,
            CharacterTestCharacterActionConfigSourceOptions options = null,
            AbilityRuntimeModule abilityRuntime = null,
            CharacterTestSkillSlotRuntime skillSlots = null,
            CharacterTestCharacterActionRuntime runtime = null,
            CharacterTestCharacterActionCatalog actionCatalog = null)
        {
            _logger = logger ?? NullRuntimeLogger.Instance;
            _options = options ?? CharacterTestCharacterActionConfigSourceOptions.CreateDefault();
            _abilityRuntime = abilityRuntime;
            _skillSlots = skillSlots;
            _runtime = runtime ?? (actionCatalog != null
                ? new CharacterTestCharacterActionRuntime(actionCatalog)
                : CharacterTestCharacterActionConfigSource.Load(_options));
            _combatEvadeTuning = CharacterTestCombatEvadeTuning.LoadDefault();
        }


        public CharacterTestCharacterActionRuntime Runtime => _runtime;
        public CharacterTestCharacterActionReloadResult? LastReloadResult => _lastReloadResult;

        public void SetTraceRecorder(CharacterTestTraceRecorder traceRecorder)
        {
            _traceRecorder = traceRecorder;
        }

        public void SetRuntimeAiPlannerDodgeOutcomeSink(
            ICharacterTestRuntimeAiPlannerDodgeOutcomeSink sink)
        {
            _runtimeAiPlannerDodgeOutcomeSink = sink;
        }

        internal void SetPrimaryAttackOpportunityGuard(
            Func<CharacterTestCharacterEntity, RuntimeFrame, bool> guard)
        {
            _primaryAttackOpportunityGuard = guard;
        }

        internal void PrepareAnimationDefinitions(CharacterTestCharacterDefinitionRegistry definitions)
        {
            _runtime.PrepareAnimationDefinitions(definitions);
            _animationDefinitions = definitions;
        }

        public CharacterTestCharacterActionReloadResult ReloadActionConfig(
            CharacterTestCharacterRuntimeWorld world,
            RuntimeFrame frame)
        {
            return ReloadActionConfig(world, frame, _options);
        }

        public CharacterTestCharacterActionReloadResult ReloadActionConfig(
            CharacterTestCharacterRuntimeWorld world,
            RuntimeFrame frame,
            CharacterTestCharacterActionConfigSourceOptions options)
        {
            CharacterTestCharacterActionRuntime reloaded =
                CharacterTestCharacterActionConfigSource.Load(options ?? _options);
            if (reloaded == null || !reloaded.IsReady)
            {
                CharacterTestCharacterActionReloadResult rejected =
                    CharacterTestCharacterActionReloadResult.Rejected(
                        reloaded,
                        frame,
                        "Action config reload rejected: " + FormatFirstDiagnostic(reloaded));
                _lastReloadResult = rejected;
                _logger.Warning(
                    "CharacterTest.Actions",
                    rejected.Message
                    + " frame=" + frame.Value
                    + " source=" + rejected.SourceDescription);
                return rejected;
            }

            if (_animationDefinitions != null)
                reloaded.PrepareAnimationDefinitions(_animationDefinitions);

            _runtime?.TerminateAll(
                world,
                frame,
                CharacterActionLifecycleReason.ConfigurationReload,
                new RuntimeTraceIdentity(ActionTraceKinds.ConfigurationReload, 0, frame.Value));
            _runtime = reloaded;
            _runtime.BindRuntimeWorld(world);
            _loggedValidation = false;
            _loggedMissingActionSetEntityIds.Clear();
            CharacterTestCharacterActionReloadResult accepted =
                CharacterTestCharacterActionReloadResult.Accepted(reloaded, frame);
            _lastReloadResult = accepted;
            _logger.Info(
                "CharacterTest.Actions",
                accepted.Message
                + " frame=" + frame.Value
                + " source=" + accepted.SourceDescription);
            return accepted;
        }

        /// <remarks>NoAlloc on the steady-state path after registration/initialization; invalid-call and exception paths may allocate diagnostics.</remarks>
        public void Tick(
            CharacterTestCharacterRuntimeWorld world,
            RuntimeFrame frame,
            ICharacterTestStatusControlProvider statusControl = null,
            int jumpBufferFrames = 0)
        {
            _currentWorld = world;
            _runtime.BindRuntimeWorld(world);
            IReadOnlyList<CharacterTestCharacterEntity> entities = world.Entities;
            LogValidationOnce();
            for (int i = 0; i < entities.Count; i++)
            {
                CharacterTestCharacterEntity entity = entities[i];
                TickCombatEvadeRegeneration(world, entity, frame);
                if (!_runtime.TryGetEntityActionSet(entity.Id, out _))
                {
                    RejectMissingEntityActionSet(entity, frame);
                    continue;
                }
                if (TickLifecycleOnly(world, entity, frame))
                    continue;

                if (statusControl != null && statusControl.IsActionFrozen(entity))
                {
                    FreezeAction(entity, frame, statusControl.GetActionFrozenRejectReasonId(entity));
                    continue;
                }

                if (!entity.BattleState.CanStartNormalAction)
                {
                    RejectNormalActionRequests(entity, frame);
                    _runtime.TickEntity(entity.Id, frame);
                    continue;
                }

                bool started = false;
                entity.GameplayIntentBuffer.ClearExpired(frame.Value);
                if (entity.GameplayIntentBuffer.TryPeekHighestPriority(
                    frame.Value,
                    out CharacterTestGameplayIntent intent))
                {
                    BufferedIntentResolveStatus status = TryResolveBufferedIntent(entity, frame, intent);
                    if (status == BufferedIntentResolveStatus.Started)
                    {
                        started = true;
                        entity.GameplayIntentBuffer.Consume(intent.Kind);
                    }
                    else if (status == BufferedIntentResolveStatus.PermanentRejected)
                    {
                        entity.GameplayIntentBuffer.Consume(intent.Kind);
                    }
                }

                if (entity.Input.HasAbilitySlotCast)
                    entity.SetInput(entity.Input.WithoutAbilitySlotCast());

                if (!started && entity.Input.HasActiveMove)
                {
                    started = _runtime.TryCancelOnly(
                        entity.Id,
                        CharacterActionRequestType.Movement,
                        frame,
                        CharacterActionSourceKind.Command,
                        CreateActionTrace(ActionTraceKinds.MovementCancel, entity, frame));
                }

                if (!started)
                {
                    _runtime.TickEntity(entity.Id, frame);
                }
            }
        }

        private void RejectMissingEntityActionSet(
            CharacterTestCharacterEntity entity,
            RuntimeFrame frame)
        {
            const CharacterActionDiagnosticCode code = CharacterActionDiagnosticCode.MissingActionSet;
            entity.SetAction(entity.Action.WithReject(code));
            entity.GameplayIntentBuffer.Clear();
            entity.SetInput(entity.Input.WithoutActionStartRequests().WithoutAbilitySlotCast());
            if (_loggedMissingActionSetEntityIds.Add(entity.Id.Value))
            {
                _logger.Warning(
                    "CharacterTest.Actions",
                    "Missing entity ActionSet. character=" + entity.Identity.CharacterStableId
                    + " package=" + entity.Identity.PrefabKey.PackageId
                    + " equipmentState=" + entity.Equipment.EquipmentStateStableId
                    + " frame=" + frame.Value);
            }
        }

        private bool TickLifecycleOnly(
            CharacterTestCharacterRuntimeWorld world,
            CharacterTestCharacterEntity entity,
            RuntimeFrame frame)
        {
            if (entity == null || entity.BattleState.LifeState == CharacterTestCharacterLifeState.Alive)
                return false;

            if (HasActionStartRequest(entity))
            {
                entity.SetAction(entity.Action.WithReject(CharacterTestActionRejectCode.BattleStateBlocked));
                _traceRecorder?.AttackAction(
                    frame,
                    entity.Id.Value,
                    "control rejected action_start reason=BattleStateBlocked"
                    + " lifeState=" + entity.BattleState.LifeState,
                    warning: true);
            }

            entity.GameplayIntentBuffer.Clear();
            entity.ConsumeJumpBuffer();
            entity.SetInput(entity.Input.WithoutActionStartRequests().WithoutAbilitySlotCast());
            if (entity.BattleState.LifeState == CharacterTestCharacterLifeState.Dead)
            {
                if (!IsDeathTerminalActionHold(entity))
                    _runtime.TerminateAndReleaseEntityAction(
                        entity.Id,
                        frame,
                        CharacterActionLifecycleReason.Death,
                        CreateActionTrace(ActionTraceKinds.DeadCleanup, entity, frame));
                return true;
            }

            CharacterActionId lifecycleActionId = entity.Action.ActiveActionId;
            string lifecycleAnimationKey = entity.Action.LastAnimationActionKey;
            int lifecycleLocalFrame = entity.Action.LocalFrame;
            _runtime.TickEntity(entity.Id, frame);
            CompleteLifecycleActionIfIdle(
                world,
                entity,
                frame,
                lifecycleActionId,
                lifecycleAnimationKey,
                lifecycleLocalFrame);
            return true;
        }

        private void CompleteLifecycleActionIfIdle(
            CharacterTestCharacterRuntimeWorld world,
            CharacterTestCharacterEntity entity,
            RuntimeFrame frame,
            CharacterActionId lifecycleActionId,
            string lifecycleAnimationKey,
            int lifecycleLocalFrame)
        {
            if (entity == null || IsRunningAction(entity))
                return;

            if (entity.BattleState.LifeState == CharacterTestCharacterLifeState.Dying)
            {
                entity.CompleteDeath(frame.Value);
                world?.SetGameplayLifecycle(entity, GameplayLifecycleState.Destroyed);
                int terminalFrame = lifecycleLocalFrame;
                if (_runtime.Registry.TryGetAction(lifecycleActionId, out CharacterActionConfig lifecycleAction)
                    && lifecycleAction.DurationFrames.HasValue)
                    terminalFrame = Math.Max(terminalFrame, lifecycleAction.DurationFrames.Value - 1);
                ApplyDeathTerminalActionHold(entity, lifecycleActionId, lifecycleAnimationKey, terminalFrame);
                if (world != null)
                    CharacterTestBattleStateSystem.RefreshEntity(world, entity, frame, null);
                _traceRecorder?.AttackAction(
                    frame,
                    entity.Id.Value,
                    "death complete target=" + entity.Id.Value
                    + " frame=" + frame.Value
                    + " lifeState=" + entity.CombatState.LifeState
                    + " lifecycle=Destroyed"
                    + " action=" + EmptyAsNone(entity.Action.ActiveActionId));
            }
            else if (entity.BattleState.LifeState == CharacterTestCharacterLifeState.Reviving)
            {
                entity.CompleteRevive(frame.Value);
                world?.SetGameplayLifecycle(entity, GameplayLifecycleState.Alive);
                if (world != null)
                    CharacterTestBattleStateSystem.RefreshEntity(world, entity, frame, null);
            }
        }

        private void RejectNormalActionRequests(
            CharacterTestCharacterEntity entity,
            RuntimeFrame frame)
        {
            if (entity == null)
                return;

            if (HasActionStartRequest(entity) || entity.Input.HasAbilitySlotCast)
            {
                entity.SetAction(entity.Action.WithReject(CharacterTestActionRejectCode.BattleStateBlocked));
                _traceRecorder?.AttackAction(
                    frame,
                    entity.Id.Value,
                    "control rejected action_start reason=BattleStateBlocked"
                    + " dominantReason=" + entity.BattleState.DominantReason,
                    warning: true);
            }

            entity.GameplayIntentBuffer.Clear();
            entity.ConsumeJumpBuffer();
            entity.SetInput(entity.Input.WithoutActionStartRequests().WithoutAbilitySlotCast());
        }

        private static void ApplyDeathTerminalActionHold(
            CharacterTestCharacterEntity entity,
            CharacterActionId lifecycleActionId,
            string lifecycleAnimationKey,
            int terminalFrame)
        {
            if (entity == null)
                return;

            CharacterActionId actionId = lifecycleActionId.IsValid
                ? lifecycleActionId
                : CharacterTestCharacterActionConfigSource.KatanaDeathReactionActionId;
            string animationKey = !string.IsNullOrWhiteSpace(lifecycleAnimationKey)
                ? lifecycleAnimationKey
                : CharacterTestCharacterActionConfigSource.DeathReactionAnimationActionKey;

            entity.SetAction(CharacterTestCharacterActionComponent.FromDeterministic(
                actionId,
                CompletedLifecycleActionState,
                DeathLifecyclePhase,
                Math.Max(0, terminalFrame),
                entity.Action.LastRejection,
                animationKey,
                string.Empty,
                0,
                0,
                0,
                CharacterMovementMode.Idle,
                movementLocked: true));
        }

        internal static bool IsDeathTerminalActionHold(CharacterTestCharacterEntity entity)
        {
            return entity != null
                && entity.BattleState.LifeState == CharacterTestCharacterLifeState.Dead
                && (entity.Action.State == CompletedLifecycleActionState)
                && (entity.Action.Phase == DeathLifecyclePhase)
                && entity.Action.ActiveActionId.IsValid
                && !string.IsNullOrWhiteSpace(entity.Action.LastAnimationActionKey)
                && entity.Action.MovementLocked;
        }

        private static bool IsRunningAction(CharacterTestCharacterEntity entity)
        {
            return entity != null
                && (entity.Action.State == RunningActionState)
                && entity.Action.ActiveActionId.IsValid;
        }

        private void FreezeAction(
            CharacterTestCharacterEntity entity,
            RuntimeFrame frame,
            CharacterTestActionRejectCode rejectReason)
        {
            if (rejectReason == CharacterTestActionRejectCode.None)
                rejectReason = CharacterTestActionRejectCode.ActionFrozenByStasis;

            if (HasActionStartRequest(entity))
            {
                entity.SetAction(entity.Action.WithReject(rejectReason));
                _traceRecorder?.AttackAction(
                    frame,
                    entity.Id.Value,
                    "control rejected action_start reason=" + new CharacterTestActionRejection((int)rejectReason).ToString()
                    + " active=" + EmptyAsNone(entity.Action.ActiveActionId)
                    + " localFrame=" + entity.Action.LocalFrame.ToString(CultureInfo.InvariantCulture),
                    warning: true);
            }

            entity.GameplayIntentBuffer.Clear();
            entity.ConsumeJumpBuffer();
            entity.SetInput(entity.Input.WithoutActionStartRequests());
            _runtime.FreezeEntityActionFrame(entity.Id);
        }

        private static bool HasActionStartRequest(CharacterTestCharacterEntity entity)
        {
            if (entity == null)
                return false;

            CharacterTestCharacterInputComponent input = entity.Input;
            return input.HasAbilitySlotCast
                || input.AttackPrimaryPressed
                || input.AttackSecondaryPressed
                || input.DodgePressed
                || input.SkillSlotPressedMask != 0
                || input.JumpPressed
                || entity.GameplayIntentBuffer.PendingCount > 0;
        }

        private BufferedIntentResolveStatus TryResolveBufferedIntent(
            CharacterTestCharacterEntity entity,
            RuntimeFrame frame,
            CharacterTestGameplayIntent intent)
        {
            switch (intent.Kind)
            {
                case CharacterTestGameplayIntentKind.AbilitySlotCast:
                    TraceAbilitySlotInput(
                        entity,
                        frame,
                        intent.AbilitySlotPressed,
                        intent.AbilityIdPressed);
                    return ClassifyStartResult(
                        entity,
                        TryStartAbilityById(
                            entity,
                            frame,
                            intent.AbilityIdPressed,
                            CreateActionTrace(ActionTraceKinds.AbilitySlot, entity, frame, intent.AbilityIdPressed, intent.AbilitySlotPressed)));
                case CharacterTestGameplayIntentKind.AttackSecondary:
                    if (HasCharacterActionIntent(entity, CharacterTestAuthoredContentIds.D404_Id1))
                    {
                        return ClassifyStartResult(
                            entity,
                            _runtime.TryStartIntent(
                                entity,
                                CharacterTestAuthoredContentIds.D404_Id1,
                                frame,
                                CharacterTestActionSlotIds.AttackSecondary));
                    }
                    TraceFireballInput(entity, frame);
                    return ClassifyStartResult(entity, TryStartFireball(entity, frame));
                case CharacterTestGameplayIntentKind.SkillSlot:
                    return ClassifyStartResult(
                        entity,
                        TryStartSkillSlot(entity, frame, intent.SkillSlotPressedMask));
                case CharacterTestGameplayIntentKind.AttackPrimary:
                    TraceAttackInput(entity, frame);
                    return ClassifyStartResult(entity, TryStartLightAttack(entity, frame));
                case CharacterTestGameplayIntentKind.Dodge:
                    TraceDodgeInput(entity, frame);
                    return ClassifyStartResult(entity, TryStartDodge(entity, frame));
                case CharacterTestGameplayIntentKind.Jump:
                    TraceJumpInput(entity, frame);
                    if (!CanStartJumpNow(entity))
                    {
                        _traceRecorder?.JumpAction(
                            frame,
                            entity.Id.Value,
                            new CharacterTestActionOutcomeTraceEvent(
                                CharacterTestActionOutcomeTraceKind.JumpDeferred));
                        return BufferedIntentResolveStatus.TemporaryBlocked;
                    }

                    return ClassifyStartResult(entity, TryStartJump(entity, frame));
                default:
                    return BufferedIntentResolveStatus.PermanentRejected;
            }
        }

        private static BufferedIntentResolveStatus ClassifyStartResult(
            CharacterTestCharacterEntity entity,
            bool started)
        {
            if (started)
                return BufferedIntentResolveStatus.Started;

            return IsTemporaryActionReject(entity)
                ? BufferedIntentResolveStatus.TemporaryBlocked
                : BufferedIntentResolveStatus.PermanentRejected;
        }

        private bool TryStartJump(CharacterTestCharacterEntity entity, RuntimeFrame frame)
        {
            bool started = _runtime.TryStartActionSlot(
                entity,
                CharacterTestActionSlotIds.Jump,
                CharacterTestCharacterActionConfigSource.JumpActionId,
                frame,
                CreateActionTrace(ActionTraceKinds.Jump, entity, frame));

            if (started)
            {
                _traceRecorder?.JumpAction(
                    frame,
                    entity.Id.Value,
                    new CharacterTestActionOutcomeTraceEvent(
                        CharacterTestActionOutcomeTraceKind.AcceptedNoAnimation,
                        CharacterTestCharacterActionConfigSource.JumpActionId));
            }
            else
            {
                _traceRecorder?.JumpAction(
                    frame,
                    entity.Id.Value,
                    new CharacterTestActionOutcomeTraceEvent(
                        CharacterTestActionOutcomeTraceKind.Rejected,
                        reason: FormatActionRejectReason(entity)));
            }

            return started;
        }

        private bool TryStartLightAttack(CharacterTestCharacterEntity entity, RuntimeFrame frame)
        {
            if (entity.Input.TurnInPlace)
            {
                entity.SetAction(entity.Action.WithReject(CharacterTestActionRejectCode.TurnInPlaceNoAttack));
                _traceRecorder?.AttackAction(
                    frame,
                    entity.Id.Value,
                    new CharacterTestActionOutcomeTraceEvent(
                        CharacterTestActionOutcomeTraceKind.Rejected,
                        reason: "TurnInPlaceNoAttack"));
                return false;
            }

            if (entity.Control.Owner == CharacterTestCharacterControlOwner.AiControlled
                && IsRuntimeAiPrimaryAttackSource(entity.Input.ControlIntentSourceId)
                && _primaryAttackOpportunityGuard != null
                && !_primaryAttackOpportunityGuard(entity, frame))
            {
                entity.SetAction(entity.Action.WithReject(CharacterTestActionRejectCode.AttackOpportunityBlocked));
                _traceRecorder?.AttackAction(
                    frame,
                    entity.Id.Value,
                    new CharacterTestActionOutcomeTraceEvent(
                        CharacterTestActionOutcomeTraceKind.Rejected,
                        reason: "AttackOpportunityBlocked"));
                return false;
            }

            bool started = _runtime.TryStartIntent(
                entity,
                CharacterTestCharacterActionConfigSource.LightAttackIntentId,
                frame,
                CharacterTestActionSlotIds.AttackPrimary);

            if (started)
            {
                _traceRecorder?.AttackAction(
                    frame,
                    entity.Id.Value,
                    new CharacterTestActionOutcomeTraceEvent(
                        CharacterTestActionOutcomeTraceKind.Accepted,
                        entity.Action.ActiveActionId,
                        entity.Action.AnimationActionKeyThisFrame));
            }
            else
            {
                _traceRecorder?.AttackAction(
                    frame,
                    entity.Id.Value,
                    new CharacterTestActionOutcomeTraceEvent(
                        CharacterTestActionOutcomeTraceKind.Rejected,
                        reason: FormatActionRejectReason(entity)));
            }

            return started;
        }

        private static bool IsRuntimeAiPrimaryAttackSource(int sourceId)
        {
            return sourceId == CharacterTestRuntimeCommandSources.RuntimeAiPlanner
                || sourceId == CharacterTestRuntimeCommandSources.RuntimeAiBehaviorPanel;
        }

        private bool TryStartDodge(CharacterTestCharacterEntity entity, RuntimeFrame frame)
        {
            bool usesCombatEvade = UsesCombatEvadeProfile(entity);
            CombatEvadeState state = GetCombatEvadeState(entity.Id.Value);
            bool activeStep = IsActiveEvadeKind(entity, "evade.step");
            bool activeRoll = IsActiveEvadeKind(entity, "evade.roll");
            CharacterTestCombatEvadeDirection direction = ResolveCombatEvadeDirection(entity, out bool hasDirection);
            bool hasRecovery = TryResolveActiveRecoveryRange(
                entity,
                out int recoveryStart,
                out int recoveryEnd);
            bool restartFromRecovery = usesCombatEvade
                && ShouldRestartCombatEvadeFromRecovery(
                    activeStep,
                    activeRoll,
                    entity.Action.LocalFrame,
                    hasRecovery,
                    recoveryStart,
                    recoveryEnd);
            if (restartFromRecovery
                && !HasMatchingCancelRule(
                    entity,
                    entity.Action.LocalFrame,
                    CharacterActionRequestType.Dodge,
                    CharacterCancelInputMask.Dodge))
            {
                entity.SetAction(entity.Action.WithReject(CharacterTestActionRejectCode.CombatEvadeRecoveryDodgeWindowClosed));
                return false;
            }
            bool rollRequest = usesCombatEvade && activeStep && !restartFromRecovery;
            int rollWindowStart = 0;
            int rollWindowEnd = -1;
            if (rollRequest && !TryResolveRollChainWindow(entity, out rollWindowStart, out rollWindowEnd))
            {
                entity.SetAction(entity.Action.WithReject(CharacterTestActionRejectCode.CombatEvadeRollWindowMissing));
                return false;
            }

            if (rollRequest && !hasDirection && state.FirstDirection != CharacterTestCombatEvadeDirection.None)
                direction = state.FirstDirection;

            if (usesCombatEvade && activeRoll && !restartFromRecovery)
            {
                entity.SetAction(entity.Action.WithReject(CharacterTestActionRejectCode.CombatEvadeRollAlreadyActive));
                return false;
            }

            if (rollRequest && entity.Action.LocalFrame < rollWindowStart)
            {
                if (!state.PendingRoll)
                {
                    state.PendingRoll = true;
                    state.PendingDirection = direction;
                }
                _combatEvadeStates[entity.Id.Value] = state;
                entity.SetAction(entity.Action.WithReject(CharacterTestActionRejectCode.CombatEvadeRollBuffered));
                return false;
            }

            if (rollRequest && entity.Action.LocalFrame > rollWindowEnd)
            {
                state.PendingRoll = false;
                state.PendingDirection = CharacterTestCombatEvadeDirection.None;
                _combatEvadeStates[entity.Id.Value] = state;
                entity.SetAction(entity.Action.WithReject(CharacterTestActionRejectCode.CombatEvadeRollWindowClosed));
                return false;
            }

            if (rollRequest && state.PendingRoll && state.PendingDirection != CharacterTestCombatEvadeDirection.None)
                direction = state.PendingDirection;
            if (restartFromRecovery)
            {
                state.PendingRoll = false;
                state.PendingDirection = CharacterTestCombatEvadeDirection.None;
            }

            AbilityDefinitionConfig evadeAbility = null;
            if (usesCombatEvade
                && (_abilityRuntime == null
                    || !_abilityRuntime.Definitions.TryGet(
                        rollRequest
                            ? CharacterTestCombatEvadeRuntime.RollAbilityId
                            : CharacterTestCombatEvadeRuntime.StepAbilityId,
                        out evadeAbility)))
            {
                state.PendingRoll = false;
                state.PendingDirection = CharacterTestCombatEvadeDirection.None;
                _combatEvadeStates[entity.Id.Value] = state;
                entity.SetAction(entity.Action.WithReject(CharacterTestActionRejectCode.CombatEvadeAbilityDefinitionMissing));
                return false;
            }
            if (usesCombatEvade
                && !_abilityRuntime.TryEvaluate(entity.Id.Value, evadeAbility, frame, out AbilityActivationResult evaluation))
            {
                state.PendingRoll = false;
                state.PendingDirection = CharacterTestCombatEvadeDirection.None;
                _combatEvadeStates[entity.Id.Value] = state;
                entity.SetAction(entity.Action.WithReject(CharacterTestActionRejection.FromAbility(evaluation.ReasonCode, evaluation.DetailId)));
                return false;
            }

            int intentId = usesCombatEvade
                ? ResolveEvadeIntentId(rollRequest, direction)
                : CharacterTestCharacterActionConfigSource.DodgeIntentId;
            bool started = _runtime.TryStartIntent(
                entity,
                intentId,
                frame,
                CharacterTestActionSlotIds.Dodge);

            if (started && usesCombatEvade)
            {
                if (!_abilityRuntime.TryCommitActivation(
                        entity.Id.Value,
                        evadeAbility,
                        frame,
                        out AbilityActivationFailure commitFailure))
                {
                    throw new InvalidOperationException(
                        "Combat evade cost commit failed after the CharacterAction gateway accepted the action: "
                        + commitFailure.Reason);
                }
                MarkCombatEvadeSpend(frame, ref state);
                if (rollRequest)
                {
                    state.PendingRoll = false;
                    state.PendingDirection = CharacterTestCombatEvadeDirection.None;
                }
                else
                {
                    state.FirstDirection = direction;
                }
                _combatEvadeStates[entity.Id.Value] = state;
            }

            if (started)
            {
                _traceRecorder?.AttackAction(
                    frame,
                    entity.Id.Value,
                    new CharacterTestActionOutcomeTraceEvent(
                        CharacterTestActionOutcomeTraceKind.DodgeAccepted,
                        entity.Action.ActiveActionId));
            }
            else
            {
                _traceRecorder?.AttackAction(
                    frame,
                    entity.Id.Value,
                    new CharacterTestActionOutcomeTraceEvent(
                        CharacterTestActionOutcomeTraceKind.DodgeRejected,
                        reason: entity.Action.LastRejectReason));
            }

            _runtimeAiPlannerDodgeOutcomeSink?.RecordDodgeActionOutcome(
                entity.Id.Value,
                frame,
                started);
            return started;
        }

        private bool UsesCombatEvadeProfile(CharacterTestCharacterEntity entity)
        {
            if (entity == null
                || !_runtime.TryGetEntityActionSetNoAlloc(entity.Id, out CharacterActionSetConfig actionSet))
                return false;

            bool hasDirectionalStep = false;
            bool hasDirectionalRoll = false;
            CharacterActionBinding[] bindings = actionSet.CommandBindings;
            for (int i = 0; i < bindings.Length; i++)
            {
                int intentId = bindings[i]?.IntentId ?? 0;
                hasDirectionalStep |= IsEvadeIntentId(intentId, false);
                hasDirectionalRoll |= IsEvadeIntentId(intentId, true);
            }

            return hasDirectionalStep && hasDirectionalRoll;
        }

        internal bool CanUseAuthoredForwardStep(CharacterTestCharacterEntity entity) => UsesCombatEvadeProfile(entity);
        internal void RecordAuthoredForwardStep(int entityId, RuntimeFrame frame)
        {
            var state = GetCombatEvadeState(entityId);
            state.FirstDirection = CharacterTestCombatEvadeDirection.Forward;
            state.PendingRoll = false; state.PendingDirection = CharacterTestCombatEvadeDirection.None;
            MarkCombatEvadeSpend(frame, ref state); _combatEvadeStates[entityId] = state;
        }

        private bool IsActiveEvadeKind(CharacterTestCharacterEntity entity, string tag)
        {
            if (entity == null || !entity.Action.ActiveActionId.IsValid
                || !(entity.Action.State == RunningActionState)
                || !_runtime.Registry.TryGetAction(entity.Action.ActiveActionId, out CharacterActionConfig action))
                return false;
            string[] tags = action.Tags;
            for (int i = 0; tags != null && i < tags.Length; i++)
                if (string.Equals(tags[i], tag, StringComparison.Ordinal))
                    return true;
            return false;
        }

        private bool TryResolveRollChainWindow(
            CharacterTestCharacterEntity entity,
            out int startFrame,
            out int endFrame)
        {
            startFrame = 0;
            endFrame = -1;
            if (entity == null
                || !entity.Action.ActiveActionId.IsValid
                || !_runtime.Registry.TryGetAction(entity.Action.ActiveActionId, out CharacterActionConfig action))
                return false;

            if (!TryResolveRecoveryRange(action, out int recoveryStart, out _))
                return false;

            CharacterCancelRule[] rules = action.CancelRules;
            for (int i = 0; rules != null && i < rules.Length; i++)
            {
                CharacterCancelRule rule = rules[i];
                bool allowsDodge = rule.RequestTypeMask == CharacterActionRequestType.Any
                    || (rule.RequestTypeMask & CharacterActionRequestType.Dodge) != 0;
                bool allowsDodgeSlot = rule.CancelInputMask == CharacterCancelInputMask.Any
                    || (rule.CancelInputMask & CharacterCancelInputMask.Dodge) != 0;
                if (!allowsDodge || !allowsDodgeSlot || rule.EndFrame >= recoveryStart)
                    continue;
                startFrame = rule.StartFrame;
                endFrame = rule.EndFrame;
                return true;
            }
            return false;
        }

        private bool TryResolveActiveRecoveryRange(
            CharacterTestCharacterEntity entity,
            out int startFrame,
            out int endFrame)
        {
            startFrame = 0;
            endFrame = -1;
            return entity != null
                && entity.Action.ActiveActionId.IsValid
                && _runtime.Registry.TryGetAction(entity.Action.ActiveActionId, out CharacterActionConfig action)
                && TryResolveRecoveryRange(action, out startFrame, out endFrame);
        }

        private static bool TryResolveRecoveryRange(
            CharacterActionConfig action,
            out int startFrame,
            out int endFrame)
        {
            startFrame = 0;
            endFrame = -1;
            CharacterActionPhase[] phases = action?.Phases;
            for (int i = 0; phases != null && i < phases.Length; i++)
            {
                if (phases[i].Kind != CharacterActionPhaseKind.Recovery)
                    continue;
                startFrame = phases[i].StartFrame;
                endFrame = phases[i].EndFrame;
                return true;
            }
            return false;
        }

        internal static bool ShouldRestartCombatEvadeFromRecovery(
            bool activeStep,
            bool activeRoll,
            int localFrame,
            bool hasRecovery,
            int recoveryStart,
            int recoveryEnd)
        {
            return (activeStep || activeRoll)
                && hasRecovery
                && localFrame >= recoveryStart
                && localFrame <= recoveryEnd;
        }

        private bool HasMatchingCancelRule(
            CharacterTestCharacterEntity entity,
            int localFrame,
            CharacterActionRequestType requestType,
            CharacterCancelInputMask cancelInput)
        {
            if (entity == null
                || !entity.Action.ActiveActionId.IsValid
                || !_runtime.Registry.TryGetAction(entity.Action.ActiveActionId, out CharacterActionConfig action))
                return false;
            CharacterCancelRule[] rules = action.CancelRules;
            for (int i = 0; rules != null && i < rules.Length; i++)
            {
                CharacterCancelRule rule = rules[i];
                bool allowsRequest = rule.RequestTypeMask == CharacterActionRequestType.Any
                    || (rule.RequestTypeMask & requestType) != 0;
                bool allowsSlot = rule.CancelInputMask == CharacterCancelInputMask.Any
                    || (rule.CancelInputMask & cancelInput) != 0;
                if (allowsRequest && allowsSlot
                    && localFrame >= rule.StartFrame
                    && localFrame <= rule.EndFrame)
                    return true;
            }
            return false;
        }

        private static CharacterTestCombatEvadeDirection ResolveCombatEvadeDirection(
            CharacterTestCharacterEntity entity,
            out bool hasDirection)
        {
            return ResolveCombatEvadeDirection(
                entity.Input,
                entity.Transform.YawMilliDegrees,
                out hasDirection);
        }

        internal static CharacterTestCombatEvadeDirection ResolveCombatEvadeDirection(
            CharacterTestCharacterInputComponent input,
            int yawMilliDegrees,
            out bool hasDirection)
        {
            double x = input.HasWorldMove ? input.WorldMoveXMilli : input.MoveXMilli;
            double y = input.HasWorldMove ? input.WorldMoveYMilli : input.MoveYMilli;
            if (input.HasWorldMove)
            {
                // CharacterTest local-to-world uses:
                // worldX = localX*cos(yaw) + localZ*sin(yaw)
                // worldZ = -localX*sin(yaw) + localZ*cos(yaw).
                // Its inverse therefore uses the positive logical yaw in the
                // matrix below. Negating yaw here mirrors both axes whenever
                // the character is not facing world-forward and can turn a
                // forward dodge request into a back step.
                double radians = yawMilliDegrees * Math.PI / 180000.0;
                double cosine = Math.Cos(radians);
                double sine = Math.Sin(radians);
                double localX = (x * cosine) - (y * sine);
                double localY = (x * sine) + (y * cosine);
                x = localX;
                y = localY;
            }

            hasDirection = (x * x) + (y * y) > 1000.0;
            if (!hasDirection)
                return CharacterTestCombatEvadeDirection.Back;
            double angle = Math.Atan2(x, y) * 180.0 / Math.PI;
            if (angle >= -22.5 && angle < 22.5) return CharacterTestCombatEvadeDirection.Forward;
            if (angle >= 22.5 && angle < 67.5) return CharacterTestCombatEvadeDirection.ForwardRight;
            if (angle >= 67.5 && angle < 112.5) return CharacterTestCombatEvadeDirection.Right;
            if (angle >= 112.5 && angle < 157.5) return CharacterTestCombatEvadeDirection.BackRight;
            if (angle >= 157.5 || angle < -157.5) return CharacterTestCombatEvadeDirection.Back;
            if (angle >= -157.5 && angle < -112.5) return CharacterTestCombatEvadeDirection.BackLeft;
            if (angle >= -112.5 && angle < -67.5) return CharacterTestCombatEvadeDirection.Left;
            return CharacterTestCombatEvadeDirection.ForwardLeft;
        }

        private static void MarkCombatEvadeSpend(
            RuntimeFrame frame,
            ref CombatEvadeState state)
        {
            state.LastSpendFrame = frame.Value;
            state.RegenerationRemainder = 0;
        }

        private void TickCombatEvadeRegeneration(
            CharacterTestCharacterRuntimeWorld world,
            CharacterTestCharacterEntity entity,
            RuntimeFrame frame)
        {
            if (!UsesCombatEvadeProfile(entity))
                return;
            CombatEvadeState state = GetCombatEvadeState(entity.Id.Value);
            if (state.PendingRoll
                && (!IsActiveEvadeKind(entity, "evade.step")
                    || !TryResolveRollChainWindow(entity, out _, out int rollWindowEnd)
                    || entity.Action.LocalFrame > rollWindowEnd))
            {
                state.PendingRoll = false;
                state.PendingDirection = CharacterTestCombatEvadeDirection.None;
            }
            if (state.LastSpendFrame > long.MinValue / 2
                && frame.Value - state.LastSpendFrame >= _combatEvadeTuning.RegenerationDelayFrames)
            {
                int current = world.GetCurrentAttribute(entity, CharacterTestCombatEvadeRuntime.StaminaAttributeId);
                if (current < _combatEvadeTuning.MaxStamina)
                {
                    state.RegenerationRemainder += _combatEvadeTuning.RegenerationPerSecond;
                    int restored = state.RegenerationRemainder / CharacterTestCombatEvadeRuntime.SimulationTickRate;
                    state.RegenerationRemainder %= CharacterTestCombatEvadeRuntime.SimulationTickRate;
                    if (restored > 0)
                    {
                        world.SetCurrentAttribute(
                            entity.GameplayEntityId,
                            CharacterTestCombatEvadeRuntime.StaminaAttributeId,
                            Math.Min(_combatEvadeTuning.MaxStamina, current + restored));
                    }
                }
                else
                {
                    state.RegenerationRemainder = 0;
                }
            }
            _combatEvadeStates[entity.Id.Value] = state;
        }

        private CombatEvadeState GetCombatEvadeState(int entityId)
        {
            return _combatEvadeStates.TryGetValue(entityId, out CombatEvadeState state)
                ? state
                : CombatEvadeState.Initial;
        }

        internal CharacterTestCombatEvadeStateSnapshot CaptureCombatEvadeState(int entityId)
        {
            CombatEvadeState state = GetCombatEvadeState(entityId);
            return new CharacterTestCombatEvadeStateSnapshot(
                state.FirstDirection,
                state.PendingRoll,
                state.PendingDirection,
                state.LastSpendFrame,
                state.RegenerationRemainder);
        }

        internal void RestoreCombatEvadeState(
            int entityId,
            CharacterTestCombatEvadeStateSnapshot snapshot)
        {
            if (entityId <= 0)
                return;
            _combatEvadeStates[entityId] = new CombatEvadeState
            {
                FirstDirection = snapshot.FirstDirection == CharacterTestCombatEvadeDirection.None ? CharacterTestCombatEvadeDirection.Back : snapshot.FirstDirection,
                PendingRoll = snapshot.PendingRoll,
                PendingDirection = snapshot.PendingDirection,
                LastSpendFrame = snapshot.LastSpendFrame,
                RegenerationRemainder = Math.Max(0, snapshot.RegenerationRemainder)
            };
        }

        private struct CombatEvadeState
        {
            public CharacterTestCombatEvadeDirection FirstDirection;
            public bool PendingRoll;
            public CharacterTestCombatEvadeDirection PendingDirection;
            public long LastSpendFrame;
            public int RegenerationRemainder;

            public static CombatEvadeState Initial => new CombatEvadeState
            {
                FirstDirection = CharacterTestCombatEvadeDirection.Back,
                PendingDirection = CharacterTestCombatEvadeDirection.None,
                LastSpendFrame = long.MinValue,
                RegenerationRemainder = 0
            };
        }

        private bool TryStartFireball(CharacterTestCharacterEntity entity, RuntimeFrame frame)
        {
            return TryStartAbilityAction(
                entity,
                frame,
                CharacterTestAbilityIds.FireballInputIntentId,
                CharacterTestCharacterActionConfigSource.FireballIntentId,
                CharacterTestCharacterActionConfigSource.FireballActionId,
                CreateActionTrace(ActionTraceKinds.AbilityInput, entity, frame, CharacterTestAbilityIds.FireballInputIntentId),
                CharacterTestActionSlotIds.AttackSecondary);
        }

        private bool TryStartFlameLance(CharacterTestCharacterEntity entity, RuntimeFrame frame)
        {
            return TryStartAbilityAction(
                entity,
                frame,
                CharacterTestAbilityIds.FlameLanceInputIntentId,
                CharacterTestCharacterActionConfigSource.FlameLanceIntentId,
                CharacterTestCharacterActionConfigSource.FlameLanceActionId,
                CreateActionTrace(ActionTraceKinds.AbilityInput, entity, frame, CharacterTestAbilityIds.FlameLanceInputIntentId),
                CharacterTestActionSlotIds.Skill);
        }

        private bool TryStartFireballLob(CharacterTestCharacterEntity entity, RuntimeFrame frame)
        {
            return TryStartAbilityAction(
                entity,
                frame,
                CharacterTestAbilityIds.FireballLobInputIntentId,
                CharacterTestCharacterActionConfigSource.FireballLobIntentId,
                CharacterTestCharacterActionConfigSource.FireballLobActionId,
                CreateActionTrace(ActionTraceKinds.AbilityInput, entity, frame, CharacterTestAbilityIds.FireballLobInputIntentId),
                CharacterTestActionSlotIds.Skill);
        }

        private bool TryStartFireballBurst(CharacterTestCharacterEntity entity, RuntimeFrame frame)
        {
            return TryStartAbilityAction(
                entity,
                frame,
                CharacterTestAbilityIds.FireballBurstInputIntentId,
                CharacterTestCharacterActionConfigSource.FireballBurstIntentId,
                CharacterTestCharacterActionConfigSource.FireballBurstActionId,
                CreateActionTrace(ActionTraceKinds.AbilityInput, entity, frame, CharacterTestAbilityIds.FireballBurstInputIntentId),
                CharacterTestActionSlotIds.Skill);
        }

        private bool TryStartSkillSlot(CharacterTestCharacterEntity entity, RuntimeFrame frame, int skillSlotPressedMask)
        {
            int slotIndex = CharacterTestSkillSlotRuntime.FirstPressedSlotIndex(skillSlotPressedMask);
            TraceSkillSlotInput(entity, frame, slotIndex);
            int characterIntentId = ResolveSkillIntentId(slotIndex);
            if (HasCharacterActionIntent(entity, characterIntentId))
            {
                return _runtime.TryStartIntent(
                    entity,
                    characterIntentId,
                    frame,
                    CharacterTestActionSlotIds.Skill);
            }

            if (_skillSlots == null || !CharacterTestSkillSlotRuntime.IsValidSlotIndex(slotIndex))
            {
                entity.SetAction(entity.Action.WithReject(CharacterTestActionRejectCode.SkillSlotEmpty));
                _traceRecorder?.AttackAction(
                    frame,
                    entity.Id.Value,
                    new CharacterTestActionOutcomeTraceEvent(
                        CharacterTestActionOutcomeTraceKind.SkillSlotRejected,
                        reason: CharacterTestSkillSlotRuntime.EmptyRejectReason));
                return false;
            }

            if (!_skillSlots.TryResolveSlot(slotIndex, out CharacterTestSkillSlotBinding binding)
                || binding.InputIntentId <= 0)
            {
                entity.SetAction(entity.Action.WithReject(CharacterTestActionRejectCode.SkillSlotEmpty));
                _traceRecorder?.AttackAction(
                    frame,
                    entity.Id.Value,
                    new CharacterTestActionOutcomeTraceEvent(
                        CharacterTestActionOutcomeTraceKind.SkillSlotRejected,
                        reason: CharacterTestSkillSlotRuntime.EmptyRejectReason,
                        slotIndex: slotIndex));
                return false;
            }

            return TryStartAbilityAction(
                entity,
                frame,
                binding.InputIntentId,
                0,
                CharacterActionId.None,
                CreateActionTrace(ActionTraceKinds.SkillSlot, entity, frame, binding.InputIntentId, slotIndex + 1),
                CharacterTestActionSlotIds.Skill);
        }

        private bool HasCharacterActionIntent(CharacterTestCharacterEntity entity, int intentId)
        {
            if (entity == null
                || intentId <= 0
                || !_runtime.TryGetEntityActionSet(entity.Id, out CharacterActionSetConfig actionSet)
                || actionSet == null)
            {
                return false;
            }

            CharacterActionBinding[] bindings = actionSet.CommandBindings;
            for (int i = 0; i < bindings.Length; i++)
            {
                if (bindings[i].IntentId == intentId)
                    return true;
            }

            CharacterActionCommandSequence[] sequences = actionSet.CommandSequences;
            for (int i = 0; i < sequences.Length; i++)
            {
                if (sequences[i].IntentId == intentId)
                    return true;
            }

            return false;
        }

        private bool TryStartAbilityById(
            CharacterTestCharacterEntity entity,
            RuntimeFrame frame,
            int abilityId,
            RuntimeTraceIdentity actionTrace)
        {
            if (_abilityRuntime == null)
            {
                _traceRecorder?.AttackAction(
                    frame,
                    entity.Id.Value,
                    new CharacterTestActionOutcomeTraceEvent(
                        CharacterTestActionOutcomeTraceKind.AbilityRejected,
                        reason: "MissingAbilityRuntime",
                        actionTrace: actionTrace));
                entity.SetAction(entity.Action.WithReject(CharacterTestActionRejectCode.MissingAbilityRuntime));
                return false;
            }

            if (!_abilityRuntime.Definitions.TryGet(abilityId, out AbilityDefinitionConfig ability))
            {
                _traceRecorder?.AttackAction(
                    frame,
                    entity.Id.Value,
                    new CharacterTestActionOutcomeTraceEvent(
                        CharacterTestActionOutcomeTraceKind.AbilityRejected,
                        reason: "MissingAbilityDefinition",
                        actionTrace: actionTrace,
                        abilityId: abilityId));
                entity.SetAction(entity.Action.WithReject(CharacterTestActionRejectCode.MissingAbilityDefinition));
                return false;
            }

            return TryStartAbilityAction(entity, frame, ability, actionTrace, CharacterTestActionSlotIds.Skill);
        }

        private bool TryStartAbilityAction(
            CharacterTestCharacterEntity entity,
            RuntimeFrame frame,
            int abilityInputIntentId,
            int fallbackActionIntentId,
            CharacterActionId fallbackActionId,
            RuntimeTraceIdentity actionTrace,
            int actionSlotId)
        {
            int actionIntentId = fallbackActionIntentId;
            CharacterActionId actionId = fallbackActionId;
            AbilityActivationResult abilityResult = default;
            bool hasAbilityResult = false;
            if (_abilityRuntime != null)
            {
                hasAbilityResult = _abilityRuntime.TryResolveInputIntent(
                    entity.Id.Value,
                    abilityInputIntentId,
                    frame,
                    out abilityResult);
                if (!hasAbilityResult)
                {
                    _traceRecorder?.AttackAction(
                        frame,
                        entity.Id.Value,
                        new CharacterTestActionOutcomeTraceEvent(
                            CharacterTestActionOutcomeTraceKind.AbilityRejected,
                            reason: abilityResult.Reason,
                            actionTrace: actionTrace,
                            remainingCooldownFrames: abilityResult.RemainingCooldownFrames));
                    entity.SetAction(entity.Action.WithReject(CharacterTestActionRejection.FromAbility(abilityResult.ReasonCode, abilityResult.DetailId)));
                    return false;
                }

                actionIntentId = abilityResult.ActionBinding.ActionIntentId;
                actionId = _runtime.ResolveAbilityActionId(entity.Id, abilityResult.Ability, fallbackActionId);
            }

            if (actionIntentId <= 0)
            {
                entity.SetAction(entity.Action.WithReject(CharacterTestActionRejectCode.SkillSlotAbilityMissing));
                _traceRecorder?.AttackAction(
                    frame,
                    entity.Id.Value,
                    new CharacterTestActionOutcomeTraceEvent(
                        CharacterTestActionOutcomeTraceKind.AbilityRejected,
                        reason: CharacterTestSkillSlotRuntime.MissingAbilityRejectReason,
                        actionTrace: actionTrace));
                return false;
            }

            bool started = _runtime.TryStartActionSlot(
                entity,
                actionSlotId,
                actionId,
                frame,
                actionTrace);

            if (started)
            {
                if (hasAbilityResult)
                    _abilityRuntime.CommitActivation(entity.Id.Value, abilityResult.Ability, frame);

                _traceRecorder?.AttackAction(
                    frame,
                    entity.Id.Value,
                    new CharacterTestActionOutcomeTraceEvent(
                        CharacterTestActionOutcomeTraceKind.Accepted,
                        actionId,
                        entity.Action.AnimationActionKeyThisFrame));
            }
            else
            {
                _traceRecorder?.AttackAction(
                    frame,
                    entity.Id.Value,
                    new CharacterTestActionOutcomeTraceEvent(
                        CharacterTestActionOutcomeTraceKind.Rejected,
                        reason: FormatActionRejectReason(entity),
                        actionTrace: actionTrace));
            }

            return started;
        }

        private bool TryStartAbilityAction(
            CharacterTestCharacterEntity entity,
            RuntimeFrame frame,
            AbilityDefinitionConfig ability,
            RuntimeTraceIdentity actionTrace,
            int actionSlotId)
        {
            if (ability == null)
            {
                entity.SetAction(entity.Action.WithReject(CharacterTestActionRejectCode.MissingAbilityDefinition));
                return false;
            }

            CharacterActionId actionId = _runtime.ResolveAbilityActionId(entity.Id, ability);
            AbilityActivationResult abilityResult = default;
            bool hasAbilityResult = false;
            if (_abilityRuntime != null)
            {
                hasAbilityResult = _abilityRuntime.TryEvaluate(
                    entity.Id.Value,
                    ability,
                    frame,
                    out abilityResult);
                if (!hasAbilityResult)
                {
                    _traceRecorder?.AttackAction(
                        frame,
                        entity.Id.Value,
                        new CharacterTestActionOutcomeTraceEvent(
                            CharacterTestActionOutcomeTraceKind.AbilityRejected,
                            reason: abilityResult.Reason,
                            actionTrace: actionTrace,
                            remainingCooldownFrames: abilityResult.RemainingCooldownFrames));
                    entity.SetAction(entity.Action.WithReject(CharacterTestActionRejection.FromAbility(abilityResult.ReasonCode, abilityResult.DetailId)));
                    return false;
                }

                actionId = _runtime.ResolveAbilityActionId(entity.Id, abilityResult.Ability);
            }

            bool started = _runtime.TryStartActionSlot(
                entity,
                actionSlotId,
                actionId,
                frame,
                actionTrace);

            if (started)
            {
                if (hasAbilityResult)
                    _abilityRuntime.CommitActivation(entity.Id.Value, abilityResult.Ability, frame);

                _traceRecorder?.AttackAction(
                    frame,
                    entity.Id.Value,
                    new CharacterTestActionOutcomeTraceEvent(
                        CharacterTestActionOutcomeTraceKind.Accepted,
                        actionId,
                        entity.Action.AnimationActionKeyThisFrame));
            }
            else
            {
                _traceRecorder?.AttackAction(
                    frame,
                    entity.Id.Value,
                    new CharacterTestActionOutcomeTraceEvent(
                        CharacterTestActionOutcomeTraceKind.Rejected,
                        reason: FormatActionRejectReason(entity),
                        actionTrace: actionTrace));
            }

            return started;
        }

        private void TraceAttackInput(CharacterTestCharacterEntity entity, RuntimeFrame frame)
        {
            _traceRecorder?.AttackInput(
                frame,
                entity.Id.Value,
                new CharacterTestActionInputTraceEvent(
                    CharacterTestActionInputTraceKind.Attack,
                    entity.Action.ActiveActionId,
                    entity.Action.State));
        }

        private void TraceFireballInput(CharacterTestCharacterEntity entity, RuntimeFrame frame)
        {
            _traceRecorder?.AttackInput(
                frame,
                entity.Id.Value,
                new CharacterTestActionInputTraceEvent(
                    CharacterTestActionInputTraceKind.Fireball,
                    entity.Action.ActiveActionId,
                    entity.Action.State));
        }

        private void TraceFlameLanceInput(CharacterTestCharacterEntity entity, RuntimeFrame frame)
        {
            _traceRecorder?.AttackInput(
                frame,
                entity.Id.Value,
                new CharacterTestActionInputTraceEvent(
                    CharacterTestActionInputTraceKind.FlameLance,
                    entity.Action.ActiveActionId,
                    entity.Action.State));
        }

        private void TraceFireballLobInput(CharacterTestCharacterEntity entity, RuntimeFrame frame)
        {
            _traceRecorder?.AttackInput(
                frame,
                entity.Id.Value,
                new CharacterTestActionInputTraceEvent(
                    CharacterTestActionInputTraceKind.FireballLob,
                    entity.Action.ActiveActionId,
                    entity.Action.State));
        }

        private void TraceFireballBurstInput(CharacterTestCharacterEntity entity, RuntimeFrame frame)
        {
            _traceRecorder?.AttackInput(
                frame,
                entity.Id.Value,
                new CharacterTestActionInputTraceEvent(
                    CharacterTestActionInputTraceKind.FireballBurst,
                    entity.Action.ActiveActionId,
                    entity.Action.State));
        }

        private void TraceSkillSlotInput(CharacterTestCharacterEntity entity, RuntimeFrame frame, int slotIndex)
        {
            _traceRecorder?.AttackInput(
                frame,
                entity.Id.Value,
                new CharacterTestActionInputTraceEvent(
                    CharacterTestActionInputTraceKind.SkillSlot,
                    entity.Action.ActiveActionId,
                    entity.Action.State,
                    CharacterTestSkillSlotRuntime.IsValidSlotIndex(slotIndex) ? slotIndex : -1));
        }

        private void TraceAbilitySlotInput(
            CharacterTestCharacterEntity entity,
            RuntimeFrame frame,
            int abilitySlotPressed,
            int abilityIdPressed)
        {
            _traceRecorder?.AttackInput(
                frame,
                entity.Id.Value,
                new CharacterTestActionInputTraceEvent(
                    CharacterTestActionInputTraceKind.AbilitySlot,
                    entity.Action.ActiveActionId,
                    entity.Action.State,
                    abilitySlot: abilitySlotPressed,
                    abilityId: abilityIdPressed));
        }

        private void TraceJumpInput(CharacterTestCharacterEntity entity, RuntimeFrame frame)
        {
            _traceRecorder?.JumpInput(
                frame,
                entity.Id.Value,
                new CharacterTestActionInputTraceEvent(
                    CharacterTestActionInputTraceKind.Jump,
                    grounded: entity.IsGrounded,
                    coyoteFrames: entity.CoyoteTimeFramesRemaining,
                    bufferFrames: entity.GameplayIntentBuffer.GetRemainingFrames(
                        CharacterTestGameplayIntentKind.Jump,
                        frame.Value)));
        }

        private void TraceDodgeInput(CharacterTestCharacterEntity entity, RuntimeFrame frame)
        {
            _traceRecorder?.AttackInput(
                frame,
                entity.Id.Value,
                new CharacterTestActionInputTraceEvent(
                    CharacterTestActionInputTraceKind.Dodge,
                    entity.Action.ActiveActionId,
                    entity.Action.State));
        }

        private static bool CanStartJumpNow(CharacterTestCharacterEntity entity)
        {
            return entity != null && (entity.IsGrounded || entity.CoyoteTimeFramesRemaining > 0);
        }

        internal static bool IsTemporaryActionReject(CharacterTestCharacterEntity entity)
        {
            if (entity == null)
                return false;

            int reason = entity.Action.LastRejectReasonCode;
            return reason == (int)CharacterActionDiagnosticCode.ActionLowerPriorityRejected
                || reason == (int)CharacterActionDiagnosticCode.CharacterCancelRejected
                || reason == (int)CharacterActionDiagnosticCode.ActionSlotBlocked
                || reason == (int)CharacterTestActionRejectCode.CombatEvadeRollBuffered;
        }

        private static string EmptyAsNone(string value)
        {
            return string.IsNullOrEmpty(value) ? "none" : value;
        }

        private static string EmptyAsNone(CharacterActionId value)
        {
            return value.IsValid ? value.ToString() : "none";
        }

        private static RuntimeTraceIdentity CreateActionTrace(
            int kind, CharacterTestCharacterEntity entity, RuntimeFrame frame, int inputId = 0, int slot = 0)
        {
            return new RuntimeTraceIdentity(kind, entity.Id.Value, frame.Value,
                ((long)(uint)slot << 32) | (uint)inputId);
        }

        private static int ResolveSkillIntentId(int slotIndex)
        {
            switch (slotIndex)
            {
                case 0: return CharacterTestAuthoredContentIds.D404_Id61;
                case 1: return CharacterTestAuthoredContentIds.D404_Id62;
                case 2: return CharacterTestAuthoredContentIds.D404_Id63;
                case 3: return CharacterTestAuthoredContentIds.D404_Id64;
                case 4: return CharacterTestAuthoredContentIds.D404_Id65;
                case 5: return CharacterTestAuthoredContentIds.D404_Id66;
                case 6: return CharacterTestAuthoredContentIds.D404_Id67;
                case 7: return CharacterTestAuthoredContentIds.D404_Id68;
                case 8: return CharacterTestAuthoredContentIds.D404_Id69;
                default: return 0;
            }
        }

        private static int ResolveEvadeIntentId(bool roll, CharacterTestCombatEvadeDirection direction)
        {
            switch (direction)
            {
                case CharacterTestCombatEvadeDirection.Forward: return roll ? CharacterTestAuthoredContentIds.D404_Id78 : CharacterTestAuthoredContentIds.D404_Id70;
                case CharacterTestCombatEvadeDirection.ForwardRight: return roll ? CharacterTestAuthoredContentIds.D404_Id79 : CharacterTestAuthoredContentIds.D404_Id71;
                case CharacterTestCombatEvadeDirection.Right: return roll ? CharacterTestAuthoredContentIds.D404_Id80 : CharacterTestAuthoredContentIds.D404_Id72;
                case CharacterTestCombatEvadeDirection.BackRight: return roll ? CharacterTestAuthoredContentIds.D404_Id81 : CharacterTestAuthoredContentIds.D404_Id73;
                case CharacterTestCombatEvadeDirection.Back: return roll ? CharacterTestAuthoredContentIds.D404_Id82 : CharacterTestAuthoredContentIds.D404_Id74;
                case CharacterTestCombatEvadeDirection.BackLeft: return roll ? CharacterTestAuthoredContentIds.D404_Id83 : CharacterTestAuthoredContentIds.D404_Id75;
                case CharacterTestCombatEvadeDirection.Left: return roll ? CharacterTestAuthoredContentIds.D404_Id84 : CharacterTestAuthoredContentIds.D404_Id76;
                case CharacterTestCombatEvadeDirection.ForwardLeft: return roll ? CharacterTestAuthoredContentIds.D404_Id85 : CharacterTestAuthoredContentIds.D404_Id77;
                default: throw new ArgumentOutOfRangeException(nameof(direction));
            }
        }

        private static bool IsEvadeIntentId(int intentId, bool roll)
        {
            switch (intentId)
            {
                case CharacterTestAuthoredContentIds.D404_Id70: return !roll;
                case CharacterTestAuthoredContentIds.D404_Id71: return !roll;
                case CharacterTestAuthoredContentIds.D404_Id72: return !roll;
                case CharacterTestAuthoredContentIds.D404_Id73: return !roll;
                case CharacterTestAuthoredContentIds.D404_Id74: return !roll;
                case CharacterTestAuthoredContentIds.D404_Id75: return !roll;
                case CharacterTestAuthoredContentIds.D404_Id76: return !roll;
                case CharacterTestAuthoredContentIds.D404_Id77: return !roll;
                case CharacterTestAuthoredContentIds.D404_Id78: return roll;
                case CharacterTestAuthoredContentIds.D404_Id79: return roll;
                case CharacterTestAuthoredContentIds.D404_Id80: return roll;
                case CharacterTestAuthoredContentIds.D404_Id81: return roll;
                case CharacterTestAuthoredContentIds.D404_Id82: return roll;
                case CharacterTestAuthoredContentIds.D404_Id83: return roll;
                case CharacterTestAuthoredContentIds.D404_Id84: return roll;
                case CharacterTestAuthoredContentIds.D404_Id85: return roll;
                default: return false;
            }
        }

        public void CompleteGroundedJumpActions(
            CharacterTestCharacterRuntimeWorld world,
            RuntimeFrame frame,
            ICharacterTestStatusControlProvider statusControl = null)
        {
            IReadOnlyList<CharacterTestCharacterEntity> entities = world != null
                ? world.Entities
                : null;
            for (int i = 0; entities != null && i < entities.Count; i++)
            {
                CharacterTestCharacterEntity entity = entities[i];
                if (statusControl != null && statusControl.IsActionFrozen(entity))
                    continue;

                if (!entity.IsGrounded)
                    continue;

                if (entity.Action.ActiveActionId != CharacterTestCharacterActionConfigSource.JumpActionId)
                {
                    continue;
                }

                if (!(entity.Action.State == CharacterTestActionStateId.Running)
                    || entity.Action.LocalFrame <= 0)
                {
                    continue;
                }

                _runtime.CompleteExternal(
                    entity.Id,
                    CharacterTestCharacterActionConfigSource.JumpActionId,
                    CharacterActionExternalCompletionReason.Landed,
                    frame,
                    CreateActionTrace(ActionTraceKinds.JumpLanded, entity, frame));
            }
        }

        public void ProcessLandingActions(
            CharacterTestCharacterMotionSystem motionSystem,
            RuntimeFrame frame,
            ICharacterTestStatusControlProvider statusControl = null)
        {
            if (motionSystem == null
                || _runtime == null
                || !_runtime.IsReady)
            {
                return;
            }

            for (int i = 0; i < motionSystem.LandingEventCount; i++)
            {
                CharacterTestLandingEvent landing = motionSystem.GetLandingEvent(i);
                CharacterTestCharacterEntity entity = landing.Entity;
                if (entity == null
                    || (statusControl != null && statusControl.IsActionFrozen(entity)))
                {
                    continue;
                }

                if (!_runtime.TryGetEntityActionSet(entity.Id, out CharacterActionSetConfig actionSet)
                    || string.IsNullOrEmpty(actionSet.LandingProfileId)
                    || !_runtime.Registry.TryGetLandingProfile(
                        actionSet.LandingProfileId,
                        out CharacterLandingProfile profile))
                {
                    continue;
                }

                if (!profile.MeetsLightLandingThreshold(
                        landing.DownwardSpeedMillimetersPerSecond))
                {
                    TraceLanding(
                        frame,
                        entity,
                        landing,
                        false,
                        false,
                        false,
                        CharacterActionId.None,
                        false,
                        belowMinimumThreshold: true);
                    continue;
                }

                bool preferredHeavy = profile.IsHeavyLanding(
                    landing.DownwardSpeedMillimetersPerSecond);
                if (!profile.TryResolveAction(
                        preferredHeavy,
                        out CharacterActionId actionId,
                        out bool resolvedHeavy,
                        out bool usedFallback))
                {
                    TraceLanding(frame, entity, landing, preferredHeavy, false, false, CharacterActionId.None, false);
                    continue;
                }

                int actionSlot = resolvedHeavy
                    ? CharacterTestActionSlotIds.HeavyLanding
                    : CharacterTestActionSlotIds.LightLanding;
                bool started = _runtime.TryStartActionSlot(
                    entity,
                    actionSlot,
                    actionId,
                    frame,
                    CreateActionTrace(ActionTraceKinds.Landing, entity, frame, actionId.Value, actionSlot),
                    CharacterActionSourceKind.Landing);
                TraceLanding(
                    frame,
                    entity,
                    landing,
                    preferredHeavy,
                    resolvedHeavy,
                    usedFallback,
                    actionId,
                    started);
            }
        }

        private void TraceLanding(
            RuntimeFrame frame,
            CharacterTestCharacterEntity entity,
            CharacterTestLandingEvent landing,
            bool preferredHeavy,
            bool resolvedHeavy,
            bool usedFallback,
            CharacterActionId actionId,
            bool started,
            bool belowMinimumThreshold = false)
        {
            if (_traceRecorder == null)
                return;

            _traceRecorder.Locomotion(
                frame,
                entity.Id.Value,
                "landing speedMmPerSec="
                + landing.DownwardSpeedMillimetersPerSecond.ToString(CultureInfo.InvariantCulture)
                + " preferred=" + (preferredHeavy ? "Heavy" : "Light")
                + " resolved=" + (!actionId.IsValid ? "None" : resolvedHeavy ? "Heavy" : "Light")
                + " fallback=" + (usedFallback ? "true" : "false")
                + " action=" + (actionId.IsValid ? actionId.ToString() : "none")
                + " result=" + (belowMinimumThreshold
                    ? "BelowMinimumThreshold"
                    : started
                        ? "Started"
                        : !actionId.IsValid
                            ? "Unconfigured"
                            : "Rejected"),
                warning: false);
        }

        private void LogValidationOnce()
        {
            if (_loggedValidation)
                return;

            _loggedValidation = true;
            if (_runtime.IsReady)
                return;

            _logger.Warning(
                "CharacterTest.Actions",
                "Character action config is not ready. importSuccess="
                + _runtime.ImportResult.Success
                + " validationSuccess="
                + _runtime.ValidationReport.Success
                + " source="
                + _runtime.ConfigSourceKind
                + " description="
                + _runtime.ConfigSourceDescription
                + " firstDiagnostic="
                + FormatFirstDiagnostic(_runtime));
        }

        private string FormatActionRejectReason(CharacterTestCharacterEntity entity)
        {
            string reason = entity != null ? entity.Action.LastRejectReason : string.Empty;
            if (_runtime == null || _runtime.IsReady)
                return EmptyAsNone(reason);

            return EmptyAsNone(reason) + " diagnostic=" + FormatFirstDiagnostic(_runtime);
        }

        private static string FormatFirstDiagnostic(CharacterTestCharacterActionRuntime runtime)
        {
            if (runtime == null)
                return "runtime missing";

            string diagnostic;
            if (TryFormatFirstSourceError(runtime, out diagnostic))
                return diagnostic;
            if (TryFormatFirstImportError(runtime, out diagnostic))
                return diagnostic;
            if (TryFormatFirstValidationError(runtime, out diagnostic))
                return diagnostic;

            if (runtime.ConfigSourceDiagnostics.Count > 0)
                return runtime.ConfigSourceDiagnostics[0].ToString();

            if (runtime.ImportResult.Diagnostics.Length > 0)
                return runtime.ImportResult.Diagnostics[0].ToString();

            if (runtime.ValidationReport.Diagnostics.Length > 0)
                return runtime.ValidationReport.Diagnostics[0].ToString();

            return "runtime is not ready";
        }

        private static bool TryFormatFirstSourceError(
            CharacterTestCharacterActionRuntime runtime,
            out string diagnostic)
        {
            for (int i = 0; i < runtime.ConfigSourceDiagnostics.Count; i++)
            {
                CharacterTestCharacterActionConfigSourceDiagnostic candidate =
                    runtime.ConfigSourceDiagnostics[i];
                if (candidate.Severity == CharacterActionDiagnosticSeverity.Error)
                {
                    diagnostic = candidate.ToString();
                    return true;
                }
            }

            diagnostic = string.Empty;
            return false;
        }

        private static bool TryFormatFirstImportError(
            CharacterTestCharacterActionRuntime runtime,
            out string diagnostic)
        {
            for (int i = 0; i < runtime.ImportResult.Diagnostics.Length; i++)
            {
                CharacterActionJsonDiagnostic candidate = runtime.ImportResult.Diagnostics[i];
                if (candidate.Severity == CharacterActionDiagnosticSeverity.Error)
                {
                    diagnostic = candidate.ToString();
                    return true;
                }
            }

            diagnostic = string.Empty;
            return false;
        }

        private static bool TryFormatFirstValidationError(
            CharacterTestCharacterActionRuntime runtime,
            out string diagnostic)
        {
            for (int i = 0; i < runtime.ValidationReport.Diagnostics.Length; i++)
            {
                CharacterActionValidationDiagnostic candidate = runtime.ValidationReport.Diagnostics[i];
                if (candidate.Severity == CharacterActionDiagnosticSeverity.Error)
                {
                    diagnostic = candidate.ToString();
                    return true;
                }
            }

            diagnostic = string.Empty;
            return false;
        }

    }
}
