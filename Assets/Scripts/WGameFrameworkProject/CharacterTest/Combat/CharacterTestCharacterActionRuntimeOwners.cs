using System;
using System.Collections.Generic;
using System.Globalization;
using MxFramework.CharacterAction;
using MxFramework.Combat.Animation;
using MxFramework.Combat.Core;
using MxFramework.Gameplay;
using MxFramework.Runtime;
using UnityEngine;

namespace MxFramework.Game.CharacterTest
{
    public sealed class CharacterTestCharacterActionCatalog
    {
        private readonly Dictionary<CharacterActionId, string> _actionIdTexts = new Dictionary<CharacterActionId, string>();
        private readonly CharacterActionResolver _resolver = new CharacterActionResolver();
        private readonly Dictionary<ActionSetContextKey, CharacterActionSetConfig> _actionSetsByContextNoAlloc =
            new Dictionary<ActionSetContextKey, CharacterActionSetConfig>();
        private readonly Dictionary<string, CharacterActionRuntimePlanCache> _planCachesByActionSet =
            new Dictionary<string, CharacterActionRuntimePlanCache>(StringComparer.Ordinal);

        public CharacterTestCharacterActionCatalog(
            CharacterActionJsonImportResult importResult,
            CharacterActionConfigRegistry registry,
            CharacterActionValidationReport validationReport,
            CharacterActionSetConfig actionSet,
            CharacterTestCharacterActionConfigSourceKind configSourceKind,
            string configSourceDescription,
            string actionSetsSourcePath,
            string actionsSourcePath,
            CharacterTestCharacterActionConfigSourceDiagnostic[] configSourceDiagnostics)
        {
            ImportResult = importResult ?? throw new ArgumentNullException(nameof(importResult));
            Registry = registry ?? throw new ArgumentNullException(nameof(registry));
            _actionIdTexts[CharacterActionId.None] = CharacterActionId.None.ToString();
            for (int i = 0; i < Registry.Actions.Length; i++)
                if (Registry.Actions[i] != null) _actionIdTexts[Registry.Actions[i].ActionId] = Registry.Actions[i].ActionId.ToString();
            ValidationReport = validationReport ?? throw new ArgumentNullException(nameof(validationReport));
            ActionSet = actionSet;
            ConfigSourceKind = configSourceKind;
            ConfigSourceDescription = configSourceDescription ?? string.Empty;
            ActionSetsSourcePath = actionSetsSourcePath ?? string.Empty;
            ActionsSourcePath = actionsSourcePath ?? string.Empty;
            ConfigSourceDiagnostics = CopyDiagnostics(configSourceDiagnostics);
            CharacterActionSetConfig[] actionSets = Registry.ActionSets;
            for (int i = 0; i < actionSets.Length; i++)
            {
                CharacterActionSetConfig candidate = actionSets[i];
                if (candidate == null)
                    continue;

                ActionSetContextKey noAllocKey = new ActionSetContextKey(
                    candidate.CharacterStableId,
                    candidate.EquipmentStateStableId);
                if (!_actionSetsByContextNoAlloc.ContainsKey(noAllocKey))
                    _actionSetsByContextNoAlloc.Add(noAllocKey, candidate);
                if (!_planCachesByActionSet.ContainsKey(candidate.StableId))
                {
                    _planCachesByActionSet.Add(
                        candidate.StableId,
                        new CharacterActionRuntimePlanCache(new CharacterActionResolverContext(
                            candidate,
                            Registry.Actions,
                            Registry.ReactionProfiles)));
                }
            }
        }

        public CharacterActionJsonImportResult ImportResult { get; }
        public CharacterActionConfigRegistry Registry { get; }
        public CharacterActionValidationReport ValidationReport { get; }
        public CharacterActionSetConfig ActionSet { get; }
        public CharacterTestCharacterActionConfigSourceKind ConfigSourceKind { get; }
        public string ConfigSourceDescription { get; }
        public string ActionSetsSourcePath { get; }
        public string ActionsSourcePath { get; }
        public IReadOnlyList<CharacterTestCharacterActionConfigSourceDiagnostic> ConfigSourceDiagnostics { get; }
        public bool IsReady => ImportResult.Success
            && ValidationReport.Success
            && (ActionSet != null || Registry.ActionSets.Length > 0);
        internal CharacterActionResolver Resolver => _resolver;

        internal bool TryResolvePlanCache(
            CharacterTestComponentWorld components,
            CharacterTestCharacterEntityId entityId,
            out CharacterActionRuntimePlanCache cache)
        {
            cache = null;
            CharacterActionSetConfig actionSet = ResolveActionSet(components, entityId);
            if (actionSet != null
                && _planCachesByActionSet.TryGetValue(
                    actionSet.StableId,
                    out cache))
            {
                return true;
            }

            return false;
        }

        internal string ActionIdText(CharacterActionId id)
            => _actionIdTexts.TryGetValue(id, out string text) ? text : id.ToString();

        internal CharacterActionSetConfig ResolveActionSet(
            CharacterTestComponentWorld components,
            CharacterTestCharacterEntityId entityId)
        {
            return ResolveActionSetNoAlloc(components, entityId);
        }

        /// <summary>Resolves an entity ActionSet without constructing the legacy context key.</summary>
        internal CharacterActionSetConfig ResolveActionSetNoAlloc(
            CharacterTestComponentWorld components,
            CharacterTestCharacterEntityId entityId)
        {
            if (components == null || !components.Contains(entityId))
                return ActionSet;

            CharacterTestCharacterIdentityComponent identity = components.GetIdentity(entityId);
            CharacterTestCharacterEquipmentComponent equipment = components.GetEquipment(entityId);
            CharacterActionSetConfig exact;
            if (_actionSetsByContextNoAlloc.TryGetValue(
                    new ActionSetContextKey(
                        identity.CharacterStableId,
                        equipment.EquipmentStateStableId),
                    out exact))
            {
                return exact;
            }

            if (ConfigSourceKind == CharacterTestCharacterActionConfigSourceKind.EmbeddedFallback)
            {
                if (!string.IsNullOrEmpty(equipment.EquipmentStateStableId))
                {
                    if (equipment.EquipmentStateStableId.IndexOf("empty_empty", StringComparison.Ordinal) >= 0
                        && _actionSetsByContextNoAlloc.TryGetValue(
                            new ActionSetContextKey("char.codex_flow_test", "equip_state.codex_flow_test.empty_empty"),
                            out CharacterActionSetConfig emptyActionSet))
                    {
                        return emptyActionSet;
                    }

                    if (equipment.EquipmentStateStableId.IndexOf("sword_empty", StringComparison.Ordinal) >= 0
                        && _actionSetsByContextNoAlloc.TryGetValue(
                            new ActionSetContextKey("char.codex_flow_test", "equip_state.codex_flow_test.sword_empty"),
                            out CharacterActionSetConfig swordActionSet))
                    {
                        return swordActionSet;
                    }
                }

                return ActionSet;
            }

            return string.IsNullOrWhiteSpace(identity.CharacterStableId)
                && string.IsNullOrWhiteSpace(equipment.EquipmentStateStableId)
                    ? ActionSet
                    : null;
        }

        internal CharacterActionResolverContext CreateResolverContext(
            CharacterTestComponentWorld components,
            CharacterTestCharacterEntityId entityId,
            CharacterActionResolverState state = default)
        {
            return new CharacterActionResolverContext(
                ResolveActionSet(components, entityId),
                Registry.Actions,
                Registry.ReactionProfiles,
                state: state);
        }



        private readonly struct ActionSetContextKey : IEquatable<ActionSetContextKey>
        {
            internal ActionSetContextKey(string characterStableId, string equipmentStateStableId)
            {
                CharacterStableId = characterStableId ?? string.Empty;
                EquipmentStateStableId = equipmentStateStableId ?? string.Empty;
            }

            private string CharacterStableId { get; }
            private string EquipmentStateStableId { get; }

            public bool Equals(ActionSetContextKey other)
            {
                return string.Equals(CharacterStableId, other.CharacterStableId, StringComparison.Ordinal)
                    && string.Equals(
                        EquipmentStateStableId,
                        other.EquipmentStateStableId,
                        StringComparison.Ordinal);
            }

            public override bool Equals(object obj)
            {
                return obj is ActionSetContextKey other && Equals(other);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    int hash = StringComparer.Ordinal.GetHashCode(CharacterStableId);
                    return hash * 397
                        ^ StringComparer.Ordinal.GetHashCode(EquipmentStateStableId);
                }
            }
        }

        /// <remarks>NoAlloc. The trace retains the explicit action-slot integer identity.</remarks>
        internal RuntimeTraceIdentity ResolveActionSlotTraceId(int actionSlotId, RuntimeTraceIdentity traceId)
        {
            return traceId.IsNone
                ? new RuntimeTraceIdentity(CharacterTestActionTraceKinds.ActionSlot, 0, 0L, actionSlotId)
                : traceId;
        }

        private static CharacterTestCharacterActionConfigSourceDiagnostic[] CopyDiagnostics(
            CharacterTestCharacterActionConfigSourceDiagnostic[] diagnostics)
        {
            if (diagnostics == null || diagnostics.Length == 0)
                return Array.Empty<CharacterTestCharacterActionConfigSourceDiagnostic>();

            var copy = new CharacterTestCharacterActionConfigSourceDiagnostic[diagnostics.Length];
            for (int i = 0; i < diagnostics.Length; i++)
                copy[i] = diagnostics[i];
            return copy;
        }
    }

    internal sealed class CharacterTestCharacterActionExecutionStore
    {
        private readonly Dictionary<int, CharacterActionExecutionGateway> _gateways =
            new Dictionary<int, CharacterActionExecutionGateway>();
        private readonly CharacterActionRunnerEventBuffer _eventBuffer =
            new CharacterActionRunnerEventBuffer(16);
        private readonly CharacterActionReactionStartBuffer _reactionBuffer =
            new CharacterActionReactionStartBuffer(20);

        internal int Count => _gateways.Count;
        internal CharacterActionRunnerEventBuffer EventBuffer => _eventBuffer;
        internal CharacterActionReactionStartBuffer ReactionBuffer => _reactionBuffer;

        internal CharacterActionExecutionGateway GetOrCreate(CharacterTestCharacterEntityId entityId)
        {
            if (!_gateways.TryGetValue(entityId.Value, out CharacterActionExecutionGateway gateway))
            {
                gateway = new CharacterActionExecutionGateway();
                _gateways.Add(entityId.Value, gateway);
            }

            return gateway;
        }

        internal bool TryGet(
            CharacterTestCharacterEntityId entityId,
            out CharacterActionExecutionGateway gateway)
        {
            gateway = null;
            return entityId.IsValid && _gateways.TryGetValue(entityId.Value, out gateway);
        }

        internal bool TryGetRunning(
            CharacterTestCharacterEntityId entityId,
            out CharacterActionExecutionGateway gateway)
        {
            if (!TryGet(entityId, out gateway))
                return false;

            CharacterActionInstance active = gateway.ActiveInstance;
            return active != null && active.IsRunning;
        }

        /// <summary>Releases terminal payload while retaining the real actor-local instance allocator.
        /// Idle gateway state is already included in runner snapshot/hash and trusted restore.</summary>
        /// <remarks>NoAlloc. Removal is reserved for rollback or rejected restore, not accepted-action cleanup.</remarks>
        internal void ReleaseCompleted(CharacterTestCharacterEntityId entityId)
        {
            if (!TryGet(entityId, out var gateway)) return;
            if (gateway.ActiveInstance != null && gateway.ActiveInstance.IsRunning)
                throw new InvalidOperationException("Cannot release a running action gateway.");
            var completed = gateway.CaptureState();
            // Restoring an actual terminal/idle receipt clears the active payload without rewinding NextInstanceId.
            if (!gateway.TryRestore(completed, null, null, out var diagnostic))
                throw new InvalidOperationException("Completed action gateway could not retain its identity: " + diagnostic.Code);
        }

        internal void Remove(CharacterTestCharacterEntityId entityId)
        {
            if (entityId.IsValid)
                _gateways.Remove(entityId.Value);
        }

        internal static bool IsTerminal(CharacterActionExecutionResult operation)
        {
            CharacterActionRunnerEventBuffer events = operation.Events;
            int eventCount = events != null ? events.Count : 0;
            for (int i = 0; i < eventCount; i++)
            {
                CharacterActionRunnerEventKind kind = events[i].Kind;
                if (kind == CharacterActionRunnerEventKind.ActionFinished
                    || kind == CharacterActionRunnerEventKind.ActionCancelled
                    || kind == CharacterActionRunnerEventKind.ActionInterrupted)
                {
                    return true;
                }
            }

            return operation.ActiveInstance != null && operation.ActiveInstance.IsFinished;
        }
    }

    internal sealed class CharacterTestCharacterActionProjectionWriter
    {
        private readonly Dictionary<int, string> _presentationSourceIds =
            new Dictionary<int, string>(32);
        private readonly CharacterTestCharacterActionTimelineEventBuffer _timelineEvents =
            new CharacterTestCharacterActionTimelineEventBuffer(256);
        private readonly Dictionary<int, List<CollisionProfileContribution>> _collisionContributions =
            new Dictionary<int, List<CollisionProfileContribution>>(32);
        private readonly Dictionary<int, List<MotionStateContribution>> _motionStateContributions =
            new Dictionary<int, List<MotionStateContribution>>(32);
        private readonly Dictionary<int, List<PendingAfterFrameStateExit>> _pendingAfterFrameStateExits =
            new Dictionary<int, List<PendingAfterFrameStateExit>>(32);
        private CharacterTestComponentWorld _components;
        private CharacterTestCharacterRuntimeWorld _thirdWorld;

        internal CharacterTestComponentWorld Components => _components;
        internal CharacterTestCharacterActionTimelineEventBuffer TimelineEvents => _timelineEvents;

        internal void BindRuntimeWorld(CharacterTestCharacterRuntimeWorld world)
        {
            _thirdWorld = world;
            CharacterTestComponentWorld components = world != null ? world.CharacterComponents : null;
            bool changed = !ReferenceEquals(_components, components);
            _components = components;
            _timelineEvents.Clear();
            if (!changed)
                return;

            _collisionContributions.Clear();
            _motionStateContributions.Clear();
            _pendingAfterFrameStateExits.Clear();
        }

        internal void BeginActionStep(
            CharacterTestComponentWorld components,
            CharacterTestCharacterEntityId entityId)
        {
            if (components == null || !components.Contains(entityId))
                return;

            CharacterTestCharacterActionComponent action = components.GetAction(entityId);
            components.SetAction(
                entityId,
                ApplyPendingAfterFrameStateExits(components, entityId, action));
        }

        internal void ApplyOperation(
            CharacterTestComponentWorld components,
            CharacterTestCharacterEntityId entityId,
            CharacterActionExecutionResult operation,
            RuntimeFrame frame = default)
        {
            if (components == null || !components.Contains(entityId))
                return;

            CharacterTestCharacterActionComponent action = components.GetAction(entityId);
            action = ApplyPendingAfterFrameStateExits(components, entityId, action);
            CharacterActionRunnerEventBuffer events = operation.Events;
            int eventCount = events != null ? events.Count : 0;
            for (int i = 0; i < eventCount; i++) {
                ObserveThirdAction(entityId, operation.ActiveInstance, events[i], frame);
                action = ApplyOperationEvent(components, entityId, action, events[i], frame);
            }

            action = ApplyActiveInstanceDebug(action, operation.ActiveInstance);
            components.SetAction(entityId, action);
        }

        private void EnqueueThirdActionMotion(CharacterTestComponentWorld components, CharacterTestCharacterEntityId id, long instance,
            CharacterTestCharacterMotionRequest request)
        {
            var actor = _thirdWorld?.FindEntity(id.Value);
            var source = actor == null ? default : _thirdWorld.DoubleEdgeState.ThirdAttack(actor.GameplayEntityId, instance);
            var channel = request.Kind == CharacterTestCharacterMotionRequestKind.IgnoreGravity
                ? TacticalCard.TacticalThirdBatchMotionChannel.ActiveGravitySuppression
                : request.Kind == CharacterTestCharacterMotionRequestKind.Impulse ? TacticalCard.TacticalThirdBatchMotionChannel.ActiveImpulse
                : request.Kind == CharacterTestCharacterMotionRequestKind.VerticalVelocityOverride ? TacticalCard.TacticalThirdBatchMotionChannel.ActiveVelocityOverride
                : TacticalCard.TacticalThirdBatchMotionChannel.Unknown;
            components.EnqueueMotionRequest(id, request.WithThirdSource(source, channel));
        }

        private void ObserveThirdAction(CharacterTestCharacterEntityId id, CharacterActionInstance active, CharacterActionRunnerEvent evt, RuntimeFrame frame)
        {
            var owner = _thirdWorld?.DoubleEdgeState; var actor = _thirdWorld?.FindEntity(id.Value);
            if (owner == null || owner.Stage == 0 || actor == null) return;
            actor.BindThirdWorld(_thirdWorld);
            if (evt.Kind == CharacterActionRunnerEventKind.ActionStarted) {
                if (active == null || active.InstanceId != evt.InstanceId || evt.InstanceId <= 0 || evt.InstanceId > int.MaxValue)
                    throw new InvalidOperationException("third-batch:AcceptedActionIdentityMissing");
                bool eligible = false;
                foreach (var track in active.Definition.TrackEvents)
                    if (track.EventKind == CharacterActionTrackEventKind.HitTraceWindow || track.EventKind == CharacterActionTrackEventKind.StartCombatAction
                        || track.EventKind == CharacterActionTrackEventKind.SendGameplayRequest && _thirdWorld.ThirdDirectProjectileRequest?.Invoke(track.GameplayRequestId) == true) eligible = true;
                owner.ObserveThirdAttack(actor.GameplayEntityId, (int)evt.InstanceId, frame.Value, eligible);
            } else if (evt.Kind == CharacterActionRunnerEventKind.ActionFinished || evt.Kind == CharacterActionRunnerEventKind.ActionCancelled
                || evt.Kind == CharacterActionRunnerEventKind.ActionInterrupted) owner.EndThirdAttack(actor.GameplayEntityId, evt.InstanceId, frame.Value);
        }

        internal void ApplyRestoredActiveInstance(
            CharacterTestComponentWorld components,
            CharacterTestCharacterEntityId entityId,
            CharacterActionInstance active)
        {
            if (components == null || active == null || !components.Contains(entityId))
                return;

            CharacterTestCharacterActionComponent action =
                components.GetAction(entityId).WithActiveActionAudioBinding(
                    active.Plan.ActionId,
                    CharacterTestCharacterActionComponent.ComputeStableAudioBindingId(active.Plan.ActionId));
            components.SetAction(entityId, ApplyActiveInstanceDebug(action, active));
        }

        private CharacterTestCharacterActionComponent ApplyOperationEvent(
            CharacterTestComponentWorld components,
            CharacterTestCharacterEntityId entityId,
            CharacterTestCharacterActionComponent action,
            CharacterActionRunnerEvent evt,
            RuntimeFrame frame)
        {
            _timelineEvents.Add(new CharacterTestCharacterActionTimelineEvent(
                frame.Value,
                entityId.Value,
                evt.Kind,
                evt.InstanceId,
                evt.ActionId,
                evt.TraceId,
                evt.DiagnosticCode));

            if (evt.Kind == CharacterActionRunnerEventKind.ActionStarted)
            {
                action = action.WithActiveActionAudioBinding(
                    evt.ActionId,
                    CharacterTestCharacterActionComponent.ComputeStableAudioBindingId(evt.ActionId));
            }

            if (!string.IsNullOrEmpty(evt.DiagnosticCode))
                action = action.WithReject(evt.DiagnosticCodeId);

            CharacterActionTrackDispatchEvent dispatch = evt.TrackDispatch;
            if (!dispatch.HasEvent)
                return action;

            CharacterActionTrackLifecyclePhase lifecyclePhase = evt.TrackLifecycle.Phase;

            string animationKey = dispatch.EffectiveAnimationKey;
            if (dispatch.TrackKind == CharacterActionTrackKind.Animation
                && !string.IsNullOrEmpty(animationKey))
            {
                int holdFrames = CharacterTestDeterministicUnits.SecondsToFrames(
                    CharacterActionAnimationKeyUtility.FramesToSeconds(
                        Math.Max(1, dispatch.EndFrame - dispatch.Frame + 1)));
                components.TryGetPreparedAnimation(entityId, evt.ActionId, dispatch.StableEventId, out CharacterTestPreparedAnimationBinding preparedAnimation);
                action = action.WithAnimationRequest(
                    animationKey,
                    dispatch.TransitionSeconds,
                    CharacterTestDeterministicUnits.FramesToSeconds(holdFrames),
                    dispatch.AnimationPlaybackSpeed, preparedAnimation.BindingId);
                components.SetAnimationRequest(
                    entityId,
                    CharacterTestCharacterAnimationRequestComponent.FromDeterministic(
                        frame.Value,
                        evt.ActionId,
                        evt.LocalFrame,
                        animationKey,
                        CharacterTestDeterministicUnits.SecondsToFrames(dispatch.TransitionSeconds),
                        holdFrames,
                        CharacterTestDeterministicUnits.ScaleToMilli(dispatch.AnimationPlaybackSpeed),
                        dispatch.StableEventId, bindingId: preparedAnimation.BindingId,
                        clipResourceKey: preparedAnimation.Clip.Id ?? string.Empty, authorityBindingId: preparedAnimation.AuthorityBindingId));
            }

            if (dispatch.TrackKind == CharacterActionTrackKind.Motion
                && dispatch.EventKind == CharacterActionTrackEventKind.ApplyImpulse
                && (dispatch.X != 0f || dispatch.Y != 0f || dispatch.Z != 0f))
            {
                EnqueueThirdActionMotion(components, entityId, evt.InstanceId, CharacterTestCharacterMotionRequest.Impulse(
                    new Vector3(dispatch.X, dispatch.Y, dispatch.Z),
                    dispatch.MovementMode,
                    evt.ActionId,
                    dispatch.StableEventId,
                    ToMotionRequestSpace(dispatch.MotionVectorSpace)));

                if (dispatch.Y > 0f)
                    action = action.WithJumpImpulse(dispatch.Y);
            }

            if (dispatch.TrackKind == CharacterActionTrackKind.Motion
                && dispatch.EventKind == CharacterActionTrackEventKind.LockMovement)
            {
                if (lifecyclePhase == CharacterActionTrackLifecyclePhase.Active)
                {
                    UpsertMotionStateContribution(entityId, evt, dispatch);
                    EnqueueThirdActionMotion(components, entityId, evt.InstanceId, CharacterTestCharacterMotionRequest.LockMovement(
                        dispatch.MovementMode,
                        evt.ActionId,
                        dispatch.StableEventId));
                }
                else if (lifecyclePhase == CharacterActionTrackLifecyclePhase.Exit)
                {
                    if (evt.TrackLifecycle.Boundary == CharacterActionTrackLifecycleBoundary.AfterFrame)
                    {
                        QueuePendingAfterFrameStateExit(entityId, evt, dispatch);
                        return action;
                    }

                    RemoveMotionStateContribution(entityId, evt.InstanceId, dispatch.StableEventId);
                }
                action = ApplyWinningMotionStateContribution(entityId, action);
            }

            if (dispatch.TrackKind == CharacterActionTrackKind.Motion
                && dispatch.EventKind == CharacterActionTrackEventKind.SetControlScale)
            {
                if (lifecyclePhase == CharacterActionTrackLifecyclePhase.Active)
                {
                    UpsertMotionStateContribution(entityId, evt, dispatch);
                    EnqueueThirdActionMotion(components, entityId, evt.InstanceId, CharacterTestCharacterMotionRequest.ControlScale(
                        dispatch.MovementControlScale,
                        dispatch.RotationControlScale,
                        dispatch.MovementMode,
                        evt.ActionId,
                        dispatch.StableEventId,
                        dispatch.HasMovementControlScale,
                        dispatch.HasRotationControlScale));
                }
                else if (lifecyclePhase == CharacterActionTrackLifecyclePhase.Exit)
                {
                    if (evt.TrackLifecycle.Boundary == CharacterActionTrackLifecycleBoundary.AfterFrame)
                    {
                        QueuePendingAfterFrameStateExit(entityId, evt, dispatch);
                        return action;
                    }

                    RemoveMotionStateContribution(entityId, evt.InstanceId, dispatch.StableEventId);
                }
                action = ApplyWinningMotionStateContribution(entityId, action);
            }

            if (dispatch.TrackKind == CharacterActionTrackKind.Motion
                && lifecyclePhase == CharacterActionTrackLifecyclePhase.Active
                && dispatch.EventKind == CharacterActionTrackEventKind.SetVerticalVelocity)
            {
                EnqueueThirdActionMotion(components, entityId, evt.InstanceId,
                    CharacterTestCharacterMotionRequest.VerticalVelocityOverride(
                        dispatch.Y, evt.ActionId, dispatch.StableEventId));
            }

            if (dispatch.TrackKind == CharacterActionTrackKind.Motion
                && lifecyclePhase == CharacterActionTrackLifecyclePhase.Active
                && dispatch.EventKind == CharacterActionTrackEventKind.IgnoreGravity)
            {
                EnqueueThirdActionMotion(components, entityId, evt.InstanceId,
                    CharacterTestCharacterMotionRequest.IgnoreGravity(
                        evt.ActionId, dispatch.StableEventId));
            }

            if (dispatch.TrackKind == CharacterActionTrackKind.Motion
                && dispatch.EventKind == CharacterActionTrackEventKind.SetCollisionProfile)
            {
                if (lifecyclePhase == CharacterActionTrackLifecyclePhase.Active)
                {
                    UpsertCollisionContribution(entityId, evt, dispatch);
                    ApplyWinningCollisionContribution(components, entityId);
                }
                else if (lifecyclePhase == CharacterActionTrackLifecyclePhase.Exit)
                {
                    if (evt.TrackLifecycle.Boundary == CharacterActionTrackLifecycleBoundary.AfterFrame)
                    {
                        QueuePendingAfterFrameStateExit(entityId, evt, dispatch);
                        return action;
                    }

                    RemoveCollisionContribution(entityId, evt.InstanceId, dispatch.StableEventId);
                    ApplyWinningCollisionContribution(components, entityId);
                }
            }

            if (dispatch.TrackKind == CharacterActionTrackKind.Motion
                && (dispatch.EventKind == CharacterActionTrackEventKind.SetMovementMode
                    || dispatch.EventKind == CharacterActionTrackEventKind.ApplyImpulse))
            {
                action = action.WithMovementRequest(
                    dispatch.MovementMode,
                    dispatch.EventKind == CharacterActionTrackEventKind.LockMovement);
            }

            if (dispatch.TrackKind == CharacterActionTrackKind.Combat)
            {
                CharacterActionAdapterDispatchMetadata metadata =
                    CreateDispatchMetadata(entityId, evt, dispatch);
                var combatEntityId = new CombatEntityId(
                    CharacterTestCharacterPhysicsRuntime.DynamicBodyIdStart + entityId.Value);
                CharacterActionCombatRequest request =
                    dispatch.EventKind == CharacterActionTrackEventKind.SetHitTargetState
                        ? new CharacterActionCombatRequest(
                            metadata,
                            combatEntityId,
                            dispatch.EventKind,
                            dispatch.TargetStateFlags)
                        : new CharacterActionCombatRequest(
                            metadata,
                            combatEntityId,
                            dispatch.EventKind,
                            dispatch.CombatActionId,
                            dispatch.TraceProfileId,
                            dispatch.EquipmentSlotId,
                            dispatch.DamagePayload,
                            dispatch.HitEvent,
                            dispatch.HitVolume);
                components.EnqueueCombatRequest(entityId, request);
            }

            if (dispatch.TrackKind == CharacterActionTrackKind.Gameplay)
            {
                components.EnqueueGameplayRequest(entityId, new CharacterActionGameplayRequest(
                    CreateDispatchMetadata(entityId, evt, dispatch),
                    components.GetGameplayEntityId(entityId),
                    dispatch.EventKind,
                    dispatch.GameplayRequestId,
                    dispatch.AbilityStableId,
                    dispatch.TargetPolicyId,
                    dispatch.EffectTimelineId,
                    dispatch.DamagePayload,
                    dispatch.HitEvent));
            }

            if (dispatch.TrackKind == CharacterActionTrackKind.Presentation
                && dispatch.EventKind == CharacterActionTrackEventKind.PlayAudioCue
                && !(dispatch.PresentationCueId == 0))
            {
                components.EnqueueAudioCueRequest(entityId, new CharacterActionAudioCueRequest(
                    CreateDispatchMetadata(entityId, evt, dispatch),
                    GetPresentationSourceId(entityId),
                    dispatch.PresentationCueId));
            }

            if (dispatch.TrackKind == CharacterActionTrackKind.Presentation
                && dispatch.EventKind == CharacterActionTrackEventKind.SpawnVisualCue
                && !string.IsNullOrEmpty(dispatch.ResourceKey)
                && (!evt.TrackLifecycle.HasEvent
                    || lifecyclePhase == CharacterActionTrackLifecyclePhase.Enter
                    || lifecyclePhase == CharacterActionTrackLifecyclePhase.Exit))
            {
                components.EnqueueVfxRequest(entityId, new CharacterActionVfxRequest(
                    CreateDispatchMetadata(entityId, evt, dispatch),
                    GetPresentationSourceId(entityId),
                    dispatch.ResourceKey,
                    dispatch.EndFrame,
                    dispatch.VfxConfig));
            }

            return action;
        }

        private void QueuePendingAfterFrameStateExit(
            CharacterTestCharacterEntityId entityId,
            CharacterActionRunnerEvent evt,
            CharacterActionTrackDispatchEvent dispatch)
        {
            if (!_pendingAfterFrameStateExits.TryGetValue(
                    entityId.Value,
                    out List<PendingAfterFrameStateExit> exits))
            {
                exits = new List<PendingAfterFrameStateExit>(4);
                _pendingAfterFrameStateExits.Add(entityId.Value, exits);
            }

            var pending = new PendingAfterFrameStateExit(
                evt.InstanceId,
                dispatch.StableEventId,
                dispatch.EventKind);
            for (int i = 0; i < exits.Count; i++)
            {
                if (exits[i].Matches(pending))
                    return;
            }

            exits.Add(pending);
        }

        private CharacterTestCharacterActionComponent ApplyPendingAfterFrameStateExits(
            CharacterTestComponentWorld components,
            CharacterTestCharacterEntityId entityId,
            CharacterTestCharacterActionComponent action)
        {
            if (!_pendingAfterFrameStateExits.TryGetValue(
                    entityId.Value,
                    out List<PendingAfterFrameStateExit> exits))
            {
                return action;
            }

            bool motionChanged = false;
            bool collisionChanged = false;
            for (int i = 0; i < exits.Count; i++)
            {
                PendingAfterFrameStateExit pending = exits[i];
                if (pending.EventKind == CharacterActionTrackEventKind.SetCollisionProfile)
                {
                    RemoveCollisionContribution(
                        entityId,
                        pending.ActionInstanceId,
                        pending.StableEventId);
                    collisionChanged = true;
                }
                else
                {
                    RemoveMotionStateContribution(
                        entityId,
                        pending.ActionInstanceId,
                        pending.StableEventId);
                    motionChanged = true;
                }
            }

            exits.Clear();
            if (collisionChanged)
                ApplyWinningCollisionContribution(components, entityId);
            return motionChanged
                ? ApplyWinningMotionStateContribution(entityId, action)
                : action;
        }

        private readonly struct PendingAfterFrameStateExit
        {
            public PendingAfterFrameStateExit(
                long actionInstanceId,
                int stableEventId,
                CharacterActionTrackEventKind eventKind)
            {
                ActionInstanceId = actionInstanceId;
                StableEventId = stableEventId;
                EventKind = eventKind;
            }

            public long ActionInstanceId { get; }
            public int StableEventId { get; }
            public CharacterActionTrackEventKind EventKind { get; }

            public bool Matches(PendingAfterFrameStateExit other)
            {
                return ActionInstanceId == other.ActionInstanceId
                    && EventKind == other.EventKind
                    && (StableEventId == other.StableEventId);
            }
        }

        private void UpsertMotionStateContribution(
            CharacterTestCharacterEntityId entityId,
            CharacterActionRunnerEvent evt,
            CharacterActionTrackDispatchEvent dispatch)
        {
            if (!_motionStateContributions.TryGetValue(
                    entityId.Value,
                    out List<MotionStateContribution> contributions))
            {
                contributions = new List<MotionStateContribution>(4);
                _motionStateContributions.Add(entityId.Value, contributions);
            }

            var contribution = new MotionStateContribution(evt.InstanceId, dispatch);
            for (int i = 0; i < contributions.Count; i++)
            {
                if (contributions[i].Matches(evt.InstanceId, dispatch.StableEventId))
                {
                    contributions[i] = contribution;
                    return;
                }
            }

            contributions.Add(contribution);
        }

        private void RemoveMotionStateContribution(
            CharacterTestCharacterEntityId entityId,
            long actionInstanceId,
            int stableEventId)
        {
            if (!_motionStateContributions.TryGetValue(
                    entityId.Value,
                    out List<MotionStateContribution> contributions))
            {
                return;
            }

            for (int i = contributions.Count - 1; i >= 0; i--)
            {
                if (contributions[i].Matches(actionInstanceId, stableEventId))
                    contributions.RemoveAt(i);
            }
        }

        private CharacterTestCharacterActionComponent ApplyWinningMotionStateContribution(
            CharacterTestCharacterEntityId entityId,
            CharacterTestCharacterActionComponent action)
        {
            if (!_motionStateContributions.TryGetValue(
                    entityId.Value,
                    out List<MotionStateContribution> contributions)
                || contributions.Count == 0)
            {
                return action.WithControlScale(
                    action.MovementMode,
                    movementControlScale: 1f,
                    rotationControlScale: 1f,
                    movementLocked: false);
            }

            MotionStateContribution movementScale = default;
            MotionStateContribution rotationScale = default;
            MotionStateContribution movementLock = default;
            bool hasMovementScale = false;
            bool hasRotationScale = false;
            bool hasMovementLock = false;
            for (int i = 0; i < contributions.Count; i++)
            {
                MotionStateContribution candidate = contributions[i];
                if (candidate.HasMovementControlScale
                    && (!hasMovementScale || candidate.HasHigherPriorityThan(movementScale)))
                {
                    movementScale = candidate;
                    hasMovementScale = true;
                }
                if (candidate.HasRotationControlScale
                    && (!hasRotationScale || candidate.HasHigherPriorityThan(rotationScale)))
                {
                    rotationScale = candidate;
                    hasRotationScale = true;
                }
                if (candidate.LocksMovement
                    && (!hasMovementLock || candidate.HasHigherPriorityThan(movementLock)))
                {
                    movementLock = candidate;
                    hasMovementLock = true;
                }
            }

            return action.WithControlScale(
                hasMovementLock ? movementLock.MovementMode : action.MovementMode,
                hasMovementScale ? movementScale.MovementControlScale : 1f,
                hasRotationScale ? rotationScale.RotationControlScale : 1f,
                hasMovementLock);
        }

        private readonly struct MotionStateContribution
        {
            public MotionStateContribution(
                long actionInstanceId,
                CharacterActionTrackDispatchEvent dispatch)
            {
                ActionInstanceId = actionInstanceId;
                StableEventId = dispatch.StableEventId;
                EventOrder = dispatch.StableEventOrder;
                StartFrame = dispatch.Frame;
                MovementMode = dispatch.MovementMode;
                LocksMovement = dispatch.EventKind == CharacterActionTrackEventKind.LockMovement;
                HasMovementControlScale =
                    dispatch.EventKind == CharacterActionTrackEventKind.SetControlScale
                    && dispatch.HasMovementControlScale;
                HasRotationControlScale =
                    dispatch.EventKind == CharacterActionTrackEventKind.SetControlScale
                    && dispatch.HasRotationControlScale;
                MovementControlScale = dispatch.MovementControlScale;
                RotationControlScale = dispatch.RotationControlScale;
            }

            public long ActionInstanceId { get; }
            public int StableEventId { get; }
            private int EventOrder { get; }
            public int StartFrame { get; }
            public CharacterMovementMode MovementMode { get; }
            public bool LocksMovement { get; }
            public bool HasMovementControlScale { get; }
            public bool HasRotationControlScale { get; }
            public float MovementControlScale { get; }
            public float RotationControlScale { get; }

            public bool Matches(long actionInstanceId, int stableEventId)
            {
                return ActionInstanceId == actionInstanceId
                    && (StableEventId == stableEventId);
            }

            public bool HasHigherPriorityThan(MotionStateContribution other)
            {
                return StartFrame > other.StartFrame
                    || (StartFrame == other.StartFrame
                        && EventOrder > other.EventOrder);
            }
        }

        private void UpsertCollisionContribution(
            CharacterTestCharacterEntityId entityId,
            CharacterActionRunnerEvent evt,
            CharacterActionTrackDispatchEvent dispatch)
        {
            if (!_collisionContributions.TryGetValue(entityId.Value, out List<CollisionProfileContribution> contributions))
            {
                contributions = new List<CollisionProfileContribution>(4);
                _collisionContributions.Add(entityId.Value, contributions);
            }

            var contribution = new CollisionProfileContribution(evt.InstanceId, evt.ActionId, dispatch);
            for (int i = 0; i < contributions.Count; i++)
            {
                if (contributions[i].Matches(evt.InstanceId, dispatch.StableEventId))
                {
                    contributions[i] = contribution;
                    return;
                }
            }

            contributions.Add(contribution);
        }

        private void RemoveCollisionContribution(
            CharacterTestCharacterEntityId entityId,
            long actionInstanceId,
            int stableEventId)
        {
            if (!_collisionContributions.TryGetValue(entityId.Value, out List<CollisionProfileContribution> contributions))
                return;
            for (int i = contributions.Count - 1; i >= 0; i--)
            {
                if (contributions[i].Matches(actionInstanceId, stableEventId))
                    contributions.RemoveAt(i);
            }
        }

        private void ApplyWinningCollisionContribution(
            CharacterTestComponentWorld components,
            CharacterTestCharacterEntityId entityId)
        {
            CharacterTestColliderPostureComponent posture = components.GetColliderPosture(entityId);
            if (!_collisionContributions.TryGetValue(entityId.Value, out List<CollisionProfileContribution> contributions)
                || contributions.Count == 0)
            {
                components.SetColliderPosture(entityId, posture.WithoutActionOverride());
                return;
            }

            CollisionProfileContribution winner = contributions[0];
            for (int i = 1; i < contributions.Count; i++)
            {
                if (contributions[i].HasHigherPriorityThan(winner))
                    winner = contributions[i];
            }
            components.SetColliderPosture(
                entityId,
                posture.WithActionOverride(
                    winner.ProfileId,
                    winner.RestorePolicy,
                    winner.TransitionFrames,
                    winner.ActionId,
                    winner.StableEventId));
        }

        private readonly struct CollisionProfileContribution
        {
            public CollisionProfileContribution(
                long actionInstanceId,
                CharacterActionId actionId,
                CharacterActionTrackDispatchEvent dispatch)
            {
                ActionInstanceId = actionInstanceId;
                ActionId = actionId;
                StableEventId = dispatch.StableEventId;
                EventOrder = dispatch.StableEventOrder;
                StartFrame = dispatch.Frame;
                ProfileId = dispatch.CollisionProfileId;
                RestorePolicy = dispatch.CollisionRestorePolicy;
                TransitionFrames = dispatch.CollisionTransitionFrames;
            }

            public long ActionInstanceId { get; }
            public CharacterActionId ActionId { get; }
            public int StableEventId { get; }
            private int EventOrder { get; }
            public int StartFrame { get; }
            public int ProfileId { get; }
            public CharacterActionCollisionProfileRestorePolicy RestorePolicy { get; }
            public int TransitionFrames { get; }

            public bool Matches(long actionInstanceId, int stableEventId)
            {
                return ActionInstanceId == actionInstanceId
                    && (StableEventId == stableEventId);
            }

            public bool HasHigherPriorityThan(CollisionProfileContribution other)
            {
                return StartFrame > other.StartFrame
                    || (StartFrame == other.StartFrame
                        && EventOrder > other.EventOrder);
            }
        }

        private static CharacterActionAdapterDispatchMetadata CreateDispatchMetadata(
            CharacterTestCharacterEntityId entityId,
            CharacterActionRunnerEvent evt,
            CharacterActionTrackDispatchEvent dispatch)
        {
            return new CharacterActionAdapterDispatchMetadata(
                new RuntimeFrame(evt.LocalFrame),
                evt.InstanceId,
                evt.PlanId,
                evt.ActionId,
                evt.LocalFrame,
                dispatch.StableEventId,
                sourceId: entityId.Value,
                traceId: evt.TraceId,
                lifecyclePhase: evt.TrackLifecycle.Phase,
                lifecycleBoundary: evt.TrackLifecycle.Boundary,
                lifecycleExitReason: evt.TrackLifecycle.ExitReason);
        }

        private static CharacterTestCharacterActionComponent ApplyActiveInstanceDebug(
            CharacterTestCharacterActionComponent action,
            CharacterActionInstance activeInstance)
        {
            if (activeInstance == null)
                return action;

            return action.WithDebugValues(
                activeInstance.Plan.ActionId,
                (CharacterTestActionStateId)activeInstance.State,
                (CharacterTestActionPhaseId)activeInstance.CurrentPhaseKind,
                activeInstance.LocalFrame,
                action.LastRejection);
        }

        private string GetPresentationSourceId(CharacterTestCharacterEntityId entityId)
        {
            int value = entityId.Value;
            if (_presentationSourceIds.TryGetValue(value, out string sourceId))
                return sourceId;

            sourceId = value.ToString(CultureInfo.InvariantCulture);
            _presentationSourceIds.Add(value, sourceId);
            return sourceId;
        }

        private static CharacterTestCharacterMotionRequestSpace ToMotionRequestSpace(
            CharacterActionMotionVectorSpace space)
        {
            return space == CharacterActionMotionVectorSpace.Local
                ? CharacterTestCharacterMotionRequestSpace.Local
                : CharacterTestCharacterMotionRequestSpace.World;
        }
    }

    internal sealed class CharacterTestCharacterActionSnapshotAdapter
    {
        private readonly CharacterTestCharacterActionCatalog _catalog;
        private readonly CharacterTestCharacterActionExecutionStore _executions;
        private readonly CharacterTestCharacterActionProjectionWriter _projection;

        public CharacterTestCharacterActionSnapshotAdapter(
            CharacterTestCharacterActionCatalog catalog,
            CharacterTestCharacterActionExecutionStore executions,
            CharacterTestCharacterActionProjectionWriter projection)
        {
            _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
            _executions = executions ?? throw new ArgumentNullException(nameof(executions));
            _projection = projection ?? throw new ArgumentNullException(nameof(projection));
        }

        internal bool TryCapture(
            CharacterTestCharacterEntityId entityId,
            out CharacterActionExecutionState state)
        {
            state = default;
            if (!_executions.TryGet(entityId, out CharacterActionExecutionGateway gateway))
                return false;

            state = gateway.CaptureState();
            return true;
        }

        internal bool TryRestore(
            CharacterTestComponentWorld components,
            CharacterTestCharacterEntityId entityId,
            CharacterActionExecutionState state,
            out string diagnostic)
        {
            diagnostic = string.Empty;
            if (components == null || !components.Contains(entityId))
                return false;

            if (!state.HasActiveAction || state.State == CharacterActionInstanceState.None)
            {
                if (state.NextInstanceId > 1L)
                {
                    CharacterActionExecutionGateway inactiveGateway = _executions.GetOrCreate(entityId);
                    inactiveGateway.TryRestore(state, null, null, out _);
                }
                else
                {
                    _executions.Remove(entityId);
                }

                return true;
            }

            if (!_catalog.IsReady)
            {
                _executions.Remove(entityId);
                diagnostic = "Character action runtime is not ready.";
                return false;
            }

            RuntimeTraceIdentity traceId = (state.TraceId).IsNone
                ? new RuntimeTraceIdentity(CharacterTestActionTraceKinds.Restore, entityId.Value, state.ActiveActionInstanceId, state.ActionId.Value)
                : state.TraceId;
            if (!_catalog.TryResolvePlanCache(components, entityId, out CharacterActionRuntimePlanCache cache))
            {
                _executions.Remove(entityId);
                diagnostic = "Character action restore failed: missing exact ActionSet for "
                    + FormatEntityActionContext(components, entityId) + ".";
                return false;
            }
            if (!cache.TryGetOrCreate(
                    state.ActionId,
                    traceId,
                    out CharacterActionRuntimePlanEntry entry)
                || entry == null
                || !entry.IsSuccess)
            {
                _executions.Remove(entityId);
                diagnostic = "Character action restore failed to resolve action: " + state.ActionId;
                return false;
            }

            CharacterActionExecutionGateway gateway = _executions.GetOrCreate(entityId);
            CharacterActionPlan restorePlan = CreateRestorePlan(entry.Plan, state.PlanId);
            if (!gateway.TryRestore(
                    state,
                    restorePlan,
                    entry.Definition,
                    out CharacterActionDiagnostic restoreDiagnostic))
            {
                _executions.Remove(entityId);
                diagnostic = restoreDiagnostic.Message;
                return false;
            }

            _projection.ApplyRestoredActiveInstance(components, entityId, gateway.ActiveInstance);
            return true;
        }

        private static string FormatEntityActionContext(
            CharacterTestComponentWorld components,
            CharacterTestCharacterEntityId entityId)
        {
            CharacterTestCharacterIdentityComponent identity = components.GetIdentity(entityId);
            CharacterTestCharacterEquipmentComponent equipment = components.GetEquipment(entityId);
            return "character=" + identity.CharacterStableId
                + " package=" + identity.PrefabKey.PackageId
                + " equipmentState=" + equipment.EquipmentStateStableId;
        }

        private static CharacterActionPlan CreateRestorePlan(
            CharacterActionPlan resolvedPlan,
            long restorePlanId)
        {
            if (resolvedPlan == null || restorePlanId <= 0L || resolvedPlan.PlanId == restorePlanId)
                return resolvedPlan;

            return new CharacterActionPlan(
                restorePlanId,
                resolvedPlan.ActionId,
                resolvedPlan.Category,
                resolvedPlan.Priority,
                resolvedPlan.DurationFrames,
                resolvedPlan.Phases,
                resolvedPlan.Tracks,
                resolvedPlan.TraceId);
        }
    }
}
