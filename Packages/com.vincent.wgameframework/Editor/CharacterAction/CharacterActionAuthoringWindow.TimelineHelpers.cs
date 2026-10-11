using System;
using System.Collections.Generic;

namespace MxFramework.Editor.CharacterAction
{
    public sealed partial class CharacterActionAuthoringWindow
    {
        private static int ResolveEventIndex<T>(
            IReadOnlyList<T> events,
            T eventRef,
            string stableEventId,
            int fallbackIndex,
            Func<T, string> getStableEventId)
            where T : class
        {
            if (events == null)
                return -1;

            if (eventRef != null)
            {
                for (int i = 0; i < events.Count; i++)
                {
                    T current = events[i];
                    if (current != null && ReferenceEquals(current, eventRef))
                        return i;
                }
            }

            if (!string.IsNullOrWhiteSpace(stableEventId))
            {
                int match = -1;
                for (int i = 0; i < events.Count; i++)
                {
                    T current = events[i];
                    if (current == null
                        || !string.Equals(getStableEventId(current), stableEventId, StringComparison.Ordinal))
                        continue;
                    if (match >= 0)
                        return -1;
                    match = i;
                }

                if (match >= 0)
                    return match;
            }

            return fallbackIndex >= 0 && fallbackIndex < events.Count ? fallbackIndex : -1;
        }
    }
}
