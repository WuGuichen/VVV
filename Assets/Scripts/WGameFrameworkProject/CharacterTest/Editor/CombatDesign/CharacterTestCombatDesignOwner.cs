using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using MxFramework.CharacterAction;
using MxFramework.Editor.CharacterAction;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

namespace MxFramework.Game.CharacterTest.Editor
{
    /// <summary>First-phase structured owner adapter. Mutations and publication are restricted to isolated facing fixtures.</summary>
    /// <remarks>LowFreqAlloc: explicit authoring commands; no scene, player save, global ID allocation or Play mutation.</remarks>
    [InitializeOnLoad]
    internal static class CharacterTestCombatDesignOwner
    {
        private static double _nextPoll;
        private static bool _processing;
        private static string ProjectRoot => Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
        private static string Root => Path.Combine(ProjectRoot, "Temp/MxFramework/CombatDesign");
        private static readonly UTF8Encoding Utf8 = new UTF8Encoding(false);
        static CharacterTestCombatDesignOwner() { EditorApplication.update += Tick; }

        private static void Tick()
        {
            if (_processing || EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode
                || EditorApplication.timeSinceStartup < _nextPoll) return;
            _nextPoll = EditorApplication.timeSinceStartup + 1;
            string requests = Path.Combine(Root, "requests");
            if (!Directory.Exists(requests)) return;
            foreach (string file in Directory.GetFiles(requests, "*.json").OrderBy(p => Path.GetFileNameWithoutExtension(p), StringComparer.Ordinal))
            {
                string id = Path.GetFileNameWithoutExtension(file);
                if (!IsToken(id) || File.Exists(Path.Combine(Root, "responses", id + ".json"))) continue;
                _processing = true;
                try { Process(file); } // One bounded operation per Editor tick; compilation never starts Play or full test suites.
                finally { _processing = false; }
                break;
            }
        }

        private static void Process(string requestPath)
        {
            string id = Path.GetFileNameWithoutExtension(requestPath);
            string running = Path.Combine(Root, "running", id + ".json");
            JObject result;
            JObject command = null;
            try
            {
                SafePath(requestPath);
                if (File.Exists(running)) throw new InvalidOperationException("RECOVERY_REQUIRED: interrupted command retained; refresh/reconcile the isolated fixture before issuing a new plan.");
                command = Read(requestPath);
                if ((string)command["schema"] != "mx.combat-design.command.v1" || (string)command["operationId"] != id)
                    throw new InvalidDataException("COMMAND_SCHEMA_OR_ID_INVALID");
                string operation = (string)command["operation"];
                Atomic(running, command.ToString(Formatting.Indented));
                string handle = null;
                switch (operation)
                {
                    case "create-fixture": handle = CreateFixture(id); break;
                    case "commit": handle = Commit((JObject)command["plan"]); break;
                    case "validate": handle = VerifyRequest((JObject)command["request"]); ValidateFixture(handle); break;
                    case "publish": handle = VerifyRequest((JObject)command["request"]); PublishFixture(handle); break;
                    case "refresh": RefreshAll(); break;
                    default: throw new ArgumentException("UNKNOWN_OPERATION");
                }
                result = new JObject { ["operationId"] = id, ["status"] = "Succeeded", ["handle"] = handle,
                    ["operation"] = operation, ["completedUtc"] = DateTime.UtcNow.ToString("O"), ["playEvidence"] = "not-run" };
                Atomic(Path.Combine(Root, "responses", id + ".json"), result.ToString(Formatting.Indented));
                File.Delete(running);
            }
            catch (Exception error)
            {
                bool rejectedBeforeWrite = new[] { "SNAPSHOT_CONFLICT", "SOURCE_CHANGED", "READ_ONLY", "INVALID_FACING_PATCH", "PLAN_EXPIRED",
                    "RATIONALE_REQUIRED", "UNSUPPORTED_FACING", "AMBIGUOUS_FACING", "COMMAND_SCHEMA_OR_ID_INVALID", "UNKNOWN_OPERATION" }
                    .Any(code => error.Message.StartsWith(code, StringComparison.Ordinal));
                var failedPlan = command?["plan"] as JObject;
                var failedRequest = command?["request"] as JObject;
                string failedHandle = (string)failedPlan?["handle"] ?? (string)failedRequest?["handle"];
                if ((string)command?["operation"] == "create-fixture") failedHandle = Hash("isolated-fixture/" + id);
                result = new JObject { ["operationId"] = id, ["status"] = !rejectedBeforeWrite && File.Exists(running) ? "RecoveryRequired" : "Rejected",
                    ["handle"] = failedHandle,
                    ["message"] = error.Message, ["errorType"] = error.GetType().FullName, ["diagnostic"] = error.ToString(),
                    ["completedUtc"] = DateTime.UtcNow.ToString("O"),
                    ["recovery"] = "refresh rereads owners and retains their recovery journals; requery/replan. A repeated key will return this same outcome, never replay the write.",
                    ["playEvidence"] = "not-run" };
                Atomic(Path.Combine(Root, "responses", id + ".json"), result.ToString(Formatting.Indented));
                if (rejectedBeforeWrite && File.Exists(running)) File.Delete(running);
            }
        }

        internal static string CreateFixture(string operationId)
        {
            RequireToken(operationId);
            string handle = Hash("isolated-fixture/" + operationId);
            string fixture = Fixture(handle);
            string manifestPath = Path.Combine(fixture, "fixture.json");
            if (File.Exists(manifestPath)) { RefreshFixture(handle); return handle; }
            Directory.CreateDirectory(fixture);
            // Identity selection is automatic and scoped. Never allocate global IDs just to run an acceptance example.
            string identityPath = Path.Combine(ProjectRoot, CharacterContentIdentityAuthoringCatalog.DefaultPath);
            var domain = ((JArray)Read(identityPath)["domains"]).OfType<JObject>().Single(d => (int)d["domainId"] == 417);
            var keys = ((JArray)domain["entries"]).OfType<JObject>()
                .Where(e => (string)e["status"] == "active" && ((string)e["sourceKey"]).StartsWith("behavior-card/", StringComparison.Ordinal)
                    && CharacterTestAuthoredContentIds.IsActive(417, (int)e["id"]))
                .OrderBy(e => (int)e["id"]).Take(3).Select(e => ((string)e["sourceKey"]).Substring("behavior-card/".Length)).ToArray();
            if (keys.Length != 3) throw new InvalidOperationException("FIXTURE_IDENTITY_DIRECTORY_UNAVAILABLE");
            var workspace = new CharacterTestRuntimeAiLabDraftWorkspace(null, "战斗设计第一期隔离样例");
            var definition = CharacterTestTacticalAuthoringTemplates.CreateFacingTargetDefinition();
            Need(workspace.TryAddReusableTacticalDefinition(definition, workspace.CurrentRevision, out var issue), issue);
            for (int i = 0; i < keys.Length; i++)
            {
                Need(workspace.TryCreateStandaloneTacticalCard(out string cardId, out issue), issue);
                var card = workspace.Cards[cardId];
                card.PublishedStableId = keys[i];
                card.IconKey = "ui.behavior-card.custom";
                card.DisplayName = i < 2 ? "隔离面向卡 " + (i + 1) : "隔离无依赖对照卡";
                if (i < 2)
                    Need(workspace.TryEditComposition(new CharacterTestTacticalEditToken(workspace, card), source => {
                        source.Root.Children.Add(new CharacterTestTacticalDslNode { NodeId = "fixture-facing-" + i, Kind = "Reference", DefinitionId = definition.DefinitionId });
                    }, "自动装配隔离面向卡", out issue), issue);
            }
            Save(fixture, workspace);
            Atomic(manifestPath, new JObject { ["schema"] = "mx.combat-design.fixture.v1", ["workspaceId"] = workspace.WorkspaceId,
                ["handle"] = handle,
                ["identityScope"] = "borrowed-existing-directory-identities/isolated-catalog-only", ["rationale"] = "隔离验证共享机制到战术卡的真实 owner 链路；不代表主项目设计取舍。" }.ToString(Formatting.Indented));
            InitializeCatalog(fixture);
            ValidateFixture(handle);
            return handle;
        }

        internal static string Commit(JObject plan)
        {
            string handle = VerifyRequest(plan);
            float threshold = (float)plan["thresholdDegrees"];
            int frames = (int)plan["maximumFrames"];
            if (float.IsNaN(threshold) || float.IsInfinity(threshold) || threshold <= 0 || threshold > 180 || frames < 1 || frames > 18000)
                throw new ArgumentException("INVALID_FACING_PATCH");
            string rationale = (string)plan["rationale"];
            if (string.IsNullOrWhiteSpace(rationale) || rationale.Length > 4000) throw new ArgumentException("RATIONALE_REQUIRED");
            if (DateTime.Parse((string)plan["expiresUtc"], null, System.Globalization.DateTimeStyles.RoundtripKind) < DateTime.UtcNow)
                throw new InvalidOperationException("PLAN_EXPIRED");
            string fixture = Fixture(handle);
            var workspace = Load(fixture);
            var definition = workspace.CloneTacticalDefinitions().Single(d => d.DefinitionId == CharacterTestTacticalAuthoringTemplates.FacingTargetDefinitionId);
            // The write adapter recognizes the semantic contract, not a name; preserve all unrelated source fields.
            var source = definition.Source;
            if (source.Timelines.Count != 1 || source.Timelines[0].EventBlocks.Count != 1) throw new InvalidOperationException("UNSUPPORTED_FACING_STRUCTURE");
            var timeline = source.Timelines[0]; var block = timeline.EventBlocks[0];
            if (!CharacterTestTacticalFacingTargetHint.TryParse("fixture", block, timeline.Events.ToArray(), out _))
                throw new InvalidOperationException("UNSUPPORTED_FACING_CONTRACT");
            var gate = timeline.Events.Single(e => e.EventId == block.CompletionEventId);
            var angles = Conditions(gate.ConditionRoot).Where(n => n.Predicate?.Fact == CharacterTestRuntimeAiLabCardConditionFact.TargetFacingAngle
                && n.Predicate.Operator == CharacterTestRuntimeAiLabCardConditionOperator.GreaterOrEqual).ToArray();
            if (angles.Length != 1) throw new InvalidOperationException("AMBIGUOUS_FACING_THRESHOLD");
            angles[0].Predicate.Value = threshold;
            block.Timing.DurationFrames = frames;
            // Compile the candidate with every actual consumer before persisting the owner transaction.
            // All consumer compositions are compiled using a projected definition list; do not mutate the live workspace to validate.
            foreach (string cardId in workspace.TacticalDefinitionImpact(definition.DefinitionId))
            {
                var document = workspace.ResolveComposition(workspace.Cards[cardId]);
                document.Definitions.Single(d => d.DefinitionId == definition.DefinitionId).Source = source;
                Need(CharacterTestTacticalDslCompiler.TryCompile(document, Inputs(), out _, out string error), error);
            }
            Need(workspace.TryReplaceTacticalDefinition(workspace.CaptureTacticalDefinitionEdit(definition.DefinitionId), source, true, out string issue), issue);
            Save(fixture, workspace);
            var manifest = Read(Path.Combine(fixture, "fixture.json")); manifest["rationale"] = rationale;
            Atomic(Path.Combine(fixture, "fixture.json"), manifest.ToString(Formatting.Indented));
            RefreshFixture(handle); // Previous evidence is retained but automatically marked stale by its fingerprint.
            return handle;
        }

        internal static void ValidateFixture(string handle)
        {
            string fixture = Fixture(handle);
            var workspace = Load(fixture);
            var previews = new JArray();
            foreach (var card in workspace.Cards.Values.OrderBy(c => c.CardDraftId, StringComparer.Ordinal))
            {
                if (!workspace.TacticalDefinitionImpact(CharacterTestTacticalAuthoringTemplates.FacingTargetDefinitionId).Contains(card.CardDraftId)) continue;
                var source = workspace.ResolveComposition(card);
                Need(CharacterTestTacticalDslCompiler.TryCompile(source, Inputs(), out var compiled, out string issue), issue);
                var description = CharacterTestTacticalDslDescription.Generate(source, Inputs(), compiled, true);
                var face = CharacterTestTacticalDslDescription.Generate(source, Inputs(), compiled, true, cardFace: true);
                Need(description.Executable && face.Executable, description.CompilationIssue ?? face.CompilationIssue);
                previews.Add(new JObject { ["cardId"] = card.CardDraftId, ["description"] = description.Text, ["details"] = description.Details,
                    ["cardFace"] = face.Text, ["warnings"] = JArray.FromObject(description.Warnings), ["dependencyIdentity"] = compiled.DependencyIdentity });
            }
            Atomic(Path.Combine(fixture, "evidence.json"), new JObject { ["sourceFingerprint"] = SourceFingerprint(workspace),
                ["level"] = "owner-compile-and-generated-text", ["play"] = "not-run", ["visualScreenshot"] = "not-run",
                ["compiledUtc"] = DateTime.UtcNow.ToString("O"), ["previews"] = previews,
                ["compilerMvid"] = typeof(CharacterTestTacticalDslCompiler).Assembly.ManifestModule.ModuleVersionId.ToString() }.ToString(Formatting.Indented));
            RefreshFixture(handle);
        }

        internal static void PublishFixture(string handle)
        {
            string fixture = Fixture(handle);
            ValidateFixture(handle);
            var workspace = Load(fixture);
            var references = References();
            var session = new CharacterTestRuntimeAiPlannerProjectCatalogAuthoringSession(LoadCatalog(fixture),
                () => LoadCatalog(fixture), CharacterTestRuntimeAiPlannerProjectCatalogEditorBridge.WriteCombatDesignFixture);
            session.ResolveTacticalSource = draft => CharacterTestTacticalPublishedSource.CaptureDraft(workspace, draft);
            session.CompileTacticalForPublish = draft => {
                var source = workspace.ResolveComposition(draft);
                Need(CharacterTestTacticalDslCompiler.TryCompile(source, Inputs(), out var compiled, out string issue), issue);
                var frozen = CharacterTestTacticalFrozenSource.Capture(workspace, draft, source, Inputs(), compiled);
                return new CharacterTestRuntimeAiLabBehaviorCardLogicCompileResult(compiled.Program.WithTacticalSource(frozen), Array.Empty<CharacterTestRuntimeAiLabCardDiagnostic>());
            };
            var previousAllocator = CharacterTestRuntimeAiPlannerPublishedBehaviorCardNumericIdentity.AuthoringAllocator;
            try
            {
                CharacterTestRuntimeAiPlannerPublishedBehaviorCardNumericIdentity.AuthoringAllocator = key =>
                    CharacterContentIdentityAuthoringCatalog.GetExisting(417, key); // Lookup only. Zero global identity writes.
                using (CharacterTestRuntimeAiPlannerProjectCatalogEditorBridge.UseCombatDesignFixtureCatalog(fixture, () => LoadCatalog(fixture)))
                {
                    CharacterTestRuntimeAiPlannerProjectCatalogEditorBridge.RecoverCombatDesignFixture();
                    foreach (string cardId in workspace.TacticalDefinitionImpact(CharacterTestTacticalAuthoringTemplates.FacingTargetDefinitionId))
                    {
                        var draft = workspace.Cards[cardId];
                        // Reconcile a committed catalog write whose draft checkpoint was interrupted.
                        var latest = session.State.CardCatalog.Cards.Where(c => c.StableCardId == draft.PublishedStableId).OrderByDescending(c => c.Revision).FirstOrDefault();
                        if (latest?.TacticalSource != null && latest.TacticalSource.ToJson().ToString(Formatting.None)
                            == CharacterTestTacticalPublishedSource.CaptureDraft(workspace, draft).ToJson().ToString(Formatting.None))
                        {
                            Need(workspace.TryAdoptStandaloneTacticalPublishedRevision(cardId, latest, out string adoptIssue), adoptIssue);
                            Save(fixture, workspace); continue;
                        }
                        if (draft.HasPublishedSource && session.TryHasUnpublishedCardChanges(draft, references, out bool changed, out _) && !changed) continue;
                        var published = session.TryPublishCard(draft, references);
                        Need(published.Success, string.Join("; ", published.Diagnostics.Select(d => d.Message)));
                        Need(workspace.TryAdoptStandaloneTacticalPublishedRevision(cardId, published.PublishedCard, out string issue), issue);
                        Save(fixture, workspace); // Each existing catalog transaction is durable; aggregate progress is explicit, not an invented all-or-nothing claim.
                    }
                }
            }
            finally { CharacterTestRuntimeAiPlannerPublishedBehaviorCardNumericIdentity.AuthoringAllocator = previousAllocator; }
            var reloaded = LoadCatalog(fixture); Need(reloaded.Success, reloaded.Diagnostic);
            foreach (var draft in workspace.Cards.Values.Where(c => c.HasPublishedSource))
            {
                Need(reloaded.CardCatalog.TryGetExact(draft.PublishedStableId, draft.SourcePublishedRevision, out var card), "RELOAD_IDENTITY_MISMATCH");
                Need(card.ContentHash == draft.SourcePublishedContentHash && card.SemanticHash == draft.SourcePublishedSemanticHash, "RELOAD_HASH_MISMATCH");
                Need(card.TacticalSource.TryCompile(card.StableCardId, card.Metadata.DisplayName, 1, Inputs(), out _, out string issue), issue);
            }
            ValidateFixture(handle);
            var evidencePath = Path.Combine(fixture, "evidence.json"); var evidence = Read(evidencePath);
            evidence["publication"] = "isolated-catalog-committed-reloaded-recompiled";
            evidence["catalogHash"] = reloaded.CardCatalog.CatalogHash;
            Atomic(evidencePath, evidence.ToString(Formatting.Indented)); RefreshFixture(handle);
        }

        private static void RefreshAll()
        {
            string fixtures = Path.Combine(Root, "fixtures");
            if (Directory.Exists(fixtures)) foreach (var dir in Directory.GetDirectories(fixtures))
            {
                string manifestPath = Path.Combine(dir, "fixture.json");
                if (!File.Exists(manifestPath)) continue;
                string handle = (string)Read(manifestPath)["handle"]; if (!IsToken(handle)) continue;
                string fixture = Fixture(handle);
                if (!File.Exists(Path.Combine(fixture, "fixture.json"))) continue;
                using (CharacterTestRuntimeAiPlannerProjectCatalogEditorBridge.UseCombatDesignFixtureCatalog(fixture, () => LoadCatalog(fixture)))
                    CharacterTestRuntimeAiPlannerProjectCatalogEditorBridge.RecoverCombatDesignFixture();
                RefreshFixture(handle);
            }
            string drafts = Path.Combine(ProjectRoot, "Authoring/RuntimeAiLab/Drafts");
            if (!Directory.Exists(drafts)) return;
            var reader = new CharacterTestRuntimeAiLabDraftPersistence(drafts, Path.Combine(Root, "ReadOnlyRecovery"));
            foreach (var dir in Directory.GetDirectories(drafts))
                if (File.Exists(Path.Combine(dir, "workspace.json")) && reader.TryLoad(Path.GetFileName(dir), out var workspace, out _))
                    Snapshot(Hash("project-read-only/" + workspace.WorkspaceId), workspace, dir, false, null, null);
        }

        private static void RefreshFixture(string handle)
        {
            string fixture = Fixture(handle); var manifest = Read(Path.Combine(fixture, "fixture.json"));
            var workspace = Load(fixture);
            string evidencePath = Path.Combine(fixture, "evidence.json");
            Snapshot(handle, workspace, Path.Combine(fixture, "Drafts", workspace.WorkspaceId), true,
                (string)manifest["rationale"], File.Exists(evidencePath) ? Read(evidencePath) : null);
        }

        private static void Snapshot(string handle, CharacterTestRuntimeAiLabDraftWorkspace workspace, string workspaceDir,
            bool writable, string rationale, JObject evidence)
        {
            string id = CharacterTestTacticalAuthoringTemplates.FacingTargetDefinitionId;
            var facing = workspace.CloneTacticalDefinitions().FirstOrDefault(d => d.DefinitionId == id);
            var impacted = workspace.TacticalDefinitionImpact(id);
            var cards = new JArray(workspace.Cards.Values.OrderBy(c => c.CardDraftId, StringComparer.Ordinal).Select(c => new JObject {
                ["id"] = c.CardDraftId, ["stableId"] = c.PublishedStableId, ["name"] = c.DisplayName, ["revision"] = c.CurrentRevision,
                ["dependsOnFacing"] = impacted.Contains(c.CardDraftId), ["publishedRevision"] = c.SourcePublishedRevision,
                ["composition"] = c.HasCompositionSource ? JObject.Parse(c.CompositionSourceJson) : null }));
            var readSet = new JArray();
            foreach (string path in Directory.GetFiles(workspaceDir, "*.json", SearchOption.AllDirectories).Concat(CodeReadSet()).Distinct().OrderBy(p => p, StringComparer.Ordinal))
                readSet.Add(new JObject { ["path"] = Relative(path), ["hash"] = Hash(File.ReadAllBytes(path)) });
            string fingerprint = SourceFingerprint(workspace);
            if (evidence != null) evidence["current"] = (string)evidence["sourceFingerprint"] == fingerprint;
            Atomic(Path.Combine(Root, "snapshots", handle + ".json"), new JObject {
                ["schema"] = "mx.combat-design.snapshot.v1", ["handle"] = handle, ["writable"] = writable,
                ["workspaceId"] = workspace.WorkspaceId, ["workspaceRevision"] = workspace.CurrentRevision, ["name"] = workspace.DisplayName,
                ["scope"] = writable ? "isolated-fixture" : "project-read-only", ["sourceFingerprint"] = fingerprint,
                ["readSet"] = readSet, ["cards"] = cards, ["evidence"] = evidence ?? new JObject {
                    ["current"] = false, ["level"] = "not-recorded", ["play"] = "not-run", ["visualScreenshot"] = "not-run" },
                ["facts"] = facing == null ? null : JObject.Parse(CharacterTestTacticalCompositionJson.WriteShared(facing)),
                ["contractFacts"] = FacingFacts(facing?.Source),
                ["rationale"] = rationale, ["rationaleOrigin"] = rationale == null ? "not-authored" : "agent-authored-design-choice",
                ["lifecycle"] = new JObject { ["design"] = rationale == null ? "unrecorded" : "documented", ["code"] = "registered-owner-contract",
                    ["configuration"] = facing == null ? "unconnected" : "connected", ["enabled"] = writable ? "fixture-only" : "not-inferred-from-draft",
                    ["validated"] = evidence == null ? "not-recorded" : (string)evidence["sourceFingerprint"] == fingerprint ? "owner-compile-text" : "stale", ["play"] = "not-run" },
                ["coverage"] = "shared-facing → composition/templates → draft/published-card; owned-instance/deck/actor/scene not part of this fixture",
                ["generatedUtc"] = DateTime.UtcNow.ToString("O") }.ToString(Formatting.Indented));
        }

        private static string VerifyRequest(JObject request)
        {
            string handle = (string)request["handle"]; RequireToken(handle);
            string path = Path.Combine(Root, "snapshots", handle + ".json"); SafePath(path);
            string raw = File.ReadAllText(path);
            if (Hash(raw) != (string)request["snapshotHash"]) throw new InvalidOperationException("SNAPSHOT_CONFLICT");
            var snapshot = JObject.Parse(raw);
            if ((bool?)snapshot["writable"] != true) throw new InvalidOperationException("READ_ONLY");
            foreach (JObject entry in (JArray)snapshot["readSet"])
            {
                string source = Path.GetFullPath(Path.Combine(ProjectRoot, (string)entry["path"])); SafePath(source);
                if (!source.StartsWith(ProjectRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
                    || !File.Exists(source) || Hash(File.ReadAllBytes(source)) != (string)entry["hash"])
                    throw new InvalidOperationException("SOURCE_CHANGED");
            }
            Fixture(handle); return handle;
        }

        private static void InitializeCatalog(string fixture)
        {
            var catalog = CharacterTestRuntimeAiPlannerPublishedBehaviorCardCatalog.ValidateAndCreate("combat-design-fixture", 1,
                Array.Empty<CharacterTestRuntimeAiPlannerPublishedBehaviorCard>(), References());
            Need(catalog.Success, string.Join(";", catalog.Diagnostics.Select(d => d.Message)));
            string directory = CatalogDirectory(fixture); Directory.CreateDirectory(directory);
            var payload = CharacterTestRuntimeAiPlannerProjectCardStorage.CreatePayload(catalog.Catalog);
            Atomic(Path.Combine(directory, CharacterTestRuntimeAiPlannerProjectCatalogLoader.CardCatalogFileName), payload.IndexJson);
            var groups = new CharacterTestRuntimeAiPlannerBehaviorGroupCatalog("combat-design-fixture-groups", 1, Array.Empty<CharacterTestRuntimeAiPlannerBehaviorGroupRevision>());
            Atomic(Path.Combine(directory, CharacterTestRuntimeAiPlannerProjectCatalogLoader.GroupCatalogFileName), CharacterTestRuntimeAiPlannerBehaviorGroupCatalogJson.ToJson(groups));
        }
        private static CharacterTestRuntimeAiPlannerProjectCatalogState LoadCatalog(string fixture)
        {
            string directory = CatalogDirectory(fixture);
            string cardsPath = Path.Combine(directory, CharacterTestRuntimeAiPlannerProjectCatalogLoader.CardCatalogFileName);
            string groupsPath = Path.Combine(directory, CharacterTestRuntimeAiPlannerProjectCatalogLoader.GroupCatalogFileName);
            var cards = CharacterTestRuntimeAiPlannerProjectCardStorage.Load(File.ReadAllText(cardsPath), directory, References());
            Need(cards.Success, string.Join(";", cards.Diagnostics.Select(d => d.Message)));
            var groups = CharacterTestRuntimeAiPlannerBehaviorGroupCatalogJson.Load(File.ReadAllText(groupsPath), cards.Catalog, CharacterTestRuntimeAiPlannerBehaviorDefinitionCatalog.CreateBuiltIn());
            Need(groups.Success, groups.Diagnostic);
            return new CharacterTestRuntimeAiPlannerProjectCatalogState(cards.Catalog, groups.Catalog, cardsPath, groupsPath,
                CharacterTestRuntimeAiPlannerProjectCatalogLoader.ComputeFileHash(cardsPath), CharacterTestRuntimeAiPlannerProjectCatalogLoader.ComputeFileHash(groupsPath), string.Empty);
        }
        private static string CatalogDirectory(string fixture) => Path.Combine(fixture, "StreamingAssets", CharacterTestRuntimeAiPlannerProjectCatalogLoader.RelativeDirectory);
        private static CharacterTestTacticalCardInputs Inputs() => CharacterTestTacticalCardInputs.Unbound(1);
        private static CharacterTestRuntimeAiLabBehaviorCardReferenceCatalog References() => new CharacterTestRuntimeAiLabBehaviorCardReferenceCatalog(null,
            new CharacterActionConfigRegistry(Array.Empty<CharacterActionSetConfig>(), Array.Empty<CharacterActionConfig>()));
        private static CharacterTestRuntimeAiLabDraftWorkspace Load(string fixture)
        {
            var manifest = Read(Path.Combine(fixture, "fixture.json"));
            var reader = new CharacterTestRuntimeAiLabDraftPersistence(Path.Combine(fixture, "Drafts"), Path.Combine(fixture, "DraftRecovery"));
            Need(reader.TryLoad((string)manifest["workspaceId"], out var workspace, out string issue), issue); return workspace;
        }
        private static void Save(string fixture, CharacterTestRuntimeAiLabDraftWorkspace workspace)
        {
            var persistence = new CharacterTestRuntimeAiLabDraftPersistence(Path.Combine(fixture, "Drafts"), Path.Combine(fixture, "DraftRecovery"));
            persistence.AdoptWorkspace(workspace); persistence.MarkDirty(); Need(persistence.Flush(), persistence.LastStatus);
        }
        private static string SourceFingerprint(CharacterTestRuntimeAiLabDraftWorkspace workspace)
            => Hash(string.Join("\n", workspace.CloneTacticalDefinitions().Select(CharacterTestTacticalCompositionJson.WriteShared)) + "\n"
                + string.Join("\n", workspace.Cards.Values.OrderBy(c => c.CardDraftId, StringComparer.Ordinal).Select(c =>
                    new JObject { ["id"] = c.CardDraftId, ["source"] = c.CompositionSourceJson, ["name"] = c.DisplayName,
                        ["icon"] = c.IconKey, ["cost"] = c.Cost, ["shortDescription"] = c.ShortDescription,
                        ["useSummary"] = c.UseSummary, ["riskSummary"] = c.RiskSummary, ["tags"] = c.TagsText }.ToString(Formatting.None)))
                + "\n" + string.Join("\n", CodeReadSet().OrderBy(p => p, StringComparer.Ordinal).Select(p => Relative(p) + "|" + Hash(File.ReadAllBytes(p)))));
        private static IEnumerable<string> CodeReadSet()
        {
            string project = Path.Combine(ProjectRoot, "Assets/Scripts/WGameFrameworkProject/CharacterTest");
            foreach (string dir in new[] { "RuntimeAiPlannerDev", "Runtime/Systems/RuntimeAiPlanner", "Editor/CombatDesign", "Editor/RuntimeAiPlanner" })
                foreach (string file in Directory.GetFiles(Path.Combine(project, dir), "*.cs", SearchOption.AllDirectories)) yield return file;
            yield return Path.Combine(ProjectRoot, CharacterContentIdentityAuthoringCatalog.DefaultPath);
            yield return Path.Combine(project, "Runtime/World/CharacterTestAuthoredContentIds.cs");
        }
        private static IEnumerable<CharacterTestRuntimeAiLabBehaviorRecipeConditionNode> Conditions(CharacterTestRuntimeAiLabBehaviorRecipeConditionNode node)
        {
            if (node == null) yield break; yield return node;
            foreach (var child in node.Children) foreach (var nested in Conditions(child)) yield return nested;
        }
        private static JObject FacingFacts(CharacterTestTacticalAuthoringSource source)
        {
            if (source?.Timelines.Count != 1 || source.Timelines[0].EventBlocks.Count != 1) return null;
            var timeline = source.Timelines[0]; var block = timeline.EventBlocks[0];
            if (!CharacterTestTacticalFacingTargetHint.TryParse("snapshot", block, timeline.Events.ToArray(), out _)) return null;
            var gate = timeline.Events.Single(e => e.EventId == block.CompletionEventId);
            if (!CharacterTestTacticalAuthoringCompiler.TryFacingThreshold(gate.ConditionRoot, out float degrees)) return null;
            return new JObject { ["contract"] = "recognized-facing-target/v1", ["comparison"] = "strict-less-than",
                ["thresholdDegrees"] = degrees, ["maximumFrames"] = block.Timing.DurationFrames,
                ["requireLivingTarget"] = block.RequireLivingTarget, ["requireValidDirection"] = true,
                ["blockId"] = block.BlockId, ["gateEventId"] = gate.EventId };
        }
        private static string Fixture(string handle)
        {
            RequireToken(handle);
            // Unity's Windows IO still encounters legacy path limits. Public handles remain full hashes.
            string path = Path.Combine(Root, "fixtures", handle.Substring(0, 12)); SafePath(path);
            string manifest = Path.Combine(path, "fixture.json");
            if (File.Exists(manifest) && (string)Read(manifest)["handle"] != handle)
                throw new InvalidOperationException("FIXTURE_DIRECTORY_IDENTITY_CONFLICT");
            return path;
        }
        private static string Relative(string path) => Path.GetFullPath(path).Substring(ProjectRoot.Length + 1).Replace('\\', '/');
        private static JObject Read(string path) { SafePath(path); return JObject.Parse(File.ReadAllText(path)); }
        private static bool IsToken(string token) => token != null && token.Length == 64 && token.All(c => c >= '0' && c <= '9' || c >= 'a' && c <= 'f');
        private static void RequireToken(string token) { if (!IsToken(token)) throw new ArgumentException("INVALID_HANDLE"); }
        private static void Need(bool success, string issue) { if (!success) throw new InvalidOperationException(issue); }
        private static string Hash(string text) => Hash(Utf8.GetBytes(text));
        private static string Hash(byte[] bytes) { using var sha = SHA256.Create(); return BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", "").ToLowerInvariant(); }
        private static void SafePath(string path)
        {
            for (var info = new DirectoryInfo(Path.GetFullPath(path)); info != null; info = info.Parent)
                if ((File.Exists(info.FullName) || Directory.Exists(info.FullName)) && (File.GetAttributes(info.FullName) & FileAttributes.ReparsePoint) != 0)
                    throw new IOException("UNSAFE_REPARSE_PATH");
        }
        private static void Atomic(string path, string text)
        {
            SafePath(path); Directory.CreateDirectory(Path.GetDirectoryName(path));
            string temp = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                using (var stream = new FileStream(temp, FileMode.CreateNew, FileAccess.Write, FileShare.None)) { byte[] bytes = Utf8.GetBytes(text); stream.Write(bytes, 0, bytes.Length); stream.Flush(true); }
                // Windows readers can briefly prevent replacement. Retry the same intact temporary file;
                // never delete the destination or replay a higher-level owner transaction.
                for (int attempt = 0; ; attempt++)
                {
                    try
                    {
                        if (File.Exists(path)) File.Replace(temp, path, null); else File.Move(temp, path);
                        break;
                    }
                    catch (IOException) when (attempt < 3 && File.Exists(temp) && File.Exists(path))
                    { System.Threading.Thread.Sleep(20 << attempt); } // Bounded authoring-only backoff, at most 140ms.
                }
            }
            finally { if (File.Exists(temp)) File.Delete(temp); }
        }
    }
}
