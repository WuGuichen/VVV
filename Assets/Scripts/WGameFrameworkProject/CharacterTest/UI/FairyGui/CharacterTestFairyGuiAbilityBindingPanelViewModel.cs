using System;
using System.Collections.Generic;
using System.Text;

namespace MxFramework.Game.CharacterTest
{
    public sealed class CharacterTestFairyGuiAbilityBindingPanelPayload
    {
        public int SlotNumber { get; set; }
        public int AbilityId { get; set; }
    }

    public sealed class CharacterTestFairyGuiAbilityBindingSlotViewModel
    {
        public int SlotNumber { get; set; }
        public int AbilityId { get; set; }
        public string Label { get; set; } = string.Empty;
        public bool Selected { get; set; }
        public bool Bound { get; set; }
    }

    public sealed class CharacterTestFairyGuiAbilityChoiceViewModel
    {
        public int Index { get; set; }
        public int AbilityId { get; set; }
        public string Label { get; set; } = string.Empty;
        public bool Visible { get; set; }
        public bool BoundToAnySlot { get; set; }
    }

    public sealed class CharacterTestFairyGuiAbilityBindingPanelViewModel
    {
        private CharacterTestFairyGuiAbilityBindingSlotViewModel[] _slots =
            Array.Empty<CharacterTestFairyGuiAbilityBindingSlotViewModel>();
        private CharacterTestFairyGuiAbilityChoiceViewModel[] _abilities =
            Array.Empty<CharacterTestFairyGuiAbilityChoiceViewModel>();

        public string Title { get; set; } = "Abilities";
        public string StatusText { get; set; } = "Ready";
        public string Message { get; set; } = "Choose a slot, then choose an ability.";
        public string SelectedSlotText { get; set; } = "Slot 1";
        public bool Dirty { get; set; }
        public bool CanEdit { get; set; } = true;
        public IReadOnlyList<CharacterTestFairyGuiAbilityBindingSlotViewModel> Slots => _slots;
        public IReadOnlyList<CharacterTestFairyGuiAbilityChoiceViewModel> Abilities => _abilities;

        public void SetSlots(IReadOnlyList<CharacterTestFairyGuiAbilityBindingSlotViewModel> slots)
        {
            if (slots == null || slots.Count == 0)
            {
                _slots = Array.Empty<CharacterTestFairyGuiAbilityBindingSlotViewModel>();
                return;
            }

            _slots = new CharacterTestFairyGuiAbilityBindingSlotViewModel[slots.Count];
            for (int i = 0; i < slots.Count; i++)
                _slots[i] = slots[i];
        }

        public void SetAbilities(IReadOnlyList<CharacterTestFairyGuiAbilityChoiceViewModel> abilities)
        {
            if (abilities == null || abilities.Count == 0)
            {
                _abilities = Array.Empty<CharacterTestFairyGuiAbilityChoiceViewModel>();
                return;
            }

            _abilities = new CharacterTestFairyGuiAbilityChoiceViewModel[abilities.Count];
            for (int i = 0; i < abilities.Count; i++)
                _abilities[i] = abilities[i];
        }
    }

    internal static class CharacterTestFairyGuiAbilityBindingPanelViewModelSignature
    {
        public static string Create(CharacterTestFairyGuiAbilityBindingPanelViewModel viewModel)
        {
            if (viewModel == null)
                return string.Empty;

            var builder = new StringBuilder(256);
            builder.Append(viewModel.Dirty ? '1' : '0').Append('|')
                .Append(viewModel.CanEdit ? '1' : '0').Append('|');
            AppendToken(builder, viewModel.Title);
            AppendToken(builder, viewModel.StatusText);
            AppendToken(builder, viewModel.Message);
            AppendToken(builder, viewModel.SelectedSlotText);
            AppendSlots(builder, viewModel.Slots);
            AppendAbilities(builder, viewModel.Abilities);
            return builder.ToString();
        }

        private static void AppendSlots(
            StringBuilder builder,
            IReadOnlyList<CharacterTestFairyGuiAbilityBindingSlotViewModel> slots)
        {
            builder.Append("slots=").Append(slots != null ? slots.Count : 0).Append('|');
            for (int i = 0; slots != null && i < slots.Count; i++)
            {
                CharacterTestFairyGuiAbilityBindingSlotViewModel slot = slots[i];
                builder.Append(slot.SlotNumber).Append('|')
                    .Append(slot.AbilityId).Append('|')
                    .Append(slot.Selected ? '1' : '0').Append('|')
                    .Append(slot.Bound ? '1' : '0').Append('|');
                AppendToken(builder, slot.Label);
            }
        }

        private static void AppendAbilities(
            StringBuilder builder,
            IReadOnlyList<CharacterTestFairyGuiAbilityChoiceViewModel> abilities)
        {
            builder.Append("abilities=").Append(abilities != null ? abilities.Count : 0).Append('|');
            for (int i = 0; abilities != null && i < abilities.Count; i++)
            {
                CharacterTestFairyGuiAbilityChoiceViewModel ability = abilities[i];
                builder.Append(ability.Index).Append('|')
                    .Append(ability.AbilityId).Append('|')
                    .Append(ability.Visible ? '1' : '0').Append('|')
                    .Append(ability.BoundToAnySlot ? '1' : '0').Append('|');
                AppendToken(builder, ability.Label);
            }
        }

        private static void AppendToken(StringBuilder builder, string value)
        {
            value = value ?? string.Empty;
            builder.Append(value.Length).Append(':').Append(value).Append('|');
        }
    }
}
