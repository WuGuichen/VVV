using System;
using System.Collections.Generic;
using System.Threading;
using MxFramework.Resources;

namespace MxFramework.CharacterRuntimeSpawn
{
    public interface ICharacterResourceOrchestrator
    {
        IResourceOperation<ResourcePreloadResult> PreloadForSpawn(
            CharacterResourcePlan plan,
            CancellationToken cancellationToken = default);

        CharacterResourceSession AcquireForSpawn(CharacterResourcePlan plan, ResourcePreloadResult preloadResult);

        CharacterEquipmentResourceDiff PrepareEquipmentChange(
            CharacterResourceSession session,
            CharacterResourcePlan nextPlan,
            CancellationToken cancellationToken = default);

        void CommitEquipmentChange(CharacterResourceSession session, CharacterEquipmentResourceDiff diff);

        void Release(CharacterResourceSession session);
    }

    public sealed class CharacterResourceOrchestrator : ICharacterResourceOrchestrator
    {
        private readonly IResourcePreloadService _preloadService;
        private readonly IResourceManager _resourceManager;
        private readonly int _maxConcurrentLoads;

        public CharacterResourceOrchestrator(
            IResourcePreloadService preloadService,
            IResourceManager resourceManager,
            int maxConcurrentLoads = 4)
        {
            _preloadService = preloadService ?? throw new ArgumentNullException(nameof(preloadService));
            _resourceManager = resourceManager ?? throw new ArgumentNullException(nameof(resourceManager));
            _maxConcurrentLoads = maxConcurrentLoads < 1 ? 1 : maxConcurrentLoads;
        }

        public IResourceOperation<ResourcePreloadResult> PreloadForSpawn(
            CharacterResourcePlan plan,
            CancellationToken cancellationToken = default)
        {
            if (plan == null)
                return FailedPreload(CharacterResourceDiagnostics.MissingPlan, "Character resource plan is required.");

            var preloadPlan = new ResourcePreloadPlan(
                CreateGroupId("character.spawn", plan),
                plan.GetResourceKeys(),
                labels: null,
                failFast: ShouldFailFast(plan),
                maxConcurrentLoads: _maxConcurrentLoads);
            return _preloadService.PreloadAsync(preloadPlan, cancellationToken);
        }

        public CharacterResourceSession AcquireForSpawn(CharacterResourcePlan plan, ResourcePreloadResult preloadResult)
        {
            var diagnostics = new List<CharacterResourceDiagnostic>();
            if (plan == null)
            {
                diagnostics.Add(CharacterResourceDiagnostic.Error(
                    CharacterResourceDiagnostics.MissingPlan,
                    string.Empty,
                    default,
                    "Character resource plan is required."));
                return new CharacterResourceSession(null, Array.Empty<ResourceHandle<object>>(), diagnostics);
            }

            if (preloadResult == null)
            {
                diagnostics.Add(CharacterResourceDiagnostic.Error(
                    CharacterResourceDiagnostics.AcquireWithoutPreload,
                    string.Empty,
                    default,
                    "AcquireForSpawn requires a completed preload result."));
                return new CharacterResourceSession(plan, Array.Empty<ResourceHandle<object>>(), diagnostics);
            }

            AddPreloadDiagnostics(preloadResult, CharacterResourceDiagnostics.PreloadFailed, diagnostics);
            AddAudioDeferredDiagnostic(plan.Audio, diagnostics);

            IReadOnlyList<ResourceHandle<object>> handles = preloadResult.Handle != null
                ? preloadResult.Handle.Handles
                : Array.Empty<ResourceHandle<object>>();
            return new CharacterResourceSession(plan, handles, diagnostics);
        }

        public CharacterEquipmentResourceDiff PrepareEquipmentChange(
            CharacterResourceSession session,
            CharacterResourcePlan nextPlan,
            CancellationToken cancellationToken = default)
        {
            if (session == null || nextPlan == null)
            {
                return CharacterEquipmentResourceDiff.Invalid(
                    session,
                    nextPlan,
                    CharacterResourceDiagnostic.Error(
                        CharacterResourceDiagnostics.MissingPlan,
                        string.Empty,
                        default,
                        "Equipment change requires an active session and next plan."));
            }

            ResourceKey[] currentEquipment = session.Plan != null
                ? session.Plan.GetResourceKeys(CharacterResourcePlanGroupKind.EquipmentInitial)
                : Array.Empty<ResourceKey>();
            ResourceKey[] nextEquipment = nextPlan.GetResourceKeys(CharacterResourcePlanGroupKind.EquipmentInitial);

            ResourceKey[] keep = Intersect(currentEquipment, nextEquipment);
            ResourceKey[] preload = Except(nextEquipment, currentEquipment);
            ResourceKey[] release = Except(currentEquipment, nextEquipment);
            CharacterAudioResourceDiff audioDiff = CharacterAudioResourceDiff.Create(
                session.Plan != null ? session.Plan.Audio : CharacterAudioResourcePlan.Empty,
                nextPlan.Audio);

            var preloadPlan = new ResourcePreloadPlan(
                CreateGroupId("character.equipment", nextPlan),
                preload,
                labels: null,
                failFast: false,
                maxConcurrentLoads: _maxConcurrentLoads);
            IResourceOperation<ResourcePreloadResult> operation = _preloadService.PreloadAsync(preloadPlan, cancellationToken);
            return new CharacterEquipmentResourceDiff(session.Plan, nextPlan, keep, preload, release, audioDiff, operation);
        }

        public void CommitEquipmentChange(CharacterResourceSession session, CharacterEquipmentResourceDiff diff)
        {
            if (session == null || diff == null)
                return;
            if (session.IsReleased)
            {
                session.AddDiagnostic(CharacterResourceDiagnostic.Warning(
                    CharacterResourceDiagnostics.SessionAlreadyReleased,
                    session.PlanHash,
                    default,
                    "Equipment change was ignored because the character resource session is already released."));
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
                ResourceError error = default;
                if (!operationResult.Success)
                    error = operationResult.Error;
                else if (preloadResult != null)
                    error = FirstError(preloadResult);

                session.AddDiagnostic(CharacterResourceDiagnostic.Error(
                    CharacterResourceDiagnostics.EquipmentPreloadFailed,
                    diff.NextPlanHash,
                    error.Key,
                    string.IsNullOrEmpty(error.Message) ? "Equipment resource preload failed." : error.Message));
                return;
            }

            if (preloadResult != null && preloadResult.Handle != null)
                session.AddLoadedHandles(preloadResult.Handle.Handles);

            ReleaseKeys(session, diff.ReleaseResources);
            session.UpdatePlan(diff.NextPlan);
            AddAudioDeferredDiagnostic(diff.AudioDiff, session);
        }

        public void Release(CharacterResourceSession session)
        {
            if (session == null)
                return;

            if (!session.TryMarkReleased())
            {
                session.AddDiagnostic(CharacterResourceDiagnostic.Warning(
                    CharacterResourceDiagnostics.SessionAlreadyReleased,
                    session.PlanHash,
                    default,
                    "Character resource session was already released."));
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

        private void ReleaseKeys(CharacterResourceSession session, IReadOnlyList<ResourceKey> keys)
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

        private static bool ShouldFailFast(CharacterResourcePlan plan)
        {
            for (int i = 0; i < plan.Groups.Count; i++)
            {
                CharacterResourcePlanGroup group = plan.Groups[i];
                if (group.Required && group.FailurePolicy == CharacterResourceFailurePolicy.FailSpawn)
                    return true;
            }

            return false;
        }

        private static void AddPreloadDiagnostics(
            ResourcePreloadResult result,
            string code,
            List<CharacterResourceDiagnostic> diagnostics)
        {
            if (result == null || result.Errors == null)
                return;

            for (int i = 0; i < result.Errors.Count; i++)
            {
                ResourceError error = result.Errors[i];
                diagnostics.Add(CharacterResourceDiagnostic.Error(code, result.GroupId, error.Key, error.Message));
            }
        }

        private static void AddAudioDeferredDiagnostic(CharacterAudioResourcePlan audio, List<CharacterResourceDiagnostic> diagnostics)
        {
            if (audio == null || !audio.HasAudioRequirements)
                return;

            diagnostics.Add(CharacterResourceDiagnostic.Info(
                CharacterResourceDiagnostics.AudioWarmupDeferred,
                "Audio",
                default,
                "Audio banks, cue ids and event definitions are recorded on the character resource session; Audio/FMOD warmup is handled outside ResourceManager."));
        }

        private static void AddAudioDeferredDiagnostic(CharacterAudioResourceDiff diff, CharacterResourceSession session)
        {
            if (diff == null || !diff.HasChanges)
                return;

            session.AddDiagnostic(CharacterResourceDiagnostic.Info(
                CharacterResourceDiagnostics.AudioWarmupDeferred,
                "Audio",
                default,
                "Audio equipment diff was recorded only; Audio/FMOD warmup is handled outside ResourceManager."));
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

        private static string CreateGroupId(string prefix, CharacterResourcePlan plan)
        {
            string suffix = !string.IsNullOrEmpty(plan.PlanHash) ? plan.PlanHash : plan.CharacterStableId;
            return string.IsNullOrEmpty(suffix) ? prefix : prefix + "." + suffix;
        }

        private static ResourceKey[] Intersect(ResourceKey[] left, ResourceKey[] right)
        {
            var rightSet = new HashSet<ResourceKey>(right);
            var result = new List<ResourceKey>();
            for (int i = 0; i < left.Length; i++)
            {
                if (rightSet.Contains(left[i]))
                    result.Add(left[i]);
            }

            return result.ToArray();
        }

        private static ResourceKey[] Except(ResourceKey[] left, ResourceKey[] right)
        {
            var rightSet = new HashSet<ResourceKey>(right);
            var result = new List<ResourceKey>();
            for (int i = 0; i < left.Length; i++)
            {
                if (!rightSet.Contains(left[i]))
                    result.Add(left[i]);
            }

            return result.ToArray();
        }
    }

    public sealed class CharacterResourceSession
    {
        private readonly List<ResourceHandle<object>> _loadedResourceHandles;
        private readonly List<CharacterResourceDiagnostic> _diagnostics;

        internal CharacterResourceSession(
            CharacterResourcePlan plan,
            IReadOnlyList<ResourceHandle<object>> loadedResourceHandles,
            IEnumerable<CharacterResourceDiagnostic> diagnostics)
        {
            Plan = plan;
            _loadedResourceHandles = loadedResourceHandles != null
                ? new List<ResourceHandle<object>>(loadedResourceHandles)
                : new List<ResourceHandle<object>>();
            _diagnostics = diagnostics != null
                ? new List<CharacterResourceDiagnostic>(diagnostics)
                : new List<CharacterResourceDiagnostic>();
        }

        public CharacterResourcePlan Plan { get; private set; }
        public string PlanHash => Plan != null ? Plan.PlanHash : string.Empty;
        public IReadOnlyList<ResourceHandle<object>> LoadedResourceHandles => _loadedResourceHandles;
        public IReadOnlyList<CharacterResourceDiagnostic> Diagnostics => _diagnostics;
        public bool IsReleased { get; private set; }
        public IReadOnlyList<string> AudioBanks => Plan != null ? Plan.Audio.RequiredBanks : Array.Empty<string>();
        public IReadOnlyList<int> AudioCueIds => Plan != null ? Plan.Audio.RequiredCueIds : Array.Empty<int>();
        public IReadOnlyList<string> AudioEventDefinitionIds => Plan != null ? Plan.Audio.RequiredEventDefinitionIds : Array.Empty<string>();

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

        internal void AddDiagnostic(CharacterResourceDiagnostic diagnostic)
        {
            _diagnostics.Add(diagnostic);
        }

        internal void UpdatePlan(CharacterResourcePlan plan)
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

    public sealed class CharacterEquipmentResourceDiff
    {
        private readonly List<ResourceKey> _keepResources;
        private readonly List<ResourceKey> _preloadResources;
        private readonly List<ResourceKey> _releaseResources;
        private readonly List<CharacterResourceDiagnostic> _diagnostics;

        internal CharacterEquipmentResourceDiff(
            CharacterResourcePlan currentPlan,
            CharacterResourcePlan nextPlan,
            IEnumerable<ResourceKey> keepResources,
            IEnumerable<ResourceKey> preloadResources,
            IEnumerable<ResourceKey> releaseResources,
            CharacterAudioResourceDiff audioDiff,
            IResourceOperation<ResourcePreloadResult> preloadOperation)
        {
            CurrentPlan = currentPlan;
            NextPlan = nextPlan;
            _keepResources = keepResources != null ? new List<ResourceKey>(keepResources) : new List<ResourceKey>();
            _preloadResources = preloadResources != null ? new List<ResourceKey>(preloadResources) : new List<ResourceKey>();
            _releaseResources = releaseResources != null ? new List<ResourceKey>(releaseResources) : new List<ResourceKey>();
            AudioDiff = audioDiff ?? CharacterAudioResourceDiff.Empty;
            PreloadOperation = preloadOperation;
            _diagnostics = new List<CharacterResourceDiagnostic>();
        }

        public CharacterResourcePlan CurrentPlan { get; }
        public CharacterResourcePlan NextPlan { get; }
        public string CurrentPlanHash => CurrentPlan != null ? CurrentPlan.PlanHash : string.Empty;
        public string NextPlanHash => NextPlan != null ? NextPlan.PlanHash : string.Empty;
        public IReadOnlyList<ResourceKey> KeepResources => _keepResources;
        public IReadOnlyList<ResourceKey> PreloadResources => _preloadResources;
        public IReadOnlyList<ResourceKey> ReleaseResources => _releaseResources;
        public CharacterAudioResourceDiff AudioDiff { get; }
        public IResourceOperation<ResourcePreloadResult> PreloadOperation { get; }
        public IReadOnlyList<CharacterResourceDiagnostic> Diagnostics => _diagnostics;

        internal static CharacterEquipmentResourceDiff Invalid(
            CharacterResourceSession session,
            CharacterResourcePlan nextPlan,
            CharacterResourceDiagnostic diagnostic)
        {
            var diff = new CharacterEquipmentResourceDiff(
                session != null ? session.Plan : null,
                nextPlan,
                Array.Empty<ResourceKey>(),
                Array.Empty<ResourceKey>(),
                Array.Empty<ResourceKey>(),
                CharacterAudioResourceDiff.Empty,
                null);
            diff._diagnostics.Add(diagnostic);
            return diff;
        }
    }

    public sealed class CharacterAudioResourceDiff
    {
        private readonly List<string> _keepBanks;
        private readonly List<string> _preloadBanks;
        private readonly List<string> _releaseBanks;
        private readonly List<int> _keepCueIds;
        private readonly List<int> _preloadCueIds;
        private readonly List<int> _releaseCueIds;

        private CharacterAudioResourceDiff(
            IEnumerable<string> keepBanks,
            IEnumerable<string> preloadBanks,
            IEnumerable<string> releaseBanks,
            IEnumerable<int> keepCueIds,
            IEnumerable<int> preloadCueIds,
            IEnumerable<int> releaseCueIds)
        {
            _keepBanks = keepBanks != null ? new List<string>(keepBanks) : new List<string>();
            _preloadBanks = preloadBanks != null ? new List<string>(preloadBanks) : new List<string>();
            _releaseBanks = releaseBanks != null ? new List<string>(releaseBanks) : new List<string>();
            _keepCueIds = keepCueIds != null ? new List<int>(keepCueIds) : new List<int>();
            _preloadCueIds = preloadCueIds != null ? new List<int>(preloadCueIds) : new List<int>();
            _releaseCueIds = releaseCueIds != null ? new List<int>(releaseCueIds) : new List<int>();
        }

        public static CharacterAudioResourceDiff Empty { get; } = new CharacterAudioResourceDiff(
            Array.Empty<string>(),
            Array.Empty<string>(),
            Array.Empty<string>(),
            Array.Empty<int>(),
            Array.Empty<int>(),
            Array.Empty<int>());

        public IReadOnlyList<string> KeepBanks => _keepBanks;
        public IReadOnlyList<string> PreloadBanks => _preloadBanks;
        public IReadOnlyList<string> ReleaseBanks => _releaseBanks;
        public IReadOnlyList<int> KeepCueIds => _keepCueIds;
        public IReadOnlyList<int> PreloadCueIds => _preloadCueIds;
        public IReadOnlyList<int> ReleaseCueIds => _releaseCueIds;
        public bool HasChanges => _preloadBanks.Count > 0 || _releaseBanks.Count > 0 || _preloadCueIds.Count > 0 || _releaseCueIds.Count > 0;

        public static CharacterAudioResourceDiff Create(CharacterAudioResourcePlan current, CharacterAudioResourcePlan next)
        {
            current = current ?? CharacterAudioResourcePlan.Empty;
            next = next ?? CharacterAudioResourcePlan.Empty;
            return new CharacterAudioResourceDiff(
                Intersect(current.RequiredBanks, next.RequiredBanks),
                Except(next.RequiredBanks, current.RequiredBanks),
                Except(current.RequiredBanks, next.RequiredBanks),
                Intersect(current.RequiredCueIds, next.RequiredCueIds),
                Except(next.RequiredCueIds, current.RequiredCueIds),
                Except(current.RequiredCueIds, next.RequiredCueIds));
        }

        private static string[] Intersect(IReadOnlyList<string> left, IReadOnlyList<string> right)
        {
            var rightSet = new HashSet<string>(right, StringComparer.Ordinal);
            var result = new List<string>();
            for (int i = 0; i < left.Count; i++)
            {
                if (rightSet.Contains(left[i]))
                    result.Add(left[i]);
            }

            return result.ToArray();
        }

        private static string[] Except(IReadOnlyList<string> left, IReadOnlyList<string> right)
        {
            var rightSet = new HashSet<string>(right, StringComparer.Ordinal);
            var result = new List<string>();
            for (int i = 0; i < left.Count; i++)
            {
                if (!rightSet.Contains(left[i]))
                    result.Add(left[i]);
            }

            return result.ToArray();
        }

        private static int[] Intersect(IReadOnlyList<int> left, IReadOnlyList<int> right)
        {
            var rightSet = new HashSet<int>(right);
            var result = new List<int>();
            for (int i = 0; i < left.Count; i++)
            {
                if (rightSet.Contains(left[i]))
                    result.Add(left[i]);
            }

            return result.ToArray();
        }

        private static int[] Except(IReadOnlyList<int> left, IReadOnlyList<int> right)
        {
            var rightSet = new HashSet<int>(right);
            var result = new List<int>();
            for (int i = 0; i < left.Count; i++)
            {
                if (!rightSet.Contains(left[i]))
                    result.Add(left[i]);
            }

            return result.ToArray();
        }
    }

    public readonly struct CharacterResourceDiagnostic
    {
        public CharacterResourceDiagnostic(
            CharacterRuntimeSpawnIssueSeverity severity,
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

        public CharacterRuntimeSpawnIssueSeverity Severity { get; }
        public string Code { get; }
        public string GroupId { get; }
        public ResourceKey ResourceKey { get; }
        public string Message { get; }
        public bool IsError => Severity == CharacterRuntimeSpawnIssueSeverity.Error;

        public static CharacterResourceDiagnostic Info(string code, string groupId, ResourceKey resourceKey, string message)
        {
            return new CharacterResourceDiagnostic(CharacterRuntimeSpawnIssueSeverity.Info, code, groupId, resourceKey, message);
        }

        public static CharacterResourceDiagnostic Warning(string code, string groupId, ResourceKey resourceKey, string message)
        {
            return new CharacterResourceDiagnostic(CharacterRuntimeSpawnIssueSeverity.Warning, code, groupId, resourceKey, message);
        }

        public static CharacterResourceDiagnostic Error(string code, string groupId, ResourceKey resourceKey, string message)
        {
            return new CharacterResourceDiagnostic(CharacterRuntimeSpawnIssueSeverity.Error, code, groupId, resourceKey, message);
        }
    }
}
