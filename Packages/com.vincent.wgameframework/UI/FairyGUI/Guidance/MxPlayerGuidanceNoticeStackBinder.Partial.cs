using UnityEngine;
using Fgui = global::FairyGUI;

namespace MxFramework.UI.FairyGui
{
    public sealed partial class MxPlayerGuidanceNoticeStackBinder
    {
        partial void OnAfterGeneratedBind(
            Fgui.GComponent root,
            MxFairyGuiGuidanceNoticeStackViewModel viewModel)
        {
            root.SetSize(
                MxFairyGuiStageLayout.ResolveStageWidth(root, 1920f),
                MxFairyGuiStageLayout.ResolveStageHeight(root, 1080f));
            DisableInputRecursively(root);
            ApplyLayout(root, viewModel.LayoutPreset);
            BindSlot(
                root,
                MxPlayerGuidanceFairyGuiIds.Notice1Chrome,
                MxPlayerGuidanceFairyGuiIds.Notice1ToneOverlay,
                MxPlayerGuidanceFairyGuiIds.Notice1Icon,
                MxPlayerGuidanceFairyGuiIds.Notice1Text,
                viewModel.Notice1Visible,
                viewModel.Notice1Glyph,
                viewModel.Notice1Tone);
            BindSlot(
                root,
                MxPlayerGuidanceFairyGuiIds.Notice2Chrome,
                MxPlayerGuidanceFairyGuiIds.Notice2ToneOverlay,
                MxPlayerGuidanceFairyGuiIds.Notice2Icon,
                MxPlayerGuidanceFairyGuiIds.Notice2Text,
                viewModel.Notice2Visible,
                viewModel.Notice2Glyph,
                viewModel.Notice2Tone);
        }

        private static void BindSlot(
            Fgui.GComponent root,
            string chromeName,
            string overlayName,
            string iconName,
            string textName,
            bool visible,
            MxUiGuidanceGlyph glyph,
            MxUiGuidanceTone tone)
        {
            SetVisible(root, chromeName, visible);
            SetVisible(root, overlayName, visible);
            SetVisible(root, iconName, visible);
            SetVisible(root, textName, visible);
            if (!visible)
                return;

            Color toneColor = MxFairyGuiGuidanceVisuals.ToneColor(tone);
            Fgui.GGraph overlay = root.GetChild(overlayName)?.asGraph;
            if (overlay != null)
                overlay.color = MxFairyGuiGuidanceVisuals.ToneOverlayColor(tone);
            Fgui.GImage icon = root.GetChild(iconName)?.asImage;
            if (icon != null)
            {
                icon.texture = MxFairyGuiGuidanceVisuals.ResolveGlyphTexture(glyph);
                icon.color = toneColor;
            }
            Fgui.GTextField text = root.GetChild(textName)?.asTextField;
            if (text != null)
                text.color = toneColor;
        }

        private static void ApplyLayout(
            Fgui.GComponent root,
            MxFairyGuiGuidanceLayoutPreset preset)
        {
            if (preset == MxFairyGuiGuidanceLayoutPreset.TopCornersOccupied)
            {
                SetXY(root, MxPlayerGuidanceFairyGuiIds.Notice1Chrome, 700f, 82f);
                SetXY(root, MxPlayerGuidanceFairyGuiIds.Notice1ToneOverlay, 705f, 83f);
                SetXY(root, MxPlayerGuidanceFairyGuiIds.Notice1Icon, 720f, 96f);
                SetXY(root, MxPlayerGuidanceFairyGuiIds.Notice1Text, 768f, 94f);
                SetXY(root, MxPlayerGuidanceFairyGuiIds.Notice2Chrome, 700f, 154f);
                SetXY(root, MxPlayerGuidanceFairyGuiIds.Notice2ToneOverlay, 705f, 155f);
                SetXY(root, MxPlayerGuidanceFairyGuiIds.Notice2Icon, 720f, 168f);
                SetXY(root, MxPlayerGuidanceFairyGuiIds.Notice2Text, 768f, 166f);
                return;
            }

            float right = root.width - 36f;
            SetXY(root, MxPlayerGuidanceFairyGuiIds.Notice1Chrome, right - 520f, 46f);
            SetXY(root, MxPlayerGuidanceFairyGuiIds.Notice1ToneOverlay, right - 515f, 47f);
            SetXY(root, MxPlayerGuidanceFairyGuiIds.Notice1Icon, right - 500f, 60f);
            SetXY(root, MxPlayerGuidanceFairyGuiIds.Notice1Text, right - 452f, 58f);
            SetXY(root, MxPlayerGuidanceFairyGuiIds.Notice2Chrome, right - 520f, 118f);
            SetXY(root, MxPlayerGuidanceFairyGuiIds.Notice2ToneOverlay, right - 515f, 119f);
            SetXY(root, MxPlayerGuidanceFairyGuiIds.Notice2Icon, right - 500f, 132f);
            SetXY(root, MxPlayerGuidanceFairyGuiIds.Notice2Text, right - 452f, 130f);
        }

        private static void SetVisible(Fgui.GComponent root, string childName, bool visible)
        {
            Fgui.GObject child = root.GetChild(childName);
            if (child != null)
                child.visible = visible;
        }

        private static void SetXY(Fgui.GComponent root, string childName, float x, float y)
        {
            Fgui.GObject child = root.GetChild(childName);
            if (child != null)
                child.SetXY(x, y);
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

    internal static class MxFairyGuiGuidanceVisuals
    {
        internal static readonly Color MutedColor = new Color32(175, 193, 198, 255);

        internal static Color ToneColor(MxUiGuidanceTone tone)
        {
            switch (tone)
            {
                case MxUiGuidanceTone.Success:
                    return new Color32(121, 215, 101, 255);
                case MxUiGuidanceTone.Warning:
                    return new Color32(242, 188, 36, 255);
                case MxUiGuidanceTone.Error:
                    return new Color32(235, 98, 104, 255);
                case MxUiGuidanceTone.Neutral:
                    return MutedColor;
                default:
                    return new Color32(99, 217, 232, 255);
            }
        }

        internal static Color ToneOverlayColor(MxUiGuidanceTone tone)
        {
            Color color = ToneColor(tone);
            color.a = 0.14f;
            return color;
        }

        internal static Fgui.NTexture ResolveGlyphTexture(MxUiGuidanceGlyph glyph)
        {
            string itemName;
            switch (glyph)
            {
                case MxUiGuidanceGlyph.Context:
                    itemName = MxPlayerGuidanceFairyGuiIds.ContextIconResourceName;
                    break;
                case MxUiGuidanceGlyph.Focus:
                    itemName = MxPlayerGuidanceFairyGuiIds.FocusIconResourceName;
                    break;
                case MxUiGuidanceGlyph.Success:
                    itemName = MxPlayerGuidanceFairyGuiIds.SuccessIconResourceName;
                    break;
                case MxUiGuidanceGlyph.Warning:
                    itemName = MxPlayerGuidanceFairyGuiIds.WarningIconResourceName;
                    break;
                case MxUiGuidanceGlyph.Error:
                    itemName = MxPlayerGuidanceFairyGuiIds.ErrorIconResourceName;
                    break;
                default:
                    itemName = MxPlayerGuidanceFairyGuiIds.ActionIconResourceName;
                    break;
            }

            string url = Fgui.UIPackage.GetItemURL(
                MxPlayerGuidanceFairyGuiIds.PackageId,
                itemName);
            Fgui.PackageItem item = Fgui.UIPackage.GetItemByURL(url);
            return item?.owner.GetItemAsset(item) as Fgui.NTexture;
        }
    }
}
