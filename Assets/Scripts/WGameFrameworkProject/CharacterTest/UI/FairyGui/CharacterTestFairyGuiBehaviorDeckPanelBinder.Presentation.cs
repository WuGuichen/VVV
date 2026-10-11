using System.Collections.Generic;
using MxFramework.UI;
using MxFramework.UI.FairyGui;
using UnityEngine;
using Fgui = global::FairyGUI;

namespace MxFramework.Game.CharacterTest
{
    public sealed partial class CharacterTestFairyGuiBehaviorDeckPanelBinder
    {
        private void EnqueueCardAction(
            string stableCardId,
            CharacterTestFairyGuiBehaviorDeckCardAction action,
            CharacterTestFairyGuiBehaviorDeckCardOrigin origin = CharacterTestFairyGuiBehaviorDeckCardOrigin.Library,
            CharacterTestFairyGuiBehaviorDeckCardActionRole actionRole = CharacterTestFairyGuiBehaviorDeckCardActionRole.Primary,
            int slotIndex = -1)
        {
            _commandSink?.Enqueue(new MxUiCommand(
                _viewId,
                CharacterTestFairyGuiBehaviorDeckPanelCommandIds.CardAction,
                new CharacterTestFairyGuiBehaviorDeckCardActionPayload
                {
                    StableCardId = stableCardId,
                    Action = action,
                    Origin = origin,
                    ActionRole = actionRole,
                    SlotIndex = slotIndex
                }));
        }

        private static void SetFeedbackColor(
            Fgui.GGraph background,
            CharacterTestFairyGuiBehaviorDeckFeedbackTone tone,
            bool visible)
        {
            if (background == null)
                return;
            background.visible = visible;
            switch (tone)
            {
                case CharacterTestFairyGuiBehaviorDeckFeedbackTone.Success:
                    background.color = new Color32(32, 122, 111, 210);
                    break;
                case CharacterTestFairyGuiBehaviorDeckFeedbackTone.Warning:
                    background.color = new Color32(139, 104, 39, 220);
                    break;
                case CharacterTestFairyGuiBehaviorDeckFeedbackTone.Error:
                    background.color = new Color32(139, 55, 51, 220);
                    break;
                default:
                    background.visible = false;
                    break;
            }
        }

        private static Color FeedbackTextColor(CharacterTestFairyGuiBehaviorDeckFeedbackTone tone)
        {
            switch (tone)
            {
                case CharacterTestFairyGuiBehaviorDeckFeedbackTone.Success:
                    return new Color32(91, 224, 197, 255);
                case CharacterTestFairyGuiBehaviorDeckFeedbackTone.Warning:
                    return new Color32(244, 188, 74, 255);
                case CharacterTestFairyGuiBehaviorDeckFeedbackTone.Error:
                    return new Color32(244, 104, 96, 255);
                default:
                    return new Color32(166, 184, 192, 255);
            }
        }

        private static void BindCardText(Fgui.GComponent visual, string childName, string value)
        {
            Fgui.GTextField text = visual.GetChild(childName)?.asTextField;
            if (text != null)
                text.text = value ?? string.Empty;
        }

        private void SetCardTextTooltip(Fgui.GComponent visual, string childName, string value)
        {
            Fgui.GObject child = visual?.GetChild(childName);
            if (child != null)
                BindTooltip(child, value);
        }

        private void BindTooltip(Fgui.GObject target, string text)
        {
            _tooltipService.Bind(target, text);
        }

        private static void SetVisible(Fgui.GComponent root, string childName, bool visible)
        {
            Fgui.GObject child = root?.GetChild(childName);
            if (child != null)
                child.visible = visible;
        }

        private static void BindCardIcon(
            Fgui.GComponent visual,
            CharacterTestFairyGuiBehaviorDeckCardIcon icon,
            bool empty)
        {
            SetVisible(visual, "iconTriangle", !empty && icon == CharacterTestFairyGuiBehaviorDeckCardIcon.Triangle);
            SetVisible(visual, "iconSquare", !empty && icon == CharacterTestFairyGuiBehaviorDeckCardIcon.Square);
            SetVisible(visual, "iconDiamond", !empty && icon == CharacterTestFairyGuiBehaviorDeckCardIcon.Diamond);
            SetVisible(visual, "iconCircle", !empty && icon == CharacterTestFairyGuiBehaviorDeckCardIcon.Circle);
            SetVisible(visual, "iconFallback", !empty && icon == CharacterTestFairyGuiBehaviorDeckCardIcon.Fallback);
            SetVisible(visual, "iconAdd", empty);
        }

        private static void BindCardStatePresentation(
            Fgui.GComponent visual,
            CharacterTestFairyGuiBehaviorDeckCardState state,
            bool installSelectionActive,
            bool selected)
        {
            bool installed = state == CharacterTestFairyGuiBehaviorDeckCardState.Installed;
            bool available = state == CharacterTestFairyGuiBehaviorDeckCardState.Available;
            bool locked = state == CharacterTestFairyGuiBehaviorDeckCardState.Locked;
            bool blocked = !installed && !available && !locked;

            SetVisible(visual, "stateStripe", true);
            SetVisible(visual, "stateInstalled", installed);
            SetVisible(visual, "stateAvailable", available);
            SetVisible(visual, "stateLocked", locked);
            SetVisible(visual, "stateBlocked", blocked);
            SetVisible(
                visual,
                "installHighlight",
                installSelectionActive && available && !selected);

            Fgui.GGraph background = visual.GetChild("background")?.asGraph;
            if (background != null)
            {
                background.color = installed
                    ? new Color32(18, 47, 45, 255)
                    : blocked || locked
                        ? new Color32(12, 19, 24, 255)
                        : installSelectionActive
                            ? new Color32(15, 34, 36, 255)
                            : new Color32(16, 25, 32, 255);
            }

            Fgui.GGraph stripe = visual.GetChild("stateStripe")?.asGraph;
            if (stripe != null)
            {
                stripe.color = installed
                    ? new Color32(85, 217, 183, 255)
                    : available
                        ? new Color32(116, 151, 163, 255)
                        : new Color32(78, 91, 99, 255);
            }

            visual.alpha = blocked || locked ? 0.58f : 1f;
        }

        private static void BindSelectedCardIcon(
            Fgui.GComponent root,
            CharacterTestFairyGuiBehaviorDeckCardIcon icon,
            bool visible)
        {
            SetVisible(root, "selectedIconTriangle", visible && icon == CharacterTestFairyGuiBehaviorDeckCardIcon.Triangle);
            SetVisible(root, "selectedIconSquare", visible && icon == CharacterTestFairyGuiBehaviorDeckCardIcon.Square);
            SetVisible(root, "selectedIconDiamond", visible && icon == CharacterTestFairyGuiBehaviorDeckCardIcon.Diamond);
            SetVisible(root, "selectedIconCircle", visible && icon == CharacterTestFairyGuiBehaviorDeckCardIcon.Circle);
            SetVisible(root, "selectedIconFallback", visible && icon == CharacterTestFairyGuiBehaviorDeckCardIcon.Fallback);
        }

        private static void SetStateColor(
            Fgui.GGraph stripe,
            CharacterTestFairyGuiBehaviorDeckCardState state)
        {
            if (stripe == null)
                return;
            switch (state)
            {
                case CharacterTestFairyGuiBehaviorDeckCardState.Installed:
                    stripe.color = new Color32(72, 184, 142, 255);
                    break;
                case CharacterTestFairyGuiBehaviorDeckCardState.Locked:
                    stripe.color = new Color32(89, 101, 110, 255);
                    break;
                case CharacterTestFairyGuiBehaviorDeckCardState.Unavailable:
                case CharacterTestFairyGuiBehaviorDeckCardState.Incompatible:
                    stripe.color = new Color32(210, 88, 76, 255);
                    break;
                default:
                    stripe.color = new Color32(63, 127, 140, 255);
                    break;
            }
        }

    }
}
