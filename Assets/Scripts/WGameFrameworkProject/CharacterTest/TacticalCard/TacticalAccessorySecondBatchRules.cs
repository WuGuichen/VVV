using MxFramework.Combat.Damage;
using MxFramework.Combat.Hit;
using MxFramework.Core.Math;
using MxFramework.Gameplay;

namespace MxFramework.Game.CharacterTest.TacticalCard
{
    /// <summary>04/08/18 mechanism inputs. Trial values are not released balance defaults or card IDs.</summary>
    internal readonly struct TacticalSecondBatchParameters
    {
        internal const int TrialRearScaleMilli = 1500;
        internal const int TrialOnePunchScaleMilli = 2000;
        internal TacticalSecondBatchParameters(bool noTurningBack, bool nonLethalOath, bool onePunch,
            int rearScaleMilli, int onePunchScaleMilli)
        { NoTurningBack = noTurningBack; NonLethalOath = nonLethalOath; OnePunch = onePunch;
            RearScaleMilli = rearScaleMilli; OnePunchScaleMilli = onePunchScaleMilli; }
        internal bool NoTurningBack { get; }
        internal bool NonLethalOath { get; }
        internal bool OnePunch { get; }
        internal int RearScaleMilli { get; }
        internal int OnePunchScaleMilli { get; }
        internal bool IsValid => (NoTurningBack || NonLethalOath || OnePunch)
            && (NoTurningBack ? RearScaleMilli > 1000 : RearScaleMilli == 0)
            && (OnePunch ? OnePunchScaleMilli > 1000 : OnePunchScaleMilli == 0);
    }

    /// <summary>Frozen horizontal body facing and actual travel INTO the receiver, at accepted collision time.
    /// Both witnesses are bounded to [-1,1] by the producer. Zero means missing evidence, not a fallback to positions.</summary>
    internal readonly struct TacticalSecondBatchDirection
    {
        internal TacticalSecondBatchDirection(FixVector3 targetBodyFacing, FixVector3 incomingTravel, bool reliable)
        { TargetBodyFacing = targetBodyFacing; IncomingTravel = incomingTravel; Reliable = reliable; }
        internal FixVector3 TargetBodyFacing { get; }
        internal FixVector3 IncomingTravel { get; }
        internal bool Reliable { get; }
    }

    internal static class TacticalAccessorySecondBatchRules
    {
        /// <summary>Rear 120-degree sector, INCLUDING the 60-degree boundaries from rear center.
        /// Incoming travel points into the target, so rear travel and forward facing have a positive dot.</summary>
        /// <remarks>NoAlloc. Exact fixed-point witness comparison without sqrt, trig, normalization or early rounding.</remarks>
        internal static bool IsRear120(in TacticalSecondBatchDirection direction)
        {
            if (!direction.Reliable) return false;
            long fx = direction.TargetBodyFacing.X.RawValue, fz = direction.TargetBodyFacing.Z.RawValue;
            long dx = direction.IncomingTravel.X.RawValue, dz = direction.IncomingTravel.Z.RawValue;
            if (!Bounded(fx) || !Bounded(fz) || !Bounded(dx) || !Bounded(dz)) return false;
            decimal facingSquared = (decimal)fx * fx + (decimal)fz * fz;
            decimal travelSquared = (decimal)dx * dx + (decimal)dz * dz;
            decimal dot = (decimal)fx * dx + (decimal)fz * dz;
            // At raw bounds all products are <= 1.6e25, within decimal's exact integer range.
            return facingSquared > 0 && travelSquared > 0 && dot > 0
                && 4 * dot * dot >= facingSquared * travelSquared;
        }

        private static bool Bounded(long raw) => raw >= -Fix64.Scale && raw <= Fix64.Scale;

        /// <remarks>NoAlloc. Round zero is a frame's pre-existing production; on-hit children use the next round.
        /// Reserve int.MaxValue as invalid. Never wrap or silently move an overflowing child into another frame.</remarks>
        internal static bool TryNextCausalRound(int parentRound, out int childRound)
        {
            childRound = 0;
            if (parentRound < 0 || parentRound >= int.MaxValue - 1) return false;
            childRound = parentRound + 1; return true;
        }

        /// <remarks>NoAlloc. Symmetric outgoing/incoming rule, frozen hit facts only. The shared direct whitelist
        /// permits weapon traces/projectile sweeps; periodic/unclassified/derived/area origins receive no rear bonus.
        /// The owner must revoke the turn rule on actor departure/death and apply its factor once per damage root.</remarks>
        internal static int RearScale(in TacticalDoubleEdgeCommitKey commit, ulong activatedTurn, bool active,
            int scaleMilli, in TacticalDoubleEdgeDamageFacts facts, in TacticalSecondBatchDirection direction)
        {
            bool direct = facts.Origin == TacticalAccessoryDamageOrigin.DirectAttack
                && (facts.SourceKind == DamageProfileSourceKind.WeaponTrace
                    || facts.SourceKind == DamageProfileSourceKind.ProjectileImpact);
            if (!active || !commit.IsValid || activatedTurn == 0 || scaleMilli <= 1000 || !direct
                || facts.Stage != commit.Stage || facts.Turn != activatedTurn || facts.Frame < commit.Frame
                || facts.Outcome != HitResolveKind.Damage || !facts.TargetAlive
                // A dead recipient cannot have a valid Damage classification. Only the EFFECT HOLDER's
                // death revokes the turn rule: an unrelated dead shooter's already airborne projectile can hit.
                || (facts.Attacker == commit.Actor && !facts.AttackerAlive)
                || !facts.Attacker.IsValid || !facts.Target.IsValid || facts.Attacker == facts.Target
                || facts.ActionInstance <= 0 || facts.ActionRevision == 0
                || (facts.Attacker != commit.Actor && facts.Target != commit.Actor)) return 1000;
            return IsRear120(direction) ? scaleMilli : 1000;
        }

        /// <remarks>NoAlloc. Reuses the existing saturating, single-final-truncation multiplier rule.
        /// Factors are returned to the shared damage owner; this never mutates an already scaled root.</remarks>
        internal static bool TryScale(int baseDamage, int rearScaleMilli, int onePunchScaleMilli, out int damage)
            => TacticalDoubleEdgeDamageRules.TryScale(baseDamage, rearScaleMilli, onePunchScaleMilli, out damage);

        /// <summary>The common damage owner supplies BOTH batches' factors in one call, before armor.
        /// Example: 3 * 1.5 * 1.5 * 2 becomes 13, never an intermediate truncation to 12.</summary>
        /// <remarks>NoAlloc. Integer milli factors have exact finite decimal fractions. All factors are >=1,
        /// so saturation during composition cannot discard a later decrease. The only integer cast is final.</remarks>
        internal static bool TryScale(int baseDamage, int duelScaleMilli, int shatteredScaleMilli,
            int rearScaleMilli, int onePunchScaleMilli, out int damage)
        {
            damage = baseDamage;
            if (baseDamage < 0 || duelScaleMilli < 1000 || shatteredScaleMilli < 1000
                || rearScaleMilli < 1000 || onePunchScaleMilli < 1000) return false;
            decimal composed = baseDamage;
            if (Saturated(ref composed, duelScaleMilli) || Saturated(ref composed, shatteredScaleMilli)
                || Saturated(ref composed, rearScaleMilli) || Saturated(ref composed, onePunchScaleMilli))
            { damage = int.MaxValue; return true; }
            damage = (int)composed; return true;
        }

        private static bool Saturated(ref decimal composed, int scaleMilli)
        { composed = composed * scaleMilli / 1000m; return composed > int.MaxValue; }

        /// <summary>Apply AFTER armor absorption and BEFORE the actual serialized HP debit.
        /// Every same-frame debit must reread the latest HP. Armor, pressure and ordinary control remain unchanged.</summary>
        /// <remarks>NoAlloc. Zero/dead HP remains zero; invalid inputs do not publish a debit.</remarks>
        internal static bool TryLimitHpDebit(bool nonLethal, int currentHp, int postArmorHpBudget, out int debit)
        {
            debit = 0;
            if (currentHp < 0 || postArmorHpBudget < 0) return false;
            int available = nonLethal && currentHp > 0 ? currentHp - 1 : currentHp;
            debit = postArmorHpBudget < available ? postArmorHpBudget : available;
            return true;
        }
    }

    /// <summary>Producer-assigned stable collision key. Sort by frame/CAUSAL ROUND/event/source-node/target/root occurrence.
    /// Event and node identities come from actual producers; callback order, ActionId and trace-window IDs alone are insufficient.</summary>
    internal readonly struct TacticalSecondBatchHitKey : System.IComparable<TacticalSecondBatchHitKey>
    {
        internal TacticalSecondBatchHitKey(long frame, ulong stableEvent, ulong sourceNode, GameplayEntityId target, ulong rootOccurrence)
            : this(frame, 0, stableEvent, sourceNode, target, rootOccurrence) { }
        internal TacticalSecondBatchHitKey(long frame, int causalRound, ulong stableEvent, ulong sourceNode,
            GameplayEntityId target, ulong rootOccurrence)
        { Frame = frame; CausalRound = causalRound; StableEvent = stableEvent; SourceNode = sourceNode;
            Target = target; RootOccurrence = rootOccurrence; }
        internal long Frame { get; }
        internal int CausalRound { get; }
        internal ulong StableEvent { get; }
        internal ulong SourceNode { get; }
        internal GameplayEntityId Target { get; }
        // Actual deterministic damage-root occurrence, not a blueprint event ID or callback arrival counter.
        internal ulong RootOccurrence { get; }
        internal bool IsValid => Frame >= 0 && Frame < long.MaxValue && CausalRound >= 0 && CausalRound < int.MaxValue
            && StableEvent != 0 && SourceNode != 0 && Target.IsValid && RootOccurrence != 0;
        public int CompareTo(TacticalSecondBatchHitKey other)
        {
            int result = Frame.CompareTo(other.Frame);
            if (result == 0) result = CausalRound.CompareTo(other.CausalRound);
            if (result == 0) result = StableEvent.CompareTo(other.StableEvent);
            if (result == 0) result = SourceNode.CompareTo(other.SourceNode);
            if (result == 0) result = Target.CompareTo(other.Target);
            return result == 0 ? RootOccurrence.CompareTo(other.RootOccurrence) : result;
        }
    }

    /// <summary>Registered lineage node. Every explosion, redirect, shadow, projectile and delayed tick must register
    /// its actual parent at birth; this value never looks up the actor's current card/action at impact.</summary>
    internal readonly struct TacticalSecondBatchSource
    {
        internal TacticalSecondBatchSource(in TacticalDoubleEdgeCommitKey commit, ulong node, ulong parent,
            GameplayEntityId producer, int rootActionInstance, ulong rootActionRevision, long createdFrame,
            int birthRound = 0, TacticalSecondBatchHitKey causalTrigger = default)
        { Commit = commit; Node = node; Parent = parent; Producer = producer;
            RootActionInstance = rootActionInstance; RootActionRevision = rootActionRevision; CreatedFrame = createdFrame;
            BirthRound = birthRound; CausalTrigger = causalTrigger; }
        internal TacticalDoubleEdgeCommitKey Commit { get; }
        internal ulong Node { get; }
        internal ulong Parent { get; }
        internal GameplayEntityId Producer { get; }
        internal int RootActionInstance { get; }
        internal ulong RootActionRevision { get; }
        internal long CreatedFrame { get; }
        internal int BirthRound { get; }
        // Exact accepted, non-suppressed parent receipt. Non-hit births retain an empty trigger and their parent's round.
        internal TacticalSecondBatchHitKey CausalTrigger { get; }
        internal bool IsValid => Commit.IsValid && Node != 0 && Node != Parent && Producer.IsValid
            && RootActionInstance > 0 && RootActionRevision != 0 && CreatedFrame >= Commit.Frame && CreatedFrame < long.MaxValue
            && BirthRound >= 0 && BirthRound < int.MaxValue;
        /// <remarks>NoAlloc. Production supplies the actual frame/event/root occurrence. The frozen source supplies
        /// its causal birth round for same-frame hits; an actually delayed impact starts in its new frame's round zero.</remarks>
        internal TacticalSecondBatchHitKey CreateHitKey(long frame, ulong stableEvent, GameplayEntityId target, ulong rootOccurrence)
            => new TacticalSecondBatchHitKey(frame, RoundAt(frame), stableEvent, Node, target, rootOccurrence);
        internal int RoundAt(long frame) => frame == CreatedFrame ? BirthRound : 0;
        internal bool Matches(in TacticalSecondBatchSource other) => Commit.Matches(other.Commit)
            && Node == other.Node && Parent == other.Parent && Producer == other.Producer
            && RootActionInstance == other.RootActionInstance && RootActionRevision == other.RootActionRevision
            && CreatedFrame == other.CreatedFrame && BirthRound == other.BirthRound
            && CausalTrigger.CompareTo(other.CausalTrigger) == 0;
    }

    /// <summary>Whole payload moves with its key when sorted. Source nodes are commit-scoped here;
    /// the stage comparator adds the complete commit identity to disambiguate equal local node/key values.</summary>
    internal readonly struct TacticalSecondBatchHit : System.IComparable<TacticalSecondBatchHit>
    {
        internal TacticalSecondBatchHit(in TacticalSecondBatchSource source, in TacticalSecondBatchHitKey key,
            GameplayEntityId actualProducer, HitResolveKind outcome, bool targetAlive, bool actualEnemy)
        { Source = source; Key = key; ActualProducer = actualProducer; Outcome = outcome;
            TargetAlive = targetAlive; ActualEnemy = actualEnemy; }
        internal TacticalSecondBatchSource Source { get; }
        internal TacticalSecondBatchHitKey Key { get; }
        internal GameplayEntityId ActualProducer { get; }
        internal HitResolveKind Outcome { get; }
        internal bool TargetAlive { get; }
        internal bool ActualEnemy { get; }
        internal bool ConsumesUniqueHit => Outcome == HitResolveKind.Damage
            || Outcome == HitResolveKind.Blocked || Outcome == HitResolveKind.Parried;
        internal bool Matches(in TacticalSecondBatchHit other) => Source.Matches(other.Source)
            && Key.CompareTo(other.Key) == 0 && ActualProducer == other.ActualProducer && Outcome == other.Outcome
            && TargetAlive == other.TargetAlive && ActualEnemy == other.ActualEnemy;
        /// <remarks>NoAlloc. Deterministic stage order with a fixed full-commit tie-breaker; never compare bare
        /// local hit keys across commits. Producers may instead supply stage-unique nodes. Admission remains separate.</remarks>
        public int CompareTo(TacticalSecondBatchHit other)
        {
            int result = Source.Commit.Stage.CompareTo(other.Source.Commit.Stage);
            if (result == 0) result = Key.CompareTo(other.Key);
            if (result == 0) result = Source.Commit.Actor.CompareTo(other.Source.Commit.Actor);
            if (result == 0) result = Source.Commit.Commit.CompareTo(other.Source.Commit.Commit);
            if (result == 0) result = Source.Commit.Execution.CompareTo(other.Source.Commit.Execution);
            if (result == 0) result = Source.Commit.BattleCardInstance.CompareTo(other.Source.Commit.BattleCardInstance);
            return result == 0 ? Source.Commit.Frame.CompareTo(other.Source.Commit.Frame) : result;
        }
    }

    internal readonly struct TacticalSecondBatchHitDecision
    {
        internal TacticalSecondBatchHitDecision(bool replay, bool suppress, int scale, bool nonLethal, bool interrupt)
        { IsReplay = replay; SuppressAttackJudgment = suppress; DamageScaleMilli = scale;
            NonLethalHp = nonLethal; AttemptOrdinaryInterrupt = interrupt; }
        internal bool IsReplay { get; }
        internal bool SuppressAttackJudgment { get; }
        internal int DamageScaleMilli { get; }
        internal bool NonLethalHp { get; }
        // A consumed REQUEST, not permission to break super armor. The existing reaction bridge decides success.
        internal bool AttemptOrdinaryInterrupt { get; }
    }
}
