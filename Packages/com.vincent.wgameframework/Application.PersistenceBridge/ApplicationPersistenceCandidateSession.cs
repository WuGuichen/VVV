using System;
using System.Threading;
using MxFramework.ApplicationStartup.SessionBridge;
using MxFramework.Persistence;
using MxFramework.Persistence.RuntimeBridge;
using MxFramework.Runtime;

namespace MxFramework.ApplicationStartup.PersistenceBridge
{
    /// <summary>
    /// Strong-exception build lease for a candidate restore coordinator constructed from the exact
    /// Runtime composition identity and host supplied to the builder.
    /// </summary>
    public interface IApplicationPersistenceCandidateCoordinatorBuildLease
    {
        ApplicationCandidateRuntimeCompositionIdentity CompositionIdentity { get; }
        RuntimeHost RuntimeHost { get; }
        RuntimeSaveStateCoordinator Coordinator { get; }
        RuntimeSaveGameService CheckpointService { get; }
        ApplicationPersistenceDurableHeadAuthority DurableHeadAuthority { get; }
        bool TryTransfer();
        /// <summary>Releases every owner retained by an untransferred build lease.</summary>
        /// <remarks>
        /// Must be idempotent and must not throw. Implementations retain their own cleanup
        /// diagnostics; the factory preserves the primary construction/transfer failure.
        /// </remarks>
        void Abandon();
    }

    public interface IApplicationPersistenceCandidateCoordinatorBuilder
    {
        /// <summary>
        /// Builds the authoritative coordinator from the supplied exact candidate generation.
        /// </summary>
        /// <remarks>AllocByDesign. The returned lease must echo both references exactly.</remarks>
        IApplicationPersistenceCandidateCoordinatorBuildLease Build(
            ApplicationCandidateRuntimeCompositionIdentity compositionIdentity,
            RuntimeHost runtimeHost);
    }

    public static class ApplicationPersistenceCandidateSessionFactory
    {
        /// <summary>
        /// Atomically binds one frozen graph to a coordinator built from its unforgeable generation
        /// identity and exact RuntimeHost.
        /// </summary>
        /// <remarks>AllocByDesign.</remarks>
        public static ApplicationPersistenceCandidateSession Create(
            string targetId,
            ApplicationCandidateSessionGraph graph,
            IApplicationPersistenceCandidateCoordinatorBuilder coordinatorBuilder)
        {
            if (string.IsNullOrWhiteSpace(targetId))
                throw new ArgumentException("A candidate target id is required.", nameof(targetId));
            if (graph == null) throw new ArgumentNullException(nameof(graph));
            if (coordinatorBuilder == null) throw new ArgumentNullException(nameof(coordinatorBuilder));
            if (!graph.TryGetFrozenHandoffLease(out _) || !graph.IsRuntimeUntickedAndReady() ||
                !graph.IsAppFlowUntickedAndReady())
                throw new ArgumentException("Candidate graph must be frozen, ready, and unticked.", nameof(graph));

            IApplicationPersistenceCandidateCoordinatorBuildLease buildLease = null;
            try
            {
                buildLease = coordinatorBuilder.Build(graph.RuntimeCompositionIdentity, graph.RuntimeHost);
                ApplicationCandidateRuntimeCompositionIdentity compositionIdentity =
                    buildLease?.CompositionIdentity;
                RuntimeHost runtimeHost = buildLease?.RuntimeHost;
                RuntimeSaveStateCoordinator coordinator = buildLease?.Coordinator;
                RuntimeSaveGameService checkpointService = buildLease?.CheckpointService;
                ApplicationPersistenceDurableHeadAuthority durableHeadAuthority =
                    buildLease?.DurableHeadAuthority;
                if (buildLease == null ||
                    !ReferenceEquals(compositionIdentity, graph.RuntimeCompositionIdentity) ||
                    !ReferenceEquals(runtimeHost, graph.RuntimeHost) ||
                    coordinator == null || !coordinator.IsAuthoritative ||
                    coordinator.RestoreProfile == null || checkpointService == null ||
                    durableHeadAuthority == null ||
                    !checkpointService.IsBoundTo(runtimeHost, coordinator, coordinator.RestoreProfile))
                    throw new InvalidOperationException("Candidate coordinator build lease is contradictory.");

                var session = new ApplicationPersistenceCandidateSession(
                    targetId, graph, coordinator, checkpointService, durableHeadAuthority);
                if (!buildLease.TryTransfer())
                    throw new InvalidOperationException("Candidate coordinator build lease was already consumed.");
                return session;
            }
            catch
            {
                if (buildLease != null)
                {
                    try { buildLease.Abandon(); }
                    catch { }
                }
                throw;
            }
        }
    }

    public enum ApplicationPersistenceCandidateStatus
    {
        Created = 0,
        Restoring = 1,
        Restored = 2,
        PublicationCandidateCreated = 3,
        Published = 4,
        Poisoned = 5,
        Retiring = 6
    }

    /// <summary>
    /// Concrete outer-layer bridge joining one frozen SessionBridge graph to one Persistence
    /// candidate coordinator and one Application publication owner.
    /// </summary>
    /// <remarks>
    /// Construction and publication-candidate creation are LowFreqAlloc. Restore acquisition,
    /// completion, state reads, publication final claim, and retirement polling are NoAlloc after
    /// initialization. The candidate RuntimeHost is never ticked by this type.
    /// </remarks>
    public sealed class ApplicationPersistenceCandidateSession :
        IRuntimeSaveGameRestoreTarget,
        IApplicationLiveSessionPublicationHandoff,
        IDisposable
    {
        private readonly object _gate = new object();
        private readonly ApplicationCandidateSessionGraph _graph;
        private readonly RuntimeSaveStateCoordinator _coordinator;
        private readonly RuntimeSaveGameService _checkpointService;
        private readonly RuntimeSaveStateRestoreProfile _profile;
        private readonly ApplicationCandidateLiveSessionLease _liveLease;
        private readonly RestoreLease _restoreLease;
        private LiveApplicationState _publicationCandidate;
        private int _status;
        private long _completedFrame = -1;
        private long _nextRuntimeFrame = -1;
        private readonly ApplicationPersistenceLiveCheckpointCapability _checkpointCapability;

        internal ApplicationPersistenceCandidateSession(
            string targetId,
            ApplicationCandidateSessionGraph graph,
            RuntimeSaveStateCoordinator coordinator,
            RuntimeSaveGameService checkpointService,
            ApplicationPersistenceDurableHeadAuthority durableHeadAuthority)
        {
            _graph = graph;
            _coordinator = coordinator;
            _checkpointService = checkpointService;
            _profile = _coordinator.RestoreProfile;
            TargetId = targetId;
            _liveLease = new ApplicationCandidateLiveSessionLease(TargetId, _graph);
            _restoreLease = new RestoreLease(this);
            _checkpointCapability = new ApplicationPersistenceLiveCheckpointCapability(
                _checkpointService, durableHeadAuthority);
        }

        public string TargetId { get; }
        public RuntimeSaveStateRestoreProfile RestoreProfile => _profile;
        public ApplicationPersistenceCandidateStatus Status
        {
            get
            {
                ApplicationPersistenceCandidateStatus stored =
                    (ApplicationPersistenceCandidateStatus)Volatile.Read(ref _status);
                if (stored != ApplicationPersistenceCandidateStatus.PublicationCandidateCreated)
                    return stored;
                if (_liveLease.IsRetiring)
                    return ApplicationPersistenceCandidateStatus.Retiring;
                return _liveLease.HasLiveOwnership
                    ? ApplicationPersistenceCandidateStatus.Published
                    : stored;
            }
        }
        public long CompletedFrame => Volatile.Read(ref _completedFrame);
        public long NextRuntimeFrame => Volatile.Read(ref _nextRuntimeFrame);
        public bool IsCandidateUnticked => _graph.IsRuntimeUntickedAndReady() &&
                                           _graph.IsAppFlowUntickedAndReady();
        public IApplicationLiveRetirementOperation CandidateCleanupOperation =>
            _liveLease.CleanupOperation;
        internal IApplicationLiveSessionPublicationHandoff PublicationHandoff => this;
        internal IApplicationLiveSessionLease LiveSessionLease => _liveLease;

        public bool TryAcquireRestoreLease(
            RuntimeSaveStateRestoreProfile expectedRestoreProfile,
            out IRuntimeSaveGameCandidateRestoreLease lease,
            out RuntimeSaveGameTargetValidationError validationError)
        {
            lock (_gate)
            {
                if (!ReferenceEquals(expectedRestoreProfile, _profile) ||
                    !ReferenceEquals(_coordinator.RestoreProfile, _profile))
                {
                    lease = null;
                    validationError = Error(
                        RuntimeSaveGameTargetValidationErrorCode.InvalidComposition,
                        "candidate.restore-profile");
                    return false;
                }
                if (Status != ApplicationPersistenceCandidateStatus.Created ||
                    !_graph.TryGetFrozenHandoffLease(out _) || !IsCandidateUnticked ||
                    !_restoreLease.TryAcquire())
                {
                    lease = null;
                    validationError = Error(
                        RuntimeSaveGameTargetValidationErrorCode.Unavailable,
                        "candidate.restore-lease");
                    return false;
                }
                Volatile.Write(ref _status, (int)ApplicationPersistenceCandidateStatus.Restoring);
                lease = _restoreLease;
                validationError = RuntimeSaveGameTargetValidationError.None;
                return true;
            }
        }

        /// <summary>Creates the one reusable unpublished aggregate after restore completed.</summary>
        /// <remarks>LowFreqAlloc on first success; repeated success returns the same reference.</remarks>
        public bool TryCreatePublicationCandidate(
            out LiveApplicationState candidate,
            out RuntimeSaveGameTargetValidationError validationError)
        {
            lock (_gate)
            {
                ApplicationPersistenceCandidateStatus status = Status;
                if (status == ApplicationPersistenceCandidateStatus.PublicationCandidateCreated)
                {
                    candidate = _publicationCandidate;
                    validationError = RuntimeSaveGameTargetValidationError.None;
                    return true;
                }
                if (status != ApplicationPersistenceCandidateStatus.Restored || !IsCandidateUnticked ||
                    _nextRuntimeFrame < 0 || !_checkpointCapability.IsInitialized)
                {
                    candidate = null;
                    validationError = Error(
                        RuntimeSaveGameTargetValidationErrorCode.Unavailable,
                        "candidate.publication");
                    return false;
                }
                candidate = LiveApplicationState.CreateCandidate(
                    true,
                    _nextRuntimeFrame,
                    _liveLease,
                    this,
                    _checkpointCapability);
                _publicationCandidate = candidate;
                Volatile.Write(ref _status,
                    (int)ApplicationPersistenceCandidateStatus.PublicationCandidateCreated);
                validationError = RuntimeSaveGameTargetValidationError.None;
                return true;
            }
        }

        public void Dispose()
        {
            ApplicationPersistenceCandidateStatus status = Status;
            if (status < ApplicationPersistenceCandidateStatus.Published)
            {
                Poison();
                return;
            }
            // The publisher receipt is the sole live-retirement capability owner.
        }

        public bool TryPreparePublicationHandoff(out ApplicationStartupError error)
        {
            if (!_checkpointCapability.IsDurableProvenanceCurrent)
            {
                error = new ApplicationStartupError(
                    ApplicationStartupErrorCode.PublicationConflict,
                    ApplicationStartupPhase.Commit,
                    default,
                    RuntimeOperationError.None,
                    null,
                    "Candidate durable save provenance is stale.",
                    null);
                return false;
            }
            return _liveLease.TryPreparePublicationHandoff(out error);
        }

        public bool ClaimPublicationHandoff() =>
            _checkpointCapability.IsDurableProvenanceCurrent &&
            _liveLease.ClaimPublicationHandoff();

        /// <summary>Pumps one step of caller-owned cleanup for a poisoned unpublished candidate.</summary>
        /// <remarks>NoAlloc after cleanup operation initialization outside failure diagnostics.</remarks>
        public void TickCandidateCleanup()
        {
            IApplicationLiveRetirementOperation operation = _liveLease.CleanupOperation;
            if (operation != null)
                operation.Tick(default);
        }

        private bool TryCompleteRestore(
            RestoreLease restoreLease,
            in RuntimeSaveGameRestoreCompletion completion,
            out RuntimeSaveGameTargetValidationError validationError)
        {
            lock (_gate)
            {
                if (Volatile.Read(ref restoreLease._state) != 1 ||
                    Status != ApplicationPersistenceCandidateStatus.Restoring ||
                    !IsValidCompletion(in completion) || !IsCandidateUnticked)
                {
                    validationError = Error(
                        completion.CompletedFrame < 0 || completion.CompletedFrame == long.MaxValue ||
                        completion.NextFrame != completion.CompletedFrame + 1L
                            ? RuntimeSaveGameTargetValidationErrorCode.InvalidFrame
                            : RuntimeSaveGameTargetValidationErrorCode.CompletionRejected,
                        "candidate.complete");
                    return false;
                }
                if (!_checkpointCapability.TryInitialize(in completion))
                {
                    validationError = Error(
                        RuntimeSaveGameTargetValidationErrorCode.CompletionRejected,
                        "candidate.checkpoint-capability");
                    return false;
                }
                Volatile.Write(ref _completedFrame, completion.CompletedFrame);
                Volatile.Write(ref _nextRuntimeFrame, completion.NextFrame);
                Volatile.Write(ref _status, (int)ApplicationPersistenceCandidateStatus.Restored);
                Volatile.Write(ref restoreLease._state, 2);
                validationError = RuntimeSaveGameTargetValidationError.None;
                return true;
            }
        }

        private static bool IsValidCompletion(in RuntimeSaveGameRestoreCompletion completion) =>
            completion.Profile.IsValid && completion.Slot.IsValid &&
            completion.OriginalGeneration.IsPublished && completion.SelectedGeneration.IsPublished &&
            completion.SelectedGeneration.CompareTo(completion.OriginalGeneration) <= 0 &&
            (completion.RecoverySource == SaveGameRecoverySource.Primary ||
             completion.RecoverySource == SaveGameRecoverySource.LastKnownGood) &&
            completion.CreatedAtUtc.Kind == DateTimeKind.Utc &&
            completion.CompletedFrame >= 0 && completion.CompletedFrame != long.MaxValue &&
            completion.NextFrame == completion.CompletedFrame + 1L;

        private void Poison()
        {
            IApplicationLiveRetirementOperation cleanup = null;
            lock (_gate)
            {
                if (Status >= ApplicationPersistenceCandidateStatus.Published)
                    return;
                if (!_liveLease.TryPoisonAndBeginRollback(out cleanup))
                    return;
                Volatile.Write(ref _status, (int)ApplicationPersistenceCandidateStatus.Poisoned);
            }
            cleanup.Tick(default);
        }

        private void DisposeRestoreLease(RestoreLease restoreLease)
        {
            IApplicationLiveRetirementOperation cleanup = null;
            lock (_gate)
            {
                int previous = Interlocked.Exchange(ref restoreLease._state, 3);
                if (previous != 1 || Status >= ApplicationPersistenceCandidateStatus.Published ||
                    !_liveLease.TryPoisonAndBeginRollback(out cleanup))
                    return;
                Volatile.Write(ref _status, (int)ApplicationPersistenceCandidateStatus.Poisoned);
            }
            cleanup.Tick(default);
        }

        private static RuntimeSaveGameTargetValidationError Error(
            RuntimeSaveGameTargetValidationErrorCode code,
            string path) => new RuntimeSaveGameTargetValidationError(code, path);

        private sealed class RestoreLease : IRuntimeSaveGameCandidateRestoreLease
        {
            private readonly ApplicationPersistenceCandidateSession _owner;
            internal int _state;

            internal RestoreLease(ApplicationPersistenceCandidateSession owner) =>
                _owner = owner ?? throw new ArgumentNullException(nameof(owner));

            public string TargetId => _owner.TargetId;
            public RuntimeSaveStateCoordinator Coordinator => _owner._coordinator;
            public RuntimeSaveStateRestoreProfile RestoreProfile => _owner._profile;

            internal bool TryAcquire() => Interlocked.CompareExchange(ref _state, 1, 0) == 0;

            public bool TryCompleteRestore(
                in RuntimeSaveGameRestoreCompletion completion,
                out RuntimeSaveGameTargetValidationError validationError)
            {
                return _owner.TryCompleteRestore(
                    this, in completion, out validationError);
            }

            public void Dispose() => _owner.DisposeRestoreLease(this);
        }
    }

    /// <summary>Private state-bound authority for one exact live persistence checkpoint service.</summary>
    /// <remarks>Property reads are NoAlloc. No Runtime or persistence authority is publicly exposed.</remarks>
    internal sealed class ApplicationPersistenceLiveCheckpointCapability :
        IApplicationLiveCheckpointCapability
    {
        internal ApplicationPersistenceLiveCheckpointCapability(
            RuntimeSaveGameService checkpointService,
            ApplicationPersistenceDurableHeadAuthority durableHeadAuthority)
        {
            CheckpointService = checkpointService ?? throw new ArgumentNullException(nameof(checkpointService));
            _durableHeadAuthority = durableHeadAuthority ??
                throw new ArgumentNullException(nameof(durableHeadAuthority));
        }

        private readonly ApplicationPersistenceDurableHeadAuthority _durableHeadAuthority;
        private int _initialized;
        private long _expectedHeadBits;
        private long _durableEpoch = -1;
        internal RuntimeSaveGameService CheckpointService { get; }
        internal SaveGameProfileId Profile { get; private set; }
        internal SaveGameSlotId Slot { get; private set; }
        internal DateTime CreatedAtUtc { get; private set; }
        internal bool IsInitialized => Volatile.Read(ref _initialized) == 1;

        internal bool TryInitialize(in RuntimeSaveGameRestoreCompletion completion)
        {
            if (Volatile.Read(ref _initialized) != 0) return false;
            if (!_durableHeadAuthority.TryBindRestore(
                    completion.Profile,
                    completion.Slot,
                    completion.OriginalGeneration,
                    out long durableEpoch))
                return false;
            Profile = completion.Profile;
            Slot = completion.Slot;
            CreatedAtUtc = completion.CreatedAtUtc;
            Volatile.Write(ref _expectedHeadBits, unchecked((long)completion.OriginalGeneration.Value));
            Volatile.Write(ref _durableEpoch, durableEpoch);
            return Interlocked.CompareExchange(ref _initialized, 1, 0) == 0;
        }

        internal bool TryGetExpectedHead(out SaveGameGeneration expectedHead)
        {
            if (!IsInitialized)
            {
                expectedHead = default;
                return false;
            }
            expectedHead = new SaveGameGeneration(
                unchecked((ulong)Volatile.Read(ref _expectedHeadBits)));
            return true;
        }

        internal bool TryAdvanceExpectedHead(
            SaveGameGeneration expected,
            SaveGameGeneration next)
        {
            if (!IsInitialized ||
                unchecked((ulong)Volatile.Read(ref _expectedHeadBits)) != expected.Value ||
                !_durableHeadAuthority.TryAdvance(
                    Profile,
                    Slot,
                    expected,
                    Volatile.Read(ref _durableEpoch),
                    next,
                    out long nextEpoch))
                return false;
            Volatile.Write(ref _expectedHeadBits, unchecked((long)next.Value));
            Volatile.Write(ref _durableEpoch, nextEpoch);
            return true;
        }

        internal bool IsDurableProvenanceCurrent =>
            IsInitialized && _durableHeadAuthority.IsCurrent(
                Profile,
                Slot,
                new SaveGameGeneration(unchecked((ulong)Volatile.Read(ref _expectedHeadBits))),
                Volatile.Read(ref _durableEpoch));

        bool IApplicationLiveCheckpointCapability.CanReplace(
            IApplicationLiveCheckpointCapability current)
        {
            if (current == null)
                return IsDurableProvenanceCurrent;

            var currentPersistence = current as ApplicationPersistenceLiveCheckpointCapability;
            if (currentPersistence == null ||
                !IsDurableProvenanceCurrent ||
                !currentPersistence.IsDurableProvenanceCurrent)
                return false;

            if (Profile != currentPersistence.Profile || Slot != currentPersistence.Slot)
                return true;

            return ReferenceEquals(
                       _durableHeadAuthority,
                       currentPersistence._durableHeadAuthority) &&
                   Volatile.Read(ref _durableEpoch) ==
                       Volatile.Read(ref currentPersistence._durableEpoch);
        }
    }
}
