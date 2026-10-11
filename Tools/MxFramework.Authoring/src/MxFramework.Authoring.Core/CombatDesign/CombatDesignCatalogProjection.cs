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
    /// <summary>Deterministic read-only directory of indexed tactical sources and card composition expressions.</summary>
    /// <remarks>LowFreqAlloc explicit authoring query. Parsing is finite and does not compile, execute or infer design intent.</remarks>
    public sealed class CombatDesignCatalogProjection
    {
        internal const string MetadataPath = "Tools/MxFramework.Authoring/metadata/combat-design-view.json";
        private const string Dev = "Assets/Scripts/WGameFrameworkProject/CharacterTest/RuntimeAiPlannerDev/";
        private readonly CombatDesignSourceReader _reader;
        private readonly Dictionary<string, Dictionary<int, string>> _enums = new Dictionary<string, Dictionary<int, string>>(StringComparer.Ordinal);
        private readonly Dictionary<string, JsonElement> _descriptors = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
        private readonly HashSet<string> _operations = new HashSet<string>(StringComparer.Ordinal);
        private JsonElement _metadata;
        private CombatDesignComponentCatalog _componentCatalog;

        public CombatDesignCatalogProjection(string projectRoot) { _reader = new CombatDesignSourceReader(projectRoot); }

        /// <summary>Rebuilds facts, reverse references and classification freshness from existing authoritative inputs.</summary>
        public object Query()
        {
            _reader.Reset(); _enums.Clear(); _descriptors.Clear(); _operations.Clear();
            _metadata = _reader.Json(MetadataPath);
            if (S(_metadata, "schema") != "mx.combat-design.view-metadata.v1") throw new InvalidDataException("VIEW_METADATA_SCHEMA_UNSUPPORTED");
            LoadVocabulary();
            var entries = new List<Dictionary<string, object>>();
            var workspaces = new List<object>();
            string drafts = _reader.Path("Authoring/RuntimeAiLab/Drafts");
            if (Directory.Exists(drafts)) foreach (string directory in Directory.GetDirectories(drafts).OrderBy(p => p, StringComparer.Ordinal))
            {
                string relative = _reader.Relative(System.IO.Path.Combine(directory, "workspace.json"));
                if (!File.Exists(_reader.Path(relative))) continue;
                try
                {
                    JsonElement workspace = _reader.Json(relative);
                    if (S(workspace, "schema") != "mx.character-test.runtime-ai-lab.workspace.v4") { _reader.Diagnostic("UNPARSED_WORKSPACE_SCHEMA", relative); continue; }
                    string workspaceId = S(workspace, "workspaceId");
                    workspaces.Add(D("workspaceId", workspaceId, "name", S(workspace, "displayName"), "revision", O(F(workspace, "currentRevision")), "source", relative, "sourceHash", _reader.Hashes[relative]));
                    var local = new List<Dictionary<string, object>>();
                    foreach (string index in new[] { "tacticalDefinitions", "cards" })
                    {
                        var ids = new HashSet<string>(StringComparer.Ordinal);
                        foreach (JsonElement item in A(workspace, index).OrderBy(e => S(e, "id"), StringComparer.Ordinal))
                        {
                            string id = S(item, "id"), itemPath = S(item, "path");
                            if (string.IsNullOrWhiteSpace(id) || !ids.Add(id)) { _reader.Diagnostic("DUPLICATE_OR_EMPTY_INDEX_ID", relative + ":" + id); continue; }
                            try
                            {
                                if (itemPath.Split('/', '\\').Any(p => p == "..") || System.IO.Path.IsPathRooted(itemPath)) throw new InvalidDataException("INDEX_PATH_ESCAPES_WORKSPACE");
                                string sourcePath = _reader.Relative(System.IO.Path.Combine(directory, itemPath));
                                if (!_reader.Path(sourcePath).StartsWith(directory + System.IO.Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("INDEX_PATH_ESCAPES_WORKSPACE");
                                JsonElement data = _reader.Json(sourcePath);
                                string normalized = _reader.Text(sourcePath).Replace("\r\n", "\n").Replace("\r", "\n");
                                if (CombatDesignSourceReader.Hash(Encoding.UTF8.GetBytes(normalized)) != S(item, "hash") && CombatDesignSourceReader.Hash(Encoding.UTF8.GetBytes(normalized.Replace("\n", "\r\n"))) != S(item, "hash")) throw new InvalidDataException("INDEX_HASH_MISMATCH");
                                string idField = index == "cards" ? "cardDraftId" : "definitionId", revisionField = index == "cards" ? "currentRevision" : "revision";
                                if (S(data, idField) != id || N(data, revisionField, -1) != N(item, "revision", -2)) throw new InvalidDataException("INDEX_IDENTITY_MISMATCH");
                                var entry = Project(data, index == "cards", id, workspaceId, sourcePath);
                                local.Add(entry);
                            }
                            catch (Exception error) when (CombatDesignSourceReader.ReadError(error)) { _reader.Diagnostic("INDEX_ENTRY_UNAVAILABLE", relative + ":" + id + ":" + error.Message); }
                        }
                    }
                    BuildGraph(local); entries.AddRange(local);
                }
                catch (Exception error) when (CombatDesignSourceReader.ReadError(error)) { _reader.Diagnostic("WORKSPACE_UNAVAILABLE", relative + ":" + error.Message); }
            }
            _componentCatalog = new CombatDesignComponentCatalog(_reader, Symbol);
            object[] components = _componentCatalog.Build(entries);
            return _reader.Seal(D("schema", "mx.combat-design.catalog-projection.v1", "generator", "fixed-catalog-parser.v2", "components", components, "entries", entries.OrderBy(e => (string)e["entryKey"], StringComparer.Ordinal).ToArray(), "workspaces", workspaces,
                "vocabulary", _enums.OrderBy(e => e.Key, StringComparer.Ordinal).ToDictionary(e => e.Key, e => (object)e.Value.OrderBy(v => v.Key).Select(v => D("value", v.Key, "symbol", v.Value)).ToArray()),
                "authority", "existing workspace indices, indexed shared source/card payloads, authoring compiler vocabulary, action catalog; separate provenance-bound design annotations",
                "coverage", "Current-tree component factories, description contracts and functional slots; indexed v4 workspaces provide block instances and references. Source members are reparsed per GET. Complete templates are not components. No isolated checkout, player library, publication or runtime readiness inferred.",
                "evidence", D("level", "source-inspection-only", "unity", "not-run", "play", "not-run", "publish", "not-performed"), "permissions", D("readOnly", true, "playerRuleOverrides", false, "templateRuleOverrides", false)));
        }

        /// <summary>Classifies a saved source with the same current-source component rules used by the directory.</summary>
        internal object[] ClassifyComponents(JsonElement source) => _componentCatalog?.ClassifySource(source) ?? Array.Empty<object>();

        /// <summary>Returns only a file already read by the current catalog, for its read-only source viewer.</summary>
        /// <remarks>LowFreqAlloc. The allowlist is rebuilt from current authority inputs; arbitrary project paths are rejected.</remarks>
        public object ReadSource(string path)
        {
            Query();
            path = (path ?? string.Empty).Replace('\\', '/');
            if (!_reader.Hashes.ContainsKey(path)) throw new ArgumentException("SOURCE_NOT_IN_CURRENT_CATALOG");
            return D("path", path, "hash", _reader.Hashes[path], "text", _reader.Text(path));
        }

        private void LoadVocabulary()
        {
            ReadEnum(Dev + "CharacterTestRuntimeAiLabBehaviorRecipeV6.cs", "CharacterTestRuntimeAiLabBehaviorRecipeConditionNodeKind", "node");
            ReadEnum(Dev + "CharacterTestRuntimeAiLabBehaviorRecipeV6.cs", "CharacterTestRuntimeAiLabBehaviorRecipeConditionSubjectKind", "subject");
            ReadEnum(Dev + "CharacterTestRuntimeAiLabBehaviorRecipeV6.cs", "CharacterTestRuntimeAiLabBehaviorRecipeEventSignal", "signal");
            ReadEnum(Dev + "CharacterTestRuntimeAiLabBehaviorCardPayload.cs", "CharacterTestRuntimeAiLabCardConditionFact", "fact");
            ReadEnum(Dev + "CharacterTestRuntimeAiLabBehaviorCardPayload.cs", "CharacterTestRuntimeAiLabCardConditionOperator", "operator");
            ReadEnum(Dev + "CharacterTestRuntimeAiLabBehaviorCardLogic.cs", "CharacterTestRuntimeAiLabMovementStrategy", "movement");
            ReadEnum(Dev + "CharacterTestRuntimeAiLabBehaviorCardLogic.cs", "CharacterTestRuntimeAiLabMovementTargetKind", "movementTarget");
            string catalog = _reader.Text(Dev + "CharacterTestRuntimeAiLabBehaviorConditionCatalog.cs");
            // Only existing descriptor factory calls, not defaults or a second fact-label/value table.
            foreach (Match match in Regex.Matches(catalog, @"\b(Bool|Number|TargetState)\(CharacterTestRuntimeAiLabCardConditionFact\.(\w+),\s*Subject\.(\w+),\s*Category\.\w+,\s*""([^""]+)""(?:,\s*""([^""]*)"")?"))
                _descriptors[match.Groups[2].Value] = JsonSerializer.SerializeToElement(D("valueKind", match.Groups[1].Value, "label", match.Groups[4].Value, "unit", match.Groups[5].Value));
            string compiler = _reader.Text(Dev + "CharacterTestTacticalAuthoringCompiler.cs");
            foreach (Match match in Regex.Matches(compiler, @"case\s+""(\w+)""\s*:\s*(?:builder\.Add\w+\(|var\s+\w+\s*=)")) _operations.Add(match.Groups[1].Value);
            if (_operations.Count == 0) _reader.Diagnostic("UNPARSED_OPERATION_VOCABULARY", Dev + "CharacterTestTacticalAuthoringCompiler.cs");
        }

        private void ReadEnum(string path, string name, string key)
        {
            string source = _reader.Text(path);
            Match body = Regex.Match(source, @"\benum\s+" + Regex.Escape(name) + @"\s*\{([^}]+)\}");
            var values = new Dictionary<int, string>();
            if (body.Success) foreach (Match member in Regex.Matches(body.Groups[1].Value, @"\b(\w+)\s*=\s*(\d+)\s*[,\r\n]")) values[int.Parse(member.Groups[2].Value, CultureInfo.InvariantCulture)] = member.Groups[1].Value;
            _enums[key] = values;
            if (values.Count == 0) _reader.Diagnostic("UNPARSED_ENUM", path + ":" + name);
        }

        private Dictionary<string, object> Project(JsonElement data, bool card, string id, string workspace, string path)
        {
            JsonElement source = F(data, card ? "payload" : "source");
            bool supported = S(source, "schema") == (card ? "mx.character-test.tactical-card-composition.v1" : "mx.character-test.tactical-card-source.v4");
            var unknown = new List<string>();
            if (!supported) unknown.Add("UNPARSED_SOURCE_SCHEMA: " + S(source, "schema"));
            var role = Role(id, card, S(source, "schema"));
            var events = new List<object>(); var blocks = new List<object>(); var slots = new List<object>(); var references = new List<object>();
            if (supported && card) Composition(F(source, "root"), "$.payload.root", references, unknown, 0);
            if (supported && !card)
            {
                foreach (var slot in A(source, "slots")) slots.Add(Slot(slot, "$.source.slots", unknown));
                int t = 0;
                foreach (var timeline in A(source, "timelines"))
                {
                    int e = 0; string prefix = "$.source.timelines[" + t + "]";
                    var eventIds = new HashSet<string>(A(timeline, "events").Select(item => S(item, "eventId")), StringComparer.Ordinal);
                    if (eventIds.Count != A(timeline, "events").Length) unknown.Add("DUPLICATE_EVENT_ID: " + prefix);
                    var anchorIds = new HashSet<string>(eventIds, StringComparer.Ordinal);
                    foreach (var block in A(timeline, "eventBlocks")) anchorIds.Add(S(block, "blockId"));
                    foreach (var evt in A(timeline, "events"))
                    {
                        string pointer = prefix + ".events[" + e++ + "]"; var operation = F(evt, "operation"); string kind = S(operation, "kind");
                        bool recognized = _operations.Contains(kind); if (!recognized) unknown.Add("UNPARSED_OPERATION: " + pointer + ":" + kind);
                        string description = Operation(operation, unknown, pointer);
                        var condition = Condition(F(evt, "conditionRoot"), unknown, pointer + ".conditionRoot", 0);
                        foreach (string bound in new[] { "start", "end" }) CheckAnchor(F(evt, bound), anchorIds, pointer + "." + bound, unknown);
                        events.Add(D("eventId", S(evt, "eventId"), "title", S(evt, "title"), "timelineId", S(timeline, "timelineId"), "pointer", pointer,
                            "operationRecognized", recognized, "description", description, "operation", O(operation), "condition", condition,
                            "timing", D("kind", O(F(evt, "timingKind")), "start", O(F(evt, "start")), "end", O(F(evt, "end")), "description", Timing(F(evt, "start")) + " → " + Timing(F(evt, "end"))),
                            "execution", D("priority", O(F(evt, "priority")), "maxExecutionsPerPass", O(F(evt, "maxExecutionsPerPass")), "cooldownFrames", O(F(evt, "cooldownFrames"))), "raw", evt));
                        string slotId = S(operation, "slotId");
                        if (kind == "CharacterAction" && !A(source, "slots").Any(s => S(s, "slotId") == slotId)) unknown.Add("MISSING_ACTION_SLOT: " + pointer + ":" + slotId);
                    }
                    foreach (var block in A(timeline, "eventBlocks"))
                    {
                        foreach (var eventId in A(block, "eventIds")) if (eventId.ValueKind == JsonValueKind.String && !eventIds.Contains(eventId.GetString())) unknown.Add("MISSING_BLOCK_EVENT: " + eventId.GetString());
                        blocks.Add(D("blockId", S(block, "blockId"), "timelineId", S(timeline, "timelineId"), "pointer", prefix + ".eventBlocks", "facts", block,
                            "description", "完成方式 " + S(block, "completion") + "；完成事件 " + S(block, "completionEventId") + "；准备上限 " + Value(block, "preparationTimeoutFrames") + "；完成预算 " + Value(block, "completionTimeoutFrames") + "；准备超时继续申请动作 " + Value(block, "continueToActionOnPreparationTimeout")));
                    }
                    t++;
                }
            }
            return D("entryKey", workspace + "/" + (card ? "card/" : "shared/") + id, "workspaceId", workspace, "id", id, "name", card ? S(data, "displayName") : S(source, "name"),
                "kind", card ? supported ? "template-expression" : "card-draft" : "shared-source", "role", role, "revision", O(F(data, card ? "currentRevision" : "revision")), "source", path, "sourceHash", _reader.Hashes[path],
                "sourceSchema", S(source, "schema"), "supported", supported, "factsStatus", unknown.Count == 0 ? "parsed" : "partial-or-unparsed", "events", events, "blocks", blocks, "slots", slots,
                "references", references, "unparsed", unknown.Distinct().OrderBy(v => v, StringComparer.Ordinal).ToArray(), "raw", data,
                "lifecycle", D("design", role["status"], "code", "fixed-source-parser; runtime-not-verified", "configuration", "indexed-source-observed", "enabled", "not-inferred", "validation", "no-runtime-receipt"),
                "compileMetadata", card ? D("revision", O(F(data, "lastCompiledRevision")), "status", O(F(data, "lastCompileStatus")), "publishedRevision", O(F(data, "sourcePublishedRevision")), "scope", "draft metadata only; no content-hash-bound usable-card receipt") : null);
        }

        private Dictionary<string, object> Role(string id, bool card, string schema)
        {
            if (card) return schema == "mx.character-test.tactical-card-composition.v1" ? D("value", "template-expression", "status", "source-schema-expression; no independent template/card identity inferred", "origin", schema, "current", true) : D("value", "unparsed-card", "status", "unsupported-card-payload; raw-visible", "origin", schema, "current", false);
            JsonElement record = A(_metadata, "roles").FirstOrDefault(r => S(r, "definitionId") == id);
            if (record.ValueKind != JsonValueKind.Object) return D("value", "pending", "status", "classification-not-recorded", "current", false);
            JsonElement provenance = F(record, "provenance"); string path = S(provenance, "path"), selector = S(provenance, "selector"); bool current = false;
            try { current = _reader.Text(path).Contains(selector) && _reader.Hashes[path] == S(provenance, "reviewedHash"); }
            catch (Exception error) when (CombatDesignSourceReader.ReadError(error)) { _reader.Diagnostic("CLASSIFICATION_SOURCE_UNAVAILABLE", id + ":" + error.Message); }
            return D("value", current ? S(record, "role") : "pending", "status", current ? "explicit-reviewed-design-classification" : "classification-provenance-stale", "origin", S(record, "origin"), "current", current, "provenance", O(provenance));
        }

        private object Slot(JsonElement slot, string pointer, List<string> unknown)
        {
            string resolver = S(slot, "resolver"); object action = null;
            // Only established functional resolver contracts; other slots require real actor preparation.
            if (resolver == "stepForward" || resolver == "stepBack")
            {
                string path = resolver == "stepForward"
                    ? "Assets/Scripts/WGameFrameworkProject/CharacterTest/Runtime/Systems/Character/CharacterTestCombatEvadeRuntime.cs"
                    : Dev + "CharacterTestTacticalCardInputs.cs";
                string name = resolver == "stepForward" ? "ForwardStepActionId" : "CommonStepBackActionId";
                Match value = Regex.Match(_reader.Text(path), @"const\s+int\s+" + name + @"\s*=\s*(\d+)\s*;");
                if (value.Success)
                {
                    int id = int.Parse(value.Groups[1].Value, CultureInfo.InvariantCulture); const string actionPath = "Assets/MxFrameworkGenerated/CharacterActions/Libraries/Common/character_actions.json";
                    JsonElement data = A(_reader.Json(actionPath), "actions").FirstOrDefault(a => N(a, "actionId", -1) == id);
                    if (data.ValueKind == JsonValueKind.Object) action = D("actionId", id, "name", S(data, "displayName"), "source", actionPath, "sourceHash", _reader.Hashes[actionPath], "resolverSource", path, "raw", data, "actorBindingVerified", false);
                }
                if (action == null) unknown.Add("UNRESOLVED_FUNCTIONAL_SLOT: " + resolver);
            }
            return D("slotId", S(slot, "slotId"), "resolver", resolver, "pointer", pointer, "declaration", slot, "action", action, "state", action == null ? "declared; actor-binding-not-resolved" : "functional-code-resolved; actor-not-verified");
        }

        private string Operation(JsonElement operation, List<string> unknown, string pointer)
        {
            string kind = S(operation, "kind");
            if (!_operations.Contains(kind)) return "未解析操作 " + kind + "；保留原始参数";
            switch (kind)
            {
                case "CharacterAction": return "申请角色动作；槽 " + S(operation, "slotId");
                case "TurnToTarget": return "转向 " + S(operation, "target") + "；速度 " + Value(operation, "speed");
                case "WaitForCondition": return "等待下列条件；不从事件标题推断成功或超时结果";
                case "Movement":
                    JsonElement movement = F(operation, "movement"); string strategy = Symbol("movement", F(movement, "strategy")), target = Symbol("movementTarget", F(movement, "target"));
                    if (strategy.StartsWith("?", StringComparison.Ordinal) || target.StartsWith("?", StringComparison.Ordinal)) unknown.Add("UNPARSED_MOVEMENT_ENUM: " + pointer);
                    return "移动 " + strategy + " → " + target + "；距离来源 " + S(movement, "distanceSource") + "；范围 [" + Value(movement, "minimum") + ", " + Value(movement, "maximum") + "]；速度 " + Value(operation, "speed");
                case "EnterTimeline": return "进入时间轴 " + S(operation, "targetTimelineId");
                case "ReturnToPlanner": return "返回规划器";
                default: return "已登记操作 " + kind + "；参数逐项见原始源";
            }
        }

        private Dictionary<string, object> Condition(JsonElement node, List<string> unknown, string pointer, int depth)
        {
            if (node.ValueKind == JsonValueKind.Undefined || node.ValueKind == JsonValueKind.Null) return D("text", "未配置条件", "parsed", true, "raw", null);
            if (depth > 32) { unknown.Add("UNPARSED_CONDITION_DEPTH: " + pointer); return D("text", "条件超出解析深度", "parsed", false, "raw", node); }
            string kind = Symbol("node", F(node, "kind"));
            var children = A(node, "children").Select((c, i) => Condition(c, unknown, pointer + ".children[" + i + "]", depth + 1)).ToArray();
            bool parsed = children.All(c => (bool)c["parsed"]); string description;
            switch (kind)
            {
                case "All": description = children.Length == 0 ? "All(空)" : string.Join(" 且 ", children.Select(c => "(" + c["text"] + ")")); break;
                case "Any": description = "任一(" + string.Join(" 或 ", children.Select(c => c["text"])) + ")"; break;
                case "Not": parsed &= children.Length == 1; description = "非(" + string.Join("；", children.Select(c => c["text"])) + ")"; break;
                case "SubjectScope": string subject = Symbol("subject", F(node, "subject")); parsed &= !subject.StartsWith("?", StringComparison.Ordinal); description = subject + "：" + string.Join("；", children.Select(c => c["text"])); break;
                case "EventSignal": string signal = Symbol("signal", F(node, "signal")); parsed &= !signal.StartsWith("?", StringComparison.Ordinal); description = "事件 " + S(node, "sourceEventId") + " 的 " + signal; break;
                case "FactPredicate":
                    JsonElement predicate = F(node, "predicate"); string fact = Symbol("fact", F(predicate, "fact")), op = Symbol("operator", F(predicate, "operator"));
                    if (!_descriptors.TryGetValue(fact, out JsonElement descriptor)) { parsed = false; description = fact + " " + op + "（值类型未解析，见 predicate）"; break; }
                    string valueKind = S(descriptor, "valueKind"), label = S(descriptor, "label"), unit = S(descriptor, "unit");
                    if (valueKind == "TargetState") { parsed &= op == "Equals"; description = label + "：allowedTargetPhases=" + Value(predicate, "allowedTargetPhases") + "，requiredTargetFlags=" + Value(predicate, "requiredTargetFlags") + "，forbiddenTargetFlags=" + Value(predicate, "forbiddenTargetFlags"); }
                    else if (valueKind == "Bool") { parsed &= op == "Equals" || op == "NotEquals"; description = label + " " + (op == "NotEquals" ? "≠" : "=") + " " + Value(predicate, "boolValue"); }
                    else
                    {
                        var operators = new Dictionary<string, string> { ["Equals"] = "=", ["NotEquals"] = "≠", ["LessOrEqual"] = "≤", ["GreaterOrEqual"] = "≥", ["GreaterThan"] = ">" };
                        if (op == "Between") description = label + " ∈ [" + Value(predicate, "value") + ", " + Value(predicate, "maxValue") + "] " + unit;
                        else if (operators.TryGetValue(op, out string symbol)) description = label + " " + symbol + " " + Value(predicate, "value") + " " + unit;
                        else { parsed = false; description = label + " 未解析运算 " + op; }
                    }
                    break;
                default: parsed = false; description = "未解析条件节点 " + kind; break;
            }
            if (!parsed) unknown.Add("UNPARSED_CONDITION: " + pointer + ":" + kind);
            return D("text", description, "parsed", parsed, "pointer", pointer, "raw", node);
        }

        private static string Timing(JsonElement timing) => S(timing, "kind") == "Absolute" ? "绝对帧 " + Value(timing, "frame") + "（" + S(timing, "space") + "）" : S(timing, "kind") == "Anchor" ? S(timing, "sourceEventId") + "." + S(timing, "anchor") + " + " + Value(timing, "offsetFrames") + " 帧" : "未解析时间 " + S(timing, "kind");
        private static void CheckAnchor(JsonElement timing, HashSet<string> events, string pointer, List<string> unknown)
        {
            if (timing.ValueKind == JsonValueKind.Undefined || timing.ValueKind == JsonValueKind.Null) return;
            string kind = S(timing, "kind");
            if (kind != "Absolute" && kind != "Anchor") unknown.Add("UNPARSED_TIMING: " + pointer);
            if (kind == "Anchor" && !new[] { "Start", "End", "SimulationEnd" }.Contains(S(timing, "anchor"), StringComparer.Ordinal)) unknown.Add("UNPARSED_ANCHOR_KIND: " + pointer + ":" + S(timing, "anchor"));
            if (kind == "Anchor" && S(timing, "sourceEventId") != "" && !events.Contains(S(timing, "sourceEventId"))) unknown.Add("MISSING_ANCHOR_EVENT: " + pointer + ":" + S(timing, "sourceEventId"));
        }

        private static void Composition(JsonElement node, string pointer, List<object> references, List<string> unknown, int depth)
        {
            if (depth > 32) { unknown.Add("UNPARSED_COMPOSITION_DEPTH: " + pointer); return; }
            string kind = S(node, "kind");
            if (kind == "Reference") references.Add(D("definitionId", S(node, "definitionId"), "pointer", pointer, "overrides", O(F(node, "overrides"))));
            else if (kind == "Sequence") { int i = 0; foreach (var child in A(node, "children")) Composition(child, pointer + ".children[" + i++ + "]", references, unknown, depth + 1); }
            else unknown.Add("UNPARSED_COMPOSITION_NODE: " + pointer + ":" + kind);
        }

        private void BuildGraph(List<Dictionary<string, object>> entries)
        {
            var definitions = entries.Where(e => (string)e["kind"] == "shared-source").ToDictionary(e => (string)e["id"], StringComparer.Ordinal);
            foreach (var entry in entries)
            {
                var refs = ((List<object>)entry["references"]).Cast<Dictionary<string, object>>().ToArray();
                var resolved = new List<object>();
                foreach (var reference in refs)
                {
                    string id = (string)reference["definitionId"]; bool exists = definitions.TryGetValue(id, out var target);
                    resolved.Add(D("id", id, "pointer", reference["pointer"], "resolved", exists, "entryKey", exists ? target["entryKey"] : null));
                    if (!exists) _reader.Diagnostic("MISSING_REFERENCE", (string)entry["entryKey"] + ":" + id);
                }
                entry["resolvedReferences"] = resolved;
                entry["usedBy"] = entries.Where(e => ((List<object>)e["references"]).Cast<Dictionary<string, object>>().Any(r => (string)r["definitionId"] == (string)entry["id"]))
                    .OrderBy(e => (string)e["entryKey"], StringComparer.Ordinal).Select(e => D("entryKey", e["entryKey"], "id", e["id"], "name", e["name"], "kind", e["kind"], "role", e["role"], "revision", e["revision"], "source", e["source"], "sourceHash", e["sourceHash"])).ToArray();
            }
        }

        private string Symbol(string key, JsonElement value)
        {
            if (_enums.TryGetValue(key, out var table))
            {
                if (value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out int number) && table.TryGetValue(number, out string name)) return name;
                if (value.ValueKind == JsonValueKind.String && table.Values.Contains(value.GetString(), StringComparer.Ordinal)) return value.GetString();
            }
            return "?" + (value.ValueKind == JsonValueKind.Undefined ? "missing" : value.ToString());
        }
        internal static Dictionary<string, object> D(params object[] pairs) { var result = new Dictionary<string, object>(StringComparer.Ordinal); for (int i = 0; i < pairs.Length; i += 2) result.Add((string)pairs[i], pairs[i + 1]); return result; }
        internal static JsonElement F(JsonElement element, string name) => element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out var field) ? field : default;
        internal static string S(JsonElement element, string name) => F(element, name).ValueKind == JsonValueKind.String ? F(element, name).GetString() : string.Empty;
        internal static int N(JsonElement element, string name, int fallback = 0) => F(element, name).TryNumber(out int number) ? number : fallback;
        internal static object O(JsonElement value) => value.ValueKind == JsonValueKind.Undefined ? null : (object)value;
        internal static JsonElement[] A(JsonElement element, string name) => F(element, name).ValueKind == JsonValueKind.Array ? F(element, name).EnumerateArray().ToArray() : System.Array.Empty<JsonElement>();
        private static string Value(JsonElement element, string name) => F(element, name).ValueKind == JsonValueKind.Undefined ? "未配置" : F(element, name).ToString();
    }

    internal static class CombatDesignJsonNumber
    {
        internal static bool TryNumber(this JsonElement value, out int number) { number = 0; return value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out number); }
    }

    internal sealed class CombatDesignSourceReader
    {
        private readonly string _root;
        private readonly Dictionary<string, string> _texts = new Dictionary<string, string>(StringComparer.Ordinal);
        internal readonly Dictionary<string, string> Hashes = new Dictionary<string, string>(StringComparer.Ordinal);
        private readonly HashSet<string> _diagnostics = new HashSet<string>(StringComparer.Ordinal);
        private bool _consistent = true;
        internal CombatDesignSourceReader(string root) { _root = System.IO.Path.GetFullPath(root ?? throw new ArgumentNullException(nameof(root))).TrimEnd(System.IO.Path.DirectorySeparatorChar); }
        internal void Reset() { _texts.Clear(); Hashes.Clear(); _diagnostics.Clear(); _consistent = true; }
        internal void MergeHash(string path, string hash)
        {
            if (Hashes.TryGetValue(path, out string earlier) && earlier != hash) { _consistent = false; Diagnostic("RELATED_SOURCE_CHANGED_DURING_QUERY", path); }
            Hashes[path] = hash;
        }
        internal void Diagnostic(string code, string context) => _diagnostics.Add(code + ": " + context);
        internal string Path(string relative)
        {
            string full = System.IO.Path.GetFullPath(System.IO.Path.Combine(_root, relative));
            if (!full.StartsWith(_root + System.IO.Path.DirectorySeparatorChar, System.IO.Path.DirectorySeparatorChar == '\\' ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal)) throw new IOException("SOURCE_OUTSIDE_PROJECT");
            for (var node = new FileInfo(full) as FileSystemInfo; node != null; node = new DirectoryInfo(System.IO.Path.GetDirectoryName(node.FullName) ?? _root))
            {
                if ((File.Exists(node.FullName) || Directory.Exists(node.FullName)) && (File.GetAttributes(node.FullName) & FileAttributes.ReparsePoint) != 0) throw new IOException("SOURCE_REPARSE_POINT");
                if (string.Equals(node.FullName, _root, StringComparison.OrdinalIgnoreCase)) break;
            }
            return full;
        }
        internal string Relative(string full) { Path(full); return full.Substring(_root.Length + 1).Replace('\\', '/'); }
        internal string Text(string path)
        {
            path = path.Replace('\\', '/'); if (_texts.TryGetValue(path, out string cached)) return cached;
            string full = Path(path); if (!File.Exists(full)) throw new IOException("SOURCE_MISSING: " + path);
            if (new FileInfo(full).Length > 8 * 1024 * 1024) throw new IOException("SOURCE_TOO_LARGE: " + path);
            byte[] bytes = File.ReadAllBytes(full); MergeHash(path, Hash(bytes)); return _texts[path] = Encoding.UTF8.GetString(bytes).TrimStart('\uFEFF');
        }
        internal JsonElement Json(string path)
        {
            using (var document = JsonDocument.Parse(Text(path), new JsonDocumentOptions { MaxDepth = 128 })) { DuplicateCheck(document.RootElement); return document.RootElement.Clone(); }
        }
        private static void DuplicateCheck(JsonElement element)
        {
            if (element.ValueKind == JsonValueKind.Object) { var names = new HashSet<string>(StringComparer.Ordinal); foreach (var property in element.EnumerateObject()) { if (!names.Add(property.Name)) throw new InvalidDataException("DUPLICATE_JSON_FIELD: " + property.Name); DuplicateCheck(property.Value); } }
            else if (element.ValueKind == JsonValueKind.Array) foreach (var item in element.EnumerateArray()) DuplicateCheck(item);
        }
        internal object Seal(Dictionary<string, object> payload)
        {
            bool stable = _consistent;
            foreach (var pair in Hashes) { try { stable &= File.Exists(Path(pair.Key)) && Hash(File.ReadAllBytes(Path(pair.Key))) == pair.Value; } catch (IOException) { stable = false; } }
            if (!stable) Diagnostic("SOURCE_CHANGED_DURING_QUERY", "retry the read; this is no validation receipt");
            payload["sourceCurrent"] = stable;
            payload["readSet"] = Hashes.OrderBy(p => p.Key, StringComparer.Ordinal).Select(p => CombatDesignCatalogProjection.D("path", p.Key, "hash", p.Value)).ToArray();
            payload["diagnostics"] = _diagnostics.OrderBy(d => d, StringComparer.Ordinal).ToArray();
            byte[] canonical;
            using (var memory = new MemoryStream()) { using (var writer = new Utf8JsonWriter(memory)) { Canonical(writer, JsonSerializer.SerializeToElement(payload)); } canonical = memory.ToArray(); }
            payload["projectionHash"] = Hash(canonical); payload["generatedUtc"] = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture);
            return payload;
        }
        private static void Canonical(Utf8JsonWriter writer, JsonElement value)
        {
            if (value.ValueKind == JsonValueKind.Object) { writer.WriteStartObject(); foreach (var p in value.EnumerateObject().OrderBy(p => p.Name, StringComparer.Ordinal)) { writer.WritePropertyName(p.Name); Canonical(writer, p.Value); } writer.WriteEndObject(); }
            else if (value.ValueKind == JsonValueKind.Array) { writer.WriteStartArray(); foreach (var item in value.EnumerateArray()) Canonical(writer, item); writer.WriteEndArray(); }
            else value.WriteTo(writer);
        }
        internal static bool ReadError(Exception error) => error is IOException || error is InvalidDataException || error is JsonException || error is InvalidOperationException || error is ArgumentException;
        internal static string Hash(byte[] bytes) { using (var algorithm = SHA256.Create()) return BitConverter.ToString(algorithm.ComputeHash(bytes)).Replace("-", string.Empty).ToLowerInvariant(); }
    }
}
