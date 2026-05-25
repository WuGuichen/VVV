using System;
using System.Collections.Generic;
using System.IO;

namespace MxFramework.Authoring
{
    public sealed class GlobalResourceBuildProfileAuthoringResourceProvider : IAuthoringResourceProvider
    {
        public string ProviderId
        {
            get { return AuthoringResourceProviderIds.GlobalResourceBuildProfile; }
        }

        public AuthoringResourceProviderDescriptor Describe(AuthoringResourceProviderContext context)
        {
            bool available = context != null && context.GlobalResourceBuildProfile != null;
            return new AuthoringResourceProviderDescriptor
            {
                ProviderId = ProviderId,
                DisplayName = "Global Resource Build Profile",
                SourceKind = AuthoringResourceSourceKind.GeneratedAsset,
                Available = available,
                Status = available ? "Ready" : "Unavailable",
                DiagnosticCode = available ? string.Empty : AuthoringResourceDiagnosticCodes.ProviderUnavailable,
                Message = available ? string.Empty : "Global resource build profile is not available."
            };
        }

        public AuthoringResourceCollection BuildResourceCollection(AuthoringResourceProviderContext context)
        {
            var collection = new AuthoringResourceCollection
            {
                ScopeId = context != null && !string.IsNullOrWhiteSpace(context.ScopeId)
                    ? context.ScopeId
                    : "globalResourceBuildProfile"
            };

            AuthoringResourceProviderDescriptor descriptor = Describe(context);
            collection.Providers.Add(descriptor);
            if (context == null || context.GlobalResourceBuildProfile == null)
            {
                collection.Diagnostics.Add(new AuthoringResourceDiagnostic
                {
                    Severity = CharacterAuthoringValidationSeverity.Warning,
                    Code = AuthoringResourceDiagnosticCodes.ProviderUnavailable,
                    ProviderId = ProviderId,
                    Message = descriptor.Message,
                    SuggestedFix = "Create Assets/Config/MxFramework/ResourceProfiles/global_resource_build_profile.json, then refresh Resource Manager."
                });
                return collection;
            }

            AddCollectionMetadata(collection, context);
            AddGeneratedArtifactDiagnostics(collection, context);

            GlobalResourceBuildProfileValidationReport validation = GlobalResourceBuildProfileValidator.Validate(context.GlobalResourceBuildProfile);
            var diagnosticsByKey = GroupDiagnosticsByKey(validation);
            for (int i = 0; i < context.GlobalResourceBuildProfile.Entries.Count; i++)
            {
                GlobalResourceBuildProfileEntry entry = context.GlobalResourceBuildProfile.Entries[i];
                if (entry == null || entry.ResourceKey == null)
                    continue;

                AuthoringResourceItem item = FromProfileEntry(context.GlobalResourceBuildProfile, entry, context);
                string key = CanonicalKey(entry.ResourceKey, context.GlobalResourceBuildProfile.PackageId);
                if (diagnosticsByKey.TryGetValue(key, out List<GlobalResourceBuildProfileValidationIssue> issues))
                {
                    for (int issueIndex = 0; issueIndex < issues.Count; issueIndex++)
                        item.Diagnostics.Add(ToDiagnostic(issues[issueIndex], item));
                }

                collection.Items.Add(item);
            }

            for (int i = 0; i < validation.Issues.Count; i++)
            {
                GlobalResourceBuildProfileValidationIssue issue = validation.Issues[i];
                if (string.IsNullOrWhiteSpace(issue.ResourceKey))
                    collection.Diagnostics.Add(ToCollectionDiagnostic(issue));
            }

            return collection;
        }

        private static AuthoringResourceItem FromProfileEntry(
            GlobalResourceBuildProfile profile,
            GlobalResourceBuildProfileEntry entry,
            AuthoringResourceProviderContext context)
        {
            string typeId = EffectiveType(entry.ResourceKey);
            string keyText = CanonicalKey(entry.ResourceKey, profile.PackageId);
            string stableId = "global.profile." + AuthoringResourceProviderUtilities.SanitizeStableSegment(keyText);
            string usage = GetProviderData(entry.ProviderData, "usage");
            var item = new AuthoringResourceItem
            {
                ResourceId = AuthoringResourceProviderUtilities.BuildResourceId(AuthoringResourceProviderIds.GlobalResourceBuildProfile, stableId, entry.ResourceKey.Id),
                StableId = stableId,
                DisplayName = AuthoringResourceProviderUtilities.FirstNonEmpty(entry.ResourceKey.Id, stableId),
                Kind = AuthoringResourceProviderUtilities.MapRuntimeTypeToLibraryKind(typeId, usage),
                Usage = usage,
                SourceProviderId = AuthoringResourceProviderIds.GlobalResourceBuildProfile,
                SourceKind = AuthoringResourceSourceKind.GeneratedAsset,
                BindingKind = entry.RuntimeLoadable && !entry.EditorOnly ? AuthoringResourceBindingKind.ResourceManagerAsset : AuthoringResourceBindingKind.UnityEditorOnlyAsset,
                ImportStatus = AuthoringResourceImportStatus.Clean,
                RuntimeAvailability = entry.RuntimeLoadable && !entry.EditorOnly
                    ? AuthoringResourceRuntimeAvailability.RuntimeReady
                    : AuthoringResourceRuntimeAvailability.EditorOnly,
                Tags = entry.Labels != null ? new List<string>(entry.Labels) : new List<string>()
            };

            AddItemMetadata(item, profile, entry, context, keyText, typeId);
            AddBindings(item, entry, typeId);
            return item;
        }

        private static void AddCollectionMetadata(AuthoringResourceCollection collection, AuthoringResourceProviderContext context)
        {
            collection.Metadata["globalResourceBuildProfilePath"] = context.GlobalResourceBuildProfilePath ?? string.Empty;
            collection.Metadata["projectRootPath"] = context.ProjectRootPath ?? string.Empty;
            collection.Metadata["globalRuntimeCatalogPath"] = context.GlobalRuntimeCatalogPath ?? string.Empty;
            collection.Metadata["globalPreloadGroupsPath"] = context.GlobalPreloadGroupsPath ?? string.Empty;
            collection.Metadata["globalBundleDependenciesPath"] = context.GlobalBundleDependenciesPath ?? string.Empty;
            collection.Metadata["globalResourceBuildReportPath"] = context.GlobalResourceBuildReportPath ?? string.Empty;
        }

        private static void AddGeneratedArtifactDiagnostics(AuthoringResourceCollection collection, AuthoringResourceProviderContext context)
        {
            AddArtifactDiagnostic(collection, context.GlobalRuntimeCatalogPath, "GLOBAL_RESOURCE_RUNTIME_CATALOG_MISSING", "Generated global runtime catalog is missing.");
            AddArtifactDiagnostic(collection, context.GlobalPreloadGroupsPath, "GLOBAL_RESOURCE_PRELOAD_GROUPS_MISSING", "Generated global preload groups file is missing.");
            AddArtifactDiagnostic(collection, context.GlobalBundleDependenciesPath, "GLOBAL_RESOURCE_BUNDLE_DEPENDENCIES_MISSING", "Generated global bundle dependency manifest is missing.");
            AddArtifactDiagnostic(collection, context.GlobalResourceBuildReportPath, "GLOBAL_RESOURCE_BUILD_REPORT_MISSING", "Generated global resource build report is missing.");
        }

        private static void AddArtifactDiagnostic(AuthoringResourceCollection collection, string projectRelativePath, string code, string message)
        {
            if (string.IsNullOrWhiteSpace(projectRelativePath))
                return;
            string fullPath = !string.IsNullOrWhiteSpace(collection.Metadata.TryGetValue("projectRootPath", out string root) ? root : string.Empty)
                ? Path.Combine(root, projectRelativePath)
                : projectRelativePath;
            if (File.Exists(fullPath))
                return;

            collection.Diagnostics.Add(new AuthoringResourceDiagnostic
            {
                Severity = CharacterAuthoringValidationSeverity.Warning,
                Code = code,
                ProviderId = AuthoringResourceProviderIds.GlobalResourceBuildProfile,
                Message = message,
                SourceField = projectRelativePath,
                SuggestedFix = "Run MxFramework/Resources/Build Global Player Resource Catalog in Unity Editor."
            });
        }

        private static void AddItemMetadata(
            AuthoringResourceItem item,
            GlobalResourceBuildProfile profile,
            GlobalResourceBuildProfileEntry entry,
            AuthoringResourceProviderContext context,
            string keyText,
            string typeId)
        {
            item.Metadata["buildProfileId"] = profile.ProfileId ?? string.Empty;
            item.Metadata["catalogId"] = profile.CatalogId ?? string.Empty;
            item.Metadata["packageId"] = EffectivePackageId(entry.ResourceKey, profile.PackageId);
            item.Metadata["runtimeResourceKey"] = entry.ResourceKey.Id ?? string.Empty;
            item.Metadata["resourceKey"] = keyText;
            item.Metadata["typeId"] = typeId;
            item.Metadata["variant"] = entry.ResourceKey.Variant ?? string.Empty;
            item.Metadata["bundleRule"] = entry.BundleRule ?? string.Empty;
            item.Metadata["preloadGroups"] = entry.PreloadGroups != null ? string.Join(",", entry.PreloadGroups) : string.Empty;
            item.Metadata["profilePath"] = context.GlobalResourceBuildProfilePath ?? string.Empty;
            item.Metadata["generatedRuntimeCatalogPath"] = context.GlobalRuntimeCatalogPath ?? string.Empty;
            item.Metadata["generatedPreloadGroupsPath"] = context.GlobalPreloadGroupsPath ?? string.Empty;
            item.Metadata["generatedBundleDependenciesPath"] = context.GlobalBundleDependenciesPath ?? string.Empty;
            item.Metadata["generatedBuildReportPath"] = context.GlobalResourceBuildReportPath ?? string.Empty;
        }

        private static void AddBindings(AuthoringResourceItem item, GlobalResourceBuildProfileEntry entry, string typeId)
        {
            item.ProviderBindings.Add(new AuthoringResourceProviderBinding
            {
                ProviderId = AuthoringResourceProviderIds.GlobalResourceBuildProfile,
                BindingKind = item.BindingKind,
                BindingKeyKind = AuthoringResourceBindingKeyKinds.RuntimeResourceKey,
                DisplayValue = entry.ResourceKey.Id ?? string.Empty,
                IsPrimary = true,
                ProviderResourceKey = entry.ResourceKey.Id ?? string.Empty,
                RuntimeResourceKey = entry.ResourceKey.Id ?? string.Empty,
                UnityGuid = entry.Source != null ? entry.Source.UnityGuid : string.Empty,
                UnityAssetPath = entry.Source != null ? entry.Source.UnityAssetPath : string.Empty,
                AssetType = typeId,
                ProviderData = entry.ProviderData != null
                    ? new Dictionary<string, string>(entry.ProviderData)
                    : new Dictionary<string, string>()
            });

            if (entry.Source != null && (!string.IsNullOrWhiteSpace(entry.Source.UnityGuid) || !string.IsNullOrWhiteSpace(entry.Source.UnityAssetPath)))
            {
                item.ProviderBindings.Add(new AuthoringResourceProviderBinding
                {
                    ProviderId = AuthoringResourceProviderIds.UnityAssetDatabase,
                    BindingKind = AuthoringResourceBindingKind.UnityAsset,
                    BindingKeyKind = AuthoringResourceBindingKeyKinds.UnityGuid,
                    DisplayValue = AuthoringResourceProviderUtilities.FirstNonEmpty(entry.Source.UnityAssetPath, entry.Source.UnityGuid),
                    ProviderResourceKey = entry.Source.UnityGuid ?? string.Empty,
                    UnityGuid = entry.Source.UnityGuid ?? string.Empty,
                    UnityAssetPath = entry.Source.UnityAssetPath ?? string.Empty,
                    AssetType = typeId
                });
            }
        }

        private static Dictionary<string, List<GlobalResourceBuildProfileValidationIssue>> GroupDiagnosticsByKey(GlobalResourceBuildProfileValidationReport validation)
        {
            var result = new Dictionary<string, List<GlobalResourceBuildProfileValidationIssue>>(StringComparer.Ordinal);
            if (validation == null)
                return result;

            for (int i = 0; i < validation.Issues.Count; i++)
            {
                GlobalResourceBuildProfileValidationIssue issue = validation.Issues[i];
                if (issue == null || string.IsNullOrWhiteSpace(issue.ResourceKey))
                    continue;

                if (!result.TryGetValue(issue.ResourceKey, out List<GlobalResourceBuildProfileValidationIssue> issues))
                {
                    issues = new List<GlobalResourceBuildProfileValidationIssue>();
                    result[issue.ResourceKey] = issues;
                }

                issues.Add(issue);
            }

            return result;
        }

        private static AuthoringResourceDiagnostic ToDiagnostic(GlobalResourceBuildProfileValidationIssue issue, AuthoringResourceItem item)
        {
            return new AuthoringResourceDiagnostic
            {
                Severity = issue.Severity == IssueSeverity.Error ? CharacterAuthoringValidationSeverity.Error : CharacterAuthoringValidationSeverity.Warning,
                Code = issue.Code ?? string.Empty,
                ResourceId = item != null ? item.ResourceId : string.Empty,
                ResourceStableId = item != null ? item.StableId : string.Empty,
                RuntimeResourceKey = item != null ? item.Metadata["runtimeResourceKey"] : string.Empty,
                ProviderId = AuthoringResourceProviderIds.GlobalResourceBuildProfile,
                SourceField = issue.SourcePath ?? string.Empty,
                Message = issue.Message ?? string.Empty
            };
        }

        private static AuthoringResourceDiagnostic ToCollectionDiagnostic(GlobalResourceBuildProfileValidationIssue issue)
        {
            return new AuthoringResourceDiagnostic
            {
                Severity = issue.Severity == IssueSeverity.Error ? CharacterAuthoringValidationSeverity.Error : CharacterAuthoringValidationSeverity.Warning,
                Code = issue.Code ?? string.Empty,
                ProviderId = AuthoringResourceProviderIds.GlobalResourceBuildProfile,
                SourceField = issue.SourcePath ?? string.Empty,
                RuntimeResourceKey = issue.ResourceKey ?? string.Empty,
                Message = issue.Message ?? string.Empty
            };
        }

        private static string CanonicalKey(GlobalResourceBuildProfileResourceKey key, string defaultPackageId)
        {
            if (key == null)
                return string.Empty;

            return EffectivePackageId(key, defaultPackageId) + ":" + EffectiveType(key) + ":" + (key.Id ?? string.Empty) + ":" + (key.Variant ?? string.Empty);
        }

        private static string EffectivePackageId(GlobalResourceBuildProfileResourceKey key, string defaultPackageId)
        {
            if (key != null && !string.IsNullOrWhiteSpace(key.PackageId))
                return key.PackageId;

            return defaultPackageId ?? string.Empty;
        }

        private static string EffectiveType(GlobalResourceBuildProfileResourceKey key)
        {
            if (key == null)
                return string.Empty;

            return !string.IsNullOrWhiteSpace(key.Type) ? key.Type : (key.TypeId ?? string.Empty);
        }

        private static string GetProviderData(Dictionary<string, string> data, string key)
        {
            if (data == null || string.IsNullOrWhiteSpace(key))
                return string.Empty;

            return data.TryGetValue(key, out string value) ? value ?? string.Empty : string.Empty;
        }
    }
}
