using System;
using System.Collections.Generic;
using MxFramework.Combat.Core;

namespace MxFramework.Combat.Diagnostics
{
    public sealed class CombatReplayRecorder
    {
        private readonly List<CombatReplayInput> _inputs = new List<CombatReplayInput>();

        public int Count => _inputs.Count;

        public IReadOnlyList<CombatReplayInput> Inputs => _inputs;

        public void Clear()
        {
            _inputs.Clear();
        }

        public void Record(CombatReplayInput input)
        {
            _inputs.Add(input);
        }

        public int CollectFrameInputs(CombatFrame frame, List<CombatReplayInput> results)
        {
            if (results == null)
            {
                throw new ArgumentNullException(nameof(results));
            }

            int startCount = results.Count;
            for (int i = 0; i < _inputs.Count; i++)
            {
                if (_inputs[i].Frame == frame)
                {
                    results.Add(_inputs[i]);
                }
            }

            results.Sort(startCount, results.Count - startCount, Comparer<CombatReplayInput>.Default);
            return results.Count - startCount;
        }

        public CombatHash ComputeInputHash()
        {
            var sorted = new List<CombatReplayInput>(_inputs);
            sorted.Sort();

            CombatHash hash = CombatHash.Empty.Add(sorted.Count);
            for (int i = 0; i < sorted.Count; i++)
            {
                hash = sorted[i].AppendHash(hash);
            }

            return hash;
        }
    }
}
