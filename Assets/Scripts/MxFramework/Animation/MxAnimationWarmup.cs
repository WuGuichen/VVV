using System;
using System.Collections.Generic;
using System.Threading;
using MxFramework.Resources;

namespace MxFramework.Animation
{
    public sealed class MxAnimationWarmupDefinition
    {
        private readonly List<ResourceKey> _requiredKeys;
        private readonly List<string> _labels;

        public MxAnimationWarmupDefinition(
            string groupId = "",
            IEnumerable<ResourceKey> requiredKeys = null,
            IEnumerable<string> labels = null,
            bool failFast = false,
            bool includeDefaultClip = true,
            bool includeFallbackClip = true,
            bool includeActionClips = true,
            bool includeLayerMasks = true)
        {
            GroupId = groupId ?? string.Empty;
            FailFast = failFast;
            IncludeDefaultClip = includeDefaultClip;
            IncludeFallbackClip = includeFallbackClip;
            IncludeActionClips = includeActionClips;
            IncludeLayerMasks = includeLayerMasks;
            _requiredKeys = requiredKeys != null
                ? new List<ResourceKey>(requiredKeys)
                : new List<ResourceKey>();
            _labels = labels != null
                ? new List<string>(labels)
                : new List<string>();
        }

        public string GroupId { get; }
        public IReadOnlyList<ResourceKey> RequiredKeys => _requiredKeys;
        public IReadOnlyList<string> Labels => _labels;
        public bool FailFast { get; }
        public bool IncludeDefaultClip { get; }
        public bool IncludeFallbackClip { get; }
        public bool IncludeActionClips { get; }
        public bool IncludeLayerMasks { get; }

        public bool IsDefault =>
            string.IsNullOrWhiteSpace(GroupId)
            && _requiredKeys.Count == 0
            && _labels.Count == 0
            && !FailFast
            && IncludeDefaultClip
            && IncludeFallbackClip
            && IncludeActionClips
            && IncludeLayerMasks;

        public static MxAnimationWarmupDefinition Default { get; } = new MxAnimationWarmupDefinition();
    }

    public sealed class MxAnimationWarmupRequest
    {
        public MxAnimationWarmupRequest(
            MxAnimationSetDefinition definition,
            MxAnimationClipRegistry clipRegistry = null,
            ResourceCatalog catalog = null,
            MxAnimationPresentationSyncState syncState = null,
            MxAnimationClipRegistry expectedClipRegistry = null,
            bool skipPreloadWhenInvalid = true)
        {
            Definition = definition;
            ClipRegistry = clipRegistry;
            Catalog = catalog;
            SyncState = syncState;
            ExpectedClipRegistry = expectedClipRegistry;
            SkipPreloadWhenInvalid = skipPreloadWhenInvalid;
        }

        public MxAnimationSetDefinition Definition { get; }
        public MxAnimationClipRegistry ClipRegistry { get; }
        public ResourceCatalog Catalog { get; }
        public MxAnimationPresentationSyncState SyncState { get; }
        public MxAnimationClipRegistry ExpectedClipRegistry { get; }
        public bool SkipPreloadWhenInvalid { get; }
    }

    public static class MxAnimationWarmupIssueCodes
    {
        public const string AnimationSetMissing = "AnimationSetMissing";
        public const string AnimationSetIdMismatch = "AnimationSetIdMismatch";
        public const string AnimationSetVersionMismatch = "AnimationSetVersionMismatch";
        public const string AnimationSetHashMismatch = "AnimationSetHashMismatch";
        public const string ResourceCatalogHashMismatch = "ResourceCatalogHashMismatch";
        public const string ClipRegistryVersionMismatch = "ClipRegistryVersionMismatch";
        public const string ClipRegistryCatalogHashMismatch = "ClipRegistryCatalogHashMismatch";
        public const string ClipRegistryEntryMissing = "ClipRegistryEntryMissing";
        public const string ExpectedClipRegistryEntryMissing = "ExpectedClipRegistryEntryMissing";
        public const string ClipRegistryEntryHashMismatch = "ClipRegistryEntryHashMismatch";
        public const string CatalogValidationFailed = "CatalogValidationFailed";
        public const string PreloadOperationFailed = "PreloadOperationFailed";
        public const string PreloadResourceFailed = "PreloadResourceFailed";
    }

    public sealed class MxAnimationWarmupIssue
    {
        public MxAnimationWarmupIssue(
            string code,
            ResourceKey key,
            string field,
            string expected,
            string actual,
            string message,
            ResourceError resourceError = default)
        {
            Code = code ?? string.Empty;
            Key = key;
            Field = field ?? string.Empty;
            Expected = expected ?? string.Empty;
            Actual = actual ?? string.Empty;
            Message = message ?? string.Empty;
            ResourceError = resourceError;
        }

        public string Code { get; }
        public ResourceKey Key { get; }
        public string Field { get; }
        public string Expected { get; }
        public string Actual { get; }
        public string Message { get; }
        public ResourceError ResourceError { get; }
    }

    public sealed class MxAnimationWarmupResult
    {
        private readonly List<ResourceKey> _requiredKeys;
        private readonly List<string> _labels;
        private readonly List<MxAnimationWarmupIssue> _issues;

        public MxAnimationWarmupResult(
            string groupId,
            ResourcePreloadResult preloadResult,
            IEnumerable<ResourceKey> requiredKeys,
            IEnumerable<string> labels,
            IEnumerable<MxAnimationWarmupIssue> issues)
        {
            GroupId = groupId ?? string.Empty;
            PreloadResult = preloadResult;
            _requiredKeys = requiredKeys != null
                ? new List<ResourceKey>(requiredKeys)
                : new List<ResourceKey>();
            _labels = labels != null
                ? new List<string>(labels)
                : new List<string>();
            _issues = issues != null
                ? new List<MxAnimationWarmupIssue>(issues)
                : new List<MxAnimationWarmupIssue>();
        }

        public string GroupId { get; }
        public ResourcePreloadResult PreloadResult { get; }
        public ResourceGroupHandle Handle => PreloadResult != null ? PreloadResult.Handle : null;
        public IReadOnlyList<ResourceKey> RequiredKeys => _requiredKeys;
        public IReadOnlyList<string> Labels => _labels;
        public IReadOnlyList<MxAnimationWarmupIssue> Issues => _issues;
        public int IssueCount => _issues.Count;
        public bool Success => _issues.Count == 0 && (PreloadResult == null || PreloadResult.Success);
    }

    public sealed class MxAnimationWarmupService
    {
        private readonly IResourcePreloadService _preloadService;

        public MxAnimationWarmupService(IResourcePreloadService preloadService)
        {
            _preloadService = preloadService ?? throw new ArgumentNullException(nameof(preloadService));
        }

        public MxAnimationWarmupResult Warmup(
            MxAnimationWarmupRequest request,
            CancellationToken cancellationToken = default)
        {
            var issues = new List<MxAnimationWarmupIssue>();
            if (request == null || request.Definition == null)
            {
                issues.Add(new MxAnimationWarmupIssue(
                    MxAnimationWarmupIssueCodes.AnimationSetMissing,
                    default,
                    "definition",
                    "present",
                    "missing",
                    "Animation warmup requires an animation set definition."));
                return new MxAnimationWarmupResult(string.Empty, null, null, null, issues);
            }

            MxAnimationSetDefinition definition = request.Definition;
            MxAnimationWarmupDefinition warmup = definition.Warmup ?? MxAnimationWarmupDefinition.Default;
            List<ResourceKey> requiredKeys = CollectRequiredKeys(definition, warmup);
            List<string> labels = CollectLabels(warmup);
            string groupId = ResolveGroupId(definition, warmup);

            ValidateDefinition(definition, request.Catalog, issues);
            ValidateSyncState(definition, request.ClipRegistry, request.SyncState, issues);
            ValidateClipRegistry(requiredKeys, request.ClipRegistry, request.ExpectedClipRegistry, issues);

            if (request.SkipPreloadWhenInvalid && issues.Count > 0)
                return new MxAnimationWarmupResult(groupId, null, requiredKeys, labels, issues);

            var preloadPlan = new ResourcePreloadPlan(groupId, requiredKeys, labels, warmup.FailFast);
            IResourceOperation<ResourcePreloadResult> operation = _preloadService.PreloadAsync(preloadPlan, cancellationToken);
            if (!operation.Result.Success)
            {
                ResourceError error = operation.Result.Error;
                issues.Add(new MxAnimationWarmupIssue(
                    MxAnimationWarmupIssueCodes.PreloadOperationFailed,
                    error.Key,
                    "preload",
                    "success",
                    error.Code.ToString(),
                    error.Message,
                    error));
                return new MxAnimationWarmupResult(groupId, null, requiredKeys, labels, issues);
            }

            ResourcePreloadResult preloadResult = operation.Result.Value;
            for (int i = 0; i < preloadResult.Errors.Count; i++)
            {
                ResourceError error = preloadResult.Errors[i];
                issues.Add(new MxAnimationWarmupIssue(
                    MxAnimationWarmupIssueCodes.PreloadResourceFailed,
                    error.Key,
                    "resource",
                    "loaded",
                    error.Code.ToString(),
                    error.Message,
                    error));
            }

            return new MxAnimationWarmupResult(groupId, preloadResult, requiredKeys, labels, issues);
        }

        public void Release(MxAnimationWarmupResult result)
        {
            if (result == null || result.Handle == null)
                return;

            _preloadService.ReleaseGroup(result.Handle);
        }

        private static void ValidateDefinition(
            MxAnimationSetDefinition definition,
            ResourceCatalog catalog,
            List<MxAnimationWarmupIssue> issues)
        {
            ResourceCatalogValidationReport report = MxAnimationSetDefinitionValidator.Validate(
                definition,
                catalog,
                catalog != null);

            for (int i = 0; i < report.Issues.Count; i++)
            {
                ResourceCatalogValidationIssue issue = report.Issues[i];
                if (issue.Severity != ResourceCatalogValidationSeverity.Error)
                    continue;

                issues.Add(new MxAnimationWarmupIssue(
                    MxAnimationWarmupIssueCodes.CatalogValidationFailed,
                    issue.Key,
                    issue.Code,
                    "valid",
                    "invalid",
                    issue.Message));
            }
        }

        private static void ValidateSyncState(
            MxAnimationSetDefinition definition,
            MxAnimationClipRegistry clipRegistry,
            MxAnimationPresentationSyncState syncState,
            List<MxAnimationWarmupIssue> issues)
        {
            if (syncState == null)
                return;

            if (!string.Equals(syncState.AnimationSetId, definition.SetId, StringComparison.Ordinal))
            {
                AddMismatch(
                    issues,
                    MxAnimationWarmupIssueCodes.AnimationSetIdMismatch,
                    "animationSetId",
                    syncState.AnimationSetId,
                    definition.SetId,
                    default,
                    "Animation sync state targets a different animation set.");
            }

            if (syncState.AnimationSetVersion != definition.Version)
            {
                AddMismatch(
                    issues,
                    MxAnimationWarmupIssueCodes.AnimationSetVersionMismatch,
                    "animationSetVersion",
                    syncState.AnimationSetVersion.ToString(),
                    definition.Version.ToString(),
                    default,
                    "Animation set version does not match sync state.");
            }

            if (!string.Equals(syncState.AnimationSetHash, definition.DefinitionHash, StringComparison.Ordinal))
            {
                AddMismatch(
                    issues,
                    MxAnimationWarmupIssueCodes.AnimationSetHashMismatch,
                    "animationSetHash",
                    syncState.AnimationSetHash,
                    definition.DefinitionHash,
                    default,
                    "Animation set hash does not match sync state.");
            }

            if (clipRegistry == null)
                return;

            if (!string.IsNullOrWhiteSpace(syncState.ResourceCatalogHash)
                && !string.Equals(syncState.ResourceCatalogHash, clipRegistry.CatalogHash, StringComparison.Ordinal))
            {
                AddMismatch(
                    issues,
                    MxAnimationWarmupIssueCodes.ResourceCatalogHashMismatch,
                    "resourceCatalogHash",
                    syncState.ResourceCatalogHash,
                    clipRegistry.CatalogHash,
                    default,
                    "Resource catalog hash does not match sync state.");
            }

            if (syncState.ClipRegistryVersion != clipRegistry.Version)
            {
                AddMismatch(
                    issues,
                    MxAnimationWarmupIssueCodes.ClipRegistryVersionMismatch,
                    "clipRegistryVersion",
                    syncState.ClipRegistryVersion.ToString(),
                    clipRegistry.Version.ToString(),
                    default,
                    "Clip registry version does not match sync state.");
            }
        }

        private static void ValidateClipRegistry(
            IReadOnlyList<ResourceKey> requiredKeys,
            MxAnimationClipRegistry clipRegistry,
            MxAnimationClipRegistry expectedClipRegistry,
            List<MxAnimationWarmupIssue> issues)
        {
            if (clipRegistry != null && expectedClipRegistry != null)
            {
                if (!string.Equals(expectedClipRegistry.CatalogHash, clipRegistry.CatalogHash, StringComparison.Ordinal))
                {
                    AddMismatch(
                        issues,
                        MxAnimationWarmupIssueCodes.ClipRegistryCatalogHashMismatch,
                        "catalogHash",
                        expectedClipRegistry.CatalogHash,
                        clipRegistry.CatalogHash,
                        default,
                        "Clip registry catalog hash does not match the expected registry.");
                }

                if (expectedClipRegistry.Version != clipRegistry.Version)
                {
                    AddMismatch(
                        issues,
                        MxAnimationWarmupIssueCodes.ClipRegistryVersionMismatch,
                        "clipRegistryVersion",
                        expectedClipRegistry.Version.ToString(),
                        clipRegistry.Version.ToString(),
                        default,
                        "Clip registry version does not match the expected registry.");
                }
            }

            for (int i = 0; i < requiredKeys.Count; i++)
            {
                ResourceKey key = requiredKeys[i];
                if (!string.Equals(key.TypeId, ResourceTypeIds.AnimationClip, StringComparison.Ordinal))
                    continue;

                MxAnimationClipRegistryEntry localEntry = default;
                bool localFound = clipRegistry == null || clipRegistry.TryFind(key, out localEntry);
                if (!localFound)
                {
                    issues.Add(new MxAnimationWarmupIssue(
                        MxAnimationWarmupIssueCodes.ClipRegistryEntryMissing,
                        key,
                        "clipRegistry",
                        "entry",
                        "missing",
                        "Clip registry does not contain required animation clip: " + key + "."));
                    continue;
                }

                if (expectedClipRegistry == null)
                    continue;

                if (!expectedClipRegistry.TryFind(key, out MxAnimationClipRegistryEntry expectedEntry))
                {
                    issues.Add(new MxAnimationWarmupIssue(
                        MxAnimationWarmupIssueCodes.ExpectedClipRegistryEntryMissing,
                        key,
                        "expectedClipRegistry",
                        "entry",
                        "missing",
                        "Expected clip registry does not contain required animation clip: " + key + "."));
                    continue;
                }

                if (string.IsNullOrWhiteSpace(expectedEntry.CatalogEntryHash)
                    || string.IsNullOrWhiteSpace(localEntry.CatalogEntryHash)
                    || string.Equals(expectedEntry.CatalogEntryHash, localEntry.CatalogEntryHash, StringComparison.Ordinal))
                {
                    continue;
                }

                AddMismatch(
                    issues,
                    MxAnimationWarmupIssueCodes.ClipRegistryEntryHashMismatch,
                    "catalogEntryHash",
                    expectedEntry.CatalogEntryHash,
                    localEntry.CatalogEntryHash,
                    key,
                    "Clip registry entry hash mismatch for required animation clip: " + key + ".");
            }
        }

        private static List<ResourceKey> CollectRequiredKeys(
            MxAnimationSetDefinition definition,
            MxAnimationWarmupDefinition warmup)
        {
            var keys = new List<ResourceKey>();
            var unique = new HashSet<ResourceKey>();

            if (warmup.IncludeDefaultClip)
                AddKey(definition.DefaultClip, keys, unique);
            if (warmup.IncludeFallbackClip)
                AddKey(definition.FallbackClip, keys, unique);
            if (warmup.IncludeActionClips)
            {
                for (int i = 0; i < definition.Actions.Count; i++)
                {
                    MxAnimationActionBinding action = definition.Actions[i];
                    if (action != null)
                        AddKey(action.Clip, keys, unique);
                }

                for (int blendIndex = 0; blendIndex < definition.Blend1DDefinitions.Count; blendIndex++)
                {
                    MxAnimationBlend1DDefinition blend = definition.Blend1DDefinitions[blendIndex];
                    if (blend == null)
                        continue;

                    for (int pointIndex = 0; pointIndex < blend.Points.Count; pointIndex++)
                    {
                        MxAnimationBlend1DPoint point = blend.Points[pointIndex];
                        if (point != null)
                            AddKey(point.ClipKey, keys, unique);
                    }
                }
            }

            if (warmup.IncludeLayerMasks)
            {
                for (int i = 0; i < definition.Layers.Count; i++)
                {
                    MxAnimationLayerDefinition layer = definition.Layers[i];
                    if (layer != null)
                        AddKey(layer.AvatarMaskKey, keys, unique);
                }
            }

            for (int i = 0; i < warmup.RequiredKeys.Count; i++)
                AddKey(warmup.RequiredKeys[i], keys, unique);

            return keys;
        }

        private static List<string> CollectLabels(MxAnimationWarmupDefinition warmup)
        {
            var labels = new List<string>();
            var unique = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < warmup.Labels.Count; i++)
            {
                string label = warmup.Labels[i] ?? string.Empty;
                if (string.IsNullOrWhiteSpace(label) || !unique.Add(label))
                    continue;

                labels.Add(label);
            }

            return labels;
        }

        private static void AddKey(ResourceKey key, List<ResourceKey> keys, HashSet<ResourceKey> unique)
        {
            if (!key.IsValid || !unique.Add(key))
                return;

            keys.Add(key);
        }

        private static string ResolveGroupId(MxAnimationSetDefinition definition, MxAnimationWarmupDefinition warmup)
        {
            if (!string.IsNullOrWhiteSpace(warmup.GroupId))
                return warmup.GroupId;

            return string.IsNullOrWhiteSpace(definition.SetId)
                ? "mxanimation.warmup"
                : "mxanimation." + definition.SetId + ".warmup";
        }

        private static void AddMismatch(
            List<MxAnimationWarmupIssue> issues,
            string code,
            string field,
            string expected,
            string actual,
            ResourceKey key,
            string message)
        {
            issues.Add(new MxAnimationWarmupIssue(code, key, field, expected, actual, message));
        }
    }
}
