using System;
using System.Collections.Generic;
using System.Globalization;
using MxFramework.Combat.Animation;

using MxFramework.Runtime;

namespace MxFramework.CharacterAction
{
    internal sealed class CharacterActionRunner
    {
        private readonly List<CharacterActionRunnerEvent> _pendingEvents = new List<CharacterActionRunnerEvent>();
        private readonly List<CharacterActionRunnerEvent> _operationEvents = new List<CharacterActionRunnerEvent>();
        private readonly List<int> _activeLifecycleTrackIndices = new List<int>();
        private long _nextInstanceId = 1L;
        private CharacterActionInstance _activeInstance;
        private CharacterActionInstance _pooledInstance;
        private readonly CharacterActionDiagnostic[] _bufferedDiagnostics = new CharacterActionDiagnostic[1];
        private CharacterActionDiagnosticCode _lastRejectReason;
        private string[] _lastFrameEvents = Array.Empty<string>();
        private bool _suppressPendingEvents;

        public CharacterActionInstance ActiveInstance => _activeInstance;
        public bool HasActiveInstance => _activeInstance != null;

        internal CharacterActionRunnerOperationResult Start(CharacterActionResolveResult resolveResult)
        {
            if (resolveResult == null)
                throw new ArgumentNullException(nameof(resolveResult));

            return Start(resolveResult, null);
        }

        internal CharacterActionRunnerOperationResult Start(
            CharacterActionResolveResult resolveResult,
            CharacterActionRunnerActionDefinition definition)
        {
            if (resolveResult == null)
                throw new ArgumentNullException(nameof(resolveResult));

            if (!resolveResult.IsSuccess)
            {
                CharacterActionDiagnostic[] diagnostics = resolveResult.Diagnostics;
                if (diagnostics == null || diagnostics.Length == 0)
                {
                    diagnostics = new[]
                    {
                        CharacterActionDiagnostic.Error(
                            resolveResult.IsQueued
                                ? CharacterActionDiagnosticCode.ActionQueued
                                : CharacterActionDiagnosticCode.MissingActionConfig,
                            "CharacterActionRunner can only start a successful resolve result.")
                    };
                }

                _lastRejectReason = diagnostics[0].CodeId;
                _lastFrameEvents = Array.Empty<string>();
                return new CharacterActionRunnerOperationResult(false, _activeInstance, Array.Empty<CharacterActionRunnerEvent>(), diagnostics);
            }

            return Start(resolveResult.Plan, definition, resolveResult.TraceId);
        }

        internal CharacterActionRunnerOperationResult Start(
            CharacterActionPlan plan,
            CharacterActionRunnerActionDefinition definition)
        {
            return Start(plan, definition, plan == null ? default : plan.TraceId);
        }

        internal CharacterActionRunnerOperationResult Start(CharacterActionPlan plan)
        {
            return Start(plan, null);
        }

        /// <remarks>NoAlloc for frames that emit no runner events; event frames return an AllocByDesign event array.</remarks>
        internal CharacterActionRunnerOperationResult Tick()
        {
            List<CharacterActionRunnerEvent> events = BeginOperationEvents();
            if (_activeInstance == null || !_activeInstance.IsRunning)
            {
                _lastFrameEvents = Array.Empty<string>();
                return new CharacterActionRunnerOperationResult(false, _activeInstance, Array.Empty<CharacterActionRunnerEvent>(), Array.Empty<CharacterActionDiagnostic>());
            }

            int nextFrame = _activeInstance.LocalFrame + 1;
            _activeInstance.SetLocalFrame(nextFrame);
            ProcessCurrentFrame(_activeInstance, events);
            UpdateLastFrameEvents(events);
            return AcceptedResult(events, Array.Empty<CharacterActionDiagnostic>());
        }

        /// <remarks>NoAlloc after initialization when the supplied buffer and runner event storage have enough capacity.</remarks>
        internal CharacterActionRunnerBufferedOperationResult Tick(CharacterActionRunnerEventBuffer buffer)
        {
            List<CharacterActionRunnerEvent> events = BeginOperationEvents(buffer);
            if (_activeInstance == null || !_activeInstance.IsRunning)
            {
                _lastFrameEvents = Array.Empty<string>();
                return new CharacterActionRunnerBufferedOperationResult(
                    false,
                    _activeInstance,
                    buffer,
                    Array.Empty<CharacterActionDiagnostic>());
            }

            int nextFrame = _activeInstance.LocalFrame + 1;
            _activeInstance.SetLocalFrame(nextFrame);
            ProcessCurrentFrame(_activeInstance, events);
            _lastFrameEvents = Array.Empty<string>();
            return BufferedAcceptedResult(buffer, Array.Empty<CharacterActionDiagnostic>());
        }

        internal CharacterActionRunnerOperationResult RequestCancel(CharacterActionTransitionRequest request)
        {
            List<CharacterActionRunnerEvent> events = BeginOperationEvents();
            CharacterActionDiagnostic diagnostic;
            if (!TryValidateTransitionRequest(request, out diagnostic))
                return RejectTransition(CharacterActionRunnerEventKind.CancelRejected, request.TraceId, diagnostic, events);

            if (_activeInstance == null || !_activeInstance.IsRunning)
            {
                diagnostic = CharacterActionDiagnostic.Error(
                    CharacterActionDiagnosticCode.CharacterCancelRejected,
                    "Cancel request requires a running action instance.");
                return RejectTransition(CharacterActionRunnerEventKind.CancelRejected, request.TraceId, diagnostic, events);
            }

            CharacterCancelConflictResult cancel = CharacterCancelConflictClassifier.Classify(
                _activeInstance.Definition.TimelineAuthority,
                _activeInstance.Definition.CancelRules,
                _activeInstance.Definition.CombatTimeline,
                _activeInstance.LocalFrame,
                request.TargetActionId,
                request.Plan.Category,
                request.ActionSlotId);

            if (!cancel.Allowed)
            {
                diagnostic = CharacterActionDiagnostic.Error(cancel.CodeId, "Cancel request rejected by " + cancel.RejectedBy + ".");
                return RejectTransition(CharacterActionRunnerEventKind.CancelRejected, request.TraceId, diagnostic, events);
            }

            CompleteActive(CharacterActionInstanceState.Cancelled, "cancelled", CharacterActionRunnerEventKind.ActionCancelled, request.TraceId, events, CharacterActionTrackExitReason.ActionReplaced);
            CharacterActionRunnerOperationResult start = StartInternal(request.Plan, request.Definition, request.TraceId, events);
            UpdateLastFrameEvents(events);
            return new CharacterActionRunnerOperationResult(start.Accepted, _activeInstance, ToEventArray(events), start.Diagnostics);
        }

        /// <remarks>NoAlloc after initialization for accepted transitions when the supplied buffer has enough capacity.</remarks>
        internal CharacterActionRunnerBufferedOperationResult RequestCancel(
            CharacterActionTransitionRequest request,
            CharacterActionRunnerEventBuffer buffer)
        {
            List<CharacterActionRunnerEvent> events = BeginOperationEvents(buffer);
            CharacterActionDiagnostic diagnostic;
            if (!TryValidateTransitionRequest(request, out diagnostic))
                return RejectTransition(CharacterActionRunnerEventKind.CancelRejected, request.TraceId, diagnostic, buffer);

            if (_activeInstance == null || !_activeInstance.IsRunning)
            {
                diagnostic = CharacterActionDiagnostic.Error(
                    CharacterActionDiagnosticCode.CharacterCancelRejected,
                    "Cancel request requires a running action instance.");
                return RejectTransition(CharacterActionRunnerEventKind.CancelRejected, request.TraceId, diagnostic, buffer);
            }

            CharacterCancelConflictResult cancel = CharacterCancelConflictClassifier.Classify(
                _activeInstance.Definition.TimelineAuthority,
                _activeInstance.Definition.CancelRules,
                _activeInstance.Definition.CombatTimeline,
                _activeInstance.LocalFrame,
                request.TargetActionId,
                request.Plan.Category,
                request.ActionSlotId);

            if (!cancel.Allowed)
            {
                diagnostic = CharacterActionDiagnostic.Error(
                    cancel.CodeId,
                    "Cancel request rejected by the active action transition policy.");
                return RejectTransition(CharacterActionRunnerEventKind.CancelRejected, request.TraceId, diagnostic, buffer);
            }

            CompleteActive(CharacterActionInstanceState.Cancelled, "cancelled", CharacterActionRunnerEventKind.ActionCancelled, request.TraceId, events, CharacterActionTrackExitReason.ActionReplaced);
            CharacterActionDiagnostic[] diagnostics;
            bool accepted = TryStartInternal(
                request.Plan,
                request.Definition,
                request.TraceId,
                events,
                out diagnostics,
                _bufferedDiagnostics);
            _lastFrameEvents = Array.Empty<string>();
            return new CharacterActionRunnerBufferedOperationResult(accepted, _activeInstance, buffer, diagnostics);
        }

        /// <summary>Exits the active action through its transition window without starting a target action.</summary>
        /// <remarks>NoAlloc after initialization when the supplied buffer has enough capacity.</remarks>
        internal CharacterActionRunnerBufferedOperationResult RequestCancelOnly(
            CharacterActionRequestType requestType,
            int actionSlotId,
            RuntimeTraceIdentity traceId,
            CharacterActionRunnerEventBuffer buffer)
        {
            List<CharacterActionRunnerEvent> events = BeginOperationEvents(buffer);
            if (_activeInstance == null || !_activeInstance.IsRunning)
            {
                CharacterActionDiagnostic diagnostic = CharacterActionDiagnostic.Error(
                    CharacterActionDiagnosticCode.CharacterCancelRejected,
                    "Cancel-only request requires a running action instance.");
                return RejectTransition(CharacterActionRunnerEventKind.CancelRejected, traceId, diagnostic, buffer);
            }

            CharacterCancelConflictResult cancel = CharacterCancelConflictClassifier.ClassifyUntargeted(
                _activeInstance.Definition.TimelineAuthority,
                _activeInstance.Definition.CancelRules,
                _activeInstance.LocalFrame,
                requestType,
                actionSlotId);
            if (!cancel.Allowed)
            {
                CharacterActionDiagnostic diagnostic = CharacterActionDiagnostic.Error(
                    cancel.CodeId,
                    "Cancel-only request rejected by the active action transition policy.");
                return RejectTransition(CharacterActionRunnerEventKind.CancelRejected, traceId, diagnostic, buffer);
            }

            CompleteActive(
                CharacterActionInstanceState.Cancelled,
                "cancelled-without-replacement",
                CharacterActionRunnerEventKind.ActionCancelled,
                traceId,
                events);
            _lastFrameEvents = Array.Empty<string>();
            return BufferedAcceptedResult(buffer, Array.Empty<CharacterActionDiagnostic>());
        }

        internal CharacterActionRunnerOperationResult RequestInterrupt(CharacterActionTransitionRequest request)
        {
            List<CharacterActionRunnerEvent> events = BeginOperationEvents();
            CharacterActionDiagnostic diagnostic;
            if (!TryValidateTransitionRequest(request, out diagnostic))
                return RejectTransition(CharacterActionRunnerEventKind.InterruptRejected, request.TraceId, diagnostic, events);

            if (_activeInstance == null || !_activeInstance.IsRunning)
            {
                diagnostic = CharacterActionDiagnostic.Error(
                    CharacterActionDiagnosticCode.CharacterCancelRejected,
                    "Interrupt request requires a running action instance.");
                return RejectTransition(CharacterActionRunnerEventKind.InterruptRejected, request.TraceId, diagnostic, events);
            }

            bool allowed = false;
            for (int i = 0; i < _activeInstance.Definition.InterruptRules.Length; i++)
            {
                CharacterInterruptRule rule = _activeInstance.Definition.InterruptRules[i];
                if (!rule.Matches(request.SourceKind, request.Priority, request.TargetActionId))
                    continue;

                if (!rule.Allow)
                {
                    diagnostic = CharacterActionDiagnostic.Error(
                        CharacterActionDiagnosticCode.CharacterCancelRejected,
                        "Interrupt request rejected by CharacterInterruptRule.");
                    return RejectTransition(CharacterActionRunnerEventKind.InterruptRejected, request.TraceId, diagnostic, events);
                }

                allowed = true;
            }

            if (!allowed)
            {
                diagnostic = CharacterActionDiagnostic.Error(
                    CharacterActionDiagnosticCode.CharacterCancelRejected,
                    "Interrupt request did not match an allowed CharacterInterruptRule.");
                return RejectTransition(CharacterActionRunnerEventKind.InterruptRejected, request.TraceId, diagnostic, events);
            }

            CompleteActive(CharacterActionInstanceState.Interrupted, "interrupted", CharacterActionRunnerEventKind.ActionInterrupted, request.TraceId, events);
            CharacterActionRunnerOperationResult start = StartInternal(request.Plan, request.Definition, request.TraceId, events);
            UpdateLastFrameEvents(events);
            return new CharacterActionRunnerOperationResult(start.Accepted, _activeInstance, ToEventArray(events), start.Diagnostics);
        }

        /// <remarks>NoAlloc after initialization for accepted transitions when the supplied buffer has enough capacity.</remarks>
        internal CharacterActionRunnerBufferedOperationResult RequestInterrupt(
            CharacterActionTransitionRequest request,
            CharacterActionRunnerEventBuffer buffer)
        {
            List<CharacterActionRunnerEvent> events = BeginOperationEvents(buffer);
            CharacterActionDiagnostic diagnostic;
            if (!TryValidateTransitionRequest(request, out diagnostic))
                return RejectTransition(CharacterActionRunnerEventKind.InterruptRejected, request.TraceId, diagnostic, buffer);

            if (_activeInstance == null || !_activeInstance.IsRunning)
            {
                diagnostic = CharacterActionDiagnostic.Error(
                    CharacterActionDiagnosticCode.CharacterCancelRejected,
                    "Interrupt request requires a running action instance.");
                return RejectTransition(CharacterActionRunnerEventKind.InterruptRejected, request.TraceId, diagnostic, buffer);
            }

            bool allowed = false;
            for (int i = 0; i < _activeInstance.Definition.InterruptRules.Length; i++)
            {
                CharacterInterruptRule rule = _activeInstance.Definition.InterruptRules[i];
                if (!rule.Matches(request.SourceKind, request.Priority, request.TargetActionId))
                    continue;

                if (!rule.Allow)
                {
                    diagnostic = CharacterActionDiagnostic.Error(
                        CharacterActionDiagnosticCode.CharacterCancelRejected,
                        "Interrupt request rejected by CharacterInterruptRule.");
                    return RejectTransition(CharacterActionRunnerEventKind.InterruptRejected, request.TraceId, diagnostic, buffer);
                }

                allowed = true;
            }

            if (!allowed)
            {
                diagnostic = CharacterActionDiagnostic.Error(
                    CharacterActionDiagnosticCode.CharacterCancelRejected,
                    "Interrupt request did not match an allowed CharacterInterruptRule.");
                return RejectTransition(CharacterActionRunnerEventKind.InterruptRejected, request.TraceId, diagnostic, buffer);
            }

            CompleteActive(CharacterActionInstanceState.Interrupted, "interrupted", CharacterActionRunnerEventKind.ActionInterrupted, request.TraceId, events);
            CharacterActionDiagnostic[] diagnostics;
            bool accepted = TryStartInternal(
                request.Plan,
                request.Definition,
                request.TraceId,
                events,
                out diagnostics,
                _bufferedDiagnostics);
            _lastFrameEvents = Array.Empty<string>();
            return new CharacterActionRunnerBufferedOperationResult(accepted, _activeInstance, buffer, diagnostics);
        }

        internal CharacterActionRunnerOperationResult CancelActive(string reason = "cancelled", RuntimeTraceIdentity traceId = default)
        {
            List<CharacterActionRunnerEvent> events = BeginOperationEvents();
            if (_activeInstance == null || !_activeInstance.IsRunning)
            {
                var diagnostic = CharacterActionDiagnostic.Error(
                    CharacterActionDiagnosticCode.CharacterCancelRejected,
                    "CancelActive requires a running action instance.");
                return RejectTransition(CharacterActionRunnerEventKind.CancelRejected, traceId, diagnostic, events);
            }

            string finishReason = string.IsNullOrEmpty(reason) ? "cancelled" : reason;
            CompleteActive(
                CharacterActionInstanceState.Cancelled,
                finishReason,
                CharacterActionRunnerEventKind.ActionCancelled,
                traceId,
                events);
            UpdateLastFrameEvents(events);
            return AcceptedResult(events, Array.Empty<CharacterActionDiagnostic>());
        }

        /// <remarks>NoAlloc after initialization for accepted cancellation when the supplied buffer has enough capacity.</remarks>
        internal CharacterActionRunnerBufferedOperationResult CancelActive(
            CharacterActionRunnerEventBuffer buffer,
            string reason = "cancelled",
            RuntimeTraceIdentity traceId = default)
        {
            List<CharacterActionRunnerEvent> events = BeginOperationEvents(buffer);
            if (_activeInstance == null || !_activeInstance.IsRunning)
            {
                var diagnostic = CharacterActionDiagnostic.Error(
                    CharacterActionDiagnosticCode.CharacterCancelRejected,
                    "CancelActive requires a running action instance.");
                return RejectTransition(CharacterActionRunnerEventKind.CancelRejected, traceId, diagnostic, buffer);
            }

            string finishReason = string.IsNullOrEmpty(reason) ? "cancelled" : reason;
            CompleteActive(
                CharacterActionInstanceState.Cancelled,
                finishReason,
                CharacterActionRunnerEventKind.ActionCancelled,
                traceId,
                events);
            _lastFrameEvents = Array.Empty<string>();
            return BufferedAcceptedResult(buffer, Array.Empty<CharacterActionDiagnostic>());
        }

        /// <summary>Completes the expected active action from an authoritative external fact.</summary>
        /// <remarks>NoAlloc after initialization when the supplied buffer has enough capacity.</remarks>
        internal CharacterActionRunnerBufferedOperationResult CompleteExternal(
            CharacterActionId expectedActionId,
            string reason,
            RuntimeTraceIdentity traceId,
            CharacterActionRunnerEventBuffer buffer)
        {
            List<CharacterActionRunnerEvent> events = BeginOperationEvents(buffer);
            if (_activeInstance == null || !_activeInstance.IsRunning)
            {
                CharacterActionDiagnostic diagnostic = CharacterActionDiagnostic.Error(
                    CharacterActionDiagnosticCode.CharacterCancelRejected,
                    "External completion requires a running action instance.");
                return RejectTransition(
                    CharacterActionRunnerEventKind.CancelRejected,
                    traceId,
                    diagnostic,
                    buffer);
            }

            if (_activeInstance.Plan.ActionId != expectedActionId)
            {
                CharacterActionDiagnostic diagnostic = CharacterActionDiagnostic.Error(
                    CharacterActionDiagnosticCode.CharacterCancelRejected,
                    "External completion expected action does not match the active action.");
                return RejectTransition(
                    CharacterActionRunnerEventKind.CancelRejected,
                    traceId,
                    diagnostic,
                    buffer);
            }

            CompleteActive(
                CharacterActionInstanceState.Finished,
                reason,
                CharacterActionRunnerEventKind.ActionFinished,
                traceId,
                events);
            _lastFrameEvents = Array.Empty<string>();
            return BufferedAcceptedResult(buffer, Array.Empty<CharacterActionDiagnostic>());
        }

        /// <summary>Atomically replaces the active action at a lifecycle boundary.</summary>
        /// <remarks>
        /// The target is validated before the active instance is mutated. After warm-up, accepted and ordinary
        /// rejected paths are NoAlloc when the supplied buffer has enough capacity.
        /// </remarks>
        internal CharacterActionRunnerBufferedOperationResult ReplaceForLifecycle(
            CharacterActionResolveResult resolveResult,
            CharacterActionRunnerActionDefinition definition,
            string reason,
            RuntimeTraceIdentity traceId,
            CharacterActionRunnerEventBuffer buffer)
        {
            return ReplaceAtExplicitBoundary(
                resolveResult,
                definition,
                CharacterActionSourceKind.Death,
                reason,
                traceId,
                buffer);
        }

        private CharacterActionRunnerBufferedOperationResult ReplaceAtExplicitBoundary(
            CharacterActionResolveResult resolveResult,
            CharacterActionRunnerActionDefinition definition,
            CharacterActionSourceKind sourceKind,
            string reason,
            RuntimeTraceIdentity traceId,
            CharacterActionRunnerEventBuffer buffer)
        {
            var request = new CharacterActionTransitionRequest(
                resolveResult,
                definition,
                sourceKind,
                resolveResult?.Plan?.Priority ?? 0,
                traceId);

            List<CharacterActionRunnerEvent> events = BeginOperationEvents(buffer);
            CharacterActionDiagnostic diagnostic;
            if (!TryValidateTransitionRequest(request, out diagnostic))
            {
                return RejectTransition(
                    CharacterActionRunnerEventKind.CancelRejected,
                    traceId,
                    diagnostic,
                    buffer);
            }

            if (_activeInstance != null && _activeInstance.IsRunning)
            {
                CompleteActive(
                    CharacterActionInstanceState.Cancelled,
                    reason,
                    CharacterActionRunnerEventKind.ActionCancelled,
                    traceId,
                    events);
            }

            CharacterActionDiagnostic[] diagnostics;
            bool accepted = TryStartInternal(
                request.Plan,
                request.Definition,
                traceId,
                events,
                out diagnostics,
                _bufferedDiagnostics);
            _lastFrameEvents = Array.Empty<string>();
            return new CharacterActionRunnerBufferedOperationResult(
                accepted,
                _activeInstance,
                buffer,
                diagnostics);
        }

        /// <summary>Atomically replaces the active action at a gameplay-orchestration boundary.</summary>
        /// <remarks>The target is validated before the active instance is mutated.</remarks>
        internal CharacterActionRunnerBufferedOperationResult ReplaceForOrchestration(
            CharacterActionResolveResult resolveResult,
            CharacterActionRunnerActionDefinition definition,
            string reason,
            RuntimeTraceIdentity traceId,
            CharacterActionRunnerEventBuffer buffer)
        {
            return ReplaceAtExplicitBoundary(
                resolveResult,
                definition,
                CharacterActionSourceKind.RuntimeAiPlanner,
                reason,
                traceId,
                buffer);
        }

        internal CharacterActionRunnerBufferedOperationResult TerminateForOrchestration(
            string reason,
            RuntimeTraceIdentity traceId,
            CharacterActionRunnerEventBuffer buffer)
        {
            return TerminateForLifecycle(reason, traceId, buffer);
        }

        /// <summary>Terminates the active action at a lifecycle boundary.</summary>
        /// <remarks>NoAlloc after initialization when the supplied buffer has enough capacity.</remarks>
        internal CharacterActionRunnerBufferedOperationResult TerminateForLifecycle(
            string reason,
            RuntimeTraceIdentity traceId,
            CharacterActionRunnerEventBuffer buffer)
        {
            return CancelActive(buffer, reason, traceId);
        }

        /// <remarks>NoAlloc after initialization; keeps the runner reusable while hiding completed state from subsequent ticks.</remarks>
        internal void ClearCompletedInstance()
        {
            if (_activeInstance != null && !_activeInstance.IsRunning)
                _activeInstance = null;

            _lastFrameEvents = Array.Empty<string>();
        }

        internal CharacterActionRunnerEvent[] DrainEvents()
        {
            CharacterActionRunnerEvent[] events = _pendingEvents.ToArray();
            _pendingEvents.Clear();
            return events;
        }

        internal CharacterActionDebugSnapshot CreateDebugSnapshot()
        {
            if (_activeInstance == null)
            {
                return new CharacterActionDebugSnapshot(
                    0,
                    0,
                    CharacterActionId.None,
                    CharacterActionInstanceState.None,
                    0,
                    CharacterActionPhaseKind.None,
                    0,
                    string.Empty,
                    _lastRejectReason,
                    _lastFrameEvents);
            }

            return new CharacterActionDebugSnapshot(
                _activeInstance.InstanceId,
                _activeInstance.Plan.PlanId,
                _activeInstance.Plan.ActionId,
                _activeInstance.State,
                _activeInstance.LocalFrame,
                _activeInstance.CurrentPhaseKind,
                _activeInstance.Plan.DurationFrames,
                _activeInstance.FinishReason,
                _lastRejectReason,
                _lastFrameEvents);
        }

        /// <summary>Captures the active runner instance state for rollback, replay, or debug restore.</summary>
        /// <remarks>LowFreqAlloc by design; this is not used by the runner tick path.</remarks>
        internal CharacterActionRunnerRestoreState CreateRestoreState()
        {
            if (_activeInstance == null)
            {
                return new CharacterActionRunnerRestoreState(
                    _nextInstanceId,
                    0L,
                    0L,
                    CharacterActionId.None,
                    CharacterActionInstanceState.None,
                    0,
                    CharacterActionPhaseKind.None,
                    0,
                    default,
                    string.Empty,
                    _lastRejectReason);
            }

            return new CharacterActionRunnerRestoreState(
                _nextInstanceId,
                _activeInstance.InstanceId,
                _activeInstance.Plan.PlanId,
                _activeInstance.Plan.ActionId,
                _activeInstance.State,
                _activeInstance.LocalFrame,
                _activeInstance.CurrentPhaseKind,
                _activeInstance.Plan.DurationFrames,
                _activeInstance.Plan.TraceId,
                _activeInstance.FinishReason,
                _lastRejectReason);
        }

        /// <summary>Restores an active runner instance without firing historical track events.</summary>
        /// <remarks>
        /// LowFreqAlloc by design for rollback, replay, and debug seek paths. The next tick advances from
        /// the restored local frame, so frame events that occurred before the snapshot are not replayed.
        /// </remarks>
        internal bool TryRestore(
            CharacterActionRunnerRestoreState state,
            CharacterActionPlan plan,
            CharacterActionRunnerActionDefinition definition,
            out CharacterActionDiagnostic diagnostic)
        {
            _pendingEvents.Clear();
            _operationEvents.Clear();
            _lastFrameEvents = Array.Empty<string>();
            _lastRejectReason = state.LastRejectReasonCode;
            _suppressPendingEvents = false;
            _nextInstanceId = Math.Max(1L, state.NextInstanceId);

            if (!state.HasActiveAction || state.State == CharacterActionInstanceState.None)
            {
                _activeInstance = null;
                _activeLifecycleTrackIndices.Clear();
                diagnostic = default;
                return true;
            }

            if (state.State != CharacterActionInstanceState.Running)
            {
                _activeInstance = null;
                _activeLifecycleTrackIndices.Clear();
                diagnostic = default;
                return true;
            }

            if (!TryValidatePlanDefinition(plan, definition, out diagnostic))
            {
                _activeInstance = null;
                _lastRejectReason = diagnostic.CodeId;
                return false;
            }

            if (plan.ActionId != state.ActionId)
            {
                diagnostic = CharacterActionDiagnostic.Error(
                    CharacterActionDiagnosticCode.MissingActionConfig,
                    "CharacterActionRunner restore state action id does not match the resolved plan.");
                _activeInstance = null;
                _lastRejectReason = diagnostic.CodeId;
                return false;
            }

            if (state.PlanId > 0L && plan.PlanId != state.PlanId)
            {
                diagnostic = CharacterActionDiagnostic.Error(
                    CharacterActionDiagnosticCode.MissingActionConfig,
                    "CharacterActionRunner restore state plan id does not match the resolved plan.");
                _activeInstance = null;
                _lastRejectReason = diagnostic.CodeId;
                return false;
            }

            CharacterActionInstance instance = RestoreOrResetInstance(
                state.ActiveActionInstanceId,
                plan,
                definition);
            instance.SetLocalFrame(state.LocalFrame);
            CharacterActionPhase phase = default;
            bool hasPhase = state.LocalFrame < plan.DurationFrames
                && TryFindPhase(plan.Phases, state.LocalFrame, out phase);
            instance.SetCurrentPhase(hasPhase ? phase : default, hasPhase);
            _activeInstance = instance;
            RebuildActiveLifecycleTracks(instance);
            _nextInstanceId = Math.Max(_nextInstanceId, state.ActiveActionInstanceId + 1L);
            diagnostic = default;
            return true;
        }

        private CharacterActionRunnerOperationResult Start(
            CharacterActionPlan plan,
            CharacterActionRunnerActionDefinition definition,
            RuntimeTraceIdentity traceId)
        {
            List<CharacterActionRunnerEvent> events = BeginOperationEvents();
            CharacterActionRunnerOperationResult result = StartInternal(plan, definition, traceId, events);
            UpdateLastFrameEvents(events);
            return result;
        }

        /// <remarks>
        /// NoAlloc after initialization when the supplied buffer has enough capacity. Diagnostics returned by a
        /// rejected buffered call are owned by the runner and remain valid only until the next buffered operation.
        /// </remarks>
        internal CharacterActionRunnerBufferedOperationResult Start(
            CharacterActionResolveResult resolveResult,
            CharacterActionRunnerActionDefinition definition,
            CharacterActionRunnerEventBuffer buffer)
        {
            if (resolveResult == null)
                throw new ArgumentNullException(nameof(resolveResult));

            if (!resolveResult.IsSuccess)
            {
                CharacterActionDiagnostic[] diagnostics = resolveResult.Diagnostics;
                if (diagnostics == null || diagnostics.Length == 0)
                {
                    diagnostics = StoreBufferedDiagnostic(CharacterActionDiagnostic.Error(
                        resolveResult.IsQueued
                            ? CharacterActionDiagnosticCode.ActionQueued
                            : CharacterActionDiagnosticCode.MissingActionConfig,
                        "CharacterActionRunner can only start a successful resolve result."));
                }

                BeginOperationEvents(buffer);
                _lastRejectReason = diagnostics[0].CodeId;
                _lastFrameEvents = Array.Empty<string>();
                return new CharacterActionRunnerBufferedOperationResult(false, _activeInstance, buffer, diagnostics);
            }

            return Start(resolveResult.Plan, definition, resolveResult.TraceId, buffer);
        }

        /// <remarks>NoAlloc after initialization for accepted starts when the supplied buffer has enough capacity.</remarks>
        internal CharacterActionRunnerBufferedOperationResult Start(
            CharacterActionPlan plan,
            CharacterActionRunnerActionDefinition definition,
            CharacterActionRunnerEventBuffer buffer)
        {
            return Start(plan, definition, plan == null ? default : plan.TraceId, buffer);
        }

        private CharacterActionRunnerBufferedOperationResult Start(
            CharacterActionPlan plan,
            CharacterActionRunnerActionDefinition definition,
            RuntimeTraceIdentity traceId,
            CharacterActionRunnerEventBuffer buffer)
        {
            List<CharacterActionRunnerEvent> events = BeginOperationEvents(buffer);
            CharacterActionDiagnostic[] diagnostics;
            bool accepted = TryStartInternal(
                plan,
                definition,
                traceId,
                events,
                out diagnostics,
                _bufferedDiagnostics);
            _lastFrameEvents = Array.Empty<string>();
            return new CharacterActionRunnerBufferedOperationResult(accepted, _activeInstance, buffer, diagnostics);
        }

        private CharacterActionRunnerOperationResult StartInternal(
            CharacterActionPlan plan,
            CharacterActionRunnerActionDefinition definition,
            RuntimeTraceIdentity traceId,
            List<CharacterActionRunnerEvent> events)
        {
            CharacterActionDiagnostic[] diagnostics;
            bool accepted = TryStartInternal(plan, definition, traceId, events, out diagnostics);
            return new CharacterActionRunnerOperationResult(accepted, _activeInstance, ToEventArray(events), diagnostics);
        }

        private bool TryStartInternal(
            CharacterActionPlan plan,
            CharacterActionRunnerActionDefinition definition,
            RuntimeTraceIdentity traceId,
            List<CharacterActionRunnerEvent> events,
            out CharacterActionDiagnostic[] diagnostics,
            CharacterActionDiagnostic[] reusableDiagnostics = null)
        {
            if (_activeInstance != null && _activeInstance.IsRunning)
            {
                var runningDiagnostic = CharacterActionDiagnostic.Error(
                    CharacterActionDiagnosticCode.CharacterCancelRejected,
                    "CharacterActionRunner cannot start a new action while another action is running; use cancel or interrupt.");
                _lastRejectReason = runningDiagnostic.CodeId;
                AddRejectedEvent(CharacterActionRunnerEventKind.CancelRejected, traceId, runningDiagnostic, events);
                diagnostics = StoreDiagnostic(runningDiagnostic, reusableDiagnostics);
                return false;
            }

            CharacterActionDiagnostic diagnostic;
            if (!TryValidatePlanDefinition(plan, definition, out diagnostic))
            {
                _lastRejectReason = diagnostic.CodeId;
                diagnostics = StoreDiagnostic(diagnostic, reusableDiagnostics);
                return false;
            }

            _lastRejectReason = CharacterActionDiagnosticCode.None;
            CharacterActionInstance instance = CreateOrResetInstance(plan, definition);
            _activeInstance = instance;
            _activeLifecycleTrackIndices.Clear();

            AddEvent(CreateEvent(
                CharacterActionRunnerEventKind.ActionStarted,
                instance,
                previousPhase: CharacterActionPhaseKind.None,
                currentPhase: CharacterActionPhaseKind.None,
                trackDispatch: CharacterActionTrackDispatchEvent.None,
                traceId: traceId),
                events);

            ProcessCurrentFrame(instance, events);
            diagnostics = Array.Empty<CharacterActionDiagnostic>();
            return true;
        }

        private static bool TryValidatePlanDefinition(
            CharacterActionPlan plan,
            CharacterActionRunnerActionDefinition definition,
            out CharacterActionDiagnostic diagnostic)
        {
            if (plan == null)
            {
                diagnostic = CharacterActionDiagnostic.Error(
                    CharacterActionDiagnosticCode.MissingActionConfig,
                    "CharacterActionRunner requires a CharacterActionPlan.");
                return false;
            }

            if (definition == null)
            {
                diagnostic = CharacterActionDiagnostic.Error(
                    CharacterActionDiagnosticCode.MissingActionConfig,
                    "CharacterActionRunner requires a runner action definition.");
                return false;
            }

            if (!definition.ActionId.IsValid)
            {
                diagnostic = CharacterActionDiagnostic.Error(
                    CharacterActionDiagnosticCode.MissingActionConfig,
                    "CharacterActionRunner requires a resolved execution definition with a stable action config id and action id.");
                return false;
            }

            if (plan.ActionId != definition.ActionId)
            {
                diagnostic = CharacterActionDiagnostic.Error(
                    CharacterActionDiagnosticCode.MissingActionConfig,
                    "Runner action definition does not match CharacterActionPlan action id.");
                return false;
            }

            if (definition.TimelineAuthority == CharacterActionTimelineAuthority.CombatAnchored && definition.CombatTimeline == null)
            {
                diagnostic = CharacterActionDiagnostic.Error(
                    CharacterActionDiagnosticCode.CombatActionMissing,
                    "CombatAnchored runner action definition requires a CombatActionTimeline.");
                return false;
            }

            if (!HasResolvedTrackEvents(plan, definition))
            {
                diagnostic = CharacterActionDiagnostic.Error(
                    CharacterActionDiagnosticCode.MissingActionConfig,
                    "Runner action definition does not include all track dispatch events declared by the resolved plan.");
                return false;
            }

            CharacterActionTrackDispatchEvent[] trackEvents = definition.TrackEvents;
            for (int i = 0; i < trackEvents.Length; i++)
            {
                CharacterActionTrackDispatchEvent candidate = trackEvents[i];
                if (!CharacterActionTrackLifecycleUtility.RequiresStatefulLifecycle(in candidate))
                    continue;
                if ((candidate.StableEventId <= 0) || candidate.EndFrame < candidate.Frame)
                {
                    diagnostic = CharacterActionDiagnostic.Error(
                        CharacterActionDiagnosticCode.InvalidTrackLifecycle,
                        "Stateful track ranges require a stable event id and an inclusive end frame.");
                    return false;
                }

                for (int j = 0; j < i; j++)
                {
                    CharacterActionTrackDispatchEvent previous = trackEvents[j];
                    if (previous.TrackKind == candidate.TrackKind
                        && (previous.StableEventId == candidate.StableEventId))
                    {
                        diagnostic = CharacterActionDiagnostic.Error(
                            CharacterActionDiagnosticCode.InvalidTrackLifecycle,
                            "Stateful track range stable event ids must be unique within an action track.");
                        return false;
                    }
                }
            }

            diagnostic = default;
            return true;
        }

        private static bool HasResolvedTrackEvents(CharacterActionPlan plan, CharacterActionRunnerActionDefinition definition)
        {
            CharacterActionTrackPlan[] tracks = plan.Tracks ?? Array.Empty<CharacterActionTrackPlan>();
            for (int i = 0; i < tracks.Length; i++)
            {
                CharacterActionTrackPlan track = tracks[i];
                if (track.EventCount <= 0)
                    continue;

                if (CountTrackEvents(definition.TrackEvents, track.Kind) < track.EventCount)
                    return false;
            }

            return true;
        }

        private static int CountTrackEvents(CharacterActionTrackDispatchEvent[] events, CharacterActionTrackKind trackKind)
        {
            events = events ?? Array.Empty<CharacterActionTrackDispatchEvent>();
            int count = 0;
            for (int i = 0; i < events.Length; i++)
            {
                if (events[i].HasEvent && events[i].TrackKind == trackKind)
                    count++;
            }

            return count;
        }

        private static bool TryValidateTransitionRequest(
            CharacterActionTransitionRequest request,
            out CharacterActionDiagnostic diagnostic)
        {
            if (request.ResolveResult == null || !request.ResolveResult.IsSuccess || request.Plan == null)
            {
                CharacterActionDiagnostic[] diagnostics = request.ResolveResult?.Diagnostics;
                if (diagnostics != null && diagnostics.Length > 0)
                {
                    diagnostic = diagnostics[0];
                    return false;
                }

                diagnostic = CharacterActionDiagnostic.Error(
                    CharacterActionDiagnosticCode.MissingActionConfig,
                    "Transition request requires a successful resolved target plan.");
                return false;
            }

            if (!TryValidatePlanDefinition(request.Plan, request.Definition, out diagnostic))
                return false;

            return true;
        }

        private CharacterActionRunnerOperationResult RejectTransition(
            CharacterActionRunnerEventKind kind,
            RuntimeTraceIdentity traceId,
            CharacterActionDiagnostic diagnostic,
            List<CharacterActionRunnerEvent> events)
        {
            _lastRejectReason = diagnostic.CodeId;
            AddRejectedEvent(kind, traceId, diagnostic, events);
            UpdateLastFrameEvents(events);
            return new CharacterActionRunnerOperationResult(false, _activeInstance, ToEventArray(events), new[] { diagnostic });
        }

        private CharacterActionRunnerBufferedOperationResult RejectTransition(
            CharacterActionRunnerEventKind kind,
            RuntimeTraceIdentity traceId,
            CharacterActionDiagnostic diagnostic,
            CharacterActionRunnerEventBuffer buffer)
        {
            List<CharacterActionRunnerEvent> events = buffer.MutableEvents;
            _lastRejectReason = diagnostic.CodeId;
            AddRejectedEvent(kind, traceId, diagnostic, events);
            _lastFrameEvents = Array.Empty<string>();
            return new CharacterActionRunnerBufferedOperationResult(
                false,
                _activeInstance,
                buffer,
                StoreBufferedDiagnostic(diagnostic));
        }

        private CharacterActionDiagnostic[] StoreBufferedDiagnostic(CharacterActionDiagnostic diagnostic)
        {
            _bufferedDiagnostics[0] = diagnostic;
            return _bufferedDiagnostics;
        }

        private static CharacterActionDiagnostic[] StoreDiagnostic(
            CharacterActionDiagnostic diagnostic,
            CharacterActionDiagnostic[] reusableDiagnostics)
        {
            if (reusableDiagnostics == null || reusableDiagnostics.Length == 0)
                return new[] { diagnostic };

            reusableDiagnostics[0] = diagnostic;
            return reusableDiagnostics;
        }

        private void AddRejectedEvent(
            CharacterActionRunnerEventKind kind,
            RuntimeTraceIdentity traceId,
            CharacterActionDiagnostic diagnostic,
            List<CharacterActionRunnerEvent> events)
        {
            if (_activeInstance != null)
            {
                AddEvent(CreateEvent(
                    kind,
                    _activeInstance,
                    _activeInstance.CurrentPhaseKind,
                    _activeInstance.CurrentPhaseKind,
                    CharacterActionTrackDispatchEvent.None,
                    diagnostic.CodeId,
                    diagnostic.Message,
                    traceId),
                    events);
                return;
            }

            AddEvent(new CharacterActionRunnerEvent(
                kind,
                0L,
                0L,
                CharacterActionId.None,
                CharacterActionInstanceState.None,
                0,
                CharacterActionPhaseKind.None,
                CharacterActionPhaseKind.None,
                CharacterActionTrackDispatchEvent.None,
                diagnostic.CodeId,
                diagnostic.Message,
                traceId),
                events);
        }

        private void ProcessCurrentFrame(CharacterActionInstance instance, List<CharacterActionRunnerEvent> events)
        {
            if (instance == null || !instance.IsRunning)
                return;

            bool withinDuration = instance.LocalFrame < instance.Plan.DurationFrames;
            CharacterActionPhase phase = default;
            bool hasPhase = withinDuration && TryFindPhase(instance.Plan.Phases, instance.LocalFrame, out phase);
            if (!hasPhase)
                phase = default;

            if (instance.HasCurrentPhase != hasPhase || (hasPhase && !instance.CurrentPhase.Equals(phase)))
            {
                CharacterActionPhaseKind previous = instance.CurrentPhaseKind;
                instance.SetCurrentPhase(phase, hasPhase);
                AddEvent(CreateEvent(
                    CharacterActionRunnerEventKind.PhaseChanged,
                    instance,
                    previous,
                    instance.CurrentPhaseKind,
                    CharacterActionTrackDispatchEvent.None),
                    events);
            }

            if (withinDuration)
                FireTrackEvents(instance, events);

            if (instance.Plan.DurationFrames == 0 || instance.LocalFrame >= instance.Plan.DurationFrames)
            {
                CompleteActive(CharacterActionInstanceState.Finished, "finished", CharacterActionRunnerEventKind.ActionFinished, instance.Plan.TraceId, events);
            }
        }

        private void FireTrackEvents(CharacterActionInstance instance, List<CharacterActionRunnerEvent> events)
        {
            CharacterActionTrackDispatchEvent[] trackEvents = instance.Definition.TrackEvents;
            for (int i = 0; i < trackEvents.Length; i++)
            {
                CharacterActionTrackDispatchEvent trackEvent = trackEvents[i];
                if (!CharacterActionTrackLifecycleUtility.IsStatefulRange(in trackEvent))
                {
                    if (trackEvent.Frame != instance.LocalFrame)
                        continue;

                    AddEvent(CreateEvent(
                        CharacterActionRunnerEventKind.TrackEventFired,
                        instance,
                        instance.CurrentPhaseKind,
                        instance.CurrentPhaseKind,
                        trackEvent),
                        events);
                    continue;
                }

                if (trackEvent.Frame == instance.LocalFrame)
                {
                    AddActiveLifecycleTrack(i);
                    AddLifecycleEvent(
                        instance,
                        trackEvent,
                        CharacterActionTrackLifecyclePhase.Enter,
                        CharacterActionTrackLifecycleBoundary.BeforeFrame,
                        CharacterActionTrackExitReason.None,
                        events);
                }
            }

            FireActiveLifecycleEvents(instance, trackEvents, events);

            for (int i = 0; i < trackEvents.Length; i++)
            {
                CharacterActionTrackDispatchEvent trackEvent = trackEvents[i];
                if (!CharacterActionTrackLifecycleUtility.IsStatefulRange(in trackEvent)
                    || trackEvent.EndFrame != instance.LocalFrame)
                {
                    continue;
                }

                AddLifecycleEvent(
                    instance,
                    trackEvent,
                    CharacterActionTrackLifecyclePhase.Exit,
                    CharacterActionTrackLifecycleBoundary.AfterFrame,
                    CharacterActionTrackExitReason.NaturalEnd,
                    events);
                _activeLifecycleTrackIndices.Remove(i);
            }
        }

        private void FireActiveLifecycleEvents(
            CharacterActionInstance instance,
            CharacterActionTrackDispatchEvent[] trackEvents,
            List<CharacterActionRunnerEvent> events)
        {
            int selectedCount = 0;
            int lastSelectedIndex = -1;
            while (true)
            {
                int nextIndex = -1;
                for (int i = 0; i < trackEvents.Length; i++)
                {
                    CharacterActionTrackDispatchEvent candidate = trackEvents[i];
                    if (!CharacterActionTrackLifecycleUtility.IsStatefulRange(in candidate)
                        || instance.LocalFrame < candidate.Frame
                        || instance.LocalFrame > candidate.EndFrame
                        || (lastSelectedIndex >= 0
                            && CompareLifecyclePriority(
                                candidate,
                                i,
                                trackEvents[lastSelectedIndex],
                                lastSelectedIndex) <= 0))
                    {
                        continue;
                    }

                    if (nextIndex < 0
                        || CompareLifecyclePriority(candidate, i, trackEvents[nextIndex], nextIndex) < 0)
                    {
                        nextIndex = i;
                    }
                }

                if (nextIndex < 0)
                    break;

                CharacterActionTrackDispatchEvent trackEvent = trackEvents[nextIndex];
                AddLifecycleEvent(
                    instance,
                    trackEvent,
                    CharacterActionTrackLifecyclePhase.Active,
                    CharacterActionTrackLifecycleBoundary.BeforeFrame,
                    CharacterActionTrackExitReason.None,
                    events);
                lastSelectedIndex = nextIndex;
                selectedCount++;
                if (selectedCount >= trackEvents.Length)
                    break;
            }
        }

        private static int CompareLifecyclePriority(
            CharacterActionTrackDispatchEvent left,
            int leftIndex,
            CharacterActionTrackDispatchEvent right,
            int rightIndex)
        {
            int track = ((int)left.TrackKind).CompareTo((int)right.TrackKind);
            if (track != 0)
                return track;
            int startFrame = left.Frame.CompareTo(right.Frame);
            if (startFrame != 0)
                return startFrame;
            int stableEventId = left.StableEventOrder.CompareTo(right.StableEventOrder);
            if (stableEventId != 0)
                return stableEventId;
            return leftIndex.CompareTo(rightIndex);
        }

        private void AddLifecycleEvent(
            CharacterActionInstance instance,
            CharacterActionTrackDispatchEvent trackEvent,
            CharacterActionTrackLifecyclePhase phase,
            CharacterActionTrackLifecycleBoundary boundary,
            CharacterActionTrackExitReason exitReason,
            List<CharacterActionRunnerEvent> events)
        {
            var lifecycle = new CharacterActionTrackLifecycleEvent(
                instance.InstanceId,
                instance.Plan.ActionId,
                instance.LocalFrame,
                phase,
                boundary,
                exitReason,
                trackEvent);
            AddEvent(CreateEvent(
                CharacterActionRunnerEventKind.TrackLifecycle,
                instance,
                instance.CurrentPhaseKind,
                instance.CurrentPhaseKind,
                trackEvent,
                trackLifecycle: lifecycle),
                events);
        }

        private void AddActiveLifecycleTrack(int trackIndex)
        {
            if (!_activeLifecycleTrackIndices.Contains(trackIndex))
                _activeLifecycleTrackIndices.Add(trackIndex);
        }

        private void ExitActiveLifecycleTracks(
            CharacterActionInstance instance,
            CharacterActionTrackExitReason exitReason,
            List<CharacterActionRunnerEvent> events)
        {
            CharacterActionTrackDispatchEvent[] trackEvents = instance.Definition.TrackEvents;
            for (int i = 0; i < _activeLifecycleTrackIndices.Count; i++)
            {
                int trackIndex = _activeLifecycleTrackIndices[i];
                if (trackIndex < 0 || trackIndex >= trackEvents.Length)
                    continue;

                AddLifecycleEvent(
                    instance,
                    trackEvents[trackIndex],
                    CharacterActionTrackLifecyclePhase.Exit,
                    CharacterActionTrackLifecycleBoundary.BeforeFrame,
                    exitReason,
                    events);
            }

            _activeLifecycleTrackIndices.Clear();
        }

        private void RebuildActiveLifecycleTracks(CharacterActionInstance instance)
        {
            _activeLifecycleTrackIndices.Clear();
            CharacterActionTrackDispatchEvent[] trackEvents = instance.Definition.TrackEvents;
            for (int i = 0; i < trackEvents.Length; i++)
            {
                CharacterActionTrackDispatchEvent trackEvent = trackEvents[i];
                if (!CharacterActionTrackLifecycleUtility.IsStatefulRange(in trackEvent))
                    continue;
                if (trackEvent.Frame <= instance.LocalFrame && instance.LocalFrame < trackEvent.EndFrame)
                    _activeLifecycleTrackIndices.Add(i);
            }
        }

        private static bool TryFindPhase(CharacterActionPhase[] phases, int localFrame, out CharacterActionPhase phase)
        {
            phases = phases ?? Array.Empty<CharacterActionPhase>();
            for (int i = 0; i < phases.Length; i++)
            {
                if (phases[i].Contains(localFrame))
                {
                    phase = phases[i];
                    return true;
                }
            }

            phase = default;
            return false;
        }

        private void CompleteActive(
            CharacterActionInstanceState state,
            string reason,
            CharacterActionRunnerEventKind eventKind,
            RuntimeTraceIdentity traceId,
            List<CharacterActionRunnerEvent> events,
            CharacterActionTrackExitReason trackExitReason = CharacterActionTrackExitReason.None)
        {
            if (_activeInstance == null || !_activeInstance.IsRunning)
                return;

            CharacterActionTrackExitReason resolvedExitReason = trackExitReason == CharacterActionTrackExitReason.None
                ? ResolveTrackExitReason(state, reason)
                : trackExitReason;
            ExitActiveLifecycleTracks(_activeInstance, resolvedExitReason, events);
            _activeInstance.Complete(state, reason);
            AddEvent(CreateEvent(
                eventKind,
                _activeInstance,
                _activeInstance.CurrentPhaseKind,
                _activeInstance.CurrentPhaseKind,
                CharacterActionTrackDispatchEvent.None,
                reason: reason,
                traceId: traceId),
                events);
        }

        private CharacterActionRunnerEvent CreateEvent(
            CharacterActionRunnerEventKind kind,
            CharacterActionInstance instance,
            CharacterActionPhaseKind previousPhase,
            CharacterActionPhaseKind currentPhase,
            CharacterActionTrackDispatchEvent trackDispatch,
            CharacterActionDiagnosticCode diagnosticCode = CharacterActionDiagnosticCode.None,
            string reason = "",
            RuntimeTraceIdentity traceId = default,
            CharacterActionTrackLifecycleEvent trackLifecycle = default)
        {
            return new CharacterActionRunnerEvent(
                kind,
                instance.InstanceId,
                instance.Plan.PlanId,
                instance.Plan.ActionId,
                instance.State,
                instance.LocalFrame,
                previousPhase,
                currentPhase,
                trackDispatch,
                diagnosticCode,
                reason,
                traceId.IsNone ? instance.Plan.TraceId : traceId,
                trackLifecycle);
        }

        private static CharacterActionTrackExitReason ResolveTrackExitReason(
            CharacterActionInstanceState state,
            string reason)
        {
            switch (reason)
            {
                case "lifecycle.death": return CharacterActionTrackExitReason.Death;
                case "lifecycle.revive-reset": return CharacterActionTrackExitReason.ReviveReset;
                case "lifecycle.entity-despawn": return CharacterActionTrackExitReason.EntityDespawn;
                case "lifecycle.configuration-reload": return CharacterActionTrackExitReason.ConfigurationReload;
                case "lifecycle.runtime-shutdown": return CharacterActionTrackExitReason.RuntimeShutdown;
                case "lifecycle.preview-seek": return CharacterActionTrackExitReason.PreviewSeek;
                case "lifecycle.replay-rebuild": return CharacterActionTrackExitReason.ReplayRebuild;
            }

            switch (state)
            {
                case CharacterActionInstanceState.Finished: return CharacterActionTrackExitReason.ActionFinished;
                case CharacterActionInstanceState.Interrupted: return CharacterActionTrackExitReason.ActionInterrupted;
                default: return CharacterActionTrackExitReason.ActionCancelled;
            }
        }

        private void AddEvent(CharacterActionRunnerEvent runnerEvent, List<CharacterActionRunnerEvent> operationEvents)
        {
            if (!_suppressPendingEvents)
            {
                _pendingEvents.Add(runnerEvent);
            }

            operationEvents.Add(runnerEvent);
        }

        private List<CharacterActionRunnerEvent> BeginOperationEvents()
        {
            _suppressPendingEvents = false;
            _operationEvents.Clear();
            return _operationEvents;
        }

        private List<CharacterActionRunnerEvent> BeginOperationEvents(CharacterActionRunnerEventBuffer buffer)
        {
            if (buffer == null)
                throw new ArgumentNullException(nameof(buffer));

            _suppressPendingEvents = true;
            buffer.Clear();
            return buffer.MutableEvents;
        }

        private CharacterActionRunnerOperationResult AcceptedResult(
            List<CharacterActionRunnerEvent> events,
            CharacterActionDiagnostic[] diagnostics)
        {
            return new CharacterActionRunnerOperationResult(true, _activeInstance, ToEventArray(events), diagnostics);
        }

        private CharacterActionRunnerBufferedOperationResult BufferedAcceptedResult(
            CharacterActionRunnerEventBuffer events,
            CharacterActionDiagnostic[] diagnostics)
        {
            return new CharacterActionRunnerBufferedOperationResult(true, _activeInstance, events, diagnostics);
        }

        private CharacterActionInstance CreateOrResetInstance(
            CharacterActionPlan plan,
            CharacterActionRunnerActionDefinition definition)
        {
            long instanceId = _nextInstanceId++;
            if (_pooledInstance == null)
                _pooledInstance = new CharacterActionInstance(instanceId, plan, definition);
            else
                _pooledInstance.Reset(instanceId, plan, definition);

            return _pooledInstance;
        }

        private CharacterActionInstance RestoreOrResetInstance(
            long instanceId,
            CharacterActionPlan plan,
            CharacterActionRunnerActionDefinition definition)
        {
            if (instanceId <= 0L)
                instanceId = 1L;

            if (_pooledInstance == null)
                _pooledInstance = new CharacterActionInstance(instanceId, plan, definition);
            else
                _pooledInstance.Reset(instanceId, plan, definition);

            return _pooledInstance;
        }

        private static CharacterActionRunnerEvent[] ToEventArray(List<CharacterActionRunnerEvent> events)
        {
            return events == null || events.Count == 0
                ? Array.Empty<CharacterActionRunnerEvent>()
                : events.ToArray();
        }

        private void UpdateLastFrameEvents(List<CharacterActionRunnerEvent> events)
        {
            if (events == null || events.Count == 0)
            {
                _lastFrameEvents = Array.Empty<string>();
                return;
            }

            var lines = new string[events.Count];
            for (int i = 0; i < events.Count; i++)
                lines[i] = events[i].ToReplayLine();
            _lastFrameEvents = lines;
        }
    }
}
