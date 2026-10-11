using System;

namespace MxFramework.Game.CharacterTest.TacticalCard
{
    /// <summary>Turn progress is independent of pause ownership. Values are protocol-stable.</summary>
    public enum TacticalTurnPhase { ReadyPaused = 0, Committing = 1, Executing = 2, AwaitingSettle = 3, Suspended = 4 }

    /// <summary>Each reason is one idempotent owner, not a reference-counted request.</summary>
    /// <summary>Frozen tactical pause discriminators. Values are protocol-stable; do not reorder or reassign.</summary>
    public enum TacticalPauseReason { Ready = 0, UserMenu = 1, RecoveryTimeout = 2, ExecutionFault = 3, BattleEnded = 4 }

    /// <summary>World adapter must gate all simulation clocks at complete-tick boundaries, preserving physical state.</summary>
    public interface ITacticalTimeAdapter
    {
        /// <remarks>NoAlloc. Must synchronously apply the gate without re-entering the coordinator.</remarks>
        void SetSimulationPaused(bool paused);
        bool IsSimulationPaused { get; }
    }

    /// <summary>Optional world-rate transition. Safety pause owners still apply an immediate gate.</summary>
    internal interface ITacticalTimeTransitionAdapter : ITacticalTimeAdapter
    {
        void SetSimulationPauseTarget(bool paused, bool immediate);
        float WorldRateFraction { get; }
        float TransitionProgress { get; }
        bool IsAccelerating { get; }
        bool IsDecelerating { get; }
    }

    /// <summary>Single-threaded turn authority. Normal operations are NoAlloc after construction.</summary>
    public interface ITacticalTimeCoordinator
    {
        TacticalTurnPhase Phase { get; }
        bool IsWorldPaused { get; }
        ulong CurrentTurnIndex { get; }
        void Initialize(ulong initialTurnIndex = 1);
        void RequestPause(TacticalPauseReason reason);
        void RequestResume(TacticalPauseReason reason);
        bool TryCommitTurn(TacticalDeckManager deck, IMomentumLedger ledger, out TacticalCardInstance committedAction);
        void NotifyActionStarted();
        void NotifyExecutionTerminal();
        bool EvaluateSettle(in SettleSample sample, TacticalDeckManager deck);
        void ForceSuspend(TacticalPauseReason reason);
    }

    /// <summary>
    /// Owns pause reasons and a frozen deck completion receipt. Executor admission is required before commit.
    /// Does not own Unity time, physics, rewards, or timeout policy. Adapters must not throw or re-enter.
    /// </summary>
    public sealed partial class TacticalTimeCoordinator : ITacticalTimeCoordinator
    {
        private readonly ITacticalTimeAdapter _adapter;
        private readonly ISettleDetector _detector;
        private uint _pauseReasons;
        private uint _suspendReasons;
        private bool _initialized;
        private bool _storyTransfer;
        private bool _actionStarted;
        private TacticalTurnPhase _resumePhase;
        private TacticalDeckManager _committedDeck;
        private ulong _committedId;
        private uint _committedVersion;

        public TacticalTurnPhase Phase { get; private set; } = TacticalTurnPhase.Suspended;
        public bool IsWorldPaused => _adapter.IsSimulationPaused;
        internal float WorldRateFraction => _adapter is ITacticalTimeTransitionAdapter transition
            ? transition.WorldRateFraction : IsWorldPaused ? 0f : 1f;
        internal bool IsAccelerating => _adapter is ITacticalTimeTransitionAdapter transition && transition.IsAccelerating;
        internal float TransitionProgress => _adapter is ITacticalTimeTransitionAdapter transition ? transition.TransitionProgress : 1f;
        internal bool IsDecelerating => _adapter is ITacticalTimeTransitionAdapter transition && transition.IsDecelerating;
        public ulong CurrentTurnIndex { get; private set; }

        /// <summary>NoAlloc. Inspect ownership without releasing another owner's pause.</summary>
        public bool HasPauseReason(TacticalPauseReason reason) => (_pauseReasons & Bit(reason)) != 0;

        /// <summary>End this tactical session's control after a formal Story request was accepted.</summary>
        /// <remarks>NoAlloc. Preserves the menu owner and all card/ledger state; this session cannot commit again.</remarks>
        internal void BeginStoryTransfer()
        {
            _storyTransfer = true;
            _pauseReasons &= Bit(TacticalPauseReason.UserMenu);
            _suspendReasons = 0;
            ApplyPause();
        }

        public TacticalTimeCoordinator(ITacticalTimeAdapter adapter, ISettleDetector detector = null)
        {
            _adapter = adapter ?? throw new ArgumentNullException(nameof(adapter));
            _detector = detector ?? new SettleDetector();
        }

        /// <summary>One-time initialization; repeated calls cannot erase a live turn or pause owners.</summary>
        public void Initialize(ulong initialTurnIndex = 1)
        {
            if (_initialized) return;
            if (initialTurnIndex == 0) throw new ArgumentOutOfRangeException(nameof(initialTurnIndex));
            _initialized = true;
            CurrentTurnIndex = initialTurnIndex;
            Phase = TacticalTurnPhase.ReadyPaused;
            if (_suspendReasons != 0)
            {
                _resumePhase = Phase;
                Phase = TacticalTurnPhase.Suspended;
            }
            _pauseReasons |= Bit(TacticalPauseReason.Ready);
            _detector.Reset();
            ApplyPause();
        }

        public void RequestPause(TacticalPauseReason reason)
        {
            if (reason >= TacticalPauseReason.RecoveryTimeout && reason <= TacticalPauseReason.BattleEnded)
            {
                ForceSuspend(reason);
                return;
            }
            _pauseReasons |= Bit(reason);
            ApplyPause();
        }

        /// <summary>Releases only this owner. Ready cannot be bypassed without a successful commit.</summary>
        public void RequestResume(TacticalPauseReason reason)
        {
            uint bit = Bit(reason);
            _pauseReasons &= ~bit;
            _suspendReasons &= ~bit;
            if (_initialized && Phase == TacticalTurnPhase.Suspended && _suspendReasons == 0)
                Phase = _resumePhase;
            ApplyPause();
        }

        /// <summary>NoAlloc. On failure preserves the pending card and Ready pause; on success releases only Ready.</summary>
        public bool TryCommitTurn(TacticalDeckManager deck, IMomentumLedger ledger, out TacticalCardInstance committedAction)
            => TryCommitTurn(deck, ledger, false, out committedAction);

        internal bool TryCommitTurn(TacticalDeckManager deck, IMomentumLedger ledger, bool deferPayment, out TacticalCardInstance committedAction)
        {
            committedAction = null;
            if (!_initialized || _storyTransfer || Phase != TacticalTurnPhase.ReadyPaused || !IsWorldPaused
                || deck == null || ledger == null) return false;
            Phase = TacticalTurnPhase.Committing;
            if (deck.CommitTurn(ledger, deferPayment, out committedAction) != CardPlayFailure.None)
            {
                Phase = TacticalTurnPhase.ReadyPaused;
                ApplyPause();
                return false;
            }
            _committedDeck = deck;
            _committedId = committedAction.InstanceId;
            _committedVersion = committedAction.Version;
            _actionStarted = false;
            _detector.Reset();
            Phase = TacticalTurnPhase.Executing;
            RequestResume(TacticalPauseReason.Ready);
            return true;
        }

        /// <summary>Forward only the current admitted execution's authoritative start event.</summary>
        public void NotifyActionStarted()
        {
            if (_committedDeck != null && (Phase == TacticalTurnPhase.Executing ||
                (Phase == TacticalTurnPhase.Suspended && _resumePhase == TacticalTurnPhase.Executing)))
                _actionStarted = true;
        }

        /// <summary>Discard incomplete or out-of-order observation windows without changing turn authority.</summary>
        internal void ResetSettleWindow() => _detector.Reset();

        /// <summary>Terminal notification permits sampling; it does not assert timeline completion or control release.</summary>
        public void NotifyExecutionTerminal()
        {
            if (Phase == TacticalTurnPhase.Executing)
            {
                Phase = TacticalTurnPhase.AwaitingSettle;
                _detector.Reset();
            }
            else if (Phase == TacticalTurnPhase.Suspended && _resumePhase == TacticalTurnPhase.Executing)
            {
                _resumePhase = TacticalTurnPhase.AwaitingSettle;
                _detector.Reset();
            }
        }

        /// <summary>NoAlloc. Call after all commands, contacts and rewards of the complete world tick have been processed.</summary>
        public bool EvaluateSettle(in SettleSample sample, TacticalDeckManager deck)
        {
            if (!_initialized || _storyTransfer || Phase != TacticalTurnPhase.AwaitingSettle || IsWorldPaused) return false;
            if (!ReferenceEquals(deck, _committedDeck) || deck == null) return false;
            if (_actionStarted && sample.NoActionStarted)
            {
                _detector.Reset();
                return false;
            }
            if (!_detector.Sample(in sample)) return false;
            if (CurrentTurnIndex == ulong.MaxValue || !deck.CompleteTurn(_committedId, _committedVersion))
            {
                ForceSuspend(TacticalPauseReason.ExecutionFault);
                return false;
            }
            _committedDeck = null;
            _committedId = 0;
            _committedVersion = 0;
            _actionStarted = false;
            CurrentTurnIndex++;
            Phase = TacticalTurnPhase.ReadyPaused;
            RequestPause(TacticalPauseReason.Ready);
            return true;
        }

        /// <summary>NoAlloc. Preserves resolving card, quota, charges and turn index; matching resumes restore progress.</summary>
        public void ForceSuspend(TacticalPauseReason reason)
        {
            uint bit = Bit(reason);
            if (Phase != TacticalTurnPhase.Suspended) _resumePhase = Phase;
            Phase = TacticalTurnPhase.Suspended;
            _pauseReasons |= bit;
            _suspendReasons |= bit;
            _detector.Reset();
            ApplyPause();
        }

        private void ApplyPause()
        {
            // Before the first Ready, ingress may run and menu/recovery owners may pause it.
            bool paused = _pauseReasons != 0 || (!_storyTransfer && _initialized &&
                (Phase == TacticalTurnPhase.ReadyPaused || Phase == TacticalTurnPhase.Committing ||
                 Phase == TacticalTurnPhase.Suspended));
            if (_adapter is ITacticalTimeTransitionAdapter transition)
            {
                bool immediate = _storyTransfer || (_pauseReasons & ~Bit(TacticalPauseReason.Ready)) != 0;
                transition.SetSimulationPauseTarget(paused, immediate);
            }
            else if (_adapter.IsSimulationPaused != paused) _adapter.SetSimulationPaused(paused);
        }

        private static uint Bit(TacticalPauseReason reason)
        {
            if (reason < TacticalPauseReason.Ready || reason > TacticalPauseReason.BattleEnded)
                throw new ArgumentOutOfRangeException(nameof(reason));
            return 1u << (int)reason;
        }
    }
}
