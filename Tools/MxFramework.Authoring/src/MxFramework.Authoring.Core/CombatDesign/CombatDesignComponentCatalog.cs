using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using static MxFramework.Authoring.CombatDesignCatalogProjection;

namespace MxFramework.Authoring
{
    /// <summary>Finite source decoders for reusable tactical components, independent of saved template identities.</summary>
    /// <remarks>LowFreqAlloc. Read-only authoring projection; never executes factories or infers player availability.</remarks>
    internal sealed class CombatDesignComponentCatalog
    {
        private const string Dev = "Assets/Scripts/WGameFrameworkProject/CharacterTest/RuntimeAiPlannerDev/";
        private const string Card = "Assets/Scripts/WGameFrameworkProject/CharacterTest/TacticalCard/";
        private const string Visual = Dev + "CharacterTestTacticalAuthoringTemplates.Visual.cs";
        private const string Wait = Dev + "CharacterTestTacticalAuthoringTemplates.WaitOpportunity.cs";
        private readonly CombatDesignSourceReader _reader;
        private readonly Func<string, JsonElement, string> _symbol;
        private readonly List<Dictionary<string, object>> _components = new List<Dictionary<string, object>>();
        private int _twoSecondFrames;

        internal CombatDesignComponentCatalog(CombatDesignSourceReader reader, Func<string, JsonElement, string> symbol)
        { _reader = reader; _symbol = symbol; }

        internal object[] Build(List<Dictionary<string, object>> entries)
        {
            // These are parser rules, not authored game entries or a second numeric ledger. Each rule must
            // resolve its exact members and discriminating tokens in the current tree before making a claim.
            Add("approach", "常规移动面向目标接近", "移动", "面向目标，接近至期望距离；到位后完成本段。",
                Rule(Dev + "CharacterTestTacticalDistanceArrivalHint.cs", "TryParse", "ApproachTarget", "Arrived", "TurnToTarget"),
                Rule(Dev + "CharacterTestTacticalApproachActionSplit.cs", "Apply", "ApproachTitle", "Completion = \"Arrived\""));
            Add("keep-distance", "常规移动面向目标守距", "移动", "面向目标，在本段持续时间内保持配置的距离。",
                Rule(Dev + "CharacterTestTacticalKeepDistanceHint.cs", "TryParse", "MaintainDistance", "TurnToTarget", "SimulationEnd"));
            Add("move-away", "常规移动面向目标后撤（远离）", "移动", "面向目标，以常规移动持续远离目标。",
                Rule(Dev + "CharacterTestTacticalBlockDescription.cs", "CharacterTestTacticalRetreatHint", "AvoidHazard", "TurnToTarget", "SimulationEnd"));
            Add("retreat-to-distance", "常规移动面向目标退至期望距离", "移动", "面向目标，后撤至期望距离；到位后完成本段。",
                Rule(Visual, "CreateRetreatToDistance", "RetreatWhenClose", "Arrived"),
                Rule(Dev + "CharacterTestTacticalDistanceArrivalHint.cs", "TryParse", "RetreatWhenClose", "CompletionEventId"));
            Add("orbit", "常规移动面向目标环绕", "移动", "面向目标，按配置距离和方向环绕。",
                Rule(Dev + "CharacterTestTacticalOrbitHint.cs", "TryParse", "OrbitTarget", "TurnToTarget", "SimulationEnd"));
            Add("facing-target", "面向目标", "朝向", "转向当前存活目标，等待朝向到位，受本段独立上限约束。",
                Rule(Visual, "CreateFacingTarget", "TurnToTarget", "WaitForCondition", "Completion = \"Facing\""),
                Rule(Visual, "CreateFacingTargetDefinition", "Revision"));
            Add("facing-action", "面向目标出招", "动作", "面向目标，按动作槽和出招条件发动装配动作。",
                Rule(Dev + "CharacterTestTacticalApproachActionHint.cs", "TryParseActionBlock", "CharacterAction", "TurnToTarget", "RootDistanceRange", "TargetFacing"),
                Rule(Dev + "CharacterTestTacticalApproachActionSplit.cs", "Apply", "ActionTitle", "Completion = \"Action\""));
            Add("forward-step", "面向目标向前垫步", "动作", "尝试面向目标，到位或准备超时后按当前朝向向前垫步一次。",
                Rule(Visual, "CreateFacingForwardStep", "stepForward", "ContinueToActionOnPreparationTimeout = true"),
                Rule(Dev + "CharacterTestTacticalCardInputs.cs", "TryResolveStepForward", "CommonStepForwardActionId"));
            Add("step-back", "Step 后撤", "动作", "通过 stepBack 功能动作槽执行公共后撤动作；这是模板中的动作组件。",
                Rule(Dev + "CharacterTestTacticalCardInputs.cs", "TryResolveStepBack", "CommonStepBackActionId", "CharacterActionCategory.Dodge"),
                Rule(Dev + "CharacterTestTacticalCardInputs.cs", "ResolveSlot", "stepBack", "StepBackAction"),
                Rule(Dev + "CharacterTestTacticalAuthoringSource.cs", "CharacterTestTacticalAuthoringSlot", "stepBack", "IsFunctional"));
            Add("wait-condition", "等待条件", "等待", "等待指定条件成立后继续后续行为；r2 使用被动等待，保留捕获窗口结束屏障。",
                Rule(Wait, "CreateWaitOpportunity", "TargetCurrentAttackWindowEnded", "PassiveWait = true", "Completion = \"Condition\""),
                Rule(Wait, "CreateWaitOpportunityDefinition", "Revision = 2"));
            Add("ordinary-guard", "普通防御", "动作", "以角色的普通防御动作维持防御；有效格挡窗口由动作拥有。",
                Rule(Visual, "CreateOrdinaryGuardDefinition", "ordinaryGuard", "MaximumGuardFrames", "Revision = 2"),
                Rule(Dev + "CharacterTestTacticalBlockDescription.cs", "TryOrdinaryGuardText", "TryValidateOrdinaryGuardAction"));
            Add("wait-two-seconds", "原地等待两秒", "等待", "等待固定两秒；时长由源码的模拟 tick rate 表达式生成。",
                Rule(Wait, "CreateWaitTwoSeconds", "DefaultTickRate * 2", "\"Delay\""),
                Rule(Wait, "CreatePurposeWait", "PassiveWait = true", "WaitForCondition"),
                Rule("Assets/Scripts/WGameFrameworkProject/CharacterTest/Runtime/World/CharacterTestCharacterEntity.cs", "DefaultTickRate", "const int"));
            Add("wait-target-distance", "等待目标进入指定距离", "等待", "等待目标进入设计距离。支持显式固定距离或已有引用距离，参数由制作侧提供。",
                Rule(Wait, "CreateWaitTargetDistance", "\"Fixed\"", "ValidateDistanceData"),
                Rule(Wait, "CreateWaitTargetDistanceReference", "SetDistanceWaitGate"),
                Rule(Wait, "SetDistanceWaitGate", "IntentDistanceRange"),
                Rule(Wait, "CreatePurposeWait", "PassiveWait = true", "RequireLivingTarget"));
            Add("selected-point", "选点移动", "移动", "每次执行选择世界点，按制作规则检查范围、可达性、到达与上限。",
                Rule(Dev + "CharacterTestTacticalAuthoringTemplates.cs", "CreatePointMove", "contract.Freeze()", "ExecutionInput = contract"),
                Rule(Card + "TacticalPointMoveRules.cs", "TacticalPointMoveAuthoring", "WorldPoint", "OwnedMoveAccepted", "RequireReachable"),
                Rule(Card + "CharacterTestTacticalPointNavigation.cs", "TryResolvePointMove", "_hasDestination"));
            Add("ground-traction", "定点牵引", "机制", "冻结范围内直线净空的地面点，将自己牵引过去，遇阻即停；不绑定动作。",
                Rule(Card + "TacticalPointMoveRules.cs", "TacticalPointMoveAuthoring", "GroundTraction", "OwnedTractionAccepted", "TractionSpeedMillimetersPerSecond"),
                Rule(Card + "CharacterTestTacticalExecutionAdapter.Traction.cs", "TickTraction", "TractionCommand", "_commands.Enqueue"),
                Rule("Assets/Scripts/WGameFrameworkProject/CharacterTest/Runtime/World/CharacterTestGroundPointTraction.cs", "TryStep", "MaximumFrames", "StopMillimeters"));
            foreach (string operation in new[] { "Record", "Return" })
                Add(operation == "Record" ? "position-recall-record" : "position-recall-return", operation == "Record" ? "记录回返点" : "返回记录点", "机制",
                    operation == "Record" ? "冻结自己的当前位置，按模拟帧计时，可成功返回一次。" : "瞬间返回自己的有效记录点；落点被占据时保留记录。",
                    Rule(Dev + "CharacterTestTacticalAuthoringTemplates.PositionRecall.cs", "CreatePositionRecall", "contract.Freeze()", "PositionRecall = contract"),
                    Rule(Card + "TacticalPositionRecallRules.cs", "TacticalPositionRecallAuthoring", "Record", "Return", "LifetimeFrames"),
                    Rule(Card + "CharacterTestTacticalExecutionAdapter.PositionRecall.cs", "TickRecall", "RecallCommand", "_commands.Enqueue"));
            foreach (string mode in new[] { "arrive", "maintain" })
                Add("behind-target-" + mode, mode == "arrive" ? "绕至目标背后" : "绕至并保持在目标背后", "移动",
                    mode == "arrive" ? "沿较短环绕弧进入目标背后范围，到达后完成。" : "沿较短环绕弧进入目标背后范围，到位停步；目标移动或转向后再调整。",
                    Rule(Visual, "CreateBehindTargetDefinition", "maintain ? \"Timed\" : \"Arrived\"", "BehindTarget = rear", "maintain ? \"maintain\" : \"arrive\""),
                    Rule(Dev + "CharacterTestRuntimeAiLabMovementResolver.cs", "CharacterTestBehindTargetResolver", "Arrived", "Resume", "DirectionSwitchHysteresis"),
                    Rule(Dev + "CharacterTestRuntimeAiLabMovementResolver.cs", "CharacterTestBehindTargetPreview", "TrySelectInitialMovement", "Create"));

            foreach (var entry in entries)
            {
                var uses = new List<object>();
                if ((string)entry["kind"] == "shared-source")
                {
                    JsonElement source = F((JsonElement)entry["raw"], "source");
                    foreach (var match in Matches(source))
                    {
                        var component = match.component; var timeline = match.timeline; var block = match.block;
                        string[] ids = A(block, "eventIds").Select(v => v.GetString()).ToArray();
                        var use = D("entryKey", entry["entryKey"], "name", entry["name"], "definitionId", entry["id"], "revision", entry["revision"],
                            "source", entry["source"], "sourceHash", entry["sourceHash"], "timelineId", S(timeline, "timelineId"), "block", block,
                            "events", A(timeline, "events").Where(v => ids.Contains(S(v, "eventId"))).ToArray(), "slots", O(F(source, "slots")),
                            "usedBy", entry["usedBy"], "scope", "structural block match; not a runtime or publication receipt");
                        ((List<object>)component["instances"]).Add(use);
                        uses.Add(D("entryKey", component["entryKey"], "name", component["name"], "blockId", S(block, "blockId")));
                    }
                }
                entry["componentUses"] = uses;
            }
            return _components.Cast<object>().ToArray();
        }

        internal object[] ClassifySource(JsonElement source) => Matches(source).Select(match => (object)D(
            "key", match.component["entryKey"], "name", match.component["name"], "category", match.component["category"],
            "blockId", S(match.block, "blockId"), "basis", "current-source-component-structural-decoder")).ToArray();

        private IEnumerable<(Dictionary<string, object> component, JsonElement timeline, JsonElement block)> Matches(JsonElement source)
        {
            if (S(source, "schema") != "mx.character-test.tactical-card-source.v4") yield break;
            Dictionary<string, object> Find(string key) => _components.FirstOrDefault(c => (string)c["key"] == key && (string)c["implementation"] == "main-source");
            var recall = F(source, "positionRecall");
            string operation = S(recall, "operation");
            if ((operation == "Record" && N(recall, "lifetimeFrames") > 0 && N(recall, "lifetimeFrames") <= 3600
                || operation == "Return" && N(recall, "lifetimeFrames") == 0) && A(source, "slots").Length == 0
                && A(source, "timelines").All(t => A(t, "events").Length == 0 && A(t, "eventBlocks").Length == 0)
                && Find(operation == "Record" ? "position-recall-record" : "position-recall-return") is var componentRecall && componentRecall != null)
                yield return (componentRecall, default, default);
            var input = F(source, "executionInput");
            if (S(input, "kind") == "WorldPoint" && S(input, "rangeCenter") == "Execution" && S(input, "startPolicy") == "OwnedMoveAccepted"
                && F(input, "requireReachable").ValueKind == JsonValueKind.True && N(input, "rangeMillimeters") > 0
                && N(input, "arrivalMillimeters") > 0 && N(input, "maximumFrames") > 0 && N(input, "speedMilli") > 0
                && A(source, "timelines").All(t => A(t, "events").Length == 0) && Find("selected-point") is var point && point != null)
                yield return (point, default, default);
            if (S(input, "kind") == "WorldPoint" && S(input, "mechanism") == "GroundTraction"
                && S(input, "rangeCenter") == "Execution" && S(input, "startPolicy") == "OwnedTractionAccepted"
                && F(input, "requireReachable").ValueKind == JsonValueKind.True && N(input, "rangeMillimeters") > 0
                && N(input, "arrivalMillimeters") > 0 && N(input, "maximumFrames") > 0
                && N(input, "tractionSpeedMillimetersPerSecond") > 0 && N(input, "tractionAccelerationMillimetersPerSecondSquared") > 0
                && A(source, "slots").Length == 0 && A(source, "timelines").All(t => A(t, "events").Length == 0)
                && Find("ground-traction") is var traction && traction != null)
                yield return (traction, default, default);
            foreach (var timeline in A(source, "timelines")) foreach (var block in A(timeline, "eventBlocks"))
            {
                var component = Find(RecognizeBlock(source, timeline, block));
                if (component != null) yield return (component, timeline, block);
            }
        }

        private static string[] Rule(string path, string member, params string[] tokens) => new[] { path, member }.Concat(tokens).ToArray();

        private void Add(string key, string name, string category, string summary, params string[][] rules)
        {
            var sources = new List<object>(); var issues = new List<string>();
            foreach (string[] rule in rules)
            {
                try
                {
                    var evidence = ReadMember(rule[0], rule[1]); string code = (string)evidence["excerpt"];
                    foreach (string token in rule.Skip(2)) if (!code.Contains(token)) throw new InvalidDataException("MEMBER_CONTRACT_CHANGED: " + rule[1] + ":" + token);
                    if (key == "wait-two-seconds" && rule[1] == "DefaultTickRate")
                    {
                        var tickRate = Regex.Match(code, @"=\s*(\d+)\s*;");
                        if (!tickRate.Success || !int.TryParse(tickRate.Groups[1].Value, out int rate) || rate <= 0 || rate > int.MaxValue / 2)
                            throw new InvalidDataException("DEFAULT_TICK_RATE_NOT_PARSED");
                        _twoSecondFrames = rate * 2;
                    }
                    sources.Add(evidence);
                }
                catch (Exception error) when (CombatDesignSourceReader.ReadError(error))
                { issues.Add(error.Message); _reader.Diagnostic("COMPONENT_SOURCE_UNAVAILABLE", key + ":" + error.Message); }
            }
            // No fallback to an old report, fixture or name-based classification when current source is absent.
            bool current = issues.Count == 0;
            var entry = D("key", key, "entryKey", "component/" + key, "name", name, "category", category,
                "kind", "intent-component", "implementation", current ? "main-source" : "needs-review",
                "summary", current ? summary : "当前源码契约缺失或变化，停止解释，待复核。",
                "sources", sources, "issues", issues, "instances", new List<object>(),
                "availability", "主树源码可见；未据此推断正式卡发布、角色装配或玩家牌库可用性。",
                "origin", "fixed-component-source-decoder.v1", "evidence", "source-inspection-only");
            if (key == "step-back" && current)
            {
                var constant = ReadMember(Dev + "CharacterTestTacticalCardInputs.cs", "CommonStepBackActionId"); sources.Add(constant);
                Match id = Regex.Match((string)constant["excerpt"], @"=\s*(\d+)\s*;");
                if (id.Success)
                {
                    const string path = "Assets/MxFrameworkGenerated/CharacterActions/Libraries/Common/character_actions.json";
                    int actionId = int.Parse(id.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture);
                    JsonElement action = A(_reader.Json(path), "actions").FirstOrDefault(a => N(a, "actionId") == actionId);
                    entry["action"] = D("source", path, "sourceHash", _reader.Hashes[path], "data", O(action));
                }
            }
            if (key == "behind-target-arrive" || key == "behind-target-maintain") entry["factoryMode"] = "maintain = " + (key.EndsWith("maintain", StringComparison.Ordinal) ? "true" : "false");
            _components.Add(entry);
        }

        private Dictionary<string, object> ReadMember(string path, string member)
        {
            string source = _reader.Text(path), mask = Mask(source);
            string declaration = @"(?m)^[ \t]*(?:public|internal|private|protected)\s+(?:(?:static|readonly|sealed|partial|abstract|const|override|virtual)\s+)*";
            Match match = Regex.Match(mask, declaration + @"(?:class|struct|enum)\s+" + Regex.Escape(member) + @"\b");
            if (!match.Success) match = Regex.Match(mask, declaration + @"[\w.<>?\[\],]+\s+" + Regex.Escape(member) + @"\b(?=\s*[(={;])");
            if (!match.Success) throw new InvalidDataException("MEMBER_NOT_FOUND: " + path + ":" + member);
            int start = match.Index, end = -1, braces = 0, parens = 0, brackets = 0;
            bool expression = false, body = false;
            for (int i = match.Index + match.Length; i < mask.Length; i++)
            {
                char c = mask[i];
                if (c == '=' && i + 1 < mask.Length && mask[i + 1] == '>' && !body) expression = true;
                if (c == '(') parens++; else if (c == ')') parens--;
                if (c == '[') brackets++; else if (c == ']') brackets--;
                if (c == '{') { braces++; body = true; }
                else if (c == '}' && --braces == 0 && body && !expression) { end = i + 1; break; }
                if (c == ';' && braces == 0 && parens == 0 && brackets == 0) { end = i + 1; break; }
            }
            if (end <= start) throw new InvalidDataException("MEMBER_NOT_PARSED: " + path + ":" + member);
            string excerpt = source.Substring(start, end - start).Trim();
            return D("path", path, "member", member, "line", source.Substring(0, start).Count(c => c == '\n') + 1,
                "hash", _reader.Hashes[path], "memberHash", CombatDesignSourceReader.Hash(Encoding.UTF8.GetBytes(excerpt)), "excerpt", excerpt);
        }

        private static string Mask(string source) => Regex.Replace(source,
            "//[^\\r\\n]*|/\\*[\\s\\S]*?\\*/|@\"(?:\"\"|[^\"])*\"|\"(?:\\\\.|[^\"\\\\])*\"|'(?:\\\\.|[^'\\\\])*'",
            m => Regex.Replace(m.Value, @"[^\r\n]", " "));

        private string RecognizeBlock(JsonElement source, JsonElement timeline, JsonElement block)
        {
            string[] ids = A(block, "eventIds").Select(v => v.GetString()).ToArray();
            var events = A(timeline, "events").Where(e => ids.Contains(S(e, "eventId"))).ToArray();
            if (ids.Length == 0 || events.Length != ids.Length) return null;
            string Kind(JsonElement e) => S(F(e, "operation"), "kind");
            string completion = S(block, "completion"); if (completion == "") completion = "Timed";
            JsonElement move = events.FirstOrDefault(e => Kind(e) == "Movement"), face = events.FirstOrDefault(e => Kind(e) == "TurnToTarget");
            bool pair = events.Length == 2 && move.ValueKind == JsonValueKind.Object && face.ValueKind == JsonValueKind.Object;
            bool Empty(JsonElement value) => value.ValueKind == JsonValueKind.Null || value.ValueKind == JsonValueKind.Undefined;
            bool LocalStart(JsonElement e) { var start = F(e, "start"); return S(start, "kind") == "Absolute" && S(start, "space") == "Block" && N(start, "frame", -1) == 0; }
            if (pair && S(F(face, "operation"), "target") == "currentCombatTarget" && events.All(e => Empty(F(e, "conditionRoot")) && S(e, "timingKind") == "Duration"
                && N(e, "maxExecutionsPerPass", 1) == 1 && N(e, "cooldownFrames") == 0 && LocalStart(e)
                && S(F(e, "end"), "kind") == "Anchor" && S(F(e, "end"), "sourceEventId") == S(block, "blockId")
                && S(F(e, "end"), "targetKind") == "Builtin" && S(F(e, "end"), "anchor") == "SimulationEnd" && N(F(e, "end"), "offsetFrames") == 0))
            {
                JsonElement movement = F(F(move, "operation"), "movement");
                if (_symbol("movementTarget", F(movement, "target")) != "CurrentCombatTarget" || !string.IsNullOrEmpty(S(block, "successConditionEventId"))) return null;
                string strategy = _symbol("movement", F(movement, "strategy"));
                var rear = F(movement, "behindTarget");
                if (!Empty(rear))
                {
                    if (strategy != "OrbitTarget" || rear.ValueKind != JsonValueKind.Object) return null;
                    if (F(rear, "maintain").ValueKind != JsonValueKind.True && F(rear, "maintain").ValueKind != JsonValueKind.False) return null;
                    if (new[] { "halfAngleDegrees", "resumeHalfAngleDegrees", "distanceTolerance", "resumeDistanceTolerance", "directionSwitchHysteresisDegrees" }
                        .Any(field => F(rear, field).ValueKind != JsonValueKind.Number)) return null;
                    bool maintain = F(rear, "maintain").ValueKind == JsonValueKind.True;
                    if (maintain) return completion == "Timed" && S(block, "completionEventId") == "" ? "behind-target-maintain" : null;
                    return completion == "Arrived" && S(block, "completionEventId") == S(move, "eventId") ? "behind-target-arrive" : null;
                }
                if (completion == "Arrived" && S(block, "completionEventId") == S(move, "eventId"))
                    return strategy == "ApproachTarget" ? "approach" : strategy == "RetreatWhenClose" ? "retreat-to-distance" : null;
                if (completion != "Timed") return null;
                return strategy == "MaintainDistance" ? "keep-distance" : strategy == "OrbitTarget" ? "orbit"
                    : strategy == "AvoidHazard" && S(movement, "distanceSource") == "Manual" && S(movement, "actionSlotId") == "" ? "move-away" : null;
            }
            JsonElement action = events.FirstOrDefault(e => Kind(e) == "CharacterAction"), gate = events.FirstOrDefault(e => Kind(e) == "WaitForCondition");
            string resolver = S(A(source, "slots").FirstOrDefault(s => S(s, "slotId") == S(F(action, "operation"), "slotId")), "resolver");
            bool living = F(block, "requireLivingTarget").ValueKind == JsonValueKind.True;
            bool passive = F(block, "passiveWait").ValueKind == JsonValueKind.True;
            if (completion == "Facing" && events.Length == 2 && living && Kind(face) == "TurnToTarget"
                && S(F(face, "operation"), "target") == "currentCombatTarget" && Kind(gate) == "WaitForCondition"
                && S(block, "completionEventId") == S(gate, "eventId")) return "facing-target";
            if (completion == "Action" && Kind(action) == "CharacterAction" && S(block, "completionEventId") == S(action, "eventId"))
            {
                if (events.Length == 1 && resolver == "ordinaryGuard" && N(block, "maximumGuardFrames") > 0) return "ordinary-guard";
                if (events.Length == 1 && resolver == "stepBack") return "step-back";
                if (S(F(face, "operation"), "target") == "currentCombatTarget")
                {
                    if (events.Length == 3 && resolver == "stepForward" && Kind(gate) == "WaitForCondition" && living
                        && N(block, "preparationTimeoutFrames") > 0 && N(block, "completionTimeoutFrames") > 0) return "forward-step";
                    if (events.Length == 2 && resolver != "stepBack" && resolver != "stepForward" && resolver != "ordinaryGuard"
                        && events.All(LocalStart) && !Empty(F(action, "conditionRoot"))) return "facing-action";
                }
            }
            if (events.Length == 1 && Kind(gate) == "WaitForCondition" && S(block, "completionEventId") == S(gate, "eventId")
                && LocalStart(gate) && S(gate, "timingKind") == "Instant" && N(gate, "maxExecutionsPerPass", 1) == 1 && N(gate, "cooldownFrames") == 0)
            {
                if (completion == "Delay" && passive && !living && Empty(F(gate, "conditionRoot")) && _twoSecondFrames > 0
                    && N(F(block, "timing"), "minimumFrames") == _twoSecondFrames && N(F(block, "timing"), "durationFrames") == _twoSecondFrames)
                    return "wait-two-seconds";
                if (completion == "Condition" && living && passive && ContainsFact(F(gate, "conditionRoot"), "TargetCurrentAttackWindowEnded")) return "wait-condition";
                if (completion == "Distance" && living && passive && A(source, "predicateBindings").Any(b => S(b, "kind") == "IntentDistanceRange")) return "wait-target-distance";
            }
            // Other instances remain explicit shared-source references. Do not guess component semantics
            // from a definition name, an input IntentId or a complete multi-block tactical template.
            return null;
        }

        private bool ContainsFact(JsonElement node, string fact) => _symbol("fact", F(F(node, "predicate"), "fact")) == fact
            || A(node, "children").Any(child => ContainsFact(child, fact));
    }
}
