using System.Collections.Generic;
using UnityEngine;
using Fgui = global::FairyGUI;

namespace MxFramework.Game.CharacterTest
{
    public sealed partial class CharacterTestFairyGuiMainMenuBinder
    {
        private sealed class HomepageFeedback
        {
            internal Fgui.GButton Button;
            internal Fgui.Controller Controller;
            internal Fgui.GImage[] Faces;
            internal bool Hovered;
            internal bool Pressed;
            internal float Emphasis;
            internal float Depression;
        }

        private readonly List<HomepageFeedback> _homepageFeedback = new List<HomepageFeedback>();
        private bool _homepageKeyboardFocus;

        private void RegisterHomepageFeedback(Fgui.GButton button)
        {
            var feedback = new HomepageFeedback
            {
                Button = button,
                Controller = button.GetController("button"),
                Faces = new[] { button.GetChild("base")?.asImage,
                    button.GetChild("overBase")?.asImage, button.GetChild("downBase")?.asImage }
            };
            _homepageFeedback.Add(feedback);
            // A transparent Graph fill still has a rectangular stroke in the published package.
            // These buttons use their authored, alpha-shaped images for every interaction state.
            SuppressHomepageOverlay(button.GetChild("overOverlay"));
            SuppressHomepageOverlay(button.GetChild("downOverlay"));
            button.onRollOver.Add(() => { feedback.Hovered = true; _homepageKeyboardFocus = false; });
            button.onRollOut.Add(() => { feedback.Hovered = false; });
            button.onTouchBegin.Add(() => { feedback.Pressed = button.enabled; _homepageKeyboardFocus = false; });
            button.onTouchEnd.Add(() => { feedback.Pressed = false; });
            button.onRemovedFromStage.Add(() =>
            {
                feedback.Hovered = false;
                feedback.Pressed = false;
                feedback.Emphasis = 0f;
                feedback.Depression = 0f;
            });
        }

        private static void SuppressHomepageOverlay(Fgui.GObject overlay)
        {
            if (overlay == null) return;
            overlay.alpha = 0f;
            overlay.touchable = false;
        }

        /// <summary>Updates the existing published button images; never changes their hit rectangles.</summary>
        internal void UpdateHomepageFeedback(float deltaTime)
        {
            float blend = 1f - Mathf.Exp(-Mathf.Max(0f, deltaTime) * 18f);
            foreach (HomepageFeedback feedback in _homepageFeedback)
            {
                Fgui.GButton button = feedback.Button;
                if (button.isDisposed) continue;
                bool modal = _homepageChoice != HomepageChoice.None;
                bool available = button.enabled && button.visible && button.onStage
                    && (button.parent != _homepageConfirmation || modal)
                    && (!modal || button.parent == _homepageConfirmation);
                bool highlighted = available && (feedback.Hovered || (_homepageKeyboardFocus && button.focused));
                bool pressed = available && feedback.Pressed && feedback.Hovered;
                if (!available) { feedback.Hovered = false; feedback.Pressed = false; }
                feedback.Emphasis = Mathf.Lerp(feedback.Emphasis, highlighted ? 1f : 0f, blend);
                feedback.Depression = Mathf.Lerp(feedback.Depression, pressed ? 1f : 0f, blend);
                float emphasis = feedback.Emphasis;
                float depression = feedback.Depression;
                bool large = button.width > 400f;
                float grow = emphasis * (large ? 2.4f : 1.2f) - depression * 2f;
                float lift = emphasis * (large ? 2f : 1f) * (1f - depression) - depression * 1.5f;
                Color tint = Color.Lerp(new Color(0.92f, 0.95f, 0.98f), Color.white, emphasis);
                tint = Color.Lerp(tint, new Color(0.77f, 0.86f, 0.93f), depression);
                feedback.Controller.selectedPage = pressed ? "down" : highlighted ? "over" : "up";
                foreach (Fgui.GImage face in feedback.Faces)
                {
                    if (face == null) continue;
                    face.color = tint;
                    face.SetSize(button.width + grow * 2f, button.height + grow * 2f);
                    face.SetXY(-grow, -grow - lift);
                }
                SetHomepageButtonContentColor(button, Color.Lerp(new Color32(33, 52, 76, 255),
                    new Color32(12, 66, 103, 255), emphasis));
                if (button.GetChild("title") is Fgui.GObject title) title.y = -lift;
                if (button.GetChild("icon") is Fgui.GObject icon) icon.y = (button.height - icon.height) * 0.5f - lift;
                Fgui.GObject pattern = button.parent?.GetChild(button.name + "Pattern");
                if (pattern != null) pattern.alpha = Mathf.Lerp(0.12f, 0.23f, emphasis);
            }
        }
    }
}
