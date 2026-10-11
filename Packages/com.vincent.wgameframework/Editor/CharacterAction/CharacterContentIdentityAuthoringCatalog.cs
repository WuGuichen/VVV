using System;
using System.Globalization;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace MxFramework.Editor.CharacterAction
{
    /// <summary>Authoring-only transactions for predeclared content identity domains.</summary>
    public static class CharacterContentIdentityAuthoringCatalog
    {
        public const string DefaultPath = "Tools/RuntimeHash/character_definition_ids.json";
        private static readonly AsyncLocal<string> TestCatalogPath = new AsyncLocal<string>();
        private static readonly object SourceKeyCacheGate = new object();
        private static string _sourceKeyCachePath;
        private static string _sourceKeyCacheHash;
        private static Dictionary<(int DomainId, int Id), string> _sourceKeyCache;
        private static Dictionary<(int DomainId, string SourceKey), int> _sourceIdCache;
        private static HashSet<int> _sourceKeyDomains;
        private static readonly AsyncLocal<SourceKeyReadScope> ActiveSourceKeyReadScope =
            new AsyncLocal<SourceKeyReadScope>();
        internal static bool IsTestCatalogScopeActive => TestCatalogPath.Value != null;

        private sealed class SourceKeyReadScope : IDisposable
        {
            private readonly SourceKeyReadScope _previous;
            private bool _disposed;

            internal SourceKeyReadScope(
                string path,
                long length,
                DateTime lastWriteTimeUtc,
                Dictionary<(int DomainId, int Id), string> sourceKeys,
                Dictionary<(int DomainId, string SourceKey), int> sourceIds,
                HashSet<int> domains)
            {
                _previous = ActiveSourceKeyReadScope.Value;
                Path = path;
                Length = length;
                LastWriteTimeUtc = lastWriteTimeUtc;
                SourceKeys = sourceKeys;
                SourceIds = sourceIds;
                Domains = domains;
                ActiveSourceKeyReadScope.Value = this;
            }

            internal string Path { get; }
            internal long Length { get; }
            internal DateTime LastWriteTimeUtc { get; }
            internal Dictionary<(int DomainId, int Id), string> SourceKeys { get; }
            internal Dictionary<(int DomainId, string SourceKey), int> SourceIds { get; }
            internal HashSet<int> Domains { get; }

            public void Dispose()
            {
                if (_disposed)
                    return;
                if (!ReferenceEquals(ActiveSourceKeyReadScope.Value, this))
                    throw new InvalidOperationException("Content identity read scopes must be disposed in reverse order.");
                ActiveSourceKeyReadScope.Value = _previous;
                _disposed = true;
            }
        }

        /// <summary>Scopes authoring tests to a copied catalog; production callers keep the default directory.</summary>
        internal static IDisposable UseCatalogForTests(string path)
        {
            string absolute = Path.GetFullPath(path ?? throw new ArgumentNullException(nameof(path)));
            if (string.Equals(absolute, Path.GetFullPath(DefaultPath), StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("Tests must use an isolated catalog copy.", nameof(path));
            ReadValidated(absolute);
            return new CatalogPathScope(absolute);
        }

        internal static void PopulateTestDefinitions(string path, IEnumerable<(int DomainId, int Id, string SourceKey)> definitions)
        {
            string absolute = Path.GetFullPath(path ?? throw new ArgumentNullException(nameof(path)));
            if (string.Equals(absolute, Path.GetFullPath(DefaultPath), StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("Test definitions cannot be added to the production catalog.", nameof(path));
            JObject document = ReadValidated(absolute);
            foreach (var definition in definitions)
            {
                if (definition.Id <= 0 || string.IsNullOrWhiteSpace(definition.SourceKey))
                    throw new ArgumentException("Test definitions require explicit positive numbers and source keys.");
                JObject domain = Domain(document, definition.DomainId);
                JArray entries = (JArray)domain["entries"];
                JObject byId = entries.OfType<JObject>().SingleOrDefault(x => (int?)x["id"] == definition.Id);
                JObject byKey = FindEntry(domain, definition.SourceKey);
                if (byId != null || byKey != null)
                {
                    if (byId != byKey || (string)byId["status"] != "active")
                        throw new InvalidOperationException("Conflicting synthetic identity in test catalog: " + definition.SourceKey);
                    continue;
                }
                entries.Add(new JObject { ["id"] = definition.Id, ["sourceKey"] = definition.SourceKey, ["status"] = "active" });
                domain["nextId"] = Math.Max(domain["nextId"].Value<long>(), (long)definition.Id + 1);
            }
            document["revision"] = document["revision"].Value<long>() + 1;
            document["catalogHash"] = CanonicalHash(document);
            File.WriteAllText(absolute, document.ToString(Formatting.Indented) + "\n", new UTF8Encoding(false));
            ReadValidated(absolute);
        }

        private static string ResolveCatalogPath(string explicitPath) =>
            Path.GetFullPath(explicitPath ?? TestCatalogPath.Value ?? DefaultPath);

        internal static IDisposable BeginSourceKeyReadScope()
        {
            string path = ResolveCatalogPath(null);
            string fileHash = HashFile(path);
            lock (SourceKeyCacheGate)
            {
                EnsureSourceKeyCache(path, fileHash);
                var file = new FileInfo(path);
                return new SourceKeyReadScope(
                    path, file.Length, file.LastWriteTimeUtc,
                    _sourceKeyCache, _sourceIdCache, _sourceKeyDomains);
            }
        }

        private sealed class CatalogPathScope : IDisposable
        {
            private readonly string _previous;
            private readonly string _current;
            private bool _disposed;
            internal CatalogPathScope(string path)
            {
                _previous = TestCatalogPath.Value;
                _current = path;
                TestCatalogPath.Value = path;
            }
            public void Dispose()
            {
                if (_disposed) return;
                if (!string.Equals(TestCatalogPath.Value, _current, StringComparison.Ordinal))
                    throw new InvalidOperationException("Catalog test scopes must be disposed in their creation context and reverse order.");
                TestCatalogPath.Value = _previous;
                _disposed = true;
            }
        }


        public static string GetSourceKey(int domainId, int id, string catalogPath = null)
        {
            string path = ResolveCatalogPath(catalogPath);
            SourceKeyReadScope scope = ActiveSourceKeyReadScope.Value;
            if (scope != null && string.Equals(scope.Path, path, StringComparison.Ordinal))
            {
                var file = new FileInfo(path);
                if (file.Exists && file.Length == scope.Length
                    && file.LastWriteTimeUtc == scope.LastWriteTimeUtc)
                {
                    return ResolveSourceKey(domainId, id, scope.SourceKeys, scope.Domains);
                }
            }

            string fileHash = HashFile(path);
            lock (SourceKeyCacheGate)
            {
                EnsureSourceKeyCache(path, fileHash);
                return ResolveSourceKey(domainId, id, _sourceKeyCache, _sourceKeyDomains);
            }
        }

        private static void EnsureSourceKeyCache(string path, string fileHash)
        {
            if (_sourceKeyCache != null
                && string.Equals(_sourceKeyCachePath, path, StringComparison.Ordinal)
                && string.Equals(_sourceKeyCacheHash, fileHash, StringComparison.Ordinal))
                return;

            JObject document = ReadValidated(path);
            var sourceKeys = new Dictionary<(int DomainId, int Id), string>();
            var sourceIds = new Dictionary<(int DomainId, string SourceKey), int>();
            var domains = new HashSet<int>();
            foreach (JObject domain in ((JArray)document["domains"]).OfType<JObject>())
            {
                int currentDomainId = (int)domain["domainId"];
                domains.Add(currentDomainId);
                foreach (JObject entry in ((JArray)domain["entries"]).OfType<JObject>())
                {
                    if (string.Equals((string)entry["status"], "active", StringComparison.Ordinal))
                    {
                        sourceKeys.Add((currentDomainId, (int)entry["id"]), (string)entry["sourceKey"]);
                        sourceIds.Add((currentDomainId, (string)entry["sourceKey"]), (int)entry["id"]);
                    }
                }
            }
            _sourceKeyCachePath = path;
            _sourceKeyCacheHash = fileHash;
            _sourceKeyCache = sourceKeys;
            _sourceIdCache = sourceIds;
            _sourceKeyDomains = domains;
        }

        private static string ResolveSourceKey(
            int domainId,
            int id,
            Dictionary<(int DomainId, int Id), string> sourceKeys,
            HashSet<int> domains)
        {
            if (!domains.Contains(domainId))
                throw new InvalidOperationException("Content identity domain is not predefined.");
            if (!sourceKeys.TryGetValue((domainId, id), out string sourceKey))
                throw new InvalidOperationException("Content identity is missing or retired.");
            return sourceKey;
        }

        public static int GetExisting(int domainId, string sourceKey, string catalogPath = null)
        {
            string path = ResolveCatalogPath(catalogPath);
            SourceKeyReadScope scope = ActiveSourceKeyReadScope.Value;
            if (scope != null && string.Equals(scope.Path, path, StringComparison.Ordinal))
            {
                var file = new FileInfo(path);
                if (file.Exists && file.Length == scope.Length
                    && file.LastWriteTimeUtc == scope.LastWriteTimeUtc)
                {
                    return ResolveSourceId(domainId, sourceKey, scope.SourceIds, scope.Domains);
                }
            }

            string fileHash = HashFile(path);
            lock (SourceKeyCacheGate)
            {
                EnsureSourceKeyCache(path, fileHash);
                return ResolveSourceId(domainId, sourceKey, _sourceIdCache, _sourceKeyDomains);
            }
        }

        private static int ResolveSourceId(
            int domainId,
            string sourceKey,
            Dictionary<(int DomainId, string SourceKey), int> sourceIds,
            HashSet<int> domains)
        {
            if (!domains.Contains(domainId))
                throw new InvalidOperationException("Content identity domain is not predefined.");
            if (!sourceIds.TryGetValue((domainId, sourceKey), out int id))
                throw new InvalidOperationException("Content identity is missing or retired: " + sourceKey);
            return id;
        }

        public static int GetOrAllocate(int domainId, string sourceKey, string catalogPath = null)
        {
            if (string.IsNullOrWhiteSpace(sourceKey)) throw new ArgumentException("Authoring source identity is required.", nameof(sourceKey));
            return Mutate(catalogPath, document =>
            {
                JObject domain = Domain(document, domainId);
                JObject existing = FindEntry(domain, sourceKey);
                if (existing != null)
                {
                    if ((string)existing["status"] != "active") throw new InvalidOperationException("Retired content identities cannot be reused.");
                    return (existing["id"].Value<int>(), false);
                }
                long next = domain["nextId"].Value<long>();
                if (next <= 0 || next > int.MaxValue) throw new InvalidOperationException("Content identity domain is exhausted.");
                ((JArray)domain["entries"]).Add(new JObject { ["id"] = (int)next, ["sourceKey"] = sourceKey, ["status"] = "active" });
                domain["nextId"] = next + 1;
                return ((int)next, true);
            });
        }

        /// <summary>
        /// Resolves or allocates integer identities for all supplied requests inside a single
        /// exclusive catalog transaction. Duplicate (domainId, sourceKey) pairs are resolved
        /// once; the catalog file is written at most once even when new identities are needed.
        /// All existing identities are returned without any file write.
        /// </summary>
        /// <param name="requests">Pairs of (domainId, sourceKey) to resolve. Duplicates are
        /// de-duplicated before the transaction is entered.</param>
        /// <param name="catalogPath">Optional explicit catalog path; defaults to the active
        /// test or production catalog.</param>
        /// <returns>A dictionary keyed by (DomainId, SourceKey) mapping to the allocated or
        /// existing integer identity.</returns>
        public static IReadOnlyDictionary<(int DomainId, string SourceKey), int> BatchGetOrAllocate(
            IEnumerable<(int DomainId, string SourceKey)> requests,
            string catalogPath = null)
        {
            if (requests == null) throw new ArgumentNullException(nameof(requests));

            // De-duplicate requests before entering the transaction.
            var deduped = new List<(int DomainId, string SourceKey)>();
            var seen = new HashSet<(int DomainId, string SourceKey)>();
            foreach (var req in requests)
            {
                if (string.IsNullOrWhiteSpace(req.SourceKey))
                    throw new ArgumentException("Authoring source identity is required in batch request.", nameof(requests));
                if (seen.Add(req))
                    deduped.Add(req);
            }

            var results = new Dictionary<(int DomainId, string SourceKey), int>(deduped.Count);
            if (deduped.Count == 0)
                return results;

            string path = ResolveCatalogPath(catalogPath);
            using (new FileStream(path + ".lock", FileMode.CreateNew, FileAccess.Write, FileShare.None, 1, FileOptions.DeleteOnClose))
            {
                JObject document = ReadValidated(path);
                string expectedHash = Hash(File.ReadAllBytes(path));
                bool anyNew = false;

                foreach (var (domainId, sourceKey) in deduped)
                {
                    JObject domain = Domain(document, domainId);
                    JObject existing = FindEntry(domain, sourceKey);
                    if (existing != null)
                    {
                        if ((string)existing["status"] != "active")
                            throw new InvalidOperationException("Retired content identities cannot be reused: " + sourceKey);
                        results[(domainId, sourceKey)] = existing["id"].Value<int>();
                        continue;
                    }

                    long next = domain["nextId"].Value<long>();
                    if (next <= 0 || next > int.MaxValue)
                        throw new InvalidOperationException("Content identity domain is exhausted for domain " + domainId + ".");
                    int allocated = (int)next;
                    ((JArray)domain["entries"]).Add(new JObject { ["id"] = allocated, ["sourceKey"] = sourceKey, ["status"] = "active" });
                    domain["nextId"] = next + 1;
                    results[(domainId, sourceKey)] = allocated;
                    anyNew = true;
                }

                if (anyNew)
                {
                    document["revision"] = document["revision"].Value<long>() + 1;
                    document["catalogHash"] = CanonicalHash(document);
                    string staging = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
                    try
                    {
                        File.WriteAllText(staging, document.ToString(Formatting.Indented) + "\n", new UTF8Encoding(false));
                        if (Hash(File.ReadAllBytes(path)) != expectedHash)
                            throw new InvalidOperationException("Content identity catalog changed during batch transaction.");
                        File.Replace(staging, path, null);
                    }
                    finally { if (File.Exists(staging)) File.Delete(staging); }
                }
            }

            return results;
        }

        public static int AllocateNewEvent(int actionId, string description, string catalogPath = null) =>
            GetOrAllocate(401, "event/" + actionId.ToString(CultureInfo.InvariantCulture) + "/"
                + (description ?? "event") + "/" + Guid.NewGuid().ToString("N"), catalogPath);

        /// <summary>Reads a frozen order table once for an authoring export; never used by Tick.</summary>
        internal static IReadOnlyDictionary<int, int> ReadEventOrders(string catalogPath = null)
        {
            JObject document = ReadValidated(ResolveCatalogPath(catalogPath));
            JObject[] entries = ((JArray)Domain(document, 401)["entries"]).OfType<JObject>()
                .OrderBy(entry => (string)entry["sourceKey"], StringComparer.Ordinal).ToArray();
            var result = new Dictionary<int, int>(entries.Length);
            for (int i = 0; i < entries.Length; i++)
                if ((string)entries[i]["status"] == "active") result.Add((int)entries[i]["id"], i + 1);
            return result;
        }

        internal static Dictionary<int, int> AllocateCopiedEvents(int actionId, IReadOnlyDictionary<int, string> sources)
        {
            var mapping = new Dictionary<int, int>();
            Mutate(null, document =>
            {
                JObject domain = Domain(document, 401);
                JArray entries = (JArray)domain["entries"];
                foreach (int source in sources.Keys)
                    if (!entries.OfType<JObject>().Any(entry => (int)entry["id"] == source && (string)entry["status"] == "active"))
                        throw new InvalidOperationException("Cannot copy an unknown or retired event definition.");
                long next = (long)domain["nextId"];
                if (next + sources.Count > (long)int.MaxValue + 1)
                    throw new InvalidOperationException("Event identity domain is exhausted.");
                foreach (var source in sources.OrderBy(pair => pair.Key))
                {
                    int id = (int)next++;
                    entries.Add(new JObject
                    {
                        ["id"] = id,
                        ["sourceKey"] = "event/" + actionId + "/copy." + source.Value + "/" + Guid.NewGuid().ToString("N"),
                        ["status"] = "active",
                        ["copiedFromId"] = source.Key,
                        ["ownerActionId"] = actionId
                    });
                    mapping.Add(source.Key, id);
                }
                domain["nextId"] = next;
                return (0, sources.Count != 0);
            });
            return mapping;
        }

        public static void Retire(int domainId, int id, string catalogPath = null)
        {
            Mutate(catalogPath, document =>
            {
                JObject entry = ((JArray)Domain(document, domainId)["entries"]).OfType<JObject>().SingleOrDefault(x => (int?)x["id"] == id);
                if (entry == null) throw new InvalidOperationException("Cannot retire an unknown content identity.");
                bool changed = (string)entry["status"] != "retired";
                entry["status"] = "retired";
                return (id, changed);
            });
        }

        private static int Mutate(string catalogPath, Func<JObject, (int Value, bool Changed)> change)
        {
            string path = ResolveCatalogPath(catalogPath);
            using (new FileStream(path + ".lock", FileMode.CreateNew, FileAccess.Write, FileShare.None, 1, FileOptions.DeleteOnClose))
            {
                JObject document = ReadValidated(path);
                string expectedHash = Hash(File.ReadAllBytes(path));
                var result = change(document);
                if (!result.Changed) return result.Value;
                document["revision"] = document["revision"].Value<long>() + 1;
                document["catalogHash"] = CanonicalHash(document);
                string staging = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
                try
                {
                    File.WriteAllText(staging, document.ToString(Formatting.Indented) + "\n", new UTF8Encoding(false));
                    if (Hash(File.ReadAllBytes(path)) != expectedHash) throw new InvalidOperationException("Content identity catalog changed during transaction.");
                    File.Replace(staging, path, null);
                }
                finally { if (File.Exists(staging)) File.Delete(staging); }
                return result.Value;
            }
        }

        private static JObject ReadValidated(string path)
        {
            JObject document = JObject.Parse(File.ReadAllText(path));
            if ((string)document["schema"] != "mx.runtime-definition-id-catalog.v1"
                || document["revision"]?.Type != JTokenType.Integer || document["revision"].Value<long>() < 0
                || document["domains"] is not JArray || (string)document["catalogHash"] != CanonicalHash(document))
                throw new InvalidDataException("Content identity catalog schema, revision or hash is invalid.");
            foreach (JObject domain in ((JArray)document["domains"]).OfType<JObject>())
            {
                var entries = ((JArray)domain["entries"]).OfType<JObject>().ToArray();
                if (entries.Any(x => (int?)x["id"] <= 0 || ((string)x["status"] != "active" && (string)x["status"] != "retired"))
                    || entries.Select(x => (int)x["id"]).Distinct().Count() != entries.Length
                    || entries.Select(x => (string)x["sourceKey"]).Distinct(StringComparer.Ordinal).Count() != entries.Length
                    || domain["nextId"].Value<long>() <= entries.Select(x => (long)x["id"]).DefaultIfEmpty(0).Max())
                    throw new InvalidDataException("Content identity catalog contains duplicate or reused numbers.");
            }
            return document;
        }
        private static JObject Domain(JObject document, int id) => ((JArray)document["domains"]).OfType<JObject>()
            .SingleOrDefault(x => (int?)x["domainId"] == id) ?? throw new InvalidOperationException("Content identity domain is not predefined.");
        private static JObject FindEntry(JObject domain, string key) => ((JArray)domain["entries"]).OfType<JObject>()
            .SingleOrDefault(x => (string)x["sourceKey"] == key);
        private static string CanonicalHash(JObject document)
        {
            var copy = (JObject)document.DeepClone(); copy.Remove("catalogHash");
            return "sha256:" + Hash(Encoding.UTF8.GetBytes(Canonical(copy).ToString(Formatting.None)));
        }
        private static JToken Canonical(JToken value) => value is JObject obj
            ? new JObject(obj.Properties().OrderBy(x => x.Name, StringComparer.Ordinal).Select(x => new JProperty(x.Name, Canonical(x.Value))))
            : value is JArray array ? new JArray(array.Select(Canonical)) : value.DeepClone();
        private static string Hash(byte[] bytes)
        {
            using SHA256 sha = SHA256.Create();
            return string.Concat(sha.ComputeHash(bytes).Select(x => x.ToString("x2", CultureInfo.InvariantCulture)));
        }

        private static string HashFile(string path)
        {
            using FileStream stream = File.OpenRead(path);
            using SHA256 sha = SHA256.Create();
            return string.Concat(sha.ComputeHash(stream).Select(x => x.ToString("x2", CultureInfo.InvariantCulture)));
        }
    }
}
