using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using static MxFramework.Authoring.TacticalCardQuery;

namespace MxFramework.Authoring
{
    /// <summary>Read-only card directory. Uses the query selector and existing component decoder; owns no card content.</summary>
    /// <remarks>LowFreqAlloc. Templates, author drafts and configured test copies retain distinct identities.</remarks>
    public sealed class TacticalCardDirectoryProjection
    {
        private readonly string _root;
        public TacticalCardDirectoryProjection(string root) { _root = Path.GetFullPath(root); }
        public TacticalCardQueryRecord[] Records { get; private set; } = System.Array.Empty<TacticalCardQueryRecord>();
        public string[] Diagnostics { get; private set; } = System.Array.Empty<string>();

        public object[] Read()
        {
            var query = new TacticalCardQuery(_root); Records = query.ReadAll(includeUnavailable: true)
                .OrderBy(r => r.SourceLayer == "disk-published" ? 0 : r.SourceLayer == "disk-author" ? 1 : 2)
                .ThenBy(r => r.Title, StringComparer.Ordinal).ThenBy(r => r.StableId, StringComparer.Ordinal).ThenByDescending(r => r.Revision).ToArray();
            var issues = new List<string>(query.Diagnostics);
            CombatDesignCatalogProjection componentProjection = null;
            try { componentProjection = new CombatDesignCatalogProjection(_root); componentProjection.Query(); }
            catch (Exception error) when (error is IOException || error is InvalidOperationException || error is JsonException)
            { componentProjection = null; issues.Add("Component classification unavailable: " + error.Message); }
            string rolesPath = Path.Combine(_root, "Authoring/RuntimeAiLab/Drafts/workspace-roles.json");
            JsonElement roles = File.Exists(rolesPath) ? TacticalCardQuery.Read(rolesPath) : default;
            var result = new List<object>();
            foreach (var record in Records)
            {
                var raw = record.Raw;
                bool author = record.SourceLayer == "disk-author", published = record.SourceLayer == "disk-published";
                var metadata = author ? raw : Field(raw, "metadata");
                int sourceRevision = Integer(raw, "sourcePublishedRevision");
                string sourceHash = Text(raw, "sourcePublishedContentHash");
                var versions = Records.Where(r => r.SourceLayer == "disk-published" && r.StableId == record.StableId).ToArray();
                int latest = versions.Select(r => r.Revision).DefaultIfEmpty(0).Max();
                bool sourceExists = versions.Any(r => r.Revision == sourceRevision && r.ContentHash == sourceHash);
                string state = published ? record.Revision == latest ? "published" : "historical"
                    : !author ? "configured" : sourceRevision == 0 ? "unpublished" : !sourceExists ? "missing-source"
                    : Text(raw, "lastCompileStatus") == "Published" && Integer(raw, "lastCompiledRevision") == record.Revision ? "draft-synced" : "draft-changed";
                if (raw.ValueKind != JsonValueKind.Object) state = "invalid";
                var uses = new Dictionary<string, object>(StringComparer.Ordinal);
                foreach (var source in Sources(record.TacticalSource))
                {
                    foreach (var use in componentProjection?.ClassifyComponents(source) ?? System.Array.Empty<object>())
                    {
                        string key = Text(JsonSerializer.SerializeToElement(use), "key");
                        uses[key] = use;
                    }
                }
                string role = Properties(roles).Where(p => Text(p.Value, "workspaceId") == record.WorkspaceId && !string.IsNullOrEmpty(record.WorkspaceId)).Select(p => p.Name).FirstOrDefault();
                result.Add(new {
                    key = Key(record), record, state, latestRevision = latest, sourcePublishedRevision = sourceRevision,
                    sourcePublishedContentHash = sourceHash, workspaceRole = role ?? "unassigned", templateCost = Number(metadata, "cost"),
                    templateCooldownSeconds = Number(author ? raw : Field(raw, "tacticalSource"), "cooldownSeconds"), iconKey = Text(metadata, "iconKey"),
                    slots = Slots(record.TacticalSource), intents = uses.Values.ToArray(),
                    mechanisms = uses.Values.Select(v => JsonSerializer.SerializeToElement(v)).Select(v => Text(v, "category")).Where(v => v.Length > 0).Distinct().ToArray(),
                    sourceUrl = SourceUrl(record), unsupportedSource = record.TacticalSource.ValueKind != JsonValueKind.Object,
                    storedDescription = record.ShortDescription, sourceIssue = record.SourceIssue,
                    classification = "Saved source blocks and execution-input contracts use the same current-source component decoder; unmatched sources stay visible."
                });
            }
            Diagnostics = issues.ToArray(); return result.ToArray();
        }

        public static string Key(TacticalCardQueryRecord record) => record.SourceLayer == "disk-published"
            ? "published:" + record.StableId + "@" + record.Revision + ":" + record.ContentHash
            : record.SourceLayer == "disk-author" ? "author:" + record.WorkspaceId + ":" + record.RecordId
            : "configured:" + record.RecordId + ":" + Hash(Encoding.UTF8.GetBytes(record.SourcePath ?? "")).Substring(0, 12);

        public static object[] Slots(JsonElement source)
        {
            var rows = new List<object>();
            void Add(JsonElement definition, string prefix) {
                foreach (var slot in Array(definition, "slots"))
                    if (!new[] { "stepBack", "stepForward" }.Contains(Text(slot, "resolver")))
                        rows.Add(new { path = prefix + Text(slot, "slotId"), label = Text(slot, "description"), resolver = Text(slot, "resolver") });
            }
            var legacy = Field(source, "legacy");
            if (legacy.ValueKind == JsonValueKind.Object) Add(legacy, "");
            else foreach (var node in References(Field(Field(source, "composition"), "root")))
            {
                var definition = Array(source, "definitions").FirstOrDefault(d => Text(d, "definitionId") == Text(node, "definitionId"));
                Add(Field(definition, "source"), Text(node, "nodeId") + "/");
            }
            return rows.GroupBy(r => Text(JsonSerializer.SerializeToElement(r), "path"), StringComparer.Ordinal).Select(g => g.First()).ToArray();
        }
        private static IEnumerable<JsonElement> References(JsonElement node)
        { if (Text(node, "kind") == "Reference") yield return node; foreach (var child in Array(node, "children")) foreach (var nested in References(child)) yield return nested; }
        private static IEnumerable<JsonElement> Sources(JsonElement source)
        {
            var legacy = Field(source, "legacy"); if (legacy.ValueKind == JsonValueKind.Object) yield return legacy;
            var used = References(Field(Field(source, "composition"), "root")).Select(n => Text(n, "definitionId")).ToHashSet();
            foreach (var definition in Array(source, "definitions")) if (used.Contains(Text(definition, "definitionId"))) yield return Field(definition, "source");
        }
        private static double? Number(JsonElement value, string key) => double.TryParse(Text(value, key), System.Globalization.NumberStyles.Float,
            System.Globalization.CultureInfo.InvariantCulture, out double number) ? number : (double?)null;
        private string SourceUrl(TacticalCardQueryRecord record)
        {
            string relative = Path.GetRelativePath(_root, record.SourcePath ?? _root).Replace('\\', '/');
            return relative.StartsWith("../", StringComparison.Ordinal) ? null : "/Tools/MxFramework.EditorHub/web/combat-design-source.html?cardKey="
                + Uri.EscapeDataString(Key(record)) + "&path=" + Uri.EscapeDataString(relative);
        }

        /// <summary>Authored action choices; availability does not imply player ownership or executable bindings.</summary>
        public static object[] Actions(string root)
        {
            var result = new List<object>(); var formalPath = Path.Combine(root, "Assets/StreamingAssets/MxFramework/CharacterTest/tactical_content.json");
            var formal = File.Exists(formalPath) ? Array(TacticalCardQuery.Read(formalPath), "actions") : System.Array.Empty<JsonElement>();
            var generatedRoot = Path.Combine(root, "Assets/MxFrameworkGenerated/CharacterActions");
            var actions = new List<(JsonElement value, string source)>();
            if (Directory.Exists(generatedRoot)) foreach (var path in Directory.EnumerateFiles(generatedRoot, "character_actions.json", SearchOption.AllDirectories))
                foreach (var action in Array(TacticalCardQuery.Read(path), "actions"))
                    if (new[] { "BasicAttack", "Skill", "Guard", "Dodge" }.Contains(Text(action, "category"))) actions.Add((action, path));
            foreach (var entry in formal) {
                var match = actions.FirstOrDefault(a => Integer(a.value, "actionId") == Integer(entry, "actionId"));
                result.Add(new { id = Text(entry, "id"), actionId = Integer(entry, "actionId"), name = Text(entry, "displayName"), category = Text(match.value, "category"), source = formalPath });
            }
            foreach (var group in actions.GroupBy(a => Integer(a.value, "actionId")))
                if (!formal.Any(a => Integer(a, "actionId") == group.Key))
                    foreach (var name in group.GroupBy(a => Text(a.value, "displayName")))
                        result.Add(new { id = "action:" + group.Key, actionId = group.Key, name = name.Key, category = Text(name.First().value, "category"), source = name.First().source });
            return result.ToArray();
        }

        public static object[] Characters(string root)
        {
            var folder = Path.Combine(root, "Assets/MxFrameworkGenerated/CharacterPackages");
            if (!Directory.Exists(folder)) return System.Array.Empty<object>();
            var result = new List<object>();
            foreach (var package in Directory.EnumerateDirectories(folder).OrderBy(p => p, StringComparer.Ordinal)) {
                string path = Path.Combine(package, "config/character_config_patch.json"); if (!File.Exists(path)) continue;
                foreach (var entry in Array(Field(TacticalCardQuery.Read(path), "patch"), "entries").Where(e => Text(e, "source") == "CharacterConfig")) {
                    var fields = Field(entry, "fields");
                    result.Add(new { id = Text(fields, "StableId"), name = Path.GetFileName(package), nameKey = Text(fields, "NameText"), source = path });
                }
            }
            return result.ToArray();
        }
    }
}
