using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;
using UnityEditor;
using UnityEngine;

namespace MxFramework.Editor
{
    [Serializable]
    internal sealed class VfxSourceLibraryView
    {
        [JsonProperty("schemaVersion")] public string SchemaVersion = "mx.vfx.source-library.v1";
        [JsonProperty("sourceLibraryId")] public string SourceLibraryId = "source.piloto.studio";
        [JsonProperty("sourceRoot")] public string SourceRoot = VfxSourceLibraryEditorService.SourceRoot;
        [JsonProperty("usagePolicy")] public string UsagePolicy = "LicensedReusable";
        [JsonProperty("licenseId")] public string LicenseId = VfxSourceLibraryEditorService.LicenseId;
        [JsonProperty("publicRedistributionAllowed")] public bool PublicRedistributionAllowed = true;
        [JsonProperty("scannerId")] public string ScannerId = "mx.vfx.source-library.unity";
        [JsonProperty("scannerVersion")] public string ScannerVersion = "1";
        [JsonProperty("deepScanComplete")] public bool DeepScanComplete;
        [JsonProperty("prefabCount")] public int PrefabCount;
        [JsonProperty("particleSystemCount")] public int ParticleSystemCount;
        [JsonProperty("supportAssetCount")] public int SupportAssetCount;
        [JsonProperty("packages")] public List<VfxSourcePackageView> Packages = new List<VfxSourcePackageView>();
        [JsonProperty("effects")] public List<VfxSourceEffectView> Effects = new List<VfxSourceEffectView>();
        [JsonProperty("components")] public List<VfxSourceComponentView> Components = new List<VfxSourceComponentView>();
        [JsonProperty("supportAssets")] public List<VfxSourceSupportAssetView> SupportAssets = new List<VfxSourceSupportAssetView>();
        [JsonProperty("duplicateClusters")] public List<VfxSourceDuplicateClusterView> DuplicateClusters = new List<VfxSourceDuplicateClusterView>();
        [JsonProperty("indexHash")] public string IndexHash = string.Empty;
    }

    [Serializable]
    internal sealed class VfxSourcePackageView
    {
        [JsonProperty("sourcePackageId")] public string SourcePackageId = string.Empty;
        [JsonProperty("displayName")] public string DisplayName = string.Empty;
        [JsonProperty("rootPath")] public string RootPath = string.Empty;
        [JsonProperty("licenseId")] public string LicenseId = string.Empty;
        [JsonProperty("publicRedistributionAllowed")] public bool PublicRedistributionAllowed;
        [JsonProperty("prefabCount")] public int PrefabCount;
        [JsonProperty("particleSystemCount")] public int ParticleSystemCount;
        [JsonProperty("contentHash")] public string ContentHash = string.Empty;
    }

    [Serializable]
    internal sealed class VfxSourceEffectView
    {
        [JsonProperty("sourceEffectId")] public string SourceEffectId = string.Empty;
        [JsonProperty("sourcePackageId")] public string SourcePackageId = string.Empty;
        [JsonProperty("displayName")] public string DisplayName = string.Empty;
        [JsonProperty("prefabGuid")] public string PrefabGuid = string.Empty;
        [JsonProperty("prefabPath")] public string PrefabPath = string.Empty;
        [JsonProperty("contentHash")] public string ContentHash = string.Empty;
        [JsonProperty("dependencyHash")] public string DependencyHash = string.Empty;
        [JsonProperty("family")] public string Family = string.Empty;
        [JsonProperty("theme")] public string Theme = string.Empty;
        [JsonProperty("qualityTier")] public string QualityTier = string.Empty;
        [JsonProperty("roles")] public List<string> Roles = new List<string>();
        [JsonProperty("particleSystemCount")] public int ParticleSystemCount;
        [JsonProperty("componentIds")] public List<string> ComponentIds = new List<string>();
    }

    [Serializable]
    internal sealed class VfxSourceComponentView
    {
        [JsonProperty("componentId")] public string ComponentId = string.Empty;
        [JsonProperty("componentKind")] public string ComponentKind = "ParticleSystemLayer";
        [JsonProperty("sourcePackageId")] public string SourcePackageId = string.Empty;
        [JsonProperty("sourceEffectId")] public string SourceEffectId = string.Empty;
        [JsonProperty("prefabGuid")] public string PrefabGuid = string.Empty;
        [JsonProperty("prefabPath")] public string PrefabPath = string.Empty;
        [JsonProperty("localFileId")] public long LocalFileId;
        [JsonProperty("objectPath")] public string ObjectPath = string.Empty;
        [JsonProperty("displayName")] public string DisplayName = string.Empty;
        [JsonProperty("backend")] public string Backend = "ParticleSystem";
        [JsonProperty("roles")] public List<string> Roles = new List<string>();
        [JsonProperty("theme")] public string Theme = string.Empty;
        [JsonProperty("shape")] public string Shape = string.Empty;
        [JsonProperty("anchor")] public string Anchor = string.Empty;
        [JsonProperty("qualityTier")] public string QualityTier = string.Empty;
        [JsonProperty("simulationSpace")] public string SimulationSpace = string.Empty;
        [JsonProperty("renderMode")] public string RenderMode = string.Empty;
        [JsonProperty("durationSeconds")] public float DurationSeconds;
        [JsonProperty("loop")] public bool Loop;
        [JsonProperty("maxParticles")] public int MaxParticles;
        [JsonProperty("materialGuids")] public List<string> MaterialGuids = new List<string>();
        [JsonProperty("meshGuids")] public List<string> MeshGuids = new List<string>();
        [JsonProperty("memberComponentIds")] public List<string> MemberComponentIds = new List<string>();
        [JsonProperty("technicalFingerprint")] public string TechnicalFingerprint = string.Empty;
        [JsonProperty("creativeFingerprint")] public string CreativeFingerprint = string.Empty;
        [JsonProperty("sourceContentHash")] public string SourceContentHash = string.Empty;
        [JsonProperty("dependencyHash")] public string DependencyHash = string.Empty;
        [JsonProperty("existingPrimitiveId")] public string ExistingPrimitiveId = string.Empty;
        [JsonProperty("preferred")] public bool Preferred;
        [JsonProperty("deprecated")] public bool Deprecated;
        [JsonProperty("note")] public string Note = string.Empty;
    }

    [Serializable]
    internal sealed class VfxSourceSupportAssetView
    {
        [JsonProperty("assetGuid")] public string AssetGuid = string.Empty;
        [JsonProperty("assetPath")] public string AssetPath = string.Empty;
        [JsonProperty("assetKind")] public string AssetKind = string.Empty;
        [JsonProperty("sourcePackageId")] public string SourcePackageId = string.Empty;
        [JsonProperty("contentHash")] public string ContentHash = string.Empty;
        [JsonProperty("consumerComponentIds")] public List<string> ConsumerComponentIds = new List<string>();
    }

    [Serializable]
    internal sealed class VfxSourceDuplicateClusterView
    {
        [JsonProperty("clusterId")] public string ClusterId = string.Empty;
        [JsonProperty("clusterKind")] public string ClusterKind = string.Empty;
        [JsonProperty("fingerprint")] public string Fingerprint = string.Empty;
        [JsonProperty("componentIds")] public List<string> ComponentIds = new List<string>();
        [JsonProperty("preferredComponentId")] public string PreferredComponentId = string.Empty;
    }

    [Serializable]
    internal sealed class VfxSourceCurationView
    {
        internal const string AutomaticCuratedPaletteMode = "AutomaticCurated";
        internal const string DraftLocalOnlyPaletteMode = "DraftLocalOnly";
        internal const string ExplicitAuthoredPaletteMode = "ExplicitAuthored";

        [JsonProperty("schemaVersion")] public string SchemaVersion = "mx.vfx.source-curation.v2";
        [JsonProperty("sourceLibraryId")] public string SourceLibraryId = "source.piloto.studio";
        [JsonProperty("paletteMode")] public string PaletteMode = AutomaticCuratedPaletteMode;
        [JsonProperty("entries")] public List<VfxSourceCurationEntryView> Entries = new List<VfxSourceCurationEntryView>();
        [JsonProperty("elementOverrides")] public List<VfxSourceElementCurationOverrideView> ElementOverrides = new List<VfxSourceElementCurationOverrideView>();
        [JsonProperty("elements")] public List<VfxSourceElementView> Elements = new List<VfxSourceElementView>();
        [JsonProperty("sourceIndexHash")] public string SourceIndexHash = string.Empty;
        [JsonProperty("curationHash")] public string CurationHash = string.Empty;

        [JsonIgnore]
        public bool DraftLocalOnly => string.Equals(PaletteMode, DraftLocalOnlyPaletteMode, StringComparison.Ordinal);
        [JsonIgnore] public bool ExplicitAuthored => string.Equals(PaletteMode, ExplicitAuthoredPaletteMode, StringComparison.Ordinal);
        [JsonIgnore] public bool AutomaticCurated => !DraftLocalOnly && !ExplicitAuthored;
    }

    [Serializable]
    internal sealed class VfxSourceElementCurationOverrideView
    {
        [JsonProperty("elementId")] public string ElementId = string.Empty;
        [JsonProperty("displayName")] public string DisplayName = string.Empty;
    }

    [Serializable]
    internal sealed class VfxSourceCurationEntryView
    {
        [JsonProperty("componentId")] public string ComponentId = string.Empty;
        [JsonProperty("displayName")] public string DisplayName = string.Empty;
        [JsonProperty("roles")] public List<string> Roles = new List<string>();
        [JsonProperty("family")] public string Family = string.Empty;
        [JsonProperty("theme")] public string Theme = string.Empty;
        [JsonProperty("shape")] public string Shape = string.Empty;
        [JsonProperty("anchor")] public string Anchor = string.Empty;
        [JsonProperty("timingPhase")] public string TimingPhase = string.Empty;
        [JsonProperty("useCases")] public List<string> UseCases = new List<string>();
        [JsonProperty("motions")] public List<string> Motions = new List<string>();
        [JsonProperty("qualityTier")] public string QualityTier = string.Empty;
        [JsonProperty("preferred")] public bool Preferred;
        [JsonProperty("deprecated")] public bool Deprecated;
        [JsonProperty("existingPrimitiveId")] public string ExistingPrimitiveId = string.Empty;
        [JsonProperty("note")] public string Note = string.Empty;
    }

    internal sealed class VfxSourceLibraryEditorStatus
    {
        public bool Available;
        public string Message = string.Empty;
        public VfxSourceLibraryView Index;
    }

    /// <summary>Builds the machine-local deep index for the authorized Piloto source library.</summary>
    /// <remarks>Editor-only, AllocByDesign. It reads source assets and writes only a rebuildable machine-local Library index.</remarks>
    internal sealed class VfxSourceLibraryEditorService
    {
        internal delegate bool SealIndexFile(string inputPath, string outputPath, out string message);

        internal const string SourceRoot = "Assets/Piloto Studio";
        internal const string IndexPath = "Library/MxFramework/VfxSourceLibrary/index.json";
        internal const string LegacyIndexPath = "Temp/MxFramework/VfxSourceLibrary/index.json";
        internal const string CurationPath = "Assets/Config/MxFramework/Vfx/vfx_source_curation.json";
        internal const string LicenseId = "operator-confirmed-public-redistribution-2026-07-14";
        private const string CliProject = "Tools/MxFramework.Authoring/src/MxFramework.Authoring.Cli/MxFramework.Authoring.Cli.csproj";
        private const int CliTimeoutMilliseconds = 120000;
        private static VfxSourceLibraryView s_cachedIndex;
        private static long s_cachedIndexLength = -1L;
        private static long s_cachedIndexWriteTicks = -1L;

        public VfxSourceLibraryEditorStatus Load()
        {
            if (!File.Exists(IndexPath) && File.Exists(LegacyIndexPath))
            {
                try
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(IndexPath) ?? "Library/MxFramework/VfxSourceLibrary");
                    File.Copy(LegacyIndexPath, IndexPath, true);
                }
                catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException)
                {
                    return new VfxSourceLibraryEditorStatus { Message = "迁移原材料索引失败：" + exception.Message };
                }
            }
            if (!File.Exists(IndexPath)) return new VfxSourceLibraryEditorStatus { Message = "尚未建立 Piloto 原材料深度索引。" };
            try
            {
                var file = new FileInfo(IndexPath);
                VfxSourceLibraryView index;
                if (s_cachedIndex != null && s_cachedIndexLength == file.Length && s_cachedIndexWriteTicks == file.LastWriteTimeUtc.Ticks)
                {
                    index = s_cachedIndex;
                }
                else
                {
                    index = JsonConvert.DeserializeObject<VfxSourceLibraryView>(File.ReadAllText(IndexPath));
                    s_cachedIndex = index;
                    s_cachedIndexLength = file.Length;
                    s_cachedIndexWriteTicks = file.LastWriteTimeUtc.Ticks;
                }
                bool valid = index != null && index.DeepScanComplete && index.SchemaVersion == "mx.vfx.source-library.v1" && !string.IsNullOrWhiteSpace(index.IndexHash);
                return new VfxSourceLibraryEditorStatus { Available = valid, Index = index, Message = valid ? "原材料索引可用" : "原材料索引不完整，请重新扫描。" };
            }
            catch (Exception exception) when (exception is IOException || exception is JsonException)
            {
                return new VfxSourceLibraryEditorStatus { Message = "读取原材料索引失败：" + exception.Message };
            }
        }

        public bool Scan(out string message)
        {
            if (!AssetDatabase.IsValidFolder(SourceRoot)) { message = "未找到只读源目录：" + SourceRoot; return false; }
            VfxSourceCurationView curation = LoadCuration();
            var curationById = curation.Entries.Where(value => value != null && !string.IsNullOrWhiteSpace(value.ComponentId)).ToDictionary(value => value.ComponentId, StringComparer.Ordinal);
            string[] prefabGuids = AssetDatabase.FindAssets("t:Prefab", new[] { SourceRoot }).OrderBy(value => value, StringComparer.Ordinal).ToArray();
            var index = new VfxSourceLibraryView();
            var supportByGuid = new Dictionary<string, VfxSourceSupportAssetView>(StringComparer.Ordinal);
            try
            {
                for (int prefabIndex = 0; prefabIndex < prefabGuids.Length; prefabIndex++)
                {
                    string prefabGuid = prefabGuids[prefabIndex];
                    string prefabPath = AssetDatabase.GUIDToAssetPath(prefabGuid);
                    if (EditorUtility.DisplayCancelableProgressBar("扫描 Piloto VFX 原材料", prefabPath, prefabGuids.Length == 0 ? 1f : prefabIndex / (float)prefabGuids.Length))
                    { message = "用户取消了原材料扫描。"; return false; }
                    GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
                    if (prefab == null) continue;
                    string packageId = PackageId(prefabPath);
                    string sourceHash = ContentHash(prefabPath);
                    string effectId = "source.effect." + PackageToken(packageId) + "." + prefabGuid;
                    ParticleSystem[] systems = prefab.GetComponentsInChildren<ParticleSystem>(true);
                    var effect = new VfxSourceEffectView
                    {
                        SourceEffectId = effectId,
                        SourcePackageId = packageId,
                        DisplayName = prefab.name,
                        PrefabGuid = prefabGuid,
                        PrefabPath = prefabPath,
                        ContentHash = sourceHash,
                        DependencyHash = AssetDatabase.GetAssetDependencyHash(prefabPath).ToString(),
                        Family = InferFamily(prefabPath, prefab.name),
                        Theme = InferTheme(prefabPath + "/" + prefab.name),
                        QualityTier = QualityTier(systems.Length),
                        ParticleSystemCount = systems.Length
                    };
                    for (int systemIndex = 0; systemIndex < systems.Length; systemIndex++)
                    {
                        VfxSourceComponentView component = BuildComponent(systems[systemIndex], prefab, effect, sourceHash, curationById);
                        if (component == null) continue;
                        index.Components.Add(component);
                        effect.ComponentIds.Add(component.ComponentId);
                        effect.Roles.AddRange(component.Roles);
                        CollectSupportAssets(component, supportByGuid);
                    }
                    effect.ComponentIds.Sort(StringComparer.Ordinal);
                    effect.Roles = effect.Roles.Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal).ToList();
                    index.Effects.Add(effect);
                }
                index.SupportAssets = supportByGuid.Values.OrderBy(value => value.AssetGuid, StringComparer.Ordinal).ToList();
                index.Packages = BuildPackages(index.Effects, index.Components);
                index.DuplicateClusters = BuildDuplicateClusters(index.Components);
                index.DeepScanComplete = true;
                index.PrefabCount = index.Effects.Count;
                index.ParticleSystemCount = index.Components.Count;
                index.SupportAssetCount = index.SupportAssets.Count;
                Directory.CreateDirectory(Path.GetDirectoryName(IndexPath) ?? "Library/MxFramework/VfxSourceLibrary");
                File.WriteAllText(IndexPath, JsonConvert.SerializeObject(index, Formatting.Indented), new UTF8Encoding(false));
                if (!RunCli("vfx source seal-index --input " + Quote(IndexPath) + " --output " + Quote(IndexPath), out message)) return false;
                VfxSourceLibraryEditorStatus loaded = Load();
                message = loaded.Available
                    ? $"已索引 {loaded.Index.PrefabCount} 个 Prefab、{loaded.Index.ParticleSystemCount} 个 ParticleSystem、{loaded.Index.SupportAssetCount} 个支持资产。"
                    : loaded.Message;
                return loaded.Available;
            }
            catch (Exception exception)
            {
                UnityEngine.Debug.LogException(exception);
                message = "原材料深度扫描失败：" + exception.Message;
                return false;
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        }

        public static void RecordPrimitiveMapping(IEnumerable<VfxSourceComponentView> components, string primitiveId)
        {
            VfxSourceCurationView curation = LoadCuration();
            foreach (VfxSourceComponentView component in components ?? Array.Empty<VfxSourceComponentView>())
            {
                VfxSourceCurationEntryView entry = curation.Entries.FirstOrDefault(value => value.ComponentId == component.ComponentId);
                if (entry == null) { entry = new VfxSourceCurationEntryView { ComponentId = component.ComponentId }; curation.Entries.Add(entry); }
                entry.ExistingPrimitiveId = primitiveId ?? string.Empty;
                component.ExistingPrimitiveId = primitiveId ?? string.Empty;
            }
            curation.Entries = curation.Entries.OrderBy(value => value.ComponentId, StringComparer.Ordinal).ToList();
            File.WriteAllText(CurationPath, JsonConvert.SerializeObject(curation, Formatting.Indented) + Environment.NewLine, new UTF8Encoding(false));
            AssetDatabase.ImportAsset(CurationPath, ImportAssetOptions.ForceSynchronousImport);
        }

        public bool RefreshCuration(out string message)
        {
            VfxSourceLibraryEditorStatus status = Load();
            if (!status.Available) { message = status.Message; return false; }
            VfxSourceCurationView curation = LoadCuration();
            if (!RefreshCurationTransactional(status.Index, curation, IndexPath, SealIndexWithCli, out VfxSourceLibraryView refreshedIndex, out message))
                return false;
            UpdateIndexCache(refreshedIndex, IndexPath);
            int mappings = refreshedIndex.Components.Count(value => !string.IsNullOrWhiteSpace(value.ExistingPrimitiveId));
            message = $"已应用 {mappings} 条现有 Primitive 映射并重新封存索引。";
            return true;
        }

        internal static bool RefreshCurationTransactional(
            VfxSourceLibraryView currentIndex,
            VfxSourceCurationView curation,
            string authoritativePath,
            SealIndexFile sealIndex,
            out VfxSourceLibraryView committedIndex,
            out string message)
        {
            return ResealIndexTransactional(
                currentIndex,
                authoritativePath,
                workingIndex =>
                {
                    Dictionary<string, VfxSourceCurationEntryView> byId = curation.Entries.Where(value => value != null && !string.IsNullOrWhiteSpace(value.ComponentId)).ToDictionary(value => value.ComponentId, StringComparer.Ordinal);
                    for (int i = 0; i < workingIndex.Components.Count; i++)
                    {
                        VfxSourceComponentView component = workingIndex.Components[i];
                        component.Preferred = false; component.Deprecated = false; component.ExistingPrimitiveId = string.Empty; component.Note = string.Empty;
                        if (byId.TryGetValue(component.ComponentId, out VfxSourceCurationEntryView curated)) ApplyCuration(component, curated);
                    }
                },
                sealIndex,
                out committedIndex,
                out message);
        }

        internal static bool ResealIndexTransactional(
            VfxSourceLibraryView currentIndex,
            string authoritativePath,
            Action<VfxSourceLibraryView> applyChanges,
            SealIndexFile sealIndex,
            out VfxSourceLibraryView committedIndex,
            out string message)
        {
            committedIndex = null;
            if (currentIndex == null || string.IsNullOrWhiteSpace(authoritativePath) || applyChanges == null || sealIndex == null)
            {
                message = "重新封存索引缺少有效输入。";
                return false;
            }

            VfxSourceLibraryView workingIndex = JsonConvert.DeserializeObject<VfxSourceLibraryView>(
                JsonConvert.SerializeObject(currentIndex, Formatting.None));
            if (workingIndex == null)
            {
                message = "无法创建原材料索引工作副本。";
                return false;
            }
            applyChanges(workingIndex);
            workingIndex.IndexHash = string.Empty;

            string token = Guid.NewGuid().ToString("N");
            string candidatePath = authoritativePath + ".refresh-" + token + ".json";
            string sealedPath = authoritativePath + ".refresh-" + token + ".sealed.json";
            string backupPath = authoritativePath + ".refresh-" + token + ".backup.json";
            bool replaced = false;
            try
            {
                string directory = Path.GetDirectoryName(authoritativePath);
                if (!string.IsNullOrWhiteSpace(directory)) Directory.CreateDirectory(directory);
                File.WriteAllText(candidatePath, JsonConvert.SerializeObject(workingIndex, Formatting.Indented), new UTF8Encoding(false));
                if (!sealIndex(candidatePath, sealedPath, out message)) return false;
                VfxSourceLibraryView sealedIndex = File.Exists(sealedPath)
                    ? JsonConvert.DeserializeObject<VfxSourceLibraryView>(File.ReadAllText(sealedPath))
                    : null;
                if (!IsBasicallyValidSealedIndex(sealedIndex, currentIndex.Components.Count))
                {
                    message = "CLI 返回的重新封存索引无效，权威索引保持不变。";
                    return false;
                }
                if (!File.Exists(authoritativePath))
                {
                    message = "重新封存期间权威索引已丢失，未提交候选索引。";
                    return false;
                }

                File.Replace(sealedPath, authoritativePath, backupPath, true);
                replaced = true;
                VfxSourceLibraryView reloaded = JsonConvert.DeserializeObject<VfxSourceLibraryView>(File.ReadAllText(authoritativePath));
                if (!IsBasicallyValidSealedIndex(reloaded, currentIndex.Components.Count))
                    throw new InvalidDataException("提交后的权威索引未通过完整性复核。");
                committedIndex = reloaded;
                message = string.Empty;
                return true;
            }
            catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException || exception is JsonException)
            {
                if (replaced && File.Exists(backupPath))
                {
                    try { File.Replace(backupPath, authoritativePath, null, true); }
                    catch (Exception restoreException) when (restoreException is IOException || restoreException is UnauthorizedAccessException)
                    {
                        message = "重新封存索引失败且恢复旧索引失败：" + restoreException.Message;
                        return false;
                    }
                }
                message = "重新封存索引失败，权威索引保持不变：" + exception.Message;
                return false;
            }
            finally
            {
                TryDelete(candidatePath);
                TryDelete(sealedPath);
                TryDelete(backupPath);
            }
        }

        private static bool SealIndexWithCli(string inputPath, string outputPath, out string message)
        {
            return RunCli("vfx source seal-index --input " + Quote(inputPath) + " --output " + Quote(outputPath), out message);
        }

        private static bool IsBasicallyValidSealedIndex(VfxSourceLibraryView index, int expectedComponentCount)
        {
            return index != null
                && index.SchemaVersion == "mx.vfx.source-library.v1"
                && index.DeepScanComplete
                && !string.IsNullOrWhiteSpace(index.IndexHash)
                && index.Components != null
                && index.Components.Count == expectedComponentCount;
        }

        private static void TryDelete(string path)
        {
            try { if (File.Exists(path)) File.Delete(path); }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }

        private static void UpdateIndexCache(VfxSourceLibraryView index, string path)
        {
            var file = new FileInfo(path);
            s_cachedIndex = index;
            s_cachedIndexLength = file.Length;
            s_cachedIndexWriteTicks = file.LastWriteTimeUtc.Ticks;
        }

        /// <summary>Writes one component-level semantic override and preserves extraction mappings.</summary>
        /// <remarks>Editor-only, AllocByDesign. Writes only the curation JSON; Piloto source assets remain read-only.</remarks>
        public bool SaveCurationOverride(
            string componentId,
            string elementId,
            string elementDisplayName,
            string componentDisplayName,
            IReadOnlyList<string> roles,
            string family,
            string theme,
            string shape,
            string anchor,
            string timingPhase,
            IReadOnlyList<string> useCases,
            IReadOnlyList<string> motions,
            string qualityTier,
            bool preferred,
            bool deprecated,
            string note,
            out string message)
        {
            VfxSourceLibraryEditorStatus status = Load();
            if (!status.Available)
            {
                message = status.Message;
                return false;
            }
            if (string.IsNullOrWhiteSpace(componentId)
                || !status.Index.Components.Any(value => string.Equals(value.ComponentId, componentId, StringComparison.Ordinal)))
            {
                message = "找不到待修正的 Source Component。";
                return false;
            }

            VfxSourceCurationView curation = LoadCuration();
            curation.ElementOverrides ??= new List<VfxSourceElementCurationOverrideView>();
            VfxSourceCurationEntryView entry = curation.Entries.FirstOrDefault(value => string.Equals(value.ComponentId, componentId, StringComparison.Ordinal));
            if (entry == null)
            {
                entry = new VfxSourceCurationEntryView { ComponentId = componentId };
                curation.Entries.Add(entry);
            }
            entry.DisplayName = componentDisplayName?.Trim() ?? string.Empty;
            entry.Roles = (roles ?? Array.Empty<string>()).Where(value => !string.IsNullOrWhiteSpace(value))
                .Select(value => value.Trim().ToLowerInvariant()).Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal).ToList();
            entry.Family = family?.Trim() ?? string.Empty;
            entry.Theme = theme?.Trim() ?? string.Empty;
            entry.Shape = shape?.Trim() ?? string.Empty;
            entry.Anchor = anchor?.Trim() ?? string.Empty;
            entry.TimingPhase = timingPhase?.Trim() ?? string.Empty;
            entry.UseCases = NormalizeControlledValues(useCases);
            entry.Motions = NormalizeControlledValues(motions);
            entry.QualityTier = qualityTier?.Trim() ?? string.Empty;
            entry.Preferred = preferred;
            entry.Deprecated = deprecated;
            entry.Note = note?.Trim() ?? string.Empty;
            VfxSourceElementCurationOverrideView elementOverride = curation.ElementOverrides.FirstOrDefault(value => string.Equals(value.ElementId, elementId, StringComparison.Ordinal));
            if (elementOverride == null)
            {
                elementOverride = new VfxSourceElementCurationOverrideView { ElementId = elementId };
                curation.ElementOverrides.Add(elementOverride);
            }
            elementOverride.DisplayName = elementDisplayName?.Trim() ?? string.Empty;
            curation.Entries = curation.Entries.OrderBy(value => value.ComponentId, StringComparer.Ordinal).ToList();
            curation.CurationHash = string.Empty;
            curation.CurationHash = VfxSourceElementCurationService.Hash(JsonConvert.SerializeObject(curation, Formatting.None));
            File.WriteAllText(CurationPath, JsonConvert.SerializeObject(curation, Formatting.Indented) + Environment.NewLine, new UTF8Encoding(false));
            AssetDatabase.ImportAsset(CurationPath, ImportAssetOptions.ForceSynchronousImport);
            message = "已保存组件语义修正。";
            return true;
        }

        /// <summary>Clears one component's semantic override while preserving extraction mappings.</summary>
        /// <remarks>Editor-only, AllocByDesign. Re-reads only the selected source Prefab and reseals the derived index.</remarks>
        public bool ClearCurationOverride(string componentId, string elementId, out string message)
        {
            VfxSourceLibraryEditorStatus status = Load();
            VfxSourceComponentView component = status.Index?.Components.FirstOrDefault(value => string.Equals(value.ComponentId, componentId, StringComparison.Ordinal));
            if (!status.Available || component == null)
            {
                message = status.Available ? "找不到待恢复的 Source Component。" : status.Message;
                return false;
            }

            VfxSourceCurationView curation = LoadCuration();
            curation.ElementOverrides ??= new List<VfxSourceElementCurationOverrideView>();
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(component.PrefabPath);
            ParticleSystem system = prefab == null ? null : prefab.GetComponentsInChildren<ParticleSystem>(true).FirstOrDefault(value =>
                AssetDatabase.TryGetGUIDAndLocalFileIdentifier(value, out _, out long localId) && localId == component.LocalFileId);
            VfxSourceEffectView effect = status.Index.Effects.FirstOrDefault(value => string.Equals(value.SourceEffectId, component.SourceEffectId, StringComparison.Ordinal));
            bool sourceRestoreAvailable = system != null && effect != null;
            bool success = ClearCurationOverrideTransactional(
                status.Index,
                curation,
                componentId,
                elementId,
                sourceRestoreAvailable,
                sourceRestoreAvailable
                    ? workingIndex =>
                    {
                        VfxSourceComponentView workingComponent = workingIndex.Components.First(value => string.Equals(value.ComponentId, componentId, StringComparison.Ordinal));
                        RestoreAutomaticSemantics(workingComponent, system, effect);
                    }
                    : null,
                IndexPath,
                WriteCuration,
                SealIndexWithCli,
                out VfxSourceLibraryView committedIndex,
                out message);
            if (committedIndex != null) UpdateIndexCache(committedIndex, IndexPath);
            return success;
        }

        internal static bool ClearCurationOverrideTransactional(
            VfxSourceLibraryView currentIndex,
            VfxSourceCurationView curation,
            string componentId,
            string elementId,
            bool sourceRestoreAvailable,
            Action<VfxSourceLibraryView> restoreAutomaticSemantics,
            string authoritativePath,
            Action<VfxSourceCurationView> persistCuration,
            SealIndexFile sealIndex,
            out VfxSourceLibraryView committedIndex,
            out string message)
        {
            committedIndex = null;
            if (!ClearCurationOverrideAndPersist(curation, componentId, elementId, sourceRestoreAvailable, persistCuration, out message))
                return false;
            if (!sourceRestoreAvailable) return true;
            if (!ResealIndexTransactional(currentIndex, authoritativePath, restoreAutomaticSemantics, sealIndex, out committedIndex, out string sealMessage))
            {
                message = "人工覆盖已清除并保存，但 Source Index 自动语义恢复未提交：" + sealMessage + " 请重建或刷新 Source Index。";
                return true;
            }
            message = "人工覆盖已清除并保存，已恢复所选组件的自动识别语义并重新封存 Source Index。";
            return true;
        }

        private static void RestoreAutomaticSemantics(
            VfxSourceComponentView component,
            ParticleSystem system,
            VfxSourceEffectView effect)
        {
            ParticleSystemRenderer renderer = system.GetComponent<ParticleSystemRenderer>();
            component.DisplayName = system.name;
            component.Roles = InferRoles(system.name, effect.PrefabPath);
            component.Theme = effect.Theme;
            component.Shape = InferShape(system, renderer);
            component.Anchor = InferAnchor(component.Roles, effect.Family);
            component.QualityTier = effect.QualityTier;
            component.Preferred = false;
            component.Deprecated = false;
            component.Note = string.Empty;
            UpdateCreativeFingerprint(component);
        }

        internal static bool ClearCurationOverrideAndPersist(
            VfxSourceCurationView curation,
            string componentId,
            string elementId,
            bool sourceRestoreAvailable,
            Action<VfxSourceCurationView> persist,
            out string message)
        {
            if (curation == null || persist == null)
            {
                message = "清除人工覆盖缺少有效策展数据或持久化入口。";
                return false;
            }

            curation.Entries ??= new List<VfxSourceCurationEntryView>();
            curation.ElementOverrides ??= new List<VfxSourceElementCurationOverrideView>();
            VfxSourceCurationEntryView entry = curation.Entries.FirstOrDefault(value =>
                value != null && string.Equals(value.ComponentId, componentId, StringComparison.Ordinal));
            if (entry != null)
            {
                entry.DisplayName = string.Empty;
                entry.Roles = new List<string>();
                entry.Family = string.Empty;
                entry.Theme = string.Empty;
                entry.Shape = string.Empty;
                entry.Anchor = string.Empty;
                entry.TimingPhase = string.Empty;
                entry.UseCases = new List<string>();
                entry.Motions = new List<string>();
                entry.QualityTier = string.Empty;
                entry.Preferred = false;
                entry.Deprecated = false;
                entry.Note = string.Empty;
            }
            curation.ElementOverrides.RemoveAll(value =>
                value != null && string.Equals(value.ElementId, elementId, StringComparison.Ordinal));
            try
            {
                persist(curation);
                message = sourceRestoreAvailable
                    ? "人工覆盖已清除并保存。"
                    : "人工覆盖已清除并保存，但无法从源 Prefab 恢复派生自动语义；请重建或刷新 Source Index。";
                return true;
            }
            catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException || exception is JsonException)
            {
                message = "人工覆盖清除后写入策展数据失败：" + exception.Message;
                return false;
            }
        }

        private static void WriteCuration(VfxSourceCurationView curation)
        {
            curation.Entries = curation.Entries.OrderBy(value => value.ComponentId, StringComparer.Ordinal).ToList();
            curation.ElementOverrides = (curation.ElementOverrides ?? new List<VfxSourceElementCurationOverrideView>())
                .Where(value => value != null && !string.IsNullOrWhiteSpace(value.ElementId) && !string.IsNullOrWhiteSpace(value.DisplayName))
                .OrderBy(value => value.ElementId, StringComparer.Ordinal).ToList();
            curation.CurationHash = string.Empty;
            curation.CurationHash = VfxSourceElementCurationService.Hash(JsonConvert.SerializeObject(curation, Formatting.None));
            File.WriteAllText(CurationPath, JsonConvert.SerializeObject(curation, Formatting.Indented) + Environment.NewLine, new UTF8Encoding(false));
            AssetDatabase.ImportAsset(CurationPath, ImportAssetOptions.ForceSynchronousImport);
        }

        private static List<string> NormalizeControlledValues(IEnumerable<string> values)
        {
            return (values ?? Array.Empty<string>()).Where(value => !string.IsNullOrWhiteSpace(value))
                .Select(value => value.Trim()).Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal).ToList();
        }

        private static VfxSourceComponentView BuildComponent(ParticleSystem system, GameObject prefab, VfxSourceEffectView effect, string sourceHash, Dictionary<string, VfxSourceCurationEntryView> curationById)
        {
            if (!AssetDatabase.TryGetGUIDAndLocalFileIdentifier(system, out string guid, out long localId) || localId == 0) return null;
            string id = "source.component." + PackageToken(effect.SourcePackageId) + "." + effect.PrefabGuid + "." + localId.ToString();
            ParticleSystemRenderer renderer = system.GetComponent<ParticleSystemRenderer>();
            List<string> roles = InferRoles(system.name, effect.PrefabPath);
            var component = new VfxSourceComponentView
            {
                ComponentId = id,
                SourcePackageId = effect.SourcePackageId,
                SourceEffectId = effect.SourceEffectId,
                PrefabGuid = effect.PrefabGuid,
                PrefabPath = effect.PrefabPath,
                LocalFileId = localId,
                ObjectPath = ObjectPath(system.transform, prefab.transform),
                DisplayName = system.name,
                Roles = roles,
                Theme = effect.Theme,
                Shape = InferShape(system, renderer),
                Anchor = InferAnchor(roles, effect.Family),
                QualityTier = effect.QualityTier,
                SimulationSpace = system.main.simulationSpace.ToString(),
                RenderMode = renderer != null ? renderer.renderMode.ToString() : string.Empty,
                DurationSeconds = system.main.duration,
                Loop = system.main.loop,
                MaxParticles = system.main.maxParticles,
                SourceContentHash = sourceHash
            };
            if (renderer != null)
            {
                component.MaterialGuids = renderer.sharedMaterials.Where(value => value != null).Select(value => AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(value))).Where(value => !string.IsNullOrWhiteSpace(value)).Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal).ToList();
                if (renderer.mesh != null)
                {
                    string meshGuid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(renderer.mesh));
                    if (!string.IsNullOrWhiteSpace(meshGuid)) component.MeshGuids.Add(meshGuid);
                }
            }
            string serialized = EditorJsonUtility.ToJson(system) + "\n" + (renderer != null ? EditorJsonUtility.ToJson(renderer) : string.Empty) + "\n" + LocalTransform(system.transform) + "\n" + string.Join(",", component.MaterialGuids) + "\n" + string.Join(",", component.MeshGuids);
            component.TechnicalFingerprint = Sha256(serialized);
            component.DependencyHash = Hash128.Compute(serialized).ToString();
            component.CreativeFingerprint = Sha256(string.Join("|", roles) + "|" + component.Theme + "|" + component.Shape + "|" + component.Anchor + "|" + component.QualityTier);
            if (curationById.TryGetValue(id, out VfxSourceCurationEntryView curated)) ApplyCuration(component, curated);
            return component;
        }

        private static void CollectSupportAssets(VfxSourceComponentView component, Dictionary<string, VfxSourceSupportAssetView> supportByGuid)
        {
            var direct = new HashSet<string>(component.MaterialGuids.Concat(component.MeshGuids), StringComparer.Ordinal);
            foreach (string materialGuid in component.MaterialGuids)
            {
                string path = AssetDatabase.GUIDToAssetPath(materialGuid);
                foreach (string dependency in AssetDatabase.GetDependencies(path, true))
                {
                    string guid = AssetDatabase.AssetPathToGUID(dependency);
                    if (!string.IsNullOrWhiteSpace(guid)) direct.Add(guid);
                }
            }
            foreach (string guid in direct)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (string.IsNullOrWhiteSpace(path) || !path.StartsWith(SourceRoot + "/", StringComparison.Ordinal)) continue;
                if (!supportByGuid.TryGetValue(guid, out VfxSourceSupportAssetView asset))
                {
                    asset = new VfxSourceSupportAssetView { AssetGuid = guid, AssetPath = path, AssetKind = AssetKind(path), SourcePackageId = PackageId(path), ContentHash = ContentHash(path) };
                    supportByGuid.Add(guid, asset);
                }
                asset.ConsumerComponentIds.Add(component.ComponentId);
            }
        }

        private static List<VfxSourcePackageView> BuildPackages(List<VfxSourceEffectView> effects, List<VfxSourceComponentView> components)
        {
            return effects.GroupBy(value => value.SourcePackageId, StringComparer.Ordinal).OrderBy(value => value.Key, StringComparer.Ordinal).Select(group => new VfxSourcePackageView
            {
                SourcePackageId = group.Key,
                DisplayName = group.Key,
                RootPath = PackageRoot(group.Key),
                LicenseId = LicenseId,
                PublicRedistributionAllowed = true,
                PrefabCount = group.Count(),
                ParticleSystemCount = components.Count(value => value.SourcePackageId == group.Key),
                ContentHash = Sha256(string.Join("|", group.OrderBy(value => value.SourceEffectId, StringComparer.Ordinal).Select(value => value.ContentHash)))
            }).ToList();
        }

        private static List<VfxSourceDuplicateClusterView> BuildDuplicateClusters(List<VfxSourceComponentView> components)
        {
            return components.GroupBy(value => value.TechnicalFingerprint, StringComparer.Ordinal).Where(group => group.Count() > 1).OrderBy(value => value.Key, StringComparer.Ordinal).Select(group =>
            {
                List<VfxSourceComponentView> members = group.OrderByDescending(value => value.Preferred).ThenBy(value => value.ComponentId, StringComparer.Ordinal).ToList();
                return new VfxSourceDuplicateClusterView { ClusterId = "duplicate.technical." + group.Key.Substring(7, 16), ClusterKind = "ExactTechnicalFingerprint", Fingerprint = group.Key, ComponentIds = members.Select(value => value.ComponentId).ToList(), PreferredComponentId = members[0].ComponentId };
            }).ToList();
        }

        private static VfxSourceCurationView LoadCuration()
        {
            try { return File.Exists(CurationPath) ? JsonConvert.DeserializeObject<VfxSourceCurationView>(File.ReadAllText(CurationPath)) ?? new VfxSourceCurationView() : new VfxSourceCurationView(); }
            catch (Exception exception) when (exception is IOException || exception is JsonException) { UnityEngine.Debug.LogWarning("VFX Source curation 无法读取，将使用自动语义：" + exception.Message); return new VfxSourceCurationView(); }
        }

        private static void ApplyCuration(VfxSourceComponentView target, VfxSourceCurationEntryView source)
        {
            if (!string.IsNullOrWhiteSpace(source.DisplayName)) target.DisplayName = source.DisplayName;
            if (source.Roles != null && source.Roles.Count > 0) target.Roles = source.Roles.Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal).ToList();
            if (!string.IsNullOrWhiteSpace(source.Theme)) target.Theme = source.Theme;
            if (!string.IsNullOrWhiteSpace(source.Shape)) target.Shape = source.Shape;
            if (!string.IsNullOrWhiteSpace(source.Anchor)) target.Anchor = source.Anchor;
            if (!string.IsNullOrWhiteSpace(source.QualityTier)) target.QualityTier = source.QualityTier;
            target.Preferred = source.Preferred;
            target.Deprecated = source.Deprecated;
            target.ExistingPrimitiveId = source.ExistingPrimitiveId ?? string.Empty;
            target.Note = source.Note ?? string.Empty;
            UpdateCreativeFingerprint(target);
        }

        private static void UpdateCreativeFingerprint(VfxSourceComponentView component)
        {
            component.CreativeFingerprint = Sha256(string.Join("|", component.Roles.OrderBy(value => value, StringComparer.Ordinal))
                + "|" + component.Theme + "|" + component.Shape + "|" + component.Anchor + "|" + component.QualityTier);
        }

        private static List<string> InferRoles(string name, string path)
        {
            string value = (name + " " + path).ToLowerInvariant();
            var roles = new List<string>();
            AddRole(value, roles, "flash", "flash", "flare", "glint");
            AddRole(value, roles, "glow", "glow", "aura", "halo");
            AddRole(value, roles, "core", "core", "orb", "sphere", "fill");
            AddRole(value, roles, "sparks", "spark", "fleck", "shard", "debris");
            AddRole(value, roles, "smoke", "smoke", "fog", "dust");
            AddRole(value, roles, "trail", "trail", "streak", "ribbon");
            AddRole(value, roles, "decal", "decal", "ground", "crack");
            AddRole(value, roles, "shockwave", "shock", "nova", "ring", "wave");
            AddRole(value, roles, "arc", "arc", "lightning", "electric");
            AddRole(value, roles, "beam", "beam", "laser", "ray");
            AddRole(value, roles, "flame", "fire", "flame", "burn");
            AddRole(value, roles, "rune", "rune", "sigil", "glyph");
            AddRole(value, roles, "slash", "slash", "stab", "lunge", "sword");
            AddRole(value, roles, "debris", "debris", "shard", "rock", "fragment");
            AddRole(value, roles, "liquid", "water", "liquid", "drop", "splash");
            AddRole(value, roles, "distortion", "distort", "heat", "refraction");
            if (roles.Count == 0) roles.Add("detail");
            return roles.Distinct(StringComparer.Ordinal).OrderBy(item => item, StringComparer.Ordinal).ToList();
        }

        private static void AddRole(string value, List<string> roles, string role, params string[] tokens)
        { if (tokens.Any(value.Contains)) roles.Add(role); }

        private static string InferFamily(string path, string name)
        {
            string value = (path + "/" + name).ToLowerInvariant();
            if (value.Contains("lightning") || value.Contains("electric")) return "Lightning";
            if (value.Contains("telegraph") || value.Contains("warning")) return "Telegraph";
            if (value.Contains("shield") || value.Contains("barrier")) return "Shield";
            if (value.Contains("portal") || value.Contains("teleport")) return "Portal";
            if (value.Contains("environment") || value.Contains("weather")) return "Environment";
            if (value.Contains("slash") || value.Contains("melee") || value.Contains("stab")) return "Melee";
            if (value.Contains("rune") || value.Contains("ground") || value.Contains("area")) return "Zone";
            if (value.Contains("cast") || value.Contains("charge") || value.Contains("summon")) return "Casting";
            if (value.Contains("curse") || value.Contains("debuff") || value.Contains("buff")) return "Status";
            if (value.Contains("projectile") || value.Contains("orb") || value.Contains("trail")) return "Projectile";
            if (value.Contains("aoe") || value.Contains("explosion") || value.Contains("nova")) return "Impact";
            if (value.Contains("aura") || value.Contains("buff") || value.Contains("casting")) return "Aura";
            if (value.Contains("beam") || value.Contains("laser")) return "Beam";
            return "General";
        }

        private static string InferTheme(string value)
        {
            string text = value.ToLowerInvariant();
            if (text.Contains("blood") || text.Contains("crimson")) return "Blood";
            if (text.Contains("mana") || text.Contains("arcane")) return "Mana";
            if (text.Contains("water") || text.Contains("liquid")) return "Water";
            if (text.Contains("wind") || text.Contains("tornado")) return "Wind";
            if (text.Contains("nature") || text.Contains("leaf")) return "Nature";
            if (text.Contains("fire") || text.Contains("flame") || text.Contains("red")) return "Fire";
            if (text.Contains("lightning") || text.Contains("electric") || text.Contains("blue")) return "Lightning";
            if (text.Contains("poison") || text.Contains("toxic") || text.Contains("green")) return "Poison";
            if (text.Contains("ice") || text.Contains("frost")) return "Ice";
            if (text.Contains("holy") || text.Contains("angel")) return "Holy";
            if (text.Contains("dark") || text.Contains("demon")) return "Dark";
            return "Generic";
        }

        private static string InferShape(ParticleSystem system, ParticleSystemRenderer renderer)
        {
            if (renderer != null && renderer.renderMode == ParticleSystemRenderMode.Mesh) return "MeshVolume";
            if (renderer != null && renderer.renderMode == ParticleSystemRenderMode.Stretch) return "Path";
            if (system.shape.enabled) return system.shape.shapeType.ToString();
            return "Billboard";
        }

        private static string InferAnchor(List<string> roles, string family)
        {
            if (roles.Contains("trail") || roles.Contains("arc") || family == "Projectile" || family == "Beam") return "Path";
            if (roles.Contains("decal")) return "Ground";
            if (roles.Contains("shockwave") || family == "Impact") return "HitPoint";
            if (family == "Aura") return "Caster";
            return "World";
        }

        private static string QualityTier(int count) => count <= 12 ? "Normal" : count <= 30 ? "GameplayImportant" : count <= 60 ? "Showcase" : "SourceComplex";
        private static string PackageId(string path) => path.Contains("Super Realistic FX Bundle 02", StringComparison.Ordinal) ? "piloto.super-realistic-fx-bundle-02" : path.Contains("Super Realistic FX Bundle", StringComparison.Ordinal) ? "piloto.super-realistic-fx-bundle" : "piloto.shared";
        private static string PackageRoot(string id) => id == "piloto.super-realistic-fx-bundle-02" ? SourceRoot + "/Super Realistic FX Bundle 02" : id == "piloto.super-realistic-fx-bundle" ? SourceRoot + "/Super Realistic FX Bundle" : SourceRoot;
        private static string PackageToken(string value) => (value ?? string.Empty).Replace("piloto.", string.Empty).Replace('-', '_').Replace('.', '_');
        private static string AssetKind(string path)
        {
            Type type = AssetDatabase.GetMainAssetTypeAtPath(path);
            if (type == typeof(Material)) return "Material";
            if (type == typeof(Texture) || (type != null && type.IsSubclassOf(typeof(Texture)))) return "Texture";
            if (type == typeof(Mesh)) return "Mesh";
            if (type == typeof(Shader)) return "Shader";
            return type?.Name ?? Path.GetExtension(path).TrimStart('.');
        }

        private static string ObjectPath(Transform target, Transform root)
        {
            var names = new Stack<string>();
            Transform current = target;
            while (current != null && current != root) { names.Push(current.name); current = current.parent; }
            return string.Join("/", names);
        }

        private static string LocalTransform(Transform value) => value.localPosition.ToString("R") + "|" + value.localRotation.ToString("R") + "|" + value.localScale.ToString("R");
        private static string ContentHash(string assetPath)
        {
            try { return File.Exists(assetPath) ? Sha256(File.ReadAllBytes(assetPath)) : Sha256(assetPath); }
            catch (IOException) { return Sha256(assetPath); }
        }

        private static string Sha256(string value) => Sha256(Encoding.UTF8.GetBytes(value ?? string.Empty));
        private static string Sha256(byte[] value)
        {
            using SHA256 algorithm = SHA256.Create();
            return "sha256:" + string.Concat(algorithm.ComputeHash(value).Select(item => item.ToString("x2")));
        }

        internal static bool RunCli(string command, out string message)
        {
            var start = new ProcessStartInfo("dotnet", "run -c Release --no-restore --project " + Quote(CliProject) + " -- " + command)
            { WorkingDirectory = Path.GetFullPath("."), UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
            try
            {
                using Process process = Process.Start(start);
                if (process == null) { message = "无法启动 VFX Authoring CLI。"; return false; }
                string output = process.StandardOutput.ReadToEnd();
                string error = process.StandardError.ReadToEnd();
                if (!process.WaitForExit(CliTimeoutMilliseconds)) { process.Kill(); message = "VFX Authoring CLI 超时。"; return false; }
                message = string.IsNullOrWhiteSpace(error) ? output.Trim() : (output + Environment.NewLine + error).Trim();
                return process.ExitCode == 0;
            }
            catch (Exception exception) { message = exception.Message; return false; }
        }

        internal static string Quote(string value) => "\"" + (value ?? string.Empty).Replace("\"", "\\\"") + "\"";
    }
}
