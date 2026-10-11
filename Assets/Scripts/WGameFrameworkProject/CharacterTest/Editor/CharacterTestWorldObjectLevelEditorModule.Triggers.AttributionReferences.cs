using System;
using System.Collections.Generic;
using System.Globalization;
using MxFramework.Combat.GameplayBridge;
using MxFramework.Editor;
using UnityEngine.UIElements;

namespace MxFramework.Game.CharacterTest.Editor
{
    public sealed partial class CharacterTestWorldObjectLevelEditorModule
    {
        private VisualElement CreateTriggerBuffAttributionEditor(
            CharacterTestLevelGameplayTriggerEntry entry,
            long payload0,
            long payload1,
            long payload2)
        {
            TryBuildTriggerBuffAttributionChoices(
                out List<MxAuthoringReferenceOption<long>> choices,
                out string catalogWarning);
            VisualElement field = MxAuthoringReferenceField.Create(
                "来源机制",
                payload1,
                choices,
                value =>
                {
                    if (UpdateTriggerPayloads(
                            entry.StableId,
                            payload0,
                            value,
                            payload2))
                    {
                        RefreshTriggerInspectorAfterMutation();
                    }
                },
                name: "character-test-trigger-buff-attribution",
                emptyDiagnostic:
                    "Aura / Mechanic Source 正式目录中没有可用的来源机制。",
                unknownValueFormatter:
                    value => "未登记来源机制 "
                        + value.ToString(
                            CultureInfo.InvariantCulture),
                unknownDiagnostic:
                    payload1 > 0L
                        ? "当前来源机制 ID 不在 Aura / Mechanic Source 正式目录中，"
                            + "原值已保留；可从有效项重选，或在高级规则中维护迁移值。"
                        : "当前来源机制值无效，原值已保留。");
            field.AddToClassList(
                "mx-level-editor__trigger-source-editor");
            field.Q<DropdownField>(
                    MxAuthoringReferenceField.ValueElementName)
                ?.AddToClassList(
                    "mx-level-editor__trigger-input");
            field.Q<Label>(
                    MxAuthoringReferenceField.SummaryElementName)
                ?.AddToClassList(
                    "mx-level-editor__trigger-source-summary");

            if (!string.IsNullOrWhiteSpace(catalogWarning))
            {
                field.Q<VisualElement>(
                        MxAuthoringReferenceField.ContentElementName)
                    ?.Add(new HelpBox(
                        catalogWarning,
                        HelpBoxMessageType.Warning)
                    {
                        name =
                            "character-test-trigger-attribution-catalog-warning"
                    });
            }
            return field;
        }

        private static bool TryBuildTriggerBuffAttributionChoices(
            out List<MxAuthoringReferenceOption<long>> choices,
            out string catalogWarning)
        {
            var byId =
                new Dictionary<long, MxAuthoringReferenceOption<long>>();
            var conflicts = new List<string>();
            AddTriggerAttributionChoice(
                byId,
                conflicts,
                0L,
                "none",
                "未指定",
                "不记录来源机制。",
                "底层 SourceId 留空；这不是 Runtime 命令来源。");

            try
            {
                CombatGameplayAuraBuffDefinition[] auraDefinitions =
                    CharacterTestGuardAuraRuntime
                        .CreateAuraDefinitions()
                        .ToArray();
                for (int i = 0; i < auraDefinitions.Length; i++)
                {
                    CombatGameplayAuraBuffDefinition definition =
                        auraDefinitions[i];
                    string stableId =
                        ResolveTriggerAttributionStableId(
                            definition.AuraId,
                            "aura:"
                                + definition.AuraId.ToString(
                                    CultureInfo.InvariantCulture));
                    AddTriggerAttributionChoice(
                        byId,
                        conflicts,
                        definition.AuraId,
                        stableId,
                        ResolveTriggerAttributionDisplayName(
                            definition.AuraId,
                            stableId),
                        BuildTriggerSourceMechanismSummary(),
                        BuildTriggerSourceMechanismTooltip(
                            stableId,
                            "Aura"));
                }

                AddMechanicSourceDefinitions(
                    CharacterTestAreaConfinementRuntime
                        .CreateMechanicSourceDefinitions()
                        .ToArray(),
                    byId,
                    conflicts);
                AddMechanicSourceDefinitions(
                    CharacterTestPoisonBottlePoolRuntime
                        .CreateMechanicSourceDefinitions()
                        .ToArray(),
                    byId,
                    conflicts);
            }
            catch (Exception exception)
            {
                conflicts.Add(
                    "来源机制目录读取失败：" + exception.Message);
            }

            choices =
                new List<MxAuthoringReferenceOption<long>>(byId.Values);
            choices.Sort(
                (left, right) => left.Value.CompareTo(right.Value));
            catalogWarning = conflicts.Count == 0
                ? string.Empty
                : string.Join(" ", conflicts);
            return choices.Count > 0;
        }

        private static void AddMechanicSourceDefinitions(
            CharacterTestMechanicSourceDefinition[] definitions,
            Dictionary<long, MxAuthoringReferenceOption<long>> byId,
            List<string> conflicts)
        {
            for (int i = 0;
                 definitions != null && i < definitions.Length;
                 i++)
            {
                CharacterTestMechanicSourceDefinition definition =
                    definitions[i];
                AddTriggerAttributionChoice(
                    byId,
                    conflicts,
                    definition.SourceId,
                    definition.StableId,
                    ResolveTriggerAttributionDisplayName(
                        definition.SourceId,
                        definition.StableId),
                    BuildTriggerSourceMechanismSummary(),
                    BuildTriggerSourceMechanismTooltip(
                        definition.StableId,
                        "Mechanic Source"));
            }
        }

        private static void AddTriggerAttributionChoice(
            Dictionary<long, MxAuthoringReferenceOption<long>> byId,
            List<string> conflicts,
            long sourceId,
            string stableId,
            string displayName,
            string summary,
            string tooltip)
        {
            string key = string.IsNullOrWhiteSpace(stableId)
                ? "source:"
                    + sourceId.ToString(
                        CultureInfo.InvariantCulture)
                : stableId.Trim();
            if (byId.TryGetValue(
                    sourceId,
                    out MxAuthoringReferenceOption<long> existing))
            {
                if (!string.Equals(
                        existing.Key,
                        key,
                        StringComparison.Ordinal))
                {
                    conflicts.Add(
                        "来源机制 ID "
                        + sourceId.ToString(
                            CultureInfo.InvariantCulture)
                        + " 同时被 "
                        + existing.Key
                        + " 与 "
                        + key
                        + " 使用。");
                }
                return;
            }

            string resolvedName = string.IsNullOrWhiteSpace(displayName)
                ? key
                : displayName.Trim();
            string label = sourceId == 0L
                ? resolvedName
                : sourceId.ToString(
                    CultureInfo.InvariantCulture)
                    + " · "
                    + resolvedName;
            byId.Add(
                sourceId,
                new MxAuthoringReferenceOption<long>(
                    key,
                    sourceId,
                    label,
                    summary,
                    tooltip));
        }

        private static string ResolveTriggerAttributionStableId(
            int sourceId,
            string fallback)
        {
            return sourceId == CharacterTestGuardAuraIds.AuraId
                ? CharacterTestAbilityIds.GuardAuraAbilityStableId
                : fallback;
        }

        private static string BuildTriggerSourceMechanismSummary()
        {
            return "仅记录施加来源，不改变 Buff 的伤害、持续时间或叠层。";
        }

        private static string BuildTriggerSourceMechanismTooltip(
            string stableId,
            string sourceKind)
        {
            return stableId
                + "\n来源类型："
                + sourceKind
                + "\n底层写入 GameplayBuffApplicationRequest.SourceId。"
                + "\n不会执行该来源定义中的其他效果，也不是 Runtime 命令来源。";
        }

        private static string ResolveTriggerAttributionDisplayName(
            int sourceId,
            string fallbackStableId)
        {
            return CharacterTestLevelEditorChineseText
                .EffectAttributionName(
                    sourceId,
                    fallbackStableId);
        }

        internal static long[]
            GetTriggerBuffAttributionIdsForTests()
        {
            TryBuildTriggerBuffAttributionChoices(
                out List<MxAuthoringReferenceOption<long>> choices,
                out _);
            var ids = new long[choices.Count];
            for (int i = 0; i < choices.Count; i++)
                ids[i] = choices[i].Value;
            return ids;
        }
    }
}
