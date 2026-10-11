using MxFramework.Runtime;
using System;
using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("MxFramework.Tests")]


namespace MxFramework.CharacterAction
{
    /// <summary>Identifies the execution policy selected by a CharacterAction request.</summary>
    public enum CharacterActionExecutionRequestKind
    {
        None = 0,
        StartSlot = 1,
        StartIntent = 2,
        React = 3,
        CompleteExternal = 4,
        ReplaceForLifecycle = 5,
        TerminateForLifecycle = 6,
        CancelOnly = 7,
        ReplaceForOrchestration = 8,
        TerminateForOrchestration = 9
    }

    /// <summary>Names the only lifecycle boundaries that may forcibly end a CharacterAction.</summary>
    public enum CharacterActionLifecycleReason
    {
        None = 0,
        Death = 1,
        ReviveReset = 2,
        EntityDespawn = 3,
        ConfigurationReload = 4,
        RuntimeShutdown = 5,
        PreviewSeek = 6,
        ReplayRebuild = 7
    }

    /// <summary>Names an external fact that normally completes the expected active action.</summary>
    public enum CharacterActionExternalCompletionReason
    {
        None = 0,
        Landed = 1
    }

    /// <summary>Names a higher-level gameplay orchestration boundary that may forcibly replace an action.</summary>
    public enum CharacterActionOrchestrationReason
    {
        None = 0,
        BehaviorRecipeTimelineJump = 1,
        BehaviorRecipeReturnToPlanner = 2
    }

    /// <summary>Describes the observable outcome of a CharacterAction execution request.</summary>
    public enum CharacterActionExecutionStatus
    {
        Rejected = 0,
        Started = 1,
        Transitioned = 2,
        Interrupted = 3,
        LifecycleReplaced = 4,
        Completed = 5,
        Terminated = 6,
        NoOp = 7,
        Cancelled = 8,
        OrchestrationReplaced = 9,
        OrchestrationTerminated = 10
    }

    /// <summary>Immutable input for the single CharacterAction execution authority.</summary>
    /// <remarks>NoAlloc value type. Targeted factories retain already-resolved plans and definitions.</remarks>
    public readonly struct CharacterActionExecutionRequest
    {
        private CharacterActionExecutionRequest(
            CharacterActionExecutionRequestKind kind,
            CharacterActionResolveResult resolveResult,
            CharacterActionRunnerActionDefinition definition,
            CharacterActionSourceKind sourceKind,
            int priority,
            RuntimeTraceIdentity traceId,
            int actionSlotId,
            CharacterActionRequestType requestType,
            CharacterActionId expectedActionId,
            CharacterActionExternalCompletionReason externalCompletionReason,
            CharacterActionLifecycleReason lifecycleReason,
            CharacterActionOrchestrationReason orchestrationReason)
        {
            Kind = kind;
            ResolveResult = resolveResult;
            Definition = definition;
            SourceKind = sourceKind;
            Priority = priority;
            TraceId = traceId.IsNone ? (resolveResult?.TraceId ?? default) : traceId;
            ActionSlotId = actionSlotId;
            RequestType = requestType;
            ExpectedActionId = expectedActionId;
            ExternalCompletionReason = externalCompletionReason;
            LifecycleReason = lifecycleReason;
            OrchestrationReason = orchestrationReason;
        }

        public CharacterActionExecutionRequestKind Kind { get; }
        public CharacterActionResolveResult ResolveResult { get; }
        public CharacterActionRunnerActionDefinition Definition { get; }
        public CharacterActionSourceKind SourceKind { get; }
        public int Priority { get; }
        public RuntimeTraceIdentity TraceId { get; }
        public int ActionSlotId { get; }
        public CharacterActionRequestType RequestType { get; }
        public CharacterActionId ExpectedActionId { get; }
        public CharacterActionExternalCompletionReason ExternalCompletionReason { get; }
        public CharacterActionLifecycleReason LifecycleReason { get; }
        public CharacterActionOrchestrationReason OrchestrationReason { get; }

        /// <summary>Creates a normal action-slot request that obeys the active action transition window.</summary>
        /// <remarks>NoAlloc.</remarks>
        public static CharacterActionExecutionRequest StartSlot(
            CharacterActionResolveResult resolveResult,
            CharacterActionRunnerActionDefinition definition,
            CharacterActionSourceKind sourceKind,
            int actionSlotId,
            int priority = 0,
            RuntimeTraceIdentity traceId = default)
        {
            if (actionSlotId <= 0)
                throw new ArgumentOutOfRangeException(nameof(actionSlotId), "Action slot id must be positive.");

            return CreateTargeted(
                CharacterActionExecutionRequestKind.StartSlot,
                resolveResult,
                definition,
                sourceKind,
                priority,
                traceId,
                actionSlotId,
                CharacterActionLifecycleReason.None);
        }

        /// <summary>Creates a normal intent request that obeys the active action transition window.</summary>
        /// <remarks>NoAlloc.</remarks>
        public static CharacterActionExecutionRequest StartIntent(
            CharacterActionResolveResult resolveResult,
            CharacterActionRunnerActionDefinition definition,
            CharacterActionSourceKind sourceKind,
            int priority = 0,
            RuntimeTraceIdentity traceId = default)
        {
            return StartIntent(
                resolveResult,
                definition,
                sourceKind,
                priority,
                traceId,
                actionSlotId: 0);
        }

        /// <summary>Creates a slot-aware intent request that obeys the active action transition window.</summary>
        /// <remarks>NoAlloc.</remarks>
        public static CharacterActionExecutionRequest StartIntent(
            CharacterActionResolveResult resolveResult,
            CharacterActionRunnerActionDefinition definition,
            CharacterActionSourceKind sourceKind,
            int priority,
            RuntimeTraceIdentity traceId,
            int actionSlotId)
        {
            if (actionSlotId < 0)
                throw new ArgumentOutOfRangeException(nameof(actionSlotId), "Action slot id cannot be negative.");

            return CreateTargeted(
                CharacterActionExecutionRequestKind.StartIntent,
                resolveResult,
                definition,
                sourceKind,
                priority,
                traceId,
                actionSlotId,
                CharacterActionLifecycleReason.None);
        }

        /// <summary>Creates a normal reaction request that obeys the active action interrupt rules.</summary>
        /// <remarks>NoAlloc.</remarks>
        public static CharacterActionExecutionRequest React(
            CharacterActionResolveResult resolveResult,
            CharacterActionRunnerActionDefinition definition,
            CharacterActionSourceKind sourceKind,
            int priority,
            RuntimeTraceIdentity traceId = default)
        {
            return CreateTargeted(
                CharacterActionExecutionRequestKind.React,
                resolveResult,
                definition,
                sourceKind,
                priority,
                traceId,
                0,
                CharacterActionLifecycleReason.None);
        }

        /// <summary>Completes the expected active action from an authoritative external fact.</summary>
        /// <remarks>NoAlloc.</remarks>
        public static CharacterActionExecutionRequest CompleteExternal(
            CharacterActionId expectedActionId,
            CharacterActionExternalCompletionReason reason,
            RuntimeTraceIdentity traceId = default)
        {
            if (!expectedActionId.IsValid)
                throw new ArgumentException("Expected action id is required.", nameof(expectedActionId));
            if (reason != CharacterActionExternalCompletionReason.Landed)
                throw new ArgumentOutOfRangeException(nameof(reason), "External completion reason is not supported.");

            return new CharacterActionExecutionRequest(
                CharacterActionExecutionRequestKind.CompleteExternal,
                null,
                null,
                CharacterActionSourceKind.Scripted,
                0,
                traceId,
                0,
                CharacterActionRequestType.Any,
                expectedActionId,
                reason,
                CharacterActionLifecycleReason.None,
                CharacterActionOrchestrationReason.None);
        }

        /// <summary>Atomically replaces the active action at an explicit lifecycle boundary.</summary>
        /// <remarks>NoAlloc. The target is fully validated before the active instance is changed.</remarks>
        public static CharacterActionExecutionRequest ReplaceForLifecycle(
            CharacterActionResolveResult resolveResult,
            CharacterActionRunnerActionDefinition definition,
            CharacterActionLifecycleReason reason,
            RuntimeTraceIdentity traceId = default)
        {
            ValidateLifecycleReason(reason);
            return CreateTargeted(
                CharacterActionExecutionRequestKind.ReplaceForLifecycle,
                resolveResult,
                definition,
                CharacterActionSourceKind.Death,
                resolveResult?.Plan?.Priority ?? 0,
                traceId,
                0,
                reason,
                CharacterActionOrchestrationReason.None);
        }

        /// <summary>Terminates the active action at an explicit lifecycle boundary.</summary>
        /// <remarks>NoAlloc.</remarks>
        public static CharacterActionExecutionRequest TerminateForLifecycle(
            CharacterActionLifecycleReason reason,
            RuntimeTraceIdentity traceId = default)
        {
            ValidateLifecycleReason(reason);
            return new CharacterActionExecutionRequest(
                CharacterActionExecutionRequestKind.TerminateForLifecycle,
                null,
                null,
                CharacterActionSourceKind.Death,
                0,
                traceId,
                0,
                CharacterActionRequestType.Any,
                CharacterActionId.None,
                CharacterActionExternalCompletionReason.None,
                reason,
                CharacterActionOrchestrationReason.None);
        }

        /// <summary>Creates a request that exits the active action through its transition window without starting another action.</summary>
        /// <remarks>NoAlloc. Requests without a slot use <paramref name="actionSlotId"/> zero.</remarks>
        public static CharacterActionExecutionRequest CancelOnly(
            CharacterActionRequestType requestType,
            CharacterActionSourceKind sourceKind,
            int actionSlotId = 0,
            RuntimeTraceIdentity traceId = default)
        {
            if ((requestType & ~CharacterActionRequestType.All) != 0
                || requestType == CharacterActionRequestType.Any)
            {
                throw new ArgumentOutOfRangeException(nameof(requestType), "Cancel-only request type must identify one or more defined request types.");
            }
            if (!CharacterActionRunnerEnumGuards.IsDefined(sourceKind) || sourceKind == CharacterActionSourceKind.None)
                throw new ArgumentOutOfRangeException(nameof(sourceKind), "Action source kind must identify a concrete source.");
            if (actionSlotId < 0)
                throw new ArgumentOutOfRangeException(nameof(actionSlotId), "Action slot id cannot be negative.");

            return new CharacterActionExecutionRequest(
                CharacterActionExecutionRequestKind.CancelOnly,
                null,
                null,
                sourceKind,
                0,
                traceId,
                actionSlotId,
                requestType,
                CharacterActionId.None,
                CharacterActionExternalCompletionReason.None,
                CharacterActionLifecycleReason.None,
                CharacterActionOrchestrationReason.None);
        }

        /// <summary>Atomically replaces the active action at an explicit gameplay-orchestration boundary.</summary>
        /// <remarks>NoAlloc. The target is fully validated before the active instance is changed.</remarks>
        public static CharacterActionExecutionRequest ReplaceForOrchestration(
            CharacterActionResolveResult resolveResult,
            CharacterActionRunnerActionDefinition definition,
            CharacterActionOrchestrationReason reason,
            RuntimeTraceIdentity traceId = default)
        {
            ValidateOrchestrationReason(reason);
            return CreateTargeted(
                CharacterActionExecutionRequestKind.ReplaceForOrchestration,
                resolveResult,
                definition,
                CharacterActionSourceKind.RuntimeAiPlanner,
                resolveResult?.Plan?.Priority ?? 0,
                traceId,
                0,
                CharacterActionLifecycleReason.None,
                reason);
        }

        /// <summary>Terminates the active action at an explicit gameplay-orchestration boundary.</summary>
        /// <remarks>NoAlloc.</remarks>
        public static CharacterActionExecutionRequest TerminateForOrchestration(
            CharacterActionOrchestrationReason reason,
            RuntimeTraceIdentity traceId = default)
        {
            ValidateOrchestrationReason(reason);
            return new CharacterActionExecutionRequest(
                CharacterActionExecutionRequestKind.TerminateForOrchestration,
                null,
                null,
                CharacterActionSourceKind.RuntimeAiPlanner,
                0,
                traceId,
                0,
                CharacterActionRequestType.Any,
                CharacterActionId.None,
                CharacterActionExternalCompletionReason.None,
                CharacterActionLifecycleReason.None,
                reason);
        }

        private static CharacterActionExecutionRequest CreateTargeted(
            CharacterActionExecutionRequestKind kind,
            CharacterActionResolveResult resolveResult,
            CharacterActionRunnerActionDefinition definition,
            CharacterActionSourceKind sourceKind,
            int priority,
            RuntimeTraceIdentity traceId,
            int actionSlotId,
            CharacterActionLifecycleReason lifecycleReason,
            CharacterActionOrchestrationReason orchestrationReason = CharacterActionOrchestrationReason.None)
        {
            if (resolveResult == null)
                throw new ArgumentNullException(nameof(resolveResult));
            if (definition == null)
                throw new ArgumentNullException(nameof(definition));
            if (!CharacterActionRunnerEnumGuards.IsDefined(sourceKind) || sourceKind == CharacterActionSourceKind.None)
                throw new ArgumentOutOfRangeException(nameof(sourceKind), "Action source kind must identify a concrete source.");

            return new CharacterActionExecutionRequest(
                kind,
                resolveResult,
                definition,
                sourceKind,
                priority,
                traceId,
                actionSlotId,
                CharacterActionRequestType.Any,
                CharacterActionId.None,
                CharacterActionExternalCompletionReason.None,
                lifecycleReason,
                orchestrationReason);
        }

        private static void ValidateLifecycleReason(CharacterActionLifecycleReason reason)
        {
            if (reason < CharacterActionLifecycleReason.Death
                || reason > CharacterActionLifecycleReason.ReplayRebuild)
            {
                throw new ArgumentOutOfRangeException(nameof(reason), "Lifecycle reason must identify an explicit boundary.");
            }
        }

        private static void ValidateOrchestrationReason(CharacterActionOrchestrationReason reason)
        {
            if (reason < CharacterActionOrchestrationReason.BehaviorRecipeTimelineJump
                || reason > CharacterActionOrchestrationReason.BehaviorRecipeReturnToPlanner)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(reason),
                    "Orchestration reason must identify an explicit gameplay boundary.");
            }
        }
    }

    /// <summary>Public execution state used by rollback, replay, and diagnostics restore paths.</summary>
    /// <remarks>LowFreqAlloc by design when captured or restored. It does not contain event arrays.</remarks>
    public readonly struct CharacterActionExecutionState
    {
        public CharacterActionExecutionState(
            long nextInstanceId,
            long activeActionInstanceId,
            long planId,
            CharacterActionId actionId,
            CharacterActionInstanceState state,
            int localFrame,
            CharacterActionPhaseKind currentPhase,
            int durationFrames,
            RuntimeTraceIdentity traceId,
            string finishReason,
            CharacterActionDiagnosticCode lastRejectReason)
        {
            if (!CharacterActionRunnerEnumGuards.IsDefined(state))
                throw new ArgumentOutOfRangeException(nameof(state), "Instance state is not defined.");
            if (!CharacterActionRunnerEnumGuards.IsDefined(currentPhase))
                throw new ArgumentOutOfRangeException(nameof(currentPhase), "Current phase is not defined.");

            NextInstanceId = nextInstanceId < 1L ? 1L : nextInstanceId;
            ActiveActionInstanceId = activeActionInstanceId < 0L ? 0L : activeActionInstanceId;
            PlanId = planId < 0L ? 0L : planId;
            ActionId = actionId;
            State = state;
            LocalFrame = localFrame < 0 ? 0 : localFrame;
            CurrentPhase = currentPhase;
            DurationFrames = durationFrames < 0 ? 0 : durationFrames;
            TraceId = traceId;
            FinishReason = finishReason ?? string.Empty;
            LastRejectReasonCode = lastRejectReason;
        }

        public long NextInstanceId { get; }
        public long ActiveActionInstanceId { get; }
        public long PlanId { get; }
        public CharacterActionId ActionId { get; }
        public CharacterActionInstanceState State { get; }
        public int LocalFrame { get; }
        public CharacterActionPhaseKind CurrentPhase { get; }
        public int DurationFrames { get; }
        public RuntimeTraceIdentity TraceId { get; }
        public string FinishReason { get; }
        public CharacterActionDiagnosticCode LastRejectReasonCode { get; }
        public string LastRejectReason => CharacterActionDiagnosticCodeText.Get(LastRejectReasonCode);
        public bool HasActiveAction =>
            ActiveActionInstanceId > 0L
            && ActionId.IsValid
            && State != CharacterActionInstanceState.None;

        public CharacterActionExecutionState WithResolvedPlanId(long planId)
        {
            return new CharacterActionExecutionState(
                NextInstanceId,
                ActiveActionInstanceId,
                planId,
                ActionId,
                State,
                LocalFrame,
                CurrentPhase,
                DurationFrames,
                TraceId,
                FinishReason,
                LastRejectReasonCode);
        }

        internal CharacterActionRunnerRestoreState ToRunnerState()
        {
            return new CharacterActionRunnerRestoreState(
                NextInstanceId,
                ActiveActionInstanceId,
                PlanId,
                ActionId,
                State,
                LocalFrame,
                CurrentPhase,
                DurationFrames,
                TraceId,
                FinishReason,
                LastRejectReasonCode);
        }

        internal static CharacterActionExecutionState FromRunnerState(CharacterActionRunnerRestoreState state)
        {
            return new CharacterActionExecutionState(
                state.NextInstanceId,
                state.ActiveActionInstanceId,
                state.PlanId,
                state.ActionId,
                state.State,
                state.LocalFrame,
                state.CurrentPhase,
                state.DurationFrames,
                state.TraceId,
                state.FinishReason,
                state.LastRejectReasonCode);
        }
    }

    /// <summary>Structured result returned by the CharacterAction execution authority.</summary>
    /// <remarks>
    /// NoAlloc value type. Events are stored in the caller-owned buffer. Rejected diagnostics are gateway-owned and
    /// remain valid only until the next buffered operation on this gateway.
    /// </remarks>
    public readonly struct CharacterActionExecutionResult
    {
        internal CharacterActionExecutionResult(
            CharacterActionExecutionStatus status,
            CharacterActionInstance activeInstance,
            CharacterActionRunnerEventBuffer events,
            CharacterActionDiagnostic[] diagnostics)
        {
            Status = status;
            ActiveInstance = activeInstance;
            Events = events;
            Diagnostics = diagnostics ?? Array.Empty<CharacterActionDiagnostic>();
        }

        public CharacterActionExecutionStatus Status { get; }
        public bool Accepted => Status != CharacterActionExecutionStatus.Rejected;
        public CharacterActionInstance ActiveInstance { get; }
        public CharacterActionRunnerEventBuffer Events { get; }
        public CharacterActionDiagnostic[] Diagnostics { get; }
    }

    /// <summary>The sole public execution owner for a character's CharacterAction state.</summary>
    public sealed class CharacterActionExecutionGateway
    {
        private readonly CharacterActionRunner _runner;

        public CharacterActionExecutionGateway()
        {
            _runner = new CharacterActionRunner();
        }

        public CharacterActionInstance ActiveInstance => _runner.ActiveInstance;
        public bool HasActiveInstance => _runner.HasActiveInstance;

        /// <summary>Executes a resolved action request through the appropriate transition policy.</summary>
        /// <remarks>NoAlloc after initialization when the supplied event buffer and runner storage are warm.</remarks>
        public CharacterActionExecutionResult Execute(
            in CharacterActionExecutionRequest request,
            CharacterActionRunnerEventBuffer buffer)
        {
            if (buffer == null)
                throw new ArgumentNullException(nameof(buffer));

            switch (request.Kind)
            {
                case CharacterActionExecutionRequestKind.StartSlot:
                case CharacterActionExecutionRequestKind.StartIntent:
                case CharacterActionExecutionRequestKind.React:
                    return ExecuteTargeted(in request, buffer);

                case CharacterActionExecutionRequestKind.CompleteExternal:
                    if (!HasRunningInstance())
                        return NoOp(buffer);
                    return FromOperation(
                        CharacterActionExecutionStatus.Completed,
                        _runner.CompleteExternal(
                            request.ExpectedActionId,
                            GetExternalCompletionReason(request.ExternalCompletionReason),
                            request.TraceId,
                            buffer));

                case CharacterActionExecutionRequestKind.ReplaceForLifecycle:
                {
                    bool replaced = HasRunningInstance();
                    CharacterActionRunnerBufferedOperationResult operation = _runner.ReplaceForLifecycle(
                        request.ResolveResult,
                        request.Definition,
                        GetLifecycleReason(request.LifecycleReason),
                        request.TraceId,
                        buffer);
                    return FromOperation(
                        replaced
                            ? CharacterActionExecutionStatus.LifecycleReplaced
                            : CharacterActionExecutionStatus.Started,
                        operation);
                }

                case CharacterActionExecutionRequestKind.ReplaceForOrchestration:
                {
                    bool replaced = HasRunningInstance();
                    CharacterActionRunnerBufferedOperationResult operation = _runner.ReplaceForOrchestration(
                        request.ResolveResult,
                        request.Definition,
                        GetOrchestrationReason(request.OrchestrationReason),
                        request.TraceId,
                        buffer);
                    return FromOperation(
                        replaced
                            ? CharacterActionExecutionStatus.OrchestrationReplaced
                            : CharacterActionExecutionStatus.Started,
                        operation);
                }

                case CharacterActionExecutionRequestKind.TerminateForLifecycle:
                    if (!HasRunningInstance())
                        return NoOp(buffer);
                    return FromOperation(
                        CharacterActionExecutionStatus.Terminated,
                        _runner.TerminateForLifecycle(
                            GetLifecycleReason(request.LifecycleReason),
                            request.TraceId,
                            buffer));

                case CharacterActionExecutionRequestKind.TerminateForOrchestration:
                    if (!HasRunningInstance())
                        return NoOp(buffer);
                    return FromOperation(
                        CharacterActionExecutionStatus.OrchestrationTerminated,
                        _runner.TerminateForOrchestration(
                            GetOrchestrationReason(request.OrchestrationReason),
                            request.TraceId,
                            buffer));

                case CharacterActionExecutionRequestKind.CancelOnly:
                    if (!HasRunningInstance())
                        return NoOp(buffer);
                    return FromOperation(
                        CharacterActionExecutionStatus.Cancelled,
                        _runner.RequestCancelOnly(
                            request.RequestType,
                            request.ActionSlotId,
                            request.TraceId,
                            buffer));

                default:
                    throw new ArgumentOutOfRangeException(nameof(request), "CharacterAction execution request kind is not supported.");
            }
        }

        /// <summary>Advances the active CharacterAction by one local frame.</summary>
        /// <remarks>NoAlloc after initialization when the supplied event buffer has enough capacity.</remarks>
        public CharacterActionExecutionResult Tick(CharacterActionRunnerEventBuffer buffer)
        {
            if (buffer == null)
                throw new ArgumentNullException(nameof(buffer));
            if (!HasRunningInstance())
                return NoOp(buffer);

            CharacterActionRunnerBufferedOperationResult operation = _runner.Tick(buffer);
            CharacterActionExecutionStatus status = ContainsEvent(buffer, CharacterActionRunnerEventKind.ActionFinished)
                ? CharacterActionExecutionStatus.Completed
                : CharacterActionExecutionStatus.NoOp;
            return FromOperation(status, operation);
        }

        /// <summary>Captures the current action authority state for rollback, replay, or diagnostics.</summary>
        /// <remarks>LowFreqAlloc by design.</remarks>
        public CharacterActionExecutionState CaptureState()
        {
            return CharacterActionExecutionState.FromRunnerState(_runner.CreateRestoreState());
        }

        /// <summary>Restores action authority without replaying historical track events.</summary>
        /// <remarks>LowFreqAlloc by design.</remarks>
        public bool TryRestore(
            CharacterActionExecutionState state,
            CharacterActionPlan plan,
            CharacterActionRunnerActionDefinition definition,
            out CharacterActionDiagnostic diagnostic)
        {
            return _runner.TryRestore(state.ToRunnerState(), plan, definition, out diagnostic);
        }

        /// <summary>Creates a low-frequency diagnostic snapshot.</summary>
        /// <remarks>AllocByDesign.</remarks>
        public CharacterActionDebugSnapshot CreateDebugSnapshot()
        {
            return _runner.CreateDebugSnapshot();
        }

        private CharacterActionExecutionResult ExecuteTargeted(
            in CharacterActionExecutionRequest request,
            CharacterActionRunnerEventBuffer buffer)
        {
            if (!HasRunningInstance())
            {
                return FromOperation(
                    CharacterActionExecutionStatus.Started,
                    _runner.Start(request.ResolveResult, request.Definition, buffer));
            }

            var transition = new CharacterActionTransitionRequest(
                request.ResolveResult,
                request.Definition,
                request.SourceKind,
                request.Priority,
                request.TraceId,
                request.ActionSlotId);
            if (request.Kind == CharacterActionExecutionRequestKind.React)
            {
                return FromOperation(
                    CharacterActionExecutionStatus.Interrupted,
                    _runner.RequestInterrupt(transition, buffer));
            }

            return FromOperation(
                CharacterActionExecutionStatus.Transitioned,
                _runner.RequestCancel(transition, buffer));
        }

        private bool HasRunningInstance()
        {
            return _runner.HasActiveInstance && _runner.ActiveInstance.IsRunning;
        }

        private CharacterActionExecutionResult NoOp(CharacterActionRunnerEventBuffer buffer)
        {
            buffer.Clear();
            return new CharacterActionExecutionResult(
                CharacterActionExecutionStatus.NoOp,
                _runner.ActiveInstance,
                buffer,
                Array.Empty<CharacterActionDiagnostic>());
        }

        private static CharacterActionExecutionResult FromOperation(
            CharacterActionExecutionStatus acceptedStatus,
            CharacterActionRunnerBufferedOperationResult operation)
        {
            return new CharacterActionExecutionResult(
                operation.Accepted ? acceptedStatus : CharacterActionExecutionStatus.Rejected,
                operation.ActiveInstance,
                operation.Events,
                operation.Diagnostics);
        }

        private static bool ContainsEvent(
            CharacterActionRunnerEventBuffer buffer,
            CharacterActionRunnerEventKind kind)
        {
            for (int i = 0; i < buffer.Count; i++)
            {
                if (buffer[i].Kind == kind)
                    return true;
            }

            return false;
        }

        private static string GetLifecycleReason(CharacterActionLifecycleReason reason)
        {
            switch (reason)
            {
                case CharacterActionLifecycleReason.Death:
                    return "lifecycle.death";
                case CharacterActionLifecycleReason.ReviveReset:
                    return "lifecycle.revive-reset";
                case CharacterActionLifecycleReason.EntityDespawn:
                    return "lifecycle.entity-despawn";
                case CharacterActionLifecycleReason.ConfigurationReload:
                    return "lifecycle.configuration-reload";
                case CharacterActionLifecycleReason.RuntimeShutdown:
                    return "lifecycle.runtime-shutdown";
                case CharacterActionLifecycleReason.PreviewSeek:
                    return "lifecycle.preview-seek";
                case CharacterActionLifecycleReason.ReplayRebuild:
                    return "lifecycle.replay-rebuild";
                default:
                    throw new ArgumentOutOfRangeException(nameof(reason), "Lifecycle reason is not supported.");
            }
        }

        private static string GetOrchestrationReason(CharacterActionOrchestrationReason reason)
        {
            switch (reason)
            {
                case CharacterActionOrchestrationReason.BehaviorRecipeTimelineJump:
                    return "orchestration.behavior-recipe.timeline-jump";
                case CharacterActionOrchestrationReason.BehaviorRecipeReturnToPlanner:
                    return "orchestration.behavior-recipe.return-to-planner";
                default:
                    throw new ArgumentOutOfRangeException(nameof(reason), "Orchestration reason is not supported.");
            }
        }

        private static string GetExternalCompletionReason(CharacterActionExternalCompletionReason reason)
        {
            switch (reason)
            {
                case CharacterActionExternalCompletionReason.Landed:
                    return "external.landed";
                default:
                    throw new ArgumentOutOfRangeException(nameof(reason), "External completion reason is not supported.");
            }
        }
    }
}
