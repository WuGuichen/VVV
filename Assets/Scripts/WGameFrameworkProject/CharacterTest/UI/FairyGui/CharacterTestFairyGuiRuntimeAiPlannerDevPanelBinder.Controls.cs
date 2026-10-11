using System;
using System.Collections.Generic;
using MxFramework.UI;
using MxFramework.UI.FairyGui;
using UnityEngine;
using Fgui = global::FairyGUI;
using static MxFramework.Game.CharacterTest.CharacterTestFairyGuiRuntimeAiLabTimelineWorkspaceBinder;
using static MxFramework.Game.CharacterTest.CharacterTestFairyGuiRuntimeAiLabVisualBinding;

namespace MxFramework.Game.CharacterTest
{
    public sealed partial class CharacterTestFairyGuiRuntimeAiPlannerDevPanelBinder
    {
        internal void BindTooltip(
            Fgui.GObject target,
            string text,
            MxFairyGuiTooltipOptions options = default)
        {
            if (_panelOverlayBinder.TooltipsSuppressed)
            {
                _rootSession.UnbindTooltip(target);
                return;
            }
            _rootSession.BindTooltip(target, text, options);
        }

        internal void BindButton(
            Fgui.GComponent root,
            string childName,
            string label,
            bool enabled,
            string commandId,
            bool selected = false,
            bool checkMode = false,
            object payload = null)
        {
            Fgui.GButton button = root.GetChild(childName)?.asButton;
            if (button == null)
                return;

            if (checkMode)
                button.mode = Fgui.ButtonMode.Check;
            button.text = label ?? string.Empty;
            button.enabled = enabled;
            button.grayed = !enabled;
            button.selected = selected;
            button.visible = true;
            CenterButtonContent(button, 10f);
            button.onClick.Set(() =>
            {
                if (!enabled)
                    return;

                _commandSink?.Enqueue(new MxUiCommand(_viewId, commandId, payload));
            });
        }

        private static void HideButton(Fgui.GComponent root, string childName)
        {
            Fgui.GButton button = root.GetChild(childName)?.asButton;
            if (button == null)
                return;

            button.visible = false;
            button.enabled = false;
            button.onClick.Clear();
        }

        private void BindProjectCardMetadata(
            Fgui.GComponent root,
            CharacterTestFairyGuiRuntimeAiPlannerDevPanelViewModel viewModel)
        {
            bool shown = viewModel.ShowProjectCardMetadata
                && (viewModel.WorkspaceMode == CharacterTestRuntimeAiLabWorkspaceMode.BehaviorCard
                    || viewModel.WorkspaceMode == CharacterTestRuntimeAiLabWorkspaceMode.BehaviorGroup || viewModel.ShowBehaviorLogic);
            string[] names =
            {
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.ProjectCardMetadataPanel,
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.ProjectCardMetadataTitle,
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.ProjectCardDisplayName,
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.ProjectCardCost,
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.ProjectCardDescription,
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.ProjectCardUseSummary,
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.ProjectCardRiskSummary,
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.ProjectCardTags
            };
            for (int i = 0; i < names.Length; i++)
            {
                Fgui.GObject child = root.GetChild(names[i]);
                if (child != null)
                    child.visible = shown;
            }
            if (!shown)
                return;

            BindText(
                root,
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.ProjectCardMetadataTitle,
                viewModel.ProjectCardMetadataTitle);
            BindProjectCardMetadataInput(root, CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.ProjectCardDisplayName,
                viewModel.ProjectCardDisplayName, "显示名称", viewModel.BehaviorCardWorkspace?.Timeline?.CompositionEditor?.CanEdit ?? viewModel.CanEditProjectCardMetadata,
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.SetProjectCardDisplayName);
            BindProjectCardMetadataInput(root, CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.ProjectCardCost,
                viewModel.ProjectCardCost, "费用 0-10", viewModel.CanEditProjectCardMetadata,
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.SetProjectCardCost);
            BindProjectCardMetadataInput(root, CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.ProjectCardDescription,
                viewModel.ProjectCardDescription, "说明", viewModel.CanEditProjectCardMetadata,
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.SetProjectCardDescription);
            BindProjectCardMetadataInput(root, CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.ProjectCardUseSummary,
                viewModel.ProjectCardUseSummary, "使用建议", viewModel.CanEditProjectCardMetadata,
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.SetProjectCardUseSummary);
            BindProjectCardMetadataInput(root, CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.ProjectCardRiskSummary,
                viewModel.ProjectCardRiskSummary, "风险说明", viewModel.CanEditProjectCardMetadata,
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.SetProjectCardRiskSummary);
            BindProjectCardMetadataInput(root, CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.ProjectCardTags,
                viewModel.ProjectCardTags, "标签（逗号分隔）", viewModel.CanEditProjectCardMetadata,
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.SetProjectCardTags);
        }

        private void BindProjectCardMetadataInput(
            Fgui.GComponent root,
            string childName,
            string value,
            string prompt,
            bool enabled,
            string commandId)
        {
            Fgui.GTextInput input = root.GetChild(childName) as Fgui.GTextInput;
            if (input == null)
                return;
            if (!Application.isPlaying || !input.focused)
                input.text = value ?? string.Empty;
            input.promptText = prompt ?? string.Empty;
            input.enabled = enabled;
            input.grayed = !enabled;
            input.onFocusOut.Set(() =>
            {
                if (enabled)
                    _commandSink?.Enqueue(new MxUiCommand(_viewId, commandId, input.text));
            });
        }

        internal static void CenterButtonContent(Fgui.GButton button, float gap)
        {
            Fgui.GObject icon = button?.GetChild("icon");
            Fgui.GTextField title = button?.GetChild("title")?.asTextField;
            if (button == null || icon == null || title == null)
                return;

            const float horizontalPadding = 12f;
            bool hasIcon = !string.IsNullOrEmpty(button.icon);
            bool hasTitle = !string.IsNullOrEmpty(title.text);
            if (!hasIcon)
            {
                title.x = horizontalPadding;
                title.y = 0f;
                title.width = Mathf.Max(0f, button.width - horizontalPadding * 2f);
                title.height = button.height;
                return;
            }

            icon.y = Mathf.Round((button.height - icon.height) * 0.5f);
            if (!hasTitle)
            {
                icon.x = Mathf.Round((button.width - icon.width) * 0.5f);
                return;
            }

            float availableTextWidth = Mathf.Max(
                0f,
                button.width - icon.width - gap - horizontalPadding * 2f);
            float textWidth = Mathf.Min(
                Mathf.Ceil(title.textWidth) + 2f,
                availableTextWidth);
            float groupWidth = icon.width + gap + textWidth;
            icon.x = Mathf.Round((button.width - groupWidth) * 0.5f);
            title.x = icon.x + icon.width + gap;
            title.y = 0f;
            title.width = textWidth;
            title.height = button.height;
        }

        internal static void ApplyGeneratedRuntimeAiIcon(
            Fgui.GButton button,
            string resourceName)
        {
            if (button == null)
                return;

            button.text = string.Empty;
            button.icon = Fgui.UIPackage.GetItemURL(
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.PackageId,
                resourceName);
            Fgui.GLoader icon = button.GetChild("icon")?.asLoader;
            if (icon != null)
                icon.color = Color.white;
            CenterButtonContent(button, 0f);
        }

        internal static void BindText(Fgui.GComponent root, string childName, string value)
        {
            Fgui.GTextField text = root.GetChild(childName)?.asTextField;
            if (text != null)
                text.text = value ?? string.Empty;
        }

        private static void SetObjectVisible(Fgui.GComponent root, string childName, bool visible)
        {
            Fgui.GObject child = root.GetChild(childName);
            if (child != null)
                child.visible = visible;
        }

        private static void SetObjectSize(Fgui.GComponent root, string childName, float width, float height)
        {
            Fgui.GObject child = root.GetChild(childName);
            child?.SetSize(width, height);
        }

        private static void SetObjectWidth(Fgui.GComponent root, string childName, float width)
        {
            Fgui.GObject child = root.GetChild(childName);
            if (child != null)
                child.width = width;
        }

        private static void SetObjectPosition(
            Fgui.GComponent root,
            string childName,
            float x,
            float y)
        {
            Fgui.GObject child = root.GetChild(childName);
            if (child != null)
                child.SetPosition(x, y, child.z);
        }

        private void ResetDynamicVisualsForNewRoot(Fgui.GComponent root)
        {
            if (_rootSession.IsCurrent(root))
                return;

            _panelOverlayBinder.ResetForRoot();
            ResetRuntimeTestTransientVisuals();
            _rootSession.ResetForRoot(root);
            _timelineWorkspaceBinder.ResetForRoot(root);
        }

        private void ConfigureFocus(Fgui.GComponent root)
        {
            _rootSession.ConfigureFocus(root);
        }
    }
}
