using System;
using System.Collections.Generic;
using System.IO;
using MxFramework.Resources;
using MxFramework.Resources.Unity;
using UnityEngine;

namespace MxFramework.Demo
{
    public sealed class RuntimeVerticalSlicePlayerResourceTest : IDisposable
    {
        public const string DefaultCatalogRelativePath = "MxFramework/Samples/mxframework_samples_player_catalog.json";
        public const string DefaultBundleRootRelativePath = "MxFramework/Samples/Bundles";
        public const string ExpectedResourceId = "ui.start_screen.button.normal";
        public const string ExpectedWarmupLabel = "warmup.demo.start_screen";
        public const string WarmupGroupId = "samples.player_streaming.start_screen";

        private readonly string _catalogRelativePath;
        private readonly string _bundleRootRelativePath;
        private readonly List<ResourceHandle<Texture2D>> _textureHandles = new List<ResourceHandle<Texture2D>>();
        private readonly List<ResourceGroupHandle> _warmupGroups = new List<ResourceGroupHandle>();

        private ResourceManager _manager;
        private ResourcePreloadService _preloadService;
        private IResourceProvider _assetBundleProvider;
        private bool _released;

        public RuntimeVerticalSlicePlayerResourceTest()
            : this(DefaultCatalogRelativePath, DefaultBundleRootRelativePath)
        {
        }

        public RuntimeVerticalSlicePlayerResourceTest(string catalogRelativePath, string bundleRootRelativePath)
        {
            _catalogRelativePath = NormalizeRelativePath(catalogRelativePath);
            _bundleRootRelativePath = NormalizeRelativePath(bundleRootRelativePath);
        }

        public string CatalogRelativePath => _catalogRelativePath;
        public string BundleRootRelativePath => _bundleRootRelativePath;

        public static ResourceKey ExpectedTextureKey => new ResourceKey(ExpectedResourceId, ResourceTypeIds.Texture2D);

        public static bool DefaultFixtureExists()
        {
            return FixtureExists(DefaultCatalogRelativePath, DefaultBundleRootRelativePath);
        }

        public RuntimeVerticalSlicePlayerResourceTestResult Run()
        {
            var lines = new List<string>();
            try
            {
                if (!FixtureExists(_catalogRelativePath, _bundleRootRelativePath))
                {
                    return RuntimeVerticalSlicePlayerResourceTestResult.Blocked(
                        CreateMissingFixtureMessage(_catalogRelativePath, _bundleRootRelativePath),
                        lines);
                }

                ResourceCatalog catalog = StreamingResourceCatalogLoader.LoadFromStreamingAssets(_catalogRelativePath);
                ResourceCatalogEntry expectedEntry = FindExpectedEntry(catalog);
                if (expectedEntry == null)
                {
                    return RuntimeVerticalSlicePlayerResourceTestResult.Failed(
                        "Player resource catalog does not contain " + ExpectedTextureKey + " with label " + ExpectedWarmupLabel + ".",
                        lines);
                }

                string bundleRoot = Path.Combine(Application.streamingAssetsPath, _bundleRootRelativePath);
                _assetBundleProvider = new AssetBundleProvider(bundleRoot);
                _manager = new ResourceManager();
                _manager.RegisterProvider(_assetBundleProvider);
                _manager.SetVariantProfile(ResourceVariantProfile.Empty);
                _manager.AddCatalog(catalog);
                _manager.ValidateCatalogs();
                _preloadService = new ResourcePreloadService(_manager);

                ResourcePreloadResult warmup = WarmupStartScreen();
                ResourceDebugSnapshot afterWarmup = _manager.CreateDebugSnapshot();
                int bundlesAfterWarmup = GetLoadedBundleCount();

                ResourceHandle<Texture2D> texture = LoadTexture();
                _textureHandles.Add(texture);
                ResourceDebugSnapshot afterDirectLoad = _manager.CreateDebugSnapshot();
                int bundlesAfterDirectLoad = GetLoadedBundleCount();
                bool directTextureLoaded = texture.Value != null;
                string warmupLine = CreateWarmupLine(warmup, bundlesAfterWarmup);
                string directLine = CreateDirectLine(texture, bundlesAfterDirectLoad);

                ReleaseDirectHandles();
                ResourceDebugSnapshot afterDirectRelease = _manager.CreateDebugSnapshot();

                ReleaseWarmupGroups();
                ResourceDebugSnapshot afterFullRelease = _manager.CreateDebugSnapshot();
                int bundlesAfterFullRelease = GetLoadedBundleCount();
                _released = true;

                lines.Add(warmupLine);
                lines.Add(directLine);
                lines.Add(CreateDiagnosticsLine(afterWarmup, afterDirectLoad, afterDirectRelease, afterFullRelease, bundlesAfterFullRelease));

                bool success = warmup.Success
                    && warmup.RequestedCount == 1
                    && warmup.LoadedCount == 1
                    && directTextureLoaded
                    && afterWarmup.LoadedCount == 1
                    && afterWarmup.TotalRefCount == 1
                    && afterDirectLoad.LoadedCount == 1
                    && afterDirectLoad.TotalRefCount == 2
                    && afterDirectRelease.LoadedCount == 1
                    && afterDirectRelease.TotalRefCount == 1
                    && afterFullRelease.LoadedCount == 0
                    && afterFullRelease.TotalRefCount == 0
                    && afterFullRelease.FailedCount == 0
                    && bundlesAfterFullRelease == 0;
                string failure = success
                    ? string.Empty
                    : "Player resource smoke did not complete cleanly.";

                return new RuntimeVerticalSlicePlayerResourceTestResult(
                    success,
                    true,
                    CreateSummary(success, warmup, afterFullRelease, bundlesAfterFullRelease),
                    failure,
                    lines,
                    warmup.RequestedCount,
                    warmup.LoadedCount,
                    warmup.FailedCount,
                    directTextureLoaded,
                    afterWarmup,
                    afterDirectLoad,
                    afterDirectRelease,
                    afterFullRelease,
                    bundlesAfterWarmup,
                    bundlesAfterDirectLoad,
                    bundlesAfterFullRelease);
            }
            catch (Exception ex)
            {
                Release();
                return RuntimeVerticalSlicePlayerResourceTestResult.Failed(
                    "Player resource smoke failed: " + ex.Message,
                    lines);
            }
        }

        public void Release()
        {
            if (_released)
                return;

            ReleaseDirectHandles();
            ReleaseWarmupGroups();
            _released = true;
        }

        public void Dispose()
        {
            Release();
        }

        private static bool FixtureExists(string catalogRelativePath, string bundleRootRelativePath)
        {
            string catalogPath = Path.Combine(Application.streamingAssetsPath, NormalizeRelativePath(catalogRelativePath));
            string bundleRoot = Path.Combine(Application.streamingAssetsPath, NormalizeRelativePath(bundleRootRelativePath));
            return File.Exists(catalogPath) && Directory.Exists(bundleRoot);
        }

        private static string CreateMissingFixtureMessage(string catalogRelativePath, string bundleRootRelativePath)
        {
            return "Player resource StreamingAssets fixture is missing. Expected catalog '" +
                catalogRelativePath +
                "' and bundle root '" +
                bundleRootRelativePath +
                "'.";
        }

        private ResourceCatalogEntry FindExpectedEntry(ResourceCatalog catalog)
        {
            if (catalog == null)
                return null;

            for (int i = 0; i < catalog.Entries.Count; i++)
            {
                ResourceCatalogEntry entry = catalog.Entries[i];
                if (entry == null)
                    continue;

                if (!string.Equals(entry.Id, ExpectedResourceId, StringComparison.Ordinal))
                    continue;
                if (!string.Equals(entry.TypeId, ResourceTypeIds.Texture2D, StringComparison.Ordinal))
                    continue;
                if (!HasLabel(entry, ExpectedWarmupLabel))
                    continue;

                return entry;
            }

            return null;
        }

        private static bool HasLabel(ResourceCatalogEntry entry, string label)
        {
            for (int i = 0; i < entry.Labels.Count; i++)
            {
                if (string.Equals(entry.Labels[i], label, StringComparison.Ordinal))
                    return true;
            }

            return false;
        }

        private ResourcePreloadResult WarmupStartScreen()
        {
            ResourceLoadResult<ResourcePreloadResult> result = _preloadService.PreloadAsync(new ResourcePreloadPlan(
                WarmupGroupId,
                labels: new[] { ExpectedWarmupLabel },
                failFast: true)).Result;
            if (!result.Success)
                throw new InvalidOperationException("Player resource warmup failed: " + result.Error);

            ResourcePreloadResult preload = result.Value;
            if (preload == null)
                throw new InvalidOperationException("Player resource warmup returned no result.");
            if (preload.Handle != null)
                _warmupGroups.Add(preload.Handle);
            if (!preload.Success)
                throw new InvalidOperationException("Player resource warmup errors: " + FormatErrors(preload.Errors));
            if (preload.RequestedCount != 1 || preload.LoadedCount != 1)
            {
                throw new InvalidOperationException(
                    "Player resource warmup count mismatch: requested=" + preload.RequestedCount +
                    " loaded=" + preload.LoadedCount + ".");
            }

            return preload;
        }

        private ResourceHandle<Texture2D> LoadTexture()
        {
            ResourceLoadResult<ResourceHandle<Texture2D>> result = _manager.Load<Texture2D>(ExpectedTextureKey);
            if (!result.Success)
                throw new InvalidOperationException("Player resource direct texture load failed: " + result.Error);
            if (result.Value == null || result.Value.Value == null)
                throw new InvalidOperationException("Player resource direct texture load returned null.");

            return result.Value;
        }

        private void ReleaseDirectHandles()
        {
            if (_manager == null)
                return;

            for (int i = _textureHandles.Count - 1; i >= 0; i--)
                _manager.Release(_textureHandles[i]);
            _textureHandles.Clear();
        }

        private void ReleaseWarmupGroups()
        {
            if (_preloadService == null)
                return;

            for (int i = _warmupGroups.Count - 1; i >= 0; i--)
                _preloadService.ReleaseGroup(_warmupGroups[i]);
            _warmupGroups.Clear();
        }

        private static string NormalizeRelativePath(string relativePath)
        {
            return (relativePath ?? string.Empty).Replace('\\', '/').Trim('/');
        }

        private static string CreateWarmupLine(ResourcePreloadResult warmup, int loadedBundles)
        {
            return "Player resources warmup: " + ExpectedWarmupLabel +
                " " + warmup.LoadedCount + "/" + warmup.RequestedCount +
                ", bundles=" + loadedBundles;
        }

        private static string CreateDirectLine(ResourceHandle<Texture2D> texture, int loadedBundles)
        {
            Texture2D value = texture.Value;
            return "Player resources direct: " + ExpectedResourceId +
                " Texture2D " + value.width + "x" + value.height +
                ", bundles=" + loadedBundles;
        }

        private static string CreateDiagnosticsLine(
            ResourceDebugSnapshot afterWarmup,
            ResourceDebugSnapshot afterDirectLoad,
            ResourceDebugSnapshot afterDirectRelease,
            ResourceDebugSnapshot afterFullRelease,
            int loadedBundles)
        {
            return "Player resources diagnostics: warmup " + FormatSnapshot(afterWarmup) +
                "; direct " + FormatSnapshot(afterDirectLoad) +
                "; directRelease " + FormatSnapshot(afterDirectRelease) +
                "; fullRelease " + FormatSnapshot(afterFullRelease) +
                "; bundles=" + loadedBundles;
        }

        private static string CreateSummary(
            bool success,
            ResourcePreloadResult warmup,
            ResourceDebugSnapshot afterFullRelease,
            int loadedBundles)
        {
            string status = success ? "ok" : "failed";
            return "Player resources " + status +
                ": warmup " + warmup.LoadedCount + "/" + warmup.RequestedCount +
                ", release loaded=" + afterFullRelease.LoadedCount +
                " refs=" + afterFullRelease.TotalRefCount +
                " bundles=" + loadedBundles;
        }

        private static string FormatSnapshot(ResourceDebugSnapshot snapshot)
        {
            if (snapshot == null)
                return "loaded=?, refs=?, failed=?";

            return "loaded=" + snapshot.LoadedCount +
                " refs=" + snapshot.TotalRefCount +
                " failed=" + snapshot.FailedCount;
        }

        private static string FormatErrors(IReadOnlyList<ResourceError> errors)
        {
            if (errors == null || errors.Count == 0)
                return "none";

            var parts = new List<string>(errors.Count);
            for (int i = 0; i < errors.Count; i++)
                parts.Add(errors[i].ToString());

            return string.Join("; ", parts.ToArray());
        }

        private int GetLoadedBundleCount()
        {
            return _assetBundleProvider is AssetBundleProvider provider ? provider.LoadedBundleCount : 0;
        }
    }

    public sealed class RuntimeVerticalSlicePlayerResourceTestResult
    {
        public RuntimeVerticalSlicePlayerResourceTestResult(
            bool success,
            bool fixtureAvailable,
            string summary,
            string failureMessage,
            IReadOnlyList<string> logLines,
            int warmupRequestedCount,
            int warmupLoadedCount,
            int warmupFailedCount,
            bool directTextureLoaded,
            ResourceDebugSnapshot afterWarmupSnapshot,
            ResourceDebugSnapshot afterDirectLoadSnapshot,
            ResourceDebugSnapshot afterDirectReleaseSnapshot,
            ResourceDebugSnapshot afterFullReleaseSnapshot,
            int loadedBundleCountAfterWarmup,
            int loadedBundleCountAfterDirectLoad,
            int loadedBundleCountAfterFullRelease)
        {
            Success = success;
            FixtureAvailable = fixtureAvailable;
            Summary = summary ?? string.Empty;
            FailureMessage = failureMessage ?? string.Empty;
            LogLines = logLines != null ? new List<string>(logLines) : new List<string>();
            WarmupRequestedCount = warmupRequestedCount;
            WarmupLoadedCount = warmupLoadedCount;
            WarmupFailedCount = warmupFailedCount;
            DirectTextureLoaded = directTextureLoaded;
            AfterWarmupSnapshot = afterWarmupSnapshot;
            AfterDirectLoadSnapshot = afterDirectLoadSnapshot;
            AfterDirectReleaseSnapshot = afterDirectReleaseSnapshot;
            AfterFullReleaseSnapshot = afterFullReleaseSnapshot;
            LoadedBundleCountAfterWarmup = loadedBundleCountAfterWarmup;
            LoadedBundleCountAfterDirectLoad = loadedBundleCountAfterDirectLoad;
            LoadedBundleCountAfterFullRelease = loadedBundleCountAfterFullRelease;
        }

        public bool Success { get; }
        public bool FixtureAvailable { get; }
        public string Summary { get; }
        public string FailureMessage { get; }
        public IReadOnlyList<string> LogLines { get; }
        public int WarmupRequestedCount { get; }
        public int WarmupLoadedCount { get; }
        public int WarmupFailedCount { get; }
        public bool DirectTextureLoaded { get; }
        public ResourceDebugSnapshot AfterWarmupSnapshot { get; }
        public ResourceDebugSnapshot AfterDirectLoadSnapshot { get; }
        public ResourceDebugSnapshot AfterDirectReleaseSnapshot { get; }
        public ResourceDebugSnapshot AfterFullReleaseSnapshot { get; }
        public int LoadedBundleCountAfterWarmup { get; }
        public int LoadedBundleCountAfterDirectLoad { get; }
        public int LoadedBundleCountAfterFullRelease { get; }

        public static RuntimeVerticalSlicePlayerResourceTestResult Blocked(string message, IReadOnlyList<string> lines)
        {
            return new RuntimeVerticalSlicePlayerResourceTestResult(
                false,
                false,
                "Player resources blocked",
                message,
                lines,
                0,
                0,
                0,
                false,
                null,
                null,
                null,
                null,
                0,
                0,
                0);
        }

        public static RuntimeVerticalSlicePlayerResourceTestResult Failed(string message, IReadOnlyList<string> lines)
        {
            return new RuntimeVerticalSlicePlayerResourceTestResult(
                false,
                true,
                "Player resources failed",
                message,
                lines,
                0,
                0,
                0,
                false,
                null,
                null,
                null,
                null,
                0,
                0,
                0);
        }
    }
}
