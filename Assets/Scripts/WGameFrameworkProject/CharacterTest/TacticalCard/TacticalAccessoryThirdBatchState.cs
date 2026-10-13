using System;
using System.Collections.Generic;
using MxFramework.Combat.Hit;
using MxFramework.Gameplay;
using MxFramework.Runtime;

namespace MxFramework.Game.CharacterTest.TacticalCard
{
    /// <summary>The actual face, enemy, hard lifetime and RNG AFTER-state published by the legal joint commit.
    /// UI MUST project this receipt after production integration; it cannot generate a separate animation result.</summary>
    internal readonly struct TacticalThirdBatchCoinReceipt
    {
        internal TacticalThirdBatchCoinReceipt(TacticalThirdBatchCommit commit, GameplayEntityId enemy,
            TacticalThirdBatchCoinFace face, RuntimeRandomState afterRandom)
        { Commit = commit; Enemy = enemy; Face = face; AfterRandom = afterRandom; }
        internal TacticalThirdBatchCommit Commit { get; }
        internal GameplayEntityId Enemy { get; }
        internal TacticalThirdBatchCoinFace Face { get; }
        internal RuntimeRandomState AfterRandom { get; }
        internal long DrawCount => AfterRandom.DrawCount;
        internal ulong ExpiresAfterFullTurn => Commit.ExpiresAfterFullTurn;
        internal bool IsValid => Commit.IsValid && Enemy.IsValid && Enemy != Commit.Actor && DrawCount > 0
            && AfterRandom.State != 0 && AfterRandom.AlgorithmId == DeterministicRandom.XorShift32AlgorithmId
            && (Face == TacticalThirdBatchCoinFace.SelfExecution || Face == TacticalThirdBatchCoinFace.EnemyNextAttack);
        internal bool IsEmpty => Commit.Stage == 0 && Commit.Commit == 0 && Commit.Execution == 0 && !Commit.Actor.IsValid
            && Commit.CardInstance == 0 && Commit.TacticalTurn == 0 && !Commit.ActivationOrder.IsValid && !Enemy.IsValid
            && Face == TacticalThirdBatchCoinFace.Unpublished && AfterRandom.AlgorithmId == null
            && AfterRandom.Seed == 0 && AfterRandom.State == 0 && AfterRandom.DrawCount == 0;
    }

    /// <summary>One private coin RNG per Stage. No deck/Unity.Random references or injected shared stream.
    /// TryPublish follows FULL lawful joint admission/payment; this type does not admit or pay a card.
    /// Keep receipts after cancel until Stage disposal. Restore the ledger WITH commit/effect state, never RNG alone.</summary>
    internal sealed partial class TacticalThirdBatchCoinLedger
    {
        private readonly ulong _stage;
        private readonly DeterministicRandom _random;
        private readonly List<TacticalThirdBatchCoinReceipt> _receipts = new List<TacticalThirdBatchCoinReceipt>();
        internal TacticalThirdBatchCoinLedger(ulong stage, uint seed)
        {
            if (stage == 0) throw new ArgumentOutOfRangeException(nameof(stage));
            _stage = stage; _random = new DeterministicRandom(seed);
        }
        internal long DrawCount => _random.CaptureState().DrawCount;
        internal RuntimeRandomState RandomState => _random.CaptureState();
        internal int ReceiptCount => _receipts.Count;
        /// <remarks>NoAlloc. Preview/rejection never draws, exposes a speculative face or changes the target.</remarks>
        internal bool CanPublish(in TacticalThirdBatchCommit commit, GameplayEntityId enemy)
        {
            if (!commit.IsValid || commit.Stage != _stage || !enemy.IsValid || enemy == commit.Actor) return false;
            int index = Find(commit.Commit);
            return index < 0 || (_receipts[index].Commit.Matches(commit) && _receipts[index].Enemy == enemy);
        }
        /// <remarks>LowFreqAlloc. First legal commit draws ONE unbiased NextInt(0,2). The detached RNG candidate
        /// and immutable receipt are prepared before publishing result + after-state, without intervening callbacks.
        /// The common joint owner still stages this ledger with payment/execution, then emits UI only after success.</remarks>
        internal bool TryPublish(bool admissionAccepted, in TacticalThirdBatchCommit commit, GameplayEntityId enemy,
            out TacticalThirdBatchCoinReceipt receipt)
        {
            receipt = default;
            if (!admissionAccepted || !CanPublish(commit, enemy)) return false;
            int index = Find(commit.Commit);
            if (index >= 0) { receipt = _receipts[index]; return true; }
            if (DrawCount == long.MaxValue) return false;
            if (_receipts.Count == _receipts.Capacity)
                _receipts.Capacity = _receipts.Count < 4 ? 4 : checked(_receipts.Count + _receipts.Count / 2 + 1);
            var candidate = new DeterministicRandom(RandomState.Seed);
            candidate.RestoreState(RandomState);
            var face = candidate.NextInt(0, 2) == 0 ? TacticalThirdBatchCoinFace.SelfExecution : TacticalThirdBatchCoinFace.EnemyNextAttack;
            receipt = new TacticalThirdBatchCoinReceipt(commit, enemy, face, candidate.CaptureState());
            _receipts.Add(receipt); _random.RestoreState(receipt.AfterRandom); return true;
        }
        private int Find(ulong commit)
        { for (int i = 0; i < _receipts.Count; i++) if (_receipts[i].Commit.Commit == commit) return i; return -1; }
        /// <remarks>AllocByDesign. Trusted detached in-process pause candidate, not disk/network admission.</remarks>
        internal TacticalThirdBatchCoinLedger CloneForTrustedPause()
        {
            var copy = new TacticalThirdBatchCoinLedger(_stage, RandomState.Seed);
            copy._random.RestoreState(RandomState); copy._receipts.AddRange(_receipts); return copy;
        }
    }

    /// <summary>Local rule state for 05/14/19, consumed by the Stage aggregate. COMPLETE stable-key schedules are frozen per causal round,
    /// then each event reads fresh pre-armor defense/liveness. Common owner atomically accepts factors, consumption,
    /// armor and HP; discard a detached candidate on failure. Actual producer/ACK/source proof is the owner's input.
    /// FullTurnSettled is a singleton POST-simulation round after ALL damage/causal children, and seals that frame.
    /// Next full actual tactical turn settlement, holder death/departure and Stage end are hard glass boundaries.</summary>
    internal sealed partial class TacticalAccessoryThirdBatchState
    {
        private readonly struct RegisteredSource
        {
            internal RegisteredSource(TacticalThirdBatchSource source, bool coinEligible)
            { Source = source; CoinEligible = coinEligible; }
            internal TacticalThirdBatchSource Source { get; }
            internal bool CoinEligible { get; }
        }
        private readonly struct AcceptedRoot
        {
            internal AcceptedRoot(ulong node, GameplayEntityId target, ulong occurrence)
            { Node = node; Target = target; Occurrence = occurrence; }
            internal ulong Node { get; }
            internal GameplayEntityId Target { get; }
            internal ulong Occurrence { get; }
        }
        private readonly List<TacticalThirdBatchAttack> _attacks = new List<TacticalThirdBatchAttack>();
        private readonly List<TacticalThirdBatchAttack> _endedAttacks = new List<TacticalThirdBatchAttack>();
        private readonly List<RegisteredSource> _sources = new List<RegisteredSource>();
        private readonly List<AcceptedRoot> _roots = new List<AcceptedRoot>();
        private readonly List<GameplayEntityId> _departed = new List<GameplayEntityId>();
        private TacticalThirdBatchOrder[] _schedule = Array.Empty<TacticalThirdBatchOrder>();
        private int _next, _scheduleCount;
        private long _frame = -1;
        private int _round = -1;
        private long _settledFrame = -1;
        private ulong _lastSettledTurn;
        private bool _hostEnded;
        private bool _battleEnded;
        private bool _coinRevoked;
        private bool _ownerDepartureConfirmed;
        private TacticalThirdBatchAttack _enemyAttack;
        private readonly bool _ironThrone;
        internal TacticalAccessoryThirdBatchState(TacticalThirdBatchCommit commit, bool glassCannon, bool ironThrone,
            TacticalThirdBatchCoinReceipt coin = default)
        {
            if (!commit.IsValid) throw new ArgumentException("Invalid trial commit.", nameof(commit));
            if (!coin.IsEmpty && (!coin.IsValid || !coin.Commit.Matches(commit)))
                throw new ArgumentException("Coin receipt must match the exact commit.", nameof(coin));
            Commit = commit; Coin = coin; _ironThrone = ironThrone;
            GlassPhase = glassCannon ? TacticalThirdBatchGlassPhase.OutputAndRiskPending : TacticalThirdBatchGlassPhase.Absent;
            CoinPhase = !coin.IsValid ? TacticalThirdBatchCoinPhase.Absent : coin.Face == TacticalThirdBatchCoinFace.SelfExecution
                ? TacticalThirdBatchCoinPhase.SelfExecution : TacticalThirdBatchCoinPhase.WaitingEnemy;
        }
        internal TacticalThirdBatchCommit Commit { get; }
        internal TacticalThirdBatchCoinReceipt Coin { get; }
        internal TacticalThirdBatchGlassPhase GlassPhase { get; private set; }
        internal TacticalThirdBatchCoinPhase CoinPhase { get; private set; }
        internal TacticalThirdBatchAttack BoundEnemyAttack => _enemyAttack;
        internal bool RoundComplete => _next == _scheduleCount;
        internal bool IronThroneActive => _ironThrone && HasLiveOwnAction() && !_hostEnded && !_battleEnded && !_ownerDepartureConfirmed && !HasDeparted(Commit.Actor);

        /// <remarks>LowFreqAlloc at capacity growth. Reusable storage copies/sorts producer keys, never stale HP
        /// facts or caller arrays. Only the logical count participates in hash and trusted restoration.
        /// Duplicate keys, unfinished rounds, skipped causal rounds and reopenings fail without mutation.
        /// Frame time alone never expires a tactical-turn promise. Commit activation is also a stable key.</remarks>
        internal bool TryOpenRound(long frame, int round, TacticalThirdBatchOrder[] keys, int count)
        {
            if (!RoundComplete || keys == null || count < 0 || count > keys.Length || frame < Commit.Frame
                || frame >= long.MaxValue || frame == _settledFrame || round < 0 || round >= int.MaxValue
                || (_frame < 0 ? round != (frame == Commit.Frame ? Commit.ActivationOrder.Round : 0)
                    : frame > _frame ? round != 0 : frame != _frame || _round >= int.MaxValue - 1 || round != _round + 1)) return false;
            for (int i = 0; i < count; i++)
                if (!keys[i].IsValid || keys[i].Frame != frame || keys[i].Round != round
                    || keys[i].CompareTo(Commit.ActivationOrder) <= 0) return false;
            for (int i = 0; i < count; i++) for (int j = 0; j < i; j++) if (keys[i].CompareTo(keys[j]) == 0) return false;
            if (_schedule.Length < count) _schedule = new TacticalThirdBatchOrder[Math.Max(32, count)];
            Array.Copy(keys, _schedule, count); Array.Sort(_schedule, 0, count);
            if (_roots.Capacity - _roots.Count < count) _roots.Capacity = checked(_roots.Count + Math.Max(32, count));
            _scheduleCount = count; _next = 0; _frame = frame; _round = round; return true;
        }

        /// <remarks>LowFreqAlloc on ACK/source/departure; Damage is NoAlloc. Replaying a key never provides
        /// another actionable multiplier. Direct Damage consumes glass even with armor=all or nonlethal HP clipping.
        /// Block/parry/evade/self/derived and a previously suppressed one-punch root do not consume.
        /// Glass is original conditional settlement: output first settles ALL; risk first pays risk ONCE and leaves output.</remarks>
        internal bool TryApplyNext(in TacticalThirdBatchEvent item, out TacticalThirdBatchDecision decision)
        {
            decision = TacticalThirdBatchDecision.Neutral;
            if (RoundComplete || !item.Order.IsValid || item.Order.CompareTo(_schedule[_next]) != 0 || !Valid(item)) return false;
            bool coinBound = false;
            switch (item.Kind)
            {
                case TacticalThirdBatchEventKind.AttackStarted:
                    // AcceptedOrder is frozen ACK payload, not physical identity. Retain this identity
                    // after termination so a conflicting order cannot resurrect the same action revision.
                    if (FindPhysicalAttack(item.Attack) >= 0) return false;
                    _attacks.Add(item.Attack);
                    if (!_coinRevoked && CoinPhase == TacticalThirdBatchCoinPhase.WaitingEnemy && item.Actor == Coin.Enemy
                        && item.ActorAlive && item.ActualEnemy && item.EligibleAttack && !HasDeparted(item.Actor))
                    { _enemyAttack = item.Attack; CoinPhase = TacticalThirdBatchCoinPhase.EnemyAttack; coinBound = true; }
                    break;
                case TacticalThirdBatchEventKind.SourceBorn:
                    bool eligible = !_coinRevoked && !_battleEnded && item.Source.IsDirect && !HasDeparted(item.Actor)
                        && !AttackEnded(item.Attack)
                        && ((CoinPhase == TacticalThirdBatchCoinPhase.SelfExecution && !_hostEnded
                            && item.Actor == Commit.Actor && item.Execution == Commit.Execution)
                            || (CoinPhase == TacticalThirdBatchCoinPhase.EnemyAttack && item.Attack.Matches(_enemyAttack)));
                    _sources.Add(new RegisteredSource(item.Source, eligible));
                    break;
                case TacticalThirdBatchEventKind.Damage:
                    // A producer's damage occurrence is immutable across frame/event aliases. A true Duplicate
                    // classification may be acknowledged neutrally, but cannot create another actionable factor.
                    bool seenRoot = SeenRoot(item);
                    if (seenRoot && item.Outcome != HitResolveKind.Duplicate) return false;
                    if (!seenRoot) _roots.Add(new AcceptedRoot(item.Source.Node, item.Actor, item.Order.Occurrence));
                    int glassScale = 1000, coinScale = 1000;
                    bool consumed = false;
                    if (!_battleEnded && item.IsDirectDamage && !HasDeparted(item.Actor))
                    {
                        if (!HasDeparted(Commit.Actor) && GlassPhase != TacticalThirdBatchGlassPhase.Absent
                            && GlassPhase != TacticalThirdBatchGlassPhase.Settled)
                        {
                            // Old self sources cannot be registered late or supplemented at impact.
                            if (item.Attack.Actor == Commit.Actor && item.Attack.AcceptedOrder.CompareTo(Commit.ActivationOrder) > 0
                                && FindSource(item.Source) >= 0)
                            { glassScale = TacticalAccessoryThirdBatchRules.TrialScaleMilli; GlassPhase = TacticalThirdBatchGlassPhase.Settled; consumed = true; }
                            else if (item.Actor == Commit.Actor && GlassPhase == TacticalThirdBatchGlassPhase.OutputAndRiskPending)
                            { glassScale = TacticalAccessoryThirdBatchRules.TrialScaleMilli; GlassPhase = TacticalThirdBatchGlassPhase.RiskPaidOutputPending; consumed = true; }
                        }
                        int sourceIndex = FindSource(item.Source);
                        if (!_coinRevoked && sourceIndex >= 0 && _sources[sourceIndex].CoinEligible)
                            coinScale = TacticalAccessoryThirdBatchRules.TrialScaleMilli;
                    }
                    decision = new TacticalThirdBatchDecision(glassScale, coinScale, consumed, false);
                    break;
                case TacticalThirdBatchEventKind.AttackEnded:
                    if (AttackEnded(item.Attack)) return false;
                    _endedAttacks.Add(item.Attack);
                    if (item.Attack.Matches(_enemyAttack)) EndCoinExecution();
                    break;
                case TacticalThirdBatchEventKind.HostEnded:
                    if (item.Actor == Commit.Actor && item.Execution == Commit.Execution)
                    { _hostEnded = true; if (CoinPhase == TacticalThirdBatchCoinPhase.SelfExecution) EndCoinExecution(); }
                    break;
                case TacticalThirdBatchEventKind.ActorDeparted:
                    if (!HasDeparted(item.Actor)) { _departed.Add(item.Actor); _departed.Sort(); }
                    RevokeForConfirmedDeparture(item.Actor);
                    break;
                case TacticalThirdBatchEventKind.BattleEnded:
                    _battleEnded = true; SettleGlass(); RevokeCoin(); break;
                case TacticalThirdBatchEventKind.FullTurnSettled:
                    _lastSettledTurn = item.TacticalTurn; _settledFrame = item.Order.Frame;
                    if (item.TacticalTurn >= Commit.ExpiresAfterFullTurn) { SettleGlass(); RevokeCoin(); }
                    break;
            }
            if (coinBound) decision = new TacticalThirdBatchDecision(1000, 1000, false, true);
            _next++; return true;
        }

        private bool Valid(in TacticalThirdBatchEvent item)
        {
            if (!item.Actor.IsValid || item.Order.Recipient != item.Actor) return false;
            switch (item.Kind)
            {
                case TacticalThirdBatchEventKind.AttackStarted:
                    return item.Attack.IsValid && item.Attack.Stage == Commit.Stage && item.Actor == item.Attack.Actor
                        && item.ActorAlive && !HasDeparted(item.Actor)
                        && !(item.Actor == Commit.Actor && item.Execution == Commit.Execution && _hostEnded)
                        && item.Order.CompareTo(item.Attack.AcceptedOrder) == 0;
                case TacticalThirdBatchEventKind.SourceBorn:
                    return item.Source.IsValid && item.Source.Attack.Stage == Commit.Stage && FindAttack(item.Attack) >= 0
                        && !HasDeparted(item.Actor) && !AttackEnded(item.Attack) && FindSourceNode(item.Source.Node) < 0
                        && !(item.Actor == Commit.Actor && item.Execution == Commit.Execution && _hostEnded)
                        && item.Order.CompareTo(item.Source.BirthOrder) == 0;
                case TacticalThirdBatchEventKind.AttackEnded:
                    return item.Attack.IsValid && item.Attack.Stage == Commit.Stage && item.Actor == item.Attack.Actor && FindAttack(item.Attack) >= 0;
                case TacticalThirdBatchEventKind.Damage:
                    // A pre-commit enemy projectile may pay the glass risk, but cannot gain a coin/output bonus.
                    return item.Source.IsValid && item.Attack.Stage == Commit.Stage && item.Order.SourceNode == item.Source.Node
                        && item.Order.CompareTo(item.Source.BirthOrder) > 0 && (int)item.Outcome >= 1 && (int)item.Outcome <= 9
                        && (FindSourceNode(item.Source.Node) < 0 || FindSource(item.Source) >= 0);
                case TacticalThirdBatchEventKind.HostEnded: return item.Execution != 0;
                case TacticalThirdBatchEventKind.ActorDeparted:
                case TacticalThirdBatchEventKind.BattleEnded: return true;
                case TacticalThirdBatchEventKind.FullTurnSettled:
                    return _scheduleCount == 1 && _next == 0 && item.TacticalTurn >= Commit.TacticalTurn
                        && item.TacticalTurn > _lastSettledTurn;
                default: return false;
            }
        }

        /// <remarks>NoAlloc. Per-channel filter BEFORE synthesis; no global request clearing. Source generation,
        /// card execution, physical instance and revision come from actual ACK. Never infer identity from ActionId.
        /// Full horizontal/vertical root, active impulse/velocity/jump/gravity-waiver are suppressed for THIS execution.
        /// Existing airborne velocity, gravity, landing, external launch, yaw, trajectory and special forces remain.
        /// The caller MUST provide the recipient's ACTUAL CURRENT ACK as holder, independently of the attacker's
        /// source. This prevents a delayed old-host end notification from carrying immunity into a new execution.
        /// Ordinary knockback is separately refused with both real identities while this execution is active.</remarks>
        internal bool SuppressesMotion(in TacticalThirdBatchAttack holder, in TacticalThirdBatchAttack source,
            TacticalThirdBatchMotionChannel channel)
        {
            if (!IronThroneActive || !holder.IsValid || holder.Actor != Commit.Actor || holder.Stage != Commit.Stage
                || holder.Execution != Commit.Execution || FindAttack(holder) < 0 || AttackEnded(holder)
                || !source.IsValid || source.Stage != Commit.Stage) return false;
            return channel == TacticalThirdBatchMotionChannel.OrdinaryKnockback
                || (TacticalAccessoryThirdBatchRules.IsActiveMotion(channel) && source.Actor == Commit.Actor
                    && source.Execution == Commit.Execution && FindAttack(source) >= 0 && !AttackEnded(source));
        }
        private int FindAttack(in TacticalThirdBatchAttack attack)
        {
            int index = FindPhysicalAttack(attack);
            return index >= 0 && _attacks[index].Matches(attack) ? index : -1;
        }
        private int FindPhysicalAttack(in TacticalThirdBatchAttack attack)
        {
            if (!attack.IsValid) return -1;
            for (int i = 0; i < _attacks.Count; i++)
                if (_attacks[i].Stage == attack.Stage && _attacks[i].Execution == attack.Execution
                    && _attacks[i].Actor == attack.Actor && _attacks[i].ActionInstance == attack.ActionInstance
                    && _attacks[i].Revision == attack.Revision) return i;
            return -1;
        }
        private bool AttackEnded(in TacticalThirdBatchAttack attack)
        { for (int i = 0; i < _endedAttacks.Count; i++) if (_endedAttacks[i].Matches(attack)) return true; return false; }
        private bool HasLiveOwnAction()
        {
            for (int i = 0; i < _attacks.Count; i++)
                if (_attacks[i].Actor == Commit.Actor && _attacks[i].Execution == Commit.Execution && !AttackEnded(_attacks[i])) return true;
            return false;
        }
        private int FindSourceNode(ulong node)
        { for (int i = 0; i < _sources.Count; i++) if (_sources[i].Source.Node == node) return i; return -1; }
        private int FindSource(in TacticalThirdBatchSource source)
        { int index = FindSourceNode(source.Node); return index >= 0 && _sources[index].Source.Matches(source) ? index : -1; }
        private bool HasDeparted(GameplayEntityId actor)
        { for (int i = 0; i < _departed.Count; i++) if (_departed[i] == actor) return true; return false; }
        private bool SeenRoot(in TacticalThirdBatchEvent item)
        {
            for (int i = 0; i < _roots.Count; i++)
                if (_roots[i].Node == item.Source.Node && _roots[i].Target == item.Actor && _roots[i].Occurrence == item.Order.Occurrence) return true;
            return false;
        }
        // World death is visible between HP roots immediately. Ordered departure remains in the schedule,
        // so genuine ACK/birth events accepted before it can finish without reviving any paid effect.
        internal void RevokeForConfirmedDeparture(GameplayEntityId actor)
        {
            if (!actor.IsValid) return;
            if (actor == Commit.Actor) { _ownerDepartureConfirmed = true; SettleGlass(); }
            if (actor == Coin.Enemy && Coin.Face == TacticalThirdBatchCoinFace.EnemyNextAttack
                || actor == Commit.Actor && Coin.Face == TacticalThirdBatchCoinFace.SelfExecution) RevokeCoin();
        }
        private void SettleGlass()
        { if (GlassPhase != TacticalThirdBatchGlassPhase.Absent) GlassPhase = TacticalThirdBatchGlassPhase.Settled; }
        private void EndCoinExecution()
        { if (CoinPhase != TacticalThirdBatchCoinPhase.Absent) CoinPhase = TacticalThirdBatchCoinPhase.Ended; }
        private void RevokeCoin() { _coinRevoked = true; EndCoinExecution(); }

        /// <remarks>AllocByDesign. Trusted independent in-process candidate includes partial schedule, frozen
        /// projectile eligibility and terminal facts. Not a shared snapshot/hash/network schema.
        /// Clone the coin ledger in the SAME outer pause/transaction boundary.</remarks>
        internal TacticalAccessoryThirdBatchState CloneForTrustedPause()
        {
            var copy = new TacticalAccessoryThirdBatchState(Commit, false, _ironThrone, Coin) {
                _next = _next, _scheduleCount = _scheduleCount, _frame = _frame, _round = _round, _hostEnded = _hostEnded,
                _battleEnded = _battleEnded, _enemyAttack = _enemyAttack, CoinPhase = CoinPhase, GlassPhase = GlassPhase,
                _schedule = (TacticalThirdBatchOrder[])_schedule.Clone(),
                _coinRevoked = _coinRevoked, _ownerDepartureConfirmed = _ownerDepartureConfirmed,
                _lastSettledTurn = _lastSettledTurn, _settledFrame = _settledFrame
            };
            copy._attacks.AddRange(_attacks); copy._endedAttacks.AddRange(_endedAttacks);
            copy._sources.AddRange(_sources); copy._departed.AddRange(_departed); copy._roots.AddRange(_roots);
            int rootCapacity = checked(copy._roots.Count + _scheduleCount - _next);
            if (copy._roots.Capacity < rootCapacity) copy._roots.Capacity = rootCapacity;
            return copy;
        }
    }
}
