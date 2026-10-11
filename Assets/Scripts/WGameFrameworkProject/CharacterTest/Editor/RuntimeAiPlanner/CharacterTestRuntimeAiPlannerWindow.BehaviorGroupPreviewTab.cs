using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace MxFramework.Game.CharacterTest.Editor
{
    public sealed partial class CharacterTestRuntimeAiPlannerWindow
    {
        private enum BehaviorGroupPreviewSourceKind
        {
            CurrentDraft,
            BuiltInDefault,
            DefaultLoadoutVariant
        }

        private sealed class BehaviorGroupPreviewCandidate
        {
            public BehaviorGroupPreviewCandidate(
                string key,
                string displayName,
                BehaviorGroupPreviewSourceKind sourceKind,
                string sourceText,
                CharacterTestRuntimeAiPlannerBehaviorCompositionProfile group)
            {
                Key = key ?? string.Empty;
                DisplayName = displayName ?? string.Empty;
                SourceKind = sourceKind;
                SourceText = sourceText ?? string.Empty;
                Group = group != null
                    ? group.Clone()
                    : CharacterTestRuntimeAiPlannerBehaviorCompositionCatalog.CreateDefaultProfile();
                Compile = CharacterTestRuntimeAiPlannerBehaviorCompositionCompiler.Compile(Group);
            }

            public string Key { get; }
            public string DisplayName { get; }
            public BehaviorGroupPreviewSourceKind SourceKind { get; }
            public string SourceText { get; }
            public CharacterTestRuntimeAiPlannerBehaviorCompositionProfile Group { get; }
            public CharacterTestRuntimeAiPlannerBehaviorCompileResult Compile { get; }
            public string ChoiceLabel => DisplayName + " | " + SourceText;
        }

        private sealed class BehaviorGroupDecisionChainItem
        {
            public int Priority { get; set; }
            public string EntryId { get; set; }
            public string BehaviorId { get; set; }
            public string DisplayName { get; set; }
            public string ConditionSummary { get; set; }
            public string OutputSummary { get; set; }
            public string StatusText { get; set; }
            public string DiagnosticsText { get; set; }
            public bool Disabled { get; set; }
            public bool HasIssue { get; set; }
            public bool CountsAsExecutable { get; set; }
        }

        private void BuildBehaviorGroupPreviewLeftPanel(VisualElement root)
        {
            List<BehaviorGroupPreviewCandidate> candidates = BuildBehaviorGroupPreviewCandidates();
            BehaviorGroupPreviewCandidate selected =
                ResolveSelectedBehaviorGroupPreviewCandidate(candidates);

            root.Add(CreateSectionTitle("行为组预览"));
            if (candidates.Count > 0)
            {
                var choices = new List<string>(candidates.Count);
                int selectedIndex = 0;
                for (int i = 0; i < candidates.Count; i++)
                {
                    choices.Add(candidates[i].ChoiceLabel);
                    if (string.Equals(candidates[i].Key, selected.Key, System.StringComparison.Ordinal))
                        selectedIndex = i;
                }

                var selector = new PopupField<string>("行为组候选", choices, selectedIndex)
                {
                    name = BehaviorGroupPreviewCandidateFieldName
                };
                selector.RegisterValueChangedCallback(evt =>
                {
                    for (int i = 0; i < candidates.Count; i++)
                    {
                        if (!string.Equals(evt.newValue, candidates[i].ChoiceLabel, System.StringComparison.Ordinal))
                            continue;

                        _selectedBehaviorGroupPreviewCandidateKey = candidates[i].Key;
                        RefreshView();
                        return;
                    }
                });
                root.Add(selector);
            }

            root.Add(CreateSpacer(6));
            root.Add(CreateWrappedLabel(selected.DisplayName));
            root.Add(CreateWrappedLabel(selected.SourceText));
            root.Add(CreateWrappedLabel(
                "可预览行为组 "
                + candidates.Count.ToString(CultureInfo.InvariantCulture)));
            root.Add(CreateWrappedLabel(
                "行为定义库 "
                + CharacterTestRuntimeAiPlannerBehaviorCompositionCatalog.Definitions.Count.ToString(
                    CultureInfo.InvariantCulture)));
            root.Add(CreateWrappedLabel(
                "组内行为 "
                + selected.Group.Entries.Count.ToString(CultureInfo.InvariantCulture)));
            root.Add(CreateWrappedLabel(selected.Compile.Success ? "编译通过" : "编译失败"));
            root.Add(CreateSpacer(8));
            root.Add(CreateNotice("这里选的是可预览的行为组，不是行为定义库。想预览某个行为，先在“行为组编辑”把它加入当前组，再预览当前草稿。"));
        }

        private void BuildBehaviorGroupPreviewTab(VisualElement root)
        {
            List<BehaviorGroupPreviewCandidate> candidates = BuildBehaviorGroupPreviewCandidates();
            BehaviorGroupPreviewCandidate selected =
                ResolveSelectedBehaviorGroupPreviewCandidate(candidates);
            List<BehaviorGroupDecisionChainItem> chainItems =
                BuildBehaviorGroupDecisionChainItems(selected);

            root.style.minWidth = 0;
            root.style.minHeight = 0;

            var scroll = new ScrollView(ScrollViewMode.Vertical);
            scroll.style.flexGrow = 1;
            scroll.style.minHeight = 0;
            scroll.style.minWidth = 0;
            scroll.Add(CreateBehaviorGroupPreviewHeader(selected, chainItems));
            scroll.Add(CreateBehaviorGroupDecisionChainPanel(chainItems));
            scroll.Add(CreateBehaviorGroupGeneratedActionStrip(selected.Compile, false));

            var runtimeFlow = new Foldout
            {
                name = BehaviorGroupRuntimeFlowFoldoutName,
                text = "底层 Runtime AI Planner 流程",
                value = false
            };
            runtimeFlow.style.marginTop = 8;
            runtimeFlow.Add(CreateBehaviorGroupFlowPreviewPanel(
                selected.Compile,
                false,
                "底层 Runtime AI Planner 流程",
                false));
            scroll.Add(runtimeFlow);
            root.Add(scroll);
        }

        private void BuildBehaviorGroupPreviewInspector(VisualElement root)
        {
            List<BehaviorGroupPreviewCandidate> candidates = BuildBehaviorGroupPreviewCandidates();
            BehaviorGroupPreviewCandidate selected =
                ResolveSelectedBehaviorGroupPreviewCandidate(candidates);
            List<BehaviorGroupDecisionChainItem> chainItems =
                BuildBehaviorGroupDecisionChainItems(selected);

            var scroll = new ScrollView(ScrollViewMode.Vertical);
            scroll.style.flexGrow = 1;
            scroll.style.minHeight = 0;
            scroll.style.minWidth = 0;
            scroll.name = BehaviorGroupPreviewSummaryName;

            scroll.Add(CreateSectionTitle("策略摘要"));
            scroll.Add(CreateWrappedLabel(BuildBehaviorGroupStrategySummary(selected, chainItems)));
            scroll.Add(CreateBehaviorInspectorLine("主要顺序", BuildBehaviorGroupOrderSummary(chainItems, 5)));
            VisualElement warnings = CreateBehaviorGroupWarningsPanel(chainItems);
            if (warnings != null)
                scroll.Add(warnings);

            var diagnostics = new Foldout
            {
                name = BehaviorGroupPreviewDiagnosticsFoldoutName,
                text = "编译诊断",
                value = false
            };
            diagnostics.Add(CreateCompactReportField(selected.Compile.Report != null
                ? selected.Compile.Report.ToText()
                : string.Empty));
            scroll.Add(diagnostics);

            var raw = new Foldout
            {
                name = BehaviorGroupPreviewRawFoldoutName,
                text = "Raw",
                value = false
            };
            raw.Add(CreateBehaviorInspectorLine("来源", selected.SourceText));
            raw.Add(CreateBehaviorInspectorLine("Group", selected.Group.GroupId));
            raw.Add(CreateBehaviorInspectorLine("显示名", selected.Group.DisplayName));
            raw.Add(CreateBehaviorInspectorLine(
                "Entries",
                selected.Group.Entries.Count.ToString(CultureInfo.InvariantCulture)));
            raw.Add(CreateBehaviorInspectorLine("Targeting", selected.Group.TargetSelectionMode.ToString()));
            raw.Add(CreateBehaviorInspectorLine(
                "Attack Range",
                selected.Group.AttackRange.ToString("F2", CultureInfo.InvariantCulture)));
            raw.Add(CreateBehaviorInspectorLine(
                "输出 Profile",
                selected.Compile.Profile != null ? selected.Compile.Profile.ProfileId : "none"));
            raw.Add(CreateBehaviorInspectorLine(
                "状态",
                selected.Compile.Success ? "编译通过" : "编译失败"));
            raw.Add(CreateBehaviorGroupPreviewEntrySummary(selected, chainItems));
            scroll.Add(raw);
            root.Add(scroll);
        }

        private VisualElement CreateBehaviorGroupPreviewHeader(
            BehaviorGroupPreviewCandidate selected,
            IReadOnlyList<BehaviorGroupDecisionChainItem> chainItems)
        {
            var panel = new VisualElement();
            panel.style.marginBottom = 6;
            panel.style.minWidth = 0;
            panel.Add(CreateSectionTitle("行为组预览"));
            panel.Add(CreateBehaviorGroupSummaryGrid(selected, chainItems));
            panel.Add(CreateWrappedLabel("预览页只读：首屏用于核对行为组决策链；底层 Runtime AI Planner 流程放在高级区，不列出全部行为定义，也不提供编辑、热更新或覆盖 profile 入口。"));
            return panel;
        }

        private VisualElement CreateBehaviorGroupPreviewEntrySummary(
            BehaviorGroupPreviewCandidate selected,
            IReadOnlyList<BehaviorGroupDecisionChainItem> chainItems)
        {
            var root = new VisualElement();
            root.Add(CreateSectionTitle("组内行为"));
            if (selected.Group.Entries.Count == 0)
            {
                root.Add(CreateNotice("该行为组没有 entries。"));
                return root;
            }

            int disabledCount = 0;
            for (int i = 0; i < selected.Group.Entries.Count; i++)
            {
                if (selected.Group.Entries[i].Disabled)
                    disabledCount++;
            }

            root.Add(CreateBehaviorInspectorLine(
                "可执行行为",
                CountExecutableBehaviorItems(chainItems).ToString(CultureInfo.InvariantCulture)));
            root.Add(CreateBehaviorInspectorLine(
                "禁用计数",
                disabledCount.ToString(CultureInfo.InvariantCulture)));
            root.Add(CreateBehaviorInspectorLine(
                "生成 action",
                selected.Compile.Profile != null
                    ? selected.Compile.Profile.Actions.Count.ToString(CultureInfo.InvariantCulture)
                    : "0"));
            return root;
        }

        private VisualElement CreateBehaviorGroupSummaryGrid(
            BehaviorGroupPreviewCandidate selected,
            IReadOnlyList<BehaviorGroupDecisionChainItem> chainItems)
        {
            var grid = new VisualElement();
            grid.style.flexDirection = FlexDirection.Row;
            grid.style.flexWrap = Wrap.Wrap;
            grid.style.marginTop = 2;
            grid.style.marginBottom = 4;
            grid.style.minWidth = 0;

            int disabledCount = CountDisabledBehaviorItems(chainItems);
            int generatedActionCount = selected.Compile.Profile != null
                ? selected.Compile.Profile.Actions.Count
                : 0;
            grid.Add(CreateBehaviorGroupMetric("候选", selected.DisplayName));
            grid.Add(CreateBehaviorGroupMetric("来源", selected.SourceText));
            grid.Add(CreateBehaviorGroupMetric(
                "组内行为",
                selected.Group.Entries.Count.ToString(CultureInfo.InvariantCulture)));
            grid.Add(CreateBehaviorGroupMetric(
                "可执行行为",
                CountExecutableBehaviorItems(chainItems).ToString(CultureInfo.InvariantCulture)));
            grid.Add(CreateBehaviorGroupMetric(
                "禁用",
                disabledCount.ToString(CultureInfo.InvariantCulture)));
            grid.Add(CreateBehaviorGroupMetric(
                "生成 action",
                generatedActionCount.ToString(CultureInfo.InvariantCulture)));
            grid.Add(CreateBehaviorGroupMetric(
                "编译",
                selected.Compile.Success ? "通过" : "失败"));
            return grid;
        }

        private static VisualElement CreateBehaviorGroupMetric(string label, string value)
        {
            var box = new VisualElement();
            box.style.minWidth = 116;
            box.style.maxWidth = 220;
            box.style.marginRight = 6;
            box.style.marginBottom = 6;
            box.style.paddingLeft = 8;
            box.style.paddingRight = 8;
            box.style.paddingTop = 5;
            box.style.paddingBottom = 5;
            box.style.borderTopWidth = 1;
            box.style.borderBottomWidth = 1;
            box.style.borderLeftWidth = 1;
            box.style.borderRightWidth = 1;
            box.style.borderTopColor = new Color(0.22f, 0.22f, 0.22f);
            box.style.borderBottomColor = new Color(0.22f, 0.22f, 0.22f);
            box.style.borderLeftColor = new Color(0.22f, 0.22f, 0.22f);
            box.style.borderRightColor = new Color(0.22f, 0.22f, 0.22f);
            box.style.backgroundColor = new Color(0.16f, 0.16f, 0.16f);

            var name = new Label(label);
            name.style.opacity = 0.72f;
            name.style.fontSize = 10;
            name.style.marginBottom = 2;
            box.Add(name);

            Label text = CreateWrappedLabel(value);
            text.style.marginBottom = 0;
            text.style.unityFontStyleAndWeight = FontStyle.Bold;
            text.tooltip = value ?? string.Empty;
            box.Add(text);
            return box;
        }

        private VisualElement CreateBehaviorGroupDecisionChainPanel(
            IReadOnlyList<BehaviorGroupDecisionChainItem> items)
        {
            var panel = new VisualElement();
            panel.name = BehaviorGroupDecisionChainName;
            panel.style.marginTop = 4;
            panel.style.marginBottom = 6;
            panel.style.minWidth = 0;
            panel.Add(CreateSectionTitle("行为组决策链"));
            panel.Add(CreateWrappedLabel("按组内 entry order 从上到下尝试；每行说明触发条件、输出动作和当前诊断状态。"));

            VisualElement header = CreateBehaviorGroupDecisionChainHeader();
            panel.Add(header);

            if (items == null || items.Count == 0)
            {
                panel.Add(CreateNotice("该行为组没有行为实例。请先到“行为组编辑”从行为定义库加入行为。"));
                return panel;
            }

            for (int i = 0; i < items.Count; i++)
                panel.Add(CreateBehaviorGroupDecisionChainRow(items[i]));
            return panel;
        }

        private static VisualElement CreateBehaviorGroupDecisionChainHeader()
        {
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;
            row.style.minHeight = 24;
            row.style.minWidth = 0;
            row.style.marginTop = 4;
            row.style.marginBottom = 2;
            row.Add(CreateDecisionChainHeaderLabel("优先级", 62, 0));
            row.Add(CreateDecisionChainHeaderLabel("行为名称", 170, 1));
            row.Add(CreateDecisionChainHeaderLabel("触发条件摘要", 240, 2));
            row.Add(CreateDecisionChainHeaderLabel("输出动作", 170, 1));
            row.Add(CreateDecisionChainHeaderLabel("状态/诊断", 110, 0));
            return row;
        }

        private static Label CreateDecisionChainHeaderLabel(string text, float width, float flexGrow)
        {
            var label = new Label(text);
            label.style.width = width;
            label.style.flexGrow = flexGrow;
            label.style.flexShrink = 1;
            label.style.marginRight = 6;
            label.style.unityFontStyleAndWeight = FontStyle.Bold;
            label.style.opacity = 0.78f;
            return label;
        }

        private static VisualElement CreateBehaviorGroupDecisionChainRow(
            BehaviorGroupDecisionChainItem item)
        {
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Stretch;
            row.style.minHeight = 42;
            row.style.marginBottom = 4;
            row.style.paddingLeft = 6;
            row.style.paddingRight = 6;
            row.style.paddingTop = 5;
            row.style.paddingBottom = 5;
            row.style.minWidth = 0;
            row.style.borderTopWidth = 1;
            row.style.borderBottomWidth = 1;
            row.style.borderLeftWidth = 1;
            row.style.borderRightWidth = 1;
            Color border = item.HasIssue
                ? new Color(0.54f, 0.33f, 0.18f)
                : new Color(0.22f, 0.22f, 0.22f);
            row.style.borderTopColor = border;
            row.style.borderBottomColor = border;
            row.style.borderLeftColor = border;
            row.style.borderRightColor = border;
            row.style.backgroundColor = ResolveDecisionChainRowColor(item);
            row.tooltip = item.EntryId
                + "\n"
                + item.BehaviorId
                + (string.IsNullOrWhiteSpace(item.DiagnosticsText)
                    ? string.Empty
                    : "\n" + item.DiagnosticsText);

            row.Add(CreateDecisionChainCell(
                item.Priority.ToString(CultureInfo.InvariantCulture),
                62,
                0,
                true));
            row.Add(CreateDecisionChainCell(item.DisplayName, 170, 1, true));
            row.Add(CreateDecisionChainCell(item.ConditionSummary, 240, 2, false));
            row.Add(CreateDecisionChainCell(item.OutputSummary, 170, 1, false));
            row.Add(CreateDecisionChainCell(item.StatusText, 110, 0, true));
            return row;
        }

        private static Label CreateDecisionChainCell(
            string text,
            float width,
            float flexGrow,
            bool bold)
        {
            Label label = CreateWrappedLabel(text);
            label.style.width = width;
            label.style.flexGrow = flexGrow;
            label.style.flexShrink = 1;
            label.style.marginRight = 6;
            label.style.marginBottom = 0;
            label.tooltip = text ?? string.Empty;
            if (bold)
                label.style.unityFontStyleAndWeight = FontStyle.Bold;
            return label;
        }

        private static Color ResolveDecisionChainRowColor(BehaviorGroupDecisionChainItem item)
        {
            if (item.Disabled)
                return new Color(0.20f, 0.20f, 0.20f);
            if (item.HasIssue)
                return new Color(0.30f, 0.20f, 0.12f);
            if (item.CountsAsExecutable)
                return new Color(0.15f, 0.23f, 0.16f);
            return new Color(0.16f, 0.19f, 0.24f);
        }

        private List<BehaviorGroupDecisionChainItem> BuildBehaviorGroupDecisionChainItems(
            BehaviorGroupPreviewCandidate selected)
        {
            var items = new List<BehaviorGroupDecisionChainItem>();
            if (selected == null || selected.Group == null)
                return items;

            List<CharacterTestRuntimeAiPlannerBehaviorModuleEntry> entries =
                CopyBehaviorGroupEntriesForDisplay(selected.Group.Entries);
            for (int i = 0; i < entries.Count; i++)
            {
                CharacterTestRuntimeAiPlannerBehaviorModuleEntry entry = entries[i];
                if (entry == null)
                    continue;

                CharacterTestRuntimeAiPlannerBehaviorCompositionCatalog.TryGetDefinition(
                    entry.BehaviorId,
                    out CharacterTestRuntimeAiPlannerBehaviorModuleDefinition definition);
                CharacterTestRuntimeAiPlannerBehaviorModuleSummary summary =
                    FindBehaviorModuleSummary(selected.Compile, entry.EntryId, entry.BehaviorId);
                items.Add(CreateBehaviorGroupDecisionChainItem(entry, definition, summary));
            }

            return items;
        }

        private static List<CharacterTestRuntimeAiPlannerBehaviorModuleEntry> CopyBehaviorGroupEntriesForDisplay(
            IReadOnlyList<CharacterTestRuntimeAiPlannerBehaviorModuleEntry> entries)
        {
            var copy = new List<CharacterTestRuntimeAiPlannerBehaviorModuleEntry>();
            if (entries == null)
                return copy;

            for (int i = 0; i < entries.Count; i++)
                copy.Add(entries[i]);
            copy.Sort(CompareBehaviorGroupEntriesForDisplay);
            return copy;
        }

        private static int CompareBehaviorGroupEntriesForDisplay(
            CharacterTestRuntimeAiPlannerBehaviorModuleEntry left,
            CharacterTestRuntimeAiPlannerBehaviorModuleEntry right)
        {
            if (left == null && right == null)
                return 0;
            if (left == null)
                return -1;
            if (right == null)
                return 1;

            int order = left.Order.CompareTo(right.Order);
            if (order != 0)
                return order;
            return string.CompareOrdinal(left.EntryId, right.EntryId);
        }

        private static BehaviorGroupDecisionChainItem CreateBehaviorGroupDecisionChainItem(
            CharacterTestRuntimeAiPlannerBehaviorModuleEntry entry,
            CharacterTestRuntimeAiPlannerBehaviorModuleDefinition definition,
            CharacterTestRuntimeAiPlannerBehaviorModuleSummary summary)
        {
            CharacterTestRuntimeAiPlannerBehaviorCardView card =
                CharacterTestRuntimeAiPlannerBehaviorCardPresentation.CreateInstanceCard(
                    entry,
                    definition,
                    summary,
                    string.Empty);

            return new BehaviorGroupDecisionChainItem
            {
                Priority = entry.Order,
                EntryId = entry.EntryId,
                BehaviorId = entry.BehaviorId,
                DisplayName = card.Title,
                ConditionSummary = card.TriggerText,
                OutputSummary = card.OutputText,
                StatusText = card.StatusText,
                DiagnosticsText = card.DiagnosticsText,
                Disabled = entry.Disabled,
                HasIssue = card.HasIssue,
                CountsAsExecutable = card.CountsAsExecutable
            };
        }

        private static string FormatBehaviorDecisionConditions(
            CharacterTestRuntimeAiPlannerBehaviorModuleEntry entry,
            CharacterTestRuntimeAiPlannerBehaviorModuleDefinition definition,
            CharacterTestRuntimeAiPlannerBehaviorModuleSummary summary)
        {
            if (entry.Disabled)
                return "组内临时禁用";
            if (definition == null)
                return "行为定义缺失";
            if (definition.Conditions.Count == 0)
            {
                if (!string.IsNullOrWhiteSpace(summary.RequiredFactsText)
                    && !string.Equals(summary.RequiredFactsText, "none", StringComparison.OrdinalIgnoreCase))
                {
                    return "依赖 facts: " + summary.RequiredFactsText;
                }

                return "无触发条件";
            }

            int displayCount = Math.Min(definition.Conditions.Count, 3);
            string text = string.Empty;
            for (int i = 0; i < displayCount; i++)
            {
                if (i > 0)
                    text += "；";
                text += FormatBehaviorConditionForDisplay(definition.Conditions[i], definition, entry);
            }

            if (definition.Conditions.Count > displayCount)
            {
                text += "；等 "
                    + definition.Conditions.Count.ToString(CultureInfo.InvariantCulture)
                    + " 项";
            }

            return text;
        }

        private static string FormatBehaviorConditionForDisplay(
            CharacterTestRuntimeAiPlannerBehaviorConditionDefinition condition,
            CharacterTestRuntimeAiPlannerBehaviorModuleDefinition definition,
            CharacterTestRuntimeAiPlannerBehaviorModuleEntry entry)
        {
            if (condition.Operator == CharacterTestRuntimeAiPlannerConditionOperator.Equals)
                return FormatBehaviorBoolCondition(condition.Fact, condition.BoolValue);
            if (condition.Operator == CharacterTestRuntimeAiPlannerConditionOperator.NotEquals)
                return FormatBehaviorBoolCondition(condition.Fact, !condition.BoolValue);

            float value = !string.IsNullOrWhiteSpace(condition.FloatParameterKey)
                ? entry.GetFloat(definition, condition.FloatParameterKey)
                : condition.FloatValue;
            string op = condition.Operator == CharacterTestRuntimeAiPlannerConditionOperator.GreaterOrEqual
                ? ">="
                : "<=";
            return FormatBehaviorFactName(condition.Fact)
                + " "
                + op
                + " "
                + value.ToString("0.##", CultureInfo.InvariantCulture);
        }

        private static string FormatBehaviorBoolCondition(
            CharacterTestRuntimeAiPlannerFactKind fact,
            bool value)
        {
            switch (fact)
            {
                case CharacterTestRuntimeAiPlannerFactKind.TargetExists:
                    return value ? "有目标" : "无目标";
                case CharacterTestRuntimeAiPlannerFactKind.TargetInAttackRange:
                    return value ? "目标在攻击范围" : "目标不在攻击范围";
                case CharacterTestRuntimeAiPlannerFactKind.SelfActionRunning:
                    return value ? "自身动作执行中" : "自身没有动作占用";
                case CharacterTestRuntimeAiPlannerFactKind.MovePathAvailable:
                    return value ? "路径可用" : "路径不可用";
                case CharacterTestRuntimeAiPlannerFactKind.TargetVisible:
                    return value ? "目标可见" : "目标不可见";
                case CharacterTestRuntimeAiPlannerFactKind.TargetLineOfSightBlocked:
                    return value ? "视线被遮挡" : "视线未遮挡";
                case CharacterTestRuntimeAiPlannerFactKind.SelfOnHardSlick:
                    return value ? "位于硬滑地表" : "不在硬滑地表";
                case CharacterTestRuntimeAiPlannerFactKind.SelfNearMovableObject:
                    return value ? "附近有可移动物" : "附近无可移动物";
                case CharacterTestRuntimeAiPlannerFactKind.MoveDirectPathClear:
                    return value ? "直线路径清晰" : "直线路径受阻";
                case CharacterTestRuntimeAiPlannerFactKind.EnvironmentNearObstacle:
                    return value ? "附近有障碍" : "附近无障碍";
                case CharacterTestRuntimeAiPlannerFactKind.EnvironmentNearHazard:
                    return value ? "附近有危险" : "附近无危险";
                case CharacterTestRuntimeAiPlannerFactKind.EnvironmentNearUsefulObject:
                    return value ? "附近有可用物" : "附近无可用物";
                default:
                    return FormatBehaviorFactName(fact) + " = " + (value ? "true" : "false");
            }
        }

        private static string FormatBehaviorFactName(CharacterTestRuntimeAiPlannerFactKind fact)
        {
            switch (fact)
            {
                case CharacterTestRuntimeAiPlannerFactKind.TargetExists:
                    return "目标存在";
                case CharacterTestRuntimeAiPlannerFactKind.TargetInAttackRange:
                    return "目标在攻击范围";
                case CharacterTestRuntimeAiPlannerFactKind.TargetDistance:
                    return "目标距离";
                case CharacterTestRuntimeAiPlannerFactKind.SelfActionRunning:
                    return "自身动作占用";
                case CharacterTestRuntimeAiPlannerFactKind.MovePathAvailable:
                    return "路径可用";
                case CharacterTestRuntimeAiPlannerFactKind.TargetVisible:
                    return "目标可见";
                case CharacterTestRuntimeAiPlannerFactKind.TargetLineOfSightBlocked:
                    return "视线遮挡";
                case CharacterTestRuntimeAiPlannerFactKind.TargetLastSeenAgeFrames:
                    return "目标丢失帧数";
                case CharacterTestRuntimeAiPlannerFactKind.TargetThreatScore:
                    return "目标威胁分";
                case CharacterTestRuntimeAiPlannerFactKind.SelfSurfaceKind:
                    return "自身地表";
                case CharacterTestRuntimeAiPlannerFactKind.SelfOnHardSlick:
                    return "硬滑地表";
                case CharacterTestRuntimeAiPlannerFactKind.SelfNearMovableObject:
                    return "附近可移动物";
                case CharacterTestRuntimeAiPlannerFactKind.MoveDirectPathClear:
                    return "直线路径";
                case CharacterTestRuntimeAiPlannerFactKind.MoveCandidateIndex:
                    return "移动候选索引";
                case CharacterTestRuntimeAiPlannerFactKind.EnvironmentNearObstacle:
                    return "附近障碍";
                case CharacterTestRuntimeAiPlannerFactKind.EnvironmentNearHazard:
                    return "附近危险";
                case CharacterTestRuntimeAiPlannerFactKind.EnvironmentNearUsefulObject:
                    return "附近可用物";
                default:
                    return fact.ToString();
            }
        }

        private static string FormatBehaviorDecisionOutput(
            CharacterTestRuntimeAiPlannerBehaviorModuleDefinition definition,
            CharacterTestRuntimeAiPlannerBehaviorModuleSummary summary)
        {
            if (!summary.Enabled)
                return "不输出";
            if (!string.IsNullOrWhiteSpace(summary.GeneratedActionsText)
                && !string.Equals(summary.GeneratedActionsText, "no action", StringComparison.Ordinal)
                && !string.Equals(summary.GeneratedActionsText, "disabled", StringComparison.Ordinal))
            {
                return FormatBehaviorGeneratedActionsForDisplay(summary.GeneratedActionsText);
            }

            if (definition == null)
                return "无法生成";
            if (definition.ActionPrimitive == CharacterTestRuntimeAiPlannerBehaviorActionPrimitive.None)
            {
                return definition.Category == CharacterTestRuntimeAiPlannerBehaviorModuleCategory.Targeting
                    ? "选择目标/更新感知事实"
                    : "不直接输出动作";
            }

            return FormatBehaviorActionPrimitiveForDisplay(definition.ActionPrimitive);
        }

        private static string FormatBehaviorActionPrimitiveForDisplay(
            CharacterTestRuntimeAiPlannerBehaviorActionPrimitive primitive)
        {
            switch (primitive)
            {
                case CharacterTestRuntimeAiPlannerBehaviorActionPrimitive.MoveToTarget:
                    return "移动到目标";
                case CharacterTestRuntimeAiPlannerBehaviorActionPrimitive.Strafe:
                    return "侧移/绕行";
                case CharacterTestRuntimeAiPlannerBehaviorActionPrimitive.Retreat:
                    return "撤退";
                case CharacterTestRuntimeAiPlannerBehaviorActionPrimitive.PrimaryAttack:
                    return "普通攻击";
                case CharacterTestRuntimeAiPlannerBehaviorActionPrimitive.CastAbilitySlot:
                    return "释放技能槽";
                case CharacterTestRuntimeAiPlannerBehaviorActionPrimitive.Wait:
                    return "等待/保持";
                default:
                    return "不直接输出动作";
            }
        }

        private static string ResolveBehaviorDecisionStatus(
            CharacterTestRuntimeAiPlannerBehaviorModuleEntry entry,
            CharacterTestRuntimeAiPlannerBehaviorModuleDefinition definition,
            CharacterTestRuntimeAiPlannerBehaviorModuleSummary summary)
        {
            if (entry.Disabled)
                return "已禁用";
            if (definition == null)
                return "定义缺失";
            if (!string.IsNullOrWhiteSpace(summary.DiagnosticsText))
                return "有诊断";
            if (definition.ActionPrimitive == CharacterTestRuntimeAiPlannerBehaviorActionPrimitive.None)
                return "准备事实";
            if (summary.GeneratedActionCount <= 0)
                return "未生成动作";
            return "可执行";
        }

        private static int CountDisabledBehaviorItems(
            IReadOnlyList<BehaviorGroupDecisionChainItem> items)
        {
            int count = 0;
            if (items == null)
                return count;

            for (int i = 0; i < items.Count; i++)
            {
                if (items[i].Disabled)
                    count++;
            }

            return count;
        }

        private static int CountExecutableBehaviorItems(
            IReadOnlyList<BehaviorGroupDecisionChainItem> items)
        {
            int count = 0;
            if (items == null)
                return count;

            for (int i = 0; i < items.Count; i++)
            {
                if (items[i].CountsAsExecutable)
                    count++;
            }

            return count;
        }

        private static string BuildBehaviorGroupStrategySummary(
            BehaviorGroupPreviewCandidate selected,
            IReadOnlyList<BehaviorGroupDecisionChainItem> items)
        {
            if (selected == null || selected.Group == null || selected.Group.Entries.Count == 0)
                return "这个组还没有行为，角色不会从行为组得到可用策略。";
            if (!selected.Compile.Success)
                return "这个组当前编译失败，修复诊断前不会成为可运行策略。";

            string order = BuildBehaviorGroupOrderSummary(items, 4);
            if (string.IsNullOrWhiteSpace(order))
                return "这个组没有可执行行为。";

            return "这个组会按优先级尝试：" + order + "。";
        }

        private static string BuildBehaviorGroupOrderSummary(
            IReadOnlyList<BehaviorGroupDecisionChainItem> items,
            int maxCount)
        {
            if (items == null || items.Count == 0)
                return "none";

            string text = string.Empty;
            int written = 0;
            for (int i = 0; i < items.Count && written < maxCount; i++)
            {
                BehaviorGroupDecisionChainItem item = items[i];
                if (item.Disabled)
                    continue;

                if (written > 0)
                    text += " -> ";
                text += item.DisplayName;
                written++;
            }

            if (written == 0)
                return "none";
            if (items.Count > written)
                text += " -> ...";
            return text;
        }

        private static VisualElement CreateBehaviorGroupWarningsPanel(
            IReadOnlyList<BehaviorGroupDecisionChainItem> items)
        {
            if (items == null || items.Count == 0)
                return null;

            var root = new VisualElement();
            int warningCount = 0;
            for (int i = 0; i < items.Count; i++)
            {
                BehaviorGroupDecisionChainItem item = items[i];
                bool noActionIssue = string.Equals(
                    item.StatusText,
                    "未生成动作",
                    StringComparison.Ordinal);
                if (!item.Disabled && !item.HasIssue && !noActionIssue)
                    continue;

                if (warningCount == 0)
                    root.Add(CreateSectionTitle("限制与风险"));
                warningCount++;

                string message = item.DisplayName + "：";
                if (item.Disabled)
                    message += "已禁用";
                else if (!string.IsNullOrWhiteSpace(item.DiagnosticsText))
                    message += item.DiagnosticsText;
                else
                    message += item.StatusText;
                root.Add(CreateWrappedLabel(message));
            }

            return warningCount > 0 ? root : null;
        }

        private List<BehaviorGroupPreviewCandidate> BuildBehaviorGroupPreviewCandidates()
        {
            var candidates = new List<BehaviorGroupPreviewCandidate>(3);
            CharacterTestRuntimeAiPlannerProfile runtimeProfile =
                _profileLoaded ? _model.ToRuntimeProfile() : null;
            CharacterTestRuntimeAiPlannerBehaviorCompositionProfile currentDraft =
                EnsureBehaviorCompositionProfile();
            candidates.Add(new BehaviorGroupPreviewCandidate(
                BehaviorGroupPreviewCandidateCurrentDraftKey,
                "当前行为组草稿",
                BehaviorGroupPreviewSourceKind.CurrentDraft,
                "当前窗口草稿",
                currentDraft));

            CharacterTestRuntimeAiPlannerBehaviorCompositionProfile builtInDefault =
                CharacterTestRuntimeAiPlannerBehaviorCompositionCatalog.CreateDefaultProfile(runtimeProfile);
            candidates.Add(new BehaviorGroupPreviewCandidate(
                BehaviorGroupPreviewCandidateBuiltInDefaultKey,
                "内置默认行为组",
                BehaviorGroupPreviewSourceKind.BuiltInDefault,
                "CharacterTest built-in",
                builtInDefault));

            CharacterTestRuntimeAiPlannerBehaviorLoadoutCatalog loadoutCatalog =
                CharacterTestRuntimeAiPlannerBehaviorLoadoutCatalog.CreateDefault(runtimeProfile);
            CharacterTestRuntimeAiPlannerBehaviorLoadoutDefinition loadout =
                loadoutCatalog.Loadouts.Count > 0 ? loadoutCatalog.Loadouts[0] : null;
            CharacterTestRuntimeAiPlannerBehaviorLoadoutVariantDefinition variant =
                loadout != null && loadout.Variants.Count > 0 ? loadout.Variants[0] : null;
            CharacterTestRuntimeAiPlannerBehaviorCompositionProfile loadoutGroup =
                variant != null ? variant.BehaviorGroup : builtInDefault;
            string loadoutName = variant != null
                ? loadout.DisplayName + " / " + variant.DisplayName
                : "默认 Loadout variant";
            candidates.Add(new BehaviorGroupPreviewCandidate(
                BehaviorGroupPreviewCandidateDefaultLoadoutVariantKey,
                loadoutName,
                BehaviorGroupPreviewSourceKind.DefaultLoadoutVariant,
                "默认 Loadout variant",
                loadoutGroup));
            return candidates;
        }

        private BehaviorGroupPreviewCandidate ResolveSelectedBehaviorGroupPreviewCandidate(
            IReadOnlyList<BehaviorGroupPreviewCandidate> candidates)
        {
            if (candidates != null)
            {
                for (int i = 0; i < candidates.Count; i++)
                {
                    if (string.Equals(
                            candidates[i].Key,
                            _selectedBehaviorGroupPreviewCandidateKey,
                            System.StringComparison.Ordinal))
                    {
                        return candidates[i];
                    }
                }

                if (candidates.Count > 0)
                {
                    _selectedBehaviorGroupPreviewCandidateKey = BehaviorGroupPreviewCandidateCurrentDraftKey;
                    return candidates[0];
                }
            }

            CharacterTestRuntimeAiPlannerBehaviorCompositionProfile fallback =
                CharacterTestRuntimeAiPlannerBehaviorCompositionCatalog.CreateDefaultProfile();
            return new BehaviorGroupPreviewCandidate(
                BehaviorGroupPreviewCandidateCurrentDraftKey,
                "当前行为组草稿",
                BehaviorGroupPreviewSourceKind.CurrentDraft,
                "fallback",
                fallback);
        }

    }
}
