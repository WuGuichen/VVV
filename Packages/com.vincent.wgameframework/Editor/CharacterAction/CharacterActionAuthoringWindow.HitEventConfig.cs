using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using MxFramework.CharacterAction;
using MxFramework.Combat.Hit;
using Newtonsoft.Json.Linq;
using UnityEngine.UIElements;

namespace MxFramework.Editor.CharacterAction
{
    public sealed partial class CharacterActionAuthoringWindow
    {
        private void AddSharedCombatHitEventFields(
            VisualElement parent,
            CombatTrackEventJsonDto evt,
            bool timeline,
            bool requestFullTracksRefresh)
        {
            if (evt == null)
                return;

            evt.HitEvent = evt.HitEvent ?? CreateDefaultHitEventDto(evt);
            AddSharedHitEventConfigFields(
                parent,
                "命中配置",
                CharacterActionHitEventAuthoringModel.FromCombatTrackEvent(evt),
                mutate =>
                {
                    Action change = () =>
                    {
                        CharacterActionHitEventAuthoringModel model =
                            CharacterActionHitEventAuthoringModel.FromCombatTrackEvent(evt);
                        mutate(model);
                        model.ApplyToCombatTrackEvent(evt);
                    };
                    if (timeline)
                        ApplyTimelineFieldChange(change, requestFullTracksRefresh: true);
                    else
                        ApplyStructuralChange(change, UiRefreshScope.Tracks);
                },
                editable: true);
        }

        private void AddSharedGameplayHitEventFields(
            VisualElement parent,
            GameplayTrackEventJsonDto evt,
            bool timeline,
            bool requestFullTracksRefresh)
        {
            if (evt == null)
                return;

            evt.HitEvent = evt.HitEvent ?? CreateDefaultHitEventDto(evt);
            AddSharedHitEventConfigFields(
                parent,
                "命中配置",
                CharacterActionHitEventAuthoringModel.FromGameplayTrackEvent(evt),
                mutate =>
                {
                    Action change = () =>
                    {
                        CharacterActionHitEventAuthoringModel model =
                            CharacterActionHitEventAuthoringModel.FromGameplayTrackEvent(evt);
                        mutate(model);
                        model.ApplyToGameplayTrackEvent(evt);
                    };
                    if (timeline)
                        ApplyTimelineFieldChange(change, requestFullTracksRefresh: true);
                    else
                        ApplyStructuralChange(change, UiRefreshScope.Tracks);
                },
                editable: true);
        }

        private void AddSharedHitEventConfigFields(
            VisualElement parent,
            string title,
            CharacterActionHitEventAuthoringModel model,
            Action<Action<CharacterActionHitEventAuthoringModel>> commit,
            bool editable)
        {
            model = model?.Clone() ?? new CharacterActionHitEventAuthoringModel();
            VisualElement hitCard = AddCard(parent, "命中对象");
            hitCard.name = "character-action-hit-targets";
            hitCard.AddToClassList("ca-hit-card");
            AddHitTargetPresetRow(hitCard, model, commit, editable);
            AddHitTargetChipGroup(
                hitCard,
                "角色",
                model,
                commit,
                editable,
                ("敌方", CombatHitTargetTypes.HostileCharacter),
                ("友方", CombatHitTargetTypes.FriendlyCharacter),
                ("自身", CombatHitTargetTypes.Self),
                ("第三方", CombatHitTargetTypes.ThirdPartyCharacter));
            AddHitTargetChipGroup(
                hitCard,
                "环境",
                model,
                commit,
                editable,
                ("可破坏物", CombatHitTargetTypes.DamageableObject),
                ("墙面", CombatHitTargetTypes.WorldWall),
                ("地面", CombatHitTargetTypes.WorldGround),
                ("静态场景", CombatHitTargetTypes.WorldStatic));

            if (model.HasWorldTargets)
            {
                AddSharedHitEventEnumPopup<CombatEnvironmentHitPolicy>(
                    hitCard,
                    "环境命中行为",
                    model.EnvironmentHitPolicy,
                    value => commit(next => next.EnvironmentHitPolicy = value),
                    editable);
            }

            VisualElement hitLimitRow = new VisualElement();
            hitLimitRow.AddToClassList("ca-hit-two-column-row");
            AddSharedHitEventIntegerField(
                hitLimitRow,
                "最大命中数",
                model.MaxAcceptedHits <= 0 ? 1 : model.MaxAcceptedHits,
                value => commit(next => next.MaxAcceptedHits = Math.Max(1, value)),
                editable);
            AddSharedHitEventEnumPopup<CombatHitSameTargetPolicy>(
                hitLimitRow,
                "重复命中",
                model.HitSameTargetPolicy,
                value => commit(next => next.HitSameTargetPolicy = value),
                editable);
            hitCard.Add(hitLimitRow);

            if (model.HasWorldTargets
                && !string.Equals(model.EnvironmentHitPolicy, CombatEnvironmentHitPolicy.BlockedPresentation.ToString(), StringComparison.Ordinal))
            {
                hitCard.Add(new HelpBox(
                    "已选择墙面/地面/静态场景，但环境命中策略不是“阻挡表现”。运行时会查询到场景体，但不会产生环境命中表现。",
                    HelpBoxMessageType.Info));
            }

            VisualElement damageCard = AddCard(parent, "伤害结果");
            damageCard.name = "character-action-damage-result";
            damageCard.AddToClassList("ca-hit-card");
            string damageTypeId = string.IsNullOrWhiteSpace(model.DamageTypeId)
                ? CharacterActionDamageAuthoringText.DefaultDamageTypeId
                : model.DamageTypeId;
            string analysisCategory = string.IsNullOrWhiteSpace(model.AnalysisCategory) ? "BasicAttack" : model.AnalysisCategory;
            string[] tags = model.Tags ?? Array.Empty<string>();

            AddDamageResultPreview(damageCard, model);
            AddSharedChoiceField(damageCard, "伤害类型", damageTypeId, BuildDamageTypeChoices(damageTypeId), value =>
                commit(next => next.DamageTypeId = value), editable);
            AddSharedHitEventIntegerField(damageCard, "基础伤害", model.BaseDamage, value =>
                commit(next => next.BaseDamage = Math.Max(0, value)), editable);
            AddSharedHitEventFloatField(damageCard, "攻击系数", model.GetAttackScale(), value =>
                commit(next => next.SetAttackScale(Math.Max(0f, value))), editable);
            AddSharedHitEventIntegerField(damageCard, "冲击强度", model.ImpactPower, value =>
                commit(next => next.ImpactPower = Math.Max(0, value)), editable);

            Label runtimeFormulaNote = new Label("ⓘ  最终公式由 CharacterTest 运行时统一解析");
            runtimeFormulaNote.AddToClassList("ca-hit-runtime-note");
            damageCard.Add(runtimeFormulaNote);

            var formulaFoldout = new Foldout { text = "公式说明", value = false };
            formulaFoldout.name = "character-action-damage-formula";
            formulaFoldout.AddToClassList("ca-hit-foldout");
            Label semantics = new Label(CharacterActionDamageAuthoringText.DamageTuningSemanticsHelpText);
            semantics.AddToClassList("ca-hit-help-text");
            formulaFoldout.Add(semantics);
            Label baseline = new Label(CharacterActionDamageAuthoringText.DamageTuningBaselineHelpText);
            baseline.AddToClassList("ca-hit-help-text");
            formulaFoldout.Add(baseline);
            parent.Add(formulaFoldout);

            var advanced = new Foldout { text = "高级", value = false };
            advanced.name = "character-action-hit-advanced";
            advanced.AddToClassList("ca-hit-foldout");
            AddSharedHitEventIntegerField(advanced, "击飞效果值", model.LaunchPower, value =>
                commit(next => next.LaunchPower = Math.Max(0, value)), editable);
            AddSharedChoiceField(advanced, "分析分类", analysisCategory, BuildAnalysisCategoryChoices(analysisCategory), value =>
                commit(next => next.AnalysisCategory = value), editable);
            AddSharedDamageTagToggles(advanced, tags, value =>
                commit(next => next.Tags = value ?? Array.Empty<string>()), editable);
            AddSharedOnHitBuffFields(advanced, model, commit, editable);
            parent.Add(advanced);
        }

        private static void AddHitTargetPresetRow(
            VisualElement parent,
            CharacterActionHitEventAuthoringModel model,
            Action<Action<CharacterActionHitEventAuthoringModel>> commit,
            bool editable)
        {
            VisualElement row = new VisualElement();
            row.AddToClassList("ca-hit-preset-row");
            Label label = new Label("预设");
            label.AddToClassList("ca-hit-group-label");
            row.Add(label);

            CombatHitTargetTypes allCharacters = CombatHitTargetTypes.HostileCharacter
                | CombatHitTargetTypes.FriendlyCharacter
                | CombatHitTargetTypes.Self
                | CombatHitTargetTypes.ThirdPartyCharacter;
            CombatHitTargetTypes environment = CombatHitTargetTypes.DamageableObject
                | CombatHitTargetTypes.WorldWall
                | CombatHitTargetTypes.WorldGround
                | CombatHitTargetTypes.WorldStatic;
            row.Add(CreateHitTargetPresetButton("仅敌人", CombatHitTargetTypes.HostileCharacter, model, commit, editable));
            row.Add(CreateHitTargetPresetButton("所有角色", allCharacters, model, commit, editable));
            row.Add(CreateHitTargetPresetButton("环境交互", environment, model, commit, editable));
            parent.Add(row);
        }

        private static Button CreateHitTargetPresetButton(
            string label,
            CombatHitTargetTypes targetTypes,
            CharacterActionHitEventAuthoringModel model,
            Action<Action<CharacterActionHitEventAuthoringModel>> commit,
            bool editable)
        {
            var button = new Button(() => commit(next => next.TargetTypes = targetTypes))
            {
                text = label,
                name = "hit-preset-" + targetTypes,
            };
            button.AddToClassList("ca-hit-chip");
            button.EnableInClassList("ca-hit-chip--selected", model.TargetTypes == targetTypes);
            button.SetEnabled(editable);
            return button;
        }

        private static void AddHitTargetChipGroup(
            VisualElement parent,
            string label,
            CharacterActionHitEventAuthoringModel model,
            Action<Action<CharacterActionHitEventAuthoringModel>> commit,
            bool editable,
            params (string Label, CombatHitTargetTypes TargetType)[] choices)
        {
            var labelElement = new Label(label);
            labelElement.AddToClassList("ca-hit-group-label");
            parent.Add(labelElement);

            VisualElement row = new VisualElement();
            row.AddToClassList("ca-hit-chip-row");
            for (int i = 0; i < choices.Length; i++)
            {
                (string choiceLabel, CombatHitTargetTypes targetType) = choices[i];
                bool selected = (model.TargetTypes & targetType) != 0;
                var button = new Button(() => commit(next =>
                {
                    CombatHitTargetTypes value = next.TargetTypes == CombatHitTargetTypes.None
                        ? CombatHitEventConfig.DefaultTargetTypes
                        : next.TargetTypes;
                    value = (value & targetType) != 0 ? value & ~targetType : value | targetType;
                    next.TargetTypes = value == CombatHitTargetTypes.None
                        ? CombatHitEventConfig.DefaultTargetTypes
                        : value;
                }))
                {
                    text = choiceLabel,
                    name = "hit-target-" + targetType,
                };
                button.AddToClassList("ca-hit-chip");
                button.EnableInClassList("ca-hit-chip--selected", selected);
                button.SetEnabled(editable);
                row.Add(button);
            }
            parent.Add(row);
        }

        private static void AddDamageResultPreview(
            VisualElement parent,
            CharacterActionHitEventAuthoringModel model)
        {
            int estimatedDamage = Math.Max(
                0,
                model.BaseDamage + (int)Math.Round(
                    CharacterActionDamageAuthoringText.BaselineDefaultAttack * model.GetAttackScale(),
                    MidpointRounding.AwayFromZero));
            VisualElement preview = new VisualElement { name = "character-action-damage-preview" };
            preview.AddToClassList("ca-hit-damage-preview");

            VisualElement metrics = new VisualElement();
            metrics.AddToClassList("ca-hit-damage-metrics");
            VisualElement damageMetric = new VisualElement();
            damageMetric.Add(new Label("预计伤害"));
            var damageValue = new Label(estimatedDamage.ToString(CultureInfo.InvariantCulture) + " HP");
            damageValue.name = "character-action-estimated-damage";
            damageValue.AddToClassList("ca-hit-damage-value");
            damageMetric.Add(damageValue);
            metrics.Add(damageMetric);

            VisualElement impactMetric = new VisualElement();
            impactMetric.AddToClassList("ca-hit-impact-metric");
            impactMetric.Add(new Label("冲击"));
            var impactValue = new Label(Math.Max(0, model.ImpactPower).ToString(CultureInfo.InvariantCulture));
            impactValue.AddToClassList("ca-hit-impact-value");
            impactMetric.Add(impactValue);
            metrics.Add(impactMetric);
            preview.Add(metrics);

            string formula = "基础 " + Math.Max(0, model.BaseDamage).ToString(CultureInfo.InvariantCulture)
                + "  +  攻击 " + CharacterActionDamageAuthoringText.BaselineDefaultAttack.ToString(CultureInfo.InvariantCulture)
                + " × " + model.GetAttackScale().ToString("0.###", CultureInfo.InvariantCulture);
            Label formulaLabel = new Label(formula);
            formulaLabel.AddToClassList("ca-hit-damage-formula-strip");
            preview.Add(formulaLabel);
            parent.Add(preview);
        }

        private void AddSharedOnHitBuffFields(
            VisualElement parent,
            CharacterActionHitEventAuthoringModel model,
            Action<Action<CharacterActionHitEventAuthoringModel>> commit,
            bool editable)
        {
            CharacterActionHitBuffJsonDto[] buffs =
                CharacterActionHitEventAuthoringModel.NormalizeOnHitBuffs(model?.OnHitBuffs);
            VisualElement buffCard = AddCard(parent, "命中 Buff");
            buffCard.Add(new HelpBox(
                "有效 Damage 命中后应用这些 Buff 引用；持续时间、叠层和效果来自 Buff v2 生成数据。这里仅编辑 buffId，来源上下文由命中运行时提供。",
                HelpBoxMessageType.Info));

            if (buffs.Length == 0)
                buffCard.Add(new Label("未配置命中 Buff。"));

            for (int i = 0; i < buffs.Length; i++)
            {
                int index = i;
                CharacterActionHitBuffJsonDto buff = buffs[i];
                VisualElement row = new VisualElement();
                row.style.flexDirection = FlexDirection.Row;
                row.style.flexWrap = Wrap.Wrap;
                row.style.alignItems = Align.Center;
                row.style.marginBottom = 4f;

                AddOnHitBuffChoiceField(row, buff.BuffId, nextBuffId =>
                    commit(next => next.OnHitBuffs = ReplaceOnHitBuff(next.OnHitBuffs, index, nextBuffId)), editable);
                AddOnHitBuffIntegerField(row, "BuffId (manual)", buff.BuffId, nextBuffId =>
                    commit(next => next.OnHitBuffs = ReplaceOnHitBuff(next.OnHitBuffs, index, nextBuffId)), editable);

                var remove = new Button(() =>
                    commit(next => next.OnHitBuffs = RemoveOnHitBuff(next.OnHitBuffs, index)))
                {
                    text = "Remove",
                };
                remove.SetEnabled(editable);
                row.Add(remove);
                buffCard.Add(row);

                if (CharacterActionOnHitBuffCatalog.TryGetKnownChoice(
                        buff.BuffId,
                        out CharacterActionOnHitBuffChoice choice))
                {
                    if (!string.IsNullOrWhiteSpace(choice.Summary))
                        buffCard.Add(new HelpBox(choice.Summary, HelpBoxMessageType.Info));
                }
                else
                {
                    buffCard.Add(new HelpBox(
                        "BuffId " + buff.BuffId + " is not present in generated buff_definitions.json; the value will be kept and runtime validation will decide.",
                        HelpBoxMessageType.Warning));
                }
            }

            var buttons = new VisualElement();
            buttons.style.flexDirection = FlexDirection.Row;
            buttons.style.flexWrap = Wrap.Wrap;
            buttons.style.marginTop = 4f;
            var add = new Button(() =>
                commit(next => next.OnHitBuffs = AppendOnHitBuff(next.OnHitBuffs)))
            {
                text = "Add Buff",
            };
            add.SetEnabled(editable);
            buttons.Add(add);

            var clear = new Button(() => commit(next => next.OnHitBuffs = Array.Empty<CharacterActionHitBuffJsonDto>()))
            {
                text = "Clear",
            };
            clear.style.marginLeft = 4f;
            clear.SetEnabled(editable && buffs.Length > 0);
            buttons.Add(clear);
            buffCard.Add(buttons);
        }

        private static void AddOnHitBuffIntegerField(
            VisualElement parent,
            string label,
            int value,
            Action<int> apply,
            bool editable)
        {
            var field = new IntegerField(label)
            {
                value = value,
                isDelayed = true,
            };
            field.style.width = 150f;
            field.SetEnabled(editable);
            field.RegisterValueChangedCallback(change => apply(change.newValue));
            parent.Add(field);
        }

        private void AddOnHitBuffChoiceField(
            VisualElement parent,
            int currentBuffId,
            Action<int> apply,
            bool editable)
        {
            List<CharacterActionOnHitBuffChoice> choices = BuildOnHitBuffChoices(currentBuffId);
            if (choices.Count == 0)
                return;

            var labels = new List<string>(choices.Count);
            int selectedIndex = 0;
            for (int i = 0; i < choices.Count; i++)
            {
                labels.Add(CharacterActionAuthoringUiLabels.ToMenuSafeLabel(choices[i].Label));
                if (choices[i].BuffId == currentBuffId)
                    selectedIndex = i;
            }

            var field = new PopupField<string>("Buff", labels, selectedIndex);
            field.style.minWidth = 320f;
            field.SetEnabled(editable);
            field.RegisterValueChangedCallback(change =>
            {
                int index = labels.IndexOf(change.newValue);
                if (index < 0 || index >= choices.Count)
                    return;
                apply(choices[index].BuffId);
            });
            parent.Add(field);
        }

        private static CharacterActionHitBuffJsonDto[] ReplaceOnHitBuff(
            CharacterActionHitBuffJsonDto[] source,
            int index,
            int buffId)
        {
            CharacterActionHitBuffJsonDto[] values = CharacterActionHitEventAuthoringModel.NormalizeOnHitBuffs(source);
            if (index < 0 || index >= values.Length)
                return values;
            values[index] = new CharacterActionHitBuffJsonDto
            {
                BuffId = buffId,
            };
            return CharacterActionHitEventAuthoringModel.NormalizeOnHitBuffs(values);
        }

        private static CharacterActionHitBuffJsonDto[] RemoveOnHitBuff(
            CharacterActionHitBuffJsonDto[] source,
            int index)
        {
            CharacterActionHitBuffJsonDto[] values = CharacterActionHitEventAuthoringModel.NormalizeOnHitBuffs(source);
            if (index < 0 || index >= values.Length)
                return values;
            var next = new List<CharacterActionHitBuffJsonDto>(values);
            next.RemoveAt(index);
            return next.ToArray();
        }

        private static CharacterActionHitBuffJsonDto[] AppendOnHitBuff(CharacterActionHitBuffJsonDto[] source)
        {
            CharacterActionHitBuffJsonDto[] values = CharacterActionHitEventAuthoringModel.NormalizeOnHitBuffs(source);
            var next = new List<CharacterActionHitBuffJsonDto>(values)
            {
                new CharacterActionHitBuffJsonDto
                {
                    BuffId = PickDefaultOnHitBuffId(values),
                },
            };
            return CharacterActionHitEventAuthoringModel.NormalizeOnHitBuffs(next.ToArray());
        }

        private static int PickDefaultOnHitBuffId(CharacterActionHitBuffJsonDto[] existing)
        {
            var used = new HashSet<int>();
            existing = existing ?? Array.Empty<CharacterActionHitBuffJsonDto>();
            for (int i = 0; i < existing.Length; i++)
            {
                if (existing[i] != null && existing[i].BuffId > 0)
                    used.Add(existing[i].BuffId);
            }

            List<CharacterActionOnHitBuffChoice> choices = BuildOnHitBuffChoices(0);
            for (int i = 0; i < choices.Count; i++)
            {
                if (choices[i].BuffId > 0 && !used.Contains(choices[i].BuffId))
                    return choices[i].BuffId;
            }

            return 1;
        }

        private static List<CharacterActionOnHitBuffChoice> BuildOnHitBuffChoices(int currentBuffId)
        {
            return CharacterActionOnHitBuffCatalog.BuildChoices(currentBuffId);
        }

        private static void AddSharedChoiceField(
            VisualElement parent,
            string label,
            string value,
            List<(string Value, string Label)> choices,
            Action<string> apply,
            bool editable)
        {
            choices = choices ?? new List<(string Value, string Label)>();
            var labels = new List<string>(choices.Count);
            int selectedIndex = 0;
            for (int i = 0; i < choices.Count; i++)
            {
                labels.Add(CharacterActionAuthoringUiLabels.ToMenuSafeLabel(choices[i].Label));
                if (string.Equals(choices[i].Value, value ?? string.Empty, StringComparison.Ordinal))
                    selectedIndex = i;
            }

            if (labels.Count == 0)
                return;

            var field = new PopupField<string>(label, labels, selectedIndex);
            field.SetEnabled(editable);
            field.RegisterValueChangedCallback(evt =>
            {
                int index = labels.IndexOf(evt.newValue);
                if (index < 0 || index >= choices.Count)
                    return;
                apply(choices[index].Value);
            });
            parent.Add(field);
        }

        private static void AddSharedDamageTagToggles(
            VisualElement parent,
            string[] tags,
            Action<string[]> apply,
            bool editable)
        {
            VisualElement row = new VisualElement { style = { flexDirection = FlexDirection.Row, flexWrap = Wrap.Wrap } };
            row.Add(new Label("标签"));
            string[] choices = { "melee", "weapon", "light", "heavy", "area", "shockwave", "scatter", "projectile", "control" };
            for (int i = 0; i < choices.Length; i++)
            {
                string tag = choices[i];
                var toggle = new Toggle(FormatDamageTagLabel(tag))
                {
                    value = ContainsTag(tags, tag),
                };
                toggle.style.marginLeft = 6f;
                toggle.SetEnabled(editable);
                toggle.RegisterValueChangedCallback(evt =>
                {
                    string[] nextTags = evt.newValue
                        ? AddTag(tags, tag)
                        : RemoveTag(tags, tag);
                    apply(nextTags);
                });
                row.Add(toggle);
            }

            parent.Add(row);
        }

        private static void AddSharedHitTargetToggle(
            VisualElement parent,
            string label,
            CharacterActionHitEventAuthoringModel model,
            CombatHitTargetTypes targetType,
            Action<Action<CharacterActionHitEventAuthoringModel>> commit,
            bool editable)
        {
            var toggle = new Toggle(label)
            {
                value = (model.TargetTypes & targetType) != 0,
            };
            toggle.SetEnabled(editable);
            toggle.RegisterValueChangedCallback(change =>
            {
                commit(next =>
                {
                    CombatHitTargetTypes value = next.TargetTypes;
                    value = change.newValue ? value | targetType : value & ~targetType;
                    next.TargetTypes = value == CombatHitTargetTypes.None
                        ? CombatHitEventConfig.DefaultTargetTypes
                        : value;
                });
            });
            parent.Add(toggle);
        }

        private void AddSharedHitEventEnumPopup<TEnum>(
            VisualElement parent,
            string label,
            string value,
            Action<string> apply,
            bool editable)
            where TEnum : struct
        {
            string[] rawValues = Enum.GetNames(typeof(TEnum));
            var labels = new List<string>(rawValues.Length);
            int selectedIndex = 0;
            for (int i = 0; i < rawValues.Length; i++)
            {
                labels.Add(CharacterActionAuthoringUiLabels.ToMenuSafeLabel(FormatSharedHitEventEnumLabel(rawValues[i])));
                if (string.Equals(rawValues[i], value, StringComparison.Ordinal))
                    selectedIndex = i;
            }

            if (labels.Count == 0)
                return;

            var field = new PopupField<string>(label, labels, selectedIndex);
            field.SetEnabled(editable);
            field.RegisterValueChangedCallback(change =>
            {
                int index = labels.IndexOf(change.newValue);
                if (index < 0 || index >= rawValues.Length)
                    return;
                apply(rawValues[index]);
            });
            parent.Add(field);
        }

        private static void AddSharedHitEventIntegerField(
            VisualElement parent,
            string label,
            int value,
            Action<int> apply,
            bool editable)
        {
            var field = new IntegerField(label) { value = value, isDelayed = true };
            field.SetEnabled(editable);
            field.RegisterValueChangedCallback(change => apply(change.newValue));
            parent.Add(field);
        }

        private static void AddSharedHitEventFloatField(
            VisualElement parent,
            string label,
            float value,
            Action<float> apply,
            bool editable)
        {
            var field = new FloatField(label) { value = value, isDelayed = true };
            field.SetEnabled(editable);
            field.RegisterValueChangedCallback(change => apply(change.newValue));
            parent.Add(field);
        }

        private static string FormatSharedHitEventEnumLabel(string value)
        {
            switch (value)
            {
                case nameof(CombatEnvironmentHitPolicy.None):
                    return "不触发";
                case nameof(CombatEnvironmentHitPolicy.BlockedPresentation):
                    return "阻挡表现";
                case nameof(CombatHitSameTargetPolicy.AllowRepeat):
                    return "允许重复";
                case nameof(CombatHitSameTargetPolicy.OncePerEvent):
                    return "同事件只命中一次";
                default:
                    return value;
            }
        }
    }

    internal static class CharacterActionOnHitBuffCatalog
    {
        internal const string GeneratedBuffDefinitionsPath =
            "Assets/MxFrameworkGenerated/Gameplay/Buffs/buff_definitions.json";

        public static List<CharacterActionOnHitBuffChoice> BuildChoices(int currentBuffId)
        {
            List<CharacterActionOnHitBuffChoice> choices = LoadGeneratedChoices(GeneratedBuffDefinitionsPath);
            AddUnknownCurrentChoice(choices, currentBuffId);
            return choices;
        }

        internal static List<CharacterActionOnHitBuffChoice> BuildChoicesFromJson(string json, int currentBuffId)
        {
            List<CharacterActionOnHitBuffChoice> choices = LoadGeneratedChoicesFromJson(json);
            AddUnknownCurrentChoice(choices, currentBuffId);
            return choices;
        }

        public static bool TryGetKnownChoice(int buffId, out CharacterActionOnHitBuffChoice choice)
        {
            choice = default;
            if (buffId <= 0)
                return false;

            List<CharacterActionOnHitBuffChoice> choices = LoadGeneratedChoices(GeneratedBuffDefinitionsPath);
            for (int i = 0; i < choices.Count; i++)
            {
                if (choices[i].BuffId == buffId)
                {
                    choice = choices[i];
                    return true;
                }
            }

            return false;
        }

        private static List<CharacterActionOnHitBuffChoice> LoadGeneratedChoices(string path)
        {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
                return new List<CharacterActionOnHitBuffChoice>();

            return LoadGeneratedChoicesFromJson(File.ReadAllText(path));
        }

        internal static List<CharacterActionOnHitBuffChoice> LoadGeneratedChoicesFromJson(string json)
        {
            var choices = new List<CharacterActionOnHitBuffChoice>();
            if (string.IsNullOrWhiteSpace(json))
                return choices;

            try
            {
                JObject root = JObject.Parse(json);
                JArray definitions = root["definitions"] as JArray;
                for (int i = 0; definitions != null && i < definitions.Count; i++)
                    AddChoice(choices, definitions[i] as JObject);
            }
            catch
            {
                choices.Clear();
            }

            choices.Sort((left, right) => left.BuffId.CompareTo(right.BuffId));
            return choices;
        }

        private static void AddChoice(List<CharacterActionOnHitBuffChoice> choices, JObject definition)
        {
            if (definition == null)
                return;

            int buffId = ReadInt(definition, "buffId");
            if (buffId <= 0)
                return;

            string displayName = ReadString(definition, "displayName");
            string stableId = ReadString(definition, "stableId");
            string label = buffId.ToString(CultureInfo.InvariantCulture);
            if (!string.IsNullOrWhiteSpace(displayName))
                label += " - " + displayName;
            else if (!string.IsNullOrWhiteSpace(stableId))
                label += " - " + stableId;

            choices.Add(new CharacterActionOnHitBuffChoice(buffId, label, BuildSummary(definition), isKnown: true));
        }

        private static void AddUnknownCurrentChoice(List<CharacterActionOnHitBuffChoice> choices, int currentBuffId)
        {
            bool containsCurrent = currentBuffId <= 0;
            for (int i = 0; i < choices.Count; i++)
            {
                if (choices[i].BuffId == currentBuffId)
                {
                    containsCurrent = true;
                    break;
                }
            }

            if (!containsCurrent)
            {
                choices.Insert(
                    0,
                    new CharacterActionOnHitBuffChoice(
                        currentBuffId,
                        currentBuffId.ToString(CultureInfo.InvariantCulture) + " [unknown]",
                        string.Empty,
                        isKnown: false));
            }
        }

        private static string BuildSummary(JObject definition)
        {
            var parts = new List<string>();
            bool isPermanent = ReadBool(definition, "isPermanent");
            int defaultDurationFrames = ReadInt(definition, "defaultDurationFrames");
            parts.Add(isPermanent
                ? "permanent"
                : defaultDurationFrames.ToString(CultureInfo.InvariantCulture) + "f");

            string stackPolicy = ReadString(definition, "stackPolicy");
            parts.Add(string.IsNullOrWhiteSpace(stackPolicy) ? "UnknownStackPolicy" : stackPolicy);

            int applyStackCount = ReadInt(definition, "applyStackCount");
            int maxStackCount = ReadInt(definition, "maxStackCount");
            parts.Add(
                "stack +"
                + applyStackCount.ToString(CultureInfo.InvariantCulture)
                + "/max "
                + maxStackCount.ToString(CultureInfo.InvariantCulture));

            string effects = BuildEffectsSummary(definition["effects"] as JArray);
            if (!string.IsNullOrWhiteSpace(effects))
                parts.Add(effects);

            return string.Join(" | ", parts.ToArray());
        }

        private static string BuildEffectsSummary(JArray effects)
        {
            if (effects == null || effects.Count == 0)
                return string.Empty;

            var summaries = new List<EffectSummary>(effects.Count);
            for (int i = 0; i < effects.Count; i++)
            {
                JObject effect = effects[i] as JObject;
                if (effect == null)
                    continue;

                int effectId = ReadInt(effect, "effectId");
                string text = BuildEffectSummary(effect);
                if (string.IsNullOrWhiteSpace(text))
                    text = "Effect(" + effectId.ToString(CultureInfo.InvariantCulture) + ")";
                summaries.Add(new EffectSummary(effectId, i, text));
            }

            summaries.Sort((left, right) =>
            {
                int byEffectId = left.EffectId.CompareTo(right.EffectId);
                return byEffectId != 0 ? byEffectId : left.Index.CompareTo(right.Index);
            });

            var parts = new List<string>(summaries.Count);
            for (int i = 0; i < summaries.Count; i++)
                parts.Add(summaries[i].Text);
            return string.Join(", ", parts.ToArray());
        }

        private static string BuildEffectSummary(JObject effect)
        {
            string kind = ReadString(effect, "kind");
            if (string.Equals(kind, "AttributeModifier", StringComparison.Ordinal))
            {
                int attributeId = ReadInt(effect, "attributeId");
                int addValue = ReadInt(effect, "addValue");
                return FormatAttributeName(attributeId) + " " + FormatSigned(addValue);
            }

            if (string.Equals(kind, "Status", StringComparison.Ordinal))
            {
                int statusId = ReadInt(effect, "statusId");
                return "Status(" + statusId.ToString(CultureInfo.InvariantCulture) + ")";
            }

            return string.IsNullOrWhiteSpace(kind) ? string.Empty : "EffectKind(" + kind + ")";
        }

        private static string FormatAttributeName(int attributeId)
        {
            switch (attributeId)
            {
                case 1:
                    return "HP(1)";
                case 2:
                    return "Attack(2)";
                case 3:
                    return "Defense(3)";
                case 4:
                    return "ScatterPressure(4)";
                default:
                    return "Attribute(" + attributeId.ToString(CultureInfo.InvariantCulture) + ")";
            }
        }

        private static string FormatSigned(int value)
        {
            return value >= 0
                ? "+" + value.ToString(CultureInfo.InvariantCulture)
                : value.ToString(CultureInfo.InvariantCulture);
        }

        private static int ReadInt(JObject source, string name)
        {
            JToken token = source?[name];
            if (token == null)
                return 0;
            if (token.Type == JTokenType.Integer)
                return token.Value<int>();
            if (token.Type == JTokenType.Float)
                return (int)token.Value<float>();
            return int.TryParse(token.ToString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int value)
                ? value
                : 0;
        }

        private static bool ReadBool(JObject source, string name)
        {
            JToken token = source?[name];
            if (token == null)
                return false;
            if (token.Type == JTokenType.Boolean)
                return token.Value<bool>();
            return bool.TryParse(token.ToString(), out bool value) && value;
        }

        private static string ReadString(JObject source, string name)
        {
            JToken token = source?[name];
            return token?.ToString() ?? string.Empty;
        }

        private readonly struct EffectSummary
        {
            public EffectSummary(int effectId, int index, string text)
            {
                EffectId = effectId;
                Index = index;
                Text = text ?? string.Empty;
            }

            public int EffectId { get; }
            public int Index { get; }
            public string Text { get; }
        }
    }

    internal readonly struct CharacterActionOnHitBuffChoice
    {
        public CharacterActionOnHitBuffChoice(int buffId, string label, string summary, bool isKnown)
        {
            BuffId = buffId;
            Label = label ?? string.Empty;
            Summary = summary ?? string.Empty;
            IsKnown = isKnown;
        }

        public int BuffId { get; }
        public string Label { get; }
        public string Summary { get; }
        public bool IsKnown { get; }
    }
}
