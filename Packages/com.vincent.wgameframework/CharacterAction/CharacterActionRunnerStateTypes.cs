using System;
using System.Collections.Generic;
using System.Globalization;
using MxFramework.Combat.Animation;

using MxFramework.Runtime;

namespace MxFramework.CharacterAction
{
    public sealed class CharacterActionRunnerActionDefinition
    {
        public CharacterActionRunnerActionDefinition(
            CharacterActionId actionId,
            CharacterActionTimelineAuthority timelineAuthority,
            CharacterCancelRule[] cancelRules,
            CharacterInterruptRule[] interruptRules,
            CombatActionTimeline combatTimeline,
            CharacterActionTrackDispatchEvent[] trackEvents,
            bool usesRootMotion = false,
            CharacterActionRootMotionConfig rootMotion = null,
            CharacterActionVerticalRootMotionConfig verticalRootMotion = null)
        {
            if (!actionId.IsValid)
                throw new ArgumentOutOfRangeException(nameof(actionId), "Character action id must be positive.");
            if (!Enum.IsDefined(typeof(CharacterActionTimelineAuthority), timelineAuthority))
                throw new ArgumentOutOfRangeException(nameof(timelineAuthority), "Timeline authority is not defined.");

            ActionId = actionId;
            TimelineAuthority = timelineAuthority;
            CancelRules = cancelRules ?? Array.Empty<CharacterCancelRule>();
            InterruptRules = interruptRules ?? Array.Empty<CharacterInterruptRule>();
            CombatTimeline = combatTimeline;
            TrackEvents = trackEvents ?? Array.Empty<CharacterActionTrackDispatchEvent>();
            UsesRootMotion = usesRootMotion;
            RootMotion = rootMotion ?? CharacterActionRootMotionConfig.Empty;
            VerticalRootMotion = verticalRootMotion;
        }

        public CharacterActionId ActionId { get; }
        public CharacterActionTimelineAuthority TimelineAuthority { get; }
        public CharacterCancelRule[] CancelRules { get; }
        public CharacterInterruptRule[] InterruptRules { get; }
        public CombatActionTimeline CombatTimeline { get; }
        public CharacterActionTrackDispatchEvent[] TrackEvents { get; }
        public bool UsesRootMotion { get; }
        public CharacterActionRootMotionConfig RootMotion { get; }
        public CharacterActionVerticalRootMotionConfig VerticalRootMotion { get; }

        public bool IsVerticalRootMotionActive(int frame)
        {
            for (int i = 0; i < TrackEvents.Length; i++)
            {
                CharacterActionTrackDispatchEvent evt = TrackEvents[i];
                if (evt.EventKind == CharacterActionTrackEventKind.SampleRootMotionY
                    && evt.Frame <= frame && frame <= evt.EndFrame)
                    return true;
            }
            return false;
        }

        public static CharacterActionRunnerActionDefinition FromPlan(CharacterActionPlan plan)
        {
            if (plan == null)
                throw new ArgumentNullException(nameof(plan));

            throw new NotSupportedException("CharacterActionRunner requires a resolved execution definition; plan-only definitions cannot execute tracks, cancel rules, or interrupt rules.");
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
                config.ActionId,
                config.TimelineAuthority,
                config.CancelRules,
                config.InterruptRules,
                combatTimeline,
                trackEvents.ToArray(),
                config.MotionTrack?.UsesRootMotion ?? false,
                config.MotionTrack?.RootMotion,
                config.MotionTrack?.VerticalRootMotion);
        }

        private static void AddMotionEvents(MotionTrackEvent[] events, List<CharacterActionTrackDispatchEvent> trackEvents)
        {
            for (int i = 0; i < events.Length; i++)
                trackEvents.Add(CharacterActionTrackDispatchEvent.FromMotion(events[i]));
        }

        private static void AddCombatEvents(CombatTrackEvent[] events, int fallbackCombatActionId, List<CharacterActionTrackDispatchEvent> trackEvents)
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

    /// <summary>Serializable restore state for a <see cref="CharacterActionRunner"/> active instance.</summary>
    /// <remarks>
    /// LowFreqAlloc by design when produced or consumed by rollback, replay, or debug tools.
    /// It is not part of the runner tick path and restoring it does not emit historical track events.
    /// </remarks>
    internal readonly struct CharacterActionRunnerRestoreState
    {
        public CharacterActionRunnerRestoreState(
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

        public CharacterActionRunnerRestoreState WithResolvedPlanId(long planId)
        {
            return new CharacterActionRunnerRestoreState(
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
    }

    public sealed class CharacterActionInstance
    {
        internal CharacterActionInstance(
            long instanceId,
            CharacterActionPlan plan,
            CharacterActionRunnerActionDefinition definition)
        {
            Reset(instanceId, plan, definition);
        }

        private CharacterActionPhase _currentPhase;

        public long InstanceId { get; private set; }
        public CharacterActionPlan Plan { get; private set; }
        public CharacterActionRunnerActionDefinition Definition { get; private set; }
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

        internal void Reset(
            long instanceId,
            CharacterActionPlan plan,
            CharacterActionRunnerActionDefinition definition)
        {
            InstanceId = instanceId;
            Plan = plan ?? throw new ArgumentNullException(nameof(plan));
            Definition = definition ?? throw new ArgumentNullException(nameof(definition));
            State = CharacterActionInstanceState.Running;
            LocalFrame = 0;
            HasCurrentPhase = false;
            _currentPhase = default;
            FinishReason = string.Empty;
        }

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
            CharacterActionId actionId,
            CharacterActionInstanceState state,
            int localFrame,
            CharacterActionPhaseKind previousPhase,
            CharacterActionPhaseKind currentPhase,
            CharacterActionTrackDispatchEvent trackDispatch,
            CharacterActionDiagnosticCode diagnosticCode = CharacterActionDiagnosticCode.None,
            string reason = "",
            RuntimeTraceIdentity traceId = default,
            CharacterActionTrackLifecycleEvent trackLifecycle = default)
        {
            if (!CharacterActionRunnerEnumGuards.IsDefined(kind))
                throw new ArgumentOutOfRangeException(nameof(kind), "Runner event kind is not defined.");
            if (!CharacterActionRunnerEnumGuards.IsDefined(state))
                throw new ArgumentOutOfRangeException(nameof(state), "Instance state is not defined.");
            if (!CharacterActionRunnerEnumGuards.IsDefined(previousPhase))
                throw new ArgumentOutOfRangeException(nameof(previousPhase), "Previous phase is not defined.");
            if (!CharacterActionRunnerEnumGuards.IsDefined(currentPhase))
                throw new ArgumentOutOfRangeException(nameof(currentPhase), "Current phase is not defined.");

            Kind = kind;
            InstanceId = instanceId;
            PlanId = planId;
            ActionId = actionId;
            State = state;
            LocalFrame = localFrame;
            PreviousPhase = previousPhase;
            CurrentPhase = currentPhase;
            TrackDispatch = trackDispatch;
            DiagnosticCodeId = diagnosticCode;
            Reason = reason ?? string.Empty;
            TraceId = traceId;
            TrackLifecycle = trackLifecycle;
        }

        public CharacterActionRunnerEventKind Kind { get; }
        public long InstanceId { get; }
        public long PlanId { get; }
        public CharacterActionId ActionId { get; }
        public CharacterActionInstanceState State { get; }
        public int LocalFrame { get; }
        public CharacterActionPhaseKind PreviousPhase { get; }
        public CharacterActionPhaseKind CurrentPhase { get; }
        public CharacterActionTrackDispatchEvent TrackDispatch { get; }
        public CharacterActionDiagnosticCode DiagnosticCodeId { get; }
        public string DiagnosticCode => CharacterActionDiagnosticCodeText.Get(DiagnosticCodeId);
        public string Reason { get; }
        public RuntimeTraceIdentity TraceId { get; }
        public CharacterActionTrackLifecycleEvent TrackLifecycle { get; }

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
                + " lifecyclePhase=" + (TrackLifecycle.HasEvent ? TrackLifecycle.Phase.ToString() : "-")
                + " lifecycleBoundary=" + (TrackLifecycle.HasEvent ? TrackLifecycle.Boundary.ToString() : "-")
                + " lifecycleExit=" + (TrackLifecycle.HasEvent ? TrackLifecycle.ExitReason.ToString() : "-")
                + " stableEvent=" + EmptyOrValue(TrackDispatch.StableEventId)
                + " combatActionId=" + EmptyOrValue(TrackDispatch.CombatActionId)
                + " traceProfileId=" + EmptyOrValue(TrackDispatch.TraceProfileId)
                + " equipmentSlotId=" + EmptyOrValue(TrackDispatch.EquipmentSlotId)
                + " damageInline=" + FormatDamageInline(TrackDispatch.DamagePayload)
                + " targetPolicyId=" + EmptyOrValue(TrackDispatch.TargetPolicyId)
                + " gameplayRequestId=" + EmptyOrValue(TrackDispatch.GameplayRequestId)
                + " abilityStableId=" + EmptyOrValue(TrackDispatch.AbilityStableId)
                + " animationActionKey=" + EmptyOrValue(TrackDispatch.AnimationActionKey)
                + " animationClipResourceKey=" + EmptyOrValue(TrackDispatch.AnimationClipResourceKey)
                + " animationStartOffsetFrames=" + TrackDispatch.AnimationStartOffsetFrames.ToString(CultureInfo.InvariantCulture)
                + " animationEndOffsetFrames=" + TrackDispatch.AnimationEndOffsetFrames.ToString(CultureInfo.InvariantCulture)
                + " animationPlaybackSpeed=" + TrackDispatch.AnimationPlaybackSpeed.ToString("R", CultureInfo.InvariantCulture)
                + " transitionSeconds=" + FormatTransitionSeconds(TrackDispatch)
                + " presentationCueId=" + EmptyOrValue(TrackDispatch.PresentationCueId)
                + " resourceKey=" + EmptyOrValue(TrackDispatch.ResourceKey)
                + " debugMarkerId=" + EmptyOrValue(TrackDispatch.DebugMarkerId)
                + " motionMode=" + FormatMotionMode(TrackDispatch)
                + " motionVector=" + FormatMotionVector(TrackDispatch)
                + " code=" + EmptyOrValue(DiagnosticCode)
                + " reason=" + EmptyOrValue(Reason)
                + " trace=" + EmptyOrValue(TraceId);
        }

        private static string FormatDamageInline(CharacterActionDamagePayload payload)
        {
            if (!payload.HasInlineData)
                return "-";

            return EmptyOrValue(payload.DamageTypeId)
                + "/"
                + EmptyOrValue(payload.FormulaId)
                + "/base:"
                + payload.BaseDamage.ToString(CultureInfo.InvariantCulture)
                + "/impact:"
                + payload.ImpactPower.ToString(CultureInfo.InvariantCulture)
                + "/launch:"
                + payload.LaunchPower.ToString(CultureInfo.InvariantCulture)
                + "/presentation:"
                + EmptyOrValue(payload.PresentationProfileId);
        }

        public bool Equals(CharacterActionRunnerEvent other)
        {
            return Kind == other.Kind
                && InstanceId == other.InstanceId
                && PlanId == other.PlanId
                && ActionId == other.ActionId
                && State == other.State
                && LocalFrame == other.LocalFrame
                && PreviousPhase == other.PreviousPhase
                && CurrentPhase == other.CurrentPhase
                && TrackDispatch.Equals(other.TrackDispatch)
                && TrackLifecycle.Equals(other.TrackLifecycle)
                && DiagnosticCodeId == other.DiagnosticCodeId
                && string.Equals(Reason, other.Reason, StringComparison.Ordinal)
                && TraceId.Equals(other.TraceId);
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
                hash = (hash * 397) ^ ActionId.GetHashCode();
                hash = (hash * 397) ^ (int)State;
                hash = (hash * 397) ^ LocalFrame;
                hash = (hash * 397) ^ (int)PreviousPhase;
                hash = (hash * 397) ^ (int)CurrentPhase;
                hash = (hash * 397) ^ TrackDispatch.GetHashCode();
                hash = (hash * 397) ^ TrackLifecycle.GetHashCode();
                hash = (hash * 397) ^ (int)DiagnosticCodeId;
                hash = (hash * 397) ^ (Reason == null ? 0 : Reason.GetHashCode());
                hash = (hash * 397) ^ TraceId.GetHashCode();
                return hash;
            }
        }

        private static string EmptyOrValue(int value) => value == 0 ? "-" : value.ToString(System.Globalization.CultureInfo.InvariantCulture);
        private static string EmptyOrValue(RuntimeTraceIdentity value) => value.IsNone ? "-" : value.ToString();
        private static string EmptyOrValue(string value)
        {
            if (string.IsNullOrEmpty(value))
                return "-";

            return value.Replace("\\", "\\\\").Replace("\n", "\\n").Replace("\r", "\\r");
        }

        private static string EmptyOrValue(CharacterActionId actionId)
        {
            return actionId.IsValid
                ? actionId.Value.ToString(CultureInfo.InvariantCulture)
                : "-";
        }

        private static string FormatMotionMode(CharacterActionTrackDispatchEvent trackDispatch)
        {
            if (!trackDispatch.HasEvent || trackDispatch.TrackKind != CharacterActionTrackKind.Motion)
                return "-";

            return trackDispatch.MovementMode.ToString();
        }

        private static string FormatMotionVector(CharacterActionTrackDispatchEvent trackDispatch)
        {
            if (!trackDispatch.HasEvent || trackDispatch.TrackKind != CharacterActionTrackKind.Motion)
                return "-";

            return trackDispatch.X.ToString(CultureInfo.InvariantCulture)
                + ","
                + trackDispatch.Y.ToString(CultureInfo.InvariantCulture)
                + ","
                + trackDispatch.Z.ToString(CultureInfo.InvariantCulture);
        }

        private static string FormatTransitionSeconds(CharacterActionTrackDispatchEvent trackDispatch)
        {
            if (!trackDispatch.HasEvent || trackDispatch.TrackKind != CharacterActionTrackKind.Animation)
                return "-";

            return trackDispatch.TransitionSeconds.ToString(CultureInfo.InvariantCulture);
        }
    }

    internal readonly struct CharacterActionRunnerOperationResult
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

    /// <summary>Reusable operation event buffer for noalloc runner start/tick/cancel calls.</summary>
    /// <remarks>
    /// Allocates during construction and when <see cref="EnsureCapacity"/> grows storage.
    /// Start/tick/cancel warm paths are NoAlloc when the buffer capacity is already sufficient.
    /// </remarks>
    public sealed class CharacterActionRunnerEventBuffer
    {
        private readonly List<CharacterActionRunnerEvent> _events;

        public CharacterActionRunnerEventBuffer(int capacity = 8)
        {
            _events = new List<CharacterActionRunnerEvent>(Math.Max(0, capacity));
        }

        public int Count => _events.Count;

        public CharacterActionRunnerEvent this[int index] => _events[index];

        /// <remarks>NoAlloc after initialization when requested capacity does not grow.</remarks>
        public void EnsureCapacity(int capacity)
        {
            if (capacity > _events.Capacity)
                _events.Capacity = capacity;
        }

        /// <remarks>NoAlloc.</remarks>
        public void Clear()
        {
            _events.Clear();
        }

        internal List<CharacterActionRunnerEvent> MutableEvents => _events;
    }

    /// <summary>Operation result that exposes events through a caller-owned reusable buffer.</summary>
    /// <remarks>
    /// NoAlloc on accepted and ordinary rejected warm paths. Rejected diagnostics are runner-owned and remain valid
    /// only until the next buffered runner operation.
    /// </remarks>
    internal readonly struct CharacterActionRunnerBufferedOperationResult
    {
        public CharacterActionRunnerBufferedOperationResult(
            bool accepted,
            CharacterActionInstance activeInstance,
            CharacterActionRunnerEventBuffer events,
            CharacterActionDiagnostic[] diagnostics)
        {
            Accepted = accepted;
            ActiveInstance = activeInstance;
            Events = events;
            Diagnostics = diagnostics ?? Array.Empty<CharacterActionDiagnostic>();
        }

        public bool Accepted { get; }
        public CharacterActionInstance ActiveInstance { get; }
        public CharacterActionRunnerEventBuffer Events { get; }
        public CharacterActionDiagnostic[] Diagnostics { get; }
    }

    internal readonly struct CharacterActionTransitionRequest
    {
        public CharacterActionTransitionRequest(
            CharacterActionResolveResult resolveResult,
            CharacterActionRunnerActionDefinition definition,
            CharacterActionSourceKind sourceKind,
            int priority = 0,
            RuntimeTraceIdentity traceId = default,
            int actionSlotId = 0)
        {
            if (!CharacterActionRunnerEnumGuards.IsDefined(sourceKind))
                throw new ArgumentOutOfRangeException(nameof(sourceKind), "Action source kind is not defined.");
            if (actionSlotId < 0)
                throw new ArgumentOutOfRangeException(nameof(actionSlotId), "Action slot id cannot be negative.");

            ResolveResult = resolveResult;
            Definition = definition;
            SourceKind = sourceKind;
            Priority = priority;
            TraceId = traceId.IsNone ? (resolveResult?.TraceId ?? default) : traceId;
            ActionSlotId = actionSlotId;
        }

        public CharacterActionResolveResult ResolveResult { get; }
        public CharacterActionRunnerActionDefinition Definition { get; }
        public CharacterActionSourceKind SourceKind { get; }
        public int Priority { get; }
        public RuntimeTraceIdentity TraceId { get; }
        public int ActionSlotId { get; }
        public CharacterActionPlan Plan => ResolveResult?.Plan;
        public CharacterActionId TargetActionId =>
            Definition == null ? CharacterActionId.None : Definition.ActionId;
    }

    internal static class CharacterActionRunnerEnumGuards
    {
        public static bool IsDefined(CharacterActionRunnerEventKind kind)
        {
            switch (kind)
            {
                case CharacterActionRunnerEventKind.None:
                case CharacterActionRunnerEventKind.ActionStarted:
                case CharacterActionRunnerEventKind.PhaseChanged:
                case CharacterActionRunnerEventKind.TrackEventFired:
                case CharacterActionRunnerEventKind.ActionFinished:
                case CharacterActionRunnerEventKind.ActionCancelled:
                case CharacterActionRunnerEventKind.ActionInterrupted:
                case CharacterActionRunnerEventKind.CancelRejected:
                case CharacterActionRunnerEventKind.InterruptRejected:
                case CharacterActionRunnerEventKind.TrackLifecycle:
                    return true;
                default:
                    return false;
            }
        }

        public static bool IsDefined(CharacterActionInstanceState state)
        {
            switch (state)
            {
                case CharacterActionInstanceState.None:
                case CharacterActionInstanceState.Running:
                case CharacterActionInstanceState.Finished:
                case CharacterActionInstanceState.Cancelled:
                case CharacterActionInstanceState.Interrupted:
                    return true;
                default:
                    return false;
            }
        }

        public static bool IsDefined(CharacterActionPhaseKind phase)
        {
            switch (phase)
            {
                case CharacterActionPhaseKind.None:
                case CharacterActionPhaseKind.Startup:
                case CharacterActionPhaseKind.Active:
                case CharacterActionPhaseKind.Recovery:
                case CharacterActionPhaseKind.Loop:
                case CharacterActionPhaseKind.Airborne:
                case CharacterActionPhaseKind.Landing:
                case CharacterActionPhaseKind.Channel:
                case CharacterActionPhaseKind.Hold:
                case CharacterActionPhaseKind.Exit:
                case CharacterActionPhaseKind.Custom:
                    return true;
                default:
                    return false;
            }
        }

        public static bool IsDefined(CharacterActionSourceKind sourceKind)
        {
            switch (sourceKind)
            {
                case CharacterActionSourceKind.None:
                case CharacterActionSourceKind.Command:
                case CharacterActionSourceKind.GameplayAbility:
                case CharacterActionSourceKind.LocalInput:
                case CharacterActionSourceKind.RuntimeAiPlanner:
                case CharacterActionSourceKind.Replay:
                case CharacterActionSourceKind.Scripted:
                case CharacterActionSourceKind.CohesionBreak:
                case CharacterActionSourceKind.GuardBreak:
                case CharacterActionSourceKind.ArmorBreak:
                case CharacterActionSourceKind.Hit:
                case CharacterActionSourceKind.Death:
                case CharacterActionSourceKind.Reaction:
                case CharacterActionSourceKind.Landing:
                case CharacterActionSourceKind.PlayerIntervention:
                case CharacterActionSourceKind.Debug:
                    return true;
                default:
                    return false;
            }
        }
    }

    public sealed class CharacterActionDebugSnapshot
    {
        public CharacterActionDebugSnapshot(
            long activeActionInstanceId,
            long planId,
            CharacterActionId actionId,
            CharacterActionInstanceState state,
            int localFrame,
            CharacterActionPhaseKind currentPhase,
            int durationFrames,
            string finishReason,
            CharacterActionDiagnosticCode lastRejectReason,
            string[] firedEventsThisFrame)
        {
            ActiveActionInstanceId = activeActionInstanceId;
            PlanId = planId;
            ActionId = actionId;
            ActiveActionId = ActionId;
            State = state;
            LocalFrame = localFrame;
            CurrentPhase = currentPhase;
            DurationFrames = durationFrames;
            FinishReason = finishReason ?? string.Empty;
            LastRejectReasonCode = lastRejectReason;
            FiredEventsThisFrame = firedEventsThisFrame ?? Array.Empty<string>();
        }

        public long ActiveActionInstanceId { get; }
        public long PlanId { get; }
        public CharacterActionId ActionId { get; }
        public CharacterActionId ActiveActionId { get; }
        public CharacterActionInstanceState State { get; }
        public int LocalFrame { get; }
        public CharacterActionPhaseKind CurrentPhase { get; }
        public int DurationFrames { get; }
        public string FinishReason { get; }
        public CharacterActionDiagnosticCode LastRejectReasonCode { get; }
        public string LastRejectReason => CharacterActionDiagnosticCodeText.Get(LastRejectReasonCode);
        public string[] FiredEventsThisFrame { get; }
        public bool IsActionCommitted => State == CharacterActionInstanceState.Running
            && CurrentPhase != CharacterActionPhaseKind.Startup
            && CurrentPhase != CharacterActionPhaseKind.None;
        public bool IsFinished => State == CharacterActionInstanceState.Finished
            || State == CharacterActionInstanceState.Cancelled
            || State == CharacterActionInstanceState.Interrupted;
    }

}
