using System;
using System.Collections.Generic;

namespace MxFramework.Combat.Physics
{
    public readonly struct CombatPhysicsQueryBatch
    {
        public CombatPhysicsQueryBatch(IReadOnlyList<CombatPhysicsQuery> queries)
        {
            Queries = queries ?? throw new ArgumentNullException(nameof(queries));
        }

        public IReadOnlyList<CombatPhysicsQuery> Queries { get; }

        public int Count => Queries.Count;

        public CombatPhysicsQuery this[int index] => Queries[index];
    }

    public sealed class CombatPhysicsQueryBatchResult
    {
        public CombatPhysicsQueryBatchResult(
            CombatPhysicsQuery query,
            int sourceIndex,
            IReadOnlyList<CombatQueryResult> hits,
            CombatPhysicsQueryDebugReport debugReport)
        {
            if (sourceIndex < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(sourceIndex), "Batch query source index cannot be negative.");
            }

            Query = query;
            SourceIndex = sourceIndex;
            Hits = hits ?? throw new ArgumentNullException(nameof(hits));
            DebugReport = debugReport ?? throw new ArgumentNullException(nameof(debugReport));
        }

        public CombatPhysicsQuery Query { get; }

        public int SourceIndex { get; }

        public IReadOnlyList<CombatQueryResult> Hits { get; }

        public CombatPhysicsQueryDebugReport DebugReport { get; }

        public int HitCount => Hits.Count;

        public bool IsUnsupported => DebugReport.IsUnsupported;
    }
}
