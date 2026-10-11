using System;
using System.Collections.Generic;
using MxFramework.Combat.Core;

namespace MxFramework.Combat.Animation
{
    public sealed class CombatActionTimelineTraceProvider : ICombatActionTraceProvider
    {
        private readonly Dictionary<int, Dictionary<int, List<WeaponTraceFrame>>> _traces =
            new Dictionary<int, Dictionary<int, List<WeaponTraceFrame>>>();

        public void RegisterTrace(int actionId, int localFrame, WeaponTraceFrame trace)
        {
            if (actionId <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(actionId), "Action id must be positive.");
            }

            if (localFrame < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(localFrame), "Local frame cannot be negative.");
            }

            if (!_traces.TryGetValue(actionId, out Dictionary<int, List<WeaponTraceFrame>> byFrame))
            {
                byFrame = new Dictionary<int, List<WeaponTraceFrame>>();
                _traces.Add(actionId, byFrame);
            }

            if (!byFrame.TryGetValue(localFrame, out List<WeaponTraceFrame> traces))
            {
                traces = new List<WeaponTraceFrame>();
                byFrame.Add(localFrame, traces);
            }

            traces.Add(trace);
        }

        public void RegisterTraceSequence(int actionId, IReadOnlyList<(int Frame, WeaponTraceFrame Trace)> sequence)
        {
            if (sequence == null)
            {
                throw new ArgumentNullException(nameof(sequence));
            }

            for (int i = 0; i < sequence.Count; i++)
            {
                RegisterTrace(actionId, sequence[i].Frame, sequence[i].Trace);
            }
        }

        public void ClearAction(int actionId)
        {
            _traces.Remove(actionId);
        }

        public void GetActiveTraces(
            CombatEntityId entityId,
            int actionId,
            int actionInstanceId,
            int localFrame,
            List<WeaponTraceFrame> results)
        {
            if (results == null)
            {
                throw new ArgumentNullException(nameof(results));
            }

            if (!_traces.TryGetValue(actionId, out Dictionary<int, List<WeaponTraceFrame>> byFrame)
                || !byFrame.TryGetValue(localFrame, out List<WeaponTraceFrame> traces))
            {
                return;
            }

            for (int i = 0; i < traces.Count; i++)
            {
                results.Add(traces[i]);
            }
        }
    }
}
