using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace MxFramework.Authoring.Cli;

internal static partial class EditorServer
{
    private const string CharacterActionIdCatalogSchema = "mx.character-action.id-catalog.v1";
    private const string CharacterActionIdCatalogRelativePath = "CharacterActionSource/action_id_catalog.json";
    private const int CharacterActionIdReservedRangeSize = 10000;

    private static CharacterActionIdCatalogLease AllocateCharacterActionId(
        string rootPath,
        string libraryId,
        string sourcePath)
    {
        if (string.IsNullOrWhiteSpace(libraryId))
            throw new InvalidOperationException("CharacterAction ID allocation requires a library id.");

        string path = ResolveSafePath(rootPath, CharacterActionIdCatalogRelativePath);
        if (!File.Exists(path))
            throw new FileNotFoundException("CharacterAction ID catalog is missing.", path);

        string previousContents = File.ReadAllText(path);
        string expectedFileHash = ComputeCharacterActionCatalogFileHash(path);
        JsonObject document = JsonNode.Parse(previousContents) as JsonObject
            ?? throw new InvalidDataException("CharacterAction ID catalog is malformed.");
        ValidateCharacterActionIdCatalog(document);

        JsonObject library = document["libraries"]?.AsArray()
            .OfType<JsonObject>()
            .FirstOrDefault(item => string.Equals(
                item["libraryId"]?.GetValue<string>(),
                libraryId,
                StringComparison.Ordinal))
            ?? throw new InvalidOperationException(
                "CharacterAction ID catalog has no reserved range for library '" + libraryId + "'.");
        JsonArray allocations = document["allocations"]?.AsArray()
            ?? throw new InvalidDataException("CharacterAction ID catalog allocations are missing.");
        var used = new HashSet<int>(allocations.OfType<JsonObject>()
            .Select(item => item["actionId"]?.GetValue<int>() ?? 0));

        int actionId = 0;
        foreach (JsonObject range in library["ranges"]?.AsArray().OfType<JsonObject>()
                 ?? Enumerable.Empty<JsonObject>())
        {
            int start = range["start"]?.GetValue<int>() ?? 0;
            int end = range["end"]?.GetValue<int>() ?? 0;
            int candidate = Math.Max(start, range["nextActionId"]?.GetValue<int>() ?? start);
            while (candidate <= end && used.Contains(candidate))
                candidate++;
            if (candidate <= 0 || candidate > end)
                continue;
            actionId = candidate;
            range["nextActionId"] = candidate + 1;
            break;
        }
        if (actionId <= 0)
            throw new InvalidOperationException(
                "CharacterAction ID ranges are exhausted for library '" + libraryId + "'.");

        allocations.Add(new JsonObject
        {
            ["actionId"] = actionId,
            ["libraryId"] = libraryId,
            ["sourcePath"] = (sourcePath ?? string.Empty).Replace('\\', '/'),
            ["status"] = "active",
        });
        document["revision"] = (document["revision"]?.GetValue<int>() ?? 0) + 1;
        document["catalogHash"] = string.Empty;
        document["catalogHash"] = "sha256:" + ComputeCharacterActionCatalogHash(CanonicalizeCharacterActionCatalog(document));

        if (!string.Equals(ComputeCharacterActionCatalogFileHash(path), expectedFileHash, StringComparison.Ordinal))
            throw new CharacterActionSourceConflictException(
                expectedFileHash,
                ComputeCharacterActionCatalogFileHash(path),
                "CharacterAction ID catalog changed during allocation.");

        string nextContents = document.ToJsonString(new JsonSerializerOptions { WriteIndented = true }) + Environment.NewLine;
        WriteCharacterActionCatalogAtomic(path, nextContents);
        return new CharacterActionIdCatalogLease(
            actionId,
            path,
            previousContents,
            ComputeCharacterActionCatalogFileHash(path));
    }

    internal static CharacterActionIdCatalogLease ReserveCharacterActionIdRange(
        string rootPath,
        string libraryId,
        IReadOnlyList<int> sourceActionIds,
        string sourcePath)
    {
        if (string.IsNullOrWhiteSpace(libraryId))
            throw new InvalidOperationException("CharacterAction ID range reservation requires a library id.");

        string path = ResolveSafePath(rootPath, CharacterActionIdCatalogRelativePath);
        if (!File.Exists(path))
            throw new FileNotFoundException("CharacterAction ID catalog is missing.", path);

        string previousContents = File.ReadAllText(path);
        string expectedFileHash = ComputeCharacterActionCatalogFileHash(path);
        JsonObject document = JsonNode.Parse(previousContents) as JsonObject
            ?? throw new InvalidDataException("CharacterAction ID catalog is malformed.");
        ValidateCharacterActionIdCatalog(document);

        JsonArray libraries = document["libraries"]?.AsArray()
            ?? throw new InvalidDataException("CharacterAction ID catalog libraries are missing.");
        if (libraries.OfType<JsonObject>().Any(item => string.Equals(
                item["libraryId"]?.GetValue<string>(),
                libraryId,
                StringComparison.Ordinal)))
        {
            throw new InvalidOperationException(
                "CharacterAction ID catalog already has a reserved range for library '" + libraryId + "'.");
        }

        int maximumEnd = libraries.OfType<JsonObject>()
            .SelectMany(item => item["ranges"]?.AsArray().OfType<JsonObject>()
                ?? Enumerable.Empty<JsonObject>())
            .Select(range => range["end"]?.GetValue<int>() ?? 0)
            .DefaultIfEmpty(0)
            .Max();
        long lowerBound = Math.Max(1_000_000L, (long)maximumEnd + 1L);
        long startLong = ((lowerBound + CharacterActionIdReservedRangeSize - 1L)
            / CharacterActionIdReservedRangeSize) * CharacterActionIdReservedRangeSize;
        long endLong = startLong + CharacterActionIdReservedRangeSize - 1L;
        if (endLong > int.MaxValue)
            throw new InvalidOperationException("CharacterAction ID range space is exhausted.");

        int start = (int)startLong;
        int end = (int)endLong;
        libraries.Add(new JsonObject
        {
            ["libraryId"] = libraryId,
            ["ranges"] = new JsonArray
            {
                new JsonObject
                {
                    ["start"] = start,
                    ["end"] = end,
                    ["nextActionId"] = start,
                }
            }
        });
        var reassigned = new Dictionary<int, int>();
        int nextActionId = start;
        foreach (int sourceActionId in sourceActionIds ?? Array.Empty<int>())
        {
            if (sourceActionId <= 0 || reassigned.ContainsKey(sourceActionId))
                throw new InvalidDataException(
                    "Character package source contains an invalid duplicate CharacterAction ID: " + sourceActionId);
            if (nextActionId > end)
                throw new InvalidOperationException(
                    "CharacterAction ID range is too small for cloned library '" + libraryId + "'.");
            reassigned[sourceActionId] = nextActionId;
            document["allocations"]!.AsArray().Add(new JsonObject
            {
                ["actionId"] = nextActionId,
                ["libraryId"] = libraryId,
                ["sourcePath"] = (sourcePath ?? string.Empty).Replace('\\', '/'),
                ["status"] = "active",
            });
            nextActionId++;
        }
        libraries.OfType<JsonObject>()
            .Single(item => string.Equals(item["libraryId"]?.GetValue<string>(), libraryId, StringComparison.Ordinal))["ranges"]!
            .AsArray()[0]!["nextActionId"] = nextActionId;
        document["revision"] = (document["revision"]?.GetValue<int>() ?? 0) + 1;
        document["catalogHash"] = string.Empty;
        document["catalogHash"] = "sha256:" + ComputeCharacterActionCatalogHash(CanonicalizeCharacterActionCatalog(document));

        if (!string.Equals(ComputeCharacterActionCatalogFileHash(path), expectedFileHash, StringComparison.Ordinal))
            throw new CharacterActionSourceConflictException(
                expectedFileHash,
                ComputeCharacterActionCatalogFileHash(path),
                "CharacterAction ID catalog changed during range reservation.");

        string nextContents = document.ToJsonString(new JsonSerializerOptions { WriteIndented = true }) + Environment.NewLine;
        WriteCharacterActionCatalogAtomic(path, nextContents);
        return new CharacterActionIdCatalogLease(
            actionId: 0,
            path,
            previousContents,
            ComputeCharacterActionCatalogFileHash(path),
            reassigned);
    }

    private static void ValidateCharacterActionIdCatalog(JsonObject document)
    {
        if (!string.Equals(
                document["schema"]?.GetValue<string>(),
                CharacterActionIdCatalogSchema,
                StringComparison.Ordinal)
            || (document["revision"]?.GetValue<int>() ?? 0) <= 0
            || document["libraries"] is not JsonArray
            || document["allocations"] is not JsonArray allocations)
        {
            throw new InvalidDataException("CharacterAction ID catalog schema is unsupported or malformed.");
        }

        string storedHash = document["catalogHash"]?.GetValue<string>() ?? string.Empty;
        JsonObject hashInput = document.DeepClone().AsObject();
        hashInput["catalogHash"] = string.Empty;
        string actualHash = "sha256:" + ComputeCharacterActionCatalogHash(CanonicalizeCharacterActionCatalog(hashInput));
        if (!string.Equals(storedHash, actualHash, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("CharacterAction ID catalog hash mismatch.");

        var ids = new HashSet<int>();
        var ranges = new List<(int Start, int End, string LibraryId)>();
        var libraryIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (JsonObject library in document["libraries"]!.AsArray().OfType<JsonObject>())
        {
            string libraryId = library["libraryId"]?.GetValue<string>() ?? string.Empty;
            if (libraryId.Length == 0 || !libraryIds.Add(libraryId))
                throw new InvalidDataException("CharacterAction ID catalog contains an invalid duplicate library: " + libraryId);
            foreach (JsonObject range in library["ranges"]?.AsArray().OfType<JsonObject>()
                     ?? Enumerable.Empty<JsonObject>())
            {
                int start = range["start"]?.GetValue<int>() ?? 0;
                int end = range["end"]?.GetValue<int>() ?? 0;
                int next = range["nextActionId"]?.GetValue<int>() ?? 0;
                if (start <= 0 || end < start || next < start || next > end + 1L)
                    throw new InvalidDataException("CharacterAction ID catalog contains an invalid reserved range.");
                if (ranges.Any(existing => start <= existing.End && end >= existing.Start))
                    throw new InvalidDataException("CharacterAction ID catalog contains overlapping reserved ranges.");
                ranges.Add((start, end, libraryId));
            }
        }
        foreach (JsonObject item in allocations.OfType<JsonObject>())
        {
            int id = item["actionId"]?.GetValue<int>() ?? 0;
            if (id <= 0 || !ids.Add(id))
                throw new InvalidDataException("CharacterAction ID catalog contains an invalid duplicate: " + id);
            string owner = item["libraryId"]?.GetValue<string>() ?? string.Empty;
            if (!ranges.Any(range => string.Equals(range.LibraryId, owner, StringComparison.Ordinal)
                    && id >= range.Start && id <= range.End))
            {
                throw new InvalidDataException(
                    "CharacterAction ID catalog allocation " + id + " is outside its owner library ranges.");
            }
        }
    }

    private static void EnsureCharacterActionIdsAreAllocated(
        string rootPath,
        IEnumerable<int> actionIds)
    {
        string path = ResolveSafePath(rootPath, CharacterActionIdCatalogRelativePath);
        if (!File.Exists(path))
            throw new FileNotFoundException("CharacterAction ID catalog is missing.", path);
        JsonObject document = JsonNode.Parse(File.ReadAllText(path)) as JsonObject
            ?? throw new InvalidDataException("CharacterAction ID catalog is malformed.");
        ValidateCharacterActionIdCatalog(document);
        var active = new HashSet<int>(document["allocations"]!.AsArray()
            .OfType<JsonObject>()
            .Where(item => string.Equals(
                item["status"]?.GetValue<string>(),
                "active",
                StringComparison.Ordinal))
            .Select(item => item["actionId"]?.GetValue<int>() ?? 0));
        foreach (int actionId in actionIds ?? Array.Empty<int>())
        {
            if (actionId <= 0 || !active.Contains(actionId))
                throw new InvalidOperationException(
                    "CharacterAction ID is not an active global catalog allocation: " + actionId);
        }
    }

    private static JsonNode CanonicalizeCharacterActionCatalog(JsonNode node)
    {
        if (node is JsonObject obj)
        {
            var result = new JsonObject();
            foreach ((string name, JsonNode value) in obj.OrderBy(item => item.Key, StringComparer.Ordinal))
                result[name] = value == null ? null : CanonicalizeCharacterActionCatalog(value);
            return result;
        }
        if (node is JsonArray array)
        {
            var result = new JsonArray();
            foreach (JsonNode item in array)
                result.Add(item == null ? null : CanonicalizeCharacterActionCatalog(item));
            return result;
        }
        return node.DeepClone();
    }

    private static string ComputeCharacterActionCatalogFileHash(string path) =>
        ComputeCharacterActionCatalogHash(File.ReadAllBytes(path));

    private static string ComputeCharacterActionCatalogHash(JsonNode node) =>
        ComputeCharacterActionCatalogHash(Encoding.UTF8.GetBytes(
            node.ToJsonString(new JsonSerializerOptions { WriteIndented = false })));

    private static string ComputeCharacterActionCatalogHash(byte[] bytes)
    {
        byte[] hash = SHA256.HashData(bytes);
        var result = new StringBuilder(hash.Length * 2);
        for (int i = 0; i < hash.Length; i++)
            result.Append(hash[i].ToString("x2", CultureInfo.InvariantCulture));
        return result.ToString();
    }

    private static void WriteCharacterActionCatalogAtomic(string path, string contents)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path) ?? string.Empty);
        string staging = path + ".staging";
        File.WriteAllText(staging, contents);
        File.Move(staging, path, overwrite: true);
    }

    internal static void RemoveLibrariesFromCharacterActionIdCatalogForTest(string rootPath, IEnumerable<string> librariesToRemove)
    {
        string path = ResolveSafePath(rootPath, CharacterActionIdCatalogRelativePath);
        if (!File.Exists(path))
            return;

        JsonObject document = JsonNode.Parse(File.ReadAllText(path)) as JsonObject
            ?? throw new InvalidDataException("CharacterAction ID catalog is malformed.");

        var removeSet = new HashSet<string>(librariesToRemove ?? Enumerable.Empty<string>(), StringComparer.Ordinal);
        if (removeSet.Count == 0)
            return;

        if (document["libraries"] is JsonArray libraries)
        {
            for (int i = libraries.Count - 1; i >= 0; i--)
            {
                string id = libraries[i]?["libraryId"]?.GetValue<string>() ?? string.Empty;
                if (removeSet.Contains(id))
                    libraries.RemoveAt(i);
            }
        }

        if (document["allocations"] is JsonArray allocations)
        {
            for (int i = allocations.Count - 1; i >= 0; i--)
            {
                string id = allocations[i]?["libraryId"]?.GetValue<string>() ?? string.Empty;
                if (removeSet.Contains(id))
                    allocations.RemoveAt(i);
            }
        }

        document["catalogHash"] = string.Empty;
        document["catalogHash"] = "sha256:" + ComputeCharacterActionCatalogHash(CanonicalizeCharacterActionCatalog(document));
        string nextContents = document.ToJsonString(new JsonSerializerOptions { WriteIndented = true }) + Environment.NewLine;
        WriteCharacterActionCatalogAtomic(path, nextContents);
    }

    internal sealed class CharacterActionIdCatalogLease
    {
        private readonly string _path;
        private readonly string _previousContents;
        private readonly string _committedFileHash;

        internal CharacterActionIdCatalogLease(
            int actionId,
            string path,
            string previousContents,
            string committedFileHash,
            IReadOnlyDictionary<int, int> reassignedActionIds = null)
        {
            ActionId = actionId;
            _path = path;
            _previousContents = previousContents;
            _committedFileHash = committedFileHash;
            ReassignedActionIds = reassignedActionIds
                ?? new Dictionary<int, int>();
        }

        internal int ActionId { get; }
        internal IReadOnlyDictionary<int, int> ReassignedActionIds { get; }

        internal void Rollback()
        {
            if (!File.Exists(_path)
                || !string.Equals(
                    ComputeCharacterActionCatalogFileHash(_path),
                    _committedFileHash,
                    StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "CharacterAction ID allocation could not be rolled back because the catalog changed again.");
            }
            WriteCharacterActionCatalogAtomic(_path, _previousContents);
        }
    }
}
