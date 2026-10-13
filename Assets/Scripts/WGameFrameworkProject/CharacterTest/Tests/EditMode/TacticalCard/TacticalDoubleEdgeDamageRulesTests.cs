using MxFramework.Combat.Damage;
using MxFramework.Combat.Hit;
using MxFramework.Gameplay;
using NUnit.Framework;

namespace MxFramework.Game.CharacterTest.TacticalCard
{
    public sealed class TacticalDoubleEdgeDamageRulesTests
    {
        private static readonly GameplayEntityId Actor = new GameplayEntityId(1, 3), Opponent = new GameplayEntityId(2, 4);
        private static TacticalDoubleEdgeCommitKey Commit(ulong stage = 7, ulong commit = 8, ulong execution = 9,
            GameplayEntityId? actor = null, ulong card = 10, long frame = 100)
            => new TacticalDoubleEdgeCommitKey(stage, commit, execution, actor ?? Actor, card, frame);
        private static TacticalDoubleEdgeDamageFacts Hit(GameplayEntityId? attacker = null, GameplayEntityId? target = null,
            ulong stage = 7, ulong turn = 2, long frame = 101, int instance = 22, bool attackerAlive = true, bool targetAlive = true,
            HitResolveKind outcome = HitResolveKind.Damage, TacticalAccessoryDamageOrigin origin = TacticalAccessoryDamageOrigin.DirectAttack,
            DamageProfileSourceKind source = DamageProfileSourceKind.WeaponTrace, ulong revision = 4)
            => new TacticalDoubleEdgeDamageFacts(stage, turn, frame, attacker ?? Actor, target ?? Opponent,
                attackerAlive, targetAlive, outcome, origin, source, instance, revision);
        private static TacticalDuelInvitationRule Duel => new TacticalDuelInvitationRule(7, 2, 100, Actor, Opponent, 1500);

        [Test]
        public void DuelAllowsBothDirectionsAndMultipleAttacksUntilTheActualTurnBoundary()
        {
            Assert.IsTrue(Duel.Qualifies(Hit()));
            Assert.IsTrue(Duel.Qualifies(Hit(attacker: Opponent, target: Actor)));
            Assert.IsTrue(Duel.Qualifies(Hit(frame: 9000, instance: 23)));
            Assert.IsFalse(Duel.Qualifies(Hit(turn: 3, frame: 9001)));
            Assert.IsFalse(Duel.Qualifies(Hit(turn: 1)));
            Assert.IsFalse(Duel.Qualifies(Hit(frame: 99)));
        }

        [Test]
        public void DuelDoesNotTransferToThirdPartiesNewGenerationsOrAnotherStage()
        {
            Assert.IsFalse(Duel.Qualifies(Hit(target: new GameplayEntityId(3, 1))));
            Assert.IsFalse(Duel.Qualifies(Hit(attacker: new GameplayEntityId(3, 1))));
            Assert.IsFalse(Duel.Qualifies(Hit(target: new GameplayEntityId(Opponent.Index, Opponent.Generation + 1))));
            Assert.IsFalse(Duel.Qualifies(Hit(attacker: new GameplayEntityId(Actor.Index, Actor.Generation + 1))));
            Assert.IsFalse(Duel.Qualifies(Hit(stage: 8)));
            Assert.IsFalse(Duel.Qualifies(Hit(attackerAlive: false)));
            Assert.IsFalse(Duel.Qualifies(Hit(targetAlive: false)));
        }

        [Test]
        public void DuelPairConflictIsSymmetricButDoesNotConflateDifferentTurnsOrTargets()
        {
            Assert.IsTrue(Duel.SamePair(new TacticalDuelInvitationRule(7, 2, 105, Opponent, Actor, 1500)));
            Assert.IsFalse(Duel.SamePair(new TacticalDuelInvitationRule(7, 3, 105, Opponent, Actor, 1500)));
            Assert.IsFalse(Duel.SamePair(new TacticalDuelInvitationRule(7, 2, 105, Actor, new GameplayEntityId(3, 1), 1500)));
            Assert.IsFalse(default(TacticalDuelInvitationRule).Qualifies(Hit()));
        }

        [TestCase(HitResolveKind.Blocked)] [TestCase(HitResolveKind.Parried)] [TestCase(HitResolveKind.Evaded)]
        [TestCase(HitResolveKind.Invincible)] [TestCase(HitResolveKind.Duplicate)] [TestCase(HitResolveKind.Friendly)]
        [TestCase(HitResolveKind.SelfDamage)] [TestCase(HitResolveKind.TargetDead)]
        public void NeitherDamageRuleAmplifiesRejectedOrDefendedHits(HitResolveKind outcome)
        {
            var facts = Hit(outcome: outcome);
            var stamp = new TacticalDirectAttackStamp(Commit(), 22, 4, 100);
            Assert.IsFalse(Duel.Qualifies(facts)); Assert.IsFalse(stamp.Qualifies(Commit(), facts));
        }

        [TestCase((int)TacticalAccessoryDamageOrigin.Unclassified, DamageProfileSourceKind.WeaponTrace)]
        [TestCase((int)TacticalAccessoryDamageOrigin.Derived, DamageProfileSourceKind.WeaponTrace)]
        [TestCase((int)TacticalAccessoryDamageOrigin.Periodic, DamageProfileSourceKind.ProjectileImpact)]
        [TestCase((int)TacticalAccessoryDamageOrigin.DirectAttack, DamageProfileSourceKind.AreaPulse)]
        [TestCase((int)TacticalAccessoryDamageOrigin.DirectAttack, DamageProfileSourceKind.StatusTick)]
        [TestCase((int)TacticalAccessoryDamageOrigin.DirectAttack, DamageProfileSourceKind.Scripted)]
        public void NeitherRuleInfersDirectProvenanceFromTheSourceKind(int origin, DamageProfileSourceKind source)
        {
            var facts = Hit(origin: (TacticalAccessoryDamageOrigin)origin, source: source);
            var stamp = new TacticalDirectAttackStamp(Commit(), 22, 4, 100);
            Assert.IsFalse(Duel.Qualifies(facts)); Assert.IsFalse(stamp.Qualifies(Commit(), facts));
        }

        [Test]
        public void ShatteredProjectileRetainsExactCommitAfterTheOriginatingTurnEnds()
        {
            var stamp = new TacticalDirectAttackStamp(Commit(), 22, 4, 101);
            var delayed = Hit(turn: 4, frame: 500, source: DamageProfileSourceKind.ProjectileImpact);
            Assert.IsTrue(stamp.Qualifies(Commit(), delayed)); Assert.IsFalse(Duel.Qualifies(delayed));
            Assert.IsFalse(stamp.Qualifies(Commit(commit: 18), delayed));
            Assert.IsFalse(stamp.Qualifies(Commit(execution: 19), delayed));
            Assert.IsFalse(stamp.Qualifies(Commit(card: 20), delayed));
            Assert.IsFalse(stamp.Qualifies(Commit(frame: 99), delayed));
            Assert.IsFalse(stamp.Qualifies(Commit(stage: 8), delayed));
            Assert.IsFalse(stamp.Qualifies(Commit(actor: new GameplayEntityId(1, 4)), delayed));
            Assert.IsFalse(stamp.Qualifies(Commit(), Hit(instance: 23)));
            Assert.IsFalse(stamp.Qualifies(Commit(), Hit(revision: 5)), "A reused action instance is a different source.");
            Assert.IsFalse(stamp.Qualifies(Commit(), Hit(frame: 100)));
            Assert.IsFalse(new TacticalDirectAttackStamp(Commit(), 22, 4, 99).IsValid);
        }

        [Test]
        public void CounterattackGetsDuelOnlyAndCompositionRoundsJustOnce()
        {
            var stamp = new TacticalDirectAttackStamp(Commit(), 22, 4, 100);
            var outgoing = Hit(); var counter = Hit(attacker: Opponent, target: Actor);
            Assert.IsTrue(TacticalDoubleEdgeDamageRules.TryScale(3, Duel.Qualifies(outgoing) ? 1500 : 1000,
                stamp.Qualifies(Commit(), outgoing) ? 2000 : 1000, out int enhanced));
            Assert.AreEqual(9, enhanced, "3 * 1.5 * 2 must not round the intermediate 4.5 down to 4.");
            Assert.IsTrue(TacticalDoubleEdgeDamageRules.TryScale(3, Duel.Qualifies(counter) ? 1500 : 1000,
                stamp.Qualifies(Commit(), counter) ? 2000 : 1000, out int retaliating));
            Assert.AreEqual(4, retaliating);
            Assert.IsTrue(TacticalDoubleEdgeDamageRules.TryScale(3, 2000, 1500, out int swapped)); Assert.AreEqual(enhanced, swapped);
        }

        [Test]
        public void AcceptedShatteredProjectileKeepsItsFrozenSourceAfterTheShooterDies()
        {
            var source = new TacticalDirectAttackStamp(Commit(), 22, 4, 100);
            var facts = Hit(attackerAlive: false, source: DamageProfileSourceKind.ProjectileImpact);
            Assert.IsTrue(source.Qualifies(Commit(), facts)); Assert.IsFalse(Duel.Qualifies(facts));
            Assert.IsTrue(TacticalDoubleEdgeDamageRules.TryScale(7, 1000, 2000, out int damage)); Assert.AreEqual(14, damage);
        }

        [TestCase(0, 1500, 2000, 0)] [TestCase(100, 1000, 1000, 100)]
        [TestCase(int.MaxValue, 1500, 2000, int.MaxValue)] [TestCase(1, int.MaxValue, int.MaxValue, int.MaxValue)]
        public void DamageBoundariesDoNotWrap(int value, int duel, int shattered, int expected)
        { Assert.IsTrue(TacticalDoubleEdgeDamageRules.TryScale(value, duel, shattered, out int damage)); Assert.AreEqual(expected, damage); }

        [TestCase(-1, 1500, 2000)] [TestCase(7, 0, 2000)] [TestCase(7, 1500, 999)]
        public void InvalidDamageInputsAreNotSilentlyApplied(int value, int duel, int shattered)
        { Assert.IsFalse(TacticalDoubleEdgeDamageRules.TryScale(value, duel, shattered, out int damage)); Assert.AreEqual(value, damage); }

        [Test]
        public void ExhaustObligationAddressesOnlyTheCommittedBattleInstanceAndRequiresARealPlayerDeck()
        {
            Assert.IsTrue(TacticalDoubleEdgeDamageRules.MustExhaustCommittedInstance(Commit(), 7, 10, true));
            Assert.IsFalse(TacticalDoubleEdgeDamageRules.MustExhaustCommittedInstance(Commit(), 7, 11, true));
            Assert.IsFalse(TacticalDoubleEdgeDamageRules.MustExhaustCommittedInstance(Commit(), 8, 10, true));
            Assert.IsFalse(TacticalDoubleEdgeDamageRules.MustExhaustCommittedInstance(Commit(), 7, 10, false));
            Assert.IsFalse(TacticalDoubleEdgeDamageRules.MustExhaustCommittedInstance(default, 7, 10, true));
        }
    }
}
