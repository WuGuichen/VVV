using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace MxFramework.Editor
{
    internal static class EditorWindowShortcutTray
    {
        private const float Gap = 4f;

        private static readonly List<Entry> Entries = new List<Entry>();
        private static Vector2 s_bottomRight;

        internal static void Add(EditorWindow window, Rect expandedPosition, Vector2 collapsedSize)
        {
            RemoveDeadEntries();
            for (int i = 0; i < Entries.Count; i++)
            {
                if (Entries[i].Window == window)
                    return;
            }

            if (Entries.Count == 0)
                s_bottomRight = new Vector2(expandedPosition.xMax, expandedPosition.yMax);

            Entries.Add(new Entry(window, collapsedSize));
            Reflow();
        }

        internal static void Remove(EditorWindow window)
        {
            for (int i = Entries.Count - 1; i >= 0; i--)
            {
                if (Entries[i].Window == null || Entries[i].Window == window)
                    Entries.RemoveAt(i);
            }

            if (Entries.Count == 0)
            {
                s_bottomRight = default;
                return;
            }

            Reflow();
        }

        private static void RemoveDeadEntries()
        {
            for (int i = Entries.Count - 1; i >= 0; i--)
            {
                if (Entries[i].Window == null)
                    Entries.RemoveAt(i);
            }
        }

        private static void Reflow()
        {
            float right = s_bottomRight.x;
            for (int i = 0; i < Entries.Count; i++)
            {
                Entry entry = Entries[i];
                entry.Window.position = BuildPosition(
                    s_bottomRight,
                    s_bottomRight.x - right,
                    entry.Size);
                right -= entry.Size.x;
                right -= Gap;
            }
        }

        internal static Rect BuildPosition(Vector2 bottomRight, float occupiedWidth, Vector2 size)
        {
            return new Rect(
                bottomRight.x - occupiedWidth - size.x,
                bottomRight.y - size.y,
                size.x,
                size.y);
        }

        private readonly struct Entry
        {
            internal Entry(EditorWindow window, Vector2 size)
            {
                Window = window;
                Size = size;
            }

            internal EditorWindow Window { get; }
            internal Vector2 Size { get; }
        }
    }
}
