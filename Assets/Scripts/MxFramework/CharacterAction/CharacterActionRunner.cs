using System;
using System.Collections.Generic;
using System.Globalization;
using MxFramework.Combat.Animation;

namespace MxFramework.CharacterAction
{
    public enum CharacterActionInstanceState
    {
        None = 0,
        Running = 1,
        Finished = 2,
        Cancelled = 3,
        Interrupted = 4,
    }

    public enum CharacterActionRunnerEventKind
    {
        None = 0,
        ActionStarted = 1,
        PhaseChanged = 2,
        TrackEventFired = 3,
        ActionFinished = 4,
        ActionCancelled = 5,
        ActionInterrupted = 6,
        CancelRejected = 7,
        InterruptRejected = 8,
    }

    public readonly struct CharacterActionTrackDispatchEvent : IEquatable<CharacterActionTrackDispatchEvent>
    {
        public static readonly CharacterActionTrackDispatchEvent None = new CharacterActionTrackDispatchEvent();

        public CharacterActionTrackDispatchEvent(
            CharacterActionTrackKind trackKind,
            CharacterActionTrackEventKind eventKind,
            int frame,
            string stableEventId = "",
            CharacterMovementMode movementMode = CharacterMovementMode.Idle,
            float x = 0f,
            float y = 0f,
            float z = 0f,
            string combatActionId = "",
            string traceProfileId = "",
            string gameplayRequestId = "",
            string abilityStableId = "",
            string animationActionKey = "",
            float transitionSeconds = 0f,
            string presentationCueId = "",
            string resourceKey = "",
            string debugMarkerId = "")
        {
            CharacterActionTrackEventValidation.ValidateFrameAndKind(frame, eventKind, trackKind);

            TrackKind = trackKind;
            EventKind = eventKind;
            Frame = frame;
            StableEventId = stableEventId ?? string.Empty;
            MovementMode = movementMode;
            X = x;
            Y = y;
            Z = z;
            CombatActionId = combatActionId ?? string.Empty;
            TraceProfileId = traceProfileId ?? string.Empty;
            GameplayRequestId = gameplayRequestId ?? string.Empty;
            AbilityStableId = abilityStableId ?? string.Empty;
            AnimationActionKey = animationActionKey ?? string.Empty;
            TransitionSeconds = transitionSeconds;
            PresentationCueId = presentationCueId ?? string.Empty;
            ResourceKey = resourceKey ?? string.Empty;
            DebugMarkerId = debugMarkerId ?? string.Empty;
        }

        public CharacterActionTrackKind TrackKind { get; }
        public CharacterActionTrackEventKind EventKind { get; }
        public int Frame { get; }
        public string StableEventId { get; }
        public CharacterMovementMode MovementMode { get; }
        public float X { get; }
        public float Y { get; }
        public float Z { get; }
        public string CombatActionId { get; }
        public string TraceProfileId { get; }
        public string GameplayRequestId { get; }
        public string AbilityStableId { get; }
        public string AnimationActionKey { get; }
        public float TransitionSeconds { get; }
        public string PresentationCueId { get; }
        public string ResourceKey { get; }
        public string DebugMarkerId { get; }
        public bool HasEvent => EventKind != CharacterActionTrackEventKind.None;

        public bool Equals(CharacterActionTrackDispatchEvent other)
        {
            return TrackKind == other.TrackKind
                && EventKind == other.EventKind
                && Frame == other.Frame
                && string.Equals(StableEventId, other.StableEventId, StringComparison.Ordinal)
                && MovementMode == other.MovementMode
                && X.Equals(other.X)
                && Y.Equals(other.Y)
                && Z.Equals(other.Z)
                && string.Equals(CombatActionId, other.CombatActionId, StringComparison.Ordinal)
                && string.Equals(TraceProfileId, other.TraceProfileId, StringComparison.Ordinal)
                && string.Equals(GameplayRequestId, other.GameplayRequestId, StringComparison.Ordinal)
                && string.Equals(AbilityStableId, other.AbilityStableId, StringComparison.Ordinal)
                && string.Equals(AnimationActionKey, other.AnimationActionKey, StringComparison.Ordinal)
                && TransitionSeconds.Equals(other.TransitionSeconds)
                && string.Equals(PresentationCueId, other.PresentationCueId, StringComparison.Ordinal)
                && string.Equals(ResourceKey, other.ResourceKey, StringComparison.Ordinal)
                && string.Equals(DebugMarkerId, other.DebugMarkerId, StringComparison.Ordinal);
        }

        public override bool Equals(object obj)
        {
            return obj is CharacterActionTrackDispatchEvent other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = (int)TrackKind;
                hash = (hash * 397) ^ (int)EventKind;
                hash = (hash * 397) ^ Frame;
                hash = (hash * 397) ^ (StableEventId == null ? 0 : StableEventId.GetHashCode());
                hash = (hash * 397) ^ (int)MovementMode;
                hash = (hash * 397) ^ X.GetHashCode();
                hash = (hash * 397) ^ Y.GetHashCode();
                hash = (hash * 397) ^ Z.GetHashCode();
                hash = (hash * 397) ^ (CombatActionId == null ? 0 : CombatActionId.GetHashCode());
                hash = (hash * 397) ^ (TraceProfileId == null ? 0 : TraceProfileId.GetHashCode());
                hash = (hash * 397) ^ (GameplayRequestId == null ? 0 : GameplayRequestId.GetHashCode());
                hash = (hash * 397) ^ (AbilityStableId == null ? 0 : AbilityStableId.GetHashCode());
                hash = (hash * 397) ^ (AnimationActionKey == null ? 0 : AnimationActionKey.GetHashCode());
                hash = (hash * 397) ^ TransitionSeconds.GetHashCode();
                hash = (hash * 397) ^ (PresentationCueId == null ? 0 : PresentationCueId.GetHashCode());
                hash = (hash * 397) ^ (ResourceKey == null ? 0 : ResourceKey.GetHashCode());
                hash = (hash * 397) ^ (DebugMarkerId == null ? 0 : DebugMarkerId.GetHashCode());
                return hash;
            }
        }

        internal static CharacterActionTrackDispatchEvent FromMotion(MotionTrackEvent trackEvent)
        {
            return new CharacterActionTrackDispatchEvent(
                CharacterActionTrackKind.Motion,
                trackEvent.Kind,
                trackEvent.Frame,
                trackEvent.StableEventId,
                trackEvent.MovementMode,
                trackEvent.X,
                trackEvent.Y,
                trackEvent.Z);
        }

        internal static CharacterActionTrackDispatchEvent FromCombat(CombatTrackEvent trackEvent, string fallbackCombatActionId)
        {
            string combatActionId = string.IsNullOrEmpty(trackEvent.CombatActionId)
                ? fallbackCombatActionId
                : trackEvent.CombatActionId;
            return new CharacterActionTrackDispatchEvent(
                CharacterActionTrackKind.Combat,
                trackEvent.Kind,
                trackEvent.Frame,
                trackEvent.StableEventId,
                combatActionId: combatActionId,
                traceProfileId: trackEvent.TraceProfileId);
        }

        internal static CharacterActionTrackDispatchEvent FromGameplay(GameplayTrackEvent trackEvent)
        {
            return new CharacterActionTrackDispatchEvent(
                CharacterActionTrackKind.Gameplay,
                trackEvent.Kind,
                trackEvent.Frame,
                trackEvent.StableEventId,
                gameplayRequestId: trackEvent.RequestId,
                abilityStableId: trackEvent.AbilityStableId);
        }

        internal static CharacterActionTrackDispatchEvent FromAnimation(AnimationTrackEvent trackEvent)
        {
            return new CharacterActionTrackDispatchEvent(
                CharacterActionTrackKind.Animation,
                trackEvent.Kind,
                trackEvent.Frame,
                trackEvent.StableEventId,
                animationActionKey: trackEvent.AnimationActionKey,
                transitionSeconds: trackEvent.TransitionSeconds);
        }

        internal static CharacterActionTrackDispatchEvent FromPresentation(PresentationTrackEvent trackEvent)
        {
            return new CharacterActionTrackDispatchEvent(
                CharacterActionTrackKind.Presentation,
                trackEvent.Kind,
                trackEvent.Frame,
                trackEvent.StableEventId,
                presentationCueId: trackEvent.CueId,
                resourceKey: trackEvent.ResourceKey);
        }

        internal static CharacterActionTrackDispatchEvent FromDebug(DebugTrackEvent trackEvent)
        {
            return new CharacterActionTrackDispatchEvent(
                CharacterActionTrackKind.Debug,
                trackEvent.Kind,
                trackEvent.Frame,
                trackEvent.StableEventId,
                debugMarkerId: trackEvent.MarkerId);
        }
    }

    public sealed class CharacterActionRunnerActionDefinition
    {
        public CharacterActionRunnerActionDefinition(
            int actionConfigId,
            string actionId,
            CharacterActionTimelineAuthority timelineAuthority,
            CharacterCancelRule[] cancelRules,
            CharacterInterruptRule[] interruptRules,
            CombatActionTimeline combatTimeline,
            CharacterActionTrackDispatchEvent[] trackEvents)
        {
            if (actionConfigId < 0)
                throw new ArgumentOutOfRangeException(nameof(actionConfigId), "Action config id cannot be negative.");
            if (!Enum.IsDefined(typeof(CharacterActionTimelineAuthority), timelineAuthority))
                throw new ArgumentOutOfRangeException(nameof(timelineAuthority), "Timeline authority is not defined.");

            ActionConfigId = actionConfigId;
            ActionId = actionId ?? string.Empty;
            TimelineAuthority = timelineAuthority;
            CancelRules = cancelRules ?? Array.Empty<CharacterCancelRule>();
            InterruptRules = interruptRules ?? Array.Empty<CharacterInterruptRule>();
            CombatTimeline = combatTimeline;
            TrackEvents = trackEvents ?? Array.Empty<CharacterActionTrackDispatchEvent>();
        }

        public int ActionConfigId { get; }
        public string ActionId { get; }
        public CharacterActionTimelineAuthority TimelineAuthority { get; }
        public CharacterCancelRule[] CancelRules { get; }
        public CharacterInterruptRule[] InterruptRules { get; }
        public CombatActionTimeline CombatTimeline { get; }
        public CharacterActionTrackDispatchEvent[] TrackEvents { get; }

        public static CharacterActionRunnerActionDefinition FromPlan(CharacterActionPlan plan)
        {
            if (plan == null)
                throw new ArgumentNullException(nameof(plan));

            return new CharacterActionRunnerActionDefinition(
                actionConfigId: 0,
                actionId: plan.ActionId,
                timelineAuthority: CharacterActionTimelineAuthority.CharacterAuthored,
                cancelRules: null,
                interruptRules: null,
                combatTimeline: null,
                trackEvents: null);
        }

        public static CharacterActionRunnerActionDefinition FromConfig(
            CharacterActionConfig config,
            CombatActionTimeline combatTimeline = null)
        {
            if (config == null)
                throw new ArgumentNullException(nameof(config));

            var trackEvents = new List<CharacterActionTrackDispatchEvent>();
            AddMotionEvents(config.MotionTrack.Events, trackEvents);
            AddCombatEvents(config.CombatTrack.Events, config.CombatTrack.CombatActionId, trackEvents);
            AddGameplayEvents(config.GameplayTrack.Events, trackEvents);
            AddAnimationEvents(config.AnimationTrack.Events, trackEvents);
            AddPresentationEvents(config.PresentationTrack.Events, trackEvents);
            AddDebugEvents(config.DebugTrack.Events, trackEvents);

            return new CharacterActionRunnerActionDefinition(
                config.Id,
                config.StableId,
                config.TimelineAuthority,
                config.CancelRules,
                config.InterruptRules,
                combatTimeline,
                trackEvents.ToArray());
        }

        private static void AddMotionEvents(MotionTrackEvent[] events, List<CharacterActionTrackDispatchEvent> trackEvents)
        {
            for (int i = 0; i < events.Length; i++)
                trackEvents.Add(CharacterActionTrackDispatchEvent.FromMotion(events[i]));
        }

        private static void AddCombatEvents(CombatTrackEvent[] events, string fallbackCombatActionId, List<CharacterActionTrackDispatchEvent> trackEvents)
        {
            for (int i = 0; i < events.Length; i++)
                trackEvents.Add(CharacterActionTrackDispatchEvent.FromCombat(events[i], fallbackCombatActionId));
        }

        private static void AddGameplayEvents(GameplayTrackEvent[] events, List<CharacterActionTrackDispatchEvent> trackEvents)
        {
            for (int i = 0; i < events.Length; i++)
                trackEvents.Add(CharacterActionTrackDispatchEvent.FromGameplay(events[i]));
        }

        private static void AddAnimationEvents(AnimationTrackEvent[] events, List<CharacterActionTrackDispatchEvent> trackEvents)
        {
            for (int i = 0; i < events.Length; i++)
                trackEvents.Add(CharacterActionTrackDispatchEvent.FromAnimation(events[i]));
        }

        private static void AddPresentationEvents(PresentationTrackEvent[] events, List<CharacterActionTrackDispatchEvent> trackEvents)
        {
            for (int i = 0; i < events.Length; i++)
                trackEvents.Add(CharacterActionTrackDispatchEvent.FromPresentation(events[i]));
        }

        private static void AddDebugEvents(DebugTrackEvent[] events, List<CharacterActionTrackDispatchEvent> trackEvents)
        {
            for (int i = 0; i < events.Length; i++)
                trackEvents.Add(CharacterActionTrackDispatchEvent.FromDebug(events[i]));
        }
    }

    public sealed class CharacterActionInstance
    {
        internal CharacterActionInstance(
            long instanceId,
            CharacterActionPlan plan,
            CharacterActionRunnerActionDefinition definition)
        {
            InstanceId = instanceId;
            Plan = plan ?? throw new ArgumentNullException(nameof(plan));
            Definition = definition ?? throw new ArgumentNullException(nameof(definition));
            State = CharacterActionInstanceState.Running;
            LocalFrame = 0;
            FinishReason = string.Empty;
        }

        private CharacterActionPhase _currentPhase;

        public long InstanceId { get; }
        public CharacterActionPlan Plan { get; }
        public CharacterActionRunnerActionDefinition Definition { get; }
        public CharacterActionInstanceState State { get; private set; }
        public int LocalFrame { get; private set; }
        public bool HasCurrentPhase { get; private set; }
        public CharacterActionPhase CurrentPhase => _currentPhase;
        public CharacterActionPhaseKind CurrentPhaseKind => HasCurrentPhase ? _currentPhase.Kind : CharacterActionPhaseKind.None;
        public string FinishReason { get; private set; }
        public bool IsRunning => State == CharacterActionInstanceState.Running;
        public bool IsFinished => State == CharacterActionInstanceState.Finished
            || State == CharacterActionInstanceState.Cancelled
            || State == CharacterActionInstanceState.Interrupted;

        internal void SetLocalFrame(int localFrame)
        {
            LocalFrame = localFrame;
        }

        internal void SetCurrentPhase(CharacterActionPhase phase, bool hasPhase)
        {
            _currentPhase = phase;
            HasCurrentPhase = hasPhase;
        }

        internal void Complete(CharacterActionInstanceState state, string reason)
        {
            if (state != CharacterActionInstanceState.Finished
                && state != CharacterActionInstanceState.Cancelled
                && state != CharacterActionInstanceState.Interrupted)
            {
                throw new ArgumentOutOfRangeException(nameof(state), "Instance can only complete into a terminal state.");
            }

            State = state;
            FinishReason = reason ?? string.Empty;
        }
    }

    public readonly struct CharacterActionRunnerEvent : IEquatable<CharacterActionRunnerEvent>
    {
        public CharacterActionRunnerEvent(
            CharacterActionRunnerEventKind kind,
            long instanceId,
            long planId,
            string actionId,
            CharacterActionInstanceState state,
            int localFrame,
            CharacterActionPhaseKind previousPhase,
            CharacterActionPhaseKind currentPhase,
            CharacterActionTrackDispatchEvent trackDispatch,
            string diagnosticCode = "",
            string reason = "",
            string traceId = "")
        {
            if (!Enum.IsDefined(typeof(CharacterActionRunnerEventKind), kind))
                throw new ArgumentOutOfRangeException(nameof(kind), "Runner event kind is not defined.");
            if (!Enum.IsDefined(typeof(CharacterActionInstanceState), state))
                throw new ArgumentOutOfRangeException(nameof(state), "Instance state is not defined.");
            if (!Enum.IsDefined(typeof(CharacterActionPhaseKind), previousPhase))
                throw new ArgumentOutOfRangeException(nameof(previousPhase), "Previous phase is not defined.");
            if (!Enum.IsDefined(typeof(CharacterActionPhaseKind), currentPhase))
                throw new ArgumentOutOfRangeException(nameof(currentPhase), "Current phase is not defined.");

            Kind = kind;
            InstanceId = instanceId;
            PlanId = planId;
            ActionId = actionId ?? string.Empty;
            State = state;
            LocalFrame = localFrame;
            PreviousPhase = previousPhase;
            CurrentPhase = currentPhase;
            TrackDispatch = trackDispatch;
            DiagnosticCode = diagnosticCode ?? string.Empty;
            Reason = reason ?? string.Empty;
            TraceId = traceId ?? string.Empty;
        }

        public CharacterActionRunnerEventKind Kind { get; }
        public long InstanceId { get; }
        public long PlanId { get; }
        public string ActionId { get; }
        public CharacterActionInstanceState State { get; }
        public int LocalFrame { get; }
        public CharacterActionPhaseKind PreviousPhase { get; }
        public CharacterActionPhaseKind CurrentPhase { get; }
        public CharacterActionTrackDispatchEvent TrackDispatch { get; }
        public string DiagnosticCode { get; }
        public string Reason { get; }
        public string TraceId { get; }

        public string ToReplayLine()
        {
            return "kind=" + Kind
                + " instance=" + InstanceId.ToString(CultureInfo.InvariantCulture)
                + " plan=" + PlanId.ToString(CultureInfo.InvariantCulture)
                + " action=" + EmptyOrValue(ActionId)
                + " state=" + State
                + " frame=" + LocalFrame.ToString(CultureInfo.InvariantCulture)
                + " previousPhase=" + PreviousPhase
                + " phase=" + CurrentPhase
                + " track=" + (TrackDispatch.HasEvent ? TrackDispatch.TrackKind.ToString() : "-")
                + " trackEvent=" + (TrackDispatch.HasEvent ? TrackDispatch.EventKind.ToString() : "-")
                + " stableEvent=" + EmptyOrValue(TrackDispatch.StableEventId)
                + " code=" + EmptyOrValue(DiagnosticCode)
                + " reason=" + EmptyOrValue(Reason)
                + " trace=" + EmptyOrValue(TraceId);
        }

        public bool Equals(CharacterActionRunnerEvent other)
        {
            return Kind == other.Kind
                && InstanceId == other.InstanceId
                && PlanId == other.PlanId
                && string.Equals(ActionId, other.ActionId, StringComparison.Ordinal)
                && State == other.State
                && LocalFrame == other.LocalFrame
                && PreviousPhase == other.PreviousPhase
                && CurrentPhase == other.CurrentPhase
                && TrackDispatch.Equals(other.TrackDispatch)
                && string.Equals(DiagnosticCode, other.DiagnosticCode, StringComparison.Ordinal)
                && string.Equals(Reason, other.Reason, StringComparison.Ordinal)
                && string.Equals(TraceId, other.TraceId, StringComparison.Ordinal);
        }

        public override bool Equals(object obj)
        {
            return obj is CharacterActionRunnerEvent other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = (int)Kind;
                hash = (hash * 397) ^ InstanceId.GetHashCode();
                hash = (hash * 397) ^ PlanId.GetHashCode();
                hash = (hash * 397) ^ (ActionId == null ? 0 : ActionId.GetHashCode());
                hash = (hash * 397) ^ (int)State;
                hash = (hash * 397) ^ LocalFrame;
                hash = (hash * 397) ^ (int)PreviousPhase;
                hash = (hash * 397) ^ (int)CurrentPhase;
                hash = (hash * 397) ^ TrackDispatch.GetHashCode();
                hash = (hash * 397) ^ (DiagnosticCode == null ? 0 : DiagnosticCode.GetHashCode());
                hash = (hash * 397) ^ (Reason == null ? 0 : Reason.GetHashCode());
                hash = (hash * 397) ^ (TraceId == null ? 0 : TraceId.GetHashCode());
                return hash;
            }
        }

        private static string EmptyOrValue(string value)
        {
            if (string.IsNullOrEmpty(value))
                return "-";

            return value.Replace("\\", "\\\\").Replace("\n", "\\n").Replace("\r", "\\r");
        }
    }

    public readonly struct CharacterActionRunnerOperationResult
    {
        public CharacterActionRunnerOperationResult(
            bool accepted,
            CharacterActionInstance activeInstance,
            CharacterActionRunnerEvent[] events,
            CharacterActionDiagnostic[] diagnostics)
        {
            Accepted = accepted;
            ActiveInstance = activeInstance;
            Events = events ?? Array.Empty<CharacterActionRunnerEvent>();
            Diagnostics = diagnostics ?? Array.Empty<CharacterActionDiagnostic>();
        }

        public bool Accepted { get; }
        public CharacterActionInstance ActiveInstance { get; }
        public CharacterActionRunnerEvent[] Events { get; }
        public CharacterActionDiagnostic[] Diagnostics { get; }
    }

    public readonly struct CharacterActionTransitionRequest
    {
        public CharacterActionTransitionRequest(
            CharacterActionResolveResult resolveResult,
            CharacterActionRunnerActionDefinition definition,
            CharacterActionSourceKind sourceKind,
            int priority = 0,
            string traceId = "")
        {
            if (!Enum.IsDefined(typeof(CharacterActionSourceKind), sourceKind))
                throw new ArgumentOutOfRangeException(nameof(sourceKind), "Action source kind is not defined.");

            ResolveResult = resolveResult;
            Definition = definition;
            SourceKind = sourceKind;
            Priority = priority;
            TraceId = traceId ?? resolveResult?.TraceId ?? string.Empty;
        }

        public CharacterActionResolveResult ResolveResult { get; }
        public CharacterActionRunnerActionDefinition Definition { get; }
        public CharacterActionSourceKind SourceKind { get; }
        public int Priority { get; }
        public string TraceId { get; }
        public CharacterActionPlan Plan => ResolveResult?.Plan;
        public int TargetActionConfigId => Definition == null ? 0 : Definition.ActionConfigId;
    }

    public sealed class CharacterActionDebugSnapshot
    {
        public CharacterActionDebugSnapshot(
            long activeActionInstanceId,
            long planId,
            string actionId,
            CharacterActionInstanceState state,
            int localFrame,
            CharacterActionPhaseKind currentPhase,
            int durationFrames,
            string finishReason,
            string lastRejectReason,
            string[] firedEventsThisFrame)
        {
            ActiveActionInstanceId = activeActionInstanceId;
            PlanId = planId;
            ActionId = actionId ?? string.Empty;
            ActiveActionId = ActionId;
            State = state;
            LocalFrame = localFrame;
            CurrentPhase = currentPhase;
            DurationFrames = durationFrames;
            FinishReason = finishReason ?? string.Empty;
            LastRejectReason = lastRejectReason ?? string.Empty;
            FiredEventsThisFrame = firedEventsThisFrame ?? Array.Empty<string>();
        }

        public long ActiveActionInstanceId { get; }
        public long PlanId { get; }
        public string ActionId { get; }
        public string ActiveActionId { get; }
        public CharacterActionInstanceState State { get; }
        public int LocalFrame { get; }
        public CharacterActionPhaseKind CurrentPhase { get; }
        public int DurationFrames { get; }
        public string FinishReason { get; }
        public string LastRejectReason { get; }
        public string[] FiredEventsThisFrame { get; }
        public bool IsActionCommitted => State == CharacterActionInstanceState.Running
            && CurrentPhase != CharacterActionPhaseKind.Startup
            && CurrentPhase != CharacterActionPhaseKind.None;
        public bool IsFinished => State == CharacterActionInstanceState.Finished
            || State == CharacterActionInstanceState.Cancelled
            || State == CharacterActionInstanceState.Interrupted;
    }

    public sealed class CharacterActionRunner
    {
        private readonly List<CharacterActionRunnerEvent> _pendingEvents = new List<CharacterActionRunnerEvent>();
        private long _nextInstanceId = 1L;
        private CharacterActionInstance _activeInstance;
        private string _lastRejectReason = string.Empty;
        private string[] _lastFrameEvents = Array.Empty<string>();

        public CharacterActionInstance ActiveInstance => _activeInstance;
        public bool HasActiveInstance => _activeInstance != null;

        public CharacterActionRunnerOperationResult Start(CharacterActionResolveResult resolveResult)
        {
            if (resolveResult == null)
                throw new ArgumentNullException(nameof(resolveResult));

            return Start(resolveResult, resolveResult.IsSuccess ? CharacterActionRunnerActionDefinition.FromPlan(resolveResult.Plan) : null);
        }

        public CharacterActionRunnerOperationResult Start(
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
                                ? CharacterActionDiagnosticCodes.ActionQueued
                                : CharacterActionDiagnosticCodes.MissingActionConfig,
                            "CharacterActionRunner can only start a successful resolve result.")
                    };
                }

                _lastRejectReason = diagnostics[0].Code;
                _lastFrameEvents = Array.Empty<string>();
                return new CharacterActionRunnerOperationResult(false, _activeInstance, Array.Empty<CharacterActionRunnerEvent>(), diagnostics);
            }

            return Start(resolveResult.Plan, definition, resolveResult.TraceId);
        }

        public CharacterActionRunnerOperationResult Start(
            CharacterActionPlan plan,
            CharacterActionRunnerActionDefinition definition)
        {
            return Start(plan, definition, plan == null ? string.Empty : plan.TraceId);
        }

        public CharacterActionRunnerOperationResult Start(CharacterActionPlan plan)
        {
            return Start(plan, plan == null ? null : CharacterActionRunnerActionDefinition.FromPlan(plan));
        }

        public CharacterActionRunnerOperationResult Tick()
        {
            var events = new List<CharacterActionRunnerEvent>();
            if (_activeInstance == null || !_activeInstance.IsRunning)
            {
                _lastFrameEvents = Array.Empty<string>();
                return new CharacterActionRunnerOperationResult(false, _activeInstance, Array.Empty<CharacterActionRunnerEvent>(), Array.Empty<CharacterActionDiagnostic>());
            }

            int nextFrame = _activeInstance.LocalFrame + 1;
            _activeInstance.SetLocalFrame(nextFrame);
            ProcessCurrentFrame(_activeInstance, events);
            UpdateLastFrameEvents(events);
            return new CharacterActionRunnerOperationResult(true, _activeInstance, events.ToArray(), Array.Empty<CharacterActionDiagnostic>());
        }

        public CharacterActionRunnerOperationResult RequestCancel(CharacterActionTransitionRequest request)
        {
            var events = new List<CharacterActionRunnerEvent>();
            CharacterActionDiagnostic diagnostic;
            if (!TryValidateTransitionRequest(request, out diagnostic))
                return RejectTransition(CharacterActionRunnerEventKind.CancelRejected, request.TraceId, diagnostic, events);

            if (_activeInstance == null || !_activeInstance.IsRunning)
            {
                diagnostic = CharacterActionDiagnostic.Error(
                    CharacterActionDiagnosticCodes.CharacterCancelRejected,
                    "Cancel request requires a running action instance.");
                return RejectTransition(CharacterActionRunnerEventKind.CancelRejected, request.TraceId, diagnostic, events);
            }

            CharacterCancelConflictResult cancel = CharacterCancelConflictClassifier.Classify(
                _activeInstance.Definition.TimelineAuthority,
                _activeInstance.Definition.CancelRules,
                _activeInstance.Definition.CombatTimeline,
                _activeInstance.LocalFrame,
                request.TargetActionConfigId,
                request.SourceKind);

            if (!cancel.Allowed)
            {
                diagnostic = CharacterActionDiagnostic.Error(cancel.Code, "Cancel request rejected by " + cancel.RejectedBy + ".");
                return RejectTransition(CharacterActionRunnerEventKind.CancelRejected, request.TraceId, diagnostic, events);
            }

            CompleteActive(CharacterActionInstanceState.Cancelled, "cancelled", CharacterActionRunnerEventKind.ActionCancelled, request.TraceId, events);
            CharacterActionRunnerOperationResult start = StartInternal(request.Plan, request.Definition, request.TraceId, events);
            UpdateLastFrameEvents(events);
            return new CharacterActionRunnerOperationResult(start.Accepted, _activeInstance, events.ToArray(), start.Diagnostics);
        }

        public CharacterActionRunnerOperationResult RequestInterrupt(CharacterActionTransitionRequest request)
        {
            var events = new List<CharacterActionRunnerEvent>();
            CharacterActionDiagnostic diagnostic;
            if (!TryValidateTransitionRequest(request, out diagnostic))
                return RejectTransition(CharacterActionRunnerEventKind.InterruptRejected, request.TraceId, diagnostic, events);

            if (_activeInstance == null || !_activeInstance.IsRunning)
            {
                diagnostic = CharacterActionDiagnostic.Error(
                    CharacterActionDiagnosticCodes.CharacterCancelRejected,
                    "Interrupt request requires a running action instance.");
                return RejectTransition(CharacterActionRunnerEventKind.InterruptRejected, request.TraceId, diagnostic, events);
            }

            bool allowed = false;
            for (int i = 0; i < _activeInstance.Definition.InterruptRules.Length; i++)
            {
                CharacterInterruptRule rule = _activeInstance.Definition.InterruptRules[i];
                if (!rule.Matches(request.SourceKind, request.Priority, request.TargetActionConfigId))
                    continue;

                if (!rule.Allow)
                {
                    diagnostic = CharacterActionDiagnostic.Error(
                        CharacterActionDiagnosticCodes.CharacterCancelRejected,
                        "Interrupt request rejected by CharacterInterruptRule.");
                    return RejectTransition(CharacterActionRunnerEventKind.InterruptRejected, request.TraceId, diagnostic, events);
                }

                allowed = true;
            }

            if (!allowed)
            {
                diagnostic = CharacterActionDiagnostic.Error(
                    CharacterActionDiagnosticCodes.CharacterCancelRejected,
                    "Interrupt request did not match an allowed CharacterInterruptRule.");
                return RejectTransition(CharacterActionRunnerEventKind.InterruptRejected, request.TraceId, diagnostic, events);
            }

            CompleteActive(CharacterActionInstanceState.Interrupted, "interrupted", CharacterActionRunnerEventKind.ActionInterrupted, request.TraceId, events);
            CharacterActionRunnerOperationResult start = StartInternal(request.Plan, request.Definition, request.TraceId, events);
            UpdateLastFrameEvents(events);
            return new CharacterActionRunnerOperationResult(start.Accepted, _activeInstance, events.ToArray(), start.Diagnostics);
        }

        public CharacterActionRunnerEvent[] DrainEvents()
        {
            CharacterActionRunnerEvent[] events = _pendingEvents.ToArray();
            _pendingEvents.Clear();
            return events;
        }

        public CharacterActionDebugSnapshot CreateDebugSnapshot()
        {
            if (_activeInstance == null)
            {
                return new CharacterActionDebugSnapshot(
                    0,
                    0,
                    string.Empty,
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

        private CharacterActionRunnerOperationResult Start(
            CharacterActionPlan plan,
            CharacterActionRunnerActionDefinition definition,
            string traceId)
        {
            var events = new List<CharacterActionRunnerEvent>();
            CharacterActionRunnerOperationResult result = StartInternal(plan, definition, traceId, events);
            UpdateLastFrameEvents(events);
            return result;
        }

        private CharacterActionRunnerOperationResult StartInternal(
            CharacterActionPlan plan,
            CharacterActionRunnerActionDefinition definition,
            string traceId,
            List<CharacterActionRunnerEvent> events)
        {
            if (_activeInstance != null && _activeInstance.IsRunning)
            {
                var runningDiagnostic = CharacterActionDiagnostic.Error(
                    CharacterActionDiagnosticCodes.CharacterCancelRejected,
                    "CharacterActionRunner cannot start a new action while another action is running; use cancel or interrupt.");
                _lastRejectReason = runningDiagnostic.Code;
                AddRejectedEvent(CharacterActionRunnerEventKind.CancelRejected, traceId, runningDiagnostic, events);
                return new CharacterActionRunnerOperationResult(false, _activeInstance, events.ToArray(), new[] { runningDiagnostic });
            }

            CharacterActionDiagnostic diagnostic;
            if (!TryValidatePlanDefinition(plan, definition, out diagnostic))
            {
                _lastRejectReason = diagnostic.Code;
                return new CharacterActionRunnerOperationResult(false, _activeInstance, events.ToArray(), new[] { diagnostic });
            }

            _lastRejectReason = string.Empty;
            var instance = new CharacterActionInstance(_nextInstanceId++, plan, definition);
            _activeInstance = instance;

            AddEvent(CreateEvent(
                CharacterActionRunnerEventKind.ActionStarted,
                instance,
                previousPhase: CharacterActionPhaseKind.None,
                currentPhase: CharacterActionPhaseKind.None,
                trackDispatch: CharacterActionTrackDispatchEvent.None,
                traceId: traceId),
                events);

            ProcessCurrentFrame(instance, events);
            return new CharacterActionRunnerOperationResult(true, _activeInstance, events.ToArray(), Array.Empty<CharacterActionDiagnostic>());
        }

        private static bool TryValidatePlanDefinition(
            CharacterActionPlan plan,
            CharacterActionRunnerActionDefinition definition,
            out CharacterActionDiagnostic diagnostic)
        {
            if (plan == null)
            {
                diagnostic = CharacterActionDiagnostic.Error(
                    CharacterActionDiagnosticCodes.MissingActionConfig,
                    "CharacterActionRunner requires a CharacterActionPlan.");
                return false;
            }

            if (definition == null)
            {
                diagnostic = CharacterActionDiagnostic.Error(
                    CharacterActionDiagnosticCodes.MissingActionConfig,
                    "CharacterActionRunner requires a runner action definition.");
                return false;
            }

            if (!string.Equals(plan.ActionId, definition.ActionId, StringComparison.Ordinal))
            {
                diagnostic = CharacterActionDiagnostic.Error(
                    CharacterActionDiagnosticCodes.MissingActionConfig,
                    "Runner action definition does not match CharacterActionPlan action id.");
                return false;
            }

            diagnostic = default;
            return true;
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
                    CharacterActionDiagnosticCodes.MissingActionConfig,
                    "Transition request requires a successful resolved target plan.");
                return false;
            }

            if (!TryValidatePlanDefinition(request.Plan, request.Definition, out diagnostic))
                return false;

            return true;
        }

        private CharacterActionRunnerOperationResult RejectTransition(
            CharacterActionRunnerEventKind kind,
            string traceId,
            CharacterActionDiagnostic diagnostic,
            List<CharacterActionRunnerEvent> events)
        {
            _lastRejectReason = diagnostic.Code;
            AddRejectedEvent(kind, traceId, diagnostic, events);
            UpdateLastFrameEvents(events);
            return new CharacterActionRunnerOperationResult(false, _activeInstance, events.ToArray(), new[] { diagnostic });
        }

        private void AddRejectedEvent(
            CharacterActionRunnerEventKind kind,
            string traceId,
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
                    diagnostic.Code,
                    diagnostic.Message,
                    traceId),
                    events);
                return;
            }

            AddEvent(new CharacterActionRunnerEvent(
                kind,
                0L,
                0L,
                string.Empty,
                CharacterActionInstanceState.None,
                0,
                CharacterActionPhaseKind.None,
                CharacterActionPhaseKind.None,
                CharacterActionTrackDispatchEvent.None,
                diagnostic.Code,
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

            if (instance.Plan.DurationFrames == 0 || instance.LocalFrame >= instance.Plan.DurationFrames - 1)
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
                if (trackEvent.Frame != instance.LocalFrame)
                    continue;

                AddEvent(CreateEvent(
                    CharacterActionRunnerEventKind.TrackEventFired,
                    instance,
                    instance.CurrentPhaseKind,
                    instance.CurrentPhaseKind,
                    trackEvent),
                    events);
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
            string traceId,
            List<CharacterActionRunnerEvent> events)
        {
            if (_activeInstance == null || !_activeInstance.IsRunning)
                return;

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
            string diagnosticCode = "",
            string reason = "",
            string traceId = "")
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
                string.IsNullOrEmpty(traceId) ? instance.Plan.TraceId : traceId);
        }

        private void AddEvent(CharacterActionRunnerEvent runnerEvent, List<CharacterActionRunnerEvent> operationEvents)
        {
            _pendingEvents.Add(runnerEvent);
            operationEvents.Add(runnerEvent);
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
