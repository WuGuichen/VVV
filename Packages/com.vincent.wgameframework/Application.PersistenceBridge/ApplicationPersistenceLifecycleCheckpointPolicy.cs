using System;
using MxFramework.ApplicationStartup.PlatformBridge;
using MxFramework.Persistence;
using MxFramework.Persistence.RuntimeBridge;
using MxFramework.Platform;

namespace MxFramework.ApplicationStartup.PersistenceBridge
{
    /// <summary>Application-lifetime exact durable profile/slot/head and save-epoch authority.</summary>
    /// <remarks>
    /// AllocByDesign construction. Bind, validation, and advancement are NoAlloc after initialization.
    /// One authority instance must be shared by every candidate builder for one active save slot.
    /// </remarks>
    public sealed class ApplicationPersistenceDurableHeadAuthority
    {
        private readonly object _gate = new object();
        private bool _initialized;
        private SaveGameProfileId _profile;
        private SaveGameSlotId _slot;
        private SaveGameGeneration _head;
        private long _epoch;

        internal bool TryBindRestore(
            SaveGameProfileId profile,
            SaveGameSlotId slot,
            SaveGameGeneration head,
            out long epoch)
        {
            lock (_gate)
            {
                if (!_initialized)
                {
                    _profile = profile;
                    _slot = slot;
                    _head = head;
                    _epoch = 0;
                    _initialized = true;
                    epoch = 0;
                    return true;
                }
                if (_profile != profile || _slot != slot || _head != head)
                {
                    epoch = -1;
                    return false;
                }
                epoch = _epoch;
                return true;
            }
        }

        internal bool IsCurrent(
            SaveGameProfileId profile,
            SaveGameSlotId slot,
            SaveGameGeneration head,
            long epoch)
        {
            lock (_gate)
                return _initialized && _profile == profile && _slot == slot &&
                       _head == head && _epoch == epoch;
        }

        internal bool TryAdvance(
            SaveGameProfileId profile,
            SaveGameSlotId slot,
            SaveGameGeneration expectedHead,
            long expectedEpoch,
            SaveGameGeneration nextHead,
            out long nextEpoch)
        {
            lock (_gate)
            {
                if (!_initialized || _profile != profile || _slot != slot ||
                    _head != expectedHead || _epoch != expectedEpoch || _epoch == long.MaxValue ||
                    !expectedHead.TryNext(out SaveGameGeneration required) || required != nextHead)
                {
                    nextEpoch = -1;
                    return false;
                }
                _head = nextHead;
                _epoch++;
                nextEpoch = _epoch;
                return true;
            }
        }
    }

    /// <summary>Creates one exact persistence request from lifecycle evidence and state-bound facts.</summary>
    public interface IApplicationPersistenceLifecycleCheckpointRequestPolicy
    {
        /// <remarks>AllocByDesign when a request is created. Failure must return a null request.</remarks>
        bool TryCreate(
            in ApplicationLifecycleCheckpointEvidence evidence,
            SaveGameProfileId profile,
            SaveGameSlotId slot,
            SaveGameGeneration expectedHead,
            DateTime createdAtUtc,
            DateTime observedUtc,
            out RuntimeSaveGameCheckpointRequest request);
    }

    /// <summary>Default checkpoint request policy preserving creation time and using empty metadata.</summary>
    public sealed class ApplicationPersistenceEmptyMetadataCheckpointRequestPolicy :
        IApplicationPersistenceLifecycleCheckpointRequestPolicy
    {
        /// <remarks>AllocByDesign. Creates one immutable checkpoint request.</remarks>
        public bool TryCreate(
            in ApplicationLifecycleCheckpointEvidence evidence,
            SaveGameProfileId profile,
            SaveGameSlotId slot,
            SaveGameGeneration expectedHead,
            DateTime createdAtUtc,
            DateTime observedUtc,
            out RuntimeSaveGameCheckpointRequest request)
        {
            if (!evidence.IsCheckpointWorthy || !profile.IsValid || !slot.IsValid ||
                !expectedHead.IsPublished || createdAtUtc.Kind != DateTimeKind.Utc ||
                observedUtc.Kind != DateTimeKind.Utc)
            {
                request = null;
                return false;
            }

            DateTime updatedAtUtc = observedUtc < createdAtUtc ? createdAtUtc : observedUtc;
            request = new RuntimeSaveGameCheckpointRequest(
                profile,
                slot,
                expectedHead,
                createdAtUtc,
                updatedAtUtc,
                null,
                null);
            return true;
        }
    }

    /// <summary>Creates the sole safe lifecycle bridge entry to the private Persistence-backed policy.</summary>
    public static class ApplicationPersistencePlatformLifecycleBridgeFactory
    {
        /// <remarks>AllocByDesign. Creates one long-lived bridge and its private policy.</remarks>
        public static ApplicationPlatformLifecycleBridgeCreateStatus TryCreate(
            IApplicationLifecycleSource source,
            LiveApplicationStatePublisher publisher,
            ApplicationPublicationSaveFence fence,
            IWallClock wallClock,
            IApplicationPersistenceLifecycleCheckpointRequestPolicy requestPolicy,
            int drainBudget,
            out ApplicationPlatformLifecycleBridge bridge)
        {
            bridge = null;
            if (wallClock == null || requestPolicy == null)
                return ApplicationPlatformLifecycleBridgeCreateStatus.InvalidArgument;
            return ApplicationPlatformLifecycleBridge.TryCreate(
                source,
                publisher,
                fence,
                new ApplicationPersistenceLifecycleCheckpointPolicy(wallClock, requestPolicy),
                drainBudget,
                out bridge);
        }
    }

    /// <summary>Private Persistence-backed exact-session lifecycle checkpoint policy.</summary>
    internal sealed class ApplicationPersistenceLifecycleCheckpointPolicy :
        IApplicationLifecycleCheckpointPolicy
    {
        private readonly IWallClock _wallClock;
        private readonly IApplicationPersistenceLifecycleCheckpointRequestPolicy _requestPolicy;

        internal ApplicationPersistenceLifecycleCheckpointPolicy(
            IWallClock wallClock,
            IApplicationPersistenceLifecycleCheckpointRequestPolicy requestPolicy)
        {
            _wallClock = wallClock ?? throw new ArgumentNullException(nameof(wallClock));
            _requestPolicy = requestPolicy ?? throw new ArgumentNullException(nameof(requestPolicy));
        }

        /// <remarks>LowFreqAlloc. Checkpoint capture, serialization, and provider operation creation allocate by design.</remarks>
        public ApplicationLifecycleCheckpointBeginResult TryBegin(
            LiveApplicationState exactState,
            in ApplicationLifecycleCheckpointEvidence evidence)
        {
            if (exactState == null || !exactState.HasRuntimeSession || !evidence.IsCheckpointWorthy ||
                !(exactState.CheckpointCapability is ApplicationPersistenceLiveCheckpointCapability capability) ||
                !capability.IsInitialized ||
                !capability.TryGetExpectedHead(out SaveGameGeneration expectedHead))
                return ApplicationLifecycleCheckpointBeginResult.Deferred;

            if (!expectedHead.TryNext(out _) ||
                !PlatformUtcClock.TryReadUtc(_wallClock, out DateTime observedUtc, out _))
                return ApplicationLifecycleCheckpointBeginResult.Failed;

            RuntimeSaveGameCheckpointRequest request;
            try
            {
                if (!_requestPolicy.TryCreate(
                        in evidence,
                        capability.Profile,
                        capability.Slot,
                        expectedHead,
                        capability.CreatedAtUtc,
                        observedUtc,
                        out request))
                    return ApplicationLifecycleCheckpointBeginResult.Failed;
            }
            catch
            {
                return ApplicationLifecycleCheckpointBeginResult.Failed;
            }

            if (request == null || request.Profile != capability.Profile ||
                request.Slot != capability.Slot || request.ExpectedHead != expectedHead ||
                request.CreatedAtUtc != capability.CreatedAtUtc ||
                request.CreatedAtUtc.Kind != DateTimeKind.Utc ||
                request.UpdatedAtUtc.Kind != DateTimeKind.Utc ||
                request.UpdatedAtUtc < request.CreatedAtUtc)
                return ApplicationLifecycleCheckpointBeginResult.Failed;

            RuntimeSaveGameBridgeResult<RuntimeSaveGameWritePlan> planned;
            try
            {
                planned = capability.CheckpointService.PlanCheckpointSave(request);
            }
            catch
            {
                return ApplicationLifecycleCheckpointBeginResult.Failed;
            }

            if (!planned.Success || planned.Value == null)
                return IsRetryablePlanFailure(planned.ErrorDetail)
                    ? ApplicationLifecycleCheckpointBeginResult.Deferred
                    : ApplicationLifecycleCheckpointBeginResult.Failed;

            RuntimeSaveGameWritePlan plan = planned.Value;
            IRuntimeSaveGameOperation<SaveGameCommitResult> operation;
            try
            {
                operation = capability.CheckpointService.Commit(plan);
            }
            catch
            {
                plan.Dispose();
                return ApplicationLifecycleCheckpointBeginResult.Failed;
            }
            if (operation == null)
            {
                plan.Dispose();
                return ApplicationLifecycleCheckpointBeginResult.Failed;
            }

            return ApplicationLifecycleCheckpointBeginResult.Started(
                new PersistenceCheckpointOperation(operation, capability, expectedHead));
        }

        private static bool IsRetryablePlanFailure(RuntimeSaveGameBridgeErrorDetail error) =>
            error != null &&
            (error.PersistenceError.Code == SaveGameErrorCode.Busy ||
             error.Code == RuntimeSaveGameBridgeErrorCode.CheckpointRejected ||
             error.Code == RuntimeSaveGameBridgeErrorCode.ParticipantFailure);

        private sealed class PersistenceCheckpointOperation :
            IApplicationLifecycleCheckpointOperation
        {
            private readonly IRuntimeSaveGameOperation<SaveGameCommitResult> _operation;
            private readonly ApplicationPersistenceLiveCheckpointCapability _capability;
            private readonly SaveGameGeneration _expectedHead;
            private ApplicationLifecycleCheckpointOperationStatus _status;
            private bool _canCancel;

            internal PersistenceCheckpointOperation(
                IRuntimeSaveGameOperation<SaveGameCommitResult> operation,
                ApplicationPersistenceLiveCheckpointCapability capability,
                SaveGameGeneration expectedHead)
            {
                _operation = operation;
                _capability = capability;
                _expectedHead = expectedHead;
                _status = ApplicationLifecycleCheckpointOperationStatus.Pending;
                Observe();
            }

            public ApplicationLifecycleCheckpointOperationStatus Status => _status;
            public bool CanCancel => _status == ApplicationLifecycleCheckpointOperationStatus.Pending && _canCancel;

            /// <remarks>NoAlloc after initialization for a conforming Runtime save operation.</remarks>
            public void Poll()
            {
                if (_status != ApplicationLifecycleCheckpointOperationStatus.Pending) return;
                try
                {
                    _operation.Poll();
                }
                catch
                {
                    _status = ApplicationLifecycleCheckpointOperationStatus.Failed;
                    _canCancel = false;
                    return;
                }
                Observe();
            }

            /// <remarks>NoAlloc after initialization for a conforming Runtime save operation.</remarks>
            public bool RequestCancel()
            {
                if (!CanCancel) return false;
                try
                {
                    _operation.RequestCancel();
                }
                catch
                {
                    _status = ApplicationLifecycleCheckpointOperationStatus.Failed;
                    _canCancel = false;
                    return false;
                }
                Observe();
                return true;
            }

            private void Observe()
            {
                if (_status != ApplicationLifecycleCheckpointOperationStatus.Pending) return;
                try
                {
                    SaveGameOperationStatus status = _operation.Status;
                    bool done = _operation.IsDone;
                    bool hasResult = _operation.HasResult;
                    RuntimeSaveGameBridgeErrorDetail error = _operation.ErrorDetail;
                    _canCancel = _operation.CanCancel;

                    if (status == SaveGameOperationStatus.Pending || status == SaveGameOperationStatus.Running)
                    {
                        if (done || hasResult || error != null)
                            Fail();
                        return;
                    }

                    _canCancel = false;
                    if (!done)
                    {
                        Fail();
                        return;
                    }
                    if (status == SaveGameOperationStatus.Succeeded)
                    {
                        if (error != null || !hasResult ||
                            !_operation.TryGetResult(out SaveGameCommitResult result) ||
                            !_expectedHead.TryNext(out SaveGameGeneration expectedGeneration) ||
                            result.Generation != expectedGeneration ||
                            !_capability.TryAdvanceExpectedHead(_expectedHead, result.Generation))
                        {
                            Fail();
                            return;
                        }
                        _status = ApplicationLifecycleCheckpointOperationStatus.Succeeded;
                        return;
                    }
                    if (hasResult || status == SaveGameOperationStatus.Failed && error == null)
                    {
                        Fail();
                        return;
                    }
                    if (status == SaveGameOperationStatus.Failed)
                    {
                        _status = error.PersistenceError.Code == SaveGameErrorCode.GenerationConflict
                            ? ApplicationLifecycleCheckpointOperationStatus.GenerationConflict
                            : ApplicationLifecycleCheckpointOperationStatus.Failed;
                        return;
                    }
                    if (status == SaveGameOperationStatus.Cancelled && error == null)
                    {
                        _status = ApplicationLifecycleCheckpointOperationStatus.Cancelled;
                        return;
                    }
                    if (status == SaveGameOperationStatus.TimedOut && error != null &&
                        error.PersistenceError.Code == SaveGameErrorCode.Timeout)
                    {
                        _status = ApplicationLifecycleCheckpointOperationStatus.TimedOut;
                        return;
                    }
                    Fail();
                }
                catch
                {
                    Fail();
                }
            }

            private void Fail()
            {
                _status = ApplicationLifecycleCheckpointOperationStatus.Failed;
                _canCancel = false;
            }
        }
    }
}
