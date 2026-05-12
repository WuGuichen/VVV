using System;
using System.Collections.Generic;

namespace MxFramework.Resources
{
    public sealed class ResourceCatalog
    {
        private readonly List<ResourceCatalogEntry> _entries;

        public ResourceCatalog(string catalogId, string packageId, IEnumerable<ResourceCatalogEntry> entries)
        {
            CatalogId = catalogId ?? string.Empty;
            PackageId = packageId ?? string.Empty;
            _entries = entries != null
                ? new List<ResourceCatalogEntry>(entries)
                : new List<ResourceCatalogEntry>();
        }

        public int SchemaVersion { get; } = 1;
        public string CatalogId { get; }
        public string PackageId { get; }
        public IReadOnlyList<ResourceCatalogEntry> Entries => _entries;
    }

    public sealed class ResourceCatalogEntry
    {
        private readonly List<string> _labels;
        private readonly List<ResourceKey> _dependencies;
        private readonly Dictionary<string, string> _providerData;

        public ResourceCatalogEntry(
            string id,
            string typeId,
            string providerId,
            string address,
            string variant = "",
            string packageId = "",
            IEnumerable<ResourceKey> dependencies = null,
            IEnumerable<string> labels = null,
            string hash = "",
            long size = 0,
            bool allowOverride = false,
            IReadOnlyDictionary<string, string> providerData = null)
        {
            Id = id ?? string.Empty;
            TypeId = typeId ?? string.Empty;
            ProviderId = providerId ?? string.Empty;
            Address = address ?? string.Empty;
            Variant = variant ?? string.Empty;
            PackageId = packageId ?? string.Empty;
            Hash = hash ?? string.Empty;
            Size = size < 0 ? 0 : size;
            AllowOverride = allowOverride;
            _dependencies = dependencies != null
                ? new List<ResourceKey>(dependencies)
                : new List<ResourceKey>();
            _labels = labels != null
                ? new List<string>(labels)
                : new List<string>();
            _providerData = providerData != null
                ? new Dictionary<string, string>(providerData, StringComparer.Ordinal)
                : new Dictionary<string, string>(StringComparer.Ordinal);
        }

        public string Id { get; }
        public string TypeId { get; }
        public string Variant { get; }
        public string PackageId { get; }
        public string ProviderId { get; }
        public string Address { get; }
        public IReadOnlyList<ResourceKey> Dependencies => _dependencies;
        public IReadOnlyList<string> Labels => _labels;
        public string Hash { get; }
        public long Size { get; }
        public bool AllowOverride { get; }
        public IReadOnlyDictionary<string, string> ProviderData => _providerData;

        public ResourceKey CreateKey(string fallbackPackageId = "")
        {
            string packageId = string.IsNullOrWhiteSpace(PackageId) ? fallbackPackageId : PackageId;
            return new ResourceKey(Id, TypeId, Variant, packageId);
        }
    }

    public sealed class ResourceCatalogException : Exception
    {
        public ResourceCatalogException(string message)
            : base(message)
        {
        }
    }
}
