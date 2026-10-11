using System.Collections.Generic;
using MxFramework.UI;
using MxFramework.UI.FairyGui;
using UnityEngine;
using Fgui = global::FairyGUI;

namespace MxFramework.Game.CharacterTest
{
    public sealed partial class CharacterTestFairyGuiRuntimeAiBehaviorLoadoutPanelBinder
    {
        private const float DesignWidth = 1920f;
        private const float DesignHeight = 1080f;

        partial void OnAfterGeneratedBind(
            Fgui.GComponent root,
            CharacterTestFairyGuiRuntimeAiBehaviorLoadoutPanelViewModel viewModel)
        {
            ApplyFullScreenRootSize(root);
            BindEntities(root, viewModel);
            BindLoadouts(root, viewModel);
            ConfigureFocus(root, viewModel);
        }

        private void BindEntities(
            Fgui.GComponent root,
            CharacterTestFairyGuiRuntimeAiBehaviorLoadoutPanelViewModel viewModel)
        {
            IReadOnlyList<CharacterTestFairyGuiRuntimeAiBehaviorEntityViewModel> entities =
                viewModel.Entities;
            for (int i = 0; i < CharacterTestFairyGuiRuntimeAiBehaviorLoadoutPanelIds.EntityRowCount; i++)
            {
                CharacterTestFairyGuiRuntimeAiBehaviorEntityViewModel entity =
                    entities != null && i < entities.Count ? entities[i] : null;
                Fgui.GButton button =
                    root.GetChild(CharacterTestFairyGuiRuntimeAiBehaviorLoadoutPanelIds.EntityButton(i))?.asButton;
                if (button == null)
                    continue;

                bool visible = entity != null && entity.Visible;
                button.visible = visible;
                button.enabled = visible;
                button.grayed = !visible;
                button.selected = visible && entity.Selected;
                button.text = visible ? entity.Label : string.Empty;
                int entityId = visible ? entity.EntityId : 0;
                button.onClick.Set(() =>
                {
                    if (entityId <= 0)
                        return;

                    _commandSink?.Enqueue(new MxUiCommand(
                        _viewId,
                        CharacterTestFairyGuiRuntimeAiBehaviorLoadoutPanelCommandIds.SelectEntity,
                        new CharacterTestFairyGuiRuntimeAiBehaviorLoadoutPanelPayload
                        {
                            EntityId = entityId
                        }));
                });
            }
        }

        private void BindLoadouts(
            Fgui.GComponent root,
            CharacterTestFairyGuiRuntimeAiBehaviorLoadoutPanelViewModel viewModel)
        {
            IReadOnlyList<CharacterTestFairyGuiRuntimeAiBehaviorLoadoutCardViewModel> loadouts =
                viewModel.Loadouts;
            for (int i = 0; i < CharacterTestFairyGuiRuntimeAiBehaviorLoadoutPanelIds.LoadoutCardCount; i++)
            {
                CharacterTestFairyGuiRuntimeAiBehaviorLoadoutCardViewModel card =
                    loadouts != null && i < loadouts.Count ? loadouts[i] : null;
                Fgui.GButton button =
                    root.GetChild(CharacterTestFairyGuiRuntimeAiBehaviorLoadoutPanelIds.LoadoutButton(i))?.asButton;
                if (button == null)
                    continue;

                bool visible = card != null && card.Visible;
                button.visible = visible;
                button.enabled = visible;
                button.grayed = !visible;
                button.selected = visible && card.Selected;
                button.text = visible
                    ? card.Label + (card.ActiveOnSelectedEntity ? "  ACTIVE" : string.Empty)
                    : string.Empty;
                int loadoutId = visible ? card.LoadoutId : 0;
                int variantId = visible ? card.VariantId : 0;
                button.onClick.Set(() =>
                {
                    if (loadoutId <= 0 || variantId <= 0)
                        return;

                    _commandSink?.Enqueue(new MxUiCommand(
                        _viewId,
                        CharacterTestFairyGuiRuntimeAiBehaviorLoadoutPanelCommandIds.SelectLoadout,
                        new CharacterTestFairyGuiRuntimeAiBehaviorLoadoutPanelPayload
                        {
                            LoadoutId = loadoutId,
                            VariantId = variantId
                        }));
                });
            }
        }

        private void ConfigureFocus(
            Fgui.GComponent root,
            CharacterTestFairyGuiRuntimeAiBehaviorLoadoutPanelViewModel viewModel)
        {
            var names = new List<string>();
            string defaultFocus = CharacterTestFairyGuiRuntimeAiBehaviorLoadoutPanelIds.Close;
            IReadOnlyList<CharacterTestFairyGuiRuntimeAiBehaviorEntityViewModel> entities = viewModel.Entities;
            for (int i = 0; i < CharacterTestFairyGuiRuntimeAiBehaviorLoadoutPanelIds.EntityRowCount; i++)
            {
                CharacterTestFairyGuiRuntimeAiBehaviorEntityViewModel entity =
                    entities != null && i < entities.Count ? entities[i] : null;
                if (entity == null || !entity.Visible)
                    continue;

                string name = CharacterTestFairyGuiRuntimeAiBehaviorLoadoutPanelIds.EntityButton(i);
                names.Add(name);
                if (entity.Selected)
                    defaultFocus = name;
            }

            IReadOnlyList<CharacterTestFairyGuiRuntimeAiBehaviorLoadoutCardViewModel> loadouts =
                viewModel.Loadouts;
            for (int i = 0; i < CharacterTestFairyGuiRuntimeAiBehaviorLoadoutPanelIds.LoadoutCardCount; i++)
            {
                CharacterTestFairyGuiRuntimeAiBehaviorLoadoutCardViewModel card =
                    loadouts != null && i < loadouts.Count ? loadouts[i] : null;
                if (card == null || !card.Visible)
                    continue;

                string name = CharacterTestFairyGuiRuntimeAiBehaviorLoadoutPanelIds.LoadoutButton(i);
                names.Add(name);
                if (card.Selected)
                    defaultFocus = name;
            }

            names.Add(CharacterTestFairyGuiRuntimeAiBehaviorLoadoutPanelIds.Apply);
            names.Add(CharacterTestFairyGuiRuntimeAiBehaviorLoadoutPanelIds.Refresh);
            names.Add(CharacterTestFairyGuiRuntimeAiBehaviorLoadoutPanelIds.Close);

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

            root.SetSize(
                MxFairyGuiStageLayout.ResolveStageWidth(root, DesignWidth),
                MxFairyGuiStageLayout.ResolveStageHeight(root, DesignHeight));
        }
    }
}
