using System;
using System.Collections.Generic;
using System.Globalization;
using MxFramework.CharacterAction;
using UnityEngine.UIElements;

namespace MxFramework.Editor.CharacterAction
{
    public sealed partial class CharacterActionAuthoringWindow
    {
        private void AddPreviewBindingFields(VisualElement parent, UiRefreshScope refreshScope)
        {
            CharacterActionSetJsonDto set = _model.ActionSet;
            CharacterActionPreviewBindingChoice[] characters = CharacterActionPreviewBindingDiscovery.DiscoverCharacters();
            AddActionSetSearchPickerField(
                parent,
                "Preview Character",
                set.CharacterStableId,
                CharacterActionAuthoringSearchCatalogBuilder.BuildCharacters(
                    characters,
                    _model.ReferenceContext),
                CharacterActionAuthoringSearchCategory.Character,
                value =>
                {
                    set.CharacterStableId = value;
                    set.EquipmentStateStableId = string.Empty;
                    _model.ReferenceContext.AddCharacterStableId(value);
                    CharacterActionPreviewBindingDiscovery.AddGeneratedReferences(_model.ReferenceContext);
                },
                allowClear: true,
                refreshScope: refreshScope);

            CharacterActionPreviewBindingChoice[] states =
                CharacterActionPreviewBindingDiscovery.DiscoverEquipmentStates(set.CharacterStableId);
            AddActionSetSearchPickerField(
                parent,
                "Preview Equipment",
                set.EquipmentStateStableId,
                CharacterActionAuthoringSearchCatalogBuilder.BuildEquipmentStates(
                    states,
                    _model.ReferenceContext),
                CharacterActionAuthoringSearchCategory.EquipmentState,
                value =>
                {
                    set.EquipmentStateStableId = value;
                    _model.ReferenceContext.AddCharacterStableId(set.CharacterStableId);
                    _model.ReferenceContext.AddEquipmentStateStableId(value);
                    CharacterActionPreviewBindingDiscovery.AddGeneratedReferences(_model.ReferenceContext);
                },
                allowClear: true,
                refreshScope: refreshScope);

            AddReferenceTextField(
                parent,
                "Manual Character Stable Id",
                set.CharacterStableId,
                _model.ReferenceContext.CharacterStableIds,
                value =>
                {
                    set.CharacterStableId = value;
                    _model.ReferenceContext.AddCharacterStableId(value);
                });
            AddReferenceTextField(
                parent,
                "Manual Equipment State Id",
                set.EquipmentStateStableId,
                _model.ReferenceContext.EquipmentStateStableIds,
                value =>
                {
                    set.EquipmentStateStableId = value;
                    _model.ReferenceContext.AddEquipmentStateStableId(value);
                });
        }

        private void RebuildBindings()
        {
            if (_bindingsHost == null)
                return;

            _bindingsHost.Clear();
            CharacterActionBindingJsonDto[] commandBindings = _model.ActionSet.CommandBindings ?? Array.Empty<CharacterActionBindingJsonDto>();
            for (int i = 0; i < commandBindings.Length; i++)
                AddCommandBinding(_bindingsHost, commandBindings, i);
        }

        private void AddCommandBinding(VisualElement parent, CharacterActionBindingJsonDto[] commandBindings, int index)
        {
            CharacterActionBindingJsonDto binding = commandBindings[index] ?? new CharacterActionBindingJsonDto();
            VisualElement row = AddCard(parent, "绑定 " + index);
            AddActionSetSearchPickerField(
                row,
                "Intent Id",
                CharacterActionAuthoringIds.Label(binding.IntentId, 404),
                CharacterActionAuthoringSearchCatalogBuilder.BuildIntents(_documentSession, _model),
                CharacterActionAuthoringSearchCategory.Intent,
                value => binding.IntentId = CharacterActionAuthoringIds.Resolve(value, 404),
                refreshScope: UiRefreshScope.Bindings);
            AddActionSetSearchPickerField(
                row,
                "Action Id",
                binding.ActionId.ToString(CultureInfo.InvariantCulture),
                CharacterActionAuthoringSearchCatalogBuilder.BuildActions(_documentSession, _model),
                CharacterActionAuthoringSearchCategory.Action,
                value => binding.ActionId = int.TryParse(
                    value,
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture,
                    out int parsed) ? parsed : 0,
                refreshScope: UiRefreshScope.Bindings);
            AddIntegerField(row, "Priority", binding.Priority, value => binding.Priority = value);
            AddToggle(row, "Allow Queue", binding.AllowQueue, value => binding.AllowQueue = value);
            AddIntegerField(row, "Queue Window Frames", binding.QueueWindowFrames, value => binding.QueueWindowFrames = value);
            row.Add(new Button(() => RemoveCommandBinding(index)) { text = "删除绑定" });
            commandBindings[index] = binding;
        }

        private void AddCommandBinding()
        {
            AddNamedCommandBinding("Intent", _model.Actions.Count > 0 ? _model.Actions[0].ActionId : 0, 0);
        }

        private void AddNamedCommandBinding(string intentId, int actionId, int priority)
        {
            ApplyStructuralChange(() =>
            {
                var bindings = new List<CharacterActionBindingJsonDto>(_model.ActionSet.CommandBindings ?? Array.Empty<CharacterActionBindingJsonDto>());
                bindings.Add(new CharacterActionBindingJsonDto
                {
                    IntentId = CharacterActionAuthoringIds.Resolve(intentId, 404),
                    ActionId = actionId,
                    Priority = priority,
                    AllowQueue = false,
                    QueueWindowFrames = 0,
                });
                _model.ActionSet.CommandBindings = bindings.ToArray();
            }, UiRefreshScope.Bindings);
        }

        private void RemoveCommandBinding(int index)
        {
            ApplyStructuralChange(() =>
            {
                var bindings = new List<CharacterActionBindingJsonDto>(_model.ActionSet.CommandBindings ?? Array.Empty<CharacterActionBindingJsonDto>());
                if (index >= 0 && index < bindings.Count)
                    bindings.RemoveAt(index);
                _model.ActionSet.CommandBindings = bindings.ToArray();
            }, UiRefreshScope.Bindings);
        }

        private void RebuildAbilityBindings()
        {
            if (_abilityBindingsHost == null)
                return;

            _abilityBindingsHost.Clear();
            _focusedAbilityBindingElement = null;
            CharacterAbilityActionBindingJsonDto[] abilityBindings = _model.ActionSet.AbilityBindings ?? Array.Empty<CharacterAbilityActionBindingJsonDto>();
            int focusedIndex = FindAbilityBindingIndex(abilityBindings, _focusedAbilityId);
            if (_focusedAbilityId > 0)
                AddFocusedAbilityBindingBanner(_abilityBindingsHost, focusedIndex);
            for (int i = 0; i < abilityBindings.Length; i++)
                AddAbilityBinding(_abilityBindingsHost, abilityBindings, i);
        }

        private void AddAbilityBinding(VisualElement parent, CharacterAbilityActionBindingJsonDto[] abilityBindings, int index)
        {
            CharacterAbilityActionBindingJsonDto binding = abilityBindings[index] ?? new CharacterAbilityActionBindingJsonDto();
            VisualElement row = AddCard(parent, "能力绑定 " + index);
            if (_focusedAbilityId > 0 && binding.AbilityId == _focusedAbilityId)
            {
                row.name = "focused-ability-binding";
                row.AddToClassList("ca-ability-binding--focused");
                _focusedAbilityBindingElement = row;
                row.schedule.Execute(() =>
                    row.GetFirstAncestorOfType<ScrollView>()?.ScrollTo(row)).ExecuteLater(50);
            }
            AddActionSetSearchPickerField(
                row,
                "Ability Id",
                binding.AbilityId.ToString(CultureInfo.InvariantCulture),
                CharacterActionAuthoringSearchCatalogBuilder.BuildAbilities(
                    _documentSession,
                    _model,
                    CharacterActionPreviewBindingDiscovery.DiscoverKnownAbilityIds(
                        _model.ActionSet.CharacterStableId,
                        _model.ActionSet.EquipmentStateStableId)),
                CharacterActionAuthoringSearchCategory.Ability,
                value =>
                {
                    if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed))
                        binding.AbilityId = parsed;
                    else
                        binding.AbilityId = 0;
                },
                refreshScope: UiRefreshScope.Bindings);
            AddActionSetSearchPickerField(
                row,
                "Action Id",
                binding.ActionId.ToString(CultureInfo.InvariantCulture),
                CharacterActionAuthoringSearchCatalogBuilder.BuildActions(_documentSession, _model),
                CharacterActionAuthoringSearchCategory.Action,
                value => binding.ActionId = int.TryParse(
                    value,
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture,
                    out int parsed) ? parsed : 0,
                refreshScope: UiRefreshScope.Bindings);
            AddActionSetTagPickerField(
                row,
                "Required Tags",
                binding.RequiredTags,
                value => binding.RequiredTags = value);
            AddActionSetTagPickerField(
                row,
                "Forbidden Tags",
                binding.ForbiddenTags,
                value => binding.ForbiddenTags = value);
            row.Add(new Label(FormatAbilityBindingStatus(binding))
            {
                style = { whiteSpace = WhiteSpace.Normal },
            });
            row.Add(new Button(() => RemoveAbilityBinding(index)) { text = "删除能力绑定" });
            abilityBindings[index] = binding;
        }

        private void AddFocusedAbilityBindingBanner(VisualElement parent, int focusedIndex)
        {
            var banner = new VisualElement();
            banner.AddToClassList("ca-ability-focus-banner");
            if (_focusedAbilityOpenConflict)
                banner.AddToClassList("ca-ability-focus-banner--conflict");
            else if (focusedIndex >= 0)
                banner.AddToClassList("ca-ability-focus-banner--found");
            else
                banner.AddToClassList("ca-ability-focus-banner--missing");

            string title = focusedIndex >= 0
                ? "已定位 abilityId " + _focusedAbilityId.ToString(CultureInfo.InvariantCulture)
                : "缺少 abilityId " + _focusedAbilityId.ToString(CultureInfo.InvariantCulture) + " 的动作绑定";
            var titleLabel = new Label(title)
            {
                name = "focused-ability-binding-title",
            };
            titleLabel.AddToClassList("ca-ability-focus-banner-title");
            banner.Add(titleLabel);
            banner.Add(new Label(string.IsNullOrWhiteSpace(_focusedAbilityOpenMessage)
                ? "来自 CharacterStudio 的定位请求。"
                : _focusedAbilityOpenMessage)
            {
                style = { whiteSpace = WhiteSpace.Normal },
            });

            if (!_focusedAbilityOpenConflict && focusedIndex < 0)
            {
                banner.Add(new Button(AddFocusedAbilityBinding)
                {
                    text = "添加该 abilityId 绑定"
                });
            }

            parent.Add(banner);
        }

        private void AddFocusedAbilityBinding()
        {
            if (_focusedAbilityId <= 0)
                return;

            ApplyStructuralChange(() =>
            {
                var bindings = new List<CharacterAbilityActionBindingJsonDto>(_model.ActionSet.AbilityBindings ?? Array.Empty<CharacterAbilityActionBindingJsonDto>());
                bindings.Add(new CharacterAbilityActionBindingJsonDto
                {
                    AbilityId = _focusedAbilityId,
                    ActionId = _model.Actions.Count > 0 ? _model.Actions[0].ActionId : 0,
                    RequiredTags = Array.Empty<string>(),
                    ForbiddenTags = Array.Empty<string>(),
                });
                _model.ActionSet.AbilityBindings = bindings.ToArray();
            }, UiRefreshScope.Bindings);
        }

        private void AddAbilityBinding()
        {
            List<string> knownAbilityIds = GetKnownAbilityIdChoices();
            int abilityId = 0;
            if (knownAbilityIds.Count > 0)
                int.TryParse(knownAbilityIds[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out abilityId);

            ApplyStructuralChange(() =>
            {
                var bindings = new List<CharacterAbilityActionBindingJsonDto>(_model.ActionSet.AbilityBindings ?? Array.Empty<CharacterAbilityActionBindingJsonDto>());
                bindings.Add(new CharacterAbilityActionBindingJsonDto
                {
                    AbilityId = abilityId,
                    ActionId = _model.Actions.Count > 0 ? _model.Actions[0].ActionId : 0,
                    RequiredTags = Array.Empty<string>(),
                    ForbiddenTags = Array.Empty<string>(),
                });
                _model.ActionSet.AbilityBindings = bindings.ToArray();
            }, UiRefreshScope.Bindings);
        }

        private void RemoveAbilityBinding(int index)
        {
            ApplyStructuralChange(() =>
            {
                var bindings = new List<CharacterAbilityActionBindingJsonDto>(_model.ActionSet.AbilityBindings ?? Array.Empty<CharacterAbilityActionBindingJsonDto>());
                if (index >= 0 && index < bindings.Count)
                    bindings.RemoveAt(index);
                _model.ActionSet.AbilityBindings = bindings.ToArray();
            }, UiRefreshScope.Bindings);
        }

        private static int FindAbilityBindingIndex(CharacterAbilityActionBindingJsonDto[] abilityBindings, int abilityId)
        {
            if (abilityId <= 0)
                return -1;
            abilityBindings = abilityBindings ?? Array.Empty<CharacterAbilityActionBindingJsonDto>();
            for (int i = 0; i < abilityBindings.Length; i++)
            {
                if (abilityBindings[i]?.AbilityId == abilityId)
                    return i;
            }

            return -1;
        }

        private List<string> GetActionIdChoices()
        {
            var choices = new List<string>();
            IReadOnlyList<CharacterActionAuthoringSearchCandidate> candidates =
                CharacterActionAuthoringSearchCatalogBuilder.BuildActions(_documentSession, _model).Candidates;
            for (int i = 0; i < candidates.Count; i++)
            {
                string stableId = candidates[i].Value;
                if (!string.IsNullOrWhiteSpace(stableId) && !choices.Contains(stableId))
                    choices.Add(stableId);
            }

            return choices;
        }

        private List<string> GetKnownAbilityIdChoices()
        {
            int[] abilityIds = CharacterActionPreviewBindingDiscovery.DiscoverKnownAbilityIds(
                _model.ActionSet.CharacterStableId,
                _model.ActionSet.EquipmentStateStableId);
            var choices = new List<string>(abilityIds.Length);
            for (int i = 0; i < abilityIds.Length; i++)
                choices.Add(abilityIds[i].ToString(CultureInfo.InvariantCulture));
            return choices;
        }

        private string FormatAbilityBindingStatus(CharacterAbilityActionBindingJsonDto binding)
        {
            int[] known = CharacterActionPreviewBindingDiscovery.DiscoverKnownAbilityIds(
                _model.ActionSet.CharacterStableId,
                _model.ActionSet.EquipmentStateStableId);
            if (known.Length == 0)
                return "当前 Preview Character 尚未解析出 effective abilities；可以先手填，或回到 CharacterStudio 检查能力 Loadout。";

            for (int i = 0; i < known.Length; i++)
            {
                if (known[i] == binding.AbilityId)
                    return "当前角色已授予 abilityId " + binding.AbilityId.ToString(CultureInfo.InvariantCulture) + "。";
            }

            return "当前角色未授予 abilityId " + binding.AbilityId.ToString(CultureInfo.InvariantCulture) + "；请检查 CharacterStudio 的能力 Loadout。";
        }
    }
}
