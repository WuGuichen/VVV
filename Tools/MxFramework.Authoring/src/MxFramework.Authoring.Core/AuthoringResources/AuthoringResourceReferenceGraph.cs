using System;
using System.Collections.Generic;

namespace MxFramework.Authoring
{
    public sealed class AuthoringResourceReferenceEdge
    {
        public string SourceConsumerKind { get; set; } = string.Empty;
        public string SourceConfigKind { get; set; } = string.Empty;
        public string SourceStableId { get; set; } = string.Empty;
        public string SourceField { get; set; } = string.Empty;
        public string TargetResourceId { get; set; } = string.Empty;
        public string TargetStableId { get; set; } = string.Empty;
        public string TargetLibraryItemStableId { get; set; } = string.Empty;
        public string TargetProviderId { get; set; } = string.Empty;
        public string TargetProviderResourceKey { get; set; } = string.Empty;
        public string TargetRuntimeResourceKey { get; set; } = string.Empty;
        public string TargetResourceKey { get; set; } = string.Empty;
        public AuthoringResourceBindingKind BindingKind { get; set; } = AuthoringResourceBindingKind.None;
        public bool IsRequiredAtRuntime { get; set; }
        public string PreloadPolicy { get; set; } = AuthoringResourcePreloadPolicies.None;
    }

    public sealed class AuthoringResourceReferenceGraph
    {
        public string SchemaVersion { get; set; } = "1.0";
        public List<AuthoringResourceReferenceEdge> Edges { get; set; } = new List<AuthoringResourceReferenceEdge>();
        public List<AuthoringResourceDiagnostic> Diagnostics { get; set; } = new List<AuthoringResourceDiagnostic>();

        public int CountReferencesToStableId(string stableId)
        {
            if (string.IsNullOrWhiteSpace(stableId))
                return 0;

            int count = 0;
            for (int i = 0; i < Edges.Count; i++)
            {
                AuthoringResourceReferenceEdge edge = Edges[i];
                if (edge != null &&
                    (string.Equals(edge.TargetStableId, stableId, StringComparison.Ordinal) ||
                     string.Equals(edge.TargetLibraryItemStableId, stableId, StringComparison.Ordinal)))
                    count++;
            }

            return count;
        }

        public int CountReferencesToResourceId(string resourceId)
        {
            if (string.IsNullOrWhiteSpace(resourceId))
                return 0;

            int count = 0;
            for (int i = 0; i < Edges.Count; i++)
            {
                AuthoringResourceReferenceEdge edge = Edges[i];
                if (edge != null && string.Equals(edge.TargetResourceId, resourceId, StringComparison.Ordinal))
                    count++;
            }

            return count;
        }

        public List<AuthoringResourceReferenceEdge> FindReferencesToResource(AuthoringResourceItem item)
        {
            var references = new List<AuthoringResourceReferenceEdge>();
            if (item == null)
                return references;

            for (int i = 0; i < Edges.Count; i++)
            {
                AuthoringResourceReferenceEdge edge = Edges[i];
                if (edge != null && AuthoringResourceReferenceGraphBuilder.EdgeTargetsItem(edge, item))
                    references.Add(edge);
            }

            return references;
        }

        public bool HasIncomingReferences(AuthoringResourceItem item)
        {
            return item != null && FindReferencesToResource(item).Count > 0;
        }
    }

    public static class AuthoringResourceReferenceGraphBuilder
    {
        public static AuthoringResourceReferenceGraph FromCharacterPackage(
            CharacterResourcePackage package,
            AuthoringResourceCollection collection)
        {
            var graph = new AuthoringResourceReferenceGraph();
            if (collection == null)
                return graph;

            ResourceIndex index = ResourceIndex.FromCollection(collection);
            AddCharacterApplicationReferences(graph, index, package);
            AddWeaponAttachmentReferences(graph, index, package);
            AddResourceCatalogReferences(graph, index, package);
            AddDiagnostics(graph, collection, index);
            return graph;
        }

        public static bool EdgeTargetsItem(AuthoringResourceReferenceEdge edge, AuthoringResourceItem item)
        {
            if (edge == null || item == null)
                return false;

            if (!string.IsNullOrWhiteSpace(edge.TargetResourceId) &&
                string.Equals(edge.TargetResourceId, item.ResourceId, StringComparison.Ordinal))
                return true;

            if (!string.IsNullOrWhiteSpace(edge.TargetStableId) &&
                string.Equals(edge.TargetStableId, item.StableId, StringComparison.Ordinal))
                return true;

            if (!string.IsNullOrWhiteSpace(edge.TargetLibraryItemStableId) &&
                string.Equals(edge.TargetLibraryItemStableId, item.StableId, StringComparison.Ordinal))
                return true;

            if (!string.IsNullOrWhiteSpace(edge.TargetProviderResourceKey) &&
                ItemHasBindingValue(item, edge.TargetProviderResourceKey))
                return true;

            if (!string.IsNullOrWhiteSpace(edge.TargetRuntimeResourceKey) &&
                ItemHasBindingValue(item, edge.TargetRuntimeResourceKey))
                return true;

            if (!string.IsNullOrWhiteSpace(edge.TargetResourceKey) &&
                ItemHasBindingValue(item, edge.TargetResourceKey))
                return true;

            return false;
        }

        private static void AddCharacterApplicationReferences(
            AuthoringResourceReferenceGraph graph,
            ResourceIndex index,
            CharacterResourcePackage package)
        {
            CharacterApplicationAuthoringSummary application = package != null ? package.ApplicationConfig : null;
            if (application == null || application.ResourceKeys == null)
                return;

            string sourceStableId = FirstNonEmpty(
                application.CharacterStableId,
                package != null && package.Manifest != null ? package.Manifest.StableId : string.Empty);

            for (int i = 0; i < application.ResourceKeys.Count; i++)
            {
                AddReferenceByKey(
                    graph,
                    index,
                    "character",
                    sourceStableId,
                    "resourceKeys/" + i.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    application.ResourceKeys[i],
                    AuthoringResourcePreloadPolicies.SpawnCritical,
                    isRequiredAtRuntime: true);
            }
        }

        private static void AddWeaponAttachmentReferences(
            AuthoringResourceReferenceGraph graph,
            ResourceIndex index,
            CharacterResourcePackage package)
        {
            CharacterAuthoringGeometry geometry = package != null ? package.Geometry : null;
            if (geometry == null || geometry.WeaponAttachments == null)
                return;

            for (int i = 0; i < geometry.WeaponAttachments.Count; i++)
            {
                WeaponAttachmentProfile attachment = geometry.WeaponAttachments[i];
                if (attachment == null)
                    continue;

                AddReferenceByKey(
                    graph,
                    index,
                    "weapon",
                    attachment.WeaponId,
                    "weaponAttachments/" + i.ToString(System.Globalization.CultureInfo.InvariantCulture) + "/previewResourceKey",
                    attachment.PreviewResourceKey,
                    AuthoringResourcePreloadPolicies.EquipmentInitial,
                    isRequiredAtRuntime: true);
            }
        }

        private static void AddResourceCatalogReferences(
            AuthoringResourceReferenceGraph graph,
            ResourceIndex index,
            CharacterResourcePackage package)
        {
            CharacterPackageResourceCatalog catalog = package != null ? package.ResourceCatalog : null;
            if (catalog == null || catalog.Entries == null)
                return;

            for (int i = 0; i < catalog.Entries.Count; i++)
            {
                CharacterPackageResourceEntry entry = catalog.Entries[i];
                if (entry == null)
                    continue;

                string sourceStableId = FirstNonEmpty(entry.StableId, entry.ResourceKey, entry.LocalId);
                AddDependencyReferences(graph, index, entry, sourceStableId);
                AddPreviewReference(graph, index, entry, sourceStableId, "preview.thumbnailResourceKey", entry.Preview != null ? entry.Preview.ThumbnailResourceKey : string.Empty, AuthoringResourcePreloadPolicies.UiDeferred);
                AddPreviewReference(graph, index, entry, sourceStableId, "preview.previewMeshResourceKey", entry.Preview != null ? entry.Preview.PreviewMeshResourceKey : string.Empty, SelectPreloadPolicy(entry));
                AddPreviewReference(graph, index, entry, sourceStableId, "preview.placeholderResourceKey", entry.Preview != null ? entry.Preview.PlaceholderResourceKey : string.Empty, AuthoringResourcePreloadPolicies.UiDeferred);
            }
        }

        private static void AddDependencyReferences(
            AuthoringResourceReferenceGraph graph,
            ResourceIndex index,
            CharacterPackageResourceEntry entry,
            string sourceStableId)
        {
            if (entry == null || entry.Dependencies == null)
                return;

            for (int i = 0; i < entry.Dependencies.Count; i++)
            {
                CharacterPackageResourceDependency dependency = entry.Dependencies[i];
                if (dependency == null)
                    continue;

                AddReferenceByKey(
                    graph,
                    index,
                    "resource",
                    sourceStableId,
                    "dependencies/" + i.ToString(System.Globalization.CultureInfo.InvariantCulture) + "/resourceKey",
                    dependency.ResourceKey,
                    SelectPreloadPolicy(entry),
                    dependency.Required);
            }
        }

        private static void AddPreviewReference(
            AuthoringResourceReferenceGraph graph,
            ResourceIndex index,
            CharacterPackageResourceEntry sourceEntry,
            string sourceStableId,
            string sourceField,
            string key,
            string preloadPolicy)
        {
            if (sourceEntry != null &&
                (string.Equals(key, sourceEntry.ResourceKey, StringComparison.Ordinal) ||
                 string.Equals(key, sourceEntry.StableId, StringComparison.Ordinal) ||
                 string.Equals(key, sourceEntry.LocalId, StringComparison.Ordinal)))
                return;

            AddReferenceByKey(
                graph,
                index,
                "resource",
                sourceStableId,
                sourceField,
                key,
                preloadPolicy,
                isRequiredAtRuntime: false);
        }

        private static void AddReferenceByKey(
            AuthoringResourceReferenceGraph graph,
            ResourceIndex index,
            string sourceConsumerKind,
            string sourceStableId,
            string sourceField,
            string providerResourceKey,
            string preloadPolicy,
            bool isRequiredAtRuntime)
        {
            if (graph == null || string.IsNullOrWhiteSpace(providerResourceKey))
                return;

            AuthoringResourceItem target = index != null ? index.Find(providerResourceKey) : null;
            string runtimeKey = target != null ? GetPrimaryRuntimeResourceKey(target) : string.Empty;
            string targetProviderKey = target != null ? FirstNonEmpty(GetPrimaryProviderResourceKey(target), providerResourceKey) : providerResourceKey;
            string targetStableId = target != null ? target.StableId ?? string.Empty : string.Empty;
            graph.Edges.Add(new AuthoringResourceReferenceEdge
            {
                SourceConsumerKind = sourceConsumerKind ?? string.Empty,
                SourceConfigKind = sourceConsumerKind ?? string.Empty,
                SourceStableId = sourceStableId ?? string.Empty,
                SourceField = sourceField ?? string.Empty,
                TargetResourceId = target != null ? target.ResourceId ?? string.Empty : string.Empty,
                TargetStableId = targetStableId,
                TargetLibraryItemStableId = targetStableId,
                TargetProviderId = target != null ? target.SourceProviderId ?? string.Empty : string.Empty,
                TargetProviderResourceKey = targetProviderKey,
                TargetRuntimeResourceKey = runtimeKey,
                TargetResourceKey = FirstNonEmpty(runtimeKey, targetProviderKey, providerResourceKey),
                BindingKind = target != null ? target.BindingKind : AuthoringResourceBindingKind.None,
                IsRequiredAtRuntime = isRequiredAtRuntime,
                PreloadPolicy = preloadPolicy ?? AuthoringResourcePreloadPolicies.None
            });
        }

        private static void AddDiagnostics(
            AuthoringResourceReferenceGraph graph,
            AuthoringResourceCollection collection,
            ResourceIndex index)
        {
            if (graph == null || collection == null)
                return;

            var referencedResourceIds = new HashSet<string>(StringComparer.Ordinal);
            var referencedStableIds = new HashSet<string>(StringComparer.Ordinal);

            for (int i = 0; i < graph.Edges.Count; i++)
            {
                AuthoringResourceReferenceEdge edge = graph.Edges[i];
                if (edge == null)
                    continue;

                AuthoringResourceItem target = index != null ? index.FindEdgeTarget(edge) : null;
                if (target == null)
                {
                    graph.Diagnostics.Add(CreateDiagnostic(
                        CharacterAuthoringValidationSeverity.Error,
                        AuthoringResourceDiagnosticCodes.ReferenceBroken,
                        null,
                        edge,
                        "reference graph edge targets a missing resource item.",
                        "Update the reference, import the resource, or restore the referenced library item."));
                    continue;
                }

                if (!string.IsNullOrWhiteSpace(target.ResourceId))
                    referencedResourceIds.Add(target.ResourceId);
                if (!string.IsNullOrWhiteSpace(target.StableId))
                    referencedStableIds.Add(target.StableId);

                if (edge.IsRequiredAtRuntime && IsExplicitlyNotRuntimeLoadable(target))
                {
                    string code = target.RuntimeAvailability == AuthoringResourceRuntimeAvailability.EditorOnly
                        ? AuthoringResourceDiagnosticCodes.EditorOnlySelectedForRuntime
                        : AuthoringResourceDiagnosticCodes.NotRuntimeLoadable;
                    graph.Diagnostics.Add(CreateDiagnostic(
                        CharacterAuthoringValidationSeverity.Error,
                        code,
                        target,
                        edge,
                        "runtime-required reference targets a resource that is not runtime-loadable.",
                        "Import or compile this resource into a runtime-loadable provider, or remove the runtime-required reference."));
                }
            }

            if (graph.Edges.Count == 0 || collection.Items == null)
                return;

            for (int i = 0; i < collection.Items.Count; i++)
            {
                AuthoringResourceItem item = collection.Items[i];
                if (item == null)
                    continue;

                bool referenced =
                    (!string.IsNullOrWhiteSpace(item.ResourceId) && referencedResourceIds.Contains(item.ResourceId)) ||
                    (!string.IsNullOrWhiteSpace(item.StableId) && referencedStableIds.Contains(item.StableId));
                if (referenced)
                    continue;

                graph.Diagnostics.Add(CreateDiagnostic(
                    CharacterAuthoringValidationSeverity.Warning,
                    AuthoringResourceDiagnosticCodes.OrphanCandidate,
                    item,
                    null,
                    "resource item has no incoming references.",
                    "Keep it as reusable library content or mark it for cleanup after confirming no editor uses it."));
            }
        }

        private static AuthoringResourceDiagnostic CreateDiagnostic(
            CharacterAuthoringValidationSeverity severity,
            string code,
            AuthoringResourceItem item,
            AuthoringResourceReferenceEdge edge,
            string message,
            string suggestedFix)
        {
            return new AuthoringResourceDiagnostic
            {
                Severity = severity,
                Code = code ?? string.Empty,
                ResourceId = item != null ? item.ResourceId ?? string.Empty : edge != null ? edge.TargetResourceId ?? string.Empty : string.Empty,
                ResourceStableId = item != null ? item.StableId ?? string.Empty : edge != null ? FirstNonEmpty(edge.TargetStableId, edge.TargetLibraryItemStableId) : string.Empty,
                RuntimeResourceKey = item != null ? GetPrimaryRuntimeResourceKey(item) : edge != null ? edge.TargetRuntimeResourceKey ?? string.Empty : string.Empty,
                ProviderId = item != null ? item.SourceProviderId ?? string.Empty : edge != null ? edge.TargetProviderId ?? string.Empty : string.Empty,
                SourceConfigKind = edge != null ? FirstNonEmpty(edge.SourceConsumerKind, edge.SourceConfigKind) : string.Empty,
                SourceStableId = edge != null ? edge.SourceStableId ?? string.Empty : string.Empty,
                SourceField = edge != null ? edge.SourceField ?? string.Empty : string.Empty,
                Message = message ?? string.Empty,
                SuggestedFix = suggestedFix ?? string.Empty
            };
        }

        private static string SelectPreloadPolicy(CharacterPackageResourceEntry entry)
        {
            if (entry == null)
                return AuthoringResourcePreloadPolicies.None;

            if (string.Equals(entry.Usage, CharacterPackageResourceUsageIds.CharacterModel, StringComparison.Ordinal))
                return AuthoringResourcePreloadPolicies.SpawnCritical;
            if (string.Equals(entry.Usage, CharacterPackageResourceUsageIds.WeaponModel, StringComparison.Ordinal))
                return AuthoringResourcePreloadPolicies.EquipmentInitial;
            if (string.Equals(entry.Usage, CharacterPackageResourceUsageIds.AnimationClipGroup, StringComparison.Ordinal) ||
                string.Equals(entry.TypeId, CharacterPackageResourceTypeIds.Animation, StringComparison.Ordinal))
                return AuthoringResourcePreloadPolicies.AnimationWarmup;
            if (string.Equals(entry.Usage, CharacterPackageResourceUsageIds.VfxCue, StringComparison.Ordinal) ||
                string.Equals(entry.TypeId, CharacterPackageResourceTypeIds.Vfx, StringComparison.Ordinal))
                return AuthoringResourcePreloadPolicies.VfxWarmup;
            if (string.Equals(entry.Usage, CharacterPackageResourceUsageIds.PreviewThumbnail, StringComparison.Ordinal) ||
                string.Equals(entry.Usage, CharacterPackageResourceUsageIds.PreviewMesh, StringComparison.Ordinal) ||
                string.Equals(entry.TypeId, CharacterPackageResourceTypeIds.Preview, StringComparison.Ordinal))
                return AuthoringResourcePreloadPolicies.UiDeferred;
            if (string.Equals(entry.Usage, CharacterPackageResourceUsageIds.AudioCue, StringComparison.Ordinal) ||
                string.Equals(entry.TypeId, CharacterPackageResourceTypeIds.Audio, StringComparison.Ordinal))
                return AuthoringResourcePreloadPolicies.Audio;

            return AuthoringResourcePreloadPolicies.PresentationCritical;
        }

        private static bool IsExplicitlyNotRuntimeLoadable(AuthoringResourceItem item)
        {
            if (item == null)
                return false;

            return item.RuntimeAvailability == AuthoringResourceRuntimeAvailability.RuntimeMissing ||
                   item.RuntimeAvailability == AuthoringResourceRuntimeAvailability.EditorOnly ||
                   item.RuntimeAvailability == AuthoringResourceRuntimeAvailability.PreviewOnly ||
                   item.RuntimeAvailability == AuthoringResourceRuntimeAvailability.NotRuntimeLoadable;
        }

        private static bool ItemHasBindingValue(AuthoringResourceItem item, string value)
        {
            if (item == null || string.IsNullOrWhiteSpace(value))
                return false;

            if (string.Equals(item.ResourceId, value, StringComparison.Ordinal) ||
                string.Equals(item.StableId, value, StringComparison.Ordinal))
                return true;

            if (item.Metadata != null)
            {
                string metadataValue;
                if ((item.Metadata.TryGetValue("localId", out metadataValue) && string.Equals(metadataValue, value, StringComparison.Ordinal)) ||
                    (item.Metadata.TryGetValue("relativePath", out metadataValue) && string.Equals(metadataValue, value, StringComparison.Ordinal)))
                    return true;
            }

            if (item.ProviderBindings == null)
                return false;

            for (int i = 0; i < item.ProviderBindings.Count; i++)
            {
                AuthoringResourceProviderBinding binding = item.ProviderBindings[i];
                if (binding == null)
                    continue;
                if (string.Equals(binding.BindingKeyKind, AuthoringResourceBindingKeyKinds.Dependency, StringComparison.Ordinal))
                    continue;

                if (string.Equals(binding.ProviderResourceKey, value, StringComparison.Ordinal) ||
                    string.Equals(binding.PackageResourceKey, value, StringComparison.Ordinal) ||
                    string.Equals(binding.RuntimeResourceKey, value, StringComparison.Ordinal) ||
                    string.Equals(binding.UnityGuid, value, StringComparison.Ordinal) ||
                    string.Equals(binding.UnityAssetPath, value, StringComparison.Ordinal) ||
                    string.Equals(binding.ExternalSourcePath, value, StringComparison.Ordinal) ||
                    string.Equals(binding.DisplayValue, value, StringComparison.Ordinal))
                    return true;
            }

            return false;
        }

        private static string GetPrimaryProviderResourceKey(AuthoringResourceItem item)
        {
            AuthoringResourceProviderBinding binding = GetPrimaryBinding(item);
            return binding != null ? FirstNonEmpty(binding.ProviderResourceKey, binding.PackageResourceKey, binding.DisplayValue) : string.Empty;
        }

        private static string GetPrimaryRuntimeResourceKey(AuthoringResourceItem item)
        {
            if (item == null || item.ProviderBindings == null)
                return string.Empty;

            for (int i = 0; i < item.ProviderBindings.Count; i++)
            {
                AuthoringResourceProviderBinding binding = item.ProviderBindings[i];
                if (binding != null && !string.IsNullOrWhiteSpace(binding.RuntimeResourceKey))
                    return binding.RuntimeResourceKey;
            }

            return string.Empty;
        }

        private static AuthoringResourceProviderBinding GetPrimaryBinding(AuthoringResourceItem item)
        {
            if (item == null || item.ProviderBindings == null || item.ProviderBindings.Count == 0)
                return null;

            for (int i = 0; i < item.ProviderBindings.Count; i++)
            {
                AuthoringResourceProviderBinding binding = item.ProviderBindings[i];
                if (binding != null && binding.IsPrimary)
                    return binding;
            }

            return item.ProviderBindings[0];
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

        private sealed class ResourceIndex
        {
            private readonly Dictionary<string, AuthoringResourceItem> byAnyId = new Dictionary<string, AuthoringResourceItem>(StringComparer.Ordinal);

            public static ResourceIndex FromCollection(AuthoringResourceCollection collection)
            {
                var index = new ResourceIndex();
                if (collection == null || collection.Items == null)
                    return index;

                for (int i = 0; i < collection.Items.Count; i++)
                    index.Add(collection.Items[i]);

                return index;
            }

            public AuthoringResourceItem Find(string key)
            {
                if (string.IsNullOrWhiteSpace(key))
                    return null;

                AuthoringResourceItem item;
                return byAnyId.TryGetValue(key, out item) ? item : null;
            }

            public AuthoringResourceItem FindEdgeTarget(AuthoringResourceReferenceEdge edge)
            {
                if (edge == null)
                    return null;

                return Find(FirstNonEmpty(
                    edge.TargetResourceId,
                    edge.TargetStableId,
                    edge.TargetLibraryItemStableId,
                    edge.TargetProviderResourceKey,
                    edge.TargetRuntimeResourceKey,
                    edge.TargetResourceKey));
            }

            private void Add(AuthoringResourceItem item)
            {
                if (item == null)
                    return;

                AddKey(item.ResourceId, item);
                AddKey(item.StableId, item);
                AddKey(item.DisplayName, item);

                if (item.Metadata != null)
                {
                    string value;
                    if (item.Metadata.TryGetValue("localId", out value))
                        AddKey(value, item);
                    if (item.Metadata.TryGetValue("relativePath", out value))
                        AddKey(value, item);
                }

                if (item.ProviderBindings == null)
                    return;

                for (int i = 0; i < item.ProviderBindings.Count; i++)
                {
                    AuthoringResourceProviderBinding binding = item.ProviderBindings[i];
                    if (binding == null)
                        continue;
                    if (string.Equals(binding.BindingKeyKind, AuthoringResourceBindingKeyKinds.Dependency, StringComparison.Ordinal))
                        continue;

                    AddKey(binding.ProviderResourceKey, item);
                    AddKey(binding.PackageResourceKey, item);
                    AddKey(binding.RuntimeResourceKey, item);
                    AddKey(binding.UnityGuid, item);
                    AddKey(binding.UnityAssetPath, item);
                    AddKey(binding.ExternalSourcePath, item);
                    AddKey(binding.DisplayValue, item);
                }
            }

            private void AddKey(string key, AuthoringResourceItem item)
            {
                if (string.IsNullOrWhiteSpace(key) || byAnyId.ContainsKey(key))
                    return;

                byAnyId.Add(key, item);
            }
        }
    }
}
