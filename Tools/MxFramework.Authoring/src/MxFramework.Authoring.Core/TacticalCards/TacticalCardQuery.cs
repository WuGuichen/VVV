using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace MxFramework.Authoring
{
    public sealed class TacticalCardQueryOptions
    {
        public string Name { get; set; }
        public string Contains { get; set; }
        public string Id { get; set; }
        public string InstanceId { get; set; }
        public string SaveJson { get; set; }
        public string SnapshotJson { get; set; }
        public string RuntimeAssembly { get; set; }
        public string Locale { get; set; } = "zh-CN";
        public string Layer { get; set; } = "generated";
        public void Validate()
        {
            if (new[] { Name, Contains, Id, InstanceId }.Count(s => !string.IsNullOrWhiteSpace(s)) != 1)
                throw new ArgumentException("Use exactly one of --name, --contains, --id, --instance-id.");
            if (!new[] { "disk", "generated", "live" }.Contains(Layer)) throw new ArgumentException("Unknown query layer.");
        }
    }

    public sealed class TacticalCardQueryRecord
    {
        public string Title { get; set; }
        public string StableId { get; set; }
        public string BaseKey { get; set; }
        public string RecordId { get; set; }
        public string InstanceId { get; set; }
        public int Revision { get; set; }
        public string ContentHash { get; set; }
        public string Owner { get; set; }
        public string SourceLayer { get; set; }
        public string SourcePath { get; set; }
        public string SourceFingerprint { get; set; }
        public string ShortDescription { get; set; }
        public Dictionary<string, string> Bindings { get; set; } = new Dictionary<string, string>();
        [System.Text.Json.Serialization.JsonIgnore] public JsonElement TacticalSource { get; set; }
        [System.Text.Json.Serialization.JsonIgnore] public JsonElement Raw { get; set; }
        public string WorkspaceId { get; set; }
        public string WorkspaceName { get; set; }
        public string SourceIssue { get; set; }
    }

    /// <summary>Fresh, read-only source selection. Description rules remain in the game's projector.</summary>
    public sealed class TacticalCardQuery
    {
        private readonly string _root;
        private readonly List<string> _diagnostics = new List<string>();
        public TacticalCardQuery(string projectRoot) { _root = Path.GetFullPath(projectRoot); }
        public string[] Diagnostics => _diagnostics.ToArray();
        public TacticalCardQueryRecord[] Find(TacticalCardQueryOptions options)
        {
            options.Validate();
            return ReadAll(options).Where(r => Match(r, options)).ToArray();
        }

        /// <summary>Enumerates the same authoritative sources used by targeted queries. Never reads player saves implicitly.</summary>
        /// <remarks>LowFreqAlloc. Invalid indexed rows may be retained as unavailable entries for directory accounting.</remarks>
        public TacticalCardQueryRecord[] ReadAll(TacticalCardQueryOptions options = null, bool includeUnavailable = false)
        {
            options = options ?? new TacticalCardQueryOptions(); _diagnostics.Clear();
            var records = new List<TacticalCardQueryRecord>();
            string catalog = Path.Combine(_root, "Assets/StreamingAssets/MxFramework/CharacterTest/runtime_ai_planner/behavior_card_catalog.json");
            if (File.Exists(catalog))
                foreach (var entry in Array(Read(catalog), "cards"))
                {
                    int start = records.Count;
                    TryReadIndexed(Path.GetDirectoryName(catalog), Text(entry, "file"), data =>
                    {
                        foreach (var card in Array(data, "cards"))
                        {
                            if (Text(card, "stableCardId") != Text(entry, "stableCardId"))
                                throw new InvalidDataException("Published index identity mismatch.");
                            records.Add(Published(card, SafeChild(Path.GetDirectoryName(catalog), Text(entry, "file"))));
                        }
                    });
                    if (includeUnavailable && records.Count == start) records.Add(new TacticalCardQueryRecord {
                        StableId = Text(entry, "stableCardId"), Title = Text(entry, "stableCardId"), Revision = Integer(entry, "latestRevision"),
                        SourceLayer = "disk-published", SourcePath = catalog, SourceIssue = "Indexed publication unavailable; inspect directory diagnostics." });
                }
            string configured = Path.Combine(_root, "Authoring/RuntimeAiLab/TestCards");
            var configuredPaths = Files(configured);
            if (!string.IsNullOrWhiteSpace(options.SnapshotJson)) configuredPaths = configuredPaths.Concat(new[] { Path.GetFullPath(options.SnapshotJson) });
            foreach (string path in configuredPaths) TryRead(path, data =>
            {
                if (Text(data, "schema") != "mx.character-test.configured-card.v1") return;
                var record = new TacticalCardQueryRecord { Title = Text(data, "name"), RecordId = Text(data, "id"),
                    BaseKey = Text(data, "baseKey"), Revision = Integer(data, "revision"), Owner = Text(data, "ownerCharacter"),
                    SourceLayer = !string.IsNullOrWhiteSpace(options.SnapshotJson) && Path.GetFullPath(options.SnapshotJson) == path ? "disk-snapshot" : "disk-configured", SourcePath = path, ShortDescription = Text(Field(data, "metadata"), "shortDescription"),
                    TacticalSource = Field(data, "source"), Raw = data, SourceFingerprint = Hash(File.ReadAllBytes(path)) };
                foreach (var binding in Properties(Field(data, "actions"))) record.Bindings[binding.Name] = Scalar(binding.Value);
                records.Add(record);
            });
            string drafts = Path.Combine(_root, "Authoring/RuntimeAiLab/Drafts");
            foreach (string path in Files(drafts).Where(p => Path.GetFileName(p) == "workspace.json")) TryRead(path, workspace =>
            {
                if (!new[] { "mx.character-test.runtime-ai-lab.workspace.v4", "mx.character-test.runtime-ai-lab.workspace.v3" }.Contains(Text(workspace, "schema")))
                {
                    _diagnostics.Add(path + ": Unsupported workspace schema; no execution source was inferred.");
                    if (includeUnavailable) foreach (var item in Array(workspace, "cards")) records.Add(new TacticalCardQueryRecord {
                        Title = Text(item, "id"), RecordId = Text(item, "id"), Revision = Integer(item, "revision"),
                        WorkspaceId = Text(workspace, "workspaceId"), SourceLayer = "disk-author", SourcePath = path,
                        SourceIssue = "Unsupported workspace schema." });
                    return;
                }
                string folder = Path.GetDirectoryName(path);
                var definitions = new Dictionary<string, JsonElement>();
                foreach (var item in Array(workspace, "tacticalDefinitions"))
                {
                    TryReadIndexed(folder, Text(item, "path"), definition => {
                        string definitionPath = SafeChild(folder, Text(item, "path"));
                        ValidateIndex(definitionPath, item, definition, "definitionId", "revision");
                        definitions.Add(Text(definition, "definitionId"), definition);
                    });
                }
                foreach (var item in Array(workspace, "cards"))
                {
                    int start = records.Count;
                    TryReadIndexed(folder, Text(item, "path"), card =>
                    {
                        string cardPath = SafeChild(folder, Text(item, "path"));
                        ValidateIndex(cardPath, item, card, "cardDraftId", "currentRevision");
                        var record = new TacticalCardQueryRecord { Title = Text(card, "displayName"), StableId = Text(card, "publishedStableId"),
                            RecordId = Text(card, "cardDraftId"), Revision = Integer(card, "currentRevision"), SourceLayer = "disk-author",
                            SourcePath = cardPath, ShortDescription = Text(card, "shortDescription"), SourceFingerprint = Hash(File.ReadAllBytes(cardPath)),
                            Raw = card, WorkspaceId = Text(workspace, "workspaceId"), WorkspaceName = Text(workspace, "displayName") };
                        var payload = Field(card, "payload"); string schema = Text(payload, "schema");
                        if (schema == "mx.character-test.tactical-card-composition.v1")
                        {
                            var needed = References(Field(payload, "root")).Select(n => Text(n, "definitionId")).Distinct().ToArray();
                            if (needed.All(definitions.ContainsKey))
                            {
                                var source = new { schema = "mx.character-test.published-tactical-source.v1", composition = payload,
                                    definitions = needed.Select(id => definitions[id]).ToArray() };
                                record.TacticalSource = Parse(JsonSerializer.Serialize(source));
                                record.SourceFingerprint = Hash(Encoding.UTF8.GetBytes(record.TacticalSource.GetRawText()));
                            }
                            else record.SourceIssue = "Indexed shared definitions unavailable; author source is not guessed.";
                        }
                        else if (schema.StartsWith("mx.character-test.tactical-card-source.", StringComparison.Ordinal))
                            record.TacticalSource = Parse(JsonSerializer.Serialize(new { schema = "mx.character-test.published-tactical-source.v1", legacy = payload }));
                        else record.SourceIssue = "This author payload has no supported tactical source.";
                        records.Add(record);
                    });
                    if (includeUnavailable && records.Count == start) records.Add(new TacticalCardQueryRecord {
                        Title = Text(item, "id"), RecordId = Text(item, "id"), Revision = Integer(item, "revision"),
                        WorkspaceId = Text(workspace, "workspaceId"), WorkspaceName = Text(workspace, "displayName"),
                        SourceLayer = "disk-author", SourcePath = path, SourceIssue = "Indexed draft unavailable or stale; inspect directory diagnostics." });
                }
            });
            if (!string.IsNullOrWhiteSpace(options.SaveJson))
            {
                string path = Path.GetFullPath(options.SaveJson);
                TryRead(path, state =>
                {
                    if (!Text(state, "schema").StartsWith("mx.character-test.tactical-player.", StringComparison.Ordinal))
                        throw new InvalidDataException("--save-json requires the decoded tactical-player JSON payload, not a save envelope.");
                    foreach (var owner in Array(state, "characters")) foreach (var reference in Array(owner, "cards"))
                    {
                        var exact = records.SingleOrDefault(r => r.SourceLayer == "disk-published" && r.StableId == Text(reference, "cardId")
                            && r.Revision == Integer(reference, "revision") && r.ContentHash == Text(reference, "contentHash"));
                        var instance = new TacticalCardQueryRecord { Title = exact?.Title, StableId = Text(reference, "cardId"),
                            InstanceId = Text(reference, "instanceId"), Revision = Integer(reference, "revision"), ContentHash = Text(reference, "contentHash"),
                            Owner = Text(owner, "characterStableId") is string ownerId && ownerId.Length > 0 ? ownerId : Text(owner, "stableId"), SourceLayer = "disk-player", SourcePath = path,
                            ShortDescription = exact?.ShortDescription, TacticalSource = exact?.TacticalSource ?? default,
                            SourceIssue = exact == null ? "Exact published revision/contentHash unavailable; no latest-version substitution." : null,
                            SourceFingerprint = Hash(Encoding.UTF8.GetBytes(Hash(File.ReadAllBytes(path)) + "/" + exact?.SourceFingerprint)) };
                        foreach (var binding in Array(owner, "bindings").Where(b => Text(b, "cardInstanceId") == instance.InstanceId))
                            instance.Bindings[Text(binding, "slotPath")] = Text(binding, "tacticalActionId");
                        records.Add(instance);
                    }
                });
            }
            return records.OrderBy(r => r.SourceLayer).ThenBy(r => r.Title, StringComparer.Ordinal).ToArray();
        }
        public static bool Match(TacticalCardQueryRecord record, TacticalCardQueryOptions q)
            => !string.IsNullOrWhiteSpace(q.Name) ? record.Title == q.Name
                : !string.IsNullOrWhiteSpace(q.Contains) ? (record.Title ?? "").IndexOf(q.Contains, StringComparison.OrdinalIgnoreCase) >= 0
                : !string.IsNullOrWhiteSpace(q.Id) ? record.StableId == q.Id || record.RecordId == q.Id
                : record.InstanceId == q.InstanceId;
        private TacticalCardQueryRecord Published(JsonElement card, string path) => new TacticalCardQueryRecord {
            Title = Text(Field(card, "metadata"), "displayName"), StableId = Text(card, "stableCardId"), Revision = Integer(card, "revision"),
            ContentHash = Text(card, "contentHash"), ShortDescription = Text(Field(card, "metadata"), "shortDescription"),
            SourceLayer = "disk-published", SourcePath = path, TacticalSource = Field(card, "tacticalSource"), Raw = card,
            SourceIssue = Field(card, "tacticalSource").ValueKind == JsonValueKind.Object ? null : "Published card has no tactical presentation source.", SourceFingerprint = Hash(File.ReadAllBytes(path)) };
        private void TryRead(string path, Action<JsonElement> consume)
        { try { consume(Read(path)); } catch (Exception e) when (e is IOException || e is InvalidDataException || e is ArgumentException || e is JsonException || e is InvalidOperationException) { _diagnostics.Add(path + ": " + e.Message); } }
        private void TryReadIndexed(string folder, string relative, Action<JsonElement> consume)
        { try { TryRead(SafeChild(folder, relative), consume); } catch (Exception e) when (e is IOException || e is ArgumentException) { _diagnostics.Add(folder + "/" + relative + ": " + e.Message); } }
        private static IEnumerable<string> Files(string root)
        {
            if (!Directory.Exists(root) || (File.GetAttributes(root) & FileAttributes.ReparsePoint) != 0) yield break;
            foreach (string file in Directory.EnumerateFiles(root, "*.json"))
                if ((File.GetAttributes(file) & FileAttributes.ReparsePoint) == 0) yield return file;
            foreach (string folder in Directory.EnumerateDirectories(root)) foreach (string file in Files(folder)) yield return file;
        }
        private static void ValidateIndex(string path, JsonElement index, JsonElement data, string idField, string revisionField)
        {
            string normalized = File.ReadAllText(path).Replace("\r\n", "\n").Replace("\r", "\n");
            string expected = Text(index, "hash");
            if (Hash(Encoding.UTF8.GetBytes(normalized)) != expected && Hash(Encoding.UTF8.GetBytes(normalized.Replace("\n", "\r\n"))) != expected)
                throw new InvalidDataException("Indexed source hash mismatch; stale source is not selected.");
            if (Text(data, idField) != Text(index, "id") || Integer(data, revisionField) != Integer(index, "revision"))
                throw new InvalidDataException("Indexed identity/revision mismatch.");
        }
        private static IEnumerable<JsonElement> References(JsonElement node)
        { if (Text(node, "kind") == "Reference") yield return node; foreach (var child in Array(node, "children")) foreach (var value in References(child)) yield return value; }
        public static string SafeChild(string root, string relative)
        {
            if (string.IsNullOrWhiteSpace(relative) || Path.IsPathRooted(relative)) throw new InvalidDataException("Invalid indexed path.");
            string folder = Path.GetFullPath(root) + Path.DirectorySeparatorChar;
            string path = Path.GetFullPath(Path.Combine(root, relative));
            if (!path.StartsWith(folder, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("Indexed path escapes its authority directory.");
            for (string current = path; current != null && current.Length >= folder.Length; current = Path.GetDirectoryName(current))
                if ((File.Exists(current) || Directory.Exists(current)) && (File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
                    throw new InvalidDataException("Indexed source crosses a reparse point.");
            return path;
        }
        public static JsonElement Parse(string json) { using (var document = JsonDocument.Parse(json)) return document.RootElement.Clone(); }
        public static JsonElement Read(string path) { if (new FileInfo(path).Length > 16 * 1024 * 1024) throw new InvalidDataException("Query source exceeds 16 MiB."); return Parse(File.ReadAllText(path)); }
        public static JsonElement Field(JsonElement node, string name) => Properties(node).FirstOrDefault(p => string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase)).Value;
        public static IEnumerable<JsonProperty> Properties(JsonElement node) => node.ValueKind == JsonValueKind.Object ? node.EnumerateObject().ToArray() : Enumerable.Empty<JsonProperty>();
        public static JsonElement[] Array(JsonElement node, string name) { var value = Field(node, name); return value.ValueKind == JsonValueKind.Array ? value.EnumerateArray().ToArray() : System.Array.Empty<JsonElement>(); }
        public static string Text(JsonElement node, string name) => Scalar(Field(node, name));
        public static string Scalar(JsonElement value) => value.ValueKind == JsonValueKind.String ? value.GetString() : value.ValueKind == JsonValueKind.Number ? value.GetRawText() : "";
        public static int Integer(JsonElement node, string name) => int.TryParse(Text(node, name), out int number) ? number : 0;
        public static string Hash(byte[] bytes) { using (var hash = SHA256.Create()) return string.Concat(hash.ComputeHash(bytes).Select(b => b.ToString("x2"))); }
    }
}
