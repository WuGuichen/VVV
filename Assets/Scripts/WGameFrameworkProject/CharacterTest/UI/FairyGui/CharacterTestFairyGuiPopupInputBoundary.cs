using System;
using UnityEngine;
using Fgui = global::FairyGUI;

namespace MxFramework.Game.CharacterTest
{
    internal static class CharacterTestFairyGuiPopupInputBoundary
    {
        internal static void Install(Fgui.GComponent popup, Action close)
        {
            if (popup == null)
                throw new ArgumentNullException(nameof(popup));

            popup.onTouchBegin.Set(context => context.StopPropagation());
            popup.onTouchMove.Set(context => context.StopPropagation());
            popup.onTouchEnd.Set(context => context.StopPropagation());
            popup.onClick.Set(context => context.StopPropagation());
            popup.displayObject.onMouseWheel.Set(context => context.StopPropagation());
            popup.onKeyDown.Set(context =>
            {
                if (context?.inputEvent?.keyCode == KeyCode.Escape)
                    close?.Invoke();
                context?.StopPropagation();
            });
        }
    }
}
