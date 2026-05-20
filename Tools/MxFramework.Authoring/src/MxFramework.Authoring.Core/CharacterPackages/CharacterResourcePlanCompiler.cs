using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace MxFramework.Authoring
{
    public static class CharacterResourcePlanFormats
    {
        public const string RuntimeResourceCatalog = "mx.characterRuntimeResourceCatalog.v1";
        public const string CharacterResourcePlan = "mx.characterResourcePlan.v1";
        public const string AudioCueManifest = "mx.characterAudioCueManifest.v1";
        public const string ResourceValidationReport = "mx.characterResourceValidationReport.v1";
    }

    public static class CharacterResourcePlanGroups
    {
        public const string SpawnCritical = "SpawnCritical";
        public const string PresentationCritical = "PresentationCritical";
        public const string EquipmentInitial = "EquipmentInitial";
        public const string AnimationWarmup = "AnimationWarmup";
        public const string VfxWarmup = "VfxWarmup";
        public const string UiDeferred = "UiDeferred";
        public const string Audio = "Audio";
    }

    public static class CharacterResourcePlanFailurePolicies
    {
        public const string FailSpawn = "FailSpawn";
        public const string UseFallbackVisual = "UseFallbackVisual";
        public const string UseFallbackEquipment = "UseFallbackEquipment";
        public const string UseFallbackPose = "UseFallbackPose";
        public const string SkipEffect = "SkipEffect";
        public const string ShowPlaceholder = "ShowPlaceholder";
        public const string MuteMissingCue = "MuteMissingCue";
    }

    public static class CharacterResourcePlanValidationCodes
    {
        public const string LibraryItemMissing = "RES_LIBRARY_ITEM_MISSING";
        public const string LibraryResourceKeyDuplicate = "RES_LIBRARY_RESOURCE_KEY_DUPLICATE";
        public const string LibrarySourceFileMissing = "RES_LIBRARY_SOURCE_FILE_MISSING";
        public const string LibraryNotRuntimeLoadable = "RES_LIBRARY_NOT_RUNTIME_LOADABLE";
        public const string PlanRequiredResourceMissing = "RES_LIBRARY_PLAN_REQUIRED_RESOURCE_MISSING";
    }

    public sealed class CharacterResourcePlanCompileRequest
    {
        public CharacterResourcePackage Package { get; set; }
        public string PackageRootPath { get; set; } = string.Empty;
        public CharacterAuthoringCompileResult AuthoringCompileResult { get; set; }
        public bool ValidateResourceFiles { get; set; }
        public bool ValidateResourceHashes { get; set; }
    }

    public sealed class CharacterResourcePlanCompileResult
    {
        public string PackageId { get; set; } = string.Empty;
        public string CharacterStableId { get; set; } = string.Empty;
        public RuntimeResourceCatalogDocument RuntimeResourceCatalog { get; set; } = new RuntimeResourceCatalogDocument();
        public CharacterResourcePlanDocument CharacterResourcePlan { get; set; } = new CharacterResourcePlanDocument();
        public AudioCueManifestDocument AudioCueManifest { get; set; } = new AudioCueManifestDocument();
        public ResourceValidationReportDocument ResourceValidationReport { get; set; } = new ResourceValidationReportDocument();
    }

    public sealed class RuntimeResourceCatalogDocument
    {
        public string Format { get; set; } = CharacterResourcePlanFormats.RuntimeResourceCatalog;
        public int SchemaVersion { get; set; } = 1;
        public string CatalogId { get; set; } = string.Empty;
        public string PackageId { get; set; } = string.Empty;
        public List<RuntimeResourceCatalogEntryDocument> Entries { get; set; } = new List<RuntimeResourceCatalogEntryDocument>();
    }

    public sealed class RuntimeResourceCatalogEntryDocument
    {
        public string Id { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public string Variant { get; set; } = string.Empty;
        public string PackageId { get; set; } = string.Empty;
        public string Provider { get; set; } = string.Empty;
        public string Address { get; set; } = string.Empty;
        public List<string> Labels { get; set; } = new List<string>();
        public List<RuntimeResourceKeyDocument> Dependencies { get; set; } = new List<RuntimeResourceKeyDocument>();
        public string Hash { get; set; } = string.Empty;
        public long Size { get; set; }
        public bool AllowOverride { get; set; }
        public Dictionary<string, string> ProviderData { get; set; } = new Dictionary<string, string>(StringComparer.Ordinal);
    }

    public sealed class RuntimeResourceKeyDocument
    {
        public string Id { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public string Variant { get; set; } = string.Empty;
        public string PackageId { get; set; } = string.Empty;
    }

    public sealed class CharacterResourcePlanDocument
    {
        public string Format { get; set; } = CharacterResourcePlanFormats.CharacterResourcePlan;
        public string SchemaVersion { get; set; } = "1.0";
        public string PackageId { get; set; } = string.Empty;
        public string CharacterStableId { get; set; } = string.Empty;
        public string PlanHash { get; set; } = string.Empty;
        public CharacterResourcePlanGroup SpawnCritical { get; set; } = CharacterResourcePlanGroup.CreateRequired(CharacterResourcePlanFailurePolicies.FailSpawn);
        public CharacterResourcePlanGroup PresentationCritical { get; set; } = CharacterResourcePlanGroup.CreateRequired(CharacterResourcePlanFailurePolicies.UseFallbackVisual);
        public CharacterResourcePlanGroup EquipmentInitial { get; set; } = CharacterResourcePlanGroup.CreateRequired(CharacterResourcePlanFailurePolicies.UseFallbackEquipment);
        public CharacterResourcePlanGroup AnimationWarmup { get; set; } = CharacterResourcePlanGroup.CreateRequired(CharacterResourcePlanFailurePolicies.UseFallbackPose);
        public CharacterResourcePlanGroup VfxWarmup { get; set; } = CharacterResourcePlanGroup.CreateOptional(CharacterResourcePlanFailurePolicies.SkipEffect);
        public CharacterResourcePlanGroup UiDeferred { get; set; } = CharacterResourcePlanGroup.CreateOptional(CharacterResourcePlanFailurePolicies.ShowPlaceholder);
        public CharacterAudioResourcePlanGroup Audio { get; set; } = new CharacterAudioResourcePlanGroup();
        public List<CharacterResourcePlanDiagnostic> Diagnostics { get; set; } = new List<CharacterResourcePlanDiagnostic>();
    }

    public sealed class CharacterResourcePlanGroup
    {
        public bool Required { get; set; }
        public string FailurePolicy { get; set; } = string.Empty;
        public List<CharacterResourcePlanResourceRef> Resources { get; set; } = new List<CharacterResourcePlanResourceRef>();

        public static CharacterResourcePlanGroup CreateRequired(string failurePolicy)
        {
            return new CharacterResourcePlanGroup { Required = true, FailurePolicy = failurePolicy ?? string.Empty };
        }

        public static CharacterResourcePlanGroup CreateOptional(string failurePolicy)
        {
            return new CharacterResourcePlanGroup { Required = false, FailurePolicy = failurePolicy ?? string.Empty };
        }
    }

    public sealed class CharacterAudioResourcePlanGroup
    {
        public bool Required { get; set; }
        public string FailurePolicy { get; set; } = CharacterResourcePlanFailurePolicies.MuteMissingCue;
        public List<string> RequiredBanks { get; set; } = new List<string>();
        public List<string> RequiredCues { get; set; } = new List<string>();
    }

    public sealed class CharacterResourcePlanResourceRef
    {
        public string ResourceKey { get; set; } = string.Empty;
        public string TypeId { get; set; } = string.Empty;
        public string Variant { get; set; } = string.Empty;
        public string PackageId { get; set; } = string.Empty;
        public string Usage { get; set; } = string.Empty;
        public string StableId { get; set; } = string.Empty;
    }

    public sealed class AudioCueManifestDocument
    {
        public string Format { get; set; } = CharacterResourcePlanFormats.AudioCueManifest;
        public string SchemaVersion { get; set; } = "1.0";
        public string PackageId { get; set; } = string.Empty;
        public string CharacterStableId { get; set; } = string.Empty;
        public List<string> Banks { get; set; } = new List<string>();
        public List<AudioCueManifestEntry> Cues { get; set; } = new List<AudioCueManifestEntry>();
        public List<CharacterResourcePlanDiagnostic> Diagnostics { get; set; } = new List<CharacterResourcePlanDiagnostic>();
    }

    public sealed class AudioCueManifestEntry
    {
        public string CueId { get; set; } = string.Empty;
        public string StableId { get; set; } = string.Empty;
        public string ResourceKey { get; set; } = string.Empty;
        public string EventPath { get; set; } = string.Empty;
        public string Bank { get; set; } = string.Empty;
        public string FallbackPolicy { get; set; } = CharacterResourcePlanFailurePolicies.MuteMissingCue;
        public Dictionary<string, string> ProviderData { get; set; } = new Dictionary<string, string>(StringComparer.Ordinal);
    }

    public sealed class ResourceValidationReportDocument
    {
        public string Format { get; set; } = CharacterResourcePlanFormats.ResourceValidationReport;
        public string SchemaVersion { get; set; } = "1.0";
        public string PackageId { get; set; } = string.Empty;
        public string CharacterStableId { get; set; } = string.Empty;
        public string Status { get; set; } = "Ready";
        public bool HasErrors { get; set; }
        public List<CharacterResourcePlanDiagnostic> Diagnostics { get; set; } = new List<CharacterResourcePlanDiagnostic>();
        public CharacterResourcePlanGroupCounts GroupCounts { get; set; } = new CharacterResourcePlanGroupCounts();
    }

    public sealed class CharacterResourcePlanGroupCounts
    {
        public int SpawnCritical { get; set; }
        public int PresentationCritical { get; set; }
        public int EquipmentInitial { get; set; }
        public int AnimationWarmup { get; set; }
        public int VfxWarmup { get; set; }
        public int UiDeferred { get; set; }
        public int Audio { get; set; }
    }

    public sealed class CharacterResourcePlanDiagnostic
    {
        public string Severity { get; set; } = string.Empty;
        public string Code { get; set; } = string.Empty;
        public string LibraryItemStableId { get; set; } = string.Empty;
        public string ResourceKey { get; set; } = string.Empty;
        public string SourceConfigKind { get; set; } = string.Empty;
        public string SourceField { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public string SuggestedFix { get; set; } = string.Empty;
    }

    public static class CharacterResourcePlanCompiler
    {
        public static CharacterResourcePlanCompileResult Compile(CharacterResourcePlanCompileRequest request)
        {
            if (request == null)
                request = new CharacterResourcePlanCompileRequest();

            CharacterResourcePackage package = request.Package;
            CharacterAuthoringCompileResult authoring = request.AuthoringCompileResult ?? CharacterAuthoringCompiler.Compile(new CharacterAuthoringCompileRequest
            {
                Package = package,
                PackageRootPath = request.PackageRootPath,
                Options = new CharacterAuthoringCompileOptions
                {
                    ValidateResourceFiles = request.ValidateResourceFiles || request.ValidateResourceHashes,
                    ValidateResourceHashes = request.ValidateResourceHashes
                }
            });

            string packageId = package != null && package.Manifest != null ? package.Manifest.PackageId : string.Empty;
            string characterStableId = GetCharacterStableId(package);
            var diagnostics = new List<CharacterResourcePlanDiagnostic>();
            CopyCompilerDiagnostics(authoring, diagnostics);

            CharacterPackageResourceCatalog packageCatalog = package != null ? package.ResourceCatalog : null;
            CharacterPackageResourceMapping mapping = authoring != null ? authoring.ResourceMapping : null;
            Dictionary<string, CharacterPackageResourceEntry> entriesByKey = BuildEntryLookup(packageCatalog, diagnostics);
            Dictionary<string, CharacterPackageResourceMappingEntry> mappingsByKey = BuildMappingLookup(mapping);
            SortedSet<string> referencedKeys = BuildReferencedKeys(package, entriesByKey, diagnostics);
            IncludeDependencies(referencedKeys, entriesByKey);

            RuntimeResourceCatalogDocument runtimeCatalog = BuildRuntimeCatalog(packageId, request.PackageRootPath, entriesByKey, mappingsByKey, referencedKeys, diagnostics);
            AudioCueManifestDocument audioManifest = BuildAudioCueManifest(packageId, characterStableId, entriesByKey, referencedKeys);
            CharacterResourcePlanDocument plan = BuildCharacterPlan(packageId, characterStableId, runtimeCatalog, audioManifest, entriesByKey, referencedKeys, diagnostics);
            ResourceValidationReportDocument validationReport = BuildValidationReport(packageId, characterStableId, plan, diagnostics);

            return new CharacterResourcePlanCompileResult
            {
                PackageId = packageId,
                CharacterStableId = characterStableId,
                RuntimeResourceCatalog = runtimeCatalog,
                CharacterResourcePlan = plan,
                AudioCueManifest = audioManifest,
                ResourceValidationReport = validationReport
            };
        }

        private static RuntimeResourceCatalogDocument BuildRuntimeCatalog(
            string packageId,
            string packageRootPath,
            Dictionary<string, CharacterPackageResourceEntry> entriesByKey,
            Dictionary<string, CharacterPackageResourceMappingEntry> mappingsByKey,
            SortedSet<string> referencedKeys,
            List<CharacterResourcePlanDiagnostic> diagnostics)
        {
            var document = new RuntimeResourceCatalogDocument
            {
                CatalogId = "character.package." + (packageId ?? string.Empty) + ".runtime",
                PackageId = packageId ?? string.Empty
            };

            foreach (string key in referencedKeys)
            {
                CharacterPackageResourceEntry entry;
                if (!entriesByKey.TryGetValue(key, out entry) || entry == null)
                    continue;

                if (!IsRuntimeCatalogLoadable(entry))
                    continue;

                CharacterPackageResourceMappingEntry mapping;
                mappingsByKey.TryGetValue(key, out mapping);
                document.Entries.Add(CreateRuntimeCatalogEntry(packageId, packageRootPath, entry, mapping, entriesByKey, mappingsByKey));
            }

            document.Entries.Sort((a, b) => string.CompareOrdinal(a.Id, b.Id));
            return document;
        }

        private static RuntimeResourceCatalogEntryDocument CreateRuntimeCatalogEntry(
            string packageId,
            string packageRootPath,
            CharacterPackageResourceEntry entry,
            CharacterPackageResourceMappingEntry mapping,
            Dictionary<string, CharacterPackageResourceEntry> entriesByKey,
            Dictionary<string, CharacterPackageResourceMappingEntry> mappingsByKey)
        {
            string typeId = MapRuntimeTypeId(entry.TypeId);
            string address = mapping != null && !string.IsNullOrWhiteSpace(mapping.ImportTargetPath)
                ? mapping.ImportTargetPath
                : entry.RelativePath;
            string provider = GetRuntimeProviderId(entry);
            var providerData = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["packageResourceKey"] = entry.ResourceKey ?? string.Empty,
                ["stableId"] = entry.StableId ?? string.Empty,
                ["sourceRelativePath"] = entry.RelativePath ?? string.Empty,
                ["sourceFormat"] = CharacterPackageResourcePipeline.GetEffectiveSourceFormat(entry),
                ["usage"] = entry.Usage ?? string.Empty,
                ["requestedProviderId"] = entry.ImportHints != null ? entry.ImportHints.ProviderId ?? string.Empty : string.Empty
            };

            if (mapping != null)
            {
                providerData["assetPath"] = mapping.ImportTargetPath ?? string.Empty;
                providerData["unityAssetPath"] = mapping.ImportTargetPath ?? string.Empty;
                providerData["importHash"] = mapping.ImportHash ?? string.Empty;
                providerData["dependencyHash"] = mapping.DependencyHash ?? string.Empty;
            }

            CopyMetadata(entry, providerData);

            return new RuntimeResourceCatalogEntryDocument
            {
                Id = entry.ResourceKey ?? string.Empty,
                Type = typeId,
                Variant = entry.Variant ?? string.Empty,
                PackageId = string.IsNullOrWhiteSpace(entry.PackageId) ? packageId ?? string.Empty : entry.PackageId,
                Provider = provider,
                Address = address ?? string.Empty,
                Labels = BuildLabels(packageId, entry),
                Dependencies = BuildRuntimeDependencies(packageId, entry, entriesByKey, mappingsByKey),
                Hash = CharacterPackageResourcePipeline.GetDeclaredContentHash(entry),
                Size = GetPackageResourceSize(packageRootPath, entry.RelativePath),
                AllowOverride = false,
                ProviderData = providerData
            };
        }

        private static CharacterResourcePlanDocument BuildCharacterPlan(
            string packageId,
            string characterStableId,
            RuntimeResourceCatalogDocument runtimeCatalog,
            AudioCueManifestDocument audioManifest,
            Dictionary<string, CharacterPackageResourceEntry> entriesByKey,
            SortedSet<string> referencedKeys,
            List<CharacterResourcePlanDiagnostic> diagnostics)
        {
            var plan = new CharacterResourcePlanDocument
            {
                PackageId = packageId ?? string.Empty,
                CharacterStableId = characterStableId ?? string.Empty
            };

            var runtimeKeys = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < runtimeCatalog.Entries.Count; i++)
                runtimeKeys.Add(runtimeCatalog.Entries[i].Id);

            foreach (string key in referencedKeys)
            {
                CharacterPackageResourceEntry entry;
                if (!entriesByKey.TryGetValue(key, out entry) || entry == null)
                    continue;

                if (IsAudioCue(entry))
                    continue;

                if (!runtimeKeys.Contains(key))
                {
                    AddDiagnostic(diagnostics, "Error", CharacterResourcePlanValidationCodes.PlanRequiredResourceMissing, entry.StableId, key, "character", "resourceKeys", "Referenced resource cannot be added to the runtime resource catalog.", "Import the resource into a runtime-loadable target or remove the runtime reference.");
                    continue;
                }

                AddResourceToGroup(SelectGroup(plan, entry), entry);
            }

            plan.Audio.RequiredCues.AddRange(audioManifest.Cues.ConvertAll(cue => cue.CueId));
            plan.Audio.RequiredBanks.AddRange(audioManifest.Banks);
            SortGroup(plan.SpawnCritical);
            SortGroup(plan.PresentationCritical);
            SortGroup(plan.EquipmentInitial);
            SortGroup(plan.AnimationWarmup);
            SortGroup(plan.VfxWarmup);
            SortGroup(plan.UiDeferred);
            plan.Audio.RequiredCues.Sort(StringComparer.Ordinal);
            plan.Audio.RequiredBanks.Sort(StringComparer.Ordinal);

            CopyDiagnostics(diagnostics, plan.Diagnostics);
            plan.PlanHash = CharacterPackageHashUtility.ComputeTextSha256(BuildPlanCanonicalText(plan));
            return plan;
        }

        private static AudioCueManifestDocument BuildAudioCueManifest(
            string packageId,
            string characterStableId,
            Dictionary<string, CharacterPackageResourceEntry> entriesByKey,
            SortedSet<string> referencedKeys)
        {
            var manifest = new AudioCueManifestDocument
            {
                PackageId = packageId ?? string.Empty,
                CharacterStableId = characterStableId ?? string.Empty
            };

            var banks = new SortedSet<string>(StringComparer.Ordinal);
            foreach (string key in referencedKeys)
            {
                CharacterPackageResourceEntry entry;
                if (!entriesByKey.TryGetValue(key, out entry) || entry == null || !IsAudioCue(entry))
                    continue;

                var providerData = new Dictionary<string, string>(StringComparer.Ordinal);
                CopyMetadata(entry, providerData);
                string bank = GetMetadata(entry, "bank");
                string banksCsv = GetMetadata(entry, "banks");
                AddCsvValues(banks, banksCsv);
                if (!string.IsNullOrWhiteSpace(bank))
                    banks.Add(bank);

                manifest.Cues.Add(new AudioCueManifestEntry
                {
                    CueId = string.IsNullOrWhiteSpace(entry.ResourceKey) ? entry.StableId : entry.ResourceKey,
                    StableId = entry.StableId ?? string.Empty,
                    ResourceKey = entry.ResourceKey ?? string.Empty,
                    EventPath = FirstNonEmpty(GetMetadata(entry, "eventPath"), GetMetadata(entry, "fmodEventPath"), GetMetadata(entry, "audioEventPath")),
                    Bank = bank ?? string.Empty,
                    ProviderData = providerData
                });
            }

            manifest.Cues.Sort((a, b) => string.CompareOrdinal(a.CueId, b.CueId));
            manifest.Banks.AddRange(banks);
            return manifest;
        }

        private static ResourceValidationReportDocument BuildValidationReport(
            string packageId,
            string characterStableId,
            CharacterResourcePlanDocument plan,
            List<CharacterResourcePlanDiagnostic> diagnostics)
        {
            var report = new ResourceValidationReportDocument
            {
                PackageId = packageId ?? string.Empty,
                CharacterStableId = characterStableId ?? string.Empty,
                GroupCounts = new CharacterResourcePlanGroupCounts
                {
                    SpawnCritical = plan.SpawnCritical.Resources.Count,
                    PresentationCritical = plan.PresentationCritical.Resources.Count,
                    EquipmentInitial = plan.EquipmentInitial.Resources.Count,
                    AnimationWarmup = plan.AnimationWarmup.Resources.Count,
                    VfxWarmup = plan.VfxWarmup.Resources.Count,
                    UiDeferred = plan.UiDeferred.Resources.Count,
                    Audio = plan.Audio.RequiredCues.Count
                }
            };

            CopyDiagnostics(diagnostics, report.Diagnostics);
            for (int i = 0; i < report.Diagnostics.Count; i++)
            {
                if (string.Equals(report.Diagnostics[i].Severity, "Error", StringComparison.Ordinal))
                    report.HasErrors = true;
            }

            report.Status = report.HasErrors ? "Blocked" : "Ready";
            return report;
        }

        private static Dictionary<string, CharacterPackageResourceEntry> BuildEntryLookup(CharacterPackageResourceCatalog catalog, List<CharacterResourcePlanDiagnostic> diagnostics)
        {
            var lookup = new Dictionary<string, CharacterPackageResourceEntry>(StringComparer.Ordinal);
            if (catalog == null || catalog.Entries == null)
                return lookup;

            for (int i = 0; i < catalog.Entries.Count; i++)
            {
                CharacterPackageResourceEntry entry = catalog.Entries[i];
                if (entry == null || string.IsNullOrWhiteSpace(entry.ResourceKey))
                    continue;

                if (lookup.ContainsKey(entry.ResourceKey))
                {
                    AddDiagnostic(diagnostics, "Error", CharacterResourcePlanValidationCodes.LibraryResourceKeyDuplicate, entry.StableId, entry.ResourceKey, "resourceCatalog", "resourceKey", "Duplicate runtime resource key in character package resource catalog.", "Give each runtime-loadable resource a unique resourceKey.");
                    continue;
                }

                lookup.Add(entry.ResourceKey, entry);
            }

            return lookup;
        }

        private static Dictionary<string, CharacterPackageResourceMappingEntry> BuildMappingLookup(CharacterPackageResourceMapping mapping)
        {
            var lookup = new Dictionary<string, CharacterPackageResourceMappingEntry>(StringComparer.Ordinal);
            if (mapping == null || mapping.Entries == null)
                return lookup;

            for (int i = 0; i < mapping.Entries.Count; i++)
            {
                CharacterPackageResourceMappingEntry entry = mapping.Entries[i];
                if (entry != null && !string.IsNullOrWhiteSpace(entry.PackageResourceKey))
                    lookup[entry.PackageResourceKey] = entry;
            }

            return lookup;
        }

        private static SortedSet<string> BuildReferencedKeys(CharacterResourcePackage package, Dictionary<string, CharacterPackageResourceEntry> entriesByKey, List<CharacterResourcePlanDiagnostic> diagnostics)
        {
            var keys = new SortedSet<string>(StringComparer.Ordinal);
            List<string> configuredKeys = package != null && package.ApplicationConfig != null ? package.ApplicationConfig.ResourceKeys : null;
            if (configuredKeys == null || configuredKeys.Count == 0)
            {
                foreach (string key in entriesByKey.Keys)
                    keys.Add(key);
                return keys;
            }

            for (int i = 0; i < configuredKeys.Count; i++)
            {
                string key = configuredKeys[i];
                if (string.IsNullOrWhiteSpace(key))
                    continue;

                keys.Add(key);
                if (!entriesByKey.ContainsKey(key))
                    AddDiagnostic(diagnostics, "Error", CharacterResourcePlanValidationCodes.LibraryItemMissing, string.Empty, key, "character", "resourceKeys", "Character application config references a resource key that is missing from resource_catalog.json.", "Import the resource, fix the resource key, or remove the reference.");
            }

            return keys;
        }

        private static void IncludeDependencies(SortedSet<string> keys, Dictionary<string, CharacterPackageResourceEntry> entriesByKey)
        {
            bool changed;
            do
            {
                changed = false;
                var snapshot = new List<string>(keys);
                for (int i = 0; i < snapshot.Count; i++)
                {
                    CharacterPackageResourceEntry entry;
                    if (!entriesByKey.TryGetValue(snapshot[i], out entry) || entry == null || entry.Dependencies == null)
                        continue;

                    for (int d = 0; d < entry.Dependencies.Count; d++)
                    {
                        CharacterPackageResourceDependency dependency = entry.Dependencies[d];
                        if (dependency != null && !string.IsNullOrWhiteSpace(dependency.ResourceKey) && keys.Add(dependency.ResourceKey))
                            changed = true;
                    }
                }
            }
            while (changed);
        }

        private static List<RuntimeResourceKeyDocument> BuildRuntimeDependencies(
            string packageId,
            CharacterPackageResourceEntry entry,
            Dictionary<string, CharacterPackageResourceEntry> entriesByKey,
            Dictionary<string, CharacterPackageResourceMappingEntry> mappingsByKey)
        {
            var result = new List<RuntimeResourceKeyDocument>();
            if (entry == null || entry.Dependencies == null)
                return result;

            for (int i = 0; i < entry.Dependencies.Count; i++)
            {
                CharacterPackageResourceDependency dependency = entry.Dependencies[i];
                if (dependency == null || string.IsNullOrWhiteSpace(dependency.ResourceKey))
                    continue;

                CharacterPackageResourceEntry target;
                if (!entriesByKey.TryGetValue(dependency.ResourceKey, out target) || target == null)
                    continue;

                result.Add(new RuntimeResourceKeyDocument
                {
                    Id = target.ResourceKey ?? string.Empty,
                    Type = MapRuntimeTypeId(target.TypeId),
                    Variant = target.Variant ?? string.Empty,
                    PackageId = string.IsNullOrWhiteSpace(target.PackageId) ? packageId ?? string.Empty : target.PackageId
                });
            }

            result.Sort((a, b) => string.CompareOrdinal(a.Id, b.Id));
            return result;
        }

        private static CharacterResourcePlanGroup SelectGroup(CharacterResourcePlanDocument plan, CharacterPackageResourceEntry entry)
        {
            string usage = entry != null ? entry.Usage : string.Empty;
            string typeId = entry != null ? entry.TypeId : string.Empty;

            if (string.Equals(usage, CharacterPackageResourceUsageIds.CharacterModel, StringComparison.Ordinal))
                return plan.SpawnCritical;
            if (string.Equals(usage, CharacterPackageResourceUsageIds.WeaponModel, StringComparison.Ordinal))
                return plan.EquipmentInitial;
            if (string.Equals(usage, CharacterPackageResourceUsageIds.AnimationClipGroup, StringComparison.Ordinal) ||
                string.Equals(typeId, CharacterPackageResourceTypeIds.Animation, StringComparison.Ordinal))
                return plan.AnimationWarmup;
            if (string.Equals(usage, CharacterPackageResourceUsageIds.VfxCue, StringComparison.Ordinal) ||
                string.Equals(typeId, CharacterPackageResourceTypeIds.Vfx, StringComparison.Ordinal))
                return plan.VfxWarmup;
            if (string.Equals(usage, CharacterPackageResourceUsageIds.PreviewThumbnail, StringComparison.Ordinal) ||
                string.Equals(usage, CharacterPackageResourceUsageIds.PreviewMesh, StringComparison.Ordinal) ||
                string.Equals(typeId, CharacterPackageResourceTypeIds.Preview, StringComparison.Ordinal))
                return plan.UiDeferred;

            return plan.PresentationCritical;
        }

        private static void AddResourceToGroup(CharacterResourcePlanGroup group, CharacterPackageResourceEntry entry)
        {
            group.Resources.Add(new CharacterResourcePlanResourceRef
            {
                ResourceKey = entry.ResourceKey ?? string.Empty,
                TypeId = MapRuntimeTypeId(entry.TypeId),
                Variant = entry.Variant ?? string.Empty,
                PackageId = entry.PackageId ?? string.Empty,
                Usage = entry.Usage ?? string.Empty,
                StableId = entry.StableId ?? string.Empty
            });
        }

        private static void SortGroup(CharacterResourcePlanGroup group)
        {
            if (group == null || group.Resources == null)
                return;

            group.Resources.Sort((a, b) => string.CompareOrdinal(a.ResourceKey, b.ResourceKey));
        }

        private static bool IsRuntimeCatalogLoadable(CharacterPackageResourceEntry entry)
        {
            if (entry == null || string.IsNullOrWhiteSpace(entry.ResourceKey))
                return false;

            string bindingKind = GetMetadata(entry, "runtimeBindingKind");
            if (string.Equals(bindingKind, "UnityEditorOnlyAsset", StringComparison.Ordinal) ||
                string.Equals(bindingKind, "GeneratedPreviewOnly", StringComparison.Ordinal))
                return false;

            if (IsFmodAudioEntry(entry))
                return false;

            return !string.IsNullOrWhiteSpace(MapRuntimeTypeId(entry.TypeId));
        }

        private static bool IsAudioCue(CharacterPackageResourceEntry entry)
        {
            return entry != null &&
                (string.Equals(entry.TypeId, CharacterPackageResourceTypeIds.Audio, StringComparison.Ordinal) ||
                 string.Equals(entry.Usage, CharacterPackageResourceUsageIds.AudioCue, StringComparison.Ordinal) ||
                 IsFmodAudioEntry(entry));
        }

        private static bool IsFmodAudioEntry(CharacterPackageResourceEntry entry)
        {
            if (entry == null)
                return false;

            string bindingKind = GetMetadata(entry, "runtimeBindingKind");
            return string.Equals(bindingKind, "AudioEventDefinition", StringComparison.Ordinal) ||
                string.Equals(bindingKind, "AudioCue", StringComparison.Ordinal) ||
                !string.IsNullOrWhiteSpace(GetMetadata(entry, "fmodEventPath")) ||
                !string.IsNullOrWhiteSpace(GetMetadata(entry, "audioEventPath")) ||
                !string.IsNullOrWhiteSpace(GetMetadata(entry, "fmodGuid"));
        }

        private static string MapRuntimeTypeId(string packageTypeId)
        {
            switch (packageTypeId)
            {
                case CharacterPackageResourceTypeIds.Model:
                case CharacterPackageResourceTypeIds.Vfx:
                    return "GameObject";
                case CharacterPackageResourceTypeIds.Texture:
                case CharacterPackageResourceTypeIds.Preview:
                    return "Texture2D";
                case CharacterPackageResourceTypeIds.Material:
                    return "Material";
                case CharacterPackageResourceTypeIds.Animation:
                    return "AnimationClip";
                case CharacterPackageResourceTypeIds.Audio:
                    return "AudioClip";
                case CharacterPackageResourceTypeIds.Config:
                case CharacterPackageResourceTypeIds.Geometry:
                    return "TextAsset";
                default:
                    return "Object";
            }
        }

        private static string GetRuntimeProviderId(CharacterPackageResourceEntry entry)
        {
            string provider = entry != null && entry.ImportHints != null ? entry.ImportHints.ProviderId : string.Empty;
            if (string.Equals(provider, "memory", StringComparison.Ordinal) ||
                string.Equals(provider, "resources", StringComparison.Ordinal) ||
                string.Equals(provider, "assetBundle", StringComparison.Ordinal) ||
                string.Equals(provider, "remoteBundle", StringComparison.Ordinal))
                return provider;

            return "memory";
        }

        private static List<string> BuildLabels(string packageId, CharacterPackageResourceEntry entry)
        {
            var labels = new SortedSet<string>(StringComparer.Ordinal);
            labels.Add("package." + (packageId ?? string.Empty));
            labels.Add("character.resourcePackage");
            if (entry != null)
            {
                if (!string.IsNullOrWhiteSpace(entry.Usage))
                    labels.Add("character.usage." + NormalizeLabelSegment(entry.Usage));
                if (!string.IsNullOrWhiteSpace(entry.TypeId))
                    labels.Add("character.type." + NormalizeLabelSegment(entry.TypeId));
                if (entry.ImportHints != null && entry.ImportHints.Labels != null)
                {
                    for (int i = 0; i < entry.ImportHints.Labels.Count; i++)
                        labels.Add(entry.ImportHints.Labels[i]);
                }

                if (entry.Tags != null)
                {
                    for (int i = 0; i < entry.Tags.Count; i++)
                        labels.Add("tag." + NormalizeLabelSegment(entry.Tags[i]));
                }
            }

            return new List<string>(labels);
        }

        private static string NormalizeLabelSegment(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return string.Empty;

            var builder = new StringBuilder(value.Length);
            for (int i = 0; i < value.Length; i++)
            {
                char c = char.ToLowerInvariant(value[i]);
                bool valid = (c >= 'a' && c <= 'z') || (c >= '0' && c <= '9') || c == '.' || c == '_' || c == '-';
                builder.Append(valid ? c : '.');
            }

            return builder.ToString().Trim('.');
        }

        private static long GetPackageResourceSize(string packageRootPath, string relativePath)
        {
            if (string.IsNullOrWhiteSpace(packageRootPath) || string.IsNullOrWhiteSpace(relativePath))
                return 0;

            string fullPath = Path.GetFullPath(Path.Combine(packageRootPath, relativePath));
            string root = Path.GetFullPath(packageRootPath);
            if (!root.EndsWith(Path.DirectorySeparatorChar.ToString(), StringComparison.Ordinal))
                root += Path.DirectorySeparatorChar;
            if (!fullPath.StartsWith(root, StringComparison.Ordinal) || !File.Exists(fullPath))
                return 0;

            return new FileInfo(fullPath).Length;
        }

        private static string GetCharacterStableId(CharacterResourcePackage package)
        {
            if (package != null && package.ApplicationConfig != null && !string.IsNullOrWhiteSpace(package.ApplicationConfig.CharacterStableId))
                return package.ApplicationConfig.CharacterStableId;
            if (package != null && package.Manifest != null && !string.IsNullOrWhiteSpace(package.Manifest.StableId))
                return package.Manifest.StableId;
            return string.Empty;
        }

        private static void CopyCompilerDiagnostics(CharacterAuthoringCompileResult authoring, List<CharacterResourcePlanDiagnostic> diagnostics)
        {
            if (authoring == null || authoring.GateReport == null || authoring.GateReport.Issues == null)
                return;

            for (int i = 0; i < authoring.GateReport.Issues.Count; i++)
            {
                CharacterAuthoringValidationIssue issue = authoring.GateReport.Issues[i];
                if (issue == null)
                    continue;

                string code = issue.Code;
                if (string.Equals(code, CharacterAuthoringValidationCodes.MissingResourceFile, StringComparison.Ordinal))
                    code = CharacterResourcePlanValidationCodes.LibrarySourceFileMissing;

                AddDiagnostic(diagnostics, issue.Severity.ToString(), code, string.Empty, string.Empty, issue.SourcePath, issue.Field, issue.Message, issue.SuggestedFix);
            }
        }

        private static void AddDiagnostic(
            List<CharacterResourcePlanDiagnostic> diagnostics,
            string severity,
            string code,
            string stableId,
            string resourceKey,
            string sourceConfigKind,
            string sourceField,
            string message,
            string suggestedFix)
        {
            if (diagnostics == null)
                return;

            diagnostics.Add(new CharacterResourcePlanDiagnostic
            {
                Severity = severity ?? string.Empty,
                Code = code ?? string.Empty,
                LibraryItemStableId = stableId ?? string.Empty,
                ResourceKey = resourceKey ?? string.Empty,
                SourceConfigKind = sourceConfigKind ?? string.Empty,
                SourceField = sourceField ?? string.Empty,
                Message = message ?? string.Empty,
                SuggestedFix = suggestedFix ?? string.Empty
            });
        }

        private static void CopyDiagnostics(List<CharacterResourcePlanDiagnostic> source, List<CharacterResourcePlanDiagnostic> target)
        {
            if (source == null || target == null)
                return;

            source.Sort((a, b) =>
            {
                int code = string.CompareOrdinal(a.Code, b.Code);
                if (code != 0)
                    return code;
                return string.CompareOrdinal(a.ResourceKey, b.ResourceKey);
            });

            for (int i = 0; i < source.Count; i++)
                target.Add(source[i]);
        }

        private static void CopyMetadata(CharacterPackageResourceEntry entry, Dictionary<string, string> target)
        {
            if (entry == null || entry.ImportHints == null || entry.ImportHints.Metadata == null || target == null)
                return;

            foreach (KeyValuePair<string, string> pair in entry.ImportHints.Metadata)
                target[pair.Key] = pair.Value ?? string.Empty;
        }

        private static string GetMetadata(CharacterPackageResourceEntry entry, string key)
        {
            if (entry == null || entry.ImportHints == null || entry.ImportHints.Metadata == null || string.IsNullOrWhiteSpace(key))
                return string.Empty;

            string value;
            return entry.ImportHints.Metadata.TryGetValue(key, out value) ? value ?? string.Empty : string.Empty;
        }

        private static void AddCsvValues(SortedSet<string> target, string csv)
        {
            if (target == null || string.IsNullOrWhiteSpace(csv))
                return;

            string[] values = csv.Split(',');
            for (int i = 0; i < values.Length; i++)
            {
                string value = values[i].Trim();
                if (!string.IsNullOrWhiteSpace(value))
                    target.Add(value);
            }
        }

        private static string FirstNonEmpty(string a, string b, string c)
        {
            if (!string.IsNullOrWhiteSpace(a))
                return a;
            if (!string.IsNullOrWhiteSpace(b))
                return b;
            return c ?? string.Empty;
        }

        private static string BuildPlanCanonicalText(CharacterResourcePlanDocument plan)
        {
            var builder = new StringBuilder();
            builder.Append("package=").Append(plan.PackageId).Append('\n');
            builder.Append("character=").Append(plan.CharacterStableId).Append('\n');
            AppendGroup(builder, CharacterResourcePlanGroups.SpawnCritical, plan.SpawnCritical);
            AppendGroup(builder, CharacterResourcePlanGroups.PresentationCritical, plan.PresentationCritical);
            AppendGroup(builder, CharacterResourcePlanGroups.EquipmentInitial, plan.EquipmentInitial);
            AppendGroup(builder, CharacterResourcePlanGroups.AnimationWarmup, plan.AnimationWarmup);
            AppendGroup(builder, CharacterResourcePlanGroups.VfxWarmup, plan.VfxWarmup);
            AppendGroup(builder, CharacterResourcePlanGroups.UiDeferred, plan.UiDeferred);
            builder.Append(CharacterResourcePlanGroups.Audio).Append('|').Append(plan.Audio.FailurePolicy).Append('\n');
            for (int i = 0; i < plan.Audio.RequiredBanks.Count; i++)
                builder.Append("bank=").Append(plan.Audio.RequiredBanks[i]).Append('\n');
            for (int i = 0; i < plan.Audio.RequiredCues.Count; i++)
                builder.Append("cue=").Append(plan.Audio.RequiredCues[i]).Append('\n');
            return builder.ToString();
        }

        private static void AppendGroup(StringBuilder builder, string name, CharacterResourcePlanGroup group)
        {
            builder.Append(name).Append('|').Append(group.Required ? "required" : "optional").Append('|').Append(group.FailurePolicy).Append('\n');
            for (int i = 0; i < group.Resources.Count; i++)
            {
                CharacterResourcePlanResourceRef resource = group.Resources[i];
                builder.Append(resource.ResourceKey)
                    .Append('|').Append(resource.TypeId)
                    .Append('|').Append(resource.Variant)
                    .Append('|').Append(resource.PackageId)
                    .Append('|').Append(resource.Usage)
                    .Append('|').Append(resource.StableId)
                    .Append('\n');
            }
        }
    }
}
