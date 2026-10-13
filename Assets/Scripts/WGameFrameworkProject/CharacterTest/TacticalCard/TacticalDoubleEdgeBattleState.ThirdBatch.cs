using System;
using System.Collections.Generic;
using MxFramework.Gameplay;
using MxFramework.Combat.Hit;

namespace MxFramework.Game.CharacterTest.TacticalCard
{
    internal sealed partial class TacticalDoubleEdgeBattleState
    {
        private TacticalThirdBatchCoinLedger _coinLedger;
        private readonly List<ThirdAttackRecord> _thirdAttacks = new List<ThirdAttackRecord>();
        private readonly List<ThirdSourceRecord> _thirdSources = new List<ThirdSourceRecord>();
        private readonly List<TacticalThirdBatchEvent> _thirdPending = new List<TacticalThirdBatchEvent>();
        private readonly List<GameplayEntityId> _thirdDeparted = new List<GameplayEntityId>();
        private readonly List<TacticalThirdBatchEvent> _thirdRoundEvents = new List<TacticalThirdBatchEvent>();
        private readonly List<TacticalThirdBatchEvent> _thirdCandidateEvents = new List<TacticalThirdBatchEvent>();
        private TacticalThirdBatchOrder[] _thirdOrderBuffer = Array.Empty<TacticalThirdBatchOrder>();
        private long _thirdFrame = -1;
        private int _thirdRound = -1, _thirdNext;
        private ulong _thirdLastSettledTurn;
        private ulong _thirdNextCommit = 1;
        private bool _thirdExplicitSeed;
        internal long ThirdObservedFrame { get; private set; }
        private bool _thirdRoundOpen;
        internal int ThirdCurrentRound => _thirdRound;
        internal bool HasThirdBatch { get { foreach (var e in _entries) if (e.ThirdBatch != null) return true; return false; } }
        internal Runtime.RuntimeRandomState ThirdRandomState => _coinLedger?.RandomState ?? default;
        internal int ThirdCoinReceiptCount => _coinLedger?.ReceiptCount ?? 0;
        internal TacticalAccessoryThirdBatchState ThirdState(in TacticalDoubleEdgeCommitKey key) => Find(key)?.ThirdBatch;

        // Fixed phase domains preserve native event identity, without a callback-arrival counter.
        internal static TacticalThirdBatchOrder ThirdOrder(long frame, int round, int phase, ulong nativeEvent,
            ulong node, GameplayEntityId recipient, ulong occurrence)
        {
            if (nativeEvent >= (1UL << 56) || occurrence == 0) throw new InvalidOperationException("third-batch:OrderOverflow");
            return new TacticalThirdBatchOrder(frame, round, ((ulong)phase << 56) | nativeEvent, node, recipient, occurrence);
        }
        private int ThirdBirthRound(long frame)
        {
            if (frame < _thirdFrame)
                throw new InvalidOperationException("third-batch:LateLifecycleObservation");
            return frame == _thirdFrame ? checked(_thirdRound + 1) : 0;
        }
        private static ulong ThirdNode(GameplayEntityId actor, int kind, long native, long generation, ulong revision)
            => ProducerNode(kind, native, generation, ProducerNode(31, actor.Index, actor.Generation, revision));

        private readonly struct ThirdAttackRecord
        {
            internal readonly TacticalThirdBatchAttack Attack;
            internal readonly bool Eligible, Ended;
            internal readonly TacticalThirdBatchOrder EndOrder;
            internal ThirdAttackRecord(TacticalThirdBatchAttack attack, bool eligible, bool ended = false, TacticalThirdBatchOrder endOrder = default)
            { Attack = attack; Eligible = eligible; Ended = ended; EndOrder = endOrder; }
        }
        private readonly struct ThirdSourceRecord
        {
            internal readonly int Kind, Native, Generation;
            internal readonly TacticalThirdBatchSource Source;
            internal ThirdSourceRecord(int kind, int native, int generation, TacticalThirdBatchSource source)
            { Kind = kind; Native = native; Generation = generation; Source = source; }
        }

        /// <summary>Called by the real ActionStarted projection after the action gateway accepted the operation.</summary>
        internal TacticalThirdBatchAttack ObserveThirdAttack(GameplayEntityId actor, int instance, long frame, bool eligible)
        {
            if (_stage == 0 || !actor.IsValid || instance <= 0 || frame < 0 || _thirdDeparted.Contains(actor)) return default;
            ulong revision = 1, execution = (1UL << 63) | (uint)instance;
            foreach (var old in _thirdAttacks) {
                if (old.Attack.Actor != actor) continue;
                if (old.Attack.ActionInstance == instance) {
                    // A terminal receipt remains a valid exact replay; it never reopens the attack.
                    if (old.Attack.AcceptedOrder.Frame != frame || old.Eligible != eligible)
                        throw new InvalidOperationException("third-batch:ConflictingPhysicalAck");
                    return old.Attack;
                }
                revision = Math.Max(revision, checked(old.Attack.Revision + 1));
            }
            for (int i = _entries.Count - 1; i >= 0; i--)
                if (_entries[i].Key.Actor == actor && !_entries[i].Terminal) { execution = _entries[i].Key.Execution; break; }
            var order = ThirdOrder(frame, ThirdBirthRound(frame), 1, (uint)instance,
                ThirdNode(actor, 30, instance, 0, revision), actor, revision);
            var attack = new TacticalThirdBatchAttack(_stage, execution, actor, instance, revision, order);
            _thirdAttacks.Add(new ThirdAttackRecord(attack, eligible));
            _thirdAttacks.Sort((a, b) => a.Attack.AcceptedOrder.CompareTo(b.Attack.AcceptedOrder));
            if (HasThirdBatch) QueueThirdEvent(TacticalThirdBatchEvent.Start(order, attack, true, true, eligible));
            _revision++; return attack;
        }
        internal TacticalThirdBatchAttack ThirdAttack(GameplayEntityId actor, long instance)
        {
            for (int i = _thirdAttacks.Count - 1; i >= 0; i--)
                if (_thirdAttacks[i].Attack.Actor == actor && _thirdAttacks[i].Attack.ActionInstance == instance)
                    return _thirdAttacks[i].Attack;
            return default;
        }
        internal TacticalThirdBatchAttack CurrentThirdAttack(GameplayEntityId actor)
        {
            for (int i = _thirdAttacks.Count - 1; i >= 0; i--)
                if (_thirdAttacks[i].Attack.Actor == actor) return _thirdAttacks[i].Ended ? default : _thirdAttacks[i].Attack;
            return default;
        }
        internal void EndThirdAttack(GameplayEntityId actor, long instance, long frame)
        {
            for (int i = _thirdAttacks.Count - 1; i >= 0; i--) {
                var row = _thirdAttacks[i];
                if (row.Attack.Actor != actor || row.Attack.ActionInstance != instance) continue;
                if (row.Ended) return;
                var endOrder = ThirdOrder(frame, ThirdBirthRound(frame), 4, (ulong)instance, row.Attack.AcceptedOrder.SourceNode, actor, row.Attack.Revision);
                _thirdAttacks[i] = new ThirdAttackRecord(row.Attack, row.Eligible, true, endOrder);
                if (HasThirdBatch) QueueThirdEvent(TacticalThirdBatchEvent.EndAttack(endOrder, row.Attack));
                _revision++; return;
            }
        }

        /// <remarks>LowFreqAlloc on a real accepted trace/projectile birth. Old source identities are retained.</remarks>
        internal bool BindThirdSource(int kind, int native, int generation, in TacticalThirdBatchAttack attack, long frame, ulong canonicalNode = 0)
        {
            if (!attack.IsValid) return true;
            if (attack.Stage != _stage || native <= 0 || generation < 0 || kind != 1 && kind != 2 && kind != 3) return false;
            foreach (var row in _thirdSources) if (row.Kind == kind && row.Native == native && row.Generation == generation)
                return row.Source.Attack.Matches(attack);
            ulong node = canonicalNode != 0 ? canonicalNode : ThirdNode(attack.Actor, kind, native, generation, attack.Revision);
            foreach (var row in _thirdSources) if (row.Source.Node == node) return false;
            var order = ThirdOrder(frame, ThirdBirthRound(frame), 2, (uint)native, node, attack.Actor, 1);
            var source = new TacticalThirdBatchSource(attack, node,
                kind == 1 ? TacticalThirdBatchOrigin.DirectProjectile : kind == 2 ? TacticalThirdBatchOrigin.DirectWeapon : TacticalThirdBatchOrigin.Derived, order);
            if (!source.IsValid) return false;
            _thirdSources.Add(new ThirdSourceRecord(kind, native, generation, source));
            _thirdSources.Sort((a, b) => a.Source.Node.CompareTo(b.Source.Node));
            // Eligibility is decided at accepted birth against already-confirmed owner facts.
            // Phase ordering cannot make a later birth precede a terminal callback.
            if (HasThirdBatch && !_thirdDeparted.Contains(attack.Actor) && !ThirdHostAlreadyEnded(attack)) foreach (var row in _thirdAttacks) {
                if (!row.Attack.Matches(attack)) continue;
                if (!row.Ended) QueueThirdEvent(TacticalThirdBatchEvent.Born(source));
                break;
            }
            _revision++; return true;
        }
        private bool ThirdHostAlreadyEnded(in TacticalThirdBatchAttack attack)
        {
            foreach (var entry in _entries)
                if (entry.Terminal && entry.Key.Stage == attack.Stage && entry.Key.Actor == attack.Actor
                    && entry.Key.Execution == attack.Execution) return true;
            return false;
        }
        internal TacticalThirdBatchSource ThirdSource(int kind, int native, int generation)
        {
            foreach (var row in _thirdSources) if (row.Kind == kind && row.Native == native && row.Generation == generation) return row.Source;
            return default;
        }

        internal void ThirdActorDeparted(GameplayEntityId actor, long frame)
        {
            if (_stage == 0 || !actor.IsValid || _thirdDeparted.Contains(actor)) return;
            _thirdDeparted.Add(actor); _thirdDeparted.Sort();
            // Death/removal is already a committed world fact, visible before the next HP root in this round.
            foreach (var e in _entries) e.ThirdBatch?.RevokeForConfirmedDeparture(actor);
            if (HasThirdBatch) QueueThirdEvent(TacticalThirdBatchEvent.Depart(
                ThirdOrder(frame, ThirdBirthRound(frame), 6, 1, ThirdNode(actor, 32, actor.Index, actor.Generation, 1), actor, 1), actor));
            _revision++;
        }
        private void ThirdHostEnded(Entry entry, long frame)
        {
            if (!HasThirdBatch) return;
            QueueThirdEvent(TacticalThirdBatchEvent.EndHost(ThirdOrder(frame, ThirdBirthRound(frame), 5, entry.Key.Execution,
                ThirdNode(entry.Key.Actor, 33, (long)entry.Key.Execution, 0, entry.Key.Commit), entry.Key.Actor, entry.Key.Commit), entry.Key.Actor, entry.Key.Execution));
        }

        /// <remarks>LowFreqAlloc at round-buffer high water. Freeze complete keys; HP liveness is read later per accepted hit.</remarks>
        internal bool BeginThirdRound(long frame, int round, TacticalThirdBatchEvent[] damage, int count)
        {
            if (_thirdRoundOpen || damage == null || count < 0 || count > damage.Length) return false;
            _thirdCandidateEvents.Clear();
            foreach (var item in _thirdPending) {
                if (item.Order.Frame != frame || item.Order.Round != round) return false;
                _thirdCandidateEvents.Add(item);
            }
            for (int i = 0; i < count; i++) {
                if (damage[i].Order.Frame != frame || damage[i].Order.Round != round) return false;
                _thirdCandidateEvents.Add(damage[i]);
            }
            _thirdCandidateEvents.Sort(CompareThirdEvents);
            for (int i = 1; i < _thirdCandidateEvents.Count; i++)
                if (_thirdCandidateEvents[i-1].Order.CompareTo(_thirdCandidateEvents[i].Order) == 0) return false;
            if (_thirdOrderBuffer.Length < _thirdCandidateEvents.Count) _thirdOrderBuffer = new TacticalThirdBatchOrder[Math.Max(32, _thirdCandidateEvents.Count * 2)];
            // Every leaf preflights on a detached candidate before any leaf is changed.
            foreach (var entry in _entries) {
                if (entry.ThirdBatch == null) continue;
                int size = ThirdKeys(entry.ThirdBatch, _thirdCandidateEvents);
                if (!entry.ThirdBatch.CanOpenProductionRound(frame, round, _thirdOrderBuffer, size)) return false;
            }
            _thirdRoundEvents.Clear(); _thirdRoundEvents.AddRange(_thirdCandidateEvents);
            foreach (var entry in _entries) {
                if (entry.ThirdBatch == null) continue;
                int size = ThirdKeys(entry.ThirdBatch, _thirdRoundEvents);
                if (!entry.ThirdBatch.TryOpenRound(frame, round, _thirdOrderBuffer, size)) throw new InvalidOperationException("third-batch:RoundChanged");
            }
            _thirdPending.Clear(); _thirdFrame = frame; _thirdRound = round; _thirdNext = 0; _thirdRoundOpen = true; _revision++;
            ConsumeThirdLifecycle(); return true;
        }
        internal void ObserveThirdFrame(long frame)
        {
            if (_stage == 0) return;
            if (frame < ThirdObservedFrame) throw new InvalidOperationException("third-batch:FrameRewind");
            if (frame != ThirdObservedFrame) { ThirdObservedFrame = frame; _revision++; }
        }
        // A frame preamble may drain an older frame only. Current-frame ACK/birth and hits
        // belong to the same complete round; closing it here would reject the real round-zero hit.
        internal void FlushThirdLifecycleBefore(long frame)
        {
            if (_thirdPending.Count != 0 && _thirdPending[0].Order.Frame < frame) FlushThirdLifecycle();
        }
        internal void FlushThirdLifecycle()
        {
            if (_thirdRoundOpen) throw new InvalidOperationException("third-batch:FlushDuringDamage");
            if (_thirdPending.Count == 0) return;
            _thirdPending.Sort(CompareThirdEvents);
            var first = _thirdPending[0].Order;
            if (!TryResolveSecondBatchRound(first.Frame, first.Round, Array.Empty<TacticalSecondBatchHit>(), 0, Array.Empty<TacticalSecondBatchHitDecision>())
                || !BeginThirdRound(first.Frame, first.Round, Array.Empty<TacticalThirdBatchEvent>(), 0) || !EndThirdRound())
                throw new InvalidOperationException("third-batch:LifecycleRoundRejected");
        }
        private void QueueThirdEvent(TacticalThirdBatchEvent item)
        { _thirdPending.Add(item); _thirdPending.Sort(CompareThirdEvents); }
        private static int CompareThirdEvents(TacticalThirdBatchEvent a, TacticalThirdBatchEvent b) => a.Order.CompareTo(b.Order);
        private int ThirdKeys(TacticalAccessoryThirdBatchState leaf, List<TacticalThirdBatchEvent> events)
        {
            int count = 0;
            foreach (var item in events) if (ThirdLeafObserves(leaf, item)) _thirdOrderBuffer[count++] = item.Order;
            return count;
        }
        private static bool ThirdLeafObserves(TacticalAccessoryThirdBatchState leaf, in TacticalThirdBatchEvent item)
        {
            if (item.Order.CompareTo(leaf.Commit.ActivationOrder) <= 0) return false;
            // Pre-commit enemy sources may pay risk; their ACK/birth are never retroactively registered.
            if ((item.Kind == TacticalThirdBatchEventKind.SourceBorn || item.Kind == TacticalThirdBatchEventKind.AttackEnded)
                && item.Attack.AcceptedOrder.CompareTo(leaf.Commit.ActivationOrder) <= 0) return false;
            if (item.Kind == TacticalThirdBatchEventKind.SourceBorn && !leaf.CanObserveProductionBirth(item.Source)) return false;
            return true;
        }
        private void ApplyThirdEvent(in TacticalThirdBatchEvent item, out decimal glass, out decimal coin)
        {
            glass = coin = 1000;
            foreach (var entry in _entries) {
                var leaf = entry.ThirdBatch;
                if (leaf == null || !ThirdLeafObserves(leaf, item)) continue;
                if (!leaf.TryApplyNext(item, out var decision)) throw new InvalidOperationException("third-batch:AcceptedEventRejected");
                glass = Math.Min((decimal)int.MaxValue * 1000m, glass * decision.GlassScaleMilli / 1000m);
                coin = Math.Min((decimal)int.MaxValue * 1000m, coin * decision.CoinScaleMilli / 1000m);
            }
            _revision++;
        }
        private void ConsumeThirdLifecycle()
        {
            while (_thirdNext < _thirdRoundEvents.Count && _thirdRoundEvents[_thirdNext].Kind != TacticalThirdBatchEventKind.Damage) {
                ApplyThirdEvent(_thirdRoundEvents[_thirdNext], out _, out _); _thirdNext++;
            }
        }
        internal bool HasAcceptedThirdRoot(in TacticalThirdBatchEvent item)
        {
            foreach (var e in _entries) if (e.ThirdBatch != null && e.ThirdBatch.HasAcceptedProductionRoot(item)) return true;
            return false;
        }
        internal bool ApplyThirdDamage(in TacticalThirdBatchOrder key, bool targetAlive, bool actualEnemy, out decimal glass, out decimal coin)
        {
            glass = coin = 1000;
            if (!_thirdRoundOpen || _thirdNext >= _thirdRoundEvents.Count) return false;
            var scheduled = _thirdRoundEvents[_thirdNext];
            if (scheduled.Kind != TacticalThirdBatchEventKind.Damage || scheduled.Order.CompareTo(key) != 0) return false;
            var current = TacticalThirdBatchEvent.Hit(key, scheduled.Source, scheduled.Outcome, targetAlive, actualEnemy, scheduled.Suppressed);
            ApplyThirdEvent(current, out glass, out coin); _thirdNext++; ConsumeThirdLifecycle(); return true;
        }
        internal bool EndThirdRound()
        {
            if (!_thirdRoundOpen || _thirdNext != _thirdRoundEvents.Count) return false;
            foreach (var e in _entries) if (e.ThirdBatch != null && !e.ThirdBatch.RoundComplete) return false;
            _thirdRoundOpen = false; _thirdRoundEvents.Clear(); _thirdNext = 0; return true;
        }
        internal bool HasPendingThirdEvents(long frame, int round)
            => _thirdPending.Count != 0 && _thirdPending[0].Order.Frame == frame && _thirdPending[0].Order.Round == round;

        internal bool SuppressesThirdMotion(GameplayEntityId recipient, in TacticalThirdBatchAttack source, TacticalThirdBatchMotionChannel channel)
        {
            var holder = CurrentThirdAttack(recipient);
            if (!holder.IsValid || _thirdDeparted.Contains(recipient) || !source.IsValid || source.Stage != _stage) return false;
            foreach (var e in _entries) {
                if (!e.ThirdBatchParameters.IronThrone || e.Terminal || e.Key.Actor != recipient || e.Key.Execution != holder.Execution) continue;
                // ACK is authoritative immediately; pending complete-round consumption must not delay the motor one tick.
                if (channel == TacticalThirdBatchMotionChannel.OrdinaryKnockback) return true;
                if (TacticalAccessoryThirdBatchRules.IsActiveMotion(channel) && holder.Matches(source)) return true;
            }
            return false;
        }
        internal void SettleThirdFullTurn(long frame, ulong settledTurn)
        {
            if (!HasThirdBatch || settledTurn == 0 || settledTurn <= _thirdLastSettledTurn) return;
            if (_thirdRoundOpen) throw new InvalidOperationException("third-batch:SettlementBeforeDamage");
            int round = ThirdBirthRound(frame);
            if (_thirdPending.Count > 0) {
                if (!TryResolveSecondBatchRound(frame, round, Array.Empty<TacticalSecondBatchHit>(), 0, Array.Empty<TacticalSecondBatchHitDecision>())
                    || !BeginThirdRound(frame, round, Array.Empty<TacticalThirdBatchEvent>(), 0) || !EndThirdRound())
                    throw new InvalidOperationException("third-batch:SettlementPendingLifecycle");
                round++;
            }
            if (!TryResolveSecondBatchRound(frame, round, Array.Empty<TacticalSecondBatchHit>(), 0, Array.Empty<TacticalSecondBatchHitDecision>()))
                throw new InvalidOperationException("third-batch:SettlementRoundRejected");
            foreach (var e in _entries) {
                var leaf = e.ThirdBatch; if (leaf == null) continue;
                var order = ThirdOrder(frame, round, 7, settledTurn, leaf.Commit.ActivationOrder.SourceNode, e.Key.Actor, settledTurn);
                var keys = new[] { order };
                if (!leaf.TryOpenRound(frame, round, keys, 1) || !leaf.TryApplyNext(TacticalThirdBatchEvent.SettleFullTurn(order, settledTurn), out _))
                    throw new InvalidOperationException("third-batch:FullSettlementRejected");
            }
            _thirdLastSettledTurn = settledTurn; _thirdFrame = frame; _thirdRound = round; _revision++;
        }
    }
}
