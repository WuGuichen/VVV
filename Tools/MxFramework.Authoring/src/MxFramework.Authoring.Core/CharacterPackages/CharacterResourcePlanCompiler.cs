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
        public const string AudioCueManifest = "mx.characterAudioCueManifest.v2";
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
        public AuthoringResourceCollection AuthoringResources { get; set; }
        public AuthoringResourceSelectionManifestDocument ResourceSelectionManifest { get; set; }
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
        public string SchemaVersion { get; set; } = "2.0";
        public string GlobalCatalogHash { get; set; } = string.Empty;
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
        public int RuntimeCueId { get; set; }
        public int RuntimeEventId { get; set; }
        public string EventDefinitionId { get; set; } = string.Empty;
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

            RuntimeResourceCatalogDocument runtimeCatalog = BuildRuntimeCatalog(
                packageId,
                characterStableId,
                request.PackageRootPath,
                request.AuthoringResources,
                entriesByKey,
                mappingsByKey,
                referencedKeys,
                diagnostics);
            AudioCueManifestDocument audioManifest = BuildAudioCueManifest(packageId, characterStableId, entriesByKey, referencedKeys);
            CharacterResourcePlanDocument plan = BuildCharacterPlan(packageId, characterStableId, runtimeCatalog, audioManifest, entriesByKey, referencedKeys, diagnostics);
            ApplyAuthoringResourceSelections(request, packageId, characterStableId, runtimeCatalog, audioManifest, plan, diagnostics);
            plan.Diagnostics.Clear();
            CopyDiagnostics(diagnostics, plan.Diagnostics);
            plan.PlanHash = CharacterPackageHashUtility.ComputeTextSha256(BuildPlanCanonicalText(plan));
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
            string characterStableId,
            string packageRootPath,
            AuthoringResourceCollection authoringResources,
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

                if (HasResourceSelection(entry.ResourceSelection))
                {
                    RuntimeResourceCatalogEntryDocument aliasEntry = CreateRuntimeCatalogEntryFromAliasSelection(
                        packageId,
                        characterStableId,
                        entry,
                        authoringResources,
                        diagnostics);
                    if (aliasEntry != null)
                    {
                        document.Entries.Add(aliasEntry);
                        continue;
                    }
                }

                if (!IsRuntimeCatalogLoadable(entry))
                    continue;

                CharacterPackageResourceMappingEntry mapping;
                mappingsByKey.TryGetValue(key, out mapping);
                document.Entries.Add(CreateRuntimeCatalogEntry(packageId, packageRootPath, entry, mapping, entriesByKey, mappingsByKey));
            }

            document.Entries.Sort((a, b) => string.CompareOrdinal(a.Id, b.Id));
            return document;
        }

        private static RuntimeResourceCatalogEntryDocument CreateRuntimeCatalogEntryFromAliasSelection(
            string packageId,
            string characterStableId,
            CharacterPackageResourceEntry entry,
            AuthoringResourceCollection authoringResources,
            List<CharacterResourcePlanDiagnostic> diagnostics)
        {
            if (entry == null || !HasResourceSelection(entry.ResourceSelection) || authoringResources == null)
                return null;

            var input = new AuthoringResourceSelectionCompileInput
            {
                SourceConfigKind = "resourceCatalog",
                SourceStableId = entry.StableId ?? string.Empty,
                SourceField = "resourceSelection",
                FieldSpec = CreateDefaultBodyModelFieldSpec(),
                Context = new AuthoringResourceConsumerContext
                {
                    ConsumerKind = "CharacterStudio",
                    ConsumerStableId = characterStableId ?? string.Empty,
                    ScopeId = packageId ?? string.Empty,
                    PackageId = packageId ?? string.Empty,
                    Metadata = new Dictionary<string, string>
                    {
                        ["packageResourceKey"] = entry.ResourceKey ?? string.Empty,
                        ["localId"] = entry.LocalId ?? string.Empty
                    }
                },
                Selection = entry.ResourceSelection
            };

            var selectionService = new AuthoringResourceSelectionService();
            AuthoringResourceSelectionResolutionResult resolution = selectionService.Resolve(
                authoringResources,
                input.FieldSpec,
                input.Context,
                input.Selection);
            AddSelectionDiagnostics(diagnostics, input, resolution);
            if (resolution == null || !resolution.Accepted || resolution.Item == null || resolution.Selection == null)
                return null;
            if (resolution.Selection.BindingKind != AuthoringResourceBindingKind.ResourceManagerAsset ||
                string.IsNullOrWhiteSpace(resolution.Selection.RuntimeResourceKey))
                return null;

            AuthoringResourceProviderBinding binding = FindSelectionBinding(resolution.Item, resolution.Selection);
            var providerData = binding != null && binding.ProviderData != null
                ? new Dictionary<string, string>(binding.ProviderData, StringComparer.Ordinal)
                : new Dictionary<string, string>(StringComparer.Ordinal);
            providerData["packageResourceKey"] = entry.ResourceKey ?? string.Empty;
            providerData["aliasOfRuntimeResourceKey"] = resolution.Selection.RuntimeResourceKey ?? string.Empty;
            providerData["authoringResourceStableId"] = resolution.Item.StableId ?? string.Empty;
            providerData["authoringResourceId"] = resolution.Item.ResourceId ?? string.Empty;
            providerData["sourceProviderId"] = resolution.Item.SourceProviderId ?? string.Empty;
            providerData["selectionSourceConfigKind"] = input.SourceConfigKind ?? string.Empty;
            providerData["selectionSourceField"] = input.SourceField ?? string.Empty;
            providerData["usage"] = FirstNonEmpty(entry.Usage, resolution.Item.Usage);
            CopyMetadata(entry, providerData);

            string providerId = FirstNonEmpty(GetMetadata(resolution.Item, "providerId"), GetProviderData(binding, "providerId"), binding != null ? binding.ProviderId : string.Empty, "memory");
            if (string.Equals(providerId, AuthoringResourceProviderIds.RuntimeCatalog, StringComparison.Ordinal))
                providerId = "memory";

            var labels = new SortedSet<string>(BuildLabels(packageId, entry), StringComparer.Ordinal);
            foreach (string label in BuildSelectionLabels(packageId, resolution.Item, input))
                labels.Add(label);

            return new RuntimeResourceCatalogEntryDocument
            {
                Id = entry.ResourceKey ?? string.Empty,
                Type = MapAuthoringKindToRuntimeType(resolution.Item.Kind, binding != null ? binding.AssetType : string.Empty),
                Variant = FirstNonEmpty(entry.Variant, GetMetadata(resolution.Item, "variant")),
                PackageId = packageId ?? string.Empty,
                Provider = providerId,
                Address = FirstNonEmpty(binding != null ? binding.Address : string.Empty, GetMetadata(resolution.Item, "address"), resolution.Selection.RuntimeResourceKey),
                Labels = new List<string>(labels),
                Hash = FirstNonEmpty(binding != null ? binding.Hash : string.Empty, GetMetadata(resolution.Item, "hash"), resolution.Selection.ExpectedHash, CharacterPackageResourcePipeline.GetDeclaredContentHash(entry)),
                Size = ParseLong(GetMetadata(resolution.Item, "size")),
                AllowOverride = string.Equals(GetMetadata(resolution.Item, "allowOverride"), "true", StringComparison.OrdinalIgnoreCase),
                ProviderData = providerData
            };
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
            bool extractsAnimationClip = CharacterGeneratedAnimationClipAssetPaths.RequiresExtraction(entry);
            string sourceAssetPath = mapping != null ? mapping.ImportTargetPath ?? string.Empty : string.Empty;
            string runtimeAssetPath = extractsAnimationClip
                ? CharacterGeneratedAnimationClipAssetPaths.Resolve(
                    packageId,
                    mapping != null && !string.IsNullOrWhiteSpace(mapping.ProjectResourceKey)
                        ? mapping.ProjectResourceKey
                        : entry.ResourceKey)
                : sourceAssetPath;
            string address = !string.IsNullOrWhiteSpace(runtimeAssetPath)
                ? runtimeAssetPath
                : entry.RelativePath;
            string provider = GetRuntimeProviderId(entry);
            var providerData = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["packageResourceKey"] = entry.ResourceKey ?? string.Empty,
                ["stableId"] = entry.StableId ?? string.Empty,
                ["sourceRelativePath"] = entry.RelativePath ?? string.Empty,
                ["sourceFormat"] = extractsAnimationClip
                    ? CharacterPackageResourceFormatIds.Anim
                    : CharacterPackageResourcePipeline.GetEffectiveSourceFormat(entry),
                ["authoringSourceFormat"] = CharacterPackageResourcePipeline.GetEffectiveSourceFormat(entry),
                ["usage"] = entry.Usage ?? string.Empty,
                ["requestedProviderId"] = entry.ImportHints != null ? entry.ImportHints.ProviderId ?? string.Empty : string.Empty
            };

            if (mapping != null)
            {
                providerData["assetPath"] = runtimeAssetPath;
                providerData["unityAssetPath"] = runtimeAssetPath;
                if (extractsAnimationClip)
                    providerData["sourceFbxAssetPath"] = sourceAssetPath;
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

        private static void ApplyAuthoringResourceSelections(
            CharacterResourcePlanCompileRequest request,
            string packageId,
            string characterStableId,
            RuntimeResourceCatalogDocument runtimeCatalog,
            AudioCueManifestDocument audioManifest,
            CharacterResourcePlanDocument plan,
            List<CharacterResourcePlanDiagnostic> diagnostics)
        {
            if (request == null ||
                request.AuthoringResources == null)
                return;

            List<AuthoringResourceSelectionCompileInput> selections = BuildResourceSelectionInputs(request);
            if (selections.Count == 0)
                return;

            var selectionService = new AuthoringResourceSelectionService();
            for (int i = 0; i < selections.Count; i++)
            {
                AuthoringResourceSelectionCompileInput input = selections[i];
                if (input == null)
                    continue;

                AuthoringResourceSelectionResolutionResult resolution = selectionService.Resolve(
                    request.AuthoringResources,
                    input.FieldSpec,
                    input.Context,
                    input.Selection);

                AddSelectionDiagnostics(diagnostics, input, resolution);
                if (resolution == null || !resolution.Accepted || resolution.Item == null || resolution.Selection == null)
                    continue;

                if (resolution.Selection.BindingKind == AuthoringResourceBindingKind.AudioCue ||
                    resolution.Selection.BindingKind == AuthoringResourceBindingKind.AudioEventDefinition)
                {
                    AddAudioSelection(packageId, characterStableId, audioManifest, plan, resolution.Item, resolution.Selection, input);
                    continue;
                }

                if (resolution.Selection.BindingKind != AuthoringResourceBindingKind.ResourceManagerAsset)
                    continue;

                string runtimeResourceKey = resolution.Selection.RuntimeResourceKey;
                if (string.IsNullOrWhiteSpace(runtimeResourceKey))
                {
                    AddDiagnostic(
                        diagnostics,
                        "Error",
                        CharacterResourcePlanValidationCodes.LibraryNotRuntimeLoadable,
                        resolution.Item.StableId,
                        string.Empty,
                        FirstNonEmpty(input.SourceConfigKind, input.Context != null ? input.Context.ConsumerKind : string.Empty, "resourceSelection"),
                        FirstNonEmpty(input.SourceField, input.FieldSpec != null ? input.FieldSpec.FieldKey : string.Empty, "selection"),
                        "Resolved ResourceSelectionRef does not contain a runtime resource key.",
                        "Select a runtime-ready ResourceManager asset or re-run resource import and plan compilation.");
                    continue;
                }

                RuntimeResourceCatalogEntryDocument runtimeEntry = EnsureRuntimeCatalogEntryFromSelection(runtimeCatalog, packageId, runtimeResourceKey, resolution.Item, resolution.Selection, input);
                CharacterResourcePlanGroup group = SelectGroupByPreloadPolicy(plan, input.FieldSpec != null ? input.FieldSpec.PreloadPolicy : string.Empty, resolution.Item);
                AddResourceToGroup(group, runtimeEntry, resolution.Item);
            }

            SortRuntimeCatalog(runtimeCatalog);
            SortGroup(plan.SpawnCritical);
            SortGroup(plan.PresentationCritical);
            SortGroup(plan.EquipmentInitial);
            SortGroup(plan.AnimationWarmup);
            SortGroup(plan.VfxWarmup);
            SortGroup(plan.UiDeferred);
            SortAudioPlan(plan.Audio);
            SortAudioManifest(audioManifest);
        }

        private static List<AuthoringResourceSelectionCompileInput> BuildResourceSelectionInputs(CharacterResourcePlanCompileRequest request)
        {
            var result = new List<AuthoringResourceSelectionCompileInput>();
            if (request == null)
                return result;

            if (request.ResourceSelectionManifest != null && request.ResourceSelectionManifest.Selections != null)
                result.AddRange(request.ResourceSelectionManifest.Selections);

            CharacterApplicationAuthoringSummary application = request.Package != null ? request.Package.ApplicationConfig : null;
            List<SingleWeaponConfiguration> singles = request.Package?.GlobalWeaponLibrary?.SingleWeaponConfigurations;
            if (application == null || singles == null)
                return result;

            string packageId = request.Package != null && request.Package.Manifest != null ? request.Package.Manifest.PackageId : string.Empty;
            string stableId = request.Package != null && request.Package.Manifest != null ? request.Package.Manifest.StableId : string.Empty;
            for (int i = 0; i < singles.Count; i++)
            {
                SingleWeaponConfiguration definition = singles[i];
                if (definition == null || !IsSingleWeaponReferenced(application, definition.SingleWeaponConfigurationId) ||
                    !HasResourceSelection(definition.WeaponRef))
                    continue;
                if (IsPackageResourceSelection(definition.WeaponRef))
                    continue;

                string slot = definition.AllowedSlots != null && definition.AllowedSlots.Count > 0
                    ? definition.AllowedSlots[0]
                    : "weapon";
                result.Add(new AuthoringResourceSelectionCompileInput
                {
                    SourceConfigKind = "singleWeaponConfiguration",
                    SourceStableId = FirstNonEmpty(definition.SingleWeaponConfigurationId,
                        "singleWeaponConfigurations/" + i.ToString(CultureInfo.InvariantCulture)),
                    SourceField = "weaponRef",
                    FieldSpec = CreateDefaultWeaponFieldSpec(slot),
                    Context = new AuthoringResourceConsumerContext
                    {
                        ConsumerKind = "CharacterStudio",
                        ConsumerStableId = stableId,
                        ScopeId = packageId,
                        PackageId = packageId,
                        PackagePath = request.PackageRootPath ?? string.Empty,
                        SlotId = slot,
                        Metadata = new Dictionary<string, string>
                        {
                            ["singleWeaponConfigurationId"] = definition.SingleWeaponConfigurationId ?? string.Empty
                        }
                    },
                    Selection = definition.WeaponRef
                });
            }

            AddReferencedGlobalAnimationSelections(result, request, application, packageId, stableId);
            AddReferencedGlobalReactionSelections(result, request, application, packageId, stableId);

            return result;
        }

        private static void AddReferencedGlobalAnimationSelections(
            List<AuthoringResourceSelectionCompileInput> result,
            CharacterResourcePlanCompileRequest request,
            CharacterApplicationAuthoringSummary application,
            string packageId,
            string stableId)
        {
            var referenced = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; application?.CombinedWeaponConfigurations != null && i < application.CombinedWeaponConfigurations.Count; i++)
                if (!string.IsNullOrWhiteSpace(application.CombinedWeaponConfigurations[i]?.AnimationConfigurationId))
                    referenced.Add(application.CombinedWeaponConfigurations[i].AnimationConfigurationId);
            List<GlobalAnimationConfiguration> configurations = request.Package?.GlobalWeaponLibrary?.AnimationConfigurations;
            for (int configurationIndex = 0; configurations != null && configurationIndex < configurations.Count; configurationIndex++)
            {
                GlobalAnimationConfiguration configuration = configurations[configurationIndex];
                if (configuration == null || !referenced.Contains(configuration.AnimationConfigurationId))
                    continue;
                for (int setIndex = 0; configuration.Sets != null && setIndex < configuration.Sets.Count; setIndex++)
                {
                    AnimationAuthoringSet set = configuration.Sets[setIndex];
                    for (int groupIndex = 0; set?.Groups != null && groupIndex < set.Groups.Count; groupIndex++)
                    {
                        AnimationGroupAuthoring group = set.Groups[groupIndex];
                        for (int clipIndex = 0; group?.Clips != null && clipIndex < group.Clips.Count; clipIndex++)
                        {
                            AnimationClipMappingAuthoring clip = group.Clips[clipIndex];
                            AddGlobalBehaviorSelection(
                                result,
                                request,
                                packageId,
                                stableId,
                                "animationConfiguration",
                                configuration.AnimationConfigurationId,
                                "sets/" + setIndex + "/groups/" + groupIndex + "/clips/" + clipIndex + "/sourceSelection",
                                clip?.SourceSelection,
                                AuthoringResourcePreloadPolicies.AnimationWarmup);
                        }
                    }
                }
            }
        }

        private static void AddReferencedGlobalReactionSelections(
            List<AuthoringResourceSelectionCompileInput> result,
            CharacterResourcePlanCompileRequest request,
            CharacterApplicationAuthoringSummary application,
            string packageId,
            string stableId)
        {
            var referenced = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; application?.CombinedWeaponConfigurations != null && i < application.CombinedWeaponConfigurations.Count; i++)
                if (!string.IsNullOrWhiteSpace(application.CombinedWeaponConfigurations[i]?.ReactionConfigurationId))
                    referenced.Add(application.CombinedWeaponConfigurations[i].ReactionConfigurationId);
            List<GlobalReactionConfiguration> configurations = request.Package?.GlobalWeaponLibrary?.ReactionConfigurations;
            for (int configurationIndex = 0; configurations != null && configurationIndex < configurations.Count; configurationIndex++)
            {
                GlobalReactionConfiguration configuration = configurations[configurationIndex];
                if (configuration == null || !referenced.Contains(configuration.ReactionConfigurationId))
                    continue;
                for (int slotIndex = 0; configuration.Slots != null && slotIndex < configuration.Slots.Count; slotIndex++)
                {
                    GlobalReactionSlotConfiguration slot = configuration.Slots[slotIndex];
                    AddGlobalBehaviorSelection(
                        result,
                        request,
                        packageId,
                        stableId,
                        "reactionConfiguration",
                        configuration.ReactionConfigurationId,
                        "slots/" + slotIndex + "/resourceSelection",
                        slot?.ResourceSelection,
                        FirstNonEmpty(slot?.PreloadPolicy, AuthoringResourcePreloadPolicies.AnimationWarmup));
                }
            }
        }

        private static void AddGlobalBehaviorSelection(
            List<AuthoringResourceSelectionCompileInput> result,
            CharacterResourcePlanCompileRequest request,
            string packageId,
            string characterStableId,
            string sourceKind,
            string sourceId,
            string sourceField,
            AuthoringResourceSelectionRef selection,
            string preloadPolicy)
        {
            if (!HasResourceSelection(selection) || IsPackageResourceSelection(selection))
                return;
            result.Add(new AuthoringResourceSelectionCompileInput
            {
                SourceConfigKind = sourceKind,
                SourceStableId = sourceId,
                SourceField = sourceField,
                FieldSpec = new AuthoringResourceFieldSpec
                {
                    FieldKey = sourceKind + "." + sourceField,
                    EditorKind = "CharacterStudio",
                    DisplayName = sourceKind,
                    AcceptedKinds = new List<string> { "Animation", "animation", CharacterPackageResourceTypeIds.Animation },
                    AcceptedUsages = new List<string> { "animationClip", CharacterPackageResourceUsageIds.AnimationClipGroup },
                    AcceptedProviderIds = new List<string>
                    {
                        AuthoringResourceProviderIds.RuntimeCatalog,
                        AuthoringResourceProviderIds.ResourceMapping,
                        AuthoringResourceProviderIds.UnityProjectAssets
                    },
                    AcceptedBindingKinds = new List<AuthoringResourceBindingKind>
                    {
                        AuthoringResourceBindingKind.ResourceManagerAsset
                    },
                    PreloadPolicy = preloadPolicy
                },
                Context = new AuthoringResourceConsumerContext
                {
                    ConsumerKind = "CharacterStudio",
                    ConsumerStableId = characterStableId,
                    ScopeId = packageId,
                    PackageId = packageId,
                    PackagePath = request.PackageRootPath ?? string.Empty
                },
                Selection = selection
            });
        }

        private static AuthoringResourceFieldSpec CreateDefaultWeaponFieldSpec(string slot)
        {
            return new AuthoringResourceFieldSpec
            {
                FieldKey = "Equipment." + FirstNonEmpty(slot, "weapon") + ".weaponRef",
                EditorKind = "CharacterStudio",
                DisplayName = "Default weapon",
                AcceptedKinds = new List<string> { CharacterPackageResourceTypeIds.Model },
                AcceptedUsages = new List<string> { CharacterPackageResourceUsageIds.WeaponModel },
                AcceptedProviderIds = new List<string>
                {
                    AuthoringResourceProviderIds.RuntimeCatalog
                    , AuthoringResourceProviderIds.ResourceMapping
                },
                AcceptedBindingKinds = new List<AuthoringResourceBindingKind>
                {
                    AuthoringResourceBindingKind.ResourceManagerAsset
                },
                RequireRuntimeLoadable = true,
                RequireUnityImported = true,
                AllowIncompatibleWithWarning = false,
                CompatibilityFilter = new AuthoringResourceCompatibility { SlotId = slot ?? string.Empty },
                PreloadPolicy = AuthoringResourcePreloadPolicies.EquipmentInitial,
                OutputKind = AuthoringResourceSelectionOutputKind.ResourceSelectionRef
            };
        }

        private static AuthoringResourceFieldSpec CreateDefaultBodyModelFieldSpec()
        {
            return new AuthoringResourceFieldSpec
            {
                FieldKey = "Character.Model.body.resourceSelection",
                EditorKind = "CharacterStudio",
                DisplayName = "Character body model",
                AcceptedKinds = new List<string> { CharacterPackageResourceTypeIds.Model },
                AcceptedUsages = new List<string> { CharacterPackageResourceUsageIds.CharacterModel },
                AcceptedProviderIds = new List<string>
                {
                    AuthoringResourceProviderIds.RuntimeCatalog
                    , AuthoringResourceProviderIds.ResourceMapping
                },
                AcceptedBindingKinds = new List<AuthoringResourceBindingKind>
                {
                    AuthoringResourceBindingKind.ResourceManagerAsset
                },
                RequireRuntimeLoadable = true,
                RequireUnityImported = true,
                AllowIncompatibleWithWarning = false,
                PreloadPolicy = AuthoringResourcePreloadPolicies.SpawnCritical,
                OutputKind = AuthoringResourceSelectionOutputKind.ResourceSelectionRef
            };
        }

        private static void AddSelectionDiagnostics(
            List<CharacterResourcePlanDiagnostic> diagnostics,
            AuthoringResourceSelectionCompileInput input,
            AuthoringResourceSelectionResolutionResult resolution)
        {
            if (resolution == null || resolution.Reasons == null)
                return;

            for (int i = 0; i < resolution.Reasons.Count; i++)
            {
                AuthoringResourceSelectionReason reason = resolution.Reasons[i];
                if (reason == null)
                    continue;

                AddDiagnostic(
                    diagnostics,
                    reason.Severity.ToString(),
                    MapSelectionReasonCode(reason.Code),
                    FirstNonEmpty(reason.ResourceStableId, resolution.Selection != null ? resolution.Selection.ResourceStableId : string.Empty),
                    resolution.Selection != null ? FirstNonEmpty(resolution.Selection.RuntimeResourceKey, resolution.Selection.PackageResourceKey, resolution.Selection.ProviderResourceKey) : string.Empty,
                    FirstNonEmpty(input.SourceConfigKind, input.Context != null ? input.Context.ConsumerKind : string.Empty, "resourceSelection"),
                    FirstNonEmpty(input.SourceField, reason.FieldKey, input.FieldSpec != null ? input.FieldSpec.FieldKey : string.Empty),
                    reason.Message,
                    reason.SuggestedFix);
            }
        }

        private static string MapSelectionReasonCode(string code)
        {
            if (string.Equals(code, AuthoringResourceSelectionReasonCodes.ItemMissing, StringComparison.Ordinal))
                return CharacterResourcePlanValidationCodes.LibraryItemMissing;
            if (string.Equals(code, AuthoringResourceSelectionReasonCodes.NotRuntimeLoadable, StringComparison.Ordinal))
                return CharacterResourcePlanValidationCodes.LibraryNotRuntimeLoadable;
            if (string.Equals(code, AuthoringResourceSelectionReasonCodes.EditorOnlySelectedForRuntime, StringComparison.Ordinal))
                return AuthoringResourceDiagnosticCodes.EditorOnlySelectedForRuntime;
            if (string.Equals(code, AuthoringResourceSelectionReasonCodes.BindingUnavailable, StringComparison.Ordinal))
                return CharacterResourcePlanValidationCodes.LibraryNotRuntimeLoadable;

            return string.IsNullOrWhiteSpace(code) ? CharacterResourcePlanValidationCodes.PlanRequiredResourceMissing : code;
        }

        private static RuntimeResourceCatalogEntryDocument EnsureRuntimeCatalogEntryFromSelection(
            RuntimeResourceCatalogDocument runtimeCatalog,
            string packageId,
            string runtimeResourceKey,
            AuthoringResourceItem item,
            AuthoringResourceSelectionRef selection,
            AuthoringResourceSelectionCompileInput input)
        {
            RuntimeResourceCatalogEntryDocument existing = FindRuntimeCatalogEntry(runtimeCatalog, runtimeResourceKey);
            if (existing != null)
                return existing;

            AuthoringResourceProviderBinding binding = FindSelectionBinding(item, selection);
            var providerData = binding != null && binding.ProviderData != null
                ? new Dictionary<string, string>(binding.ProviderData, StringComparer.Ordinal)
                : new Dictionary<string, string>(StringComparer.Ordinal);
            providerData["authoringResourceStableId"] = item.StableId ?? string.Empty;
            providerData["authoringResourceId"] = item.ResourceId ?? string.Empty;
            providerData["sourceProviderId"] = item.SourceProviderId ?? string.Empty;
            providerData["selectionSourceConfigKind"] = input != null ? input.SourceConfigKind ?? string.Empty : string.Empty;
            providerData["selectionSourceField"] = input != null ? input.SourceField ?? string.Empty : string.Empty;
            providerData["usage"] = item.Usage ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(selection.PackageResourceKey))
                providerData["packageResourceKey"] = selection.PackageResourceKey;

            string providerId = FirstNonEmpty(GetMetadata(item, "providerId"), GetProviderData(binding, "providerId"), binding != null ? binding.ProviderId : string.Empty, "memory");
            if (string.Equals(providerId, AuthoringResourceProviderIds.RuntimeCatalog, StringComparison.Ordinal))
                providerId = "memory";
            string resourcePackageId = FirstNonEmpty(GetMetadata(item, "packageId"), packageId);

            var entry = new RuntimeResourceCatalogEntryDocument
            {
                Id = runtimeResourceKey ?? string.Empty,
                Type = MapAuthoringKindToRuntimeType(item != null ? item.Kind : string.Empty, binding != null ? binding.AssetType : string.Empty),
                Variant = GetMetadata(item, "variant"),
                PackageId = resourcePackageId ?? string.Empty,
                Provider = providerId,
                Address = FirstNonEmpty(binding != null ? binding.Address : string.Empty, GetMetadata(item, "address"), runtimeResourceKey),
                Labels = BuildSelectionLabels(resourcePackageId, item, input),
                Hash = FirstNonEmpty(binding != null ? binding.Hash : string.Empty, GetMetadata(item, "hash"), selection.ExpectedHash),
                Size = ParseLong(GetMetadata(item, "size")),
                AllowOverride = string.Equals(GetMetadata(item, "allowOverride"), "true", StringComparison.OrdinalIgnoreCase),
                ProviderData = providerData
            };
            runtimeCatalog.Entries.Add(entry);
            return entry;
        }

        private static void AddAudioSelection(
            string packageId,
            string characterStableId,
            AudioCueManifestDocument audioManifest,
            CharacterResourcePlanDocument plan,
            AuthoringResourceItem item,
            AuthoringResourceSelectionRef selection,
            AuthoringResourceSelectionCompileInput input)
        {
            AuthoringResourceProviderBinding binding = FindSelectionBinding(item, selection);
            string cueId = FirstNonEmpty(selection.AudioCueId, GetProviderData(binding, "audioCueId"), selection.AudioEventDefinitionId, GetProviderData(binding, "audioEventDefinitionId"), selection.ProviderResourceKey, item.StableId);
            if (string.IsNullOrWhiteSpace(cueId))
                return;

            AudioCueManifestEntry cue = FindAudioCue(audioManifest, cueId);
            if (cue == null)
            {
                var providerData = binding != null && binding.ProviderData != null
                    ? new Dictionary<string, string>(binding.ProviderData, StringComparer.Ordinal)
                    : new Dictionary<string, string>(StringComparer.Ordinal);
                providerData["authoringResourceStableId"] = item.StableId ?? string.Empty;
                providerData["authoringResourceId"] = item.ResourceId ?? string.Empty;
                providerData["sourceProviderId"] = item.SourceProviderId ?? string.Empty;
                providerData["selectionSourceConfigKind"] = input != null ? input.SourceConfigKind ?? string.Empty : string.Empty;
                providerData["selectionSourceField"] = input != null ? input.SourceField ?? string.Empty : string.Empty;

                cue = new AudioCueManifestEntry
                {
                    CueId = cueId,
                    StableId = item.StableId ?? string.Empty,
                    ResourceKey = selection.PackageResourceKey ?? string.Empty,
                    EventPath = FirstNonEmpty(binding != null ? binding.FmodEventPath : string.Empty, GetProviderData(binding, "fmodEventPath"), GetMetadata(item, "fmodEventPath")),
                    Bank = FirstBank(FirstNonEmpty(GetProviderData(binding, "bank"), GetProviderData(binding, "banks"), GetMetadata(item, "bank"), GetMetadata(item, "banks"))),
                    ProviderData = providerData
                };
                audioManifest.Cues.Add(cue);
            }

            AddUnique(plan.Audio.RequiredCues, cueId);
            AddUnique(audioManifest.Banks, cue.Bank);
            AddCsvValuesToList(plan.Audio.RequiredBanks, FirstNonEmpty(cue.Bank, GetProviderData(binding, "banks"), GetMetadata(item, "banks")));
        }

        private static RuntimeResourceCatalogEntryDocument FindRuntimeCatalogEntry(RuntimeResourceCatalogDocument runtimeCatalog, string runtimeResourceKey)
        {
            if (runtimeCatalog == null || runtimeCatalog.Entries == null || string.IsNullOrWhiteSpace(runtimeResourceKey))
                return null;

            for (int i = 0; i < runtimeCatalog.Entries.Count; i++)
            {
                RuntimeResourceCatalogEntryDocument entry = runtimeCatalog.Entries[i];
                if (entry != null && string.Equals(entry.Id, runtimeResourceKey, StringComparison.Ordinal))
                    return entry;
            }

            return null;
        }

        private static AudioCueManifestEntry FindAudioCue(AudioCueManifestDocument manifest, string cueId)
        {
            if (manifest == null || manifest.Cues == null || string.IsNullOrWhiteSpace(cueId))
                return null;

            for (int i = 0; i < manifest.Cues.Count; i++)
            {
                AudioCueManifestEntry entry = manifest.Cues[i];
                if (entry != null && string.Equals(entry.CueId, cueId, StringComparison.Ordinal))
                    return entry;
            }

            return null;
        }

        private static CharacterResourcePlanGroup SelectGroupByPreloadPolicy(CharacterResourcePlanDocument plan, string preloadPolicy, AuthoringResourceItem item)
        {
            if (string.Equals(preloadPolicy, AuthoringResourcePreloadPolicies.SpawnCritical, StringComparison.Ordinal))
                return plan.SpawnCritical;
            if (string.Equals(preloadPolicy, AuthoringResourcePreloadPolicies.PresentationCritical, StringComparison.Ordinal))
                return plan.PresentationCritical;
            if (string.Equals(preloadPolicy, AuthoringResourcePreloadPolicies.EquipmentInitial, StringComparison.Ordinal))
                return plan.EquipmentInitial;
            if (string.Equals(preloadPolicy, AuthoringResourcePreloadPolicies.AnimationWarmup, StringComparison.Ordinal))
                return plan.AnimationWarmup;
            if (string.Equals(preloadPolicy, AuthoringResourcePreloadPolicies.VfxWarmup, StringComparison.Ordinal))
                return plan.VfxWarmup;
            if (string.Equals(preloadPolicy, AuthoringResourcePreloadPolicies.UiDeferred, StringComparison.Ordinal))
                return plan.UiDeferred;

            if (item != null)
            {
                if (string.Equals(item.Usage, CharacterPackageResourceUsageIds.CharacterModel, StringComparison.Ordinal))
                    return plan.SpawnCritical;
                if (string.Equals(item.Usage, CharacterPackageResourceUsageIds.WeaponModel, StringComparison.Ordinal))
                    return plan.EquipmentInitial;
                if (string.Equals(item.Usage, CharacterPackageResourceUsageIds.AnimationClipGroup, StringComparison.Ordinal) ||
                    string.Equals(item.Kind, CharacterPackageResourceTypeIds.Animation, StringComparison.Ordinal))
                    return plan.AnimationWarmup;
                if (string.Equals(item.Usage, CharacterPackageResourceUsageIds.VfxCue, StringComparison.Ordinal) ||
                    string.Equals(item.Kind, CharacterPackageResourceTypeIds.Vfx, StringComparison.Ordinal))
                    return plan.VfxWarmup;
                if (string.Equals(item.Usage, CharacterPackageResourceUsageIds.PreviewThumbnail, StringComparison.Ordinal) ||
                    string.Equals(item.Usage, CharacterPackageResourceUsageIds.PreviewMesh, StringComparison.Ordinal) ||
                    string.Equals(item.Kind, CharacterPackageResourceTypeIds.Preview, StringComparison.Ordinal))
                    return plan.UiDeferred;
            }

            return plan.PresentationCritical;
        }

        private static void AddResourceToGroup(CharacterResourcePlanGroup group, RuntimeResourceCatalogEntryDocument runtimeEntry, AuthoringResourceItem item)
        {
            if (group == null || runtimeEntry == null || string.IsNullOrWhiteSpace(runtimeEntry.Id))
                return;

            for (int i = 0; i < group.Resources.Count; i++)
            {
                CharacterResourcePlanResourceRef existing = group.Resources[i];
                if (existing != null && string.Equals(existing.ResourceKey, runtimeEntry.Id, StringComparison.Ordinal))
                    return;
            }

            group.Resources.Add(new CharacterResourcePlanResourceRef
            {
                ResourceKey = runtimeEntry.Id,
                TypeId = runtimeEntry.Type,
                Variant = runtimeEntry.Variant,
                PackageId = runtimeEntry.PackageId,
                Usage = item != null ? item.Usage ?? string.Empty : string.Empty,
                StableId = item != null ? item.StableId ?? string.Empty : string.Empty
            });
        }

        private static AuthoringResourceProviderBinding FindSelectionBinding(AuthoringResourceItem item, AuthoringResourceSelectionRef selection)
        {
            if (item == null || item.ProviderBindings == null || item.ProviderBindings.Count == 0)
                return null;

            for (int i = 0; i < item.ProviderBindings.Count; i++)
            {
                AuthoringResourceProviderBinding binding = item.ProviderBindings[i];
                if (binding == null)
                    continue;
                if (selection != null && binding.BindingKind != selection.BindingKind)
                    continue;
                if (!string.IsNullOrWhiteSpace(selection != null ? selection.RuntimeResourceKey : string.Empty) && string.Equals(binding.RuntimeResourceKey, selection.RuntimeResourceKey, StringComparison.Ordinal))
                    return binding;
                if (!string.IsNullOrWhiteSpace(selection != null ? selection.ProviderResourceKey : string.Empty) && string.Equals(binding.ProviderResourceKey, selection.ProviderResourceKey, StringComparison.Ordinal))
                    return binding;
                if (!string.IsNullOrWhiteSpace(selection != null ? selection.PackageResourceKey : string.Empty) && string.Equals(binding.PackageResourceKey, selection.PackageResourceKey, StringComparison.Ordinal))
                    return binding;
                if (!string.IsNullOrWhiteSpace(selection != null ? selection.AudioCueId : string.Empty) && binding.ProviderData != null && string.Equals(GetProviderData(binding, "audioCueId"), selection.AudioCueId, StringComparison.Ordinal))
                    return binding;
                if (!string.IsNullOrWhiteSpace(selection != null ? selection.AudioEventDefinitionId : string.Empty) && binding.ProviderData != null && string.Equals(GetProviderData(binding, "audioEventDefinitionId"), selection.AudioEventDefinitionId, StringComparison.Ordinal))
                    return binding;
            }

            for (int i = 0; i < item.ProviderBindings.Count; i++)
            {
                AuthoringResourceProviderBinding binding = item.ProviderBindings[i];
                if (binding != null && binding.IsPrimary)
                    return binding;
            }

            return item.ProviderBindings[0];
        }

        private static string MapAuthoringKindToRuntimeType(string kind, string assetType)
        {
            if (!string.IsNullOrWhiteSpace(assetType))
            {
                if (assetType.EndsWith(".GameObject", StringComparison.Ordinal) || string.Equals(assetType, "GameObject", StringComparison.Ordinal))
                    return "GameObject";
                if (assetType.EndsWith(".Texture2D", StringComparison.Ordinal) || string.Equals(assetType, "Texture2D", StringComparison.Ordinal))
                    return "Texture2D";
                if (assetType.EndsWith(".Material", StringComparison.Ordinal) || string.Equals(assetType, "Material", StringComparison.Ordinal))
                    return "Material";
                if (assetType.EndsWith(".AnimationClip", StringComparison.Ordinal) || string.Equals(assetType, "AnimationClip", StringComparison.Ordinal))
                    return "AnimationClip";
                if (assetType.EndsWith(".AudioClip", StringComparison.Ordinal) || string.Equals(assetType, "AudioClip", StringComparison.Ordinal))
                    return "AudioClip";
                if (assetType.EndsWith(".TextAsset", StringComparison.Ordinal) || string.Equals(assetType, "TextAsset", StringComparison.Ordinal))
                    return "TextAsset";
            }

            return MapRuntimeTypeId(kind);
        }

        private static List<string> BuildSelectionLabels(string packageId, AuthoringResourceItem item, AuthoringResourceSelectionCompileInput input)
        {
            var labels = new SortedSet<string>(StringComparer.Ordinal);
            labels.Add("package." + (packageId ?? string.Empty));
            labels.Add("authoring.resourceSelection");
            if (item != null)
            {
                if (!string.IsNullOrWhiteSpace(item.Kind))
                    labels.Add("character.type." + NormalizeLabelSegment(item.Kind));
                if (!string.IsNullOrWhiteSpace(item.Usage))
                    labels.Add("character.usage." + NormalizeLabelSegment(item.Usage));
                if (item.Tags != null)
                {
                    for (int i = 0; i < item.Tags.Count; i++)
                        labels.Add("tag." + NormalizeLabelSegment(item.Tags[i]));
                }
            }

            if (input != null && input.FieldSpec != null && !string.IsNullOrWhiteSpace(input.FieldSpec.PreloadPolicy))
                labels.Add("preload." + NormalizeLabelSegment(input.FieldSpec.PreloadPolicy));

            return new List<string>(labels);
        }

        private static void SortRuntimeCatalog(RuntimeResourceCatalogDocument runtimeCatalog)
        {
            if (runtimeCatalog == null || runtimeCatalog.Entries == null)
                return;

            runtimeCatalog.Entries.Sort((a, b) => string.CompareOrdinal(a != null ? a.Id : string.Empty, b != null ? b.Id : string.Empty));
        }

        private static void SortAudioPlan(CharacterAudioResourcePlanGroup audio)
        {
            if (audio == null)
                return;

            SortUnique(audio.RequiredCues);
            SortUnique(audio.RequiredBanks);
        }

        private static void SortAudioManifest(AudioCueManifestDocument manifest)
        {
            if (manifest == null)
                return;

            SortUnique(manifest.Banks);
            if (manifest.Cues != null)
                manifest.Cues.Sort((a, b) => string.CompareOrdinal(a != null ? a.CueId : string.Empty, b != null ? b.CueId : string.Empty));
        }

        private static void SortUnique(List<string> values)
        {
            if (values == null)
                return;

            var set = new SortedSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < values.Count; i++)
            {
                if (!string.IsNullOrWhiteSpace(values[i]))
                    set.Add(values[i]);
            }

            values.Clear();
            values.AddRange(set);
        }

        private static void AddUnique(List<string> values, string value)
        {
            if (values == null || string.IsNullOrWhiteSpace(value))
                return;

            for (int i = 0; i < values.Count; i++)
            {
                if (string.Equals(values[i], value, StringComparison.Ordinal))
                    return;
            }

            values.Add(value);
        }

        private static void AddCsvValuesToList(List<string> target, string csv)
        {
            if (target == null || string.IsNullOrWhiteSpace(csv))
                return;

            string[] values = csv.Split(',');
            for (int i = 0; i < values.Length; i++)
                AddUnique(target, values[i].Trim());
        }

        private static string FirstBank(string csv)
        {
            if (string.IsNullOrWhiteSpace(csv))
                return string.Empty;

            string[] values = csv.Split(',');
            for (int i = 0; i < values.Length; i++)
            {
                string value = values[i].Trim();
                if (!string.IsNullOrWhiteSpace(value))
                    return value;
            }

            return string.Empty;
        }

        private static long ParseLong(string value)
        {
            long parsed;
            return long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out parsed) ? parsed : 0;
        }

        private static string GetProviderData(AuthoringResourceProviderBinding binding, string key)
        {
            if (binding == null || binding.ProviderData == null || string.IsNullOrWhiteSpace(key))
                return string.Empty;

            string value;
            return binding.ProviderData.TryGetValue(key, out value) ? value ?? string.Empty : string.Empty;
        }

        private static string GetMetadata(AuthoringResourceItem item, string key)
        {
            if (item == null || item.Metadata == null || string.IsNullOrWhiteSpace(key))
                return string.Empty;

            string value;
            return item.Metadata.TryGetValue(key, out value) ? value ?? string.Empty : string.Empty;
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
                // “列表为空则选全部”分支同样必须应用导出用途分类，
                // 否则专用预览 GLB 会绕过 mapping 直接进入运行计划。
                foreach (string key in entriesByKey.Keys)
                {
                    CharacterPackageResourceEntry candidate;
                    if (entriesByKey.TryGetValue(key, out candidate)
                        && CharacterPackageResourceExportPolicy.IsAuthoringPreviewGlb(candidate))
                    {
                        continue;
                    }

                    keys.Add(key);
                }

                return keys;
            }

            for (int i = 0; i < configuredKeys.Count; i++)
            {
                string key = configuredKeys[i];
                if (string.IsNullOrWhiteSpace(key))
                    continue;

                CharacterPackageResourceEntry configured;
                if (entriesByKey.TryGetValue(key, out configured)
                    && CharacterPackageResourceExportPolicy.IsAuthoringPreviewGlb(configured))
                {
                    // 源键存在但属制作侧专用预览：从运行集合剔除，不报缺失。
                    continue;
                }

                keys.Add(key);
                if (!entriesByKey.ContainsKey(key))
                    AddDiagnostic(diagnostics, "Error", CharacterResourcePlanValidationCodes.LibraryItemMissing, string.Empty, key, "character", "resourceKeys", "Character application config references a resource key that is missing from resource_catalog.json.", "Import the resource, fix the resource key, or remove the reference.");
            }

            AddDefaultWeaponAttachmentKeys(package, entriesByKey, keys);

            var images = package?.ApplicationConfig?.PresentationImages;
            if (images != null)
            {
                AddImagePlanKey(images.PortraitResourceKey, entriesByKey, keys, diagnostics);
                AddImagePlanKey(images.StandingResourceKey, entriesByKey, keys, diagnostics);
                for (int i = 0; images.Variants != null && i < images.Variants.Count; i++)
                {
                    var variant = images.Variants[i];
                    if (variant == null) continue;
                    AddImagePlanKey(variant.PortraitResourceKey, entriesByKey, keys, diagnostics);
                    AddImagePlanKey(variant.StandingResourceKey, entriesByKey, keys, diagnostics);
                }
            }

            return keys;
        }

        private static void AddImagePlanKey(string key, Dictionary<string, CharacterPackageResourceEntry> entriesByKey,
            SortedSet<string> keys, List<CharacterResourcePlanDiagnostic> diagnostics)
        {
            if (string.IsNullOrWhiteSpace(key)) return;
            keys.Add(key);
            if (!entriesByKey.ContainsKey(key))
                AddDiagnostic(diagnostics, "Error", CharacterResourcePlanValidationCodes.LibraryItemMissing, string.Empty, key,
                    "character", "presentationImages", "Display image key is missing from resource_catalog.json.", "Repair or clear the display image reference.");
        }

        private static void AddDefaultWeaponAttachmentKeys(CharacterResourcePackage package, Dictionary<string, CharacterPackageResourceEntry> entriesByKey, SortedSet<string> keys)
        {
            CharacterApplicationAuthoringSummary application = package != null ? package.ApplicationConfig : null;
            List<SingleWeaponConfiguration> singles = package?.GlobalWeaponLibrary?.SingleWeaponConfigurations;
            if (application == null || singles == null || entriesByKey == null || keys == null)
                return;

            for (int i = 0; i < singles.Count; i++)
            {
                SingleWeaponConfiguration definition = singles[i];
                if (definition == null || !IsSingleWeaponReferenced(application, definition.SingleWeaponConfigurationId))
                    continue;
                string key = GetWeaponDefinitionPackageFallbackKey(definition);
                if (!string.IsNullOrWhiteSpace(key) && entriesByKey.ContainsKey(key))
                    keys.Add(key);
            }
        }

        private static string GetWeaponDefinitionPackageFallbackKey(SingleWeaponConfiguration definition)
        {
            if (definition == null)
                return string.Empty;

            if (HasResourceSelection(definition.WeaponRef))
                return definition.WeaponRef.PackageResourceKey ?? string.Empty;

            return string.Empty;
        }

        private static bool IsSingleWeaponReferenced(
            CharacterApplicationAuthoringSummary application,
            string singleWeaponConfigurationId)
        {
            List<CombinedWeaponConfigurationAuthoringSummary> configurations =
                application?.CombinedWeaponConfigurations;
            for (int i = 0; configurations != null && i < configurations.Count; i++)
            {
                List<CombinedWeaponConfigurationSlotAuthoringSummary> slots = configurations[i]?.Slots;
                for (int slotIndex = 0; slots != null && slotIndex < slots.Count; slotIndex++)
                    if (slots[slotIndex] != null && string.Equals(
                        slots[slotIndex].SingleWeaponConfigurationId,
                        singleWeaponConfigurationId,
                        StringComparison.Ordinal))
                        return true;
            }
            return false;
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
                        if (dependency == null || string.IsNullOrWhiteSpace(dependency.ResourceKey))
                            continue;

                        // 依赖展开同样不得把专用预览 GLB 重新加回运行集合。
                        CharacterPackageResourceEntry dependencyEntry;
                        if (entriesByKey.TryGetValue(dependency.ResourceKey, out dependencyEntry)
                            && CharacterPackageResourceExportPolicy.IsAuthoringPreviewGlb(dependencyEntry))
                        {
                            continue;
                        }

                        if (keys.Add(dependency.ResourceKey))
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
                string.Equals(usage, "characterPortrait", StringComparison.Ordinal) ||
                string.Equals(usage, "characterStanding", StringComparison.Ordinal) ||
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

        private static bool HasResourceSelection(AuthoringResourceSelectionRef selection)
        {
            return selection != null &&
                (!string.IsNullOrWhiteSpace(selection.ResourceStableId) ||
                 !string.IsNullOrWhiteSpace(selection.RuntimeResourceKey) ||
                 !string.IsNullOrWhiteSpace(selection.ProviderResourceKey) ||
                 !string.IsNullOrWhiteSpace(selection.PackageResourceKey) ||
                 !string.IsNullOrWhiteSpace(selection.UnityGuid) ||
                 !string.IsNullOrWhiteSpace(selection.UnityAssetPath));
        }

        private static bool IsPackageResourceSelection(AuthoringResourceSelectionRef selection)
        {
            return selection != null &&
                !string.IsNullOrWhiteSpace(selection.PackageResourceKey) &&
                (selection.BindingKind == AuthoringResourceBindingKind.PackageResource ||
                 string.Equals(selection.SourceProviderId, AuthoringResourceProviderIds.CharacterPackage, StringComparison.Ordinal));
        }

        private static string FirstNonEmpty(params string[] values)
        {
            if (values == null)
                return string.Empty;

            for (int i = 0; i < values.Length; i++)
            {
                if (!string.IsNullOrWhiteSpace(values[i]))
                    return values[i];
            }

            return string.Empty;
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
