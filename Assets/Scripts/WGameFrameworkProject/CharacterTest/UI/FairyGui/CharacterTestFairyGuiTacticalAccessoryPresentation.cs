using System;
using MxFramework.Game.CharacterTest.TacticalCard;
using UnityEngine;
using Fgui = global::FairyGUI;

namespace MxFramework.Game.CharacterTest
{
    // Projects published image/slot components only; never installs or consumes an effect.
    internal static class CharacterTestFairyGuiTacticalAccessoryPresentation
    {
        internal static string ArtworkControl(TacticalAccessoryCombatKind kind)
        {
            switch (kind)
            {
                case TacticalAccessoryCombatKind.BorrowMomentum: return "borrowMomentum";
                case TacticalAccessoryCombatKind.PushAway: return "pushAway";
                case TacticalAccessoryCombatKind.BreakMomentum: return "breakMomentum";
                default: return null;
            }
        }

        // The published marker boxes are 28/34 px. Keep a readable single glyph; full identity and fee stay in the tooltip.
        internal static string CompactLabel(string fullName)
        {
            switch (fullName) {
                case null: case "": case "空槽": return "＋";
                case "借力": return "借";
                case "推离": return "推";
                case "破势": return "破";
                case "决斗邀请": return "决";
                case "破碎幻想": return "碎";
                case "黑洞口袋": return "洞";
                case "不许回头": return "回";
                case "不杀之誓": return "誓";
                case "一拳之约": return "拳";
                case "玻璃大炮": return "璃";
                case "幸运硬币": return "币";
                case "铁王座": return "座";
                default: return "配";
            }
        }

        internal static void Bind(Fgui.GComponent slot, Fgui.NTexture texture,
            CharacterTacticalCardQuality quality, bool selected, bool interactive, string fallback = null)
        {
            var icon = slot.GetChild("icon")?.asImage
                ?? throw new InvalidOperationException("Published accessory slot is missing its icon image.");
            var surface = slot.GetChild("surface")?.asGraph
                ?? throw new InvalidOperationException("Published accessory slot is missing its surface.");
            var empty = slot.GetChild("empty")?.asTextField
                ?? throw new InvalidOperationException("Published accessory slot is missing its empty marker.");
            var marker = slot.GetChild("selected")?.asGraph
                ?? throw new InvalidOperationException("Published accessory slot is missing its selection marker.");
            slot.touchable = interactive; slot.opaque = interactive;
            icon.touchable = surface.touchable = empty.touchable = marker.touchable = false;
            icon.texture = texture; icon.visible = texture != null;
            empty.autoSize = Fgui.AutoSizeType.None;
            empty.singleLine = true; empty.align = Fgui.AlignType.Center; empty.verticalAlign = Fgui.VertAlignType.Middle;
            var format = empty.textFormat; format.size = 18; format.letterSpacing = 0; empty.textFormat = format;
            empty.text = texture == null ? CompactLabel(fallback) : "";
            empty.visible = texture == null; marker.visible = selected;
            surface.color = quality == CharacterTacticalCardQuality.Gold ? new Color32(239, 223, 160, 221)
                : quality == CharacterTacticalCardQuality.Rainbow ? new Color32(213, 227, 238, 219)
                : new Color32(200, 208, 213, 223);
            icon.color = quality == CharacterTacticalCardQuality.Gold ? new Color32(249, 238, 209, 255)
                : new Color32(242, 247, 251, 255);
            surface.alpha = selected ? 1 : .88f;
        }
    }
}
