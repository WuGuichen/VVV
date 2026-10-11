using System;
using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;
using System.Threading;

[assembly: InternalsVisibleTo("MxFramework.Application.SessionBridge")]
[assembly: InternalsVisibleTo("MxFramework.Application.PersistenceBridge")]
[assembly: InternalsVisibleTo("MxFramework.Application.PlatformBridge")]
[assembly: InternalsVisibleTo("MxFramework.Application.Unity")]
[assembly: InternalsVisibleTo("MxFramework.Tests")]

namespace MxFramework.ApplicationStartup
{
    /// <summary>Marker for a policy capability bound to one exact live session graph.</summary>
    /// <remarks>The marker exposes no Runtime, persistence, cleanup, or ownership API.</remarks>
    internal interface IApplicationLiveCheckpointCapability
    {
        /// <summary>Validates this candidate against the exact current capability.</summary>
        /// <remarks>NoAlloc, non-mutating, and fail-closed for unknown capability types.</remarks>
        bool CanReplace(IApplicationLiveCheckpointCapability currentCapability);
    }

    /// <summary>Mutually excludes publication and a durable save authority interval.</summary>
    public sealed class ApplicationPublicationSaveFence
    {
        private int _owner;

        internal bool TryAcquirePublication(out ApplicationPublicationSaveFenceLease lease) =>
            TryAcquire(1, out lease);
        internal bool TryAcquireSave(out ApplicationPublicationSaveFenceLease lease) =>
            TryAcquire(2, out lease);

        private bool TryAcquire(int owner, out ApplicationPublicationSaveFenceLease lease)
        {
            // Allocate before publishing ownership so allocation failure cannot poison the fence.
            var candidate = new ApplicationPublicationSaveFenceLease(this, owner);
            if (Interlocked.CompareExchange(ref _owner, owner, 0) != 0)
            {
                lease = null;
                return false;
            }
            lease = candidate;
            return true;
        }

        internal void Release(int owner)
        {
            if (Interlocked.CompareExchange(ref _owner, 0, owner) != owner)
                throw new InvalidOperationException("Publication/save fence lease ownership is inconsistent.");
        }
    }

    /// <summary>Exact-once lease for one publication or save authority interval.</summary>
    internal sealed class ApplicationPublicationSaveFenceLease : IDisposable
    {
        private ApplicationPublicationSaveFence _fence;
        private readonly int _owner;
        internal ApplicationPublicationSaveFenceLease(ApplicationPublicationSaveFence fence, int owner)
        {
            _fence = fence;
            _owner = owner;
        }
        public void Dispose()
        {
            ApplicationPublicationSaveFence fence = Interlocked.Exchange(ref _fence, null);
            if (fence != null) fence.Release(_owner);
        }
    }

    /// <summary>Callback invoked with the exact Current inside a publisher safe boundary.</summary>
    internal interface IApplicationLiveSafeBoundaryCallback
    {
        void Invoke(LiveApplicationState exactState);
    }

    internal enum ApplicationLiveSafeBoundaryDispatchStatus
    {
        Succeeded = 0,
        InvalidArgument = 1,
        Reentrant = 2
    }

    /// <summary>Opaque proof bound to one publisher and one exact published state.</summary>
    /// <remarks>Created at publication time. All property reads are NoAlloc.</remarks>
    public sealed class ApplicationLiveStateRevisionToken
    {
        private readonly object _publisherIdentity;
        private readonly LiveApplicationState _state;
        private long _nextCallbackSequence;
        private bool _callbackSequenceExhausted;

        internal ApplicationLiveStateRevisionToken(object publisherIdentity, LiveApplicationState state)
        {
            _publisherIdentity = publisherIdentity ?? throw new ArgumentNullException(nameof(publisherIdentity));
            _state = state ?? throw new ArgumentNullException(nameof(state));
        }

        public long Revision => _state.Revision;

        internal bool Matches(object publisherIdentity, LiveApplicationState state) =>
            ReferenceEquals(_publisherIdentity, publisherIdentity) && ReferenceEquals(_state, state);

        internal bool TryConsumeCallbackSequence(long sequence)
        {
            if (sequence < 0 || _callbackSequenceExhausted || sequence != _nextCallbackSequence)
                return false;
            if (sequence == long.MaxValue)
                _callbackSequenceExhausted = true;
            else
                _nextCallbackSequence = sequence + 1L;
            return true;
        }
    }

    /// <summary>Allocation-free callback executed only while its revision remains current.</summary>
    public interface IApplicationLiveRevisionCallback
    {
        void Invoke();
    }

    public enum ApplicationLiveFrameTickStatus
    {
        RuntimeAttemptSucceeded = 0,
        Boot = 1,
        FrameExhausted = 2,
        RuntimePumpUnavailable = 3,
        Reentrant = 4,
        NoRuntimeAttempt = 5,
        RuntimeAttemptFailed = 6
    }

    public readonly struct ApplicationLiveFrameTickResult
    {
        internal ApplicationLiveFrameTickResult(
            ApplicationLiveFrameTickStatus status,
            LiveApplicationState state,
            long frame)
        {
            Status = status;
            State = state;
            Frame = frame;
        }

        public ApplicationLiveFrameTickStatus Status { get; }
        public LiveApplicationState State { get; }
        public long Frame { get; }
        public bool HasRuntimeAttempt =>
            Status == ApplicationLiveFrameTickStatus.RuntimeAttemptSucceeded ||
            Status == ApplicationLiveFrameTickStatus.RuntimeAttemptFailed;
    }

    public enum ApplicationLiveCallbackDispatchStatus
    {
        Succeeded = 0,
        InvalidArgument = 1,
        StaleOrForeignToken = 2,
        OutOfSequence = 3,
        Reentrant = 4
    }

    /// <summary>The unique live pump owner granted by one state publisher.</summary>
    /// <remarks>Tick and callback dispatch are NoAlloc after initialization outside failure diagnostics.</remarks>
    public sealed class ApplicationLiveFrameDriver
    {
        private readonly LiveApplicationStatePublisher _publisher;
        private LiveApplicationState _seededState;
        private long _nextFrame;
        private bool _frameExhausted;

        internal ApplicationLiveFrameDriver(LiveApplicationStatePublisher publisher) =>
            _publisher = publisher ?? throw new ArgumentNullException(nameof(publisher));

        public LiveApplicationState Current => _publisher.Current;
        public bool TryGetCurrentRevisionToken(out ApplicationLiveStateRevisionToken token) =>
            _publisher.TryGetCurrentRevisionToken(this, out token);
        /// <summary>Pumps the exact current live state at one publisher safe boundary.</summary>
        /// <remarks>NoAlloc after initialization outside implementation-owned failure diagnostics.</remarks>
        public ApplicationLiveFrameTickResult Tick(double deltaTime, double elapsedTime = 0d) =>
            _publisher.TickLiveFrame(this, deltaTime, elapsedTime);
        public ApplicationLiveCallbackDispatchStatus TryDispatchCallback(
            ApplicationLiveStateRevisionToken token,
            long sequence,
            IApplicationLiveRevisionCallback callback) =>
            _publisher.TryDispatchLiveCallback(this, token, sequence, callback);

        internal void Seed(LiveApplicationState state)
        {
            if (ReferenceEquals(_seededState, state)) return;
            _seededState = state;
            _nextFrame = state.NextRuntimeFrame;
            _frameExhausted = false;
        }

        internal long NextFrame => _nextFrame;
        internal bool IsFrameExhausted => _frameExhausted;
        internal void PoisonSeededGeneration() => _frameExhausted = true;
        internal void ConsumeFrame()
        {
            if (_nextFrame == long.MaxValue) _frameExhausted = true;
            else _nextFrame++;
        }
    }

    /// <summary>Internal live pump authority retained only by an immutable live state.</summary>
        internal interface IApplicationLiveRuntimePump
    {
        void Tick(long frameIndex, double deltaTime, double elapsedTime);
        /// <summary>Returns the exact attempt result for one frame without throwing.</summary>
        ApplicationLiveRuntimeAttemptStatus GetTickAttemptStatus(long frameIndex);
    }

    internal enum ApplicationLiveRuntimeAttemptStatus
    {
        None = 0,
        Succeeded = 1,
        Failed = 2
    }

    public interface IApplicationLiveSession
    {
        string SessionId { get; }
    }

    public interface IApplicationLiveSessionLease : IApplicationLiveSession, IDisposable
    {
    }

    /// <summary>
    /// Optional exact-once ownership seam used by the live-state publisher to transfer a fully
    /// prepared candidate graph into live ownership.
    /// </summary>
    /// <remarks>
    /// <see cref="TryPreparePublicationHandoff"/> is LowFreqAlloc, must be repeatable while the
    /// candidate remains rollback-owned, and must retain that ownership on success, false, or
    /// exception. <see cref="ClaimPublicationHandoff"/> is NoAlloc, must not throw, and must
    /// atomically transfer every candidate owner or return false without mutation. Its result is an
    /// invariant proof: false must leave ownership unchanged and prevents publisher CAS. Only the
    /// publisher may invoke the claim after all fallible validation and allocation has completed.
    /// </remarks>
    public interface IApplicationLiveSessionPublicationHandoff
    {
        bool TryPreparePublicationHandoff(out ApplicationStartupError error);
        bool ClaimPublicationHandoff();
    }

    /// <summary>Immutable aggregate publication value. Creation is LowFreqAlloc; getters are NoAlloc.</summary>
    public sealed class LiveApplicationState
    {
        private sealed class LiveSessionView : IApplicationLiveSession
        {
            public LiveSessionView(string sessionId) => SessionId = sessionId ?? string.Empty;
            public string SessionId { get; }
        }

        private sealed class BootLease : IApplicationLiveSessionLease
        {
            public static readonly BootLease Instance = new BootLease();
            public string SessionId => "boot";
            public void Dispose() { }
        }

        private LiveApplicationState(
            long revision,
            bool hasRuntimeSession,
            long nextRuntimeFrame,
            IApplicationLiveSessionLease sessionLease,
            IApplicationLiveSession session = null,
            IApplicationLiveSessionPublicationHandoff publicationHandoff = null,
            IApplicationLiveRuntimePump runtimePump = null,
            IApplicationLiveCheckpointCapability checkpointCapability = null)
        {
            Revision = revision;
            HasRuntimeSession = hasRuntimeSession;
            NextRuntimeFrame = nextRuntimeFrame;
            SessionLease = sessionLease;
            Session = session ?? new LiveSessionView(sessionLease.SessionId);
            PublicationHandoff = publicationHandoff;
            RuntimePump = runtimePump;
            CheckpointCapability = checkpointCapability;
        }

        private int _publicationState;

        public long Revision { get; }
        public bool HasRuntimeSession { get; }
        public long NextRuntimeFrame { get; }
        public IApplicationLiveSession Session { get; }
        public bool IsCandidate => Revision < 0;

        internal IApplicationLiveSessionLease SessionLease { get; }
        internal IApplicationLiveSessionPublicationHandoff PublicationHandoff { get; }
        internal IApplicationLiveRuntimePump RuntimePump { get; }
        internal IApplicationLiveCheckpointCapability CheckpointCapability { get; }
        internal ApplicationLiveStateRevisionToken RevisionToken { get; private set; }

        public static LiveApplicationState CreateBootSentinel() => new LiveApplicationState(0, false, -1, BootLease.Instance);

        internal static LiveApplicationState CreateBootSentinel(object publisherIdentity)
        {
            var state = new LiveApplicationState(0, false, -1, BootLease.Instance);
            state.RevisionToken = new ApplicationLiveStateRevisionToken(publisherIdentity, state);
            return state;
        }

        public static LiveApplicationState CreateCandidate(
            bool hasRuntimeSession,
            long nextRuntimeFrame,
            IApplicationLiveSessionLease sessionLease) =>
            CreateCandidate(hasRuntimeSession, nextRuntimeFrame, sessionLease, null);

        public static LiveApplicationState CreateCandidate(
            bool hasRuntimeSession,
            long nextRuntimeFrame,
            IApplicationLiveSessionLease sessionLease,
            IApplicationLiveSessionPublicationHandoff publicationHandoff)
        {
            if (sessionLease == null)
            {
                throw new ArgumentNullException(nameof(sessionLease));
            }

            if ((hasRuntimeSession && nextRuntimeFrame < 0) || (!hasRuntimeSession && nextRuntimeFrame != -1))
            {
                throw new ArgumentOutOfRangeException(nameof(nextRuntimeFrame));
            }

            return new LiveApplicationState(
                -1,
                hasRuntimeSession,
                nextRuntimeFrame,
                sessionLease,
                null,
                publicationHandoff,
                sessionLease as IApplicationLiveRuntimePump);
        }

        internal static LiveApplicationState CreateCandidate(
            bool hasRuntimeSession,
            long nextRuntimeFrame,
            IApplicationLiveSessionLease sessionLease,
            IApplicationLiveSessionPublicationHandoff publicationHandoff,
            IApplicationLiveCheckpointCapability checkpointCapability)
        {
            if (sessionLease == null) throw new ArgumentNullException(nameof(sessionLease));
            if ((hasRuntimeSession && nextRuntimeFrame < 0) || (!hasRuntimeSession && nextRuntimeFrame != -1))
                throw new ArgumentOutOfRangeException(nameof(nextRuntimeFrame));
            if (!hasRuntimeSession && checkpointCapability != null)
                throw new ArgumentException("A checkpoint capability requires a runtime session.", nameof(checkpointCapability));
            return new LiveApplicationState(
                -1, hasRuntimeSession, nextRuntimeFrame, sessionLease, null,
                publicationHandoff, sessionLease as IApplicationLiveRuntimePump, checkpointCapability);
        }

        internal LiveApplicationState PublishAs(long revision, object publisherIdentity)
        {
            if (!IsCandidate || revision <= 0)
            {
                throw new InvalidOperationException("Only an unpublished candidate can receive a live revision.");
            }

            var state = new LiveApplicationState(
                revision,
                HasRuntimeSession,
                NextRuntimeFrame,
                SessionLease,
                Session,
                null,
                RuntimePump,
                CheckpointCapability);
            state.RevisionToken = new ApplicationLiveStateRevisionToken(publisherIdentity, state);
            return state;
        }

        internal bool TryReservePublication() => Interlocked.CompareExchange(ref _publicationState, 1, 0) == 0;

        internal void ReleasePublicationReservation()
        {
            if (Interlocked.CompareExchange(ref _publicationState, 0, 1) != 1)
            {
                throw new InvalidOperationException("Only a reserved candidate publication can be released.");
            }
        }

        internal bool ClaimPublication()
        {
            // This is the final no-failure handoff interval. Implementations of the optional
            // capability are contractually required to be NoAlloc and non-throwing here.
            if (PublicationHandoff != null && !PublicationHandoff.ClaimPublicationHandoff())
                return false;
            Volatile.Write(ref _publicationState, 2);
            return true;
        }
    }

    public enum ApplicationStartupCommitStatus
    {
        Succeeded = 0,
        PublicationConflict = 1,
        Failed = 2,
        PublicationDeferred = 3
    }

    public readonly struct ApplicationStartupCommitResult
    {
        public ApplicationStartupCommitResult(ApplicationStartupCommitStatus status, ApplicationStartupError error)
        {
            Status = status;
            Error = error;
            PublishedState = null;
        }

        public ApplicationStartupCommitResult(ApplicationStartupCommitStatus status, ApplicationStartupError error, LiveApplicationState publishedState)
        {
            if (status == ApplicationStartupCommitStatus.Succeeded && publishedState == null)
            {
                throw new ArgumentNullException(nameof(publishedState));
            }

            if (status != ApplicationStartupCommitStatus.Succeeded && publishedState != null)
            {
                throw new ArgumentException("Only a successful publication may carry a published state.", nameof(publishedState));
            }

            Status = status;
            Error = error;
            PublishedState = publishedState;
        }

        public ApplicationStartupCommitStatus Status { get; }
        public ApplicationStartupError Error { get; }
        public LiveApplicationState PublishedState { get; }
        public bool Success => Status == ApplicationStartupCommitStatus.Succeeded && PublishedState != null;
        [Obsolete("Successful publication must carry the exact publisher receipt. Use Published(state).")]
        public static ApplicationStartupCommitResult Succeeded => new ApplicationStartupCommitResult(ApplicationStartupCommitStatus.Succeeded, default);
        public static ApplicationStartupCommitResult Published(LiveApplicationState publishedState) => new ApplicationStartupCommitResult(ApplicationStartupCommitStatus.Succeeded, default, publishedState);
    }

    public readonly struct ApplicationStartupRollbackResult
    {
        public ApplicationStartupRollbackResult(bool success, ApplicationStartupError error)
        {
            Success = success;
            Error = error;
        }

        public bool Success { get; }
        public ApplicationStartupError Error { get; }
        public static ApplicationStartupRollbackResult Succeeded => new ApplicationStartupRollbackResult(true, default);
    }

    public enum ApplicationStartupPublicationOutcome
    {
        NotAttempted = 0,
        NotPublished = 1,
        Published = 2
    }

    /// <summary>Deterministic observation used to resolve a TryCommit exception without guessing publication ownership.</summary>
    public readonly struct ApplicationStartupCommitObservation
    {
        public ApplicationStartupCommitObservation(ApplicationStartupPublicationOutcome outcome, LiveApplicationState publishedState)
        {
            if (outcome == ApplicationStartupPublicationOutcome.Published && publishedState == null)
            {
                throw new ArgumentNullException(nameof(publishedState));
            }

            if (outcome != ApplicationStartupPublicationOutcome.Published && publishedState != null)
            {
                throw new ArgumentException("Only a published observation may carry a published state.", nameof(publishedState));
            }

            Outcome = outcome;
            PublishedState = publishedState;
        }

        public ApplicationStartupPublicationOutcome Outcome { get; }
        public LiveApplicationState PublishedState { get; }
        public static ApplicationStartupCommitObservation NotAttempted => default;
        public static ApplicationStartupCommitObservation NotPublished => new ApplicationStartupCommitObservation(ApplicationStartupPublicationOutcome.NotPublished, null);
        public static ApplicationStartupCommitObservation Published(LiveApplicationState state) => new ApplicationStartupCommitObservation(ApplicationStartupPublicationOutcome.Published, state);
    }

    public interface ILiveApplicationStatePublisher
    {
        LiveApplicationState Current { get; }
        ApplicationStartupCommitResult TryPublish(LiveApplicationState expected, LiveApplicationState candidate);
    }

    /// <summary>
    /// Composition-root publisher using one locked compare-exchange and a bounded retired-state queue.
    /// Publication is LowFreqAlloc; Current and retirement dequeue are NoAlloc.
    /// </summary>
    public sealed class LiveApplicationStatePublisher : ILiveApplicationStatePublisher
    {
        private readonly object _gate = new object();
        private readonly object _publisherIdentity = new object();
        private readonly LiveApplicationStateRetirement[] _retired;
        private LiveApplicationState _current;
        private ApplicationLiveFrameDriver _frameDriver;
        private int _retiredHead;
        private int _retiredCount;
        private bool _publicationHandoffInProgress;
        private bool _liveBoundaryInProgress;
        private readonly ApplicationPublicationSaveFence _publicationSaveFence;
        private LiveApplicationState _lastSuccessfulTickState;

        public LiveApplicationStatePublisher(
            int retirementCapacity = 8,
            ApplicationPublicationSaveFence publicationSaveFence = null)
        {
            if (retirementCapacity <= 0 || retirementCapacity > 1024)
            {
                throw new ArgumentOutOfRangeException(nameof(retirementCapacity));
            }

            _retired = new LiveApplicationStateRetirement[retirementCapacity];
            _publicationSaveFence = publicationSaveFence ?? new ApplicationPublicationSaveFence();
            _current = LiveApplicationState.CreateBootSentinel(_publisherIdentity);
        }

        public LiveApplicationState Current => Volatile.Read(ref _current);
        internal int RetirementCapacity => _retired.Length;
        internal bool UsesPublicationSaveFence(ApplicationPublicationSaveFence fence) =>
            ReferenceEquals(_publicationSaveFence, fence);
        public int RetiredCount
        {
            get
            {
                lock (_gate)
                {
                    return _retiredCount;
                }
            }
        }

        public ApplicationStartupCommitResult TryPublish(LiveApplicationState expected, LiveApplicationState candidate)
        {
            if (expected == null || candidate == null || !candidate.IsCandidate)
            {
                return CreateFailure(ApplicationStartupCommitStatus.Failed, ApplicationStartupErrorCode.CommitFailed, "Publication requires a current expected state and an unpublished candidate.");
            }
            if (candidate.HasRuntimeSession && candidate.RuntimePump == null)
            {
                return CreateFailure(ApplicationStartupCommitStatus.Failed, ApplicationStartupErrorCode.CommitFailed, "A runtime live state requires an internal runtime pump capability.");
            }

            lock (_gate)
            {
                if (_publicationHandoffInProgress || _liveBoundaryInProgress)
                {
                    return CreateFailure(ApplicationStartupCommitStatus.Failed, ApplicationStartupErrorCode.CommitFailed, "Reentrant publication from a live publisher boundary is not allowed.");
                }

                if (!ReferenceEquals(_current, expected))
                {
                    return CreateFailure(ApplicationStartupCommitStatus.PublicationConflict, ApplicationStartupErrorCode.PublicationConflict, "Expected live state is stale.");
                }


                if (!_publicationSaveFence.TryAcquirePublication(out ApplicationPublicationSaveFenceLease publicationLease))
                {
                    return CreateFailure(ApplicationStartupCommitStatus.PublicationDeferred, ApplicationStartupErrorCode.CommitFailed, "Publication is deferred while a durable save owns the shared fence.");
                }

                using (publicationLease)
                {

                IApplicationLiveCheckpointCapability expectedCapability = expected.CheckpointCapability;
                IApplicationLiveCheckpointCapability candidateCapability = candidate.CheckpointCapability;
                if (expectedCapability != null || candidateCapability != null)
                {
                    bool compatible;
                    try
                    {
                        compatible = candidateCapability != null && candidateCapability.CanReplace(expectedCapability);
                    }
                    catch (Exception exception)
                    {
                        return new ApplicationStartupCommitResult(
                            ApplicationStartupCommitStatus.Failed,
                            new ApplicationStartupError(ApplicationStartupErrorCode.CommitFailed, ApplicationStartupPhase.Commit, default, default, null, "Checkpoint provenance validation failed before publication mutation.", exception));
                    }
                    if (!compatible)
                    {
                        return CreateFailure(ApplicationStartupCommitStatus.PublicationConflict, ApplicationStartupErrorCode.PublicationConflict, "Candidate checkpoint provenance is stale or foreign.");
                    }
                }

                if (_retiredCount == _retired.Length)
                {
                    return CreateFailure(ApplicationStartupCommitStatus.Failed, ApplicationStartupErrorCode.CleanupIncomplete, "Retirement queue is full.");
                }

                if (ReferenceEquals(candidate.SessionLease, expected.SessionLease))
                {
                    return CreateFailure(ApplicationStartupCommitStatus.Failed, ApplicationStartupErrorCode.CommitFailed, "Candidate lease is already owned by the current live state.");
                }

                for (int i = 0; i < _retired.Length; i++)
                {
                    LiveApplicationStateRetirement reserved = _retired[i];
                    if (reserved != null && ReferenceEquals(candidate.SessionLease, reserved.OwnerLease))
                    {
                        return CreateFailure(ApplicationStartupCommitStatus.Failed, ApplicationStartupErrorCode.CommitFailed, "Candidate lease is still owned by a retired live state.");
                    }
                }

                long revision;
                try
                {
                    revision = checked(expected.Revision + 1L);
                }
                catch (OverflowException exception)
                {
                    return new ApplicationStartupCommitResult(
                        ApplicationStartupCommitStatus.Failed,
                        new ApplicationStartupError(ApplicationStartupErrorCode.CommitFailed, ApplicationStartupPhase.Commit, default, default, null, "Live revision is exhausted.", exception));
                }

                int slot = FindEmptyRetirementSlot();
                if (!candidate.TryReservePublication())
                {
                    return CreateFailure(ApplicationStartupCommitStatus.Failed, ApplicationStartupErrorCode.CommitFailed, "Candidate is already reserved or was consumed by another publication.");
                }

                LiveApplicationState published;
                LiveApplicationStateRetirement retirement;
                bool handoffIntervalEntered = false;
                try
                {
                    try
                    {
                        // Complete every fallible allocation before the handoff preflight reserves
                        // rollback against the final no-failure claim interval.
                        published = candidate.PublishAs(revision, _publisherIdentity);
                        retirement = new LiveApplicationStateRetirement(this, slot, expected, expected.SessionLease);

                        IApplicationLiveSessionPublicationHandoff handoff = candidate.PublicationHandoff;
                        if (handoff != null)
                        {
                            ApplicationStartupError handoffError;
                            _publicationHandoffInProgress = true;
                            handoffIntervalEntered = true;
                            bool prepared = handoff.TryPreparePublicationHandoff(out handoffError);
                            if (!prepared)
                            {
                                candidate.ReleasePublicationReservation();
                                return CreateHandoffFailure(handoffError);
                            }
                        }
                    }
                    catch (Exception exception)
                    {
                        candidate.ReleasePublicationReservation();
                        return new ApplicationStartupCommitResult(
                            ApplicationStartupCommitStatus.Failed,
                            new ApplicationStartupError(ApplicationStartupErrorCode.CommitFailed, ApplicationStartupPhase.Commit, default, default, null, "Publication preflight failed before ownership handoff.", exception));
                    }

                    if (!candidate.ClaimPublication())
                    {
                        return CreateFailure(
                            ApplicationStartupCommitStatus.Failed,
                            ApplicationStartupErrorCode.CommitFailed,
                            "Publication ownership claim contradicted its successful preflight.");
                    }
                    LiveApplicationState observed = Interlocked.CompareExchange(ref _current, published, expected);
                    if (!ReferenceEquals(observed, expected))
                    {
                        throw new InvalidOperationException("Live state changed inside the publisher's exclusive no-failure handoff interval.");
                    }

                    _retired[slot] = retirement;
                    _retiredCount++;
                    return ApplicationStartupCommitResult.Published(published);
                }
                finally
                {
                    if (handoffIntervalEntered)
                    {
                        _publicationHandoffInProgress = false;
                    }
                }
                }
            }
        }

        /// <summary>Dispatches against the exact Current without consuming frame callback ordering.</summary>
        /// <remarks>NoAlloc after initialization for a conforming callback.</remarks>
        internal ApplicationLiveSafeBoundaryDispatchStatus TryDispatchSafeBoundary(
            IApplicationLiveSafeBoundaryCallback callback)
        {
            if (callback == null) return ApplicationLiveSafeBoundaryDispatchStatus.InvalidArgument;
            LiveApplicationState state;
            lock (_gate)
            {
                if (_liveBoundaryInProgress || _publicationHandoffInProgress)
                    return ApplicationLiveSafeBoundaryDispatchStatus.Reentrant;
                state = _current;
                _liveBoundaryInProgress = true;
            }
            try
            {
                callback.Invoke(state);
                return ApplicationLiveSafeBoundaryDispatchStatus.Succeeded;
            }
            finally
            {
                lock (_gate) _liveBoundaryInProgress = false;
            }
        }

        public bool TryDequeueRetired(out LiveApplicationStateRetirement retirement)
        {
            lock (_gate)
            {
                if (_retiredCount == 0)
                {
                    retirement = null;
                    return false;
                }

                for (int offset = 0; offset < _retired.Length; offset++)
                {
                    int slot = (_retiredHead + offset) % _retired.Length;
                    LiveApplicationStateRetirement candidate = _retired[slot];
                    if (candidate != null && candidate.TryTransfer())
                    {
                        _retiredHead = (slot + 1) % _retired.Length;
                        retirement = candidate;
                        return true;
                    }
                }

                retirement = null;
                return false;
            }
        }

        /// <summary>Claims this publisher's unique live frame owner.</summary>
        /// <remarks>LowFreqAlloc on first success. A second claim returns false and no owner.</remarks>
        public bool TryClaimLiveFrameDriver(out ApplicationLiveFrameDriver driver)
        {
            lock (_gate)
            {
                if (_frameDriver != null)
                {
                    driver = null;
                    return false;
                }
                driver = new ApplicationLiveFrameDriver(this);
                _frameDriver = driver;
                return true;
            }
        }

        internal bool TryGetCurrentRevisionToken(
            ApplicationLiveFrameDriver driver,
            out ApplicationLiveStateRevisionToken token)
        {
            lock (_gate)
            {
                if (!ReferenceEquals(driver, _frameDriver))
                {
                    token = null;
                    return false;
                }
                token = _current.RevisionToken;
                return token != null;
            }
        }

        internal ApplicationLiveFrameTickResult TickLiveFrame(
            ApplicationLiveFrameDriver driver,
            double deltaTime,
            double elapsedTime)
        {
            LiveApplicationState state;
            IApplicationLiveRuntimePump runtimePump;
            long frame;
            lock (_gate)
            {
                state = _current;
                if (!ReferenceEquals(driver, _frameDriver) ||
                    _liveBoundaryInProgress || _publicationHandoffInProgress)
                    return new ApplicationLiveFrameTickResult(
                        ApplicationLiveFrameTickStatus.Reentrant, state, -1L);

                driver.Seed(state);
                if (!state.HasRuntimeSession)
                    return new ApplicationLiveFrameTickResult(
                        ApplicationLiveFrameTickStatus.Boot, state, -1L);
                if (driver.IsFrameExhausted)
                    return new ApplicationLiveFrameTickResult(
                        ApplicationLiveFrameTickStatus.FrameExhausted, state, long.MaxValue);
                runtimePump = state.RuntimePump;
                if (runtimePump == null)
                    return new ApplicationLiveFrameTickResult(
                        ApplicationLiveFrameTickStatus.RuntimePumpUnavailable, state, driver.NextFrame);

                frame = driver.NextFrame;
                _liveBoundaryInProgress = true;
            }

            ApplicationLiveRuntimeAttemptStatus attemptStatus = ApplicationLiveRuntimeAttemptStatus.None;
            Exception tickException = null;
            Exception observationException = null;
            try
            {
                runtimePump.Tick(frame, deltaTime, elapsedTime);
            }
            catch (Exception exception)
            {
                tickException = exception;
            }
            finally
            {
                lock (_gate)
                {
                    try
                    {
                        attemptStatus = runtimePump.GetTickAttemptStatus(frame);
                        if (attemptStatus != ApplicationLiveRuntimeAttemptStatus.None)
                            driver.ConsumeFrame();
                        if (tickException == null &&
                            attemptStatus == ApplicationLiveRuntimeAttemptStatus.Succeeded &&
                            ReferenceEquals(_current, state))
                            _lastSuccessfulTickState = state;
                    }
                    catch (Exception exception)
                    {
                        observationException = exception;
                        driver.PoisonSeededGeneration();
                    }
                    finally
                    {
                        _liveBoundaryInProgress = false;
                    }
                }
            }

            if (tickException != null)
                ExceptionDispatchInfo.Capture(tickException).Throw();
            if (observationException != null)
                throw new InvalidOperationException(
                    "The live runtime pump attempt observation contract threw.",
                    observationException);

            return new ApplicationLiveFrameTickResult(
                attemptStatus == ApplicationLiveRuntimeAttemptStatus.Succeeded
                    ? ApplicationLiveFrameTickStatus.RuntimeAttemptSucceeded
                    : attemptStatus == ApplicationLiveRuntimeAttemptStatus.Failed
                        ? ApplicationLiveFrameTickStatus.RuntimeAttemptFailed
                        : ApplicationLiveFrameTickStatus.NoRuntimeAttempt,
                state,
                frame);
        }

        internal ApplicationLiveCallbackDispatchStatus TryDispatchLiveCallback(
            ApplicationLiveFrameDriver driver,
            ApplicationLiveStateRevisionToken token,
            long sequence,
            IApplicationLiveRevisionCallback callback)
        {
            if (token == null || callback == null || sequence < 0)
                return ApplicationLiveCallbackDispatchStatus.InvalidArgument;
            lock (_gate)
            {
                if (!ReferenceEquals(driver, _frameDriver) ||
                    _liveBoundaryInProgress || _publicationHandoffInProgress)
                    return ApplicationLiveCallbackDispatchStatus.Reentrant;
                LiveApplicationState state = _current;
                if (!ReferenceEquals(token, state.RevisionToken) ||
                    !token.Matches(_publisherIdentity, state))
                    return ApplicationLiveCallbackDispatchStatus.StaleOrForeignToken;
                if (!token.TryConsumeCallbackSequence(sequence))
                    return ApplicationLiveCallbackDispatchStatus.OutOfSequence;

                _liveBoundaryInProgress = true;
            }

            try
            {
                callback.Invoke();
                return ApplicationLiveCallbackDispatchStatus.Succeeded;
            }
            finally
            {
                lock (_gate)
                {
                    _liveBoundaryInProgress = false;
                }
            }
        }

        internal bool IsCheckpointQualified(LiveApplicationState state)
        {
            lock (_gate)
            {
                return ReferenceEquals(_current, state)
                    && ReferenceEquals(_lastSuccessfulTickState, state);
            }
        }

        private int FindEmptyRetirementSlot()
        {
            for (int i = 0; i < _retired.Length; i++)
            {
                if (_retired[i] == null)
                {
                    return i;
                }
            }

            throw new InvalidOperationException("Retirement capacity accounting is inconsistent.");
        }

        internal IApplicationLiveSessionLease GetRetirementLease(LiveApplicationStateRetirement retirement, int slot)
        {
            lock (_gate)
            {
                if ((uint)slot >= (uint)_retired.Length || !ReferenceEquals(_retired[slot], retirement))
                {
                    return null;
                }

                return retirement.OwnerLease;
            }
        }

        internal void CompleteRetirement(LiveApplicationStateRetirement retirement, int slot)
        {
            lock (_gate)
            {
                if ((uint)slot >= (uint)_retired.Length || !ReferenceEquals(_retired[slot], retirement))
                {
                    return;
                }

                _retired[slot] = null;
                _retiredCount--;
            }
        }

        private static ApplicationStartupCommitResult CreateFailure(ApplicationStartupCommitStatus status, ApplicationStartupErrorCode code, string message)
        {
            return new ApplicationStartupCommitResult(
                status,
                new ApplicationStartupError(code, ApplicationStartupPhase.Commit, default, default, null, message, null));
        }

        private static ApplicationStartupCommitResult CreateHandoffFailure(ApplicationStartupError error)
        {
            return error.IsNone
                ? CreateFailure(ApplicationStartupCommitStatus.Failed, ApplicationStartupErrorCode.CommitFailed, "Candidate publication handoff preflight failed.")
                : new ApplicationStartupCommitResult(ApplicationStartupCommitStatus.Failed, error);
        }
    }

    /// <summary>Exact-once owner capability transferred only when a retired state is dequeued for cleanup.</summary>
    /// <remarks>
    /// Construction is LowFreqAlloc. BeginRetirement may allocate through a bridge-owned retirement
    /// source; steady TickRetirement and evidence reads are NoAlloc after initialization. A failed
    /// retirement permanently poisons its bounded publisher reservation.
    /// </remarks>
    public sealed class LiveApplicationStateRetirement : IDisposable
    {
        private readonly LiveApplicationStatePublisher _publisher;
        private readonly int _slot;
        private readonly IApplicationLiveSessionLease _lease;
        private readonly ApplicationLiveRetirementDriver _driver;
        private int _state;

        internal LiveApplicationStateRetirement(LiveApplicationStatePublisher publisher, int slot, LiveApplicationState state, IApplicationLiveSessionLease lease)
        {
            _publisher = publisher ?? throw new ArgumentNullException(nameof(publisher));
            _slot = slot;
            State = state ?? throw new ArgumentNullException(nameof(state));
            _lease = lease ?? throw new ArgumentNullException(nameof(lease));
            _driver = new ApplicationLiveRetirementDriver(state.Revision, state.Session.SessionId, lease);
        }

        public LiveApplicationState State { get; }
        public ApplicationLiveRetirementStatus RetirementStatus => _driver.Status;
        public float RetirementProgress => _driver.Progress;
        public MxFramework.Runtime.RuntimeOperationError RetirementError => _driver.Error;
        public int WarningCount => _driver.WarningCount;
        public ApplicationLiveRetirementOwnershipDebt OwnershipDebt => _driver.OwnershipDebt;
        public bool IsTerminal => _driver.IsTerminal;
        internal IApplicationLiveSessionLease OwnerLease => _lease;

        internal bool TryTransfer()
        {
            return Interlocked.CompareExchange(ref _state, 1, 0) == 0;
        }

        public ApplicationLiveRetirementWarning GetWarning(int index) => _driver.GetWarning(index);

        public void BeginRetirement()
        {
            if (Interlocked.CompareExchange(ref _state, 2, 1) != 1)
            {
                return;
            }

            if (_publisher.GetRetirementLease(this, _slot) == null)
            {
                Volatile.Write(ref _state, 4);
                return;
            }

            _driver.Begin();
            ObserveDriverTerminal();
        }

        public void TickRetirement()
        {
            if (Volatile.Read(ref _state) != 2)
            {
                return;
            }

            _driver.Tick();
            ObserveDriverTerminal();
        }

        public void Dispose()
        {
            BeginRetirement();
            TickRetirement();
        }

        private void ObserveDriverTerminal()
        {
            if (_driver.CanReleaseReservation)
            {
                if (Interlocked.CompareExchange(ref _state, 3, 2) == 2)
                {
                    _publisher.CompleteRetirement(this, _slot);
                }

                return;
            }

            if (_driver.Status == ApplicationLiveRetirementStatus.Failed)
            {
                Interlocked.CompareExchange(ref _state, 4, 2);
            }
        }
    }

    public interface IApplicationStartupTransaction : IDisposable
    {
        string TransactionId { get; }
        /// <summary>
        /// Last commit publication outcome. The value must become NotPublished or Published before
        /// TryCommit returns or throws, and PublishedState must be the exact immutable state installed
        /// by the publisher. Before exposing Published, the transaction must detach every rollback and
        /// disposal capability for the published lease; subsequent FinalizeRollback or Dispose calls
        /// must be unable to release that live/retired owner.
        /// </summary>
        /// <remarks>NoAlloc query.</remarks>
        ApplicationStartupCommitObservation CommitObservation { get; }
        ApplicationStartupCommitResult TryCommit(LiveApplicationState expected, ILiveApplicationStatePublisher publisher);
        ApplicationStartupRollbackResult FinalizeRollback();
    }

    public interface IApplicationStartupTransactionFactory
    {
        IApplicationStartupTransaction Create(ApplicationStartupPlanId planId);
    }
}
