using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using MxFramework.Authoring;

namespace MxFramework.Authoring.Tests;

internal static class TacticalCardQueryTests
{
    public static int Run()
    {
        int passed = 0;
        void Check(bool value, string message) { if (!value) throw new InvalidOperationException(message); passed++; }
        void Throws(Action action) { bool threw = false; try { action(); } catch (ArgumentException) { threw = true; } Check(threw, "Invalid selector must fail."); }
        string root = Path.Combine(Path.GetTempPath(), "MxTacticalCardQueryTests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        void Write(string path, object data) { path = Path.Combine(root, path); Directory.CreateDirectory(Path.GetDirectoryName(path)); File.WriteAllText(path, JsonSerializer.Serialize(data)); }
        object Source() => new { schema = "mx.character-test.published-tactical-source.v1", legacy = new { schema = "mx.character-test.tactical-card-source.v4" } };
        object Configured(string id, string action) => new { Schema = "mx.character-test.configured-card.v1", Id = id, Name = "same", Revision = 1, BaseKey = "draft:origin",
            Source = Source(), Metadata = new { ShortDescription = "static, not generated" }, Actions = new System.Collections.Generic.Dictionary<string, string> { ["attack/primary"] = action } };
        try
        {
            Write("Authoring/RuntimeAiLab/TestCards/Shared/a.json", Configured("a", "301"));
            Write("Authoring/RuntimeAiLab/TestCards/Shared/b.json", Configured("b", "302"));
            var query = new TacticalCardQuery(root);
            Check(query.Find(new() { Name = "same" }).Length == 2, "Duplicate names must remain ambiguous.");
            Check(query.Find(new() { Contains = "SAM" }).Length == 2, "Fuzzy selector ignores case.");
            var one = query.Find(new() { Id = "a" }).Single();
            Check(one.Bindings["attack/primary"] == "301" && one.StableId == null && one.RecordId == "a", "PascalCase binding and record identity.");
            Check(one.ShortDescription == "static, not generated", "Static shortDescription remains separately labeled.");
            string old = one.SourceFingerprint;
            Write("Authoring/RuntimeAiLab/TestCards/Shared/a.json", Configured("a", "455"));
            one = query.Find(new() { Id = "a" }).Single();
            Check(one.SourceFingerprint != old && one.Bindings["attack/primary"] == "455", "Source and binding edits cannot return stale cache.");
            Throws(() => query.Find(new() { Name = "same", Id = "a" }));
            Throws(() => query.Find(new() { Id = "a", Layer = "unknown" }));
            Write("Assets/StreamingAssets/MxFramework/CharacterTest/runtime_ai_planner/behavior_card_catalog.json", new { cards = new[] { new { file = "behavior_cards/p.json" } } });
            Write("Assets/StreamingAssets/MxFramework/CharacterTest/runtime_ai_planner/behavior_cards/p.json", new { cards = new[] { new { stableCardId = "p", revision = 2, contentHash = "exact", metadata = new { displayName = "published" }, tacticalSource = Source() } } });
            Write("save.json", new { schema = "mx.character-test.tactical-player.v3", characters = new[] { new { characterStableId = "role", cards = new[] { new { instanceId = "instance", cardId = "p", revision = 1, contentHash = "old" } }, bindings = new[] { new { cardInstanceId = "instance", slotPath = "attack/primary", tacticalActionId = "301" } } } } });
            var player = query.Find(new() { InstanceId = "instance", SaveJson = Path.Combine(root, "save.json") }).Single();
            Check(player.SourceIssue != null && player.Revision == 1 && player.TacticalSource.ValueKind == JsonValueKind.Undefined, "Missing exact version cannot resolve to latest.");
            Check(player.Bindings["attack/primary"] == "301" && player.Owner == "role", "Player instance bindings stay scoped.");
            string cardPath = "Authoring/RuntimeAiLab/Drafts/w/cards/card.json";
            Write(cardPath, new { cardDraftId = "draft", currentRevision = 1, displayName = "author", payload = new { schema = "mx.character-test.tactical-card-source.v4" } });
            string hash = TacticalCardQuery.Hash(Encoding.UTF8.GetBytes(File.ReadAllText(Path.Combine(root, cardPath))));
            Write("Authoring/RuntimeAiLab/Drafts/w/workspace.json", new { schema = "mx.character-test.runtime-ai-lab.workspace.v4", cards = new[] { new { id = "draft", revision = 1, hash, path = "cards/card.json" } } });
            Check(query.Find(new() { Id = "draft" }).Length == 1, "Valid indexed source.");
            File.AppendAllText(Path.Combine(root, cardPath), " ");
            Check(query.Find(new() { Id = "draft" }).Length == 0 && query.Diagnostics.Any(d => d.Contains("hash mismatch")), "Stale indexed content fails closed.");
            bool escape = false; try { TacticalCardQuery.SafeChild(root, "../escape.json"); } catch (InvalidDataException) { escape = true; }
            Check(escape, "Indexed path traversal rejected.");
            Console.WriteLine("Tactical card query tests passed: " + passed);
            return passed;
        }
        finally
        {
            if (Path.GetDirectoryName(Path.GetFullPath(root)) == Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar)
                && Path.GetFileName(root).StartsWith("MxTacticalCardQueryTests-", StringComparison.Ordinal)) Directory.Delete(root, true);
        }
    }
}
