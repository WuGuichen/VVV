using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using MxFramework.Authoring;

if (args.Length != 1) throw new ArgumentException("Pass the project root; tests only read it and mutate a private temporary fixture.");
string root = Path.GetFullPath(args[0]);
int checks = 0;
void Check(bool pass, string name) { if (!pass) throw new Exception("FAIL " + name); checks++; Console.WriteLine("PASS " + name); }
JsonElement Query(string path) => JsonSerializer.SerializeToElement(new CombatDesignCatalogProjection(path).Query());
JsonElement[] Components(JsonElement data) => data.GetProperty("components").EnumerateArray().ToArray();
JsonElement Component(JsonElement data, string key) => Components(data).Single(e => e.GetProperty("key").GetString() == key);
string Hash(string text) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text))).ToLowerInvariant();
string State(JsonElement data, string key) => Component(data, key).GetProperty("implementation").GetString();
JsonElement baseline = Query(root);
string[] expected = { "approach", "keep-distance", "move-away", "retreat-to-distance", "orbit", "facing-target", "facing-action", "forward-step", "step-back", "wait-condition", "ordinary-guard", "wait-two-seconds", "wait-target-distance", "selected-point", "behind-target-arrive", "behind-target-maintain" };
Check(Components(baseline).Select(c => c.GetProperty("key").GetString()).Order().SequenceEqual(expected.Order()), "complete unique component contracts");
Check(Components(baseline).All(c => c.GetProperty("implementation").GetString() == "main-source"), "all supported components backed by current main-tree members");
Check(Components(baseline).All(c => !c.TryGetProperty("definitionId", out _) && c.GetProperty("kind").GetString() == "intent-component"), "navigation identity does not fabricate a shared definition or input IntentId");
Check(baseline.GetProperty("entries").EnumerateArray().Any(e => e.GetProperty("kind").GetString() == "template-expression") && Components(baseline).All(c => c.GetProperty("name").GetString() != "守距观望"), "complete tactical templates stay outside component directory");
Check(Component(baseline, "wait-condition").GetProperty("sources").EnumerateArray().Any(s => s.GetProperty("excerpt").GetString().Contains("Revision = 2")), "wait-condition r2 from current factory");
Check(Component(baseline, "step-back").GetProperty("action").GetProperty("data").GetProperty("actionId").GetInt32() == 950003, "step-back uses authored functional action, not input binding");
Check(Component(baseline, "behind-target-arrive").GetProperty("factoryMode").GetString() == "maintain = false" && Component(baseline, "behind-target-maintain").GetProperty("factoryMode").GetString() == "maintain = true", "two behind-target modes remain distinct");
Check(Query(root).GetProperty("projectionHash").GetString() == baseline.GetProperty("projectionHash").GetString(), "unchanged sources generate deterministic payload");
foreach (var component in Components(baseline)) foreach (var evidence in component.GetProperty("sources").EnumerateArray())
{
    string file = Path.Combine(root, evidence.GetProperty("path").GetString());
    int line = evidence.GetProperty("line").GetInt32();
    if (!File.ReadAllLines(file)[line - 1].Contains(evidence.GetProperty("member").GetString())) throw new Exception("Wrong source line " + file);
}
Check(true, "every source link targets the actual member declaration");
var sourceReply = JsonSerializer.SerializeToElement(new CombatDesignCatalogProjection(root).ReadSource(Component(baseline, "wait-condition").GetProperty("sources")[0].GetProperty("path").GetString()));
Check(sourceReply.GetProperty("text").GetString().Contains("CreateWaitOpportunity"), "source viewer returns current source");
bool rejected = false;
try { new CombatDesignCatalogProjection(root).ReadSource("../../outside.txt"); } catch (ArgumentException) { rejected = true; }
Check(rejected, "source viewer rejects arbitrary paths");

string fixture = Path.Combine(Path.GetTempPath(), "mx-combat-catalog-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(fixture);
foreach (var read in baseline.GetProperty("readSet").EnumerateArray())
{
    string relative = read.GetProperty("path").GetString(), target = Path.Combine(fixture, relative);
    Directory.CreateDirectory(Path.GetDirectoryName(target)); File.Copy(Path.Combine(root, relative), target);
}
string[] Keys(JsonElement data) => Components(data).Select(c => c.GetProperty("key") + ":" + c.GetProperty("implementation")).ToArray();
string doc = Path.Combine(fixture, "Docs/Guides/CHARACTER_TEST_TACTICAL_CARD_AUTHORING.md");
File.AppendAllText(doc, "\nUnrelated prose change for catalog regression.\n");
Check(Keys(Query(fixture)).SequenceEqual(Keys(baseline)), "unrelated document edits cannot hide implemented components");

string keepPath = Component(baseline, "keep-distance").GetProperty("instances")[0].GetProperty("source").GetString();
string keepFile = Path.Combine(fixture, keepPath), original = File.ReadAllText(keepFile);
void SaveIndexed(JsonNode data)
{
    string text = data.ToJsonString(new JsonSerializerOptions { WriteIndented = true }); File.WriteAllText(keepFile, text, new UTF8Encoding(false));
    string workspace = Path.Combine(Path.GetDirectoryName(Path.GetDirectoryName(keepFile)), "workspace.json");
    var manifest = JsonNode.Parse(File.ReadAllText(workspace));
    foreach (var item in manifest["tacticalDefinitions"].AsArray()) if (item["id"].GetValue<string>() == data["definitionId"].GetValue<string>()) item["hash"] = Hash(text);
    File.WriteAllText(workspace, manifest.ToJsonString(), new UTF8Encoding(false));
}
var keep = JsonNode.Parse(original);
keep["source"]["name"] = "Rename is not a classifier";
keep["source"]["timelines"][0]["eventBlocks"][0]["title"] = "Arbitrary block title";
SaveIndexed(keep);
Check(Component(Query(fixture), "keep-distance").GetProperty("instances").GetArrayLength() == Component(baseline, "keep-distance").GetProperty("instances").GetArrayLength(), "component recognition ignores names and template titles");
keep["source"]["timelines"][0]["events"][0]["conditionRoot"] = JsonNode.Parse("{\"kind\":\"All\",\"children\":[]}");
SaveIndexed(keep);
Check(Component(Query(fixture), "keep-distance").GetProperty("instances").GetArrayLength() == 0, "changed event conditions stop standard movement-instance recognition");
SaveIndexed(JsonNode.Parse(original));

string visual = Path.Combine(fixture, "Assets/Scripts/WGameFrameworkProject/CharacterTest/RuntimeAiPlannerDev/CharacterTestTacticalAuthoringTemplates.Visual.cs");
string visualOriginal = File.ReadAllText(visual);
File.WriteAllText(visual, visualOriginal.Replace("CreateFacingForwardStep()", "RemovedFacingForwardStep()"));
Check(State(Query(fixture), "forward-step") == "needs-review", "missing factory becomes a review candidate instead of claiming implementation");
File.WriteAllText(visual, visualOriginal);
string wait = Path.Combine(fixture, "Assets/Scripts/WGameFrameworkProject/CharacterTest/RuntimeAiPlannerDev/CharacterTestTacticalAuthoringTemplates.WaitOpportunity.cs");
File.WriteAllText(wait, File.ReadAllText(wait).Replace("Revision = 2", "Revision = 3"));
Check(State(Query(fixture), "wait-condition") == "needs-review" && State(Query(fixture), "wait-two-seconds") == "main-source", "contract drift is scoped to the changed component");
Console.WriteLine($"{checks} checks passed. Fixture retained at {fixture}");
