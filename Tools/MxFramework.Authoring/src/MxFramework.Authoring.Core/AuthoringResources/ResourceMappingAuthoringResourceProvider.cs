using System;
using System.Collections.Generic;
using MxFramework.ResourceMapping.Authoring;

namespace MxFramework.Authoring
{
    /// <summary>Projects the canonical production mapping Profile into the low-frequency Authoring resource inventory.</summary>
    public sealed class ResourceMappingAuthoringResourceProvider : IAuthoringResourceProvider
    {
        public string ProviderId => AuthoringResourceProviderIds.ResourceMapping;

        /// <remarks>LowFreqAlloc. Resource Library refresh only.</remarks>
        public AuthoringResourceProviderDescriptor Describe(AuthoringResourceProviderContext context)
        {
            bool available = context?.ResourceMappingProfile != null;
            return new AuthoringResourceProviderDescriptor
            {
                ProviderId = ProviderId,
                DisplayName = "Production Resource Mapping",
                SourceKind = AuthoringResourceSourceKind.RuntimeCatalogAsset,
                Available = available,
                Status = available ? "Ready" : "Unavailable",
                DiagnosticCode = available ? string.Empty : AuthoringResourceDiagnosticCodes.ProviderUnavailable,
                Message = available ? string.Empty : "The canonical resource mapping Profile is unavailable."
            };
        }

        /// <remarks>AllocByDesign. Resource Library refresh materializes a complete inventory projection.</remarks>
        public AuthoringResourceCollection BuildResourceCollection(AuthoringResourceProviderContext context)
        {
            var collection = new AuthoringResourceCollection { ScopeId = context?.ScopeId ?? "resourceMapping" };
            collection.Providers.Add(Describe(context));
            ResourceMappingProfileV2 profile = context?.ResourceMappingProfile;
            if (profile == null) return collection;
            AuthoringResourceProviderUtilities.AddIfPresent(collection.Metadata, "resourceMappingProfilePath", context.ResourceMappingProfilePath);
            AuthoringResourceProviderUtilities.AddIfPresent(collection.Metadata, "catalogId", profile.CatalogId);
            for (int i = 0; i < profile.Entries.Count; i++)
            {
                ResourceMappingEntry entry = profile.Entries[i];
                if (entry?.ResourceKey == null || entry.DeliveryIntent == ResourceMappingDeliveryIntent.Excluded) continue;
                collection.Items.Add(Project(entry));
            }
            return collection;
        }

        private static AuthoringResourceItem Project(ResourceMappingEntry entry)
        {
            ResourceMappingResourceKey key = entry.ResourceKey;
            string identity = key.ToIdentityString();
            string path = entry.SourceLocator?.CachedPath ?? string.Empty;
            string usage = NormalizeUsage(entry, key.TypeId);
            bool runtime = entry.DeliveryIntent == ResourceMappingDeliveryIntent.Internal || entry.DeliveryIntent == ResourceMappingDeliveryIntent.External;
            var item = new AuthoringResourceItem
            {
                ResourceId = AuthoringResourceProviderUtilities.BuildResourceId(AuthoringResourceProviderIds.ResourceMapping, identity, key.Id),
                StableId = "resource.mapping." + identity,
                DisplayName = string.IsNullOrWhiteSpace(path) ? key.Id : AuthoringResourceNaming.GetFileDisplayName(path, key.Id),
                Kind = AuthoringResourceProviderUtilities.MapRuntimeTypeToLibraryKind(key.TypeId, usage),
                Usage = usage,
                SourceProviderId = AuthoringResourceProviderIds.ResourceMapping,
                SourceKind = AuthoringResourceSourceKind.RuntimeCatalogAsset,
                BindingKind = runtime ? AuthoringResourceBindingKind.ResourceManagerAsset : AuthoringResourceBindingKind.UnityEditorOnlyAsset,
                ImportStatus = AuthoringResourceImportStatus.Clean,
                RuntimeAvailability = runtime ? AuthoringResourceRuntimeAvailability.RuntimeReady : AuthoringResourceRuntimeAvailability.EditorOnly,
                Tags = entry.Labels != null ? new List<string>(entry.Labels) : new List<string>()
            };
            AuthoringResourceProviderUtilities.AddIfPresent(item.Metadata, "packageId", key.PackageId);
            AuthoringResourceProviderUtilities.AddIfPresent(item.Metadata, "variant", key.Variant);
            AuthoringResourceProviderUtilities.AddIfPresent(item.Metadata, "producerId", entry.ProducerReceipt?.ProducerId);
            AuthoringResourceProviderUtilities.AddIfPresent(item.Metadata, "unityGuid", entry.SourceLocator?.UnityGuid);
            AuthoringResourceProviderUtilities.AddIfPresent(item.Metadata, "unityAssetPath", path);
            item.ProviderBindings.Add(new AuthoringResourceProviderBinding
            {
                ProviderId = AuthoringResourceProviderIds.ResourceMapping,
                BindingKind = item.BindingKind,
                BindingKeyKind = AuthoringResourceBindingKeyKinds.RuntimeResourceKey,
                DisplayValue = key.Id,
                IsPrimary = true,
                ProviderResourceKey = key.Id,
                RuntimeResourceKey = key.Id,
                AssetType = key.TypeId,
                UnityGuid = entry.SourceLocator?.UnityGuid ?? string.Empty,
                UnityAssetPath = path,
                ProviderData = BuildProviderData(entry, usage, path)
            });
            return item;
        }

        private static Dictionary<string, string> BuildProviderData(
            ResourceMappingEntry entry,
            string usage,
            string unityAssetPath)
        {
            var result = entry?.ProviderData != null
                ? new Dictionary<string, string>(entry.ProviderData, StringComparer.Ordinal)
                : new Dictionary<string, string>(StringComparer.Ordinal);
            AddIfMissing(result, "usage", usage);
            AddIfMissing(result, "unityAssetGuid", entry?.SourceLocator?.UnityGuid);
            AddIfMissing(result, "unityAssetPath", unityAssetPath);
            AddIfMissing(result, "generatedAssetPath", unityAssetPath);
            AddIfMissing(result, "providerId", AuthoringResourceProviderIds.ResourceMapping);
            return result;
        }

        private static void AddIfMissing(Dictionary<string, string> data, string key, string value)
        {
            if (data == null || string.IsNullOrWhiteSpace(key) || string.IsNullOrWhiteSpace(value) || data.ContainsKey(key))
                return;
            data[key] = value;
        }

        private static string NormalizeUsage(ResourceMappingEntry entry, string typeId)
        {
            if (string.Equals(typeId, "AnimationClip", StringComparison.OrdinalIgnoreCase))
                return AnimationAuthoringResourceUsages.AnimationClip;

            string usage = Get(entry?.ProviderData, "usage");
            if (string.IsNullOrWhiteSpace(usage))
                usage = GetUsageLabel(entry?.Labels);

            switch ((usage ?? string.Empty).Trim().ToLowerInvariant())
            {
                case "charactermodel":
                    return CharacterPackageResourceUsageIds.CharacterModel;
                case "weaponmodel":
                    return CharacterPackageResourceUsageIds.WeaponModel;
                case "texture":
                    return CharacterPackageResourceUsageIds.Texture;
                case "material":
                    return CharacterPackageResourceUsageIds.Material;
                case "animationclip":
                    return AnimationAuthoringResourceUsages.AnimationClip;
                case "animationclipgroup":
                    return CharacterPackageResourceUsageIds.AnimationClipGroup;
                case "audiocue":
                    return CharacterPackageResourceUsageIds.AudioCue;
                case "vfxcue":
                    return CharacterPackageResourceUsageIds.VfxCue;
                case "previewthumbnail":
                    return CharacterPackageResourceUsageIds.PreviewThumbnail;
                case "previewmesh":
                    return CharacterPackageResourceUsageIds.PreviewMesh;
                case "characterconfig":
                    return CharacterPackageResourceUsageIds.CharacterConfig;
                case "geometryauthoring":
                    return CharacterPackageResourceUsageIds.GeometryAuthoring;
                default:
                    return usage ?? string.Empty;
            }
        }

        private static string GetUsageLabel(List<string> labels)
        {
            const string prefix = "usage.";
            for (int i = 0; labels != null && i < labels.Count; i++)
            {
                string label = labels[i];
                if (!string.IsNullOrWhiteSpace(label) && label.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                    return label.Substring(prefix.Length);
            }

            return string.Empty;
        }

        private static string Get(Dictionary<string, string> data, string key) =>
            data != null && data.TryGetValue(key, out string value) ? value ?? string.Empty : string.Empty;
    }
}
