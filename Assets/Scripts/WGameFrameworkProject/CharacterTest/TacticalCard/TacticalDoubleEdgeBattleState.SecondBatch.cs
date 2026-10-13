using System;
using MxFramework.Gameplay;

namespace MxFramework.Game.CharacterTest.TacticalCard
{
    internal sealed partial class TacticalDoubleEdgeBattleState
    {
        private TacticalSecondBatchHit[] _roundHits = Array.Empty<TacticalSecondBatchHit>();
        private TacticalSecondBatchHitDecision[] _roundDecisions = Array.Empty<TacticalSecondBatchHitDecision>();
        private int[] _roundOrder = Array.Empty<int>(), _leafIndices = Array.Empty<int>();

        internal bool HasSecondBatch
        { get { foreach (var entry in _entries) if (entry.SecondBatch != null) return true; return false; } }

        internal bool HasUnclosedBirth(long frame, int round)
        {
            foreach (var entry in _entries) {
                var leaf = entry.SecondBatch;
                if (leaf == null) continue;
                for (int i = 0; i < leaf.SourceCount; i++) {
                    var source = leaf.SourceAt(i);
                    if (source.CreatedFrame == frame && source.BirthRound >= round) return true;
                }
            }
            return false;
        }

        internal bool SuppressesAttack(in TacticalDirectAttackStamp root)
            => root.IsValid && Find(root.Commit)?.SecondBatch?.SuppressFutureAttackJudgments == true;

        // Deterministic producer identity, never callback order. Exact registration detects hash collisions.
        internal static ulong ProducerNode(int kind, long nativeId, long generation, ulong rootRevision)
        {
            unchecked {
                ulong hash = 14695981039346656037UL;
                hash = (hash ^ (uint)kind) * 1099511628211UL;
                hash = (hash ^ (ulong)nativeId) * 1099511628211UL;
                hash = (hash ^ (ulong)generation) * 1099511628211UL;
                hash = (hash ^ rootRevision) * 1099511628211UL;
                return hash | (1UL << 63);
            }
        }

        internal TacticalSecondBatchSource SecondBatchProducer(in TacticalDirectAttackStamp root, int kind, long id, long generation)
        {
            var leaf = root.IsValid ? Find(root.Commit)?.SecondBatch : null;
            if (leaf == null) return default;
            ulong node = ProducerNode(kind, id, generation, root.ActionRevision);
            for (int i = 0; i < leaf.SourceCount; i++) if (leaf.SourceAt(i).Node == node) return leaf.SourceAt(i);
            return default;
        }

        internal TacticalSecondBatchSource SecondBatchRoot(in TacticalDirectAttackStamp root)
        {
            var leaf = root.IsValid ? Find(root.Commit)?.SecondBatch : null;
            if (leaf == null) return default;
            for (int i = 0; i < leaf.SourceCount; i++) {
                var source = leaf.SourceAt(i);
                if (source.Parent == 0 && source.RootActionInstance == root.ActionInstance
                    && source.RootActionRevision == root.ActionRevision && source.CreatedFrame == root.CreatedFrame) return source;
            }
            return default;
        }

        internal bool TryRegisterSecondBatchBirth(in TacticalSecondBatchSource parent, ulong node,
            GameplayEntityId producer, long frame, in TacticalSecondBatchHitKey acceptedParent,
            out TacticalSecondBatchSource source)
        {
            source = default;
            var leaf = Find(parent.Commit)?.SecondBatch;
            if (leaf == null) return false;
            int before = leaf.SourceCount;
            bool result = acceptedParent.IsValid
                ? leaf.TryRegisterDerivedAfterHit(parent, acceptedParent, node, producer, frame, out source)
                : leaf.TryRegisterDerived(parent, node, producer, frame, out source);
            if (result && leaf.SourceCount != before) _revision++;
            return result;
        }

        internal int RearScale(in TacticalDoubleEdgeDamageFacts facts, in TacticalSecondBatchDirection direction)
        {
            int scale = 1000;
            foreach (var entry in _entries)
                if (entry.SecondBatch != null) scale = entry.SecondBatch.AccumulateRearScale(scale, facts, direction);
            return scale;
        }

        /// <remarks>LowFreqAlloc on capacity growth. All leaves are preflighted before any publication.
        /// Inputs and outputs are unchanged on rejection. Decisions retain caller input order.
        /// The stable full-commit order disambiguates local node IDs; empty leaves close the same phase.</remarks>
        internal bool TryResolveSecondBatchRound(long frame, int round, TacticalSecondBatchHit[] hits, int count,
            TacticalSecondBatchHitDecision[] decisions)
        {
            if (hits == null || decisions == null || count < 0 || count > hits.Length || count > decisions.Length) return false;
            if (_roundHits.Length < count) {
                int capacity = Math.Max(count, Math.Max(16, _roundHits.Length * 2));
                _roundHits = new TacticalSecondBatchHit[capacity]; _roundDecisions = new TacticalSecondBatchHitDecision[capacity];
                _roundOrder = new int[capacity]; _leafIndices = new int[capacity];
            }
            for (int i = 0; i < count; i++) {
                if (Find(hits[i].Source.Commit)?.SecondBatch == null) return false;
                int j = i;
                while (j > 0 && hits[_roundOrder[j - 1]].CompareTo(hits[i]) > 0) {
                    _roundOrder[j] = _roundOrder[j - 1]; j--;
                }
                _roundOrder[j] = i;
            }
            foreach (var entry in _entries) {
                if (entry.SecondBatch == null) continue;
                int size = GatherLeaf(entry, hits, count);
                if (SkipEmptyHistoricalLeaf(entry, frame, round, size)) continue;
                if (!entry.SecondBatch.CanResolveRound(frame, round, _roundHits, size, _roundDecisions)) return false;
            }
            foreach (var entry in _entries) {
                if (entry.SecondBatch == null) continue;
                int size = GatherLeaf(entry, hits, count);
                if (frame > entry.SecondBatch.ClosedFrame || frame == entry.SecondBatch.ClosedFrame && round > entry.SecondBatch.ClosedRound)
                    entry.SecondBatch.ReserveRound(size);
            }
            bool changed = false;
            foreach (var entry in _entries) {
                if (entry.SecondBatch == null) continue;
                int size = GatherLeaf(entry, hits, count);
                if (SkipEmptyHistoricalLeaf(entry, frame, round, size)) continue;
                changed |= frame > entry.SecondBatch.ClosedFrame || frame == entry.SecondBatch.ClosedFrame && round > entry.SecondBatch.ClosedRound;
                if (!entry.SecondBatch.TryResolveRound(frame, round, _roundHits, size, _roundDecisions))
                    throw new InvalidOperationException("accessory:RoundChangedAfterPreflight");
                for (int i = 0; i < size; i++) decisions[_leafIndices[i]] = _roundDecisions[i];
            }
            if (changed) _revision++; return true;
        }

        private static bool SkipEmptyHistoricalLeaf(Entry entry, long frame, int round, int size)
            => size == 0 && (entry.Key.Frame > frame || frame < entry.SecondBatch.ClosedFrame
                || frame == entry.SecondBatch.ClosedFrame && round < entry.SecondBatch.ClosedRound);

        private int GatherLeaf(Entry entry, TacticalSecondBatchHit[] hits, int count)
        {
            int size = 0;
            for (int i = 0; i < count; i++) {
                int index = _roundOrder[i];
                if (!entry.Key.Matches(hits[index].Source.Commit)) continue;
                _roundHits[size] = hits[index]; _leafIndices[size++] = index;
            }
            return size;
        }
    }
}
