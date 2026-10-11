namespace MxFramework.UI
{
    /// <summary>Fixed reasons for rejected guidance state changes.</summary>
    public enum MxUiGuidanceRejectionReason : byte
    {
        None = 0,
        InvalidCard = 1,
        StaleCardRevision = 2,
        InvalidNotice = 3,
        NoticeCapacity = 4,
        InvalidScope = 5
    }

    /// <summary>Read-only state for one active transient notice slot.</summary>
    /// <remarks>NoAlloc value type.</remarks>
    public readonly struct MxUiGuidanceNoticeState
    {
        internal MxUiGuidanceNoticeState(
            in MxUiGuidanceNotice notice,
            float elapsedSeconds,
            float remainingSeconds,
            float durationSeconds,
            long sequence,
            long revision)
        {
            Notice = notice;
            ElapsedSeconds = elapsedSeconds;
            RemainingSeconds = remainingSeconds;
            DurationSeconds = durationSeconds;
            Sequence = sequence;
            Revision = revision;
        }

        public MxUiGuidanceNotice Notice { get; }
        public float ElapsedSeconds { get; }
        public float RemainingSeconds { get; }
        public float DurationSeconds { get; }
        public long Sequence { get; }
        public long Revision { get; }
        public bool IsActive => Sequence > 0L && RemainingSeconds > 0f;
    }

    /// <summary>
    /// Owns the one-card, two-notice presentation policy without depending on a rendering surface.
    /// </summary>
    /// <remarks>
    /// NoAlloc after construction. Submit methods are intended for low-frequency owner-thread use;
    /// <see cref="Advance"/> is suitable for a no-allocation presentation Tick.
    /// </remarks>
    public sealed class MxUiGuidanceState : IMxUiGuidanceSink
    {
        private NoticeSlot _notice0;
        private NoticeSlot _notice1;
        private long _nextNoticeSequence = 1L;

        public bool HasCard { get; private set; }
        public MxUiGuidanceCardSnapshot Card { get; private set; }
        public int NoticeCount => (_notice0.Active ? 1 : 0) + (_notice1.Active ? 1 : 0);
        public long Version { get; private set; }
        public MxUiGuidanceRejectionReason LastRejectionReason { get; private set; }
        public MxUiGuidanceScopeId LastRejectedScopeId { get; private set; }
        public MxUiGuidanceMessageId LastRejectedMessageId { get; private set; }
        public int InvalidCardCount { get; private set; }
        public int StaleCardRevisionCount { get; private set; }
        public int InvalidNoticeCount { get; private set; }
        public int NoticeOverflowCount { get; private set; }
        public int InvalidScopeCount { get; private set; }

        /// <summary>Shows or updates the one active card when the snapshot is valid and monotonic.</summary>
        /// <remarks>NoAlloc.</remarks>
        public void PresentCard(in MxUiGuidanceCardSnapshot snapshot)
        {
            TryPresentCard(in snapshot);
        }

        /// <summary>Attempts to show or update the one active card.</summary>
        /// <remarks>NoAlloc. Exposed so a rendering presenter can coordinate visual transitions.</remarks>
        public bool TryPresentCard(in MxUiGuidanceCardSnapshot snapshot)
        {
            if (!snapshot.IsValid)
            {
                InvalidCardCount++;
                Reject(MxUiGuidanceRejectionReason.InvalidCard, snapshot.ScopeId, default);
                return false;
            }

            if (HasCard && Card.ScopeId == snapshot.ScopeId && snapshot.Revision <= Card.Revision)
            {
                StaleCardRevisionCount++;
                Reject(MxUiGuidanceRejectionReason.StaleCardRevision, snapshot.ScopeId, default);
                return false;
            }

            Card = snapshot;
            HasCard = true;
            Accept();
            Version++;
            return true;
        }

        /// <summary>Attempts to insert, merge, or priority-replace one notice.</summary>
        /// <remarks>NoAlloc after construction.</remarks>
        public bool TryPushNotice(in MxUiGuidanceNotice notice)
        {
            if (!notice.IsValid)
            {
                InvalidNoticeCount++;
                Reject(MxUiGuidanceRejectionReason.InvalidNotice, notice.ScopeId, notice.MessageId);
                return false;
            }

            if (_notice0.Matches(in notice))
            {
                _notice0.Refresh(in notice);
                Accept();
                Version++;
                return true;
            }

            if (_notice1.Matches(in notice))
            {
                _notice1.Refresh(in notice);
                Accept();
                Version++;
                return true;
            }

            NoticeSlot incoming = NoticeSlot.Create(in notice, NextNoticeSequence());
            if (!_notice0.Active)
            {
                _notice0 = incoming;
                Accept();
                Version++;
                return true;
            }

            if (!_notice1.Active)
            {
                _notice1 = _notice0;
                _notice0 = incoming;
                Accept();
                Version++;
                return true;
            }

            int incomingSeverity = MxUiGuidanceValidation.NoticeSeverity(notice.Tone);
            bool replace0 = MxUiGuidanceValidation.NoticeSeverity(_notice0.Notice.Tone) < incomingSeverity;
            bool replace1 = MxUiGuidanceValidation.NoticeSeverity(_notice1.Notice.Tone) < incomingSeverity;
            if (!replace0 && !replace1)
            {
                NoticeOverflowCount++;
                Reject(MxUiGuidanceRejectionReason.NoticeCapacity, notice.ScopeId, notice.MessageId);
                return false;
            }

            if (replace1 && (!replace0 || _notice1.Sequence < _notice0.Sequence))
            {
                _notice1 = _notice0;
                _notice0 = incoming;
            }
            else
            {
                _notice0 = incoming;
            }

            Accept();
            Version++;
            return true;
        }

        /// <summary>Clears the card and notices owned by one scope.</summary>
        /// <remarks>NoAlloc.</remarks>
        public void ClearScope(MxUiGuidanceScopeId scopeId)
        {
            if (!scopeId.IsValid)
            {
                InvalidScopeCount++;
                Reject(MxUiGuidanceRejectionReason.InvalidScope, scopeId, default);
                return;
            }

            bool changed = false;
            if (HasCard && Card.ScopeId == scopeId)
            {
                HasCard = false;
                Card = default;
                changed = true;
            }

            if (_notice0.Active && _notice0.Notice.ScopeId == scopeId)
            {
                _notice0 = default;
                changed = true;
            }

            if (_notice1.Active && _notice1.Notice.ScopeId == scopeId)
            {
                _notice1 = default;
                changed = true;
            }

            PackNoticeSlots();
            Accept();
            if (changed)
                Version++;
        }

        /// <summary>Advances fixed notice lifetimes and removes expired slots.</summary>
        /// <remarks>NoAlloc. Invalid or non-positive deltas are ignored.</remarks>
        public void Advance(float deltaSeconds)
        {
            if (!(deltaSeconds > 0f) || float.IsNaN(deltaSeconds) || float.IsInfinity(deltaSeconds))
                return;

            bool expired0 = _notice0.Advance(deltaSeconds);
            bool expired1 = _notice1.Advance(deltaSeconds);
            if (!expired0 && !expired1)
                return;

            PackNoticeSlots();
            Version++;
        }

        /// <summary>Reads one of the two visible slots, ordered newest first.</summary>
        /// <remarks>NoAlloc.</remarks>
        public bool TryGetNotice(int index, out MxUiGuidanceNoticeState state)
        {
            NoticeSlot slot;
            if (index == 0)
                slot = _notice0;
            else if (index == 1)
                slot = _notice1;
            else
            {
                state = default;
                return false;
            }

            if (!slot.Active)
            {
                state = default;
                return false;
            }

            state = slot.ToState();
            return true;
        }

        private long NextNoticeSequence()
        {
            long value = _nextNoticeSequence;
            _nextNoticeSequence = value == long.MaxValue ? 1L : value + 1L;
            return value;
        }

        private void PackNoticeSlots()
        {
            if (!_notice0.Active && _notice1.Active)
            {
                _notice0 = _notice1;
                _notice1 = default;
            }
        }

        private void Accept()
        {
            LastRejectionReason = MxUiGuidanceRejectionReason.None;
            LastRejectedScopeId = default;
            LastRejectedMessageId = default;
        }

        private void Reject(
            MxUiGuidanceRejectionReason reason,
            MxUiGuidanceScopeId scopeId,
            MxUiGuidanceMessageId messageId)
        {
            LastRejectionReason = reason;
            LastRejectedScopeId = scopeId;
            LastRejectedMessageId = messageId;
        }

        private struct NoticeSlot
        {
            public MxUiGuidanceNotice Notice;
            public float ElapsedSeconds;
            public float RemainingSeconds;
            public float DurationSeconds;
            public long Sequence;
            public long Revision;

            public bool Active => Sequence > 0L && RemainingSeconds > 0f;

            public static NoticeSlot Create(in MxUiGuidanceNotice notice, long sequence)
            {
                float duration = MxUiGuidanceValidation.ResolveNoticeDurationSeconds(
                    notice.Tone,
                    notice.DurationSeconds);
                return new NoticeSlot
                {
                    Notice = notice,
                    ElapsedSeconds = 0f,
                    RemainingSeconds = duration,
                    DurationSeconds = duration,
                    Sequence = sequence,
                    Revision = 1L
                };
            }

            public bool Matches(in MxUiGuidanceNotice notice)
            {
                return Active
                    && Notice.ScopeId == notice.ScopeId
                    && Notice.MessageId == notice.MessageId;
            }

            public void Refresh(in MxUiGuidanceNotice notice)
            {
                Notice = notice;
                DurationSeconds = MxUiGuidanceValidation.ResolveNoticeDurationSeconds(
                    notice.Tone,
                    notice.DurationSeconds);
                ElapsedSeconds = 0f;
                RemainingSeconds = DurationSeconds;
                Revision++;
            }

            public bool Advance(float deltaSeconds)
            {
                if (!Active)
                    return false;

                ElapsedSeconds += deltaSeconds;
                RemainingSeconds -= deltaSeconds;
                if (RemainingSeconds > 0f)
                    return false;

                this = default;
                return true;
            }

            public MxUiGuidanceNoticeState ToState()
            {
                return new MxUiGuidanceNoticeState(
                    in Notice,
                    ElapsedSeconds,
                    RemainingSeconds,
                    DurationSeconds,
                    Sequence,
                    Revision);
            }
        }
    }
}
