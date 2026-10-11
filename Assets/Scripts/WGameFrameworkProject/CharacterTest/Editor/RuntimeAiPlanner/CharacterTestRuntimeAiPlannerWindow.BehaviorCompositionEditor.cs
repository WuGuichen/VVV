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
        private void BuildBehaviorCompositionLeftPanel(VisualElement root)
        {
            CharacterTestRuntimeAiPlannerBehaviorCompositionProfile profile =
                EnsureBehaviorCompositionProfile();
            CharacterTestRuntimeAiPlannerBehaviorCompileResult compile =
                CharacterTestRuntimeAiPlannerBehaviorCompositionCompiler.Compile(profile);

            root.Add(CreateSectionTitle("行为组编辑"));
            root.Add(CreateWrappedLabel(profile.GroupId));
            root.Add(CreateWrappedLabel("组内行为 " + profile.Entries.Count.ToString(CultureInfo.InvariantCulture)));
            root.Add(CreateWrappedLabel(
                "行为定义库 " + CharacterTestRuntimeAiPlannerBehaviorCompositionCatalog.Definitions.Count.ToString(
                    CultureInfo.InvariantCulture)));
            root.Add(CreateWrappedLabel(compile.Success ? "编译通过" : "编译失败"));
            root.Add(CreateSpacer(8));

            VisualElement actions = CreateButtonRow();
            actions.Add(CreateMiniButton("重置为内置行为组", () =>
            {
                _behaviorCompositionProfile =
                    CharacterTestRuntimeAiPlannerBehaviorCompositionCatalog.CreateDefaultProfile(
                        _model.ToRuntimeProfile());
                _lastBehaviorCompositionReport = string.Empty;
                _statusText = "已重置行为组草稿";
                RefreshView();
            }));
            actions.Add(CreateMiniButton("复制行为组 JSON", CopyBehaviorCompositionJson));
            root.Add(actions);

            var boundary = new Foldout
            {
                text = "边界",
                value = false
            };
            boundary.Add(CreateWrappedLabel("行为组只保存已加入的行为实例，不是全行为库启用表。"));
            boundary.Add(CreateWrappedLabel("Play Mode 修改只走显式热更新，不写回 JSON，也不进入 SaveState/network/replay/hash。"));
            root.Add(boundary);
        }

        private void BuildBehaviorCompositionTab(VisualElement root)
        {
            CharacterTestRuntimeAiPlannerBehaviorCompositionProfile profile =
                EnsureBehaviorCompositionProfile();
            CharacterTestRuntimeAiPlannerBehaviorCompileResult compile =
                CharacterTestRuntimeAiPlannerBehaviorCompositionCompiler.Compile(profile);
            _lastBehaviorCompositionReport = compile.Report != null
                ? compile.Report.ToText()
                : string.Empty;
            CharacterTestRuntimeAiPlannerBehaviorActionChainPreview actionChainPreview =
                compile.Profile != null
                    ? CharacterTestRuntimeAiPlannerPlayMonitorActionChainCache.BuildPreview(compile.Profile)
                    : new CharacterTestRuntimeAiPlannerBehaviorActionChainPreview(null, string.Empty);

            root.style.minWidth = 0;
            root.style.minHeight = 0;

            var scroll = new ScrollView(ScrollViewMode.Vertical);
            scroll.style.flexGrow = 1;
            scroll.style.minHeight = 0;
            scroll.style.minWidth = 0;
            scroll.Add(CreateBehaviorGroupHeader(profile, compile));
            scroll.Add(CreateBehaviorGroupGuideFoldout());
            VisualElement columns = new VisualElement();
            columns.style.flexDirection = FlexDirection.Row;
            columns.style.height = 360;
            columns.style.minHeight = 280;
            columns.style.minWidth = 0;
            columns.Add(CreateBehaviorDefinitionLibraryPanel(profile));
            columns.Add(CreateBehaviorGroupEntriesPanel(profile, compile, actionChainPreview));
            scroll.Add(columns);
            root.Add(scroll);
        }

        private VisualElement CreateBehaviorGroupHeader(
            CharacterTestRuntimeAiPlannerBehaviorCompositionProfile profile,
            CharacterTestRuntimeAiPlannerBehaviorCompileResult compile)
        {
            var header = new VisualElement();
            header.style.flexDirection = FlexDirection.Row;
            header.style.alignItems = Align.Center;
            header.style.marginBottom = 6;
            header.style.minWidth = 0;

            var textColumn = new VisualElement();
            textColumn.style.flexGrow = 1;
            textColumn.style.minWidth = 0;
            Label title = CreateSectionTitle("行为组编辑");
            title.style.marginBottom = 1;
            textColumn.Add(title);
            string summary = profile.Entries.Count.ToString(CultureInfo.InvariantCulture)
                + " 个组内行为 / "
                + CharacterTestRuntimeAiPlannerBehaviorCompositionCatalog.Definitions.Count.ToString(
                    CultureInfo.InvariantCulture)
                + " 个库定义 / "
                + (compile.Success ? "编译通过" : "编译失败");
            Label subtitle = CreateWrappedLabel(summary);
            subtitle.style.opacity = 0.72f;
            subtitle.style.marginBottom = 0;
            textColumn.Add(subtitle);
            header.Add(textColumn);

            VisualElement commandRow = CreateButtonRow();
            commandRow.style.marginBottom = 0;
            commandRow.style.flexShrink = 0;
            commandRow.Add(CreateMiniButton("查看预览", SwitchToBehaviorGroupPreviewCurrentDraft));
            Button applyToProfile = CreatePrimaryButton("编译覆盖底层 Profile 草稿", ApplyCompiledBehaviorCompositionToEditorProfile);
            applyToProfile.SetEnabled(compile.Success);
            commandRow.Add(applyToProfile);
            Button hotUpdate = CreateMiniButton("应用到 Play Mode", ApplyBehaviorCompositionHotUpdate);
            hotUpdate.SetEnabled(EditorApplication.isPlaying
                && BehaviorHotUpdateEnabled
                && FindBehaviorHotUpdateTarget() != null
                && compile.Success);
            commandRow.Add(hotUpdate);
            header.Add(commandRow);
            return header;
        }

        private Foldout CreateBehaviorGroupGuideFoldout()
        {
            var foldout = new Foldout
            {
                name = BehaviorGroupGuideFoldoutName,
                text = "说明与边界",
                value = BehaviorGroupGuideExpanded
            };
            foldout.style.marginBottom = 6;
            foldout.RegisterValueChangedCallback(evt =>
            {
                BehaviorGroupGuideExpanded = evt.newValue;
            });
            foldout.Add(CreateWrappedLabel("行为组只保存已经加入的行为实例；只有组内实例会被编译为角色实际使用的 Runtime AI Planner。"));
            foldout.Add(CreateWrappedLabel("行为定义库未加入项不会出现在流程图中。Play Mode 修改只走显式热更新，不写回 JSON，也不进入 SaveState/network/replay/hash。"));
            return foldout;
        }

        private Foldout CreateBehaviorGroupEditFoldout(
            CharacterTestRuntimeAiPlannerBehaviorCompositionProfile profile,
            CharacterTestRuntimeAiPlannerBehaviorCompileResult compile,
            CharacterTestRuntimeAiPlannerBehaviorActionChainPreview actionChainPreview)
        {
            var foldout = new Foldout
            {
                name = BehaviorGroupEditFoldoutName,
                text = "编辑行为组：行为定义库 / 当前组实例",
                value = BehaviorGroupEditExpanded
            };
            foldout.style.marginTop = 6;
            foldout.RegisterValueChangedCallback(evt =>
            {
                BehaviorGroupEditExpanded = evt.newValue;
            });

            VisualElement columns = new VisualElement();
            columns.style.flexDirection = FlexDirection.Row;
            columns.style.height = 320;
            columns.style.minHeight = 260;
            columns.style.minWidth = 0;
            columns.Add(CreateBehaviorDefinitionLibraryPanel(profile));
            columns.Add(CreateBehaviorGroupEntriesPanel(profile, compile, actionChainPreview));
            foldout.Add(columns);
            return foldout;
        }

        private VisualElement CreateBehaviorGroupFlowPreviewPanel(
            CharacterTestRuntimeAiPlannerBehaviorCompileResult compile,
            bool interactiveActionStrip,
            string titleText = "底层 Runtime AI Planner 流程",
            bool includeActionStrip = true)
        {
            var panel = new VisualElement();
            panel.style.marginTop = 4;
            panel.style.marginBottom = 6;
            panel.style.minWidth = 0;

            VisualElement header = new VisualElement();
            header.style.flexDirection = FlexDirection.Row;
            header.style.alignItems = Align.Center;
            header.style.minHeight = 26;
            header.style.marginBottom = 6;
            Label title = CreateSectionTitle(titleText);
            title.style.flexGrow = 1;
            title.style.marginBottom = 0;
            header.Add(title);

            Button expand = CreatePrimaryButton("放大预览", () => OpenBehaviorGroupFlowPreviewWindow(compile));
            expand.name = BehaviorGroupFlowExpandButtonName;
            expand.tooltip = "打开当前行为组编译产物的只读流程图。";
            expand.style.flexShrink = 0;
            expand.SetEnabled(compile != null && compile.Success && compile.Profile != null);
            header.Add(expand);
            panel.Add(header);

            if (compile == null || !compile.Success || compile.Profile == null)
            {
                panel.Add(CreateNotice("行为组编译失败。修复诊断后再查看流程预览。"));
                return panel;
            }

            CharacterTestRuntimeAiPlannerEditorModel previewModel =
                CreateBehaviorGroupFlowPreviewModel(compile.Profile);
            panel.Add(new CharacterTestRuntimeAiPlannerFlowPreview(
                previewModel,
                compile.Report,
                previewModel.InitialStateId,
                -1,
                SelectBehaviorGroupFlowState,
                SelectBehaviorGroupFlowTransition,
                CharacterTestRuntimeAiPlannerFlowPreviewMode.Overview,
                GetFlowPreviewViewportWidth(),
                _behaviorGroupFlowPreviewZoom,
                SetBehaviorGroupFlowPreviewZoom,
                FitBehaviorGroupFlowPreviewZoom));
            if (includeActionStrip)
                panel.Add(CreateBehaviorGroupGeneratedActionStrip(compile, interactiveActionStrip));
            return panel;
        }

        private VisualElement CreateBehaviorGroupGeneratedActionStrip(
            CharacterTestRuntimeAiPlannerBehaviorCompileResult compile,
            bool interactive)
        {
            var root = new VisualElement();
            root.name = BehaviorGroupGeneratedActionStripName;
            root.style.flexDirection = FlexDirection.Row;
            root.style.alignItems = Align.Center;
            root.style.marginTop = 5;
            root.style.marginBottom = 2;
            root.style.minWidth = 0;

            Label title = CreateSectionTitle("组内行为执行结果");
            title.style.width = 136;
            title.style.flexShrink = 0;
            title.style.marginBottom = 0;
            root.Add(title);

            var scroller = new ScrollView(ScrollViewMode.Horizontal);
            scroller.name = BehaviorGroupGeneratedActionScrollerName;
            scroller.style.flexGrow = 1;
            scroller.style.minWidth = 0;
            scroller.style.height = 32;
            scroller.contentContainer.style.flexDirection = FlexDirection.Row;
            scroller.contentContainer.style.flexWrap = Wrap.NoWrap;
            var chips = new VisualElement();
            chips.style.flexDirection = FlexDirection.Row;
            chips.style.flexWrap = Wrap.NoWrap;
            chips.style.minWidth = 0;
            IReadOnlyList<CharacterTestRuntimeAiPlannerBehaviorModuleSummary> summaries =
                compile != null
                    ? compile.ModuleSummaries
                    : Array.Empty<CharacterTestRuntimeAiPlannerBehaviorModuleSummary>();
            for (int i = 0; i < summaries.Count; i++)
            {
                CharacterTestRuntimeAiPlannerBehaviorModuleSummary summary = summaries[i];
                chips.Add(CreateBehaviorGroupActionChip(summary, interactive));
            }

            scroller.Add(chips);
            root.Add(scroller);
            return root;
        }

        private VisualElement CreateBehaviorGroupActionChip(
            CharacterTestRuntimeAiPlannerBehaviorModuleSummary summary,
            bool interactive)
        {
            string generated = string.IsNullOrWhiteSpace(summary.GeneratedActionsText)
                ? (summary.Enabled ? "no action" : "disabled")
                : summary.GeneratedActionsText;
            string displayName = string.IsNullOrWhiteSpace(summary.DisplayName)
                ? summary.EntryId
                : summary.DisplayName;
            TextElement chip = interactive
                ? new Button(() => SelectBehaviorGroupEntry(summary.EntryId))
                : new Label();
            chip.text = displayName + " -> " + FormatBehaviorGeneratedActionsForDisplay(generated);
            chip.tooltip = summary.DisplayName
                + "\n"
                + "Entry: "
                + summary.EntryId
                + "\n"
                + summary.ModuleId
                + "\n"
                + summary.Intent
                + (string.IsNullOrWhiteSpace(summary.DiagnosticsText)
                    ? string.Empty
                    : "\n" + summary.DiagnosticsText);
            chip.style.alignItems = Align.Center;
            chip.style.justifyContent = Justify.Center;
            if (!interactive)
            {
                chip.style.borderTopWidth = 1;
                chip.style.borderBottomWidth = 1;
                chip.style.borderLeftWidth = 1;
                chip.style.borderRightWidth = 1;
                chip.style.borderTopColor = new Color(0.20f, 0.20f, 0.20f);
                chip.style.borderBottomColor = new Color(0.20f, 0.20f, 0.20f);
                chip.style.borderLeftColor = new Color(0.20f, 0.20f, 0.20f);
                chip.style.borderRightColor = new Color(0.20f, 0.20f, 0.20f);
            }
            chip.style.marginRight = 4;
            chip.style.marginBottom = 0;
            chip.style.height = 24;
            chip.style.minWidth = 96;
            chip.style.maxWidth = 260;
            chip.style.paddingLeft = 7;
            chip.style.paddingRight = 7;
            chip.style.unityTextAlign = TextAnchor.MiddleLeft;
            chip.style.overflow = Overflow.Hidden;
            chip.style.whiteSpace = WhiteSpace.NoWrap;
            chip.style.textOverflow = TextOverflow.Ellipsis;
            chip.style.backgroundColor = ResolveBehaviorGroupActionChipColor(summary);
            return chip;
        }

        private static string FormatBehaviorGeneratedActionsForDisplay(string generatedActionsText)
        {
            if (string.IsNullOrWhiteSpace(generatedActionsText))
                return "未生成动作";
            if (string.Equals(generatedActionsText, "disabled", StringComparison.Ordinal))
                return "已禁用，不会执行";
            if (string.Equals(generatedActionsText, "no action", StringComparison.Ordinal))
                return "未生成动作";

            string[] actions = generatedActionsText.Split(',');
            for (int i = 0; i < actions.Length; i++)
                actions[i] = FormatBehaviorGeneratedActionToken(actions[i].Trim());
            return string.Join("，", actions);
        }

        private static string FormatBehaviorGeneratedActionToken(string token)
        {
            if (string.IsNullOrWhiteSpace(token))
                return "未生成动作";

            string action = token;
            string state = string.Empty;
            int stateSeparator = token.IndexOf('@');
            if (stateSeparator >= 0)
            {
                action = token.Substring(0, stateSeparator);
                state = token.Substring(stateSeparator + 1);
            }

            string actionText = action;
            switch (action)
            {
                case "MoveToTarget":
                    actionText = "移动到目标";
                    break;
                case "Strafe":
                    actionText = "侧移/绕行";
                    break;
                case "Retreat":
                    actionText = "撤退";
                    break;
                case "PrimaryAttack":
                    actionText = "普通攻击";
                    break;
                case "CastAbilitySlot":
                    actionText = "释放技能槽";
                    break;
                case "Wait":
                    actionText = "等待/保持";
                    break;
            }

            return string.IsNullOrWhiteSpace(state)
                ? actionText
                : actionText + "（" + state + "）";
        }

        private static Color ResolveBehaviorGroupActionChipColor(
            CharacterTestRuntimeAiPlannerBehaviorModuleSummary summary)
        {
            if (!summary.Enabled)
                return new Color(0.24f, 0.24f, 0.24f);
            if (!string.IsNullOrWhiteSpace(summary.DiagnosticsText))
                return new Color(0.42f, 0.28f, 0.12f);
            if (summary.GeneratedActionCount <= 0)
                return new Color(0.18f, 0.24f, 0.32f);

            return new Color(0.18f, 0.30f, 0.20f);
        }

        private void BuildBehaviorCompositionInspector(VisualElement root)
        {
            CharacterTestRuntimeAiPlannerBehaviorCompositionProfile profile =
                EnsureBehaviorCompositionProfile();
            CharacterTestRuntimeAiPlannerBehaviorCompileResult compile =
                CharacterTestRuntimeAiPlannerBehaviorCompositionCompiler.Compile(profile);

            var scroll = new ScrollView(ScrollViewMode.Vertical);
            scroll.style.flexGrow = 1;
            scroll.style.minHeight = 0;
            scroll.style.minWidth = 0;

            scroll.Add(CreateSectionTitle("行为组 Inspector"));
            scroll.Add(CreateBehaviorInspectorLine("Group", profile.GroupId));
            scroll.Add(CreateBehaviorInspectorLine("Targeting", profile.TargetSelectionMode.ToString()));
            scroll.Add(CreateBehaviorInspectorLine(
                "Attack Range",
                profile.AttackRange.ToString("F2", CultureInfo.InvariantCulture)));
            scroll.Add(CreateBehaviorInspectorLine("输出 Profile", compile.Profile != null ? compile.Profile.ProfileId : "none"));
            scroll.Add(CreateBehaviorInspectorLine("状态", compile.Success ? "编译通过" : "编译失败"));

            scroll.Add(CreateSpacer(8));
            scroll.Add(CreateSelectedBehaviorEntryInspector(profile));

            scroll.Add(CreateSpacer(8));
            var advanced = new Foldout
            {
                text = "热更新与诊断",
                value = false
            };
            advanced.style.marginTop = 4;
            var gate = new Toggle("行为组热更新")
            {
                value = BehaviorHotUpdateEnabled
            };
            gate.RegisterValueChangedCallback(evt =>
            {
                BehaviorHotUpdateEnabled = evt.newValue;
                RefreshView();
            });
            advanced.Add(gate);
            CharacterTestRuntimeAiPlannerEditorDebugTarget target = FindBehaviorHotUpdateTarget();
            advanced.Add(CreateBehaviorInspectorLine("Play Mode", EditorApplication.isPlaying ? "running" : "not playing"));
            advanced.Add(CreateBehaviorInspectorLine("Target", target != null ? target.Name : "none"));
            advanced.Add(CreateBehaviorInspectorLine("Profile Slot", "0"));
            advanced.Add(CreateNotice("热更新 overlay 只替换当前 CharacterTest 会话 evaluator。关闭 Play Mode 后消失；失败时保留旧 evaluator。"));
            advanced.Add(CreateSectionTitle("编译诊断"));
            advanced.Add(CreateCompactReportField(string.IsNullOrWhiteSpace(_lastBehaviorCompositionReport)
                ? (compile.Report != null ? compile.Report.ToText() : string.Empty)
                : _lastBehaviorCompositionReport));
            scroll.Add(advanced);
            root.Add(scroll);
        }

        private static VisualElement CreateBehaviorInspectorLine(string label, string value)
        {
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.marginBottom = 3;
            row.style.minWidth = 0;

            var name = new Label(label);
            name.style.width = 112;
            name.style.flexShrink = 0;
            name.style.unityFontStyleAndWeight = FontStyle.Bold;
            row.Add(name);
            Label text = CreateWrappedLabel(value);
            text.style.flexGrow = 1;
            text.tooltip = value ?? string.Empty;
            row.Add(text);
            return row;
        }

        private VisualElement CreateSelectedBehaviorEntryInspector(
            CharacterTestRuntimeAiPlannerBehaviorCompositionProfile profile)
        {
            var root = new VisualElement();
            root.Add(CreateSectionTitle("行为实例"));
            if (profile.Entries.Count == 0)
            {
                root.Add(CreateNotice("当前行为组没有行为。请先从行为定义库搜索结果加入行为。"));
                return root;
            }

            if (_selectedBehaviorGroupEntryIndex < 0
                || _selectedBehaviorGroupEntryIndex >= profile.Entries.Count)
            {
                _selectedBehaviorGroupEntryIndex = 0;
            }

            CharacterTestRuntimeAiPlannerBehaviorModuleEntry entry =
                profile.Entries[_selectedBehaviorGroupEntryIndex];
            CharacterTestRuntimeAiPlannerBehaviorCompositionCatalog.TryGetDefinition(
                entry.BehaviorId,
                out CharacterTestRuntimeAiPlannerBehaviorModuleDefinition definition);

            root.Add(CreateBehaviorInspectorLine("Entry", entry.EntryId));
            root.Add(CreateBehaviorInspectorLine("Behavior", entry.BehaviorId));
            root.Add(CreateBehaviorInspectorLine("名称", definition != null ? definition.DisplayName : "Unknown"));
            root.Add(CreateBehaviorInspectorLine("分类", definition != null ? definition.Category.ToString() : "Unknown"));
            root.Add(CreateBehaviorInspectorLine("Primitive", definition != null ? definition.ActionPrimitive.ToString() : "none"));
            root.Add(CreateBehaviorInspectorLine(
                "所需 facts",
                definition != null
                    ? FormatFactsForEditor(definition.RequiredFacts)
                    : "none"));

            var disabled = new Toggle("临时禁用")
            {
                value = entry.Disabled
            };
            disabled.RegisterValueChangedCallback(evt =>
            {
                entry.Disabled = evt.newValue;
                RefreshView();
            });
            root.Add(disabled);

            var order = new IntegerField("顺序")
            {
                value = entry.Order,
                isDelayed = true
            };
            order.RegisterValueChangedCallback(evt =>
            {
                entry.Order = evt.newValue;
                RefreshView();
            });
            root.Add(order);

            VisualElement actions = CreateButtonRow();
            actions.Add(CreateMiniButton("上移", () => MoveSelectedBehaviorEntry(profile, -1)));
            actions.Add(CreateMiniButton("下移", () => MoveSelectedBehaviorEntry(profile, 1)));
            actions.Add(CreateMiniButton("复制", () => DuplicateSelectedBehaviorEntry(profile)));
            actions.Add(CreateMiniButton("删除", () => DeleteSelectedBehaviorEntry(profile)));
            root.Add(actions);

            if (definition != null && definition.Parameters.Count > 0)
            {
                root.Add(CreateSectionTitle("参数 Override"));
                for (int i = 0; i < definition.Parameters.Count; i++)
                    root.Add(CreateBehaviorParameterField(entry, definition, definition.Parameters[i]));
            }

            return root;
        }

        private VisualElement CreateBehaviorDefinitionLibraryPanel(
            CharacterTestRuntimeAiPlannerBehaviorCompositionProfile profile)
        {
            var panel = new VisualElement();
            panel.style.width = 320;
            panel.style.minWidth = 260;
            panel.style.marginRight = 8;
            panel.style.minHeight = 0;
            panel.Add(CreateSectionTitle("行为卡库"));

            var search = new ToolbarSearchField
            {
                value = _behaviorDefinitionSearchText
            };
            search.RegisterValueChangedCallback(evt =>
            {
                _behaviorDefinitionSearchText = evt.newValue ?? string.Empty;
                RefreshView();
            });
            panel.Add(search);

            List<CharacterTestRuntimeAiPlannerBehaviorModuleDefinition> filtered =
                GetFilteredBehaviorDefinitions(_behaviorDefinitionSearchText);
            panel.Add(CreateReadOnlyLine(
                "结果",
                filtered.Count.ToString(CultureInfo.InvariantCulture)
                + " / "
                + CharacterTestRuntimeAiPlannerBehaviorCompositionCatalog.Definitions.Count.ToString(
                    CultureInfo.InvariantCulture)));

            var listView = new ListView();
            listView.itemsSource = filtered;
            listView.fixedItemHeight = 56;
            listView.virtualizationMethod = CollectionVirtualizationMethod.FixedHeight;
            listView.selectionType = SelectionType.Single;
            listView.style.flexGrow = 1;
            listView.style.minHeight = 0;
            listView.makeItem = () =>
            {
                var row = new VisualElement();
                row.style.flexDirection = FlexDirection.Row;
                row.style.alignItems = Align.Center;
                row.style.minWidth = 0;
                var label = new Label();
                label.style.flexGrow = 1;
                label.style.whiteSpace = WhiteSpace.Normal;
                label.style.unityTextOverflowPosition = TextOverflowPosition.End;
                row.Add(label);
                row.Add(CreateMiniButton("+", () =>
                {
                    if (row.userData is CharacterTestRuntimeAiPlannerBehaviorModuleDefinition definition)
                        AddBehaviorDefinitionToGroup(profile, definition);
                }));
                return row;
            };
            listView.bindItem = (element, index) =>
            {
                CharacterTestRuntimeAiPlannerBehaviorModuleDefinition definition = filtered[index];
                element.userData = definition;
                Label label = element.Q<Label>();
                if (label != null)
                {
                    CharacterTestRuntimeAiPlannerBehaviorCardView card =
                        CharacterTestRuntimeAiPlannerBehaviorCardPresentation.CreateTemplateCard(definition);
                    label.text = card.TemplateLabel + "\n" + card.OutputText + " | " + card.RiskText;
                    label.tooltip = card.TemplateDetail + "\nRaw: " + definition.BehaviorId;
                }
            };
            panel.Add(listView);
            return panel;
        }

        private VisualElement CreateBehaviorGroupEntriesPanel(
            CharacterTestRuntimeAiPlannerBehaviorCompositionProfile profile,
            CharacterTestRuntimeAiPlannerBehaviorCompileResult compile,
            CharacterTestRuntimeAiPlannerBehaviorActionChainPreview actionChainPreview)
        {
            var panel = new VisualElement();
            panel.style.flexGrow = 1;
            panel.style.minWidth = 0;
            panel.style.minHeight = 0;
            panel.Add(CreateSectionTitle("当前卡组"));
            panel.Add(CreateReadOnlyLine(
                "Entries",
                profile.Entries.Count.ToString(CultureInfo.InvariantCulture)));

            if (_selectedBehaviorGroupEntryIndex >= profile.Entries.Count)
                _selectedBehaviorGroupEntryIndex = profile.Entries.Count - 1;
            if (_selectedBehaviorGroupEntryIndex < 0 && profile.Entries.Count > 0)
                _selectedBehaviorGroupEntryIndex = 0;

            var listView = new ListView();
            listView.itemsSource = profile.Entries;
            listView.fixedItemHeight = 60;
            listView.virtualizationMethod = CollectionVirtualizationMethod.FixedHeight;
            listView.selectionType = SelectionType.Single;
            listView.selectedIndex = _selectedBehaviorGroupEntryIndex;
            listView.style.flexGrow = 1;
            listView.style.minHeight = 0;
            listView.makeItem = () =>
            {
                var row = new VisualElement();
                row.style.flexDirection = FlexDirection.Row;
                row.style.alignItems = Align.Center;
                row.style.minWidth = 0;
                var disabled = new Toggle();
                disabled.tooltip = "临时禁用该组内行为实例；禁用后不进入可执行候选。";
                disabled.RegisterValueChangedCallback(evt =>
                {
                    if (disabled.userData is CharacterTestRuntimeAiPlannerBehaviorModuleEntry entry)
                    {
                        entry.Disabled = evt.newValue;
                        RefreshView();
                    }
                });
                row.Add(disabled);
                var label = new Label();
                label.style.flexGrow = 1;
                label.style.whiteSpace = WhiteSpace.Normal;
                label.style.unityTextOverflowPosition = TextOverflowPosition.End;
                row.Add(label);
                return row;
            };
            listView.bindItem = (element, index) =>
            {
                CharacterTestRuntimeAiPlannerBehaviorModuleEntry entry = profile.Entries[index];
                CharacterTestRuntimeAiPlannerBehaviorCompositionCatalog.TryGetDefinition(
                    entry.BehaviorId,
                    out CharacterTestRuntimeAiPlannerBehaviorModuleDefinition definition);
                Toggle disabled = element.Q<Toggle>();
                if (disabled != null)
                {
                    disabled.userData = entry;
                    disabled.SetValueWithoutNotify(entry.Disabled);
                }
                Label label = element.Q<Label>();
                if (label != null)
                {
                    CharacterTestRuntimeAiPlannerBehaviorModuleSummary summary =
                        FindBehaviorModuleSummary(compile, entry.EntryId, entry.BehaviorId);
                    CharacterTestRuntimeAiPlannerBehaviorCardView card =
                        CharacterTestRuntimeAiPlannerBehaviorCardPresentation.CreateInstanceCard(
                            entry,
                            definition,
                            summary,
                            string.Empty);
                    label.text = entry.Order.ToString(CultureInfo.InvariantCulture)
                        + "  "
                        + card.InstanceLabel
                        + "\n"
                        + card.OutputText
                        + " | "
                        + card.TriggerText;
                    label.tooltip = card.InstanceDetail
                        + "\nRaw: "
                        + entry.EntryId
                        + " / "
                        + entry.BehaviorId;
                }
            };
            listView.selectionChanged += _ =>
            {
                _selectedBehaviorGroupEntryIndex = listView.selectedIndex;
                RefreshView();
            };
            panel.Add(listView);

            if (_selectedBehaviorGroupEntryIndex >= 0
                && _selectedBehaviorGroupEntryIndex < profile.Entries.Count)
            {
                CharacterTestRuntimeAiPlannerBehaviorModuleEntry selected =
                    profile.Entries[_selectedBehaviorGroupEntryIndex];
                CharacterTestRuntimeAiPlannerBehaviorModuleSummary summary =
                    FindBehaviorModuleSummary(compile, selected.EntryId, selected.BehaviorId);
                if (!string.IsNullOrWhiteSpace(summary.DiagnosticsText))
                    panel.Add(CreateNotice(summary.DiagnosticsText));
                panel.Add(CreateReadOnlyLine(
                    "动作链",
                    ResolveBehaviorModuleActionChainText(selected, actionChainPreview)));
            }

            return panel;
        }

        private VisualElement CreateBehaviorParameterField(
            CharacterTestRuntimeAiPlannerBehaviorModuleEntry module,
            CharacterTestRuntimeAiPlannerBehaviorModuleDefinition definition,
            CharacterTestRuntimeAiPlannerBehaviorParameterDefinition parameter)
        {
            switch (parameter.Kind)
            {
                case CharacterTestRuntimeAiPlannerBehaviorParameterKind.Bool:
                {
                    var field = new Toggle(parameter.DisplayName)
                    {
                        value = module.GetBool(definition, parameter.Key)
                    };
                    field.RegisterValueChangedCallback(evt =>
                    {
                        module.SetBool(parameter.Key, evt.newValue);
                        RefreshView();
                    });
                    return field;
                }
                case CharacterTestRuntimeAiPlannerBehaviorParameterKind.Int:
                {
                    var field = new IntegerField(parameter.DisplayName)
                    {
                        value = module.GetInt(definition, parameter.Key),
                        isDelayed = true
                    };
                    field.RegisterValueChangedCallback(evt =>
                    {
                        module.SetInt(parameter.Key, evt.newValue);
                        RefreshView();
                    });
                    return field;
                }
                default:
                {
                    var field = new FloatField(parameter.DisplayName)
                    {
                        value = module.GetFloat(definition, parameter.Key),
                        isDelayed = true
                    };
                    field.RegisterValueChangedCallback(evt =>
                    {
                        module.SetFloat(parameter.Key, evt.newValue);
                        RefreshView();
                    });
                    return field;
                }
            }
        }

    }
}
