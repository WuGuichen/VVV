using System;
using System.Collections.Generic;
using System.Text;
using MxFramework.Config.Levels;
using MxFramework.Core.Math;
using MxFramework.Editor;
using MxFramework.Gameplay;
using MxFramework.Gameplay.Triggers;
using MxFramework.Gameplay.Triggers.Config;
using UnityEngine;

namespace MxFramework.Game.CharacterTest.Editor
{
    public sealed partial class CharacterTestWorldObjectLevelEditorModule
    {
        private const string TriggerWorkspaceAllCategory = "全部";
        private const string TriggerWorkspaceRegionCategory = "区域玩法";
        private const string TriggerWorkspaceFlowCategory = "关卡流程";
        private const string TriggerWorkspaceCombatStateCategory =
            "战斗状态";

        private List<TriggerWorkspaceItem> BuildTriggerWorkspaceItems()
        {
            var items = new List<TriggerWorkspaceItem>();
            if (_triggerDocument?.Entries == null)
                return items;

            for (int i = 0; i < _triggerDocument.Entries.Count; i++)
            {
                CharacterTestLevelGameplayTriggerEntry entry =
                    _triggerDocument.Entries[i];
                if (entry == null)
                    continue;

                bool playerHpThreshold =
                    CharacterTestLevelGameplayTriggerAuthoringDocument
                        .IsPlayerHpThresholdTemplate(entry.TemplateId);
                CharacterTestLevelGameplayTriggerRegionBinding binding =
                    FindTriggerRegionBinding(entry.SourceBindingId);
                LevelWorldObjectRegionDefinition region =
                    FindTriggerWorkspaceRegion(binding?.RegionStableId);
                string regionName = playerHpThreshold
                    ? "当前玩家生命值"
                    : ResolveTriggerWorkspaceRegionName(
                        region,
                        binding?.RegionStableId);
                bool settingsResolved =
                    _triggerDocument.TryGetTemplateSettings(
                        entry,
                        out bool enabled,
                        out GameplayTriggerStateScope stateScope,
                        out GameplayTriggerPresenceLimit presenceLimit,
                        out long cooldownFrames,
                        out int maxActivations);
                bool payloadResolved =
                    _triggerDocument.TryGetTemplatePayloads(
                        entry,
                        out long payload0,
                        out long payload1,
                        out long payload2);
                string category = playerHpThreshold
                    ? TriggerWorkspaceCombatStateCategory
                    : IsStoryTriggerTemplate(entry.TemplateId)
                        ? TriggerWorkspaceFlowCategory
                        : TriggerWorkspaceRegionCategory;
                string displayName =
                    string.IsNullOrWhiteSpace(entry.DisplayName)
                        ? ResolveTriggerTemplateName(entry.TemplateId)
                        : entry.DisplayName.Trim();
                string effectSentence = BuildTriggerEffectSentence(
                    entry,
                    regionName,
                    binding,
                    settingsResolved,
                    stateScope,
                    presenceLimit,
                    cooldownFrames,
                    maxActivations,
                    payloadResolved,
                    payload0,
                    payload1);
                string conditionSummary =
                    ResolveTriggerWorkspaceConditionSummary(entry);
                string actionReference = IsStoryTriggerTemplate(
                    entry.TemplateId)
                    ? payload0.ToString()
                    : ResolveTriggerBuffLabel(payload0);
                string searchText = string.Join(
                    "\n",
                    displayName,
                    entry.StableId,
                    category,
                    regionName,
                    ResolveTriggerTemplateName(entry.TemplateId),
                    actionReference,
                    conditionSummary,
                    effectSentence);
                items.Add(
                    new TriggerWorkspaceItem(
                        entry,
                        region,
                        binding,
                        displayName,
                        regionName,
                        category,
                        settingsResolved
                        && payloadResolved
                        && (playerHpThreshold || region != null)
                        && !HasTriggerWorkspaceError(
                            i,
                            binding,
                            region),
                        settingsResolved && enabled,
                        effectSentence,
                        conditionSummary,
                        searchText));
            }

            items.Sort(CompareTriggerWorkspaceItems);
            return items;
        }

        private bool HasTriggerWorkspaceError(
            int entryIndex,
            CharacterTestLevelGameplayTriggerRegionBinding binding,
            LevelWorldObjectRegionDefinition region)
        {
            if (_report?.Issues == null)
                return false;

            string triggerRoot = "$.modules."
                + CharacterTestLevelGameplayTriggerAuthoringAdapter.ModuleId
                + ".payload";
            string entryPath = triggerRoot
                + ".entries["
                + entryIndex
                + "]";
            int bindingIndex = _triggerDocument?.RegionBindings == null
                ? -1
                : _triggerDocument.RegionBindings.IndexOf(binding);
            string bindingPath = bindingIndex < 0
                ? string.Empty
                : triggerRoot
                    + ".regionBindings["
                    + bindingIndex
                    + "]";
            int regionIndex = _document?.Regions == null
                ? -1
                : _document.Regions.IndexOf(region);
            string regionPathSuffix = regionIndex < 0
                ? string.Empty
                : ".regions[" + regionIndex + "]";

            for (int i = 0; i < _report.Issues.Count; i++)
            {
                LevelWorldObjectAuthoringIssue issue =
                    _report.Issues[i];
                if (issue == null
                    || issue.Severity
                        != LevelWorldObjectAuthoringSeverity.Error
                    || string.IsNullOrWhiteSpace(issue.Path))
                {
                    continue;
                }

                string path = issue.Path;
                if (path.StartsWith(
                        entryPath,
                        StringComparison.Ordinal)
                    || (!string.IsNullOrEmpty(bindingPath)
                        && path.StartsWith(
                            bindingPath,
                            StringComparison.Ordinal))
                    || path.StartsWith(
                        triggerRoot + ".authoring",
                        StringComparison.Ordinal)
                    || string.Equals(
                        path,
                        triggerRoot,
                        StringComparison.Ordinal)
                    || (!string.IsNullOrEmpty(regionPathSuffix)
                        && path.IndexOf(
                            regionPathSuffix,
                            StringComparison.Ordinal) >= 0))
                {
                    return true;
                }
            }

            return false;
        }

        private static int CompareTriggerWorkspaceItems(
            TriggerWorkspaceItem left,
            TriggerWorkspaceItem right)
        {
            int category = string.Compare(
                left?.Category,
                right?.Category,
                StringComparison.Ordinal);
            if (category != 0)
                return category;
            int displayName = string.Compare(
                left?.DisplayName,
                right?.DisplayName,
                StringComparison.Ordinal);
            return displayName != 0
                ? displayName
                : string.Compare(
                    left?.Entry?.StableId,
                    right?.Entry?.StableId,
                    StringComparison.Ordinal);
        }

        private LevelWorldObjectRegionDefinition FindTriggerWorkspaceRegion(
            string regionStableId)
        {
            if (_document?.Regions == null
                || string.IsNullOrWhiteSpace(regionStableId))
            {
                return null;
            }

            for (int i = 0; i < _document.Regions.Count; i++)
            {
                LevelWorldObjectRegionDefinition region =
                    _document.Regions[i];
                if (region != null
                    && string.Equals(
                        region.StableId,
                        regionStableId,
                        StringComparison.Ordinal))
                {
                    return region;
                }
            }

            return null;
        }

        private string ResolveTriggerWorkspaceRegionName(
            LevelWorldObjectRegionDefinition region,
            string fallbackStableId)
        {
            if (region != null
                && ManagedSceneRoot != null
                && CharacterTestWorldObjectAuthoringAdapter
                    .TryResolveTransform(
                        ManagedSceneRoot,
                        region.TransformRef,
                        out Transform transform)
                && transform != null
                && !string.IsNullOrWhiteSpace(transform.name))
            {
                return transform.name;
            }

            string stableId = region?.StableId;
            if (string.IsNullOrWhiteSpace(stableId))
                stableId = fallbackStableId;
            return string.IsNullOrWhiteSpace(stableId)
                ? "[缺失区域]"
                : stableId.Trim();
        }

        private string BuildTriggerEffectSentence(
            CharacterTestLevelGameplayTriggerEntry entry,
            string regionName,
            CharacterTestLevelGameplayTriggerRegionBinding binding,
            bool settingsResolved,
            GameplayTriggerStateScope stateScope,
            GameplayTriggerPresenceLimit presenceLimit,
            long cooldownFrames,
            int maxActivations,
            bool payloadResolved,
            long payload0,
            long payload1)
        {
            if (entry == null)
                return "效果：[缺失触发器配置]";

            string subject = FormatTriggerWorkspaceSubject(
                binding?.SubjectFilter);
            string source = string.IsNullOrWhiteSpace(regionName)
                ? "[缺失区域]"
                : "“" + regionName.Trim() + "”";
            var sentence = new StringBuilder(160);
            sentence.Append("效果：当");
            if (CharacterTestLevelGameplayTriggerAuthoringDocument
                    .IsPlayerHpThresholdTemplate(entry.TemplateId))
            {
                if (_triggerDocument.TryGetPlayerHpThresholds(
                        entry,
                        out int enterThreshold,
                        out int exitThreshold))
                {
                    sentence.Append("当前玩家生命值降至 ");
                    sentence.Append(enterThreshold);
                    sentence.Append(" 或以下时，触发剧情 ");
                    sentence.Append(payloadResolved
                        ? "“"
                            + ResolveTriggerStoryReferenceLabel(payload0)
                            + "”"
                        : "[缺失剧情参数]");
                    sentence.Append("；恢复到 ");
                    sentence.Append(exitThreshold);
                    sentence.Append(" 以上后可再次触发");
                }
                else
                {
                    sentence.Append(
                        "[无效的当前玩家生命阈值]时执行[缺失动作]");
                }
            }
            else
            {
                sentence.Append(subject);
                if (IsStoryTriggerTemplate(entry.TemplateId))
                {
                    sentence.Append("进入");
                    sentence.Append(source);
                    sentence.Append("时，触发剧情 ");
                    sentence.Append(payloadResolved
                        ? "“"
                            + ResolveTriggerStoryReferenceLabel(payload0)
                            + "”"
                        : "[缺失剧情参数]");
                }
                else if (string.Equals(
                         entry.TemplateId,
                         CharacterTestLevelGameplayTriggerAuthoringDocument
                             .GameplayApplyRemoveBuffPresenceTemplateId,
                         StringComparison.Ordinal))
                {
                    sentence.Append("进入或停留在");
                    sentence.Append(source);
                    sentence.Append("时，为其施加");
                    sentence.Append(FormatTriggerWorkspaceBuff(payloadResolved, payload0));
                    AppendTriggerWorkspaceAttribution(sentence, payload1);
                    sentence.Append("；离开时移除");
                }
                else if (string.Equals(
                         entry.TemplateId,
                         CharacterTestLevelGameplayTriggerAuthoringDocument
                             .GameplayApplyBuffEnterOnceTemplateId,
                         StringComparison.Ordinal))
                {
                    sentence.Append("进入");
                    sentence.Append(source);
                    sentence.Append("时，为其施加");
                    sentence.Append(FormatTriggerWorkspaceBuff(payloadResolved, payload0));
                    AppendTriggerWorkspaceAttribution(sentence, payload1);
                }
                else
                {
                    sentence.Append("触发");
                    sentence.Append(source);
                    sentence.Append("时，执行[未知动作：");
                    sentence.Append(entry.TemplateId);
                    sentence.Append(']');
                }
            }

            string conditionSummary =
                ResolveTriggerWorkspaceConditionSummary(entry);
            if (!string.IsNullOrWhiteSpace(conditionSummary))
            {
                sentence.Append("；仅当");
                sentence.Append(conditionSummary);
                sentence.Append("时生效");
            }

            if (CharacterTestLevelGameplayTriggerAuthoringDocument
                    .IsPlayerHpThresholdTemplate(entry.TemplateId))
            {
                AppendTriggerWorkspaceThresholdPolicy(
                    sentence,
                    settingsResolved,
                    cooldownFrames,
                    maxActivations);
            }
            else
            {
                AppendTriggerWorkspacePolicy(
                    sentence,
                    settingsResolved,
                    stateScope,
                    presenceLimit,
                    cooldownFrames,
                    maxActivations);
            }
            sentence.Append('。');
            return sentence.ToString();
        }

        private static string FormatTriggerWorkspaceSubject(
            CharacterTestLevelGameplayTriggerSubjectFilter filter)
        {
            if (filter == null)
                return "[缺失触发对象]";

            var text = new StringBuilder(64);
            switch (filter.RelationFilter)
            {
                case GameplayTargetRelationFilter.SameTeam:
                    text.Append("与队伍 ");
                    text.Append(filter.ReferenceTeamId);
                    text.Append(" 同队的");
                    break;
                case GameplayTargetRelationFilter.Enemy:
                    text.Append("与队伍 ");
                    text.Append(filter.ReferenceTeamId);
                    text.Append(" 敌对的");
                    break;
                case GameplayTargetRelationFilter.Neutral:
                    text.Append("与队伍 ");
                    text.Append(filter.ReferenceTeamId);
                    text.Append(" 中立的");
                    break;
            }

            var requirements = new List<string>(3);
            if (filter.RequireAlive)
                requirements.Add("存活");
            if ((filter.RequiredCapabilities
                    & GameplayTargetCapabilities.Damageable) != 0)
            {
                requirements.Add("可伤害");
            }
            if ((filter.RequiredCapabilities
                    & GameplayTargetCapabilities.Movable) != 0)
            {
                requirements.Add("可移动");
            }
            if (requirements.Count > 0)
            {
                text.Append(string.Join("、", requirements));
                text.Append("的");
            }

            text.Append(
                (filter.AcceptedIdentityTypes
                    & GameplayTargetIdentityTypes.Character) != 0
                    ? "角色"
                    : "对象");
            return text.ToString();
        }

        private static string FormatTriggerWorkspaceBuff(
            bool payloadResolved,
            long buffId)
        {
            if (!payloadResolved || buffId <= 0L)
                return "[缺失 Buff：" + buffId + "]";
            return "“" + ResolveTriggerBuffLabel(buffId) + "”";
        }

        private static void AppendTriggerWorkspaceAttribution(
            StringBuilder sentence,
            long attributionId)
        {
            if (sentence == null || attributionId <= 0L)
                return;
            sentence.Append("，效果归因于“");
            sentence.Append(
                ResolveTriggerWorkspaceAttributionLabel(attributionId));
            sentence.Append('”');
        }

        private static string ResolveTriggerWorkspaceAttributionLabel(
            long attributionId)
        {
            if (TryBuildTriggerBuffAttributionChoices(
                    out List<MxAuthoringReferenceOption<long>> choices,
                    out _))
            {
                for (int i = 0; i < choices.Count; i++)
                {
                    if (choices[i].Value == attributionId)
                        return choices[i].DisplayName;
                }
            }

            return "未登记来源 " + attributionId;
        }

        private static void AppendTriggerWorkspacePolicy(
            StringBuilder sentence,
            bool settingsResolved,
            GameplayTriggerStateScope stateScope,
            GameplayTriggerPresenceLimit presenceLimit,
            long cooldownFrames,
            int maxActivations)
        {
            if (sentence == null)
                return;
            if (!settingsResolved)
            {
                sentence.Append("；[缺失重复规则]");
                return;
            }

            var policies = new List<string>(3);
            if (stateScope == GameplayTriggerStateScope.PerSubject)
                policies.Add("每个对象独立");
            if (presenceLimit
                == GameplayTriggerPresenceLimit.OncePerPresence)
            {
                policies.Add("每次进入期间最多一次");
            }
            if (cooldownFrames > 0L)
                policies.Add(FormatTriggerWorkspaceCooldown(cooldownFrames));
            if (maxActivations > 0)
                policies.Add("最多触发 " + maxActivations + " 次");
            if (policies.Count == 0)
                return;

            sentence.Append("；");
            sentence.Append(string.Join("，", policies));
        }

        private static void AppendTriggerWorkspaceThresholdPolicy(
            StringBuilder sentence,
            bool settingsResolved,
            long cooldownFrames,
            int maxActivations)
        {
            if (sentence == null)
                return;
            if (!settingsResolved)
            {
                sentence.Append("；[缺失重复规则]");
                return;
            }

            if (cooldownFrames > 0L)
            {
                sentence.Append("；冷却 ");
                sentence.Append(cooldownFrames);
                sentence.Append(" 帧");
            }
            if (maxActivations > 0)
            {
                sentence.Append("；最多触发 ");
                sentence.Append(maxActivations);
                sentence.Append(" 次");
            }
        }

        private static string FormatTriggerWorkspaceCooldown(
            long cooldownFrames)
        {
            int tickRate = CharacterTestDeterministicUnits.DefaultTickRate;
            if (tickRate > 0 && cooldownFrames % tickRate == 0L)
            {
                return "冷却 "
                    + (cooldownFrames / tickRate)
                    + " 秒";
            }

            return "冷却 " + cooldownFrames + " 帧";
        }

        private string ResolveTriggerWorkspaceConditionSummary(
            CharacterTestLevelGameplayTriggerEntry entry)
        {
            GameplayTriggerConditionAuthoring[] conditions =
                _triggerDocument?.Authoring?.Conditions;
            if (entry == null || conditions == null)
                return "[缺失条件]";

            for (int i = 0; i < conditions.Length; i++)
            {
                GameplayTriggerConditionAuthoring condition =
                    conditions[i];
                if (condition?.ConditionId != entry.ConditionId)
                    continue;

                GameplayTriggerConditionNodeAuthoring root =
                    condition.Root;
                if (root == null)
                    return "[缺失条件根]";
                if (root.Operation
                        == GameplayTriggerConditionOperation.PushTrue
                    && (root.Children == null
                        || root.Children.Length == 0))
                {
                    return string.Empty;
                }

                var text = new StringBuilder(96);
                AppendTriggerWorkspaceConditionNode(
                    text,
                    root,
                    CharacterTestLevelGameplayTriggerAuthoringDocument
                        .IsPlayerHpThresholdTemplate(entry.TemplateId));
                return text.ToString();
            }

            return "[缺失条件 " + entry.ConditionId + "]";
        }

        private static void AppendTriggerWorkspaceConditionNode(
            StringBuilder text,
            GameplayTriggerConditionNodeAuthoring node,
            bool healthScalar)
        {
            if (text == null)
                return;
            if (node == null)
            {
                text.Append("[缺失节点]");
                return;
            }

            switch (node.Operation)
            {
                case GameplayTriggerConditionOperation.PushTrue:
                    text.Append("始终成立");
                    return;
                case GameplayTriggerConditionOperation.PushFalse:
                    text.Append("始终不成立");
                    return;
                case GameplayTriggerConditionOperation.HasSubject:
                    text.Append(
                        node.Operand != 0L
                            ? "存在触发对象"
                            : "不存在触发对象");
                    return;
                case GameplayTriggerConditionOperation.HasInstigator:
                    text.Append(
                        node.Operand != 0L
                            ? "存在触发来源"
                            : "不存在触发来源");
                    return;
                case GameplayTriggerConditionOperation.Not:
                    text.Append("非(");
                    AppendTriggerWorkspaceConditionChild(
                        text,
                        node,
                        0,
                        healthScalar);
                    text.Append(')');
                    return;
                case GameplayTriggerConditionOperation.All:
                    AppendTriggerWorkspaceConditionGroup(
                        text,
                        "全部满足",
                        node,
                        healthScalar);
                    return;
                case GameplayTriggerConditionOperation.Any:
                    AppendTriggerWorkspaceConditionGroup(
                        text,
                        "任一满足",
                        node,
                        healthScalar);
                    return;
                default:
                    AppendTriggerWorkspaceConditionLeaf(
                        text,
                        node,
                        healthScalar);
                    return;
            }
        }

        private static void AppendTriggerWorkspaceConditionGroup(
            StringBuilder text,
            string label,
            GameplayTriggerConditionNodeAuthoring node,
            bool healthScalar)
        {
            text.Append(label);
            text.Append('(');
            GameplayTriggerConditionNodeAuthoring[] children =
                node.Children;
            for (int i = 0; i < (children?.Length ?? 0); i++)
            {
                if (i > 0)
                    text.Append("，");
                AppendTriggerWorkspaceConditionNode(
                    text,
                    children[i],
                    healthScalar);
            }
            text.Append(')');
        }

        private static void AppendTriggerWorkspaceConditionChild(
            StringBuilder text,
            GameplayTriggerConditionNodeAuthoring node,
            int index,
            bool healthScalar)
        {
            GameplayTriggerConditionNodeAuthoring[] children =
                node?.Children;
            if (children == null
                || index < 0
                || index >= children.Length)
            {
                text.Append("[缺失子条件]");
                return;
            }
            AppendTriggerWorkspaceConditionNode(
                text,
                children[index],
                healthScalar);
        }

        private static void AppendTriggerWorkspaceConditionLeaf(
            StringBuilder text,
            GameplayTriggerConditionNodeAuthoring node,
            bool healthScalar)
        {
            text.Append(
                ResolveTriggerWorkspaceConditionOperand(
                    node.Operation,
                    healthScalar));
            text.Append(
                ResolveTriggerWorkspaceConditionComparison(
                    node.Comparison));
            if (healthScalar
                && (node.Operation
                        == GameplayTriggerConditionOperation.PreviousScalar
                    || node.Operation
                        == GameplayTriggerConditionOperation.CurrentScalar))
            {
                text.Append(Fix64.FromRaw(node.Operand));
            }
            else
            {
                text.Append(node.Operand);
            }
        }

        private static string ResolveTriggerWorkspaceConditionOperand(
            GameplayTriggerConditionOperation operation,
            bool healthScalar)
        {
            switch (operation)
            {
                case GameplayTriggerConditionOperation.OccurrenceMask:
                    return "事件掩码";
                case GameplayTriggerConditionOperation.SignalIdEquals:
                    return "Signal ID";
                case GameplayTriggerConditionOperation.PreviousScalar:
                    return healthScalar
                        ? "触发前生命值"
                        : "先前标量";
                case GameplayTriggerConditionOperation.CurrentScalar:
                    return healthScalar
                        ? "当前生命值"
                        : "当前标量";
                case GameplayTriggerConditionOperation.Payload0:
                    return "参数 0";
                case GameplayTriggerConditionOperation.Payload1:
                    return "参数 1";
                case GameplayTriggerConditionOperation.Payload2:
                    return "参数 2";
                default:
                    return "[未知条件 " + (int)operation + "]";
            }
        }

        private static string
            ResolveTriggerWorkspaceConditionComparison(
                GameplayTriggerComparison comparison)
        {
            switch (comparison)
            {
                case GameplayTriggerComparison.Equal:
                    return " = ";
                case GameplayTriggerComparison.NotEqual:
                    return " ≠ ";
                case GameplayTriggerComparison.Less:
                    return " < ";
                case GameplayTriggerComparison.LessOrEqual:
                    return " ≤ ";
                case GameplayTriggerComparison.Greater:
                    return " > ";
                case GameplayTriggerComparison.GreaterOrEqual:
                    return " ≥ ";
                default:
                    return " ? ";
            }
        }

        private static bool IsStoryTriggerTemplate(string templateId)
        {
            return string.Equals(
                templateId,
                CharacterTestLevelGameplayTriggerAuthoringDocument
                    .StoryRaiseTriggerEnterOnceTemplateId,
                StringComparison.Ordinal)
                || CharacterTestLevelGameplayTriggerAuthoringDocument
                    .IsPlayerHpThresholdTemplate(templateId);
        }

        internal string GetTriggerEffectSentenceForTests(string stableId)
        {
            List<TriggerWorkspaceItem> items =
                BuildTriggerWorkspaceItems();
            for (int i = 0; i < items.Count; i++)
            {
                if (string.Equals(
                        items[i].Entry.StableId,
                        stableId,
                        StringComparison.Ordinal))
                {
                    return items[i].EffectSentence;
                }
            }

            return string.Empty;
        }

        private sealed class TriggerWorkspaceItem
        {
            internal TriggerWorkspaceItem(
                CharacterTestLevelGameplayTriggerEntry entry,
                LevelWorldObjectRegionDefinition region,
                CharacterTestLevelGameplayTriggerRegionBinding binding,
                string displayName,
                string regionName,
                string category,
                bool isValid,
                bool isEnabled,
                string effectSentence,
                string conditionSummary,
                string searchText)
            {
                Entry = entry;
                Region = region;
                Binding = binding;
                DisplayName = displayName;
                RegionName = regionName;
                Category = category;
                IsValid = isValid;
                IsEnabled = isEnabled;
                EffectSentence = effectSentence;
                ConditionSummary = conditionSummary ?? string.Empty;
                SearchText = searchText;
            }

            internal CharacterTestLevelGameplayTriggerEntry Entry { get; }

            internal LevelWorldObjectRegionDefinition Region { get; }

            internal CharacterTestLevelGameplayTriggerRegionBinding Binding
            {
                get;
            }

            internal string DisplayName { get; }

            internal string RegionName { get; }

            internal string Category { get; }

            internal bool IsValid { get; }

            internal bool IsEnabled { get; }

            internal string EffectSentence { get; }

            internal string ConditionSummary { get; }

            internal string SearchText { get; }
        }
    }
}
