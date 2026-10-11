using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using MxFramework.CharacterAction;
using MxFramework.CharacterApplication;
using MxFramework.Game.CharacterTest;
using MxFramework.Game.CharacterTest.TacticalCard;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

// Executes production source codecs, publisher, catalog projection, deployment and compiler.
// No Editor connection, scene mutation, player service opening or save store is involved.
static class Program
{
    static int checks;
    static void Check(string name, bool pass) { if (!pass) throw new Exception(name); checks++; Console.WriteLine("PASS " + name); }
    static void Reject(string name, Action action)
    {
        try { action(); } catch (Exception e) when (e is ArgumentException || e is JsonException || e is OverflowException)
        { Check(name, true); return; }
        throw new Exception("Accepted invalid data: " + name);
    }
    static TacticalPointMoveAuthoring Contract() => new TacticalPointMoveAuthoring {
        RangeMillimeters = 5000, ArrivalMillimeters = 200, MaximumFrames = 180, SpeedMilli = 1000, RequireReachable = true };
    static CharacterTestTacticalAuthoringSource Source() => CharacterTestTacticalAuthoringTemplates.CreatePointMove("Point fixture", Contract());
    static CharacterTestRuntimeAiLabBehaviorCardDraft Draft() {
        var d = new CharacterTestRuntimeAiLabBehaviorCardDraft("point-fixture-draft", "bc_qkjc3ka7za", "Point fixture", "Tactical", null);
        d.SetTacticalSource(Source()); d.Cost = 3; d.CooldownSeconds = 2.25f; return d;
    }
    static CharacterTestRuntimeAiLabBehaviorCardLogicCompileResult Compile(CharacterTestRuntimeAiLabBehaviorCardDraft draft) {
        var inputs = CharacterTestTacticalCardInputs.Unbound(1);
        Check("author contract compiles without a physical action or selected point",
            CharacterTestTacticalAuthoringCompiler.TryCompile(draft.CloneTacticalSource(), inputs, out var c, out var issues));
        var frozen = CharacterTestTacticalFrozenSource.CaptureLegacy(null, draft, inputs, c);
        return new CharacterTestRuntimeAiLabBehaviorCardLogicCompileResult(c.Program.WithTacticalSource(frozen), null);
    }
    static CharacterTestRuntimeAiPlannerPublishedBehaviorCardMetadata Metadata(int cost = 3, float cooldown = 2.25f) =>
        new CharacterTestRuntimeAiPlannerPublishedBehaviorCardMetadata("Point fixture", "fixture.icon", cost, "Point fixture", "", "", cooldownSeconds: cooldown);
    static CharacterTestRuntimeAiPlannerPublishedBehaviorCardPublishResult Publish(CharacterTestRuntimeAiLabBehaviorCardDraft d,
        CharacterTestRuntimeAiLabBehaviorCardLogicCompileResult compiled, CharacterTestRuntimeAiPlannerPublishedBehaviorCardMetadata metadata = null) =>
        CharacterTestRuntimeAiPlannerPublishedBehaviorCardPublisher.ValidateAndCreate(d,
            new CharacterTestRuntimeAiPlannerPublishedBehaviorCardPublishRequest(d.PublishedStableId, 1,
                CharacterTestRuntimeAiPlannerPublishedBehaviorCardPublisher.CurrentRuntimeContractVersion,
                CharacterTestRuntimeAiPlannerPublishedBehaviorCardPublisher.CurrentRuntimeContractVersion,
                metadata ?? Metadata(), CharacterTestAuthoredContentIds.D417_Id3), null, compiled);
    static void Main(string[] args)
    {
        var source = Source(); var json = JObject.Parse(CharacterTestTacticalAuthoringJson.Write(source));
        Check("source contains only per-play WorldPoint contract", source.ExecutionInput.Kind == "WorldPoint" && source.Slots.Count == 0
            && source.Timelines[0].Events.Count == 0 && !json["executionInput"].Children<JProperty>().Any(p => p.Name == "x" || p.Name == "z"));
        foreach (var field in ((JObject)json["executionInput"]).Properties().Select(p => p.Name).ToArray()) {
            var next = (JObject)json.DeepClone(); ((JObject)next["executionInput"]).Remove(field);
            Reject("required contract field " + field, () => CharacterTestTacticalAuthoringJson.Read(next.ToString()));
        }
        foreach (var pair in new[] { ("rangeMillimeters", (JToken)0), ("arrivalMillimeters", (JToken)5000),
            ("arrivalMillimeters", (JToken)(-1)), ("maximumFrames", (JToken)0), ("maximumFrames", (JToken)int.MaxValue),
            ("speedMilli", (JToken)99), ("speedMilli", (JToken)1001), ("kind", (JToken)"None"),
            ("rangeCenter", (JToken)"Selection"), ("startPolicy", (JToken)"OnEnqueue"), ("requireReachable", (JToken)false),
            ("rangeMillimeters", (JToken)2.5), ("speedMilli", (JToken)"1000"), ("requireReachable", (JToken)"true"),
            ("x", (JToken)0) }) {
            var next = (JObject)json.DeepClone(); next["executionInput"][pair.Item1] = pair.Item2;
            Reject("invalid contract " + pair.Item1 + "=" + pair.Item2, () => CharacterTestTacticalAuthoringJson.Read(next.ToString()));
        }
        var mixed = Source(); mixed.Slots.Add(new CharacterTestTacticalAuthoringSlot { SlotId = "primary" });
        Reject("mixed point and action authorities", () => CharacterTestTacticalAuthoringJson.Read(CharacterTestTacticalAuthoringJson.Write(mixed)));
        var malformed = (JObject)json.DeepClone(); malformed["timelines"][0] = JValue.CreateNull();
        Reject("null point entry timeline rejected cleanly", () => CharacterTestTacticalAuthoringJson.Read(malformed.ToString()));
        malformed = (JObject)json.DeepClone(); malformed["timelines"][0]["eventBlocks"] = JValue.CreateNull();
        Check("optional null blocks canonicalize to an empty point timeline", CharacterTestTacticalAuthoringJson.Read(malformed.ToString()).Timelines[0].EventBlocks.Count == 0);
        var fingerprint = CharacterTestTacticalAuthoringJson.SemanticFingerprint(source);
        foreach (Action<TacticalPointMoveAuthoring> edit in new Action<TacticalPointMoveAuthoring>[] {
            c => c.RangeMillimeters++, c => c.ArrivalMillimeters++, c => c.MaximumFrames++, c => c.SpeedMilli-- }) {
            var next = Source(); edit(next.ExecutionInput);
            Check("author rule changes semantic fingerprint", fingerprint != CharacterTestTacticalAuthoringJson.SemanticFingerprint(next));
        }
        var d = Draft(); var compiled = Compile(d); var published = Publish(d, compiled);
        Check("unbound author program cannot start or restore without per-play input", !compiled.Program.TacticalSource.ValidateBinding(null, null, out var authorRequired)
            && authorRequired == "execution-input:PointRequired");
        Check("existing publisher creates an in-memory exact point card", published.Success);
        Check("metadata freezes authored cost and cooldown", published.Card.Metadata.Cost == 3 && published.Card.Metadata.CooldownSeconds == 2.25f);
        var frozenJson = published.Card.TacticalSource.ToJson().ToString();
        var mutable = published.Card.TacticalSource.CloneLegacy(); mutable.ExecutionInput.RangeMillimeters = 9000;
        Check("published rules do not change with clone edits", frozenJson == published.Card.TacticalSource.ToJson().ToString());
        Check("unbound published execution explicitly requires this play's point", !published.Card.TacticalSource.TryCompile(
            published.Card.StableCardId, "Point", 1, CharacterTestTacticalCardInputs.Unbound(1), out _, out var required) && required == "execution-input:PointRequired");
        Check("missing automatic provider cannot compile a fixed fallback", published.Card.TacticalSource.ExecutionInputKind == TacticalExecutionInputKind.WorldPoint);
        foreach (var m in new[] { Metadata(-1), Metadata(11), Metadata(3, -1), Metadata(3, float.NaN),
            Metadata(3, float.PositiveInfinity), Metadata(3, int.MaxValue), Metadata(4), Metadata(3, 1) })
            Check("invalid or stale publish metadata rejected", !Publish(d, compiled, m).Success);
        d.SetTacticalSource(mutable);
        Check("source changed after compile rejects publishing even at unchanged revision", !Publish(d, compiled).Success);
        var zero = Metadata(3, 0); var explicitZero = zero.Clone();
        long hash = CharacterTestRuntimeAiPlannerPublishedBehaviorCardPublisher.ComputeContentHash("fixture", 1, 1, 1, zero, 123);
        Check("default cooldown preserves historical content hash", hash ==
            CharacterTestRuntimeAiPlannerPublishedBehaviorCardPublisher.ComputeContentHashWithoutResponsibilities("fixture", 1, 1, 1, explicitZero, 123));
        Check("nonzero cooldown changes content hash", hash !=
            CharacterTestRuntimeAiPlannerPublishedBehaviorCardPublisher.ComputeContentHash("fixture", 1, 1, 1, Metadata(3, 0.5f), 123));
        var references = new CharacterTestRuntimeAiLabBehaviorCardReferenceCatalog(null,
            new CharacterActionConfigRegistry(Array.Empty<CharacterActionSetConfig>(), Array.Empty<CharacterActionConfig>()));
        var catalog = CharacterTestRuntimeAiPlannerPublishedBehaviorCardCatalog.ValidateAndCreate("point-fixture", 1, new[] { published.Card }, references);
        Check("existing catalog accepts point publish", catalog.Success);
        var catalogJson = CharacterTestRuntimeAiPlannerPublishedBehaviorCardCatalogJson.ToJson(catalog.Catalog);
        var loaded = CharacterTestRuntimeAiPlannerPublishedBehaviorCardCatalogJson.Load(catalogJson, references);
        Check("published source and cooldown round trip with exact hashes", loaded.Success && loaded.Catalog.Cards[0].ContentHash == published.Card.ContentHash
            && loaded.Catalog.Cards[0].SemanticHash == published.Card.SemanticHash && loaded.Catalog.Cards[0].Metadata.CooldownSeconds == 2.25f);
        var tampered = JObject.Parse(catalogJson);
        var cards = (JArray)tampered["cards"];
        cards[0]["metadata"]["cooldownSeconds"] = 0.25f;
        Check("tampered cooldown rejects published authentication", !CharacterTestRuntimeAiPlannerPublishedBehaviorCardCatalogJson.Load(tampered.ToString(), references).Success);
        tampered = JObject.Parse(catalogJson); tampered["cards"][0]["metadata"]["cooldownSeconds"] = "2.25";
        Check("published cooldown cannot be coerced from a string", !CharacterTestRuntimeAiPlannerPublishedBehaviorCardCatalogJson.Load(tampered.ToString(), references).Success);
        tampered = JObject.Parse(catalogJson); tampered["cards"][0]["tacticalSource"]["legacy"]["executionInput"]["rangeMillimeters"] = 6000;
        Check("tampered point rule rejects published authentication", !CharacterTestRuntimeAiPlannerPublishedBehaviorCardCatalogJson.Load(tampered.ToString(), references).Success);
        var projection = new CharacterTacticalContentCatalog(); projection.SetPublishedCards(loaded.Catalog.Cards);
        var p = projection.Cards.Single(); var reference = new CharacterTacticalCardReference { CardId = p.CardId, Revision = p.Revision,
            ContentHash = p.ContentHash, InstanceId = 41 };
        Check("catalog projects range, cost, cooldown and no action slots", p.PointMove.Input.RadiusMillimeters == 5000
            && p.MotionCost == 3 && p.CooldownSeconds == 2.25f && !p.HasActionSlot);
        var definition = TacticalDeployment.CreateCard(reference, p);
        Check("deployment constructs a point definition with exact reference", definition.PointMove != null && definition.PublishedCardId == reference.CardId
            && definition.PublishedRevision == reference.Revision && definition.PublishedContentHash == reference.ContentHash
            && definition.DeckEntryInstanceId == 41 && definition.CooldownSeconds == 2.25f && definition.BaseMomentumCost == 3);
        reference.ContentHash = "wrong"; Reject("deployment rejects a wrong hash", () => TacticalDeployment.CreateCard(reference, p));
        reference.ContentHash = p.ContentHash; reference.Revision++; Reject("deployment rejects a wrong revision", () => TacticalDeployment.CreateCard(reference, p));
        reference.Revision = p.Revision; reference.CardId = "other"; Reject("deployment rejects a wrong stable ID", () => TacticalDeployment.CreateCard(reference, p));
        var pointCompiler = typeof(CharacterTestTacticalExecutionAdapter).GetMethod("CreatePointSource", BindingFlags.Static | BindingFlags.NonPublic);
        var pointA = (CharacterTestTacticalAuthoringSource)pointCompiler.Invoke(null, new object[] { definition, new TacticalWorldPoint(1000, 0, 2000) });
        var pointB = (CharacterTestTacticalAuthoringSource)pointCompiler.Invoke(null, new object[] { definition, new TacticalWorldPoint(-1000, 0, 3000) });
        Check("separate plays bind their own fixed destinations", pointA.Timelines[0].Events[0].Operation.Movement.X == 1
            && pointB.Timelines[0].Events[0].Operation.Movement.X == -1 && published.Card.TacticalSource.ToJson().ToString() == frozenJson);
        Check("bound point source compiles through the production compiler", CharacterTestTacticalAuthoringCompiler.TryCompile(
            pointA, CharacterTestTacticalCardInputs.Unbound(1), out var a, out _));
        Check("second point compiles independently", CharacterTestTacticalAuthoringCompiler.TryCompile(
            pointB, CharacterTestTacticalCardInputs.Unbound(1), out var b, out _));
        Check("different selected points have different execution identities", a.Program.Execution.Identity != b.Program.Execution.Identity);
        var workspace = CharacterTestRuntimeAiLabDraftWorkspace.Import(CharacterTestArenaMatchConfig.CreateDefault());
        Check("existing import transaction accepts author-owned point source", workspace.TryImportTacticalSource(1, 0,
            workspace.CurrentRevision, Source(), out var draftId, out var issue));
        var draft = workspace.Cards[draftId]; var token = new CharacterTestTacticalEditToken(workspace, draft);
        string originalSource = draft.TacticalSourceJson; string stableId = draft.PublishedStableId;
        int originalRevision = draft.CurrentRevision, originalWorkspaceRevision = workspace.CurrentRevision;
        var nextRules = Contract(); nextRules.RangeMillimeters = 6500;
        Check("existing author transaction edits rules, cost and cooldown atomically", workspace.TryEditPointMove(token, nextRules, 4, 1.5f, out issue)
            && draft.Cost == 4 && draft.CooldownSeconds == 1.5f && draft.CurrentRevision == originalRevision + 1
            && workspace.CurrentRevision == originalWorkspaceRevision + 1 && draft.CloneTacticalSource().ExecutionInput.RangeMillimeters == 6500
            && draft.PublishedStableId == stableId);
        Check("stale author edit cannot mutate the replacement", !workspace.TryEditPointMove(token, Contract(), 1, 0, out issue)
            && draft.Cost == 4 && draft.CooldownSeconds == 1.5f);
        var badToken = new CharacterTestTacticalEditToken(workspace, draft); nextRules.ArrivalMillimeters = 6500;
        Check("invalid author rules leave source, cost and revision intact", !workspace.TryEditPointMove(badToken, nextRules, 2, 0, out issue)
            && draft.Cost == 4 && draft.CurrentRevision == originalRevision + 1);
        var persistence = typeof(CharacterTestRuntimeAiLabDraftPersistence);
        var write = persistence.GetMethod("WriteCard", BindingFlags.Static | BindingFlags.NonPublic);
        var read = persistence.GetMethod("ReadCard", BindingFlags.Static | BindingFlags.NonPublic);
        string draftJson = (string)write.Invoke(null, new object[] { draft });
        var reopened = (CharacterTestRuntimeAiLabBehaviorCardDraft)read.Invoke(null, new object[] { draftJson });
        Check("draft persistence round trips exact source, cost, cooldown and identities", reopened.TacticalSourceJson == draft.TacticalSourceJson
            && reopened.CooldownSeconds == 1.5f && reopened.Cost == 4 && reopened.CardDraftId == draft.CardDraftId && reopened.PublishedStableId == stableId);
        foreach (var field in new[] { "cost", "cooldownSeconds" }) {
            var invalidDraft = JObject.Parse(draftJson); invalidDraft[field] = "1";
            Reject("draft metadata cannot be coerced " + field, () => {
                try { read.Invoke(null, new object[] { invalidDraft.ToString() }); }
                catch (TargetInvocationException e) { throw e.InnerException; }
            });
        }
        var publishedCopy = CharacterTestRuntimeAiLabBehaviorCardDraft.CreatePublishedCopy(published.Card);
        Check("published-to-draft copy retains point contract and cooldown", publishedCopy.CloneTacticalSource().ExecutionInput.RangeMillimeters == 5000
            && publishedCopy.CooldownSeconds == 2.25f && publishedCopy.Cost == 3);
        Check("undo restores source and settlement metadata through the original owner", workspace.TryUndo(out issue)
            && workspace.Cards[draftId].TacticalSourceJson == originalSource && workspace.Cards[draftId].Cost == 0
            && workspace.Cards[draftId].CooldownSeconds == 0 && workspace.Cards[draftId].PublishedStableId == stableId);
        if (args.Length != 0) {
            string directory = Path.Combine(args[0], "Assets/StreamingAssets/MxFramework/CharacterTest/runtime_ai_planner/behavior_cards");
            int oldCards = 0;
            foreach (string file in Directory.GetFiles(directory, "*.json")) {
                var document = JObject.Parse(File.ReadAllText(file));
                foreach (JObject old in document["cards"]) {
                    if ((string)old["logicSchema"] != CharacterTestTacticalPublishedSource.Schema) continue;
                    var metadata = old["metadata"]; var oldSource = CharacterTestTacticalPublishedSource.Read((JObject)old["tacticalSource"]);
                    var oldMetadata = new CharacterTestRuntimeAiPlannerPublishedBehaviorCardMetadata((string)metadata["displayName"],
                        (string)metadata["iconKey"], (int)metadata["cost"], (string)metadata["shortDescription"], (string)metadata["useSummary"],
                        (string)metadata["riskSummary"], metadata["tags"]?.ToObject<string[]>(),
                        metadata["requiredCapabilities"]?.ToObject<CharacterTestRuntimeAiPlannerBehaviorCapability[]>(),
                        metadata["responsibilities"]?.ToObject<CharacterTestRuntimeAiPlannerBehaviorResponsibilityKind[]>());
                    Check("existing tactical source semantic hash unchanged " + old["stableCardId"] + " r" + old["revision"], oldSource.SemanticHash == (long)old["semanticHash"]);
                    Check("existing tactical metadata content hash unchanged " + old["stableCardId"] + " r" + old["revision"],
                        CharacterTestRuntimeAiPlannerPublishedBehaviorCardPublisher.ComputeContentHash((string)old["stableCardId"], (int)old["revision"],
                            (int)old["minimumRuntimeContractVersion"], (int)old["maximumRuntimeContractVersion"], oldMetadata, oldSource.SemanticHash) == (long)old["contentHash"]);
                    oldCards++;
                }
            }
            Check("authenticated existing production tactical cards without rewriting them", oldCards > 0);
        }
        var editor = Assembly.Load("MxFramework.Game.CharacterTest.Editor");
        var gate = editor.GetType("MxFramework.Game.CharacterTest.Editor.CharacterTestSelectedPointValidationFixture").GetMethod("IsSafeContext", BindingFlags.Static | BindingFlags.NonPublic);
        bool Safe(bool playing, int count, string path, bool manager, bool player, bool roots) => (bool)gate.Invoke(null, new object[] { playing, count, path, manager, player, roots });
        Check("fixture allows only empty unsaved Play scene", Safe(true, 1, "", false, false, false));
        Check("fixture rejects edit mode", !Safe(false, 1, "", false, false, false));
        Check("fixture rejects saved user scene", !Safe(true, 1, "Assets/User.unity", false, false, false));
        Check("fixture rejects multi-scene context", !Safe(true, 2, "", false, false, false));
        Check("fixture rejects an active user game", !Safe(true, 1, "", true, false, false));
        Check("fixture rejects opened player services", !Safe(true, 1, "", false, true, false));
        Check("fixture rejects user scene objects", !Safe(true, 1, "", false, false, true));
        Console.WriteLine("PASS TOTAL " + checks.ToString(CultureInfo.InvariantCulture));
    }
}
