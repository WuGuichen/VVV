using System;
using System.Collections.Generic;
using System.Threading;

namespace MxFramework.Resources
{
    public sealed class ResourceManager : IResourceManager, IResourceCatalogQuery
    {
        private const int MaxRecentErrors = 16;
        private const int MaxRecentEvictions = 16;

        private readonly Dictionary<string, IResourceProvider> _providers = new Dictionary<string, IResourceProvider>(StringComparer.Ordinal);
        private readonly Dictionary<ResourceKey, ResolvedEntry> _entriesByPackage = new Dictionary<ResourceKey, ResolvedEntry>();
        private readonly Dictionary<ResourceKey, ResolvedEntry> _entriesGlobal = new Dictionary<ResourceKey, ResolvedEntry>();
        private readonly Dictionary<ResourceKey, ResourceRecord> _loaded = new Dictionary<ResourceKey, ResourceRecord>();
        private readonly List<ResourceCatalog> _catalogs = new List<ResourceCatalog>();
        private readonly List<ResourceCatalogSummary> _catalogSummaries = new List<ResourceCatalogSummary>();
        private readonly List<ResourceEntryOrigin> _entryOrigins = new List<ResourceEntryOrigin>();
        private readonly Queue<ResourceError> _recentErrors = new Queue<ResourceError>();
        private readonly Queue<ResourceEvictionRecord> _recentEvictions = new Queue<ResourceEvictionRecord>();
        private ResourceVariantProfile _variantProfile = ResourceVariantProfile.Empty;
        private ResourceRetainPolicy _retainPolicy = ResourceRetainPolicy.None;
        private int _failedCount;

        public IResourceManager RegisterProvider(IResourceProvider provider)
        {
            if (provider == null)
                throw new ArgumentNullException(nameof(provider));
            if (string.IsNullOrWhiteSpace(provider.ProviderId))
                throw new ResourceCatalogException("Resource provider id is missing.");
            if (_providers.ContainsKey(provider.ProviderId))
                throw new ResourceCatalogException("Duplicate resource provider id: " + provider.ProviderId + ".");

            _providers.Add(provider.ProviderId, provider);
            return this;
        }

        public ResourceManager SetVariantProfile(ResourceVariantProfile profile)
        {
            _variantProfile = profile ?? ResourceVariantProfile.Empty;
            return this;
        }

        public ResourceManager SetRetainPolicy(ResourceRetainPolicy policy)
        {
            _retainPolicy = policy ?? ResourceRetainPolicy.None;
            return this;
        }

        public IResourceManager AddCatalog(ResourceCatalog catalog)
        {
            if (catalog == null)
                throw new ArgumentNullException(nameof(catalog));
            if (catalog.SchemaVersion != 1)
                throw new ResourceCatalogException("Unsupported resource catalog schema version: " + catalog.SchemaVersion + ".");

            var localKeys = new HashSet<ResourceKey>();
            var pending = new List<ResolvedEntry>();
            for (int i = 0; i < catalog.Entries.Count; i++)
            {
                ResourceCatalogEntry entry = catalog.Entries[i];
                ValidateEntry(catalog, entry);

                ResourceKey packageKey = entry.CreateKey(catalog.PackageId);
                ResourceKey globalKey = packageKey.WithoutPackage();
                if (!localKeys.Add(globalKey))
                    throw new ResourceCatalogException("Duplicate resource key in catalog: " + globalKey + ".");

                pending.Add(new ResolvedEntry(catalog, entry, packageKey, globalKey, false));
            }

            for (int i = 0; i < pending.Count; i++)
            {
                ResolvedEntry resolved = pending[i];
                if (_entriesByPackage.ContainsKey(resolved.PackageKey))
                    throw new ResourceCatalogException("Duplicate resource package key: " + resolved.PackageKey + ".");

                bool overridesAnotherEntry = _entriesGlobal.TryGetValue(resolved.GlobalKey, out ResolvedEntry currentGlobal);
                if (overridesAnotherEntry)
                {
                    if (!resolved.Entry.AllowOverride)
                        throw new ResourceCatalogException("Resource key conflict requires override=true: " + resolved.GlobalKey + ".");
                    if (!string.Equals(currentGlobal.Entry.TypeId, resolved.Entry.TypeId, StringComparison.Ordinal))
                        throw new ResourceCatalogException("Resource override type mismatch: " + resolved.GlobalKey + ".");
                    resolved = resolved.WithOverride(true);
                }
                else if (resolved.Entry.AllowOverride && TryFindGlobalEntryWithSameIdVariant(resolved.GlobalKey, out currentGlobal))
                {
                    throw new ResourceCatalogException(
                        "Resource override type mismatch: " + resolved.GlobalKey +
                        " conflicts with " + currentGlobal.GlobalKey + ".");
                }

                _entriesByPackage.Add(resolved.PackageKey, resolved);
                _entriesGlobal[resolved.GlobalKey] = resolved;
                _entryOrigins.Add(new ResourceEntryOrigin(resolved.PackageKey, resolved.Catalog.CatalogId, resolved.PackageKey.PackageId, resolved.OverridesAnotherEntry));
            }

            _catalogs.Add(catalog);
            _catalogSummaries.Add(new ResourceCatalogSummary(catalog.CatalogId, catalog.PackageId, catalog.Entries.Count));
            return this;
        }

        public bool Contains(ResourceKey key)
        {
            return TryResolve(key, GetLookupTypeId<object>(key), out _, out _);
        }

        public IReadOnlyList<ResourceKey> FindKeysByLabel(string label)
        {
            var keys = new List<ResourceKey>();
            var unique = new HashSet<ResourceKey>();
            if (string.IsNullOrWhiteSpace(label))
                return keys;

            foreach (KeyValuePair<ResourceKey, ResolvedEntry> pair in _entriesGlobal)
            {
                IReadOnlyList<string> labels = pair.Value.Entry.Labels;
                for (int i = 0; i < labels.Count; i++)
                {
                    if (!string.Equals(labels[i], label, StringComparison.Ordinal))
                        continue;

                    ResourceKey logicalKey = new ResourceKey(pair.Key.Id, pair.Key.TypeId);
                    if (unique.Add(logicalKey))
                        keys.Add(logicalKey);
                    break;
                }
            }

            return keys;
        }

        public ResourceLoadResult<ResourceHandle<T>> Load<T>(ResourceKey key)
        {
            string requestedTypeId = ResourceTypeIds.FromType<T>();
            string lookupTypeId = GetLookupTypeId<T>(key);
            if (!IsCompatibleType<T>(lookupTypeId, requestedTypeId))
            {
                return Fail<ResourceHandle<T>>(new ResourceError(
                    ResourceErrorCode.TypeMismatch,
                    key,
                    string.Empty,
                    "Requested type '" + requestedTypeId + "' does not match key type '" + lookupTypeId + "'."));
            }

            if (!TryResolve(key, lookupTypeId, out ResolvedEntry resolved, out ResourceError resolveError))
                return Fail<ResourceHandle<T>>(resolveError);

            if (!IsCompatibleType<T>(resolved.Entry.TypeId, requestedTypeId))
            {
                return Fail<ResourceHandle<T>>(new ResourceError(
                    ResourceErrorCode.TypeMismatch,
                    resolved.PackageKey,
                    resolved.Entry.ProviderId,
                    "Requested type '" + requestedTypeId + "' does not match catalog type '" + resolved.Entry.TypeId + "'.",
                    resolved.Entry.Address));
            }

            ResourceLoadResult<object> loaded = LoadEntry(resolved, new HashSet<ResourceKey>());
            if (!loaded.Success)
                return Fail<ResourceHandle<T>>(loaded.Error);

            if (!(loaded.Value is T value))
            {
                return Fail<ResourceHandle<T>>(new ResourceError(
                    ResourceErrorCode.TypeMismatch,
                    resolved.PackageKey,
                    resolved.Entry.ProviderId,
                    "Provider returned '" + GetRuntimeTypeName(loaded.Value) + "' but caller requested '" + typeof(T).Name + "'.",
                    resolved.Entry.Address));
            }

            var handle = new ResourceHandle<T>(resolved.PackageKey, resolved.Entry, resolved.Entry.ProviderId, value);
            return ResourceLoadResult<ResourceHandle<T>>.Loaded(handle);
        }

        public IResourceOperation<ResourceHandle<T>> LoadAsync<T>(ResourceKey key, CancellationToken cancellationToken = default)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                return new ImmediateResourceOperation<ResourceHandle<T>>(Fail<ResourceHandle<T>>(new ResourceError(
                    ResourceErrorCode.Cancelled,
                    key,
                    string.Empty,
                    "Resource operation was cancelled.")));
            }

            return new ImmediateResourceOperation<ResourceHandle<T>>(Load<T>(key));
        }

        public void Release<T>(ResourceHandle<T> handle)
        {
            if (handle == null)
                return;
            if (!handle.TryMarkReleased())
            {
                AddError(new ResourceError(ResourceErrorCode.HandleReleased, handle.Key, handle.ProviderId, "Resource handle was already released."));
                return;
            }

            ReleaseKey(handle.Key);
        }

        public ResourceDebugSnapshot CreateDebugSnapshot()
        {
            int totalRefCount = 0;
            int retainedCount = 0;
            int evictableCount = 0;
            int pinnedCount = 0;
            foreach (ResourceRecord record in _loaded.Values)
            {
                totalRefCount += record.RefCount;
                if (!record.IsRetained)
                    continue;

                retainedCount++;
                if (record.RetainMode == ResourceRetainMode.KeepAlive)
                    pinnedCount++;
                else
                    evictableCount++;
            }

            return new ResourceDebugSnapshot(
                _catalogs.Count,
                _entriesGlobal.Count,
                _providers.Count,
                _loaded.Count,
                0,
                _failedCount,
                0,
                totalRefCount,
                new List<ResourceError>(_recentErrors),
                new List<ResourceCatalogSummary>(_catalogSummaries),
                new List<ResourceEntryOrigin>(_entryOrigins),
                retainedCount,
                evictableCount,
                pinnedCount,
                _retainPolicy.Mode == ResourceRetainMode.None ? 0 : 1,
                new List<ResourceEvictionRecord>(_recentEvictions));
        }

        public int AdvanceRetainTime(float deltaSeconds)
        {
            if (deltaSeconds < 0f)
                deltaSeconds = 0f;

            var expired = new List<ResourceKey>();
            foreach (KeyValuePair<ResourceKey, ResourceRecord> pair in _loaded)
            {
                ResourceRecord record = pair.Value;
                if (!record.AdvanceRetainTime(deltaSeconds))
                    continue;

                expired.Add(pair.Key);
            }

            return ReleaseExpiredRetained(expired, "timed");
        }

        public int AdvanceRetainFrames(int frameCount)
        {
            if (frameCount < 0)
                frameCount = 0;

            var expired = new List<ResourceKey>();
            foreach (KeyValuePair<ResourceKey, ResourceRecord> pair in _loaded)
            {
                ResourceRecord record = pair.Value;
                if (!record.AdvanceRetainFrames(frameCount))
                    continue;

                expired.Add(pair.Key);
            }

            return ReleaseExpiredRetained(expired, "timed");
        }

        public int EvictRetainedResources()
        {
            var retained = new List<ResourceKey>();
            foreach (KeyValuePair<ResourceKey, ResourceRecord> pair in _loaded)
            {
                if (pair.Value.IsRetained)
                    retained.Add(pair.Key);
            }

            return ReleaseExpiredRetained(retained, "manual");
        }

        public void ValidateCatalogs()
        {
            foreach (ResolvedEntry resolved in _entriesByPackage.Values)
            {
                for (int i = 0; i < resolved.Entry.Dependencies.Count; i++)
                {
                    ResourceKey dependency = resolved.Entry.Dependencies[i];
                    if (!TryResolve(dependency, dependency.TypeId, out _, out ResourceError error))
                        throw new ResourceCatalogException("Invalid resource dependency for " + resolved.PackageKey + ": " + error.Message);
                }
            }
        }

        private static string GetRuntimeTypeName(object value)
        {
            return value == null ? "null" : value.GetType().Name;
        }

        private static string GetLookupTypeId<T>(ResourceKey key)
        {
            return string.IsNullOrWhiteSpace(key.TypeId)
                ? ResourceTypeIds.FromType<T>()
                : key.TypeId;
        }

        private static bool IsCompatibleType<T>(string catalogTypeId, string requestedTypeId)
        {
            if (typeof(T) == typeof(object))
                return true;
            return string.Equals(catalogTypeId, requestedTypeId, StringComparison.Ordinal);
        }

        private bool TryFindGlobalEntryWithSameIdVariant(ResourceKey key, out ResolvedEntry resolved)
        {
            foreach (KeyValuePair<ResourceKey, ResolvedEntry> pair in _entriesGlobal)
            {
                ResourceKey existing = pair.Key;
                if (string.Equals(existing.Id, key.Id, StringComparison.Ordinal) &&
                    string.Equals(existing.Variant, key.Variant, StringComparison.Ordinal))
                {
                    resolved = pair.Value;
                    return true;
                }
            }

            resolved = default;
            return false;
        }

        private void ValidateEntry(ResourceCatalog catalog, ResourceCatalogEntry entry)
        {
            if (entry == null)
                throw new ResourceCatalogException("Resource catalog contains a null entry.");

            ResourceKey packageKey = entry.CreateKey(catalog.PackageId);
            if (!packageKey.IsValid)
                throw new ResourceCatalogException("Invalid resource key: " + packageKey + ".");
            if (string.IsNullOrWhiteSpace(entry.ProviderId))
                throw new ResourceCatalogException("Resource provider id is missing for key: " + packageKey + ".");
            if (!_providers.ContainsKey(entry.ProviderId))
                throw new ResourceCatalogException("Resource provider is not registered: " + entry.ProviderId + ".");
            if (!IsSafeRelativeAddress(entry.Address))
                throw new ResourceCatalogException("Resource address is not a safe relative path: " + entry.Address + ".");
        }

        private static bool IsSafeRelativeAddress(string address)
        {
            if (string.IsNullOrWhiteSpace(address))
                return false;
            if (address.StartsWith("/", StringComparison.Ordinal) || address.StartsWith("\\", StringComparison.Ordinal))
                return false;
            if (address.IndexOf("..", StringComparison.Ordinal) >= 0)
                return false;
            if (address.IndexOf(':') >= 0)
                return false;

            return true;
        }

        private bool TryResolve(ResourceKey key, string requestedTypeId, out ResolvedEntry resolved, out ResourceError error)
        {
            string typeId = string.IsNullOrWhiteSpace(key.TypeId) ? requestedTypeId : key.TypeId;
            ResourceKey requested = new ResourceKey(key.Id, typeId, key.Variant, key.PackageId);
            if (!requested.IsValid)
            {
                resolved = default;
                error = new ResourceError(ResourceErrorCode.InvalidKey, requested, string.Empty, "Resource key is invalid.");
                return false;
            }

            List<string> candidateVariants = _variantProfile.CreateCandidateVariants(requested.Variant);
            for (int i = 0; i < candidateVariants.Count; i++)
            {
                ResourceKey lookup = new ResourceKey(requested.Id, requested.TypeId, candidateVariants[i], requested.PackageId);
                if (!string.IsNullOrWhiteSpace(lookup.PackageId))
                {
                    if (_entriesByPackage.TryGetValue(lookup, out resolved))
                    {
                        error = ResourceError.None;
                        return true;
                    }

                    continue;
                }

                if (_entriesGlobal.TryGetValue(lookup, out resolved))
                {
                    error = ResourceError.None;
                    return true;
                }
            }

            resolved = default;
            error = new ResourceError(ResourceErrorCode.NotFound, requested, string.Empty, "Resource key was not found.");
            return false;
        }

        private ResourceLoadResult<object> LoadEntry(ResolvedEntry resolved, HashSet<ResourceKey> loadingPath)
        {
            if (_loaded.TryGetValue(resolved.PackageKey, out ResourceRecord existing))
            {
                existing.RetainMode = ResourceRetainMode.None;
                existing.RefCount = existing.RefCount <= 0 ? 1 : existing.RefCount + 1;
                return ResourceLoadResult<object>.Loaded(existing.Value);
            }

            if (!loadingPath.Add(resolved.PackageKey))
            {
                return Fail<object>(new ResourceError(
                    ResourceErrorCode.DependencyInvalid,
                    resolved.PackageKey,
                    resolved.Entry.ProviderId,
                    "Resource dependency graph contains a cycle.",
                    resolved.Entry.Address));
            }

            var loadedDependencies = new List<ResourceKey>();
            for (int i = 0; i < resolved.Entry.Dependencies.Count; i++)
            {
                ResourceKey dependencyKey = resolved.Entry.Dependencies[i];
                if (!TryResolve(dependencyKey, dependencyKey.TypeId, out ResolvedEntry dependency, out ResourceError resolveError))
                {
                    ReleaseLoadedDependencies(loadedDependencies, false);
                    loadingPath.Remove(resolved.PackageKey);
                    return Fail<object>(new ResourceError(
                        ResourceErrorCode.DependencyInvalid,
                        resolved.PackageKey,
                        resolved.Entry.ProviderId,
                        "Resource dependency is invalid: " + resolveError.Message,
                        resolved.Entry.Address));
                }

                ResourceLoadResult<object> dependencyResult = LoadEntry(dependency, loadingPath);
                if (!dependencyResult.Success)
                {
                    ReleaseLoadedDependencies(loadedDependencies, false);
                    loadingPath.Remove(resolved.PackageKey);
                    return dependencyResult;
                }

                loadedDependencies.Add(dependency.PackageKey);
            }

            if (!_providers.TryGetValue(resolved.Entry.ProviderId, out IResourceProvider provider))
            {
                ReleaseLoadedDependencies(loadedDependencies, false);
                loadingPath.Remove(resolved.PackageKey);
                return Fail<object>(new ResourceError(ResourceErrorCode.ProviderMissing, resolved.PackageKey, resolved.Entry.ProviderId, "Resource provider is not registered.", resolved.Entry.Address));
            }

            ResourceLoadResult<object> loadResult = provider.Load(new ResourceLoadContext(resolved.PackageKey, resolved.Entry));
            if (!loadResult.Success)
            {
                ReleaseLoadedDependencies(loadedDependencies, false);
                loadingPath.Remove(resolved.PackageKey);
                return Fail<object>(loadResult.Error);
            }

            _loaded.Add(resolved.PackageKey, new ResourceRecord(resolved, provider, loadResult.Value, loadedDependencies));
            loadingPath.Remove(resolved.PackageKey);
            return loadResult;
        }

        private int ReleaseExpiredRetained(List<ResourceKey> keys, string reason)
        {
            int released = 0;
            for (int i = 0; i < keys.Count; i++)
            {
                ResourceKey key = keys[i];
                if (!_loaded.TryGetValue(key, out ResourceRecord record) || !record.IsRetained)
                    continue;

                ReleaseRecord(key, record, reason);
                released++;
            }

            return released;
        }

        private void ReleaseLoadedDependencies(List<ResourceKey> loadedDependencies, bool allowRetain = true)
        {
            for (int i = loadedDependencies.Count - 1; i >= 0; i--)
                ReleaseKey(loadedDependencies[i], allowRetain);
        }

        private void ReleaseKey(ResourceKey key)
        {
            ReleaseKey(key, true);
        }

        private void ReleaseKey(ResourceKey key, bool allowRetain)
        {
            if (!_loaded.TryGetValue(key, out ResourceRecord record))
                return;

            record.RefCount--;
            if (record.RefCount > 0)
                return;

            if (allowRetain && TryRetainRecord(record))
                return;

            ReleaseRecord(key, record, "refCountZero");
        }

        private bool TryRetainRecord(ResourceRecord record)
        {
            if (_retainPolicy.Mode == ResourceRetainMode.None)
                return false;

            if (_retainPolicy.Mode == ResourceRetainMode.Timed &&
                _retainPolicy.DurationSeconds <= 0f &&
                _retainPolicy.FrameCount <= 0)
            {
                return false;
            }

            record.BeginRetain(_retainPolicy);
            return true;
        }

        private void ReleaseRecord(ResourceKey key, ResourceRecord record, string reason)
        {
            _loaded.Remove(key);
            record.Provider.Release(new ResourceReleaseContext(key, record.Resolved.Entry, record.Value));
            AddEviction(new ResourceEvictionRecord(key, record.Resolved.Entry.ProviderId, reason));
            ReleaseLoadedDependencies(record.Dependencies, false);
        }

        private ResourceLoadResult<T> Fail<T>(ResourceError error)
        {
            AddError(error);
            return ResourceLoadResult<T>.Failed(error);
        }

        private void AddError(ResourceError error)
        {
            if (error.IsNone)
                return;

            _failedCount++;
            _recentErrors.Enqueue(error);
            while (_recentErrors.Count > MaxRecentErrors)
                _recentErrors.Dequeue();
        }

        private void AddEviction(ResourceEvictionRecord record)
        {
            _recentEvictions.Enqueue(record);
            while (_recentEvictions.Count > MaxRecentEvictions)
                _recentEvictions.Dequeue();
        }

        private readonly struct ResolvedEntry
        {
            public ResolvedEntry(ResourceCatalog catalog, ResourceCatalogEntry entry, ResourceKey packageKey, ResourceKey globalKey, bool overridesAnotherEntry)
            {
                Catalog = catalog;
                Entry = entry;
                PackageKey = packageKey;
                GlobalKey = globalKey;
                OverridesAnotherEntry = overridesAnotherEntry;
            }

            public ResourceCatalog Catalog { get; }
            public ResourceCatalogEntry Entry { get; }
            public ResourceKey PackageKey { get; }
            public ResourceKey GlobalKey { get; }
            public bool OverridesAnotherEntry { get; }

            public ResolvedEntry WithOverride(bool overridesAnotherEntry)
            {
                return new ResolvedEntry(Catalog, Entry, PackageKey, GlobalKey, overridesAnotherEntry);
            }
        }

        private sealed class ResourceRecord
        {
            public ResourceRecord(ResolvedEntry resolved, IResourceProvider provider, object value, List<ResourceKey> dependencies)
            {
                Resolved = resolved;
                Provider = provider;
                Value = value;
                Dependencies = dependencies;
                RefCount = 1;
            }

            public ResolvedEntry Resolved { get; }
            public IResourceProvider Provider { get; }
            public object Value { get; }
            public List<ResourceKey> Dependencies { get; }
            public int RefCount { get; set; }
            public ResourceRetainMode RetainMode { get; set; }
            public float RemainingRetainSeconds { get; private set; }
            public int RemainingRetainFrames { get; private set; }
            public bool IsRetained => RefCount <= 0 && RetainMode != ResourceRetainMode.None;

            public void BeginRetain(ResourceRetainPolicy policy)
            {
                RetainMode = policy.Mode;
                RemainingRetainSeconds = policy.DurationSeconds;
                RemainingRetainFrames = policy.FrameCount;
            }

            public bool AdvanceRetainTime(float deltaSeconds)
            {
                if (!IsRetained || RetainMode != ResourceRetainMode.Timed || RemainingRetainSeconds <= 0f)
                    return false;

                RemainingRetainSeconds -= deltaSeconds;
                return RemainingRetainSeconds <= 0f && RemainingRetainFrames <= 0;
            }

            public bool AdvanceRetainFrames(int frameCount)
            {
                if (!IsRetained || RetainMode != ResourceRetainMode.Timed || RemainingRetainFrames <= 0)
                    return false;

                RemainingRetainFrames -= frameCount;
                return RemainingRetainFrames <= 0 && RemainingRetainSeconds <= 0f;
            }
        }
    }
}
