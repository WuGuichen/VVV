using System;
using System.Collections.Generic;
using MxFramework.UI.FairyGui;
using UnityEngine;
using Fgui = global::FairyGUI;

namespace MxFramework.Game.CharacterTest
{
    internal sealed class CharacterTestFairyGuiCardTooltip : IDisposable
    {
        private sealed class Binding { internal Fgui.EventCallback0 Enter, Leave; }
        private readonly Dictionary<Fgui.GObject, Binding> _bindings = new();
        private readonly MxFairyGuiTooltipService _fallback;
        private Fgui.GComponent _window;
        private Fgui.GObject _activeTarget;
        internal CharacterTestFairyGuiCardTooltip(bool frosted)
        { if (!frosted) _fallback = new MxFairyGuiTooltipService(); }
        internal void Bind(Fgui.GObject target, string text, MxFairyGuiTooltipOptions options)
        {
            if (_fallback != null) { _fallback.Bind(target, text, options); return; }
            if (_bindings.TryGetValue(target, out var old))
            { target.onRollOver.Remove(old.Enter); target.onRollOut.Remove(old.Leave); }
            if (string.IsNullOrWhiteSpace(text))
            {
                _bindings.Remove(target);
                if (_activeTarget == target) Hide();
                return;
            }
            var binding = new Binding { Enter = () => Show(target, text, options), Leave = Hide };
            _bindings[target] = binding; target.onRollOver.Add(binding.Enter); target.onRollOut.Add(binding.Leave);
        }
        private void Show(Fgui.GObject target, string text, MxFairyGuiTooltipOptions options)
        {
            if (target.isDisposed || string.IsNullOrWhiteSpace(text)) return;
            _window ??= Fgui.UIPackage.CreateObject("MxCharacterTestTacticalDeckUi", "TacticalPreparationHint").asCom;
            _window.name = "characterTestFrostedTooltip";
            _window.touchable = false; _window.sortingOrder = 10000;
            foreach (var child in _window.GetChildren()) child.touchable = false;
            var root = Fgui.GRoot.inst;
            var label = _window.GetChild("label").asTextField;
            label.autoSize = Fgui.AutoSizeType.Both; label.text = text;
            float maximum = Mathf.Min(options.MaximumTextWidth > 0 ? options.MaximumTextWidth : 360, root.width - 64);
            float minimum = Mathf.Min(options.MinimumTextWidth > 0 ? options.MinimumTextWidth : 96, maximum);
            float textWidth = Mathf.Clamp(Mathf.Ceil(label.textWidth), minimum, maximum);
            label.autoSize = Fgui.AutoSizeType.Height; label.SetXY(16,12);
            label.SetSize(textWidth, label.height); label.height = Mathf.Ceil(label.textHeight);
            float width = textWidth + 32, height = Mathf.Max(44,label.height+24);
            _window.SetSize(width,height);
            _window.GetChild("background").SetXY(-4,-4); _window.GetChild("background").SetSize(width+8,height+8);
            var top = root.GlobalToLocal(target.LocalToGlobal(Vector2.zero));
            var bottom = root.GlobalToLocal(target.LocalToGlobal(new Vector2(target.width,target.height)));
            float gap = options.Gap > 0 ? options.Gap : 12;
            bool fitsAbove = top.y - height - gap >= 16;
            bool fitsBelow = bottom.y + gap + height <= root.height - 16;
            bool below = options.PreferredPlacement == MxFairyGuiTooltipPlacement.Below
                ? fitsBelow || !fitsAbove : !fitsAbove && fitsBelow;
            float y = below ? bottom.y + gap : top.y - height - gap;
            _window.SetXY(Mathf.Clamp(top.x,16,Mathf.Max(16,root.width-width-16)),
                Mathf.Clamp(y,16,Mathf.Max(16,root.height-height-16)));
            _activeTarget = target;
            root.AddChild(_window); _window.visible = true;
        }
        internal void Hide() { _fallback?.Hide(); if (_window != null) _window.visible = false; _activeTarget = null; }
        public void Dispose()
        {
            foreach (var pair in _bindings) if (!pair.Key.isDisposed)
            { pair.Key.onRollOver.Remove(pair.Value.Enter); pair.Key.onRollOut.Remove(pair.Value.Leave); }
            _bindings.Clear(); _window?.Dispose(); _window = null; _fallback?.Dispose();
        }
    }
}
