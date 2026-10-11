using System.Collections.Generic;
using UnityEngine;
using Fgui = global::FairyGUI;

namespace MxFramework.Game.CharacterTest
{
    public sealed partial class CharacterTestFairyGuiSettingsPanelBinder
    {
        private Fgui.GComponent _layoutRoot;
        private readonly Dictionary<Fgui.GObject, Rect> _settingsBounds = new Dictionary<Fgui.GObject, Rect>();
        private Fgui.GList _scrollList;
        private bool _scrollDragging;

        private static string FormatSettingsBinding(string value)
        {
            if (!CharacterTestFairyGuiFonts.CurrentLocale.StartsWith("zh", System.StringComparison.Ordinal)) return value ?? string.Empty;
            switch (value)
            {
                case "Left Button": return "鼠标左键";
                case "Right Button": return "鼠标右键";
                case "Middle Button": return "鼠标中键";
                case "—": case "-": case "": case null: return "未绑定";
                default: return value;
            }
        }

        private void ConfigureInputScrollIndicator(Fgui.GComponent root, Fgui.GList list)
        {
            Fgui.GObject track = root.GetChild("inputScrollTrack");
            Fgui.GObject thumb = root.GetChild("inputScrollThumb");
            if (track == null || thumb == null || list.scrollPane == null) return;
            if (_scrollList != list)
            {
                _scrollList = list;
                _scrollDragging = false;
                list.scrollPane.onScroll.Add(() => UpdateInputScrollIndicator(list, track, thumb));
                track.onTouchBegin.Add(context =>
                {
                    _scrollDragging = true;
                    context.CaptureTouch();
                    list.scrollPane.SetPercY(Mathf.Clamp01(track.GlobalToLocal(Fgui.Stage.inst.touchPosition).y / track.height), false);
                });
                track.onTouchMove.Add(() =>
                {
                    if (_scrollDragging)
                        list.scrollPane.SetPercY(Mathf.Clamp01(track.GlobalToLocal(Fgui.Stage.inst.touchPosition).y / track.height), false);
                });
                track.onTouchEnd.Add(() => _scrollDragging = false);
            }
            list.EnsureBoundsCorrect();
            UpdateInputScrollIndicator(list, track, thumb);
        }

        private static void UpdateInputScrollIndicator(Fgui.GList list, Fgui.GObject track, Fgui.GObject thumb)
        {
            if (list.isDisposed || track.isDisposed || thumb.isDisposed) return;
            float fraction = list.scrollPane.viewHeight / Mathf.Max(1f, list.scrollPane.contentHeight);
            track.visible = list.visible && fraction < 1f;
            thumb.visible = track.visible;
            thumb.touchable = false;
            float height = Mathf.Clamp(track.height * fraction, 28f, track.height);
            thumb.SetSize(track.width, height);
            thumb.SetScale(track.scaleX, track.scaleY);
            thumb.SetXY(track.x, track.y + (track.height - height) * list.scrollPane.percY * track.scaleY);
        }

        private void ApplyScaledSettingsLayout(Fgui.GComponent root, float width, float height)
        {
            if (_layoutRoot != root)
            {
                _settingsBounds.Clear();
                _layoutRoot = root;
                for (int i = 0; i < root.numChildren; i++)
                {
                    Fgui.GObject child = root.GetChildAt(i);
                    if (child.name != "scrim")
                        _settingsBounds.Add(child, new Rect(child.x, child.y, child.width, child.height));
                }
            }
            float scale = Mathf.Min(width / 1440f, height / 900f);
            float x = (width - DesignWidth * scale) * 0.5f;
            float y = (height - DesignHeight * scale) * 0.5f;
            foreach (var item in _settingsBounds)
            {
                if (item.Key.isDisposed) continue;
                item.Key.SetSize(item.Value.width, item.Value.height);
                item.Key.SetScale(scale, scale);
                item.Key.SetXY(x + item.Value.x * scale, y + item.Value.y * scale);
            }
        }

        private static void StyleFrostedButton(Fgui.GButton button)
        {
            if (button == null) return;
            foreach (string name in FrostedOverlayNames)
            {
                Fgui.GObject overlay = button.GetChild(name);
                if (overlay != null) { overlay.alpha = 0f; overlay.touchable = false; }
            }
            bool navigation = button.name.StartsWith("btnNav", System.StringComparison.Ordinal);
            if (button.GetChild("base")?.asImage is Fgui.GImage normal)
            {
                normal.alpha = navigation && !button.selected ? 0f : 1f;
                normal.color = Color.white;
            }
            if (button.GetChild("overBase")?.asImage is Fgui.GImage hover)
                hover.color = new Color32(201, 235, 232, 255);
            if (button.GetChild("downBase")?.asImage is Fgui.GImage down)
                down.color = new Color32(165, 218, 210, 255);
            Fgui.GTextField title = button.GetTextField();
            if (title != null) title.color = button.enabled ? BindingTextColor : DisabledBindingTextColor;
            Fgui.GLoader icon = button.GetChild("icon")?.asLoader;
            if (icon != null && !string.IsNullOrEmpty(icon.url))
            {
                float size = navigation ? 34f : 22f;
                icon.SetSize(size, size);
                icon.SetXY(navigation ? 18f : 14f, (button.height - size) * 0.5f);
                icon.color = navigation ? Color.white : BindingTextColor;
                if (title != null) { title.x = navigation ? 58f : 44f; title.width = button.width - title.x - 12f; }
            }
        }

        private static readonly string[] FrostedOverlayNames = { "overOverlay", "downOverlay" };
        private static readonly string[] QuietHiddenNames =
        {
            "subtitle", "pageDescription", "sidebarBackground", "graphicsDivider", "audioDivider",
            "graphicsHeader", "audioHeader", "controlsHeader"
        };

        private static void ApplyFrostedTheme(Fgui.GComponent root, CharacterTestFairyGuiSettingsPanelViewModel model)
        {
            foreach (string name in QuietHiddenNames)
                if (root.GetChild(name) is Fgui.GObject child) child.visible = false;
            for (int i = 0; i < root.numChildren; i++)
            {
                Fgui.GObject child = root.GetChildAt(i);
                if (child is Fgui.GButton button) StyleFrostedButton(button);
                if (child is Fgui.GGraph graph)
                    graph.DrawRect(graph.width, graph.height, 0, Color.clear, graph.color);
            }
            Fgui.GTextField help = root.GetChild("inputInstruction")?.asTextField;
            if (help != null) { help.text = "?"; help.tooltips = model.InputInstruction; help.touchable = true; }
            Fgui.GObject pageTitle = root.GetChild("pageTitle");
            if (pageTitle != null) pageTitle.tooltips = model.PageDescription;
            Color badgeColor = model.Dirty ? new Color32(164, 119, 57, 255) : new Color32(71, 117, 111, 255);
            if (root.GetChild("dirtyMarker")?.asTextField is Fgui.GTextField dirty) { dirty.color = badgeColor; dirty.touchable = false; }
            if (root.GetChild("dirtyBadgeBg")?.asImage is Fgui.GImage badge)
            {
                badge.color = model.Dirty ? new Color32(241, 220, 171, 255) : new Color32(200, 229, 218, 255);
                badge.touchable = false;
            }
            if (root.GetChild("status")?.asTextField is Fgui.GTextField status)
            {
                status.tooltips = model.StatusText;
                status.visible = model.IsRebinding || (model.StatusText != "就绪" && model.StatusText != "已关闭。"
                    && model.StatusText != "有尚未应用的更改" && !string.IsNullOrEmpty(model.StatusText));
            }
            Fgui.GTextField reset = root.GetChild("btnResetInput")?.asButton?.GetTextField();
            if (reset != null) reset.text = "重置按键";
        }
    }
}
