using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;
using UnityEngine;

namespace MxFramework.Resources.Unity
{
#pragma warning disable 0649
    public sealed class ResolvedResourceCatalogMountSet
    {
        internal ResolvedResourceCatalogMountSet(
            IEnumerable<ResourceCatalog> catalogs,
            string mappingInputFingerprint,
            string buildPlanFingerprint)
        {
            Catalogs = new List<ResourceCatalog>(catalogs ?? Array.Empty<ResourceCatalog>());
            MappingInputFingerprint = mappingInputFingerprint ?? string.Empty;
            BuildPlanFingerprint = buildPlanFingerprint ?? string.Empty;
        }

        public IReadOnlyList<ResourceCatalog> Catalogs { get; }
        public string MappingInputFingerprint { get; }
        public string BuildPlanFingerprint { get; }
    }

    /// <summary>Resolves concrete external catalogs exclusively through the generated mount allowlist.</summary>
    public static class GeneratedResourceCatalogMountLoader
    {
        /// <remarks>AllocByDesign. Runs once during runtime bootstrap.</remarks>
        public static ResolvedResourceCatalogMountSet ResolveFromStreamingAssets(
            string relativeManifestPath,
            IEnumerable<ResourceCatalog> compositionRootCatalogs,
            IEnumerable<string> availableProviderIds)
        {
            if (string.IsNullOrWhiteSpace(relativeManifestPath))
                throw new ResourceCatalogException("Resource catalog mount manifest path is missing.");
            string manifestPath = Path.Combine(Application.streamingAssetsPath, relativeManifestPath);
            if (!File.Exists(manifestPath))
                throw new ResourceCatalogException("Resource catalog mount manifest was not found: " + manifestPath + ".");
            return ResolveFromJson(File.ReadAllText(manifestPath), Application.streamingAssetsPath, compositionRootCatalogs, availableProviderIds);
        }

        /// <remarks>AllocByDesign. Validation/bootstrap helper.</remarks>
        public static ResolvedResourceCatalogMountSet ResolveFromJson(
            string json,
            string streamingAssetsRoot,
            IEnumerable<ResourceCatalog> compositionRootCatalogs,
            IEnumerable<string> availableProviderIds)
        {
            ManifestDto manifest;
            try { manifest = JsonConvert.DeserializeObject<ManifestDto>(json); }
            catch (JsonException ex) { throw new ResourceCatalogException("Resource catalog mount manifest could not be parsed: " + ex.Message + "."); }
            if (manifest == null || manifest.schemaVersion != 1)
                throw new ResourceCatalogException("Unsupported resource catalog mount manifest schema.");
            RequireFingerprint(manifest.mappingInputFingerprint, "mapping input");
            RequireFingerprint(manifest.buildPlanFingerprint, "build plan");

            var providers = new HashSet<string>(StringComparer.Ordinal);
            if (availableProviderIds != null)
            {
                foreach (string providerId in availableProviderIds)
                    if (!string.IsNullOrWhiteSpace(providerId)) providers.Add(providerId);
            }

            var compositionByCatalogId = new Dictionary<string, ResourceCatalog>(StringComparer.Ordinal);
            if (compositionRootCatalogs != null)
            {
                foreach (ResourceCatalog catalog in compositionRootCatalogs)
                {
                    if (catalog == null) continue;
                    if (string.IsNullOrWhiteSpace(catalog.CatalogId) || !compositionByCatalogId.TryAdd(catalog.CatalogId, catalog))
                        throw new ResourceCatalogException("Composition-root catalog id is missing or duplicated.");
                }
            }

            MountDto[] mounts = manifest.mounts ?? Array.Empty<MountDto>();
            Array.Sort(mounts, CompareMounts);
            var catalogs = new List<ResourceCatalog>(mounts.Length);
            var mountIds = new HashSet<string>(StringComparer.Ordinal);
            var orders = new HashSet<int>();
            for (int i = 0; i < mounts.Length; i++)
            {
                MountDto mount = mounts[i];
                if (mount == null || string.IsNullOrWhiteSpace(mount.catalogMountId) || !mountIds.Add(mount.catalogMountId))
                    throw new ResourceCatalogException("Catalog mount id is missing or duplicated.");
                if (mount.mountOrder < 0 || !orders.Add(mount.mountOrder))
                    throw new ResourceCatalogException("Catalog mount order is invalid or duplicated: " + mount.catalogMountId + ".");
                RequireFingerprint(mount.expectedCatalogFingerprint, "catalog " + mount.catalogMountId);

                ResourceCatalog source;
                if (string.Equals(mount.bootstrapKind, "StreamingAssetsCatalog", StringComparison.OrdinalIgnoreCase))
                {
                    source = LoadStreamingCatalog(mount, streamingAssetsRoot);
                }
                else if (string.Equals(mount.bootstrapKind, "CompositionRoot", StringComparison.OrdinalIgnoreCase))
                {
                    if (!string.IsNullOrWhiteSpace(mount.relativeCatalogPath))
                        throw new ResourceCatalogException("Composition-root catalog cannot declare a StreamingAssets path: " + mount.catalogMountId + ".");
                    if (!compositionByCatalogId.Remove(mount.catalogId ?? string.Empty, out source))
                        throw new ResourceCatalogException("Composition-root catalog was not supplied exactly once: " + mount.catalogMountId + ".");
                }
                else
                {
                    throw new ResourceCatalogException("Catalog mount bootstrap kind is unsupported: " + mount.catalogMountId + ".");
                }

                catalogs.Add(ValidateAndFilter(mount, source, providers));
            }

            if (compositionByCatalogId.Count != 0)
                throw new ResourceCatalogException("A composition-root catalog was supplied without a declared CatalogMount.");
            return new ResolvedResourceCatalogMountSet(catalogs, manifest.mappingInputFingerprint, manifest.buildPlanFingerprint);
        }

        private static ResourceCatalog LoadStreamingCatalog(MountDto mount, string root)
        {
            string relative = (mount.relativeCatalogPath ?? string.Empty).Replace('\\', '/');
            if (string.IsNullOrWhiteSpace(relative) || Path.IsPathRooted(relative) || relative.StartsWith("../", StringComparison.Ordinal) || relative.Contains("/../"))
                throw new ResourceCatalogException("Catalog mount path is invalid: " + mount.catalogMountId + ".");
            string fullRoot = Path.GetFullPath(root ?? string.Empty).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            string fullPath = Path.GetFullPath(Path.Combine(fullRoot, relative));
            if (!fullPath.StartsWith(fullRoot, StringComparison.Ordinal))
                throw new ResourceCatalogException("Catalog mount escapes StreamingAssets: " + mount.catalogMountId + ".");
            if (!File.Exists(fullPath))
                throw new ResourceCatalogException("Mounted catalog was not found: " + relative + ".");
            return StreamingResourceCatalogLoader.LoadFromFile(fullPath);
        }

        private static ResourceCatalog ValidateAndFilter(MountDto mount, ResourceCatalog source, HashSet<string> providers)
        {
            if (source == null || !string.Equals(source.CatalogId, mount.catalogId, StringComparison.Ordinal))
                throw new ResourceCatalogException("Mounted catalog id mismatch: " + mount.catalogMountId + ".");
            string observedFingerprint = ResourceCatalogCanonicalFingerprint.Compute(source);
            if (!string.Equals(mount.expectedCatalogFingerprint, observedFingerprint, StringComparison.OrdinalIgnoreCase))
                throw new ResourceCatalogException("Mounted catalog fingerprint mismatch: " + mount.catalogMountId + ".");

            var entriesByKey = new Dictionary<ResourceKey, ResourceCatalogEntry>();
            for (int i = 0; i < source.Entries.Count; i++)
            {
                ResourceCatalogEntry entry = source.Entries[i];
                if (entry == null || !entriesByKey.TryAdd(entry.CreateKey(source.PackageId), entry))
                    throw new ResourceCatalogException("Mounted catalog contains a missing or duplicate complete ResourceKey: " + mount.catalogMountId + ".");
            }

            var allowlist = new HashSet<ResourceKey>();
            var filtered = new List<ResourceCatalogEntry>();
            AllowlistEntryDto[] entries = mount.allowlistedKeys ?? Array.Empty<AllowlistEntryDto>();
            for (int i = 0; i < entries.Length; i++)
            {
                AllowlistEntryDto allowed = entries[i] ?? throw new ResourceCatalogException("Catalog allowlist entry is missing: " + mount.catalogMountId + ".");
                var key = new ResourceKey(allowed.id, allowed.type, allowed.variant, allowed.packageId);
                if (!allowlist.Add(key))
                    throw new ResourceCatalogException("Catalog allowlist contains a duplicate complete ResourceKey: " + mount.catalogMountId + ".");
                if (!entriesByKey.TryGetValue(key, out ResourceCatalogEntry entry))
                    throw new ResourceCatalogException("Mounted catalog does not contain every allowlisted resource: " + mount.catalogMountId + ".");
                if (entry.AllowOverride != allowed.allowOverride)
                    throw new ResourceCatalogException("Mounted catalog override intent does not match the compiled allowlist: " + key + ".");
                if (!providers.Contains(entry.ProviderId))
                    throw new ResourceCatalogException("Mounted catalog requires an unavailable Provider '" + entry.ProviderId + "': " + mount.catalogMountId + ".");
                filtered.Add(entry);
            }
            return new ResourceCatalog(source.CatalogId, source.PackageId, filtered);
        }

        private static void RequireFingerprint(string value, string label)
        {
            const string prefix = "sha256:";
            if (string.IsNullOrWhiteSpace(value) || !value.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) || value.Length != prefix.Length + 64)
                throw new ResourceCatalogException("Required " + label + " fingerprint is missing or invalid.");
            for (int i = prefix.Length; i < value.Length; i++)
            {
                char c = value[i];
                if (!((c >= '0' && c <= '9') || (c >= 'a' && c <= 'f') || (c >= 'A' && c <= 'F')))
                    throw new ResourceCatalogException("Required " + label + " fingerprint is missing or invalid.");
            }
        }

        private static int CompareMounts(MountDto left, MountDto right)
        {
            int order = (left?.mountOrder ?? 0).CompareTo(right?.mountOrder ?? 0);
            return order != 0 ? order : string.Compare(left?.catalogMountId, right?.catalogMountId, StringComparison.Ordinal);
        }

        private sealed class ManifestDto
        {
            public int schemaVersion;
            public string mappingInputFingerprint;
            public string buildPlanFingerprint;
            public MountDto[] mounts;
        }

        private sealed class MountDto
        {
            public string catalogMountId;
            public string catalogId;
            public string bootstrapKind;
            public string relativeCatalogPath;
            public int mountOrder;
            public string expectedCatalogFingerprint;
            public AllowlistEntryDto[] allowlistedKeys;
        }

        private sealed class AllowlistEntryDto
        {
            public string id;
            public string type;
            public string variant;
            public string packageId;
            public bool allowOverride;
        }
    }

    public static class ResourceCatalogCanonicalFingerprint
    {
        /// <remarks>AllocByDesign. Catalog validation and artifact generation only.</remarks>
        public static string Compute(ResourceCatalog catalog)
        {
            if (catalog == null) throw new ArgumentNullException(nameof(catalog));
            var entries = new List<object>();
            var sorted = new List<ResourceCatalogEntry>(catalog.Entries);
            sorted.Sort((left, right) => CompareKeys(left.CreateKey(catalog.PackageId), right.CreateKey(catalog.PackageId)));
            for (int i = 0; i < sorted.Count; i++)
            {
                ResourceCatalogEntry entry = sorted[i];
                var dependencies = new List<ResourceKey>(entry.Dependencies);
                dependencies.Sort(CompareKeys);
                var labels = new List<string>(entry.Labels); labels.Sort(StringComparer.Ordinal);
                var providerData = new SortedDictionary<string, string>(StringComparer.Ordinal);
                foreach (KeyValuePair<string, string> pair in entry.ProviderData) providerData[pair.Key] = pair.Value;
                entries.Add(new
                {
                    id = entry.Id, type = entry.TypeId, variant = entry.Variant, packageId = entry.PackageId,
                    provider = entry.ProviderId, address = entry.Address,
                    dependencies = dependencies.ConvertAll(key => new { id = key.Id, type = key.TypeId, variant = key.Variant, packageId = key.PackageId }),
                    labels, hash = entry.Hash, size = entry.Size, allowOverride = entry.AllowOverride, providerData
                });
            }
            string canonical = JsonConvert.SerializeObject(new { schemaVersion = 1, catalogId = catalog.CatalogId, packageId = catalog.PackageId, entries }, Formatting.None);
            using SHA256 sha = SHA256.Create();
            byte[] hash = sha.ComputeHash(Encoding.UTF8.GetBytes(canonical));
            return "sha256:" + BitConverter.ToString(hash).Replace("-", string.Empty).ToLowerInvariant();
        }

        private static int CompareKeys(ResourceKey left, ResourceKey right)
        {
            int value = string.Compare(left.PackageId, right.PackageId, StringComparison.Ordinal);
            if (value != 0) return value;
            value = string.Compare(left.Id, right.Id, StringComparison.Ordinal);
            if (value != 0) return value;
            value = string.Compare(left.TypeId, right.TypeId, StringComparison.Ordinal);
            return value != 0 ? value : string.Compare(left.Variant, right.Variant, StringComparison.Ordinal);
        }
    }
#pragma warning restore 0649
}
