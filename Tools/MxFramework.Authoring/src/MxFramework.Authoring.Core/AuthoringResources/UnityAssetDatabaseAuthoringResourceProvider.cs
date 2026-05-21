using System;
using System.Collections.Generic;
using System.IO;

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
                    {
                        collection.Items.Add(FromUnityEntry(entry, context));
                        AddSubAssetItems(collection, entry, context);
                    }
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
            string subAssetKey = GetProviderData(entry, "unitySubAssetKey");
            string unityObjectName = GetProviderData(entry, "unityObjectName");
            string assetType = ResolveUnityAssetType(entry);
            string usage = NormalizeUnityUsage(entry.Usage, entry.Type, assetType);
            string stableId = "unity." + AuthoringResourceProviderUtilities.SanitizeStableSegment(
                AuthoringResourceProviderUtilities.FirstNonEmpty(entry.StableId, subAssetKey, entry.UnityAssetGuid, assetPath, entry.Id));
            string providerKey = AuthoringResourceProviderUtilities.FirstNonEmpty(subAssetKey, entry.UnityAssetGuid, assetPath, entry.Id);
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
                DisplayName = AuthoringResourceProviderUtilities.FirstNonEmpty(unityObjectName, AuthoringResourceProviderUtilities.GetFileDisplayName(assetPath, AuthoringResourceProviderUtilities.FirstNonEmpty(entry.Id, entry.PackageResourceKey, stableId))),
                Kind = AuthoringResourceProviderUtilities.MapRuntimeTypeToLibraryKind(entry.Type, usage),
                Usage = usage,
                SourceProviderId = AuthoringResourceProviderIds.UnityAssetDatabase,
                SourceKind = AuthoringResourceSourceKind.UnityAsset,
                BindingKind = AuthoringResourceBindingKind.UnityAsset,
                ImportStatus = importStatus,
                RuntimeAvailability = AuthoringResourceRuntimeAvailability.EditorOnly,
                Tags = BuildTags(entry.Labels, usage, IsAnimationClip(assetType) ? "unity-animation-clip" : string.Empty)
            };

            AddMetadata(item, entry, context, assetExists);
            AddUnityBindings(item, entry, assetPath, providerKey);
            AddDependencyBindings(item, entry);
            AddEntryDiagnostics(item, entry);
            if (missing)
                AddMissingDiagnostic(item, assetPath);
            return item;
        }

        private static void AddSubAssetItems(
            AuthoringResourceCollection collection,
            AuthoringUnityResourceCatalogEntry entry,
            AuthoringResourceProviderContext context)
        {
            if (collection == null || entry == null || entry.SubAssets == null)
                return;

            for (int i = 0; i < entry.SubAssets.Count; i++)
            {
                AuthoringUnityResourceCatalogSubAsset subAsset = entry.SubAssets[i];
                if (subAsset == null || !IsAnimationClip(ResolveSubAssetType(subAsset)))
                    continue;

                collection.Items.Add(FromUnitySubAsset(entry, subAsset, context));
            }
        }

        private static AuthoringResourceItem FromUnitySubAsset(
            AuthoringUnityResourceCatalogEntry entry,
            AuthoringUnityResourceCatalogSubAsset subAsset,
            AuthoringResourceProviderContext context)
        {
            string assetPath = AuthoringResourceProviderUtilities.FirstNonEmpty(entry.UnityAssetPath, entry.Address);
            string sourceFormat = ResolveSourceFormat(entry, assetPath);
            string subAssetName = ResolveSubAssetName(subAsset);
            string subAssetId = ResolveSubAssetId(subAsset, subAssetName);
            string providerKey = ResolveSubAssetProviderKey(entry, subAsset, assetPath, subAssetId, subAssetName);
            string stableIdSeed = AuthoringResourceProviderUtilities.FirstNonEmpty(entry.StableId, entry.UnityAssetGuid, assetPath, entry.Id);
            string stableId = "unity." + AuthoringResourceProviderUtilities.SanitizeStableSegment(stableIdSeed + "." + AuthoringResourceProviderUtilities.FirstNonEmpty(subAssetId, subAssetName, providerKey));
            bool assetExists = AuthoringResourceProviderUtilities.ProjectFileExists(context != null ? context.ProjectRootPath : string.Empty, assetPath);
            bool missing = !string.IsNullOrWhiteSpace(assetPath) && !assetExists;
            AuthoringResourceImportStatus importStatus = missing
                ? AuthoringResourceImportStatus.UnityMissing
                : MapImportStatus(entry.ImportStatus ?? string.Empty);

            var item = new AuthoringResourceItem
            {
                ResourceId = AuthoringResourceProviderUtilities.BuildResourceId(AuthoringResourceProviderIds.UnityAssetDatabase, stableId, providerKey),
                StableId = stableId,
                DisplayName = AuthoringResourceProviderUtilities.FirstNonEmpty(subAssetName, subAssetId, providerKey),
                Kind = CharacterPackageResourceTypeIds.Animation,
                Usage = AnimationAuthoringResourceUsages.AnimationClip,
                SourceProviderId = AuthoringResourceProviderIds.UnityAssetDatabase,
                SourceKind = AuthoringResourceSourceKind.UnityAsset,
                BindingKind = AuthoringResourceBindingKind.UnityAsset,
                ImportStatus = importStatus,
                RuntimeAvailability = AuthoringResourceRuntimeAvailability.EditorOnly,
                Tags = BuildTags(entry.Labels, AnimationAuthoringResourceUsages.AnimationClip, "unity-model-sub-clip")
            };

            AddSubAssetMetadata(item, entry, subAsset, context, assetPath, providerKey, sourceFormat, assetExists);
            AddSubAssetBindings(item, entry, subAsset, assetPath, providerKey, sourceFormat);
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
            AuthoringResourceProviderUtilities.AddIfPresent(item.Metadata, "sourceFormat", ResolveSourceFormat(entry, entry.UnityAssetPath));
            AuthoringResourceProviderUtilities.AddIfPresent(item.Metadata, "unityAssetPath", entry.UnityAssetPath);
            AuthoringResourceProviderUtilities.AddIfPresent(item.Metadata, "unityAssetGuid", entry.UnityAssetGuid);
            AuthoringResourceProviderUtilities.AddIfPresent(item.Metadata, "unityMainObjectType", entry.UnityMainObjectType);
            AuthoringResourceProviderUtilities.AddIfPresent(item.Metadata, "unitySubAssetKey", GetProviderData(entry, "unitySubAssetKey"));
            AuthoringResourceProviderUtilities.AddIfPresent(item.Metadata, "unityObjectType", GetProviderData(entry, "unityObjectType"));
            AuthoringResourceProviderUtilities.AddIfPresent(item.Metadata, "unityObjectName", GetProviderData(entry, "unityObjectName"));
            AuthoringResourceProviderUtilities.AddIfPresent(item.Metadata, "unityLocalFileId", GetProviderData(entry, "unityLocalFileId"));
            AuthoringResourceProviderUtilities.AddIfPresent(item.Metadata, "parentUnityAssetPath", GetProviderData(entry, "parentUnityAssetPath"));
            AuthoringResourceProviderUtilities.AddIfPresent(item.Metadata, "importerKind", entry.ImporterKind);
            AuthoringResourceProviderUtilities.AddIfPresent(item.Metadata, "unityImportStatus", entry.ImportStatus);
            AuthoringResourceProviderUtilities.AddIfPresent(item.Metadata, "hash", AuthoringResourceProviderUtilities.FirstNonEmpty(entry.Hash, entry.ContentHash, entry.DeclaredContentHash));
            AddClipMetadata(item.Metadata, entry, item.Usage, item.BindingKind, item.RuntimeAvailability);
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
            string subAssetKey = GetProviderData(entry, "unitySubAssetKey");
            bool hasSubAssetKey = !string.IsNullOrWhiteSpace(subAssetKey);
            string assetType = ResolveUnityAssetType(entry);
            item.ProviderBindings.Add(new AuthoringResourceProviderBinding
            {
                ProviderId = AuthoringResourceProviderIds.UnityAssetDatabase,
                BindingKind = AuthoringResourceBindingKind.UnityAsset,
                BindingKeyKind = hasSubAssetKey ? AuthoringResourceBindingKeyKinds.UnitySubAssetKey : hasGuid ? AuthoringResourceBindingKeyKinds.UnityGuid : AuthoringResourceBindingKeyKinds.UnityAssetPath,
                DisplayValue = hasSubAssetKey ? subAssetKey : hasGuid ? entry.UnityAssetGuid : assetPath,
                IsPrimary = true,
                ProviderResourceKey = providerKey,
                UnityGuid = entry.UnityAssetGuid ?? string.Empty,
                UnityAssetPath = assetPath ?? string.Empty,
                AssetType = assetType,
                Hash = AuthoringResourceProviderUtilities.FirstNonEmpty(entry.Hash, entry.ContentHash, entry.DeclaredContentHash),
                ProviderData = BuildEntryProviderData(entry, assetPath, item.Usage, item.BindingKind, item.RuntimeAvailability)
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
                    AssetType = assetType
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

        private static void AddSubAssetMetadata(
            AuthoringResourceItem item,
            AuthoringUnityResourceCatalogEntry entry,
            AuthoringUnityResourceCatalogSubAsset subAsset,
            AuthoringResourceProviderContext context,
            string assetPath,
            string providerKey,
            string sourceFormat,
            bool assetExists)
        {
            string subClipName = ResolveSubAssetName(subAsset);
            string subClipId = ResolveSubAssetId(subAsset, subClipName);
            AuthoringResourceProviderUtilities.AddIfPresent(item.Metadata, "packageId", entry.PackageId);
            AuthoringResourceProviderUtilities.AddIfPresent(item.Metadata, "packageStableId", entry.StableId);
            AuthoringResourceProviderUtilities.AddIfPresent(item.Metadata, "sourceRelativePath", entry.SourceRelativePath);
            AuthoringResourceProviderUtilities.AddIfPresent(item.Metadata, "sourceFormat", sourceFormat);
            AuthoringResourceProviderUtilities.AddIfPresent(item.Metadata, "unityAssetPath", assetPath);
            AuthoringResourceProviderUtilities.AddIfPresent(item.Metadata, "unityAssetGuid", entry.UnityAssetGuid);
            AuthoringResourceProviderUtilities.AddIfPresent(item.Metadata, "unityMainObjectType", entry.UnityMainObjectType);
            AuthoringResourceProviderUtilities.AddIfPresent(item.Metadata, "mainObjectType", entry.UnityMainObjectType);
            AuthoringResourceProviderUtilities.AddIfPresent(item.Metadata, "importerKind", entry.ImporterKind);
            AuthoringResourceProviderUtilities.AddIfPresent(item.Metadata, "parentUnityAssetPath", assetPath);
            AuthoringResourceProviderUtilities.AddIfPresent(item.Metadata, "unitySubAssetKey", providerKey);
            AuthoringResourceProviderUtilities.AddIfPresent(item.Metadata, "subAssetId", subClipId);
            AuthoringResourceProviderUtilities.AddIfPresent(item.Metadata, "subAssetName", subClipName);
            AuthoringResourceProviderUtilities.AddIfPresent(item.Metadata, "subAssetType", ResolveSubAssetType(subAsset));
            AuthoringResourceProviderUtilities.AddIfPresent(item.Metadata, "unityLocalFileId", AuthoringResourceProviderUtilities.FirstNonEmpty(subAsset.UnityLocalFileId, GetSubAssetData(subAsset, "unityLocalFileId")));
            AuthoringResourceProviderUtilities.AddIfPresent(item.Metadata, "clipName", subClipName);
            AuthoringResourceProviderUtilities.AddIfPresent(item.Metadata, "subClipName", subClipName);
            AuthoringResourceProviderUtilities.AddIfPresent(item.Metadata, "subClipId", subClipId);
            AuthoringResourceProviderUtilities.AddIfPresent(item.Metadata, "preloadPolicy", AuthoringResourcePreloadPolicies.AnimationWarmup);
            if (subAsset.DurationSeconds > 0f)
                item.Metadata["durationSeconds"] = subAsset.DurationSeconds.ToString(System.Globalization.CultureInfo.InvariantCulture);
            item.Metadata["loopTime"] = subAsset.LoopTime ? "true" : "false";
            item.Metadata["humanMotion"] = subAsset.HumanMotion ? "true" : "false";
            item.Metadata["bindingKind"] = item.BindingKind.ToString();
            item.Metadata["runtimeAvailability"] = item.RuntimeAvailability.ToString();
            item.Metadata["sourceLoadability"] = item.RuntimeAvailability.ToString();
            item.Metadata["assetExists"] = assetExists ? "true" : "false";
            if (context != null)
                AuthoringResourceProviderUtilities.AddIfPresent(item.Metadata, "unityResourceCatalogPath", context.UnityResourceCatalogPath);
        }

        private static void AddSubAssetBindings(
            AuthoringResourceItem item,
            AuthoringUnityResourceCatalogEntry entry,
            AuthoringUnityResourceCatalogSubAsset subAsset,
            string assetPath,
            string providerKey,
            string sourceFormat)
        {
            string assetType = ResolveSubAssetType(subAsset);
            item.ProviderBindings.Add(new AuthoringResourceProviderBinding
            {
                ProviderId = AuthoringResourceProviderIds.UnityAssetDatabase,
                BindingKind = AuthoringResourceBindingKind.UnityAsset,
                BindingKeyKind = AuthoringResourceBindingKeyKinds.UnitySubAssetKey,
                DisplayValue = providerKey,
                IsPrimary = true,
                ProviderResourceKey = providerKey,
                UnityGuid = entry.UnityAssetGuid ?? string.Empty,
                UnityAssetPath = assetPath ?? string.Empty,
                AssetType = assetType,
                Hash = AuthoringResourceProviderUtilities.FirstNonEmpty(entry.Hash, entry.ContentHash, entry.DeclaredContentHash),
                ProviderData = BuildSubAssetProviderData(entry, subAsset, assetPath, providerKey, sourceFormat)
            });

            if (!string.IsNullOrWhiteSpace(entry.UnityAssetGuid))
            {
                item.ProviderBindings.Add(new AuthoringResourceProviderBinding
                {
                    ProviderId = AuthoringResourceProviderIds.UnityAssetDatabase,
                    BindingKind = AuthoringResourceBindingKind.UnityAsset,
                    BindingKeyKind = AuthoringResourceBindingKeyKinds.UnityGuid,
                    DisplayValue = entry.UnityAssetGuid,
                    ProviderResourceKey = entry.UnityAssetGuid,
                    UnityGuid = entry.UnityAssetGuid,
                    UnityAssetPath = assetPath,
                    AssetType = assetType
                });
            }

            if (!string.IsNullOrWhiteSpace(assetPath))
            {
                item.ProviderBindings.Add(new AuthoringResourceProviderBinding
                {
                    ProviderId = AuthoringResourceProviderIds.UnityAssetDatabase,
                    BindingKind = AuthoringResourceBindingKind.UnityAsset,
                    BindingKeyKind = AuthoringResourceBindingKeyKinds.UnityAssetPath,
                    DisplayValue = assetPath,
                    ProviderResourceKey = assetPath,
                    UnityAssetPath = assetPath,
                    AssetType = assetType
                });
            }
        }

        private static Dictionary<string, string> BuildEntryProviderData(
            AuthoringUnityResourceCatalogEntry entry,
            string assetPath,
            string usage,
            AuthoringResourceBindingKind bindingKind,
            AuthoringResourceRuntimeAvailability runtimeAvailability)
        {
            var data = entry.ProviderData != null
                ? new Dictionary<string, string>(entry.ProviderData, StringComparer.Ordinal)
                : new Dictionary<string, string>(StringComparer.Ordinal);
            AddProviderDataIfMissing(data, "sourceFormat", ResolveSourceFormat(entry, assetPath));
            AddProviderDataIfMissing(data, "usage", usage);
            if (string.Equals(usage, AnimationAuthoringResourceUsages.AnimationClip, StringComparison.Ordinal))
            {
                AddProviderDataIfMissing(data, "clipName", ResolveClipName(entry));
                AddProviderDataIfMissing(data, "subClipName", ResolveSubClipName(entry));
                AddProviderDataIfMissing(data, "subClipId", ResolveSubClipId(entry));
                AddProviderDataIfMissing(data, "preloadPolicy", AuthoringResourcePreloadPolicies.AnimationWarmup);
            }
            AddProviderDataIfMissing(data, "bindingKind", bindingKind.ToString());
            AddProviderDataIfMissing(data, "runtimeAvailability", runtimeAvailability.ToString());
            AddProviderDataIfMissing(data, "sourceLoadability", runtimeAvailability.ToString());
            return data;
        }

        private static Dictionary<string, string> BuildSubAssetProviderData(
            AuthoringUnityResourceCatalogEntry entry,
            AuthoringUnityResourceCatalogSubAsset subAsset,
            string assetPath,
            string providerKey,
            string sourceFormat)
        {
            var data = subAsset.ProviderData != null
                ? new Dictionary<string, string>(subAsset.ProviderData, StringComparer.Ordinal)
                : new Dictionary<string, string>(StringComparer.Ordinal);
            string subClipName = ResolveSubAssetName(subAsset);
            string subClipId = ResolveSubAssetId(subAsset, subClipName);
            AddProviderDataIfMissing(data, "sourceFormat", sourceFormat);
            AddProviderDataIfMissing(data, "usage", AnimationAuthoringResourceUsages.AnimationClip);
            AddProviderDataIfMissing(data, "unityAssetPath", assetPath);
            AddProviderDataIfMissing(data, "unityAssetGuid", entry.UnityAssetGuid);
            AddProviderDataIfMissing(data, "unitySubAssetKey", providerKey);
            AddProviderDataIfMissing(data, "clipName", subClipName);
            AddProviderDataIfMissing(data, "subClipName", subClipName);
            AddProviderDataIfMissing(data, "subClipId", subClipId);
            AddProviderDataIfMissing(data, "subAssetId", subClipId);
            AddProviderDataIfMissing(data, "subAssetName", subClipName);
            AddProviderDataIfMissing(data, "subAssetType", ResolveSubAssetType(subAsset));
            AddProviderDataIfMissing(data, "preloadPolicy", AuthoringResourcePreloadPolicies.AnimationWarmup);
            AddProviderDataIfMissing(data, "bindingKind", AuthoringResourceBindingKind.UnityAsset.ToString());
            AddProviderDataIfMissing(data, "runtimeAvailability", AuthoringResourceRuntimeAvailability.EditorOnly.ToString());
            AddProviderDataIfMissing(data, "sourceLoadability", AuthoringResourceRuntimeAvailability.EditorOnly.ToString());
            return data;
        }

        private static string GetProviderData(AuthoringUnityResourceCatalogEntry entry, string key)
        {
            if (entry == null || entry.ProviderData == null || string.IsNullOrWhiteSpace(key))
                return string.Empty;

            string value;
            return entry.ProviderData.TryGetValue(key, out value) ? value ?? string.Empty : string.Empty;
        }

        private static string GetSubAssetData(AuthoringUnityResourceCatalogSubAsset subAsset, string key)
        {
            if (subAsset == null || subAsset.ProviderData == null || string.IsNullOrWhiteSpace(key))
                return string.Empty;

            string value;
            return subAsset.ProviderData.TryGetValue(key, out value) ? value ?? string.Empty : string.Empty;
        }

        private static string ResolveUnityAssetType(AuthoringUnityResourceCatalogEntry entry)
        {
            return AuthoringResourceProviderUtilities.FirstNonEmpty(
                GetProviderData(entry, "unityObjectType"),
                GetProviderData(entry, "subAssetType"),
                entry != null ? entry.Type : string.Empty,
                entry != null ? entry.UnityMainObjectType : string.Empty);
        }

        private static string ResolveSubAssetType(AuthoringUnityResourceCatalogSubAsset subAsset)
        {
            return AuthoringResourceProviderUtilities.FirstNonEmpty(
                subAsset != null ? subAsset.SubAssetType : string.Empty,
                GetSubAssetData(subAsset, "subAssetType"),
                GetSubAssetData(subAsset, "unityObjectType"));
        }

        private static bool IsAnimationClip(string type)
        {
            return string.Equals(type, "AnimationClip", StringComparison.OrdinalIgnoreCase);
        }

        private static string NormalizeUnityUsage(string usage, string entryType, string assetType)
        {
            if (IsAnimationClip(assetType) || IsAnimationClip(entryType))
                return AnimationAuthoringResourceUsages.AnimationClip;

            return usage ?? string.Empty;
        }

        private static string ResolveSourceFormat(AuthoringUnityResourceCatalogEntry entry, string assetPath)
        {
            string format = AuthoringResourceProviderUtilities.FirstNonEmpty(
                entry != null ? entry.SourceFormat : string.Empty,
                GetProviderData(entry, "sourceFormat"));
            if (!string.IsNullOrWhiteSpace(format))
                return format.TrimStart('.').ToLowerInvariant();

            string extension = Path.GetExtension(assetPath ?? string.Empty);
            return string.IsNullOrWhiteSpace(extension) ? string.Empty : extension.TrimStart('.').ToLowerInvariant();
        }

        private static string ResolveClipName(AuthoringUnityResourceCatalogEntry entry)
        {
            return AuthoringResourceProviderUtilities.FirstNonEmpty(
                GetProviderData(entry, "clipName"),
                GetProviderData(entry, "subClipName"),
                GetProviderData(entry, "subAssetName"),
                GetProviderData(entry, "unityObjectName"),
                entry != null ? entry.Id : string.Empty);
        }

        private static string ResolveSubClipName(AuthoringUnityResourceCatalogEntry entry)
        {
            if (!IsAnimationClip(ResolveUnityAssetType(entry)) && !IsAnimationClip(entry != null ? entry.Type : string.Empty))
                return string.Empty;

            return AuthoringResourceProviderUtilities.FirstNonEmpty(
                GetProviderData(entry, "subClipName"),
                GetProviderData(entry, "subAssetName"),
                GetProviderData(entry, "unityObjectName"),
                ResolveClipName(entry));
        }

        private static string ResolveSubClipId(AuthoringUnityResourceCatalogEntry entry)
        {
            if (!IsAnimationClip(ResolveUnityAssetType(entry)) && !IsAnimationClip(entry != null ? entry.Type : string.Empty))
                return string.Empty;

            return AuthoringResourceProviderUtilities.FirstNonEmpty(
                GetProviderData(entry, "subClipId"),
                GetProviderData(entry, "subAssetId"),
                GetProviderData(entry, "unitySubAssetKey"),
                GetProviderData(entry, "unityLocalFileId"),
                ResolveSubClipName(entry));
        }

        private static string ResolveSubAssetName(AuthoringUnityResourceCatalogSubAsset subAsset)
        {
            return AuthoringResourceProviderUtilities.FirstNonEmpty(
                subAsset != null ? subAsset.SubAssetName : string.Empty,
                GetSubAssetData(subAsset, "subAssetName"),
                GetSubAssetData(subAsset, "subClipName"),
                GetSubAssetData(subAsset, "unityObjectName"));
        }

        private static string ResolveSubAssetId(AuthoringUnityResourceCatalogSubAsset subAsset, string subAssetName)
        {
            return AuthoringResourceProviderUtilities.FirstNonEmpty(
                subAsset != null ? subAsset.SubAssetId : string.Empty,
                GetSubAssetData(subAsset, "subAssetId"),
                GetSubAssetData(subAsset, "subClipId"),
                subAsset != null ? subAsset.UnityLocalFileId : string.Empty,
                GetSubAssetData(subAsset, "unityLocalFileId"),
                subAssetName);
        }

        private static string ResolveSubAssetProviderKey(
            AuthoringUnityResourceCatalogEntry entry,
            AuthoringUnityResourceCatalogSubAsset subAsset,
            string assetPath,
            string subAssetId,
            string subAssetName)
        {
            string explicitKey = AuthoringResourceProviderUtilities.FirstNonEmpty(
                subAsset != null ? subAsset.UnitySubAssetKey : string.Empty,
                GetSubAssetData(subAsset, "unitySubAssetKey"));
            if (!string.IsNullOrWhiteSpace(explicitKey))
                return explicitKey;

            string parentKey = AuthoringResourceProviderUtilities.FirstNonEmpty(
                entry != null ? entry.UnityAssetGuid : string.Empty,
                assetPath,
                entry != null ? entry.Id : string.Empty);
            return parentKey + "#" + AuthoringResourceProviderUtilities.FirstNonEmpty(subAssetId, subAssetName);
        }

        private static void AddClipMetadata(
            Dictionary<string, string> metadata,
            AuthoringUnityResourceCatalogEntry entry,
            string usage,
            AuthoringResourceBindingKind bindingKind,
            AuthoringResourceRuntimeAvailability runtimeAvailability)
        {
            if (metadata == null)
                return;

            string clipName = ResolveClipName(entry);
            string subClipName = ResolveSubClipName(entry);
            string subClipId = ResolveSubClipId(entry);
            if (string.Equals(usage, AnimationAuthoringResourceUsages.AnimationClip, StringComparison.Ordinal))
            {
                AuthoringResourceProviderUtilities.AddIfPresent(metadata, "clipName", clipName);
                AuthoringResourceProviderUtilities.AddIfPresent(metadata, "subClipName", subClipName);
                AuthoringResourceProviderUtilities.AddIfPresent(metadata, "subClipId", subClipId);
                AuthoringResourceProviderUtilities.AddIfPresent(metadata, "preloadPolicy", AuthoringResourcePreloadPolicies.AnimationWarmup);
            }
            metadata["bindingKind"] = bindingKind.ToString();
            metadata["runtimeAvailability"] = runtimeAvailability.ToString();
            metadata["sourceLoadability"] = runtimeAvailability.ToString();
        }

        private static List<string> BuildTags(List<string> sourceLabels, string usage, string extra)
        {
            var tags = sourceLabels != null ? new List<string>(sourceLabels) : new List<string>();
            AddTag(tags, usage);
            AddTag(tags, extra);
            return tags;
        }

        private static void AddTag(List<string> tags, string tag)
        {
            if (tags == null || string.IsNullOrWhiteSpace(tag))
                return;

            for (int i = 0; i < tags.Count; i++)
            {
                if (string.Equals(tags[i], tag, StringComparison.Ordinal))
                    return;
            }

            tags.Add(tag);
        }

        private static void AddProviderDataIfMissing(Dictionary<string, string> data, string key, string value)
        {
            if (data == null || string.IsNullOrWhiteSpace(key) || string.IsNullOrWhiteSpace(value) || data.ContainsKey(key))
                return;

            data[key] = value;
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
