using System.Collections.Generic;
using System.Globalization;
using MxFramework.Editor;
using MxFramework.Gameplay.Triggers;
using MxFramework.Gameplay.Triggers.Config;
using MxFramework.Gameplay.Triggers.GameplayBridge;
using MxFramework.Gameplay.Triggers.StoryBridge;
using UnityEngine.UIElements;

namespace MxFramework.Game.CharacterTest.Editor
{
    public sealed partial class CharacterTestWorldObjectLevelEditorModule
    {
        private VisualElement CreateTriggerTargetReferenceField(
            CharacterTestLevelGameplayTriggerEntry entry,
            out bool resolved,
            out GameplayTriggerTargetSelector selector,
            out string diagnostic)
        {
            resolved = TryBuildTriggerTargetChoices(
                entry,
                out string currentKey,
                out List<MxAuthoringReferenceOption<string>> choices,
                out diagnostic,
                out selector);
            VisualElement field = MxAuthoringReferenceField.Create(
                string.Empty,
                currentKey,
                choices,
                onValueChanged: null,
                name: "character-test-trigger-target-reference",
                emptyDiagnostic: resolved
                    ? "当前 Trigger 没有可持久化的目标候选。"
                    : diagnostic,
                unknownValueFormatter: _ => "未解析目标",
                unknownDiagnostic: diagnostic);
            field.AddToClassList(
                "mx-level-editor__trigger-target-primary");

            Label fieldLabel = field.Q<Label>(
                MxAuthoringReferenceField.LabelElementName);
            if (fieldLabel != null)
                fieldLabel.style.display = DisplayStyle.None;

            VisualElement value = field.Q<VisualElement>(
                MxAuthoringReferenceField.ValueElementName);
            value?.AddToClassList(
                "mx-level-editor__trigger-target-primary-value");
            return field;
        }

        private bool TryBuildTriggerTargetChoices(
            CharacterTestLevelGameplayTriggerEntry entry,
            out string currentKey,
            out List<MxAuthoringReferenceOption<string>> choices,
            out string diagnostic,
            out GameplayTriggerTargetSelector selector)
        {
            currentKey = string.Empty;
            choices =
                new List<MxAuthoringReferenceOption<string>>();
            diagnostic = string.Empty;
            selector = default;

            if (entry == null
                || _triggerDocument?.Authoring == null)
            {
                diagnostic = "Trigger 创作数据不可用，无法解析作用目标。";
                return false;
            }

            GameplayTriggerDefinitionAuthoring definition =
                FindTriggerDefinition(entry.DefinitionId);
            GameplayTriggerBindingAuthoring binding =
                FindTriggerBinding(entry.BindingId);
            GameplayTriggerSourceAuthoring source =
                FindTriggerSource(entry.SourceDefinitionId);
            if (definition?.Routes == null
                || definition.Routes.Length == 0
                || binding == null
                || source == null)
            {
                diagnostic = "Trigger 路由、绑定或来源定义不完整。";
                return false;
            }

            if (binding.DefinitionId != entry.DefinitionId
                || binding.SourceBindingId != entry.SourceBindingId
                || definition.SourceDefinitionId
                    != entry.SourceDefinitionId)
            {
                diagnostic = "Trigger 路由与来源绑定不一致。";
                return false;
            }

            if (source.SourceKind
                != GameplayTriggerSourceKind.Occupancy)
            {
                diagnostic =
                    "当前区域 Trigger 不是 Occupancy 来源，无法提供区域对象目标。";
                return false;
            }

            if (!TryResolveCommonActionTarget(
                    definition,
                    out selector,
                    out int targetSlotId,
                    out int actionFamily))
            {
                diagnostic =
                    "当前路由动作没有一致且受支持的作用目标。";
                return false;
            }

            if (_triggerCompileResult?.Succeeded == true
                && !CompiledTriggerContains(entry))
            {
                diagnostic =
                    "Trigger 编译结果与当前条目不一致，未提供目标候选。";
                return false;
            }

            if (selector == GameplayTriggerTargetSelector.Subject
                && actionFamily == 1)
            {
                CharacterTestLevelGameplayTriggerRegionBinding region =
                    FindTriggerRegionBinding(entry.SourceBindingId);
                if (region == null
                    || region.ProviderId
                        != CharacterTestLevelGameplayTriggerAuthoringAdapter
                            .CombatRegionProviderId
                    || region.MaxMappedSubjects <= 0)
                {
                    diagnostic =
                        "区域来源没有可保证的 Trigger Subject 能力。";
                    return false;
                }

                currentKey = "trigger-subject";
                choices.Add(
                    new MxAuthoringReferenceOption<string>(
                        currentKey,
                        currentKey,
                        "进入区域的角色",
                        string.Empty,
                        "GameplayTriggerTargetSelector.Subject；"
                        + "目标由 Trigger Source 的 Subject 上下文提供；"
                        + "目标域为 Gameplay Entity。"));
                return true;
            }

            if (selector
                    == GameplayTriggerTargetSelector.ExplicitBindingTarget
                && actionFamily == 2
                && TryResolveStoryBindingTarget(
                    binding,
                    targetSlotId,
                    entry.SourceBindingId))
            {
                currentKey = "source-binding:"
                    + entry.SourceBindingId.ToString(
                        CultureInfo.InvariantCulture);
                choices.Add(
                    new MxAuthoringReferenceOption<string>(
                        currentKey,
                        currentKey,
                        "当前区域",
                        string.Empty,
                        "GameplayTriggerTargetSelector.ExplicitBindingTarget；"
                        + "目标由当前 Trigger Source Binding 提供；"
                        + "目标域为 Gameplay Trigger Source Binding。"));
                return true;
            }

            diagnostic =
                "Owner、Instigator 或不兼容目标域不会作为当前 Trigger 的候选。";
            return false;
        }

        private GameplayTriggerDefinitionAuthoring FindTriggerDefinition(
            int definitionId)
        {
            GameplayTriggerDefinitionAuthoring[] rows =
                _triggerDocument?.Authoring?.Definitions;
            for (int i = 0; rows != null && i < rows.Length; i++)
            {
                if (rows[i]?.DefinitionId == definitionId)
                    return rows[i];
            }
            return null;
        }

        private GameplayTriggerBindingAuthoring FindTriggerBinding(
            int bindingId)
        {
            GameplayTriggerBindingAuthoring[] rows =
                _triggerDocument?.Authoring?.Bindings;
            for (int i = 0; rows != null && i < rows.Length; i++)
            {
                if (rows[i]?.BindingId == bindingId)
                    return rows[i];
            }
            return null;
        }

        private GameplayTriggerSourceAuthoring FindTriggerSource(
            int sourceDefinitionId)
        {
            GameplayTriggerSourceAuthoring[] rows =
                _triggerDocument?.Authoring?.Sources;
            for (int i = 0; rows != null && i < rows.Length; i++)
            {
                if (rows[i]?.SourceDefinitionId == sourceDefinitionId)
                    return rows[i];
            }
            return null;
        }

        private static bool TryResolveCommonActionTarget(
            GameplayTriggerDefinitionAuthoring definition,
            out GameplayTriggerTargetSelector selector,
            out int targetSlotId,
            out int actionFamily)
        {
            selector = default;
            targetSlotId = 0;
            actionFamily = 0;
            bool hasAction = false;
            for (int routeIndex = 0;
                 routeIndex < definition.Routes.Length;
                 routeIndex++)
            {
                GameplayTriggerActionAuthoring[] actions =
                    definition.Routes[routeIndex]?.Actions;
                if (actions == null || actions.Length == 0)
                    return false;

                for (int actionIndex = 0;
                     actionIndex < actions.Length;
                     actionIndex++)
                {
                    GameplayTriggerActionAuthoring action =
                        actions[actionIndex];
                    int family = ResolveTriggerActionFamily(
                        action.ActionKindId);
                    if (family == 0)
                        return false;

                    if (!hasAction)
                    {
                        selector = action.TargetSelector;
                        targetSlotId = action.TargetSlotId;
                        actionFamily = family;
                        hasAction = true;
                        continue;
                    }

                    if (selector != action.TargetSelector
                        || targetSlotId != action.TargetSlotId
                        || actionFamily != family)
                    {
                        return false;
                    }
                }
            }
            return hasAction;
        }

        private static int ResolveTriggerActionFamily(int actionKindId)
        {
            if (actionKindId
                    == GameplayTriggerGameplayBuffActionKinds
                        .ApplyComponentBuffValue
                || actionKindId
                    == GameplayTriggerGameplayBuffActionKinds
                        .RemoveComponentBuffValue)
            {
                return 1;
            }

            return actionKindId
                == GameplayTriggerStoryCommandActionKinds.RaiseTriggerValue
                    ? 2
                    : 0;
        }

        private static bool TryResolveStoryBindingTarget(
            GameplayTriggerBindingAuthoring binding,
            int targetSlotId,
            int sourceBindingId)
        {
            GameplayTriggerBindingTargetAuthoring[] targets =
                binding?.ExplicitTargets;
            for (int i = 0; targets != null && i < targets.Length; i++)
            {
                GameplayTriggerBindingTargetAuthoring target = targets[i];
                if (target.SlotId == targetSlotId
                    && target.DomainId
                        == GameplayTriggerTargetRef
                            .GameplayTriggerSourceBindingDomainId
                    && target.Value == sourceBindingId
                    && target.Generation == 0)
                {
                    return true;
                }
            }
            return false;
        }

        private bool CompiledTriggerContains(
            CharacterTestLevelGameplayTriggerEntry entry)
        {
            GameplayTriggerCompiledCatalogPlan plan =
                _triggerCompileResult?.CompiledCatalog;
            if (plan == null)
                return false;

            bool hasDefinition = false;
            for (int i = 0; i < plan.DefinitionCount; i++)
            {
                if (plan.GetDefinition(i).DefinitionId.Value
                    == entry.DefinitionId)
                {
                    hasDefinition = true;
                    break;
                }
            }
            if (!hasDefinition)
                return false;

            for (int i = 0; i < plan.BindingCount; i++)
            {
                GameplayTriggerBinding binding = plan.GetBinding(i);
                if (binding.BindingId.Value == entry.BindingId
                    && binding.DefinitionId.Value == entry.DefinitionId
                    && binding.SourceBindingId.Value
                        == entry.SourceBindingId)
                {
                    return true;
                }
            }
            return false;
        }

        internal bool TryGetTriggerTargetReferenceForTests(
            string stableId,
            out GameplayTriggerTargetSelector selector,
            out string displayName,
            out int optionCount)
        {
            selector = default;
            displayName = string.Empty;
            optionCount = 0;
            if (_triggerDocument == null
                || !_triggerDocument.TryFindEntry(
                    stableId,
                    out CharacterTestLevelGameplayTriggerEntry entry)
                || !TryBuildTriggerTargetChoices(
                    entry,
                    out _,
                    out List<MxAuthoringReferenceOption<string>> choices,
                    out _,
                    out selector))
            {
                return false;
            }

            optionCount = choices.Count;
            if (choices.Count > 0)
                displayName = choices[0].DisplayName;
            return choices.Count > 0;
        }
    }
}
