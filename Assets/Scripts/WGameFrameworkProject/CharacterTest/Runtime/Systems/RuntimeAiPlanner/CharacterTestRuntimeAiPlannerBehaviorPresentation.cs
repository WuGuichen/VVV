using System;
using System.Collections.Generic;
using System.Globalization;
using MxFramework.CharacterAction;
using MxFramework.Gameplay;

namespace MxFramework.Game.CharacterTest
{
    internal enum CharacterTestRuntimeAiPlannerPlayerBehaviorStrategyKind
    {
        Assault = 0,
        Kite = 1,
        Guard = 2,
        Control = 3,
        Survival = 4,
        Coordination = 5
    }

    internal sealed class CharacterTestRuntimeAiPlannerPlayerBehaviorStrategyDefinition
    {
        private readonly string[] _tags;

        public CharacterTestRuntimeAiPlannerPlayerBehaviorStrategyDefinition(
            CharacterTestRuntimeAiPlannerPlayerBehaviorStrategyKind kind,
            string displayName,
            string shortDescription,
            string primaryActionSummary,
            string suitableScenario,
            string riskText,
            string[] tags)
        {
            Kind = kind;
            DisplayName = displayName ?? string.Empty;
            ShortDescription = shortDescription ?? string.Empty;
            PrimaryActionSummary = primaryActionSummary ?? string.Empty;
            SuitableScenario = suitableScenario ?? string.Empty;
            RiskText = riskText ?? string.Empty;
            _tags = Copy(tags);
        }

        public CharacterTestRuntimeAiPlannerPlayerBehaviorStrategyKind Kind { get; }
        public string DisplayName { get; }
        public string ShortDescription { get; }
        public string PrimaryActionSummary { get; }
        public string SuitableScenario { get; }
        public string RiskText { get; }
        public IReadOnlyList<string> Tags => _tags;

        private static string[] Copy(string[] source)
        {
            if (source == null || source.Length == 0)
                return Array.Empty<string>();

            var copy = new string[source.Length];
            Array.Copy(source, copy, source.Length);
            return copy;
        }
    }

    internal readonly struct CharacterTestRuntimeAiPlannerPlayerBehaviorPresentationViewModel
    {
        public CharacterTestRuntimeAiPlannerPlayerBehaviorPresentationViewModel(
            int loadoutId,
            int variantId,
            string strategyName,
            string shortDescription,
            string primaryActionSummary,
            string suitableScenario,
            string riskText,
            string combatStatusLabel,
            string[] tags)
        {
            LoadoutId = loadoutId;
            VariantId = variantId;
            StrategyName = strategyName ?? string.Empty;
            ShortDescription = shortDescription ?? string.Empty;
            PrimaryActionSummary = primaryActionSummary ?? string.Empty;
            SuitableScenario = suitableScenario ?? string.Empty;
            RiskText = riskText ?? string.Empty;
            CombatStatusLabel = combatStatusLabel ?? string.Empty;
            Tags = Copy(tags);
        }

        public int LoadoutId { get; }
        public int VariantId { get; }
        public string StrategyName { get; }
        public string ShortDescription { get; }
        public string PrimaryActionSummary { get; }
        public string SuitableScenario { get; }
        public string RiskText { get; }
        public string CombatStatusLabel { get; }
        public IReadOnlyList<string> Tags { get; }

        public string StrategyCardText
        {
            get
            {
                return StrategyName
                    + "\n"
                    + ShortDescription
                    + "\n动作倾向: "
                    + PrimaryActionSummary
                    + "\n适合: "
                    + SuitableScenario
                    + "\n限制: "
                    + RiskText;
            }
        }

        public bool ContainsDebugOnlyText
        {
            get
            {
                return ContainsDebugToken(StrategyCardText)
                    || ContainsDebugToken(CombatStatusLabel);
            }
        }

        private static bool ContainsDebugToken(string text)
        {
            if (string.IsNullOrEmpty(text))
                return false;

            return text.IndexOf("module", StringComparison.OrdinalIgnoreCase) >= 0
                || text.IndexOf("fact", StringComparison.OrdinalIgnoreCase) >= 0
                || text.IndexOf("score", StringComparison.OrdinalIgnoreCase) >= 0
                || text.IndexOf("cost", StringComparison.OrdinalIgnoreCase) >= 0
                || text.IndexOf("profileSlot", StringComparison.OrdinalIgnoreCase) >= 0
                || text.IndexOf("catalogHash", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static string[] Copy(string[] source)
        {
            if (source == null || source.Length == 0)
                return Array.Empty<string>();

            var copy = new string[source.Length];
            Array.Copy(source, copy, source.Length);
            return copy;
        }
    }

    internal static class CharacterTestRuntimeAiPlannerPlayerBehaviorPresentationCatalog
    {
        private static readonly CharacterTestRuntimeAiPlannerPlayerBehaviorStrategyDefinition[] Definitions =
        {
            new CharacterTestRuntimeAiPlannerPlayerBehaviorStrategyDefinition(
                CharacterTestRuntimeAiPlannerPlayerBehaviorStrategyKind.Assault,
                "突击",
                "主动接近最近威胁，抓住进入攻击范围后的输出窗口。",
                "接近、普通攻击、短距离绕行",
                "默认近战、先锋、低门槛随从",
                "被远程拉扯时需要额外位移或控场支持。",
                new[] { "近战", "主动", "直接" }),
            new CharacterTestRuntimeAiPlannerPlayerBehaviorStrategyDefinition(
                CharacterTestRuntimeAiPlannerPlayerBehaviorStrategyKind.Kite,
                "风筝",
                "保持距离并优先使用远程或技能输出。",
                "保持距离、寻找视线、技能释放",
                "远程单位、脆皮输出、开阔场地",
                "狭窄地形和高机动敌人会降低稳定性。",
                new[] { "远程", "距离", "技能" }),
            new CharacterTestRuntimeAiPlannerPlayerBehaviorStrategyDefinition(
                CharacterTestRuntimeAiPlannerPlayerBehaviorStrategyKind.Guard,
                "守卫",
                "围绕指定区域或友方单位保持防守姿态。",
                "守位、拦截、低风险输出",
                "护卫、据点、防线单位",
                "目标切换保守，追击能力弱。",
                new[] { "防守", "护卫", "稳健" }),
            new CharacterTestRuntimeAiPlannerPlayerBehaviorStrategyDefinition(
                CharacterTestRuntimeAiPlannerPlayerBehaviorStrategyKind.Control,
                "控场",
                "优先创造限制区域、打断窗口或群体压力。",
                "技能施放、寻找视线、控制区域",
                "群怪、关口、需要限制移动的战斗",
                "依赖技能槽和目标条件，空窗期需要队友补位。",
                new[] { "控制", "区域", "技能" }),
            new CharacterTestRuntimeAiPlannerPlayerBehaviorStrategyDefinition(
                CharacterTestRuntimeAiPlannerPlayerBehaviorStrategyKind.Survival,
                "保命",
                "危险优先级高于输出，优先撤离风险区域。",
                "撤离危险、保持距离、等待恢复",
                "低血量单位、精英敌人二阶段、危险地形",
                "输出节奏较慢，可能错过短攻击窗口。",
                new[] { "生存", "回避", "保守" }),
            new CharacterTestRuntimeAiPlannerPlayerBehaviorStrategyDefinition(
                CharacterTestRuntimeAiPlannerPlayerBehaviorStrategyKind.Coordination,
                "协同",
                "围绕队伍节奏选择补位、集火或技能支援。",
                "补位、集火、支援技能",
                "多单位队伍、随从协作、战术指令",
                "需要未来队伍/随从 authority 提供协同目标。",
                new[] { "队伍", "支援", "协作" })
        };

        public static IReadOnlyList<CharacterTestRuntimeAiPlannerPlayerBehaviorStrategyDefinition> StrategyDefinitions =>
            Definitions;

        public static bool TryGetDefinition(
            CharacterTestRuntimeAiPlannerPlayerBehaviorStrategyKind kind,
            out CharacterTestRuntimeAiPlannerPlayerBehaviorStrategyDefinition definition)
        {
            for (int i = 0; i < Definitions.Length; i++)
            {
                if (Definitions[i].Kind != kind)
                    continue;

                definition = Definitions[i];
                return true;
            }

            definition = null;
            return false;
        }

        public static CharacterTestRuntimeAiPlannerPlayerBehaviorPresentationViewModel CreateLoadoutCard(
            CharacterTestRuntimeAiPlannerBehaviorLoadoutDefinition loadout,
            CharacterTestRuntimeAiPlannerBehaviorLoadoutVariantDefinition variant)
        {
            CharacterTestRuntimeAiPlannerPlayerBehaviorStrategyDefinition definition =
                ResolveDefinition(loadout);
            string description = !string.IsNullOrWhiteSpace(loadout?.PlayerShortDescription)
                ? loadout.PlayerShortDescription
                : definition.ShortDescription;
            string actionSummary = !string.IsNullOrWhiteSpace(loadout?.PlayerPrimaryActionSummary)
                ? loadout.PlayerPrimaryActionSummary
                : definition.PrimaryActionSummary;
            string scenario = !string.IsNullOrWhiteSpace(loadout?.PlayerSuitableScenario)
                ? loadout.PlayerSuitableScenario
                : definition.SuitableScenario;
            string risk = !string.IsNullOrWhiteSpace(loadout?.PlayerRiskText)
                ? loadout.PlayerRiskText
                : definition.RiskText;

            return new CharacterTestRuntimeAiPlannerPlayerBehaviorPresentationViewModel(
                loadout != null ? loadout.LoadoutId : 0,
                variant != null ? variant.VariantId : 0,
                definition.DisplayName,
                description,
                actionSummary,
                scenario,
                risk,
                "待命",
                CopyTags(definition.Tags));
        }

        public static CharacterTestRuntimeAiPlannerPlayerBehaviorPresentationViewModel CreateCombatStatus(
            CharacterTestRuntimeAiPlannerBehaviorLoadoutDefinition loadout,
            CharacterTestRuntimeAiPlannerBehaviorLoadoutVariantDefinition variant,
            CharacterTestRuntimeAiPlannerAgentSnapshot snapshot)
        {
            CharacterTestRuntimeAiPlannerPlayerBehaviorPresentationViewModel card =
                CreateLoadoutCard(loadout, variant);
            return new CharacterTestRuntimeAiPlannerPlayerBehaviorPresentationViewModel(
                card.LoadoutId,
                card.VariantId,
                card.StrategyName,
                card.ShortDescription,
                card.PrimaryActionSummary,
                card.SuitableScenario,
                card.RiskText,
                BuildCombatStatusLabel(snapshot),
                CopyTags(card.Tags));
        }

        public static string BuildCombatStatusLabel(CharacterTestRuntimeAiPlannerAgentSnapshot snapshot)
        {
            if (!string.IsNullOrWhiteSpace(snapshot.RejectReason))
                return "动作受阻";
            if (!snapshot.TargetExists)
                return "目标丢失";
            if (snapshot.TargetDead)
                return "目标已死亡";
            if (snapshot.EnvironmentNearHazard)
                return "撤离危险";
            if (!snapshot.MovePathAvailable)
                return "路径受阻";
            if (snapshot.TargetLineOfSightBlocked || !snapshot.TargetVisible)
                return "寻找视线";

            switch (snapshot.SelectedActionKind)
            {
                case CharacterTestRuntimeAiPlannerActionKind.CastAbilitySlot:
                    return "准备施法";
                case CharacterTestRuntimeAiPlannerActionKind.Retreat:
                    return "保持距离";
                case CharacterTestRuntimeAiPlannerActionKind.Strafe:
                    return "绕行中";
                case CharacterTestRuntimeAiPlannerActionKind.Dodge:
                    return "闪避中";
                case CharacterTestRuntimeAiPlannerActionKind.MoveToTarget:
                case CharacterTestRuntimeAiPlannerActionKind.PrimaryAttack:
                    return "突击中";
                default:
                    return "待命";
            }
        }

        private static CharacterTestRuntimeAiPlannerPlayerBehaviorStrategyDefinition ResolveDefinition(
            CharacterTestRuntimeAiPlannerBehaviorLoadoutDefinition loadout)
        {
            CharacterTestRuntimeAiPlannerPlayerBehaviorStrategyKind kind =
                loadout != null ? loadout.PlayerStrategyKind : CharacterTestRuntimeAiPlannerPlayerBehaviorStrategyKind.Assault;
            if (TryGetDefinition(kind, out CharacterTestRuntimeAiPlannerPlayerBehaviorStrategyDefinition definition))
                return definition;

            return Definitions[0];
        }

        private static string[] CopyTags(IReadOnlyList<string> tags)
        {
            if (tags == null || tags.Count == 0)
                return Array.Empty<string>();

            var copy = new string[tags.Count];
            for (int i = 0; i < tags.Count; i++)
                copy[i] = tags[i] ?? string.Empty;
            return copy;
        }
    }

    internal readonly struct CharacterTestRuntimeAiPlannerBehaviorActionChainPreviewItem
    {
        public CharacterTestRuntimeAiPlannerBehaviorActionChainPreviewItem(
            int actionId,
            CharacterTestRuntimeAiPlannerActionKind actionKind,
            int abilitySlot,
            int abilityId,
            string abilityName,
            string abilityStableId,
            string actionIntentId,
            CharacterActionId characterActionId,
            string characterActionName,
            bool hasCharacterAction,
            bool hasGameplayRequest,
            bool hasHitTrace,
            bool hasPresentationEvent,
            bool hasVisualCue,
            string keyFramesText,
            string warningText,
            string playerSummary,
            string debugSummary)
        {
            ActionId = actionId;
            ActionKind = actionKind;
            AbilitySlot = abilitySlot;
            AbilityId = abilityId;
            AbilityName = abilityName ?? string.Empty;
            AbilityStableId = abilityStableId ?? string.Empty;
            ActionIntentId = actionIntentId ?? string.Empty;
            CharacterActionId = characterActionId;
            CharacterActionName = characterActionName ?? string.Empty;
            HasCharacterAction = hasCharacterAction;
            HasGameplayRequest = hasGameplayRequest;
            HasHitTrace = hasHitTrace;
            HasPresentationEvent = hasPresentationEvent;
            HasVisualCue = hasVisualCue;
            KeyFramesText = keyFramesText ?? string.Empty;
            WarningText = warningText ?? string.Empty;
            PlayerSummary = playerSummary ?? string.Empty;
            DebugSummary = debugSummary ?? string.Empty;
        }

        public int ActionId { get; }
        public CharacterTestRuntimeAiPlannerActionKind ActionKind { get; }
        public int AbilitySlot { get; }
        public int AbilityId { get; }
        public string AbilityName { get; }
        public string AbilityStableId { get; }
        public string ActionIntentId { get; }
        public MxFramework.CharacterAction.CharacterActionId CharacterActionId { get; }
        public string CharacterActionName { get; }
        public bool HasCharacterAction { get; }
        public bool HasGameplayRequest { get; }
        public bool HasHitTrace { get; }
        public bool HasPresentationEvent { get; }
        public bool HasVisualCue { get; }
        public string KeyFramesText { get; }
        public string WarningText { get; }
        public string PlayerSummary { get; }
        public string DebugSummary { get; }
        public bool HasWarning => !string.IsNullOrWhiteSpace(WarningText);
    }

    internal readonly struct CharacterTestRuntimeAiPlannerBehaviorActionChainPreview
    {
        private readonly CharacterTestRuntimeAiPlannerBehaviorActionChainPreviewItem[] _items;

        public CharacterTestRuntimeAiPlannerBehaviorActionChainPreview(
            CharacterTestRuntimeAiPlannerBehaviorActionChainPreviewItem[] items,
            string summaryText)
        {
            _items = items ?? Array.Empty<CharacterTestRuntimeAiPlannerBehaviorActionChainPreviewItem>();
            SummaryText = summaryText ?? string.Empty;
        }

        public IReadOnlyList<CharacterTestRuntimeAiPlannerBehaviorActionChainPreviewItem> Items => _items;
        public string SummaryText { get; }

        public int WarningCount
        {
            get
            {
                int count = 0;
                for (int i = 0; i < _items.Length; i++)
                {
                    if (_items[i].HasWarning)
                        count++;
                }

                return count;
            }
        }

        public bool TryFindByActionId(
            int actionId,
            out CharacterTestRuntimeAiPlannerBehaviorActionChainPreviewItem item)
        {
            for (int i = 0; i < _items.Length; i++)
            {
                if (_items[i].ActionId == actionId)
                {
                    item = _items[i];
                    return true;
                }
            }

            item = default;
            return false;
        }

        public bool TryFindByActionKind(
            CharacterTestRuntimeAiPlannerActionKind actionKind,
            out CharacterTestRuntimeAiPlannerBehaviorActionChainPreviewItem item)
        {
            for (int i = 0; i < _items.Length; i++)
            {
                if (_items[i].ActionKind == actionKind)
                {
                    item = _items[i];
                    return true;
                }
            }

            item = default;
            return false;
        }
    }

    internal static class CharacterTestRuntimeAiPlannerBehaviorActionChainPreviewBuilder
    {
        public static CharacterTestRuntimeAiPlannerBehaviorActionChainPreview Build(
            CharacterTestRuntimeAiPlannerProfile profile,
            CharacterTestAbilityHotbarRuntime hotbar,
            CharacterTestCharacterActionRuntime actionRuntime)
        {
            if (profile == null || profile.Actions.Count == 0)
            {
                return new CharacterTestRuntimeAiPlannerBehaviorActionChainPreview(
                    Array.Empty<CharacterTestRuntimeAiPlannerBehaviorActionChainPreviewItem>(),
                    "无 Runtime AI Planner action。");
            }

            var items = new List<CharacterTestRuntimeAiPlannerBehaviorActionChainPreviewItem>(profile.Actions.Count);
            for (int i = 0; i < profile.Actions.Count; i++)
                items.Add(Build(profile.Actions[i], hotbar, actionRuntime));

            return new CharacterTestRuntimeAiPlannerBehaviorActionChainPreview(
                items.ToArray(),
                BuildSummary(items));
        }

        public static CharacterTestRuntimeAiPlannerBehaviorActionChainPreviewItem BuildSelectedAction(
            CharacterTestRuntimeAiPlannerAgentSnapshot snapshot,
            CharacterTestAbilityHotbarRuntime hotbar,
            CharacterTestCharacterActionRuntime actionRuntime)
        {
            var action = new CharacterTestRuntimeAiPlannerActionDefinition(
                snapshot.SelectedActionId,
                snapshot.ActiveStateId,
                snapshot.SelectedActionKind,
                snapshot.SelectedUtilityScore,
                snapshot.SelectedActionCost,
                snapshot.SelectedActionName,
                abilitySlot: snapshot.SelectedAbilitySlot);
            return Build(action, hotbar, actionRuntime);
        }

        public static CharacterTestRuntimeAiPlannerBehaviorActionChainPreviewItem Build(
            CharacterTestRuntimeAiPlannerActionDefinition action,
            CharacterTestAbilityHotbarRuntime hotbar,
            CharacterTestCharacterActionRuntime actionRuntime)
        {
            if (action == null)
                return CreateNonTimelineItem(0, CharacterTestRuntimeAiPlannerActionKind.Wait, "无动作。");

            switch (action.Kind)
            {
                case CharacterTestRuntimeAiPlannerActionKind.PrimaryAttack:
                    return BuildCharacterActionItem(
                        action,
                        CharacterTestCharacterActionConfigSource.LightAttackIntentLabel,
                        CharacterTestCharacterActionConfigSource.LightAttackActionId,
                        abilitySlot: 0,
                        ability: null,
                        actionRuntime,
                        requireHitTrace: true);
                case CharacterTestRuntimeAiPlannerActionKind.CastAbilitySlot:
                    return BuildAbilitySlotItem(action, hotbar, actionRuntime);
                case CharacterTestRuntimeAiPlannerActionKind.MoveToTarget:
                    return CreateNonTimelineItem(action.ActionId, action.Kind, "接近目标：移动命令，不触发 CharacterAction timeline。");
                case CharacterTestRuntimeAiPlannerActionKind.Retreat:
                    return CreateNonTimelineItem(action.ActionId, action.Kind, "保持距离：移动命令，不触发 CharacterAction timeline。");
                case CharacterTestRuntimeAiPlannerActionKind.Strafe:
                    return CreateNonTimelineItem(action.ActionId, action.Kind, "绕行：移动命令，不触发 CharacterAction timeline。");
                case CharacterTestRuntimeAiPlannerActionKind.Dodge:
                    return BuildCharacterActionItem(
                        action,
                        "intent.dodge",
                        CharacterTestCharacterActionConfigSource.DefaultEvadeActionId,
                        abilitySlot: 0,
                        ability: null,
                        actionRuntime,
                        requireHitTrace: false);
                default:
                    return CreateNonTimelineItem(action.ActionId, action.Kind, "待命：不触发 Ability 或 CharacterAction。");
            }
        }

        private static CharacterTestRuntimeAiPlannerBehaviorActionChainPreviewItem BuildAbilitySlotItem(
            CharacterTestRuntimeAiPlannerActionDefinition action,
            CharacterTestAbilityHotbarRuntime hotbar,
            CharacterTestCharacterActionRuntime actionRuntime)
        {
            var warnings = new List<string>(4);
            int slot = action.AbilitySlot;
            if (!CharacterTestAbilityHotbarRuntime.IsValidSlot(slot))
            {
                warnings.Add("技能槽号无效。");
                return CreateAbilityWarningItem(action, slot, null, warnings);
            }

            if (hotbar == null)
            {
                warnings.Add("技能槽运行时不可用。");
                return CreateAbilityWarningItem(action, slot, null, warnings);
            }

            if (!hotbar.TryGetAbilityForSlot(slot, out AbilityDefinitionConfig ability))
            {
                warnings.Add("技能槽为空或 Ability 未注册。");
                return CreateAbilityWarningItem(action, slot, null, warnings);
            }

            if (ability.ActionBinding == null || !ability.ActionBinding.ActionId.IsValid)
            {
                warnings.Add("Ability 未绑定 CharacterAction。");
                return CreateAbilityWarningItem(action, slot, ability, warnings);
            }

            return BuildCharacterActionItem(
                action,
                ability.ActionBinding.ActionIntentId.ToString(CultureInfo.InvariantCulture),
                ability.ActionBinding.ActionId,
                slot,
                ability,
                actionRuntime,
                requireHitTrace: false);
        }

        private static CharacterTestRuntimeAiPlannerBehaviorActionChainPreviewItem BuildCharacterActionItem(
            CharacterTestRuntimeAiPlannerActionDefinition action,
            string actionIntentId,
            CharacterActionId characterActionId,
            int abilitySlot,
            AbilityDefinitionConfig ability,
            CharacterTestCharacterActionRuntime actionRuntime,
            bool requireHitTrace)
        {
            var warnings = new List<string>(4);
            CharacterActionConfig actionConfig = null;
            if (actionRuntime == null || !actionRuntime.IsReady)
            {
                warnings.Add("CharacterAction runtime 未就绪。");
            }
            else if (!actionRuntime.Registry.TryGetAction(characterActionId, out actionConfig))
            {
                warnings.Add("CharacterAction 未找到: " + characterActionId);
            }

            bool hasCharacterAction = actionConfig != null;
            bool hasGameplayRequest = hasCharacterAction && HasGameplayRequest(actionConfig);
            bool hasHitTrace = hasCharacterAction && HasHitTrace(actionConfig);
            bool hasPresentationEvent = hasCharacterAction && HasPresentationEvent(actionConfig);
            bool hasVisualCue = hasCharacterAction && HasVisualCue(actionConfig);

            if (hasCharacterAction)
            {
                if (requireHitTrace && !hasHitTrace)
                    warnings.Add("CharacterAction 没有 hit trace。");
                if (!requireHitTrace && !hasGameplayRequest && !hasHitTrace)
                    warnings.Add("CharacterAction 没有 gameplay request 或 hit trace。");
                if (!hasPresentationEvent)
                    warnings.Add("CharacterAction 没有表现事件。");
            }

            string abilityName = ability != null ? ability.DisplayName : string.Empty;
            string playerSummary = BuildPlayerSummary(action.Kind, abilitySlot, abilityName, actionConfig);
            return new CharacterTestRuntimeAiPlannerBehaviorActionChainPreviewItem(
                action.ActionId,
                action.Kind,
                abilitySlot,
                ability != null ? ability.AbilityId : 0,
                abilityName,
                ability != null ? ability.AbilityStableId : string.Empty,
                actionIntentId,
                characterActionId,
                actionConfig != null ? actionConfig.DisplayName : string.Empty,
                hasCharacterAction,
                hasGameplayRequest,
                hasHitTrace,
                hasPresentationEvent,
                hasVisualCue,
                actionConfig != null ? BuildKeyFramesText(actionConfig) : string.Empty,
                JoinWarnings(warnings),
                playerSummary,
                BuildDebugSummary(action, abilitySlot, ability, actionConfig));
        }

        private static CharacterTestRuntimeAiPlannerBehaviorActionChainPreviewItem CreateAbilityWarningItem(
            CharacterTestRuntimeAiPlannerActionDefinition action,
            int abilitySlot,
            AbilityDefinitionConfig ability,
            List<string> warnings)
        {
            return new CharacterTestRuntimeAiPlannerBehaviorActionChainPreviewItem(
                action.ActionId,
                action.Kind,
                abilitySlot,
                ability != null ? ability.AbilityId : 0,
                ability != null ? ability.DisplayName : string.Empty,
                ability != null ? ability.AbilityStableId : string.Empty,
                ability != null && ability.ActionBinding != null
                    ? ability.ActionBinding.ActionIntentId.ToString(CultureInfo.InvariantCulture)
                    : string.Empty,
                ability != null && ability.ActionBinding != null
                    ? ability.ActionBinding.ActionId
                    : CharacterActionId.None,
                string.Empty,
                false,
                false,
                false,
                false,
                false,
                string.Empty,
                JoinWarnings(warnings),
                "技能槽 " + abilitySlot.ToString(CultureInfo.InvariantCulture) + " 暂不可用。",
                "CastAbilitySlot slot=" + abilitySlot.ToString(CultureInfo.InvariantCulture));
        }

        private static CharacterTestRuntimeAiPlannerBehaviorActionChainPreviewItem CreateNonTimelineItem(
            int actionId,
            CharacterTestRuntimeAiPlannerActionKind actionKind,
            string summary)
        {
            return new CharacterTestRuntimeAiPlannerBehaviorActionChainPreviewItem(
                actionId,
                actionKind,
                0,
                0,
                string.Empty,
                string.Empty,
                string.Empty,
                CharacterActionId.None,
                string.Empty,
                false,
                false,
                false,
                false,
                false,
                string.Empty,
                string.Empty,
                summary,
                actionKind.ToString());
        }

        private static string BuildPlayerSummary(
            CharacterTestRuntimeAiPlannerActionKind actionKind,
            int abilitySlot,
            string abilityName,
            CharacterActionConfig actionConfig)
        {
            if (actionKind == CharacterTestRuntimeAiPlannerActionKind.PrimaryAttack)
                return "普通攻击 -> " + EmptyAsNone(actionConfig?.DisplayName);
            if (actionKind == CharacterTestRuntimeAiPlannerActionKind.CastAbilitySlot)
            {
                return "技能槽 "
                    + abilitySlot.ToString(CultureInfo.InvariantCulture)
                    + " -> "
                    + EmptyAsNone(abilityName)
                    + " -> "
                    + EmptyAsNone(actionConfig?.DisplayName);
            }

            return actionKind.ToString();
        }

        private static string BuildDebugSummary(
            CharacterTestRuntimeAiPlannerActionDefinition action,
            int abilitySlot,
            AbilityDefinitionConfig ability,
            CharacterActionConfig actionConfig)
        {
            return "plannerAction="
                + action.ActionId.ToString(CultureInfo.InvariantCulture)
                + " kind="
                + action.Kind
                + " abilitySlot="
                + abilitySlot.ToString(CultureInfo.InvariantCulture)
                + " ability="
                + EmptyAsNone(ability?.AbilityStableId)
                + " characterAction="
                + (actionConfig?.ActionId.IsValid == true ? actionConfig.ActionId.ToString() : "none");
        }

        private static bool HasGameplayRequest(CharacterActionConfig action)
        {
            GameplayTrackEvent[] events = action.GameplayTrack.Events;
            for (int i = 0; i < events.Length; i++)
            {
                CharacterActionTrackEventKind kind = events[i].Kind;
                if (kind == CharacterActionTrackEventKind.SendGameplayRequest
                    || kind == CharacterActionTrackEventKind.CastAbility
                    || kind == CharacterActionTrackEventKind.ApplyGameplayEffect
                    || kind == CharacterActionTrackEventKind.StartEffectTimeline)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool HasHitTrace(CharacterActionConfig action)
        {
            CombatTrackEvent[] events = action.CombatTrack.Events;
            for (int i = 0; i < events.Length; i++)
            {
                CharacterActionTrackEventKind kind = events[i].Kind;
                if (kind == CharacterActionTrackEventKind.HitTraceWindow
                    || kind == CharacterActionTrackEventKind.StartCombatAction)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool HasPresentationEvent(CharacterActionConfig action)
        {
            return action.AnimationTrack.Events.Length > 0
                || action.PresentationTrack.Events.Length > 0;
        }

        private static bool HasVisualCue(CharacterActionConfig action)
        {
            PresentationTrackEvent[] events = action.PresentationTrack.Events;
            for (int i = 0; i < events.Length; i++)
            {
                if (events[i].Kind == CharacterActionTrackEventKind.SpawnVisualCue)
                    return true;
            }

            return false;
        }

        private static string BuildKeyFramesText(CharacterActionConfig action)
        {
            var parts = new List<string>(6);
            AddCombatEvents(action.CombatTrack.Events, parts);
            AddGameplayEvents(action.GameplayTrack.Events, parts);
            AddAnimationEvents(action.AnimationTrack.Events, parts);
            AddPresentationEvents(action.PresentationTrack.Events, parts);
            if (parts.Count == 0)
                return "no key events";

            return string.Join(" / ", parts.ToArray());
        }

        private static void AddCombatEvents(CombatTrackEvent[] events, List<string> parts)
        {
            for (int i = 0; i < events.Length && parts.Count < 6; i++)
            {
                CharacterActionTrackEventKind kind = events[i].Kind;
                if (kind != CharacterActionTrackEventKind.StartCombatAction
                    && kind != CharacterActionTrackEventKind.HitTraceWindow)
                {
                    continue;
                }

                parts.Add("f"
                    + events[i].Frame.ToString(CultureInfo.InvariantCulture)
                    + " "
                    + kind);
            }
        }

        private static void AddGameplayEvents(GameplayTrackEvent[] events, List<string> parts)
        {
            for (int i = 0; i < events.Length && parts.Count < 6; i++)
            {
                CharacterActionTrackEventKind kind = events[i].Kind;
                if (kind != CharacterActionTrackEventKind.SendGameplayRequest
                    && kind != CharacterActionTrackEventKind.CastAbility
                    && kind != CharacterActionTrackEventKind.ApplyGameplayEffect
                    && kind != CharacterActionTrackEventKind.StartEffectTimeline)
                {
                    continue;
                }

                parts.Add("f"
                    + events[i].Frame.ToString(CultureInfo.InvariantCulture)
                    + " "
                    + kind);
            }
        }

        private static void AddAnimationEvents(AnimationTrackEvent[] events, List<string> parts)
        {
            for (int i = 0; i < events.Length && parts.Count < 6; i++)
            {
                parts.Add("f"
                    + events[i].Frame.ToString(CultureInfo.InvariantCulture)
                    + " "
                    + events[i].Kind);
            }
        }

        private static void AddPresentationEvents(PresentationTrackEvent[] events, List<string> parts)
        {
            for (int i = 0; i < events.Length && parts.Count < 6; i++)
            {
                parts.Add("f"
                    + events[i].Frame.ToString(CultureInfo.InvariantCulture)
                    + " "
                    + events[i].Kind);
            }
        }

        private static string BuildSummary(
            IReadOnlyList<CharacterTestRuntimeAiPlannerBehaviorActionChainPreviewItem> items)
        {
            int timelineCount = 0;
            int warningCount = 0;
            for (int i = 0; i < items.Count; i++)
            {
                if (items[i].HasCharacterAction)
                    timelineCount++;
                if (items[i].HasWarning)
                    warningCount++;
            }

            return "actions="
                + items.Count.ToString(CultureInfo.InvariantCulture)
                + " timeline="
                + timelineCount.ToString(CultureInfo.InvariantCulture)
                + " warnings="
                + warningCount.ToString(CultureInfo.InvariantCulture);
        }

        private static string JoinWarnings(IReadOnlyList<string> warnings)
        {
            if (warnings == null || warnings.Count == 0)
                return string.Empty;

            var values = new string[warnings.Count];
            for (int i = 0; i < warnings.Count; i++)
                values[i] = warnings[i] ?? string.Empty;
            return string.Join("; ", values);
        }

        private static string EmptyAsNone(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? "none" : value;
        }
    }
}
