using System.Collections.Generic;
using System.Globalization;
using MxFramework.UI;
using MxFramework.UI.FairyGui;
using UnityEngine;
using Fgui = global::FairyGUI;

namespace MxFramework.Game.CharacterTest
{
    public sealed partial class CharacterTestFairyGuiAbilityBindingPanelBinder
    {
        private const float DesignWidth = 1920f;
        private const float DesignHeight = 1080f;
        private const int AbilityVisibleRowCount = 12;
        private const float AbilityListX = 548f;
        private const float AbilityListFirstY = 274f;
        private const float AbilityListRowHeight = 50f;
        private const float AbilityButtonWidth = 920f;
        private const float AbilityButtonHeight = 40f;

        private Fgui.GComponent _abilityScrollRoot;
        private CharacterTestFairyGuiAbilityBindingPanelViewModel _abilityScrollViewModel;
        private int _abilityScrollOffset;

        partial void OnAfterGeneratedBind(
            Fgui.GComponent root,
            CharacterTestFairyGuiAbilityBindingPanelViewModel viewModel)
        {
            ApplyFullScreenRootSize(root);
            EnsureAbilityChoiceButtons(root);
            ConfigureAbilityScroll(root, viewModel);
            BindSlots(root, viewModel);
            BindAbilities(root, viewModel);
            ConfigureFocus(root, viewModel);
        }

        private static void EnsureAbilityChoiceButtons(Fgui.GComponent root)
        {
            if (root == null)
                return;

            Fgui.GButton template = null;
            int templateIndex = -1;
            for (int i = CharacterTestFairyGuiAbilityBindingPanelIds.AbilityChoiceCount - 1; i >= 0; i--)
            {
                template = root.GetChild(CharacterTestFairyGuiAbilityBindingPanelIds.AbilityButton(i))?.asButton;
                if (template != null)
                {
                    templateIndex = i;
                    break;
                }
            }

            if (template == null || templateIndex < 0 || string.IsNullOrWhiteSpace(template.resourceURL))
                return;

            for (int i = templateIndex + 1; i < CharacterTestFairyGuiAbilityBindingPanelIds.AbilityChoiceCount; i++)
            {
                string buttonName = CharacterTestFairyGuiAbilityBindingPanelIds.AbilityButton(i);
                if (root.GetChild(buttonName) != null)
                    continue;

                Fgui.GButton button = Fgui.UIPackage.CreateObjectFromURL(template.resourceURL)?.asButton;
                if (button == null)
                    return;

                button.name = buttonName;
                button.SetXY(template.x, template.y + 50f * (i - templateIndex));
                button.SetSize(template.width, template.height);
                button.visible = false;
                root.AddChild(button);
            }
        }

        private void BindSlots(
            Fgui.GComponent root,
            CharacterTestFairyGuiAbilityBindingPanelViewModel viewModel)
        {
            IReadOnlyList<CharacterTestFairyGuiAbilityBindingSlotViewModel> slots = viewModel.Slots;
            for (int i = 0; i < CharacterTestAbilityHotbarRuntime.SlotCount; i++)
            {
                int slotNumber = i + 1;
                CharacterTestFairyGuiAbilityBindingSlotViewModel slot =
                    slots != null && i < slots.Count ? slots[i] : null;
                Fgui.GButton button =
                    root.GetChild(CharacterTestFairyGuiAbilityBindingPanelIds.SlotButton(slotNumber))?.asButton;
                if (button == null)
                    continue;

                button.text = slot != null ? slot.Label : slotNumber.ToString(CultureInfo.InvariantCulture);
                button.enabled = viewModel.CanEdit;
                button.grayed = !viewModel.CanEdit;
                button.selected = slot != null && slot.Selected;
                button.visible = true;
                button.onClick.Set(() =>
                {
                    if (!viewModel.CanEdit)
                        return;

                    _commandSink?.Enqueue(new MxUiCommand(
                        _viewId,
                        CharacterTestFairyGuiAbilityBindingPanelCommandIds.SelectSlot,
                        new CharacterTestFairyGuiAbilityBindingPanelPayload
                        {
                            SlotNumber = slotNumber
                        }));
                });
            }
        }

        private void BindAbilities(
            Fgui.GComponent root,
            CharacterTestFairyGuiAbilityBindingPanelViewModel viewModel)
        {
            IReadOnlyList<CharacterTestFairyGuiAbilityChoiceViewModel> abilities = viewModel.Abilities;
            _abilityScrollOffset = ClampAbilityScrollOffset(_abilityScrollOffset, abilities);
            for (int i = 0; i < CharacterTestFairyGuiAbilityBindingPanelIds.AbilityChoiceCount; i++)
            {
                Fgui.GButton button =
                    root.GetChild(CharacterTestFairyGuiAbilityBindingPanelIds.AbilityButton(i))?.asButton;
                if (button == null)
                    continue;

                bool rowVisible = i < AbilityVisibleRowCount;
                CharacterTestFairyGuiAbilityChoiceViewModel ability = rowVisible
                    ? GetVisibleAbilityAt(abilities, _abilityScrollOffset + i)
                    : null;
                bool visible = ability != null && ability.Visible;
                button.visible = visible;
                button.enabled = visible && viewModel.CanEdit;
                button.grayed = !button.enabled;
                button.text = visible ? ability.Label : string.Empty;
                if (rowVisible)
                {
                    button.SetXY(AbilityListX, AbilityListFirstY + i * AbilityListRowHeight);
                    button.SetSize(AbilityButtonWidth, AbilityButtonHeight);
                }

                int abilityId = ability != null ? ability.AbilityId : 0;
                button.onClick.Set(() =>
                {
                    if (!viewModel.CanEdit || abilityId <= 0)
                        return;

                    _commandSink?.Enqueue(new MxUiCommand(
                        _viewId,
                        CharacterTestFairyGuiAbilityBindingPanelCommandIds.SelectAbility,
                        new CharacterTestFairyGuiAbilityBindingPanelPayload
                        {
                            AbilityId = abilityId
                        }));
                });
            }
        }

        private void ConfigureAbilityScroll(
            Fgui.GComponent root,
            CharacterTestFairyGuiAbilityBindingPanelViewModel viewModel)
        {
            if (_abilityScrollRoot != null
                && _abilityScrollRoot != root
                && _abilityScrollRoot.displayObject != null)
            {
                _abilityScrollRoot.displayObject.onMouseWheel.Set((Fgui.EventCallback1)null);
            }

            _abilityScrollRoot = root;
            _abilityScrollViewModel = viewModel;
            _abilityScrollOffset = ClampAbilityScrollOffset(_abilityScrollOffset, viewModel.Abilities);

            if (root?.displayObject != null)
                root.displayObject.onMouseWheel.Set(OnAbilityMouseWheel);
        }

        private void OnAbilityMouseWheel(Fgui.EventContext context)
        {
            float delta = context?.inputEvent?.mouseWheelDelta ?? 0f;
            if (delta == 0f)
                return;

            if (ScrollAbilityChoices(delta < 0f ? 1 : -1))
                context.StopPropagation();
        }

        internal bool ScrollAbilityChoicesForTest(int rowDelta)
        {
            return ScrollAbilityChoices(rowDelta);
        }

        private bool ScrollAbilityChoices(int rowDelta)
        {
            if (rowDelta == 0 || _abilityScrollRoot == null || _abilityScrollViewModel == null)
                return false;

            int nextOffset = ClampAbilityScrollOffset(
                _abilityScrollOffset + rowDelta,
                _abilityScrollViewModel.Abilities);
            if (nextOffset == _abilityScrollOffset)
                return false;

            _abilityScrollOffset = nextOffset;
            BindAbilities(_abilityScrollRoot, _abilityScrollViewModel);
            ConfigureFocus(_abilityScrollRoot, _abilityScrollViewModel);
            return true;
        }

        private static int ClampAbilityScrollOffset(
            int offset,
            IReadOnlyList<CharacterTestFairyGuiAbilityChoiceViewModel> abilities)
        {
            int maxOffset = Mathf.Max(0, CountVisibleAbilities(abilities) - AbilityVisibleRowCount);
            return Mathf.Clamp(offset, 0, maxOffset);
        }

        private static int CountVisibleAbilities(
            IReadOnlyList<CharacterTestFairyGuiAbilityChoiceViewModel> abilities)
        {
            int count = 0;
            for (int i = 0; abilities != null && i < abilities.Count; i++)
            {
                CharacterTestFairyGuiAbilityChoiceViewModel ability = abilities[i];
                if (ability != null && ability.Visible)
                    count++;
            }

            return count;
        }

        private static CharacterTestFairyGuiAbilityChoiceViewModel GetVisibleAbilityAt(
            IReadOnlyList<CharacterTestFairyGuiAbilityChoiceViewModel> abilities,
            int visibleIndex)
        {
            if (visibleIndex < 0)
                return null;

            int current = 0;
            for (int i = 0; abilities != null && i < abilities.Count; i++)
            {
                CharacterTestFairyGuiAbilityChoiceViewModel ability = abilities[i];
                if (ability == null || !ability.Visible)
                    continue;

                if (current == visibleIndex)
                    return ability;

                current++;
            }

            return null;
        }

        private void ConfigureFocus(
            Fgui.GComponent root,
            CharacterTestFairyGuiAbilityBindingPanelViewModel viewModel)
        {
            var names = new List<string>();
            string defaultFocus = CharacterTestFairyGuiAbilityBindingPanelIds.SlotButton(1);
            IReadOnlyList<CharacterTestFairyGuiAbilityBindingSlotViewModel> slots = viewModel.Slots;
            for (int i = 1; i <= CharacterTestAbilityHotbarRuntime.SlotCount; i++)
            {
                names.Add(CharacterTestFairyGuiAbilityBindingPanelIds.SlotButton(i));
                if (slots != null
                    && i - 1 < slots.Count
                    && slots[i - 1] != null
                    && slots[i - 1].Selected)
                {
                    defaultFocus = CharacterTestFairyGuiAbilityBindingPanelIds.SlotButton(i);
                }
            }

            IReadOnlyList<CharacterTestFairyGuiAbilityChoiceViewModel> abilities = viewModel.Abilities;
            int visibleAbilityCount = CountVisibleAbilities(abilities);
            int visibleRows = Mathf.Min(AbilityVisibleRowCount, Mathf.Max(0, visibleAbilityCount - _abilityScrollOffset));
            for (int i = 0; i < visibleRows; i++)
            {
                names.Add(CharacterTestFairyGuiAbilityBindingPanelIds.AbilityButton(i));
            }

            names.Add(CharacterTestFairyGuiAbilityBindingPanelIds.Clear);
            names.Add(CharacterTestFairyGuiAbilityBindingPanelIds.ResetDefaults);
            names.Add(CharacterTestFairyGuiAbilityBindingPanelIds.Save);
            names.Add(CharacterTestFairyGuiAbilityBindingPanelIds.Cancel);

            MxFairyGuiFocusNavigation.Configure(
                root,
                new MxFairyGuiFocusNavigationMetadata(
                    _viewId,
                    defaultFocus,
                    names));
        }

        private static void ApplyFullScreenRootSize(Fgui.GComponent root)
        {
            if (root == null)
                return;

            root.SetSize(ResolveRootWidth(root), ResolveRootHeight(root));
        }

        private static float ResolveRootWidth(Fgui.GComponent root)
        {
            return MxFairyGuiStageLayout.ResolveStageWidth(root, DesignWidth);
        }

        private static float ResolveRootHeight(Fgui.GComponent root)
        {
            return MxFairyGuiStageLayout.ResolveStageHeight(root, DesignHeight);
        }
    }
}
