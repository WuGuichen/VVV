using System.Security.Cryptography;
using System.Text.Json;
using MxFramework.Authoring;

namespace MxFramework.Authoring.Cli;

internal static partial class EditorServer
{
    private const string CharacterIdCatalogRelativePath = "Authoring/CharacterPackages/character_id_catalog.json";
    private const string CharacterIdCatalogSchemaVersion = "1.0";
    private static readonly object CharacterIdCatalogGate = new object();

    internal static object ReadCharacterIdCatalogState(string rootPath, JsonSerializerOptions jsonOptions)
    {
        lock (CharacterIdCatalogGate)
        {
            CharacterIdCatalogDocument document = ReadCharacterIdCatalog(rootPath, jsonOptions);
            return new
            {
                schemaVersion = document.SchemaVersion,
                width = document.Width,
                minimumId = document.MinimumId,
                maximumId = document.MaximumId,
                nextCandidate = FormatCharacterId(document.NextCandidate, document.Width),
                revision = document.Revision,
                activeCount = document.Entries.Count(item => string.Equals(item.Status, "active", StringComparison.Ordinal)),
                archivedCount = document.Entries.Count(item => string.Equals(item.Status, "archived", StringComparison.Ordinal)),
                retiredCount = document.RetiredIds.Count
            };
        }
    }

    internal static object ReallocateDraftCharacterId(
        string rootPath,
        CharacterIdReallocateRequest request,
        JsonSerializerOptions jsonOptions)
    {
        string sourcePackage = NormalizePackageRelativePath(request.sourcePackage);
        string packagePath = ResolveSafePath(rootPath, sourcePackage);
        CharacterResourcePackage package = CharacterPackageCommands.ReadPackage(packagePath, jsonOptions);
        string oldId = package.Manifest?.PackageId ?? string.Empty;
        if (!IsNumericCharacterId(oldId) || !string.Equals(oldId, request.confirmCharacterId, StringComparison.Ordinal))
            throw new ArgumentException("confirmCharacterId must match the current six-digit character ID.");
        string[] forbidden =
        {
            Path.Combine(rootPath, "Assets", "MxFrameworkGenerated", "CharacterPackages", oldId),
            Path.Combine(rootPath, "Assets", "MxFrameworkGenerated", "CharacterActions", "Profiles", oldId),
            Path.Combine(rootPath, "Assets", "MxFrameworkGenerated", "CharacterActions", "ActionSets", oldId),
            Path.Combine(packagePath, "package_cache", "unity_editor_import_receipt.json")
        };
        if (forbidden.Any(path => Directory.Exists(path) || File.Exists(path)))
            throw new InvalidOperationException("Character ID reallocation is forbidden after generated outputs or a Unity import receipt exist.");

        object duplicated = DuplicateCharacterPackage(
            rootPath,
            new CharacterStudioDuplicateRequest
            {
                sourcePackage = sourcePackage,
                displayName = package.Manifest?.DisplayName ?? oldId,
                idCatalogRevision = request.idCatalogRevision
            },
            jsonOptions);
        string newId = GetAnonymousProperty(duplicated, "allocatedCharacterId");
        string newPackage = GetAnonymousProperty(duplicated, "packageRelative");
        try
        {
            ArchiveCharacterPackage(
                rootPath,
                new CharacterStudioArchiveRequest { package = sourcePackage, confirmPackageId = oldId },
                jsonOptions);
        }
        catch
        {
            try
            {
                ArchiveCharacterPackage(
                    rootPath,
                    new CharacterStudioArchiveRequest { package = newPackage, confirmPackageId = newId },
                    jsonOptions);
            }
            catch
            {
            }
            throw;
        }
        return new { success = true, previousCharacterId = oldId, allocatedCharacterId = newId, packageRelative = newPackage };
    }

    private static CharacterIdCatalogLease AllocateCharacterId(
        string rootPath,
        string displayName,
        int expectedRevision,
        JsonSerializerOptions jsonOptions)
    {
        lock (CharacterIdCatalogGate)
        {
            string path = ResolveSafePath(rootPath, CharacterIdCatalogRelativePath);
            CharacterIdCatalogDocument document = ReadCharacterIdCatalog(rootPath, jsonOptions);
            if (expectedRevision > 0 && expectedRevision != document.Revision)
                throw new CharacterIdCatalogConflictException(
                    "CHARACTER_ID_CATALOG_REVISION_CONFLICT",
                    "Character ID catalog revision changed. Refresh before creating a character.",
                    document.Revision);

            var used = new HashSet<string>(document.Entries.Select(item => item.Id), StringComparer.Ordinal);
            used.UnionWith(document.RetiredIds);
            int candidate = Math.Max(document.MinimumId, document.NextCandidate);
            string id = string.Empty;
            while (candidate <= document.MaximumId)
            {
                string formatted = FormatCharacterId(candidate, document.Width);
                if (!used.Contains(formatted))
                {
                    id = formatted;
                    break;
                }
                candidate++;
            }
            if (string.IsNullOrEmpty(id))
                throw new InvalidOperationException("Character ID catalog is exhausted.");

            document.NextCandidate = candidate + 1;
            return new CharacterIdCatalogLease(
                path,
                ComputeCharacterIdCatalogFileSha256(path),
                document,
                id,
                displayName ?? string.Empty,
                jsonOptions);
        }
    }

    private static void RetireCharacterId(
        string rootPath,
        string characterId,
        JsonSerializerOptions jsonOptions)
    {
        if (!IsNumericCharacterId(characterId))
            return;
        lock (CharacterIdCatalogGate)
        {
            string path = ResolveSafePath(rootPath, CharacterIdCatalogRelativePath);
            CharacterIdCatalogDocument document = ReadCharacterIdCatalog(rootPath, jsonOptions);
            CharacterIdCatalogEntry entry = document.Entries.FirstOrDefault(item => string.Equals(item.Id, characterId, StringComparison.Ordinal))
                ?? throw new InvalidOperationException("Numeric character ID is missing from the catalog: " + characterId);
            entry.Status = "archived";
            if (!document.RetiredIds.Contains(characterId, StringComparer.Ordinal))
                document.RetiredIds.Add(characterId);
            document.RetiredIds.Sort(StringComparer.Ordinal);
            document.Revision++;
            WriteJsonFileAtomic(path, document, jsonOptions);
        }
    }

    internal static bool IsNumericCharacterId(string value)
    {
        return !string.IsNullOrEmpty(value)
            && value.Length == 6
            && value != "000000"
            && value.All(character => character >= '0' && character <= '9');
    }

    private static string FormatCharacterId(int value, int width)
    {
        return value.ToString(new string('0', width), System.Globalization.CultureInfo.InvariantCulture);
    }

    private static CharacterIdCatalogDocument ReadCharacterIdCatalog(string rootPath, JsonSerializerOptions jsonOptions)
    {
        string path = ResolveSafePath(rootPath, CharacterIdCatalogRelativePath);
        if (!File.Exists(path))
            throw new FileNotFoundException("Character ID catalog is missing.", path);
        CharacterIdCatalogDocument document = JsonSerializer.Deserialize<CharacterIdCatalogDocument>(File.ReadAllText(path), jsonOptions)
            ?? throw new InvalidDataException("Character ID catalog is malformed.");
        if (!string.Equals(document.SchemaVersion, CharacterIdCatalogSchemaVersion, StringComparison.Ordinal)
            || document.Width != 6
            || document.MinimumId != 1
            || document.MaximumId != 999999)
            throw new InvalidDataException("Character ID catalog schema or range is invalid.");
        document.Entries ??= new List<CharacterIdCatalogEntry>();
        document.RetiredIds ??= new List<string>();
        if (document.Entries.GroupBy(item => item.Id, StringComparer.Ordinal).Any(group => group.Count() > 1))
            throw new InvalidDataException("Character ID catalog contains duplicate entries.");
        if (document.RetiredIds.GroupBy(id => id, StringComparer.Ordinal).Any(group => group.Count() > 1))
            throw new InvalidDataException("Character ID catalog contains duplicate retired IDs.");
        foreach (CharacterIdCatalogEntry entry in document.Entries)
        {
            if (!IsNumericCharacterId(entry.Id))
                throw new InvalidDataException("Character ID catalog contains an invalid ID: " + entry.Id);
            if (!string.Equals(entry.Status, "active", StringComparison.Ordinal)
                && !string.Equals(entry.Status, "archived", StringComparison.Ordinal))
                throw new InvalidDataException("Character ID catalog contains an invalid status: " + entry.Status);
        }
        return document;
    }

    private static string ComputeCharacterIdCatalogFileSha256(string path)
    {
        using SHA256 sha = SHA256.Create();
        using FileStream stream = File.OpenRead(path);
        return Convert.ToHexString(sha.ComputeHash(stream));
    }

    private sealed class CharacterIdCatalogLease
    {
        private readonly string _path;
        private readonly string _expectedHash;
        private readonly CharacterIdCatalogDocument _document;
        private readonly string _displayName;
        private readonly JsonSerializerOptions _jsonOptions;
        private bool _committed;

        internal CharacterIdCatalogLease(
            string path,
            string expectedHash,
            CharacterIdCatalogDocument document,
            string characterId,
            string displayName,
            JsonSerializerOptions jsonOptions)
        {
            _path = path;
            _expectedHash = expectedHash;
            _document = document;
            CharacterId = characterId;
            _displayName = displayName;
            _jsonOptions = jsonOptions;
        }

        internal string CharacterId { get; }

        internal void Commit(string packagePath, string artSourcePath)
        {
            if (_committed)
                return;
            lock (CharacterIdCatalogGate)
            {
                string actualHash = ComputeCharacterIdCatalogFileSha256(_path);
                if (!string.Equals(actualHash, _expectedHash, StringComparison.Ordinal))
                    throw new CharacterIdCatalogConflictException(
                        "CHARACTER_ID_CATALOG_FILE_CONFLICT",
                        "Character ID catalog changed during character creation.",
                        _document.Revision);
                _document.Entries.Add(new CharacterIdCatalogEntry
                {
                    Id = CharacterId,
                    Status = "active",
                    DisplayName = _displayName,
                    PackagePath = packagePath.Replace('\\', '/'),
                    ArtSourcePath = artSourcePath.Replace('\\', '/')
                });
                _document.Entries.Sort((left, right) => string.CompareOrdinal(left.Id, right.Id));
                _document.Revision++;
                WriteJsonFileAtomic(_path, _document, _jsonOptions);
                _committed = true;
            }
        }
    }

    internal sealed class CharacterIdCatalogDocument
    {
        public string SchemaVersion { get; set; } = CharacterIdCatalogSchemaVersion;
        public int Width { get; set; } = 6;
        public int MinimumId { get; set; } = 1;
        public int MaximumId { get; set; } = 999999;
        public int NextCandidate { get; set; } = 1;
        public int Revision { get; set; } = 1;
        public List<CharacterIdCatalogEntry> Entries { get; set; } = new List<CharacterIdCatalogEntry>();
        public List<string> RetiredIds { get; set; } = new List<string>();
    }

    internal sealed class CharacterIdCatalogEntry
    {
        public string Id { get; set; } = string.Empty;
        public string Status { get; set; } = "active";
        public string DisplayName { get; set; } = string.Empty;
        public string PackagePath { get; set; } = string.Empty;
        public string ArtSourcePath { get; set; } = string.Empty;
    }

    internal sealed class CharacterIdReallocateRequest
    {
        public string sourcePackage { get; set; } = string.Empty;
        public string confirmCharacterId { get; set; } = string.Empty;
        public int idCatalogRevision { get; set; }
    }

    internal sealed class CharacterIdCatalogConflictException : Exception
    {
        internal CharacterIdCatalogConflictException(string code, string message, int actualRevision) : base(message)
        {
            Code = code;
            ActualRevision = actualRevision;
        }
        internal string Code { get; }
        internal int ActualRevision { get; }
    }
}
