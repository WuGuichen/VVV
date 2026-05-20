using System;
using System.Collections.Generic;
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

        private static string CreateGroupId(ResourcePlan plan)
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
        private readonly List<ResourceKey> _resourceKeys;
        private readonly List<string> _labels;

        public ResourcePlan(
            string planId,
            string planHash,
            IEnumerable<ResourceKey> resourceKeys,
            IEnumerable<string> labels = null,
            bool failFast = false)
        {
            PlanId = planId ?? string.Empty;
            PlanHash = planHash ?? string.Empty;
            _resourceKeys = resourceKeys != null ? new List<ResourceKey>(resourceKeys) : new List<ResourceKey>();
            _labels = labels != null ? new List<string>(labels) : new List<string>();
            FailFast = failFast;
        }

        public string PlanId { get; }
        public string PlanHash { get; }
        public IReadOnlyList<ResourceKey> ResourceKeys => _resourceKeys;
        public IReadOnlyList<string> Labels => _labels;
        public bool FailFast { get; }
    }

    public sealed class ResourcePlanSession
    {
        private readonly List<ResourceHandle<object>> _loadedResourceHandles;
        private readonly List<ResourcePlanDiagnostic> _diagnostics;

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
        }

        public ResourcePlan Plan { get; private set; }
        public string PlanId => Plan != null ? Plan.PlanId : string.Empty;
        public string PlanHash => Plan != null ? Plan.PlanHash : string.Empty;
        public IReadOnlyList<ResourceHandle<object>> LoadedResourceHandles => _loadedResourceHandles;
        public IReadOnlyList<ResourcePlanDiagnostic> Diagnostics => _diagnostics;
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
