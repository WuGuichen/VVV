using System.Collections.Generic;

namespace MxFramework.Resources
{
    public sealed class ResourceDebugSnapshot
    {
        public ResourceDebugSnapshot(
            int catalogCount,
            int entryCount,
            int providerCount,
            int loadedCount,
            int loadingCount,
            int failedCount,
            int bundleCount,
            int totalRefCount,
            IReadOnlyList<ResourceError> recentErrors,
            IReadOnlyList<ResourceCatalogSummary> catalogs,
            IReadOnlyList<ResourceEntryOrigin> entryOrigins,
            int retainedCount = 0,
            int evictableCount = 0,
            int pinnedCount = 0,
            int retainPolicyCount = 0,
            IReadOnlyList<ResourceEvictionRecord> recentEvictions = null)
        {
            CatalogCount = catalogCount;
            EntryCount = entryCount;
            ProviderCount = providerCount;
            LoadedCount = loadedCount;
            LoadingCount = loadingCount;
            FailedCount = failedCount;
            BundleCount = bundleCount;
            TotalRefCount = totalRefCount;
            RecentErrors = recentErrors;
            Catalogs = catalogs;
            EntryOrigins = entryOrigins;
            RetainedCount = retainedCount;
            EvictableCount = evictableCount;
            PinnedCount = pinnedCount;
            RetainPolicyCount = retainPolicyCount;
            RecentEvictions = recentEvictions ?? new List<ResourceEvictionRecord>();
        }

        public int CatalogCount { get; }
        public int EntryCount { get; }
        public int ProviderCount { get; }
        public int LoadedCount { get; }
        public int LoadingCount { get; }
        public int FailedCount { get; }
        public int BundleCount { get; }
        public int TotalRefCount { get; }
        public int RetainedCount { get; }
        public int EvictableCount { get; }
        public int PinnedCount { get; }
        public int RetainPolicyCount { get; }
        public IReadOnlyList<ResourceError> RecentErrors { get; }
        public IReadOnlyList<ResourceCatalogSummary> Catalogs { get; }
        public IReadOnlyList<ResourceEntryOrigin> EntryOrigins { get; }
        public IReadOnlyList<ResourceEvictionRecord> RecentEvictions { get; }
    }

    public readonly struct ResourceCatalogSummary
    {
        public ResourceCatalogSummary(string catalogId, string packageId, int entryCount)
        {
            CatalogId = catalogId ?? string.Empty;
            PackageId = packageId ?? string.Empty;
            EntryCount = entryCount;
        }

        public string CatalogId { get; }
        public string PackageId { get; }
        public int EntryCount { get; }
    }

    public readonly struct ResourceEntryOrigin
    {
        public ResourceEntryOrigin(ResourceKey key, string catalogId, string packageId, bool overridesAnotherEntry)
        {
            Key = key;
            CatalogId = catalogId ?? string.Empty;
            PackageId = packageId ?? string.Empty;
            OverridesAnotherEntry = overridesAnotherEntry;
        }

        public ResourceKey Key { get; }
        public string CatalogId { get; }
        public string PackageId { get; }
        public bool OverridesAnotherEntry { get; }
    }

    public readonly struct ResourceEvictionRecord
    {
        public ResourceEvictionRecord(ResourceKey key, string providerId, string reason)
        {
            Key = key;
            ProviderId = providerId ?? string.Empty;
            Reason = reason ?? string.Empty;
        }

        public ResourceKey Key { get; }
        public string ProviderId { get; }
        public string Reason { get; }
    }
}
