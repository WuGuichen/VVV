using System;
using System.Collections.Generic;
using System.Globalization;
using MxFramework.Gameplay;
using MxFramework.UI;

namespace MxFramework.Game.CharacterTest
{
    public static class CharacterTestFairyGuiAbilityBindingPanelViewModelBuilder
    {
        public static CharacterTestFairyGuiAbilityBindingPanelViewModel Build(
            CharacterTestAbilityHotbarRuntime runtime,
            IReadOnlyList<CharacterTestAbilityHotbarSlotBinding> workingBindings,
            int selectedSlot,
            bool dirty,
            string statusText = "",
            IMxUiTextProvider textProvider = null)
        {
            if (runtime == null)
            {
                return new CharacterTestFairyGuiAbilityBindingPanelViewModel
                {
                    Title = L(textProvider, "ui.character_test.abilities.title", "Abilities"),
                    StatusText = L(textProvider, "ui.character_test.abilities.status.unavailable", "Ability hotbar unavailable."),
                    CanEdit = false
                };
            }

            CharacterTestAbilityHotbarSlotBinding[] bindings =
                CopyOrRuntimeBindings(runtime, workingBindings);
            var viewModel = new CharacterTestFairyGuiAbilityBindingPanelViewModel
            {
                Title = L(textProvider, "ui.character_test.abilities.title", "Abilities"),
                StatusText = string.IsNullOrWhiteSpace(statusText)
                    ? dirty
                        ? L(textProvider, "ui.character_test.abilities.status.dirty", "Unsaved changes.")
                        : L(textProvider, "ui.character_test.abilities.status.ready", "Ready.")
                    : statusText,
                Message = L(textProvider, "ui.character_test.abilities.message", "Choose a slot, then choose an ability."),
                SelectedSlotText = L(textProvider, "ui.character_test.abilities.selected_slot", "Selected slot")
                    + " "
                    + Math.Max(1, selectedSlot).ToString(CultureInfo.InvariantCulture),
                Dirty = dirty,
                CanEdit = true
            };
            viewModel.SetSlots(CreateSlots(runtime, bindings, selectedSlot, textProvider));
            viewModel.SetAbilities(CreateAbilities(runtime, bindings, textProvider));
            return viewModel;
        }

        private static CharacterTestFairyGuiAbilityBindingSlotViewModel[] CreateSlots(
            CharacterTestAbilityHotbarRuntime runtime,
            IReadOnlyList<CharacterTestAbilityHotbarSlotBinding> bindings,
            int selectedSlot,
            IMxUiTextProvider textProvider)
        {
            var slots = new CharacterTestFairyGuiAbilityBindingSlotViewModel[
                CharacterTestAbilityHotbarRuntime.SlotCount];
            for (int i = 0; i < slots.Length; i++)
            {
                int slotNumber = i + 1;
                int abilityId = GetAbilityId(bindings, slotNumber);
                string abilityText = FormatAbilityName(runtime, abilityId, textProvider);
                slots[i] = new CharacterTestFairyGuiAbilityBindingSlotViewModel
                {
                    SlotNumber = slotNumber,
                    AbilityId = abilityId,
                    Bound = abilityId > 0,
                    Selected = slotNumber == selectedSlot,
                    Label = (slotNumber == selectedSlot ? "> " : string.Empty)
                        + slotNumber.ToString(CultureInfo.InvariantCulture)
                        + "  "
                        + abilityText
                };
            }

            return slots;
        }

        private static CharacterTestFairyGuiAbilityChoiceViewModel[] CreateAbilities(
            CharacterTestAbilityHotbarRuntime runtime,
            IReadOnlyList<CharacterTestAbilityHotbarSlotBinding> bindings,
            IMxUiTextProvider textProvider)
        {
            IReadOnlyList<AbilityDefinitionConfig> abilities = runtime.AvailableAbilities;
            var choices = new CharacterTestFairyGuiAbilityChoiceViewModel[
                abilities != null ? abilities.Count : 0];
            for (int i = 0; i < choices.Length; i++)
            {
                AbilityDefinitionConfig ability = abilities[i];
                if (ability == null)
                {
                    choices[i] = new CharacterTestFairyGuiAbilityChoiceViewModel
                    {
                        Index = i,
                        Visible = false
                    };
                    continue;
                }

                bool bound = IsBound(bindings, ability.AbilityId);
                choices[i] = new CharacterTestFairyGuiAbilityChoiceViewModel
                {
                    Index = i,
                    AbilityId = ability.AbilityId,
                    Visible = true,
                    BoundToAnySlot = bound,
                    Label = CharacterTestAbilityHotbarRuntime.CreateShortName(ability)
                        + (bound
                            ? "  " + L(textProvider, "ui.character_test.abilities.bound", "Bound")
                            : string.Empty)
                };
            }

            return choices;
        }

        private static CharacterTestAbilityHotbarSlotBinding[] CopyOrRuntimeBindings(
            CharacterTestAbilityHotbarRuntime runtime,
            IReadOnlyList<CharacterTestAbilityHotbarSlotBinding> bindings)
        {
            if (bindings == null || bindings.Count == 0)
                return runtime.CreateBindingSnapshot();

            var result = new CharacterTestAbilityHotbarSlotBinding[
                CharacterTestAbilityHotbarRuntime.SlotCount];
            for (int i = 0; i < result.Length; i++)
            {
                int slotNumber = i + 1;
                result[i] = new CharacterTestAbilityHotbarSlotBinding(
                    slotNumber,
                    GetAbilityId(bindings, slotNumber));
            }

            return result;
        }

        private static int GetAbilityId(
            IReadOnlyList<CharacterTestAbilityHotbarSlotBinding> bindings,
            int slotNumber)
        {
            if (bindings == null)
                return 0;

            for (int i = 0; i < bindings.Count; i++)
            {
                if (bindings[i].SlotNumber == slotNumber)
                    return bindings[i].AbilityId;
            }

            return 0;
        }

        private static bool IsBound(
            IReadOnlyList<CharacterTestAbilityHotbarSlotBinding> bindings,
            int abilityId)
        {
            for (int i = 0; bindings != null && i < bindings.Count; i++)
            {
                if (bindings[i].AbilityId == abilityId)
                    return true;
            }

            return false;
        }

        private static string FormatAbilityName(
            CharacterTestAbilityHotbarRuntime runtime,
            int abilityId,
            IMxUiTextProvider textProvider)
        {
            if (abilityId <= 0)
                return L(textProvider, "ui.character_test.ability.empty", "Empty");

            IReadOnlyList<AbilityDefinitionConfig> abilities = runtime.AvailableAbilities;
            for (int i = 0; i < abilities.Count; i++)
            {
                if (abilities[i].AbilityId == abilityId)
                    return CharacterTestAbilityHotbarRuntime.CreateShortName(abilities[i]);
            }

            return L(textProvider, "ui.character_test.abilities.unknown", "Unknown");
        }

        private static string L(IMxUiTextProvider provider, string key, string fallback)
        {
            return CharacterTestUiText.Resolve(provider, key, fallback);
        }
    }
}
