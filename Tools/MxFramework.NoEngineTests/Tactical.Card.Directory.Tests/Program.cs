using System.Text.Json;
using System.Text.Json.Nodes;
using MxFramework.Authoring;

namespace MxFramework.Authoring.Cli;

// Standalone read-only regression entry. It uses the existing memory-stream runtime loader;
// no Unity process, player save, authoring command or shared service is opened.
internal static class Program
{
    internal const int ExitReady = 0, ExitToolError = 1;
    internal static string GetOption(string[] args, string name, string fallback)
    { int index = Array.IndexOf(args, name); return index >= 0 && index + 1 < args.Length ? args[index + 1] : fallback; }
    private static readonly JsonSerializerOptions Json = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, WriteIndented = true };
    private static JsonElement Serialize(object value) => JsonSerializer.SerializeToElement(value, Json);
    private static string Kind(JsonElement value) => value.GetProperty("kind").GetString();

    public static int Main(string[] args)
    {
        if (args.Length < 2) throw new ArgumentException("Pass tests|catalog|preview, project-root and optional output JSON path.");
        string mode = args[0], root = Path.GetFullPath(args[1]);
        if (mode == "preview")
        {
            var result = TacticalCardDirectoryCommands.Preview(root, args[2], "zh-CN", "", "{}", null);
            Console.WriteLine(JsonSerializer.Serialize(result, Json)); return 0;
        }
        if (mode == "catalog")
        {
            string result = JsonSerializer.Serialize(TacticalCardDirectoryCommands.Catalog(root, "zh-CN"), Json);
            if (args.Length > 2) File.WriteAllText(args[2], result); else Console.WriteLine(result);
            return 0;
        }
        if (mode != "tests") throw new ArgumentException("Unknown mode.");
        int checks = 0;
        void Check(bool pass, string name) { if (!pass) throw new Exception("FAIL " + name); checks++; Console.WriteLine("PASS " + name); }
        var catalog = Serialize(TacticalCardDirectoryCommands.Catalog(root, "zh-CN"));
        var cards = catalog.GetProperty("cards").EnumerateArray().ToArray();
        Check(cards.Length > 0 && cards.All(c => c.TryGetProperty("cardType", out _)), "every source record carries an explicit type outcome");
        var formal = cards.Where(c => c.GetProperty("state").GetString() == "published").ToArray();
        var utilities = formal.Where(c => Kind(c.GetProperty("cardType")) == "Utility").ToArray();
        Check(utilities.Length == 5, "five current formal preparation cards are Utility");
        Check(utilities.All(c => c.GetProperty("cardType").GetProperty("utilityPayloadKind").GetString() == "InteractionPreparation"
            && c.GetProperty("cardType").GetProperty("utilityPayloadKindValue").GetInt32() == 3), "real utility payload name and numeric discriminator are preserved");
        Check(formal.Any(c => Kind(c.GetProperty("cardType")) == "Action"), "supported formal action sources are Action");
        Check(cards.Where(c => Kind(c.GetProperty("cardType")) != "Unknown").All(c => c.GetProperty("cardType").GetProperty("status").GetString()
            == (c.GetProperty("record").GetProperty("sourceLayer").GetString() == "disk-published" ? "published-source" : "source-preview")), "draft and configured types remain source previews");
        var records = new TacticalCardQuery(root).ReadAll(includeUnavailable: true);
        var utilityIds = utilities.Select(c => c.GetProperty("key").GetString()).ToHashSet();
        var prep = records.First(r => utilityIds.Contains(TacticalCardDirectoryProjection.Key(r)));
        var actionId = formal.First(c => Kind(c.GetProperty("cardType")) == "Action").GetProperty("key").GetString();
        var action = records.Single(r => TacticalCardDirectoryProjection.Key(r) == actionId);
        using (var projector = new TacticalCardQueryCommands.RuntimeProjector(root))
        {
            var expectedEffects = new Dictionary<string, string> {
                ["PursuitMark"] = "追加8点基础伤害", ["MomentumRecovery"] = "获得3动能",
                ["DefensePreparation"] = "12点散势压力", ["ReturnMark"] = "尝试回返一次", ["HurtCharge"] = "外来实际HP伤害"
            };
            var observedEffects = new HashSet<string>();
            var texts = new HashSet<string>();
            foreach (var record in records.Where(r => utilityIds.Contains(TacticalCardDirectoryProjection.Key(r))))
            {
                string effect = record.TacticalSource.GetProperty("legacy").GetProperty("interactionPreparation").GetProperty("effect").GetString();
                record.Title = "误导的行动卡名称"; record.ShortDescription = "过时副本文案，不是效果权威";
                var result = Serialize(projector.Project(record, "zh-CN"));
                string text = result.GetProperty("description").GetString(), details = result.GetProperty("descriptionDetails").GetString();
                Check(expectedEffects.TryGetValue(effect, out var expected) && text.Contains(expected), "fixed authoritative description for " + effect + " ignores title and stored copy");
                Check(result.GetProperty("descriptionBasis").GetString() == "game-fixed-interaction-preparation"
                    && result.GetProperty("unboundDescription").GetString() == text && details.Contains("F+600")
                    && details.Contains("同类每回合一次") && details.Contains("派生伤害不触发准备效果")
                    && !result.GetProperty("measured").GetBoolean() && !result.GetProperty("executable").GetBoolean(), "fixed rules, warnings and read-only scope retained for " + effect);
                texts.Add(text); observedEffects.Add(effect);
            }
            Check(observedEffects.SetEquals(expectedEffects.Keys) && texts.Count == 5, "all five effect contracts produce distinct useful descriptions");
            string actionName = action.Title; action.Title = "交互准备"; prep.Title = actionName;
            Check(Kind(Serialize(projector.ProjectType(action))) == "Action" && Kind(Serialize(projector.ProjectType(prep))) == "Utility", "misleading and renamed titles cannot change the type");
            prep.SourceLayer = "disk-author";
            Check(Serialize(projector.ProjectType(prep)).GetProperty("status").GetString() == "source-preview", "unpublished preparation source is marked as a preview");
            var invalid = JsonNode.Parse(prep.TacticalSource.GetRawText());
            invalid["legacy"]["interactionPreparation"]["ruleset"] = "future.unsupported";
            bool rejected = false;
            try { projector.ProjectType(new() { TacticalSource = TacticalCardQuery.Parse(invalid.ToJsonString()) }); }
            catch { rejected = true; }
            Check(rejected, "unsupported preparation contracts are rejected by the game parser");
            foreach (string unknownEffect in new[] { "UnknownEffect", "" })
            {
                invalid = JsonNode.Parse(prep.TacticalSource.GetRawText());
                invalid["legacy"]["interactionPreparation"]["effect"] = unknownEffect;
                bool descriptionRejected = false;
                try { projector.Project(new() { TacticalSource = TacticalCardQuery.Parse(invalid.ToJsonString()), Raw = prep.Raw }, "zh-CN"); }
                catch { descriptionRejected = true; }
                Check(descriptionRejected, "unknown/empty effect cannot fabricate a fixed description: " + unknownEffect);
            }
            foreach (string empty in new[] { null, "", " \n\t", "。", "...！?" })
            {
                bool emptyRejected = false;
                try { TacticalCardQueryCommands.RuntimeProjector.RequireDescriptionText(empty); }
                catch (InvalidDataException) { emptyRejected = true; }
                Check(emptyRejected, "empty or punctuation-only fixed text is an unavailable projection");
            }
            bool unavailable = false;
            try { projector.ProjectType(new() { Title = "正式行动卡", SourceLayer = "disk-published" }); }
            catch (InvalidDataException) { unavailable = true; }
            Check(unavailable, "unavailable indexed source cannot be promoted to Action by its title or publication layer");
            var emptySource = Serialize(projector.Project(new() { Title = "追击印记" }, "zh-CN"));
            Check(emptySource.GetProperty("status").GetString() == "source-unavailable" && !emptySource.TryGetProperty("description", out _), "empty source cannot borrow a preparation description from its name");
            var generated = Serialize(projector.Project(action, "zh-CN"));
            Check(generated.TryGetProperty("description", out _) && !generated.TryGetProperty("cardType", out _), "existing description projection remains separate from directory type outcomes");
        }
        string fixture = Path.Combine(Path.GetTempPath(), "mx-card-directory-type-" + Guid.NewGuid().ToString("N"));
        string configured = Path.Combine(fixture, "Authoring/RuntimeAiLab/TestCards/future.json");
        Directory.CreateDirectory(Path.GetDirectoryName(configured));
        File.WriteAllText(configured, "{\"schema\":\"mx.character-test.configured-card.v1\",\"id\":\"future\",\"name\":\"准备行动卡\",\"baseKey\":\"published:missing\",\"source\":{\"schema\":\"unsupported\"}}");
        var unknown = Serialize(TacticalCardDirectoryCommands.Catalog(fixture, "zh-CN")).GetProperty("cards")[0];
        Check(Kind(unknown.GetProperty("cardType")) == "Unknown" && unknown.GetProperty("cardType").GetProperty("status").GetString() == "unavailable", "missing runtime retains source record with unknown type and diagnostics");
        File.Delete(configured); // Only a file created by this test; no recursive repository operations.
        Check(cards.Select(c => c.GetProperty("key").GetString()).Distinct().Count() == cards.Length, "source identity and historical versions stay distinct");
        Console.WriteLine($"{checks} directory type checks passed; {cards.Length} records / {formal.Length} current formal / {utilities.Length} Utility.");
        return 0;
    }
}
