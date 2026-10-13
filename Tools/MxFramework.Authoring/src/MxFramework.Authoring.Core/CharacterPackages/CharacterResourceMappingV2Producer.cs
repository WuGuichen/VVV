using System;
using System.Collections.Generic;
using System.IO;
using MxFramework.ResourceMapping.Authoring;

namespace MxFramework.Authoring
{
    public static class CharacterResourceMappingV2ProducerContracts
    {
        public const string ContractVersion = "1";
        public const string DefaultBundleRuleId = "character.package.assets";
        public const string MaterialTargetCollision = "CharacterGeneratedMaterialTargetCollision";
    }

    public sealed class CharacterResourceMappingV2Materialization
    {
        public ResourceMappingProducerSnapshot Snapshot { get; set; } = new ResourceMappingProducerSnapshot();
        public List<ResourceMappingDiagnostic> Diagnostics { get; set; } = new List<ResourceMappingDiagnostic>();

        public bool HasErrors
        {
            get
            {
                for (int i = 0; i < Diagnostics.Count; i++)
                {
                    if (Diagnostics[i].Severity == ResourceMappingDiagnosticSeverity.Error)
                        return true;
                }
                return false;
            }
        }
    }

    public static class CharacterResourceMappingV2Producer
    {
        /// <remarks>AllocByDesign. Materializes one complete low-frequency Character producer snapshot.</remarks>
        public static CharacterResourceMappingV2Materialization Materialize(
            CharacterResourcePackage package,
            CharacterPackageResourceMapping legacyCompilerMapping,
            string bundleRuleId = CharacterResourceMappingV2ProducerContracts.DefaultBundleRuleId)
        {
            string packageId = package != null && package.Manifest != null ? package.Manifest.PackageId ?? string.Empty : string.Empty;
            var result = new CharacterResourceMappingV2Materialization
            {
                Snapshot = new ResourceMappingProducerSnapshot
                {
                    ProducerId = "character.package." + NormalizeSegment(packageId)
                }
            };
            if (string.IsNullOrWhiteSpace(packageId))
            {
                AddError(result, "CharacterPackageIdMissing", "Character package requires an explicit packageId.", "manifest.packageId");
                return result;
            }

            CharacterPackageResourceCatalog catalog = package.ResourceCatalog ?? new CharacterPackageResourceCatalog();
            var mappings = BuildMappingLookup(legacyCompilerMapping);
            var entries = BuildEntryLookup(catalog, result);
            for (int i = 0; i < catalog.Entries.Count; i++)
            {
                CharacterPackageResourceEntry source = catalog.Entries[i];
                if (source == null)
                    continue;
                mappings.TryGetValue(source.ResourceKey ?? string.Empty, out CharacterPackageResourceMappingEntry mapping);
                ResourceMappingEntry entry = MaterializeEntry(result.Snapshot.ProducerId, packageId, source, mapping, entries, bundleRuleId, result);
                if (entry != null)
                    result.Snapshot.Entries.Add(entry);
            }
            result.Snapshot.Entries.Sort((left, right) => string.Compare(left.AuthoringRecordId, right.AuthoringRecordId, StringComparison.Ordinal));
            ValidateMaterialTargets(result);
            return result;
        }

        private static void ValidateMaterialTargets(CharacterResourceMappingV2Materialization result)
        {
            var targets = new Dictionary<string, ResourceMappingEntry>(StringComparer.OrdinalIgnoreCase);
            string materialTypeId = MapCanonicalTypeId(CharacterPackageResourceTypeIds.Material);
            foreach (ResourceMappingEntry entry in result.Snapshot.Entries)
            {
                if (entry.ResourceKey?.TypeId != materialTypeId
                    || entry.SourceLocator?.Kind != ResourceMappingSourceLocatorKind.GeneratedUnityAsset)
                    continue;

                string target = NormalizeProjectPath(entry.SourceLocator.CachedPath);
                if (targets.TryGetValue(target, out ResourceMappingEntry previous))
                {
                    if (previous.AuthoringRecordId != entry.AuthoringRecordId || previous.ResourceKey.Id != entry.ResourceKey.Id)
                        AddError(result, CharacterResourceMappingV2ProducerContracts.MaterialTargetCollision,
                            "Material resources '" + previous.ResourceKey.Id + "' and '" + entry.ResourceKey.Id
                            + "' target the same generated Unity asset: " + previous.SourceLocator.CachedPath + ".",
                            entry.AuthoringRecordId + ".sourceLocator.cachedPath");
                }
                else
                    targets.Add(target, entry);
            }
        }

        private static ResourceMappingEntry MaterializeEntry(
            string producerId,
            string packageId,
            CharacterPackageResourceEntry source,
            CharacterPackageResourceMappingEntry mapping,
            IDictionary<string, CharacterPackageResourceEntry> entries,
            string bundleRuleId,
            CharacterResourceMappingV2Materialization result)
        {
            string authoringRecordId = !string.IsNullOrWhiteSpace(source.StableId)
                ? source.StableId
                : "resource." + (source.ResourceKey ?? string.Empty);
            bool editorOnly = IsEditorOnly(source);
            bool excluded = IsExcluded(source);
            string canonicalTypeId = MapCanonicalTypeId(source.TypeId);
            if (!editorOnly && !excluded && string.IsNullOrWhiteSpace(canonicalTypeId))
            {
                AddError(result, "CharacterTypeIdUnsupported", "Character runtime resource type has no canonical v2 TypeId: " + source.TypeId + ".", authoringRecordId + ".typeId");
                return null;
            }

            ResourceMappingSourceLocator sourceLocator = CreateSourceLocator(
                packageId,
                source,
                mapping,
                result,
                authoringRecordId + ".sourceLocator");
            if (sourceLocator == null)
                return null;

            var entry = new ResourceMappingEntry
            {
                ProducerReceipt = Receipt(producerId, authoringRecordId, ComputeSourceFingerprint(source, mapping)),
                AuthoringRecordId = authoringRecordId,
                DeliveryIntent = excluded
                    ? ResourceMappingDeliveryIntent.Excluded
                    : (editorOnly ? ResourceMappingDeliveryIntent.EditorOnly : ResourceMappingDeliveryIntent.Internal),
                SourceLocator = sourceLocator,
                Labels = BuildLabels(packageId, source),
                ProviderData = new Dictionary<string, string>(StringComparer.Ordinal)
            };
            AddStableValue(entry.ProviderData, "usage", source.Usage);
            AddStableValue(entry.ProviderData, "sourceFormat", source.SourceFormat);

            if (!editorOnly && !excluded)
            {
                entry.ResourceKey = new ResourceMappingResourceKey
                {
                    Id = source.ResourceKey ?? string.Empty,
                    TypeId = canonicalTypeId,
                    Variant = source.Variant ?? string.Empty,
                    PackageId = string.IsNullOrWhiteSpace(source.PackageId) ? packageId : source.PackageId
                };
                entry.OverrideIntent = source.ConflictPolicy != null && source.ConflictPolicy.AllowOverwrite
                    ? ResourceMappingOverrideIntent.Allow
                    : ResourceMappingOverrideIntent.Forbid;
                entry.BundleIntent = new ResourceMappingBundleIntent { RuleId = bundleRuleId ?? string.Empty };
                entry.Dependencies = BuildDependencies(packageId, source, entries, result, authoringRecordId);
            }
            return entry;
        }

        private static ResourceMappingSourceLocator CreateSourceLocator(
            string packageId,
            CharacterPackageResourceEntry source,
            CharacterPackageResourceMappingEntry mapping,
            CharacterResourceMappingV2Materialization result,
            string path)
        {
            if (mapping != null && !string.IsNullOrWhiteSpace(mapping.ImportTargetPath) && !IsEditorOnly(source) && !IsExcluded(source))
            {
                string generatedAssetPath = ResolveGeneratedUnityAssetPath(packageId, source, mapping, result, path);
                if (string.IsNullOrWhiteSpace(generatedAssetPath))
                    return null;
                return new ResourceMappingSourceLocator
                {
                    Kind = ResourceMappingSourceLocatorKind.GeneratedUnityAsset,
                    GeneratedAssetId = !string.IsNullOrWhiteSpace(source.StableId) ? source.StableId : source.ResourceKey,
                    CachedPath = generatedAssetPath
                };
            }
            return new ResourceMappingSourceLocator
            {
                Kind = ResourceMappingSourceLocatorKind.ImportedPackageFile,
                PackageRelativePath = source.RelativePath ?? string.Empty,
                ContentHash = CharacterPackageResourcePipeline.GetDeclaredContentHash(source)
            };
        }

        private static string ResolveGeneratedUnityAssetPath(
            string packageId,
            CharacterPackageResourceEntry source,
            CharacterPackageResourceMappingEntry mapping,
            CharacterResourceMappingV2Materialization result,
            string path)
        {
            string importTargetPath = NormalizeProjectPath(mapping.ImportTargetPath);
            string sourceFormat = CharacterPackageResourcePipeline.GetEffectiveSourceFormat(source);
            if (CharacterGeneratedAnimationClipAssetPaths.RequiresExtraction(source))
            {
                return CharacterGeneratedAnimationClipAssetPaths.Resolve(
                    packageId,
                    !string.IsNullOrWhiteSpace(mapping.ProjectResourceKey)
                        ? mapping.ProjectResourceKey
                        : source.ResourceKey);
            }

            bool materialDescriptor = string.Equals(source.TypeId, CharacterPackageResourceTypeIds.Material, StringComparison.OrdinalIgnoreCase)
                && (string.Equals(sourceFormat, CharacterPackageResourceFormatIds.MaterialJson, StringComparison.OrdinalIgnoreCase)
                    || importTargetPath.EndsWith(".material.json", StringComparison.OrdinalIgnoreCase));
            if (materialDescriptor)
                return ResolveGeneratedMaterialPath(packageId, source, importTargetPath, result, path);

            if (string.Equals(sourceFormat, CharacterPackageResourceFormatIds.AnimationGroupJson, StringComparison.OrdinalIgnoreCase)
                || string.Equals(sourceFormat, CharacterPackageResourceFormatIds.VfxJson, StringComparison.OrdinalIgnoreCase))
            {
                AddError(
                    result,
                    "CharacterGeneratedUnityDescriptorUnsupported",
                    "Character runtime descriptor format requires an explicit final Unity asset path resolver: " + sourceFormat + ".",
                    path);
                return string.Empty;
            }

            if (string.Equals(source.TypeId, CharacterPackageResourceTypeIds.Material, StringComparison.OrdinalIgnoreCase)
                && !importTargetPath.EndsWith(".mat", StringComparison.OrdinalIgnoreCase))
            {
                AddError(
                    result,
                    "CharacterGeneratedMaterialPathInvalid",
                    "Character Material ResourceMapping must reference a generated .mat asset, not its source descriptor: " + importTargetPath + ".",
                    path);
                return string.Empty;
            }

            return importTargetPath;
        }

        private static string ResolveGeneratedMaterialPath(
            string packageId,
            CharacterPackageResourceEntry source,
            string importTargetPath,
            CharacterResourceMappingV2Materialization result,
            string path)
        {
            string targetRelativePath = NormalizeProjectPath(
                source != null && source.ImportHints != null && !string.IsNullOrWhiteSpace(source.ImportHints.TargetRelativePath)
                    ? source.ImportHints.TargetRelativePath
                    : source != null ? source.RelativePath : string.Empty).TrimStart('/');
            const string descriptorSuffix = ".material.json";
            string descriptorFileName = Path.GetFileName(targetRelativePath) ?? string.Empty;
            if (string.IsNullOrWhiteSpace(targetRelativePath)
                || !descriptorFileName.EndsWith(descriptorSuffix, StringComparison.OrdinalIgnoreCase))
            {
                AddError(
                    result,
                    "CharacterMaterialDescriptorPathInvalid",
                    "materialJson requires a targetRelativePath ending in .material.json.",
                    path);
                return string.Empty;
            }

            string relativeSuffix = "/" + targetRelativePath;
            if (!importTargetPath.EndsWith(relativeSuffix, StringComparison.OrdinalIgnoreCase))
            {
                AddError(
                    result,
                    "CharacterMaterialDescriptorTargetMismatch",
                    "Material descriptor import target does not end with its declared targetRelativePath: " + importTargetPath + ".",
                    path);
                return string.Empty;
            }

            string packageRoot = importTargetPath.Substring(0, importTargetPath.Length - relativeSuffix.Length).TrimEnd('/');
            if (string.IsNullOrWhiteSpace(packageRoot))
            {
                AddError(result, "CharacterMaterialPackageRootMissing", "Material descriptor import target has no generated package root.", path);
                return string.Empty;
            }

            string descriptorId = descriptorFileName.Substring(0, descriptorFileName.Length - descriptorSuffix.Length);
            return packageRoot + "/materials/"
                + SanitizeGeneratedAssetPathSegment(packageId) + "_"
                + SanitizeGeneratedAssetPathSegment(descriptorId) + ".mat";
        }

        private static string NormalizeProjectPath(string value)
        {
            return (value ?? string.Empty).Replace('\\', '/').Trim();
        }

        private static string SanitizeGeneratedAssetPathSegment(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return "unknown";

            char[] characters = value.Trim().ToCharArray();
            for (int i = 0; i < characters.Length; i++)
            {
                char c = characters[i];
                if (c < 32 || c == '<' || c == '>' || c == ':' || c == '"' || c == '/'
                    || c == '\\' || c == '|' || c == '?' || c == '*' || c == '.')
                {
                    characters[i] = '_';
                }
            }
            return new string(characters);
        }

        private static List<ResourceMappingResourceKey> BuildDependencies(
            string packageId,
            CharacterPackageResourceEntry source,
            IDictionary<string, CharacterPackageResourceEntry> entries,
            CharacterResourceMappingV2Materialization result,
            string path)
        {
            var dependencies = new List<ResourceMappingResourceKey>();
            IList<CharacterPackageResourceDependency> sourceDependencies = source.Dependencies ?? new List<CharacterPackageResourceDependency>();
            for (int i = 0; i < sourceDependencies.Count; i++)
            {
                CharacterPackageResourceDependency dependency = sourceDependencies[i];
                if (dependency == null || string.IsNullOrWhiteSpace(dependency.ResourceKey))
                    continue;
                if (!entries.TryGetValue(dependency.ResourceKey, out CharacterPackageResourceEntry target) || target == null || IsEditorOnly(target) || IsExcluded(target))
                {
                    if (dependency.Required)
                        AddError(result, "CharacterDependencyNotRuntime", "Required Character dependency does not resolve one runtime package row.", path + ".dependencies[" + i + "]");
                    continue;
                }
                string typeId = MapCanonicalTypeId(target.TypeId);
                if (string.IsNullOrWhiteSpace(typeId))
                    continue;
                dependencies.Add(new ResourceMappingResourceKey
                {
                    Id = target.ResourceKey ?? string.Empty,
                    TypeId = typeId,
                    Variant = target.Variant ?? string.Empty,
                    PackageId = string.IsNullOrWhiteSpace(target.PackageId) ? packageId : target.PackageId
                });
            }
            return dependencies;
        }

        private static Dictionary<string, CharacterPackageResourceEntry> BuildEntryLookup(CharacterPackageResourceCatalog catalog, CharacterResourceMappingV2Materialization result)
        {
            var entries = new Dictionary<string, CharacterPackageResourceEntry>(StringComparer.Ordinal);
            for (int i = 0; i < catalog.Entries.Count; i++)
            {
                CharacterPackageResourceEntry entry = catalog.Entries[i];
                if (entry == null || string.IsNullOrWhiteSpace(entry.ResourceKey))
                    continue;
                if (!entries.TryAdd(entry.ResourceKey, entry))
                    AddError(result, "CharacterResourceKeyDuplicate", "Character package resource key must be unique.", "resourceCatalog.entries[" + i + "].resourceKey");
            }
            return entries;
        }

        private static Dictionary<string, CharacterPackageResourceMappingEntry> BuildMappingLookup(CharacterPackageResourceMapping mapping)
        {
            var result = new Dictionary<string, CharacterPackageResourceMappingEntry>(StringComparer.Ordinal);
            if (mapping == null || mapping.Entries == null)
                return result;
            for (int i = 0; i < mapping.Entries.Count; i++)
            {
                CharacterPackageResourceMappingEntry entry = mapping.Entries[i];
                if (entry != null && !string.IsNullOrWhiteSpace(entry.PackageResourceKey))
                    result[entry.PackageResourceKey] = entry;
            }
            return result;
        }

        private static string MapCanonicalTypeId(string value)
        {
            return ResourceMappingCanonicalTypeIds.TryCanonicalize(value, out string canonical) ? canonical : string.Empty;
        }

        private static bool IsEditorOnly(CharacterPackageResourceEntry entry)
        {
            return entry != null && (string.Equals(entry.TypeId, CharacterPackageResourceTypeIds.Preview, StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(entry.Usage, CharacterPackageResourceUsageIds.PreviewThumbnail, StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(entry.Usage, CharacterPackageResourceUsageIds.PreviewMesh, StringComparison.OrdinalIgnoreCase));
        }

        private static bool IsExcluded(CharacterPackageResourceEntry entry)
        {
            return entry != null && (string.Equals(entry.TypeId, CharacterPackageResourceTypeIds.Config, StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(entry.TypeId, CharacterPackageResourceTypeIds.Geometry, StringComparison.OrdinalIgnoreCase));
        }

        private static List<string> BuildLabels(string packageId, CharacterPackageResourceEntry entry)
        {
            var labels = new SortedSet<string>(StringComparer.Ordinal) { "domain.character", "package." + packageId };
            if (entry.Tags != null)
            {
                for (int i = 0; i < entry.Tags.Count; i++)
                    if (!string.IsNullOrWhiteSpace(entry.Tags[i])) labels.Add(entry.Tags[i]);
            }
            if (entry.ImportHints != null && entry.ImportHints.Labels != null)
            {
                for (int i = 0; i < entry.ImportHints.Labels.Count; i++)
                    if (!string.IsNullOrWhiteSpace(entry.ImportHints.Labels[i])) labels.Add(entry.ImportHints.Labels[i]);
            }
            return new List<string>(labels);
        }

        private static string ComputeSourceFingerprint(CharacterPackageResourceEntry entry, CharacterPackageResourceMappingEntry mapping)
        {
            var input = new CharacterResourceMappingV2FingerprintInput
            {
                StableId = entry.StableId,
                ResourceKey = entry.ResourceKey,
                CanonicalTypeId = MapCanonicalTypeId(entry.TypeId),
                Variant = entry.Variant,
                PackageId = entry.PackageId,
                Usage = entry.Usage,
                SourceFormat = entry.SourceFormat,
                RelativePath = entry.RelativePath,
                ContentHash = CharacterPackageResourcePipeline.GetDeclaredContentHash(entry),
                ImportHash = mapping != null ? mapping.ImportHash : string.Empty,
                DependencyHash = mapping != null ? mapping.DependencyHash : string.Empty,
                ImportTargetPath = mapping != null ? mapping.ImportTargetPath : string.Empty
            };
            return ResourceMappingCanonicalJson.ComputeFingerprint(ResourceMappingCanonicalJson.Serialize(input));
        }

        private static ResourceMappingProducerReceipt Receipt(string producerId, string authoringRecordId, string fingerprint)
        {
            return new ResourceMappingProducerReceipt
            {
                ProducerId = producerId,
                ProducerContractVersion = CharacterResourceMappingV2ProducerContracts.ContractVersion,
                SourceRecordFingerprint = fingerprint
            };
        }

        private static void CopyOwned<T>(IList<T> source, IList<T> target, string producerId, Func<T, ResourceMappingProducerReceipt> receipt)
        {
            if (source == null)
                return;
            for (int i = 0; i < source.Count; i++)
            {
                T item = source[i];
                ResourceMappingProducerReceipt owner = item != null ? receipt(item) : null;
                if (owner != null && string.Equals(owner.ProducerId, producerId, StringComparison.Ordinal))
                    target.Add(item);
            }
        }

        private static string NormalizeSegment(string value)
        {
            return (value ?? string.Empty).Trim().ToLowerInvariant().Replace('_', '-');
        }

        private static void AddStableValue(IDictionary<string, string> data, string key, string value)
        {
            if (!string.IsNullOrWhiteSpace(value))
                data[key] = value;
        }

        private static void AddError(CharacterResourceMappingV2Materialization result, string code, string message, string path)
        {
            result.Diagnostics.Add(new ResourceMappingDiagnostic { Severity = ResourceMappingDiagnosticSeverity.Error, Code = code, Message = message, Path = path });
        }

        private sealed class CharacterResourceMappingV2FingerprintInput
        {
            public string StableId { get; set; } = string.Empty;
            public string ResourceKey { get; set; } = string.Empty;
            public string CanonicalTypeId { get; set; } = string.Empty;
            public string Variant { get; set; } = string.Empty;
            public string PackageId { get; set; } = string.Empty;
            public string Usage { get; set; } = string.Empty;
            public string SourceFormat { get; set; } = string.Empty;
            public string RelativePath { get; set; } = string.Empty;
            public string ContentHash { get; set; } = string.Empty;
            public string ImportHash { get; set; } = string.Empty;
            public string DependencyHash { get; set; } = string.Empty;
            public string ImportTargetPath { get; set; } = string.Empty;
        }
    }
}
