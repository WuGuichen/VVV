using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading;

namespace MxFramework.Resources
{
    public interface IResourcePlanOrchestrator
    {
        IResourceOperation<ResourcePreloadResult> Preload(
            ResourcePlan plan,
            CancellationToken cancellationToken = default);

        ResourcePlanSession Acquire(ResourcePlan plan, ResourcePreloadResult preloadResult);

        ResourcePlanDiff PrepareChange(
            ResourcePlanSession session,
            ResourcePlan nextPlan,
            CancellationToken cancellationToken = default);

        void CommitChange(ResourcePlanSession session, ResourcePlanDiff diff);
        void Release(ResourcePlanSession session);
    }

    public sealed class ResourcePlanOrchestrator : IResourcePlanOrchestrator
    {
        private readonly IResourcePreloadService _preloadService;
        private readonly IResourceManager _resourceManager;
        private readonly int _maxConcurrentLoads;

        public ResourcePlanOrchestrator(
            IResourcePreloadService preloadService,
            IResourceManager resourceManager,
            int maxConcurrentLoads = 4)
        {
            _preloadService = preloadService ?? throw new ArgumentNullException(nameof(preloadService));
            _resourceManager = resourceManager ?? throw new ArgumentNullException(nameof(resourceManager));
            _maxConcurrentLoads = maxConcurrentLoads < 1 ? 1 : maxConcurrentLoads;
        }

        public IResourceOperation<ResourcePreloadResult> Preload(
            ResourcePlan plan,
            CancellationToken cancellationToken = default)
        {
            if (plan == null)
                return FailedPreload(ResourcePlanDiagnosticCodes.MissingPlan, "Resource plan is required.");

            return _preloadService.PreloadAsync(CreatePreloadPlan(plan, plan.ResourceKeys), cancellationToken);
        }

        /// <summary>
        /// Starts an owner-bound preparation against one sealed runtime configuration.
        /// </summary>
        /// <remarks>LowFreqAlloc. Polling the returned preparation is NoAlloc after initialization.</remarks>
        public ResourcePlanPreparation Prepare(
            ResourceRuntimeConfigurationIdentity configurationIdentity,
            ResourcePlan plan,
            CancellationToken cancellationToken = default)
        {
            if (configurationIdentity == null)
            {
                return ResourcePlanPreparation.Invalid(
                    this,
                    _resourceManager,
                    configurationIdentity,
                    plan,
                    ResourcePlanPreparationErrorCode.ConfigurationIdentityMissing);
            }

            if (!configurationIdentity.IsOwnedBy(_resourceManager))
            {
                return ResourcePlanPreparation.Invalid(
                    this,
                    _resourceManager,
                    configurationIdentity,
                    plan,
                    ResourcePlanPreparationErrorCode.ConfigurationIdentityMismatch);
            }

            if (plan == null)
            {
                return ResourcePlanPreparation.Invalid(
                    this,
                    _resourceManager,
                    configurationIdentity,
                    null,
                    ResourcePlanPreparationErrorCode.PlanMissing);
            }

            return new ResourcePlanPreparation(
                this,
                _resourceManager,
                _preloadService,
                configurationIdentity,
                plan,
                Preload(plan, cancellationToken));
        }

        /// <summary>
        /// Consumes a completed strong preparation exactly once and transfers its handles into a plan session.
        /// </summary>
        /// <remarks>LowFreqAlloc on success or diagnostic failure.</remarks>
        public ResourcePlanAcquireResult AcquirePrepared(ResourcePlan plan, ResourcePlanPreparation preparation)
        {
            if (preparation == null)
                return ResourcePlanAcquireResult.Failed(ResourcePlanPreparationErrorCode.PreparationMissing);

            if (preparation.HasInitialError)
                return ResourcePlanAcquireResult.Failed(preparation.Error);

            if (!preparation.IsForManager(_resourceManager))
                return ResourcePlanAcquireResult.Failed(ResourcePlanPreparationErrorCode.ConfigurationIdentityMismatch);

            if (!preparation.IsOwnedBy(this))
                return ResourcePlanAcquireResult.Failed(ResourcePlanPreparationErrorCode.ForeignOwner);

            if (!preparation.IsForPlan(plan))
                return ResourcePlanAcquireResult.Failed(ResourcePlanPreparationErrorCode.PlanMismatch);

            if (preparation.IsClaimed)
                return ResourcePlanAcquireResult.Failed(ResourcePlanPreparationErrorCode.AlreadyAcquired);
            if (preparation.IsReleasing || preparation.IsReleased)
                return ResourcePlanAcquireResult.Failed(ResourcePlanPreparationErrorCode.PreparationReleased);
            if (preparation.IsOwnershipUnavailable)
                return ResourcePlanAcquireResult.Failed(ResourcePlanPreparationErrorCode.HandleOwnershipUnavailable);

            ResourcePlanPreparationStatus status = preparation.Status;
            if (status == ResourcePlanPreparationStatus.Preparing)
                return ResourcePlanAcquireResult.Failed(ResourcePlanPreparationErrorCode.PreparationIncomplete);
            if (status != ResourcePlanPreparationStatus.Prepared)
                return ResourcePlanAcquireResult.Failed(preparation.Error);
            ResourcePreloadResult preloadResult = preparation.PreloadResult;
            ResourceGroupHandle group = preloadResult != null ? preloadResult.Handle : null;
            if (!preparation.TryClaim(group, out ResourcePlanPreparationErrorCode claimError))
                return ResourcePlanAcquireResult.Failed(claimError);

            return ResourcePlanAcquireResult.Acquired(new ResourcePlanSession(
                plan,
                group.Handles,
                Array.Empty<ResourcePlanDiagnostic>()));
        }

        public ResourcePlanSession Acquire(ResourcePlan plan, ResourcePreloadResult preloadResult)
        {
            var diagnostics = new List<ResourcePlanDiagnostic>();
            if (plan == null)
            {
                diagnostics.Add(ResourcePlanDiagnostic.Error(
                    ResourcePlanDiagnosticCodes.MissingPlan,
                    string.Empty,
                    default,
                    "Resource plan is required."));
                return new ResourcePlanSession(null, Array.Empty<ResourceHandle<object>>(), diagnostics);
            }

            if (preloadResult == null)
            {
                diagnostics.Add(ResourcePlanDiagnostic.Error(
                    ResourcePlanDiagnosticCodes.AcquireWithoutPreload,
                    plan.PlanHash,
                    default,
                    "Acquire requires a completed preload result."));
                return new ResourcePlanSession(plan, Array.Empty<ResourceHandle<object>>(), diagnostics);
            }

            AddPreloadDiagnostics(preloadResult, ResourcePlanDiagnosticCodes.PreloadFailed, diagnostics);
            IReadOnlyList<ResourceHandle<object>> handles = preloadResult.Handle != null
                ? preloadResult.Handle.Handles
                : Array.Empty<ResourceHandle<object>>();
            return new ResourcePlanSession(plan, handles, diagnostics);
        }

        public ResourcePlanDiff PrepareChange(
            ResourcePlanSession session,
            ResourcePlan nextPlan,
            CancellationToken cancellationToken = default)
        {
            if (session == null || nextPlan == null)
            {
                return ResourcePlanDiff.Invalid(
                    session,
                    nextPlan,
                    ResourcePlanDiagnostic.Error(
                        ResourcePlanDiagnosticCodes.MissingPlan,
                        string.Empty,
                        default,
                        "Resource plan change requires an active session and next plan."));
            }

            if (session.IsReleased)
            {
                return ResourcePlanDiff.Invalid(
                    session,
                    nextPlan,
                    ResourcePlanDiagnostic.Warning(
                        ResourcePlanDiagnosticCodes.SessionAlreadyReleased,
                        session.PlanHash,
                        default,
                        "Resource plan change was ignored because the session is already released."));
            }

            ResourceKey[] keep = Intersect(session.Plan.ResourceKeys, nextPlan.ResourceKeys);
            ResourceKey[] preload = Except(nextPlan.ResourceKeys, session.Plan.ResourceKeys);
            ResourceKey[] release = Except(session.Plan.ResourceKeys, nextPlan.ResourceKeys);
            IResourceOperation<ResourcePreloadResult> operation = _preloadService.PreloadAsync(CreatePreloadPlan(nextPlan, preload), cancellationToken);
            return new ResourcePlanDiff(session.Plan, nextPlan, keep, preload, release, operation);
        }

        public void CommitChange(ResourcePlanSession session, ResourcePlanDiff diff)
        {
            if (session == null || diff == null)
                return;
            if (session.IsReleased)
            {
                session.AddDiagnostic(ResourcePlanDiagnostic.Warning(
                    ResourcePlanDiagnosticCodes.SessionAlreadyReleased,
                    session.PlanHash,
                    default,
                    "Resource plan change was ignored because the session is already released."));
                return;
            }

            IResourceOperation<ResourcePreloadResult> operation = diff.PreloadOperation;
            if (operation != null && !operation.IsDone)
                return;

            ResourceLoadResult<ResourcePreloadResult> operationResult = operation != null
                ? operation.Result
                : ResourceLoadResult<ResourcePreloadResult>.Loaded(null);
            ResourcePreloadResult preloadResult = operationResult.Value;
            if (operation != null && (!operationResult.Success || preloadResult == null || !preloadResult.Success))
            {
                ResourceError error = !operationResult.Success ? operationResult.Error : FirstError(preloadResult);
                session.AddDiagnostic(ResourcePlanDiagnostic.Error(
                    ResourcePlanDiagnosticCodes.ChangePreloadFailed,
                    diff.NextPlanHash,
                    error.Key,
                    string.IsNullOrEmpty(error.Message) ? "Resource plan change preload failed." : error.Message));
                return;
            }

            if (preloadResult != null && preloadResult.Handle != null)
                session.AddLoadedHandles(preloadResult.Handle.Handles);

            ReleaseKeys(session, diff.ReleaseResources);
            session.UpdatePlan(diff.NextPlan);
        }

        public void Release(ResourcePlanSession session)
        {
            if (session == null)
                return;

            if (!session.TryMarkReleased())
            {
                session.AddDiagnostic(ResourcePlanDiagnostic.Warning(
                    ResourcePlanDiagnosticCodes.SessionAlreadyReleased,
                    session.PlanHash,
                    default,
                    "Resource plan session was already released."));
                return;
            }

            IReadOnlyList<ResourceHandle<object>> handles = session.LoadedResourceHandles;
            for (int i = handles.Count - 1; i >= 0; i--)
            {
                ResourceHandle<object> handle = handles[i];
                if (handle != null && !handle.IsReleased)
                    _resourceManager.Release(handle);
            }
        }

        private ResourcePreloadPlan CreatePreloadPlan(ResourcePlan plan, IEnumerable<ResourceKey> keys)
        {
            return new ResourcePreloadPlan(
                CreateGroupId(plan),
                keys,
                plan.Labels,
                plan.FailFast,
                _maxConcurrentLoads);
        }

        private void ReleaseKeys(ResourcePlanSession session, IReadOnlyList<ResourceKey> keys)
        {
            if (keys == null || keys.Count == 0)
                return;

            var releaseSet = new HashSet<ResourceKey>(keys);
            IReadOnlyList<ResourceHandle<object>> handles = session.LoadedResourceHandles;
            for (int i = handles.Count - 1; i >= 0; i--)
            {
                ResourceHandle<object> handle = handles[i];
                if (handle == null || handle.IsReleased || !releaseSet.Contains(handle.Key))
                    continue;

                _resourceManager.Release(handle);
            }

            session.PruneReleasedHandles();
        }

        private static void AddPreloadDiagnostics(
            ResourcePreloadResult result,
            string code,
            List<ResourcePlanDiagnostic> diagnostics)
        {
            if (result == null || result.Errors == null)
                return;

            for (int i = 0; i < result.Errors.Count; i++)
            {
                ResourceError error = result.Errors[i];
                diagnostics.Add(ResourcePlanDiagnostic.Error(code, result.GroupId, error.Key, error.Message));
            }
        }

        private static ResourceError FirstError(ResourcePreloadResult result)
        {
            return result != null && result.Errors.Count > 0 ? result.Errors[0] : default;
        }

        private static IResourceOperation<ResourcePreloadResult> FailedPreload(string code, string message)
        {
            return new ImmediateResourceOperation<ResourcePreloadResult>(
                ResourceLoadResult<ResourcePreloadResult>.Failed(new ResourceError(
                    ResourceErrorCode.InvalidCatalog,
                    default,
                    code,
                    message)));
        }

        internal static string CreateGroupId(ResourcePlan plan)
        {
            string suffix = !string.IsNullOrEmpty(plan.PlanHash) ? plan.PlanHash : plan.PlanId;
            return string.IsNullOrEmpty(suffix) ? "resource.plan" : "resource.plan." + suffix;
        }

        private static ResourceKey[] Intersect(IReadOnlyList<ResourceKey> left, IReadOnlyList<ResourceKey> right)
        {
            var rightSet = new HashSet<ResourceKey>(right);
            var result = new List<ResourceKey>();
            for (int i = 0; i < left.Count; i++)
            {
                if (rightSet.Contains(left[i]))
                    result.Add(left[i]);
            }

            return result.ToArray();
        }

        private static ResourceKey[] Except(IReadOnlyList<ResourceKey> left, IReadOnlyList<ResourceKey> right)
        {
            var rightSet = new HashSet<ResourceKey>(right);
            var result = new List<ResourceKey>();
            for (int i = 0; i < left.Count; i++)
            {
                if (!rightSet.Contains(left[i]))
                    result.Add(left[i]);
            }

            return result.ToArray();
        }
    }

    public sealed class ResourcePlan
    {
        private readonly ReadOnlyCollection<ResourceKey> _resourceKeys;
        private readonly ReadOnlyCollection<string> _labels;

        public ResourcePlan(
            string planId,
            string planHash,
            IEnumerable<ResourceKey> resourceKeys,
            IEnumerable<string> labels = null,
            bool failFast = false)
        {
            PlanId = planId ?? string.Empty;
            PlanHash = planHash ?? string.Empty;
            _resourceKeys = (resourceKeys != null ? new List<ResourceKey>(resourceKeys) : new List<ResourceKey>()).AsReadOnly();
            _labels = (labels != null ? new List<string>(labels) : new List<string>()).AsReadOnly();
            FailFast = failFast;
            var fingerprint = new ResourceDiagnosticFingerprint();
            fingerprint.Add("resource-plan-v1");
            fingerprint.Add(PlanId);
            fingerprint.Add(PlanHash);
            fingerprint.Add(FailFast);
            fingerprint.Add(_resourceKeys.Count);
            for (int i = 0; i < _resourceKeys.Count; i++)
                fingerprint.Add(_resourceKeys[i]);
            fingerprint.Add(_labels.Count);
            for (int i = 0; i < _labels.Count; i++)
                fingerprint.Add(_labels[i]);
            PlanFingerprint = fingerprint.Complete();
        }

        public string PlanId { get; }
        public string PlanHash { get; }
        /// <remarks>NoAlloc.</remarks>
        public string PlanFingerprint { get; }
        public IReadOnlyList<ResourceKey> ResourceKeys => _resourceKeys;
        public IReadOnlyList<string> Labels => _labels;
        public bool FailFast { get; }
    }

    public sealed class ResourcePlanSession
    {
        private readonly List<ResourceHandle<object>> _loadedResourceHandles;
        private readonly List<ResourcePlanDiagnostic> _diagnostics;
        private readonly ReadOnlyCollection<ResourceHandle<object>> _readOnlyLoadedResourceHandles;
        private readonly ReadOnlyCollection<ResourcePlanDiagnostic> _readOnlyDiagnostics;

        internal ResourcePlanSession(
            ResourcePlan plan,
            IReadOnlyList<ResourceHandle<object>> loadedResourceHandles,
            IEnumerable<ResourcePlanDiagnostic> diagnostics)
        {
            Plan = plan;
            _loadedResourceHandles = loadedResourceHandles != null
                ? new List<ResourceHandle<object>>(loadedResourceHandles)
                : new List<ResourceHandle<object>>();
            _diagnostics = diagnostics != null
                ? new List<ResourcePlanDiagnostic>(diagnostics)
                : new List<ResourcePlanDiagnostic>();
            _readOnlyLoadedResourceHandles = _loadedResourceHandles.AsReadOnly();
            _readOnlyDiagnostics = _diagnostics.AsReadOnly();
        }

        public ResourcePlan Plan { get; private set; }
        public string PlanId => Plan != null ? Plan.PlanId : string.Empty;
        public string PlanHash => Plan != null ? Plan.PlanHash : string.Empty;
        public IReadOnlyList<ResourceHandle<object>> LoadedResourceHandles => _readOnlyLoadedResourceHandles;
        public IReadOnlyList<ResourcePlanDiagnostic> Diagnostics => _readOnlyDiagnostics;
        public bool IsReleased { get; private set; }

        internal void AddLoadedHandles(IReadOnlyList<ResourceHandle<object>> handles)
        {
            if (handles == null)
                return;

            for (int i = 0; i < handles.Count; i++)
            {
                if (handles[i] != null)
                    _loadedResourceHandles.Add(handles[i]);
            }
        }

        internal void AddDiagnostic(ResourcePlanDiagnostic diagnostic)
        {
            _diagnostics.Add(diagnostic);
        }

        internal void UpdatePlan(ResourcePlan plan)
        {
            Plan = plan;
        }

        internal bool TryMarkReleased()
        {
            if (IsReleased)
                return false;

            IsReleased = true;
            return true;
        }

        internal void PruneReleasedHandles()
        {
            for (int i = _loadedResourceHandles.Count - 1; i >= 0; i--)
            {
                ResourceHandle<object> handle = _loadedResourceHandles[i];
                if (handle == null || handle.IsReleased)
                    _loadedResourceHandles.RemoveAt(i);
            }
        }
    }

    public sealed class ResourcePlanPreparation : IDisposable
    {
        private readonly ResourcePlanOrchestrator _owner;
        private readonly IResourceManager _resourceManager;
        private readonly IResourcePreloadService _preloadService;
        private readonly ResourceRuntimeConfigurationIdentity _configurationIdentity;
        private readonly ResourcePlan _plan;
        private readonly string _expectedGroupId;
        private readonly IResourceOperation<ResourcePreloadResult> _preloadOperation;
        private readonly ResourcePlanPreparationError _initialError;
        private int _ownershipState;
        private int _failedPreloadReleaseState;

        internal ResourcePlanPreparation(
            ResourcePlanOrchestrator owner,
            IResourceManager resourceManager,
            IResourcePreloadService preloadService,
            ResourceRuntimeConfigurationIdentity configurationIdentity,
            ResourcePlan plan,
            IResourceOperation<ResourcePreloadResult> preloadOperation)
            : this(
                owner,
                resourceManager,
                preloadService,
                configurationIdentity,
                plan,
                preloadOperation,
                ResourcePlanPreparationError.None)
        {
        }

        private ResourcePlanPreparation(
            ResourcePlanOrchestrator owner,
            IResourceManager resourceManager,
            IResourcePreloadService preloadService,
            ResourceRuntimeConfigurationIdentity configurationIdentity,
            ResourcePlan plan,
            IResourceOperation<ResourcePreloadResult> preloadOperation,
            ResourcePlanPreparationError initialError)
        {
            _owner = owner;
            _resourceManager = resourceManager;
            _preloadService = preloadService;
            _configurationIdentity = configurationIdentity;
            _plan = plan;
            _expectedGroupId = plan != null ? ResourcePlanOrchestrator.CreateGroupId(plan) : string.Empty;
            _preloadOperation = preloadOperation;
            _initialError = initialError;
            PlanFingerprint = plan != null ? plan.PlanFingerprint : string.Empty;
        }

        /// <remarks>NoAlloc after initialization.</remarks>
        public ResourcePlanPreparationStatus Status
        {
            get
            {
                if (IsClaimed)
                    return ResourcePlanPreparationStatus.Acquired;
                if (IsReleasing)
                {
                    EnsureReleasedPreload();
                    return IsReleased
                        ? ResourcePlanPreparationStatus.Released
                        : ResourcePlanPreparationStatus.Releasing;
                }
                if (IsReleased)
                    return ResourcePlanPreparationStatus.Released;
                if (IsOwnershipUnavailable)
                    return ResourcePlanPreparationStatus.Failed;
                if (!_initialError.IsNone)
                    return ResourcePlanPreparationStatus.Invalid;
                if (_preloadOperation == null)
                    return ResourcePlanPreparationStatus.Failed;
                if (!_preloadOperation.IsDone)
                    return ResourcePlanPreparationStatus.Preparing;

                ResourceLoadResult<ResourcePreloadResult> result = _preloadOperation.Result;
                bool prepared = !_preloadOperation.IsCancelled &&
                                result.Success &&
                                result.Value != null &&
                                result.Value.Success &&
                                IsCompletePreloadResult(result.Value);
                if (!prepared)
                    EnsureFailedPreloadReleased(result);

                return prepared
                    ? ResourcePlanPreparationStatus.Prepared
                    : ResourcePlanPreparationStatus.Failed;
            }
        }

        /// <remarks>NoAlloc after initialization.</remarks>
        public float Progress => Volatile.Read(ref _ownershipState) == 0 && _preloadOperation != null
            ? _preloadOperation.Progress
            : 1f;

        /// <remarks>NoAlloc.</remarks>
        public string PlanFingerprint { get; }

        /// <remarks>NoAlloc after initialization.</remarks>
        public ResourcePlanPreparationError Error
        {
            get
            {
                if (IsClaimed)
                    return ResourcePlanPreparationError.None;
                if (IsOwnershipUnavailable)
                    return new ResourcePlanPreparationError(ResourcePlanPreparationErrorCode.HandleOwnershipUnavailable, default);
                if (IsReleasing || IsReleased)
                    return new ResourcePlanPreparationError(ResourcePlanPreparationErrorCode.PreparationReleased, default);
                if (!_initialError.IsNone)
                    return _initialError;
                if (_preloadOperation == null)
                    return new ResourcePlanPreparationError(ResourcePlanPreparationErrorCode.PreparationFailed, default);
                if (!_preloadOperation.IsDone)
                    return ResourcePlanPreparationError.None;

                ResourceLoadResult<ResourcePreloadResult> result = _preloadOperation.Result;
                EnsureFailedPreloadReleased(result);
                if (_preloadOperation.IsCancelled)
                    return new ResourcePlanPreparationError(ResourcePlanPreparationErrorCode.PreparationCancelled, result.Error);
                if (!result.Success)
                    return new ResourcePlanPreparationError(ResourcePlanPreparationErrorCode.PreparationFailed, result.Error);
                if (result.Value == null)
                    return new ResourcePlanPreparationError(ResourcePlanPreparationErrorCode.PreparationFailed, default);
                if (!result.Value.Success)
                {
                    ResourceError resourceError = result.Value.Errors.Count > 0
                        ? result.Value.Errors[0]
                        : default;
                    return new ResourcePlanPreparationError(ResourcePlanPreparationErrorCode.PreparationFailed, resourceError);
                }
                if (!IsCompletePreloadResult(result.Value))
                    return new ResourcePlanPreparationError(ResourcePlanPreparationErrorCode.PreparationResultInvalid, default);

                return ResourcePlanPreparationError.None;
            }
        }

        /// <remarks>NoAlloc.</remarks>
        public ResourceRuntimeConfigurationIdentity ConfigurationIdentity => _configurationIdentity;

        /// <summary>Requests cooperative cancellation without releasing operation ownership early.</summary>
        /// <remarks>NoAlloc except for provider-owned cancellation diagnostics on a fault path.</remarks>
        public void Cancel()
        {
            if (Volatile.Read(ref _ownershipState) != 0 || _preloadOperation == null || _preloadOperation.IsDone)
                return;

            _preloadOperation.Cancel();
            _ = Status;
        }

        /// <summary>Begins abandoning this candidate and releases only its owned preload handles.</summary>
        /// <remarks>
        /// NoAlloc after initialization. Idempotent and non-blocking; acquired session ownership is never released.
        /// The caller must retain and poll <see cref="Status"/> until <see cref="ResourcePlanPreparationStatus.Released"/>
        /// before considering rollback complete.
        /// </remarks>
        public void Release()
        {
            int priorState = Interlocked.CompareExchange(ref _ownershipState, 2, 0);
            if (priorState != 0)
            {
                if (priorState == 2)
                    EnsureReleasedPreload();
                return;
            }

            if (_preloadOperation != null && !_preloadOperation.IsDone)
                _preloadOperation.Cancel();
            EnsureReleasedPreload();
        }

        public void Dispose()
        {
            Release();
        }

        internal bool IsClaimed => Volatile.Read(ref _ownershipState) == 1;
        internal bool IsReleasing => Volatile.Read(ref _ownershipState) == 2;
        internal bool IsOwnershipUnavailable => Volatile.Read(ref _ownershipState) == 3;
        internal bool IsReleased => Volatile.Read(ref _ownershipState) == 4;
        internal bool HasInitialError => !_initialError.IsNone;

        internal ResourcePreloadResult PreloadResult
        {
            get
            {
                if (_preloadOperation == null || !_preloadOperation.IsDone)
                    return null;

                ResourceLoadResult<ResourcePreloadResult> result = _preloadOperation.Result;
                return result.Success ? result.Value : null;
            }
        }

        internal bool IsOwnedBy(ResourcePlanOrchestrator owner)
        {
            return ReferenceEquals(_owner, owner);
        }

        internal bool IsForPlan(ResourcePlan plan)
        {
            return ReferenceEquals(_plan, plan) &&
                   plan != null &&
                   string.Equals(PlanFingerprint, plan.PlanFingerprint, StringComparison.Ordinal);
        }

        internal bool IsForManager(IResourceManager resourceManager)
        {
            return ReferenceEquals(_resourceManager, resourceManager) &&
                   _configurationIdentity != null &&
                   _configurationIdentity.IsOwnedBy(resourceManager);
        }

        internal bool TryClaim(
            ResourceGroupHandle group,
            out ResourcePlanPreparationErrorCode errorCode)
        {
            int priorState = Interlocked.CompareExchange(ref _ownershipState, 1, 0);
            if (priorState != 0)
            {
                errorCode = priorState == 1
                    ? ResourcePlanPreparationErrorCode.AlreadyAcquired
                    : priorState == 2 || priorState == 4
                        ? ResourcePlanPreparationErrorCode.PreparationReleased
                        : ResourcePlanPreparationErrorCode.HandleOwnershipUnavailable;
                return false;
            }

            if (group != null && group.TryTransferOwnership())
            {
                errorCode = ResourcePlanPreparationErrorCode.None;
                return true;
            }

            Volatile.Write(ref _ownershipState, 3);
            errorCode = ResourcePlanPreparationErrorCode.HandleOwnershipUnavailable;
            return false;
        }

        private void EnsureReleasedPreload()
        {
            if (!IsReleasing)
                return;

            if (_preloadOperation == null)
            {
                Volatile.Write(ref _ownershipState, 4);
                return;
            }

            if (!_preloadOperation.IsDone)
                return;

            ResourceLoadResult<ResourcePreloadResult> result = _preloadOperation.Result;
            if (_preloadService != null &&
                result.Success &&
                result.Value != null &&
                result.Value.Handle != null &&
                Interlocked.CompareExchange(ref _failedPreloadReleaseState, 1, 0) == 0)
            {
                _preloadService.ReleaseGroup(result.Value.Handle);
            }

            Volatile.Write(ref _ownershipState, 4);
        }

        private void EnsureFailedPreloadReleased(ResourceLoadResult<ResourcePreloadResult> result)
        {
            if (_preloadService == null ||
                !result.Success ||
                result.Value == null ||
                result.Value.Handle == null ||
                (result.Value.Success && IsCompletePreloadResult(result.Value)) ||
                Interlocked.CompareExchange(ref _failedPreloadReleaseState, 1, 0) != 0)
            {
                return;
            }

            _preloadService.ReleaseGroup(result.Value.Handle);
        }

        private bool IsCompletePreloadResult(ResourcePreloadResult result)
        {
            if (result == null ||
                result.Handle == null ||
                !string.Equals(result.GroupId, _expectedGroupId, StringComparison.Ordinal) ||
                !string.Equals(result.Handle.GroupId, result.GroupId, StringComparison.Ordinal) ||
                result.RequestedCount != result.LoadedCount)
            {
                return false;
            }

            for (int i = 0; i < _plan.ResourceKeys.Count; i++)
            {
                bool found = false;
                for (int j = 0; j < result.RequestedKeys.Count; j++)
                {
                    if (result.RequestedKeys[j] == _plan.ResourceKeys[i])
                    {
                        found = true;
                        break;
                    }
                }

                if (!found)
                    return false;
            }

            return result.Handle.HasCompleteAcquisitions(_resourceManager, result.RequestedKeys);
        }

        internal static ResourcePlanPreparation Invalid(
            ResourcePlanOrchestrator owner,
            IResourceManager resourceManager,
            ResourceRuntimeConfigurationIdentity configurationIdentity,
            ResourcePlan plan,
            ResourcePlanPreparationErrorCode errorCode)
        {
            return new ResourcePlanPreparation(
                owner,
                resourceManager,
                null,
                configurationIdentity,
                plan,
                null,
                new ResourcePlanPreparationError(errorCode, default));
        }
    }

    public readonly struct ResourcePlanAcquireResult
    {
        private ResourcePlanAcquireResult(
            ResourcePlanSession session,
            ResourcePlanPreparationError error)
        {
            Session = session;
            Error = error;
        }

        public bool Success => Session != null && Error.IsNone;
        public ResourcePlanSession Session { get; }
        public ResourcePlanPreparationError Error { get; }

        internal static ResourcePlanAcquireResult Acquired(ResourcePlanSession session)
        {
            return new ResourcePlanAcquireResult(session, ResourcePlanPreparationError.None);
        }

        internal static ResourcePlanAcquireResult Failed(ResourcePlanPreparationErrorCode errorCode)
        {
            return Failed(new ResourcePlanPreparationError(errorCode, default));
        }

        internal static ResourcePlanAcquireResult Failed(ResourcePlanPreparationError error)
        {
            return new ResourcePlanAcquireResult(null, error);
        }
    }

    public readonly struct ResourcePlanPreparationError
    {
        public ResourcePlanPreparationError(
            ResourcePlanPreparationErrorCode code,
            ResourceError resourceError)
        {
            Code = code;
            ResourceError = resourceError;
        }

        public ResourcePlanPreparationErrorCode Code { get; }
        public ResourceError ResourceError { get; }
        public bool IsNone => Code == ResourcePlanPreparationErrorCode.None;
        public static ResourcePlanPreparationError None => default;
    }

    public enum ResourcePlanPreparationStatus
    {
        Invalid = 0,
        Preparing = 1,
        Prepared = 2,
        Failed = 3,
        Acquired = 4,
        Releasing = 5,
        Released = 6
    }

    public enum ResourcePlanPreparationErrorCode
    {
        None = 0,
        ConfigurationIdentityMissing = 1,
        ConfigurationIdentityMismatch = 2,
        PlanMissing = 3,
        PreparationMissing = 4,
        ForeignOwner = 5,
        PlanMismatch = 6,
        PreparationIncomplete = 7,
        PreparationFailed = 8,
        PreparationCancelled = 9,
        AlreadyAcquired = 10,
        HandleOwnershipUnavailable = 11,
        PreparationResultInvalid = 12,
        PreparationReleased = 13
    }

    public sealed class ResourcePlanDiff
    {
        private readonly List<ResourceKey> _keepResources;
        private readonly List<ResourceKey> _preloadResources;
        private readonly List<ResourceKey> _releaseResources;
        private readonly List<ResourcePlanDiagnostic> _diagnostics;

        internal ResourcePlanDiff(
            ResourcePlan currentPlan,
            ResourcePlan nextPlan,
            IEnumerable<ResourceKey> keepResources,
            IEnumerable<ResourceKey> preloadResources,
            IEnumerable<ResourceKey> releaseResources,
            IResourceOperation<ResourcePreloadResult> preloadOperation)
        {
            CurrentPlan = currentPlan;
            NextPlan = nextPlan;
            _keepResources = keepResources != null ? new List<ResourceKey>(keepResources) : new List<ResourceKey>();
            _preloadResources = preloadResources != null ? new List<ResourceKey>(preloadResources) : new List<ResourceKey>();
            _releaseResources = releaseResources != null ? new List<ResourceKey>(releaseResources) : new List<ResourceKey>();
            PreloadOperation = preloadOperation;
            _diagnostics = new List<ResourcePlanDiagnostic>();
        }

        public ResourcePlan CurrentPlan { get; }
        public ResourcePlan NextPlan { get; }
        public string CurrentPlanHash => CurrentPlan != null ? CurrentPlan.PlanHash : string.Empty;
        public string NextPlanHash => NextPlan != null ? NextPlan.PlanHash : string.Empty;
        public IReadOnlyList<ResourceKey> KeepResources => _keepResources;
        public IReadOnlyList<ResourceKey> PreloadResources => _preloadResources;
        public IReadOnlyList<ResourceKey> ReleaseResources => _releaseResources;
        public IResourceOperation<ResourcePreloadResult> PreloadOperation { get; }
        public IReadOnlyList<ResourcePlanDiagnostic> Diagnostics => _diagnostics;

        internal static ResourcePlanDiff Invalid(
            ResourcePlanSession session,
            ResourcePlan nextPlan,
            ResourcePlanDiagnostic diagnostic)
        {
            var diff = new ResourcePlanDiff(
                session != null ? session.Plan : null,
                nextPlan,
                Array.Empty<ResourceKey>(),
                Array.Empty<ResourceKey>(),
                Array.Empty<ResourceKey>(),
                null);
            diff._diagnostics.Add(diagnostic);
            return diff;
        }
    }

    public static class ResourcePlanDiagnosticCodes
    {
        public const string MissingPlan = "RESOURCE_PLAN_MISSING";
        public const string PreloadFailed = "RESOURCE_PLAN_PRELOAD_FAILED";
        public const string AcquireWithoutPreload = "RESOURCE_PLAN_ACQUIRE_WITHOUT_PRELOAD";
        public const string ChangePreloadFailed = "RESOURCE_PLAN_CHANGE_PRELOAD_FAILED";
        public const string SessionAlreadyReleased = "RESOURCE_PLAN_SESSION_ALREADY_RELEASED";
    }

    public readonly struct ResourcePlanDiagnostic
    {
        public ResourcePlanDiagnostic(
            ResourcePlanDiagnosticSeverity severity,
            string code,
            string groupId,
            ResourceKey resourceKey,
            string message)
        {
            Severity = severity;
            Code = code ?? string.Empty;
            GroupId = groupId ?? string.Empty;
            ResourceKey = resourceKey;
            Message = message ?? string.Empty;
        }

        public ResourcePlanDiagnosticSeverity Severity { get; }
        public string Code { get; }
        public string GroupId { get; }
        public ResourceKey ResourceKey { get; }
        public string Message { get; }
        public bool IsError => Severity == ResourcePlanDiagnosticSeverity.Error;

        public static ResourcePlanDiagnostic Info(string code, string groupId, ResourceKey resourceKey, string message)
        {
            return new ResourcePlanDiagnostic(ResourcePlanDiagnosticSeverity.Info, code, groupId, resourceKey, message);
        }

        public static ResourcePlanDiagnostic Warning(string code, string groupId, ResourceKey resourceKey, string message)
        {
            return new ResourcePlanDiagnostic(ResourcePlanDiagnosticSeverity.Warning, code, groupId, resourceKey, message);
        }

        public static ResourcePlanDiagnostic Error(string code, string groupId, ResourceKey resourceKey, string message)
        {
            return new ResourcePlanDiagnostic(ResourcePlanDiagnosticSeverity.Error, code, groupId, resourceKey, message);
        }
    }

    public enum ResourcePlanDiagnosticSeverity
    {
        Info = 0,
        Warning = 1,
        Error = 2
    }
}
