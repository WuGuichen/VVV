using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace MxFramework.Authoring
{
    /// <summary>Read-only, source-bound management view of one recognized tactical intent. Never compiles or writes Unity content.</summary>
    /// <remarks>LowFreqAlloc: explicit authoring queries. Source hashes are observations, not runtime validation receipts.</remarks>
    public sealed class CombatDesignIntentProjection
    {
        private const string DefinitionId = "tactical.shared.facing-forward-step";
        private const string Dev = "Assets/Scripts/WGameFrameworkProject/CharacterTest/RuntimeAiPlannerDev/";
        // Reviewed code contracts, not a second parameter ledger. Any code drift disables semantic claims.
        private static readonly Dictionary<string, string> Contracts = new Dictionary<string, string> {
            [Dev + "CharacterTestBehaviorExecutionEvaluator.Blocks.cs"] = "dc44a52b0a50422a08e640bfcea85f246ef73876cb703c1a1d581562d26e971e",
            [Dev + "CharacterTestBehaviorExecutionEvaluator.cs"] = "bff8331eba8c9c295197f0986647ea464aea8bbd9782441e488895c080eba048",
            [Dev + "CharacterTestTacticalCardInputs.cs"] = "62779150eb0b868a82b656c781a21d536cb9b2223f9a0d3405ddb6bd1bf33d18",
            [Dev + "CharacterTestRuntimeAiLabBehaviorCardPayload.cs"] = "024a2576db1afef829813f950f5e0265bb0218b2b980e69464cd78cebf368fd5",
            [Dev + "CharacterTestRuntimeAiLabBehaviorRecipeV6.cs"] = "ee5ac615364db88ef0b91c9fa42246ba260069849ecc56b6a19965a29aaf15b8",
            [Dev + "CharacterTestTacticalAuthoringCompiler.cs"] = "6ee8443e440417bb32bd62fe786050cefa700745acc9286fcae57101f280c581",
            [Dev + "CharacterTestTacticalFacingStepHint.cs"] = "a9cc4c511cf1901d64248e964eb34e254fdd554db2ebf1017bcaf2b0e9a980a4",
            ["Assets/Scripts/WGameFrameworkProject/CharacterTest/Runtime/Systems/Character/CharacterTestCombatEvadeRuntime.cs"] = "63433c490c5269e9bf9bb2f5488aef0663dcd330cc2cd9ba7e3a52f35d3c2388"
        };
        private readonly string _root;
        private readonly Dictionary<string, string> _reads = new Dictionary<string, string>(StringComparer.Ordinal);
        private readonly Dictionary<string, string> _texts = new Dictionary<string, string>(StringComparer.Ordinal);
        private readonly List<string> _diagnostics = new List<string>();

        public CombatDesignIntentProjection(string projectRoot) { _root = Path.GetFullPath(projectRoot ?? throw new ArgumentNullException(nameof(projectRoot))); }

        /// <summary>Rebuilds the intent, reverse references and evidence boundaries from authoritative disk sources.</summary>
        public object Query()
        {
            _reads.Clear(); _texts.Clear(); _diagnostics.Clear();
            bool codeCurrent = true;
            foreach (var contract in Contracts)
            {
                try { ReadText(contract.Key); if (_reads[contract.Key] != contract.Value) { codeCurrent = false; _diagnostics.Add("CODE_CONTRACT_CHANGED: " + contract.Key); } }
                catch (IOException error) { codeCurrent = false; _diagnostics.Add(error.Message); }
            }
            var workspaces = new List<object>();
            string draftRoot = SafePath("Authoring/RuntimeAiLab/Drafts");
            if (Directory.Exists(draftRoot))
                foreach (string directory in Directory.GetDirectories(draftRoot).OrderBy(value => value, StringComparer.Ordinal))
                {
                    string relative = Relative(Path.Combine(directory, "workspace.json"));
                    if (!File.Exists(SafePath(relative))) continue;
                    try
                    {
                        var workspace = ReadJson(relative);
                        if (Text(workspace, "schema") != "mx.character-test.runtime-ai-lab.workspace.v4") { _diagnostics.Add("UNSUPPORTED_WORKSPACE_SCHEMA: " + relative); continue; }
                        var definitions = ReadIndex(workspace, "tacticalDefinitions", directory);
                        var intent = definitions.FirstOrDefault(item => Text(item.Data, "definitionId") == DefinitionId);
                        if (intent == null) continue;
                        var cards = ReadIndex(workspace, "cards", directory);
                        workspaces.Add(ProjectWorkspace(workspace, relative, intent, definitions, cards, codeCurrent));
                    }
                    catch (Exception error) when (error is IOException || error is JsonException || error is InvalidDataException || error is InvalidOperationException)
                    { _diagnostics.Add("WORKSPACE_NOT_PROJECTED: " + relative + " — " + error.Message); }
                }
            var readSet = _reads.OrderBy(pair => pair.Key, StringComparer.Ordinal).Select(pair => new { path = pair.Key, hash = pair.Value }).ToArray();
            bool stable = readSet.All(entry => File.Exists(SafePath(entry.path)) && Hash(File.ReadAllBytes(SafePath(entry.path))) == entry.hash);
            if (!stable) _diagnostics.Add("SOURCE_CHANGED_DURING_QUERY: retry this read; do not treat mixed observations as current evidence.");
            return new { schema = "mx.combat-design.intent-projection.v1", generatedUtc = DateTime.UtcNow.ToString("O"), definitionId = DefinitionId,
                sourceCurrent = stable, codeContractCurrent = codeCurrent, projectionHash = Hash(Encoding.UTF8.GetBytes(string.Join("\n", readSet.Select(entry => entry.path + ":" + entry.hash)))),
                workspaces, readSet, diagnostics = _diagnostics.ToArray(),
                authority = "existing workspace manifest/shared definitions/card payloads/action catalog + reviewed source-code contract",
                permissions = new { readOnly = true, rulesOwner = "designer", templateRuleOverrides = false, playerRuleOverrides = false },
                evidence = new { level = "source-inspection-only", unityCompilation = "not-run", runtimePlay = "not-run", publish = "not-performed", screenshotScope = "management-web-only" },
                coverage = "one registered facing-forward-step intent; indexed workspace references only; no instance/deck/actor readiness inferred" };
        }

        private object ProjectWorkspace(JsonElement workspace, string workspacePath, Document intent, List<Document> definitions, List<Document> cards, bool codeCurrent)
        {
            JsonElement source = intent.Data.GetProperty("source");
            bool recognized = TryRecognize(source, out var face, out var wait, out var step, out var block, out double threshold);
            bool semanticCurrent = codeCurrent && recognized;
            int preparation = Number(block, "preparationTimeoutFrames"), completion = Number(block, "completionTimeoutFrames");
            var references = new List<object>();
            foreach (var card in cards)
            {
                var paths = ReferencePaths(Field(card.Data, "payload"), "$.payload", DefinitionId).ToArray();
                if (paths.Length == 0) continue;
                references.Add(new { cardId = Text(card.Data, "cardDraftId"), name = Text(card.Data, "displayName"), revision = Number(card.Data, "currentRevision"),
                    stableId = Text(card.Data, "publishedStableId"), publishedRevision = Number(card.Data, "sourcePublishedRevision"),
                    source = card.Path, sourceHash = card.Hash, referencePaths = paths,
                    organization = Optional(Field(Field(card.Data, "payload"), "root")),
                    overrides = ReferenceOverrides(Field(card.Data, "payload")),
                    rulePolicy = "设计者规则随引用保留；此投影不提供超时结果、成功/失败条件或准入规则覆盖。",
                    compileRecord = new { status = Text(card.Data, "lastCompileStatus"), revision = Number(card.Data, "lastCompiledRevision"),
                        current = Number(card.Data, "lastCompiledRevision") > 0 && Number(card.Data, "lastCompiledRevision") == Number(card.Data, "currentRevision"),
                        scope = "existing draft metadata only; not a hash-bound runtime receipt" } });
            }
            var combinationReferences = definitions.Where(item => item != intent).SelectMany(item =>
                ReferencePaths(item.Data, "$", DefinitionId).Select(pointer => (object)new { definitionId = Text(item.Data, "definitionId"),
                    name = Text(Field(item.Data, "source"), "name"), source = item.Path, pointer, classification = "existing shared-source reference; not inferred to be an independent combination object" })).ToArray();
            var slots = Array(source, "slots").Select(slot => new { slotId = Text(slot, "slotId"), resolver = Text(slot, "resolver"),
                ownership = Text(slot, "resolver") == "stepForward" ? "code-resolved-functional-slot" : "unclassified",
                playerEditable = false, configurationState = "declared-in-source; actor preparation not verified" }).ToArray();
            object action = null;
            if (semanticCurrent)
            {
                string actionCode = ReadText("Assets/Scripts/WGameFrameworkProject/CharacterTest/Runtime/Systems/Character/CharacterTestCombatEvadeRuntime.cs");
                var match = Regex.Match(actionCode, @"const\s+int\s+ForwardStepActionId\s*=\s*(\d+)\s*;");
                if (match.Success)
                {
                    int id = int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture);
                    const string actionPath = "Assets/MxFrameworkGenerated/CharacterActions/Libraries/Common/character_actions.json";
                    var candidate = Array(ReadJson(actionPath), "actions").FirstOrDefault(item => Number(item, "actionId") == id);
                    if (candidate.ValueKind == JsonValueKind.Object)
                        action = new { actionId = id, name = Text(candidate, "displayName"), category = Text(candidate, "category"), durationFrames = Number(candidate, "durationFrames"),
                            source = actionPath, sourceHash = _reads[actionPath], metadataObserved = true, actorBindingVerified = false,
                            note = "动作时长读取目录；倍率/装配后的实际时长仍需既有编译和动作内容 hash。此功能槽不是任意玩家选动作。" };
                }
            }
            return new { workspaceId = Text(workspace, "workspaceId"), name = Text(workspace, "displayName"), revision = Number(workspace, "currentRevision"),
                source = workspacePath, sourceHash = _reads[workspacePath],
                intent = new { definitionId = DefinitionId, name = Text(source, "name"), revision = Number(intent.Data, "revision"), source = intent.Path, sourceHash = intent.Hash,
                    semanticCurrent, recognition = recognized ? "recognized-three-event-facing-step-source" : "unknown-source-shape",
                    purpose = semanticCurrent ? "尝试面向目标，达标或超时后按当前朝向向前垫步一次。" : "源或代码合同已变化，停止自动解释；请查看原始源。",
                    purposeOrigin = "existing CharacterTestTacticalFacingStepHint description contract; not an independently stored rationale",
                    rationale = new { status = "not-recorded", text = "尚未记录独立设计理由；行为说明与代码注释不能代替设计者的取舍记录。" },
                    events = Array(Field(Array(source, "timelines").FirstOrDefault(), "events"), null).Select(item => new { eventId = Text(item, "eventId"), title = Text(item, "title"),
                        operation = Optional(Field(item, "operation")), start = Optional(Field(item, "start")), end = Optional(Field(item, "end")), conditions = Optional(Field(item, "conditionRoot")),
                        maxExecutionsPerPass = Number(item, "maxExecutionsPerPass", 1), cooldownFrames = Number(item, "cooldownFrames"), sourcePointer = "$.source.timelines[0].events" }).ToArray(),
                    block = block.ValueKind == JsonValueKind.Undefined ? null : (object)block, slots, action,
                    parameters = semanticCurrent ? new object[] {
                        new { name = "朝向到位阈值", value = (object)threshold, unit = "degrees", comparison = "less-or-equal", owner = "designer-rule", pointer = "aligned.conditionRoot → TargetFacingAngle.Value" },
                        new { name = "准备等待上限", value = (object)preparation, unit = "frames", owner = "designer-rule", pointer = "$.source.timelines[0].eventBlocks[0].preparationTimeoutFrames" },
                        new { name = "完成保护预算", value = (object)completion, unit = "action-clock-frames-if-acknowledged", owner = "designer-rule", pointer = "$.source.timelines[0].eventBlocks[0].completionTimeoutFrames" },
                        new { name = "准备超时策略", value = (object)Flag(block, "continueToActionOnPreparationTimeout"), owner = "designer-rule", pointer = "$.source.timelines[0].eventBlocks[0].continueToActionOnPreparationTimeout" } } : System.Array.Empty<object>(),
                    mechanisms = semanticCurrent ? new object[] {
                        new { name = "有界对齐准备", rule = "朝向夹角 ≤ " + threshold.ToString(CultureInfo.InvariantCulture) + "° 即到位；最多等待 " + preparation + " 帧；准备超时继续到动作申请。", appliesTo = new[] { "face", "aligned", "step" } },
                        new { name = "目标存活与动作准入", rule = "准备超时不绕过目标存活、SelfActionInterruptible 条件或正式动作准入；拒绝仍结束执行。", appliesTo = new[] { "face", "aligned", "step" } },
                        new { name = "动作回执与完成保护", rule = "完成依据实际动作回执；已确认动作优先用实际动作时钟检查 " + completion + " 帧预算，不能把准备上限与动作目录时长相加当固定总时长。", appliesTo = new[] { "step" } } } : System.Array.Empty<object>(),
                    outcomes = semanticCurrent ? new { preparationTimeout = "continue-to-action-request-with-independent-gates", completion = "actual-action-completed-receipt",
                        failures = new[] { "TargetMissing", "ConditionRejected", "ActionRejected", "TimedOut" },
                        policyOwner = "designer", templateMayChangeOutcome = false, playerMayChangeOutcome = false } : null,
                    standaloneFacingDifference = "独立 Facing 使用严格小于阈值，超时失败；本意图的 ≤ 阈值及超时继续来自自己的源，不能按名称共用该语义。",
                    raw = intent.Data },
                references, combinationReferences,
                relationships = new { intent = "three events enclosed by a source block", composition = "optional; no separate combination inferred from Sequence alone",
                    templates = "existing card composition payload is a template-making expression, not proof of a configured usable card",
                    configuredCards = "published revision metadata and action preparation must be checked separately", instances = "not-projected", deck = "not-projected" },
                lifecycle = new { designRationale = "not-recorded", implementation = semanticCurrent ? "reviewed-code-and-source-shape" : "needs-review",
                    configuration = "shared-source-registered-and-references-observed", enabled = "not-inferred", validation = "source-inspection-only", play = "not-run" } };
        }

        private static bool TryRecognize(JsonElement source, out JsonElement face, out JsonElement wait, out JsonElement step, out JsonElement block, out double threshold)
        {
            face = wait = step = block = default; threshold = 0;
            var timelines = Array(source, "timelines"); if (Text(source, "schema") != "mx.character-test.tactical-card-source.v4" || timelines.Length != 1) return false;
            var events = Array(timelines[0], "events"); var blocks = Array(timelines[0], "eventBlocks");
            if (events.Length != 3 || blocks.Length != 1) return false;
            face = events.FirstOrDefault(item => Text(Field(item, "operation"), "kind") == "TurnToTarget");
            wait = events.FirstOrDefault(item => Text(Field(item, "operation"), "kind") == "WaitForCondition");
            step = events.FirstOrDefault(item => Text(Field(item, "operation"), "kind") == "CharacterAction"); block = blocks[0];
            if (face.ValueKind != JsonValueKind.Object || wait.ValueKind != JsonValueKind.Object || step.ValueKind != JsonValueKind.Object) return false;
            var slot = Array(source, "slots");
            if (slot.Length != 1 || Text(slot[0], "resolver") != "stepForward" || Text(slot[0], "slotId") != Text(Field(step, "operation"), "slotId")
                || Text(Field(face, "operation"), "target") != "currentCombatTarget" || Text(wait, "timingKind") != "Instant"
                || !LocalStart(Field(face, "start")) || !LocalStart(Field(wait, "start"))
                || !Anchor(Field(face, "end"), Text(step, "eventId"), "Start") || !Anchor(Field(step, "start"), Text(wait, "eventId"), "End")
                || Text(block, "completion") != "Action" || Text(block, "completionEventId") != Text(step, "eventId")
                || !Flag(block, "requireLivingTarget") || !Flag(block, "continueToActionOnPreparationTimeout")
                || Number(block, "preparationTimeoutFrames") <= 0 || Number(block, "completionTimeoutFrames") <= 0
                || events.Any(item => Number(item, "maxExecutionsPerPass", 1) != 1 || Number(item, "cooldownFrames") != 0)) return false;
            var waitChildren = Array(Field(wait, "conditionRoot"), "children"); var stepChildren = Array(Field(step, "conditionRoot"), "children");
            if (Number(Field(wait, "conditionRoot"), "kind", -1) != 0 || Number(Field(step, "conditionRoot"), "kind", -1) != 0
                || waitChildren.Length != 2 || stepChildren.Length != 2 || !Alive(Field(face, "conditionRoot"))) return false;
            var angleScope = waitChildren.FirstOrDefault(item => ScopedPredicate(item, 1, 20, 2));
            var permission = stepChildren.FirstOrDefault(item => ScopedPredicate(item, 2, 18, 0));
            if (waitChildren.Count(Alive) != 1 || stepChildren.Count(Alive) != 1 || angleScope.ValueKind != JsonValueKind.Object
                || permission.ValueKind != JsonValueKind.Object || !Flag(Array(permission, "children")[0].GetProperty("predicate"), "boolValue")) return false;
            var value = Field(Array(angleScope, "children")[0].GetProperty("predicate"), "value");
            return value.ValueKind == JsonValueKind.Number && value.TryGetDouble(out threshold) && threshold >= 0 && threshold <= 180;
        }
        private static bool ScopedPredicate(JsonElement node, int subject, int fact, int op)
        { var children = Array(node, "children"); return Number(node, "kind", -1) == 5 && Number(node, "subject", -1) == subject && children.Length == 1
            && Number(children[0], "kind", -1) == 3 && Number(Field(children[0], "predicate"), "fact", -1) == fact && Number(Field(children[0], "predicate"), "operator", -1) == op; }
        private static bool Alive(JsonElement node) => ScopedPredicate(node, 1, 17, 0) && Number(Field(Array(node, "children")[0], "predicate"), "allowedTargetPhases", -1) == 2;
        private static bool LocalStart(JsonElement value) => Text(value, "kind") == "Absolute" && Text(value, "space") == "Block" && Number(value, "frame", -1) == 0;
        private static bool Anchor(JsonElement value, string id, string side) => Text(value, "kind") == "Anchor" && Text(value, "sourceEventId") == id && Text(value, "anchor") == side && Number(value, "offsetFrames") == 0;
        private static object[] ReferenceOverrides(JsonElement value) => Walk(value, "$").Where(item => item.Value.ValueKind == JsonValueKind.Object)
            .SelectMany(item => item.Value.EnumerateObject().Where(property => property.Name == "blockDurations" || property.Name == "maximumWaitFrames")
                .Select(property => (object)new { pointer = item.Path + "." + property.Name, value = property.Value.Clone(), note = "已存设计者作者字段；不证明玩家可修改规则。" })).ToArray();
        private static IEnumerable<string> ReferencePaths(JsonElement value, string path, string id) => Walk(value, path).Where(item => Text(item.Value, "kind") == "Reference" && Text(item.Value, "definitionId") == id).Select(item => item.Path);
        private static IEnumerable<(string Path, JsonElement Value)> Walk(JsonElement value, string path)
        {
            yield return (path, value);
            if (value.ValueKind == JsonValueKind.Object) foreach (var property in value.EnumerateObject()) foreach (var child in Walk(property.Value, path + "." + property.Name)) yield return child;
            else if (value.ValueKind == JsonValueKind.Array) { int i = 0; foreach (var item in value.EnumerateArray()) { foreach (var child in Walk(item, path + "[" + i + "]")) yield return child; i++; } }
        }
        private List<Document> ReadIndex(JsonElement workspace, string field, string directory)
        {
            var documents = new List<Document>(); var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (var entry in Array(workspace, field))
            {
                string id = Text(entry, "id"); if (!ids.Add(id)) throw new InvalidDataException("DUPLICATE_INDEX_ID: " + id);
                string entryPath = Text(entry, "path");
                if (entryPath.Contains("..") || Path.IsPathRooted(entryPath)) throw new InvalidDataException("INDEX_PATH_ESCAPES_WORKSPACE");
                string relative = Relative(Path.Combine(directory, entryPath)); var data = ReadJson(relative);
                // Existing DraftPersistence normalizes newlines and accepts its legacy Windows text hash.
                // The projection read-set separately retains exact file-byte hashes.
                string normalized = ReadText(relative).Replace("\r\n", "\n").Replace("\r", "\n");
                string expectedHash = Text(entry, "hash");
                if (Hash(Encoding.UTF8.GetBytes(normalized)) != expectedHash && Hash(Encoding.UTF8.GetBytes(normalized.Replace("\n", "\r\n"))) != expectedHash)
                    throw new InvalidDataException("INDEX_HASH_MISMATCH: " + relative);
                string actualId = Text(data, field == "cards" ? "cardDraftId" : "definitionId");
                int revision = Number(data, field == "cards" ? "currentRevision" : "revision");
                if (actualId != id || revision != Number(entry, "revision")) throw new InvalidDataException("INDEX_IDENTITY_MISMATCH: " + relative);
                documents.Add(new Document { Data = data, Path = relative, Hash = _reads[relative] });
            }
            return documents;
        }
        private JsonElement ReadJson(string relative)
        {
            using (var document = JsonDocument.Parse(ReadText(relative), new JsonDocumentOptions { MaxDepth = 128 }))
            {
                foreach (var item in Walk(document.RootElement, "$"))
                    if (item.Value.ValueKind == JsonValueKind.Object && item.Value.EnumerateObject().GroupBy(property => property.Name, StringComparer.Ordinal).Any(group => group.Count() > 1))
                        throw new InvalidDataException("DUPLICATE_JSON_FIELD: " + item.Path);
                return document.RootElement.Clone();
            }
        }
        private string ReadText(string relative)
        {
            relative = relative.Replace('\\', '/');
            if (_texts.TryGetValue(relative, out string cached)) return cached;
            string path = SafePath(relative); if (!File.Exists(path)) throw new IOException("SOURCE_MISSING: " + relative);
            if (new FileInfo(path).Length > 8 * 1024 * 1024) throw new IOException("SOURCE_TOO_LARGE: " + relative);
            byte[] bytes = File.ReadAllBytes(path); _reads[relative] = Hash(bytes);
            string text = Encoding.UTF8.GetString(bytes).TrimStart('\uFEFF'); _texts[relative] = text; return text;
        }
        private string Relative(string absolute) { SafePath(absolute); return absolute.Substring(_root.Length + 1).Replace('\\', '/'); }
        private string SafePath(string relative)
        {
            string absolute = Path.GetFullPath(Path.Combine(_root, relative));
            var comparison = Path.DirectorySeparatorChar == '\\' ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
            if (!absolute.StartsWith(_root + Path.DirectorySeparatorChar, comparison)) throw new IOException("SOURCE_OUTSIDE_PROJECT");
            for (var node = new DirectoryInfo(absolute); node != null; node = node.Parent)
                if ((File.Exists(node.FullName) || Directory.Exists(node.FullName)) && (File.GetAttributes(node.FullName) & FileAttributes.ReparsePoint) != 0) throw new IOException("SOURCE_REPARSE_POINT");
            return absolute;
        }
        private static JsonElement Field(JsonElement value, string name) => value.ValueKind == JsonValueKind.Object && value.TryGetProperty(name, out var field) ? field : default;
        private static object Optional(JsonElement value) => value.ValueKind == JsonValueKind.Undefined ? null : (object)value;
        private static string Text(JsonElement value, string name) { var field = Field(value, name); return field.ValueKind == JsonValueKind.String ? field.GetString() : string.Empty; }
        private static int Number(JsonElement value, string name, int fallback = 0) { var field = Field(value, name); return field.ValueKind == JsonValueKind.Number && field.TryGetInt32(out int number) ? number : fallback; }
        private static bool Flag(JsonElement value, string name) => Field(value, name).ValueKind == JsonValueKind.True;
        private static JsonElement[] Array(JsonElement value, string name) { var field = name == null ? value : Field(value, name); return field.ValueKind == JsonValueKind.Array ? field.EnumerateArray().ToArray() : System.Array.Empty<JsonElement>(); }
        private static string Hash(byte[] bytes) { using (var hash = SHA256.Create()) return BitConverter.ToString(hash.ComputeHash(bytes)).Replace("-", string.Empty).ToLowerInvariant(); }
        private sealed class Document { public JsonElement Data; public string Path; public string Hash; }
    }
}
