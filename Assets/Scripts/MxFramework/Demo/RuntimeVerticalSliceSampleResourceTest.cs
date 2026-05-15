using System;
using System.Collections.Generic;
using MxFramework.Resources;
using UnityEngine;

namespace MxFramework.Demo
{
    public sealed class RuntimeVerticalSliceSampleResourceTest : IDisposable
    {
        private static readonly RuntimeVerticalSliceResourcePreloadCase[] PreloadCases =
        {
            new RuntimeVerticalSliceResourcePreloadCase("samples.package", "package", TempImportedResourceCatalog.PackageLabel, 16),
            new RuntimeVerticalSliceResourcePreloadCase("samples.start_screen", "StartScreen", TempImportedResourceCatalog.WarmupStartScreenLabel, 7),
            new RuntimeVerticalSliceResourcePreloadCase("samples.combat", "Combat", TempImportedResourceCatalog.WarmupCombatLabel, 9),
            new RuntimeVerticalSliceResourcePreloadCase("samples.status_effects", "StatusEffects", TempImportedResourceCatalog.WarmupStatusEffectsLabel, 4),
            new RuntimeVerticalSliceResourcePreloadCase("samples.magic_effects", "MagicEffects", TempImportedResourceCatalog.WarmupMagicEffectsLabel, 4)
        };

        private readonly List<ResourceGroupHandle> _warmupGroups = new List<ResourceGroupHandle>();
        private readonly List<ResourceHandle<GameObject>> _prefabHandles = new List<ResourceHandle<GameObject>>();
        private readonly List<ResourceHandle<Texture2D>> _textureHandles = new List<ResourceHandle<Texture2D>>();
        private readonly List<ResourceHandle<AudioClip>> _audioHandles = new List<ResourceHandle<AudioClip>>();
        private readonly List<GameObject> _instances = new List<GameObject>();

        private ResourceManager _manager;
        private ResourcePreloadService _preloadService;
        private MemoryResourceProvider _provider;
        private bool _released;

        public static Func<string, UnityEngine.Object> AssetPathLoader { get; set; }

        public RuntimeVerticalSliceSampleResourceTestResult Run()
        {
            var lines = new List<string>();
            try
            {
                Func<string, UnityEngine.Object> loader = AssetPathLoader;
                if (loader == null)
                {
                    return RuntimeVerticalSliceSampleResourceTestResult.Failed(
                        "Samples resource test needs an AssetPathLoader. In Editor Play Mode this is registered by TempImportedResourceCatalogEditorBootstrap.",
                        lines);
                }

                ResourceCatalog catalog = TempImportedResourceCatalog.CreateCatalog();
                _provider = TempImportedResourceCatalog.CreateMemoryProvider(catalog, loader);
                ResourceCatalogValidationReport validation = ValidateCatalog(catalog, _provider);
                if (validation.HasErrors)
                {
                    return RuntimeVerticalSliceSampleResourceTestResult.Failed(
                        "Samples resource catalog validation failed: " + FormatValidationReport(validation),
                        lines);
                }

                _manager = new ResourceManager();
                _manager.RegisterProvider(_provider);
                _manager.SetVariantProfile(ResourceVariantProfile.Empty);
                _manager.AddCatalog(catalog);
                _manager.ValidateCatalogs();
                _preloadService = new ResourcePreloadService(_manager);

                List<RuntimeVerticalSliceResourcePreloadReport> preloadReports = PreloadSamples();
                ResourceDebugSnapshot afterWarmup = _manager.CreateDebugSnapshot();

                RuntimeVerticalSliceDirectResourceReport directReport = LoadDirectSamples();
                ResourceDebugSnapshot afterDirectLoad = _manager.CreateDebugSnapshot();

                int destroyedInstances = DestroyInstances();
                directReport = CreateDestroyedReport(directReport, destroyedInstances);
                ReleaseDirectHandles();
                ResourceDebugSnapshot afterDirectRelease = _manager.CreateDebugSnapshot();

                ReleaseWarmupGroups();
                ResourceDebugSnapshot afterFullRelease = _manager.CreateDebugSnapshot();
                _released = true;

                lines.Add(CreateWarmupLine(preloadReports));
                lines.Add(CreateDirectLine(directReport));
                lines.Add(CreateDiagnosticsLine(afterWarmup, afterDirectLoad, afterDirectRelease, afterFullRelease));

                bool success = afterFullRelease.LoadedCount == 0
                    && afterFullRelease.TotalRefCount == 0
                    && afterFullRelease.FailedCount == 0;
                string failure = success
                    ? string.Empty
                    : "Samples resource test did not release all resources.";

                return new RuntimeVerticalSliceSampleResourceTestResult(
                    success,
                    CreateSummary(success, directReport, afterFullRelease),
                    failure,
                    lines,
                    directReport.PrefabCount,
                    directReport.TextureCount,
                    directReport.AudioClipCount,
                    directReport.InstantiatedPrefabCount,
                    directReport.DestroyedPrefabCount,
                    afterWarmup,
                    afterDirectLoad,
                    afterDirectRelease,
                    afterFullRelease,
                    _provider.LoadCount,
                    _provider.ReleaseCount);
            }
            catch (Exception ex)
            {
                Release();
                return RuntimeVerticalSliceSampleResourceTestResult.Failed(
                    "Samples resource test failed: " + ex.Message,
                    lines);
            }
        }

        public void Release()
        {
            if (_released)
                return;

            DestroyInstances();
            ReleaseDirectHandles();
            ReleaseWarmupGroups();
            _released = true;
        }

        public void Dispose()
        {
            Release();
        }

        private static ResourceCatalogValidationReport ValidateCatalog(ResourceCatalog catalog, MemoryResourceProvider provider)
        {
            ResourceCatalogValidationReport report = ResourceCatalogValidator.Validate(
                catalog,
                new[] { TempImportedResourceCatalog.MemoryProviderId });
            report.Merge(TempImportedResourceCatalog.ValidateCatalogBootstrap(catalog));

            if (catalog == null || provider == null)
                return report;

            for (int i = 0; i < catalog.Entries.Count; i++)
            {
                ResourceCatalogEntry entry = catalog.Entries[i];
                if (entry == null || !string.Equals(entry.ProviderId, TempImportedResourceCatalog.MemoryProviderId, StringComparison.Ordinal))
                    continue;

                if (!provider.CanLoad(entry))
                {
                    report.AddError(
                        "MemoryAddressUnregistered",
                        entry.CreateKey(catalog.PackageId),
                        "Memory provider address is not registered for RuntimeVerticalSlice samples: " + entry.Address + ".");
                }
            }

            return report;
        }

        private List<RuntimeVerticalSliceResourcePreloadReport> PreloadSamples()
        {
            var reports = new List<RuntimeVerticalSliceResourcePreloadReport>(PreloadCases.Length);
            for (int i = 0; i < PreloadCases.Length; i++)
            {
                RuntimeVerticalSliceResourcePreloadCase preload = PreloadCases[i];
                ResourceLoadResult<ResourcePreloadResult> loadResult = _preloadService.PreloadAsync(new ResourcePreloadPlan(
                    preload.GroupId,
                    labels: new[] { preload.Label })).Result;
                if (!loadResult.Success)
                    throw new InvalidOperationException("Preload failed for " + preload.DisplayName + ": " + loadResult.Error);

                ResourcePreloadResult result = loadResult.Value;
                if (result == null)
                    throw new InvalidOperationException("Preload result is missing for " + preload.DisplayName + ".");

                if (result.Handle != null)
                    _warmupGroups.Add(result.Handle);

                if (!result.Success)
                    throw new InvalidOperationException("Preload errors for " + preload.DisplayName + ": " + FormatErrors(result.Errors));
                if (result.RequestedCount != preload.ExpectedCount || result.LoadedCount != preload.ExpectedCount)
                {
                    throw new InvalidOperationException(
                        "Preload count mismatch for " + preload.DisplayName +
                        ": expected=" + preload.ExpectedCount +
                        " requested=" + result.RequestedCount +
                        " loaded=" + result.LoadedCount + ".");
                }

                reports.Add(new RuntimeVerticalSliceResourcePreloadReport(
                    preload.DisplayName,
                    result.RequestedCount,
                    result.LoadedCount,
                    result.FailedCount));
            }

            return reports;
        }

        private RuntimeVerticalSliceDirectResourceReport LoadDirectSamples()
        {
            ResourceKeyConfigProfile profile = ResourceKeyConfigProfile.CreateSample();
            int instantiated = 0;

            ResourceHandle<GameObject> weapon = Load<GameObject>("Katana prefab", profile.WeaponPrefab);
            _prefabHandles.Add(weapon);
            instantiated += InstantiatePrefab("Katana", weapon);

            for (int i = 0; i < profile.StatusAuraPrefabs.Count; i++)
            {
                ResourceHandle<GameObject> aura = Load<GameObject>("StatusAura prefab " + i, profile.StatusAuraPrefabs[i]);
                _prefabHandles.Add(aura);
                instantiated += InstantiatePrefab("StatusAura" + i, aura);
            }

            _textureHandles.Add(Load<Texture2D>("StartScreen button normal", profile.StartScreenButtonNormalTexture));
            _textureHandles.Add(Load<Texture2D>("StartScreen button hover", profile.StartScreenButtonHoverTexture));
            _textureHandles.Add(Load<Texture2D>("StartScreen separator", profile.StartScreenSeparatorTexture));
            _textureHandles.Add(Load<Texture2D>("StartScreen archive icon", profile.StartScreenArchiveIconTexture));
            _textureHandles.Add(Load<Texture2D>("StartScreen continue icon", profile.StartScreenContinueIconTexture));
            _textureHandles.Add(Load<Texture2D>("StartScreen exit icon", profile.StartScreenExitIconTexture));
            _textureHandles.Add(Load<Texture2D>("StartScreen settings icon", profile.StartScreenSettingsIconTexture));

            for (int i = 0; i < profile.MagicEffectAudioClips.Count; i++)
                _audioHandles.Add(Load<AudioClip>("MagicEffects AudioClip " + i, profile.MagicEffectAudioClips[i]));

            return new RuntimeVerticalSliceDirectResourceReport(
                _prefabHandles.Count,
                _textureHandles.Count,
                _audioHandles.Count,
                instantiated,
                0);
        }

        private ResourceHandle<T> Load<T>(string label, ResourceKey key)
        {
            ResourceLoadResult<ResourceHandle<T>> result = _manager.Load<T>(key);
            if (!result.Success)
                throw new InvalidOperationException("Direct load failed for " + label + ": " + result.Error);
            if (result.Value == null || result.Value.Value == null)
                throw new InvalidOperationException("Direct load returned null for " + label + ".");

            return result.Value;
        }

        private int InstantiatePrefab(string label, ResourceHandle<GameObject> handle)
        {
            if (handle == null || handle.Value == null)
                return 0;

            GameObject instance = UnityEngine.Object.Instantiate(handle.Value);
            instance.name = "RuntimeVerticalSliceResourceTest_" + label;
            _instances.Add(instance);
            return 1;
        }

        private int DestroyInstances()
        {
            int destroyed = 0;
            for (int i = _instances.Count - 1; i >= 0; i--)
            {
                GameObject instance = _instances[i];
                if (instance == null)
                    continue;

                if (Application.isPlaying)
                    UnityEngine.Object.Destroy(instance);
                else
                    UnityEngine.Object.DestroyImmediate(instance);
                destroyed++;
            }

            _instances.Clear();
            return destroyed;
        }

        private void ReleaseDirectHandles()
        {
            if (_manager == null)
                return;

            ReleaseAll(_audioHandles);
            ReleaseAll(_textureHandles);
            ReleaseAll(_prefabHandles);
        }

        private void ReleaseAll<T>(List<ResourceHandle<T>> handles)
        {
            for (int i = handles.Count - 1; i >= 0; i--)
                _manager.Release(handles[i]);

            handles.Clear();
        }

        private void ReleaseWarmupGroups()
        {
            if (_preloadService == null)
                return;

            for (int i = _warmupGroups.Count - 1; i >= 0; i--)
                _preloadService.ReleaseGroup(_warmupGroups[i]);

            _warmupGroups.Clear();
        }

        private static string CreateWarmupLine(IReadOnlyList<RuntimeVerticalSliceResourcePreloadReport> reports)
        {
            var parts = new List<string>(reports.Count);
            for (int i = 0; i < reports.Count; i++)
            {
                RuntimeVerticalSliceResourcePreloadReport report = reports[i];
                parts.Add(report.DisplayName + " " + report.LoadedCount + "/" + report.RequestedCount);
            }

            return "Samples warmup: " + string.Join(", ", parts.ToArray());
        }

        private static string CreateDirectLine(RuntimeVerticalSliceDirectResourceReport report)
        {
            return "Samples direct: Katana=1, StatusAura prefabs=4, StartScreen textures=" + report.TextureCount +
                ", MagicEffects AudioClips=" + report.AudioClipCount +
                ", prefab instances=" + report.InstantiatedPrefabCount + "/" + report.PrefabCount +
                ", destroyed=" + report.DestroyedPrefabCount;
        }

        private static RuntimeVerticalSliceDirectResourceReport CreateDestroyedReport(
            RuntimeVerticalSliceDirectResourceReport report,
            int destroyedCount)
        {
            return new RuntimeVerticalSliceDirectResourceReport(
                report.PrefabCount,
                report.TextureCount,
                report.AudioClipCount,
                report.InstantiatedPrefabCount,
                destroyedCount);
        }

        private static string CreateDiagnosticsLine(
            ResourceDebugSnapshot afterWarmup,
            ResourceDebugSnapshot afterDirectLoad,
            ResourceDebugSnapshot afterDirectRelease,
            ResourceDebugSnapshot afterFullRelease)
        {
            return "Samples diagnostics: warmup " + FormatSnapshot(afterWarmup) +
                "; direct " + FormatSnapshot(afterDirectLoad) +
                "; directRelease " + FormatSnapshot(afterDirectRelease) +
                "; fullRelease " + FormatSnapshot(afterFullRelease);
        }

        private static string FormatSnapshot(ResourceDebugSnapshot snapshot)
        {
            if (snapshot == null)
                return "loaded=?, refs=?";

            return "loaded=" + snapshot.LoadedCount +
                " refs=" + snapshot.TotalRefCount +
                " failed=" + snapshot.FailedCount;
        }

        private static string CreateSummary(
            bool success,
            RuntimeVerticalSliceDirectResourceReport direct,
            ResourceDebugSnapshot afterFullRelease)
        {
            string status = success ? "ok" : "failed";
            return "Samples resources " + status +
                ": warmup 16/7/9/4/4, direct P" + direct.PrefabCount +
                "/T" + direct.TextureCount +
                "/A" + direct.AudioClipCount +
                ", release loaded=" + afterFullRelease.LoadedCount +
                " refs=" + afterFullRelease.TotalRefCount;
        }

        private static string FormatValidationReport(ResourceCatalogValidationReport report)
        {
            if (report == null || report.Issues.Count == 0)
                return "no issues";

            var parts = new List<string>(report.Issues.Count);
            for (int i = 0; i < report.Issues.Count; i++)
            {
                ResourceCatalogValidationIssue issue = report.Issues[i];
                parts.Add(issue.Code + " " + issue.Key + " " + issue.Message);
            }

            return string.Join("; ", parts.ToArray());
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
    }

    public sealed class RuntimeVerticalSliceSampleResourceTestResult
    {
        public RuntimeVerticalSliceSampleResourceTestResult(
            bool success,
            string summary,
            string failureMessage,
            IReadOnlyList<string> logLines,
            int directPrefabCount,
            int directTextureCount,
            int directAudioClipCount,
            int instantiatedPrefabCount,
            int destroyedPrefabCount,
            ResourceDebugSnapshot afterWarmupSnapshot,
            ResourceDebugSnapshot afterDirectLoadSnapshot,
            ResourceDebugSnapshot afterDirectReleaseSnapshot,
            ResourceDebugSnapshot afterFullReleaseSnapshot,
            int providerLoadCount,
            int providerReleaseCount)
        {
            Success = success;
            Summary = summary ?? string.Empty;
            FailureMessage = failureMessage ?? string.Empty;
            LogLines = logLines != null ? new List<string>(logLines) : new List<string>();
            DirectPrefabCount = directPrefabCount;
            DirectTextureCount = directTextureCount;
            DirectAudioClipCount = directAudioClipCount;
            InstantiatedPrefabCount = instantiatedPrefabCount;
            DestroyedPrefabCount = destroyedPrefabCount;
            AfterWarmupSnapshot = afterWarmupSnapshot;
            AfterDirectLoadSnapshot = afterDirectLoadSnapshot;
            AfterDirectReleaseSnapshot = afterDirectReleaseSnapshot;
            AfterFullReleaseSnapshot = afterFullReleaseSnapshot;
            ProviderLoadCount = providerLoadCount;
            ProviderReleaseCount = providerReleaseCount;
        }

        public bool Success { get; }
        public string Summary { get; }
        public string FailureMessage { get; }
        public IReadOnlyList<string> LogLines { get; }
        public int DirectPrefabCount { get; }
        public int DirectTextureCount { get; }
        public int DirectAudioClipCount { get; }
        public int InstantiatedPrefabCount { get; }
        public int DestroyedPrefabCount { get; }
        public ResourceDebugSnapshot AfterWarmupSnapshot { get; }
        public ResourceDebugSnapshot AfterDirectLoadSnapshot { get; }
        public ResourceDebugSnapshot AfterDirectReleaseSnapshot { get; }
        public ResourceDebugSnapshot AfterFullReleaseSnapshot { get; }
        public int ProviderLoadCount { get; }
        public int ProviderReleaseCount { get; }

        public static RuntimeVerticalSliceSampleResourceTestResult Failed(string message, IReadOnlyList<string> lines)
        {
            return new RuntimeVerticalSliceSampleResourceTestResult(
                false,
                "Samples resources failed",
                message,
                lines,
                0,
                0,
                0,
                0,
                0,
                null,
                null,
                null,
                null,
                0,
                0);
        }
    }

    internal readonly struct RuntimeVerticalSliceResourcePreloadCase
    {
        public RuntimeVerticalSliceResourcePreloadCase(string groupId, string displayName, string label, int expectedCount)
        {
            GroupId = groupId;
            DisplayName = displayName;
            Label = label;
            ExpectedCount = expectedCount;
        }

        public string GroupId { get; }
        public string DisplayName { get; }
        public string Label { get; }
        public int ExpectedCount { get; }
    }

    internal readonly struct RuntimeVerticalSliceResourcePreloadReport
    {
        public RuntimeVerticalSliceResourcePreloadReport(string displayName, int requestedCount, int loadedCount, int failedCount)
        {
            DisplayName = displayName;
            RequestedCount = requestedCount;
            LoadedCount = loadedCount;
            FailedCount = failedCount;
        }

        public string DisplayName { get; }
        public int RequestedCount { get; }
        public int LoadedCount { get; }
        public int FailedCount { get; }
    }

    internal readonly struct RuntimeVerticalSliceDirectResourceReport
    {
        public RuntimeVerticalSliceDirectResourceReport(
            int prefabCount,
            int textureCount,
            int audioClipCount,
            int instantiatedPrefabCount,
            int destroyedPrefabCount)
        {
            PrefabCount = prefabCount;
            TextureCount = textureCount;
            AudioClipCount = audioClipCount;
            InstantiatedPrefabCount = instantiatedPrefabCount;
            DestroyedPrefabCount = destroyedPrefabCount;
        }

        public int PrefabCount { get; }
        public int TextureCount { get; }
        public int AudioClipCount { get; }
        public int InstantiatedPrefabCount { get; }
        public int DestroyedPrefabCount { get; }
    }
}
