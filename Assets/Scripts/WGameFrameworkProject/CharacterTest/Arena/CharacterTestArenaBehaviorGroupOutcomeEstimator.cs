using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using MxFramework.AI;

namespace MxFramework.Game.CharacterTest
{
    internal enum CharacterTestArenaBehaviorGroupOutcomeScenario
    {
        OpeningEngage = 0,
        CloseAttack = 1,
        TargetBlocked = 2,
        PathUnavailable = 3,
        NearHazard = 4,
        NoTarget = 5,
        ActionLocked = 6
    }

    internal sealed class CharacterTestArenaBehaviorGroupOutcomeRow
    {
        public CharacterTestArenaBehaviorGroupOutcomeScenario Scenario { get; set; }
        public string ScenarioName { get; set; } = string.Empty;
        public string ExpectedEntryId { get; set; } = string.Empty;
        public string ExpectedBehavior { get; set; } = string.Empty;
        public string ExpectedAction { get; set; } = string.Empty;
        public CharacterTestRuntimeAiPlannerActionKind ExpectedActionKind { get; set; }
        public bool HasExpectedAction { get; set; }
        public string SuppressedEntryId { get; set; } = string.Empty;
        public string SuppressedBehavior { get; set; } = string.Empty;
        public string Reason { get; set; } = string.Empty;
        public string Notice { get; set; } = string.Empty;
    }

    internal sealed class CharacterTestArenaBehaviorGroupOutcomeForecast
    {
        private readonly CharacterTestArenaBehaviorGroupOutcomeRow[] _rows;

        public CharacterTestArenaBehaviorGroupOutcomeForecast(
            string headline,
            string text,
            CharacterTestArenaBehaviorGroupOutcomeRow[] rows)
        {
            Headline = headline ?? string.Empty;
            Text = text ?? string.Empty;
            _rows = rows ?? Array.Empty<CharacterTestArenaBehaviorGroupOutcomeRow>();
        }

        public string Headline { get; }
        public string Text { get; }
        public IReadOnlyList<CharacterTestArenaBehaviorGroupOutcomeRow> Rows => _rows;
    }

    internal static class CharacterTestArenaBehaviorGroupOutcomeEstimator
    {
        private const int ForecastAgentId = 1;

        public static CharacterTestArenaBehaviorGroupOutcomeForecast Estimate(
            CharacterTestArenaBehaviorGroupDraft draft,
            CharacterTestRuntimeAiPlannerBehaviorDefinitionCatalog definitionCatalog,
            CharacterTestRuntimeAiPlannerBehaviorCompileResult compile)
        {
            if (draft == null)
                return CreateUnavailable("结果预估：未选择行为组。");

            if (compile == null || !compile.Success || compile.Profile == null)
                return CreateUnavailable("结果预估：先修复编译错误，再查看配置结果。");

            if (!CharacterTestRuntimeAiPlannerCompiledProfile.TryCompile(
                    compile.Profile,
                    out CharacterTestRuntimeAiPlannerCompiledProfile compiled,
                    out _))
            {
                return CreateUnavailable("结果预估：编译产物不可运行，先修复编译错误。");
            }

            CharacterTestArenaBehaviorGroupOutcomeRow[] rows =
            {
                EvaluateScenario(
                    CharacterTestArenaBehaviorGroupOutcomeScenario.OpeningEngage,
                    "开局接敌",
                    "有目标、可见、未进攻击范围、路径可用。",
                    compiled,
                    CreateFacts(
                        compiled.AttackRange,
                        targetExists: true,
                        inAttackRange: false,
                        targetVisible: true,
                        lineOfSightBlocked: false,
                        pathAvailable: true,
                        directPathClear: true,
                        nearHazard: false,
                        selfActionRunning: false)),
                EvaluateScenario(
                    CharacterTestArenaBehaviorGroupOutcomeScenario.CloseAttack,
                    "近身可打",
                    "有目标、可见、已进攻击范围、自身空闲。",
                    compiled,
                    CreateFacts(
                        compiled.AttackRange,
                        targetExists: true,
                        inAttackRange: true,
                        targetVisible: true,
                        lineOfSightBlocked: false,
                        pathAvailable: true,
                        directPathClear: true,
                        nearHazard: false,
                        selfActionRunning: false)),
                EvaluateScenario(
                    CharacterTestArenaBehaviorGroupOutcomeScenario.TargetBlocked,
                    "目标被挡",
                    "有目标、视线被挡、路径仍可用。",
                    compiled,
                    CreateFacts(
                        compiled.AttackRange,
                        targetExists: true,
                        inAttackRange: false,
                        targetVisible: false,
                        lineOfSightBlocked: true,
                        pathAvailable: true,
                        directPathClear: false,
                        nearHazard: false,
                        selfActionRunning: false)),
                EvaluateScenario(
                    CharacterTestArenaBehaviorGroupOutcomeScenario.PathUnavailable,
                    "路径不可用",
                    "有目标但移动路径不可用。",
                    compiled,
                    CreateFacts(
                        compiled.AttackRange,
                        targetExists: true,
                        inAttackRange: false,
                        targetVisible: true,
                        lineOfSightBlocked: false,
                        pathAvailable: false,
                        directPathClear: false,
                        nearHazard: false,
                        selfActionRunning: false)),
                EvaluateScenario(
                    CharacterTestArenaBehaviorGroupOutcomeScenario.NearHazard,
                    "身边危险",
                    "附近危险为 true，用来检查生存行为是否压过战斗/移动。",
                    compiled,
                    CreateFacts(
                        compiled.AttackRange,
                        targetExists: true,
                        inAttackRange: true,
                        targetVisible: true,
                        lineOfSightBlocked: false,
                        pathAvailable: true,
                        directPathClear: true,
                        nearHazard: true,
                        selfActionRunning: false)),
                EvaluateScenario(
                    CharacterTestArenaBehaviorGroupOutcomeScenario.NoTarget,
                    "无目标",
                    "target exists=false，用来检查保底等待。",
                    compiled,
                    CreateFacts(
                        compiled.AttackRange,
                        targetExists: false,
                        inAttackRange: false,
                        targetVisible: false,
                        lineOfSightBlocked: false,
                        pathAvailable: false,
                        directPathClear: false,
                        nearHazard: false,
                        selfActionRunning: false)),
                EvaluateScenario(
                    CharacterTestArenaBehaviorGroupOutcomeScenario.ActionLocked,
                    "动作硬直",
                    "目标在范围内，但自身正在动作。",
                    compiled,
                    CreateFacts(
                        compiled.AttackRange,
                        targetExists: true,
                        inAttackRange: true,
                        targetVisible: true,
                        lineOfSightBlocked: false,
                        pathAvailable: true,
                        directPathClear: true,
                        nearHazard: false,
                        selfActionRunning: true))
            };

            string disabledNotice = FormatDisabledNotice(draft);
            string headline = BuildHeadline(rows);
            string text = BuildText(rows, disabledNotice);
            return new CharacterTestArenaBehaviorGroupOutcomeForecast(headline, text, rows);
        }

        private static CharacterTestArenaBehaviorGroupOutcomeForecast CreateUnavailable(string message)
        {
            return new CharacterTestArenaBehaviorGroupOutcomeForecast(
                message,
                message + "\n预估只判断会选择哪个 Runtime AI Planner action，不预测伤害、胜负、真实路径或动画命中帧。",
                Array.Empty<CharacterTestArenaBehaviorGroupOutcomeRow>());
        }

        private static CharacterTestArenaBehaviorGroupOutcomeRow EvaluateScenario(
            CharacterTestArenaBehaviorGroupOutcomeScenario scenario,
            string name,
            string setup,
            CharacterTestRuntimeAiPlannerCompiledProfile compiled,
            CharacterTestRuntimeAiPlannerFactState facts)
        {
            var stateMachine = new AiHfsmStateMachine(
                compiled.HfsmStates,
                compiled.InitialStateId,
                compiled.HfsmTransitions);
            var agent = new ForecastAgent(facts);
            stateMachine.Tick(agent, facts, 0);

            CharacterTestRuntimeAiPlannerCompiledAction selected = default;
            CharacterTestRuntimeAiPlannerCompiledAction runnerUp = default;
            bool hasSelected = false;
            bool hasRunnerUp = false;
            int blockedByCondition = 0;
            int executableOutsideState = 0;

            IReadOnlyList<CharacterTestRuntimeAiPlannerCompiledAction> actions = compiled.Actions;
            for (int i = 0; i < actions.Count; i++)
            {
                CharacterTestRuntimeAiPlannerCompiledAction action = actions[i];
                bool inActivePath = compiled.IsActionInActivePath(action, stateMachine.ActivePath);
                bool canExecute = action.CanExecute(facts);
                if (inActivePath && canExecute)
                {
                    if (!hasSelected || IsBetter(action, selected))
                    {
                        if (hasSelected)
                        {
                            runnerUp = selected;
                            hasRunnerUp = true;
                        }

                        selected = action;
                        hasSelected = true;
                    }
                    else if (!hasRunnerUp || IsBetter(action, runnerUp))
                    {
                        runnerUp = action;
                        hasRunnerUp = true;
                    }
                }
                else if (inActivePath)
                {
                    blockedByCondition++;
                }
                else if (canExecute)
                {
                    executableOutsideState++;
                }
            }

            int currentStateId = stateMachine.CurrentStateId;
            string activeState = compiled.GetStateDisplayName(currentStateId);
            string pathText = compiled.GetStatePathText(currentStateId);
            if (!hasSelected)
            {
                return new CharacterTestArenaBehaviorGroupOutcomeRow
                {
                    Scenario = scenario,
                    ScenarioName = name,
                    ExpectedBehavior = "无可执行行为",
                    ExpectedAction = "无输出",
                    ExpectedActionKind = CharacterTestRuntimeAiPlannerActionKind.Wait,
                    HasExpectedAction = false,
                    Reason = setup + " 当前进入 " + activeState + "，该状态没有满足条件的 action。",
                    Notice = FormatNoSelectionNotice(blockedByCondition, executableOutsideState, pathText)
                };
            }

            string notice = FormatSelectionNotice(scenario, runnerUp, hasRunnerUp, executableOutsideState, pathText);
            return new CharacterTestArenaBehaviorGroupOutcomeRow
            {
                Scenario = scenario,
                ScenarioName = name,
                ExpectedEntryId = ExtractEntryId(selected.DisplayName),
                ExpectedBehavior = FormatBehaviorDisplay(selected),
                ExpectedAction = FormatActionKind(selected.Kind),
                ExpectedActionKind = selected.Kind,
                HasExpectedAction = true,
                SuppressedEntryId = hasRunnerUp ? ExtractEntryId(runnerUp.DisplayName) : string.Empty,
                SuppressedBehavior = hasRunnerUp ? FormatBehaviorDisplay(runnerUp) : string.Empty,
                Reason = setup
                    + " 进入 "
                    + activeState
                    + "，条件满足后按 score/cost/actionId 选择；score="
                    + selected.Score.ToString("0.##", CultureInfo.InvariantCulture)
                    + " cost="
                    + selected.Cost.ToString("0.##", CultureInfo.InvariantCulture)
                    + "。",
                Notice = notice
            };
        }

        private static CharacterTestRuntimeAiPlannerFactState CreateFacts(
            float attackRange,
            bool targetExists,
            bool inAttackRange,
            bool targetVisible,
            bool lineOfSightBlocked,
            bool pathAvailable,
            bool directPathClear,
            bool nearHazard,
            bool selfActionRunning)
        {
            float nearDistance = attackRange > 0.5f ? attackRange * 0.75f : 0.5f;
            float farDistance = attackRange > 0f ? attackRange + 2f : 4f;
            var facts = new CharacterTestRuntimeAiPlannerFactState();
            facts.SetValue(CharacterTestRuntimeAiPlannerFactKeys.TargetExists, targetExists);
            facts.SetValue(CharacterTestRuntimeAiPlannerFactKeys.TargetInAttackRange, inAttackRange);
            facts.SetValue(CharacterTestRuntimeAiPlannerFactKeys.TargetVisible, targetVisible);
            facts.SetValue(CharacterTestRuntimeAiPlannerFactKeys.TargetLineOfSightBlocked, lineOfSightBlocked);
            facts.SetValue(CharacterTestRuntimeAiPlannerFactKeys.TargetLastSeenAgeFrames, targetVisible ? 0 : 30);
            facts.SetValue(CharacterTestRuntimeAiPlannerFactKeys.TargetDistance, inAttackRange ? nearDistance : farDistance);
            facts.SetValue(CharacterTestRuntimeAiPlannerFactKeys.TargetThreatScore, targetExists ? 10f : 0f);
            facts.SetValue(CharacterTestRuntimeAiPlannerFactKeys.SelfActionRunning, selfActionRunning);
            facts.SetValue(CharacterTestRuntimeAiPlannerFactKeys.SelfSurfaceKind, 0);
            facts.SetValue(CharacterTestRuntimeAiPlannerFactKeys.SelfOnHardSlick, false);
            facts.SetValue(CharacterTestRuntimeAiPlannerFactKeys.SelfNearMovableObject, false);
            facts.SetValue(CharacterTestRuntimeAiPlannerFactKeys.MovePathAvailable, pathAvailable);
            facts.SetValue(CharacterTestRuntimeAiPlannerFactKeys.MoveDirectPathClear, directPathClear);
            facts.SetValue(CharacterTestRuntimeAiPlannerFactKeys.MoveCandidateIndex, pathAvailable ? 0 : -1);
            facts.SetValue(CharacterTestRuntimeAiPlannerFactKeys.MoveDirectionX, pathAvailable ? 1f : 0f);
            facts.SetValue(CharacterTestRuntimeAiPlannerFactKeys.MoveDirectionZ, 0f);
            facts.SetValue(CharacterTestRuntimeAiPlannerFactKeys.EnvironmentNearObstacle, lineOfSightBlocked || !directPathClear);
            facts.SetValue(CharacterTestRuntimeAiPlannerFactKeys.EnvironmentNearHazard, nearHazard);
            facts.SetValue(CharacterTestRuntimeAiPlannerFactKeys.EnvironmentNearUsefulObject, false);
            return facts;
        }

        private static bool IsBetter(
            CharacterTestRuntimeAiPlannerCompiledAction candidate,
            CharacterTestRuntimeAiPlannerCompiledAction current)
        {
            int scoreCompare = candidate.Score.CompareTo(current.Score);
            if (scoreCompare != 0)
                return scoreCompare > 0;

            int costCompare = candidate.Cost.CompareTo(current.Cost);
            if (costCompare != 0)
                return costCompare < 0;

            return candidate.ActionId < current.ActionId;
        }

        private static string BuildHeadline(IReadOnlyList<CharacterTestArenaBehaviorGroupOutcomeRow> rows)
        {
            var parts = new List<string>(4);
            AddHeadlinePart(rows, CharacterTestArenaBehaviorGroupOutcomeScenario.OpeningEngage, "开局");
            AddHeadlinePart(rows, CharacterTestArenaBehaviorGroupOutcomeScenario.CloseAttack, "近身");
            AddHeadlinePart(rows, CharacterTestArenaBehaviorGroupOutcomeScenario.NearHazard, "危险时");
            AddHeadlinePart(rows, CharacterTestArenaBehaviorGroupOutcomeScenario.NoTarget, "无目标");

            return parts.Count == 0
                ? "结果预估：当前行为组在典型情境下没有稳定输出，先检查触发条件和保底行为。"
                : "结果预估：通常会 " + string.Join("，", parts) + "。";

            void AddHeadlinePart(
                IReadOnlyList<CharacterTestArenaBehaviorGroupOutcomeRow> source,
                CharacterTestArenaBehaviorGroupOutcomeScenario scenario,
                string prefix)
            {
                for (int i = 0; source != null && i < source.Count; i++)
                {
                    CharacterTestArenaBehaviorGroupOutcomeRow row = source[i];
                    if (row.Scenario != scenario || !row.HasExpectedAction)
                        continue;

                    parts.Add(prefix + FormatHeadlineAction(row.ExpectedActionKind));
                    return;
                }
            }
        }

        private static string BuildText(
            IReadOnlyList<CharacterTestArenaBehaviorGroupOutcomeRow> rows,
            string disabledNotice)
        {
            var builder = new StringBuilder(1024);
            for (int i = 0; rows != null && i < rows.Count; i++)
            {
                CharacterTestArenaBehaviorGroupOutcomeRow row = rows[i];
                if (i > 0)
                    builder.AppendLine();
                builder.Append(row.ScenarioName)
                    .Append(" | 预计命中卡: ")
                    .Append(row.ExpectedBehavior)
                    .Append(" -> ")
                    .Append(row.ExpectedAction)
                    .Append(" | 为什么: ")
                    .Append(row.Reason);
                if (!string.IsNullOrWhiteSpace(row.Notice))
                    builder.Append(" | 注意: ").Append(row.Notice);
            }

            if (!string.IsNullOrWhiteSpace(disabledNotice))
                builder.AppendLine().Append(disabledNotice);

            builder.AppendLine()
                .Append("边界: 只预估会选择哪个 Runtime AI Planner action，不预测伤害、胜负、真实路径长度或动画命中帧。");
            return builder.ToString();
        }

        private static string FormatDisabledNotice(CharacterTestArenaBehaviorGroupDraft draft)
        {
            int disabled = 0;
            IReadOnlyList<CharacterTestRuntimeAiPlannerBehaviorModuleEntry> entries =
                draft?.Profile?.Entries;
            for (int i = 0; entries != null && i < entries.Count; i++)
            {
                if (entries[i] != null && entries[i].Disabled)
                    disabled++;
            }

            return disabled > 0
                ? "注意: 组内有 " + disabled.ToString(CultureInfo.InvariantCulture) + " 个临时禁用行为未参与预估。"
                : string.Empty;
        }

        private static string FormatNoSelectionNotice(
            int blockedByCondition,
            int executableOutsideState,
            string pathText)
        {
            string text = "activePath=" + pathText + "。";
            if (blockedByCondition > 0)
            {
                text += "本状态有 "
                    + blockedByCondition.ToString(CultureInfo.InvariantCulture)
                    + " 个 action 条件不满足。";
            }

            if (executableOutsideState > 0)
            {
                text += "另有 "
                    + executableOutsideState.ToString(CultureInfo.InvariantCulture)
                    + " 个 action 被当前状态优先级压住。";
            }

            return text;
        }

        private static string FormatSelectionNotice(
            CharacterTestArenaBehaviorGroupOutcomeScenario scenario,
            CharacterTestRuntimeAiPlannerCompiledAction runnerUp,
            bool hasRunnerUp,
            int executableOutsideState,
            string pathText)
        {
            var parts = new List<string>(3)
            {
                "activePath=" + pathText
            };
            if (hasRunnerUp)
            {
                parts.Add(
                    "被压制卡 "
                    + FormatBehaviorDisplay(runnerUp)
                    + " 也满足，但 score/cost/actionId 排在后面");
            }

            if (executableOutsideState > 0)
            {
                parts.Add(
                    executableOutsideState.ToString(CultureInfo.InvariantCulture)
                    + " 个其它状态 action 被当前状态优先级压住");
            }

            if (scenario == CharacterTestArenaBehaviorGroupOutcomeScenario.NearHazard
                && executableOutsideState > 0)
            {
                parts.Add("危险优先级会压过战斗/移动");
            }

            return string.Join("；", parts);
        }

        private static string FormatBehaviorDisplay(CharacterTestRuntimeAiPlannerCompiledAction action)
        {
            string name = action.DisplayName ?? string.Empty;
            int bracket = name.IndexOf(" [", StringComparison.Ordinal);
            if (bracket > 0)
                name = name.Substring(0, bracket);

            switch (name)
            {
                case "Approach target":
                    return "接近目标";
                case "Keep distance":
                    return "保持距离";
                case "Strafe when blocked":
                    return "被阻挡时侧移";
                case "Avoid hazard":
                    return "躲避危险";
                case "Primary attack":
                    return "普通攻击";
                case "Cast ability slot":
                    return "释放技能槽";
                case "Wait or hold":
                    return "等待/保持";
                case "":
                    return FormatActionKind(action.Kind);
                default:
                    return name;
            }
        }

        private static string ExtractEntryId(string displayName)
        {
            if (string.IsNullOrWhiteSpace(displayName))
                return string.Empty;

            int bracket = displayName.IndexOf(" [", StringComparison.Ordinal);
            int end = displayName.LastIndexOf(']');
            return bracket >= 0 && end > bracket + 2
                ? displayName.Substring(bracket + 2, end - bracket - 2)
                : string.Empty;
        }

        private static string FormatActionKind(CharacterTestRuntimeAiPlannerActionKind kind)
        {
            switch (kind)
            {
                case CharacterTestRuntimeAiPlannerActionKind.MoveToTarget:
                    return "向目标移动";
                case CharacterTestRuntimeAiPlannerActionKind.Strafe:
                    return "侧移绕位";
                case CharacterTestRuntimeAiPlannerActionKind.Retreat:
                    return "后撤";
                case CharacterTestRuntimeAiPlannerActionKind.PrimaryAttack:
                    return "普通攻击";
                case CharacterTestRuntimeAiPlannerActionKind.CastAbilitySlot:
                    return "释放技能";
                default:
                    return "等待/保持";
            }
        }

        private static string FormatHeadlineAction(CharacterTestRuntimeAiPlannerActionKind kind)
        {
            switch (kind)
            {
                case CharacterTestRuntimeAiPlannerActionKind.MoveToTarget:
                    return "接近目标";
                case CharacterTestRuntimeAiPlannerActionKind.Strafe:
                    return "侧移绕位";
                case CharacterTestRuntimeAiPlannerActionKind.Retreat:
                    return "后撤";
                case CharacterTestRuntimeAiPlannerActionKind.PrimaryAttack:
                    return "普通攻击";
                case CharacterTestRuntimeAiPlannerActionKind.CastAbilitySlot:
                    return "释放技能";
                default:
                    return "等待";
            }
        }

        private sealed class ForecastAgent : IAiAgent
        {
            public ForecastAgent(IAiWorldState worldState)
            {
                WorldState = worldState;
            }

            public int Id => ForecastAgentId;
            public IAiWorldState WorldState { get; }
        }
    }
}
