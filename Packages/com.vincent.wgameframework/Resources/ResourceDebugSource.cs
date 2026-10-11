using System;
using System.Text;
using MxFramework.Diagnostics;

namespace MxFramework.Resources
{
    public sealed class ResourceDebugSource : IFrameworkDebugSource
    {
        private readonly IResourceManager _resourceManager;

        public ResourceDebugSource(IResourceManager resourceManager, string name = "Resources")
        {
            _resourceManager = resourceManager ?? throw new ArgumentNullException(nameof(resourceManager));
            Name = string.IsNullOrWhiteSpace(name) ? "Resources" : name;
        }

        public string Name { get; }
        public FrameworkDebugMode Mode => FrameworkDebugMode.Runtime;
        public bool IsAvailable => true;

        public FrameworkDebugSnapshot CreateSnapshot()
        {
            return ResourceDebugSnapshotFormatter.ToFrameworkSnapshot(
                _resourceManager.CreateDebugSnapshot(),
                Name);
        }
    }

    public static class ResourceDebugSnapshotFormatter
    {
        public static FrameworkDebugSnapshot ToFrameworkSnapshot(ResourceDebugSnapshot snapshot, string sourceName = "Resources")
        {
            if (snapshot == null)
                throw new ArgumentNullException(nameof(snapshot));

            return new FrameworkDebugSnapshot(
                string.IsNullOrWhiteSpace(sourceName) ? "Resources" : sourceName,
                FrameworkDebugMode.Runtime,
                new[]
                {
                    new FrameworkDebugSection("Summary", CreateSummary(snapshot)),
                    new FrameworkDebugSection("Catalogs", CreateCatalogs(snapshot)),
                    new FrameworkDebugSection("Entry Origins", CreateEntryOrigins(snapshot)),
                    new FrameworkDebugSection("Recent Errors", CreateRecentErrors(snapshot)),
                    new FrameworkDebugSection("Recent Evictions", CreateRecentEvictions(snapshot))
                });
        }

        private static string CreateSummary(ResourceDebugSnapshot snapshot)
        {
            var builder = new StringBuilder();
            builder.Append("catalogs: ").Append(snapshot.CatalogCount).Append('\n');
            builder.Append("entries: ").Append(snapshot.EntryCount).Append('\n');
            builder.Append("providers: ").Append(snapshot.ProviderCount).Append('\n');
            builder.Append("loaded: ").Append(snapshot.LoadedCount).Append('\n');
            builder.Append("loading: ").Append(snapshot.LoadingCount).Append('\n');
            builder.Append("failed: ").Append(snapshot.FailedCount).Append('\n');
            builder.Append("bundles: ").Append(snapshot.BundleCount).Append('\n');
            builder.Append("totalRefCount: ").Append(snapshot.TotalRefCount).Append('\n');
            builder.Append("retained: ").Append(snapshot.RetainedCount).Append('\n');
            builder.Append("evictable: ").Append(snapshot.EvictableCount).Append('\n');
            builder.Append("pinned: ").Append(snapshot.PinnedCount).Append('\n');
            builder.Append("retainedBytes: ").Append(snapshot.RetainedBytes).Append('\n');
            builder.Append("retainBudgetBytes: ").Append(snapshot.RetainBudgetBytes).Append('\n');
            builder.Append("retainBudgetOverageBytes: ").Append(snapshot.RetainBudgetOverageBytes).Append('\n');
            builder.Append("retainBudgetExceeded: ").Append(snapshot.RetainBudgetExceeded ? "true" : "false").Append('\n');
            builder.Append("retainPolicies: ").Append(snapshot.RetainPolicyCount);
            return builder.ToString();
        }

        private static string CreateCatalogs(ResourceDebugSnapshot snapshot)
        {
            if (snapshot.Catalogs == null || snapshot.Catalogs.Count == 0)
                return "none";

            var builder = new StringBuilder();
            for (int i = 0; i < snapshot.Catalogs.Count; i++)
            {
                ResourceCatalogSummary catalog = snapshot.Catalogs[i];
                builder.Append(catalog.CatalogId)
                    .Append(" package=")
                    .Append(catalog.PackageId)
                    .Append(" entries=")
                    .Append(catalog.EntryCount);
                if (i + 1 < snapshot.Catalogs.Count)
                    builder.Append('\n');
            }

            return builder.ToString();
        }

        private static string CreateEntryOrigins(ResourceDebugSnapshot snapshot)
        {
            if (snapshot.EntryOrigins == null || snapshot.EntryOrigins.Count == 0)
                return "none";

            var builder = new StringBuilder();
            for (int i = 0; i < snapshot.EntryOrigins.Count; i++)
            {
                ResourceEntryOrigin origin = snapshot.EntryOrigins[i];
                builder.Append(origin.Key)
                    .Append(" catalog=")
                    .Append(origin.CatalogId)
                    .Append(" package=")
                    .Append(origin.PackageId)
                    .Append(" override=")
                    .Append(origin.OverridesAnotherEntry ? "true" : "false");
                if (i + 1 < snapshot.EntryOrigins.Count)
                    builder.Append('\n');
            }

            return builder.ToString();
        }

        private static string CreateRecentErrors(ResourceDebugSnapshot snapshot)
        {
            if (snapshot.RecentErrors == null || snapshot.RecentErrors.Count == 0)
                return "none";

            var builder = new StringBuilder();
            for (int i = 0; i < snapshot.RecentErrors.Count; i++)
            {
                ResourceError error = snapshot.RecentErrors[i];
                builder.Append(error.Code)
                    .Append(" key=")
                    .Append(error.Key)
                    .Append(" provider=")
                    .Append(error.ProviderId)
                    .Append(" address=")
                    .Append(error.Address)
                    .Append(" message=")
                    .Append(error.Message);
                if (i + 1 < snapshot.RecentErrors.Count)
                    builder.Append('\n');
            }

            return builder.ToString();
        }

        private static string CreateRecentEvictions(ResourceDebugSnapshot snapshot)
        {
            if (snapshot.RecentEvictions == null || snapshot.RecentEvictions.Count == 0)
                return "none";

            var builder = new StringBuilder();
            for (int i = 0; i < snapshot.RecentEvictions.Count; i++)
            {
                ResourceEvictionRecord eviction = snapshot.RecentEvictions[i];
                builder.Append(eviction.Reason)
                    .Append(" key=")
                    .Append(eviction.Key)
                    .Append(" provider=")
                    .Append(eviction.ProviderId);
                if (i + 1 < snapshot.RecentEvictions.Count)
                    builder.Append('\n');
            }

            return builder.ToString();
        }
    }
}
