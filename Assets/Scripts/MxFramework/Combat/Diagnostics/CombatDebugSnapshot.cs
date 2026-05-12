using System.Collections.Generic;
using MxFramework.Combat.Core;

namespace MxFramework.Combat.Diagnostics
{
    public sealed class CombatDebugSnapshot
    {
        private readonly List<CombatReplayInput> _inputs;
        private readonly List<CombatQueryTrace> _queries;
        private readonly List<CombatHitExplain> _hits;

        public CombatDebugSnapshot(
            CombatFrame frame,
            CombatHash frameHash,
            IReadOnlyList<CombatReplayInput> inputs,
            IReadOnlyList<CombatQueryTrace> queries,
            IReadOnlyList<CombatHitExplain> hits)
        {
            Frame = frame;
            FrameHash = frameHash;
            _inputs = inputs == null ? new List<CombatReplayInput>() : new List<CombatReplayInput>(inputs);
            _queries = queries == null ? new List<CombatQueryTrace>() : new List<CombatQueryTrace>(queries);
            _hits = hits == null ? new List<CombatHitExplain>() : new List<CombatHitExplain>(hits);
            _inputs.Sort();
            _queries.Sort();
            _hits.Sort();
        }

        public CombatFrame Frame { get; }

        public CombatHash FrameHash { get; }

        public IReadOnlyList<CombatReplayInput> Inputs => _inputs;

        public IReadOnlyList<CombatQueryTrace> Queries => _queries;

        public IReadOnlyList<CombatHitExplain> Hits => _hits;

        public string Summary
        {
            get
            {
                return $"frame={Frame.Value} hash={FrameHash} inputs={_inputs.Count} queries={_queries.Count} hits={_hits.Count}";
            }
        }
    }
}
