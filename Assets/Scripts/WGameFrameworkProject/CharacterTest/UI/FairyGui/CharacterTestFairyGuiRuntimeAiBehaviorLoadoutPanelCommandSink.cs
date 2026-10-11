using System;
using MxFramework.Runtime;
using MxFramework.UI;
using MxFramework.UI.FairyGui;

namespace MxFramework.Game.CharacterTest
{
    public sealed class CharacterTestFairyGuiRuntimeAiBehaviorLoadoutPanelCommandSink : IMxUiCommandSink
    {
        private CharacterTestSliceServices _services;
        private readonly Func<bool> _closePanel;
        private readonly Func<bool> _refreshPanel;

        internal CharacterTestFairyGuiRuntimeAiBehaviorLoadoutPanelCommandSink(
            CharacterTestSliceServices services,
            Func<bool> closePanel = null,
            Func<bool> refreshPanel = null)
        {
            _services = services;
            _closePanel = closePanel;
            _refreshPanel = refreshPanel;
            ResetSelectionFromRuntime();
        }

        internal void UpdateServices(CharacterTestSliceServices services)
        {
            _services = services;
            ResetSelectionFromRuntime();
        }

        public int CommandCount { get; private set; }
        public int ApplyCount { get; private set; }
        public int SelectedEntityId { get; private set; }
        public int SelectedLoadoutId { get; private set; }
        public int SelectedVariantId { get; private set; }
        public string LastStatusMessage { get; private set; } = string.Empty;
        public MxUiCommand LastCommand { get; private set; }
        public RuntimeCommandValidationResult LastApplyResult { get; private set; }

        public void ResetSelectionFromRuntime()
        {
            SelectedEntityId = ResolveFirstSelectableEntityId();
            CharacterTestRuntimeAiBehaviorSelection active = ResolveActiveSelection(SelectedEntityId);
            if (active.HasSelection)
            {
                SelectedLoadoutId = active.LoadoutId;
                SelectedVariantId = active.VariantId;
            }
            else
            {
                CharacterTestRuntimeAiBehaviorSelection fallback =
                    _services?.Modules.RuntimeAiBehaviorLoadouts?.DefaultSelection
                    ?? CharacterTestRuntimeAiBehaviorSelection.None;
                SelectedLoadoutId = fallback.LoadoutId;
                SelectedVariantId = fallback.VariantId;
            }

            LastStatusMessage = string.Empty;
        }

        public void Enqueue(MxUiCommand command)
        {
            CommandCount++;
            LastCommand = command;

            if (command.CommandId == CharacterTestFairyGuiRuntimeAiBehaviorLoadoutPanelCommandIds.Close
                || command.CommandId == MxFairyGuiCommandIds.Cancel)
            {
                LastStatusMessage = "Closed.";
                _closePanel?.Invoke();
                return;
            }

            switch (command.CommandId)
            {
                case CharacterTestFairyGuiRuntimeAiBehaviorLoadoutPanelCommandIds.SelectEntity:
                    SelectEntity(command.Payload as CharacterTestFairyGuiRuntimeAiBehaviorLoadoutPanelPayload);
                    break;
                case CharacterTestFairyGuiRuntimeAiBehaviorLoadoutPanelCommandIds.SelectLoadout:
                    SelectLoadout(command.Payload as CharacterTestFairyGuiRuntimeAiBehaviorLoadoutPanelPayload);
                    break;
                case CharacterTestFairyGuiRuntimeAiBehaviorLoadoutPanelCommandIds.ApplySelection:
                    ApplySelection();
                    break;
                case CharacterTestFairyGuiRuntimeAiBehaviorLoadoutPanelCommandIds.Refresh:
                    LastStatusMessage = "Refreshed.";
                    _refreshPanel?.Invoke();
                    break;
                default:
                    LastStatusMessage = "Unsupported strategy panel command: " + command.CommandId;
                    break;
            }
        }

        private void SelectEntity(CharacterTestFairyGuiRuntimeAiBehaviorLoadoutPanelPayload payload)
        {
            int entityId = payload != null ? payload.EntityId : 0;
            if (!IsSelectableEntity(entityId))
            {
                LastStatusMessage = "Selected unit is not available.";
                return;
            }

            SelectedEntityId = entityId;
            CharacterTestRuntimeAiBehaviorSelection active = ResolveActiveSelection(entityId);
            if (active.HasSelection)
            {
                SelectedLoadoutId = active.LoadoutId;
                SelectedVariantId = active.VariantId;
            }

            LastStatusMessage = "Unit selected.";
        }

        private void SelectLoadout(CharacterTestFairyGuiRuntimeAiBehaviorLoadoutPanelPayload payload)
        {
            int loadoutId = payload != null ? payload.LoadoutId : 0;
            int variantId = payload != null ? payload.VariantId : 0;
            if (!IsAvailableLoadout(loadoutId, variantId))
            {
                LastStatusMessage = "Selected strategy is not available.";
                return;
            }

            SelectedLoadoutId = loadoutId;
            SelectedVariantId = variantId;
            LastStatusMessage = "Strategy selected.";
        }

        private void ApplySelection()
        {
            ApplyCount++;
            if (_services == null || _services.RuntimeCommands == null)
            {
                LastStatusMessage = "Runtime command module is unavailable.";
                return;
            }

            if (!_services.RuntimeCommands.RuntimeAiBehaviorSelectionUnlocked)
            {
                LastStatusMessage = "Selection phase is locked.";
                return;
            }

            if (!IsSelectableEntity(SelectedEntityId))
            {
                LastStatusMessage = "No selectable AI unit.";
                return;
            }

            if (!IsAvailableLoadout(SelectedLoadoutId, SelectedVariantId))
            {
                LastStatusMessage = "No selectable strategy.";
                return;
            }

            RuntimeCommand command = CharacterTestRuntimeCommandFactory.SetRuntimeAiBehaviorSelection(
                _services.CurrentFrame,
                CharacterTestRuntimeCommandSources.RuntimeAiBehaviorPanel,
                SelectedEntityId,
                SelectedLoadoutId,
                SelectedVariantId,
                RuntimeTraceIdentity.None);
            LastApplyResult = _services.EnqueueRuntimeCommand(command);
            LastStatusMessage = LastApplyResult.Success
                ? "Strategy selection command submitted."
                : "Strategy selection command rejected: " + LastApplyResult.Error.Message;
        }

        private int ResolveFirstSelectableEntityId()
        {
            var entities = _services?.CharacterEntities;
            for (int i = 0; entities != null && i < entities.Count; i++)
            {
                if (entities[i] != null
                    && entities[i].Control.Owner == CharacterTestCharacterControlOwner.AiControlled)
                {
                    return entities[i].Id.Value;
                }
            }

            return 0;
        }

        private bool IsSelectableEntity(int entityId)
        {
            var entities = _services?.CharacterEntities;
            for (int i = 0; entities != null && i < entities.Count; i++)
            {
                CharacterTestCharacterEntity entity = entities[i];
                if (entity != null
                    && entity.Id.Value == entityId
                    && entity.Control.Owner == CharacterTestCharacterControlOwner.AiControlled)
                {
                    return true;
                }
            }

            return false;
        }

        private CharacterTestRuntimeAiBehaviorSelection ResolveActiveSelection(int entityId)
        {
            var entities = _services?.CharacterEntities;
            for (int i = 0; entities != null && i < entities.Count; i++)
            {
                CharacterTestCharacterEntity entity = entities[i];
                if (entity != null && entity.Id.Value == entityId)
                    return entity.Control.RuntimeAiBehaviorSelection;
            }

            return CharacterTestRuntimeAiBehaviorSelection.None;
        }

        private bool IsAvailableLoadout(int loadoutId, int variantId)
        {
            CharacterTestRuntimeAiPlannerBehaviorLoadoutRuntime runtime =
                _services?.Modules.RuntimeAiBehaviorLoadouts;
            return runtime != null
                && runtime.TryResolveSelection(
                    loadoutId,
                    variantId,
                    out _,
                    out _);
        }
    }
}
