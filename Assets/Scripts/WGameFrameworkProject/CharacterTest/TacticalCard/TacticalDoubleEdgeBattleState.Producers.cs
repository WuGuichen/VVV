using System;
using System.Collections.Generic;
using MxFramework.Runtime;

namespace MxFramework.Game.CharacterTest.TacticalCard
{
    internal sealed partial class TacticalDoubleEdgeBattleState
    {
        // Native identities are assigned by their real providers. These bindings outlive the main host.
        private readonly List<ProducerBinding> _producerBindings = new List<ProducerBinding>();
        internal readonly struct ProducerBinding
        {
            internal readonly int Kind;
            internal readonly long Id, Generation;
            internal readonly TacticalDirectAttackStamp Root;
            internal readonly TacticalSecondBatchSource Source;
            internal ProducerBinding(int kind, long id, long generation, TacticalDirectAttackStamp root, TacticalSecondBatchSource source)
            { Kind = kind; Id = id; Generation = generation; Root = root; Source = source; }
        }

        internal ProducerBinding FindProducer(int kind, long id, long generation)
        {
            foreach (var binding in _producerBindings)
                if (binding.Kind == kind && binding.Id == id && binding.Generation == generation) return binding;
            return default;
        }

        internal bool BindProducer(int kind, long id, long generation, in TacticalDirectAttackStamp root,
            in TacticalSecondBatchSource parent, long frame, in TacticalSecondBatchHitKey acceptedParent = default)
        {
            if (!root.IsValid) return true; // Ordinary producers need no paid-commit authority.
            if (kind < 1 || id <= 0 || generation < 0 || frame < root.CreatedFrame) return false;
            var acknowledged = FindSource(root.Commit, root.ActionInstance, root.ActionRevision);
            if (!acknowledged.IsValid || acknowledged.CreatedFrame != root.CreatedFrame) return false;
            var existing = FindProducer(kind, id, generation);
            if (existing.Root.IsValid)
                return existing.Root.Commit.Matches(root.Commit) && existing.Root.ActionInstance == root.ActionInstance
                    && existing.Root.ActionRevision == root.ActionRevision && existing.Root.CreatedFrame == root.CreatedFrame
                    && (!parent.IsValid || existing.Source.Parent == parent.Node
                        && existing.Source.CausalTrigger.CompareTo(acceptedParent) == 0);
            TacticalSecondBatchSource child = default;
            if (parent.IsValid && !TryRegisterSecondBatchBirth(parent, ProducerNode(kind, id, generation, root.ActionRevision),
                parent.Producer, frame, acceptedParent, out child)) return false;
            if (Find(root.Commit)?.SecondBatch != null && !parent.IsValid && !SuppressesAttack(root)) return false;
            var candidate = new ProducerBinding(kind, id, generation, root, child);
            int at = _producerBindings.Count;
            while (at > 0 && CompareProducer(_producerBindings[at - 1], candidate) > 0) at--;
            _producerBindings.Insert(at, candidate); _revision++; return true;
        }

        private static int CompareProducer(in ProducerBinding a, in ProducerBinding b)
        {
            int c = a.Kind.CompareTo(b.Kind);
            if (c == 0) c = a.Id.CompareTo(b.Id);
            return c == 0 ? a.Generation.CompareTo(b.Generation) : c;
        }

        private void AddProducerHash(ref RuntimeHashAccumulator hash)
        {
            hash.BeginArray(6, _producerBindings.Count);
            for (int i = 0; i < _producerBindings.Count; i++) {
                var b = _producerBindings[i]; var root = b.Root; var k = root.Commit;
                hash.BeginElement(i);
                hash.AddInt(1, b.Kind); hash.AddLong(2, b.Id); hash.AddLong(3, b.Generation);
                hash.AddLong(4, unchecked((long)k.Stage)); hash.AddLong(5, unchecked((long)k.Commit));
                hash.AddLong(6, unchecked((long)k.Execution)); hash.AddInt(7, k.Actor.Index); hash.AddInt(8, k.Actor.Generation);
                hash.AddLong(9, unchecked((long)k.BattleCardInstance)); hash.AddLong(10, k.Frame);
                hash.AddInt(11, root.ActionInstance); hash.AddLong(12, unchecked((long)root.ActionRevision));
                hash.AddLong(13, root.CreatedFrame); hash.AddLong(14, unchecked((long)b.Source.Node));
                hash.EndElement();
            }
            hash.EndArray();
        }

        private void AppendProducerProjection(List<long[]> rows)
        {
            foreach (var b in _producerBindings) {
                var root = b.Root; var k = root.Commit;
                rows.Add(new long[] { 10, b.Kind, b.Id, b.Generation, unchecked((long)k.Stage), unchecked((long)k.Commit),
                    unchecked((long)k.Execution), k.Actor.Index, k.Actor.Generation, unchecked((long)k.BattleCardInstance),
                    k.Frame, root.ActionInstance, unchecked((long)root.ActionRevision), root.CreatedFrame,
                    unchecked((long)b.Source.Node) });
            }
        }
    }
}
