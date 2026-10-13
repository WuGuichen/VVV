using System;
using System.Collections.Generic;
using MxFramework.AI;
using MxFramework.CharacterAction;
using MxFramework.Combat.Core;
using MxFramework.Combat.Physics;
using MxFramework.Core.Math;
using MxFramework.Runtime;
using UnityEngine;

namespace MxFramework.Game.CharacterTest
{
    internal sealed partial class CharacterTestRuntimeAiPlannerEvaluator
    {
        private const string SuppressedNone = "None";
        private const string SuppressedTargetMissing = "TargetMissing";
        private const string SuppressedNoExecutableAction = "NoExecutableAction";
        private const string CommandNone = "none";
        private const string CommandSetControlIntent = "SetControlIntent";
        private const int AgentInitialCapacity = 16;
        private const float DirectionEpsilonSquared = 0.000001f;

        private readonly CharacterTestRuntimeAiPlannerCompiledProfile _profile;
        private readonly ICharacterTestAiNavigationService _navigationService;
        private readonly ICharacterTestRuntimeAiPlannerPerceptionSource _perceptionSource;
        private readonly CharacterTestPerceptionFrameCache _perceptionFrameCache;
        private readonly CharacterTestCharacterActionRuntime _actionRuntime;
        private readonly CharacterActionConfigRegistry _actionRegistry;
        private readonly Func<CharacterTestCharacterEntity, int, RuntimeFrame,
            CharacterTestAttackOpportunityResult>
            _primaryAttackEligibility;
        private readonly CharacterTestRuntimeAiPlannerAttackOpportunityAdapter
            _attackOpportunityAdapter;
        private readonly List<CharacterTestRuntimeAiPlannerAgentState> _agentStates =
            new List<CharacterTestRuntimeAiPlannerAgentState>(AgentInitialCapacity);

        public CharacterTestRuntimeAiPlannerEvaluator(
            CharacterTestRuntimeAiPlannerCompiledProfile profile,
            ICharacterTestAiNavigationService navigationService,
            ICharacterTestRuntimeAiPlannerPerceptionSource perceptionSource = null,
            CharacterTestPerceptionFrameCache perceptionFrameCache = null,
            CharacterTestCharacterActionRuntime actionRuntime = null,
            Func<CharacterTestCharacterEntity, int, RuntimeFrame,
                CharacterTestAttackOpportunityResult> primaryAttackEligibility = null,
            CharacterTestRuntimeAiPlannerAttackOpportunityAdapter attackOpportunityAdapter = null)
        {
            _profile = profile ?? throw new ArgumentNullException(nameof(profile));
            _navigationService = navigationService ?? CharacterTestDirectAiNavigationService.Instance;
            _perceptionSource = perceptionSource ?? CharacterTestRuntimeAiPlannerWorldPerceptionSource.Empty;
            _perceptionFrameCache = perceptionFrameCache;
            _actionRuntime = actionRuntime;
            _actionRegistry = actionRuntime?.Registry;
            _primaryAttackEligibility = primaryAttackEligibility;
            _attackOpportunityAdapter = attackOpportunityAdapter;
            InitializeDecisionTraceCapture();
        }

        public CharacterTestRuntimeAiPlannerCompiledProfile Profile => _profile;
        // Diagnostic counters only; they are deliberately excluded from authority snapshots and hashes.
        internal long DecisionEvaluationCount { get; private set; }
        internal long FactRefreshCount { get; private set; }

        internal bool TryRefreshFactsWithoutSelection(CharacterTestCharacterEntity entity,
            IReadOnlyList<CharacterTestCharacterEntity> entities, RuntimeFrame frame,
            out CharacterTestRuntimeAiPlannerDecision observation, CharacterTestRuntimeAiPerceptionRequirements requirements = CharacterTestRuntimeAiPerceptionRequirements.All)
        {
            observation = default;
            if (!TryRefreshFacts(entity, entities, frame, default, out var state, requirements)) return false;
            observation = CreateNoActionDecision(state, false, 0, 0, "BehaviorCardExecuting");
            state.LastDecision = observation;
            return true;
        }

        private bool TryRefreshFacts(CharacterTestCharacterEntity entity, IReadOnlyList<CharacterTestCharacterEntity> entities,
            RuntimeFrame frame, CharacterTestRuntimeAiJointDirective jointDirective, out CharacterTestRuntimeAiPlannerAgentState state, CharacterTestRuntimeAiPerceptionRequirements requirements = CharacterTestRuntimeAiPerceptionRequirements.All)
        {
            state = null;
            if (entity == null || entity.Control.Owner != CharacterTestCharacterControlOwner.AiControlled) return false;
            state = GetOrCreateAgentState(entity.Id.Value);
            int frameValue = ClampFrame(frame.Value);
            BeginDecisionTraceCapture(state.Id, frameValue, state.LastDecision);
            state.ExpirePendingDodgeAttempt(frameValue);
            Sense(entity, entities, state, frame, jointDirective, requirements);
            state.Facts.CaptureSelfHealth(entity, frameValue);
            CaptureDecisionTraceFacts(state.Id, state.Facts, frameValue);
            FactRefreshCount++;
            return true;
        }

        /// <remarks>NoAlloc on the steady-state path once agents are registered and the agent-state list capacity is stable.</remarks>
        public bool TryEvaluate(
            CharacterTestCharacterEntity entity,
            IReadOnlyList<CharacterTestCharacterEntity> entities,
            RuntimeFrame frame,
            out CharacterTestRuntimeAiPlannerDecision decision)
        {
            return TryEvaluate(
                entity,
                entities,
                frame,
                default,
                out decision);
        }

        internal bool TryEvaluate(
            CharacterTestCharacterEntity entity,
            IReadOnlyList<CharacterTestCharacterEntity> entities,
            RuntimeFrame frame,
            CharacterTestRuntimeAiJointDirective jointDirective,
            out CharacterTestRuntimeAiPlannerDecision decision)
        {
            decision = default;
            if (!TryRefreshFacts(entity, entities, frame, jointDirective, out var state)) return false;
            DecisionEvaluationCount++;
            int previousStateId = state.StateMachine.CurrentStateId;
            int frameValue = ClampFrame(frame.Value);

            if (!state.Facts.TargetExists)
            {
                // A completed joint pass with no valid leaf is an authoritative
                // fail-closed decision. Submit the zero intent even on the first
                // tick so legacy input cannot survive outside planner state.
                bool clearStaleInput = state.HadSubmittedControlIntent
                    || jointDirective.Evaluated;
                state.ResetMovement();
                decision = CreateNoTargetDecision(state, clearStaleInput);
                state.LastDecision = decision;
                RecordDecisionTraceFallback(state.Id, decision, frameValue, targetMissing: true);
                if (!clearStaleInput)
                    state.LastCommand = CommandNone;
                return true;
            }

            state.StateMachine.Tick(state, state.Facts, frameValue);
            int currentStateId = state.StateMachine.CurrentStateId;
            int transitionFrom = (previousStateId == currentStateId)
                ? 0
                : previousStateId;
            int transitionTo = (previousStateId == currentStateId)
                ? 0
                : currentStateId;

            if (!TrySelectAction(
                    entity,
                    state,
                    frame,
                    jointDirective,
                    out CharacterTestRuntimeAiPlannerCompiledAction action))
            {
                bool clearStaleInput = state.HadSubmittedControlIntent;
                state.ResetMovement();
                decision = CreateNoActionDecision(state, clearStaleInput, transitionFrom, transitionTo);
                state.LastDecision = decision;
                RecordDecisionTraceFallback(state.Id, decision, frameValue, targetMissing: false);
                if (!clearStaleInput)
                    state.LastCommand = CommandNone;
                return true;
            }

            decision = CreateActionDecision(state, action, transitionFrom, transitionTo, frameValue);
            state.LastDecision = decision;
            CommitDecisionTraceSelection(state.Id, decision, frameValue);
            return true;
        }

        public void RecordCommandResult(
            int entityId,
            RuntimeCommandValidationResult result)
        {
            CharacterTestRuntimeAiPlannerAgentState state = FindAgentState(entityId);
            if (state == null)
                return;

            RecordDecisionTraceCommandResult(entityId, result);

            if (result.Success)
            {
                state.HadSubmittedControlIntent =
                    state.LastDecision.TargetExists && state.LastDecision.SelectedActionId > 0;
                state.LastCommand = CommandSetControlIntent;
                return;
            }

            if (state.LastDecision.DodgePressed)
                state.RejectDodgeAttempt();
            state.LastCommand = CommandSetControlIntent;
            state.LastDecision = new CharacterTestRuntimeAiPlannerDecision(
                state.LastDecision.EntityId,
                state.LastDecision.TargetEntityId,
                state.LastDecision.ActiveStateId,
                state.LastDecision.ActiveStatePath,
                state.LastDecision.SelectedActionId,
                state.LastDecision.SelectedActionName,
                state.LastDecision.SelectedActionKind,
                state.LastDecision.SelectedUtilityScore,
                state.LastDecision.SelectedActionCost,
                state.LastDecision.Move,
                state.LastDecision.AttackPrimaryPressed,
                state.LastDecision.SkillSlotPressedMask,
                state.LastDecision.ShouldSubmitControlIntent,
                state.LastDecision.SuppressedReason,
                result.Error.Message,
                state.LastDecision.TransitionFromStateId,
                state.LastDecision.TransitionToStateId,
                state.LastDecision.TargetExists,
                state.LastDecision.TargetInAttackRange,
                state.LastDecision.TargetDistance,
                state.LastDecision.MovePathAvailable,
                state.LastDecision.TargetVisible,
                state.LastDecision.TargetLineOfSightBlocked,
                state.LastDecision.TargetLastSeenAgeFrames,
                state.LastDecision.TargetThreatScore,
                state.LastDecision.SelfSurfaceKind,
                state.LastDecision.SelfOnHardSlick,
                state.LastDecision.SelfNearMovableObject,
                state.LastDecision.MoveDirectPathClear,
                state.LastDecision.MoveCandidateIndex,
                state.LastDecision.EnvironmentNearObstacle,
                state.LastDecision.EnvironmentNearHazard,
                state.LastDecision.EnvironmentNearUsefulObject,
                state.LastDecision.MovementSpeedScale,
                state.LastDecision.MovementStrafeSide,
                state.LastDecision.MovementDirectionHoldFrames,
                state.LastDecision.MovementReactionDelayFrames,
                state.LastDecision.HasScenePreviewGeometry,
                state.LastDecision.ScenePreviewSourceCenterX,
                state.LastDecision.ScenePreviewSourceCenterY,
                state.LastDecision.ScenePreviewSourceCenterZ,
                state.LastDecision.ScenePreviewTargetCenterX,
                state.LastDecision.ScenePreviewTargetCenterY,
                state.LastDecision.ScenePreviewTargetCenterZ,
                state.LastDecision.ScenePreviewTargetDirectionX,
                state.LastDecision.ScenePreviewTargetDirectionZ,
                state.LastDecision.ScenePreviewTargetBearingMilliDegrees,
                state.LastDecision.ScenePreviewFacingDeltaMilliDegrees,
                state.LastDecision.ScenePreviewFacingDot,
                state.LastDecision.ScenePreviewAttackRange,
                targetDead: state.LastDecision.TargetDead,
                targetLifeState: state.LastDecision.TargetLifeState);
        }

        internal bool TryGetFactState(
            int entityId,
            out CharacterTestRuntimeAiPlannerFactState facts)
        {
            CharacterTestRuntimeAiPlannerAgentState state = FindAgentState(entityId);
            facts = state?.Facts;
            return facts != null;
        }

        public void RecordDodgeActionOutcome(int entityId, RuntimeFrame frame, bool accepted)
        {
            CharacterTestRuntimeAiPlannerAgentState state = FindAgentState(entityId);
            if (state == null)
                return;

            if (accepted)
                state.AcceptDodgeAttempt(ClampFrame(frame.Value));
            else
                state.RejectDodgeAttempt();
        }

        internal int SnapshotAgentCount => _agentStates.Count;

        internal void CopySnapshotAgentsTo(CharacterTestRuntimeAiPlannerAgentSnapshot[] agents, int offset)
        {
            for (int i = 0; i < _agentStates.Count; i++)
            {
                CharacterTestRuntimeAiPlannerAgentState state = _agentStates[i];
                CharacterTestRuntimeAiPlannerDecision decision = state.LastDecision;
                int targetLogicalHurtboxId = 0;
                if (_attackOpportunityAdapter != null
                    && _attackOpportunityAdapter.TryGetDamageZoneLock(
                        state.Id,
                        out CharacterTestDamageZoneLock targetLock))
                {
                    targetLogicalHurtboxId = targetLock.LogicalHurtboxId;
                }
                CharacterTestRuntimeAiPlannerAgentSnapshot agent =
                    new CharacterTestRuntimeAiPlannerAgentSnapshot(
                    state.Id,
                    decision.TargetEntityId,
                    decision.ActiveStatePath,
                    decision.TargetExists,
                    decision.TargetInAttackRange,
                    decision.TargetDistance,
                    decision.MovePathAvailable,
                    decision.SelectedUtilityScore,
                    decision.SelectedActionCost,
                    decision.SelectedActionId,
                    decision.SelectedActionName,
                    decision.SelectedActionKind,
                    state.LastCommand,
                    decision.SuppressedReason,
                    decision.RejectReason,
                    decision.TransitionFromStateId,
                    decision.TransitionToStateId,
                    decision.TargetVisible,
                    decision.TargetLineOfSightBlocked,
                    decision.TargetLastSeenAgeFrames,
                    decision.TargetThreatScore,
                    decision.SelfSurfaceKind,
                    decision.SelfOnHardSlick,
                    decision.SelfNearMovableObject,
                    decision.MoveDirectPathClear,
                    decision.MoveCandidateIndex,
                    decision.EnvironmentNearObstacle,
                    decision.EnvironmentNearHazard,
                    decision.EnvironmentNearUsefulObject,
                    decision.MovementSpeedScale,
                    decision.MovementStrafeSide,
                    decision.MovementDirectionHoldFrames,
                    decision.MovementReactionDelayFrames,
                    decision.HasScenePreviewGeometry,
                    decision.ScenePreviewSourceCenterX,
                    decision.ScenePreviewSourceCenterY,
                    decision.ScenePreviewSourceCenterZ,
                    decision.ScenePreviewTargetCenterX,
                    decision.ScenePreviewTargetCenterY,
                    decision.ScenePreviewTargetCenterZ,
                    decision.ScenePreviewTargetDirectionX,
                    decision.ScenePreviewTargetDirectionZ,
                    decision.ScenePreviewTargetBearingMilliDegrees,
                    decision.ScenePreviewFacingDeltaMilliDegrees,
                    decision.ScenePreviewFacingDot,
                    decision.ScenePreviewDecisionMoveX,
                    decision.ScenePreviewDecisionMoveZ,
                    decision.ScenePreviewAttackRange,
                    selectedAbilitySlot: ResolveSelectedAbilitySlot(decision.SkillSlotPressedMask),
                    targetAttackActionId: state.Facts.TargetAttackActionId,
                    targetAttackSequence: state.Facts.TargetAttackSequence,
                    targetAttackImpactFrames: state.Facts.TargetAttackImpactFrames,
                    targetAttackWillHitSelf: state.Facts.TargetAttackWillHitSelf,
                    targetAttackInstanceResponded: state.Facts.TargetAttackInstanceResponded,
                    selfDodgeCooldownReady: state.Facts.SelfDodgeCooldownReady,
                    nextDodgeAllowedFrame: state.NextDodgeAllowedFrame,
                    targetDead: decision.TargetDead,
                    targetLifeState: decision.TargetLifeState,
                    targetDistanceAvailable: state.Facts.TargetDistanceAvailable,
                    targetRootDistance: state.Facts.TargetRootDistance,
                    hasScenePreviewDistanceWitness: state.HasScenePreviewDistanceWitness,
                    scenePreviewDistanceSourceX: state.ScenePreviewDistanceSourceX,
                    scenePreviewDistanceSourceY: state.ScenePreviewDistanceSourceY,
                    scenePreviewDistanceSourceZ: state.ScenePreviewDistanceSourceZ,
                    scenePreviewDistanceTargetX: state.ScenePreviewDistanceTargetX,
                    scenePreviewDistanceTargetY: state.ScenePreviewDistanceTargetY,
                    scenePreviewDistanceTargetZ: state.ScenePreviewDistanceTargetZ,
                    scenePreviewDistanceGeometryFrame: state.ScenePreviewDistanceGeometryFrame,
                    targetLogicalHurtboxId: targetLogicalHurtboxId,
                    selfActionInterruptible: state.Facts.SelfActionInterruptible,
                    activeStateId: decision.ActiveStateId,
                    activeStatePathDigest: _profile.GetStatePathDigest(decision.ActiveStateId));
                if (state.HasNavigationSample)
                    agent = agent.WithNavigationDiagnostics(state.NavigationSample);
                agents[offset + i] = agent;
            }

        }

        public CharacterTestRuntimeAiPlannerSnapshot CreateSnapshot(string validationReport)
        {
            var agents = new CharacterTestRuntimeAiPlannerAgentSnapshot[_agentStates.Count];
            CopySnapshotAgentsTo(agents, 0);
            return new CharacterTestRuntimeAiPlannerSnapshot(true, _profile.Source.ProfileId, agents, validationReport);
        }

        internal void CopyStateSnapshotTo(CharacterTestRuntimeAiPlannerSlotStateSnapshot destination)
        {
            var agents = destination.Agents;
            if (agents.Length != _agentStates.Count) throw new InvalidOperationException("Planner capture layout changed; prepare capture storage again.");
            for (int i = 0; i < _agentStates.Count; i++)
            {
                CharacterTestRuntimeAiPlannerAgentState state = _agentStates[i];
                var target = agents[i];
                target.EntityId = state.Id;
                target.CurrentStateId = state.StateMachine.CurrentStateId;
                target.HadSubmittedControlIntent = state.HadSubmittedControlIntent;
                target.LastSeenTargetEntityId = state.LastSeenTargetEntityId;
                target.TargetLastSeenAgeFrames = state.TargetLastSeenAgeFrames;
                target.ObservedTargetAttackEntityId = state.ObservedTargetAttackEntityId;
                target.ObservedTargetAttackActionId = state.ObservedTargetAttackActionId;
                target.ObservedTargetAttackLocalFrame = state.ObservedTargetAttackLocalFrame;
                target.TargetAttackSequence = state.TargetAttackSequence;
                target.LastRespondedTargetAttackSequence = state.LastRespondedTargetAttackSequence;
                target.NextDodgeAllowedFrame = state.NextDodgeAllowedFrame;
                target.PendingDodgeTargetAttackSequence = state.PendingDodgeTargetAttackSequence;
                target.PendingDodgeCooldownFrames = state.PendingDodgeCooldownFrames;
                target.PendingDodgeRequestFrame = state.PendingDodgeRequestFrame;
                target.LastMovementActionId = state.LastMovementActionId;
                target.LastDesiredMoveX = state.LastDesiredMove.x;
                target.LastDesiredMoveY = state.LastDesiredMove.y;
                target.LastOutputMoveX = state.LastOutputMove.x;
                target.LastOutputMoveY = state.LastOutputMove.y;
                target.StrafeSide = state.StrafeSide;
                target.NextStrafeSwitchFrame = state.NextStrafeSwitchFrame;
                target.DirectionHoldFramesRemaining = state.DirectionHoldFramesRemaining;
                target.ReactionDelayFramesRemaining = state.ReactionDelayFramesRemaining;
                target.LastMovementSpeedScale = state.LastMovementSpeedScale;
            }
        }

        internal CharacterTestRuntimeAiPlannerSlotStateSnapshot CreateStateSnapshot(int slot)
        {
            var agents = new CharacterTestRuntimeAiPlannerAgentStateSnapshot[_agentStates.Count];
            for (int i = 0; i < agents.Length; i++) agents[i] = new CharacterTestRuntimeAiPlannerAgentStateSnapshot();
            var result = new CharacterTestRuntimeAiPlannerSlotStateSnapshot { Slot = Math.Max(0, slot), Agents = agents };
            CopyStateSnapshotTo(result);
            return result;
        }

        internal bool TryApplyStateSnapshot(
            CharacterTestRuntimeAiPlannerSlotStateSnapshot snapshot,
            out string failure)
        {
            failure = string.Empty;
            if (!ValidateStateSnapshot(snapshot, out failure)) return false;
            if (snapshot == null || snapshot.Agents == null || snapshot.Agents.Length == 0)
            {
                _agentStates.Clear();
                return true;
            }
            var restoredStates = new List<CharacterTestRuntimeAiPlannerAgentState>(snapshot.Agents.Length);

            for (int i = 0; i < snapshot.Agents.Length; i++)
            {
                CharacterTestRuntimeAiPlannerAgentStateSnapshot agentSnapshot = snapshot.Agents[i];
                if (agentSnapshot == null)
                    continue;
                if (agentSnapshot.EntityId <= 0)
                {
                    failure = "Runtime AI Planner state snapshot contains an invalid entity id.";
                    return false;
                }

                CharacterTestRuntimeAiPlannerAgentState state =
                    new CharacterTestRuntimeAiPlannerAgentState(agentSnapshot.EntityId, _profile);
                int targetStateId = agentSnapshot.CurrentStateId;
                try
                {
                    if (!(
                            state.StateMachine.CurrentStateId == targetStateId))
                    {
                        state.StateMachine.TransitionTo(targetStateId, state, state.Facts, frame: 0);
                    }
                }
                catch (Exception exception)
                {
                    failure = "Runtime AI Planner state restore failed for entity "
                        + agentSnapshot.EntityId
                        + ": "
                        + exception.Message;
                    return false;
                }

                state.ApplyStateSnapshot(agentSnapshot);
                state.LastDecision = CharacterTestRuntimeAiPlannerDecision.Empty(
                    state.Id,
                    state.StateMachine.CurrentStateId,
                    _profile.GetStatePathText(state.StateMachine.CurrentStateId),
                    SuppressedTargetMissing);
                restoredStates.Add(state);
            }
            _agentStates.Clear();
            _agentStates.AddRange(restoredStates);
            return true;
        }

        internal bool ValidateStateSnapshot(CharacterTestRuntimeAiPlannerSlotStateSnapshot snapshot, out string failure)
        {
            failure = string.Empty;
            if (snapshot?.Agents == null) return true;
            var seen = new HashSet<int>();
            for (int i = 0; i < snapshot.Agents.Length; i++)
            {
                var agent = snapshot.Agents[i];
                if (agent == null || agent.EntityId <= 0 || !seen.Add(agent.EntityId) || !_profile.ContainsState(agent.CurrentStateId))
                {
                    failure = "Runtime AI Planner snapshot contains a missing, duplicate, or unknown numeric entity/state identity.";
                    return false;
                }
            }
            return true;
        }

        private static int ResolveSelectedAbilitySlot(int skillSlotPressedMask)
        {
            int mask = skillSlotPressedMask & CharacterTestControlIntent.SkillSlotMaskBits;
            if (mask == 0)
                return 0;

            for (int i = 0; i < CharacterTestControlIntent.SkillSlotCount; i++)
            {
                if ((mask & (1 << i)) != 0)
                    return i + 1;
            }

            return 0;
        }

        private CharacterTestRuntimeAiPlannerDecision CreateNoTargetDecision(
            CharacterTestRuntimeAiPlannerAgentState state,
            bool clearStaleInput)
        {
            return new CharacterTestRuntimeAiPlannerDecision(
                state.Id,
                targetEntityId: 0,
                state.StateMachine.CurrentStateId,
                _profile.GetStatePathText(state.StateMachine.CurrentStateId),
                selectedActionId: 0,
                selectedActionName: string.Empty,
                CharacterTestRuntimeAiPlannerActionKind.Wait,
                selectedUtilityScore: 0f,
                selectedActionCost: 0f,
                Vector2.zero,
                attackPrimaryPressed: false,
                skillSlotPressedMask: 0,
                shouldSubmitControlIntent: clearStaleInput,
                SuppressedTargetMissing,
                rejectReason: string.Empty,
                transitionFromStateId: 0,
                transitionToStateId: 0,
                targetExists: false,
                targetInAttackRange: false,
                targetDistance: 0f,
                movePathAvailable: false);
        }

        private CharacterTestRuntimeAiPlannerDecision CreateNoActionDecision(
            CharacterTestRuntimeAiPlannerAgentState state,
            bool clearStaleInput,
            int transitionFrom,
            int transitionTo,
            string suppressedReason = SuppressedNoExecutableAction)
        {
            return new CharacterTestRuntimeAiPlannerDecision(
                state.Id,
                state.Facts.TargetEntityId,
                state.StateMachine.CurrentStateId,
                _profile.GetStatePathText(state.StateMachine.CurrentStateId),
                selectedActionId: 0,
                selectedActionName: string.Empty,
                CharacterTestRuntimeAiPlannerActionKind.Wait,
                selectedUtilityScore: 0f,
                selectedActionCost: 0f,
                Vector2.zero,
                attackPrimaryPressed: false,
                skillSlotPressedMask: 0,
                shouldSubmitControlIntent: clearStaleInput,
                suppressedReason,
                rejectReason: string.Empty,
                transitionFrom,
                transitionTo,
                state.Facts.TargetExists,
                state.Facts.TargetInAttackRange,
                state.Facts.TargetDistance,
                state.Facts.MovePathAvailable,
                state.Facts.TargetVisible,
                state.Facts.TargetLineOfSightBlocked,
                state.Facts.TargetLastSeenAgeFrames,
                state.Facts.TargetThreatScore,
                state.Facts.SelfSurfaceKind,
                state.Facts.SelfOnHardSlick,
                state.Facts.SelfNearMovableObject,
                state.Facts.DirectPathClear,
                state.Facts.NavigationCandidateIndex,
                state.Facts.EnvironmentNearObstacle,
                state.Facts.EnvironmentNearHazard,
                state.Facts.EnvironmentNearUsefulObject,
                hasScenePreviewGeometry: state.HasScenePreviewGeometry,
                scenePreviewSourceCenterX: state.ScenePreviewSourceCenterX,
                scenePreviewSourceCenterY: state.ScenePreviewSourceCenterY,
                scenePreviewSourceCenterZ: state.ScenePreviewSourceCenterZ,
                scenePreviewTargetCenterX: state.ScenePreviewTargetCenterX,
                scenePreviewTargetCenterY: state.ScenePreviewTargetCenterY,
                scenePreviewTargetCenterZ: state.ScenePreviewTargetCenterZ,
                scenePreviewTargetDirectionX: state.ScenePreviewTargetDirectionX,
                scenePreviewTargetDirectionZ: state.ScenePreviewTargetDirectionZ,
                scenePreviewTargetBearingMilliDegrees: state.ScenePreviewTargetBearingMilliDegrees,
                scenePreviewFacingDeltaMilliDegrees: state.ScenePreviewFacingDeltaMilliDegrees,
                scenePreviewFacingDot: state.ScenePreviewFacingDot,
                scenePreviewAttackRange: state.ScenePreviewAttackRange,
                targetDead: state.Facts.TargetDead,
                targetLifeState: state.Facts.TargetLifeState);
        }

        private CharacterTestRuntimeAiPlannerDecision CreateActionDecision(
            CharacterTestRuntimeAiPlannerAgentState state,
            CharacterTestRuntimeAiPlannerCompiledAction action,
            int transitionFrom,
            int transitionTo,
            int frameValue)
        {
            Vector2 move = ResolveActionMove(state, action, frameValue);
            if (action.Kind == CharacterTestRuntimeAiPlannerActionKind.Dodge)
                state.BeginDodgeAttempt(frameValue, action.ReactionCooldownFrames);
            bool attackPrimary = action.Kind == CharacterTestRuntimeAiPlannerActionKind.PrimaryAttack
                && !state.Facts.SelfActionRunning;
            int skillSlotMask = action.Kind == CharacterTestRuntimeAiPlannerActionKind.CastAbilitySlot
                ? CharacterTestSkillSlotRuntime.ToMask(action.AbilitySlot - 1)
                : 0;

            return new CharacterTestRuntimeAiPlannerDecision(
                state.Id,
                state.Facts.TargetEntityId,
                state.StateMachine.CurrentStateId,
                _profile.GetStatePathText(state.StateMachine.CurrentStateId),
                action.ActionId,
                action.DisplayName,
                action.Kind,
                action.Score,
                action.Cost,
                move,
                attackPrimary,
                skillSlotMask,
                shouldSubmitControlIntent: true,
                SuppressedNone,
                rejectReason: string.Empty,
                transitionFrom,
                transitionTo,
                state.Facts.TargetExists,
                state.Facts.TargetInAttackRange,
                state.Facts.TargetDistance,
                state.Facts.MovePathAvailable,
                state.Facts.TargetVisible,
                state.Facts.TargetLineOfSightBlocked,
                state.Facts.TargetLastSeenAgeFrames,
                state.Facts.TargetThreatScore,
                state.Facts.SelfSurfaceKind,
                state.Facts.SelfOnHardSlick,
                state.Facts.SelfNearMovableObject,
                state.Facts.DirectPathClear,
                state.Facts.NavigationCandidateIndex,
                state.Facts.EnvironmentNearObstacle,
                state.Facts.EnvironmentNearHazard,
                state.Facts.EnvironmentNearUsefulObject,
                state.LastMovementSpeedScale,
                state.StrafeSide,
                state.DirectionHoldFramesRemaining,
                state.ReactionDelayFramesRemaining,
                state.HasScenePreviewGeometry,
                state.ScenePreviewSourceCenterX,
                state.ScenePreviewSourceCenterY,
                state.ScenePreviewSourceCenterZ,
                state.ScenePreviewTargetCenterX,
                state.ScenePreviewTargetCenterY,
                state.ScenePreviewTargetCenterZ,
                state.ScenePreviewTargetDirectionX,
                state.ScenePreviewTargetDirectionZ,
                state.ScenePreviewTargetBearingMilliDegrees,
                state.ScenePreviewFacingDeltaMilliDegrees,
                state.ScenePreviewFacingDot,
                state.ScenePreviewAttackRange,
                targetDead: state.Facts.TargetDead,
                targetLifeState: state.Facts.TargetLifeState);
        }

        private void Sense(
            CharacterTestCharacterEntity entity,
            IReadOnlyList<CharacterTestCharacterEntity> entities,
            CharacterTestRuntimeAiPlannerAgentState state,
            RuntimeFrame frame,
            CharacterTestRuntimeAiJointDirective jointDirective, CharacterTestRuntimeAiPerceptionRequirements requirements)
        {
            CharacterTestRuntimeAiPlannerFactState facts = state.Facts;
            facts.SetPerceptionRequirements(requirements);
            bool requirePrimaryAttackFact = (requirements & CharacterTestRuntimeAiPerceptionRequirements.PrimaryAttack) != 0;
            bool requireNavigation = (requirements & CharacterTestRuntimeAiPerceptionRequirements.Navigation) != 0;
            bool requireEnvironment = (requirements & CharacterTestRuntimeAiPerceptionRequirements.Environment) != 0;
            CharacterTestRuntimeAiPlannerPerceptionSettings perceptionSettings = _profile.PerceptionSettings;
            if (requireEnvironment)
            {
                if (_perceptionFrameCache != null
                    && _perceptionFrameCache.TryGetEnvironment(
                        frame,
                        entity,
                        perceptionSettings,
                        out CharacterTestEnvironmentSample cachedEnvironment))
                {
                    WriteEnvironmentSample(state.EnvironmentSample, cachedEnvironment);
                }
                else
                {
                    _perceptionSource.SampleEnvironment(entity, perceptionSettings, state.EnvironmentSample);
                }
            }

            bool actionRunning = IsActionRunning(entity);
            bool actionInterruptible = !actionRunning
                || (_actionRuntime != null
                    && _actionRuntime.IsCurrentActionInterruptible(entity.Id));
            CharacterTestCharacterEntity target = null;
            CharacterTestTargetGeometrySample targetGeometry = default;
            bool hasTargetGeometry = false;
            bool hasTarget = jointDirective.Evaluated
                ? jointDirective.HasSelection
                    && TryFindEntity(entities, jointDirective.TargetEntityId, out target)
                    && TrySampleTargetGeometry(entity, target, frame, out targetGeometry, out hasTargetGeometry)
                : TrySelectTarget(
                    entity,
                    entities,
                    _profile.TargetSelectionMode,
                    frame,
                    out target,
                    out targetGeometry,
                    out hasTargetGeometry);
            if (!hasTarget)
            {
                facts.SetNoTarget(actionRunning);
                facts.SetSelfActionState(actionRunning, actionInterruptible);
                state.ClearObservedTargetAttack();
                state.ClearNavigationSample();
                ApplyEnvironmentSample(facts, state.EnvironmentSample);
                ClearScenePreviewGeometry(state);
                return;
            }

            if (hasTargetGeometry)
                WriteScenePreviewGeometry(state, targetGeometry, _profile.AttackRange);
            else
                ClearScenePreviewGeometry(state);

            CharacterTestAiNavigationResult navigation = default;
            if (requireNavigation)
            {
                if (!_navigationService.TryResolveMove(
                        entity,
                        target,
                        _profile.AttackRange,
                        out navigation))
                {
                    CharacterTestDirectAiNavigationService.Instance.TryResolveMove(
                        entity,
                        target,
                        _profile.AttackRange,
                        out navigation);
                }
            }

            if (requireNavigation && _perceptionFrameCache != null
                && _perceptionFrameCache.TryGetCachedNavigation(
                    entity.Id.Value,
                    target.Id.Value,
                    out CharacterTestNavigationSample navigationSample))
            {
                state.SetNavigationSample(navigationSample);
            }
            else
            {
                state.ClearNavigationSample();
            }

            float targetRootDistance;
            if (hasTargetGeometry)
            {
                targetRootDistance = targetGeometry.DistanceMeters;
            }
            else
            {
                Vector2 toTarget = CharacterTestDirectAiNavigationService.ToTarget(entity, target);
                targetRootDistance = toTarget.magnitude;
            }

            bool targetDistanceAvailable = false;
            float targetDistance = 0f;
            if (_attackOpportunityAdapter != null
                && _attackOpportunityAdapter.TryResolveCombatTarget(
                    entity,
                    target,
                    frame,
                    out _,
                    out CharacterTestLockedHurtboxDistanceSample lockedDistance)
                && lockedDistance.HasDistance)
            {
                targetDistanceAvailable = true;
                targetDistance = lockedDistance.SurfaceGapMeters;
                WriteScenePreviewDistanceWitness(state, lockedDistance);
                // Hurtbox distance witnesses do not replace validated navigation or its stop result.
                if (requirePrimaryAttackFact && _primaryAttackEligibility != null && !actionRunning)
                {
                    bool canHit;
                    if (!_attackOpportunityAdapter.TryGetPreparedPrimaryAttackFact(entity, target, frame, jointDirective, out canHit))
                    {
                        CharacterTestAttackOpportunityResult rangeOpportunity = _primaryAttackEligibility(entity, target.Id.Value, frame);
                        canHit = rangeOpportunity.Status == CharacterTestAttackOpportunityStatus.Valid
                            && rangeOpportunity.Level == CharacterTestAttackOpportunityLevel.CurrentPose
                            && rangeOpportunity.CanHitFromCurrentPose;
                    }
                    navigation = new CharacterTestAiNavigationResult(
                        navigation.Move,
                        navigation.DirectPathClear,
                        canHit,
                        navigation.SelectedCandidateIndex);
                }
                else
                {
                    navigation = new CharacterTestAiNavigationResult(
                        navigation.Move,
                        navigation.DirectPathClear,
                        false,
                        navigation.SelectedCandidateIndex);
                }
            }
            else if (_attackOpportunityAdapter == null)
            {
                // Standalone compatibility evaluators have no combat-target authority.
                targetDistanceAvailable = true;
                targetDistance = targetRootDistance;
                ClearScenePreviewDistanceWitness(state);
            }
            else
            {
                ClearScenePreviewDistanceWitness(state);
            }

            bool lineOfSightBlocked = !navigation.DirectPathClear;
            bool targetVisible;
            if (_perceptionFrameCache != null
                && hasTargetGeometry
                && _perceptionFrameCache.TryGetVisibility(
                    frame,
                    entity,
                    target,
                    perceptionSettings,
                    targetGeometry,
                    out CharacterTestVisibilitySample visibility))
            {
                lineOfSightBlocked = visibility.LineOfSightBlocked;
                targetVisible = visibility.TargetVisible;
            }
            else
            {
                if (_perceptionSource.TryResolveLineOfSightBlocked(
                    entity,
                    target,
                    perceptionSettings,
                    out bool sampledLineOfSightBlocked))
                {
                    lineOfSightBlocked = sampledLineOfSightBlocked;
                }

                targetVisible = targetRootDistance <= perceptionSettings.VisionRange && !lineOfSightBlocked;
            }

            int lastSeenAgeFrames = UpdateLastSeenState(state, target.Id.Value, targetVisible);
            float threatScore = ResolveThreatScore(
                perceptionSettings,
                targetRootDistance,
                targetVisible,
                lastSeenAgeFrames);
            facts.SetTarget(
                target.Id.Value,
                targetDistance,
                targetDistanceAvailable,
                targetRootDistance,
                navigation,
                actionRunning,
                targetVisible,
                lineOfSightBlocked,
                lastSeenAgeFrames,
                threatScore,
                targetDead: !target.CombatState.IsAlive,
                targetLifeState: ToPlannerTargetLifeState(target.CombatState.LifeState));
            facts.SetPrimaryAttackFactObserved(requirePrimaryAttackFact || actionRunning || !targetDistanceAvailable);
            facts.SetSelfActionState(actionRunning, actionInterruptible);
            SampleTargetAttackThreat(
                entity,
                target,
                state,
                targetRootDistance,
                ClampFrame(frame.Value));
            ApplyEnvironmentSample(facts, state.EnvironmentSample);
        }

        private void SampleTargetAttackThreat(
            CharacterTestCharacterEntity observer,
            CharacterTestCharacterEntity target,
            CharacterTestRuntimeAiPlannerAgentState state,
            float targetDistance,
            int frameValue)
        {
            bool cooldownReady = frameValue >= state.NextDodgeAllowedFrame;
            CharacterTestCharacterActionComponent targetAction = target.Action;
            if (!(targetAction.State == CharacterTestActionStateId.Running)
                || !targetAction.ActiveActionId.IsValid
                || _actionRegistry == null
                || !_actionRegistry.TryGetAction(targetAction.ActiveActionId, out CharacterActionConfig action)
                || !TryResolveNextHitTraceStart(action, targetAction.LocalFrame, out int impactFrames))
            {
                state.ClearObservedTargetAttack();
                state.Facts.SetTargetAttackThreat(
                    CharacterActionId.None,
                    0,
                    -1,
                    false,
                    false,
                    cooldownReady);
                return;
            }

            int attackSequence = state.ObserveTargetAttack(
                target.Id.Value,
                targetAction.ActiveActionId,
                targetAction.LocalFrame);
            bool responded = attackSequence > 0
                && (attackSequence == state.LastRespondedTargetAttackSequence
                    || attackSequence == state.PendingDodgeTargetAttackSequence);
            bool willHitSelf = IsPredictedAttackHit(
                observer,
                target,
                action,
                targetDistance);
            state.Facts.SetTargetAttackThreat(
                targetAction.ActiveActionId,
                attackSequence,
                impactFrames,
                willHitSelf,
                responded,
                cooldownReady);
        }

        private static bool TryResolveNextHitTraceStart(
            CharacterActionConfig action,
            int localFrame,
            out int impactFrames)
        {
            impactFrames = -1;
            CombatTrackEvent[] events = action?.CombatTrack?.Events;
            for (int i = 0; events != null && i < events.Length; i++)
            {
                CombatTrackEvent trackEvent = events[i];
                if (trackEvent.Kind != CharacterActionTrackEventKind.HitTraceWindow
                    || trackEvent.Frame < localFrame)
                {
                    continue;
                }

                int candidate = trackEvent.Frame - localFrame;
                if (impactFrames < 0 || candidate < impactFrames)
                    impactFrames = candidate;
            }

            return impactFrames >= 0;
        }

        private bool IsPredictedAttackHit(
            CharacterTestCharacterEntity observer,
            CharacterTestCharacterEntity target,
            CharacterActionConfig action,
            float targetDistance)
        {
            if (observer == null || target == null || action == null)
                return false;
            if (action.Category != CharacterActionCategory.BasicAttack
                && action.Category != CharacterActionCategory.Skill)
            {
                return false;
            }

            float reach = Mathf.Max(1.5f, _profile.AttackRange)
                + observer.Physics.EffectiveRadiusMeters
                + target.Physics.EffectiveRadiusMeters
                + 0.35f;
            if (targetDistance > reach)
                return false;

            float toObserverX = observer.Transform.X - target.Transform.X;
            float toObserverZ = observer.Transform.Z - target.Transform.Z;
            float planarLength = Mathf.Sqrt(toObserverX * toObserverX + toObserverZ * toObserverZ);
            if (planarLength <= 0.0001f)
                return true;

            float yawRadians = target.Transform.YawDegrees * Mathf.Deg2Rad;
            float facingX = Mathf.Sin(yawRadians);
            float facingZ = Mathf.Cos(yawRadians);
            float facingDot = (facingX * toObserverX + facingZ * toObserverZ) / planarLength;
            return facingDot >= 0.2f;
        }

        private static void ApplyEnvironmentSample(
            CharacterTestRuntimeAiPlannerFactState facts,
            CharacterTestRuntimeAiPlannerEnvironmentSample sample)
        {
            facts.SetEnvironment(
                sample.SurfaceKind,
                sample.OnHardSlick,
                sample.NearMovableObject,
                sample.NearObstacle,
                sample.NearHazard,
                sample.NearUsefulObject);
        }

        private static void WriteScenePreviewGeometry(
            CharacterTestRuntimeAiPlannerAgentState state,
            CharacterTestTargetGeometrySample sample,
            float attackRange)
        {
            if (state == null)
                return;

            state.HasScenePreviewGeometry = true;
            state.ScenePreviewSourceCenterX = ToFloat(sample.SourceCenter.X);
            state.ScenePreviewSourceCenterY = ToFloat(sample.SourceCenter.Y);
            state.ScenePreviewSourceCenterZ = ToFloat(sample.SourceCenter.Z);
            state.ScenePreviewTargetCenterX = ToFloat(sample.TargetCenter.X);
            state.ScenePreviewTargetCenterY = ToFloat(sample.TargetCenter.Y);
            state.ScenePreviewTargetCenterZ = ToFloat(sample.TargetCenter.Z);
            state.ScenePreviewTargetDirectionX = sample.DirectionX;
            state.ScenePreviewTargetDirectionZ = sample.DirectionZ;
            state.ScenePreviewTargetBearingMilliDegrees = sample.TargetBearingMilliDegrees;
            state.ScenePreviewFacingDeltaMilliDegrees = sample.FacingDeltaMilliDegrees;
            state.ScenePreviewFacingDot = sample.FacingDot;
            state.ScenePreviewAttackRange = attackRange > 0f ? attackRange : 0f;
        }

        private static void WriteScenePreviewDistanceWitness(
            CharacterTestRuntimeAiPlannerAgentState state,
            in CharacterTestLockedHurtboxDistanceSample sample)
        {
            if (state == null || !sample.HasDistance)
            {
                ClearScenePreviewDistanceWitness(state);
                return;
            }

            state.HasScenePreviewDistanceWitness = true;
            state.ScenePreviewDistanceSourceX = ToFloat(sample.SourceWitnessPoint.X);
            state.ScenePreviewDistanceSourceY = ToFloat(sample.SourceWitnessPoint.Y);
            state.ScenePreviewDistanceSourceZ = ToFloat(sample.SourceWitnessPoint.Z);
            state.ScenePreviewDistanceTargetX = ToFloat(sample.TargetWitnessPoint.X);
            state.ScenePreviewDistanceTargetY = ToFloat(sample.TargetWitnessPoint.Y);
            state.ScenePreviewDistanceTargetZ = ToFloat(sample.TargetWitnessPoint.Z);
            state.ScenePreviewDistanceGeometryFrame = sample.GeometryFrame.Value;
        }

        private static void ClearScenePreviewDistanceWitness(
            CharacterTestRuntimeAiPlannerAgentState state)
        {
            if (state == null)
                return;
            state.HasScenePreviewDistanceWitness = false;
            state.ScenePreviewDistanceSourceX = 0f;
            state.ScenePreviewDistanceSourceY = 0f;
            state.ScenePreviewDistanceSourceZ = 0f;
            state.ScenePreviewDistanceTargetX = 0f;
            state.ScenePreviewDistanceTargetY = 0f;
            state.ScenePreviewDistanceTargetZ = 0f;
            state.ScenePreviewDistanceGeometryFrame = 0L;
        }

        private static void ClearScenePreviewGeometry(CharacterTestRuntimeAiPlannerAgentState state)
        {
            if (state == null)
                return;

            state.HasScenePreviewGeometry = false;
            state.ScenePreviewSourceCenterX = 0f;
            state.ScenePreviewSourceCenterY = 0f;
            state.ScenePreviewSourceCenterZ = 0f;
            state.ScenePreviewTargetCenterX = 0f;
            state.ScenePreviewTargetCenterY = 0f;
            state.ScenePreviewTargetCenterZ = 0f;
            state.ScenePreviewTargetDirectionX = 0f;
            state.ScenePreviewTargetDirectionZ = 0f;
            state.ScenePreviewTargetBearingMilliDegrees = 0;
            state.ScenePreviewFacingDeltaMilliDegrees = 0;
            state.ScenePreviewFacingDot = 0f;
            state.ScenePreviewAttackRange = 0f;
            ClearScenePreviewDistanceWitness(state);
        }

        private static void WriteEnvironmentSample(
            CharacterTestRuntimeAiPlannerEnvironmentSample target,
            CharacterTestEnvironmentSample source)
        {
            if (target == null)
                return;

            target.SurfaceKind = (int)source.SurfaceKind;
            target.OnHardSlick = source.OnHardSlick;
            target.NearMovableObject = source.NearMovableObject;
            target.NearObstacle = source.NearObstacle;
            target.NearHazard = source.NearHazard;
            target.NearUsefulObject = source.NearUsefulObject;
        }

        private static float ToFloat(Fix64 value)
        {
            return (float)(value.RawValue / (double)Fix64.Scale);
        }

        private static int UpdateLastSeenState(
            CharacterTestRuntimeAiPlannerAgentState state,
            int targetEntityId,
            bool targetVisible)
        {
            if (targetVisible)
            {
                state.LastSeenTargetEntityId = targetEntityId;
                state.TargetLastSeenAgeFrames = 0;
                return 0;
            }

            if (state.LastSeenTargetEntityId != targetEntityId)
            {
                state.LastSeenTargetEntityId = targetEntityId;
                state.TargetLastSeenAgeFrames = 1;
                return state.TargetLastSeenAgeFrames;
            }

            if (state.TargetLastSeenAgeFrames < int.MaxValue)
                state.TargetLastSeenAgeFrames++;
            return state.TargetLastSeenAgeFrames;
        }

        private static float ResolveThreatScore(
            CharacterTestRuntimeAiPlannerPerceptionSettings settings,
            float targetDistance,
            bool targetVisible,
            int lastSeenAgeFrames)
        {
            CharacterTestRuntimeAiPlannerPerceptionSettings safeSettings =
                CharacterTestRuntimeAiPlannerPerceptionSettings.Normalize(settings);
            float distanceScore = Mathf.Max(0f, safeSettings.VisionRange - Mathf.Max(0f, targetDistance));
            if (distanceScore <= 0f || safeSettings.ThreatDistanceWeight <= 0f)
                return 0f;

            if (targetVisible)
                return distanceScore * safeSettings.ThreatDistanceWeight;

            if (lastSeenAgeFrames <= safeSettings.TargetMemoryFrames)
                return distanceScore * safeSettings.ThreatDistanceWeight * 0.5f;

            return 0f;
        }

        private bool TrySelectAction(
            CharacterTestCharacterEntity entity,
            CharacterTestRuntimeAiPlannerAgentState state,
            RuntimeFrame frame,
            CharacterTestRuntimeAiJointDirective jointDirective,
            out CharacterTestRuntimeAiPlannerCompiledAction selected)
        {
            selected = default;
            bool found = false;
            IReadOnlyList<CharacterTestRuntimeAiPlannerCompiledAction> actions = _profile.Actions;
            for (int i = 0; i < actions.Count; i++)
            {
                CharacterTestRuntimeAiPlannerCompiledAction action = actions[i];
                bool inActivePath = _profile.IsActionInActivePath(action, state.StateMachine.ActivePath);
                if (jointDirective.Evaluated)
                {
                    inActivePath = jointDirective.HasSelection
                        && action.ActionId == jointDirective.CommandPlannerActionId;
                }
                else if (_primaryAttackEligibility != null
                    && !state.Facts.SelfActionRunning
                    && (action.Kind == CharacterTestRuntimeAiPlannerActionKind.PrimaryAttack
                        || action.Kind == CharacterTestRuntimeAiPlannerActionKind.MoveToTarget))
                {
                    CharacterTestAttackOpportunityResult opportunity =
                        _primaryAttackEligibility(entity, state.Facts.TargetEntityId, frame);
                    if (action.Kind == CharacterTestRuntimeAiPlannerActionKind.PrimaryAttack
                        && IsPrimaryAttackSelectionOpportunity(opportunity))
                    {
                        // The legacy HFSM may still be in Chase because its
                        // center-distance fact is false. M1 is allowed to
                        // qualify the attack candidate without mutating that
                        // compatibility fact.
                        inActivePath = true;
                    }
                    else if (action.Kind == CharacterTestRuntimeAiPlannerActionKind.MoveToTarget
                        && IsTranslationSelectionOpportunity(opportunity))
                    {
                        // Conversely, a target can be center-in-range while
                        // the M1 geometry requires translation. Keep the
                        // approach action eligible in the Attack state.
                        inActivePath = true;
                    }
                }
                bool executable = inActivePath
                    && EvaluateActionConditionsForTrace(
                        entity,
                        frame,
                        state.Id,
                        action,
                        state.Facts,
                        jointDirective);
                RecordDecisionTraceActionCandidate(state.Id, action, i, inActivePath, executable);
                if (!executable)
                    continue;

                if (!found || IsBetter(action, selected))
                {
                    selected = action;
                    found = true;
                }
            }

            return found;
        }

        private bool TrySampleTargetGeometry(
            CharacterTestCharacterEntity source,
            CharacterTestCharacterEntity target,
            RuntimeFrame frame,
            out CharacterTestTargetGeometrySample geometry,
            out bool hasGeometry)
        {
            geometry = default;
            hasGeometry = false;
            if (source == null || target == null)
                return false;
            if (_perceptionFrameCache != null
                && _perceptionFrameCache.TryGetTargetGeometry(
                    frame,
                    source,
                    target,
                    out geometry))
            {
                hasGeometry = true;
            }
            return true;
        }

        private static bool TryFindEntity(
            IReadOnlyList<CharacterTestCharacterEntity> entities,
            int entityId,
            out CharacterTestCharacterEntity entity)
        {
            for (int i = 0; entities != null && i < entities.Count; i++)
            {
                CharacterTestCharacterEntity candidate = entities[i];
                if (candidate != null && candidate.Id.Value == entityId)
                {
                    entity = candidate;
                    return true;
                }
            }
            entity = null;
            return false;
        }

        private static bool IsPrimaryAttackSelectionOpportunity(
            CharacterTestAttackOpportunityResult opportunity)
        {
            return opportunity.Status == CharacterTestAttackOpportunityStatus.Valid
                && (opportunity.Level == CharacterTestAttackOpportunityLevel.CurrentPose
                    || opportunity.Level == CharacterTestAttackOpportunityLevel.TurnInPlace);
        }

        private static bool IsTranslationSelectionOpportunity(
            CharacterTestAttackOpportunityResult opportunity)
        {
            return opportunity.Status == CharacterTestAttackOpportunityStatus.Valid
                && opportunity.Level == CharacterTestAttackOpportunityLevel.RequiresTranslation;
        }

        private static bool IsBetter(
            CharacterTestRuntimeAiPlannerCompiledAction candidate,
            CharacterTestRuntimeAiPlannerCompiledAction current)
        {
            int scoreCompare = candidate.Score.CompareTo(current.Score);
            if (scoreCompare != 0)
                return scoreCompare > 0;

            int costCompare = candidate.Cost.CompareTo(current.Cost);
            if (costCompare != 0)
                return costCompare < 0;

            return candidate.ActionId < current.ActionId;
        }

        private CharacterTestRuntimeAiPlannerAgentState GetOrCreateAgentState(int entityId)
        {
            CharacterTestRuntimeAiPlannerAgentState state = FindAgentState(entityId);
            if (state != null)
                return state;

            state = new CharacterTestRuntimeAiPlannerAgentState(entityId, _profile);
            _agentStates.Add(state);
            return state;
        }

        private CharacterTestRuntimeAiPlannerAgentState FindAgentState(int entityId)
        {
            for (int i = 0; i < _agentStates.Count; i++)
            {
                if (_agentStates[i].Id == entityId)
                    return _agentStates[i];
            }

            return null;
        }

        private Vector2 ResolveActionMove(
            CharacterTestRuntimeAiPlannerAgentState state,
            CharacterTestRuntimeAiPlannerCompiledAction action,
            int frameValue)
        {
            CharacterTestRuntimeAiPlannerCompiledMovementSettings settings =
                action.MovementSettings;
            state.EnsureMovementAction(action.ActionId, settings, frameValue);

            Vector2 desired = ResolveDesiredMove(state, action, settings, frameValue);
            if (state.ReactionDelayFramesRemaining > 0)
            {
                state.ReactionDelayFramesRemaining--;
                desired = Vector2.zero;
            }
            else if (state.DirectionHoldFramesRemaining > 0
                && state.LastDesiredMove.sqrMagnitude > DirectionEpsilonSquared)
            {
                state.DirectionHoldFramesRemaining--;
                desired = state.LastDesiredMove;
            }
            else
            {
                state.LastDesiredMove = desired;
                state.DirectionHoldFramesRemaining = settings.DirectionHoldFrames;
            }

            float speedScale = ResolveActionSpeedScale(state, action.Kind, settings);
            Vector2 steered = ApplySteering(
                state.LastOutputMove,
                desired,
                settings.SteeringResponsiveness);
            Vector2 output = Vector2.ClampMagnitude(steered * speedScale, 1f);
            state.LastOutputMove = output;
            state.LastMovementSpeedScale = speedScale;
            return output;
        }

        private Vector2 ResolveDesiredMove(
            CharacterTestRuntimeAiPlannerAgentState state,
            CharacterTestRuntimeAiPlannerCompiledAction action,
            CharacterTestRuntimeAiPlannerCompiledMovementSettings settings,
            int frameValue)
        {
            Vector2 forward = NormalizeOrZero(state.Facts.MoveDirection);
            if (forward.sqrMagnitude <= DirectionEpsilonSquared)
                return Vector2.zero;

            if (ShouldSwitchStrafeSide(state, action.Kind, frameValue))
                state.SwitchStrafeSide(action.ActionId, settings, frameValue);

            Vector2 lateral = new Vector2(-forward.y, forward.x) * state.StrafeSide;
            switch (action.Kind)
            {
                case CharacterTestRuntimeAiPlannerActionKind.MoveToTarget:
                    return NormalizeOrZero(forward + lateral * ResolveApproachBlend(state, settings));
                case CharacterTestRuntimeAiPlannerActionKind.Strafe:
                    return lateral;
                case CharacterTestRuntimeAiPlannerActionKind.Retreat:
                    return NormalizeOrZero(-forward + lateral * settings.ApproachLateralBlend);
                case CharacterTestRuntimeAiPlannerActionKind.Dodge:
                    return lateral;
                default:
                    return Vector2.zero;
            }
        }

        private static bool ShouldSwitchStrafeSide(
            CharacterTestRuntimeAiPlannerAgentState state,
            CharacterTestRuntimeAiPlannerActionKind kind,
            int frameValue)
        {
            if (kind != CharacterTestRuntimeAiPlannerActionKind.Strafe)
                return false;
            if (frameValue >= state.NextStrafeSwitchFrame)
                return true;
            return state.Facts.EnvironmentNearHazard
                || (state.Facts.TargetLineOfSightBlocked && !state.Facts.TargetVisible);
        }

        private static float ResolveApproachBlend(
            CharacterTestRuntimeAiPlannerAgentState state,
            CharacterTestRuntimeAiPlannerCompiledMovementSettings settings)
        {
            float blend = settings.ApproachLateralBlend;
            if (blend <= 0f)
                return 0f;
            if (state.Facts.EnvironmentNearHazard
                || state.Facts.EnvironmentNearObstacle
                || state.Facts.TargetLineOfSightBlocked)
            {
                blend *= 1.5f;
            }

            return blend > 1f ? 1f : blend;
        }

        private float ResolveActionSpeedScale(
            CharacterTestRuntimeAiPlannerAgentState state,
            CharacterTestRuntimeAiPlannerActionKind kind,
            CharacterTestRuntimeAiPlannerCompiledMovementSettings settings)
        {
            float speedScale = settings.SpeedScale;
            if (kind == CharacterTestRuntimeAiPlannerActionKind.Strafe
                || kind == CharacterTestRuntimeAiPlannerActionKind.Retreat)
                return speedScale;
            if (kind == CharacterTestRuntimeAiPlannerActionKind.Dodge)
                return speedScale > 0f ? speedScale : 1f;
            if (kind != CharacterTestRuntimeAiPlannerActionKind.MoveToTarget)
                return 0f;

            float targetDistance = state.Facts.TargetDistance;
            float preferredMin = settings.PreferredDistanceMin;
            float preferredMax = settings.PreferredDistanceMax;
            if (preferredMax > 0f && targetDistance >= preferredMin && targetDistance <= preferredMax)
                return 0f;

            float stopDistance = preferredMax > 0f ? preferredMax : _profile.AttackRange;
            if (settings.ArrivalSlowdownDistance <= 0f || targetDistance >= stopDistance + settings.ArrivalSlowdownDistance)
                return speedScale;
            if (targetDistance <= stopDistance)
                return 0f;

            float slowdown = (targetDistance - stopDistance) / settings.ArrivalSlowdownDistance;
            if (slowdown < 0f)
                slowdown = 0f;
            else if (slowdown > 1f)
                slowdown = 1f;
            return speedScale * slowdown;
        }

        private static Vector2 ApplySteering(
            Vector2 previous,
            Vector2 desired,
            float responsiveness)
        {
            if (responsiveness >= 1f || previous.sqrMagnitude <= DirectionEpsilonSquared)
                return Vector2.ClampMagnitude(desired, 1f);
            if (responsiveness <= 0f)
                return Vector2.ClampMagnitude(previous, 1f);
            return Vector2.ClampMagnitude(Vector2.Lerp(previous, desired, responsiveness), 1f);
        }

        private static Vector2 NormalizeOrZero(Vector2 value)
        {
            float sqrMagnitude = value.sqrMagnitude;
            if (sqrMagnitude <= DirectionEpsilonSquared)
                return Vector2.zero;
            float magnitude = Mathf.Sqrt(sqrMagnitude);
            return value / magnitude;
        }

        private bool TrySelectTarget(
            CharacterTestCharacterEntity self,
            IReadOnlyList<CharacterTestCharacterEntity> entities,
            CharacterTestAiTargetSelectionMode targetSelectionMode,
            RuntimeFrame frame,
            out CharacterTestCharacterEntity target,
            out CharacterTestTargetGeometrySample targetGeometry,
            out bool hasTargetGeometry)
        {
            target = null;
            targetGeometry = default;
            hasTargetGeometry = false;
            switch (targetSelectionMode)
            {
                case CharacterTestAiTargetSelectionMode.NearestOpposingTeam:
                    return TrySelectNearestOpposingTeamTarget(
                        self,
                        entities,
                        frame,
                        out target,
                        out targetGeometry,
                        out hasTargetGeometry);
                default:
                    return TrySelectNearestOpposingTeamTarget(
                        self,
                        entities,
                        frame,
                        out target,
                        out targetGeometry,
                        out hasTargetGeometry);
            }
        }

        private bool TrySelectNearestOpposingTeamTarget(
            CharacterTestCharacterEntity self,
            IReadOnlyList<CharacterTestCharacterEntity> entities,
            RuntimeFrame frame,
            out CharacterTestCharacterEntity target,
            out CharacterTestTargetGeometrySample targetGeometry,
            out bool hasTargetGeometry)
        {
            target = null;
            targetGeometry = default;
            hasTargetGeometry = false;
            if (self == null || entities == null)
                return false;

            if (_perceptionFrameCache != null
                && _perceptionFrameCache.TryFindNearestTarget(
                    frame,
                    self,
                    CharacterTestPerceptionTargetRelationFilter.OpposingTeam,
                    float.PositiveInfinity,
                    CharacterTestPerceptionTargetTiePolicy.First,
                    out targetGeometry,
                    out target))
            {
                hasTargetGeometry = true;
                return true;
            }

            float bestDistance = float.MaxValue;
            for (int i = 0; i < entities.Count; i++)
            {
                CharacterTestCharacterEntity candidate = entities[i];
                if (candidate == null || candidate == self)
                    continue;

                if (candidate.Team.TeamId == self.Team.TeamId)
                    continue;

                float distance = SqrDistance(self, candidate);
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    target = candidate;
                }
            }

            if (target == null)
                return false;

            if (_perceptionFrameCache != null
                && _perceptionFrameCache.TryGetTargetGeometry(frame, self, target, out targetGeometry))
            {
                hasTargetGeometry = true;
            }

            return true;
        }

        private static float SqrDistance(
            CharacterTestCharacterEntity lhs,
            CharacterTestCharacterEntity rhs)
        {
            float dx = lhs.Transform.X - rhs.Transform.X;
            float dz = lhs.Transform.Z - rhs.Transform.Z;
            return dx * dx + dz * dz;
        }

        private static bool IsActionRunning(CharacterTestCharacterEntity entity)
        {
            return (entity.Action.State == CharacterTestActionStateId.Running);
        }

        private static int ClampFrame(long frame)
        {
            if (frame < 0L)
                return 0;
            return frame > int.MaxValue ? int.MaxValue : (int)frame;
        }

        private static CharacterTestRuntimeAiPlannerTargetLifeState ToPlannerTargetLifeState(
            CharacterTestCharacterLifeState lifeState)
        {
            switch (lifeState)
            {
                case CharacterTestCharacterLifeState.Alive:
                    return CharacterTestRuntimeAiPlannerTargetLifeState.Alive;
                case CharacterTestCharacterLifeState.Dying:
                    return CharacterTestRuntimeAiPlannerTargetLifeState.Dying;
                case CharacterTestCharacterLifeState.Dead:
                    return CharacterTestRuntimeAiPlannerTargetLifeState.Dead;
                case CharacterTestCharacterLifeState.Reviving:
                    return CharacterTestRuntimeAiPlannerTargetLifeState.Reviving;
                default:
                    return CharacterTestRuntimeAiPlannerTargetLifeState.None;
            }
        }

        private sealed class CharacterTestRuntimeAiPlannerAgentState : IAiAgent
        {
            public CharacterTestRuntimeAiPlannerAgentState(
                int id,
                CharacterTestRuntimeAiPlannerCompiledProfile profile)
            {
                Id = id;
                Facts = new CharacterTestRuntimeAiPlannerFactState();
                StateMachine = new AiHfsmStateMachine(
                    profile.HfsmStates,
                    profile.InitialStateId,
                    profile.HfsmTransitions);
                LastCommand = CommandNone;
                LastDecision = CharacterTestRuntimeAiPlannerDecision.Empty(
                    id,
                    StateMachine.CurrentStateId,
                    profile.GetStatePathText(StateMachine.CurrentStateId),
                    SuppressedTargetMissing);
            }

            public int Id { get; }
            public IAiWorldState WorldState => Facts;
            public CharacterTestRuntimeAiPlannerFactState Facts { get; }
            public AiHfsmStateMachine StateMachine { get; }
            public CharacterTestRuntimeAiPlannerEnvironmentSample EnvironmentSample { get; } =
                new CharacterTestRuntimeAiPlannerEnvironmentSample();
            public CharacterTestRuntimeAiPlannerDecision LastDecision;
            public bool HadSubmittedControlIntent;
            public string LastCommand;
            public int LastSeenTargetEntityId;
            public int TargetLastSeenAgeFrames;
            public int ObservedTargetAttackEntityId;
            public CharacterActionId ObservedTargetAttackActionId = CharacterActionId.None;
            public int ObservedTargetAttackLocalFrame = -1;
            public int TargetAttackSequence;
            public int LastRespondedTargetAttackSequence;
            public int NextDodgeAllowedFrame;
            public int PendingDodgeTargetAttackSequence;
            public int PendingDodgeCooldownFrames;
            public int PendingDodgeRequestFrame = -1;
            public int LastMovementActionId;
            public Vector2 LastDesiredMove;
            public Vector2 LastOutputMove;
            public int StrafeSide;
            public int NextStrafeSwitchFrame;
            public int DirectionHoldFramesRemaining;
            public int ReactionDelayFramesRemaining;
            public float LastMovementSpeedScale;
            public bool HasNavigationSample;
            public CharacterTestNavigationSample NavigationSample;
            public bool HasScenePreviewGeometry;
            public float ScenePreviewSourceCenterX;
            public float ScenePreviewSourceCenterY;
            public float ScenePreviewSourceCenterZ;
            public float ScenePreviewTargetCenterX;
            public float ScenePreviewTargetCenterY;
            public float ScenePreviewTargetCenterZ;
            public float ScenePreviewTargetDirectionX;
            public float ScenePreviewTargetDirectionZ;
            public int ScenePreviewTargetBearingMilliDegrees;
            public int ScenePreviewFacingDeltaMilliDegrees;
            public float ScenePreviewFacingDot;
            public float ScenePreviewAttackRange;
            public bool HasScenePreviewDistanceWitness;
            public float ScenePreviewDistanceSourceX;
            public float ScenePreviewDistanceSourceY;
            public float ScenePreviewDistanceSourceZ;
            public float ScenePreviewDistanceTargetX;
            public float ScenePreviewDistanceTargetY;
            public float ScenePreviewDistanceTargetZ;
            public long ScenePreviewDistanceGeometryFrame;

            public int ObserveTargetAttack(
                int targetEntityId,
                CharacterActionId actionId,
                int localFrame)
            {
                bool changed = ObservedTargetAttackEntityId != targetEntityId
                    || ObservedTargetAttackActionId != actionId
                    || localFrame < ObservedTargetAttackLocalFrame;
                if (changed)
                {
                    TargetAttackSequence = TargetAttackSequence == int.MaxValue
                        ? 1
                        : TargetAttackSequence + 1;
                }

                ObservedTargetAttackEntityId = targetEntityId;
                ObservedTargetAttackActionId = actionId;
                ObservedTargetAttackLocalFrame = localFrame;
                return TargetAttackSequence;
            }

            public void ClearObservedTargetAttack()
            {
                ObservedTargetAttackEntityId = 0;
                ObservedTargetAttackActionId = CharacterActionId.None;
                ObservedTargetAttackLocalFrame = -1;
            }

            public void BeginDodgeAttempt(int frameValue, int cooldownFrames)
            {
                PendingDodgeTargetAttackSequence = TargetAttackSequence;
                PendingDodgeCooldownFrames = Math.Max(0, cooldownFrames);
                PendingDodgeRequestFrame = frameValue;
            }

            public void AcceptDodgeAttempt(int frameValue)
            {
                if (PendingDodgeTargetAttackSequence <= 0)
                    return;

                LastRespondedTargetAttackSequence = PendingDodgeTargetAttackSequence;
                NextDodgeAllowedFrame = SafeAddFrame(frameValue, PendingDodgeCooldownFrames);
                RejectDodgeAttempt();
            }

            public void RejectDodgeAttempt()
            {
                PendingDodgeTargetAttackSequence = 0;
                PendingDodgeCooldownFrames = 0;
                PendingDodgeRequestFrame = -1;
            }

            public void ExpirePendingDodgeAttempt(int frameValue)
            {
                if (PendingDodgeRequestFrame >= 0 && PendingDodgeRequestFrame < frameValue)
                    RejectDodgeAttempt();
            }

            public void EnsureMovementAction(
                int actionId,
                CharacterTestRuntimeAiPlannerCompiledMovementSettings settings,
                int frameValue)
            {
                if (LastMovementActionId == actionId)
                    return;

                LastMovementActionId = actionId;
                StrafeSide = ResolveInitialStrafeSide(Id, actionId);
                NextStrafeSwitchFrame = ResolveNextStrafeSwitchFrame(Id, actionId, frameValue, settings);
                DirectionHoldFramesRemaining = 0;
                ReactionDelayFramesRemaining = settings.ReactionDelayFrames;
            }

            public void SwitchStrafeSide(
                int actionId,
                CharacterTestRuntimeAiPlannerCompiledMovementSettings settings,
                int frameValue)
            {
                StrafeSide = StrafeSide >= 0 ? -1 : 1;
                NextStrafeSwitchFrame = ResolveNextStrafeSwitchFrame(Id, actionId, frameValue, settings);
                DirectionHoldFramesRemaining = 0;
            }

            public void ResetMovement()
            {
                LastMovementActionId = 0;
                LastDesiredMove = Vector2.zero;
                LastOutputMove = Vector2.zero;
                DirectionHoldFramesRemaining = 0;
                ReactionDelayFramesRemaining = 0;
                LastMovementSpeedScale = 0f;
            }

            public void SetNavigationSample(CharacterTestNavigationSample sample)
            {
                HasNavigationSample = true;
                NavigationSample = sample;
            }

            public void ClearNavigationSample()
            {
                HasNavigationSample = false;
                NavigationSample = default;
            }

            public void ApplyStateSnapshot(CharacterTestRuntimeAiPlannerAgentStateSnapshot snapshot)
            {
                HadSubmittedControlIntent = snapshot.HadSubmittedControlIntent;
                LastCommand = string.IsNullOrEmpty(snapshot.LastCommand)
                    ? CommandNone
                    : snapshot.LastCommand;
                LastSeenTargetEntityId = snapshot.LastSeenTargetEntityId;
                TargetLastSeenAgeFrames = snapshot.TargetLastSeenAgeFrames;
                ObservedTargetAttackEntityId = snapshot.ObservedTargetAttackEntityId;
                ObservedTargetAttackActionId = snapshot.ObservedTargetAttackActionId;
                ObservedTargetAttackLocalFrame = snapshot.ObservedTargetAttackLocalFrame;
                TargetAttackSequence = snapshot.TargetAttackSequence;
                LastRespondedTargetAttackSequence = snapshot.LastRespondedTargetAttackSequence;
                NextDodgeAllowedFrame = snapshot.NextDodgeAllowedFrame;
                PendingDodgeTargetAttackSequence = snapshot.PendingDodgeTargetAttackSequence;
                PendingDodgeCooldownFrames = snapshot.PendingDodgeCooldownFrames;
                PendingDodgeRequestFrame = snapshot.PendingDodgeTargetAttackSequence > 0
                    ? snapshot.PendingDodgeRequestFrame
                    : -1;
                LastMovementActionId = snapshot.LastMovementActionId;
                LastDesiredMove = new Vector2(snapshot.LastDesiredMoveX, snapshot.LastDesiredMoveY);
                LastOutputMove = new Vector2(snapshot.LastOutputMoveX, snapshot.LastOutputMoveY);
                StrafeSide = snapshot.StrafeSide;
                NextStrafeSwitchFrame = snapshot.NextStrafeSwitchFrame;
                DirectionHoldFramesRemaining = snapshot.DirectionHoldFramesRemaining;
                ReactionDelayFramesRemaining = snapshot.ReactionDelayFramesRemaining;
                LastMovementSpeedScale = snapshot.LastMovementSpeedScale;
                ClearNavigationSample();
            }

            private static int ResolveInitialStrafeSide(int entityId, int actionId)
            {
                return (PositiveHash(entityId, actionId, 17) & 1) == 0 ? -1 : 1;
            }

            private static int ResolveNextStrafeSwitchFrame(
                int entityId,
                int actionId,
                int frameValue,
                CharacterTestRuntimeAiPlannerCompiledMovementSettings settings)
            {
                int min = settings.StrafeSwitchMinFrames;
                int max = settings.StrafeSwitchMaxFrames;
                if (max <= 0)
                    return int.MaxValue;
                int span = max - min + 1;
                int offset = span > 1
                    ? PositiveHash(entityId, actionId, frameValue) % span
                    : 0;
                return SafeAddFrame(frameValue, min + offset);
            }

            private static int SafeAddFrame(int frameValue, int offset)
            {
                if (offset <= 0)
                    return frameValue;
                return frameValue > int.MaxValue - offset
                    ? int.MaxValue
                    : frameValue + offset;
            }

            private static int PositiveHash(int a, int b, int c)
            {
                unchecked
                {
                    int hash = 17;
                    hash = hash * 31 + a;
                    hash = hash * 31 + b;
                    hash = hash * 31 + c;
                    return hash & 0x7fffffff;
                }
            }
        }
    }
}
