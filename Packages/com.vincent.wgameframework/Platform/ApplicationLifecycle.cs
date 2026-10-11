using System;

namespace MxFramework.Platform
{
    /// <summary>
    /// Contains orthogonal application lifecycle facts at a local revision.
    /// </summary>
    /// <remarks>NoAlloc when copied or inspected.</remarks>
    public readonly struct ApplicationLifecycleSnapshot : IEquatable<ApplicationLifecycleSnapshot>
    {
        /// <summary>
        /// Creates a lifecycle snapshot.
        /// </summary>
        /// <remarks>NoAlloc for valid input. Invalid revision diagnostics are LowFreqAlloc.</remarks>
        public ApplicationLifecycleSnapshot(
            bool hasFocus,
            bool isSuspended,
            bool isQuitting,
            long revision)
        {
            if (revision < 0)
                throw new ArgumentOutOfRangeException(nameof(revision));

            HasFocus = hasFocus;
            IsSuspended = isSuspended;
            IsQuitting = isQuitting;
            Revision = revision;
        }

        public bool HasFocus { get; }
        public bool IsSuspended { get; }
        public bool IsQuitting { get; }
        public long Revision { get; }

        public bool Equals(ApplicationLifecycleSnapshot other)
        {
            return HasFocus == other.HasFocus
                && IsSuspended == other.IsSuspended
                && IsQuitting == other.IsQuitting
                && Revision == other.Revision;
        }

        public override bool Equals(object obj)
        {
            return obj is ApplicationLifecycleSnapshot other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hashCode = HasFocus.GetHashCode();
                hashCode = (hashCode * 397) ^ IsSuspended.GetHashCode();
                hashCode = (hashCode * 397) ^ IsQuitting.GetHashCode();
                hashCode = (hashCode * 397) ^ Revision.GetHashCode();
                return hashCode;
            }
        }

        public static bool operator ==(ApplicationLifecycleSnapshot left, ApplicationLifecycleSnapshot right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(ApplicationLifecycleSnapshot left, ApplicationLifecycleSnapshot right)
        {
            return !left.Equals(right);
        }
    }

    /// <summary>
    /// Identifies an effective lifecycle transition or a loss-aware state resynchronization.
    /// </summary>
    public enum ApplicationLifecycleChangeKind
    {
        Unknown = 0,
        FocusChanged = 1,
        SuspendChanged = 2,
        Quitting = 3,
        StateResyncRequired = 4
    }

    /// <summary>
    /// Contains one lifecycle transition or one complete lost-range resynchronization record.
    /// </summary>
    /// <remarks>NoAlloc when copied or inspected.</remarks>
    public readonly struct ApplicationLifecycleChange : IEquatable<ApplicationLifecycleChange>
    {
        internal ApplicationLifecycleChange(
            ApplicationLifecycleChangeKind kind,
            ApplicationLifecycleSnapshot snapshot,
            long lostRevisionStart,
            long lostRevisionEnd,
            long lostChangeCount,
            bool sawSuspendInLostRange,
            bool sawQuittingInLostRange)
        {
            Kind = kind;
            Snapshot = snapshot;
            LostRevisionStart = lostRevisionStart;
            LostRevisionEnd = lostRevisionEnd;
            LostChangeCount = lostChangeCount;
            SawSuspendInLostRange = sawSuspendInLostRange;
            SawQuittingInLostRange = sawQuittingInLostRange;
        }

        public ApplicationLifecycleChangeKind Kind { get; }
        public ApplicationLifecycleSnapshot Snapshot { get; }
        public long LostRevisionStart { get; }
        public long LostRevisionEnd { get; }
        public long LostChangeCount { get; }
        public bool SawSuspendInLostRange { get; }
        public bool SawQuittingInLostRange { get; }

        public bool Equals(ApplicationLifecycleChange other)
        {
            return Kind == other.Kind
                && Snapshot.Equals(other.Snapshot)
                && LostRevisionStart == other.LostRevisionStart
                && LostRevisionEnd == other.LostRevisionEnd
                && LostChangeCount == other.LostChangeCount
                && SawSuspendInLostRange == other.SawSuspendInLostRange
                && SawQuittingInLostRange == other.SawQuittingInLostRange;
        }

        public override bool Equals(object obj)
        {
            return obj is ApplicationLifecycleChange other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hashCode = (int)Kind;
                hashCode = (hashCode * 397) ^ Snapshot.GetHashCode();
                hashCode = (hashCode * 397) ^ LostRevisionStart.GetHashCode();
                hashCode = (hashCode * 397) ^ LostRevisionEnd.GetHashCode();
                hashCode = (hashCode * 397) ^ LostChangeCount.GetHashCode();
                hashCode = (hashCode * 397) ^ SawSuspendInLostRange.GetHashCode();
                hashCode = (hashCode * 397) ^ SawQuittingInLostRange.GetHashCode();
                return hashCode;
            }
        }

        public static bool operator ==(ApplicationLifecycleChange left, ApplicationLifecycleChange right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(ApplicationLifecycleChange left, ApplicationLifecycleChange right)
        {
            return !left.Equals(right);
        }
    }

    /// <summary>
    /// Accepts normalized lifecycle facts from a host adapter.
    /// </summary>
    public interface IApplicationLifecycleReporter
    {
        /// <remarks>NoAlloc after initialization.</remarks>
        bool ReportFocus(bool hasFocus);

        /// <remarks>NoAlloc after initialization.</remarks>
        bool ReportSuspend(bool isSuspended);

        /// <remarks>NoAlloc after initialization.</remarks>
        bool ReportQuitting();
    }

    /// <summary>
    /// Grants the application-lifetime lifecycle drain exactly once.
    /// </summary>
    public interface IApplicationLifecycleSource
    {
        /// <summary>Claims the sole drain owner.</summary>
        /// <remarks>LowFreqAlloc on first success. Later calls return false without an owner.</remarks>
        bool TryClaimDrain(out IApplicationLifecycleDrainOwner drainOwner);
    }

    /// <summary>The sole owner allowed to observe and drain one lifecycle source.</summary>
    public interface IApplicationLifecycleDrainOwner
    {
        /// <remarks>NoAlloc after initialization.</remarks>
        ApplicationLifecycleSnapshot Current { get; }

        /// <summary>
        /// Gets the cumulative saturating count of changes folded by overflow.
        /// </summary>
        /// <remarks>NoAlloc after initialization.</remarks>
        long DroppedChangeCount { get; }

        /// <summary>
        /// Dequeues one ordinary change or one sticky state-resynchronization record.
        /// </summary>
        /// <remarks>NoAlloc after initialization.</remarks>
        bool TryDequeue(out ApplicationLifecycleChange change);
    }
}
