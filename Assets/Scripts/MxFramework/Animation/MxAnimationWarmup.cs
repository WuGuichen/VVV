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
            bool skipPreloadWhenInvalid = true,
            MxAnimationCompatibilityProfile compatibilityProfile = null)
            : this(
                definition,
                clipRegistry,
                catalog,
                syncState,
                expectedClipRegistry,
                skipPreloadWhenInvalid,
                compatibilityProfile,
                null,
                null)
        {
        }

        public MxAnimationWarmupRequest(
            MxAnimationSetDefinition definition,
            MxAnimationClipRegistry clipRegistry,
            ResourceCatalog catalog,
            MxAnimationPresentationSyncState syncState,
            MxAnimationClipRegistry expectedClipRegistry,
            bool skipPreloadWhenInvalid,
            MxAnimationCompatibilityProfile compatibilityProfile,
            MxAnimationPackageExpectation packageExpectation,
            MxAnimationPackageCatalog packageCatalog = null)
        {
            Definition = definition;
            ClipRegistry = clipRegistry;
            Catalog = catalog;
            SyncState = syncState;
            ExpectedClipRegistry = expectedClipRegistry;
            CompatibilityProfile = compatibilityProfile;
            PackageExpectation = packageExpectation;
            PackageCatalog = packageCatalog;
            SkipPreloadWhenInvalid = skipPreloadWhenInvalid;
        }

        public MxAnimationSetDefinition Definition { get; }
        public MxAnimationClipRegistry ClipRegistry { get; }
        public ResourceCatalog Catalog { get; }
        public MxAnimationPresentationSyncState SyncState { get; }
        public MxAnimationClipRegistry ExpectedClipRegistry { get; }
        public MxAnimationCompatibilityProfile CompatibilityProfile { get; }
        public MxAnimationPackageExpectation PackageExpectation { get; }
        public MxAnimationPackageCatalog PackageCatalog { get; }
        public bool SkipPreloadWhenInvalid { get; }
    }

    public static class MxAnimationResourceTypeIds
    {
        public const string BakeArtifact = "MxAnimationBakeArtifact";
        public const string CompatibilityProfile = "MxAnimationCompatibilityProfile";
    }

    public enum MxAnimationPackageResourceKind
    {
        Resource = 0,
        AnimationClip = 1,
        AvatarMask = 2,
        BakeArtifact = 3,
        CompatibilityProfile = 4
    }

    public sealed class MxAnimationPackageResourceExpectation
    {
        public MxAnimationPackageResourceExpectation(
            ResourceKey key,
            string catalogEntryHash = "",
            string providerId = "",
            bool requiredForWarmup = true,
            MxAnimationPackageResourceKind kind = MxAnimationPackageResourceKind.Resource)
        {
            Key = key;
            CatalogEntryHash = catalogEntryHash ?? string.Empty;
            ProviderId = providerId ?? string.Empty;
            RequiredForWarmup = requiredForWarmup;
            Kind = kind == MxAnimationPackageResourceKind.Resource
                ? InferKind(key)
                : kind;
        }

        public ResourceKey Key { get; }
        public string CatalogEntryHash { get; }
        public string ProviderId { get; }
        public bool RequiredForWarmup { get; }
        public MxAnimationPackageResourceKind Kind { get; }

        private static MxAnimationPackageResourceKind InferKind(ResourceKey key)
        {
            if (string.Equals(key.TypeId, ResourceTypeIds.AnimationClip, StringComparison.Ordinal))
                return MxAnimationPackageResourceKind.AnimationClip;
            if (string.Equals(key.TypeId, ResourceTypeIds.AvatarMask, StringComparison.Ordinal))
                return MxAnimationPackageResourceKind.AvatarMask;
            if (string.Equals(key.TypeId, MxAnimationResourceTypeIds.BakeArtifact, StringComparison.Ordinal))
                return MxAnimationPackageResourceKind.BakeArtifact;
            if (string.Equals(key.TypeId, MxAnimationResourceTypeIds.CompatibilityProfile, StringComparison.Ordinal))
                return MxAnimationPackageResourceKind.CompatibilityProfile;

            return MxAnimationPackageResourceKind.Resource;
        }
    }

    public sealed class MxAnimationPackageExpectation
    {
        private readonly List<string> _acceptedProviderIds;
        private readonly List<MxAnimationPackageResourceExpectation> _resources;

        public MxAnimationPackageExpectation(
            string packageId,
            int version = 0,
            string catalogId = "",
            string catalogHash = "",
            IEnumerable<string> acceptedProviderIds = null,
            IEnumerable<MxAnimationPackageResourceExpectation> resources = null)
        {
            PackageId = packageId ?? string.Empty;
            Version = version < 0 ? 0 : version;
            CatalogId = catalogId ?? string.Empty;
            CatalogHash = catalogHash ?? string.Empty;
            _acceptedProviderIds = acceptedProviderIds != null
                ? new List<string>(acceptedProviderIds)
                : new List<string>();
            _resources = resources != null
                ? new List<MxAnimationPackageResourceExpectation>(resources)
                : new List<MxAnimationPackageResourceExpectation>();
        }

        public string PackageId { get; }
        public int Version { get; }
        public string CatalogId { get; }
        public string CatalogHash { get; }
        public IReadOnlyList<string> AcceptedProviderIds => _acceptedProviderIds;
        public IReadOnlyList<MxAnimationPackageResourceExpectation> Resources => _resources;

        public bool IsDefault =>
            string.IsNullOrWhiteSpace(PackageId)
            && Version == 0
            && string.IsNullOrWhiteSpace(CatalogId)
            && string.IsNullOrWhiteSpace(CatalogHash)
            && _acceptedProviderIds.Count == 0
            && _resources.Count == 0;
    }

    public sealed class MxAnimationPackageCatalog
    {
        public MxAnimationPackageCatalog(
            ResourceCatalog catalog,
            int version = 0,
            string catalogHash = "",
            string packageId = "",
            string catalogId = "")
        {
            Catalog = catalog;
            Version = version < 0 ? 0 : version;
            CatalogHash = catalogHash ?? string.Empty;
            PackageId = !string.IsNullOrWhiteSpace(packageId)
                ? packageId
                : catalog != null ? catalog.PackageId : string.Empty;
            CatalogId = !string.IsNullOrWhiteSpace(catalogId)
                ? catalogId
                : catalog != null ? catalog.CatalogId : string.Empty;
        }

        public ResourceCatalog Catalog { get; }
        public int Version { get; }
        public string CatalogHash { get; }
        public string PackageId { get; }
        public string CatalogId { get; }
    }

    public enum MxAnimationPackageValidationIssueSeverity
    {
        Error = 0,
        Warning = 1
    }

    public static class MxAnimationPackageValidationIssueCodes
    {
        public const string PackageCatalogMissing = "PackageCatalogMissing";
        public const string PackageIdMismatch = "PackageIdMismatch";
        public const string PackageVersionMismatch = "PackageVersionMismatch";
        public const string PackageCatalogIdMismatch = "PackageCatalogIdMismatch";
        public const string PackageCatalogHashMismatch = "PackageCatalogHashMismatch";
        public const string PackageResourceKeyInvalid = "PackageResourceKeyInvalid";
        public const string PackageResourceMissing = "PackageResourceMissing";
        public const string AnimationClipMissing = "AnimationClipMissing";
        public const string AvatarMaskMissing = "AvatarMaskMissing";
        public const string BakeArtifactMissing = "BakeArtifactMissing";
        public const string CompatibilityProfileMissing = "CompatibilityProfileMissing";
        public const string PackageResourceProviderMismatch = "PackageResourceProviderMismatch";
        public const string PackageResourceHashMismatch = "PackageResourceHashMismatch";
    }

    public sealed class MxAnimationPackageValidationIssue
    {
        public MxAnimationPackageValidationIssue(
            MxAnimationPackageValidationIssueSeverity severity,
            string code,
            ResourceKey key,
            string field,
            string expected,
            string actual,
            string message)
        {
            Severity = severity;
            Code = code ?? string.Empty;
            Key = key;
            Field = field ?? string.Empty;
            Expected = expected ?? string.Empty;
            Actual = actual ?? string.Empty;
            Message = message ?? string.Empty;
        }

        public MxAnimationPackageValidationIssueSeverity Severity { get; }
        public string Code { get; }
        public ResourceKey Key { get; }
        public string Field { get; }
        public string Expected { get; }
        public string Actual { get; }
        public string Message { get; }
    }

    public sealed class MxAnimationPackageValidationReport
    {
        private readonly List<MxAnimationPackageValidationIssue> _issues = new List<MxAnimationPackageValidationIssue>();

        public IReadOnlyList<MxAnimationPackageValidationIssue> Issues => _issues;
        public bool Success
        {
            get
            {
                for (int i = 0; i < _issues.Count; i++)
                {
                    if (_issues[i].Severity == MxAnimationPackageValidationIssueSeverity.Error)
                        return false;
                }

                return true;
            }
        }

        public void AddError(string code, ResourceKey key, string field, string expected, string actual, string message)
        {
            _issues.Add(new MxAnimationPackageValidationIssue(
                MxAnimationPackageValidationIssueSeverity.Error,
                code,
                key,
                field,
                expected,
                actual,
                message));
        }
    }

    public static class MxAnimationPackageCatalogValidator
    {
        public static MxAnimationPackageValidationReport Validate(
            MxAnimationPackageCatalog packageCatalog,
            MxAnimationPackageExpectation expectation)
        {
            var report = new MxAnimationPackageValidationReport();
            if (expectation == null || expectation.IsDefault)
                return report;

            ResourceCatalog catalog = packageCatalog != null ? packageCatalog.Catalog : null;
            if (catalog == null)
            {
                report.AddError(
                    MxAnimationPackageValidationIssueCodes.PackageCatalogMissing,
                    default,
                    "catalog",
                    "present",
                    "missing",
                    "Animation package validation requires a resource catalog.");
                return report;
            }

            ValidatePackageMetadata(packageCatalog, expectation, report);
            for (int i = 0; i < expectation.Resources.Count; i++)
                ValidateResource(catalog, expectation, expectation.Resources[i], report);

            return report;
        }

        private static void ValidatePackageMetadata(
            MxAnimationPackageCatalog packageCatalog,
            MxAnimationPackageExpectation expectation,
            MxAnimationPackageValidationReport report)
        {
            if (!string.IsNullOrWhiteSpace(expectation.PackageId)
                && !string.Equals(expectation.PackageId, packageCatalog.PackageId, StringComparison.Ordinal))
            {
                report.AddError(
                    MxAnimationPackageValidationIssueCodes.PackageIdMismatch,
                    default,
                    "packageId",
                    expectation.PackageId,
                    packageCatalog.PackageId,
                    "Animation package id does not match the expected package.");
            }

            if (expectation.Version > 0 && expectation.Version != packageCatalog.Version)
            {
                report.AddError(
                    MxAnimationPackageValidationIssueCodes.PackageVersionMismatch,
                    default,
                    "version",
                    expectation.Version.ToString(),
                    packageCatalog.Version.ToString(),
                    "Animation package version does not match the expected package.");
            }

            if (!string.IsNullOrWhiteSpace(expectation.CatalogId)
                && !string.Equals(expectation.CatalogId, packageCatalog.CatalogId, StringComparison.Ordinal))
            {
                report.AddError(
                    MxAnimationPackageValidationIssueCodes.PackageCatalogIdMismatch,
                    default,
                    "catalogId",
                    expectation.CatalogId,
                    packageCatalog.CatalogId,
                    "Animation package catalog id does not match the expected catalog.");
            }

            if (!string.IsNullOrWhiteSpace(expectation.CatalogHash)
                && !string.Equals(expectation.CatalogHash, packageCatalog.CatalogHash, StringComparison.Ordinal))
            {
                report.AddError(
                    MxAnimationPackageValidationIssueCodes.PackageCatalogHashMismatch,
                    default,
                    "catalogHash",
                    expectation.CatalogHash,
                    packageCatalog.CatalogHash,
                    "Animation package catalog hash does not match the expected catalog.");
            }
        }

        private static void ValidateResource(
            ResourceCatalog catalog,
            MxAnimationPackageExpectation packageExpectation,
            MxAnimationPackageResourceExpectation resourceExpectation,
            MxAnimationPackageValidationReport report)
        {
            if (resourceExpectation == null)
                return;

            ResourceKey key = resourceExpectation.Key;
            if (!key.IsValid)
            {
                report.AddError(
                    MxAnimationPackageValidationIssueCodes.PackageResourceKeyInvalid,
                    key,
                    "resourceKey",
                    "valid",
                    "invalid",
                    "Animation package resource expectation has an invalid resource key.");
                return;
            }

            if (!TryFindEntry(catalog, key, out ResourceCatalogEntry entry))
            {
                string code = MissingCode(resourceExpectation.Kind);
                report.AddError(
                    code,
                    key,
                    "catalogEntry",
                    "present",
                    "missing",
                    "Animation package catalog does not contain required " + DescribeKind(resourceExpectation.Kind) + ": " + key + ".");
                return;
            }

            string expectedProvider = resourceExpectation.ProviderId;
            if (!string.IsNullOrWhiteSpace(expectedProvider))
            {
                if (!string.Equals(expectedProvider, entry.ProviderId, StringComparison.Ordinal))
                {
                    report.AddError(
                        MxAnimationPackageValidationIssueCodes.PackageResourceProviderMismatch,
                        key,
                        "providerId",
                        expectedProvider,
                        entry.ProviderId,
                        "Animation package resource provider does not match the expected provider.");
                }
            }
            else if (packageExpectation.AcceptedProviderIds.Count > 0
                && !ContainsProvider(packageExpectation.AcceptedProviderIds, entry.ProviderId))
            {
                report.AddError(
                    MxAnimationPackageValidationIssueCodes.PackageResourceProviderMismatch,
                    key,
                    "providerId",
                    string.Join(",", packageExpectation.AcceptedProviderIds),
                    entry.ProviderId,
                    "Animation package resource provider is not allowed for this package expectation.");
            }

            if (!string.IsNullOrWhiteSpace(resourceExpectation.CatalogEntryHash)
                && !string.Equals(resourceExpectation.CatalogEntryHash, entry.Hash, StringComparison.Ordinal))
            {
                report.AddError(
                    MxAnimationPackageValidationIssueCodes.PackageResourceHashMismatch,
                    key,
                    "catalogEntryHash",
                    resourceExpectation.CatalogEntryHash,
                    entry.Hash,
                    "Animation package resource hash does not match the expected catalog entry hash.");
            }
        }

        private static bool TryFindEntry(ResourceCatalog catalog, ResourceKey key, out ResourceCatalogEntry entry)
        {
            for (int i = 0; i < catalog.Entries.Count; i++)
            {
                ResourceCatalogEntry candidate = catalog.Entries[i];
                if (candidate == null)
                    continue;

                ResourceKey candidateKey = candidate.CreateKey(catalog.PackageId);
                if (!MatchesKey(candidateKey, key))
                    continue;

                entry = candidate;
                return true;
            }

            entry = null;
            return false;
        }

        private static bool MatchesKey(ResourceKey catalogKey, ResourceKey requestedKey)
        {
            if (!string.Equals(catalogKey.Id, requestedKey.Id, StringComparison.Ordinal))
                return false;
            if (!string.Equals(catalogKey.TypeId, requestedKey.TypeId, StringComparison.Ordinal))
                return false;
            if (!string.Equals(catalogKey.Variant, requestedKey.Variant, StringComparison.Ordinal))
                return false;
            if (!string.IsNullOrWhiteSpace(requestedKey.PackageId)
                && !string.Equals(catalogKey.PackageId, requestedKey.PackageId, StringComparison.Ordinal))
                return false;

            return true;
        }

        private static bool ContainsProvider(IReadOnlyList<string> acceptedProviderIds, string providerId)
        {
            for (int i = 0; i < acceptedProviderIds.Count; i++)
            {
                if (string.Equals(acceptedProviderIds[i], providerId, StringComparison.Ordinal))
                    return true;
            }

            return false;
        }

        private static string MissingCode(MxAnimationPackageResourceKind kind)
        {
            switch (kind)
            {
                case MxAnimationPackageResourceKind.AnimationClip:
                    return MxAnimationPackageValidationIssueCodes.AnimationClipMissing;
                case MxAnimationPackageResourceKind.AvatarMask:
                    return MxAnimationPackageValidationIssueCodes.AvatarMaskMissing;
                case MxAnimationPackageResourceKind.BakeArtifact:
                    return MxAnimationPackageValidationIssueCodes.BakeArtifactMissing;
                case MxAnimationPackageResourceKind.CompatibilityProfile:
                    return MxAnimationPackageValidationIssueCodes.CompatibilityProfileMissing;
                default:
                    return MxAnimationPackageValidationIssueCodes.PackageResourceMissing;
            }
        }

        private static string DescribeKind(MxAnimationPackageResourceKind kind)
        {
            switch (kind)
            {
                case MxAnimationPackageResourceKind.AnimationClip:
                    return "animation clip";
                case MxAnimationPackageResourceKind.AvatarMask:
                    return "AvatarMask";
                case MxAnimationPackageResourceKind.BakeArtifact:
                    return "bake artifact";
                case MxAnimationPackageResourceKind.CompatibilityProfile:
                    return "compatibility profile";
                default:
                    return "resource";
            }
        }
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
        public const string CompatibilityValidationFailed = "CompatibilityValidationFailed";
        public const string PackageValidationFailed = "PackageValidationFailed";
        public const string PreloadOperationPending = "PreloadOperationPending";
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
            WarmupContext context = CreateWarmupContext(request);
            if (context.SkipPreload)
                return CreateWarmupResult(context, null);

            IResourceOperation<ResourcePreloadResult> operation = StartPreload(context, cancellationToken);
            if (operation == null)
                return CreateMissingPreloadOperationResult(context);

            if (!operation.IsDone)
            {
                operation.Cancel();
                return CreatePendingPreloadResult(context);
            }

            return CompleteWarmup(context, operation.Result);
        }

        public IResourceOperation<MxAnimationWarmupResult> WarmupAsync(
            MxAnimationWarmupRequest request,
            CancellationToken cancellationToken = default)
        {
            WarmupContext context = CreateWarmupContext(request);
            if (context.SkipPreload)
            {
                return new ImmediateResourceOperation<MxAnimationWarmupResult>(
                    ResourceLoadResult<MxAnimationWarmupResult>.Loaded(CreateWarmupResult(context, null)));
            }

            IResourceOperation<ResourcePreloadResult> operation = StartPreload(context, cancellationToken);
            if (operation == null)
            {
                return new ImmediateResourceOperation<MxAnimationWarmupResult>(
                    ResourceLoadResult<MxAnimationWarmupResult>.Loaded(CreateMissingPreloadOperationResult(context)));
            }

            return new MxAnimationWarmupOperation(context, operation);
        }

        public void Release(MxAnimationWarmupResult result)
        {
            if (result == null || result.Handle == null)
                return;

            _preloadService.ReleaseGroup(result.Handle);
        }

        private WarmupContext CreateWarmupContext(MxAnimationWarmupRequest request)
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
                return new WarmupContext(string.Empty, null, null, false, issues, true);
            }

            MxAnimationSetDefinition definition = request.Definition;
            MxAnimationWarmupDefinition warmup = definition.Warmup ?? MxAnimationWarmupDefinition.Default;
            List<ResourceKey> requiredKeys = CollectRequiredKeys(definition, warmup, request.PackageExpectation);
            List<string> labels = CollectLabels(warmup);
            string groupId = ResolveGroupId(definition, warmup);

            ValidateDefinition(definition, request.Catalog, issues);
            ValidateSyncState(definition, request.ClipRegistry, request.SyncState, issues);
            ValidateClipRegistry(requiredKeys, request.ClipRegistry, request.ExpectedClipRegistry, issues);
            ValidateCompatibility(definition, request.CompatibilityProfile, issues);
            ValidatePackageExpectation(request, issues);

            bool skipPreload = request.SkipPreloadWhenInvalid && issues.Count > 0;
            return new WarmupContext(groupId, requiredKeys, labels, warmup.FailFast, issues, skipPreload);
        }

        private IResourceOperation<ResourcePreloadResult> StartPreload(
            WarmupContext context,
            CancellationToken cancellationToken)
        {
            var preloadPlan = new ResourcePreloadPlan(
                context.GroupId,
                context.RequiredKeys,
                context.Labels,
                context.FailFast);
            return _preloadService.PreloadAsync(preloadPlan, cancellationToken);
        }

        private static MxAnimationWarmupResult CompleteWarmup(
            WarmupContext context,
            ResourceLoadResult<ResourcePreloadResult> operationResult)
        {
            if (!operationResult.Success)
            {
                ResourceError error = operationResult.Error;
                var issues = CopyIssues(context);
                issues.Add(new MxAnimationWarmupIssue(
                    MxAnimationWarmupIssueCodes.PreloadOperationFailed,
                    error.Key,
                    "preload",
                    "success",
                    error.Code.ToString(),
                    error.Message,
                    error));
                return new MxAnimationWarmupResult(context.GroupId, null, context.RequiredKeys, context.Labels, issues);
            }

            ResourcePreloadResult preloadResult = operationResult.Value;
            if (preloadResult == null)
            {
                var issues = CopyIssues(context);
                var error = new ResourceError(
                    ResourceErrorCode.ProviderFailed,
                    default,
                    string.Empty,
                    "Animation warmup preload completed without a result.");
                issues.Add(new MxAnimationWarmupIssue(
                    MxAnimationWarmupIssueCodes.PreloadOperationFailed,
                    error.Key,
                    "preload",
                    "result",
                    "missing",
                    error.Message,
                    error));
                return new MxAnimationWarmupResult(context.GroupId, null, context.RequiredKeys, context.Labels, issues);
            }

            List<MxAnimationWarmupIssue> preloadIssues = CopyIssues(context);
            for (int i = 0; i < preloadResult.Errors.Count; i++)
            {
                ResourceError error = preloadResult.Errors[i];
                preloadIssues.Add(new MxAnimationWarmupIssue(
                    MxAnimationWarmupIssueCodes.PreloadResourceFailed,
                    error.Key,
                    "resource",
                    "loaded",
                    error.Code.ToString(),
                    error.Message,
                    error));
            }

            return new MxAnimationWarmupResult(
                context.GroupId,
                preloadResult,
                context.RequiredKeys,
                context.Labels,
                preloadIssues);
        }

        private static MxAnimationWarmupResult CreateWarmupResult(
            WarmupContext context,
            ResourcePreloadResult preloadResult)
        {
            return new MxAnimationWarmupResult(
                context.GroupId,
                preloadResult,
                context.RequiredKeys,
                context.Labels,
                context.Issues);
        }

        private static MxAnimationWarmupResult CreateMissingPreloadOperationResult(WarmupContext context)
        {
            var issues = CopyIssues(context);
            var error = new ResourceError(
                ResourceErrorCode.ProviderFailed,
                default,
                string.Empty,
                "Animation warmup preload service returned no operation.");
            issues.Add(new MxAnimationWarmupIssue(
                MxAnimationWarmupIssueCodes.PreloadOperationFailed,
                error.Key,
                "preload",
                "operation",
                "missing",
                error.Message,
                error));
            return new MxAnimationWarmupResult(context.GroupId, null, context.RequiredKeys, context.Labels, issues);
        }

        private static MxAnimationWarmupResult CreatePendingPreloadResult(WarmupContext context)
        {
            var issues = CopyIssues(context);
            issues.Add(new MxAnimationWarmupIssue(
                MxAnimationWarmupIssueCodes.PreloadOperationPending,
                default,
                "preload",
                "completed",
                "pending",
                "Animation warmup preload did not complete synchronously. Use WarmupAsync to poll non-immediate preload operations."));
            return new MxAnimationWarmupResult(context.GroupId, null, context.RequiredKeys, context.Labels, issues);
        }

        private static List<MxAnimationWarmupIssue> CopyIssues(WarmupContext context)
        {
            return new List<MxAnimationWarmupIssue>(context.Issues);
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

        private static void ValidateCompatibility(
            MxAnimationSetDefinition definition,
            MxAnimationCompatibilityProfile compatibilityProfile,
            List<MxAnimationWarmupIssue> issues)
        {
            MxAnimationCompatibilityExpectation expectation = definition.CompatibilityExpectation;
            if (expectation == null || expectation.IsDefault)
                return;

            MxAnimationCompatibilityValidationReport report = MxAnimationCompatibilityValidator.Validate(
                compatibilityProfile,
                expectation);
            for (int i = 0; i < report.Issues.Count; i++)
            {
                MxAnimationCompatibilityIssue issue = report.Issues[i];
                if (issue.Severity != MxAnimationCompatibilityIssueSeverity.Error)
                    continue;

                issues.Add(new MxAnimationWarmupIssue(
                    MxAnimationWarmupIssueCodes.CompatibilityValidationFailed,
                    issue.Key,
                    issue.Code,
                    issue.Expected,
                    issue.Actual,
                    issue.Message));
            }
        }

        private static void ValidatePackageExpectation(
            MxAnimationWarmupRequest request,
            List<MxAnimationWarmupIssue> issues)
        {
            if (request.PackageExpectation == null || request.PackageExpectation.IsDefault)
                return;

            MxAnimationPackageCatalog packageCatalog = request.PackageCatalog;
            if (packageCatalog == null)
            {
                string catalogHash = request.ClipRegistry != null
                    ? request.ClipRegistry.CatalogHash
                    : string.Empty;
                packageCatalog = new MxAnimationPackageCatalog(request.Catalog, catalogHash: catalogHash);
            }

            MxAnimationPackageValidationReport report = MxAnimationPackageCatalogValidator.Validate(
                packageCatalog,
                request.PackageExpectation);
            for (int i = 0; i < report.Issues.Count; i++)
            {
                MxAnimationPackageValidationIssue issue = report.Issues[i];
                if (issue.Severity != MxAnimationPackageValidationIssueSeverity.Error)
                    continue;

                issues.Add(new MxAnimationWarmupIssue(
                    MxAnimationWarmupIssueCodes.PackageValidationFailed,
                    issue.Key,
                    issue.Code,
                    issue.Expected,
                    issue.Actual,
                    issue.Message));
            }
        }

        private static List<ResourceKey> CollectRequiredKeys(
            MxAnimationSetDefinition definition,
            MxAnimationWarmupDefinition warmup,
            MxAnimationPackageExpectation packageExpectation)
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

                for (int blendIndex = 0; blendIndex < definition.Blend2DDefinitions.Count; blendIndex++)
                {
                    MxAnimationBlend2DDefinition blend = definition.Blend2DDefinitions[blendIndex];
                    if (blend == null)
                        continue;

                    for (int pointIndex = 0; pointIndex < blend.Points.Count; pointIndex++)
                    {
                        MxAnimationBlend2DPoint point = blend.Points[pointIndex];
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

            MxAnimationCompatibilityExpectation expectation = definition.CompatibilityExpectation;
            if (expectation != null && !expectation.IsDefault)
            {
                for (int i = 0; i < expectation.ClipExpectations.Count; i++)
                {
                    MxAnimationClipCompatibilityExpectation clip = expectation.ClipExpectations[i];
                    if (clip != null)
                        AddKey(clip.ClipKey, keys, unique);
                }

                for (int i = 0; i < expectation.AvatarMaskExpectations.Count; i++)
                {
                    MxAnimationAvatarMaskCompatibilityExpectation mask = expectation.AvatarMaskExpectations[i];
                    if (mask != null)
                        AddKey(mask.AvatarMaskKey, keys, unique);
                }
            }

            for (int i = 0; i < warmup.RequiredKeys.Count; i++)
                AddKey(warmup.RequiredKeys[i], keys, unique);

            if (packageExpectation != null)
            {
                for (int i = 0; i < packageExpectation.Resources.Count; i++)
                {
                    MxAnimationPackageResourceExpectation resource = packageExpectation.Resources[i];
                    if (resource != null && resource.RequiredForWarmup)
                        AddKey(resource.Key, keys, unique);
                }
            }

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

        private sealed class WarmupContext
        {
            private readonly List<ResourceKey> _requiredKeys;
            private readonly List<string> _labels;
            private readonly List<MxAnimationWarmupIssue> _issues;

            public WarmupContext(
                string groupId,
                IEnumerable<ResourceKey> requiredKeys,
                IEnumerable<string> labels,
                bool failFast,
                IEnumerable<MxAnimationWarmupIssue> issues,
                bool skipPreload)
            {
                GroupId = groupId ?? string.Empty;
                _requiredKeys = requiredKeys != null
                    ? new List<ResourceKey>(requiredKeys)
                    : new List<ResourceKey>();
                _labels = labels != null
                    ? new List<string>(labels)
                    : new List<string>();
                FailFast = failFast;
                _issues = issues != null
                    ? new List<MxAnimationWarmupIssue>(issues)
                    : new List<MxAnimationWarmupIssue>();
                SkipPreload = skipPreload;
            }

            public string GroupId { get; }
            public IReadOnlyList<ResourceKey> RequiredKeys => _requiredKeys;
            public IReadOnlyList<string> Labels => _labels;
            public bool FailFast { get; }
            public IReadOnlyList<MxAnimationWarmupIssue> Issues => _issues;
            public bool SkipPreload { get; }
        }

        private sealed class MxAnimationWarmupOperation : IResourceOperation<MxAnimationWarmupResult>
        {
            private readonly WarmupContext _context;
            private readonly IResourceOperation<ResourcePreloadResult> _preloadOperation;

            public MxAnimationWarmupOperation(
                WarmupContext context,
                IResourceOperation<ResourcePreloadResult> preloadOperation)
            {
                _context = context;
                _preloadOperation = preloadOperation;
            }

            public bool IsDone => _preloadOperation.IsDone;
            public bool IsCancelled => _preloadOperation.IsCancelled;
            public float Progress => _preloadOperation.Progress;

            public ResourceLoadResult<MxAnimationWarmupResult> Result
            {
                get
                {
                    if (!_preloadOperation.IsDone)
                    {
                        return ResourceLoadResult<MxAnimationWarmupResult>.Failed(new ResourceError(
                            ResourceErrorCode.ProviderFailed,
                            default,
                            string.Empty,
                            "Animation warmup preload operation is not complete."));
                    }

                    return ResourceLoadResult<MxAnimationWarmupResult>.Loaded(CompleteWarmup(_context, _preloadOperation.Result));
                }
            }

            public void Cancel()
            {
                _preloadOperation.Cancel();
            }
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
