using System.Collections.Generic;
using MxFramework.Combat.Core;

namespace MxFramework.Combat.Diagnostics
{
    public sealed class CombatDebugSnapshotBuilder
    {
        private readonly List<CombatReplayInput> _inputs = new List<CombatReplayInput>();
        private readonly List<CombatQueryTrace> _queries = new List<CombatQueryTrace>();
        private readonly List<CombatHitExplain> _hits = new List<CombatHitExplain>();

        public void Clear()
        {
            _inputs.Clear();
            _queries.Clear();
            _hits.Clear();
        }

        public void AddInput(CombatReplayInput input)
        {
            _inputs.Add(input);
        }

        public void AddQuery(CombatQueryTrace query)
        {
            _queries.Add(query);
        }

        public void AddHit(CombatHitExplain hit)
        {
            _hits.Add(hit);
        }

        public CombatDebugSnapshot Build(CombatFrame frame)
        {
            CombatHash hash = ComputeFrameHash(frame);
            return new CombatDebugSnapshot(frame, hash, _inputs, _queries, _hits);
        }

        public CombatHash ComputeFrameHash(CombatFrame frame)
        {
            var inputs = new List<CombatReplayInput>(_inputs);
            var queries = new List<CombatQueryTrace>(_queries);
            var hits = new List<CombatHitExplain>(_hits);
            inputs.Sort();
            queries.Sort();
            hits.Sort();

            CombatHash hash = CombatHash.Empty.Add(frame);
            hash = hash.Add(inputs.Count);
            for (int i = 0; i < inputs.Count; i++)
            {
                hash = inputs[i].AppendHash(hash);
            }

            hash = hash.Add(queries.Count);
            for (int i = 0; i < queries.Count; i++)
            {
                hash = queries[i].AppendHash(hash);
            }

            hash = hash.Add(hits.Count);
            for (int i = 0; i < hits.Count; i++)
            {
                hash = hits[i].AppendHash(hash);
            }

            return hash;
        }
    }
}
