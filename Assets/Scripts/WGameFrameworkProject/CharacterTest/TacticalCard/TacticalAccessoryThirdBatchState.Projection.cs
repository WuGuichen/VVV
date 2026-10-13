using System;
using System.Collections.Generic;
using MxFramework.Runtime;

namespace MxFramework.Game.CharacterTest.TacticalCard
{
    /// <summary>Versioned numeric sequence used by both the Stage hash and cold projection. Hash traversal is NoAlloc.</summary>
    internal struct TacticalThirdBatchStateWriter
    {
        private readonly RuntimeHashAccumulator _hash;
        private readonly List<long> _values;
        internal int Count;
        internal TacticalThirdBatchStateWriter(RuntimeHashAccumulator hash, List<long> values = null)
        { _hash = hash; _values = values; Count = 0; }
        internal void Value(long value)
        {
            if (_hash != null) { _hash.BeginElement(Count); _hash.AddLong(1, value); _hash.EndElement(); }
            _values?.Add(value); Count++;
        }
        internal void Unsigned(ulong value) => Value(unchecked((long)value));
        internal void Entity(Gameplay.GameplayEntityId value) { Value(value.Index); Value(value.Generation); }
        internal void Order(in TacticalThirdBatchOrder value)
        { Value(value.Frame); Value(value.Round); Unsigned(value.StableEvent); Unsigned(value.SourceNode); Entity(value.Recipient); Unsigned(value.Occurrence); }
        internal void Commit(in TacticalThirdBatchCommit value)
        { Unsigned(value.Stage); Unsigned(value.Commit); Unsigned(value.Execution); Entity(value.Actor); Unsigned(value.CardInstance); Unsigned(value.TacticalTurn); Order(value.ActivationOrder); }
        internal void Attack(in TacticalThirdBatchAttack value)
        { Unsigned(value.Stage); Unsigned(value.Execution); Entity(value.Actor); Value(value.ActionInstance); Unsigned(value.Revision); Order(value.AcceptedOrder); }
        internal void Source(in TacticalThirdBatchSource value)
        { Attack(value.Attack); Unsigned(value.Node); Value((int)value.Origin); Order(value.BirthOrder); }
        internal void Random(in RuntimeRandomState value)
        {
            Value(value.AlgorithmId == null ? 0 : value.AlgorithmId == DeterministicRandom.XorShift32AlgorithmId ? 1
                : throw new InvalidOperationException("third-batch:UnknownRngAlgorithm"));
            Unsigned(value.Seed); Unsigned(value.State); Value(value.DrawCount);
        }
        internal void Coin(in TacticalThirdBatchCoinReceipt value)
        { Commit(value.Commit); Entity(value.Enemy); Value((int)value.Face); Random(value.AfterRandom); }
        internal void Event(in TacticalThirdBatchEvent value)
        {
            Value((int)value.Kind); Order(value.Order); Attack(value.Attack); Entity(value.Actor); Unsigned(value.Execution);
            Value((int)value.Origin); Value((int)value.Outcome); Value(value.ActorAlive ? 1 : 0); Value(value.ActualEnemy ? 1 : 0);
            Value(value.EligibleAttack ? 1 : 0); Source(value.Source); Unsigned(value.TacticalTurn); Value(value.Suppressed ? 1 : 0);
        }
    }
    internal sealed partial class TacticalThirdBatchCoinLedger
    {
        internal TacticalThirdBatchCoinReceipt ReceiptAt(int index) => _receipts[index];
        internal void WriteState(ref TacticalThirdBatchStateWriter writer)
        {
            writer.Unsigned(_stage); writer.Random(RandomState); writer.Value(_receipts.Count);
            foreach (var receipt in _receipts) writer.Coin(receipt);
        }
    }
    internal sealed partial class TacticalAccessoryThirdBatchState
    {
        internal bool HasAcceptedProductionRoot(in TacticalThirdBatchEvent item) => SeenRoot(item);
        internal bool CanObserveProductionBirth(in TacticalThirdBatchSource source)
            => !(source.Attack.Actor == Commit.Actor && source.Attack.Execution == Commit.Execution && _hostEnded);
        internal bool CanOpenProductionRound(long frame, int round, TacticalThirdBatchOrder[] keys, int count)
        {
            if (!RoundComplete || keys == null || count < 0 || count > keys.Length || frame < Commit.Frame
                || frame >= long.MaxValue || frame == _settledFrame || round < 0 || round >= int.MaxValue
                || (_frame < 0 ? round != (frame == Commit.Frame ? Commit.ActivationOrder.Round : 0)
                    : frame > _frame ? round != 0 : frame != _frame || _round >= int.MaxValue - 1 || round != _round + 1)) return false;
            for (int i = 0; i < count; i++) {
                if (!keys[i].IsValid || keys[i].Frame != frame || keys[i].Round != round || keys[i].CompareTo(Commit.ActivationOrder) <= 0) return false;
                for (int j = 0; j < i; j++) if (keys[i].CompareTo(keys[j]) == 0) return false;
            }
            return true;
        }
        internal void WriteState(ref TacticalThirdBatchStateWriter writer)
        {
            writer.Commit(Commit); writer.Coin(Coin); writer.Value(_ironThrone ? 1 : 0);
            writer.Value((int)GlassPhase); writer.Value((int)CoinPhase); writer.Attack(_enemyAttack);
            writer.Value(_frame); writer.Value(_round); writer.Value(_next); writer.Value(_settledFrame); writer.Unsigned(_lastSettledTurn);
            writer.Value(_hostEnded ? 1 : 0); writer.Value(_battleEnded ? 1 : 0); writer.Value(_coinRevoked ? 1 : 0);
            writer.Value(_ownerDepartureConfirmed ? 1 : 0);
            writer.Value(_scheduleCount); for (int i = 0; i < _scheduleCount; i++) writer.Order(_schedule[i]);
            writer.Value(_attacks.Count); foreach (var attack in _attacks) writer.Attack(attack);
            writer.Value(_endedAttacks.Count); foreach (var attack in _endedAttacks) writer.Attack(attack);
            writer.Value(_sources.Count); foreach (var row in _sources) { writer.Source(row.Source); writer.Value(row.CoinEligible ? 1 : 0); }
            writer.Value(_roots.Count); foreach (var row in _roots) { writer.Unsigned(row.Node); writer.Entity(row.Target); writer.Unsigned(row.Occurrence); }
            writer.Value(_departed.Count); foreach (var actor in _departed) writer.Entity(actor);
        }
    }
    internal sealed partial class TacticalDoubleEdgeBattleState
    {
        private void ClearThirdState()
        {
            _coinLedger = null; _thirdAttacks.Clear(); _thirdSources.Clear(); _thirdPending.Clear(); _thirdDeparted.Clear();
            _thirdRoundEvents.Clear(); _thirdProjectileRequests.Clear(); _thirdFrame = -1; _thirdRound = -1;
            _thirdNext = 0; _thirdRoundOpen = false; _thirdLastSettledTurn = 0; _thirdNextCommit = 1; ThirdObservedFrame = 0; _thirdExplicitSeed = false;
        }
        private void CopyThirdState(TacticalDoubleEdgeBattleState copy)
        {
            copy._coinLedger = _coinLedger?.CloneForTrustedPause(); copy._thirdAttacks.AddRange(_thirdAttacks);
            copy._thirdSources.AddRange(_thirdSources); copy._thirdPending.AddRange(_thirdPending); copy._thirdDeparted.AddRange(_thirdDeparted);
            copy._thirdRoundEvents.AddRange(_thirdRoundEvents); copy._thirdProjectileRequests.AddRange(_thirdProjectileRequests);
            copy._thirdFrame = _thirdFrame; copy._thirdRound = _thirdRound; copy._thirdNext = _thirdNext;
            copy._thirdRoundOpen = _thirdRoundOpen; copy._thirdLastSettledTurn = _thirdLastSettledTurn;
            copy._thirdNextCommit = _thirdNextCommit; copy._thirdExplicitSeed = _thirdExplicitSeed; copy.ThirdObservedFrame = ThirdObservedFrame;
        }
        private void WriteThirdState(ref TacticalThirdBatchStateWriter writer)
        {
            writer.Value(1); // Third-batch numeric schema v1. Projection row 17 / Stage hash field 13.
            writer.Value(_coinLedger != null ? 1 : 0); _coinLedger?.WriteState(ref writer);
            writer.Value(_thirdFrame); writer.Value(_thirdRound); writer.Value(_thirdNext); writer.Value(_thirdRoundOpen ? 1 : 0);
            writer.Unsigned(_thirdLastSettledTurn); writer.Unsigned(_thirdNextCommit); writer.Value(_thirdExplicitSeed ? 1 : 0); writer.Value(ThirdObservedFrame);
            writer.Value(_entries.Count);
            foreach (var e in _entries) { writer.Value(e.ThirdBatch != null ? 1 : 0); e.ThirdBatch?.WriteState(ref writer); }
            writer.Value(_thirdAttacks.Count);
            foreach (var a in _thirdAttacks) { writer.Attack(a.Attack); writer.Value(a.Eligible ? 1 : 0); writer.Value(a.Ended ? 1 : 0); writer.Order(a.EndOrder); }
            writer.Value(_thirdSources.Count);
            foreach (var s in _thirdSources) { writer.Value(s.Kind); writer.Value(s.Native); writer.Value(s.Generation); writer.Source(s.Source); }
            writer.Value(_thirdPending.Count); foreach (var e in _thirdPending) writer.Event(e);
            writer.Value(_thirdRoundEvents.Count); foreach (var e in _thirdRoundEvents) writer.Event(e);
            writer.Value(_thirdDeparted.Count); foreach (var a in _thirdDeparted) writer.Entity(a);
            WriteThirdProjectileRequests(ref writer);
        }
        private void AddThirdHash(ref RuntimeHashAccumulator hash)
        {
            if (_stage == 0) return;
            var counter = new TacticalThirdBatchStateWriter(null); WriteThirdState(ref counter);
            hash.BeginArray(13, counter.Count); var writer = new TacticalThirdBatchStateWriter(hash); WriteThirdState(ref writer); hash.EndArray();
        }
        private void AppendThirdProjection(List<long[]> rows)
        {
            if (_stage == 0) return;
            var values = new List<long> { 17 }; var writer = new TacticalThirdBatchStateWriter(null, values); WriteThirdState(ref writer); rows.Add(values.ToArray());
        }
    }
}
