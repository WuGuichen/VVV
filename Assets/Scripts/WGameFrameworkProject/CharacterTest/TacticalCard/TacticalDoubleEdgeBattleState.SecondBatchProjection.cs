using System.Collections.Generic;
using MxFramework.Runtime;

namespace MxFramework.Game.CharacterTest.TacticalCard
{
    internal sealed partial class TacticalDoubleEdgeBattleState
    {
        // Stage object field 5; projection row tags 6-9. Commit identity is inherited from the entry.
        private void AddSecondBatchHash(ref RuntimeHashAccumulator hash)
        {
            hash.BeginArray(5, _entries.Count);
            for (int i = 0; i < _entries.Count; i++) {
                hash.BeginElement(i);
                var leaf = _entries[i].SecondBatch;
                hash.AddBool(1, leaf != null);
                if (leaf != null) {
                    hash.BeginArray(2, 1); hash.BeginElement(0);
                    hash.AddLong(1, leaf.Parameters.NoTurningBack ? 1 : 0);
                    hash.AddLong(2, leaf.Parameters.NonLethalOath ? 1 : 0);
                    hash.AddLong(3, leaf.Parameters.OnePunch ? 1 : 0);
                    hash.AddLong(4, leaf.Parameters.RearScaleMilli);
                    hash.AddLong(5, leaf.Parameters.OnePunchScaleMilli);
                    hash.AddLong(6, leaf.IsInvalidated ? 1 : 0);
                    hash.AddLong(7, leaf.HostEnded ? 1 : 0);
                    hash.AddLong(8, leaf.RearRevoked ? 1 : 0);
                    hash.AddLong(9, leaf.OnePunchConsumed ? 1 : 0);
                    hash.AddLong(10, leaf.ClosedFrame);
                    hash.AddLong(11, leaf.ClosedRound);
                    hash.AddLong(12, leaf.Winner.Frame);
                    hash.AddLong(13, leaf.Winner.CausalRound);
                    hash.AddLong(14, unchecked((long)leaf.Winner.StableEvent));
                    hash.AddLong(15, unchecked((long)leaf.Winner.SourceNode));
                    hash.AddLong(16, leaf.Winner.Target.Index);
                    hash.AddLong(17, leaf.Winner.Target.Generation);
                    hash.AddLong(18, unchecked((long)leaf.Winner.RootOccurrence));
                    hash.AddLong(19, leaf.LastOrder.Frame);
                    hash.AddLong(20, leaf.LastOrder.CausalRound);
                    hash.AddLong(21, unchecked((long)leaf.LastOrder.StableEvent));
                    hash.AddLong(22, unchecked((long)leaf.LastOrder.SourceNode));
                    hash.AddLong(23, leaf.LastOrder.Target.Index);
                    hash.AddLong(24, leaf.LastOrder.Target.Generation);
                    hash.AddLong(25, unchecked((long)leaf.LastOrder.RootOccurrence));
                    hash.EndElement(); hash.EndArray();
                    hash.BeginArray(3, leaf.SourceCount);
                    for (int j = 0; j < leaf.SourceCount; j++) {
                        var s = leaf.SourceAt(j); hash.BeginElement(j);
                        hash.AddLong(1, unchecked((long)s.Node));
                        hash.AddLong(2, unchecked((long)s.Parent));
                        hash.AddLong(3, s.Producer.Index);
                        hash.AddLong(4, s.Producer.Generation);
                        hash.AddLong(5, s.RootActionInstance);
                        hash.AddLong(6, unchecked((long)s.RootActionRevision));
                        hash.AddLong(7, s.CreatedFrame);
                        hash.AddLong(8, s.BirthRound);
                        hash.AddLong(9, s.CausalTrigger.Frame);
                        hash.AddLong(10, s.CausalTrigger.CausalRound);
                        hash.AddLong(11, unchecked((long)s.CausalTrigger.StableEvent));
                        hash.AddLong(12, unchecked((long)s.CausalTrigger.SourceNode));
                        hash.AddLong(13, s.CausalTrigger.Target.Index);
                        hash.AddLong(14, s.CausalTrigger.Target.Generation);
                        hash.AddLong(15, unchecked((long)s.CausalTrigger.RootOccurrence));
                        hash.EndElement();
                    }
                    hash.EndArray();
                    hash.BeginArray(4, leaf.ReceiptCount);
                    for (int j = 0; j < leaf.ReceiptCount; j++) {
                        var h = leaf.ReceiptAt(j); hash.BeginElement(j);
                        hash.AddLong(1, unchecked((long)h.Source.Node));
                        hash.AddLong(2, unchecked((long)h.Source.Parent));
                        hash.AddLong(3, h.Source.Producer.Index);
                        hash.AddLong(4, h.Source.Producer.Generation);
                        hash.AddLong(5, h.Source.RootActionInstance);
                        hash.AddLong(6, unchecked((long)h.Source.RootActionRevision));
                        hash.AddLong(7, h.Source.CreatedFrame);
                        hash.AddLong(8, h.Source.BirthRound);
                        hash.AddLong(9, h.Source.CausalTrigger.Frame);
                        hash.AddLong(10, h.Source.CausalTrigger.CausalRound);
                        hash.AddLong(11, unchecked((long)h.Source.CausalTrigger.StableEvent));
                        hash.AddLong(12, unchecked((long)h.Source.CausalTrigger.SourceNode));
                        hash.AddLong(13, h.Source.CausalTrigger.Target.Index);
                        hash.AddLong(14, h.Source.CausalTrigger.Target.Generation);
                        hash.AddLong(15, unchecked((long)h.Source.CausalTrigger.RootOccurrence));
                        hash.AddLong(16, h.Key.Frame);
                        hash.AddLong(17, h.Key.CausalRound);
                        hash.AddLong(18, unchecked((long)h.Key.StableEvent));
                        hash.AddLong(19, unchecked((long)h.Key.SourceNode));
                        hash.AddLong(20, h.Key.Target.Index);
                        hash.AddLong(21, h.Key.Target.Generation);
                        hash.AddLong(22, unchecked((long)h.Key.RootOccurrence));
                        hash.AddLong(23, h.ActualProducer.Index);
                        hash.AddLong(24, h.ActualProducer.Generation);
                        hash.AddLong(25, (int)h.Outcome);
                        hash.AddLong(26, h.TargetAlive ? 1 : 0);
                        hash.AddLong(27, h.ActualEnemy ? 1 : 0);
                        hash.EndElement();
                    }
                    hash.EndArray();
                    hash.BeginArray(5, leaf.InterruptAttemptCount);
                    for (int j = 0; j < leaf.InterruptAttemptCount; j++) {
                        var target = leaf.InterruptAttemptAt(j); hash.BeginElement(j);
                        hash.AddInt(1, target.Index); hash.AddInt(2, target.Generation); hash.EndElement();
                    }
                    hash.EndArray();
                }
                hash.EndElement();
            }
            hash.EndArray();
        }

        private void AppendSecondBatchProjection(List<long[]> rows)
        {
            for (int i = 0; i < _entries.Count; i++) {
                var leaf = _entries[i].SecondBatch;
                if (leaf == null) continue;
                rows.Add(new long[] { 6, i, leaf.Parameters.NoTurningBack ? 1 : 0, leaf.Parameters.NonLethalOath ? 1 : 0, leaf.Parameters.OnePunch ? 1 : 0, leaf.Parameters.RearScaleMilli, leaf.Parameters.OnePunchScaleMilli, leaf.IsInvalidated ? 1 : 0, leaf.HostEnded ? 1 : 0, leaf.RearRevoked ? 1 : 0, leaf.OnePunchConsumed ? 1 : 0, leaf.ClosedFrame, leaf.ClosedRound, leaf.Winner.Frame, leaf.Winner.CausalRound, unchecked((long)leaf.Winner.StableEvent), unchecked((long)leaf.Winner.SourceNode), leaf.Winner.Target.Index, leaf.Winner.Target.Generation, unchecked((long)leaf.Winner.RootOccurrence), leaf.LastOrder.Frame, leaf.LastOrder.CausalRound, unchecked((long)leaf.LastOrder.StableEvent), unchecked((long)leaf.LastOrder.SourceNode), leaf.LastOrder.Target.Index, leaf.LastOrder.Target.Generation, unchecked((long)leaf.LastOrder.RootOccurrence) });
                for (int j = 0; j < leaf.SourceCount; j++) {
                    var s = leaf.SourceAt(j);
                    rows.Add(new long[] { 7, i, unchecked((long)s.Node), unchecked((long)s.Parent), s.Producer.Index, s.Producer.Generation, s.RootActionInstance, unchecked((long)s.RootActionRevision), s.CreatedFrame, s.BirthRound, s.CausalTrigger.Frame, s.CausalTrigger.CausalRound, unchecked((long)s.CausalTrigger.StableEvent), unchecked((long)s.CausalTrigger.SourceNode), s.CausalTrigger.Target.Index, s.CausalTrigger.Target.Generation, unchecked((long)s.CausalTrigger.RootOccurrence) });
                }
                for (int j = 0; j < leaf.ReceiptCount; j++) {
                    var h = leaf.ReceiptAt(j);
                    rows.Add(new long[] { 8, i, unchecked((long)h.Source.Node), unchecked((long)h.Source.Parent), h.Source.Producer.Index, h.Source.Producer.Generation, h.Source.RootActionInstance, unchecked((long)h.Source.RootActionRevision), h.Source.CreatedFrame, h.Source.BirthRound, h.Source.CausalTrigger.Frame, h.Source.CausalTrigger.CausalRound, unchecked((long)h.Source.CausalTrigger.StableEvent), unchecked((long)h.Source.CausalTrigger.SourceNode), h.Source.CausalTrigger.Target.Index, h.Source.CausalTrigger.Target.Generation, unchecked((long)h.Source.CausalTrigger.RootOccurrence), h.Key.Frame, h.Key.CausalRound, unchecked((long)h.Key.StableEvent), unchecked((long)h.Key.SourceNode), h.Key.Target.Index, h.Key.Target.Generation, unchecked((long)h.Key.RootOccurrence), h.ActualProducer.Index, h.ActualProducer.Generation, (int)h.Outcome, h.TargetAlive ? 1 : 0, h.ActualEnemy ? 1 : 0 });
                }
                for (int j = 0; j < leaf.InterruptAttemptCount; j++) {
                    var target = leaf.InterruptAttemptAt(j);
                    rows.Add(new long[] { 9, i, target.Index, target.Generation });
                }
            }
        }
    }
}
