using System;
using System.Collections.Generic;

namespace MxFramework.Game.CharacterTest
{
    /// <summary>Reads existing trace identities; it does not own attack lifecycle or predict future windows.</summary>
    internal static class CharacterTestAttackWindowObservation
    {
        /// <remarks>LowFreqAlloc. Capture once when the wait begins; an empty set means no active window.</remarks>
        internal static int[] Capture(IReadOnlyList<CharacterTestActiveHitTrace> active, int targetId, long instanceId)
        {
            if (targetId <= 0 || instanceId <= 0) return Array.Empty<int>();
            int count = 0;
            for (int i = 0; i < active.Count; i++)
                if (active[i].EntityId == targetId && active[i].SourceInstanceId == instanceId) count++;
            if (count == 0) return Array.Empty<int>();
            var ids = new int[count];
            int index = 0;
            for (int i = 0; i < active.Count; i++)
                if (active[i].EntityId == targetId && active[i].SourceInstanceId == instanceId)
                    ids[index++] = active[i].HitWindowId;
            return ids;
        }

        /// <remarks>NoAlloc. A later window never prolongs the captured window set.</remarks>
        internal static bool AnyCapturedActive(IReadOnlyList<CharacterTestActiveHitTrace> active,
            int targetId, long instanceId, int[] captured)
        {
            for (int i = 0; i < active.Count; i++)
            {
                var trace = active[i];
                if (trace.EntityId != targetId || trace.SourceInstanceId != instanceId) continue;
                for (int j = 0; j < captured.Length; j++)
                    if (trace.HitWindowId == captured[j]) return true;
            }
            return false;
        }
    }
}
