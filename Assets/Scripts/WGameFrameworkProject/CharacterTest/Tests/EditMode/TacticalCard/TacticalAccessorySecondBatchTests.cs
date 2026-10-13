using System;
using MxFramework.Combat.Damage;
using MxFramework.Combat.Hit;
using MxFramework.Core.Math;
using MxFramework.Gameplay;
using NUnit.Framework;

namespace MxFramework.Game.CharacterTest.TacticalCard
{
    public sealed class TacticalAccessorySecondBatchTests
    {
        private static readonly GameplayEntityId Actor = new GameplayEntityId(1, 3);
        private static readonly GameplayEntityId Enemy = new GameplayEntityId(2, 4);
        private static readonly GameplayEntityId Other = new GameplayEntityId(3, 5);
        private static TacticalDoubleEdgeCommitKey Commit(ulong stage = 7, ulong commit = 8, ulong execution = 9,
            GameplayEntityId? actor = null, ulong card = 10, long frame = 100)
            => new TacticalDoubleEdgeCommitKey(stage, commit, execution, actor ?? Actor, card, frame);
        private static TacticalSecondBatchParameters Parameters(bool rear = false, bool oath = false, bool punch = false)
            => new TacticalSecondBatchParameters(rear, oath, punch, rear ? 1500 : 0, punch ? 2000 : 0);
        private static TacticalAccessorySecondBatchState State(bool rear = false, bool oath = false, bool punch = false,
            TacticalDoubleEdgeCommitKey? commit = null)
        {
            Assert.IsTrue(TacticalAccessorySecondBatchState.TryCreateForConfirmedCommit(commit ?? Commit(), 2,
                Parameters(rear, oath, punch), out var state)); return state;
        }
        private static TacticalSecondBatchSource Root(TacticalAccessorySecondBatchState state, ulong node = 1,
            int instance = 22, ulong revision = 4, long frame = 100)
        { Assert.IsTrue(state.TryRegisterRoot(node, instance, revision, frame, out var root)); return root; }
        private static TacticalSecondBatchSource Derived(TacticalAccessorySecondBatchState state, TacticalSecondBatchSource parent,
            ulong node, GameplayEntityId? producer = null, long frame = 101)
        { Assert.IsTrue(state.TryRegisterDerived(parent, node, producer ?? Actor, frame, out var source)); return source; }
        private static TacticalSecondBatchHit Hit(TacticalSecondBatchSource source, GameplayEntityId? target = null,
            long frame = 102, ulong stableEvent = 1, HitResolveKind outcome = HitResolveKind.Damage,
            bool alive = true, bool enemy = true, GameplayEntityId? producer = null, ulong? node = null, ulong occurrence = 1)
            => new TacticalSecondBatchHit(source, new TacticalSecondBatchHitKey(frame, source.RoundAt(frame), stableEvent,
                node ?? source.Node, target ?? Enemy, occurrence),
                producer ?? source.Producer, outcome, alive, enemy);
        private static TacticalSecondBatchHitDecision Resolve(TacticalAccessorySecondBatchState state, TacticalSecondBatchHit hit)
        {
            var decisions = new TacticalSecondBatchHitDecision[1];
            Assert.IsTrue(state.TryResolveBatch(new[] { hit }, 1, decisions)); return decisions[0];
        }
        private static FixVector3 Vector(long x, long z, long y = 0)
            => new FixVector3(Fix64.FromRaw(x), Fix64.FromRaw(y), Fix64.FromRaw(z));
        private static TacticalSecondBatchDirection Direction(long dx = 0, long dz = 1000000,
            long fx = 0, long fz = 1000000, bool reliable = true)
            => new TacticalSecondBatchDirection(Vector(fx, fz), Vector(dx, dz), reliable);
        private static TacticalDoubleEdgeDamageFacts Facts(GameplayEntityId? attacker = null, GameplayEntityId? target = null,
            ulong stage = 7, ulong turn = 2, long frame = 102, bool attackerAlive = true, bool targetAlive = true,
            HitResolveKind outcome = HitResolveKind.Damage, TacticalAccessoryDamageOrigin origin = TacticalAccessoryDamageOrigin.DirectAttack,
            DamageProfileSourceKind kind = DamageProfileSourceKind.WeaponTrace, ulong actionRevision = 4)
            => new TacticalDoubleEdgeDamageFacts(stage, turn, frame, attacker ?? Actor, target ?? Enemy,
                attackerAlive, targetAlive, outcome, origin, kind, 22, actionRevision);

        [TestCase(0, 1000000, true)] [TestCase(0, -1000000, false)]
        [TestCase(1000000, 0, false)] [TestCase(-1000000, 0, false)]
        [TestCase(866025, 500000, true)] [TestCase(-866025, 500000, true)]
        [TestCase(866025, 499999, false)] [TestCase(-866025, 499999, false)]
        [TestCase(0, 1, true)] [TestCase(0, 0, false)]
        public void RearSectorHasSymmetricEdgesAtTheFixedPointQuantumAndPreservesTinyEvidence(long x, long z, bool expected)
        { Assert.AreEqual(expected, TacticalAccessorySecondBatchRules.IsRear120(Direction(x, z))); }

        [Test]
        public void RearSectorUsesFrozenBodyFacingAndActualTravelIncludingReturnProjectiles()
        {
            var frozen = Direction();
            var turnedAfterHit = Direction(fz: -1000000);
            Assert.IsTrue(TacticalAccessorySecondBatchRules.IsRear120(frozen));
            Assert.IsFalse(TacticalAccessorySecondBatchRules.IsRear120(turnedAfterHit));
            Assert.IsTrue(TacticalAccessorySecondBatchRules.IsRear120(frozen));
            Assert.IsFalse(TacticalAccessorySecondBatchRules.IsRear120(Direction(dz: -1000000)));
            Assert.IsTrue(TacticalAccessorySecondBatchRules.IsRear120(Direction(1000000, 0, 1000000, 0)));
        }

        [TestCase(1000001L)] [TestCase(-1000001L)] [TestCase(long.MinValue)] [TestCase(long.MaxValue)]
        public void UnboundedDirectionFailsWithoutOverflow(long raw)
        { Assert.IsFalse(TacticalAccessorySecondBatchRules.IsRear120(Direction(raw))); }

        [Test]
        public void MissingOrVerticalEvidenceNeverFallsBackToAnAttackersPosition()
        {
            Assert.IsFalse(TacticalAccessorySecondBatchRules.IsRear120(Direction(reliable: false)));
            Assert.IsFalse(TacticalAccessorySecondBatchRules.IsRear120(Direction(fz: 0)));
            Assert.IsFalse(TacticalAccessorySecondBatchRules.IsRear120(new TacticalSecondBatchDirection(
                Vector(0, 0, 1000000), Vector(0, 0, 1000000), true)));
        }

        [Test]
        public void RearBonusIsSymmetricTurnScopedAndContinuesAfterHostCompletion()
        {
            var state = State(rear: true); state.EndHost();
            Assert.AreEqual(1500, state.RearScale(Facts(), Direction()));
            Assert.AreEqual(1500, state.RearScale(Facts(attacker: Enemy, target: Actor), Direction()));
            Assert.AreEqual(1000, state.RearScale(Facts(turn: 3), Direction()));
            Assert.AreEqual(1000, state.RearScale(Facts(turn: 1), Direction()));
            Assert.AreEqual(1000, state.RearScale(Facts(stage: 8), Direction()));
            Assert.AreEqual(1000, state.RearScale(Facts(frame: 99), Direction()));
            Assert.AreEqual(1000, state.RearScale(Facts(target: Other, attacker: Enemy), Direction()));
            Assert.AreEqual(1000, state.RearScale(Facts(attacker: new GameplayEntityId(1, 4)), Direction()));
            state.RevokeRearOnActorDeparture();
            Assert.AreEqual(1000, state.RearScale(Facts(), Direction()));
            Assert.AreEqual(1000, state.CloneForTrustedPause().RearScale(Facts(), Direction()));
        }

        [TestCase(HitResolveKind.Blocked)] [TestCase(HitResolveKind.Parried)] [TestCase(HitResolveKind.Invincible)]
        [TestCase(HitResolveKind.Evaded)] [TestCase(HitResolveKind.Duplicate)] [TestCase(HitResolveKind.TargetDead)]
        public void RearBonusDoesNotAmplifyRejectedOrDefendedOutcomes(HitResolveKind outcome)
        { Assert.AreEqual(1000, State(rear: true).RearScale(Facts(outcome: outcome), Direction())); }

        [Test]
        public void BothParticipantsBeingArmedNeverStacksTheRearFactorOnTheSameRoot()
        {
            var attacker = State(rear: true);
            var defender = State(rear: true, commit: Commit(actor: Enemy, commit: 18, execution: 19));
            var facts = Facts(); var direction = Direction(); int rear = 1000;
            rear = attacker.AccumulateRearScale(rear, facts, direction);
            rear = defender.AccumulateRearScale(rear, facts, direction);
            rear = attacker.AccumulateRearScale(rear, facts, direction);
            Assert.AreEqual(1500, rear);
            Assert.IsTrue(TacticalAccessorySecondBatchRules.TryScale(10, rear, 1000, out int damage));
            Assert.AreEqual(15, damage);
        }

        [Test]
        public void RearBonusRequiresDirectReliableProvenanceAndLivingActors()
        {
            var state = State(rear: true);
            Assert.AreEqual(1000, state.RearScale(Facts(origin: TacticalAccessoryDamageOrigin.Periodic,
                kind: DamageProfileSourceKind.ProjectileImpact), Direction()));
            Assert.AreEqual(1000, state.RearScale(Facts(origin: TacticalAccessoryDamageOrigin.Derived), Direction()));
            Assert.AreEqual(1000, state.RearScale(Facts(origin: TacticalAccessoryDamageOrigin.Unclassified), Direction()));
            Assert.AreEqual(1000, state.RearScale(Facts(kind: DamageProfileSourceKind.StatusTick), Direction()));
            Assert.AreEqual(1000, state.RearScale(Facts(kind: DamageProfileSourceKind.AreaPulse), Direction(reliable: false)));
            Assert.AreEqual(1000, state.RearScale(Facts(kind: DamageProfileSourceKind.AreaPulse), Direction()));
            Assert.AreEqual(1000, state.RearScale(Facts(attackerAlive: false), Direction()));
            Assert.AreEqual(1000, state.RearScale(Facts(targetAlive: false), Direction()));
            Assert.AreEqual(1000, state.RearScale(Facts(actionRevision: 0), Direction()));
        }

        [TestCase(3, 1500, 2000, 9)] [TestCase(1, 1500, 1000, 1)]
        [TestCase(0, 1500, 2000, 0)] [TestCase(int.MaxValue, 1500, 2000, int.MaxValue)]
        [TestCase(1, int.MaxValue, int.MaxValue, int.MaxValue)]
        public void SecondBatchCompositionRoundsOnceAndSaturates(int baseDamage, int rear, int punch, int expected)
        {
            Assert.IsTrue(TacticalAccessorySecondBatchRules.TryScale(baseDamage, rear, punch, out int damage));
            Assert.AreEqual(expected, damage);
        }

        [Test]
        public void UnifiedOwnerComposesBothBatchesBeforeTheOnlyIntegerTruncation()
        {
            Assert.IsTrue(TacticalAccessorySecondBatchRules.TryScale(3, 1500, 1000, 1500, 2000, out int all));
            Assert.AreEqual(13, all);
            Assert.IsTrue(TacticalDoubleEdgeDamageRules.TryScale(3, 1500, 1000, out int premature));
            Assert.IsTrue(TacticalAccessorySecondBatchRules.TryScale(premature, 1500, 2000, out int wrong));
            Assert.AreEqual(12, wrong, "This demonstrates why the shared owner must not chain two integer scaling calls.");
            Assert.IsTrue(TacticalAccessorySecondBatchRules.TryScale(3, 1500, 2000, 1500, 2000, out all));
            Assert.AreEqual(27, all);
            Assert.IsTrue(TacticalAccessorySecondBatchRules.TryScale(int.MaxValue, int.MaxValue, int.MaxValue, int.MaxValue, int.MaxValue, out all));
            Assert.AreEqual(int.MaxValue, all);
            Assert.IsTrue(TacticalAccessorySecondBatchRules.TryScale(0, int.MaxValue, int.MaxValue, int.MaxValue, int.MaxValue, out all));
            Assert.AreEqual(0, all);
            Assert.IsFalse(TacticalAccessorySecondBatchRules.TryScale(3, 999, 1000, 1500, 2000, out _));
        }

        [TestCase(true, 1, 99, 0)] [TestCase(true, 0, 99, 0)] [TestCase(true, 10, 99, 9)]
        [TestCase(true, 10, 0, 0)] [TestCase(true, 10, 3, 3)] [TestCase(false, 1, 99, 1)]
        [TestCase(true, int.MaxValue, int.MaxValue, int.MaxValue - 1)]
        public void OathCapsTheFinalHpBudgetAfterArmorWithoutHealing(bool oath, int hp, int afterArmor, int expected)
        {
            Assert.IsTrue(TacticalAccessorySecondBatchRules.TryLimitHpDebit(oath, hp, afterArmor, out int debit));
            Assert.AreEqual(expected, debit);
        }

        [TestCase(-1, 2)] [TestCase(2, -1)]
        public void InvalidHpBudgetsNeverPublishDamage(int hp, int budget)
        { Assert.IsFalse(TacticalAccessorySecondBatchRules.TryLimitHpDebit(true, hp, budget, out int debit)); Assert.AreEqual(0, debit); }

        [Test]
        public void SameFrameSeparateRootsRereadHpAndCannotKillButAnotherCardCanFinish()
        {
            int hp = 10;
            Assert.IsTrue(TacticalAccessorySecondBatchRules.TryLimitHpDebit(true, hp, 6, out int first)); hp -= first;
            Assert.IsTrue(TacticalAccessorySecondBatchRules.TryLimitHpDebit(true, hp, 6, out int second)); hp -= second;
            Assert.AreEqual(1, hp); Assert.AreEqual(3, second);
            Assert.IsTrue(TacticalAccessorySecondBatchRules.TryLimitHpDebit(false, hp, 6, out int final)); hp -= final;
            Assert.AreEqual(0, hp);
        }

        [Test]
        public void OathAttemptsOrdinaryInterruptOncePerEnemyGenerationEvenIfImmuneOrNoHpWasLost()
        {
            var state = State(oath: true); var source = Root(state);
            var first = Resolve(state, Hit(source));
            Assert.IsTrue(first.AttemptOrdinaryInterrupt); Assert.IsTrue(first.NonLethalHp);
            // The shared reaction bridge may reject this request for super armor. No success callback can refund it.
            Assert.IsFalse(Resolve(state, Hit(source, frame: 103)).AttemptOrdinaryInterrupt);
            Assert.IsTrue(Resolve(state, Hit(source, Other, frame: 104)).AttemptOrdinaryInterrupt);
            Assert.IsTrue(Resolve(state, Hit(source, new GameplayEntityId(Enemy.Index, Enemy.Generation + 1), frame: 105)).AttemptOrdinaryInterrupt);
            Assert.AreEqual(3, state.InterruptAttemptCount);
            Assert.IsTrue(TacticalAccessorySecondBatchRules.TryLimitHpDebit(first.NonLethalHp, 1, 10, out int debit));
            Assert.AreEqual(0, debit);
            var armor = State(oath: true); var armorHit = Resolve(armor, Hit(Root(armor)));
            Assert.IsTrue(armorHit.AttemptOrdinaryInterrupt);
            Assert.IsTrue(TacticalAccessorySecondBatchRules.TryLimitHpDebit(armorHit.NonLethalHp, 10, 0, out debit));
            Assert.AreEqual(0, debit);
        }

        [TestCase(HitResolveKind.Blocked)] [TestCase(HitResolveKind.Parried)] [TestCase(HitResolveKind.Evaded)]
        [TestCase(HitResolveKind.Invincible)] [TestCase(HitResolveKind.Duplicate)] [TestCase(HitResolveKind.Friendly)]
        public void OathDefenseOrRejectedHitDoesNotSpendInterrupt(HitResolveKind outcome)
        {
            var state = State(oath: true); var source = Root(state);
            Assert.IsFalse(Resolve(state, Hit(source, outcome: outcome)).AttemptOrdinaryInterrupt);
            Assert.AreEqual(0, state.InterruptAttemptCount);
            Assert.IsTrue(Resolve(state, Hit(source, frame: 103)).AttemptOrdinaryInterrupt);
        }

        [Test]
        public void FriendlyAcceptedDamageStaysNonLethalWithoutAnEnemyInterruptBenefit()
        {
            var state = State(oath: true); var decision = Resolve(state, Hit(Root(state), enemy: false));
            Assert.IsTrue(decision.NonLethalHp); Assert.IsFalse(decision.AttemptOrdinaryInterrupt);
        }

        [Test]
        public void EveryDerivedGenerationRetainsOathAfterHostEndAndAnotherCommitCannotInheritIt()
        {
            var state = State(oath: true); var trace = Root(state);
            var projectile = Derived(state, trace, 2); state.EndHost();
            var explosion = Derived(state, projectile, 3, frame: 300);
            var redirected = Derived(state, explosion, 4, Other, frame: 301);
            var dot = Derived(state, redirected, 5, Other, frame: 900);
            var decision = Resolve(state, Hit(dot, frame: 901));
            Assert.IsTrue(decision.NonLethalHp); Assert.IsTrue(decision.AttemptOrdinaryInterrupt);
            Assert.AreEqual(trace.RootActionRevision, dot.RootActionRevision);
            Assert.IsTrue(dot.Commit.Matches(trace.Commit));
            var another = State(punch: true, commit: Commit(commit: 18, execution: 19, card: 20, frame: 101));
            Assert.IsFalse(another.TryRegisterDerived(dot, 6, Actor, 902, out _));
            Assert.IsFalse(another.TryResolveBatch(new[] { Hit(dot, frame: 902) }, 1, new TacticalSecondBatchHitDecision[1]));
            Assert.IsFalse(Resolve(another, Hit(Root(another, frame: 101), frame: 902)).NonLethalHp);
        }

        [Test]
        public void ActorDepartureRevokesRearButNeverClearsOathFromAlreadyProducedDescendants()
        {
            var state = State(rear: true, oath: true); var root = Root(state);
            state.RevokeRearOnActorDeparture(); state.EndHost();
            var delayed = Derived(state, root, 2, frame: 1000);
            Assert.AreEqual(1000, state.RearScale(Facts(turn: 100, attackerAlive: false), Direction()));
            Assert.IsTrue(Resolve(state, Hit(delayed, frame: 1001)).NonLethalHp);
        }

        [Test]
        public void SourceRegistrationRejectsMissingParentsChangedPayloadsCyclesAndProducerSubstitution()
        {
            var state = State(oath: true); var root = Root(state);
            Assert.IsTrue(state.TryRegisterRoot(1, 22, 4, 100, out var repeated)); Assert.IsTrue(root.Matches(repeated));
            Assert.IsFalse(state.TryRegisterRoot(1, 22, 5, 100, out _));
            Assert.IsFalse(state.TryRegisterRoot(2, 22, 4, 100, out _));
            Assert.IsFalse(state.TryRegisterDerived(default, 2, Actor, 101, out _));
            Assert.IsFalse(state.TryRegisterDerived(root, 1, Actor, 101, out _));
            Assert.IsFalse(state.TryRegisterDerived(root, 2, Actor, 99, out _));
            var child = Derived(state, root, 2);
            Assert.IsTrue(state.TryRegisterDerived(root, 2, Actor, 101, out _));
            Assert.IsFalse(state.TryRegisterDerived(root, 2, Other, 101, out _));
            Assert.IsFalse(state.TryRegisterDerived(child, 1, Actor, 102, out _));
            Assert.IsFalse(state.TryResolveBatch(new[] { Hit(root, producer: Other) }, 1, new TacticalSecondBatchHitDecision[1]));
            Assert.AreEqual(0, state.ReceiptCount);
        }

        [TestCase(0)] [TestCase(1)] [TestCase(2)] [TestCase(3)] [TestCase(4)] [TestCase(5)] [TestCase(6)]
        public void ForeignCommitIdentityCannotSupplyAnApparentlyMatchingSource(int changedPart)
        {
            var state = State(oath: true); Root(state);
            var key = Commit(stage: changedPart == 0 ? 8UL : 7UL, commit: changedPart == 1 ? 18UL : 8UL,
                execution: changedPart == 2 ? 19UL : 9UL,
                actor: changedPart == 3 ? new GameplayEntityId(1, 4) : Actor,
                card: changedPart == 4 ? 20UL : 10UL, frame: changedPart == 5 ? 99 : 100);
            var forged = new TacticalSecondBatchSource(key, 1, 0, Actor, 22, changedPart == 6 ? 5UL : 4UL, 100);
            Assert.IsFalse(state.TryRegisterDerived(forged, 2, Actor, 101, out _));
            Assert.IsFalse(state.TryResolveBatch(new[] { Hit(forged) }, 1, new TacticalSecondBatchHitDecision[1]));
        }

        [Test]
        public void OnePunchSortsAllSameFrameTargetsIndependentOfCallbackOrderAndBoostsOnlyOne()
        {
            foreach (bool reverse in new[] { false, true }) {
                var state = State(punch: true); var source = Root(state);
                var low = Hit(source, Enemy, stableEvent: 1); var high = Hit(source, Other, stableEvent: 1);
                var hits = reverse ? new[] { high, low } : new[] { low, high };
                var decisions = new TacticalSecondBatchHitDecision[2];
                Assert.IsTrue(state.TryResolveBatch(hits, hits.Length, decisions));
                Assert.AreEqual(Enemy, hits[0].Key.Target); Assert.AreEqual(Enemy, state.Winner.Target);
                Assert.AreEqual(2000, decisions[0].DamageScaleMilli); Assert.IsFalse(decisions[0].SuppressAttackJudgment);
                Assert.IsTrue(decisions[1].SuppressAttackJudgment); Assert.AreEqual(1000, decisions[1].DamageScaleMilli);
                Assert.IsTrue(state.OnePunchConsumed); Assert.IsTrue(state.Suppresses(source));
            }
        }

        [Test]
        public void OnePunchStableEventThenSourceNodeThenGenerationDeterminesTheWinner()
        {
            var state = State(punch: true); var root = Root(state);
            var otherWindow = Root(state, 2, 23, 5);
            var hits = new[] { Hit(root, stableEvent: 3), Hit(otherWindow, stableEvent: 2), Hit(root, Other, stableEvent: 2) };
            var decisions = new TacticalSecondBatchHitDecision[hits.Length];
            Assert.IsTrue(state.TryResolveBatch(hits, hits.Length, decisions));
            Assert.AreEqual(2, state.Winner.StableEvent); Assert.AreEqual(1, state.Winner.SourceNode);
            Assert.AreEqual(Other, state.Winner.Target);
            var a = new TacticalSecondBatchHitKey(1, 1, 1, new GameplayEntityId(2, 4), 1);
            var b = new TacticalSecondBatchHitKey(1, 1, 1, new GameplayEntityId(2, 5), 1);
            Assert.Less(a.CompareTo(b), 0);
        }

        [TestCase(HitResolveKind.Blocked)] [TestCase(HitResolveKind.Parried)]
        public void DefenseConsumesOnePunchAndCannotCreateAnOathInterrupt(HitResolveKind defense)
        {
            var state = State(oath: true, punch: true); var source = Root(state);
            var defended = Resolve(state, Hit(source, outcome: defense));
            Assert.IsFalse(defended.SuppressAttackJudgment); Assert.AreEqual(1000, defended.DamageScaleMilli);
            Assert.IsFalse(defended.AttemptOrdinaryInterrupt); Assert.IsTrue(state.OnePunchConsumed);
            var later = Resolve(state, Hit(source, frame: 103));
            Assert.IsTrue(later.SuppressAttackJudgment); Assert.IsFalse(later.AttemptOrdinaryInterrupt);
        }

        [TestCase(HitResolveKind.Evaded)] [TestCase(HitResolveKind.Invincible)] [TestCase(HitResolveKind.Duplicate)]
        [TestCase(HitResolveKind.Friendly)] [TestCase(HitResolveKind.TargetDead)] [TestCase(HitResolveKind.SelfDamage)]
        public void RejectedHitDoesNotConsumeOnePunchAndAValidLaterHitStillWorks(HitResolveKind outcome)
        {
            var state = State(punch: true); var source = Root(state);
            var noHit = Resolve(state, Hit(source, outcome: outcome, alive: outcome != HitResolveKind.TargetDead));
            Assert.IsFalse(state.OnePunchConsumed); Assert.AreEqual(1000, noHit.DamageScaleMilli);
            Assert.AreEqual(2000, Resolve(state, Hit(source, frame: 103)).DamageScaleMilli);
        }

        [Test]
        public void ConsumedPunchSuppressesFutureWindowsAirborneProjectilesAndDescendantsWithoutEndingTheHost()
        {
            var state = State(punch: true); var root = Root(state);
            var futureWindow = Root(state, 2, 23, 5);
            var projectile = Derived(state, root, 3);
            Assert.AreEqual(2000, Resolve(state, Hit(root)).DamageScaleMilli);
            Assert.IsTrue(state.Suppresses(futureWindow)); Assert.IsTrue(state.Suppresses(projectile));
            Assert.IsTrue(Resolve(state, Hit(futureWindow, frame: 500)).SuppressAttackJudgment);
            var explosion = Derived(state, projectile, 4, frame: 600);
            Assert.IsTrue(Resolve(state, Hit(explosion, frame: 601)).SuppressAttackJudgment);
            Assert.IsFalse(state.TryRegisterRoot(5, 24, 6, 602, out _));
            state.EndHost();
            Assert.IsTrue(state.Suppresses(projectile));
            var next = State(punch: true, commit: Commit(commit: 18, execution: 19, frame: 700));
            Assert.AreEqual(2000, Resolve(next, Hit(Root(next, frame: 700), frame: 701)).DamageScaleMilli);
        }

        [Test]
        public void ReplayedWinnerNeverAmplifiesOrInterruptsOrDebitsTwice()
        {
            var state = State(oath: true, punch: true); var root = Root(state); var hit = Hit(root);
            var decisions = new TacticalSecondBatchHitDecision[2];
            Assert.IsTrue(state.TryResolveBatch(new[] { hit, hit }, 2, decisions));
            Assert.AreEqual(2000, decisions[0].DamageScaleMilli); Assert.IsTrue(decisions[0].AttemptOrdinaryInterrupt);
            Assert.IsTrue(decisions[1].IsReplay); Assert.IsTrue(decisions[1].SuppressAttackJudgment);
            Assert.IsFalse(decisions[1].AttemptOrdinaryInterrupt); Assert.AreEqual(1000, decisions[1].DamageScaleMilli);
            Assert.IsTrue(Resolve(state, hit).IsReplay); Assert.AreEqual(1, state.ReceiptCount);
            Assert.AreEqual(1, state.InterruptAttemptCount);
        }

        [Test]
        public void DistinctSameFrameRootsDoNotCollapseIntoAnAuthoringEventId()
        {
            var state = State(punch: true); var root = Root(state);
            var hits = new[] { Hit(root, occurrence: 2), Hit(root, occurrence: 1) };
            var decisions = new TacticalSecondBatchHitDecision[2];
            Assert.IsTrue(state.TryResolveBatch(hits, 2, decisions));
            Assert.AreEqual(1, state.Winner.RootOccurrence); Assert.AreEqual(2000, decisions[0].DamageScaleMilli);
            Assert.IsTrue(decisions[1].SuppressAttackJudgment); Assert.IsFalse(decisions[1].IsReplay);
            Assert.AreEqual(2, state.ReceiptCount);
        }

        [Test]
        public void ChangedReplayOrLateEarlierKeyRejectsBeforeAnyConsumption()
        {
            var state = State(oath: true); var root = Root(state); Resolve(state, Hit(root, stableEvent: 3));
            var decisions = new TacticalSecondBatchHitDecision[2];
            Assert.IsFalse(state.TryResolveBatch(new[] { Hit(root, Other, frame: 103),
                Hit(root, stableEvent: 3, outcome: HitResolveKind.Blocked) }, 2, decisions));
            Assert.AreEqual(1, state.InterruptAttemptCount); Assert.AreEqual(1, state.ReceiptCount);
            Assert.IsFalse(state.TryResolveBatch(new[] { Hit(root, Other, stableEvent: 2) }, 1, decisions));
            Assert.AreEqual(1, state.ReceiptCount);
            Assert.IsTrue(Resolve(state, Hit(root, Other, frame: 103)).AttemptOrdinaryInterrupt);
        }

        [Test]
        public void TrustedPauseClonePreservesSourcesConsumptionAndReceiptsWithoutSharingMutation()
        {
            var state = State(oath: true, punch: true); var root = Root(state); Resolve(state, Hit(root));
            state.EndHost(); var copy = state.CloneForTrustedPause();
            Assert.IsTrue(copy.OnePunchConsumed); Assert.AreEqual(1, copy.InterruptAttemptCount);
            Assert.IsTrue(Resolve(copy, Hit(root)).IsReplay);
            var descendant = Derived(copy, root, 2, frame: 200);
            Assert.IsTrue(copy.Suppresses(descendant)); Assert.IsTrue(Resolve(copy, Hit(descendant, frame: 201)).SuppressAttackJudgment);
            Assert.AreEqual(1, state.SourceCount); Assert.AreEqual(1, state.ReceiptCount);
            Assert.IsFalse(copy.TryRegisterRoot(3, 23, 5, 202, out _));
            state.InvalidateStage(); Assert.IsTrue(copy.Suppresses(root));
        }

        [Test]
        public void OathPauseDoesNotRefundConsumedInterruptAndRetainsDelayedSourceConstraint()
        {
            var state = State(oath: true); var root = Root(state); Resolve(state, Hit(root));
            var copy = state.CloneForTrustedPause(); var delayed = Derived(copy, root, 2, frame: 500);
            var decision = Resolve(copy, Hit(delayed, frame: 501));
            Assert.IsTrue(decision.NonLethalHp); Assert.IsFalse(decision.AttemptOrdinaryInterrupt);
            Assert.IsTrue(Resolve(copy, Hit(delayed, Other, frame: 502)).AttemptOrdinaryInterrupt);
            Assert.AreEqual(1, state.InterruptAttemptCount);
        }

        [Test]
        public void PooledActionRevisionMustChangeAndStageTeardownInvalidatesEverySource()
        {
            var state = State(oath: true); var old = Root(state);
            var reused = Root(state, 2, 22, 5, 200);
            Assert.IsFalse(old.Matches(reused));
            Assert.IsFalse(state.TryRegisterRoot(3, 22, 4, 201, out _));
            Assert.IsFalse(state.TryRegisterRoot(3, 22, 3, 201, out _));
            Assert.IsFalse(state.TryRegisterRoot(3, 22, 6, 199, out _));
            var modifiedOld = new TacticalSecondBatchSource(old.Commit, old.Node, old.Parent, old.Producer,
                old.RootActionInstance, 5, old.CreatedFrame);
            Assert.IsFalse(state.TryRegisterDerived(modifiedOld, 3, Actor, 201, out _));
            state.InvalidateStage();
            Assert.IsFalse(state.TryRegisterDerived(reused, 3, Actor, 201, out _));
            Assert.IsFalse(state.TryResolveBatch(new[] { Hit(old) }, 1, new TacticalSecondBatchHitDecision[1]));
            Assert.AreEqual(1000, state.RearScale(Facts(), Direction()));
        }

        [Test]
        public void SharedOwnerCanInspectAllLeafFactsWithoutMintingTransportRestorationAuthority()
        {
            var state = State(oath: true, punch: true); var root = Root(state); var hit = Hit(root);
            Resolve(state, hit); state.EndHost(); state.RevokeRearOnActorDeparture();
            var copy = state.CloneForTrustedPause();
            Assert.IsTrue(copy.SourceAt(0).Matches(root)); Assert.IsTrue(copy.ReceiptAt(0).Matches(hit));
            Assert.AreEqual(Enemy, copy.InterruptAttemptAt(0)); Assert.AreEqual(0, copy.LastOrder.CompareTo(hit.Key));
            Assert.AreEqual(0, copy.Winner.CompareTo(hit.Key)); Assert.IsTrue(copy.HostEnded); Assert.IsTrue(copy.RearRevoked);
            Assert.IsFalse(copy.IsInvalidated); copy.InvalidateStage(); Assert.IsTrue(copy.IsInvalidated);
            Assert.IsFalse(state.IsInvalidated);
        }

        [Test]
        public void InvalidEvidenceAndInvalidParametersDoNotMintAuthorityOrSpendTheUniqueHit()
        {
            Assert.IsFalse(TacticalAccessorySecondBatchState.TryCreateForConfirmedCommit(default, 2, Parameters(punch: true), out _));
            Assert.IsFalse(TacticalAccessorySecondBatchState.TryCreateForConfirmedCommit(Commit(), 0, Parameters(punch: true), out _));
            Assert.IsFalse(TacticalAccessorySecondBatchState.TryCreateForConfirmedCommit(Commit(), 2, Parameters(), out _));
            Assert.IsFalse(new TacticalSecondBatchParameters(true, false, false, 1000, 0).IsValid);
            Assert.IsFalse(new TacticalSecondBatchParameters(false, true, false, 1500, 0).IsValid);
            var state = State(punch: true); var root = Root(state);
            var decisions = new TacticalSecondBatchHitDecision[1];
            Assert.IsFalse(state.TryResolveBatch(new[] { Hit(root, frame: 99) }, 1, decisions));
            Assert.IsFalse(state.TryResolveBatch(new[] { Hit(root, node: 2) }, 1, decisions));
            Assert.IsFalse(state.TryResolveBatch(new[] { Hit(root, outcome: (HitResolveKind)99) }, 1, decisions));
            Assert.IsFalse(state.TryResolveBatch(new[] { Hit(root, alive: false) }, 1, decisions));
            Assert.IsFalse(state.TryResolveBatch(new[] { Hit(root, occurrence: 0) }, 1, decisions));
            Assert.IsFalse(state.TryResolveBatch(new[] { Hit(root) }, 2, decisions));
            Assert.IsFalse(state.TryResolveBatch(null, 1, decisions));
            Assert.IsFalse(state.OnePunchConsumed); Assert.AreEqual(0, state.ReceiptCount);
        }

        [Test]
        public void ActualSelfDamageClassificationDoesNotRejectTheOtherLegalHit()
        {
            var state = State(oath: true, punch: true); var root = Root(state);
            var hits = new[] { Hit(root, Actor, stableEvent: 1, outcome: HitResolveKind.SelfDamage, enemy: false),
                Hit(root, Enemy, stableEvent: 2) };
            var decisions = new TacticalSecondBatchHitDecision[2];
            Assert.IsTrue(state.TryResolveBatch(hits, 2, decisions), "Actual SelfDamage must not poison the whole batch.");
            Assert.AreEqual(Enemy, state.Winner.Target);
            Assert.AreEqual(1000, decisions[0].DamageScaleMilli); Assert.IsFalse(decisions[0].AttemptOrdinaryInterrupt);
            Assert.IsFalse(decisions[0].NonLethalHp); Assert.AreEqual(2000, decisions[1].DamageScaleMilli);
            Assert.IsTrue(decisions[1].AttemptOrdinaryInterrupt); Assert.AreEqual(1, state.InterruptAttemptCount);
        }

        [TestCase(HitResolveKind.Damage)] [TestCase(HitResolveKind.Blocked)] [TestCase(HitResolveKind.Parried)]
        public void SelfTargetCannotMasqueradeAsAnAcceptedOrDefendedAttack(HitResolveKind outcome)
        {
            var state = State(oath: true, punch: true); var root = Root(state);
            var hits = new[] { Hit(root, Actor, outcome: outcome, enemy: false), Hit(root, Enemy, stableEvent: 2) };
            Assert.IsFalse(state.TryResolveBatch(hits, 2, new TacticalSecondBatchHitDecision[2]));
            Assert.IsFalse(state.OnePunchConsumed); Assert.AreEqual(0, state.InterruptAttemptCount);
            Assert.AreEqual(0, state.ReceiptCount);
        }

        [Test]
        public void LivingReceiversRearRiskSurvivesAnUnrelatedShootersDeath()
        {
            var state = State(rear: true);
            var facts = Facts(attacker: Enemy, target: Actor, attackerAlive: false,
                kind: DamageProfileSourceKind.ProjectileImpact);
            Assert.AreEqual(1500, state.RearScale(facts, Direction()),
                "Only the living receiver owns this active rear-risk rule.");
            Assert.AreEqual(1000, state.RearScale(Facts(attacker: Enemy, target: Actor,
                attackerAlive: false, targetAlive: false, kind: DamageProfileSourceKind.ProjectileImpact), Direction()));
            Assert.AreEqual(1000, state.RearScale(Facts(attackerAlive: false,
                kind: DamageProfileSourceKind.ProjectileImpact), Direction()), "A dead holder cannot gain its old rear bonus.");
            state.RevokeRearOnActorDeparture();
            Assert.AreEqual(1000, state.RearScale(facts, Direction()));
        }

        [Test]
        public void CausalSameFrameDescendantCanResolveAfterItsParentWasAccepted()
        {
            var state = State(oath: true); var root = Root(state);
            var parent = Hit(root, frame: 101, stableEvent: 100); Resolve(state, parent);
            Assert.IsTrue(state.TryRegisterDerivedAfterHit(root, parent.Key, 2, Actor, 101, out var child));
            var childHit = Hit(child, frame: 101, stableEvent: 1);
            Assert.AreEqual(1, child.BirthRound); Assert.AreEqual(1, childHit.Key.CausalRound);
            Assert.AreEqual(101, child.CreatedFrame); Assert.AreEqual(101, childHit.Key.Frame);
            Assert.AreEqual(0, child.CausalTrigger.CompareTo(parent.Key));
            Assert.Greater(childHit.Key.CompareTo(parent.Key), 0, "Causal round orders child event1 AFTER parent event100.");
            Assert.IsTrue(Resolve(state, childHit).NonLethalHp, "A legal same-frame child retains the oath.");
            Assert.AreEqual(101, state.ClosedFrame); Assert.AreEqual(1, state.ClosedRound);
            Assert.AreEqual(2, state.ReceiptCount);
            Assert.IsTrue(Resolve(state, parent).IsReplay); Assert.AreEqual(1, state.InterruptAttemptCount);
        }

        [Test]
        public void EmptyRoundsAreExplicitlyClosedAndLateNewRootsCannotReopenThem()
        {
            var state = State(oath: true); var root = Root(state);
            var none = Array.Empty<TacticalSecondBatchHit>(); var noDecisions = Array.Empty<TacticalSecondBatchHitDecision>();
            Assert.IsFalse(state.TryCloseCausalRound(101, 1), "A frame starts at round zero.");
            Assert.IsTrue(state.TryResolveRound(101, 0, none, 0, noDecisions));
            Assert.AreEqual(101, state.ClosedFrame); Assert.AreEqual(0, state.ClosedRound);
            Assert.IsTrue(state.TryCloseCausalRound(101, 0), "Exact latest closure is idempotent.");
            Assert.IsFalse(state.TryCloseCausalRound(101, 2), "No round skipping, even when empty.");
            Assert.IsTrue(state.TryCloseCausalRound(101, 1));
            Assert.IsFalse(state.TryResolveBatch(new[] { Hit(root, frame: 101, stableEvent: 1000) }, 1,
                new TacticalSecondBatchHitDecision[1]));
            Assert.IsFalse(state.TryRegisterRoot(2, 23, 5, 101, out _));
            Assert.IsFalse(state.TryRegisterDerived(root, 2, Actor, 101, out _), "Late non-hit production cannot invent a causal round.");
            Assert.IsFalse(state.TryCloseCausalRound(100, 0));
            Assert.IsFalse(state.TryCloseCausalRound(102, 1), "An actual new frame resets to zero.");
            Assert.IsTrue(state.TryResolveRound(102, 0, new[] { Hit(root, frame: 102) }, 1,
                new TacticalSecondBatchHitDecision[1]));
            Assert.AreEqual(1, state.ReceiptCount); Assert.AreEqual(102, state.ClosedFrame); Assert.AreEqual(0, state.ClosedRound);
        }

        [TestCase(-1L, -1)] [TestCase(99L, 0)] [TestCase(long.MaxValue, 0)]
        [TestCase(101L, -1)] [TestCase(101L, int.MaxValue)]
        public void InvalidRoundIdentityCannotCloseEvenTheInitialEmptyState(long frame, int round)
        {
            var state = State(oath: true);
            Assert.IsFalse(state.TryCloseCausalRound(frame, round));
            Assert.IsFalse(state.TryResolveRound(frame, round, Array.Empty<TacticalSecondBatchHit>(), 0,
                Array.Empty<TacticalSecondBatchHitDecision>()));
            Assert.AreEqual(-1, state.ClosedFrame); Assert.AreEqual(-1, state.ClosedRound);
        }

        [Test]
        public void SameRoundLateCandidatesAreRejectedEvenWhenTheirEventIdIsHigher()
        {
            var state = State(oath: true); var root = Root(state); Resolve(state, Hit(root, frame: 101, stableEvent: 100));
            var decisions = new TacticalSecondBatchHitDecision[1];
            Assert.IsFalse(state.TryResolveBatch(new[] { Hit(root, Other, frame: 101, stableEvent: 1) }, 1, decisions));
            Assert.IsFalse(state.TryResolveBatch(new[] { Hit(root, Other, frame: 101, stableEvent: 200) }, 1, decisions));
            Assert.AreEqual(1, state.ReceiptCount); Assert.AreEqual(0, state.ClosedRound);
        }

        [Test]
        public void CausalSiblingsAreGatheredAndSortedWithinTheirRoundRegardlessOfRegistrationOrder()
        {
            foreach (bool reverse in new[] { false, true }) {
                var state = State(oath: true); var root = Root(state); var parent = Hit(root, frame: 101, stableEvent: 100);
                Resolve(state, parent);
                TacticalSecondBatchSource first, second;
                if (reverse) {
                    Assert.IsTrue(state.TryRegisterDerivedAfterHit(root, parent.Key, 3, Actor, 101, out second));
                    Assert.IsTrue(state.TryRegisterDerivedAfterHit(root, parent.Key, 2, Actor, 101, out first));
                } else {
                    Assert.IsTrue(state.TryRegisterDerivedAfterHit(root, parent.Key, 2, Actor, 101, out first));
                    Assert.IsTrue(state.TryRegisterDerivedAfterHit(root, parent.Key, 3, Actor, 101, out second));
                }
                var a = Hit(first, Other, frame: 101, stableEvent: 50); var b = Hit(second, Enemy, frame: 101, stableEvent: 1);
                var hits = reverse ? new[] { b, a } : new[] { a, b }; var decisions = new TacticalSecondBatchHitDecision[2];
                Assert.IsTrue(state.TryResolveRound(101, 1, hits, 2, decisions));
                Assert.AreEqual(3, hits[0].Source.Node); Assert.AreEqual(1, hits[0].Key.CausalRound);
                Assert.AreEqual(1, state.SourceAt(0).Node); Assert.AreEqual(2, state.SourceAt(1).Node);
                Assert.AreEqual(3, state.SourceAt(2).Node, "The shared hash source view must not depend on registration enumeration.");
                Assert.IsFalse(decisions[0].AttemptOrdinaryInterrupt); Assert.IsTrue(decisions[1].AttemptOrdinaryInterrupt);
                Assert.AreEqual(2, state.InterruptAttemptCount); Assert.AreEqual(3, state.ReceiptCount);
            }
        }

        [Test]
        public void AcceptedParentProofIsExactAndCannotBeTakenFromAnUnrelatedSource()
        {
            var state = State(oath: true); var root = Root(state); var unrelated = Root(state, 2, 23, 5);
            var parent = Hit(root, frame: 101, stableEvent: 100);
            Assert.IsFalse(state.TryRegisterDerivedAfterHit(root, parent.Key, 3, Actor, 101, out _), "No receipt yet.");
            Resolve(state, parent);
            Assert.IsFalse(state.TryRegisterDerivedAfterHit(unrelated, parent.Key, 3, Actor, 101, out _));
            Assert.IsFalse(state.TryRegisterDerivedAfterHit(root, Hit(root, Other, frame: 101, stableEvent: 100).Key,
                3, Actor, 101, out _));
            Assert.IsFalse(state.TryRegisterDerivedAfterHit(root, parent.Key, 3, Actor, 100, out _));
            Assert.IsFalse(state.TryRegisterDerived(root, 3, Actor, 101, out _), "The old non-hit API must not guess this cause.");
            Assert.IsTrue(state.TryRegisterDerivedAfterHit(root, parent.Key, 3, Actor, 101, out var child));
            Assert.IsTrue(state.TryRegisterDerivedAfterHit(root, parent.Key, 3, Actor, 101, out var replay));
            Assert.IsTrue(child.Matches(replay));
            Assert.IsFalse(state.TryRegisterDerived(root, 3, Actor, 101, out _), "Wrong cause metadata is not an exact replay.");
        }

        [TestCase(HitResolveKind.Evaded)] [TestCase(HitResolveKind.Invincible)] [TestCase(HitResolveKind.Duplicate)]
        [TestCase(HitResolveKind.Friendly)] [TestCase(HitResolveKind.TargetDead)] [TestCase(HitResolveKind.SelfDamage)]
        public void RejectedClassificationsCannotMintAnOnHitCausalRound(HitResolveKind outcome)
        {
            var state = State(oath: true); var root = Root(state);
            var parent = Hit(root, outcome == HitResolveKind.SelfDamage ? Actor : Enemy, frame: 101,
                outcome: outcome, alive: outcome != HitResolveKind.TargetDead, enemy: outcome != HitResolveKind.SelfDamage);
            Resolve(state, parent);
            Assert.IsFalse(state.TryRegisterDerivedAfterHit(root, parent.Key, 2, Actor, 101, out _));
            Assert.AreEqual(1, state.SourceCount); Assert.AreEqual(0, state.ClosedRound);
        }

        [TestCase(HitResolveKind.Blocked)] [TestCase(HitResolveKind.Parried)]
        public void AcceptedDefenseMayBeACauseButPunchStillSuppressesItsChildAttack(HitResolveKind outcome)
        {
            var state = State(oath: true, punch: true); var root = Root(state);
            var parent = Hit(root, frame: 101, outcome: outcome); Resolve(state, parent);
            Assert.IsTrue(state.TryRegisterDerivedAfterHit(root, parent.Key, 2, Actor, 101, out var child));
            var decision = Resolve(state, Hit(child, frame: 101));
            Assert.IsTrue(decision.SuppressAttackJudgment); Assert.IsFalse(decision.AttemptOrdinaryInterrupt);
            Assert.AreEqual(0, state.InterruptAttemptCount);
        }

        [Test]
        public void AParentSuppressedByOnePunchCannotCreateAnOnHitChildSource()
        {
            var state = State(oath: true, punch: true); var root = Root(state);
            var winner = Hit(root, frame: 101, stableEvent: 1); var suppressed = Hit(root, Other, frame: 101, stableEvent: 2);
            Assert.IsTrue(state.TryResolveRound(101, 0, new[] { suppressed, winner }, 2,
                new TacticalSecondBatchHitDecision[2]));
            Assert.IsFalse(state.TryRegisterDerivedAfterHit(root, suppressed.Key, 2, Actor, 101, out _));
            Assert.IsTrue(state.TryRegisterDerivedAfterHit(root, winner.Key, 2, Actor, 101, out var child));
            Assert.IsTrue(Resolve(state, Hit(child, frame: 101)).SuppressAttackJudgment);
            Assert.AreEqual(1, state.InterruptAttemptCount);
        }

        [Test]
        public void SameFrameNonHitDescendantsInheritTheirStillOpenParentBirthRound()
        {
            var state = State(oath: true); var root = Root(state); var parent = Hit(root, frame: 101); Resolve(state, parent);
            Assert.IsTrue(state.TryRegisterDerivedAfterHit(root, parent.Key, 2, Actor, 101, out var child));
            Assert.IsTrue(state.TryRegisterDerived(child, 3, Actor, 101, out var nested));
            Assert.AreEqual(1, nested.BirthRound); Assert.IsFalse(nested.CausalTrigger.IsValid);
            Assert.IsTrue(state.TryResolveRound(101, 1, new[] { Hit(child, frame: 101, stableEvent: 2),
                Hit(nested, Other, frame: 101, stableEvent: 1) }, 2, new TacticalSecondBatchHitDecision[2]));
            Assert.IsFalse(state.TryRegisterDerived(child, 4, Actor, 101, out _), "Closed wave cannot acquire a late sibling.");
            Assert.IsFalse(state.TryRegisterDerivedAfterHit(root, parent.Key, 4, Actor, 101, out _));
        }

        [Test]
        public void OnlyAnActualDelayedImpactResetsToANewFramesRoundZero()
        {
            var state = State(oath: true); var root = Root(state); var parent = Hit(root, frame: 101); Resolve(state, parent);
            Assert.IsTrue(state.TryRegisterDerivedAfterHit(root, parent.Key, 2, Actor, 101, out var child));
            Assert.AreEqual(1, child.CreateHitKey(101, 1, Enemy, 1).CausalRound);
            Assert.AreEqual(0, child.CreateHitKey(102, 1, Enemy, 1).CausalRound);
            Assert.IsTrue(state.TryCloseCausalRound(101, 1));
            Assert.IsTrue(Resolve(state, Hit(child, frame: 102)).NonLethalHp);
            Assert.AreEqual(102, state.ClosedFrame); Assert.AreEqual(0, state.ClosedRound);
        }

        [Test]
        public void AnAirborneCausalBirthRequiresItsEmptyRoundToCloseBeforeTheNextFrame()
        {
            var state = State(oath: true); var root = Root(state); var parent = Hit(root, frame: 101); Resolve(state, parent);
            Assert.IsTrue(state.TryRegisterDerivedAfterHit(root, parent.Key, 2, Actor, 101, out var airborne));
            var delayedHit = Hit(airborne, frame: 102);
            Assert.IsFalse(state.TryResolveBatch(new[] { delayedHit }, 1, new TacticalSecondBatchHitDecision[1]),
                "An actual delayed impact cannot silently skip its recorded birth round's empty closure.");
            Assert.IsFalse(state.TryCloseCausalRound(102, 0));
            Assert.AreEqual(101, state.ClosedFrame); Assert.AreEqual(0, state.ClosedRound); Assert.AreEqual(1, state.ReceiptCount);
            Assert.IsTrue(state.TryResolveRound(101, 1, Array.Empty<TacticalSecondBatchHit>(), 0,
                Array.Empty<TacticalSecondBatchHitDecision>()));
            Assert.IsTrue(Resolve(state, delayedHit).NonLethalHp);
            Assert.AreEqual(102, state.ClosedFrame); Assert.AreEqual(0, state.ClosedRound);
        }

        [Test]
        public void RoundAndParentTriggerRemainFrozenAcrossTrustedPauseAndResume()
        {
            var state = State(oath: true); var root = Root(state); var parent = Hit(root, frame: 101, stableEvent: 100);
            Resolve(state, parent);
            Assert.IsTrue(state.TryRegisterDerivedAfterHit(root, parent.Key, 2, Actor, 101, out var child));
            var childHit = Hit(child, frame: 101, stableEvent: 1); Resolve(state, childHit);
            var copy = state.CloneForTrustedPause();
            Assert.AreEqual(101, copy.ClosedFrame); Assert.AreEqual(1, copy.ClosedRound);
            Assert.AreEqual(1, copy.SourceAt(1).BirthRound);
            Assert.AreEqual(0, copy.SourceAt(1).CausalTrigger.CompareTo(parent.Key));
            Assert.IsTrue(Resolve(copy, childHit).IsReplay);
            Assert.IsFalse(copy.TryRegisterDerivedAfterHit(root, parent.Key, 3, Actor, 101, out _));
            Assert.IsTrue(copy.TryRegisterDerivedAfterHit(child, childHit.Key, 3, Actor, 101, out var grandchild));
            Assert.AreEqual(2, grandchild.BirthRound); Assert.IsTrue(Resolve(copy, Hit(grandchild, frame: 101)).NonLethalHp);
            Assert.AreEqual(2, copy.ClosedRound); Assert.AreEqual(1, state.ClosedRound);
            Assert.AreEqual(2, state.SourceCount); Assert.AreEqual(2, state.ReceiptCount);
            Assert.AreEqual(1, copy.InterruptAttemptCount);
        }

        [Test]
        public void ACallerCannotAssignItsOwnHigherRoundOrUseTheRoundZeroKeyForACausalChild()
        {
            var state = State(oath: true); var root = Root(state); var parent = Hit(root, frame: 101); Resolve(state, parent);
            Assert.IsTrue(state.TryRegisterDerivedAfterHit(root, parent.Key, 2, Actor, 101, out var child));
            var invented = new TacticalSecondBatchHit(root, new TacticalSecondBatchHitKey(101, 1, 1, root.Node, Other, 1),
                Actor, HitResolveKind.Damage, true, true);
            Assert.IsFalse(state.TryResolveBatch(new[] { invented }, 1, new TacticalSecondBatchHitDecision[1]));
            var missingRound = new TacticalSecondBatchHit(child, new TacticalSecondBatchHitKey(101, 1, child.Node, Enemy, 1),
                Actor, HitResolveKind.Damage, true, true);
            Assert.IsFalse(state.TryResolveBatch(new[] { missingRound }, 1, new TacticalSecondBatchHitDecision[1]));
            Assert.AreEqual(1, state.ReceiptCount); Assert.AreEqual(0, state.ClosedRound);
            Assert.IsTrue(Resolve(state, Hit(child, frame: 101)).NonLethalHp);
        }

        [TestCase(-1, false, 0)] [TestCase(0, true, 1)] [TestCase(int.MaxValue - 2, true, int.MaxValue - 1)]
        [TestCase(int.MaxValue - 1, false, 0)] [TestCase(int.MaxValue, false, 0)]
        public void CausalRoundOverflowIsRejectedWithoutWrappingOrDeferring(int parent, bool success, int expected)
        {
            Assert.AreEqual(success, TacticalAccessorySecondBatchRules.TryNextCausalRound(parent, out int child));
            Assert.AreEqual(expected, child);
        }

        [Test]
        public void ExactOldRoundAndOldFrameReplaysRemainReadOnlyThroughTheExplicitProtocol()
        {
            var state = State(oath: true); var root = Root(state); var parent = Hit(root, frame: 101, stableEvent: 100);
            Resolve(state, parent);
            Assert.IsTrue(state.TryRegisterDerivedAfterHit(root, parent.Key, 2, Actor, 101, out var child));
            var childHit = Hit(child, frame: 101); Resolve(state, childHit);
            var decisions = new TacticalSecondBatchHitDecision[1];
            Assert.IsTrue(state.TryResolveRound(101, 0, new[] { parent }, 1, decisions));
            Assert.IsTrue(decisions[0].IsReplay); Assert.IsFalse(decisions[0].AttemptOrdinaryInterrupt);
            Assert.AreEqual(1, state.ClosedRound);
            Resolve(state, Hit(root, Other, frame: 102));
            var last = state.LastOrder; int receipts = state.ReceiptCount, attempts = state.InterruptAttemptCount;
            Assert.IsTrue(state.TryResolveRound(101, 1, new[] { childHit }, 1, decisions));
            Assert.IsTrue(decisions[0].IsReplay); Assert.IsTrue(decisions[0].SuppressAttackJudgment);
            Assert.IsTrue(state.TryResolveRound(101, 0, new[] { parent }, 1, decisions));
            Assert.IsFalse(state.TryResolveRound(101, 1, new[] { parent }, 1, decisions), "Declared round must match the replay.");
            Assert.IsFalse(state.TryResolveRound(103, 2, new[] { parent }, 1, decisions), "A replay cannot authorize future closure.");
            Assert.IsFalse(state.TryResolveRound(101, 1, new[] { Hit(child, frame: 101, outcome: HitResolveKind.Blocked) }, 1, decisions));
            Assert.AreEqual(102, state.ClosedFrame); Assert.AreEqual(0, state.ClosedRound);
            Assert.AreEqual(0, state.LastOrder.CompareTo(last)); Assert.AreEqual(receipts, state.ReceiptCount);
            Assert.AreEqual(attempts, state.InterruptAttemptCount);
        }

        [Test]
        public void EqualLocalNodesAcrossCommitsHaveAStableStageOrderAndCannotAdoptEachOthersParents()
        {
            foreach (bool reverse in new[] { false, true }) {
                var oath = State(oath: true);
                var punch = State(punch: true, commit: Commit(commit: 18, execution: 19, card: 20));
                var oathRoot = Root(oath); var punchRoot = Root(punch);
                var oathHit = Hit(oathRoot); var punchHit = Hit(punchRoot);
                Assert.AreEqual(0, oathHit.Key.CompareTo(punchHit.Key), "Bare local keys deliberately collide in this counterexample.");
                Assert.AreNotEqual(0, oathHit.CompareTo(punchHit), "The stage order must include a fixed commit tie-breaker.");
                var hits = reverse ? new[] { punchHit, oathHit } : new[] { oathHit, punchHit };
                Array.Sort(hits);
                Assert.IsTrue(hits[0].Source.Commit.Matches(oath.Commit));
                int hp = 10;
                foreach (var hit in hits) {
                    var state = hit.Source.Commit.Matches(oath.Commit) ? oath : punch;
                    var decision = Resolve(state, hit);
                    Assert.IsTrue(TacticalAccessorySecondBatchRules.TryScale(state.Parameters.OnePunch ? 3 : 6,
                        1000, decision.DamageScaleMilli, out int budget));
                    Assert.IsTrue(TacticalAccessorySecondBatchRules.TryLimitHpDebit(decision.NonLethalHp, hp, budget, out int debit));
                    hp -= debit;
                }
                Assert.AreEqual(0, hp, "Oath before normal damage must have the same deterministic result for both enumerations.");
                Assert.AreEqual(oath.ClosedFrame, punch.ClosedFrame); Assert.AreEqual(oath.ClosedRound, punch.ClosedRound);
                Assert.IsFalse(punch.TryRegisterDerivedAfterHit(oathRoot, oathHit.Key, 2, Actor, 103, out _));
                Assert.IsFalse(oath.TryRegisterDerivedAfterHit(punchRoot, punchHit.Key, 2, Actor, 103, out _));
            }
        }

        [TestCase(0)] [TestCase(1)] [TestCase(2)] [TestCase(3)] [TestCase(4)] [TestCase(5)]
        public void EveryParentKeyFieldIncludingTargetGenerationAndRootOccurrenceIsPartOfTheProof(int changed)
        {
            var state = State(oath: true); var root = Root(state); var parent = Hit(root, frame: 101, stableEvent: 100);
            Resolve(state, parent);
            var forged = new TacticalSecondBatchHitKey(changed == 0 ? 102 : 101, changed == 1 ? 1 : 0,
                changed == 2 ? 101UL : 100UL, changed == 3 ? 2UL : root.Node,
                changed == 4 ? new GameplayEntityId(Enemy.Index, Enemy.Generation + 1) : Enemy,
                changed == 5 ? 2UL : 1UL);
            Assert.IsFalse(state.TryRegisterDerivedAfterHit(root, forged, 2, Actor, 102, out _));
            Assert.AreEqual(1, state.SourceCount); Assert.AreEqual(1, state.ReceiptCount);
        }

        [Test]
        public void DelayedBirthRetainsSavedParentProofAndActualBirthContextInsteadOfTheCurrentCard()
        {
            var state = State(rear: true, oath: true); var root = Root(state); var parent = Hit(root, frame: 101);
            Resolve(state, parent); var savedSource = root; var savedKey = parent.Key;
            state.EndHost(); state.RevokeRearOnActorDeparture(); var restored = state.CloneForTrustedPause();
            var nextCard = State(punch: true, commit: Commit(commit: 18, execution: 19, card: 20, frame: 400));
            Root(nextCard, revision: 5, frame: 400);
            Assert.IsFalse(nextCard.TryRegisterDerivedAfterHit(savedSource, savedKey, 2, Actor, 500, out _));
            Assert.IsTrue(restored.TryRegisterDerivedAfterHit(savedSource, savedKey, 2, Actor, 500, out var born));
            Assert.AreEqual(500, born.CreatedFrame); Assert.AreEqual(0, born.BirthRound);
            Assert.AreEqual(4, born.RootActionRevision); Assert.IsTrue(born.Commit.Matches(state.Commit));
            Assert.AreEqual(0, born.CausalTrigger.CompareTo(savedKey));
            var replacementTarget = new GameplayEntityId(Enemy.Index, Enemy.Generation + 1);
            var actualHit = new TacticalSecondBatchHit(born, born.CreateHitKey(501, 1, replacementTarget, 1),
                Actor, HitResolveKind.Damage, true, true);
            var result = Resolve(restored, actualHit);
            Assert.IsTrue(result.NonLethalHp); Assert.IsTrue(result.AttemptOrdinaryInterrupt);
            Assert.AreEqual(2, restored.InterruptAttemptCount); Assert.AreEqual(1, state.InterruptAttemptCount);
        }

        [TestCase(101L, 1)] [TestCase(102L, 0)]
        public void AnOldReceiptCannotCloseANewRound(long frame, int round)
        {
            var state = State(oath: true); var root = Root(state); var parent = Hit(root, frame: 101, stableEvent: 100);
            Resolve(state, parent); var last = state.LastOrder;
            var decisions = new[] { new TacticalSecondBatchHitDecision(false, false, 777, false, false) };
            var hits = new[] { parent };
            Assert.IsFalse(state.TryResolveRound(frame, round, hits, 1, decisions),
                "An old receipt must not authorize a different declared phase.");
            Assert.AreEqual(101, state.ClosedFrame); Assert.AreEqual(0, state.ClosedRound);
            Assert.AreEqual(0, state.LastOrder.CompareTo(last)); Assert.AreEqual(1, state.ReceiptCount);
            Assert.AreEqual(1, state.InterruptAttemptCount); Assert.AreEqual(1, state.SourceCount);
            Assert.IsTrue(state.SourceAt(0).Matches(root)); Assert.IsFalse(state.OnePunchConsumed);
            Assert.AreEqual(777, decisions[0].DamageScaleMilli, "A rejected declaration leaves output buffers untouched.");
            Assert.IsTrue(hits[0].Matches(parent));
        }

        [Test]
        public void LatestClosedRoundReplayMustAlsoMatchDeclaredIdentity()
        {
            var state = State(oath: true); var root = Root(state); var parent = Hit(root, frame: 101, stableEvent: 100);
            Resolve(state, parent); Assert.IsTrue(state.TryCloseCausalRound(101, 1));
            var last = state.LastOrder; var decisions = new TacticalSecondBatchHitDecision[1];
            Assert.IsFalse(state.TryResolveRound(101, 1, new[] { parent }, 1, decisions));
            Assert.AreEqual(101, state.ClosedFrame); Assert.AreEqual(1, state.ClosedRound);
            Assert.AreEqual(0, state.LastOrder.CompareTo(last)); Assert.AreEqual(1, state.ReceiptCount);
            Assert.AreEqual(1, state.InterruptAttemptCount);
            Assert.IsTrue(state.TryResolveRound(101, 0, new[] { parent }, 1, decisions));
            Assert.IsTrue(decisions[0].IsReplay); Assert.IsTrue(decisions[0].SuppressAttackJudgment);
            Assert.IsFalse(decisions[0].AttemptOrdinaryInterrupt);
            Assert.AreEqual(101, state.ClosedFrame); Assert.AreEqual(1, state.ClosedRound);
            Assert.AreEqual(0, state.LastOrder.CompareTo(last)); Assert.AreEqual(1, state.ReceiptCount);
            Assert.AreEqual(1, state.InterruptAttemptCount);
        }
    }
}
