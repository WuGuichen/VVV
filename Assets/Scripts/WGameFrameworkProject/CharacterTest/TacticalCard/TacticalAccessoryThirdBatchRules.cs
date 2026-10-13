using System;
using MxFramework.Combat.Hit;
using MxFramework.Gameplay;

namespace MxFramework.Game.CharacterTest.TacticalCard
{
    /// <summary>Local trial identity. The joint owner supplies these facts after admission/payment;
    /// this value is not an admission receipt, a transport contract, or a new card ID.</summary>
    internal readonly struct TacticalThirdBatchCommit
    {
        internal TacticalThirdBatchCommit(ulong stage, ulong commit, ulong execution,
            GameplayEntityId actor, ulong cardInstance, ulong tacticalTurn, TacticalThirdBatchOrder activationOrder)
        { Stage = stage; Commit = commit; Execution = execution; Actor = actor; CardInstance = cardInstance;
            TacticalTurn = tacticalTurn; ActivationOrder = activationOrder; }
        internal ulong Stage { get; }
        internal ulong Commit { get; }
        internal ulong Execution { get; }
        internal GameplayEntityId Actor { get; }
        internal ulong CardInstance { get; }
        internal ulong TacticalTurn { get; }
        internal ulong ExpiresAfterFullTurn => TacticalTurn + 1;
        internal TacticalThirdBatchOrder ActivationOrder { get; }
        internal long Frame => ActivationOrder.Frame;
        internal bool IsValid => Stage != 0 && Commit != 0 && Execution != 0 && Actor.IsValid
            && CardInstance != 0 && TacticalTurn != 0 && TacticalTurn < ulong.MaxValue
            && ActivationOrder.IsValid && ActivationOrder.Recipient == Actor;
        internal bool Matches(in TacticalThirdBatchCommit other) => IsValid && other.IsValid
            && Stage == other.Stage && Commit == other.Commit && Execution == other.Execution
            && Actor == other.Actor && CardInstance == other.CardInstance && TacticalTurn == other.TacticalTurn
            && ActivationOrder.CompareTo(other.ActivationOrder) == 0;
    }

    /// <summary>Producer identity, never an ActionId/template name or the actor's action at impact.
    /// A projectile retains this stamp at birth. ActionInstance and Revision describe the real ACK.</summary>
    internal readonly struct TacticalThirdBatchAttack
    {
        internal TacticalThirdBatchAttack(ulong stage, ulong execution, GameplayEntityId actor,
            int actionInstance, ulong revision, TacticalThirdBatchOrder acceptedOrder)
        { Stage = stage; Execution = execution; Actor = actor; ActionInstance = actionInstance;
            Revision = revision; AcceptedOrder = acceptedOrder; }
        internal ulong Stage { get; }
        internal ulong Execution { get; }
        internal GameplayEntityId Actor { get; }
        internal int ActionInstance { get; }
        internal ulong Revision { get; }
        internal TacticalThirdBatchOrder AcceptedOrder { get; }
        internal bool IsValid => Stage != 0 && Execution != 0 && Actor.IsValid && ActionInstance > 0 && Revision != 0
            && AcceptedOrder.IsValid && AcceptedOrder.Recipient == Actor;
        internal bool Matches(in TacticalThirdBatchAttack other) => IsValid && other.IsValid
            && Stage == other.Stage && Execution == other.Execution && Actor == other.Actor
            && ActionInstance == other.ActionInstance && Revision == other.Revision
            && AcceptedOrder.CompareTo(other.AcceptedOrder) == 0;
    }

    /// <summary>Frozen real trace/projectile source at birth. The producer must prove origin and actual node.
    /// A node cannot borrow the holder's current execution at impact. Direct damage needs BOTH an origin whitelist
    /// and a matching actual source. Source registration is not a cryptographic/provenance verifier.</summary>
    internal readonly struct TacticalThirdBatchSource
    {
        internal TacticalThirdBatchSource(TacticalThirdBatchAttack attack, ulong node,
            TacticalThirdBatchOrigin origin, TacticalThirdBatchOrder birthOrder)
        { Attack = attack; Node = node; Origin = origin; BirthOrder = birthOrder; }
        internal TacticalThirdBatchAttack Attack { get; }
        internal ulong Node { get; }
        internal TacticalThirdBatchOrigin Origin { get; }
        internal TacticalThirdBatchOrder BirthOrder { get; }
        internal bool IsValid => Attack.IsValid && Node != 0 && BirthOrder.IsValid
            && BirthOrder.SourceNode == Node && BirthOrder.Recipient == Attack.Actor
            && BirthOrder.CompareTo(Attack.AcceptedOrder) > 0 && (int)Origin >= 0 && (int)Origin <= 5;
        internal bool IsDirect => Origin == TacticalThirdBatchOrigin.DirectWeapon || Origin == TacticalThirdBatchOrigin.DirectProjectile;
        internal bool Matches(in TacticalThirdBatchSource other) => IsValid && other.IsValid
            && Attack.Matches(other.Attack) && Node == other.Node && Origin == other.Origin
            && BirthOrder.CompareTo(other.BirthOrder) == 0;
    }

    /// <summary>Stable order: frame, causal round, producer event, producer node, recipient generation,
    /// root occurrence. The common owner collects the COMPLETE causal round before opening it.
    /// Slot callbacks and callback arrival counters are not stable producer identities.</summary>
    internal readonly struct TacticalThirdBatchOrder : IComparable<TacticalThirdBatchOrder>
    {
        internal TacticalThirdBatchOrder(long frame, int round, ulong stableEvent, ulong sourceNode,
            GameplayEntityId recipient, ulong occurrence)
        { Frame = frame; Round = round; StableEvent = stableEvent; SourceNode = sourceNode; Recipient = recipient; Occurrence = occurrence; }
        internal long Frame { get; }
        internal int Round { get; }
        internal ulong StableEvent { get; }
        internal ulong SourceNode { get; }
        internal GameplayEntityId Recipient { get; }
        internal ulong Occurrence { get; }
        internal bool IsValid => Frame >= 0 && Frame < long.MaxValue && Round >= 0 && Round < int.MaxValue
            && StableEvent != 0 && SourceNode != 0 && Recipient.IsValid && Occurrence != 0;
        public int CompareTo(TacticalThirdBatchOrder other)
        {
            int result = Frame.CompareTo(other.Frame);
            if (result == 0) result = Round.CompareTo(other.Round);
            if (result == 0) result = StableEvent.CompareTo(other.StableEvent);
            if (result == 0) result = SourceNode.CompareTo(other.SourceNode);
            if (result == 0) result = Recipient.CompareTo(other.Recipient);
            return result == 0 ? Occurrence.CompareTo(other.Occurrence) : result;
        }
    }

    // These rule enums remain internal to the ThirdBatch state; production accessory kinds map to this contract.
    internal enum TacticalThirdBatchOrigin { Unknown, DirectWeapon, DirectProjectile, Derived, Periodic, Area }
    internal enum TacticalThirdBatchEventKind { AttackStarted, SourceBorn, Damage, AttackEnded, HostEnded, ActorDeparted, BattleEnded, FullTurnSettled }
    internal enum TacticalThirdBatchCoinFace { Unpublished, SelfExecution, EnemyNextAttack }
    internal enum TacticalThirdBatchCoinPhase { Absent, SelfExecution, WaitingEnemy, EnemyAttack, Ended }
    internal enum TacticalThirdBatchGlassPhase { Absent, OutputAndRiskPending, RiskPaidOutputPending, Settled }
    internal enum TacticalThirdBatchMotionChannel
    { Unknown, ActiveTranslation, RootTranslation, OrdinaryKnockback, Yaw, WeaponTrajectory, SpecialExternalForce,
        Gravity, ActiveImpulse, ActiveVelocityOverride, NewJump, ActiveGravitySuppression, ExistingAirborneVelocity, ExternalLaunch }

    /// <summary>Facts are read at this event's serial acceptance boundary, before armor/HP.
    /// Armor fully absorbing a Damage result does not change qualification. Defense and derived results do.
    /// HostEnded is the CARD execution end (including cancel/interrupt), AttackEnded the exact physical ACK end.</summary>
    internal readonly struct TacticalThirdBatchEvent
    {
        private TacticalThirdBatchEvent(TacticalThirdBatchEventKind kind, TacticalThirdBatchOrder order,
            TacticalThirdBatchAttack attack, GameplayEntityId actor, ulong execution,
            TacticalThirdBatchOrigin origin, HitResolveKind outcome, bool alive, bool enemy, bool eligibleAttack,
            TacticalThirdBatchSource source = default, ulong tacticalTurn = 0, bool suppressed = false)
        { Kind = kind; Order = order; Attack = attack; Actor = actor; Execution = execution;
            Origin = origin; Outcome = outcome; ActorAlive = alive; ActualEnemy = enemy; EligibleAttack = eligibleAttack;
            Source = source; TacticalTurn = tacticalTurn; Suppressed = suppressed; }
        internal TacticalThirdBatchEventKind Kind { get; }
        internal TacticalThirdBatchOrder Order { get; }
        internal TacticalThirdBatchAttack Attack { get; }
        internal GameplayEntityId Actor { get; }
        internal ulong Execution { get; }
        internal TacticalThirdBatchOrigin Origin { get; }
        internal HitResolveKind Outcome { get; }
        internal bool ActorAlive { get; }
        internal bool ActualEnemy { get; }
        internal bool EligibleAttack { get; }
        internal TacticalThirdBatchSource Source { get; }
        internal ulong TacticalTurn { get; }
        internal bool Suppressed { get; }
        internal static TacticalThirdBatchEvent Start(TacticalThirdBatchOrder order, TacticalThirdBatchAttack attack,
            bool actorAlive, bool actualEnemy, bool eligibleAttack)
            => new TacticalThirdBatchEvent(TacticalThirdBatchEventKind.AttackStarted, order, attack, attack.Actor,
                attack.Execution, 0, 0, actorAlive, actualEnemy, eligibleAttack);
        internal static TacticalThirdBatchEvent Born(TacticalThirdBatchSource source)
            => new TacticalThirdBatchEvent(TacticalThirdBatchEventKind.SourceBorn, source.BirthOrder, source.Attack,
                source.Attack.Actor, source.Attack.Execution, source.Origin, 0, false, false, false, source);
        internal static TacticalThirdBatchEvent Hit(TacticalThirdBatchOrder order, TacticalThirdBatchSource source,
            HitResolveKind outcome, bool targetAlive, bool actualEnemy, bool suppressed = false)
            => new TacticalThirdBatchEvent(TacticalThirdBatchEventKind.Damage, order, source.Attack, order.Recipient,
                source.Attack.Execution, source.Origin, outcome, targetAlive, actualEnemy, false, source, 0, suppressed);
        internal static TacticalThirdBatchEvent EndAttack(TacticalThirdBatchOrder order, TacticalThirdBatchAttack attack)
            => new TacticalThirdBatchEvent(TacticalThirdBatchEventKind.AttackEnded, order, attack, attack.Actor,
                attack.Execution, 0, 0, false, false, false);
        internal static TacticalThirdBatchEvent EndHost(TacticalThirdBatchOrder order, GameplayEntityId actor, ulong execution)
            => new TacticalThirdBatchEvent(TacticalThirdBatchEventKind.HostEnded, order, default, actor, execution, 0, 0, false, false, false);
        internal static TacticalThirdBatchEvent Depart(TacticalThirdBatchOrder order, GameplayEntityId actor)
            => new TacticalThirdBatchEvent(TacticalThirdBatchEventKind.ActorDeparted, order, default, actor, 0, 0, 0, false, false, false);
        internal static TacticalThirdBatchEvent EndBattle(TacticalThirdBatchOrder order)
            => new TacticalThirdBatchEvent(TacticalThirdBatchEventKind.BattleEnded, order, default, order.Recipient, 0, 0, 0, false, false, false);
        internal static TacticalThirdBatchEvent SettleFullTurn(TacticalThirdBatchOrder order, ulong tacticalTurn)
            => new TacticalThirdBatchEvent(TacticalThirdBatchEventKind.FullTurnSettled, order, default, order.Recipient,
                0, 0, 0, false, false, false, default, tacticalTurn);
        internal bool IsDirectDamage => Kind == TacticalThirdBatchEventKind.Damage && ActorAlive && ActualEnemy && !Suppressed
            && Attack.Actor != Actor && Outcome == HitResolveKind.Damage
            && (Origin == TacticalThirdBatchOrigin.DirectWeapon || Origin == TacticalThirdBatchOrigin.DirectProjectile);
    }

    internal readonly struct TacticalThirdBatchDecision
    {
        internal TacticalThirdBatchDecision(int glassScale, int coinScale, bool glassConsumed, bool coinBound)
        { GlassScaleMilli = glassScale; CoinScaleMilli = coinScale; GlassConsumed = glassConsumed; CoinBound = coinBound; }
        internal int GlassScaleMilli { get; }
        internal int CoinScaleMilli { get; }
        internal bool GlassConsumed { get; }
        internal bool CoinBound { get; }
        internal static TacticalThirdBatchDecision Neutral => new TacticalThirdBatchDecision(1000, 1000, false, false);
    }

    internal static class TacticalAccessoryThirdBatchRules
    {
        internal const int TrialScaleMilli = 1500;
        /// <remarks>NoAlloc. Returned factors join ALL batches at the common pre-armor owner.
        /// Do not sequentially round each accessory. This local two-factor check never writes actual HP.</remarks>
        internal static bool TryScale(int baseDamage, int glassScaleMilli, int coinScaleMilli, out int damage)
        {
            damage = baseDamage;
            if (baseDamage < 0 || glassScaleMilli < 1000 || coinScaleMilli < 1000) return false;
            decimal value = (decimal)baseDamage * glassScaleMilli / 1000m * coinScaleMilli / 1000m;
            damage = value >= int.MaxValue ? int.MaxValue : (int)value;
            return true;
        }

        /// <remarks>NoAlloc. Translation includes horizontal AND vertical components. A new jump's gravity waiver
        /// is paired with the suppressed jump. Already airborne velocity, normal gravity and external launches remain.
        /// These channel rules are a minimum contract; actual motor classification and vertical visuals are unverified.</remarks>
        internal static bool IsActiveMotion(TacticalThirdBatchMotionChannel channel)
            => channel == TacticalThirdBatchMotionChannel.ActiveTranslation || channel == TacticalThirdBatchMotionChannel.RootTranslation
                || channel == TacticalThirdBatchMotionChannel.ActiveImpulse || channel == TacticalThirdBatchMotionChannel.ActiveVelocityOverride
                || channel == TacticalThirdBatchMotionChannel.NewJump || channel == TacticalThirdBatchMotionChannel.ActiveGravitySuppression;
    }
}
