using System;
using System.Collections.Generic;
using MxFramework.UI;
using MxFramework.UI.FairyGui;

namespace MxFramework.Game.CharacterTest
{
    public sealed class CharacterTestFairyGuiAbilityBindingPanelCommandSink : IMxUiCommandSink
    {
        private readonly CharacterTestAbilityHotbarRuntime _runtime;
        private readonly Func<bool> _closePanel;
        private CharacterTestAbilityHotbarSlotBinding[] _workingBindings =
            Array.Empty<CharacterTestAbilityHotbarSlotBinding>();

        public CharacterTestFairyGuiAbilityBindingPanelCommandSink(
            CharacterTestAbilityHotbarRuntime runtime,
            Func<bool> closePanel = null)
        {
            _runtime = runtime;
            _closePanel = closePanel;
            ResetWorkingFromRuntime();
        }

        public int CommandCount { get; private set; }
        public int SaveCount { get; private set; }
        public int CancelCount { get; private set; }
        public int SelectedSlot { get; private set; } = CharacterTestAbilityHotbarRuntime.FirstSlotNumber;
        public bool Dirty { get; private set; }
        public string LastStatusMessage { get; private set; } = string.Empty;
        public MxUiCommand LastCommand { get; private set; }
        public IReadOnlyList<CharacterTestAbilityHotbarSlotBinding> WorkingBindings => _workingBindings;

        public void ResetWorkingFromRuntime()
        {
            _workingBindings = _runtime != null
                ? _runtime.CreateBindingSnapshot()
                : Array.Empty<CharacterTestAbilityHotbarSlotBinding>();
            SelectedSlot = CharacterTestAbilityHotbarRuntime.FirstSlotNumber;
            Dirty = false;
            LastStatusMessage = string.Empty;
        }

        public void Enqueue(MxUiCommand command)
        {
            CommandCount++;
            LastCommand = command;

            if (command.CommandId == CharacterTestFairyGuiAbilityBindingPanelCommandIds.Cancel
                || command.CommandId == MxFairyGuiCommandIds.Cancel)
            {
                CancelCount++;
                LastStatusMessage = "Canceled.";
                _closePanel?.Invoke();
                return;
            }

            if (_runtime == null)
            {
                LastStatusMessage = "Ability hotbar unavailable.";
                return;
            }

            switch (command.CommandId)
            {
                case CharacterTestFairyGuiAbilityBindingPanelCommandIds.SelectSlot:
                    SelectSlot(command.Payload as CharacterTestFairyGuiAbilityBindingPanelPayload);
                    break;
                case CharacterTestFairyGuiAbilityBindingPanelCommandIds.SelectAbility:
                    SelectAbility(command.Payload as CharacterTestFairyGuiAbilityBindingPanelPayload);
                    break;
                case CharacterTestFairyGuiAbilityBindingPanelCommandIds.ClearSlot:
                    SetSelectedSlotAbility(CharacterTestAbilityHotbarRuntime.EmptyAbilityId);
                    LastStatusMessage = "Slot cleared.";
                    break;
                case CharacterTestFairyGuiAbilityBindingPanelCommandIds.ResetDefaults:
                    ResetDefaults();
                    break;
                case CharacterTestFairyGuiAbilityBindingPanelCommandIds.Save:
                    Save();
                    break;
                default:
                    LastStatusMessage = "Unsupported ability binding command: " + command.CommandId;
                    break;
            }
        }

        private void SelectSlot(CharacterTestFairyGuiAbilityBindingPanelPayload payload)
        {
            int slotNumber = payload != null ? payload.SlotNumber : 0;
            if (!CharacterTestAbilityHotbarRuntime.IsValidSlot(slotNumber))
            {
                LastStatusMessage = "Invalid slot.";
                return;
            }

            SelectedSlot = slotNumber;
            LastStatusMessage = "Slot " + slotNumber + " selected.";
        }

        private void SelectAbility(CharacterTestFairyGuiAbilityBindingPanelPayload payload)
        {
            int abilityId = payload != null ? payload.AbilityId : 0;
            if (abilityId <= 0)
            {
                LastStatusMessage = "Invalid ability.";
                return;
            }

            SetSelectedSlotAbility(abilityId);
            LastStatusMessage = "Ability assigned.";
        }

        private void SetSelectedSlotAbility(int abilityId)
        {
            if (!CharacterTestAbilityHotbarRuntime.IsValidSlot(SelectedSlot))
                SelectedSlot = CharacterTestAbilityHotbarRuntime.FirstSlotNumber;

            EnsureWorkingBindings();
            _workingBindings[SelectedSlot - 1] =
                new CharacterTestAbilityHotbarSlotBinding(SelectedSlot, abilityId);
            Dirty = true;
        }

        private void ResetDefaults()
        {
            int[] defaults = CharacterTestAbilityHotbarRuntime.CreateDefaultAbilityIds();
            _workingBindings = new CharacterTestAbilityHotbarSlotBinding[
                CharacterTestAbilityHotbarRuntime.SlotCount];
            for (int i = 0; i < _workingBindings.Length; i++)
                _workingBindings[i] = new CharacterTestAbilityHotbarSlotBinding(i + 1, defaults[i]);
            Dirty = true;
            LastStatusMessage = "Defaults restored.";
        }

        private void Save()
        {
            EnsureWorkingBindings();
            _runtime.ApplyBindings(_workingBindings);
            CharacterTestAbilityHotbarPersistenceResult result = _runtime.SaveToFile();
            SaveCount++;
            Dirty = !result.Success;
            if (result.Success)
                _workingBindings = _runtime.CreateBindingSnapshot();
            LastStatusMessage = result.Message;
        }

        private void EnsureWorkingBindings()
        {
            if (_workingBindings != null
                && _workingBindings.Length == CharacterTestAbilityHotbarRuntime.SlotCount)
            {
                return;
            }

            _workingBindings = _runtime != null
                ? _runtime.CreateBindingSnapshot()
                : new CharacterTestAbilityHotbarSlotBinding[CharacterTestAbilityHotbarRuntime.SlotCount];
        }
    }
}
