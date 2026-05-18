using System;
using System.Collections.Generic;
using MxFramework.Events;
using MxFramework.Gameplay;
using MxFramework.Runtime;

namespace MxFramework.CharacterControl
{
    public enum CharacterPressureReactionKind
    {
        None = 0,
        PostureBandChanged = 1,
        GuardBandChanged = 2,
        PostureBreak = 3,
        GuardBreak = 4,
        ArmorBreak = 5
    }

    public enum CharacterPressureReactionSuppressedReason
    {
        None = 0,
        AdapterDisabled = 1,
        MissingEntityMapping = 2,
        PolicyIgnored = 3,
        TransitionRejected = 4,
        NonEscalatingBandChange = 5
    }

    public readonly struct CharacterPressureReactionPolicyEntry
    {
        public CharacterPressureReactionPolicyEntry(
            bool enabled,
            int durationFrames,
            CharacterControlLockMask lockMask,
            bool cancelAction,
            PressureBand minimumBand = PressureBand.Stable)
        {
            if (durationFrames < 0)
                throw new ArgumentOutOfRangeException(nameof(durationFrames), "Reaction duration cannot be negative.");
            if (!Enum.IsDefined(typeof(PressureBand), minimumBand))
                throw new ArgumentOutOfRangeException(nameof(minimumBand), "Minimum pressure band is not defined.");

            Enabled = enabled;
            DurationFrames = durationFrames;
            LockMask = lockMask;
            CancelAction = cancelAction;
            MinimumBand = minimumBand;
        }

        public bool Enabled { get; }

        public int DurationFrames { get; }

        public CharacterControlLockMask LockMask { get; }

        public bool CancelAction { get; }

        public PressureBand MinimumBand { get; }

        public bool AllowsBand(PressureBand band)
        {
            return (int)band >= (int)MinimumBand;
        }

        public static CharacterPressureReactionPolicyEntry Ignore()
        {
            return new CharacterPressureReactionPolicyEntry(
                enabled: false,
                durationFrames: 0,
                lockMask: CharacterControlLockMask.None,
                cancelAction: false);
        }

        public static CharacterPressureReactionPolicyEntry Reaction(
            int durationFrames,
            CharacterControlLockMask lockMask,
            bool cancelAction,
            PressureBand minimumBand = PressureBand.Stable)
        {
            return new CharacterPressureReactionPolicyEntry(
                enabled: true,
                durationFrames: durationFrames,
                lockMask: lockMask,
                cancelAction: cancelAction,
                minimumBand: minimumBand);
        }
    }

    public sealed class CharacterPressureReactionPolicy
    {
        public CharacterPressureReactionPolicy()
        {
            PostureBandChanged = CharacterPressureReactionPolicyEntry.Reaction(
                durationFrames: 4,
                lockMask: CharacterControlLockMask.Move | CharacterControlLockMask.Jump,
                cancelAction: false,
                minimumBand: PressureBand.Critical);
            GuardBandChanged = CharacterPressureReactionPolicyEntry.Reaction(
                durationFrames: 3,
                lockMask: CharacterControlLockMask.Action,
                cancelAction: false,
                minimumBand: PressureBand.Critical);
            PostureBreak = CharacterPressureReactionPolicyEntry.Reaction(
                durationFrames: 12,
                lockMask: CharacterControlLockMask.Move | CharacterControlLockMask.Jump | CharacterControlLockMask.Action,
                cancelAction: true,
                minimumBand: PressureBand.Broken);
            GuardBreak = CharacterPressureReactionPolicyEntry.Reaction(
                durationFrames: 8,
                lockMask: CharacterControlLockMask.Action,
                cancelAction: true,
                minimumBand: PressureBand.Broken);
            ArmorBreak = CharacterPressureReactionPolicyEntry.Ignore();
        }

        public CharacterPressureReactionPolicyEntry PostureBandChanged { get; set; }

        public CharacterPressureReactionPolicyEntry GuardBandChanged { get; set; }

        public CharacterPressureReactionPolicyEntry PostureBreak { get; set; }

        public CharacterPressureReactionPolicyEntry GuardBreak { get; set; }

        public CharacterPressureReactionPolicyEntry ArmorBreak { get; set; }

        public CharacterPressureReactionPolicyEntry GetEntry(CharacterPressureReactionKind kind)
        {
            switch (kind)
            {
                case CharacterPressureReactionKind.PostureBandChanged:
                    return PostureBandChanged;
                case CharacterPressureReactionKind.GuardBandChanged:
                    return GuardBandChanged;
                case CharacterPressureReactionKind.PostureBreak:
                    return PostureBreak;
                case CharacterPressureReactionKind.GuardBreak:
                    return GuardBreak;
                case CharacterPressureReactionKind.ArmorBreak:
                    return ArmorBreak;
                default:
                    return CharacterPressureReactionPolicyEntry.Ignore();
            }
        }
    }

    public readonly struct CharacterPressureReactionTarget
    {
        public CharacterPressureReactionTarget(
            CharacterControlStateMachine stateMachine,
            CharacterActionController actionController = null)
        {
            StateMachine = stateMachine;
            ActionController = actionController;
        }

        public CharacterControlStateMachine StateMachine { get; }

        public CharacterActionController ActionController { get; }

        public CharacterControlEntityRef Entity => StateMachine == null ? default : StateMachine.Entity;

        public bool IsValid => StateMachine != null;
    }

    public interface ICharacterPressureReactionTargetResolver
    {
        bool TryResolve(GameplayEntityId entityId, out CharacterPressureReactionTarget target);
    }

    public sealed class CharacterPressureReactionTargetRegistry : ICharacterPressureReactionTargetResolver
    {
        private readonly Dictionary<GameplayEntityId, CharacterPressureReactionTarget> _targets =
            new Dictionary<GameplayEntityId, CharacterPressureReactionTarget>();

        public int Count => _targets.Count;

        public void Register(CharacterControlStateMachine stateMachine, CharacterActionController actionController = null)
        {
            if (stateMachine == null)
                throw new ArgumentNullException(nameof(stateMachine));
            if (!stateMachine.Entity.HasGameplayEntity)
                throw new ArgumentException("Character control state machine entity must include a GameplayEntityId.", nameof(stateMachine));

            Register(stateMachine.Entity.GameplayEntityId, stateMachine, actionController);
        }

        public void Register(
            GameplayEntityId entityId,
            CharacterControlStateMachine stateMachine,
            CharacterActionController actionController = null)
        {
            if (!entityId.IsValid)
                throw new ArgumentException("Reaction target GameplayEntityId must be valid.", nameof(entityId));
            if (stateMachine == null)
                throw new ArgumentNullException(nameof(stateMachine));

            _targets[entityId] = new CharacterPressureReactionTarget(stateMachine, actionController);
        }

        public bool Unregister(GameplayEntityId entityId)
        {
            return _targets.Remove(entityId);
        }

        public bool TryResolve(GameplayEntityId entityId, out CharacterPressureReactionTarget target)
        {
            return _targets.TryGetValue(entityId, out target) && target.IsValid;
        }
    }

    public sealed class CharacterPressureReactionAdapterOptions
    {
        public ICharacterPressureReactionTargetResolver TargetResolver { get; set; }

        public CharacterPressureReactionPolicy Policy { get; set; }

        public IEventBus<PressureBandChangedEvent> PostureBandChangedEvents { get; set; }

        public IEventBus<PostureBreakEvent> PostureBreakEvents { get; set; }

        public IEventBus<PressureBandChangedEvent> GuardBandChangedEvents { get; set; }

        public IEventBus<GuardBreakEvent> GuardBreakEvents { get; set; }

        public IEventBus<ArmorBreakEvent> ArmorBreakEvents { get; set; }
    }

    public readonly struct CharacterPressureReactionRecord
    {
        public CharacterPressureReactionRecord(
            CharacterPressureReactionKind kind,
            GameplayEntityId gameplayEntityId,
            RuntimeFrame frame,
            RuntimeFrame endFrame,
            int durationFrames,
            CharacterControlLockMask lockMask,
            bool cancelRequested,
            bool cancelSucceeded,
            bool applied,
            CharacterPressureReactionSuppressedReason suppressedReason,
            string message,
            string traceId)
        {
            Kind = kind;
            GameplayEntityId = gameplayEntityId;
            Frame = frame;
            EndFrame = endFrame;
            DurationFrames = durationFrames;
            LockMask = lockMask;
            CancelRequested = cancelRequested;
            CancelSucceeded = cancelSucceeded;
            Applied = applied;
            SuppressedReason = suppressedReason;
            Message = message ?? string.Empty;
            TraceId = traceId ?? string.Empty;
        }

        public CharacterPressureReactionKind Kind { get; }

        public GameplayEntityId GameplayEntityId { get; }

        public RuntimeFrame Frame { get; }

        public RuntimeFrame EndFrame { get; }

        public int DurationFrames { get; }

        public CharacterControlLockMask LockMask { get; }

        public bool CancelRequested { get; }

        public bool CancelSucceeded { get; }

        public bool Applied { get; }

        public CharacterPressureReactionSuppressedReason SuppressedReason { get; }

        public string Message { get; }

        public string TraceId { get; }
    }

    public sealed class CharacterPressureReactionAdapter : IDisposable
    {
        private readonly ICharacterPressureReactionTargetResolver _targetResolver;
        private readonly CharacterPressureReactionPolicy _policy;
        private readonly CharacterPressureReactionAdapterOptions _options;
        private readonly List<IDisposable> _subscriptions = new List<IDisposable>(5);
        private readonly List<ActiveReaction> _activeReactions = new List<ActiveReaction>(4);
        private RuntimeFrame _lastFrame;
        private bool _hasLastFrame;
        private bool _disposed;

        public CharacterPressureReactionAdapter(
            ICharacterPressureReactionTargetResolver targetResolver,
            CharacterPressureReactionPolicy policy = null)
            : this(new CharacterPressureReactionAdapterOptions
            {
                TargetResolver = targetResolver,
                Policy = policy
            })
        {
        }

        public CharacterPressureReactionAdapter(CharacterPressureReactionAdapterOptions options)
        {
            if (options == null)
                throw new ArgumentNullException(nameof(options));

            _targetResolver = options.TargetResolver ?? throw new ArgumentNullException(nameof(options.TargetResolver));
            _policy = options.Policy ?? new CharacterPressureReactionPolicy();
            _options = options;
        }

        public event Action<CharacterPressureReactionRecord> ReactionRecorded;

        public bool IsEnabled { get; private set; }

        public CharacterPressureReactionRecord LastRecord { get; private set; }

        public CharacterPressureReactionSuppressedReason LastSuppressedReason { get; private set; }

        public int ActiveReactionCount => _activeReactions.Count;

        public void Enable()
        {
            ThrowIfDisposed();
            if (IsEnabled)
            {
                return;
            }

            Subscribe(_options.PostureBandChangedEvents, evt => ConsumePostureBandChanged(evt));
            Subscribe(_options.PostureBreakEvents, evt => ConsumePostureBreak(evt));
            Subscribe(_options.GuardBandChangedEvents, evt => ConsumeGuardBandChanged(evt));
            Subscribe(_options.GuardBreakEvents, evt => ConsumeGuardBreak(evt));
            Subscribe(_options.ArmorBreakEvents, evt => ConsumeArmorBreak(evt));
            IsEnabled = true;
        }

        public void Disable()
        {
            ReleaseActiveReactions("Pressure reaction adapter disabled.");

            for (int i = _subscriptions.Count - 1; i >= 0; i--)
            {
                _subscriptions[i].Dispose();
            }

            _subscriptions.Clear();
            _activeReactions.Clear();
            IsEnabled = false;
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            Disable();
            _disposed = true;
        }

        public void Tick(RuntimeFrame frame)
        {
            ThrowIfDisposed();
            RememberFrame(frame);
            for (int i = _activeReactions.Count - 1; i >= 0; i--)
            {
                ActiveReaction active = _activeReactions[i];
                if (frame < active.EndFrame)
                {
                    continue;
                }

                _activeReactions.RemoveAt(i);
                if (active.Target.StateMachine.CurrentState == CharacterControlState.Reaction)
                {
                    active.Target.StateMachine.FinishReaction(frame, "Pressure reaction window expired.");
                }
            }
        }

        public bool ConsumePostureBandChanged(PressureBandChangedEvent evt)
        {
            return ApplyBandChangedReaction(
                CharacterPressureReactionKind.PostureBandChanged,
                evt);
        }

        public bool ConsumeGuardBandChanged(PressureBandChangedEvent evt)
        {
            return ApplyBandChangedReaction(
                CharacterPressureReactionKind.GuardBandChanged,
                evt);
        }

        public bool ConsumePostureBreak(PostureBreakEvent evt)
        {
            return ApplyReaction(
                CharacterPressureReactionKind.PostureBreak,
                evt.EntityId,
                evt.Frame,
                PressureBand.Broken,
                evt.Reason,
                evt.TraceId);
        }

        public bool ConsumeGuardBreak(GuardBreakEvent evt)
        {
            return ApplyReaction(
                CharacterPressureReactionKind.GuardBreak,
                evt.EntityId,
                evt.Frame,
                PressureBand.Broken,
                evt.Reason,
                evt.TraceId);
        }

        public bool ConsumeArmorBreak(ArmorBreakEvent evt)
        {
            return ApplyReaction(
                CharacterPressureReactionKind.ArmorBreak,
                evt.EntityId,
                evt.Frame,
                PressureBand.Broken,
                "ArmorBreak",
                evt.TraceId);
        }

        private bool ApplyReaction(
            CharacterPressureReactionKind kind,
            GameplayEntityId entityId,
            RuntimeFrame frame,
            PressureBand band,
            string reason,
            string traceId)
        {
            ThrowIfDisposed();
            RememberFrame(frame);
            if (!IsEnabled)
            {
                Record(kind, entityId, frame, default, CharacterPressureReactionPolicyEntry.Ignore(), false, false, false, CharacterPressureReactionSuppressedReason.AdapterDisabled, reason, traceId);
                return false;
            }

            CharacterPressureReactionPolicyEntry entry = _policy.GetEntry(kind);
            if (!entry.Enabled || !entry.AllowsBand(band))
            {
                Record(kind, entityId, frame, default, entry, false, false, false, CharacterPressureReactionSuppressedReason.PolicyIgnored, reason, traceId);
                return false;
            }

            if (!_targetResolver.TryResolve(entityId, out CharacterPressureReactionTarget target))
            {
                Record(kind, entityId, frame, default, entry, false, false, false, CharacterPressureReactionSuppressedReason.MissingEntityMapping, reason, traceId);
                return false;
            }

            bool cancelRequested = false;
            bool cancelSucceeded = false;
            if (entry.CancelAction
                && target.ActionController != null
                && target.StateMachine.CurrentState != CharacterControlState.Disabled)
            {
                cancelRequested = true;
                CharacterActionResult cancel = target.ActionController.Submit(
                    CharacterActionRequest.Cancel(frame, target.Entity, traceId: traceId));
                cancelSucceeded = cancel.Success;
            }

            string message = BuildMessage(kind, reason, traceId);
            CharacterControlTransitionResult transition = target.StateMachine.BeginReaction(
                frame,
                CharacterControlTransitionReason.PressureBreak,
                entry.LockMask,
                message);
            if (!transition.Success)
            {
                Record(kind, entityId, frame, default, entry, cancelRequested, cancelSucceeded, false, CharacterPressureReactionSuppressedReason.TransitionRejected, transition.Message, traceId);
                return false;
            }

            RuntimeFrame endFrame = AddFrames(frame, entry.DurationFrames);
            ReplaceActiveReaction(entityId, target, kind, frame, endFrame);
            Record(kind, entityId, frame, endFrame, entry, cancelRequested, cancelSucceeded, true, CharacterPressureReactionSuppressedReason.None, message, traceId);
            return true;
        }

        private bool ApplyBandChangedReaction(
            CharacterPressureReactionKind kind,
            PressureBandChangedEvent evt)
        {
            ThrowIfDisposed();
            RememberFrame(evt.Frame);
            if (!IsEnabled)
            {
                Record(kind, evt.EntityId, evt.Frame, default, CharacterPressureReactionPolicyEntry.Ignore(), false, false, false, CharacterPressureReactionSuppressedReason.AdapterDisabled, evt.Reason, evt.TraceId);
                return false;
            }

            CharacterPressureReactionPolicyEntry entry = _policy.GetEntry(kind);
            if (!IsEscalatingBandChange(evt))
            {
                Record(kind, evt.EntityId, evt.Frame, default, entry, false, false, false, CharacterPressureReactionSuppressedReason.NonEscalatingBandChange, evt.Reason, evt.TraceId);
                return false;
            }

            return ApplyReaction(
                kind,
                evt.EntityId,
                evt.Frame,
                evt.NewBand,
                evt.Reason,
                evt.TraceId);
        }

        private void Record(
            CharacterPressureReactionKind kind,
            GameplayEntityId entityId,
            RuntimeFrame frame,
            RuntimeFrame endFrame,
            CharacterPressureReactionPolicyEntry entry,
            bool cancelRequested,
            bool cancelSucceeded,
            bool applied,
            CharacterPressureReactionSuppressedReason suppressedReason,
            string message,
            string traceId)
        {
            LastSuppressedReason = suppressedReason;
            LastRecord = new CharacterPressureReactionRecord(
                kind,
                entityId,
                frame,
                endFrame,
                entry.DurationFrames,
                entry.LockMask,
                cancelRequested,
                cancelSucceeded,
                applied,
                suppressedReason,
                message,
                traceId);
            ReactionRecorded?.Invoke(LastRecord);
        }

        private void ReplaceActiveReaction(
            GameplayEntityId entityId,
            CharacterPressureReactionTarget target,
            CharacterPressureReactionKind kind,
            RuntimeFrame startFrame,
            RuntimeFrame endFrame)
        {
            for (int i = _activeReactions.Count - 1; i >= 0; i--)
            {
                if (_activeReactions[i].GameplayEntityId.Equals(entityId))
                {
                    _activeReactions.RemoveAt(i);
                }
            }

            _activeReactions.Add(new ActiveReaction(entityId, target, kind, startFrame, endFrame));
        }

        private void ReleaseActiveReactions(string message)
        {
            for (int i = _activeReactions.Count - 1; i >= 0; i--)
            {
                ActiveReaction active = _activeReactions[i];
                if (active.Target.StateMachine.CurrentState == CharacterControlState.Reaction)
                {
                    active.Target.StateMachine.FinishReaction(GetReleaseFrame(active), message);
                }
            }

            _activeReactions.Clear();
        }

        private RuntimeFrame GetReleaseFrame(ActiveReaction active)
        {
            return _hasLastFrame && _lastFrame >= active.StartFrame
                ? _lastFrame
                : active.StartFrame;
        }

        private void RememberFrame(RuntimeFrame frame)
        {
            if (!_hasLastFrame || frame >= _lastFrame)
            {
                _lastFrame = frame;
                _hasLastFrame = true;
            }
        }

        private void Subscribe<T>(IEventBus<T> bus, Action<T> handler) where T : struct
        {
            if (bus == null)
            {
                return;
            }

            _subscriptions.Add(bus.Subscribe(handler));
        }

        private static RuntimeFrame AddFrames(RuntimeFrame frame, int frames)
        {
            return frames <= 0 ? frame : new RuntimeFrame(checked(frame.Value + frames));
        }

        private static bool IsEscalatingBandChange(PressureBandChangedEvent evt)
        {
            return evt.Delta > 0 && (int)evt.NewBand > (int)evt.PreviousBand;
        }

        private static string BuildMessage(CharacterPressureReactionKind kind, string reason, string traceId)
        {
            string normalizedReason = reason ?? string.Empty;
            if (string.IsNullOrEmpty(traceId))
            {
                return string.IsNullOrEmpty(normalizedReason)
                    ? kind.ToString()
                    : kind + ":" + normalizedReason;
            }

            return string.IsNullOrEmpty(normalizedReason)
                ? kind + ":" + traceId
                : kind + ":" + normalizedReason + ":" + traceId;
        }

        private void ThrowIfDisposed()
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(CharacterPressureReactionAdapter));
        }

        private readonly struct ActiveReaction
        {
            public ActiveReaction(
                GameplayEntityId gameplayEntityId,
                CharacterPressureReactionTarget target,
                CharacterPressureReactionKind kind,
                RuntimeFrame startFrame,
                RuntimeFrame endFrame)
            {
                GameplayEntityId = gameplayEntityId;
                Target = target;
                Kind = kind;
                StartFrame = startFrame;
                EndFrame = endFrame;
            }

            public GameplayEntityId GameplayEntityId { get; }

            public CharacterPressureReactionTarget Target { get; }

            public CharacterPressureReactionKind Kind { get; }

            public RuntimeFrame StartFrame { get; }

            public RuntimeFrame EndFrame { get; }
        }
    }
}
