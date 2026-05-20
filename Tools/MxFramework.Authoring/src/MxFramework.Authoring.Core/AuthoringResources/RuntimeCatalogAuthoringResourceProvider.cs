using System;
using System.Collections.Generic;

namespace MxFramework.Authoring
{
    public sealed class RuntimeCatalogAuthoringResourceProvider : IAuthoringResourceProvider
    {
        public string ProviderId
        {
            get { return AuthoringResourceProviderIds.RuntimeCatalog; }
        }

        public AuthoringResourceProviderDescriptor Describe(AuthoringResourceProviderContext context)
        {
            bool available = context != null && context.RuntimeResourceCatalog != null;
            return new AuthoringResourceProviderDescriptor
            {
                ProviderId = ProviderId,
                DisplayName = "Runtime Resource Catalog",
                SourceKind = AuthoringResourceSourceKind.RuntimeCatalogAsset,
                Available = available,
                Status = available ? "Ready" : "Unavailable",
                DiagnosticCode = available ? string.Empty : AuthoringResourceDiagnosticCodes.ProviderUnavailable,
                Message = available ? string.Empty : "Runtime resource catalog is not available. Compile resource plans before selecting runtime-loadable assets."
            };
        }

        public AuthoringResourceCollection BuildResourceCollection(AuthoringResourceProviderContext context)
        {
            return FromRuntimeResourceCatalog(context != null ? context.RuntimeResourceCatalog : null, context);
        }

        public static AuthoringResourceCollection FromRuntimeResourceCatalog(
            RuntimeResourceCatalogDocument catalog,
            AuthoringResourceProviderContext context)
        {
            var provider = new RuntimeCatalogAuthoringResourceProvider();
            if (context != null && catalog != null && context.RuntimeResourceCatalog == null)
                context.RuntimeResourceCatalog = catalog;
            var collection = new AuthoringResourceCollection
            {
                ScopeId = context != null && !string.IsNullOrWhiteSpace(context.ScopeId)
                    ? context.ScopeId
                    : "runtimeCatalog"
            };
            if (context != null)
            {
                AuthoringResourceProviderUtilities.AddIfPresent(collection.Metadata, "packageId", context.PackageId);
                AuthoringResourceProviderUtilities.AddIfPresent(collection.Metadata, "packagePath", context.PackagePath);
                AuthoringResourceProviderUtilities.AddIfPresent(collection.Metadata, "projectRootPath", context.ProjectRootPath);
                AuthoringResourceProviderUtilities.AddIfPresent(collection.Metadata, "runtimeResourceCatalogPath", context.RuntimeResourceCatalogPath);
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
                    SuggestedFix = "Run resource plan compilation, then refresh the Resource Manager."
                });
                return collection;
            }

            if (catalog.Entries != null)
            {
                for (int i = 0; i < catalog.Entries.Count; i++)
                {
                    RuntimeResourceCatalogEntryDocument entry = catalog.Entries[i];
                    if (entry != null)
                        collection.Items.Add(FromRuntimeEntry(entry, catalog, context));
                }
            }

            return collection;
        }

        private static AuthoringResourceItem FromRuntimeEntry(
            RuntimeResourceCatalogEntryDocument entry,
            RuntimeResourceCatalogDocument catalog,
            AuthoringResourceProviderContext context)
        {
            string packageStableId = GetProviderData(entry.ProviderData, "stableId");
            string packageResourceKey = GetProviderData(entry.ProviderData, "packageResourceKey");
            string usage = GetProviderData(entry.ProviderData, "usage");
            string stableId = "runtime." + AuthoringResourceProviderUtilities.SanitizeStableSegment(
                AuthoringResourceProviderUtilities.FirstNonEmpty(packageStableId, entry.Id, entry.Address));
            string resourceKey = entry.Id ?? string.Empty;
            var item = new AuthoringResourceItem
            {
                ResourceId = AuthoringResourceProviderUtilities.BuildResourceId(AuthoringResourceProviderIds.RuntimeCatalog, stableId, resourceKey),
                StableId = stableId,
                DisplayName = AuthoringResourceProviderUtilities.GetFileDisplayName(entry.Address, AuthoringResourceProviderUtilities.FirstNonEmpty(resourceKey, stableId)),
                Kind = AuthoringResourceProviderUtilities.MapRuntimeTypeToLibraryKind(entry.Type, usage),
                Usage = usage,
                SourceProviderId = AuthoringResourceProviderIds.RuntimeCatalog,
                SourceKind = AuthoringResourceSourceKind.RuntimeCatalogAsset,
                BindingKind = AuthoringResourceBindingKind.ResourceManagerAsset,
                ImportStatus = AuthoringResourceImportStatus.Clean,
                RuntimeAvailability = AuthoringResourceRuntimeAvailability.RuntimeReady,
                Tags = entry.Labels != null ? new List<string>(entry.Labels) : new List<string>()
            };

            AddMetadata(item, entry, catalog, context, packageStableId, packageResourceKey);
            AddRuntimeBindings(item, entry, packageResourceKey);
            AddDependencyBindings(item, entry);
            return item;
        }

        private static void AddMetadata(
            AuthoringResourceItem item,
            RuntimeResourceCatalogEntryDocument entry,
            RuntimeResourceCatalogDocument catalog,
            AuthoringResourceProviderContext context,
            string packageStableId,
            string packageResourceKey)
        {
            AuthoringResourceProviderUtilities.AddIfPresent(item.Metadata, "catalogId", catalog != null ? catalog.CatalogId : string.Empty);
            AuthoringResourceProviderUtilities.AddIfPresent(item.Metadata, "packageId", entry.PackageId);
            AuthoringResourceProviderUtilities.AddIfPresent(item.Metadata, "packageStableId", packageStableId);
            AuthoringResourceProviderUtilities.AddIfPresent(item.Metadata, "packageResourceKey", packageResourceKey);
            AuthoringResourceProviderUtilities.AddIfPresent(item.Metadata, "runtimeResourceKey", entry.Id);
            AuthoringResourceProviderUtilities.AddIfPresent(item.Metadata, "variant", entry.Variant);
            AuthoringResourceProviderUtilities.AddIfPresent(item.Metadata, "providerId", entry.Provider);
            AuthoringResourceProviderUtilities.AddIfPresent(item.Metadata, "address", entry.Address);
            AuthoringResourceProviderUtilities.AddIfPresent(item.Metadata, "hash", entry.Hash);
            AuthoringResourceProviderUtilities.AddIfPresent(item.Metadata, "retainPolicy", GetProviderData(entry.ProviderData, "retainPolicy"));
            AuthoringResourceProviderUtilities.AddIfPresent(item.Metadata, "sourceRelativePath", GetProviderData(entry.ProviderData, "sourceRelativePath"));
            AuthoringResourceProviderUtilities.AddIfPresent(item.Metadata, "sourceFormat", GetProviderData(entry.ProviderData, "sourceFormat"));
            if (context != null)
                AuthoringResourceProviderUtilities.AddIfPresent(item.Metadata, "runtimeResourceCatalogPath", context.RuntimeResourceCatalogPath);
            item.Metadata["size"] = entry.Size.ToString(System.Globalization.CultureInfo.InvariantCulture);
            item.Metadata["allowOverride"] = entry.AllowOverride ? "true" : "false";
        }

        private static void AddRuntimeBindings(
            AuthoringResourceItem item,
            RuntimeResourceCatalogEntryDocument entry,
            string packageResourceKey)
        {
            item.ProviderBindings.Add(new AuthoringResourceProviderBinding
            {
                ProviderId = AuthoringResourceProviderIds.RuntimeCatalog,
                BindingKind = AuthoringResourceBindingKind.ResourceManagerAsset,
                BindingKeyKind = AuthoringResourceBindingKeyKinds.RuntimeResourceKey,
                DisplayValue = entry.Id ?? string.Empty,
                IsPrimary = true,
                ProviderResourceKey = entry.Id ?? string.Empty,
                RuntimeResourceKey = entry.Id ?? string.Empty,
                Address = entry.Address ?? string.Empty,
                AssetType = entry.Type ?? string.Empty,
                Hash = entry.Hash ?? string.Empty,
                ProviderData = entry.ProviderData != null
                    ? new Dictionary<string, string>(entry.ProviderData, StringComparer.Ordinal)
                    : new Dictionary<string, string>(StringComparer.Ordinal)
            });

            if (!string.IsNullOrWhiteSpace(packageResourceKey))
            {
                item.ProviderBindings.Add(new AuthoringResourceProviderBinding
                {
                    ProviderId = AuthoringResourceProviderIds.RuntimeCatalog,
                    BindingKind = AuthoringResourceBindingKind.PackageResource,
                    BindingKeyKind = AuthoringResourceBindingKeyKinds.PackageResourceKey,
                    DisplayValue = packageResourceKey,
                    ProviderResourceKey = packageResourceKey,
                    PackageResourceKey = packageResourceKey
                });
            }
        }

        private static void AddDependencyBindings(AuthoringResourceItem item, RuntimeResourceCatalogEntryDocument entry)
        {
            if (entry == null || entry.Dependencies == null)
                return;

            for (int i = 0; i < entry.Dependencies.Count; i++)
            {
                RuntimeResourceKeyDocument dependency = entry.Dependencies[i];
                if (dependency == null || string.IsNullOrWhiteSpace(dependency.Id))
                    continue;

                item.ProviderBindings.Add(new AuthoringResourceProviderBinding
                {
                    ProviderId = AuthoringResourceProviderIds.RuntimeCatalog,
                    BindingKind = AuthoringResourceBindingKind.ResourceManagerAsset,
                    BindingKeyKind = AuthoringResourceBindingKeyKinds.Dependency,
                    DisplayValue = dependency.Id,
                    ProviderResourceKey = dependency.Id,
                    RuntimeResourceKey = dependency.Id,
                    AssetType = dependency.Type ?? string.Empty,
                    ProviderData = new Dictionary<string, string>
                    {
                        { "variant", dependency.Variant ?? string.Empty },
                        { "packageId", dependency.PackageId ?? string.Empty }
                    }
                });
            }
        }

        private static string GetProviderData(Dictionary<string, string> data, string key)
        {
            if (data == null || string.IsNullOrWhiteSpace(key))
                return string.Empty;

            string value;
            return data.TryGetValue(key, out value) ? value ?? string.Empty : string.Empty;
        }
    }
}
