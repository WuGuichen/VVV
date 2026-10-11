using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor;

namespace MxFramework.Editor
{
    [Serializable]
    internal sealed class VfxSourceExtractionRegistryView
    {
        [JsonProperty("schemaVersion")] public string SchemaVersion = "mx.vfx.source-extraction-registry.v1";
        [JsonProperty("sourceLibraryId")] public string SourceLibraryId = "source.piloto.studio";
        [JsonProperty("sourceIndexHash")] public string SourceIndexHash = string.Empty;
        [JsonProperty("entries")] public List<VfxSourceExtractionRegistryEntryView> Entries = new List<VfxSourceExtractionRegistryEntryView>();
        [JsonProperty("registryHash")] public string RegistryHash = string.Empty;
    }

    [Serializable]
    internal sealed class VfxSourceExtractionRegistryEntryView
    {
        [JsonProperty("primitiveId")] public string PrimitiveId = string.Empty;
        [JsonProperty("prefabGuid")] public string PrefabGuid = string.Empty;
        [JsonProperty("prefabPath")] public string PrefabPath = string.Empty;
        [JsonProperty("sourceComponentIds")] public List<string> SourceComponentIds = new List<string>();
        [JsonProperty("technicalFingerprints")] public List<string> TechnicalFingerprints = new List<string>();
        [JsonProperty("memberSetHash")] public string MemberSetHash = string.Empty;
        [JsonProperty("dependencyHash")] public string DependencyHash = string.Empty;
        [JsonProperty("derivationMode")] public string DerivationMode = "ExtractedLayer";
        [JsonProperty("variantOverrideHash")] public string VariantOverrideHash = string.Empty;
        [JsonProperty("extractorId")] public string ExtractorId = string.Empty;
        [JsonProperty("extractorVersion")] public string ExtractorVersion = string.Empty;
        [JsonProperty("catalogEvidenceHash")] public string CatalogEvidenceHash = string.Empty;
        [JsonProperty("receiptHash")] public string ReceiptHash = string.Empty;
        [JsonProperty("equivalentComponentIds")] public List<string> EquivalentComponentIds = new List<string>();
        [JsonProperty("stale")] public bool Stale;
    }

    [Serializable]
    internal sealed class VfxSourceElementIndexView
    {
        [JsonProperty("schemaVersion")] public string SchemaVersion = "mx.vfx.source-element-index.v1";
        [JsonProperty("sourceIndexHash")] public string SourceIndexHash = string.Empty;
        [JsonProperty("registryHash")] public string RegistryHash = string.Empty;
        [JsonProperty("elements")] public List<VfxSourceElementView> Elements = new List<VfxSourceElementView>();
        [JsonProperty("indexHash")] public string IndexHash = string.Empty;
    }

    /// <summary>Maintains the canonical extraction mapping used to block duplicate Unity asset creation.</summary>
    /// <remarks>Editor-only, AllocByDesign. The registry is authoring evidence and is never read by Runtime.</remarks>
    internal sealed class VfxSourceExtractionRegistryService
    {
        internal const string RegistryPath = "Assets/Config/MxFramework/Vfx/vfx_source_extraction_registry.json";
        private const string CatalogPath = "Assets/Config/MxFramework/Vfx/vfx_authoring_catalog.json";

        public VfxSourceExtractionRegistryView Load(VfxSourceLibraryView index)
        {
            VfxSourceExtractionRegistryView registry = File.Exists(RegistryPath)
                ? JsonConvert.DeserializeObject<VfxSourceExtractionRegistryView>(File.ReadAllText(RegistryPath))
                : new VfxSourceExtractionRegistryView();
            registry ??= new VfxSourceExtractionRegistryView();
            if (registry.Entries.Count == 0 && index != null)
            {
                registry = BuildFromCatalog(index);
                if (registry.Entries.Count > 0)
                    Save(registry);
                return registry;
            }
            if (RefreshStatus(registry, index) | Normalize(registry)) Save(registry);
            return registry;
        }

        public VfxSourceExtractionRegistryView BuildFromCatalog(VfxSourceLibraryView index)
        {
            var registry = new VfxSourceExtractionRegistryView { SourceLibraryId = index.SourceLibraryId, SourceIndexHash = index.IndexHash };
            if (!File.Exists(CatalogPath)) return registry;
            JObject catalog = JObject.Parse(File.ReadAllText(CatalogPath));
            foreach (JObject primitive in ((JArray)catalog["primitives"]).OfType<JObject>())
            {
                JObject source = primitive["source"] as JObject;
                List<string> componentIds = source?["sourceComponentIds"]?.Values<string>().Where(value => !string.IsNullOrWhiteSpace(value)).Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal).ToList();
                if (componentIds == null || componentIds.Count == 0) continue;
                List<VfxSourceComponentView> components = componentIds.Select(id => index.Components.FirstOrDefault(value => value.ComponentId == id)).Where(value => value != null).ToList();
                List<string> fingerprints = components.Select(value => value.TechnicalFingerprint).Where(value => !string.IsNullOrWhiteSpace(value)).Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal).ToList();
                string prefabPath = primitive["assetRef"]?.Value<string>("path") ?? string.Empty;
                registry.Entries.Add(new VfxSourceExtractionRegistryEntryView
                {
                    PrimitiveId = primitive.Value<string>("primitiveId") ?? string.Empty,
                    PrefabPath = prefabPath,
                    PrefabGuid = AssetDatabase.AssetPathToGUID(prefabPath),
                    SourceComponentIds = componentIds,
                    TechnicalFingerprints = fingerprints,
                    MemberSetHash = HashList(componentIds),
                    DependencyHash = source?.Value<string>("sourceDependencyHash") ?? string.Empty,
                    DerivationMode = source?.Value<string>("derivationMode") ?? "ExtractedLayer",
                    VariantOverrideHash = source?.Value<string>("variantOverrideHash") ?? string.Empty,
                    ExtractorId = source?.Value<string>("extractorId") ?? string.Empty,
                    ExtractorVersion = source?.Value<string>("extractorVersion") ?? string.Empty,
                    CatalogEvidenceHash = VfxSourceElementCurationService.Hash(primitive.ToString(Formatting.None)),
                    EquivalentComponentIds = index.Components.Where(value => fingerprints.Contains(value.TechnicalFingerprint)).Select(value => value.ComponentId).Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal).ToList(),
                    Stale = components.Count != componentIds.Count || !File.Exists(prefabPath)
                });
            }
            Normalize(registry);
            return registry;
        }

        public void Save(VfxSourceExtractionRegistryView registry)
        {
            Normalize(registry);
            registry.RegistryHash = string.Empty;
            File.WriteAllText(RegistryPath, JsonConvert.SerializeObject(registry, Formatting.Indented) + Environment.NewLine, new UTF8Encoding(false));
            if (!VfxSourceLibraryEditorService.RunCli("vfx source seal-registry --project-root . --input " + VfxSourceLibraryEditorService.Quote(RegistryPath) + " --output " + VfxSourceLibraryEditorService.Quote(RegistryPath), out string message))
                throw new InvalidOperationException("无法封装提取注册表：" + message);
            VfxSourceExtractionRegistryView sealedRegistry = JsonConvert.DeserializeObject<VfxSourceExtractionRegistryView>(File.ReadAllText(RegistryPath));
            registry.SchemaVersion = sealedRegistry.SchemaVersion;
            registry.SourceLibraryId = sealedRegistry.SourceLibraryId;
            registry.SourceIndexHash = sealedRegistry.SourceIndexHash;
            registry.Entries = sealedRegistry.Entries;
            registry.RegistryHash = sealedRegistry.RegistryHash;
            AssetDatabase.ImportAsset(RegistryPath, ImportAssetOptions.ForceSynchronousImport);
        }

        public static VfxSourceExtractionRegistryEntryView Find(VfxSourceExtractionRegistryView registry, VfxSourceComponentView component)
        {
            if (registry == null || component == null) return null;
            return registry.Entries.FirstOrDefault(value => !value.Stale && (value.SourceComponentIds.Contains(component.ComponentId) || value.TechnicalFingerprints.Contains(component.TechnicalFingerprint)));
        }

        private static bool RefreshStatus(VfxSourceExtractionRegistryView registry, VfxSourceLibraryView index)
        {
            bool changed = !string.Equals(registry.SourceIndexHash, index?.IndexHash ?? string.Empty, StringComparison.Ordinal);
            registry.SourceIndexHash = index?.IndexHash ?? string.Empty;
            foreach (VfxSourceExtractionRegistryEntryView entry in registry.Entries)
            {
                List<VfxSourceComponentView> components = index == null
                    ? new List<VfxSourceComponentView>()
                    : entry.SourceComponentIds.Select(id => index.Components.FirstOrDefault(value => value.ComponentId == id)).Where(value => value != null).ToList();
                List<string> sourceDependencyHashes = index == null
                    ? new List<string>()
                    : components.Select(component => index.Effects.FirstOrDefault(effect => effect.SourceEffectId == component.SourceEffectId)?.DependencyHash)
                        .Where(value => !string.IsNullOrWhiteSpace(value)).Distinct(StringComparer.Ordinal).ToList();
                bool stale = index == null || components.Count != entry.SourceComponentIds.Count || !File.Exists(entry.PrefabPath)
                    || (!string.IsNullOrWhiteSpace(entry.DependencyHash) && (sourceDependencyHashes.Count != 1 || !string.Equals(sourceDependencyHashes[0], entry.DependencyHash, StringComparison.Ordinal)));
                changed |= stale != entry.Stale;
                entry.Stale = stale;
            }
            return changed;
        }

        private static bool Normalize(VfxSourceExtractionRegistryView registry)
        {
            bool changed = false;
            foreach (VfxSourceExtractionRegistryEntryView entry in registry.Entries)
            {
                List<string> sourceIds = entry.SourceComponentIds.Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal).ToList();
                List<string> fingerprints = entry.TechnicalFingerprints.Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal).ToList();
                List<string> equivalentIds = entry.EquivalentComponentIds.Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal).ToList();
                string memberSetHash = HashList(sourceIds);
                changed |= !entry.SourceComponentIds.SequenceEqual(sourceIds) || !entry.TechnicalFingerprints.SequenceEqual(fingerprints)
                    || !entry.EquivalentComponentIds.SequenceEqual(equivalentIds) || !string.Equals(entry.MemberSetHash, memberSetHash, StringComparison.Ordinal);
                entry.SourceComponentIds = sourceIds;
                entry.TechnicalFingerprints = fingerprints;
                entry.EquivalentComponentIds = equivalentIds;
                entry.MemberSetHash = memberSetHash;
            }
            List<VfxSourceExtractionRegistryEntryView> entries = registry.Entries.OrderBy(value => value.PrimitiveId, StringComparer.Ordinal).ToList();
            changed |= !registry.Entries.SequenceEqual(entries);
            registry.Entries = entries;
            return changed;
        }

        private static string HashList(IEnumerable<string> values) => VfxSourceElementCurationService.Hash(JsonConvert.SerializeObject(values.OrderBy(value => value, StringComparer.Ordinal).ToList(), Formatting.None));
    }

    /// <summary>Builds the complete technical-fingerprint-deduplicated Element index.</summary>
    /// <remarks>Editor-only, AllocByDesign. Results are cached below Library and thumbnails remain lazy.</remarks>
    internal sealed class VfxSourceElementIndexService
    {
        internal const string IndexPath = "Library/MxFramework/VfxSourceLibrary/element-index.json";

        public VfxSourceElementIndexView LoadOrBuild(VfxSourceLibraryView source, VfxSourceCurationView curation, VfxSourceExtractionRegistryView registry)
        {
            registry ??= new VfxSourceExtractionRegistryView();
            if (File.Exists(IndexPath))
            {
                VfxSourceElementIndexView cached = JsonConvert.DeserializeObject<VfxSourceElementIndexView>(File.ReadAllText(IndexPath));
                if (cached != null && cached.SourceIndexHash == (source?.IndexHash ?? string.Empty) && cached.RegistryHash == registry.RegistryHash) return cached;
            }
            VfxSourceElementIndexView built = Build(source, curation, registry);
            Directory.CreateDirectory(Path.GetDirectoryName(IndexPath) ?? "Library/MxFramework/VfxSourceLibrary");
            File.WriteAllText(IndexPath, JsonConvert.SerializeObject(built, Formatting.Indented), new UTF8Encoding(false));
            return built;
        }

        internal static VfxSourceElementIndexView Build(VfxSourceLibraryView source, VfxSourceCurationView curation, VfxSourceExtractionRegistryView registry)
        {
            registry ??= new VfxSourceExtractionRegistryView();
            var result = new VfxSourceElementIndexView { SourceIndexHash = source?.IndexHash ?? string.Empty, RegistryHash = registry.RegistryHash };
            if (source == null) return result;

            var curatedIds = new HashSet<string>((curation?.Elements ?? new List<VfxSourceElementView>())
                .Where(value => value != null)
                .SelectMany(value => value.MemberComponentIds ?? new List<string>()), StringComparer.Ordinal);
            Dictionary<string, VfxSourceEffectView> effects = (source.Effects ?? new List<VfxSourceEffectView>())
                .Where(value => value != null && !string.IsNullOrWhiteSpace(value.SourceEffectId))
                .GroupBy(value => value.SourceEffectId, StringComparer.Ordinal)
                .ToDictionary(value => value.Key, value => value.First(), StringComparer.Ordinal);
            foreach (IGrouping<string, VfxSourceComponentView> group in (source.Components ?? new List<VfxSourceComponentView>())
                .Where(value => value != null && !value.Deprecated && value.MaterialGuids != null && value.MaterialGuids.Count > 0 && !string.IsNullOrWhiteSpace(value.TechnicalFingerprint))
                .GroupBy(value => value.TechnicalFingerprint, StringComparer.Ordinal))
            {
                List<VfxSourceComponentView> members = group.OrderByDescending(value => value.Preferred)
                    .ThenByDescending(value => curatedIds.Contains(value.ComponentId))
                    .ThenByDescending(value => registry?.Entries.Any(entry => !entry.Stale && entry.SourceComponentIds.Contains(value.ComponentId)) == true)
                    .ThenByDescending(value => VfxSourceExtractionRegistryService.Find(registry, value) != null || !string.IsNullOrWhiteSpace(value.ExistingPrimitiveId))
                    .ThenBy(value => value.MaxParticles).ThenBy(value => value.ComponentId, StringComparer.Ordinal).ToList();
                VfxSourceComponentView canonical = members[0];
                VfxSourceExtractionRegistryEntryView extraction = VfxSourceExtractionRegistryService.Find(registry, canonical);
                VfxSourceEffectView effect = effects.TryGetValue(canonical.SourceEffectId, out VfxSourceEffectView found) ? found : null;
                string token = canonical.TechnicalFingerprint.StartsWith("sha256:", StringComparison.Ordinal) ? canonical.TechnicalFingerprint.Substring(7, 16) : VfxCompositionDraftService.FileToken(canonical.ComponentId);
                result.Elements.Add(new VfxSourceElementView
                {
                    ElementId = "vfx.element.unique." + token,
                    DisplayName = canonical.DisplayName,
                    ElementKind = "UniqueSourceLayer",
                    MemberComponentIds = new List<string> { canonical.ComponentId },
                    EquivalentComponentIds = members.Skip(1).Select(value => value.ComponentId).ToList(),
                    Roles = canonical.Roles.ToList(), Theme = canonical.Theme, Family = effect?.Family ?? "General", Shape = canonical.Shape,
                    Anchor = canonical.Anchor, Lifecycle = canonical.Loop ? "Loop" : "OneShot", TimingPhase = "Peak", QualityTier = canonical.QualityTier,
                    BudgetMaxParticles = canonical.MaxParticles, SourcePackageIds = new List<string> { canonical.SourcePackageId }, SourceEffectIds = new List<string> { canonical.SourceEffectId },
                    SourcePrefabGuids = new List<string> { canonical.PrefabGuid }, SourceContentHashes = new List<string> { canonical.SourceContentHash },
                    TechnicalFingerprint = canonical.TechnicalFingerprint, ExistingPrimitiveId = extraction?.PrimitiveId ?? canonical.ExistingPrimitiveId,
                    ExtractionStatus = extraction == null ? "NotExtracted" : extraction.Stale ? "Stale" : "ReuseExisting", CurationStatus = curatedIds.Contains(canonical.ComponentId) ? "Curated" : "AutoSuggested",
                    PreviewEvidencePath = VfxSourceElementCurationService.PreviewRoot + "/unique-" + token + "/manifest.json"
                });
            }
            result.Elements = result.Elements.OrderBy(value => value.ElementId, StringComparer.Ordinal).ToList();
            result.IndexHash = VfxSourceElementCurationService.Hash(JsonConvert.SerializeObject(result.Elements, Formatting.None));
            return result;
        }
    }
}
