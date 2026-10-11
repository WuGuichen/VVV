using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace MxFramework.Authoring.Cli;

internal static partial class EditorServer
{
    private static bool TryHandleTacticalContentApi(HttpListenerContext context, string rootPath, JsonSerializerOptions options, string path)
    {
        if (path != "/api/character/tactical-content" || context.Request.HttpMethod != "GET") return false;
        var directory = new DirectoryInfo(rootPath);
        while (directory != null)
        {
            string file = Path.Combine(directory.FullName, "Assets", "StreamingAssets", "MxFramework", "CharacterTest", "tactical_content.json");
            if (File.Exists(file))
            {
                var document = JsonNode.Parse(File.ReadAllText(file))!.AsObject();
                var cards = new JsonArray();
                string behaviorRoot = Path.Combine(Path.GetDirectoryName(file)!, "runtime_ai_planner");
                var index = JsonNode.Parse(File.ReadAllText(Path.Combine(behaviorRoot, "behavior_card_catalog.json")))!;
                foreach (var entry in index["cards"]!.AsArray())
                {
                    string relative = entry!["file"]!.GetValue<string>();
                    string cardPath = ResolveSafePath(behaviorRoot, relative);
                    var catalog = JsonNode.Parse(File.ReadAllText(cardPath))!;
                    foreach (var card in catalog["cards"]!.AsArray())
                    {
                        var source = card?["tacticalSource"];
                        if (source == null) continue;
                        var slots = new JsonArray();
                        if (source["legacy"] is JsonNode legacy)
                            foreach (var slot in legacy["slots"]!.AsArray()) slots.Add(slot!["slotId"]!.GetValue<string>());
                        else CollectTacticalSlots(source["composition"]!["root"]!, source["definitions"]!.AsArray(), slots);
                        cards.Add(new JsonObject {
                            ["cardId"] = card!["stableCardId"]!.GetValue<string>(), ["revision"] = card["revision"]!.GetValue<int>(),
                            ["contentHash"] = card["contentHash"]!.ToJsonString(),
                            ["displayName"] = card["metadata"]!["displayName"]!.GetValue<string>(), ["slots"] = slots,
                            ["motionCost"] = card["metadata"]!["cost"]!.GetValue<int>()
                        });
                    }
                }
                document["cards"] = cards;
                WriteJson(context.Response, document, options);
                return true;
            }
            directory = directory.Parent;
        }
        WriteJson(context.Response, new { message = "项目尚未发布战术内容目录。" }, options, 404);
        return true;
    }

    private static void CollectTacticalSlots(JsonNode node, JsonArray definitions, JsonArray result)
    {
        if (node["kind"]?.GetValue<string>() == "Reference")
        {
            string definitionId = node["definitionId"]!.GetValue<string>();
            var definition = definitions.First(value => value?["definitionId"]?.GetValue<string>() == definitionId)!;
            foreach (var slot in definition["source"]!["slots"]!.AsArray())
                result.Add(node["nodeId"]!.GetValue<string>() + "/" + slot!["slotId"]!.GetValue<string>());
        }
        if (node["children"] is JsonArray children)
            foreach (var child in children) CollectTacticalSlots(child!, definitions, result);
    }
}
