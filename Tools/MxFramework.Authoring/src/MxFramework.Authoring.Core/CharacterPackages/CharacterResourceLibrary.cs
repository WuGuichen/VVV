using System;
using System.Collections.Generic;

namespace MxFramework.Authoring
{
    public enum RuntimeBindingKind
    {
        None = 0,
        ResourceManagerAsset = 1,
        UnityEditorOnlyAsset = 2,
        AudioEventDefinition = 3,
        AudioCue = 4,
        GeneratedPreviewOnly = 5
    }

    public enum ResourceRuntimeAvailability
    {
        Unknown = 0,
        RuntimeReady = 1,
        RuntimeMissing = 2,
        EditorOnly = 3,
        PreviewOnly = 4,
        AudioCueOnly = 5,
        NotRuntimeLoadable = 6
    }

    public enum ResourceImportStatus
    {
        New = 0,
        Clean = 1,
        SourceChanged = 2,
        UnityMissing = 3,
        ImportFailed = 4,
        Conflict = 5,
        ManualOverride = 6,
        OrphanCandidate = 7
    }

    public enum ResourceLibrarySourceKind
    {
        Unknown = 0,
        ExternalFile = 1,
        UnityAsset = 2,
        FmodLibrary = 3,
        GeneratedAsset = 4
    }

    public enum ResourceSelectionOutputKind
    {
        Unknown = 0,
        ResourceKey = 1,
        AudioCueId = 2,
        AudioEventDefinitionId = 3,
        LibraryItemStableId = 4
    }

    public static class ResourceLibraryDiagnosticCodes
    {
        public const string ItemMissing = "RES_LIBRARY_ITEM_MISSING";
        public const string StableIdDuplicate = "RES_LIBRARY_STABLE_ID_DUPLICATE";
        public const string ResourceKeyDuplicate = "RES_LIBRARY_RESOURCE_KEY_DUPLICATE";
        public const string KindUsageMismatch = "RES_LIBRARY_KIND_USAGE_MISMATCH";
        public const string SourceFileMissing = "RES_LIBRARY_SOURCE_FILE_MISSING";
        public const string HashMismatch = "RES_LIBRARY_HASH_MISMATCH";
        public const string UnityAssetMissing = "RES_LIBRARY_UNITY_ASSET_MISSING";
        public const string NotRuntimeLoadable = "RES_LIBRARY_NOT_RUNTIME_LOADABLE";
        public const string EditorOnlySelectedForRuntime = "RES_LIBRARY_EDITOR_ONLY_SELECTED_FOR_RUNTIME";
        public const string FmodEventMissing = "RES_LIBRARY_FMOD_EVENT_MISSING";
        public const string FmodGuidPathMismatch = "RES_LIBRARY_FMOD_GUID_PATH_MISMATCH";
        public const string FmodBankMissing = "RES_LIBRARY_FMOD_BANK_MISSING";
        public const string FmodParameterMismatch = "RES_LIBRARY_FMOD_PARAMETER_MISMATCH";
        public const string CompatibilitySkeletonMismatch = "RES_LIBRARY_COMPAT_SKELETON_MISMATCH";
        public const string CompatibilitySlotMismatch = "RES_LIBRARY_COMPAT_SLOT_MISMATCH";
        public const string OrphanCandidate = "RES_LIBRARY_ORPHAN_CANDIDATE";
        public const string ReferenceBroken = "RES_LIBRARY_REFERENCE_BROKEN";
        public const string PlanRequiredResourceMissing = "RES_LIBRARY_PLAN_REQUIRED_RESOURCE_MISSING";
    }

    public static class ResourceLibraryPreloadPolicies
    {
        public const string None = "None";
        public const string SpawnCritical = "SpawnCritical";
        public const string PresentationCritical = "PresentationCritical";
        public const string EquipmentInitial = "EquipmentInitial";
        public const string AnimationWarmup = "AnimationWarmup";
        public const string VfxWarmup = "VfxWarmup";
        public const string UiDeferred = "UiDeferred";
        public const string Audio = "Audio";
        public const string AudioBank = "AudioBank";
    }

    public sealed class ResourceSelectionRef
    {
        public string LibraryItemStableId { get; set; } = string.Empty;
        public RuntimeBindingKind BindingKind { get; set; } = RuntimeBindingKind.None;
        public string ExpectedKind { get; set; } = string.Empty;
        public string ExpectedUsage { get; set; } = string.Empty;
        public string ExpectedHash { get; set; } = string.Empty;
        public string ResourceKey { get; set; } = string.Empty;
        public string ProviderId { get; set; } = string.Empty;
        public string Hash { get; set; } = string.Empty;
        public string AudioCueId { get; set; } = string.Empty;
        public string AudioEventDefinitionId { get; set; } = string.Empty;
    }

    public sealed class ResourceFieldSpec
    {
        public string FieldKey { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public List<string> AcceptedKinds { get; set; } = new List<string>();
        public List<string> AcceptedUsages { get; set; } = new List<string>();
        public List<RuntimeBindingKind> AcceptedBindingKinds { get; set; } = new List<RuntimeBindingKind>();
        public bool RequireRuntimeLoadable { get; set; }
        public bool RequireUnityImported { get; set; }
        public bool AllowIncompatibleWithWarning { get; set; }
        public ResourceCompatibilityFilter CompatibilityFilter { get; set; } = new ResourceCompatibilityFilter();
        public string PreloadPolicy { get; set; } = ResourceLibraryPreloadPolicies.None;
        public ResourceSelectionOutputKind OutputKind { get; set; } = ResourceSelectionOutputKind.Unknown;
    }

    public sealed class ResourceCompatibilityFilter
    {
        public string SkeletonStableId { get; set; } = string.Empty;
        public string AvatarStableId { get; set; } = string.Empty;
        public string BodyKind { get; set; } = string.Empty;
        public string SlotId { get; set; } = string.Empty;
        public string WeaponClass { get; set; } = string.Empty;
        public string CoordinateConvention { get; set; } = string.Empty;
        public Dictionary<string, string> Metadata { get; set; } = new Dictionary<string, string>();
    }

    public sealed class ResourceLibraryCompatibility
    {
        public string SkeletonStableId { get; set; } = string.Empty;
        public string AvatarStableId { get; set; } = string.Empty;
        public string BodyKind { get; set; } = string.Empty;
        public string SlotId { get; set; } = string.Empty;
        public string WeaponClass { get; set; } = string.Empty;
        public string CoordinateConvention { get; set; } = string.Empty;
        public Dictionary<string, string> Metadata { get; set; } = new Dictionary<string, string>();
    }

    public sealed class ResourceLibraryPreview
    {
        public string ThumbnailResourceKey { get; set; } = string.Empty;
        public string PreviewMeshResourceKey { get; set; } = string.Empty;
        public string PreviewCameraPresetId { get; set; } = string.Empty;
        public string PreviewPoseId { get; set; } = string.Empty;
        public bool IsPlaceholder { get; set; }
    }

    public sealed class ResourceLibraryDiagnostic
    {
        public CharacterAuthoringValidationSeverity Severity { get; set; }
        public string Code { get; set; } = string.Empty;
        public string LibraryItemStableId { get; set; } = string.Empty;
        public string ResourceKey { get; set; } = string.Empty;
        public string SourceConfigKind { get; set; } = string.Empty;
        public string SourceStableId { get; set; } = string.Empty;
        public string SourceField { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public string SuggestedFix { get; set; } = string.Empty;
    }

    public sealed class ResourceLibraryItem
    {
        public string LibraryItemId { get; set; } = string.Empty;
        public string StableId { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string Kind { get; set; } = string.Empty;
        public string Usage { get; set; } = string.Empty;
        public ResourceLibrarySourceKind SourceKind { get; set; } = ResourceLibrarySourceKind.Unknown;
        public RuntimeBindingKind RuntimeBindingKind { get; set; } = RuntimeBindingKind.None;
        public ResourceLibraryCompatibility Compatibility { get; set; } = new ResourceLibraryCompatibility();
        public ResourceLibraryPreview Preview { get; set; } = new ResourceLibraryPreview();
        public ResourceImportStatus ImportStatus { get; set; } = ResourceImportStatus.New;
        public ResourceRuntimeAvailability RuntimeAvailability { get; set; } = ResourceRuntimeAvailability.Unknown;
        public string ResourceKey { get; set; } = string.Empty;
        public string ProviderId { get; set; } = string.Empty;
        public string Hash { get; set; } = string.Empty;
        public string SourcePath { get; set; } = string.Empty;
        public string UnityAssetPath { get; set; } = string.Empty;
        public string FmodEventPath { get; set; } = string.Empty;
        public string AudioCueId { get; set; } = string.Empty;
        public string AudioEventDefinitionId { get; set; } = string.Empty;
        public List<string> Tags { get; set; } = new List<string>();
        public List<ResourceLibraryDiagnostic> Diagnostics { get; set; } = new List<ResourceLibraryDiagnostic>();
    }

    public sealed class ResourceReferenceEdge
    {
        public string SourceConfigKind { get; set; } = string.Empty;
        public string SourceStableId { get; set; } = string.Empty;
        public string SourceField { get; set; } = string.Empty;
        public string TargetLibraryItemStableId { get; set; } = string.Empty;
        public string TargetResourceKey { get; set; } = string.Empty;
        public RuntimeBindingKind BindingKind { get; set; } = RuntimeBindingKind.None;
        public bool IsRequiredAtRuntime { get; set; }
        public string PreloadPolicy { get; set; } = ResourceLibraryPreloadPolicies.None;
    }

    public sealed class ResourceReferenceGraph
    {
        public string SchemaVersion { get; set; } = "1.0";
        public List<ResourceReferenceEdge> Edges { get; set; } = new List<ResourceReferenceEdge>();

        public int CountReferencesToStableId(string stableId)
        {
            if (string.IsNullOrWhiteSpace(stableId))
                return 0;

            int count = 0;
            for (int i = 0; i < Edges.Count; i++)
            {
                ResourceReferenceEdge edge = Edges[i];
                if (edge != null && string.Equals(edge.TargetLibraryItemStableId, stableId, StringComparison.Ordinal))
                    count++;
            }

            return count;
        }
    }

    public sealed class ResourceSelectionResolutionResult
    {
        public bool Accepted { get; set; }
        public ResourceLibraryItem Item { get; set; }
        public ResourceSelectionRef Selection { get; set; } = new ResourceSelectionRef();
        public List<ResourceLibraryDiagnostic> Diagnostics { get; set; } = new List<ResourceLibraryDiagnostic>();
    }

    public sealed class CharacterResourceLibrary
    {
        public string SchemaVersion { get; set; } = "1.0";
        public string PackageId { get; set; } = string.Empty;
        public List<ResourceLibraryItem> Items { get; set; } = new List<ResourceLibraryItem>();
        public ResourceReferenceGraph ReferenceGraph { get; set; } = new ResourceReferenceGraph();
        public List<ResourceLibraryDiagnostic> Diagnostics { get; set; } = new List<ResourceLibraryDiagnostic>();
    }

    public static class CharacterResourceLibraryBuilder
    {
        public static CharacterResourceLibrary FromPackageResourceCatalog(CharacterPackageResourceCatalog catalog)
        {
            var library = new CharacterResourceLibrary();
            if (catalog == null)
                return library;

            for (int i = 0; i < catalog.Entries.Count; i++)
            {
                CharacterPackageResourceEntry entry = catalog.Entries[i];
                if (entry == null)
                    continue;

                if (string.IsNullOrWhiteSpace(library.PackageId))
                    library.PackageId = entry.PackageId ?? string.Empty;

                library.Items.Add(FromPackageResourceEntry(entry));
            }

            library.Diagnostics.AddRange(ValidateLibrary(library));
            return library;
        }

        public static ResourceLibraryItem FromPackageResourceEntry(CharacterPackageResourceEntry entry)
        {
            if (entry == null)
                return new ResourceLibraryItem();

            string contentHash = CharacterPackageResourcePipeline.GetDeclaredContentHash(entry);
            var item = new ResourceLibraryItem
            {
                LibraryItemId = !string.IsNullOrWhiteSpace(entry.ResourceKey) ? entry.ResourceKey : entry.StableId,
                StableId = entry.StableId ?? string.Empty,
                DisplayName = !string.IsNullOrWhiteSpace(entry.LocalId) ? entry.LocalId : entry.ResourceKey,
                Kind = entry.TypeId ?? string.Empty,
                Usage = entry.Usage ?? string.Empty,
                SourceKind = ResourceLibrarySourceKind.ExternalFile,
                RuntimeBindingKind = RuntimeBindingKind.ResourceManagerAsset,
                ImportStatus = ResourceImportStatus.New,
                RuntimeAvailability = ResourceRuntimeAvailability.Unknown,
                ResourceKey = entry.ResourceKey ?? string.Empty,
                ProviderId = entry.ImportHints != null ? entry.ImportHints.ProviderId : string.Empty,
                Hash = contentHash,
                SourcePath = entry.RelativePath ?? string.Empty,
                Tags = entry.Tags != null ? new List<string>(entry.Tags) : new List<string>()
            };

            if (entry.Preview != null)
            {
                item.Preview.ThumbnailResourceKey = entry.Preview.ThumbnailResourceKey ?? string.Empty;
                item.Preview.PreviewMeshResourceKey = entry.Preview.PreviewMeshResourceKey ?? string.Empty;
                item.Preview.PreviewCameraPresetId = entry.Preview.PreviewCameraPresetId ?? string.Empty;
                item.Preview.IsPlaceholder = entry.Preview.IsPlaceholder;
            }

            if (string.Equals(entry.TypeId, CharacterPackageResourceTypeIds.Preview, StringComparison.OrdinalIgnoreCase))
            {
                item.RuntimeBindingKind = RuntimeBindingKind.GeneratedPreviewOnly;
                item.RuntimeAvailability = ResourceRuntimeAvailability.PreviewOnly;
            }

            return item;
        }

        public static ResourceReferenceGraph BuildReferenceGraph(IEnumerable<ResourceReferenceEdge> edges)
        {
            var graph = new ResourceReferenceGraph();
            if (edges == null)
                return graph;

            foreach (ResourceReferenceEdge edge in edges)
            {
                if (edge != null)
                    graph.Edges.Add(edge);
            }

            return graph;
        }

        public static List<ResourceLibraryDiagnostic> ValidateLibrary(CharacterResourceLibrary library)
        {
            var diagnostics = new List<ResourceLibraryDiagnostic>();
            if (library == null)
                return diagnostics;

            var stableIds = new HashSet<string>(StringComparer.Ordinal);
            var resourceKeys = new HashSet<string>(StringComparer.Ordinal);
            var referencedStableIds = new HashSet<string>(StringComparer.Ordinal);

            for (int i = 0; i < library.Items.Count; i++)
            {
                ResourceLibraryItem item = library.Items[i];
                if (item == null)
                    continue;

                if (!string.IsNullOrWhiteSpace(item.StableId) && !stableIds.Add(item.StableId))
                    diagnostics.Add(CreateDiagnostic(CharacterAuthoringValidationSeverity.Error, ResourceLibraryDiagnosticCodes.StableIdDuplicate, item.StableId, item.ResourceKey, "", "", "", "library item stable id must be unique.", "Remove or rename the duplicate library item."));

                if (item.RuntimeBindingKind == RuntimeBindingKind.ResourceManagerAsset && !string.IsNullOrWhiteSpace(item.ResourceKey) && !resourceKeys.Add(item.ResourceKey))
                    diagnostics.Add(CreateDiagnostic(CharacterAuthoringValidationSeverity.Error, ResourceLibraryDiagnosticCodes.ResourceKeyDuplicate, item.StableId, item.ResourceKey, "", "", "", "runtime resource key must be unique for ResourceManager assets.", "Keep one runtime asset per resource key."));
            }

            bool shouldDetectOrphans = library.ReferenceGraph != null && library.ReferenceGraph.Edges.Count > 0;
            if (library.ReferenceGraph != null)
            {
                for (int i = 0; i < library.ReferenceGraph.Edges.Count; i++)
                {
                    ResourceReferenceEdge edge = library.ReferenceGraph.Edges[i];
                    if (edge == null)
                        continue;

                    if (!string.IsNullOrWhiteSpace(edge.TargetLibraryItemStableId))
                        referencedStableIds.Add(edge.TargetLibraryItemStableId);

                    if (!string.IsNullOrWhiteSpace(edge.TargetLibraryItemStableId) && !stableIds.Contains(edge.TargetLibraryItemStableId))
                        diagnostics.Add(CreateDiagnostic(CharacterAuthoringValidationSeverity.Error, ResourceLibraryDiagnosticCodes.ReferenceBroken, edge.TargetLibraryItemStableId, edge.TargetResourceKey, edge.SourceConfigKind, edge.SourceStableId, edge.SourceField, "reference graph edge targets a missing library item.", "Update the selection or restore the referenced library item."));
                }
            }

            if (!shouldDetectOrphans)
                return diagnostics;

            for (int i = 0; i < library.Items.Count; i++)
            {
                ResourceLibraryItem item = library.Items[i];
                if (item == null || string.IsNullOrWhiteSpace(item.StableId))
                    continue;

                if (!referencedStableIds.Contains(item.StableId))
                    diagnostics.Add(CreateDiagnostic(CharacterAuthoringValidationSeverity.Warning, ResourceLibraryDiagnosticCodes.OrphanCandidate, item.StableId, item.ResourceKey, "", "", "", "library item has no incoming references.", "Keep it as reusable library content or mark it for cleanup."));
            }

            return diagnostics;
        }

        public static ResourceSelectionResolutionResult ResolveSelection(CharacterResourceLibrary library, ResourceFieldSpec spec, ResourceSelectionRef selection)
        {
            var result = new ResourceSelectionResolutionResult();
            if (selection != null)
                result.Selection = selection;

            ResourceLibraryItem item = FindItemByStableId(library, selection != null ? selection.LibraryItemStableId : string.Empty);
            result.Item = item;
            if (item == null)
            {
                result.Diagnostics.Add(CreateDiagnostic(CharacterAuthoringValidationSeverity.Error, ResourceLibraryDiagnosticCodes.ItemMissing, selection != null ? selection.LibraryItemStableId : string.Empty, selection != null ? selection.ResourceKey : string.Empty, "", "", spec != null ? spec.FieldKey : string.Empty, "selected library item does not exist.", "Choose an existing library item or repair the saved selection reference."));
                result.Accepted = false;
                return result;
            }

            ValidateSelectionSpec(result.Diagnostics, item, spec, selection);
            result.Accepted = !HasError(result.Diagnostics);
            if (result.Accepted)
                FillCompiledSelection(result.Selection, item);
            return result;
        }

        private static ResourceLibraryItem FindItemByStableId(CharacterResourceLibrary library, string stableId)
        {
            if (library == null || string.IsNullOrWhiteSpace(stableId))
                return null;

            for (int i = 0; i < library.Items.Count; i++)
            {
                ResourceLibraryItem item = library.Items[i];
                if (item != null && string.Equals(item.StableId, stableId, StringComparison.Ordinal))
                    return item;
            }

            return null;
        }

        private static void ValidateSelectionSpec(List<ResourceLibraryDiagnostic> diagnostics, ResourceLibraryItem item, ResourceFieldSpec spec, ResourceSelectionRef selection)
        {
            if (diagnostics == null || item == null || spec == null)
                return;

            string sourceField = spec.FieldKey ?? string.Empty;
            if (spec.AcceptedKinds != null && spec.AcceptedKinds.Count > 0 && !ContainsOrdinal(spec.AcceptedKinds, item.Kind))
                diagnostics.Add(CreateDiagnostic(CharacterAuthoringValidationSeverity.Error, ResourceLibraryDiagnosticCodes.KindUsageMismatch, item.StableId, item.ResourceKey, "", "", sourceField, "library item kind is not accepted by this field.", "Pick a resource with an accepted kind."));

            if (spec.AcceptedUsages != null && spec.AcceptedUsages.Count > 0 && !ContainsOrdinal(spec.AcceptedUsages, item.Usage))
                diagnostics.Add(CreateDiagnostic(CharacterAuthoringValidationSeverity.Error, ResourceLibraryDiagnosticCodes.KindUsageMismatch, item.StableId, item.ResourceKey, "", "", sourceField, "library item usage is not accepted by this field.", "Pick a resource with an accepted usage."));

            if (spec.AcceptedBindingKinds != null && spec.AcceptedBindingKinds.Count > 0 && !spec.AcceptedBindingKinds.Contains(item.RuntimeBindingKind))
                diagnostics.Add(CreateDiagnostic(CharacterAuthoringValidationSeverity.Error, ResourceLibraryDiagnosticCodes.KindUsageMismatch, item.StableId, item.ResourceKey, "", "", sourceField, "runtime binding kind is not accepted by this field.", "Pick a resource with an accepted runtime binding kind."));

            RuntimeBindingKind requestedBinding = selection != null ? selection.BindingKind : RuntimeBindingKind.None;
            if (requestedBinding != RuntimeBindingKind.None && requestedBinding != item.RuntimeBindingKind)
                diagnostics.Add(CreateDiagnostic(CharacterAuthoringValidationSeverity.Error, ResourceLibraryDiagnosticCodes.KindUsageMismatch, item.StableId, item.ResourceKey, "", "", sourceField, "saved selection binding kind no longer matches the library item.", "Re-select the resource so the binding kind is refreshed."));

            if (spec.RequireRuntimeLoadable && !IsRuntimeLoadable(item))
            {
                string code = item.RuntimeBindingKind == RuntimeBindingKind.UnityEditorOnlyAsset
                    ? ResourceLibraryDiagnosticCodes.EditorOnlySelectedForRuntime
                    : ResourceLibraryDiagnosticCodes.NotRuntimeLoadable;
                diagnostics.Add(CreateDiagnostic(CharacterAuthoringValidationSeverity.Error, code, item.StableId, item.ResourceKey, "", "", sourceField, "field requires a runtime-loadable resource but the selected item is not runtime-ready.", "Import or generate a runtime resource, or choose another item."));
            }

            if (spec.RequireUnityImported && item.ImportStatus != ResourceImportStatus.Clean && item.ImportStatus != ResourceImportStatus.ManualOverride)
                diagnostics.Add(CreateDiagnostic(CharacterAuthoringValidationSeverity.Error, ResourceLibraryDiagnosticCodes.UnityAssetMissing, item.StableId, item.ResourceKey, "", "", sourceField, "field requires a Unity imported asset but import status is not clean.", "Run Unity import or repair the imported asset."));

            ValidateCompatibility(diagnostics, item, spec, sourceField);
        }

        private static void ValidateCompatibility(List<ResourceLibraryDiagnostic> diagnostics, ResourceLibraryItem item, ResourceFieldSpec spec, string sourceField)
        {
            if (spec.CompatibilityFilter == null || item.Compatibility == null)
                return;

            CharacterAuthoringValidationSeverity severity = spec.AllowIncompatibleWithWarning
                ? CharacterAuthoringValidationSeverity.Warning
                : CharacterAuthoringValidationSeverity.Error;

            if (!MatchesFilter(spec.CompatibilityFilter.SkeletonStableId, item.Compatibility.SkeletonStableId))
                diagnostics.Add(CreateDiagnostic(severity, ResourceLibraryDiagnosticCodes.CompatibilitySkeletonMismatch, item.StableId, item.ResourceKey, "", "", sourceField, "library item skeleton is incompatible with this field.", "Choose a resource matching the requested skeleton."));

            if (!MatchesFilter(spec.CompatibilityFilter.SlotId, item.Compatibility.SlotId))
                diagnostics.Add(CreateDiagnostic(severity, ResourceLibraryDiagnosticCodes.CompatibilitySlotMismatch, item.StableId, item.ResourceKey, "", "", sourceField, "library item slot is incompatible with this field.", "Choose a resource matching the requested slot."));
        }

        private static void FillCompiledSelection(ResourceSelectionRef selection, ResourceLibraryItem item)
        {
            if (selection == null || item == null)
                return;

            selection.LibraryItemStableId = item.StableId;
            selection.BindingKind = item.RuntimeBindingKind;
            selection.ExpectedKind = item.Kind;
            selection.ExpectedUsage = item.Usage;
            selection.Hash = item.Hash;
            if (item.RuntimeBindingKind == RuntimeBindingKind.ResourceManagerAsset)
            {
                selection.ResourceKey = item.ResourceKey;
                selection.ProviderId = item.ProviderId;
            }
            else if (item.RuntimeBindingKind == RuntimeBindingKind.AudioCue)
            {
                selection.AudioCueId = item.AudioCueId;
            }
            else if (item.RuntimeBindingKind == RuntimeBindingKind.AudioEventDefinition)
            {
                selection.AudioEventDefinitionId = item.AudioEventDefinitionId;
            }
        }

        private static bool IsRuntimeLoadable(ResourceLibraryItem item)
        {
            if (item == null)
                return false;

            if (item.RuntimeBindingKind == RuntimeBindingKind.ResourceManagerAsset)
                return item.RuntimeAvailability == ResourceRuntimeAvailability.RuntimeReady;
            if (item.RuntimeBindingKind == RuntimeBindingKind.AudioCue ||
                item.RuntimeBindingKind == RuntimeBindingKind.AudioEventDefinition)
                return item.RuntimeAvailability == ResourceRuntimeAvailability.AudioCueOnly ||
                       item.RuntimeAvailability == ResourceRuntimeAvailability.RuntimeReady;

            return false;
        }

        private static bool ContainsOrdinal(List<string> values, string value)
        {
            for (int i = 0; i < values.Count; i++)
            {
                if (string.Equals(values[i], value, StringComparison.Ordinal))
                    return true;
            }

            return false;
        }

        private static bool MatchesFilter(string expected, string actual)
        {
            return string.IsNullOrWhiteSpace(expected) || string.Equals(expected, actual, StringComparison.Ordinal);
        }

        private static bool HasError(List<ResourceLibraryDiagnostic> diagnostics)
        {
            if (diagnostics == null)
                return false;

            for (int i = 0; i < diagnostics.Count; i++)
            {
                if (diagnostics[i] != null && diagnostics[i].Severity == CharacterAuthoringValidationSeverity.Error)
                    return true;
            }

            return false;
        }

        private static ResourceLibraryDiagnostic CreateDiagnostic(
            CharacterAuthoringValidationSeverity severity,
            string code,
            string libraryItemStableId,
            string resourceKey,
            string sourceConfigKind,
            string sourceStableId,
            string sourceField,
            string message,
            string suggestedFix)
        {
            return new ResourceLibraryDiagnostic
            {
                Severity = severity,
                Code = code ?? string.Empty,
                LibraryItemStableId = libraryItemStableId ?? string.Empty,
                ResourceKey = resourceKey ?? string.Empty,
                SourceConfigKind = sourceConfigKind ?? string.Empty,
                SourceStableId = sourceStableId ?? string.Empty,
                SourceField = sourceField ?? string.Empty,
                Message = message ?? string.Empty,
                SuggestedFix = suggestedFix ?? string.Empty
            };
        }
    }
}
