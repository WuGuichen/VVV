using System;
using System.Collections.Generic;

namespace MxFramework.Authoring
{
    public sealed class UnityAssetDatabaseAuthoringResourceProvider : IAuthoringResourceProvider
    {
        public string ProviderId
        {
            get { return AuthoringResourceProviderIds.UnityAssetDatabase; }
        }

        public AuthoringResourceProviderDescriptor Describe(AuthoringResourceProviderContext context)
        {
            bool available = context != null && IsAssetDatabaseSnapshotAvailable(context.UnityResourceCatalog);
            return new AuthoringResourceProviderDescriptor
            {
                ProviderId = ProviderId,
                DisplayName = "Unity AssetDatabase",
                SourceKind = AuthoringResourceSourceKind.UnityAsset,
                Available = available,
                Status = available ? "Ready" : "Unavailable",
                DiagnosticCode = available ? string.Empty : AuthoringResourceDiagnosticCodes.ProviderUnavailable,
                Message = available ? string.Empty : "Unity AssetDatabase bridge snapshot is not available or has not been enriched. Run Unity import/sync to export unity_resource_catalog.json with GUID/object type data."
            };
        }

        public AuthoringResourceCollection BuildResourceCollection(AuthoringResourceProviderContext context)
        {
            return FromUnityResourceCatalog(context != null ? context.UnityResourceCatalog : null, context);
        }

        public static AuthoringResourceCollection FromUnityResourceCatalog(
            AuthoringUnityResourceCatalogDocument catalog,
            AuthoringResourceProviderContext context)
        {
            var provider = new UnityAssetDatabaseAuthoringResourceProvider();
            if (context != null && catalog != null && context.UnityResourceCatalog == null)
                context.UnityResourceCatalog = catalog;
            var collection = new AuthoringResourceCollection
            {
                ScopeId = context != null && !string.IsNullOrWhiteSpace(context.ScopeId)
                    ? context.ScopeId
                    : "unityAssetDatabase"
            };
            if (context != null)
            {
                AuthoringResourceProviderUtilities.AddIfPresent(collection.Metadata, "packageId", context.PackageId);
                AuthoringResourceProviderUtilities.AddIfPresent(collection.Metadata, "packagePath", context.PackagePath);
                AuthoringResourceProviderUtilities.AddIfPresent(collection.Metadata, "projectRootPath", context.ProjectRootPath);
                AuthoringResourceProviderUtilities.AddIfPresent(collection.Metadata, "unityResourceCatalogPath", context.UnityResourceCatalogPath);
            }

            AuthoringResourceProviderDescriptor descriptor = provider.Describe(context);
            collection.Providers.Add(descriptor);
            if (catalog == null)
            {
                collection.Diagnostics.Add(new AuthoringResourceDiagnostic
                {
                    Severity = CharacterAuthoringValidationSeverity.Warning,
                    Code = AuthoringResourceDiagnosticCodes.ProviderUnavailable,
                    ProviderId = provider.ProviderId,
                    Message = descriptor.Message,
                    SuggestedFix = "Run the Authoring Unity import bridge, then refresh the Resource Manager."
                });
                return collection;
            }

            if (!descriptor.Available)
            {
                collection.Diagnostics.Add(new AuthoringResourceDiagnostic
                {
                    Severity = CharacterAuthoringValidationSeverity.Warning,
                    Code = AuthoringResourceDiagnosticCodes.ProviderUnavailable,
                    ProviderId = provider.ProviderId,
                    Message = descriptor.Message,
                    SuggestedFix = "Open Unity and refresh the Unity resource catalog snapshot, then reload the Resource Manager."
                });
            }

            if (catalog.Entries != null)
            {
                for (int i = 0; i < catalog.Entries.Count; i++)
                {
                    AuthoringUnityResourceCatalogEntry entry = catalog.Entries[i];
                    if (entry != null)
                        collection.Items.Add(FromUnityEntry(entry, context));
                }
            }

            if (catalog.OrphanedUnityAssets != null)
            {
                for (int i = 0; i < catalog.OrphanedUnityAssets.Count; i++)
                {
                    AuthoringUnityResourceCatalogOrphan orphan = catalog.OrphanedUnityAssets[i];
                    if (orphan != null && !string.IsNullOrWhiteSpace(orphan.UnityAssetPath))
                        collection.Items.Add(FromOrphan(orphan, context));
                }
            }

            return collection;
        }

        private static bool IsAssetDatabaseSnapshotAvailable(AuthoringUnityResourceCatalogDocument catalog)
        {
            if (catalog == null || catalog.Entries == null || catalog.Entries.Count == 0)
                return false;

            for (int i = 0; i < catalog.Entries.Count; i++)
            {
                AuthoringUnityResourceCatalogEntry entry = catalog.Entries[i];
                if (entry == null)
                    continue;

                if (!string.IsNullOrWhiteSpace(entry.UnityAssetGuid) ||
                    !string.IsNullOrWhiteSpace(entry.UnityMainObjectType))
                    return true;
                if (!string.Equals(entry.ImportStatus, "PendingUnityImport", StringComparison.OrdinalIgnoreCase) &&
                    !string.IsNullOrWhiteSpace(entry.ImportStatus))
                    return true;
            }

            return false;
        }

        private static AuthoringResourceItem FromUnityEntry(
            AuthoringUnityResourceCatalogEntry entry,
            AuthoringResourceProviderContext context)
        {
            string assetPath = AuthoringResourceProviderUtilities.FirstNonEmpty(entry.UnityAssetPath, entry.Address);
            string stableId = "unity." + AuthoringResourceProviderUtilities.SanitizeStableSegment(
                AuthoringResourceProviderUtilities.FirstNonEmpty(entry.StableId, entry.UnityAssetGuid, assetPath, entry.Id));
            string providerKey = AuthoringResourceProviderUtilities.FirstNonEmpty(entry.UnityAssetGuid, assetPath, entry.Id);
            bool assetExists = AuthoringResourceProviderUtilities.ProjectFileExists(context != null ? context.ProjectRootPath : string.Empty, assetPath);
            bool missing = !string.IsNullOrWhiteSpace(assetPath) && !assetExists;
            string importStatusText = entry.ImportStatus ?? string.Empty;
            AuthoringResourceImportStatus importStatus = missing
                ? AuthoringResourceImportStatus.UnityMissing
                : MapImportStatus(importStatusText);

            var item = new AuthoringResourceItem
            {
                ResourceId = AuthoringResourceProviderUtilities.BuildResourceId(AuthoringResourceProviderIds.UnityAssetDatabase, stableId, providerKey),
                StableId = stableId,
                DisplayName = AuthoringResourceProviderUtilities.GetFileDisplayName(assetPath, AuthoringResourceProviderUtilities.FirstNonEmpty(entry.Id, entry.PackageResourceKey, stableId)),
                Kind = AuthoringResourceProviderUtilities.MapRuntimeTypeToLibraryKind(entry.Type, entry.Usage),
                Usage = entry.Usage ?? string.Empty,
                SourceProviderId = AuthoringResourceProviderIds.UnityAssetDatabase,
                SourceKind = AuthoringResourceSourceKind.UnityAsset,
                BindingKind = AuthoringResourceBindingKind.UnityAsset,
                ImportStatus = importStatus,
                RuntimeAvailability = AuthoringResourceRuntimeAvailability.EditorOnly,
                Tags = entry.Labels != null ? new List<string>(entry.Labels) : new List<string>()
            };

            AddMetadata(item, entry, context, assetExists);
            AddUnityBindings(item, entry, assetPath, providerKey);
            AddDependencyBindings(item, entry);
            AddEntryDiagnostics(item, entry);
            if (missing)
                AddMissingDiagnostic(item, assetPath);
            return item;
        }

        private static AuthoringResourceItem FromOrphan(
            AuthoringUnityResourceCatalogOrphan orphan,
            AuthoringResourceProviderContext context)
        {
            string assetPath = orphan.UnityAssetPath ?? string.Empty;
            string stableId = "unity.orphan." + AuthoringResourceProviderUtilities.SanitizeStableSegment(assetPath);
            var item = new AuthoringResourceItem
            {
                ResourceId = AuthoringResourceProviderUtilities.BuildResourceId(AuthoringResourceProviderIds.UnityAssetDatabase, stableId, assetPath),
                StableId = stableId,
                DisplayName = AuthoringResourceProviderUtilities.GetFileDisplayName(assetPath, stableId),
                Kind = "unknown",
                Usage = string.Empty,
                SourceProviderId = AuthoringResourceProviderIds.UnityAssetDatabase,
                SourceKind = AuthoringResourceSourceKind.UnityAsset,
                BindingKind = AuthoringResourceBindingKind.UnityAsset,
                ImportStatus = AuthoringResourceImportStatus.OrphanCandidate,
                RuntimeAvailability = AuthoringResourceRuntimeAvailability.EditorOnly
            };
            AuthoringResourceProviderUtilities.AddIfPresent(item.Metadata, "unityAssetPath", assetPath);
            AuthoringResourceProviderUtilities.AddIfPresent(item.Metadata, "importStatus", orphan.ImportStatus);
            AuthoringResourceProviderUtilities.AddIfPresent(item.Metadata, "message", orphan.Message);
            item.ProviderBindings.Add(new AuthoringResourceProviderBinding
            {
                ProviderId = AuthoringResourceProviderIds.UnityAssetDatabase,
                BindingKind = AuthoringResourceBindingKind.UnityAsset,
                BindingKeyKind = AuthoringResourceBindingKeyKinds.UnityAssetPath,
                DisplayValue = assetPath,
                IsPrimary = true,
                ProviderResourceKey = assetPath,
                UnityAssetPath = assetPath
            });
            item.Diagnostics.Add(new AuthoringResourceDiagnostic
            {
                Severity = CharacterAuthoringValidationSeverity.Warning,
                Code = AuthoringResourceDiagnosticCodes.OrphanCandidate,
                ResourceId = item.ResourceId,
                ResourceStableId = item.StableId,
                ProviderId = AuthoringResourceProviderIds.UnityAssetDatabase,
                Message = string.IsNullOrWhiteSpace(orphan.Message) ? "Unity asset is not referenced by the current resource snapshot." : orphan.Message,
                SuggestedFix = "Keep it as a shared Unity asset or clean it after confirming no editor references it."
            });
            return item;
        }

        private static AuthoringResourceImportStatus MapImportStatus(string value)
        {
            if (string.Equals(value, "ImportFailed", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(value, "Error", StringComparison.OrdinalIgnoreCase))
                return AuthoringResourceImportStatus.ImportFailed;
            if (string.Equals(value, "Conflict", StringComparison.OrdinalIgnoreCase))
                return AuthoringResourceImportStatus.Conflict;
            if (string.Equals(value, "SourceChanged", StringComparison.OrdinalIgnoreCase))
                return AuthoringResourceImportStatus.SourceChanged;
            if (string.Equals(value, "UnityMissing", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(value, "Missing", StringComparison.OrdinalIgnoreCase))
                return AuthoringResourceImportStatus.UnityMissing;

            return AuthoringResourceImportStatus.Clean;
        }

        private static void AddMetadata(
            AuthoringResourceItem item,
            AuthoringUnityResourceCatalogEntry entry,
            AuthoringResourceProviderContext context,
            bool assetExists)
        {
            AuthoringResourceProviderUtilities.AddIfPresent(item.Metadata, "packageId", entry.PackageId);
            AuthoringResourceProviderUtilities.AddIfPresent(item.Metadata, "packageResourceKey", entry.PackageResourceKey);
            AuthoringResourceProviderUtilities.AddIfPresent(item.Metadata, "packageStableId", entry.StableId);
            AuthoringResourceProviderUtilities.AddIfPresent(item.Metadata, "runtimeResourceKey", entry.Id);
            AuthoringResourceProviderUtilities.AddIfPresent(item.Metadata, "variant", entry.Variant);
            AuthoringResourceProviderUtilities.AddIfPresent(item.Metadata, "sourceRelativePath", entry.SourceRelativePath);
            AuthoringResourceProviderUtilities.AddIfPresent(item.Metadata, "sourceFormat", entry.SourceFormat);
            AuthoringResourceProviderUtilities.AddIfPresent(item.Metadata, "unityAssetPath", entry.UnityAssetPath);
            AuthoringResourceProviderUtilities.AddIfPresent(item.Metadata, "unityAssetGuid", entry.UnityAssetGuid);
            AuthoringResourceProviderUtilities.AddIfPresent(item.Metadata, "unityMainObjectType", entry.UnityMainObjectType);
            AuthoringResourceProviderUtilities.AddIfPresent(item.Metadata, "importerKind", entry.ImporterKind);
            AuthoringResourceProviderUtilities.AddIfPresent(item.Metadata, "unityImportStatus", entry.ImportStatus);
            AuthoringResourceProviderUtilities.AddIfPresent(item.Metadata, "hash", AuthoringResourceProviderUtilities.FirstNonEmpty(entry.Hash, entry.ContentHash, entry.DeclaredContentHash));
            if (context != null)
                AuthoringResourceProviderUtilities.AddIfPresent(item.Metadata, "unityResourceCatalogPath", context.UnityResourceCatalogPath);
            item.Metadata["assetExists"] = assetExists ? "true" : "false";
        }

        private static void AddUnityBindings(
            AuthoringResourceItem item,
            AuthoringUnityResourceCatalogEntry entry,
            string assetPath,
            string providerKey)
        {
            bool hasGuid = !string.IsNullOrWhiteSpace(entry.UnityAssetGuid);
            item.ProviderBindings.Add(new AuthoringResourceProviderBinding
            {
                ProviderId = AuthoringResourceProviderIds.UnityAssetDatabase,
                BindingKind = AuthoringResourceBindingKind.UnityAsset,
                BindingKeyKind = hasGuid ? AuthoringResourceBindingKeyKinds.UnityGuid : AuthoringResourceBindingKeyKinds.UnityAssetPath,
                DisplayValue = hasGuid ? entry.UnityAssetGuid : assetPath,
                IsPrimary = true,
                ProviderResourceKey = providerKey,
                UnityGuid = entry.UnityAssetGuid ?? string.Empty,
                UnityAssetPath = assetPath ?? string.Empty,
                AssetType = AuthoringResourceProviderUtilities.FirstNonEmpty(entry.UnityMainObjectType, entry.Type),
                Hash = AuthoringResourceProviderUtilities.FirstNonEmpty(entry.Hash, entry.ContentHash, entry.DeclaredContentHash),
                ProviderData = entry.ProviderData != null
                    ? new Dictionary<string, string>(entry.ProviderData, StringComparer.Ordinal)
                    : new Dictionary<string, string>(StringComparer.Ordinal)
            });

            if (hasGuid && !string.IsNullOrWhiteSpace(assetPath))
            {
                item.ProviderBindings.Add(new AuthoringResourceProviderBinding
                {
                    ProviderId = AuthoringResourceProviderIds.UnityAssetDatabase,
                    BindingKind = AuthoringResourceBindingKind.UnityAsset,
                    BindingKeyKind = AuthoringResourceBindingKeyKinds.UnityAssetPath,
                    DisplayValue = assetPath,
                    ProviderResourceKey = assetPath,
                    UnityAssetPath = assetPath,
                    AssetType = AuthoringResourceProviderUtilities.FirstNonEmpty(entry.UnityMainObjectType, entry.Type)
                });
            }

            if (!string.IsNullOrWhiteSpace(entry.PackageResourceKey))
            {
                item.ProviderBindings.Add(new AuthoringResourceProviderBinding
                {
                    ProviderId = AuthoringResourceProviderIds.UnityAssetDatabase,
                    BindingKind = AuthoringResourceBindingKind.PackageResource,
                    BindingKeyKind = AuthoringResourceBindingKeyKinds.PackageResourceKey,
                    DisplayValue = entry.PackageResourceKey,
                    ProviderResourceKey = entry.PackageResourceKey,
                    PackageResourceKey = entry.PackageResourceKey
                });
            }
        }

        private static void AddDependencyBindings(AuthoringResourceItem item, AuthoringUnityResourceCatalogEntry entry)
        {
            if (entry == null || entry.Dependencies == null)
                return;

            for (int i = 0; i < entry.Dependencies.Count; i++)
            {
                AuthoringUnityResourceKey dependency = entry.Dependencies[i];
                if (dependency == null || string.IsNullOrWhiteSpace(dependency.Id))
                    continue;

                item.ProviderBindings.Add(new AuthoringResourceProviderBinding
                {
                    ProviderId = AuthoringResourceProviderIds.UnityAssetDatabase,
                    BindingKind = AuthoringResourceBindingKind.UnityAsset,
                    BindingKeyKind = AuthoringResourceBindingKeyKinds.Dependency,
                    DisplayValue = dependency.Id,
                    ProviderResourceKey = dependency.Id,
                    ProviderData = new Dictionary<string, string>
                    {
                        { "type", dependency.Type ?? string.Empty },
                        { "variant", dependency.Variant ?? string.Empty },
                        { "packageId", dependency.PackageId ?? string.Empty }
                    }
                });
            }
        }

        private static void AddEntryDiagnostics(AuthoringResourceItem item, AuthoringUnityResourceCatalogEntry entry)
        {
            if (entry == null || entry.Diagnostics == null)
                return;

            for (int i = 0; i < entry.Diagnostics.Count; i++)
            {
                AuthoringUnityResourceCatalogDiagnostic diagnostic = entry.Diagnostics[i];
                if (diagnostic == null)
                    continue;

                item.Diagnostics.Add(new AuthoringResourceDiagnostic
                {
                    Severity = MapSeverity(diagnostic.Severity),
                    Code = diagnostic.Code ?? string.Empty,
                    ResourceId = item.ResourceId,
                    ResourceStableId = item.StableId,
                    ProviderId = AuthoringResourceProviderIds.UnityAssetDatabase,
                    SourceConfigKind = "unityResourceCatalog",
                    SourceStableId = entry.StableId ?? string.Empty,
                    SourceField = diagnostic.Field ?? string.Empty,
                    Message = diagnostic.Message ?? string.Empty,
                    SuggestedFix = string.IsNullOrWhiteSpace(diagnostic.SourcePath)
                        ? "Refresh the Unity import snapshot and inspect this asset."
                        : "Inspect source path: " + diagnostic.SourcePath
                });
            }
        }

        private static void AddMissingDiagnostic(AuthoringResourceItem item, string assetPath)
        {
            item.Diagnostics.Add(new AuthoringResourceDiagnostic
            {
                Severity = CharacterAuthoringValidationSeverity.Error,
                Code = AuthoringResourceDiagnosticCodes.UnityAssetMissing,
                ResourceId = item.ResourceId,
                ResourceStableId = item.StableId,
                ProviderId = AuthoringResourceProviderIds.UnityAssetDatabase,
                SourceConfigKind = "unityResourceCatalog",
                SourceField = "unityAssetPath",
                Message = "Unity asset path is missing from the project: " + assetPath,
                SuggestedFix = "Run import/sync again or update the Unity asset snapshot."
            });
        }

        private static CharacterAuthoringValidationSeverity MapSeverity(string value)
        {
            if (string.Equals(value, "Error", StringComparison.OrdinalIgnoreCase))
                return CharacterAuthoringValidationSeverity.Error;
            if (string.Equals(value, "Info", StringComparison.OrdinalIgnoreCase))
                return CharacterAuthoringValidationSeverity.Info;
            return CharacterAuthoringValidationSeverity.Warning;
        }
    }
}
