using System;
using System.Collections.Generic;
using System.Globalization;
using MxFramework.Config.Levels;
using MxFramework.Editor;
using MxFramework.Editor.Levels;
using MxFramework.Gameplay;
using MxFramework.Gameplay.Triggers;
using UnityEngine.UIElements;

namespace MxFramework.Game.CharacterTest.Editor
{
    public sealed partial class CharacterTestWorldObjectLevelEditorModule
    {
        private VisualElement CreateTriggerTargetSemanticsField(
            bool sourceResolved,
            GameplayTriggerTargetSelector selector,
            string sourceDiagnostic)
        {
            if (!sourceResolved)
            {
                return CreateTriggerTargetRequirementsLabel(
                    "对象要求未解析",
                    sourceDiagnostic);
            }

            if (!TryResolveTriggerTargetSemantics(
                    selector,
                    out GameplayTargetClassificationComponent classification,
                    out GameplayTargetRelationFilter relationFilter,
                    out bool relationApplies,
                    out string diagnostic))
            {
                return CreateTriggerTargetRequirementsLabel(
                    "对象要求未解析",
                    diagnostic);
            }

            return CreateTriggerTargetRequirementsLabel(
                FormatTriggerTargetRequirements(
                    classification,
                    relationFilter,
                    relationApplies),
                BuildTriggerTargetSemanticsTooltip(
                    selector,
                    classification,
                    relationFilter,
                    relationApplies));
        }

        private VisualElement CreateTriggerSubjectFilterEditor(
            CharacterTestLevelGameplayTriggerEntry entry,
            bool sourceResolved,
            GameplayTriggerTargetSelector selector,
            string sourceDiagnostic)
        {
            var root = new VisualElement
            {
                name = "character-test-trigger-subject-filter"
            };
            if (!sourceResolved
                || selector != GameplayTriggerTargetSelector.Subject
                || !_triggerDocument.TryGetSubjectFilter(
                    entry,
                    out CharacterTestLevelGameplayTriggerSubjectFilter filter))
            {
                root.Add(
                    new HelpBox(
                        string.IsNullOrWhiteSpace(sourceDiagnostic)
                            ? "触发对象筛选规则无法解析。"
                            : sourceDiagnostic,
                        HelpBoxMessageType.Warning));
                return root;
            }

            List<MxAuthoringReferenceOption<
                GameplayTargetRelationFilter>> relationOptions =
                CreateTriggerRelationOptions();
            VisualElement relationField =
                MxAuthoringReferenceField.Create(
                    string.Empty,
                    filter.RelationFilter,
                    relationOptions,
                    relation =>
                    {
                        int referenceTeamId = 0;
                        if (relation != GameplayTargetRelationFilter.Any)
                        {
                            List<MxAuthoringReferenceOption<int>>
                                teamOptions =
                                    CreateTriggerReferenceTeamOptions();
                            if (filter.ReferenceTeamId > 0)
                            {
                                referenceTeamId =
                                    filter.ReferenceTeamId;
                            }
                            else if (teamOptions.Count > 0)
                            {
                                referenceTeamId =
                                    teamOptions[0].Value;
                            }
                            else
                            {
                                RefreshInspector(
                                    preserveScrollPosition: true);
                                return;
                            }
                        }

                        if (UpdateTriggerSubjectRelation(
                                entry.StableId,
                                relation,
                                referenceTeamId))
                        {
                            RefreshTriggerInspectorAfterMutation();
                        }
                        else
                        {
                            RefreshInspector(
                                preserveScrollPosition: true);
                        }
                    },
                    name:
                        "character-test-trigger-subject-relation",
                    unknownValueFormatter:
                        value => "未识别关系：" + value,
                    unknownDiagnostic:
                        "当前关系值不受此区域来源支持；原值已保留，请选择有效关系。");
            PrepareTriggerClauseReference(
                relationField,
                "mx-level-editor__trigger-clause-control--medium");

            root.Add(CreateTriggerClauseRow(
                CreateTriggerClauseValue("触发角色"),
                CreateTriggerClauseWord("必须是"),
                CreateTriggerClauseValue(
                    FormatSubjectFilterIdentityRule(filter),
                    "身份、最低能力和存活要求由当前正式模板锁定。"),
                CreateTriggerClauseScope("锁定")));

            if (filter.RelationFilter
                != GameplayTargetRelationFilter.Any)
            {
                List<MxAuthoringReferenceOption<int>> teamOptions =
                    CreateTriggerReferenceTeamOptions();
                VisualElement teamField =
                    MxAuthoringReferenceField.Create(
                        string.Empty,
                        filter.ReferenceTeamId,
                        teamOptions,
                        teamId =>
                        {
                            if (UpdateTriggerSubjectRelation(
                                    entry.StableId,
                                    filter.RelationFilter,
                                    teamId))
                            {
                                RefreshTriggerInspectorAfterMutation();
                            }
                        },
                        name:
                            "character-test-trigger-subject-reference-team",
                        emptyDiagnostic:
                            "场景角色文档中没有可用的正整数队伍 ID。",
                        unknownValueFormatter:
                            value => value > 0
                                ? "队伍 " + value + "（未登记）"
                                : "未选择参照队伍",
                        unknownDiagnostic:
                            "当前参照队伍不在场景角色文档中；原值已保留，请重新选择。");
                PrepareTriggerClauseReference(
                    teamField,
                    "mx-level-editor__trigger-clause-control--medium");
                root.Add(CreateTriggerClauseRow(
                    CreateTriggerClauseValue("触发角色"),
                    CreateTriggerClauseWord("与"),
                    teamField,
                    CreateTriggerClauseWord("的关系为"),
                    relationField));
            }
            else
            {
                root.Add(CreateTriggerClauseRow(
                    CreateTriggerClauseValue("触发角色"),
                    CreateTriggerClauseWord("的阵营关系"),
                    relationField));
            }

            return root;
        }

        private static string FormatSubjectFilterIdentityRule(
            CharacterTestLevelGameplayTriggerSubjectFilter filter)
        {
            string summary = FormatTargetIdentityType(
                filter.AcceptedIdentityTypes);
            if (filter.RequiredCapabilities
                != GameplayTargetCapabilities.None)
            {
                summary += " · "
                    + FormatTargetCapabilities(
                        filter.RequiredCapabilities);
            }
            if (filter.RequireAlive)
                summary += " · 存活";
            return summary;
        }

        private static List<MxAuthoringReferenceOption<
            GameplayTargetRelationFilter>> CreateTriggerRelationOptions()
        {
            return new List<MxAuthoringReferenceOption<
                GameplayTargetRelationFilter>>
            {
                CreateRelationOption(
                    GameplayTargetRelationFilter.Any,
                    "阵营不限"),
                CreateRelationOption(
                    GameplayTargetRelationFilter.SameTeam,
                    "仅同队"),
                CreateRelationOption(
                    GameplayTargetRelationFilter.Enemy,
                    "仅敌对"),
                CreateRelationOption(
                    GameplayTargetRelationFilter.Neutral,
                    "仅中立")
            };
        }

        private static MxAuthoringReferenceOption<
            GameplayTargetRelationFilter> CreateRelationOption(
            GameplayTargetRelationFilter value,
            string displayName)
        {
            return new MxAuthoringReferenceOption<
                GameplayTargetRelationFilter>(
                ((int)value).ToString(CultureInfo.InvariantCulture),
                value,
                displayName,
                tooltip:
                    "使用 GameplayTeamRelations 对参照队伍和进入区域的角色进行判定。");
        }

        private List<MxAuthoringReferenceOption<int>>
            CreateTriggerReferenceTeamOptions()
        {
            var result =
                new List<MxAuthoringReferenceOption<int>>();
            if (!CharacterTestSceneCharacterAuthoringAdapter
                    .TryExtractCharactersDocument(
                        _envelope,
                        out CharacterTestSceneCharacterAuthoringDocument
                            characters,
                        out _)
                || characters?.Characters == null)
            {
                return result;
            }

            var teamIds = new List<int>();
            for (int i = 0; i < characters.Characters.Count; i++)
            {
                CharacterTestSceneCharacterDefinition character =
                    characters.Characters[i];
                if (character == null
                    || character.TeamId <= 0
                    || teamIds.Contains(character.TeamId))
                {
                    continue;
                }

                teamIds.Add(character.TeamId);
            }

            teamIds.Sort();
            for (int i = 0; i < teamIds.Count; i++)
            {
                int teamId = teamIds[i];
                result.Add(
                    new MxAuthoringReferenceOption<int>(
                        teamId.ToString(CultureInfo.InvariantCulture),
                        teamId,
                        "队伍 " + teamId,
                        tooltip:
                            "候选来自当前 CharacterTest 场景角色文档的 TeamId。"));
            }

            return result;
        }

        private static Label CreateTriggerTargetRequirementsLabel(
            string text,
            string tooltip)
        {
            var label = new Label(text ?? string.Empty)
            {
                name = "character-test-trigger-target-requirements",
                tooltip = tooltip ?? string.Empty
            };
            label.AddToClassList(
                "mx-level-editor__trigger-target-requirements");
            label.style.display = string.IsNullOrWhiteSpace(text)
                ? DisplayStyle.None
                : DisplayStyle.Flex;
            return label;
        }

        private static string FormatTriggerTargetRequirements(
            GameplayTargetClassificationComponent classification,
            GameplayTargetRelationFilter relationFilter,
            bool relationApplies)
        {
            string summary = string.Empty;
            if ((classification.Capabilities
                    & GameplayTargetCapabilities.Damageable) != 0)
            {
                AppendTriggerTargetRequirement(
                    ref summary,
                    "可伤害");
            }

            if (relationApplies)
            {
                AppendTriggerTargetRequirement(
                    ref summary,
                    FormatTargetAudience(relationFilter));
            }

            return summary;
        }

        private static void AppendTriggerTargetRequirement(
            ref string summary,
            string requirement)
        {
            if (string.IsNullOrWhiteSpace(requirement))
                return;

            if (!string.IsNullOrEmpty(summary))
                summary += " · ";
            summary += requirement;
        }

        private static string FormatTargetAudience(
            GameplayTargetRelationFilter relationFilter)
        {
            switch (relationFilter)
            {
                case GameplayTargetRelationFilter.Self:
                    return "仅自身";
                case GameplayTargetRelationFilter.SameTeam:
                    return "仅同队";
                case GameplayTargetRelationFilter.Enemy:
                    return "仅敌对";
                case GameplayTargetRelationFilter.Neutral:
                    return "仅中立";
                case GameplayTargetRelationFilter.Any:
                    return "阵营不限";
                default:
                    return "关系未识别";
            }
        }

        private static bool TryResolveTriggerTargetSemantics(
            GameplayTriggerTargetSelector selector,
            out GameplayTargetClassificationComponent classification,
            out GameplayTargetRelationFilter relationFilter,
            out bool relationApplies,
            out string diagnostic)
        {
            classification = default;
            relationFilter = GameplayTargetRelationFilter.Any;
            relationApplies = false;
            diagnostic = string.Empty;

            if (selector == GameplayTriggerTargetSelector.Subject)
            {
                classification =
                    new GameplayTargetClassificationComponent(
                        GameplayTargetIdentityTypes.Character,
                        GameplayTargetCapabilities.Damageable
                            | GameplayTargetCapabilities.Movable);
                relationApplies = true;
                return true;
            }

            if (selector
                == GameplayTriggerTargetSelector.ExplicitBindingTarget)
            {
                if (MxLevelAuthoringTargetClassification.TryResolve(
                    LevelAuthoringDefinitionUsage.Region,
                    out classification))
                {
                    return true;
                }

                diagnostic =
                    "当前区域绑定没有可用的关卡身份映射。";
                return false;
            }

            diagnostic =
                "当前目标来源没有对应的正式基础身份规则。";
            return false;
        }

        private static string FormatTriggerTargetSemantics(
            GameplayTargetClassificationComponent classification,
            GameplayTargetRelationFilter relationFilter,
            bool relationApplies)
        {
            string summary = FormatTargetIdentityType(
                classification.IdentityType);
            if (classification.Capabilities
                != GameplayTargetCapabilities.None)
            {
                summary += " · "
                    + FormatTargetCapabilities(
                        classification.Capabilities);
            }

            if (relationApplies)
            {
                summary += " · "
                    + FormatTargetRelation(relationFilter);
            }

            return summary;
        }

        private static string BuildTriggerTargetSemanticsTooltip(
            GameplayTriggerTargetSelector selector,
            GameplayTargetClassificationComponent classification,
            GameplayTargetRelationFilter relationFilter,
            bool relationApplies)
        {
            string tooltip =
                "目标来源："
                + selector
                + "；基础身份："
                + classification.IdentityType
                + "；能力："
                + classification.Capabilities;
            if (relationApplies)
                tooltip += "；关系：" + relationFilter;
            return tooltip
                + "；标签/状态限制：无。"
                + "本行只投影当前正式契约，不会写入第二份配置。";
        }

        private static string FormatTargetIdentityType(
            GameplayTargetIdentityTypes identityType)
        {
            switch (identityType)
            {
                case GameplayTargetIdentityTypes.Character:
                    return "角色";
                case GameplayTargetIdentityTypes.WorldObject:
                    return "世界对象";
                case GameplayTargetIdentityTypes.SpatialPoint:
                    return "空间点";
                case GameplayTargetIdentityTypes.SpatialRegion:
                    return "区域";
                default:
                    return "未指定身份";
            }
        }

        private static string FormatTargetCapabilities(
            GameplayTargetCapabilities capabilities)
        {
            string value = string.Empty;
            AppendTargetCapability(
                ref value,
                capabilities,
                GameplayTargetCapabilities.Damageable,
                "可伤害");
            AppendTargetCapability(
                ref value,
                capabilities,
                GameplayTargetCapabilities.Movable,
                "可移动");
            AppendTargetCapability(
                ref value,
                capabilities,
                GameplayTargetCapabilities.Static,
                "静态");
            return string.IsNullOrEmpty(value)
                ? "无通用能力"
                : value;
        }

        private static void AppendTargetCapability(
            ref string value,
            GameplayTargetCapabilities capabilities,
            GameplayTargetCapabilities flag,
            string displayName)
        {
            if ((capabilities & flag) == 0)
                return;

            if (!string.IsNullOrEmpty(value))
                value += "/";
            value += displayName;
        }

        private static string FormatTargetRelation(
            GameplayTargetRelationFilter relationFilter)
        {
            switch (relationFilter)
            {
                case GameplayTargetRelationFilter.Self:
                    return "自身";
                case GameplayTargetRelationFilter.SameTeam:
                    return "同队";
                case GameplayTargetRelationFilter.Enemy:
                    return "敌对";
                case GameplayTargetRelationFilter.Neutral:
                    return "中立";
                case GameplayTargetRelationFilter.Any:
                    return "不限关系";
                default:
                    return "未识别关系";
            }
        }

        internal bool TryGetTriggerTargetSemanticsForTests(
            string stableId,
            out GameplayTriggerTargetSelector selector,
            out GameplayTargetClassificationComponent classification,
            out GameplayTargetRelationFilter relationFilter,
            out bool relationApplies,
            out string summary)
        {
            selector = default;
            classification = default;
            relationFilter = GameplayTargetRelationFilter.Any;
            relationApplies = false;
            summary = string.Empty;
            if (_triggerDocument == null
                || !_triggerDocument.TryFindEntry(
                    stableId,
                    out CharacterTestLevelGameplayTriggerEntry entry)
                || !TryBuildTriggerTargetChoices(
                    entry,
                    out _,
                    out _,
                    out _,
                    out selector)
                || !TryResolveTriggerTargetSemantics(
                    selector,
                    out classification,
                    out relationFilter,
                    out relationApplies,
                    out _))
            {
                return false;
            }

            summary = FormatTriggerTargetSemantics(
                classification,
                relationFilter,
                relationApplies);
            return true;
        }
    }
}
