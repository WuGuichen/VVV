using System;
using MxFramework.Combat.Damage;
using MxFramework.Combat.Hit;
using MxFramework.Gameplay;

namespace MxFramework.Game.CharacterTest.TacticalCard
{
    /// <summary>Value identity supplied by the joint commit owner, not an admission or payment receipt.</summary>
    internal readonly struct TacticalDoubleEdgeCommitKey
    {
        internal TacticalDoubleEdgeCommitKey(ulong stage, ulong commit, ulong execution,
            GameplayEntityId actor, ulong battleCardInstance, long frame)
        { Stage = stage; Commit = commit; Execution = execution; Actor = actor; BattleCardInstance = battleCardInstance; Frame = frame; }
        internal ulong Stage { get; }
        internal ulong Commit { get; }
        internal ulong Execution { get; }
        internal GameplayEntityId Actor { get; }
        internal ulong BattleCardInstance { get; }
        internal long Frame { get; }
        internal bool IsValid => Stage != 0 && Commit != 0 && Execution != 0 && Actor.IsValid && BattleCardInstance != 0 && Frame >= 0 && Frame < long.MaxValue;
        internal bool Matches(in TacticalDoubleEdgeCommitKey other) => IsValid && other.IsValid
            && Stage == other.Stage && Commit == other.Commit && Execution == other.Execution
            && Actor == other.Actor && BattleCardInstance == other.BattleCardInstance && Frame == other.Frame;
    }

    /// <summary>Pre-HP facts. The producer must prove origin; SourceKind alone never proves a direct attack.</summary>
    internal readonly struct TacticalDoubleEdgeDamageFacts
    {
        internal TacticalDoubleEdgeDamageFacts(ulong stage, ulong turn, long frame, GameplayEntityId attacker,
            GameplayEntityId target, bool attackerAlive, bool targetAlive, HitResolveKind outcome,
            TacticalAccessoryDamageOrigin origin, DamageProfileSourceKind sourceKind, int actionInstance, ulong actionRevision)
        {
            Stage = stage; Turn = turn; Frame = frame; Attacker = attacker; Target = target;
            AttackerAlive = attackerAlive; TargetAlive = targetAlive; Outcome = outcome;
            Origin = origin; SourceKind = sourceKind; ActionInstance = actionInstance; ActionRevision = actionRevision;
        }
        internal ulong Stage { get; }
        internal ulong Turn { get; }
        internal long Frame { get; }
        internal GameplayEntityId Attacker { get; }
        internal GameplayEntityId Target { get; }
        internal bool AttackerAlive { get; }
        internal bool TargetAlive { get; }
        internal HitResolveKind Outcome { get; }
        internal TacticalAccessoryDamageOrigin Origin { get; }
        internal DamageProfileSourceKind SourceKind { get; }
        internal int ActionInstance { get; }
        internal ulong ActionRevision { get; }
        internal bool IsDirectDamage => Stage != 0 && Turn != 0 && Frame >= 0 && Attacker.IsValid && Target.IsValid
            && Attacker != Target && TargetAlive && ActionInstance > 0 && Outcome == HitResolveKind.Damage
            && Origin == TacticalAccessoryDamageOrigin.DirectAttack
            && (SourceKind == DamageProfileSourceKind.WeaponTrace || SourceKind == DamageProfileSourceKind.ProjectileImpact);
    }

    /// <summary>Pair-and-turn predicate only. The existing owner must invalidate it on either actor's departure/death.</summary>
    internal readonly struct TacticalDuelInvitationRule
    {
        internal TacticalDuelInvitationRule(ulong stage, ulong turn, long activatedFrame, GameplayEntityId actor,
            GameplayEntityId opponent, int damageScaleMilli)
        {
            Stage = stage; Turn = turn; ActivatedFrame = activatedFrame; Actor = actor;
            Opponent = opponent; DamageScaleMilli = damageScaleMilli;
        }
        internal ulong Stage { get; }
        internal ulong Turn { get; }
        internal long ActivatedFrame { get; }
        internal GameplayEntityId Actor { get; }
        internal GameplayEntityId Opponent { get; }
        internal int DamageScaleMilli { get; }
        internal bool IsValid => Stage != 0 && Turn != 0 && ActivatedFrame >= 0 && Actor.IsValid
            && Opponent.IsValid && Actor != Opponent && DamageScaleMilli > 1000;
        /// <remarks>NoAlloc. The last damage frame still has this turn; expiry follows the complete simulation step.</remarks>
        internal bool Qualifies(in TacticalDoubleEdgeDamageFacts facts) => IsValid && facts.IsDirectDamage && facts.AttackerAlive
            && facts.Stage == Stage && facts.Turn == Turn && facts.Frame >= ActivatedFrame
            && ((facts.Attacker == Actor && facts.Target == Opponent) || (facts.Attacker == Opponent && facts.Target == Actor));
        internal bool SamePair(in TacticalDuelInvitationRule other) => IsValid && other.IsValid && Stage == other.Stage && Turn == other.Turn
            && ((Actor == other.Actor && Opponent == other.Opponent) || (Actor == other.Opponent && Opponent == other.Actor));
    }

    /// <summary>A producer's frozen per-action source, retained by a delayed projectile after its host ends.</summary>
    internal readonly struct TacticalDirectAttackStamp
    {
        internal TacticalDirectAttackStamp(in TacticalDoubleEdgeCommitKey commit, int actionInstance, ulong actionRevision, long createdFrame)
        { Commit = commit; ActionInstance = actionInstance; ActionRevision = actionRevision; CreatedFrame = createdFrame; }
        internal TacticalDoubleEdgeCommitKey Commit { get; }
        internal int ActionInstance { get; }
        internal ulong ActionRevision { get; }
        internal long CreatedFrame { get; }
        internal bool IsValid => Commit.IsValid && ActionInstance > 0 && ActionRevision > 0 && CreatedFrame >= Commit.Frame;
        /// <remarks>NoAlloc. No ActionId/template comparison and no dependency on the host still running.</remarks>
        internal bool Qualifies(in TacticalDoubleEdgeCommitKey committedCard, in TacticalDoubleEdgeDamageFacts facts)
            => IsValid && Commit.Matches(committedCard) && facts.IsDirectDamage && facts.Stage == Commit.Stage
                && facts.Attacker == Commit.Actor && facts.ActionInstance == ActionInstance
                && facts.ActionRevision == ActionRevision && facts.Frame >= CreatedFrame;
    }

    internal static class TacticalDoubleEdgeDamageRules
    {
        /// <summary>Combines two explicit positive multipliers with one final truncation, before armor/HP.</summary>
        /// <remarks>NoAlloc. Invalid inputs fail without publishing a damage value; positive results saturate at int.MaxValue.</remarks>
        internal static bool TryScale(int incomingDamage, int duelScaleMilli, int shatteredScaleMilli, out int damage)
        {
            damage = incomingDamage;
            if (incomingDamage < 0 || duelScaleMilli < 1000 || shatteredScaleMilli < 1000) return false;
            long scale = (long)duelScaleMilli * shatteredScaleMilli;
            // Compare before multiplication; even int.MaxValue parameters cannot overflow the intermediate.
            if (incomingDamage != 0 && scale > (long)int.MaxValue * 1000000 / incomingDamage)
            { damage = int.MaxValue; return true; }
            damage = (int)((long)incomingDamage * scale / 1000000);
            return true;
        }

        /// <summary>Destination obligation only. The deck owner still owns atomic commitment, movement and deduplication.</summary>
        /// <remarks>NoAlloc. A matching paid commitment is independent of hit count, cancellation and animation completion.</remarks>
        internal static bool MustExhaustCommittedInstance(in TacticalDoubleEdgeCommitKey paidCommit,
            ulong stage, ulong battleCardInstance, bool hasRealPlayerDeck)
            => hasRealPlayerDeck && paidCommit.IsValid && paidCommit.Stage == stage && paidCommit.BattleCardInstance == battleCardInstance;
    }
}
