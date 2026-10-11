using System;
using UnityEditor;
using UnityEditor.ShortcutManagement;
using UnityEngine;

namespace MxFramework.Editor
{
    /// <summary>
    /// Toggles the Unity Game View between its docked and maximized layouts.
    /// </summary>
    internal static class GameViewFullscreenShortcut
    {
        internal const string ShortcutId = "MxFramework/Editor/Toggle Game View Fullscreen";
        internal const string MenuPath = "MxFramework/Editor/Toggle Game View Fullscreen";

        [Shortcut(ShortcutId, KeyCode.G)]
        [MenuItem(MenuPath, priority = 21)]
        private static void Toggle()
        {
            Type gameViewType = typeof(EditorWindow).Assembly.GetType("UnityEditor.GameView");
            if (gameViewType == null)
            {
                Debug.LogWarning("MxFramework could not locate the Unity Game View window type.");
                return;
            }

            EditorWindow gameView = IsGameView(EditorWindow.focusedWindow)
                ? EditorWindow.focusedWindow
                : EditorWindow.GetWindow(gameViewType);

            gameView.maximized = !gameView.maximized;
            gameView.Focus();
        }

        private static bool IsGameView(EditorWindow window)
        {
            return window != null
                && string.Equals(window.GetType().FullName, "UnityEditor.GameView", StringComparison.Ordinal);
        }
    }
}
