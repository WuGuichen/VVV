using UnityEngine;
using Fgui = global::FairyGUI;

namespace MxFramework.UI.FairyGui
{
    public sealed partial class MxPlayerGuidanceCardBinder
    {
        private static readonly string[] VisualNames =
        {
            MxPlayerGuidanceFairyGuiIds.CardChrome,
            MxPlayerGuidanceFairyGuiIds.HeaderIcon,
            MxPlayerGuidanceFairyGuiIds.Title,
            MxPlayerGuidanceFairyGuiIds.ActionIcon,
            MxPlayerGuidanceFairyGuiIds.ActionLabel,
            MxPlayerGuidanceFairyGuiIds.ActionBody,
            MxPlayerGuidanceFairyGuiIds.FocusIcon,
            MxPlayerGuidanceFairyGuiIds.FocusLabel,
            MxPlayerGuidanceFairyGuiIds.FocusBody,
            MxPlayerGuidanceFairyGuiIds.StatusIcon,
            MxPlayerGuidanceFairyGuiIds.StatusLabel,
            MxPlayerGuidanceFairyGuiIds.StatusBody
        };

        partial void OnAfterGeneratedBind(
            Fgui.GComponent root,
            MxFairyGuiGuidanceCardViewModel viewModel)
        {
            root.SetSize(
                MxFairyGuiStageLayout.ResolveStageWidth(root, 1920f),
                MxFairyGuiStageLayout.ResolveStageHeight(root, 1080f));
            DisableInputRecursively(root);
            SetVisualsVisible(root, viewModel.Visible);
            ApplyLayout(root, viewModel.LayoutPreset);
            ApplyGlyph(root, MxPlayerGuidanceFairyGuiIds.HeaderIcon, viewModel.HeaderGlyph);
            ApplyGlyph(root, MxPlayerGuidanceFairyGuiIds.ActionIcon, viewModel.ActionGlyph);
            ApplyGlyph(root, MxPlayerGuidanceFairyGuiIds.FocusIcon, viewModel.FocusGlyph);
            ApplyGlyph(root, MxPlayerGuidanceFairyGuiIds.StatusIcon, viewModel.StatusGlyph);
            ApplyLineTone(
                root,
                MxPlayerGuidanceFairyGuiIds.ActionIcon,
                MxPlayerGuidanceFairyGuiIds.ActionLabel,
                MxPlayerGuidanceFairyGuiIds.ActionBody,
                viewModel.ActionTone);
            ApplyLineTone(
                root,
                MxPlayerGuidanceFairyGuiIds.FocusIcon,
                MxPlayerGuidanceFairyGuiIds.FocusLabel,
                MxPlayerGuidanceFairyGuiIds.FocusBody,
                viewModel.FocusTone);
            ApplyLineTone(
                root,
                MxPlayerGuidanceFairyGuiIds.StatusIcon,
                MxPlayerGuidanceFairyGuiIds.StatusLabel,
                MxPlayerGuidanceFairyGuiIds.StatusBody,
                viewModel.StatusTone);
        }

        private static void SetVisualsVisible(Fgui.GComponent root, bool visible)
        {
            for (int i = 0; i < VisualNames.Length; i++)
            {
                Fgui.GObject child = root.GetChild(VisualNames[i]);
                if (child != null)
                    child.visible = visible;
            }
        }

        private static void ApplyLayout(
            Fgui.GComponent root,
            MxFairyGuiGuidanceLayoutPreset preset)
        {
            float yOffset = preset == MxFairyGuiGuidanceLayoutPreset.TopCornersOccupied
                ? 208f
                : 0f;
            SetY(root, MxPlayerGuidanceFairyGuiIds.CardChrome, 36f + yOffset);
            SetY(root, MxPlayerGuidanceFairyGuiIds.HeaderIcon, 54f + yOffset);
            SetY(root, MxPlayerGuidanceFairyGuiIds.Title, 56f + yOffset);
            SetY(root, MxPlayerGuidanceFairyGuiIds.ActionIcon, 124f + yOffset);
            SetY(root, MxPlayerGuidanceFairyGuiIds.ActionLabel, 126f + yOffset);
            SetY(root, MxPlayerGuidanceFairyGuiIds.ActionBody, 126f + yOffset);
            SetY(root, MxPlayerGuidanceFairyGuiIds.FocusIcon, 190f + yOffset);
            SetY(root, MxPlayerGuidanceFairyGuiIds.FocusLabel, 192f + yOffset);
            SetY(root, MxPlayerGuidanceFairyGuiIds.FocusBody, 192f + yOffset);
            SetY(root, MxPlayerGuidanceFairyGuiIds.StatusIcon, 256f + yOffset);
            SetY(root, MxPlayerGuidanceFairyGuiIds.StatusLabel, 258f + yOffset);
            SetY(root, MxPlayerGuidanceFairyGuiIds.StatusBody, 258f + yOffset);
        }

        private static void ApplyLineTone(
            Fgui.GComponent root,
            string iconName,
            string labelName,
            string bodyName,
            MxUiGuidanceTone tone)
        {
            Color color = MxFairyGuiGuidanceVisuals.ToneColor(tone);
            Fgui.GImage icon = root.GetChild(iconName)?.asImage;
            if (icon != null)
                icon.color = color;
            Fgui.GTextField label = root.GetChild(labelName)?.asTextField;
            if (label != null)
                label.color = color;
            Fgui.GTextField body = root.GetChild(bodyName)?.asTextField;
            if (body != null)
            {
                body.color = tone == MxUiGuidanceTone.Neutral
                    ? MxFairyGuiGuidanceVisuals.MutedColor
                    : color;
            }
        }

        private static void ApplyGlyph(
            Fgui.GComponent root,
            string childName,
            MxUiGuidanceGlyph glyph)
        {
            Fgui.GImage icon = root.GetChild(childName)?.asImage;
            if (icon != null)
                icon.texture = MxFairyGuiGuidanceVisuals.ResolveGlyphTexture(glyph);
        }

        private static void SetY(Fgui.GComponent root, string childName, float y)
        {
            Fgui.GObject child = root.GetChild(childName);
            if (child != null)
                child.y = y;
        }

        private static void DisableInputRecursively(Fgui.GComponent component)
        {
            if (component == null)
                return;

            component.touchable = false;
            for (int i = 0; i < component.numChildren; i++)
            {
                Fgui.GObject child = component.GetChildAt(i);
                if (child == null)
                    continue;
                child.touchable = false;
                DisableInputRecursively(child.asCom);
            }
        }
    }
}
