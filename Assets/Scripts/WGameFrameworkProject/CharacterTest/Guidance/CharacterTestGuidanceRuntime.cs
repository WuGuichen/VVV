using System;
using System.Collections.Generic;
using MxFramework.CharacterAction;
using MxFramework.Combat.Damage;
using MxFramework.UI;
using Newtonsoft.Json.Linq;

namespace MxFramework.Game.CharacterTest
{
    /// <summary>
    /// Keeps the authored zh-CN fallback as the project source text while delegating every
    /// translated locale and all shared UI keys to the configured localization provider.
    /// </summary>
    internal sealed class CharacterTestGuidanceTextProvider : IMxUiTextProvider
    {
        private readonly IMxUiTextProvider _inner;

        internal CharacterTestGuidanceTextProvider(IMxUiTextProvider inner)
        {
            _inner = inner ?? MxUiNullTextProvider.Instance;
        }

        public MxUiLocaleId CurrentLocale => _inner.CurrentLocale;
        public long Revision => _inner.Revision;

        public bool TryGetText(MxUiLocalizedTextRequest request, out string text)
        {
            if (string.Equals(
                    CurrentLocale.Value,
                    CharacterTestGuidanceAuthoringAdapter.SourceLocaleId,
                    StringComparison.Ordinal)
                && CharacterTestGuidanceAuthoringAdapter.IsManagedTextKey(request.Key.Value))
            {
                text = request.FallbackText ?? string.Empty;
                return !string.IsNullOrEmpty(text);
            }
            return _inner.TryGetText(request, out text);
        }
    }

    internal readonly struct CharacterTestGuidanceObservation
    {
        internal CharacterTestGuidanceObservation(
            bool hasLocalPlayer,
            int localPlayerEntityId,
            int xMillimeters,
            int zMillimeters,
            int horizontalSpeedMillimetersPerSecond,
            bool grounded,
            CharacterTestCameraMode cameraMode,
            bool targetLocked)
        {
            HasLocalPlayer = hasLocalPlayer;
            LocalPlayerEntityId = localPlayerEntityId;
            XMillimeters = xMillimeters;
            ZMillimeters = zMillimeters;
            HorizontalSpeedMillimetersPerSecond = horizontalSpeedMillimetersPerSecond;
            Grounded = grounded;
            CameraMode = cameraMode;
            TargetLocked = targetLocked;
        }

        internal bool HasLocalPlayer { get; }
        internal int LocalPlayerEntityId { get; }
        internal int XMillimeters { get; }
        internal int ZMillimeters { get; }
        internal int HorizontalSpeedMillimetersPerSecond { get; }
        internal bool Grounded { get; }
        internal CharacterTestCameraMode CameraMode { get; }
        internal bool TargetLocked { get; }
    }

    internal readonly struct CharacterTestGuidanceObservationContext
    {
        internal CharacterTestGuidanceObservationContext(
            IReadOnlyList<CharacterTestRuntimeEvent> runtimeEvents,
            IReadOnlyList<DamageTelemetryRow> damageRows,
            in CharacterTestGuidanceObservation observation)
        {
            RuntimeEvents = runtimeEvents;
            DamageRows = damageRows;
            Observation = observation;
        }

        internal IReadOnlyList<CharacterTestRuntimeEvent> RuntimeEvents { get; }
        internal IReadOnlyList<DamageTelemetryRow> DamageRows { get; }
        internal CharacterTestGuidanceObservation Observation { get; }
    }

    internal enum CharacterTestGuidanceCompletionOutcomeKind : byte
    {
        None = 0,
        Completed = 1,
        Rejected = 2,
        Context = 3
    }

    internal readonly struct CharacterTestGuidanceCompletionOutcome
    {
        private CharacterTestGuidanceCompletionOutcome(
            CharacterTestGuidanceCompletionOutcomeKind kind,
            string messageId,
            string textKey,
            string fallback)
        {
            Kind = kind;
            MessageId = messageId ?? string.Empty;
            TextKey = textKey ?? string.Empty;
            Fallback = fallback ?? string.Empty;
        }

        internal CharacterTestGuidanceCompletionOutcomeKind Kind { get; }
        internal string MessageId { get; }
        internal string TextKey { get; }
        internal string Fallback { get; }
        internal bool HasValue => Kind != CharacterTestGuidanceCompletionOutcomeKind.None;

        internal static CharacterTestGuidanceCompletionOutcome Completed(
            string messageId,
            string fallback = "",
            string textKey = "")
        {
            return new CharacterTestGuidanceCompletionOutcome(
                CharacterTestGuidanceCompletionOutcomeKind.Completed,
                messageId,
                textKey,
                fallback);
        }

        internal static CharacterTestGuidanceCompletionOutcome Rejected(
            string messageId,
            string textKey,
            string fallback)
        {
            return new CharacterTestGuidanceCompletionOutcome(
                CharacterTestGuidanceCompletionOutcomeKind.Rejected,
                messageId,
                textKey,
                fallback);
        }

        internal static CharacterTestGuidanceCompletionOutcome Context(
            string messageId,
            string textKey,
            string fallback)
        {
            return new CharacterTestGuidanceCompletionOutcome(
                CharacterTestGuidanceCompletionOutcomeKind.Context,
                messageId,
                textKey,
                fallback);
        }
    }

    internal interface ICharacterTestGuidanceCompletionObserver
    {
        void Prime(in CharacterTestGuidanceObservationContext context);

        CharacterTestGuidanceCompletionOutcome Observe(
            in CharacterTestGuidanceObservationContext context);

        void Reset();
    }

    internal readonly struct CharacterTestGuidanceSelectionSnapshot
    {
        internal CharacterTestGuidanceSelectionSnapshot(
            string selectedStableId,
            int selectedStepIndex,
            int stepCount,
            bool completed,
            float autoAdvanceRemainingSeconds)
        {
            SelectedStableId = selectedStableId ?? string.Empty;
            SelectedStepIndex = selectedStepIndex;
            StepCount = stepCount;
            Completed = completed;
            AutoAdvanceRemainingSeconds = autoAdvanceRemainingSeconds;
        }

        internal string SelectedStableId { get; }
        internal int SelectedStepIndex { get; }
        internal int StepCount { get; }
        internal bool Completed { get; }
        internal float AutoAdvanceRemainingSeconds { get; }
    }

    /// <summary>
    /// Owns only project-level guidance sequence state and delegates completion facts to typed
    /// observers. It never mutates CharacterTest gameplay state.
    /// </summary>
    internal sealed class CharacterTestGuidanceSequenceCoordinator
    {
        private readonly CharacterTestGuidancePlan _plan;
        private ICharacterTestGuidanceCompletionObserver _observer;
        private int _selectedStepIndex;
        private float _autoAdvanceRemainingSeconds;
        private bool _completed;
        private bool _autoAdvancePending;

        internal CharacterTestGuidanceSequenceCoordinator(CharacterTestGuidancePlan plan)
        {
            _plan = plan ?? throw new ArgumentNullException(nameof(plan));
            if (!plan.Enabled || plan.StepCount == 0)
                throw new ArgumentException("Guidance plan must contain an enabled sequence.", nameof(plan));
            _selectedStepIndex = 0;
        }

        internal CharacterTestGuidanceStepDefinition CurrentStep =>
            _plan.Steps[_selectedStepIndex];

        internal int SelectedStepIndex => _selectedStepIndex;
        internal int StepCount => _plan.StepCount;
        internal bool Completed => _completed;
        internal float AutoAdvanceRemainingSeconds => _autoAdvanceRemainingSeconds;

        internal CharacterTestGuidanceSelectionSnapshot CreateSelectionSnapshot()
        {
            return new CharacterTestGuidanceSelectionSnapshot(
                CurrentStep.StableId,
                _selectedStepIndex,
                _plan.StepCount,
                _completed,
                _autoAdvanceRemainingSeconds);
        }

        internal void Prime(in CharacterTestGuidanceObservationContext context)
        {
            ActivateStep(0, in context);
        }

        internal bool TrySelectStableId(
            string stableId,
            in CharacterTestGuidanceObservationContext context)
        {
            if (string.IsNullOrWhiteSpace(stableId))
                return false;
            for (int i = 0; i < _plan.StepCount; i++)
            {
                if (!string.Equals(_plan.Steps[i].StableId, stableId, StringComparison.Ordinal))
                    continue;
                ActivateStep(i, in context);
                return true;
            }
            return false;
        }

        internal bool TrySelectPrevious(in CharacterTestGuidanceObservationContext context)
        {
            if (_selectedStepIndex <= 0)
                return false;
            ActivateStep(_selectedStepIndex - 1, in context);
            return true;
        }

        internal bool TrySelectNext(in CharacterTestGuidanceObservationContext context)
        {
            if (_selectedStepIndex >= _plan.StepCount - 1)
                return false;
            ActivateStep(_selectedStepIndex + 1, in context);
            return true;
        }

        internal bool TryObserve(
            in CharacterTestGuidanceObservationContext context,
            out CharacterTestGuidanceCompletionOutcome outcome)
        {
            if (_completed || _observer == null)
            {
                outcome = default;
                return false;
            }

            outcome = _observer.Observe(in context);
            if (!outcome.HasValue)
                return false;
            if (outcome.Kind == CharacterTestGuidanceCompletionOutcomeKind.Completed)
            {
                _completed = true;
                _autoAdvanceRemainingSeconds = Math.Max(0f, CurrentStep.AutoAdvanceSeconds);
                _autoAdvancePending = _selectedStepIndex < _plan.StepCount - 1;
            }
            return true;
        }

        internal bool TryAdvance(
            float deltaTime,
            in CharacterTestGuidanceObservationContext context)
        {
            if (!_completed || !_autoAdvancePending)
                return false;

            _autoAdvanceRemainingSeconds = Math.Max(
                0f,
                _autoAdvanceRemainingSeconds - Math.Max(0f, deltaTime));
            if (_autoAdvanceRemainingSeconds > 0f)
                return false;

            ActivateStep(_selectedStepIndex + 1, in context);
            return true;
        }

        internal void Reset()
        {
            _observer?.Reset();
            _observer = null;
            _selectedStepIndex = 0;
            _autoAdvanceRemainingSeconds = 0f;
            _completed = false;
            _autoAdvancePending = false;
        }

        private void ActivateStep(
            int stepIndex,
            in CharacterTestGuidanceObservationContext context)
        {
            _observer?.Reset();
            _selectedStepIndex = Math.Max(0, Math.Min(_plan.StepCount - 1, stepIndex));
            _completed = false;
            _autoAdvanceRemainingSeconds = 0f;
            _autoAdvancePending = false;
            _observer = CharacterTestGuidanceCompletionRegistry.CreateObserver(CurrentStep);
            _observer.Prime(in context);
        }
    }

    /// <summary>Projects compiled content and coordinator state to the shared Guidance DTOs.</summary>
    internal sealed class CharacterTestGuidanceSnapshotMapper
    {
        internal static readonly MxUiGuidanceScopeId ScopeId =
            new MxUiGuidanceScopeId("character_test.guidance.sequence");

        private readonly CharacterTestGuidancePlan _plan;
        private MxUiLocalizedTextRequest[] _stepTitlePrompts;
        private MxUiLocalizedTextRequest[] _stepActionPrompts;
        private long _cardRevision;

        internal CharacterTestGuidanceSnapshotMapper(CharacterTestGuidancePlan plan)
        {
            _plan = plan ?? throw new ArgumentNullException(nameof(plan));
            _stepTitlePrompts = Array.Empty<MxUiLocalizedTextRequest>();
            _stepActionPrompts = Array.Empty<MxUiLocalizedTextRequest>();
        }

        internal void SetStepTitlePrompts(IReadOnlyList<MxUiLocalizedTextRequest> prompts)
        {
            _stepTitlePrompts = new MxUiLocalizedTextRequest[_plan.StepCount];
            for (int i = 0; i < _stepTitlePrompts.Length; i++)
            {
                MxUiLocalizedTextRequest prompt = prompts != null && i < prompts.Count
                    ? prompts[i]
                    : default;
                CharacterTestGuidanceStepDefinition step = _plan.Steps[i];
                _stepTitlePrompts[i] = prompt.IsValid
                    ? prompt
                    : Text(step.Content.Title.Key, step.Content.Title.Fallback);
            }
        }

        internal void SetStepActionPrompts(IReadOnlyList<MxUiLocalizedTextRequest> prompts)
        {
            _stepActionPrompts = new MxUiLocalizedTextRequest[_plan.StepCount];
            for (int i = 0; i < _stepActionPrompts.Length; i++)
            {
                MxUiLocalizedTextRequest prompt = prompts != null && i < prompts.Count
                    ? prompts[i]
                    : default;
                CharacterTestGuidanceStepDefinition step = _plan.Steps[i];
                _stepActionPrompts[i] = prompt.IsValid
                    ? prompt
                    : Text(step.Content.ActionInstruction.Key, step.Content.ActionInstruction.Fallback);
            }
        }

        internal MxUiGuidanceCardSnapshot CreateCurrentCard(
            CharacterTestGuidanceSequenceCoordinator coordinator,
            in CharacterTestGuidanceObservation observation)
        {
            return CreateCard(
                coordinator,
                in observation,
                coordinator.Completed
                    ? CharacterTestGuidanceCardState.Completed
                    : ResolveWaitingState(coordinator.CurrentStep, in observation));
        }

        internal bool TryMapOutcome(
            CharacterTestGuidanceSequenceCoordinator coordinator,
            in CharacterTestGuidanceObservation observation,
            in CharacterTestGuidanceCompletionOutcome outcome,
            out MxUiGuidanceCardSnapshot card,
            out MxUiGuidanceNotice notice)
        {
            switch (outcome.Kind)
            {
                case CharacterTestGuidanceCompletionOutcomeKind.Completed:
                    card = CreateCard(
                        coordinator,
                        in observation,
                        CharacterTestGuidanceCardState.Completed);
                    CharacterTestGuidanceStepDefinition step = coordinator.CurrentStep;
                    notice = CreateNotice(
                        string.IsNullOrWhiteSpace(outcome.MessageId)
                            ? step.StableId + ".completed"
                            : outcome.MessageId,
                        MxUiGuidanceTone.Success,
                        MxUiGuidanceGlyph.Success,
                        string.IsNullOrWhiteSpace(outcome.TextKey)
                            ? step.Content.SuccessNotice.Key
                            : outcome.TextKey,
                        string.IsNullOrWhiteSpace(outcome.Fallback)
                            ? step.Content.SuccessNotice.Fallback
                            : outcome.Fallback);
                    return true;
                case CharacterTestGuidanceCompletionOutcomeKind.Rejected:
                    card = CreateCard(
                        coordinator,
                        in observation,
                        CharacterTestGuidanceCardState.Rejected);
                    notice = CreateNotice(
                        outcome.MessageId,
                        MxUiGuidanceTone.Warning,
                        MxUiGuidanceGlyph.Warning,
                        outcome.TextKey,
                        outcome.Fallback);
                    return true;
                case CharacterTestGuidanceCompletionOutcomeKind.Context:
                    card = CreateCard(
                        coordinator,
                        in observation,
                        ResolveWaitingState(coordinator.CurrentStep, in observation));
                    notice = CreateNotice(
                        outcome.MessageId,
                        MxUiGuidanceTone.Info,
                        MxUiGuidanceGlyph.Context,
                        outcome.TextKey,
                        outcome.Fallback);
                    return true;
                default:
                    card = default;
                    notice = default;
                    return false;
            }
        }

        internal void Reset()
        {
            _cardRevision = 0L;
            _stepTitlePrompts = Array.Empty<MxUiLocalizedTextRequest>();
            _stepActionPrompts = Array.Empty<MxUiLocalizedTextRequest>();
        }

        private MxUiGuidanceCardSnapshot CreateCard(
            CharacterTestGuidanceSequenceCoordinator coordinator,
            in CharacterTestGuidanceObservation observation,
            CharacterTestGuidanceCardState state)
        {
            CharacterTestGuidanceStepDefinition step = coordinator.CurrentStep;
            MxUiGuidanceTone statusTone;
            MxUiGuidanceGlyph statusGlyph;
            MxUiLocalizedTextRequest statusText;
            switch (state)
            {
                case CharacterTestGuidanceCardState.Completed:
                    statusTone = MxUiGuidanceTone.Success;
                    statusGlyph = MxUiGuidanceGlyph.Success;
                    statusText = Text(
                        step.Content.CompletedStatus.Key,
                        step.Content.CompletedStatus.Fallback);
                    break;
                case CharacterTestGuidanceCardState.Rejected:
                    statusTone = MxUiGuidanceTone.Warning;
                    statusGlyph = MxUiGuidanceGlyph.Warning;
                    statusText = Text(
                        "ui.character_test.guidance.target_test.rejected",
                        "未完成：当前目标不可锁定，请更换目标重试");
                    break;
                case CharacterTestGuidanceCardState.WaitingAfterClear:
                    statusTone = MxUiGuidanceTone.Info;
                    statusGlyph = MxUiGuidanceGlyph.Progress;
                    statusText = Text(
                        "ui.character_test.guidance.target_test.cleared",
                        "请先锁定一个目标，再执行取消锁定");
                    break;
                default:
                    statusTone = MxUiGuidanceTone.Neutral;
                    statusGlyph = MxUiGuidanceGlyph.Progress;
                    statusText = Text(
                        step.Content.WaitingStatus.Key,
                        step.Content.WaitingStatus.Fallback);
                    break;
            }

            return new MxUiGuidanceCardSnapshot(
                ScopeId,
                NextCardRevision(),
                true,
                TitlePrompt(coordinator.SelectedStepIndex, step),
                new MxUiGuidanceLine(
                    string.Equals(
                        step.CompletionTypeId,
                        CharacterTestGuidanceCompletionRegistry.MoveDistanceAndStop,
                        StringComparison.Ordinal)
                            ? MxUiGuidanceGlyph.Move
                            : MxUiGuidanceGlyph.Action,
                    MxUiGuidanceTone.Info,
                    Text("ui.guidance.label.action", "步骤："),
                    ActionPrompt(coordinator.SelectedStepIndex, step)),
                new MxUiGuidanceLine(
                    MxUiGuidanceGlyph.Focus,
                    MxUiGuidanceTone.Info,
                    Text("ui.guidance.label.focus", "观察："),
                    Text(step.Content.Focus.Key, step.Content.Focus.Fallback)),
                new MxUiGuidanceLine(
                    statusGlyph,
                    statusTone,
                    Text("ui.guidance.label.status", "状态："),
                    statusText));
        }

        private MxUiLocalizedTextRequest ActionPrompt(
            int stepIndex,
            CharacterTestGuidanceStepDefinition step)
        {
            if (stepIndex >= 0
                && stepIndex < _stepActionPrompts.Length
                && _stepActionPrompts[stepIndex].IsValid)
            {
                return _stepActionPrompts[stepIndex];
            }
            return Text(step.Content.ActionInstruction.Key, step.Content.ActionInstruction.Fallback);
        }

        private MxUiLocalizedTextRequest TitlePrompt(
            int stepIndex,
            CharacterTestGuidanceStepDefinition step)
        {
            if (stepIndex >= 0
                && stepIndex < _stepTitlePrompts.Length
                && _stepTitlePrompts[stepIndex].IsValid)
            {
                return _stepTitlePrompts[stepIndex];
            }
            return Text(step.Content.Title.Key, step.Content.Title.Fallback);
        }

        private static CharacterTestGuidanceCardState ResolveWaitingState(
            CharacterTestGuidanceStepDefinition step,
            in CharacterTestGuidanceObservation observation)
        {
            if (!string.Equals(
                    step.CompletionTypeId,
                    CharacterTestGuidanceCompletionRegistry.TargetLockState,
                    StringComparison.Ordinal)
                || step.CompletionPayload.Value<bool?>("expectedLocked") != false
                || observation.TargetLocked)
            {
                return CharacterTestGuidanceCardState.Waiting;
            }
            return CharacterTestGuidanceCardState.WaitingAfterClear;
        }

        private long NextCardRevision()
        {
            _cardRevision = _cardRevision == long.MaxValue ? 1L : _cardRevision + 1L;
            return _cardRevision;
        }

        private static MxUiGuidanceNotice CreateNotice(
            string messageId,
            MxUiGuidanceTone tone,
            MxUiGuidanceGlyph glyph,
            string textKey,
            string fallback)
        {
            return new MxUiGuidanceNotice(
                ScopeId,
                new MxUiGuidanceMessageId(messageId),
                tone,
                glyph,
                Text(textKey, fallback));
        }

        private static MxUiLocalizedTextRequest Text(string key, string fallback)
        {
            return new MxUiLocalizedTextRequest(new MxUiTextKey(key), fallback);
        }

        private enum CharacterTestGuidanceCardState : byte
        {
            Waiting = 0,
            Completed = 1,
            Rejected = 2,
            WaitingAfterClear = 3
        }
    }

    internal sealed class CharacterTestGuidanceMoveDistanceAndStopObserver :
        ICharacterTestGuidanceCompletionObserver
    {
        private readonly int _minimumDistanceMillimeters;
        private readonly int _maximumStopSpeedMillimetersPerSecond;
        private readonly bool _requireGrounded;
        private bool _hasOrigin;
        private int _originX;
        private int _originZ;
        private bool _distanceReached;

        internal CharacterTestGuidanceMoveDistanceAndStopObserver(JObject payload)
        {
            _minimumDistanceMillimeters = payload?.Value<int?>("minimumDistanceMillimeters") ?? 2000;
            _maximumStopSpeedMillimetersPerSecond =
                payload?.Value<int?>("maximumStopSpeedMillimetersPerSecond") ?? 150;
            _requireGrounded = payload?.Value<bool?>("requireGrounded") ?? true;
        }

        public void Prime(in CharacterTestGuidanceObservationContext context)
        {
            Reset();
            CharacterTestGuidanceObservation observation = context.Observation;
            CaptureOrigin(in observation);
        }

        public CharacterTestGuidanceCompletionOutcome Observe(
            in CharacterTestGuidanceObservationContext context)
        {
            CharacterTestGuidanceObservation observation = context.Observation;
            if (!observation.HasLocalPlayer)
                return default;
            if (!_hasOrigin)
            {
                CaptureOrigin(in observation);
                return default;
            }

            long deltaX = Math.Abs((long)observation.XMillimeters - _originX);
            long deltaZ = Math.Abs((long)observation.ZMillimeters - _originZ);
            long minimumDistance = _minimumDistanceMillimeters;
            _distanceReached |= deltaX >= minimumDistance
                || deltaZ >= minimumDistance
                || deltaX * deltaX + deltaZ * deltaZ
                    >= minimumDistance * minimumDistance;
            if (!_distanceReached
                || observation.HorizontalSpeedMillimetersPerSecond
                    > _maximumStopSpeedMillimetersPerSecond
                || (_requireGrounded && !observation.Grounded))
            {
                return default;
            }
            return CharacterTestGuidanceCompletionOutcome.Completed("movement.completed");
        }

        public void Reset()
        {
            _hasOrigin = false;
            _originX = 0;
            _originZ = 0;
            _distanceReached = false;
        }

        private void CaptureOrigin(in CharacterTestGuidanceObservation observation)
        {
            if (!observation.HasLocalPlayer)
                return;
            _originX = observation.XMillimeters;
            _originZ = observation.ZMillimeters;
            _hasOrigin = true;
        }
    }

    internal sealed class CharacterTestGuidanceJumpAndLandObserver :
        ICharacterTestGuidanceCompletionObserver
    {
        private bool _airborneObserved;

        public void Prime(in CharacterTestGuidanceObservationContext context)
        {
            Reset();
        }

        public CharacterTestGuidanceCompletionOutcome Observe(
            in CharacterTestGuidanceObservationContext context)
        {
            if (!context.Observation.HasLocalPlayer)
                return default;
            if (!context.Observation.Grounded)
            {
                _airborneObserved = true;
                return default;
            }
            return _airborneObserved
                ? CharacterTestGuidanceCompletionOutcome.Completed("jump.completed")
                : default;
        }

        public void Reset()
        {
            _airborneObserved = false;
        }
    }

    internal sealed class CharacterTestGuidanceCameraModeChangedObserver :
        ICharacterTestGuidanceCompletionObserver
    {
        private CharacterTestGuidanceEventKey _lastEvent;
        private bool _hasLastEvent;

        public void Prime(in CharacterTestGuidanceObservationContext context)
        {
            Reset();
            if (TryFindLatest(context.RuntimeEvents, out CharacterTestRuntimeEvent latest))
            {
                _lastEvent = CharacterTestGuidanceEventKey.Create(in latest);
                _hasLastEvent = true;
            }
        }

        public CharacterTestGuidanceCompletionOutcome Observe(
            in CharacterTestGuidanceObservationContext context)
        {
            if (!TryFindLatest(context.RuntimeEvents, out CharacterTestRuntimeEvent latest))
                return default;
            CharacterTestGuidanceEventKey key = CharacterTestGuidanceEventKey.Create(in latest);
            if (_hasLastEvent && key.Equals(_lastEvent))
                return default;
            _lastEvent = key;
            _hasLastEvent = true;
            return CharacterTestGuidanceCompletionOutcome.Completed("camera.completed");
        }

        public void Reset()
        {
            _lastEvent = default;
            _hasLastEvent = false;
        }

        private static bool TryFindLatest(
            IReadOnlyList<CharacterTestRuntimeEvent> events,
            out CharacterTestRuntimeEvent latest)
        {
            for (int i = events != null ? events.Count - 1 : -1; i >= 0; i--)
            {
                if (events[i].Type != CharacterTestRuntimeEventType.CameraModeChanged)
                    continue;
                latest = events[i];
                return true;
            }
            latest = default;
            return false;
        }
    }

    internal sealed class CharacterTestGuidanceTargetLockStateObserver :
        ICharacterTestGuidanceCompletionObserver
    {
        private readonly bool _expectedLocked;
        private CharacterTestGuidanceEventKey _lastEvent;
        private bool _hasLastEvent;

        internal CharacterTestGuidanceTargetLockStateObserver(JObject payload)
        {
            _expectedLocked = payload?.Value<bool?>("expectedLocked") ?? true;
        }

        public void Prime(in CharacterTestGuidanceObservationContext context)
        {
            Reset();
            if (TryFindLatest(context.RuntimeEvents, out CharacterTestRuntimeEvent latest))
            {
                _lastEvent = CharacterTestGuidanceEventKey.Create(in latest);
                _hasLastEvent = true;
            }
        }

        public CharacterTestGuidanceCompletionOutcome Observe(
            in CharacterTestGuidanceObservationContext context)
        {
            if (!TryFindLatest(context.RuntimeEvents, out CharacterTestRuntimeEvent latest))
                return default;
            CharacterTestGuidanceEventKey key = CharacterTestGuidanceEventKey.Create(in latest);
            if (_hasLastEvent && key.Equals(_lastEvent))
                return default;
            _lastEvent = key;
            _hasLastEvent = true;

            if (latest.Type == CharacterTestRuntimeEventType.TargetLockRejected)
            {
                return CharacterTestGuidanceCompletionOutcome.Rejected(
                    "target.rejected",
                    "ui.character_test.guidance.target_rejected",
                    "当前无法锁定目标");
            }

            bool targetLocked = latest.EntityId > 0;
            if (targetLocked == _expectedLocked)
            {
                return CharacterTestGuidanceCompletionOutcome.Completed(
                    _expectedLocked ? "target.locked" : "target.unlocked");
            }
            return CharacterTestGuidanceCompletionOutcome.Context(
                targetLocked ? "target.locked.precondition" : "target.cleared.precondition",
                targetLocked
                    ? "ui.character_test.guidance.target_locked.precondition"
                    : "ui.character_test.guidance.target_cleared.precondition",
                targetLocked ? "目标已锁定，请执行取消锁定" : "当前没有锁定目标");
        }

        public void Reset()
        {
            _lastEvent = default;
            _hasLastEvent = false;
        }

        private static bool TryFindLatest(
            IReadOnlyList<CharacterTestRuntimeEvent> events,
            out CharacterTestRuntimeEvent latest)
        {
            for (int i = events != null ? events.Count - 1 : -1; i >= 0; i--)
            {
                CharacterTestRuntimeEvent candidate = events[i];
                if (candidate.Type != CharacterTestRuntimeEventType.TargetLockChanged
                    && candidate.Type != CharacterTestRuntimeEventType.TargetLockRejected)
                {
                    continue;
                }
                latest = candidate;
                return true;
            }
            latest = default;
            return false;
        }
    }

    internal sealed class CharacterTestGuidanceBasicAttackDamageObserver :
        ICharacterTestGuidanceCompletionObserver
    {
        private readonly CharacterActionId _actionId;
        private readonly int _minimumFinalDamage;
        private CharacterTestGuidanceDamageKey _lastDamage;
        private bool _hasLastDamage;

        internal CharacterTestGuidanceBasicAttackDamageObserver(JObject payload)
        {
            int actionId = payload?.Value<int?>("actionId")
                ?? CharacterTestCharacterActionConfigSource.LightAttackActionId.Value;
            _actionId = actionId > 0 ? new CharacterActionId(actionId) : CharacterActionId.None;
            _minimumFinalDamage = Math.Max(1, payload?.Value<int?>("minimumFinalDamage") ?? 1);
        }

        public void Prime(in CharacterTestGuidanceObservationContext context)
        {
            Reset();
            if (TryFindLatest(
                    context.DamageRows,
                    context.Observation.LocalPlayerEntityId,
                    out DamageTelemetryRow latest))
            {
                _lastDamage = CharacterTestGuidanceDamageKey.Create(in latest);
                _hasLastDamage = true;
            }
        }

        public CharacterTestGuidanceCompletionOutcome Observe(
            in CharacterTestGuidanceObservationContext context)
        {
            int localPlayerEntityId = context.Observation.LocalPlayerEntityId;
            if (localPlayerEntityId <= 0
                || !TryFindLatest(context.DamageRows, localPlayerEntityId, out DamageTelemetryRow latest))
            {
                return default;
            }
            CharacterTestGuidanceDamageKey key = CharacterTestGuidanceDamageKey.Create(in latest);
            if (_hasLastDamage && key.Equals(_lastDamage))
                return default;
            _lastDamage = key;
            _hasLastDamage = true;
            return CharacterTestGuidanceCompletionOutcome.Completed(
                "attack.completed",
                "DMG " + latest.FinalDamage + " · HP "
                    + latest.HpBefore + " → " + latest.HpAfter,
                "ui.character_test.guidance.basic_attack_hit.damage.resolved");
        }

        public void Reset()
        {
            _lastDamage = default;
            _hasLastDamage = false;
        }

        private bool TryFindLatest(
            IReadOnlyList<DamageTelemetryRow> rows,
            int localPlayerEntityId,
            out DamageTelemetryRow latest)
        {
            for (int i = rows != null ? rows.Count - 1 : -1; i >= 0; i--)
            {
                DamageTelemetryRow candidate = rows[i];
                if ((localPlayerEntityId <= 0 || candidate.SourceEntityId == localPlayerEntityId)
                    && candidate.TargetEntityId != candidate.SourceEntityId
                    && candidate.FinalDamage >= _minimumFinalDamage
                    && candidate.HpAfter < candidate.HpBefore
                    && candidate.ActionId == _actionId)
                {
                    latest = candidate;
                    return true;
                }
            }
            latest = default;
            return false;
        }
    }

    internal readonly struct CharacterTestGuidanceEventKey :
        IEquatable<CharacterTestGuidanceEventKey>
    {
        private CharacterTestGuidanceEventKey(
            long frame,
            CharacterTestRuntimeEventType type,
            int commandId,
            int sourceId,
            int targetId,
            int entityId,
            int payload0,
            int payload1,
            int payload2,
            string message)
        {
            Frame = frame;
            Type = type;
            CommandId = commandId;
            SourceId = sourceId;
            TargetId = targetId;
            EntityId = entityId;
            Payload0 = payload0;
            Payload1 = payload1;
            Payload2 = payload2;
            Message = message ?? string.Empty;
        }

        private long Frame { get; }
        private CharacterTestRuntimeEventType Type { get; }
        private int CommandId { get; }
        private int SourceId { get; }
        private int TargetId { get; }
        private int EntityId { get; }
        private int Payload0 { get; }
        private int Payload1 { get; }
        private int Payload2 { get; }
        private string Message { get; }

        internal static CharacterTestGuidanceEventKey Create(in CharacterTestRuntimeEvent evt)
        {
            return new CharacterTestGuidanceEventKey(
                evt.Frame.Value,
                evt.Type,
                evt.CommandId,
                evt.SourceId,
                evt.TargetId,
                evt.EntityId,
                evt.Payload0,
                evt.Payload1,
                evt.Payload2,
                evt.Message);
        }

        public bool Equals(CharacterTestGuidanceEventKey other)
        {
            return Frame == other.Frame
                && Type == other.Type
                && CommandId == other.CommandId
                && SourceId == other.SourceId
                && TargetId == other.TargetId
                && EntityId == other.EntityId
                && Payload0 == other.Payload0
                && Payload1 == other.Payload1
                && Payload2 == other.Payload2
                && string.Equals(Message, other.Message, StringComparison.Ordinal);
        }
    }

    internal readonly struct CharacterTestGuidanceDamageKey :
        IEquatable<CharacterTestGuidanceDamageKey>
    {
        private CharacterTestGuidanceDamageKey(
            long runtimeFrame,
            int actionInstanceId,
            int sourceEntityId,
            int targetEntityId,
            int localFrame,
            int hpBefore,
            int hpAfter,
            CharacterActionId actionId,
            int stableEventId)
        {
            RuntimeFrame = runtimeFrame;
            ActionInstanceId = actionInstanceId;
            SourceEntityId = sourceEntityId;
            TargetEntityId = targetEntityId;
            LocalFrame = localFrame;
            HpBefore = hpBefore;
            HpAfter = hpAfter;
            ActionId = actionId;
            StableEventId = stableEventId;
        }

        private long RuntimeFrame { get; }
        private int ActionInstanceId { get; }
        private int SourceEntityId { get; }
        private int TargetEntityId { get; }
        private int LocalFrame { get; }
        private int HpBefore { get; }
        private int HpAfter { get; }
        private CharacterActionId ActionId { get; }
        private int StableEventId { get; }

        internal static CharacterTestGuidanceDamageKey Create(in DamageTelemetryRow row)
        {
            return new CharacterTestGuidanceDamageKey(
                row.RuntimeFrame,
                row.ActionInstanceId,
                row.SourceEntityId,
                row.TargetEntityId,
                row.LocalFrame,
                row.HpBefore,
                row.HpAfter,
                row.ActionId,
                row.StableEventId);
        }

        public bool Equals(CharacterTestGuidanceDamageKey other)
        {
            return RuntimeFrame == other.RuntimeFrame
                && ActionInstanceId == other.ActionInstanceId
                && SourceEntityId == other.SourceEntityId
                && TargetEntityId == other.TargetEntityId
                && LocalFrame == other.LocalFrame
                && HpBefore == other.HpBefore
                && HpAfter == other.HpAfter
                && ActionId == other.ActionId
                && StableEventId == other.StableEventId;
        }
    }
}
