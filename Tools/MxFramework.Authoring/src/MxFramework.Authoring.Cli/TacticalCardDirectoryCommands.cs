using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Nodes;
using MxFramework.Authoring;
using static MxFramework.Authoring.TacticalCardQuery;

namespace MxFramework.Authoring.Cli;

/// <summary>Read-only EditorHub directory and temporary binding projection. Never submits authoring commands.</summary>
internal static class TacticalCardDirectoryCommands
{
    private static readonly JsonSerializerOptions Json = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
    internal static object Source(string root, string key)
    {
        var record = new TacticalCardQuery(root).ReadAll(includeUnavailable: true).SingleOrDefault(r => TacticalCardDirectoryProjection.Key(r) == key)
            ?? throw new ArgumentException("CARD_NOT_FOUND");
        // Path comes from the same safe indexed selector, never from the request.
        string text = File.ReadAllText(record.SourcePath);
        return new { path = Path.GetRelativePath(root, record.SourcePath).Replace('\\', '/'),
            hash = Hash(System.Text.Encoding.UTF8.GetBytes(text)), text };
    }
    internal static object Catalog(string root, string locale)
    {
        var timer = Stopwatch.StartNew();
        var directory = new TacticalCardDirectoryProjection(root);
        var rows = directory.Read(); var cards = new List<JsonObject>();
        TacticalCardQueryCommands.RuntimeProjector projector = null; string unavailable = null;
        try { projector = new TacticalCardQueryCommands.RuntimeProjector(root); }
        catch (Exception error) { unavailable = (error.InnerException ?? error).Message; }
        using (projector)
            for (int i = 0; i < rows.Length; i++)
            {
                var row = JsonSerializer.SerializeToNode(rows[i], Json).AsObject();
                row["cardType"] = JsonSerializer.SerializeToNode(GenerateType(projector, directory.Records[i], unavailable), Json);
                row["generated"] = JsonSerializer.SerializeToNode(Generate(projector, directory.Records[i], locale, unavailable), Json);
                cards.Add(row);
            }
        return new {
            schema = "mx.tactical-card.directory.v1", generatedUtc = DateTime.UtcNow.ToString("O"), elapsedMs = timer.ElapsedMilliseconds,
            cards, counts = new { published = cards.Count(c => (string)c["state"] == "published"),
                publishedVersions = directory.Records.Count(r => r.SourceLayer == "disk-published"),
                authorDrafts = directory.Records.Count(r => r.SourceLayer == "disk-author"), configured = directory.Records.Count(r => r.SourceLayer == "disk-configured"),
                unavailable = cards.Count(c => (string)c["state"] == "invalid"), total = cards.Count },
            characters = TacticalCardDirectoryProjection.Characters(root), actions = TacticalCardDirectoryProjection.Actions(root),
            diagnostics = directory.Diagnostics, fingerprint = Hash(System.Text.Encoding.UTF8.GetBytes(JsonSerializer.Serialize(cards, Json))),
            scope = "Current indexed publications, indexed author drafts and saved configured test copies. No player save is read; no card is created or published."
        };
    }

    internal static object Preview(string root, string key, string locale, string role, string bindings, string expectedSource)
    {
        var query = new TacticalCardQuery(root);
        var record = query.ReadAll(includeUnavailable: true).SingleOrDefault(r => TacticalCardDirectoryProjection.Key(r) == key)
            ?? throw new ArgumentException("CARD_NOT_FOUND: 卡来源已变化，请刷新目录。");
        if (!string.IsNullOrEmpty(expectedSource) && record.SourceFingerprint != expectedSource)
            throw new ArgumentException("SOURCE_CHANGED: 卡来源已变化，请刷新目录后重选预览。");
        if (!string.IsNullOrEmpty(role) && !TacticalCardDirectoryProjection.Characters(root).Any(c => Text(JsonSerializer.SerializeToElement(c), "id") == role))
            throw new ArgumentException("UNKNOWN_CHARACTER: 请选择当前角色目录中的角色。");
        var slots = TacticalCardDirectoryProjection.Slots(record.TacticalSource).Select(s => Text(JsonSerializer.SerializeToElement(s), "path")).ToHashSet(StringComparer.Ordinal);
        if (!string.IsNullOrEmpty(bindings))
        {
            if (bindings.Length > 16384) throw new ArgumentException("BINDINGS_TOO_LARGE");
            var value = Parse(bindings);
            if (value.ValueKind != JsonValueKind.Object) throw new ArgumentException("INVALID_BINDINGS");
            var available = TacticalCardDirectoryProjection.Actions(root).Select(a => JsonSerializer.SerializeToElement(a)).ToArray();
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var property in Properties(value))
            {
                if (!slots.Contains(property.Name) || !seen.Add(property.Name)) throw new ArgumentException("UNKNOWN_OR_DUPLICATE_SLOT: " + property.Name);
                string action = Scalar(property.Value);
                if (action == "" || action == "0") { record.Bindings.Remove(property.Name); continue; }
                if (!available.Any(a => Text(a, "id") == action || Text(a, "actionId") == action)) throw new ArgumentException("UNKNOWN_ACTION: " + action);
                record.Bindings[property.Name] = action;
            }
        }
        if (role != null) record.Owner = role;
        using var projector = new TacticalCardQueryCommands.RuntimeProjector(root);
        return new { schema = "mx.tactical-card.preview.v1", key, record, generated = Generate(projector, record, locale, null),
            temporary = true, playerInstance = false, measured = false, executable = false,
            message = "临时装配预览；未读取玩家持有或启动角色测量。费用与动作距离等待真实装配。" };
    }

    private static object GenerateType(TacticalCardQueryCommands.RuntimeProjector projector, TacticalCardQueryRecord record, string unavailable)
    {
        try
        {
            if (projector != null) return projector.ProjectType(record);
        }
        catch (Exception error) { unavailable = (error.InnerException ?? error).Message; }
        return new { kind = "Unknown", kindValue = (int?)null, utilityPayloadKind = (string)null, utilityPayloadKindValue = (int?)null,
            status = "unavailable", basis = "No type inferred from title, source layer, or an unavailable publication.", message = unavailable };
    }

    private static object Generate(TacticalCardQueryCommands.RuntimeProjector projector, TacticalCardQueryRecord record, string locale, string unavailable)
    {
        if (projector == null) return new { status = "projection-unavailable", message = unavailable, measured = false, executable = false };
        try { return projector.Project(record, locale ?? "zh-CN"); }
        catch (Exception error) { return new { status = "projection-error", message = (error.InnerException ?? error).Message, measured = false, executable = false }; }
    }
}
