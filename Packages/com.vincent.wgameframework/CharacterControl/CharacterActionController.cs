using System;
using System.Globalization;
using MxFramework.CharacterAction;
using MxFramework.Combat.Animation;
using MxFramework.Gameplay;
using MxFramework.Runtime;

namespace MxFramework.CharacterControl
{
    public enum CharacterActionEventType
    {
        Accepted = 0,
        Rejected = 1,
        Queued = 2,
        Started = 3,
        Finished = 4,
        Canceled = 5
    }

    public enum CharacterActionRejectedReason
    {
        None = 0,
        InvalidRequest = 1,
        EntityMismatch = 2,
        ControlStateBlocked = 3,
        ActionLocked = 4,
        DuplicateSameFrame = 5,
        Busy = 6,
        ConstraintRejected = 7,
        MissingCharacterActionRuntime = 8,
        CharacterActionResolveRejected = 9,
        CharacterActionExecutionRejected = 10,
        UnsupportedCharacterActionTrack = 11
    }

    public readonly struct CharacterActionConstraintResult
    {
        private CharacterActionConstraintResult(bool success, CharacterActionRejectedReason reason, string message)
        {
            Success = success;
            Reason = reason;
            Message = message ?? string.Empty;
        }

        public bool Success { get; }
        public CharacterActionRejectedReason Reason { get; }
        public string Message { get; }

        public static CharacterActionConstraintResult Allowed()
        {
            return new CharacterActionConstraintResult(true, CharacterActionRejectedReason.None, default);
        }

        public static CharacterActionConstraintResult Rejected(CharacterActionRejectedReason reason, string message)
        {
            return new CharacterActionConstraintResult(false, reason, message);
        }
    }

    public readonly struct CharacterActionContext
    {
        public CharacterActionContext(
            CharacterActionRequest request,
            CharacterControlState state,
            CharacterControlLockMask lockMask)
        {
            Request = request;
            State = state;
            LockMask = lockMask;
        }

        public CharacterActionRequest Request { get; }
        public CharacterControlState State { get; }
        public CharacterControlLockMask LockMask { get; }
    }

    public interface ICharacterActionConstraint
    {
        CharacterActionConstraintResult Evaluate(CharacterActionContext context);
    }

    public sealed class CharacterActionControllerOptions
    {
        public CharacterActionControllerOptions(
            CharacterActionSetConfig actionSet,
            CharacterActionConfig[] actions,
            CharacterReactionProfile[] reactionProfiles = null,
            CharacterActionCombatTimelineBinding[] combatTimelineBindings = null,
            CharacterActionDurationPolicy durationPolicy = default,
            string[] contextTags = null,
            CharacterActionTrackAdapter trackAdapter = null,
            CharacterActionPresentationTrackAdapter presentationTrackAdapter = null,
            ICharacterActionDownstreamLifecycleSink downstreamLifecycleSink = null,
            string presentationTargetActorId = "",
            string animationLayerId = "base",
            CharacterControlLockMask reactionLockMask =
                CharacterControlLockMask.Move | CharacterControlLockMask.Jump | CharacterControlLockMask.Action)
        {
            ActionSet = actionSet;
            Actions = actions ?? Array.Empty<CharacterActionConfig>();
            ReactionProfiles = reactionProfiles ?? Array.Empty<CharacterReactionProfile>();
            CombatTimelineBindings = combatTimelineBindings ?? Array.Empty<CharacterActionCombatTimelineBinding>();
            DurationPolicy = durationPolicy;
            ContextTags = contextTags ?? Array.Empty<string>();
            TrackAdapter = trackAdapter;
            PresentationTrackAdapter = presentationTrackAdapter;
            DownstreamLifecycleSink = downstreamLifecycleSink;
            PresentationTargetActorId = presentationTargetActorId ?? string.Empty;
            AnimationLayerId = string.IsNullOrWhiteSpace(animationLayerId) ? "base" : animationLayerId;
            ReactionLockMask = reactionLockMask;
        }

        public CharacterActionSetConfig ActionSet { get; }
        public CharacterActionConfig[] Actions { get; }
        public CharacterReactionProfile[] ReactionProfiles { get; }
        public CharacterActionCombatTimelineBinding[] CombatTimelineBindings { get; }
        public CharacterActionDurationPolicy DurationPolicy { get; }
        public string[] ContextTags { get; }
        public CharacterActionTrackAdapter TrackAdapter { get; }
        public CharacterActionPresentationTrackAdapter PresentationTrackAdapter { get; }
        public ICharacterActionDownstreamLifecycleSink DownstreamLifecycleSink { get; }
        public string PresentationTargetActorId { get; }
        public string AnimationLayerId { get; }
        public CharacterControlLockMask ReactionLockMask { get; }
        public bool Enabled => ActionSet != null && Actions.Length > 0;

        internal bool TryGetAction(CharacterActionId actionId, out CharacterActionConfig action)
        {
            for (int i = 0; i < Actions.Length; i++)
            {
                CharacterActionConfig candidate = Actions[i];
                if (candidate != null && candidate.ActionId == actionId)
                {
                    action = candidate;
                    return true;
                }
            }

            action = null;
            return false;
        }

        internal CombatActionTimeline FindCombatTimeline(int combatActionId)
        {
            if ((combatActionId <= 0))
                return null;

            for (int i = 0; i < CombatTimelineBindings.Length; i++)
            {
                CharacterActionCombatTimelineBinding binding = CombatTimelineBindings[i];
                if (binding.Timeline != null
                    && binding.CombatActionId == combatActionId)
                {
                    return binding.Timeline;
                }
            }

            return null;
        }
    }

    public readonly struct CharacterActionResult
    {
        private CharacterActionResult(
            bool success,
            bool queued,
            CharacterActionRejectedReason rejectedReason,
            int actionInstanceId,
            string message)
        {
            Success = success;
            Queued = queued;
            RejectedReason = rejectedReason;
            ActionInstanceId = actionInstanceId;
            Message = message ?? string.Empty;
        }

        public bool Success { get; }
        public bool Queued { get; }
        public CharacterActionRejectedReason RejectedReason { get; }
        public int ActionInstanceId { get; }
        public string Message { get; }

        public static CharacterActionResult Accepted(int actionInstanceId = 0)
        {
            return new CharacterActionResult(
                true,
                false,
                CharacterActionRejectedReason.None,
                actionInstanceId,
                default);
        }

        public static CharacterActionResult QueuedResult()
        {
            return new CharacterActionResult(
                true,
                true,
                CharacterActionRejectedReason.None,
                0,
                default);
        }

        public static CharacterActionResult Rejected(CharacterActionRejectedReason reason, string message)
        {
            return new CharacterActionResult(false, false, reason, 0, message);
        }
    }

    public readonly struct CharacterActionEvent
    {
        public CharacterActionEvent(
            CharacterActionEventType type,
            CharacterActionRequest request,
            CharacterActionRejectedReason rejectedReason,
            int actionInstanceId,
            string message)
        {
            Type = type;
            Request = request;
            RejectedReason = rejectedReason;
            ActionInstanceId = actionInstanceId;
            Message = message ?? string.Empty;
        }

        public CharacterActionEventType Type { get; }
        public CharacterActionRequest Request { get; }
        public CharacterActionRejectedReason RejectedReason { get; }
        public int ActionInstanceId { get; }
        public string Message { get; }
    }

    internal readonly struct CharacterActionCoordinatedExecution
    {
        public CharacterActionCoordinatedExecution(
            CharacterActionResolveResult resolve,
            CharacterActionExecutionResult execution,
            CharacterActionRunnerActionDefinition definition,
            bool hasExecution,
            bool queued)
        {
            Resolve = resolve;
            Execution = execution;
            Definition = definition;
            HasExecution = hasExecution;
            Queued = queued;
        }

        public CharacterActionResolveResult Resolve { get; }
        public CharacterActionExecutionResult Execution { get; }
        public CharacterActionRunnerActionDefinition Definition { get; }
        public bool HasExecution { get; }
        public bool Queued { get; }
        public bool Accepted => HasExecution && Execution.Accepted;
    }

    /// <summary>Owns request resolution, plan lookup, and the single execution authority.</summary>
    internal sealed class CharacterActionRequestCoordinator
    {
        private readonly CharacterActionControllerOptions _options;
        private readonly CharacterActionResolver _resolver;
        private readonly CharacterActionExecutionGateway _gateway;
        private readonly CharacterActionRunnerEventBuffer _executionBuffer;
        private readonly CharacterActionRuntimePlanCache _planCache;
        private readonly CharacterActionReactionStartBuffer _reactionBuffer;

        public CharacterActionRequestCoordinator(CharacterActionControllerOptions options)
        {
            _options = options ?? throw new ArgumentNullException(nameof(options));
            _resolver = new CharacterActionResolver();
            _gateway = new CharacterActionExecutionGateway();
            _executionBuffer = new CharacterActionRunnerEventBuffer(24);
            CharacterActionResolverContext baseContext = CreateContext(default);
            _planCache = new CharacterActionRuntimePlanCache(baseContext);
            _reactionBuffer = new CharacterActionReactionStartBuffer(24);
        }

        public CharacterActionInstance ActiveInstance => _gateway.ActiveInstance;
        public bool HasRunningAction =>
            _gateway.HasActiveInstance
            && _gateway.ActiveInstance != null
            && _gateway.ActiveInstance.IsRunning;

        /// <summary>Resolves one command request and submits it to the owned execution gateway.</summary>
        /// <remarks>AllocByDesign: resolver plans, results, and rejection diagnostics are request-scoped objects.</remarks>
        public CharacterActionCoordinatedExecution Execute(
            CharacterActionRequest request,
            CharacterActionResolverState state)
        {
            CharacterActionResolverContext context = CreateContext(state);
            CharacterActionSourceKind sourceKind = MapSourceKind(request);
            var intent = new CharacterActionIntentRequest(
                ResolveGameplayEntity(request),
                ResolveActionIntentId(request),
                request.HasGameplayAbility ? request.GameplayAbilityId : (int?)null,
                abilityStableId: 0,
                requestedActionId: CharacterActionId.None,
                sourceKind,
                priority: 0,
                request.Frame,
                request.TraceId);
            CharacterActionResolveResult resolve = request.HasGameplayAbility
                ? _resolver.ResolveAbility(context, intent)
                : _resolver.ResolveCommand(context, intent);
            if (resolve == null || resolve.IsRejected)
                return new CharacterActionCoordinatedExecution(resolve, default, null, false, false);
            if (resolve.IsQueued)
                return new CharacterActionCoordinatedExecution(resolve, default, null, false, true);
            if (resolve.Plan == null || !_options.TryGetAction(resolve.Plan.ActionId, out CharacterActionConfig action))
                return new CharacterActionCoordinatedExecution(resolve, default, null, false, false);

            CombatActionTimeline combatTimeline = _options.FindCombatTimeline(action.CombatTrack.CombatActionId);
            CharacterActionRunnerActionDefinition definition =
                CharacterActionRunnerActionDefinition.FromConfig(action, combatTimeline);
            _executionBuffer.EnsureCapacity(Math.Max(24, definition.TrackEvents.Length + 8));
            CharacterActionExecutionRequest executionRequest = CharacterActionExecutionRequest.StartIntent(
                resolve,
                definition,
                sourceKind,
                resolve.Plan.Priority,
                request.TraceId);
            CharacterActionExecutionResult execution = _gateway.Execute(in executionRequest, _executionBuffer);
            return new CharacterActionCoordinatedExecution(resolve, execution, definition, true, false);
        }

        public CharacterActionReactionStartResult React(
            CharacterReactionContext reactionContext,
            CharacterActionResolverState state,
            bool allowFallback)
        {
            return CharacterActionReactionRuntime.TryStart(
                CreateContext(state),
                reactionContext,
                _gateway,
                _planCache,
                _reactionBuffer,
                allowFallback);
        }

        /// <summary>Advances the owned execution gateway by one runtime frame.</summary>
        /// <remarks>NoAlloc after initialization when the reusable execution buffer has enough capacity.</remarks>
        public CharacterActionExecutionResult Tick()
        {
            return _gateway.Tick(_executionBuffer);
        }

        public CharacterActionExecutionResult CompleteExternal(
            CharacterActionId expectedActionId,
            CharacterActionExternalCompletionReason reason,
            RuntimeTraceIdentity traceId)
        {
            CharacterActionExecutionRequest request =
                CharacterActionExecutionRequest.CompleteExternal(expectedActionId, reason, traceId);
            return _gateway.Execute(in request, _executionBuffer);
        }

        public CharacterActionExecutionResult ReplaceForLifecycle(
            CharacterActionId targetActionId,
            CharacterActionLifecycleReason reason,
            RuntimeTraceIdentity traceId)
        {
            if (!_planCache.TryGetOrCreate(targetActionId, traceId, out CharacterActionRuntimePlanEntry entry)
                || entry == null
                || !entry.IsSuccess)
                return default;

            CharacterActionExecutionRequest request = CharacterActionExecutionRequest.ReplaceForLifecycle(
                entry.ResolveResult,
                entry.Definition,
                reason,
                traceId);
            _executionBuffer.EnsureCapacity(Math.Max(24, entry.Definition.TrackEvents.Length + 8));
            return _gateway.Execute(in request, _executionBuffer);
        }

        public CharacterActionExecutionResult TerminateForLifecycle(
            CharacterActionLifecycleReason reason,
            RuntimeTraceIdentity traceId)
        {
            CharacterActionExecutionRequest request =
                CharacterActionExecutionRequest.TerminateForLifecycle(reason, traceId);
            return _gateway.Execute(in request, _executionBuffer);
        }

        public CharacterActionDebugSnapshot CreateDebugSnapshot()
        {
            return _gateway.CreateDebugSnapshot();
        }

        private CharacterActionResolverContext CreateContext(CharacterActionResolverState state)
        {
            return new CharacterActionResolverContext(
                _options.ActionSet,
                _options.Actions,
                _options.ReactionProfiles,
                combatTimelines: null,
                state,
                _options.DurationPolicy,
                _options.CombatTimelineBindings,
                _options.ContextTags);
        }

        private static GameplayEntityId ResolveGameplayEntity(CharacterActionRequest request)
        {
            return request.Entity.HasGameplayEntity ? request.Entity.GameplayEntityId : default;
        }

        private static CharacterActionSourceKind MapSourceKind(CharacterActionRequest request)
        {
            return request.SourceKind == CharacterActionSourceKind.None
                ? (request.HasGameplayAbility
                    ? CharacterActionSourceKind.GameplayAbility
                    : CharacterActionSourceKind.Command)
                : request.SourceKind;
        }

        internal static int ResolveActionIntentId(CharacterActionRequest request)
        {
            if ((request.ActionIntentId > 0))
                return request.ActionIntentId;

            switch (request.Kind)
            {
                case CharacterActionKind.Attack: return CharacterActionRequest.AttackIntentId;
                case CharacterActionKind.Skill: return CharacterActionRequest.SkillIntentId;
                case CharacterActionKind.Interact: return CharacterActionRequest.InteractIntentId;
                case CharacterActionKind.Dodge: return CharacterActionRequest.DodgeIntentId;
                case CharacterActionKind.GameplayAbility: return CharacterActionRequest.GameplayAbilityIntentId;
                default: return 0;
            }
        }
    }

    internal readonly struct CharacterActionDispatchOutcome
    {
        public CharacterActionDispatchOutcome(bool accepted, string message)
        {
            Accepted = accepted;
            Message = message ?? string.Empty;
        }

        public bool Accepted { get; }
        public string Message { get; }
        public static CharacterActionDispatchOutcome Success => new CharacterActionDispatchOutcome(true, default);
    }

    /// <summary>Owns track, presentation, root-motion, and downstream terminal dispatch.</summary>
    internal sealed class CharacterActionDispatchCoordinator
    {
        private readonly CharacterActionControllerOptions _options;
        private readonly CharacterActionDispatchBuffer _trackBuffer = new CharacterActionDispatchBuffer(8);
        private readonly CharacterActionDispatchBuffer _presentationBuffer = new CharacterActionDispatchBuffer(8);

        public CharacterActionDispatchCoordinator(CharacterActionControllerOptions options)
        {
            _options = options ?? throw new ArgumentNullException(nameof(options));
        }

        public CharacterActionDispatchOutcome DispatchTrack(
            CharacterActionRunnerEvent evt,
            CharacterActionRequest request,
            RuntimeFrame frame)
        {
            CharacterActionTrackDispatchEvent dispatch = evt.TrackDispatch;
            if (!dispatch.HasEvent)
                return CharacterActionDispatchOutcome.Success;

            if (dispatch.TrackKind == CharacterActionTrackKind.Animation
                || dispatch.TrackKind == CharacterActionTrackKind.Presentation)
            {
                CharacterActionPresentationTrackAdapter adapter = _options.PresentationTrackAdapter;
                if (adapter == null)
                    return CharacterActionDispatchOutcome.Success;
                CharacterActionPresentationAdapterResult result = adapter.Adapt(
                    evt,
                    CreatePresentationContext(request, frame),
                    _presentationBuffer);
                return ToOutcome(result.Accepted, result.DiagnosticsBuffer);
            }

            CharacterActionTrackAdapter trackAdapter = _options.TrackAdapter;
            if (trackAdapter == null
                || (dispatch.TrackKind == CharacterActionTrackKind.Debug && !trackAdapter.HasDebugSink))
                return CharacterActionDispatchOutcome.Success;
            CharacterActionAdapterResult trackResult = trackAdapter.Adapt(
                evt,
                CreateAdapterContext(request, frame),
                _trackBuffer);
            return ToOutcome(trackResult.Accepted, trackResult.DiagnosticsBuffer);
        }

        public CharacterActionDispatchOutcome DispatchRootMotion(
            CharacterActionRequest request,
            RuntimeFrame frame,
            CharacterActionInstance instance,
            CharacterActionRunnerActionDefinition definition)
        {
            CharacterActionTrackAdapter adapter = _options.TrackAdapter;
            if (adapter == null
                || definition == null
                || instance == null
                || !instance.IsRunning)
                return CharacterActionDispatchOutcome.Success;

            int localFrame = instance.LocalFrame;
            bool verticalActive = definition.IsVerticalRootMotionActive(localFrame);
            if (!definition.UsesRootMotion && !verticalActive)
                return CharacterActionDispatchOutcome.Success;

            CharacterActionRootMotionConfig rootMotion = definition.RootMotion;
            if (definition.UsesRootMotion && (rootMotion == null || !rootMotion.HasFrames))
                return new CharacterActionDispatchOutcome(false, "CharacterAction root motion frames are missing.");

            float x = 0f;
            float y = 0f;
            float z = 0f;
            if (definition.UsesRootMotion
                && localFrame >= rootMotion.StartFrame && localFrame <= rootMotion.EndFrame
                && !rootMotion.TryGetDelta(localFrame, out x, out _, out z))
                return new CharacterActionDispatchOutcome(false, "CharacterAction root motion sample is missing.");
            if (verticalActive
                && (definition.VerticalRootMotion == null
                    || !definition.VerticalRootMotion.TryGetDelta(localFrame, out y)))
                return new CharacterActionDispatchOutcome(false, "CharacterAction vertical root motion sample is missing.");
            if (x.Equals(0f) && y.Equals(0f) && z.Equals(0f))
                return CharacterActionDispatchOutcome.Success;

            RuntimeTraceIdentity traceId = request.TraceId.IsNone ? instance.Plan.TraceId : request.TraceId;
            var rootMotionRequest = new CharacterActionRootMotionRequest(
                new CharacterActionAdapterDispatchMetadata(
                    frame,
                    instance.InstanceId,
                    instance.Plan.PlanId,
                    instance.Plan.ActionId,
                    localFrame,
                    verticalActive
                        ? definition.VerticalRootMotion.SourceAnimationEventId
                        : rootMotion.SourceAnimationEventId,
                    request.SourceId,
                    traceId),
                x,
                y,
                z,
                definition.UsesRootMotion ? rootMotion.Space : CharacterActionRootMotionSpace.Local,
                definition.UsesRootMotion ? rootMotion.ObstaclePolicy : CharacterActionRootMotionObstaclePolicy.Default);
            CharacterActionAdapterSinkResult result = adapter.SubmitRootMotionRequest(rootMotionRequest);
            return result.Accepted
                ? CharacterActionDispatchOutcome.Success
                : new CharacterActionDispatchOutcome(false, ResolveDiagnosticMessage(in result));
        }

        public CharacterActionDispatchOutcome TerminateDownstream(CharacterActionRunnerEvent evt)
        {
            if (_options.DownstreamLifecycleSink == null)
                return CharacterActionDispatchOutcome.Success;

            CharacterActionInstanceState state = evt.Kind == CharacterActionRunnerEventKind.ActionFinished
                ? CharacterActionInstanceState.Finished
                : evt.Kind == CharacterActionRunnerEventKind.ActionInterrupted
                    ? CharacterActionInstanceState.Interrupted
                    : CharacterActionInstanceState.Cancelled;
            CharacterActionAdapterSinkResult result = _options.DownstreamLifecycleSink.TerminateDownstream(
                new CharacterActionDownstreamTermination(
                    evt.InstanceId,
                    evt.ActionId,
                    state,
                    evt.Reason,
                    evt.TraceId));
            return result.Accepted
                ? CharacterActionDispatchOutcome.Success
                : new CharacterActionDispatchOutcome(false, ResolveDiagnosticMessage(in result));
        }

        private CharacterActionAdapterContext CreateAdapterContext(
            CharacterActionRequest request,
            RuntimeFrame frame)
        {
            CharacterControlEntityRef entity = request.Entity;
            return new CharacterActionAdapterContext(
                frame,
                entity.GameplayEntityId,
                entity.CombatEntityId,
                entity.CombatBodyId,
                request.SourceId,
                request.TraceId);
        }

        private CharacterActionPresentationAdapterContext CreatePresentationContext(
            CharacterActionRequest request,
            RuntimeFrame frame)
        {
            string targetActorId = _options.PresentationTargetActorId;
            if (string.IsNullOrEmpty(targetActorId))
            {
                CharacterControlEntityRef entity = request.Entity;
                if (entity.StableId > 0)
                    targetActorId = entity.StableId.ToString(CultureInfo.InvariantCulture);
                else if (entity.HasGameplayEntity)
                    targetActorId = entity.GameplayEntityId.ToString();
                else if (entity.HasCombatEntity)
                    targetActorId = entity.CombatEntityId.ToString();
            }

            return new CharacterActionPresentationAdapterContext(
                frame,
                targetActorId,
                request.SourceId,
                request.TraceId,
                _options.AnimationLayerId);
        }

        private static CharacterActionDispatchOutcome ToOutcome(
            bool accepted,
            CharacterActionDispatchBuffer diagnostics)
        {
            return accepted
                ? CharacterActionDispatchOutcome.Success
                : new CharacterActionDispatchOutcome(false, ResolveDiagnosticMessage(diagnostics));
        }

        private static string ResolveDiagnosticMessage(CharacterActionDispatchBuffer diagnostics)
        {
            int count = diagnostics?.Count ?? 0;
            for (int i = 0; i < count; i++)
            {
                CharacterActionDiagnostic diagnostic = diagnostics[i];
                if (!string.IsNullOrEmpty(diagnostic.Message)) return diagnostic.Message;
                if (!string.IsNullOrEmpty(diagnostic.Code)) return diagnostic.Code;
            }
            return "CharacterAction downstream dispatch was rejected.";
        }

        private static string ResolveDiagnosticMessage(in CharacterActionAdapterSinkResult result)
        {
            for (int i = 0; i < result.DiagnosticCount; i++)
            {
                CharacterActionDiagnostic diagnostic = result[i];
                if (!string.IsNullOrEmpty(diagnostic.Message)) return diagnostic.Message;
                if (!string.IsNullOrEmpty(diagnostic.Code)) return diagnostic.Code;
            }
            return "CharacterAction downstream dispatch was rejected.";
        }
    }

    /// <summary>Coordinates CharacterControl state with the formal CharacterAction authority.</summary>
    public sealed class CharacterActionController : IDisposable
    {
        private readonly CharacterControlStateMachine _stateMachine;
        private readonly ICharacterActionConstraint[] _constraints;
        private readonly CharacterActionControllerOptions _options;
        private readonly CharacterActionRequestCoordinator _requests;
        private readonly CharacterActionDispatchCoordinator _dispatch;
        private CharacterActionRequest _lastSubmittedRequest;
        private bool _hasLastSubmittedRequest;
        private CharacterActionRequest _activeRequest;
        private bool _hasActiveRequest;
        private CharacterActionRunnerActionDefinition _activeDefinition;
        private CharacterReactionContext _pendingReactionContext;
        private bool _hasPendingReactionContext;
        private CharacterActionRequest _queuedRequest;
        private bool _hasQueuedRequest;
        private bool _disposed;

        public CharacterActionController(
            CharacterControlStateMachine stateMachine,
            ICharacterActionConstraint[] constraints = null,
            CharacterActionControllerOptions characterActionOptions = null)
        {
            _stateMachine = stateMachine ?? throw new ArgumentNullException(nameof(stateMachine));
            _constraints = constraints ?? Array.Empty<ICharacterActionConstraint>();
            _options = characterActionOptions;
            if (_options != null && _options.Enabled)
            {
                _requests = new CharacterActionRequestCoordinator(_options);
                _dispatch = new CharacterActionDispatchCoordinator(_options);
            }
        }

        public event Action<CharacterActionEvent> ActionEvent;
        public bool HasQueuedRequest => _hasQueuedRequest;
        public bool UsesCharacterActionRuntime => _requests != null;

        public bool ClearQueuedRequest()
        {
            ThrowIfDisposed();
            if (!_hasQueuedRequest)
                return false;
            _queuedRequest = default;
            _hasQueuedRequest = false;
            return true;
        }

        public CharacterActionResult Submit(CharacterActionRequest request)
        {
            ThrowIfDisposed();
            CharacterActionResult validation = ValidateRequest(request, fromQueue: false);
            if (!validation.Success)
            {
                Emit(CharacterActionEventType.Rejected, request, validation.RejectedReason, 0, validation.Message);
                return validation;
            }

            _lastSubmittedRequest = request;
            _hasLastSubmittedRequest = true;
            CharacterActionCoordinatedExecution operation = _requests.Execute(request, CreateResolverState());
            if (operation.Queued)
                return Queue(request);
            if (!operation.HasExecution || !operation.Accepted)
            {
                if (request.QueueIfBusy && _requests.HasRunningAction)
                    return Queue(request);

                CharacterActionRejectedReason reason = operation.HasExecution
                    ? CharacterActionRejectedReason.CharacterActionExecutionRejected
                    : CharacterActionRejectedReason.CharacterActionResolveRejected;
                string message = operation.HasExecution
                    ? ResolveDiagnosticMessage(operation.Execution.Diagnostics, "CharacterAction execution was rejected.")
                    : ResolveDiagnosticMessage(operation.Resolve, "CharacterAction resolution was rejected.");
                CharacterActionResult rejected = CharacterActionResult.Rejected(reason, message);
                Emit(CharacterActionEventType.Rejected, request, reason, 0, message);
                return rejected;
            }

            Emit(CharacterActionEventType.Accepted, request, CharacterActionRejectedReason.None, 0, default);
            CharacterActionResult dispatch = DispatchExecution(operation.Execution, request, request.Frame);
            if (!dispatch.Success)
                return dispatch;

            _activeDefinition = operation.Definition;
            return DispatchRootMotion(request, request.Frame, operation.Execution.ActiveInstance);
        }

        /// <remarks>NoAlloc after the selected reaction plan and event buffers are warm.</remarks>
        public CharacterActionResult SubmitReaction(
            CharacterReactionContext reactionContext,
            bool allowFallback = true)
        {
            ThrowIfDisposed();
            CharacterActionRequest request = CreateReactionRequest(reactionContext);
            CharacterActionResult validation = ValidateReactionRequest(reactionContext, request);
            if (!validation.Success)
            {
                Emit(CharacterActionEventType.Rejected, request, validation.RejectedReason, 0, validation.Message);
                return validation;
            }

            _lastSubmittedRequest = request;
            _hasLastSubmittedRequest = true;
            CharacterActionReactionStartResult start = _requests.React(
                reactionContext,
                CreateResolverState(),
                allowFallback);
            if (!start.Started)
            {
                if (start.HasExecution)
                    DispatchExecution(start.Execution, request, reactionContext.Frame);
                string message = ResolveReactionMessage(start);
                CharacterActionRejectedReason reason = start.HasExecution
                    ? CharacterActionRejectedReason.CharacterActionExecutionRejected
                    : CharacterActionRejectedReason.CharacterActionResolveRejected;
                CharacterActionResult rejected = CharacterActionResult.Rejected(reason, message);
                Emit(CharacterActionEventType.Rejected, request, reason, 0, message);
                return rejected;
            }

            Emit(CharacterActionEventType.Accepted, request, CharacterActionRejectedReason.None, 0, default);
            _pendingReactionContext = reactionContext;
            _hasPendingReactionContext = true;
            try
            {
                CharacterActionResult dispatch = DispatchExecution(start.Execution, request, reactionContext.Frame);
                if (!dispatch.Success)
                    return dispatch;
                _activeDefinition = start.Execution.ActiveInstance?.Definition;
                return DispatchRootMotion(request, reactionContext.Frame, start.Execution.ActiveInstance);
            }
            finally
            {
                _pendingReactionContext = default;
                _hasPendingReactionContext = false;
            }
        }

        /// <remarks>NoAlloc after execution and dispatch buffers are warm.</remarks>
        public CharacterActionResult TickCharacterAction(RuntimeFrame frame)
        {
            ThrowIfDisposed();
            if (_requests == null)
                return CharacterActionResult.Rejected(
                    CharacterActionRejectedReason.MissingCharacterActionRuntime,
                    "CharacterAction runtime is not configured.");

            CharacterActionExecutionResult execution = _requests.Tick();
            CharacterActionInstance instance = execution.ActiveInstance;
            long instanceId = instance?.InstanceId ?? 0L;
            CharacterActionRequest request = _hasActiveRequest ? _activeRequest : default;
            CharacterActionResult dispatch = DispatchExecution(execution, request, frame);
            // A terminal event can synchronously start the queued action and reset this pooled instance.
            // Its initial sample belongs to Submit, not to the completed Tick operation.
            if (!dispatch.Success || (instance != null && instance.InstanceId != instanceId))
                return dispatch;
            return DispatchRootMotion(request, frame, instance);
        }

        public CharacterActionResult CompleteExternal(
            RuntimeFrame frame,
            CharacterActionId expectedActionId,
            CharacterActionExternalCompletionReason reason,
            RuntimeTraceIdentity traceId = default)
        {
            ThrowIfDisposed();
            if (_requests == null)
                return MissingRuntime();
            CharacterActionExecutionResult execution = _requests.CompleteExternal(expectedActionId, reason, traceId);
            return execution.Accepted
                ? DispatchExecution(execution, _activeRequest, frame)
                : RejectExecution(_activeRequest, execution.Diagnostics);
        }

        public CharacterActionResult ReplaceForLifecycle(
            RuntimeFrame frame,
            CharacterActionId targetActionId,
            CharacterActionLifecycleReason reason,
            RuntimeTraceIdentity traceId = default)
        {
            ThrowIfDisposed();
            if (_requests == null)
                return MissingRuntime();
            CharacterActionExecutionResult execution = _requests.ReplaceForLifecycle(targetActionId, reason, traceId);
            if (!execution.Accepted || execution.Events == null)
                return RejectExecution(_activeRequest, execution.Diagnostics);

            CharacterActionRequest replacement = new CharacterActionRequest(
                frame,
                0,
                _stateMachine.Entity,
                CharacterActionKind.Reaction,
                0,
                0,
                default,
                false,
                false,
                traceId,
                targetActionId.IsValid
                    ? targetActionId.Value
                    : 0);
            return DispatchExecution(execution, replacement, frame);
        }

        public CharacterActionResult TerminateForLifecycle(
            RuntimeFrame frame,
            CharacterActionLifecycleReason reason,
            RuntimeTraceIdentity traceId = default)
        {
            ThrowIfDisposed();
            if (_requests == null)
                return MissingRuntime();
            CharacterActionExecutionResult execution = _requests.TerminateForLifecycle(reason, traceId);
            return execution.Accepted
                ? DispatchExecution(execution, _activeRequest, frame)
                : RejectExecution(_activeRequest, execution.Diagnostics);
        }

        public CharacterActionDebugSnapshot CreateDebugSnapshot()
        {
            ThrowIfDisposed();
            return _requests?.CreateDebugSnapshot();
        }

        public void Dispose()
        {
            if (_disposed)
                return;
            if (_requests != null && _requests.HasRunningAction)
            {
                CharacterActionExecutionResult termination = _requests.TerminateForLifecycle(
                    CharacterActionLifecycleReason.RuntimeShutdown,
                    default);
                DispatchExecution(termination, _activeRequest, default);
            }
            _disposed = true;
        }

        private CharacterActionResult ValidateRequest(CharacterActionRequest request, bool fromQueue)
        {
            if (_requests == null)
                return MissingRuntime();
            if (request.Kind == CharacterActionKind.None)
                return CharacterActionResult.Rejected(CharacterActionRejectedReason.InvalidRequest, "Action kind is None.");
            if (_stateMachine.Entity.IsValid && request.Entity.IsValid && !_stateMachine.Entity.Equals(request.Entity))
                return CharacterActionResult.Rejected(CharacterActionRejectedReason.EntityMismatch, "Action entity does not match the controller.");
            if (!fromQueue && _hasLastSubmittedRequest && IsDuplicateSameFrame(_lastSubmittedRequest, request))
                return CharacterActionResult.Rejected(CharacterActionRejectedReason.DuplicateSameFrame, "Duplicate same-frame action request.");
            if (_stateMachine.CurrentState == CharacterControlState.Disabled
                || _stateMachine.CurrentState == CharacterControlState.Reaction)
                return CharacterActionResult.Rejected(CharacterActionRejectedReason.ControlStateBlocked, "Current control state blocks action requests.");
            if (_stateMachine.IsLocked(CharacterControlLockMask.Action))
                return CharacterActionResult.Rejected(CharacterActionRejectedReason.ActionLocked, "Action control is locked.");

            var context = new CharacterActionContext(request, _stateMachine.CurrentState, _stateMachine.ControlLockMask);
            for (int i = 0; i < _constraints.Length; i++)
            {
                CharacterActionConstraintResult result = _constraints[i].Evaluate(context);
                if (!result.Success)
                {
                    return CharacterActionResult.Rejected(
                        result.Reason == CharacterActionRejectedReason.None
                            ? CharacterActionRejectedReason.ConstraintRejected
                            : result.Reason,
                        result.Message);
                }
            }
            return CharacterActionResult.Accepted();
        }

        private CharacterActionResult ValidateReactionRequest(
            CharacterReactionContext reactionContext,
            CharacterActionRequest request)
        {
            if (_requests == null)
                return MissingRuntime();
            if (_stateMachine.CurrentState == CharacterControlState.Disabled)
                return CharacterActionResult.Rejected(CharacterActionRejectedReason.ControlStateBlocked, "Disabled state blocks reactions.");
            CharacterControlEntityRef entity = _stateMachine.Entity;
            if (entity.HasGameplayEntity
                && reactionContext.EntityId.IsValid
                && !entity.GameplayEntityId.Equals(reactionContext.EntityId))
                return CharacterActionResult.Rejected(CharacterActionRejectedReason.EntityMismatch, "Reaction entity does not match the controller.");
            if (entity.IsValid && request.Entity.IsValid && !entity.Equals(request.Entity))
                return CharacterActionResult.Rejected(CharacterActionRejectedReason.EntityMismatch, "Reaction request entity does not match the controller.");
            return CharacterActionResult.Accepted();
        }

        private CharacterActionResult DispatchExecution(
            CharacterActionExecutionResult execution,
            CharacterActionRequest operationRequest,
            RuntimeFrame frame)
        {
            CharacterActionRunnerEventBuffer events = execution.Events;
            int count = events?.Count ?? 0;
            for (int i = 0; i < count; i++)
            {
                CharacterActionResult result = DispatchEvent(events[i], operationRequest, frame);
                if (!result.Success)
                    return result;
            }
            return CharacterActionResult.Accepted(ToActionInstanceId(execution.ActiveInstance));
        }

        private CharacterActionResult DispatchEvent(
            CharacterActionRunnerEvent evt,
            CharacterActionRequest operationRequest,
            RuntimeFrame frame)
        {
            switch (evt.Kind)
            {
                case CharacterActionRunnerEventKind.ActionStarted:
                    _activeRequest = operationRequest;
                    _hasActiveRequest = true;
                    _activeDefinition = _requests.ActiveInstance?.Definition;
                    if (IsActiveReaction())
                    {
                        CharacterControlTransitionReason reason = _hasPendingReactionContext
                            ? ResolveReactionTransitionReason(_pendingReactionContext.SourceKind)
                            : CharacterControlTransitionReason.ReactionStarted;
                        _stateMachine.BeginReaction(
                            frame,
                            reason,
                            _options.ReactionLockMask,
                            _hasPendingReactionContext ? _pendingReactionContext.Reason : default);
                    }
                    else
                    {
                        _stateMachine.BeginAction(frame);
                    }
                    Emit(CharacterActionEventType.Started, operationRequest, CharacterActionRejectedReason.None, ToActionInstanceId(evt.InstanceId), default);
                    return CharacterActionResult.Accepted(ToActionInstanceId(evt.InstanceId));

                case CharacterActionRunnerEventKind.TrackEventFired:
                case CharacterActionRunnerEventKind.TrackLifecycle:
                    CharacterActionDispatchOutcome track = _dispatch.DispatchTrack(evt, operationRequest, frame);
                    return track.Accepted
                        ? CharacterActionResult.Accepted()
                        : RejectTrack(operationRequest, track.Message);

                case CharacterActionRunnerEventKind.ActionFinished:
                    return FinishAction(evt, frame, canceled: false);

                case CharacterActionRunnerEventKind.ActionCancelled:
                case CharacterActionRunnerEventKind.ActionInterrupted:
                    return FinishAction(evt, frame, canceled: true);

                default:
                    return CharacterActionResult.Accepted();
            }
        }

        private CharacterActionResult FinishAction(
            CharacterActionRunnerEvent evt,
            RuntimeFrame frame,
            bool canceled)
        {
            CharacterActionRequest request = _hasActiveRequest ? _activeRequest : default;
            CharacterActionDispatchOutcome downstream = _dispatch.TerminateDownstream(evt);
            if (IsActiveReaction(request))
                _stateMachine.FinishReaction(frame, evt.Reason);
            else if (canceled)
                _stateMachine.CancelAction(frame, evt.Reason);
            else
                _stateMachine.FinishAction(frame, evt.Reason);

            Emit(
                canceled ? CharacterActionEventType.Canceled : CharacterActionEventType.Finished,
                request,
                CharacterActionRejectedReason.None,
                ToActionInstanceId(evt.InstanceId),
                evt.Reason);
            _activeRequest = default;
            _hasActiveRequest = false;
            _activeDefinition = null;

            if (!downstream.Accepted)
                return RejectTrack(request, downstream.Message);
            if (!canceled && request.Kind != CharacterActionKind.Reaction)
                TrySubmitQueuedRequest(frame);
            return CharacterActionResult.Accepted();
        }

        private CharacterActionResult DispatchRootMotion(
            CharacterActionRequest request,
            RuntimeFrame frame,
            CharacterActionInstance instance)
        {
            CharacterActionDispatchOutcome outcome = _dispatch.DispatchRootMotion(
                request,
                frame,
                instance,
                _activeDefinition);
            return outcome.Accepted
                ? CharacterActionResult.Accepted(ToActionInstanceId(instance))
                : RejectTrack(request, outcome.Message);
        }

        private void TrySubmitQueuedRequest(RuntimeFrame frame)
        {
            if (!_hasQueuedRequest)
                return;
            CharacterActionRequest queued = _queuedRequest.WithFrame(frame);
            _queuedRequest = default;
            _hasQueuedRequest = false;
            CharacterActionResult validation = ValidateRequest(queued, fromQueue: true);
            if (validation.Success)
                Submit(queued);
            else
                Emit(CharacterActionEventType.Rejected, queued, validation.RejectedReason, 0, validation.Message);
        }

        private CharacterActionResolverState CreateResolverState()
        {
            if (_requests == null || !_requests.HasRunningAction)
                return new CharacterActionResolverState(
                    isDisabled: _stateMachine.CurrentState == CharacterControlState.Disabled);

            CharacterActionDebugSnapshot snapshot = _requests.CreateDebugSnapshot();
            CharacterCancelRule[] cancelRules = _activeDefinition?.CancelRules ?? Array.Empty<CharacterCancelRule>();
            return new CharacterActionResolverState(
                isDisabled: _stateMachine.CurrentState == CharacterControlState.Disabled,
                hasActiveAction: true,
                activeActionBlocksImmediateStart: false,
                activeActionId: snapshot.ActionId,
                activeActionLocalFrame: snapshot.LocalFrame,
                activeActionAuthority: _activeDefinition?.TimelineAuthority
                    ?? CharacterActionTimelineAuthority.CharacterAuthored,
                activeCancelRules: cancelRules,
                activeCombatTimeline: _activeDefinition?.CombatTimeline);
        }

        private CharacterActionRequest CreateReactionRequest(CharacterReactionContext reactionContext)
        {
            CharacterControlEntityRef entity = _stateMachine.Entity.IsValid
                ? _stateMachine.Entity
                : CharacterControlEntityRef.FromGameplay(reactionContext.EntityId);
            return new CharacterActionRequest(
                reactionContext.Frame,
                reactionContext.SourceId,
                entity,
                CharacterActionKind.Reaction,
                0,
                0,
                default,
                false,
                false,
                reactionContext.TraceId,
                (int)reactionContext.SourceKind);
        }

        private CharacterActionResult Queue(CharacterActionRequest request)
        {
            _queuedRequest = request;
            _hasQueuedRequest = true;
            Emit(CharacterActionEventType.Queued, request, CharacterActionRejectedReason.None, 0, default);
            return CharacterActionResult.QueuedResult();
        }

        private CharacterActionResult RejectExecution(
            CharacterActionRequest request,
            CharacterActionDiagnostic[] diagnostics)
        {
            string message = ResolveDiagnosticMessage(diagnostics, "CharacterAction execution was rejected.");
            Emit(CharacterActionEventType.Rejected, request, CharacterActionRejectedReason.CharacterActionExecutionRejected, 0, message);
            return CharacterActionResult.Rejected(CharacterActionRejectedReason.CharacterActionExecutionRejected, message);
        }

        private CharacterActionResult RejectTrack(CharacterActionRequest request, string message)
        {
            Emit(CharacterActionEventType.Rejected, request, CharacterActionRejectedReason.UnsupportedCharacterActionTrack, 0, message);
            return CharacterActionResult.Rejected(CharacterActionRejectedReason.UnsupportedCharacterActionTrack, message);
        }

        private CharacterActionResult MissingRuntime()
        {
            return CharacterActionResult.Rejected(
                CharacterActionRejectedReason.MissingCharacterActionRuntime,
                "CharacterAction runtime is not configured.");
        }

        private bool IsActiveReaction()
        {
            CharacterActionInstance active = _requests?.ActiveInstance;
            return active?.Plan != null && active.Plan.Category == CharacterActionCategory.Reaction;
        }

        private bool IsActiveReaction(CharacterActionRequest request)
        {
            return request.Kind == CharacterActionKind.Reaction || IsActiveReaction();
        }

        private void Emit(
            CharacterActionEventType type,
            CharacterActionRequest request,
            CharacterActionRejectedReason rejectedReason,
            int instanceId,
            string message)
        {
            ActionEvent?.Invoke(new CharacterActionEvent(type, request, rejectedReason, instanceId, message));
        }

        private static string ResolveReactionMessage(CharacterActionReactionStartResult result)
        {
            if (!string.IsNullOrEmpty(result.DiagnosticMessage)) return result.DiagnosticMessage;
            if (!string.IsNullOrEmpty(result.DiagnosticCode)) return result.DiagnosticCode;
            return ResolveDiagnosticMessage(result.ResolveResult, "CharacterAction reaction was rejected.");
        }

        private static string ResolveDiagnosticMessage(CharacterActionResolveResult resolve, string fallback)
        {
            return resolve == null
                ? fallback
                : ResolveDiagnosticMessage(resolve.Diagnostics, fallback);
        }

        private static string ResolveDiagnosticMessage(CharacterActionDiagnostic[] diagnostics, string fallback)
        {
            diagnostics = diagnostics ?? Array.Empty<CharacterActionDiagnostic>();
            for (int i = 0; i < diagnostics.Length; i++)
            {
                if (!string.IsNullOrEmpty(diagnostics[i].Message)) return diagnostics[i].Message;
                if (!string.IsNullOrEmpty(diagnostics[i].Code)) return diagnostics[i].Code;
            }
            return fallback ?? string.Empty;
        }

        private static CharacterControlTransitionReason ResolveReactionTransitionReason(
            CharacterReactionContextSourceKind sourceKind)
        {
            switch (sourceKind)
            {
                case CharacterReactionContextSourceKind.CohesionBreak:
                case CharacterReactionContextSourceKind.PressureBandChanged:
                    return CharacterControlTransitionReason.PressureBreak;
                case CharacterReactionContextSourceKind.GuardBreak:
                    return CharacterControlTransitionReason.GuardBreak;
                case CharacterReactionContextSourceKind.ArmorBreak:
                    return CharacterControlTransitionReason.ArmorBreak;
                case CharacterReactionContextSourceKind.Death:
                case CharacterReactionContextSourceKind.Lifecycle:
                    return CharacterControlTransitionReason.Death;
                default:
                    return CharacterControlTransitionReason.ReactionStarted;
            }
        }

        private static bool IsDuplicateSameFrame(CharacterActionRequest left, CharacterActionRequest right)
        {
            return left.Frame == right.Frame
                && left.SourceId == right.SourceId
                && left.Entity.Equals(right.Entity)
                && left.Kind == right.Kind
                && CharacterActionRequestCoordinator.ResolveActionIntentId(left)
                    == CharacterActionRequestCoordinator.ResolveActionIntentId(right)
                && left.CombatActionId == right.CombatActionId
                && left.GameplayAbilityId == right.GameplayAbilityId
                && left.TargetGameplayEntityId.Equals(right.TargetGameplayEntityId)
                && left.TraceId.Equals(right.TraceId);
        }

        private static int ToActionInstanceId(CharacterActionInstance instance)
        {
            return instance == null ? 0 : ToActionInstanceId(instance.InstanceId);
        }

        private static int ToActionInstanceId(long instanceId)
        {
            return instanceId > int.MaxValue ? int.MaxValue : instanceId < 0 ? 0 : (int)instanceId;
        }

        private void ThrowIfDisposed()
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(CharacterActionController));
        }
    }
}
