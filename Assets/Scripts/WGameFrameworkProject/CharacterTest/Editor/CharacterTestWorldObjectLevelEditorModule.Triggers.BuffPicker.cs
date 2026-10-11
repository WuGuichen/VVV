using System;
using System.Collections.Generic;
using System.Globalization;
using MxFramework.Editor;
using MxFramework.Gameplay;
using UnityEngine.UIElements;

namespace MxFramework.Game.CharacterTest.Editor
{
    public sealed partial class CharacterTestWorldObjectLevelEditorModule
    {
        private VisualElement CreateTriggerBuffPicker(
            long currentBuffId,
            Func<long, bool> apply)
        {
            if (!TryBuildTriggerBuffChoices(
                    out List<MxAuthoringReferenceOption<long>> choices,
                    out string catalogWarning))
            {
                choices.Clear();
            }

            VisualElement root = MxAuthoringReferenceField.Create(
                "Buff",
                currentBuffId,
                choices,
                value =>
                {
                    if (apply(value))
                        RefreshTriggerInspectorAfterMutation();
                },
                name: "character-test-trigger-buff-picker",
                emptyDiagnostic:
                    "未找到可用的 Gameplay Buff Registry 定义。"
                    + catalogWarning,
                unknownValueFormatter:
                    value => value <= 0L
                        ? "请选择 Buff"
                        : "未登记 Buff "
                            + value.ToString(
                                CultureInfo.InvariantCulture),
                unknownDiagnostic:
                    currentBuffId <= 0L
                        ? "保存前请选择 Gameplay Buff Registry 中的正式定义。"
                        : "当前 Buff ID 不在 Gameplay Buff Registry 中，"
                            + "原值已保留；仅迁移旧数据时使用高级规则中的手动 ID。");
            root.AddToClassList(
                "mx-level-editor__trigger-buff-editor");
            root.Q<DropdownField>(
                    MxAuthoringReferenceField.ValueElementName)
                ?.AddToClassList(
                    "mx-level-editor__trigger-input");
            root.Q<Label>(
                    MxAuthoringReferenceField.SummaryElementName)
                ?.AddToClassList(
                    "mx-level-editor__trigger-buff-summary");

            if (!string.IsNullOrWhiteSpace(catalogWarning)
                && choices.Count > 0)
            {
                root.Q<VisualElement>(
                        MxAuthoringReferenceField.ContentElementName)
                    ?.Add(new HelpBox(
                        catalogWarning,
                        HelpBoxMessageType.Warning)
                    {
                        name =
                            "character-test-trigger-buff-catalog-warning"
                    });
            }
            return root;
        }

        private static bool TryBuildTriggerBuffChoices(
            out List<MxAuthoringReferenceOption<long>> choices,
            out string catalogWarning)
        {
            choices =
                new List<MxAuthoringReferenceOption<long>>();
            catalogWarning = string.Empty;

            GameplayBuffDefinitionRegistry definitions;
            try
            {
                definitions =
                    CharacterTestGuardAuraRuntime.CreateBuffDefinitions();
            }
            catch (Exception exception)
            {
                catalogWarning = " " + exception.Message;
                return false;
            }

            if (definitions.Count == 0)
            {
                catalogWarning =
                    " Gameplay Buff Registry 为空。";
                return false;
            }

            GameplayBuffPresentationDefinitionRegistry presentations;
            try
            {
                presentations =
                    CharacterTestGuardAuraRuntime
                        .CreateBuffPresentationDefinitions();
            }
            catch (Exception exception)
            {
                presentations =
                    new GameplayBuffPresentationDefinitionRegistry();
                catalogWarning =
                    "展示目录不可用，名称已回退为 Stable ID。"
                    + exception.Message;
            }

            GameplayBuffDefinition[] rows = definitions.ToArray();
            for (int i = 0; i < rows.Length; i++)
            {
                GameplayBuffDefinition definition = rows[i];
                GameplayBuffPresentationDefinition presentation;
                if (!presentations.TryGet(
                        definition.BuffId,
                        out presentation))
                {
                    presentation =
                        GameplayBuffPresentationSnapshotBuilder
                            .CreateFallbackPresentation(definition);
                }

                choices.Add(
                    new MxAuthoringReferenceOption<long>(
                        string.IsNullOrWhiteSpace(definition.StableId)
                            ? "buff:"
                                + definition.BuffId.ToString(
                                    CultureInfo.InvariantCulture)
                            : definition.StableId,
                        definition.BuffId,
                        BuildTriggerBuffLabel(definition, presentation),
                        BuildTriggerBuffSummary(definition, presentation),
                        BuildTriggerBuffTooltip(
                            definition,
                            presentation,
                            catalogWarning)));
            }

            return true;
        }

        private static int FindTriggerBuffChoice(
            IReadOnlyList<MxAuthoringReferenceOption<long>> choices,
            long buffId)
        {
            for (int i = 0; choices != null && i < choices.Count; i++)
            {
                if (choices[i].Value == buffId)
                    return i;
            }

            return -1;
        }

        private static string BuildTriggerBuffLabel(
            GameplayBuffDefinition definition,
            GameplayBuffPresentationDefinition presentation)
        {
            string displayName =
                string.IsNullOrWhiteSpace(presentation.DisplayName)
                    ? definition.StableId
                    : presentation.DisplayName;
            displayName = CharacterTestLevelEditorChineseText.BuffName(
                definition.BuffId,
                displayName);
            return definition.BuffId.ToString(
                    CultureInfo.InvariantCulture)
                + " · "
                + displayName;
        }

        private static string BuildTriggerBuffSummary(
            GameplayBuffDefinition definition,
            GameplayBuffPresentationDefinition presentation)
        {
            string ruleSummary =
                BuildTriggerBuffDurationSummary(definition)
                + " · "
                + BuildTriggerBuffStackingSummary(definition);
            string gameplaySummary =
                CharacterTestLevelEditorChineseText.BuffGameplaySummary(
                    definition.BuffId,
                    string.IsNullOrWhiteSpace(presentation.Description)
                        ? presentation.EffectSummary
                        : presentation.Description);
            return string.IsNullOrWhiteSpace(gameplaySummary)
                ? ruleSummary
                : ruleSummary + "\n" + gameplaySummary;
        }

        private static string BuildTriggerBuffTooltip(
            GameplayBuffDefinition definition,
            GameplayBuffPresentationDefinition presentation,
            string catalogWarning)
        {
            string tooltip = string.IsNullOrWhiteSpace(definition.StableId)
                ? string.Empty
                : definition.StableId + "\n";
            tooltip += definition.IsPermanent
                ? "持续时间：永久"
                : "持续时间："
                    + definition.DefaultDurationFrames.ToString(
                        CultureInfo.InvariantCulture)
                    + " 逻辑帧（按 "
                    + CharacterTestDeterministicUnits.DefaultTickRate
                        .ToString(CultureInfo.InvariantCulture)
                    + " Hz 约 "
                    + FormatTriggerBuffDurationSeconds(
                        definition.DefaultDurationFrames)
                    + "）";
            tooltip += "\n叠层策略："
                + definition.StackPolicy
                + "\n每次施加层数："
                + definition.ApplyStackCount.ToString(
                    CultureInfo.InvariantCulture)
                + "\n最大层数："
                + definition.MaxStackCount.ToString(
                    CultureInfo.InvariantCulture);
            if (!string.IsNullOrWhiteSpace(presentation.EffectSummary))
            {
                tooltip += "\n定义效果："
                    + presentation.EffectSummary;
            }
            return string.IsNullOrWhiteSpace(catalogWarning)
                ? tooltip
                : tooltip + "\n" + catalogWarning;
        }

        private static string BuildTriggerBuffDurationSummary(
            GameplayBuffDefinition definition)
        {
            return definition.IsPermanent
                ? "永久"
                : "持续 "
                    + FormatTriggerBuffDurationSeconds(
                        definition.DefaultDurationFrames);
        }

        private static string BuildTriggerBuffStackingSummary(
            GameplayBuffDefinition definition)
        {
            switch (definition.StackPolicy)
            {
                case GameplayBuffStackPolicy.Replace:
                    return definition.MaxStackCount == 1
                        ? "重复施加替换并重新计时 · 不可叠加"
                        : "重复施加重置为 "
                            + definition.ApplyStackCount.ToString(
                                CultureInfo.InvariantCulture)
                            + " 层 · 最多 "
                            + definition.MaxStackCount.ToString(
                                CultureInfo.InvariantCulture)
                            + " 层";
                case GameplayBuffStackPolicy.Stack:
                    return "重复施加每次 +"
                        + definition.ApplyStackCount.ToString(
                            CultureInfo.InvariantCulture)
                        + " 层 · 最多 "
                        + definition.MaxStackCount.ToString(
                            CultureInfo.InvariantCulture)
                        + " 层";
                default:
                    return definition.MaxStackCount == 1
                        ? "重复施加刷新 · 不可叠加"
                        : "重复施加刷新 · 初始 "
                            + definition.ApplyStackCount.ToString(
                                CultureInfo.InvariantCulture)
                            + " 层 · 刷新不增层";
            }
        }

        private static string FormatTriggerBuffDurationSeconds(long frames)
        {
            double seconds = frames
                / (double)CharacterTestDeterministicUnits.DefaultTickRate;
            string format = Math.Abs(seconds - Math.Round(seconds)) < 0.0001d
                ? "0"
                : "0.##";
            return seconds.ToString(format, CultureInfo.InvariantCulture)
                + " 秒";
        }
    }
}
