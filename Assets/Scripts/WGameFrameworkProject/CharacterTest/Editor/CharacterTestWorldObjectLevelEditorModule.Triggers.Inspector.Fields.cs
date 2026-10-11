using System;
using System.Collections.Generic;
using MxFramework.Editor;
using MxFramework.Gameplay;
using MxFramework.Gameplay.Triggers;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace MxFramework.Game.CharacterTest.Editor
{
    public sealed partial class CharacterTestWorldObjectLevelEditorModule
    {
        private const string TriggerStrategyShared =
            "全区域共用";
        private const string TriggerStrategyPerSubject =
            "每个对象独立";
        private const string TriggerStrategyPerPresence =
            "每次进入一次";
        private const string TriggerCooldownNone =
            "无冷却";
        private const string TriggerCooldownCustom =
            "自定义";
        private const string TriggerActivationUnlimited =
            "不限";
        private const string TriggerActivationOnce =
            "1 次";
        private const string TriggerActivationCustom =
            "自定义";
        private const int DefaultCustomCooldownFrames =
            CharacterTestDeterministicUnits.DefaultTickRate;
        private const int DefaultCustomMaxActivations = 2;

        private VisualElement CreateTriggerConditionGroup(
            CharacterTestLevelGameplayTriggerEntry entry,
            GameplayTriggerStateScope stateScope,
            GameplayTriggerPresenceLimit presenceLimit,
            long cooldownFrames,
            int maxActivations)
        {
            VisualElement group = CreateTriggerEditorGroup("触发条件");
            group.Add(CreateTriggerTimingField(entry.TemplateId));
            group.Add(CreateTriggerStrategyEditor(
                entry,
                stateScope,
                presenceLimit,
                cooldownFrames,
                maxActivations));
            return group;
        }

        private VisualElement CreateTriggerStrategyEditor(
            CharacterTestLevelGameplayTriggerEntry entry,
            GameplayTriggerStateScope stateScope,
            GameplayTriggerPresenceLimit presenceLimit,
            long cooldownFrames,
            int maxActivations)
        {
            var strategies = new List<string>
            {
                TriggerStrategyShared,
                TriggerStrategyPerSubject,
                TriggerStrategyPerPresence
            };
            int strategyIndex = ResolveTriggerStrategyIndex(
                stateScope,
                presenceLimit);
            var strategy = new PopupField<string>(
                strategies,
                strategyIndex)
            {
                name = "character-test-trigger-strategy",
                tooltip =
                    "决定执行次数和冷却由整个区域共用、按对象独立，"
                    + "或让每个对象在每次进入期间最多执行一次。"
            };
            strategy.AddToClassList(
                "mx-level-editor__trigger-input");
            strategy.RegisterValueChangedCallback(
                change =>
                {
                    if (change.newValue == change.previousValue)
                        return;
                    ResolveTriggerStrategy(
                        change.newValue,
                        out GameplayTriggerStateScope newScope,
                        out GameplayTriggerPresenceLimit newPresence);
                    if (UpdateTriggerPolicy(
                            entry.StableId,
                            newScope,
                            newPresence,
                            cooldownFrames,
                            maxActivations))
                    {
                        RefreshTriggerInspectorAfterMutation();
                    }
                });
            return CreateTriggerFieldRow(
                "计数与冷却范围",
                strategy);
        }

        private VisualElement CreateTriggerActionGroup(
            CharacterTestLevelGameplayTriggerEntry entry,
            long payload0,
            long payload1,
            long payload2)
        {
            VisualElement group = CreateTriggerEditorGroup("动作");
            group.name = "character-test-trigger-action-section";

            if (IsStoryTriggerTemplate(entry.TemplateId))
            {
                VisualElement storyReference =
                    CreateTriggerStoryReferenceField(
                    entry,
                    payload0,
                    payload1,
                    payload2);
                Label storySummary =
                    DetachTriggerReferenceSummary(storyReference);
                PrepareTriggerClauseReference(
                    storyReference,
                    "mx-level-editor__trigger-clause-control--wide");
                bool threshold =
                    CharacterTestLevelGameplayTriggerAuthoringDocument
                        .IsPlayerHpThresholdTemplate(entry.TemplateId);
                group.Add(CreateTriggerClauseRow(
                    CreateTriggerClauseScope(
                        threshold ? "生命值降至阈值" : "进入"),
                    CreateTriggerClauseWord("为"),
                    CreateTriggerClauseValue(
                        threshold ? "当前触发器" : "当前区域"),
                    CreateTriggerClauseWord("触发剧情"),
                    storyReference));
                AddTriggerClauseSummary(group, storySummary);
                return group;
            }

            VisualElement buff =
                CreateTriggerBuffPicker(
                payload0,
                value => UpdateTriggerPayloads(
                    entry.StableId,
                    value,
                    payload1,
                    payload2));
            Label buffSummary = DetachTriggerReferenceSummary(buff);
            PrepareTriggerClauseReference(
                buff,
                "mx-level-editor__trigger-clause-control--wide");

            VisualElement attribution =
                CreateTriggerBuffAttributionEditor(
                entry,
                payload0,
                payload1,
                payload2);
            DetachTriggerReferenceSummary(attribution);
            PrepareTriggerClauseReference(
                attribution,
                "mx-level-editor__trigger-clause-control--medium");

            bool presenceTemplate = string.Equals(
                entry.TemplateId,
                CharacterTestLevelGameplayTriggerAuthoringDocument
                    .GameplayApplyRemoveBuffPresenceTemplateId,
                StringComparison.Ordinal);
            group.Add(CreateTriggerClauseRow(
                CreateTriggerClauseScope(
                    presenceTemplate ? "进入 / 停留" : "进入"),
                CreateTriggerClauseWord("为"),
                CreateTriggerClauseValue("触发角色"),
                CreateTriggerClauseWord("施加"),
                buff,
                CreateTriggerClauseWord("，来源"),
                attribution));
            AddTriggerClauseSummary(group, buffSummary);

            if (presenceTemplate)
            {
                group.Add(CreateTriggerClauseRow(
                    CreateTriggerClauseScope("离开"),
                    CreateTriggerClauseWord("从"),
                    CreateTriggerClauseValue("触发角色"),
                    CreateTriggerClauseWord("移除"),
                    CreateTriggerClauseValue(
                        ResolveTriggerBuffLabel(payload0))));
            }
            return group;
        }

        private Foldout CreateGameplayTriggerAdvancedOptions(
            CharacterTestLevelGameplayTriggerEntry entry,
            long cooldownFrames,
            int maxActivations,
            long payload0,
            long payload1,
            long payload2)
        {
            var foldout = new Foldout
            {
                name = "character-test-trigger-options",
                text = "高级规则",
                value = _expandedTriggerRuleEntryIds.Contains(
                    entry.StableId)
            };
            foldout.AddToClassList(
                "mx-level-editor__trigger-foldout");
            foldout.RegisterValueChangedCallback(
                change =>
                {
                    if (change.newValue)
                        _expandedTriggerRuleEntryIds.Add(entry.StableId);
                    else
                        _expandedTriggerRuleEntryIds.Remove(entry.StableId);
                });
            foldout.Add(CreateTriggerCooldownEditor(
                entry,
                cooldownFrames));
            foldout.Add(CreateTriggerActivationLimitEditor(
                entry,
                maxActivations));
            foldout.Add(CreateTriggerSubjectCapabilityEditor(entry));
            string conditionSummary =
                ResolveTriggerWorkspaceConditionSummary(entry);
            foldout.Add(CreateTriggerReadOnlyField(
                "附加条件",
                string.IsNullOrWhiteSpace(conditionSummary)
                    ? "无（当前模板）"
                    : conditionSummary,
                string.IsNullOrWhiteSpace(conditionSummary)
                    ? "当前模板使用项目 Gameplay Trigger 的恒真条件。"
                    : "该条件由现有 Gameplay Trigger Authoring 提供，"
                        + "当前模板界面只读保留，不会自动改写。"));

            if (!string.Equals(
                    entry.TemplateId,
                    CharacterTestLevelGameplayTriggerAuthoringDocument
                        .StoryRaiseTriggerEnterOnceTemplateId,
                    StringComparison.Ordinal))
            {
                foldout.Add(CreateTriggerIntegerField(
                    "Buff ID（手动）",
                    "character-test-trigger-buff-id",
                    payload0,
                    value => UpdateTriggerPayloads(
                        entry.StableId,
                        value,
                        payload1,
                        payload2),
                    minimumValue: 0,
                    tooltip:
                        "仅用于迁移目录外的旧数据；普通关卡请使用上方 Buff 选择器。"));
                foldout.Add(CreateTriggerIntegerField(
                    "来源机制 ID（手动）",
                    "character-test-trigger-buff-source-id",
                    payload1,
                    value => UpdateTriggerPayloads(
                        entry.StableId,
                        payload0,
                        value,
                        payload2),
                    minimumValue: 0,
                    tooltip:
                        "仅用于迁移未登记的历史来源机制；普通关卡请使用上方正式目录选择器。"
                        + "该值写入 GameplayBuffApplicationRequest.SourceId，"
                        + "不是 Runtime 命令来源。"));
            }
            else
            {
                foldout.Add(CreateTriggerIntegerField(
                    "Story 参数 1",
                    "character-test-trigger-story-parameter-1",
                    payload1,
                    value => UpdateTriggerPayloads(
                        entry.StableId,
                        payload0,
                        value,
                        payload2),
                    tooltip:
                        "当前 Story Action 的兼容参数；普通规则通常保持 0。"));
                foldout.Add(CreateTriggerIntegerField(
                    "Story 参数 2",
                    "character-test-trigger-story-parameter-2",
                    payload2,
                    value => UpdateTriggerPayloads(
                        entry.StableId,
                        payload0,
                        payload1,
                        value),
                    tooltip:
                        "当前 Story Action 的兼容参数；普通规则通常保持 0。"));
            }

            return foldout;
        }

        private VisualElement CreateTriggerSubjectCapabilityEditor(
            CharacterTestLevelGameplayTriggerEntry entry)
        {
            if (!_triggerDocument.TryGetSubjectFilter(
                    entry,
                    out CharacterTestLevelGameplayTriggerSubjectFilter filter))
            {
                return CreateTriggerReadOnlyField(
                    "对象能力",
                    "无法解析",
                    "当前 Trigger 没有可用的 Subject Filter。");
            }

            bool requiresMovable =
                (filter.RequiredCapabilities
                    & GameplayTargetCapabilities.Movable) != 0;
            var toggle = new Toggle
            {
                name =
                    "character-test-trigger-subject-require-movable",
                value = requiresMovable,
                tooltip =
                    "开启后，只有声明 Gameplay 可移动能力的角色才能触发。"
                    + "模板锁定的可伤害要求不会被取消。"
            };
            toggle.RegisterValueChangedCallback(
                change =>
                {
                    GameplayTargetCapabilities next =
                        filter.RequiredCapabilities;
                    if (change.newValue)
                        next |= GameplayTargetCapabilities.Movable;
                    else
                        next &= ~GameplayTargetCapabilities.Movable;
                    if (UpdateTriggerSubjectCapabilities(
                            entry.StableId,
                            next))
                    {
                        RefreshTriggerInspectorAfterMutation();
                    }
                    else
                    {
                        toggle.SetValueWithoutNotify(
                            change.previousValue);
                    }
                });
            return CreateTriggerFieldRow(
                "必须可移动",
                toggle,
                toggle.tooltip);
        }

        private VisualElement CreateTriggerCooldownEditor(
            CharacterTestLevelGameplayTriggerEntry entry,
            long cooldownFrames)
        {
            var root = new VisualElement();
            var choices = new List<string>
            {
                TriggerCooldownNone,
                TriggerCooldownCustom
            };
            int selectedIndex = cooldownFrames > 0L ? 1 : 0;
            var mode = new PopupField<string>(
                choices,
                selectedIndex)
            {
                name = "character-test-trigger-cooldown-mode",
                tooltip =
                    "普通规则保持无冷却；只有确实需要抑制重复触发时才设置自定义逻辑帧数。"
            };
            mode.AddToClassList(
                "mx-level-editor__trigger-input");
            root.Add(CreateTriggerFieldRow("冷却", mode));

            VisualElement custom = CreateTriggerIntegerField(
                "冷却帧数",
                "character-test-trigger-cooldown-frames",
                Math.Max(1L, cooldownFrames),
                value => UpdateTriggerCooldown(
                    entry.StableId,
                    value),
                minimumValue: 1,
                tooltip:
                    "使用 Runtime 逻辑帧。CharacterTest 默认 60 帧约为 1 秒。");
            custom.name =
                "character-test-trigger-cooldown-custom-row";
            custom.style.display = selectedIndex == 1
                ? DisplayStyle.Flex
                : DisplayStyle.None;
            root.Add(custom);

            mode.RegisterValueChangedCallback(
                change =>
                {
                    int index = choices.IndexOf(change.newValue);
                    if (index < 0)
                        return;

                    long nextValue = index == 0
                        ? 0L
                        : cooldownFrames > 0L
                            ? cooldownFrames
                            : DefaultCustomCooldownFrames;
                    if (nextValue == cooldownFrames)
                    {
                        custom.style.display = index == 1
                            ? DisplayStyle.Flex
                            : DisplayStyle.None;
                        return;
                    }
                    if (UpdateTriggerCooldown(
                            entry.StableId,
                            nextValue))
                    {
                        RefreshTriggerInspectorAfterMutation();
                    }
                });
            return root;
        }

        private VisualElement CreateTriggerActivationLimitEditor(
            CharacterTestLevelGameplayTriggerEntry entry,
            int maxActivations)
        {
            var root = new VisualElement();
            var choices = new List<string>
            {
                TriggerActivationUnlimited,
                TriggerActivationOnce,
                TriggerActivationCustom
            };
            int selectedIndex = maxActivations <= 0
                ? 0
                : maxActivations == 1
                    ? 1
                    : 2;
            var mode = new PopupField<string>(
                choices,
                selectedIndex)
            {
                name =
                    "character-test-trigger-activation-limit-mode",
                tooltip =
                    "次数按上方计数与冷却范围累计；不限、1 次和自定义需明确选择。"
            };
            mode.AddToClassList(
                "mx-level-editor__trigger-input");
            root.Add(CreateTriggerFieldRow("执行次数", mode));

            VisualElement custom = CreateTriggerIntegerField(
                "次数上限",
                "character-test-trigger-max-activations",
                Math.Max(1, maxActivations),
                value => UpdateTriggerActivationLimit(
                    entry.StableId,
                    (int)value),
                minimumValue: 1,
                tooltip:
                    "这是当前计数与冷却范围内的累计次数，不会在每一帧重置。");
            custom.name =
                "character-test-trigger-activation-custom-row";
            custom.style.display = selectedIndex == 2
                ? DisplayStyle.Flex
                : DisplayStyle.None;
            root.Add(custom);

            mode.RegisterValueChangedCallback(
                change =>
                {
                    int index = choices.IndexOf(change.newValue);
                    if (index < 0)
                        return;

                    int nextValue = index == 0
                        ? 0
                        : index == 1
                            ? 1
                            : maxActivations > 1
                                ? maxActivations
                                : DefaultCustomMaxActivations;
                    if (nextValue == maxActivations)
                    {
                        custom.style.display = index == 2
                            ? DisplayStyle.Flex
                            : DisplayStyle.None;
                        return;
                    }
                    if (UpdateTriggerActivationLimit(
                            entry.StableId,
                            nextValue))
                    {
                        RefreshTriggerInspectorAfterMutation();
                    }
                });
            return root;
        }

        private Foldout CreateGameplayTriggerTechnicalFoldout(
            CharacterTestLevelGameplayTriggerEntry entry)
        {
            var foldout = new Foldout
            {
                name = "character-test-trigger-advanced",
                text = "技术信息（只读）",
                value = false
            };
            foldout.AddToClassList(
                "mx-level-editor__trigger-foldout");
            foldout.Add(CreateTriggerReadOnlyField(
                "模板",
                entry.TemplateId));
            foldout.Add(CreateTriggerReadOnlyField(
                "稳定 ID",
                entry.StableId));
            foldout.Add(CreateTriggerReadOnlyField(
                "源 / 条件 / 定义",
                entry.SourceDefinitionId
                + " / "
                + entry.ConditionId
                + " / "
                + entry.DefinitionId));
            foldout.Add(CreateTriggerReadOnlyField(
                "路由",
                entry.RouteIds == null
                    ? string.Empty
                    : string.Join(", ", entry.RouteIds)));
            foldout.Add(CreateTriggerReadOnlyField(
                "绑定 / 来源绑定",
                entry.BindingId
                + " / "
                + entry.SourceBindingId));

            CharacterTestLevelGameplayTriggerRegionBinding regionBinding =
                FindTriggerRegionBinding(entry.SourceBindingId);
            if (regionBinding != null)
            {
                foldout.Add(CreateTriggerReadOnlyField(
                    "Provider / Layer",
                    regionBinding.ProviderId
                    + " / "
                    + regionBinding.LayerMask));
                foldout.Add(CreateTriggerReadOnlyField(
                    "容量",
                    "query="
                    + regionBinding.MaxQueryHits
                    + "，mapped="
                    + regionBinding.MaxMappedSubjects));
            }

            foldout.Add(CreateTriggerReadOnlyField(
                "交付",
                ResolveTriggerDeliveryContract(entry.TemplateId).ToString()));
            foldout.Add(CreateTriggerReadOnlyField(
                "编译器",
                "GameplayTriggerConfigCompiler"));
            return foldout;
        }

        private VisualElement CreateTriggerTimingField(string templateId)
        {
            if (CharacterTestLevelGameplayTriggerAuthoringDocument
                    .IsPlayerHpThresholdTemplate(templateId))
            {
                return CreateTriggerReadOnlyField(
                    "触发时机",
                    "生命值降至进入阈值",
                    "恢复到退出阈值以上后可再次触发。");
            }
            var chips = new VisualElement();
            chips.AddToClassList(
                "mx-level-editor__trigger-timing");
            bool includesExit = string.Equals(
                    templateId,
                    CharacterTestLevelGameplayTriggerAuthoringDocument
                        .GameplayApplyRemoveBuffPresenceTemplateId,
                    StringComparison.Ordinal);
            chips.Add(CreateTriggerTimingChip("进入", selected: true));
            chips.Add(CreateTriggerTimingChip("停留", selected: false));
            chips.Add(CreateTriggerTimingChip("离开", includesExit));

            return CreateTriggerFieldRow(
                "触发时机",
                chips,
                "触发时机由当前模板决定。");
        }

        private static Label CreateTriggerTimingChip(
            string text,
            bool selected)
        {
            var chip = new Label(text);
            chip.AddToClassList(
                "mx-level-editor__trigger-chip");
            chip.EnableInClassList(
                "mx-level-editor__trigger-chip--selected",
                selected);
            return chip;
        }

        private static VisualElement CreateTriggerEditorGroup(string title)
        {
            var group = new VisualElement();
            group.AddToClassList(
                "mx-level-editor__trigger-group");
            var label = new Label(title);
            label.AddToClassList(
                "mx-level-editor__trigger-group-title");
            group.Add(label);
            return group;
        }

        private static VisualElement CreateTriggerFieldRow(
            string label,
            VisualElement editor,
            string tooltip = null)
        {
            var row = new VisualElement();
            row.AddToClassList(
                "mx-level-editor__trigger-field-row");
            if (!string.IsNullOrWhiteSpace(tooltip))
                row.tooltip = tooltip;

            var fieldLabel = new Label(label);
            fieldLabel.AddToClassList(
                "mx-level-editor__trigger-field-label");
            row.Add(fieldLabel);

            editor.AddToClassList(
                "mx-level-editor__trigger-field-editor");
            row.Add(editor);
            return row;
        }

        private static VisualElement CreateTriggerReadOnlyField(
            string label,
            string value,
            string tooltip = null)
        {
            var display = new Label(value ?? string.Empty);
            display.AddToClassList(
                "mx-level-editor__trigger-readonly");
            return CreateTriggerFieldRow(
                label,
                display,
                tooltip);
        }

        private static VisualElement CreateTriggerClauseRow(
            params VisualElement[] elements)
        {
            var row = new VisualElement();
            row.AddToClassList(
                "mx-level-editor__trigger-clause-row");
            for (int i = 0;
                 elements != null && i < elements.Length;
                 i++)
            {
                if (elements[i] != null)
                    row.Add(elements[i]);
            }
            return row;
        }

        private static Label CreateTriggerClauseWord(string text)
        {
            var label = new Label(text ?? string.Empty);
            label.AddToClassList(
                "mx-level-editor__trigger-clause-word");
            return label;
        }

        private static Label CreateTriggerClauseValue(
            string text,
            string tooltip = null)
        {
            var label = new Label(text ?? string.Empty)
            {
                tooltip = tooltip ?? string.Empty
            };
            label.AddToClassList(
                "mx-level-editor__trigger-clause-value");
            return label;
        }

        private static Label CreateTriggerClauseScope(string text)
        {
            var label = new Label(text ?? string.Empty);
            label.AddToClassList(
                "mx-level-editor__trigger-clause-scope");
            return label;
        }

        private static void PrepareTriggerClauseReference(
            VisualElement field,
            string widthClass)
        {
            if (field == null)
                return;

            field.AddToClassList(
                "mx-level-editor__trigger-clause-control");
            if (!string.IsNullOrWhiteSpace(widthClass))
                field.AddToClassList(widthClass);

            Label fieldLabel = field.Q<Label>(
                MxAuthoringReferenceField.LabelElementName);
            if (fieldLabel != null)
                fieldLabel.style.display = DisplayStyle.None;

            VisualElement content = field.Q<VisualElement>(
                MxAuthoringReferenceField.ContentElementName);
            if (content != null)
            {
                content.style.flexGrow = 1f;
                content.style.flexShrink = 1f;
                content.style.minWidth = 0f;
            }
        }

        private static Label DetachTriggerReferenceSummary(
            VisualElement field)
        {
            Label summary = field?.Q<Label>(
                MxAuthoringReferenceField.SummaryElementName);
            summary?.RemoveFromHierarchy();
            return summary;
        }

        private static void AddTriggerClauseSummary(
            VisualElement group,
            Label summary)
        {
            if (group == null
                || summary == null
                || string.IsNullOrWhiteSpace(summary.text))
            {
                return;
            }

            summary.style.display = DisplayStyle.Flex;
            summary.AddToClassList(
                "mx-level-editor__trigger-clause-summary");
            group.Add(summary);
        }

        private VisualElement CreateTriggerIntegerField(
            string label,
            string name,
            long value,
            Func<long, bool> apply,
            int? minimumValue = null,
            string tooltip = null)
        {
            int fieldValue = value > int.MaxValue
                ? int.MaxValue
                : value < int.MinValue
                    ? int.MinValue
                    : (int)value;
            var field = new IntegerField
            {
                name = name,
                value = fieldValue,
                isDelayed = true,
                tooltip = tooltip
            };
            field.AddToClassList(
                "mx-level-editor__trigger-input");
            field.RegisterValueChangedCallback(
                change =>
                {
                    if (change.newValue == change.previousValue)
                        return;

                    int normalized = minimumValue.HasValue
                        ? Math.Max(
                            minimumValue.Value,
                            change.newValue)
                        : change.newValue;
                    if (normalized != change.newValue)
                        field.SetValueWithoutNotify(normalized);
                    if (apply(normalized))
                        RefreshTriggerInspectorAfterMutation();
                });
            return CreateTriggerFieldRow(
                label,
                field,
                tooltip);
        }

        private VisualElement CreatePlayerHpThresholdEditor(
            CharacterTestLevelGameplayTriggerEntry entry,
            int enterThreshold,
            int exitThreshold)
        {
            var root = new VisualElement();
            var enter = new IntegerField
            {
                name = "character-test-trigger-player-hp-enter-threshold",
                value = enterThreshold,
                isDelayed = true,
                tooltip = "当前玩家生命值小于或等于该值时进入触发状态。"
            };
            enter.AddToClassList("mx-level-editor__trigger-input");
            enter.AddToClassList(
                "mx-level-editor__trigger-clause-number");
            enter.RegisterValueChangedCallback(
                change =>
                {
                    if (change.newValue == change.previousValue)
                        return;
                    int normalized = Math.Max(
                        CharacterTestLevelGameplayTriggerAuthoringDocument
                            .MinimumPlayerHpThreshold,
                        Math.Min(
                            CharacterTestLevelGameplayTriggerAuthoringDocument
                                .MaximumPlayerHpThreshold,
                            change.newValue));
                    int normalizedExit = Math.Max(
                        exitThreshold,
                        normalized);
                    if (UpdatePlayerHpThresholds(
                            entry.StableId,
                            normalized,
                            normalizedExit))
                    {
                        RefreshTriggerInspectorAfterMutation();
                    }
                });
            root.Add(CreateTriggerClauseRow(
                CreateTriggerClauseWord("当前玩家生命值"),
                CreateTriggerClauseWord("≤"),
                enter,
                CreateTriggerClauseWord("时触发")));

            var exit = new IntegerField
            {
                name = "character-test-trigger-player-hp-exit-threshold",
                value = exitThreshold,
                isDelayed = true,
                tooltip = "生命值高于该值后离开触发状态，避免临界值反复触发。"
            };
            exit.AddToClassList("mx-level-editor__trigger-input");
            exit.AddToClassList(
                "mx-level-editor__trigger-clause-number");
            exit.RegisterValueChangedCallback(
                change =>
                {
                    if (change.newValue == change.previousValue)
                        return;
                    int normalized = Math.Max(
                        enterThreshold,
                        Math.Min(
                            CharacterTestLevelGameplayTriggerAuthoringDocument
                                .MaximumPlayerHpThreshold,
                            change.newValue));
                    if (UpdatePlayerHpThresholds(
                            entry.StableId,
                            enterThreshold,
                            normalized))
                    {
                        RefreshTriggerInspectorAfterMutation();
                    }
                });
            root.Add(CreateTriggerClauseRow(
                CreateTriggerClauseWord("生命值恢复到"),
                CreateTriggerClauseWord(">"),
                exit,
                CreateTriggerClauseWord("后可再次触发")));
            return root;
        }

        private static int ResolveTriggerStrategyIndex(
            GameplayTriggerStateScope stateScope,
            GameplayTriggerPresenceLimit presenceLimit)
        {
            if (stateScope == GameplayTriggerStateScope.PerInstance)
                return 0;
            return presenceLimit
                == GameplayTriggerPresenceLimit.OncePerPresence
                    ? 2
                    : 1;
        }

        private static void ResolveTriggerStrategy(
            string value,
            out GameplayTriggerStateScope stateScope,
            out GameplayTriggerPresenceLimit presenceLimit)
        {
            if (string.Equals(
                    value,
                    TriggerStrategyShared,
                    StringComparison.Ordinal))
            {
                stateScope = GameplayTriggerStateScope.PerInstance;
                presenceLimit = GameplayTriggerPresenceLimit.Ignore;
                return;
            }

            stateScope = GameplayTriggerStateScope.PerSubject;
            presenceLimit = string.Equals(
                    value,
                    TriggerStrategyPerPresence,
                    StringComparison.Ordinal)
                ? GameplayTriggerPresenceLimit.OncePerPresence
                : GameplayTriggerPresenceLimit.Ignore;
        }

        private static string ResolveTriggerBuffLabel(long buffId)
        {
            if (buffId <= 0L)
                return "未选择 Buff";

            if (TryBuildTriggerBuffChoices(
                    out List<MxAuthoringReferenceOption<long>> choices,
                    out _))
            {
                int index = FindTriggerBuffChoice(choices, buffId);
                if (index >= 0)
                    return choices[index].DisplayName;
            }

            return "未登记 Buff " + buffId;
        }
    }
}
