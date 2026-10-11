using System;
using System.Collections.Generic;
using System.Linq;
using System.Globalization;
using MxFramework.UI;

namespace MxFramework.Game.CharacterTest
{
    internal sealed class CharacterTestTacticalDslDescription
    {
        internal string Text { get; private set; }
        internal string Details { get; private set; }
        internal string[] Warnings { get; private set; }
        internal bool Executable { get; private set; }
        internal string CompilationIssue { get; private set; }
        internal string DependencyIdentity { get; private set; }
        internal CharacterTestTacticalApproachActionHint[] Hints { get; private set; } = Array.Empty<CharacterTestTacticalApproachActionHint>();
        internal CharacterTestTacticalDescriptionSegment[] Segments { get; private set; } = Array.Empty<CharacterTestTacticalDescriptionSegment>();
        internal bool HasStructuredDescription { get; private set; }
        internal CharacterTestTacticalDescriptionFlow DescriptionFlow { get; private set; }
        internal CharacterTestTacticalUtilityFace UtilityFace { get; private set; }

        internal static CharacterTestTacticalDslDescription GenerateInteractionPreparation(TacticalCard.InteractionPreparationPayload payload)
            => new CharacterTestTacticalDslDescription {
                Text = TacticalCard.TacticalInteractionTrialCards.Description(payload.Effect),
                UtilityFace = CharacterTestTacticalUtilityFace.Create(payload),
                Details = "试用配置：消耗5动能。F帧确认，F+1至F+600帧有效；暂停不计时。每角色同类每回合一次；确认后移入耗尽区，不补牌、不启动行动。",
                Warnings = new[] { "不同类可共存；本回合结束、死亡或解绑清理。派生伤害不触发准备效果。格挡、闪避、无敌、零基础伤害不算有效命中；护甲吸收至零仍算有效命中。返还饱和仍消费。失败不退款。" }, Executable = true };

        internal static CharacterTestTacticalDslDescription GeneratePositionRecall(TacticalCard.TacticalPositionRecallRules rules,
            double frameSeconds = 1d / CharacterTestDeterministicUnits.DefaultTickRate)
        {
            bool record = rules.Operation == CharacterTestPositionRecallOperation.Record;
            return new CharacterTestTacticalDslDescription {
                Text = record ? "记录当前位置为回返点。" : "瞬间返回自己的有效记录点。",
                Details = record ? "冻结当前世界位置；覆盖自己的旧记录。有效 " + rules.LifetimeFrames + " 个模拟帧（当前约 "
                    + (rules.LifetimeFrames * frameSeconds).ToString("0.###", CultureInfo.InvariantCulture) + " 秒），暂停时不计时，可成功返回一次。"
                    : "仅返回原记录点，不替换相邻落点。回点被占据或地面失效时拒绝并保留尚有效记录；成功后消费记录并停止当前移动。",
                Warnings = new[] { "仅自由且有地面支撑时使用；运行中动作、活动牵引及待执行外力拒绝。实际记录或返回成功才消耗动势；失败释放预留。死亡、场景或战斗结束清理记录。无需动作槽。" },
                Executable = true, CompilationIssue = string.Empty, DependencyIdentity = string.Empty };
        }
        internal static CharacterTestTacticalDslDescription GenerateGroundTraction(TacticalCard.TacticalPointMoveRules rules,
            double frameSeconds = 1d / CharacterTestDeterministicUnits.DefaultTickRate)
        {
            string M(int n) => (n / 1000d).ToString("0.###", CultureInfo.InvariantCulture);
            return new CharacterTestTacticalDslDescription {
                Text = "将自己牵引至本次选定的地面点，遇阻即停。",
                Details = "冻结 " + M(rules.Input.RadiusMillimeters) + " 米范围内、直线净空的地面落点；以 "
                    + M(rules.TractionAccelerationMillimetersPerSecondSquared) + " 米/秒²加速至 "
                    + M(rules.TractionSpeedMillimetersPerSecond) + " 米/秒，距落点 " + M(rules.ArrivalMillimeters)
                    + " 米停止。执行上限 " + (rules.MaximumFrames * frameSeconds).ToString("0.###", CultureInfo.InvariantCulture)
                    + " 秒；受阻、取消、失去地面支撑或受控中断后结束，只释放自身牵引。",
                Warnings = new[] { "实际牵引位移开始时消耗动势；开始前失败释放预留，开始后结束保留消耗。无需动作槽；不产生物理动作冷却。" },
                Executable = true, CompilationIssue = string.Empty, DependencyIdentity = string.Empty };
        }

        internal static CharacterTestTacticalDslDescription Generate(CharacterTestTacticalDslSource source, CharacterTestTacticalCardInputs inputs,
            CharacterTestTacticalDslCompilation prepared = null, bool attempted = false, string preparationIssue = null,
            bool cardFace = false, IReadOnlyDictionary<string, string> selectedActions = null)
            => GenerateLocalized(source, inputs, prepared, attempted, preparationIssue, cardFace, selectedActions);

        /// <remarks>LowFreqAlloc. UI-only entry; source compilation and editor callers retain the locale-independent Generate contract.</remarks>
        internal static CharacterTestTacticalDslDescription GenerateLocalized(CharacterTestTacticalDslSource source, CharacterTestTacticalCardInputs inputs,
            CharacterTestTacticalDslCompilation prepared = null, bool attempted = false, string preparationIssue = null,
            bool cardFace = false, IReadOnlyDictionary<string, string> selectedActions = null, IMxUiTextProvider textProvider = null,
            double frameSeconds = 1d / CharacterTestDeterministicUnits.DefaultTickRate)
        {
            try { return GenerateCore(source, inputs, prepared, attempted, preparationIssue, cardFace, selectedActions, textProvider, frameSeconds); }
            catch (Exception error) when (error is ArgumentException || error is InvalidOperationException
                || error is Newtonsoft.Json.JsonException || error is NullReferenceException)
            {
                string issue = preparationIssue;
                if (!attempted) CharacterTestTacticalDslCompiler.TryCompile(source, inputs, out _, out issue);
                return new CharacterTestTacticalDslDescription { Text = "当前源结构不完整，暂时无法生成可靠说明。", Details = string.Empty,
                    Warnings = new[] { "说明结构诊断：" + error.Message }, Executable = false,
                    CompilationIssue = issue, DependencyIdentity = string.Empty };
            }
        }

        private static CharacterTestTacticalDslDescription GenerateCore(CharacterTestTacticalDslSource source, CharacterTestTacticalCardInputs inputs,
            CharacterTestTacticalDslCompilation prepared, bool attempted, string preparationIssue,
            bool cardFace, IReadOnlyDictionary<string, string> selectedActions, IMxUiTextProvider textProvider, double frameSeconds)
        {
            var warnings = new List<string>();
            var hints = new List<CharacterTestTacticalApproachActionHint>();
            var segments = new List<CharacterTestTacticalDescriptionSegment>();
            var structuredNodes = new HashSet<string>(StringComparer.Ordinal);
            if (inputs?.UsesDefaultSummary == true)
                warnings.Add("当前距离推荐借用默认角色 " + inputs.SummarySourceCharacterId + " 的同动作测量摘要，尚非当前装配的实测结果。");
            var compiled = prepared; string issue = preparationIssue;
            bool valid = attempted ? compiled != null : CharacterTestTacticalDslCompiler.TryCompile(source, inputs, out compiled, out issue);
            bool bound = inputs != null && (inputs.Action != null || inputs.HasExplicitBindings) && (attempted && compiled != null || inputs.TryValidatePreparedAction(out _));
            if (!bound && (CharacterTestTacticalDependencies.Analyze(source) & CharacterTestTacticalInputRequirements.PrimaryAction) != 0) warnings.Add("当前动作绑定不可用，说明未使用旧动作名称。");
            string action = inputs?.Action?.DisplayName ?? "槽内动作";
            var definitions = source.Definitions.ToDictionary(d => d.DefinitionId, StringComparer.Ordinal);
            var terminalDefinitions = new HashSet<string>(StringComparer.Ordinal);
            var terminalNodes = new HashSet<string>(StringComparer.Ordinal);
            var eventCache = new Dictionary<string, CharacterTestTacticalAuthoringEvent[]>(StringComparer.Ordinal);
            var gatePhrases = new Dictionary<string, string>(StringComparer.Ordinal);
            var simultaneousGateDefinitions = new HashSet<string>(StringComparer.Ordinal);
            string Number(double value) => value.ToString("0.###", CultureInfo.InvariantCulture);
            string SlotPath(string slot, string instance) => instance == "legacy-use" ? slot : instance + "/" + slot;
            CharacterTestTacticalCardInputs SlotInput(string slot, string instance)
            {
                try { return inputs?.SlotInputs(SlotPath(slot, instance)); }
                catch (InvalidOperationException) { return null; }
            }

            CharacterTestTacticalAuthoringEvent[] ReachableEvents(CharacterTestTacticalSharedDefinition definition, string nodeId = null)
            {
                string key = inputs?.HasExplicitBindings == true && nodeId != null ? nodeId : definition.DefinitionId;
                if (eventCache.TryGetValue(key, out var retained)) return retained;
                var events = definition.Source.Timelines.SelectMany(t => t.Events).ToArray();
                CharacterTestTacticalAuthoringCompilation ready = null;
                compiled?.SharedCompilations.TryGetValue(key, out ready);
                if (ready == null && !attempted) CharacterTestTacticalAuthoringCompiler.TryCompileSharedTrial(definition.Source, inputs, out ready, out _);
                if (ready != null)
                {
                    var positions = ready.Events.ToArray().ToDictionary(e => e.EventId, e => e.StartFrame, StringComparer.Ordinal);
                    if (events.Any(g => g.Operation.Kind == "WaitForCondition" && events.Any(a => a.Operation.Kind == "CharacterAction"
                        && positions[g.EventId] == positions[a.EventId]))) simultaneousGateDefinitions.Add(definition.DefinitionId);
                    int cutoff = events.Where(e => e.Operation.Kind == "ReturnToPlanner" && e.ConditionRoot == null)
                        .Select(e => positions[e.EventId]).DefaultIfEmpty(int.MaxValue).Min();
                    if (cutoff != int.MaxValue) events = events.Where(e => positions[e.EventId] < cutoff
                        || positions[e.EventId] == cutoff && e.Operation.Kind == "ReturnToPlanner").ToArray();
                }
                eventCache.Add(key, events); return events;
            }

            string Condition(CharacterTestRuntimeAiLabBehaviorRecipeConditionNode node, CharacterTestTacticalAuthoringSource shared = null, string instance = null)
            {
                if (node?.MovementQuery != null) return "移动 " + node.SourceEventId + (node.MovementQuery == "Arrived" ? " 达到所配距离" : " 的目标失效");
                if (node?.Kind == CharacterTestRuntimeAiLabBehaviorRecipeConditionNodeKind.EventSignal)
                    return node.Signal == CharacterTestRuntimeAiLabBehaviorRecipeEventSignal.CommitFailed
                        ? "指定动作本次提交失败（没有出招）"
                        : "指定事件结果为“" + (node.Signal == CharacterTestRuntimeAiLabBehaviorRecipeEventSignal.Completed ? "完成"
                            : node.Signal == CharacterTestRuntimeAiLabBehaviorRecipeEventSignal.Failed ? "失败"
                            : node.Signal == CharacterTestRuntimeAiLabBehaviorRecipeEventSignal.Rejected ? "拒绝" : "取消") + "”";
                if (node == null) return "无额外条件";
                if (node.Kind == CharacterTestRuntimeAiLabBehaviorRecipeConditionNodeKind.SubjectScope)
                    return Condition(node.Children[0], shared, instance);
                if (node.Kind == CharacterTestRuntimeAiLabBehaviorRecipeConditionNodeKind.All || node.Kind == CharacterTestRuntimeAiLabBehaviorRecipeConditionNodeKind.Any)
                    return "（" + string.Join(node.Kind == CharacterTestRuntimeAiLabBehaviorRecipeConditionNodeKind.All ? "且" : "或", node.Children.Select(c => Condition(c, shared, instance))) + "）";
                if (node.Kind == CharacterTestRuntimeAiLabBehaviorRecipeConditionNodeKind.Not) return "不满足“" + Condition(node.Children[0], shared, instance) + "”";
                var binding = shared?.PredicateBindings.SingleOrDefault(b => b.NodeId == node.NodeId);
                if (binding != null)
                {
                    if (binding.Kind == "MovementArrived") return "达到移动事件配置的距离";
                    if (binding.Kind == "TurnAligned") return "满足转向事件引用的面向参数";
                    if (cardFace)
                    {
                        var evt = shared.Timelines.SelectMany(t => t.Events).FirstOrDefault(e => e.EventId == binding.SourceActionEventId);
                        var summary = SlotInput(evt?.Operation.SlotId, instance)?.SelectedRange;
                        if (binding.Kind == "RootDistanceRange") return summary?.MinDistance != null && summary.MaxDistance != null
                            ? Number(summary.MinDistance.Value) + "～" + Number(summary.MaxDistance.Value) + "米"
                                + (summary.IgnoresFurtherIntervals ? "（仅第一段，不采用远段）" : "") : "攻击距离待准备";
                        if (binding.Kind == "TargetFacing") return summary == null ? "朝向数据待准备"
                            : (summary.Sector.CenterMilliDegrees == 0 ? "正前方" : Number(summary.Sector.CenterMilliDegrees / 1000d) + "°方向")
                                + "±" + Number(summary.Sector.ToleranceMilliDegrees / 1000d) + "°";
                    }
                    if (binding.Kind == "RootDistanceRange") return "距离适合攻击";
                    if (binding.Kind == "TargetFacing") return "朝向适合攻击";
                    if (binding.Kind == "BoundActionOpportunity") { warnings.Add("旧动作机会条件已停用，需要删除：" + node.NodeId); return "已停用的动作机会条件"; }
                }
                var fact = node.Predicate;
                if (fact?.Fact == CharacterTestRuntimeAiLabCardConditionFact.TargetState
                    && fact.Operator == CharacterTestRuntimeAiLabCardConditionOperator.Equals
                    && fact.AllowedTargetPhases == (uint)CharacterTestRuntimeAiPlannerTargetStateMask.Alive
                    && fact.RequiredTargetFlags == 0 && fact.ForbiddenTargetFlags == 0) return "当前有存活目标";
                if (fact?.Fact == CharacterTestRuntimeAiLabCardConditionFact.SelfHealthRatio
                    && fact.Operator == CharacterTestRuntimeAiLabCardConditionOperator.LessOrEqual)
                    return "自身血量不高于" + (fact.Value * 100).ToString("0.#", System.Globalization.CultureInfo.InvariantCulture) + "%";
                if (fact?.Fact == CharacterTestRuntimeAiLabCardConditionFact.TargetRootDistance
                    && (fact.Operator == CharacterTestRuntimeAiLabCardConditionOperator.GreaterThan || fact.Operator == CharacterTestRuntimeAiLabCardConditionOperator.LessOrEqual))
                    return "与当前目标距离" + (fact.Operator == CharacterTestRuntimeAiLabCardConditionOperator.GreaterThan ? "大于" : "不超过")
                        + fact.Value.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture) + "米";
                warnings.Add("部分条件尚不能准确概括：" + node.NodeId);
                return "另有已配置条件（说明未完整解析）";
            }

            string Shared(string id, string instance)
            {
                if (!definitions.TryGetValue(id, out var definition)) { warnings.Add("共享定义缺失：" + id); return "部分行为不可解析"; }
                var events = ReachableEvents(definition, instance);
                var phrases = new List<string>();
                foreach (var evt in events)
                {
                    string phrase;
                    switch (evt.Operation.Kind)
                    {
                        case "CharacterAction":
                            var resolver = definition.Source.Slots.FirstOrDefault(s => s.SlotId == evt.Operation.SlotId)?.Resolver;
                            phrase = inputs?.HasExplicitBindings == true || resolver == "stepForward" || resolver == "stepBack" ? "尝试执行“" + BoundActionName(definition, evt, instance) + "”"
                                : "尝试施放“" + action + "”"; break;
                        case "Movement": phrase = (cardFace ? "" : "尝试") + MovementPhrase(evt, instance); break;
                        case "ApproachOptimalRange": phrase = "调整与当前目标的距离"; break;
                        case "TurnToTarget": phrase = "在允许转向时面向当前目标"; break;
                        case "RetreatFromTarget": phrase = "尝试远离当前目标后撤"; break;
                        case "ReturnToPlanner": phrase = "结束整张卡，后面的内容不再执行"; break;
                        case "CompleteBlock": phrase = "提前结束当前意图段，继续后续行为"; break;
                        case "WaitForCondition":
                            // Bound leaves are described from their declared binding, not invented literals.
                            var nodeIds = new HashSet<string>(StringComparer.Ordinal);
                            void Collect(CharacterTestRuntimeAiLabBehaviorRecipeConditionNode n)
                            { if (n == null) return; nodeIds.Add(n.NodeId); foreach (var c in n.Children ?? new List<CharacterTestRuntimeAiLabBehaviorRecipeConditionNode>()) Collect(c); }
                            Collect(evt.ConditionRoot);
                            var kinds = definition.Source.PredicateBindings.Where(b => nodeIds.Contains(b.NodeId)).Select(b => b.Kind).ToArray();
                            phrase = evt.ConditionRoot == null ? "在等待点停留" : "等待" + Condition(evt.ConditionRoot, definition.Source, instance);
                            if (!cardFace && kinds.Contains("RootDistanceRange") && kinds.Contains("TargetFacing"))
                                phrase += "（包括距离和朝向）";
                            gatePhrases[id + "/" + evt.EventId] = phrase;
                            break;
                        default: phrase = "部分行为说明尚未解析"; warnings.Add("未解析事件：" + evt.EventId); break;
                    }
                    if (evt.ConditionRoot != null && evt.Operation.Kind != "WaitForCondition" && evt.Operation.Kind != "CharacterAction")
                    {
                        phrase = "在“" + Condition(evt.ConditionRoot, definition.Source, instance) + "”成立时" + phrase;
                    }
                    phrases.Add(phrase);
                }
                if (definition.Source.ActivationConditionRoot != null || definition.Source.Timelines.Any(t => t.EntryConditionRoot != null))
                    warnings.Add("共享源含额外入口条件，需核对编译诊断：" + id);
                if (simultaneousGateDefinitions.Contains(id))
                {
                    warnings.Add("等待与动作同帧，Gate 不阻止同帧动作采样：" + id);
                    phrases.Add("等待点不拦截同时开始的动作尝试，动作仍按自己的起手条件判断");
                }
                bool unconditionalExit = events.Any(e => e.Operation.Kind == "ReturnToPlanner" && e.ConditionRoot == null);
                bool conditionalExit = events.Any(e => e.Operation.Kind == "ReturnToPlanner" && e.ConditionRoot != null);
                if (unconditionalExit) terminalDefinitions.Add(id);
                return phrases.Count == 0 ? "无内部行为" : "执行这段行为：" + string.Join("、", phrases)
                    + (unconditionalExit ? "。执行到整卡退出时结束" : conditionalExit ? "。未触发整卡退出时，正常走完整段后继续" : "。正常走完整段后继续");
            }

            string Node(CharacterTestTacticalDslNode node)
            {
                string text;
                switch (node.Kind)
                {
                    case "Reference": text = Shared(node.DefinitionId, node.NodeId); if (terminalDefinitions.Contains(node.DefinitionId)) terminalNodes.Add(node.NodeId); break;
                    case "Exit": terminalNodes.Add(node.NodeId); return "结束本卡，后续内容不再执行";
                    case "If": text = "到这里时，若" + Condition(node.Condition) + "，则执行以下整段：〔" + Sequence(node.Children)
                        + "〕；否则跳过这段。进入后不因该入口条件变化中止这段"; break;
                    case "Wait":
                        text = "等待 ≤ " + (node.MaximumWaitFrames ?? 60) + " 帧，“" + Condition(node.Condition) + "”成立后执行：〔" + Sequence(node.Children) + "〕；超时结束本卡";
                        if (node.Children?.Any(c => terminalNodes.Contains(c.NodeId)) == true) terminalNodes.Add(node.NodeId);
                        break;
                    default: text = Sequence(node.Children); if (node.Children?.Any(c => terminalNodes.Contains(c.NodeId)) == true) terminalNodes.Add(node.NodeId); break;
                }
                if (node.EndCondition != null)
                { terminalNodes.Remove(node.NodeId); text = "〔" + text + "〕。这段进行中，若" + Condition(node.EndCondition)
                    + "，则中止这段并略过段内剩余内容，转而处理后续；已配置的整卡退出仍优先"; }
                return text;
            }
            string Sequence(List<CharacterTestTacticalDslNode> children)
            {
                var phrases = new List<string>();
                foreach (var child in children ?? new List<CharacterTestTacticalDslNode>())
                { phrases.Add(Node(child)); if (terminalNodes.Contains(child.NodeId)) break; }
                return string.Join("。随后，", phrases);
            }
            string activation = source.ActivationConditionRoot == null ? "没有额外整卡启用条件；动作可用时可参与使用。"
                : "整卡在“" + Condition(source.ActivationConditionRoot) + "”成立且动作可用时可参与使用。";
            string details = activation + Node(source.Root) + "。";

            string BoundActionName(CharacterTestTacticalSharedDefinition definition, CharacterTestTacticalAuthoringEvent evt, string instance)
            {
                string functional = definition.Source.Slots.FirstOrDefault(s => s.SlotId == evt.Operation.SlotId)?.Resolver;
                if (functional == "stepForward") return inputs?.StepForwardAction?.DisplayName ?? "前向垫步";
                if (functional == "stepBack") return inputs?.StepBackAction?.DisplayName ?? "后向垫步";
                if (inputs?.HasExplicitBindings == true)
                {
                    string path = instance == "legacy-use" ? evt.Operation.SlotId : instance + "/" + evt.Operation.SlotId;
                    try { var selected = inputs.SlotInputs(path).Action; return string.IsNullOrWhiteSpace(selected.DisplayName) ? "动作 " + selected.ActionId.Value : selected.DisplayName; }
                    catch (InvalidOperationException)
                    {
                        warnings.Add("槽位未就绪：" + path);
                        return selectedActions != null && selectedActions.TryGetValue(path, out var selectedName) ? selectedName : "未装配动作";
                    }
                }
                return definition.Source.Slots.FirstOrDefault(s => s.SlotId == evt.Operation.SlotId)?.Resolver == "stepBack"
                    ? inputs?.StepBackAction?.DisplayName ?? "槽内后撤动作" : action;
            }

            string MovementPhrase(CharacterTestTacticalAuthoringEvent evt, string instance)
            {
                var movement = evt.Operation.Movement;
                if (movement == null) return "移动（配置缺失）";
                if (cardFace && movement.Target == CharacterTestRuntimeAiLabMovementTargetKind.CurrentCombatTarget && movement.UsesDistance)
                {
                    var slot = SlotInput(movement.ActionSlotId, instance);
                    if (movement.DistanceSource != "Manual" && slot?.SpatialSummary == null)
                        return CharacterTestTacticalMovement.Label(movement.Strategy) + "（距离待准备）";
                    var definition = definitions.Values.First(d => d.Source.Timelines.Any(t => t.Events.Contains(evt)));
                    var resolved = movement.Compile(definition.Source,
                        inputs?.HasExplicitBindings == true && instance != "legacy-use" ? inputs.ForInstance(instance) : inputs,
                        evt.Operation.Speed, evt.EventId);
                    string band = Number(resolved.Minimum) + (resolved.Minimum == resolved.Maximum ? "" : "～" + Number(resolved.Maximum));
                    switch (movement.Strategy)
                    {
                        case CharacterTestRuntimeAiLabMovementStrategy.ApproachTarget:
                            return "接近至" + Number(resolved.IsOptimalRange ? resolved.Optimal : resolved.Minimum) + "米";
                        case CharacterTestRuntimeAiLabMovementStrategy.MaintainDistance: return "与目标保持 " + band + " 米距离";
                        case CharacterTestRuntimeAiLabMovementStrategy.RetreatWhenClose: return "距目标小于 " + Number(resolved.Minimum) + " 米时后撤";
                        case CharacterTestRuntimeAiLabMovementStrategy.OrbitTarget:
                            return "在距目标 " + band + " 米处" + (movement.Side > 0 ? "向左" : movement.Side < 0 ? "向右" : "") + "环绕";
                    }
                }
                string phrase = movement.Describe();
                if (movement.DistanceSource == "Manual" || !movement.UsesDistance) return phrase;
                string name = action;
                if (inputs?.HasExplicitBindings == true)
                {
                    string path = instance == "legacy-use" ? movement.ActionSlotId : instance + "/" + movement.ActionSlotId;
                    try
                    {
                        var selected = inputs.SlotInputs(path).Action;
                        name = string.IsNullOrWhiteSpace(selected.DisplayName) ? "动作 " + selected.ActionId.Value : selected.DisplayName;
                    }
                    catch (InvalidOperationException) { name = "未装配动作"; }
                }
                return phrase.Replace("动作槽 " + movement.ActionSlotId, "“" + name + "”");
            }

            string CompactShared(CharacterTestTacticalSharedDefinition definition, CharacterTestTacticalAuthoringEvent[] events, string instance)
            {
                if (events.Length == 1 && events[0].ConditionRoot == null)
                {
                    if (events[0].Operation.Kind == "RetreatFromTarget") return "尝试后撤，远离当前目标";
                    if (events[0].Operation.Kind == "TurnToTarget") return "在动作允许时继续面向当前目标片刻";
                }
                var attacks = events.Where(e => e.Operation.Kind == "CharacterAction").ToArray();
                var gates = events.Where(e => e.Operation.Kind == "WaitForCondition").ToArray();
                var movements = events.Where(e => e.Operation.Kind == "Movement").ToArray();
                if (attacks.Length != 1 || gates.Length > 1 || events.Any(e => e.Operation.Kind != "CharacterAction"
                    && e.Operation.Kind != "WaitForCondition" && e.Operation.Kind != "ApproachOptimalRange" && e.Operation.Kind != "TurnToTarget"
                    && e.Operation.Kind != "Movement")) return null;
                if (movements.Length > 1 || movements.Any(e => e.Operation.Movement == null
                    || e.Operation.Movement.Strategy != CharacterTestRuntimeAiLabMovementStrategy.ApproachTarget
                    || e.Operation.Movement.Target != CharacterTestRuntimeAiLabMovementTargetKind.CurrentCombatTarget
                    || e.Operation.Movement.DistanceSource != "ActionOptimal"
                    || e.Operation.Movement.ActionSlotId != attacks[0].Operation.SlotId)) return null;
                if (events.Any(e => (e.Operation.Kind == "ApproachOptimalRange" || e.Operation.Kind == "TurnToTarget"
                    || e.Operation.Kind == "Movement") && e.ConditionRoot != null)) return null;
                var sharedInputs = inputs?.HasExplicitBindings == true && instance != "legacy-use" ? inputs.ForInstance(instance) : inputs;
                if (!CharacterTestTacticalAuthoringCompiler.TryCompileSharedTrial(definition.Source, sharedInputs, out var ready, out _)) return null;
                var positions = ready.Events.ToArray().ToDictionary(e => e.EventId, e => e.StartFrame, StringComparer.Ordinal);
                if (gates.Length == 1 && positions[gates[0].EventId] >= positions[attacks[0].EventId]) return null;
                if (events.Any(e => positions[e.EventId] > positions[attacks[0].EventId])) return null;
                bool Leaves(CharacterTestRuntimeAiLabBehaviorRecipeConditionNode node, HashSet<string> leaves)
                {
                    if (node == null) return true;
                    if (node.Kind == CharacterTestRuntimeAiLabBehaviorRecipeConditionNodeKind.SubjectScope
                        || node.Kind == CharacterTestRuntimeAiLabBehaviorRecipeConditionNodeKind.All)
                        return node.Children != null && node.Children.All(c => Leaves(c, leaves));
                    if (node.Kind != CharacterTestRuntimeAiLabBehaviorRecipeConditionNodeKind.FactPredicate) return false;
                    int before = warnings.Count; string leaf = Condition(node, definition.Source);
                    if (warnings.Count != before) return false;
                    leaves.Add(leaf); return true;
                }
                var gateLeaves = new HashSet<string>(); var actionLeaves = new HashSet<string>();
                if (gates.Length == 1 && (gates[0].ConditionRoot == null || !Leaves(gates[0].ConditionRoot, gateLeaves))) return null;
                if (gates.Length == 1 ? !actionLeaves.IsSubsetOf(gateLeaves) : actionLeaves.Count > 0) return null;
                bool approach = events.Any(e => e.Operation.Kind == "ApproachOptimalRange");
                bool turn = events.Any(e => e.Operation.Kind == "TurnToTarget");
                string controls = approach && turn ? "调整与当前目标的距离和朝向（较远时先接近）"
                    : approach ? "调整与当前目标的距离（较远时先接近）" : turn ? "在动作允许时面向当前目标" : string.Empty;
                if (movements.Length == 1) controls = turn ? "接近目标，并在动作允许时面向目标" : "接近当前目标";
                string gateText = gateLeaves.SetEquals(new[] { "当前有存活目标", "距离适合攻击", "朝向适合攻击", "当前有攻击机会" })
                    ? "攻击条件合适" : string.Join("、", gateLeaves.OrderBy(value => value, StringComparer.Ordinal));
                if (gateLeaves.SetEquals(new[] { "当前有存活目标", "距离适合攻击", "朝向适合攻击" }))
                    gateText = "目标存活且距离、朝向适合攻击";
                string waiting = gates.Length == 0 ? string.Empty : "等待" + gateText + "后";
                return (controls.Length == 0 ? string.Empty : controls + "，") + waiting + "尝试施放“" + BoundActionName(definition, attacks[0], instance) + "”";
            }

            bool ContainsAttack(CharacterTestTacticalDslNode node)
            {
                if (node.Kind == "Reference") return definitions.TryGetValue(node.DefinitionId, out var definition)
                    && ReachableEvents(definition, node.NodeId).Any(e => e.Operation.Kind == "CharacterAction");
                return node.Children?.Any(ContainsAttack) == true;
            }

            string CardFaceShared(CharacterTestTacticalSharedDefinition definition, CharacterTestTacticalAuthoringEvent[] events, string instance)
            {
                var attacks = events.Where(e => e.Operation.Kind == "CharacterAction").ToArray();
                var gates = events.Where(e => e.Operation.Kind == "WaitForCondition").ToArray();
                if (attacks.Length != 1 || gates.Length > 1 || definition.Source.Timelines.Count != 1
                    || definition.Source.Timelines[0].EntryConditionRoot != null || definition.Source.ActivationConditionRoot != null
                    || simultaneousGateDefinitions.Contains(definition.DefinitionId)
                    || events.Any(e => e.Operation.Kind != "CharacterAction" && e.Operation.Kind != "WaitForCondition"
                        && e.Operation.Kind != "Movement" && e.Operation.Kind != "ApproachOptimalRange" && e.Operation.Kind != "TurnToTarget")) return null;
                bool Leaves(CharacterTestRuntimeAiLabBehaviorRecipeConditionNode condition, List<CharacterTestRuntimeAiLabBehaviorRecipeConditionNode> boundLeaves)
                {
                    if (condition == null) return true;
                    if (condition.Kind == CharacterTestRuntimeAiLabBehaviorRecipeConditionNodeKind.All
                        || condition.Kind == CharacterTestRuntimeAiLabBehaviorRecipeConditionNodeKind.SubjectScope)
                        return condition.Children != null && condition.Children.All(c => Leaves(c, boundLeaves));
                    if (condition.Kind != CharacterTestRuntimeAiLabBehaviorRecipeConditionNodeKind.FactPredicate) return false;
                    var binding = definition.Source.PredicateBindings.FirstOrDefault(b => b.NodeId == condition.NodeId);
                    if (binding != null && (binding.Kind == "RootDistanceRange" || binding.Kind == "TargetFacing"))
                    { boundLeaves.Add(condition); return true; }
                    return Condition(condition, definition.Source, instance) == "当前有存活目标";
                }
                var actionConditions = new List<CharacterTestRuntimeAiLabBehaviorRecipeConditionNode>();
                var gateConditions = new List<CharacterTestRuntimeAiLabBehaviorRecipeConditionNode>();
                if (gates.Length == 1 && (gates[0].ConditionRoot == null || !Leaves(gates[0].ConditionRoot, gateConditions))) return null;
                if (events.Any(e => e.Operation.Kind != "CharacterAction" && e.Operation.Kind != "WaitForCondition" && e.ConditionRoot != null)) return null;
                var controls = new List<string>();
                foreach (var movement in events.Where(e => e.Operation.Kind == "Movement")) controls.Add(MovementPhrase(movement, instance));
                foreach (var approach in events.Where(e => e.Operation.Kind == "ApproachOptimalRange"))
                {
                    var evt = events.FirstOrDefault(e => e.EventId == approach.Operation.SourceActionEventId);
                    var summary = SlotInput(evt?.Operation.SlotId, instance)?.SelectedRange;
                    controls.Add(summary?.OptimalDistance != null ? "接近至" + Number(summary.OptimalDistance.Value) + "米" : "接近目标（距离待准备）");
                }
                if (events.Any(e => e.Operation.Kind == "TurnToTarget")) controls.Insert(0, "面向目标");
                string[] conditions = gateConditions.Select(c => Condition(c, definition.Source, instance)).Distinct().ToArray();
                bool missing = conditions.Any(c => c.Contains("待准备"));
                string gate = conditions.Length == 0 || missing ? "" : "在" + string.Join("、", conditions) + "内，";
                return (controls.Count == 0 ? "" : string.Join("，", controls) + "。") + gate
                    + "使出“" + BoundActionName(definition, attacks[0], instance) + "”"
                    + (missing ? "。\n出手距离与朝向数据待准备" : "");
            }

            string Summary(CharacterTestTacticalDslNode node)
            {
                string text;
                if (node.Kind == "Reference")
                {
                    if (!definitions.TryGetValue(node.DefinitionId, out var definition)) return "部分行为尚不可解析";
                    var events = ReachableEvents(definition, node.NodeId);
                    CharacterTestTacticalAuthoringCompilation sharedCompilation = null;
                    compiled?.SharedCompilations.TryGetValue(inputs?.HasExplicitBindings == true ? node.NodeId : definition.DefinitionId, out sharedCompilation);
                    if (CharacterTestTacticalBlockDescription.TryParse(definition, node, events, inputs, selectedActions,
                        sharedCompilation, textProvider, frameSeconds, out var blockSegments))
                    {
                        segments.AddRange(blockSegments);
                        foreach (var segment in blockSegments) if (segment.Action != null) hints.Add(segment.Action);
                        structuredNodes.Add(node.NodeId);
                        text = blockSegments[0].Text;
                        for (int i = 1; i < blockSegments.Length; i++)
                        {
                            bool afterArrival = blockSegments[i - 1].Arrival != null && blockSegments[i].Action?.IsActionOnly == true;
                            text = CharacterTestUiText.Resolve(textProvider,
                                "ui.character_test.tactical.description." + (afterArrival ? "arrival_join" : "sequence_join"),
                                "{previous}{next}")
                                .Replace("{previous}", text).Replace("{next}", CharacterTestTacticalBlockDescription.Continuation(blockSegments[i].Text));
                        }
                        if (text.EndsWith("。", StringComparison.Ordinal) || text.EndsWith(".", StringComparison.Ordinal))
                            text = text.Substring(0, text.Length - 1);
                        if (node.EndCondition != null) text += "。这段行为期间若" + Condition(node.EndCondition) + "，会提前中止这段并略过剩余项";
                        return text;
                    }
                    if (CharacterTestTacticalApproachActionHint.TryParse(definition, node.NodeId, events, inputs, selectedActions, sharedCompilation, out var hint, textProvider, frameSeconds))
                    {
                        hints.Add(hint);
                        segments.Add(new CharacterTestTacticalDescriptionSegment(string.Empty, hint));
                        structuredNodes.Add(node.NodeId);
                        text = hint.Text.Substring(0, hint.Text.Length - 1);
                        if (node.EndCondition != null) text += "。这段进攻期间若" + Condition(node.EndCondition) + "，会提前中止这段并略过剩余项";
                        return text;
                    }
                    string compact = cardFace ? CardFaceShared(definition, events, node.NodeId) : CompactShared(definition, events, node.NodeId);
                    if (compact != null) return node.EndCondition == null ? compact : compact + "。这段" + (ContainsAttack(node) ? "进攻" : "行为")
                        + "期间若" + Condition(node.EndCondition) + "，会提前中止这段并略过剩余项";
                    if (events.Length > 1) warnings.Add("此封装采用通用说明，完整条件与时序可在制作详情核对：" + node.DefinitionId);
                    var parts = new List<string>();
                    foreach (var movement in events.Where(e => e.Operation.Kind == "Movement"))
                        parts.Add((cardFace ? "" : "尝试") + MovementPhrase(movement, node.NodeId));
                    if (events.Any(e => e.Operation.Kind == "ApproachOptimalRange")) parts.Add("调整距离（较远时先接近）");
                    if (events.Any(e => e.Operation.Kind == "TurnToTarget")) parts.Add("在动作允许时面向当前目标");
                    foreach (var gate in events.Where(e => e.Operation.Kind == "WaitForCondition"))
                        parts.Add(gatePhrases.TryGetValue(node.DefinitionId + "/" + gate.EventId, out var gateText) ? gateText : "等待已配置条件满足");
                    foreach (var actionEvent in events.Where(e => e.Operation.Kind == "CharacterAction")) parts.Add(
                        (cardFace ? "使出“" : "播放“") + BoundActionName(definition, actionEvent, node.NodeId) + "”");
                    if (simultaneousGateDefinitions.Contains(node.DefinitionId))
                        parts.Add("等待点不拦截同时开始的动作，动作按时间轴播放");
                    if (events.Any(e => e.Operation.Kind == "RetreatFromTarget")) parts.Add("尝试后撤，远离当前目标");
                    foreach (var conditional in events.Where(e => e.ConditionRoot != null && e.Operation.Kind != "WaitForCondition" && e.Operation.Kind != "ReturnToPlanner" && e.Operation.Kind != "CharacterAction"))
                        parts.Add("其中相应行为还需满足" + Condition(conditional.ConditionRoot, definition.Source, node.NodeId));
                    foreach (var exit in events.Where(e => e.Operation.Kind == "ReturnToPlanner"))
                        parts.Add(exit.ConditionRoot == null ? "结束本卡，不再执行后续" : "若" + Condition(exit.ConditionRoot, definition.Source, node.NodeId) + "则结束本卡，否则走完后继续");
                    text = string.Join("，", parts);
                }
                else if (node.Kind == "Exit") text = "结束本卡";
                else
                {
                    var parts = new List<string>(); CharacterTestTacticalDslNode previous = null;
                    foreach (var child in node.Children ?? new List<CharacterTestTacticalDslNode>())
                    {
                        string childSummary = Summary(child);
                        parts.Add(previous == null ? childSummary : previous.EndCondition != null ? "。正常结束或提前中止后，" + childSummary
                            : CharacterTestUiText.Resolve(textProvider, "ui.character_test.tactical.description.next", "。{next}")
                                .Replace("{next}", CharacterTestTacticalBlockDescription.Continuation(childSummary)));
                        previous = child;
                        if (AlwaysEnds(child)) break;
                    }
                    text = string.Concat(parts);
                    if (node.Kind == "If") text = node.Children?.Count == 1 && node.Children[0].Kind == "Reference"
                        ? "若" + Condition(node.Condition) + "，就" + text + "，否则略过这一步"
                        : "到此时若" + Condition(node.Condition) + "，就〔" + text + "〕，否则略过这段";
                    if (node.Kind == "Wait") text = "等待 ≤ " + (node.MaximumWaitFrames ?? 60) + " 帧，" + Condition(node.Condition) + "成立后〔" + text + "〕；超时结束本卡";
                }
                if (node.EndCondition != null) text += "。这段" + (ContainsAttack(node) ? "进攻" : "行为") + "期间若" + Condition(node.EndCondition)
                    + "，会提前中止这段并略过剩余项";
                return text;
            }
            string conciseActivation = source.ActivationConditionRoot == null ? "动作可用时可使用。"
                : "在" + Condition(source.ActivationConditionRoot) + "且动作可用时可使用。";
            string summaryText = Summary(source.Root);
            foreach (var segment in segments)
                if (!string.IsNullOrEmpty(segment.DetailsText))
                    details += "\n\n" + segment.DetailsText;
            bool AlwaysEnds(CharacterTestTacticalDslNode node) => node.EndCondition == null
                && (node.Kind == "Exit" || node.Kind == "Reference" && terminalDefinitions.Contains(node.DefinitionId)
                    || (node.Kind == "Sequence" || node.Kind == "Wait") && node.Children?.Any(AlwaysEnds) == true);
            string ending = segments.Count > 0 ? CharacterTestUiText.Resolve(textProvider, "ui.character_test.tactical.description.sentence_end", "。") : "。";
            string fallback = (cardFace || segments.Count > 0 ? "" : conciseActivation) + summaryText + ending;
            var flow = CharacterTestTacticalDescriptionFlow.Build(source.Root, segments.ToArray(), node => Condition(node), AlwaysEnds, textProvider, fallback);
            return new CharacterTestTacticalDslDescription { Text = flow.Complete ? flow.Text : fallback, Details = details, DescriptionFlow = flow,
                Hints = hints.ToArray(),
                Segments = segments.ToArray(), HasStructuredDescription = segments.Count > 0 && flow.Complete,
                Warnings = warnings.Distinct().ToArray(), Executable = valid, CompilationIssue = issue,
                DependencyIdentity = compiled?.DependencyIdentity ?? string.Empty };
        }
    }
}
