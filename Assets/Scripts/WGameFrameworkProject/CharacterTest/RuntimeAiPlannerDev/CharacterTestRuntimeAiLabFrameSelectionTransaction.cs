using System;

namespace MxFramework.Game.CharacterTest
{
    internal enum CharacterTestRuntimeAiLabFrameSelectionPhase
    {
        Idle = 0,
        Previewing = 1,
        Restoring = 2,
        Committing = 3,
        Recovering = 4,
        FailedLocked = 5
    }

    internal enum CharacterTestRuntimeAiLabFrameSelectionCompletion
    {
        Rejected = 0,
        Committed = 1,
        Superseded = 2,
        Unchanged = 3
    }

    internal static class CharacterTestRuntimeAiLabFrameSelectionFailureCodes
    {
        internal const string None = "";
        internal const string SeekBusy = "SEEK_BUSY";
        internal const string SessionLocked = "SESSION_LOCKED";
        internal const string StaleCompletion = "STALE_COMPLETION";
    }

    internal readonly struct CharacterTestRuntimeAiLabFrameSelectionTarget : IEquatable<CharacterTestRuntimeAiLabFrameSelectionTarget>
    {
        internal CharacterTestRuntimeAiLabFrameSelectionTarget(
            int frame,
            int agentEntityId,
            long recordGeneration,
            long sessionGeneration,
            CharacterTestRuntimeAiLabSeekOrigin origin)
        {
            Frame = frame;
            AgentEntityId = agentEntityId;
            RecordGeneration = recordGeneration;
            SessionGeneration = sessionGeneration;
            Origin = origin;
        }

        internal int Frame { get; }
        internal int AgentEntityId { get; }
        internal long RecordGeneration { get; }
        internal long SessionGeneration { get; }
        internal CharacterTestRuntimeAiLabSeekOrigin Origin { get; }
        internal bool IsValid => Frame >= 0 && RecordGeneration > 0 && SessionGeneration > 0;

        public bool Equals(CharacterTestRuntimeAiLabFrameSelectionTarget other)
        {
            return Frame == other.Frame
                && AgentEntityId == other.AgentEntityId
                && RecordGeneration == other.RecordGeneration
                && SessionGeneration == other.SessionGeneration
                && Origin == other.Origin;
        }

        public override bool Equals(object obj)
        {
            return obj is CharacterTestRuntimeAiLabFrameSelectionTarget other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = Frame;
                hash = hash * 397 ^ AgentEntityId;
                hash = hash * 397 ^ RecordGeneration.GetHashCode();
                hash = hash * 397 ^ SessionGeneration.GetHashCode();
                hash = hash * 397 ^ (int)Origin;
                return hash;
            }
        }
    }

    internal sealed class CharacterTestRuntimeAiLabCommittedFrameBundle
    {
        internal CharacterTestRuntimeAiLabCommittedFrameBundle(
            CharacterTestRuntimeAiLabTimelineSessionIdentity sessionIdentity,
            CharacterTestRuntimeAiPlannerDevTimelineRecord record,
            long fullRecordContentHash,
            long typedEventEvidenceHash,
            long appliedBehaviorRevisionHash,
            long presentationGeneration,
            long presentationHash,
            bool presentationAvailable)
        {
            Initialize(sessionIdentity, record, fullRecordContentHash, typedEventEvidenceHash, appliedBehaviorRevisionHash, presentationGeneration, presentationHash, presentationAvailable);
        }

        internal CharacterTestRuntimeAiLabCommittedFrameBundle() { }
        private bool _initialized;
        internal bool IsInitialized => _initialized;
        internal void Initialize(
            CharacterTestRuntimeAiLabTimelineSessionIdentity sessionIdentity,
            CharacterTestRuntimeAiPlannerDevTimelineRecord record,
            long fullRecordContentHash,
            long typedEventEvidenceHash,
            long appliedBehaviorRevisionHash,
            long presentationGeneration,
            long presentationHash,
            bool presentationAvailable)
        {
            if (_initialized) throw new InvalidOperationException("A published capture cannot be overwritten.");
            _initialized = true;
            if (!sessionIdentity.IsValid)
                throw new ArgumentException("Timeline session identity is invalid.", nameof(sessionIdentity));
            Record = record ?? throw new ArgumentNullException(nameof(record));
            SessionIdentity = sessionIdentity;
            RecordGeneration = record.RecordGeneration;
            FullRecordContentHash = fullRecordContentHash;
            TypedEventEvidenceHash = typedEventEvidenceHash;
            AppliedBehaviorRevisionHash = appliedBehaviorRevisionHash;
            PresentationGeneration = presentationGeneration;
            PresentationHash = presentationHash;
            PresentationAvailable = presentationAvailable;
            FullContentHash = ComputeFullContentHash(
                sessionIdentity,
                record,
                fullRecordContentHash,
                typedEventEvidenceHash,
                appliedBehaviorRevisionHash,
                presentationGeneration,
                presentationHash,
                presentationAvailable);
        }

        internal CharacterTestRuntimeAiLabTimelineSessionIdentity SessionIdentity { get; private set; }
        internal CharacterTestRuntimeAiPlannerDevTimelineRecord Record { get; private set; }
        internal long RecordGeneration { get; private set; }
        internal long FullRecordContentHash { get; private set; }
        internal long TypedEventEvidenceHash { get; private set; }
        internal long AppliedBehaviorRevisionHash { get; private set; }
        internal long FullContentHash { get; private set; }
        internal long AuthorityHash => Record.StateHash;
        internal long PresentationGeneration { get; private set; }
        internal long PresentationHash { get; private set; }
        internal bool PresentationAvailable { get; private set; }

        private static long ComputeFullContentHash(
            CharacterTestRuntimeAiLabTimelineSessionIdentity sessionIdentity,
            CharacterTestRuntimeAiPlannerDevTimelineRecord record,
            long fullRecordContentHash,
            long typedEventEvidenceHash,
            long appliedBehaviorRevisionHash,
            long presentationGeneration,
            long presentationHash,
            bool presentationAvailable)
        {
            unchecked
            {
                long hash = 17L;
                hash = hash * 397L ^ sessionIdentity.Generation;
                hash = hash * 397L ^ sessionIdentity.SessionStartFrame;
                hash = hash * 397L ^ sessionIdentity.FrameLimit;
                hash = hash * 397L ^ record.Frame;
                hash = hash * 397L ^ record.RecordGeneration;
                hash = hash * 397L ^ fullRecordContentHash;
                hash = hash * 397L ^ record.StateHash;
                hash = hash * 397L ^ typedEventEvidenceHash;
                hash = hash * 397L ^ appliedBehaviorRevisionHash;
                hash = hash * 397L ^ presentationGeneration;
                hash = hash * 397L ^ presentationHash;
                hash = hash * 397L ^ (presentationAvailable ? 1L : 0L);
                return hash;
            }
        }
    }

    internal readonly struct CharacterTestRuntimeAiLabCommittedFrameReceipt
    {
        internal CharacterTestRuntimeAiLabCommittedFrameReceipt(
            long commitGeneration,
            int preferredAgentEntityId,
            int resolvedAgentEntityId,
            CharacterTestRuntimeAiLabCommittedFrameBundle bundle)
        {
            CommitGeneration = commitGeneration;
            PreferredAgentEntityId = preferredAgentEntityId;
            ResolvedAgentEntityId = resolvedAgentEntityId;
            Bundle = bundle;
        }

        internal long CommitGeneration { get; }
        internal int PreferredAgentEntityId { get; }
        internal int ResolvedAgentEntityId { get; }
        internal CharacterTestRuntimeAiLabCommittedFrameBundle Bundle { get; }
        internal bool IsValid => CommitGeneration > 0 && Bundle != null;
        internal int Frame => Bundle?.Record.Frame ?? 0;
        internal long AuthorityHash => Bundle?.AuthorityHash ?? 0L;
    }

    /// <summary>
    /// Serializes Runtime AI Lab frame-selection intents. This is an internal UI/session
    /// transaction boundary and does not alter Runtime AI Planner behavior semantics.
    /// </summary>
    /// <remarks>NoAlloc after initialization.</remarks>
    internal sealed class CharacterTestRuntimeAiLabFrameSelectionCoordinator
    {
        private long _transactionId;
        private long _commitGeneration;
        private bool _scrubGestureActive;
        private bool _hasPendingTarget;
        private bool _hasQueuedTarget;
        private CharacterTestRuntimeAiLabFrameSelectionTarget _pendingTarget;
        private CharacterTestRuntimeAiLabFrameSelectionTarget _activeRestoreTarget;
        private CharacterTestRuntimeAiLabFrameSelectionTarget _queuedTarget;
        private CharacterTestRuntimeAiLabCommittedFrameReceipt _committedReceipt;

        internal CharacterTestRuntimeAiLabFrameSelectionPhase Phase { get; private set; }
        internal long TransactionId => _transactionId;
        internal bool ScrubGestureActive => _scrubGestureActive;
        internal bool HasQueuedTarget => _hasQueuedTarget;
        internal CharacterTestRuntimeAiLabFrameSelectionTarget ActiveRestoreTarget => _activeRestoreTarget;
        internal CharacterTestRuntimeAiLabFrameSelectionTarget PendingTarget =>
            _activeRestoreTarget.IsValid ? _activeRestoreTarget : _pendingTarget;
        internal CharacterTestRuntimeAiLabFrameSelectionTarget QueuedTarget => _queuedTarget;
        internal CharacterTestRuntimeAiLabCommittedFrameReceipt CommittedReceipt => _committedReceipt;
        internal bool IsBusy => _scrubGestureActive || Phase == CharacterTestRuntimeAiLabFrameSelectionPhase.Restoring
            || Phase == CharacterTestRuntimeAiLabFrameSelectionPhase.Committing
            || Phase == CharacterTestRuntimeAiLabFrameSelectionPhase.Recovering;

        internal void Reset(CharacterTestRuntimeAiLabCommittedFrameReceipt initialReceipt = default)
        {
            _transactionId++;
            _commitGeneration = initialReceipt.CommitGeneration;
            _scrubGestureActive = false;
            _hasPendingTarget = false;
            _hasQueuedTarget = false;
            _pendingTarget = default;
            _activeRestoreTarget = default;
            _queuedTarget = default;
            _committedReceipt = initialReceipt;
            Phase = CharacterTestRuntimeAiLabFrameSelectionPhase.Idle;
        }

        internal bool BeginScrub(
            CharacterTestRuntimeAiLabFrameSelectionTarget target,
            out string failureCode)
        {
            if (!CanAcceptScrub(target, out failureCode))
                return false;
            _scrubGestureActive = true;
            _transactionId++;
            QueueScrubTarget(target);
            return true;
        }

        internal bool UpdateScrub(
            CharacterTestRuntimeAiLabFrameSelectionTarget target,
            out string failureCode)
        {
            if (!_scrubGestureActive)
                return BeginScrub(target, out failureCode);
            if (!CanAcceptScrub(target, out failureCode))
                return false;
            QueueScrubTarget(target);
            return true;
        }

        internal bool EndScrub(
            CharacterTestRuntimeAiLabFrameSelectionTarget finalTarget,
            out string failureCode)
        {
            failureCode = CharacterTestRuntimeAiLabFrameSelectionFailureCodes.None;
            if (!_scrubGestureActive || !CanAcceptScrub(finalTarget, out failureCode))
                return false;
            QueueScrubTarget(finalTarget);
            _scrubGestureActive = false;
            return true;
        }

        internal bool ReplacePendingTarget(
            CharacterTestRuntimeAiLabFrameSelectionTarget target,
            out string failureCode)
        {
            if (Phase != CharacterTestRuntimeAiLabFrameSelectionPhase.Previewing
                || IsBusy
                || !target.IsValid)
            {
                failureCode = Phase == CharacterTestRuntimeAiLabFrameSelectionPhase.FailedLocked
                    ? CharacterTestRuntimeAiLabFrameSelectionFailureCodes.SessionLocked
                    : CharacterTestRuntimeAiLabFrameSelectionFailureCodes.SeekBusy;
                return false;
            }

            _pendingTarget = target;
            _hasPendingTarget = true;
            failureCode = CharacterTestRuntimeAiLabFrameSelectionFailureCodes.None;
            return true;
        }

        internal bool CommitLiveFrame(
            int resolvedAgentEntityId,
            CharacterTestRuntimeAiLabCommittedFrameBundle bundle,
            out CharacterTestRuntimeAiLabCommittedFrameReceipt receipt,
            int preferredAgentEntityId = 0)
        {
            receipt = _committedReceipt;
            int preferred = preferredAgentEntityId > 0 ? preferredAgentEntityId : resolvedAgentEntityId;
            if (Phase != CharacterTestRuntimeAiLabFrameSelectionPhase.Idle || bundle == null)
                return false;

            if (_committedReceipt.IsValid
                && _committedReceipt.Frame == bundle.Record.Frame
                && _committedReceipt.PreferredAgentEntityId == preferred
                && _committedReceipt.ResolvedAgentEntityId == resolvedAgentEntityId
                && _committedReceipt.Bundle.RecordGeneration == bundle.RecordGeneration
                && _committedReceipt.Bundle.FullContentHash == bundle.FullContentHash)
            {
                return true;
            }

            _commitGeneration++;
            _committedReceipt = new CharacterTestRuntimeAiLabCommittedFrameReceipt(
                _commitGeneration,
                preferred,
                resolvedAgentEntityId,
                bundle);
            receipt = _committedReceipt;
            return true;
        }

        internal bool TryBeginExplicit(
            CharacterTestRuntimeAiLabFrameSelectionTarget target,
            out string failureCode)
        {
            if (Phase == CharacterTestRuntimeAiLabFrameSelectionPhase.FailedLocked)
            {
                failureCode = CharacterTestRuntimeAiLabFrameSelectionFailureCodes.SessionLocked;
                return false;
            }
            if (IsBusy || Phase != CharacterTestRuntimeAiLabFrameSelectionPhase.Idle)
            {
                failureCode = CharacterTestRuntimeAiLabFrameSelectionFailureCodes.SeekBusy;
                return false;
            }
            if (!target.IsValid)
            {
                failureCode = CharacterTestRuntimeAiLabFrameSelectionFailureCodes.StaleCompletion;
                return false;
            }
            _transactionId++;
            _pendingTarget = target;
            _hasPendingTarget = true;
            Phase = CharacterTestRuntimeAiLabFrameSelectionPhase.Previewing;
            failureCode = CharacterTestRuntimeAiLabFrameSelectionFailureCodes.None;
            return true;
        }

        internal bool TryTakeNextRestore(out CharacterTestRuntimeAiLabFrameSelectionTarget target)
        {
            if (Phase != CharacterTestRuntimeAiLabFrameSelectionPhase.Previewing || !_hasPendingTarget)
            {
                target = default;
                return false;
            }
            _activeRestoreTarget = _pendingTarget;
            _pendingTarget = default;
            _hasPendingTarget = false;
            Phase = CharacterTestRuntimeAiLabFrameSelectionPhase.Restoring;
            target = _activeRestoreTarget;
            return true;
        }

        internal CharacterTestRuntimeAiLabFrameSelectionCompletion CompleteRestore(
            long transactionId,
            CharacterTestRuntimeAiLabFrameSelectionTarget completedTarget,
            int resolvedAgentEntityId,
            CharacterTestRuntimeAiLabCommittedFrameBundle bundle,
            out CharacterTestRuntimeAiLabCommittedFrameReceipt receipt)
        {
            receipt = _committedReceipt;
            if (!CanCompleteRestore(transactionId, completedTarget, bundle))
            {
                return CharacterTestRuntimeAiLabFrameSelectionCompletion.Rejected;
            }

            if (_hasQueuedTarget && !_queuedTarget.Equals(completedTarget))
            {
                _pendingTarget = _queuedTarget;
                _hasPendingTarget = true;
                _queuedTarget = default;
                _hasQueuedTarget = false;
                _activeRestoreTarget = default;
                Phase = CharacterTestRuntimeAiLabFrameSelectionPhase.Previewing;
                return CharacterTestRuntimeAiLabFrameSelectionCompletion.Superseded;
            }

            if (_committedReceipt.IsValid
                && _committedReceipt.Frame == bundle.Record.Frame
                && _committedReceipt.PreferredAgentEntityId == completedTarget.AgentEntityId
                && _committedReceipt.ResolvedAgentEntityId == resolvedAgentEntityId
                && _committedReceipt.Bundle.RecordGeneration == bundle.RecordGeneration
                && _committedReceipt.Bundle.FullContentHash == bundle.FullContentHash)
            {
                _activeRestoreTarget = default;
                _queuedTarget = default;
                _hasQueuedTarget = false;
                Phase = _scrubGestureActive
                    ? CharacterTestRuntimeAiLabFrameSelectionPhase.Previewing
                    : CharacterTestRuntimeAiLabFrameSelectionPhase.Idle;
                return CharacterTestRuntimeAiLabFrameSelectionCompletion.Unchanged;
            }

            Phase = CharacterTestRuntimeAiLabFrameSelectionPhase.Committing;
            _commitGeneration++;
            _committedReceipt = new CharacterTestRuntimeAiLabCommittedFrameReceipt(
                _commitGeneration,
                completedTarget.AgentEntityId,
                resolvedAgentEntityId,
                bundle);
            receipt = _committedReceipt;
            _activeRestoreTarget = default;
            _queuedTarget = default;
            _hasQueuedTarget = false;
            Phase = _scrubGestureActive
                ? CharacterTestRuntimeAiLabFrameSelectionPhase.Previewing
                : CharacterTestRuntimeAiLabFrameSelectionPhase.Idle;
            return CharacterTestRuntimeAiLabFrameSelectionCompletion.Committed;
        }

        internal bool CanCompleteRestore(
            long transactionId,
            CharacterTestRuntimeAiLabFrameSelectionTarget completedTarget,
            CharacterTestRuntimeAiLabCommittedFrameBundle bundle)
        {
            return Phase == CharacterTestRuntimeAiLabFrameSelectionPhase.Restoring
                && transactionId == _transactionId
                && completedTarget.Equals(_activeRestoreTarget)
                && bundle != null
                && bundle.SessionIdentity.Generation == completedTarget.SessionGeneration
                && bundle.Record.Frame == completedTarget.Frame
                && bundle.RecordGeneration == completedTarget.RecordGeneration;
        }

        internal bool IsActiveRestoreSuperseded(
            long transactionId,
            CharacterTestRuntimeAiLabFrameSelectionTarget completedTarget)
        {
            return Phase == CharacterTestRuntimeAiLabFrameSelectionPhase.Restoring
                && transactionId == _transactionId
                && completedTarget.Equals(_activeRestoreTarget)
                && _hasQueuedTarget
                && !_queuedTarget.Equals(completedTarget);
        }

        internal bool IsEquivalentToCommitted(
            int preferredAgentEntityId,
            int resolvedAgentEntityId,
            CharacterTestRuntimeAiLabCommittedFrameBundle bundle)
        {
            return bundle != null
                && _committedReceipt.IsValid
                && _committedReceipt.Frame == bundle.Record.Frame
                && _committedReceipt.PreferredAgentEntityId == preferredAgentEntityId
                && _committedReceipt.ResolvedAgentEntityId == resolvedAgentEntityId
                && _committedReceipt.Bundle.RecordGeneration == bundle.RecordGeneration
                && _committedReceipt.Bundle.FullContentHash == bundle.FullContentHash;
        }

        internal void BeginRecovery()
        {
            _scrubGestureActive = false;
            _hasPendingTarget = false;
            _hasQueuedTarget = false;
            Phase = CharacterTestRuntimeAiLabFrameSelectionPhase.Recovering;
        }

        internal void AbortRestoreBeforeMutation()
        {
            _activeRestoreTarget = default;
            if (_hasQueuedTarget)
            {
                _pendingTarget = _queuedTarget;
                _hasPendingTarget = true;
                _queuedTarget = default;
                _hasQueuedTarget = false;
                Phase = CharacterTestRuntimeAiLabFrameSelectionPhase.Previewing;
                return;
            }

            _pendingTarget = default;
            _hasPendingTarget = false;
            _scrubGestureActive = false;
            Phase = CharacterTestRuntimeAiLabFrameSelectionPhase.Idle;
        }

        internal void CancelScrubPreview()
        {
            if (Phase == CharacterTestRuntimeAiLabFrameSelectionPhase.Restoring
                || Phase == CharacterTestRuntimeAiLabFrameSelectionPhase.Recovering
                || Phase == CharacterTestRuntimeAiLabFrameSelectionPhase.Committing)
            {
                return;
            }

            _scrubGestureActive = false;
            _hasPendingTarget = false;
            _hasQueuedTarget = false;
            _pendingTarget = default;
            _queuedTarget = default;
            if (Phase != CharacterTestRuntimeAiLabFrameSelectionPhase.FailedLocked)
                Phase = CharacterTestRuntimeAiLabFrameSelectionPhase.Idle;
        }

        internal void CompleteRecovery(bool success)
        {
            _activeRestoreTarget = default;
            _pendingTarget = default;
            _queuedTarget = default;
            _hasPendingTarget = false;
            _hasQueuedTarget = false;
            Phase = success
                ? CharacterTestRuntimeAiLabFrameSelectionPhase.Idle
                : CharacterTestRuntimeAiLabFrameSelectionPhase.FailedLocked;
        }

        private bool CanAcceptScrub(
            CharacterTestRuntimeAiLabFrameSelectionTarget target,
            out string failureCode)
        {
            if (Phase == CharacterTestRuntimeAiLabFrameSelectionPhase.FailedLocked)
            {
                failureCode = CharacterTestRuntimeAiLabFrameSelectionFailureCodes.SessionLocked;
                return false;
            }
            if (!target.IsValid)
            {
                failureCode = CharacterTestRuntimeAiLabFrameSelectionFailureCodes.StaleCompletion;
                return false;
            }
            if (Phase == CharacterTestRuntimeAiLabFrameSelectionPhase.Recovering
                || Phase == CharacterTestRuntimeAiLabFrameSelectionPhase.Committing)
            {
                failureCode = CharacterTestRuntimeAiLabFrameSelectionFailureCodes.SeekBusy;
                return false;
            }
            failureCode = CharacterTestRuntimeAiLabFrameSelectionFailureCodes.None;
            return true;
        }

        private void QueueScrubTarget(CharacterTestRuntimeAiLabFrameSelectionTarget target)
        {
            if (Phase == CharacterTestRuntimeAiLabFrameSelectionPhase.Restoring)
            {
                _queuedTarget = target;
                _hasQueuedTarget = true;
                return;
            }
            _pendingTarget = target;
            _hasPendingTarget = true;
            Phase = CharacterTestRuntimeAiLabFrameSelectionPhase.Previewing;
        }
    }
}
