using System;
using MxFramework.Combat.Hit;
using MxFramework.Game.CharacterTest.TacticalCard;
using MxFramework.Gameplay;
using MxFramework.Runtime;
using NUnit.Framework;

namespace MxFramework.Tests.TacticalCard
{
    [TestFixture]
    internal sealed class TacticalAccessoryThirdBatchTests
    {
        private static readonly GameplayEntityId Self = new GameplayEntityId(1, 1);
        private static readonly GameplayEntityId Enemy = new GameplayEntityId(2, 1);
        private static readonly GameplayEntityId Other = new GameplayEntityId(3, 1);
        private static TacticalThirdBatchOrder Order(ulong id, GameplayEntityId actor, ulong node = 11,
            long frame = 10, int round = 0, ulong occurrence = 1)
            => new TacticalThirdBatchOrder(frame, round, id, node, actor, occurrence);
        private static TacticalThirdBatchCommit Commit(ulong id = 1, ulong execution = 101, ulong stage = 7,
            ulong turn = 1, GameplayEntityId actor = default, ulong card = 1001, ulong activation = 1, long frame = 10)
        {
            if (!actor.IsValid) actor = Self;
            return new TacticalThirdBatchCommit(stage, id, execution, actor, card, turn, Order(activation, actor, 1, frame));
        }
        private static TacticalThirdBatchAttack Attack(GameplayEntityId actor, ulong id = 2, ulong execution = 101,
            long frame = 10, int round = 0, int instance = 1, ulong revision = 1, ulong stage = 7)
            => new TacticalThirdBatchAttack(stage, execution, actor, instance, revision, Order(id, actor, 10, frame, round));
        private static TacticalThirdBatchSource Source(TacticalThirdBatchAttack attack, ulong id = 3, ulong node = 11,
            TacticalThirdBatchOrigin origin = TacticalThirdBatchOrigin.DirectWeapon, long frame = 10, int round = 0)
            => new TacticalThirdBatchSource(attack, node, origin, Order(id, attack.Actor, node, frame, round));
        private static TacticalThirdBatchEvent Start(TacticalThirdBatchAttack attack, bool eligible = true, bool enemy = false)
            => TacticalThirdBatchEvent.Start(attack.AcceptedOrder, attack, true, enemy, eligible);
        private static TacticalThirdBatchEvent Hit(TacticalThirdBatchSource source, GameplayEntityId target, ulong id = 4,
            HitResolveKind outcome = HitResolveKind.Damage, bool alive = true, bool enemy = true,
            bool suppressed = false, long frame = 10, int round = 0, ulong occurrence = 1)
            => TacticalThirdBatchEvent.Hit(Order(id, target, source.Node, frame, round, occurrence), source, outcome, alive, enemy, suppressed);
        private static TacticalThirdBatchEvent EndHost(ulong id = 8, ulong execution = 101, long frame = 10)
            => TacticalThirdBatchEvent.EndHost(Order(id, Self, 90, frame), Self, execution);
        private static TacticalThirdBatchEvent Settle(ulong turn, long frame = 10, int round = 1)
            => TacticalThirdBatchEvent.SettleFullTurn(Order(1, Self, 90, frame, round), turn);
        private static TacticalThirdBatchSource OldEnemySource()
            => Source(Attack(Enemy, 1, 201, 0), 2, 12, TacticalThirdBatchOrigin.DirectProjectile, 0);
        private static TacticalThirdBatchCoinReceipt Coin(TacticalThirdBatchCoinFace face, TacticalThirdBatchCommit commit = default)
        {
            if (!commit.IsValid) commit = Commit();
            var ledger = new TacticalThirdBatchCoinLedger(commit.Stage, face == TacticalThirdBatchCoinFace.SelfExecution ? 1u : 8192u);
            Assert.That(ledger.TryPublish(true, commit, Enemy, out var receipt), Is.True);
            Assert.That(receipt.Face, Is.EqualTo(face));
            return receipt;
        }
        private static TacticalAccessoryThirdBatchState State(bool glass = false, bool throne = false,
            TacticalThirdBatchCoinFace face = TacticalThirdBatchCoinFace.Unpublished)
            => new TacticalAccessoryThirdBatchState(Commit(), glass, throne,
                face == TacticalThirdBatchCoinFace.Unpublished ? default : Coin(face));
        // Actual data is accepted serially after the stable key schedule is sealed. This is a local harness,
        // not a replacement Combat, Motion, Runtime host or payment implementation.
        private static TacticalThirdBatchDecision[] Play(TacticalAccessoryThirdBatchState state, params TacticalThirdBatchEvent[] events)
        {
            Assert.That(events.Length, Is.GreaterThan(0));
            var keys = new TacticalThirdBatchOrder[events.Length];
            for (int i = 0; i < events.Length; i++) keys[i] = events[i].Order;
            Assert.That(state.TryOpenRound(keys[0].Frame, keys[0].Round, keys, keys.Length), Is.True);
            var sorted = (TacticalThirdBatchEvent[])events.Clone();
            Array.Sort(sorted, (a, b) => a.Order.CompareTo(b.Order));
            var decisions = new TacticalThirdBatchDecision[sorted.Length];
            for (int i = 0; i < sorted.Length; i++) Assert.That(state.TryApplyNext(sorted[i], out decisions[i]), Is.True);
            Assert.That(state.RoundComplete, Is.True);
            return decisions;
        }

        [Test]
        public void Glass_OutputFirstSettlesRiskAndOutput()
        {
            var state = State(glass: true);
            var attack = Attack(Self); var source = Source(attack);
            var result = Play(state, Start(attack), TacticalThirdBatchEvent.Born(source), Hit(source, Enemy),
                Hit(OldEnemySource(), Self, 5));
            Assert.That(result[2].GlassScaleMilli, Is.EqualTo(1500));
            Assert.That(result[2].GlassConsumed, Is.True);
            Assert.That(result[3].GlassScaleMilli, Is.EqualTo(1000));
            Assert.That(state.GlassPhase, Is.EqualTo(TacticalThirdBatchGlassPhase.Settled));
        }

        [Test]
        public void Glass_RiskFirstPaysExactlyOnceAndLaterOutputRemains()
        {
            var state = State(glass: true);
            var attack = Attack(Self, 6); var source = Source(attack, 7);
            var result = Play(state, Hit(OldEnemySource(), Self, 2), Hit(OldEnemySource(), Self, 3, occurrence: 2),
                Start(attack), TacticalThirdBatchEvent.Born(source), Hit(source, Enemy, 8), Hit(OldEnemySource(), Self, 9, occurrence: 3));
            Assert.That(result[0].GlassScaleMilli, Is.EqualTo(1500));
            Assert.That(result[1].GlassScaleMilli, Is.EqualTo(1000));
            Assert.That(result[4].GlassScaleMilli, Is.EqualTo(1500));
            Assert.That(result[5].GlassScaleMilli, Is.EqualTo(1000));
            Assert.That(state.GlassPhase, Is.EqualTo(TacticalThirdBatchGlassPhase.Settled));
        }

        [Test]
        public void Glass_RiskPaidPhaseIsVisibleBeforeOutput()
        {
            var state = State(glass: true);
            Play(state, Hit(OldEnemySource(), Self, 2));
            Assert.That(state.GlassPhase, Is.EqualTo(TacticalThirdBatchGlassPhase.RiskPaidOutputPending));
        }

        [Test]
        public void Glass_WhiffAndCancelDoNotClearAndNextRealExecutionCanPayOutput()
        {
            var state = State(glass: true);
            Play(state, EndHost(2));
            Assert.That(state.GlassPhase, Is.EqualTo(TacticalThirdBatchGlassPhase.OutputAndRiskPending));
            var attack = Attack(Self, 2, 102, 20); var source = Source(attack, 3, frame: 20);
            var result = Play(state, Start(attack), TacticalThirdBatchEvent.Born(source), Hit(source, Enemy, frame: 20));
            Assert.That(result[2].GlassScaleMilli, Is.EqualTo(1500));
        }

        [Test]
        public void Glass_OldSelfProjectileNeverSupplementsOutput()
        {
            var state = State(glass: true);
            var source = Source(Attack(Self, 1, 100, 0), 2, origin: TacticalThirdBatchOrigin.DirectProjectile, frame: 0);
            var result = Play(state, Hit(source, Enemy, 2));
            Assert.That(result[0].GlassScaleMilli, Is.EqualTo(1000));
            Assert.That(state.GlassPhase, Is.EqualTo(TacticalThirdBatchGlassPhase.OutputAndRiskPending));
        }

        [Test]
        public void Glass_ArmorAllAndNonLethalClippingDoNotChangeAcceptedDamageConsumption()
        {
            var state = State(glass: true); var attack = Attack(Self); var source = Source(attack);
            var decisions = Play(state, Start(attack), TacticalThirdBatchEvent.Born(source), Hit(source, Enemy));
            Assert.That(TacticalAccessoryThirdBatchRules.TryScale(10, decisions[2].GlassScaleMilli, 1000, out int damage), Is.True);
            Assert.That(damage, Is.EqualTo(15));
            Assert.That(Math.Max(0, damage - 15), Is.EqualTo(0)); // Fully absorbed; no HP-loss qualification.
            Assert.That(Math.Min(damage, 0), Is.EqualTo(0)); // Receiver already at nonlethal floor.
            Assert.That(state.GlassPhase, Is.EqualTo(TacticalThirdBatchGlassPhase.Settled));
        }

        [TestCase(HitResolveKind.Blocked)]
        [TestCase(HitResolveKind.Parried)]
        [TestCase(HitResolveKind.Evaded)]
        [TestCase(HitResolveKind.Invincible)]
        [TestCase(HitResolveKind.Duplicate)]
        [TestCase(HitResolveKind.TargetDead)]
        [TestCase(HitResolveKind.SelfDamage)]
        [TestCase(HitResolveKind.Friendly)]
        public void Glass_DefenseAndRejectedRootsDoNotConsume(HitResolveKind outcome)
        {
            var state = State(glass: true); var attack = Attack(Self); var source = Source(attack);
            var result = Play(state, Start(attack), TacticalThirdBatchEvent.Born(source), Hit(source, Enemy, outcome: outcome));
            Assert.That(result[2].GlassScaleMilli, Is.EqualTo(1000));
            Assert.That(state.GlassPhase, Is.EqualTo(TacticalThirdBatchGlassPhase.OutputAndRiskPending));
        }

        [TestCase(TacticalThirdBatchOrigin.Unknown)]
        [TestCase(TacticalThirdBatchOrigin.Derived)]
        [TestCase(TacticalThirdBatchOrigin.Periodic)]
        [TestCase(TacticalThirdBatchOrigin.Area)]
        public void Glass_NonDirectSourcesDoNotConsume(TacticalThirdBatchOrigin origin)
        {
            var state = State(glass: true); var attack = Attack(Self); var source = Source(attack, origin: origin);
            var result = Play(state, Start(attack), TacticalThirdBatchEvent.Born(source), Hit(source, Enemy));
            Assert.That(result[2].GlassScaleMilli, Is.EqualTo(1000));
            Assert.That(state.GlassPhase, Is.EqualTo(TacticalThirdBatchGlassPhase.OutputAndRiskPending));
        }

        [TestCase(true, true, false)]
        [TestCase(false, true, false)]
        [TestCase(true, false, false)]
        public void Glass_SuppressionDeathAndAllyClassificationDoNotConsume(bool suppressed, bool alive, bool enemy)
        {
            var state = State(glass: true); var attack = Attack(Self); var source = Source(attack);
            Play(state, Start(attack), TacticalThirdBatchEvent.Born(source), Hit(source, Enemy, suppressed: suppressed, alive: alive, enemy: enemy));
            Assert.That(state.GlassPhase, Is.EqualTo(TacticalThirdBatchGlassPhase.OutputAndRiskPending));
        }

        [Test]
        public void Glass_OnePunchSuppressedRootLeavesFirstDamagePromise()
        {
            var state = State(glass: true); var attack = Attack(Self); var source = Source(attack);
            var result = Play(state, Start(attack), TacticalThirdBatchEvent.Born(source),
                Hit(source, Enemy, suppressed: true), Hit(source, Other, 5));
            Assert.That(result[2].GlassConsumed, Is.False);
            Assert.That(result[3].GlassScaleMilli, Is.EqualTo(1500));
        }

        [Test]
        public void Glass_SelfDamageNeverPaysRiskOrOutput()
        {
            var state = State(glass: true); var attack = Attack(Self); var source = Source(attack);
            Play(state, Start(attack), TacticalThirdBatchEvent.Born(source), Hit(source, Self));
            Assert.That(state.GlassPhase, Is.EqualTo(TacticalThirdBatchGlassPhase.OutputAndRiskPending));
        }

        [Test]
        public void Lifetime_ActualNextFullTurnPostSimulationExpiresGlassAndCoin()
        {
            var state = State(glass: true, face: TacticalThirdBatchCoinFace.EnemyNextAttack);
            Play(state, Settle(1, round: 0));
            Assert.That(state.GlassPhase, Is.EqualTo(TacticalThirdBatchGlassPhase.OutputAndRiskPending));
            Assert.That(state.CoinPhase, Is.EqualTo(TacticalThirdBatchCoinPhase.WaitingEnemy));
            Assert.That(state.TryOpenRound(10, 1, Array.Empty<TacticalThirdBatchOrder>(), 0), Is.False);
            Play(state, Settle(2, 500, 0));
            Assert.That(state.GlassPhase, Is.EqualTo(TacticalThirdBatchGlassPhase.Settled));
            Assert.That(state.CoinPhase, Is.EqualTo(TacticalThirdBatchCoinPhase.Ended));
        }

        [Test]
        public void Lifetime_FinalDamageAndCausalChildBeforeBoundaryRemainEligible()
        {
            var state = State(glass: true);
            Play(state, ArrayEvent(10000));
            Assert.That(state.GlassPhase, Is.EqualTo(TacticalThirdBatchGlassPhase.OutputAndRiskPending));
            var decisions = Play(state, Hit(OldEnemySource(), Self, 1, frame: 10000, round: 1));
            Assert.That(decisions[0].GlassScaleMilli, Is.EqualTo(1500));
            Play(state, Settle(2, 10000, 2));
            Assert.That(state.GlassPhase, Is.EqualTo(TacticalThirdBatchGlassPhase.Settled));
        }
        private static TacticalThirdBatchEvent ArrayEvent(long frame)
            => TacticalThirdBatchEvent.EndHost(Order(2, Other, 90, frame), Other, 999);

        [TestCase(false)]
        [TestCase(true)]
        public void Lifetime_DeathDepartureAndGenerationChangeNeverTransferState(bool battleEnd)
        {
            var state = State(glass: true, throne: true); var attack = Attack(Self);
            Play(state, Start(attack), battleEnd ? TacticalThirdBatchEvent.EndBattle(Order(3, Self))
                : TacticalThirdBatchEvent.Depart(Order(3, Self), Self));
            Assert.That(state.IronThroneActive, Is.False);
            Assert.That(state.GlassPhase, Is.EqualTo(TacticalThirdBatchGlassPhase.Settled));
            var replacement = new GameplayEntityId(Self.Index, Self.Generation + 1);
            var source = Source(Attack(replacement, 2, 102, 20), 3, frame: 20);
            var result = Play(state, Start(source.Attack), TacticalThirdBatchEvent.Born(source), Hit(source, Enemy, frame: 20));
            Assert.That(result[2].GlassScaleMilli, Is.EqualTo(1000));
        }

        [Test]
        public void Coin_StagePrivateRngDrawsOnceAndPublishesActualAfterState()
        {
            var ledger = new TacticalThirdBatchCoinLedger(7, 1);
            var expected = new DeterministicRandom(1);
            int face = expected.NextInt(0, 2);
            Assert.That(ledger.TryPublish(true, Commit(), Enemy, out var receipt), Is.True);
            Assert.That(receipt.Face, Is.EqualTo(face == 0 ? TacticalThirdBatchCoinFace.SelfExecution : TacticalThirdBatchCoinFace.EnemyNextAttack));
            Assert.That(receipt.AfterRandom.State, Is.EqualTo(expected.CaptureState().State));
            Assert.That(receipt.AfterRandom.AlgorithmId, Is.EqualTo(DeterministicRandom.XorShift32AlgorithmId));
            Assert.That(receipt.DrawCount, Is.EqualTo(1));
            Assert.That(ledger.RandomState.State, Is.EqualTo(receipt.AfterRandom.State));
            Assert.That(ledger.ReceiptCount, Is.EqualTo(1));
        }

        [Test]
        public void Coin_PreviewAndRejectedAdmissionNeverDrawOrPublish()
        {
            var ledger = new TacticalThirdBatchCoinLedger(7, 8192);
            for (int i = 0; i < 20; i++) Assert.That(ledger.CanPublish(Commit(), Enemy), Is.True);
            Assert.That(ledger.TryPublish(false, Commit(), Enemy, out var receipt), Is.False);
            Assert.That(receipt.IsEmpty, Is.True);
            Assert.That(ledger.DrawCount, Is.Zero);
            Assert.That(ledger.ReceiptCount, Is.Zero);
        }

        [TestCase(0)]
        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        [TestCase(4)]
        [TestCase(5)]
        [TestCase(6)]
        [TestCase(7)]
        public void Coin_AlteredFrozenCommitOrEnemyCannotReroll(int field)
        {
            var ledger = new TacticalThirdBatchCoinLedger(7, 8192);
            Assert.That(ledger.TryPublish(true, Commit(), Enemy, out var original), Is.True);
            var changed = Commit(execution: field == 0 ? 102ul : 101ul, stage: field == 1 ? 8ul : 7ul,
                turn: field == 2 ? 2ul : 1ul, actor: field == 3 ? Other : Self,
                card: field == 4 ? 1002ul : 1001ul, activation: field == 5 ? 2ul : 1ul, frame: field == 6 ? 11 : 10);
            Assert.That(ledger.TryPublish(true, changed, field == 7 ? Other : Enemy, out _), Is.False);
            Assert.That(ledger.TryPublish(true, Commit(), Enemy, out var repeat), Is.True);
            Assert.That(repeat.Face, Is.EqualTo(original.Face));
            Assert.That(repeat.AfterRandom.State, Is.EqualTo(original.AfterRandom.State));
            Assert.That(ledger.DrawCount, Is.EqualTo(1));
        }

        [Test]
        public void Coin_InvalidTargetAndStageFailBeforeDrawing()
        {
            var ledger = new TacticalThirdBatchCoinLedger(7, 1);
            Assert.That(ledger.TryPublish(true, Commit(), Self, out _), Is.False);
            Assert.That(ledger.TryPublish(true, Commit(), default, out _), Is.False);
            Assert.That(ledger.TryPublish(true, Commit(stage: 8), Enemy, out _), Is.False);
            Assert.That(ledger.TryPublish(true, Commit(turn: ulong.MaxValue), Enemy, out _), Is.False);
            Assert.That(ledger.DrawCount, Is.Zero);
        }

        [Test]
        public void Coin_SameSeedAndLegalSequenceMatchIndependentlyOfDeckRng()
        {
            var a = new TacticalThirdBatchCoinLedger(7, 1234); var b = new TacticalThirdBatchCoinLedger(7, 1234);
            var deck = new DeterministicRandom(1234);
            for (ulong i = 1; i <= 30; i++)
            {
                deck.NextInt(0, 99);
                Assert.That(a.TryPublish(true, Commit(i), Enemy, out var ar), Is.True);
                Assert.That(b.TryPublish(true, Commit(i), Enemy, out var br), Is.True);
                Assert.That(ar.Face, Is.EqualTo(br.Face));
                Assert.That(ar.AfterRandom.State, Is.EqualTo(br.AfterRandom.State));
                Assert.That(ar.DrawCount, Is.EqualTo((long)i));
            }
        }

        [Test]
        public void Coin_FiftyFiftyUsesExactTwoBucketsAndBothOutcomesAreVisible()
        {
            Assert.That(Coin(TacticalThirdBatchCoinFace.SelfExecution).Face, Is.EqualTo(TacticalThirdBatchCoinFace.SelfExecution));
            Assert.That(Coin(TacticalThirdBatchCoinFace.EnemyNextAttack).Face, Is.EqualTo(TacticalThirdBatchCoinFace.EnemyNextAttack));
        }

        [Test]
        public void Coin_SelfAllDirectSegmentsOfThisCommitGainBonus()
        {
            var state = State(face: TacticalThirdBatchCoinFace.SelfExecution); var attack = Attack(Self);
            var weapon = Source(attack); var projectile = Source(attack, 4, 12, TacticalThirdBatchOrigin.DirectProjectile);
            var result = Play(state, Start(attack), TacticalThirdBatchEvent.Born(weapon), TacticalThirdBatchEvent.Born(projectile),
                Hit(weapon, Enemy, 5), Hit(projectile, Other, 6), Hit(weapon, Other, 7));
            Assert.That(result[3].CoinScaleMilli, Is.EqualTo(1500));
            Assert.That(result[4].CoinScaleMilli, Is.EqualTo(1500));
            Assert.That(result[5].CoinScaleMilli, Is.EqualTo(1500));
        }

        [Test]
        public void Coin_SelfResultDoesNotMoveToNextCommit()
        {
            var state = State(face: TacticalThirdBatchCoinFace.SelfExecution);
            var attack = Attack(Self, execution: 102); var source = Source(attack);
            var result = Play(state, Start(attack), TacticalThirdBatchEvent.Born(source), Hit(source, Enemy));
            Assert.That(result[2].CoinScaleMilli, Is.EqualTo(1000));
        }

        [Test]
        public void Coin_EnemyAlreadyExecutingIsNotRetroactivelyBound()
        {
            var state = State(face: TacticalThirdBatchCoinFace.EnemyNextAttack);
            var result = Play(state, Hit(OldEnemySource(), Self, 2));
            Assert.That(result[0].CoinScaleMilli, Is.EqualTo(1000));
            Assert.That(state.CoinPhase, Is.EqualTo(TacticalThirdBatchCoinPhase.WaitingEnemy));
        }

        [Test]
        public void Coin_EnemyBindsFirstAcceptedEligibleAttackBeforeFirstHit()
        {
            var state = State(face: TacticalThirdBatchCoinFace.EnemyNextAttack);
            var nonAttack = Attack(Enemy, 2, 201); var chosen = Attack(Enemy, 3, 202, instance: 2); var later = Attack(Enemy, 4, 203, instance: 3);
            var source = Source(chosen, 5);
            var result = Play(state, Start(nonAttack, false, true), Start(chosen, true, true),
                Start(later, true, true), TacticalThirdBatchEvent.Born(source), Hit(source, Other, 6));
            Assert.That(result[0].CoinBound, Is.False);
            Assert.That(result[1].CoinBound, Is.True);
            Assert.That(result[2].CoinBound, Is.False);
            Assert.That(state.BoundEnemyAttack.Matches(chosen), Is.True);
            Assert.That(result[4].CoinScaleMilli, Is.EqualTo(1500));
        }

        [Test]
        public void Coin_UnrelatedEnemyStartCannotBindSelectedTarget()
        {
            var state = State(face: TacticalThirdBatchCoinFace.EnemyNextAttack);
            Play(state, Start(Attack(Other, execution: 201), enemy: true));
            Assert.That(state.CoinPhase, Is.EqualTo(TacticalThirdBatchCoinPhase.WaitingEnemy));
        }

        [Test]
        public void Coin_BoundAttackWhiffOrCancelNeverTransfersResult()
        {
            var state = State(face: TacticalThirdBatchCoinFace.EnemyNextAttack);
            var chosen = Attack(Enemy, 2, 201); var later = Attack(Enemy, 4, 202, instance: 2); var source = Source(later, 5);
            var result = Play(state, Start(chosen, enemy: true), TacticalThirdBatchEvent.EndAttack(Order(3, Enemy), chosen),
                Start(later, enemy: true), TacticalThirdBatchEvent.Born(source), Hit(source, Self, 6));
            Assert.That(result[4].CoinScaleMilli, Is.EqualTo(1000));
            Assert.That(state.CoinPhase, Is.EqualTo(TacticalThirdBatchCoinPhase.Ended));
            Assert.That(state.BoundEnemyAttack.Matches(chosen), Is.True);
        }

        [TestCase(TacticalThirdBatchCoinFace.SelfExecution)]
        [TestCase(TacticalThirdBatchCoinFace.EnemyNextAttack)]
        public void Coin_DelayedProjectileKeepsFrozenEligibilityAfterExecutionEnds(TacticalThirdBatchCoinFace face)
        {
            var state = State(face: face); var actor = face == TacticalThirdBatchCoinFace.SelfExecution ? Self : Enemy;
            var attack = Attack(actor, execution: actor == Self ? 101ul : 201ul);
            var source = Source(attack, origin: TacticalThirdBatchOrigin.DirectProjectile);
            Play(state, Start(attack, enemy: actor == Enemy), TacticalThirdBatchEvent.Born(source),
                face == TacticalThirdBatchCoinFace.SelfExecution ? EndHost(4) : TacticalThirdBatchEvent.EndAttack(Order(4, Enemy), attack));
            var result = Play(state, Hit(source, actor == Self ? Enemy : Self, 2, frame: 20));
            Assert.That(result[0].CoinScaleMilli, Is.EqualTo(1500));
            Play(state, Settle(2, 20, 1));
            result = Play(state, Hit(source, actor == Self ? Enemy : Self, 2, frame: 30, occurrence: 2));
            Assert.That(result[0].CoinScaleMilli, Is.EqualTo(1000));
        }

        [Test]
        public void Coin_EnemyFaceSurvivesOriginatorCancelAndDeparture()
        {
            var state = State(face: TacticalThirdBatchCoinFace.EnemyNextAttack);
            Play(state, EndHost(2), TacticalThirdBatchEvent.Depart(Order(3, Self), Self));
            var attack = Attack(Enemy, 2, 201, 20); var source = Source(attack, 3, frame: 20);
            var result = Play(state, Start(attack, enemy: true), TacticalThirdBatchEvent.Born(source), Hit(source, Other, frame: 20));
            Assert.That(result[2].CoinScaleMilli, Is.EqualTo(1500));
        }

        [Test]
        public void Coin_TargetDeathRevokesExistingProjectilesAndNeverChangesTarget()
        {
            var state = State(face: TacticalThirdBatchCoinFace.EnemyNextAttack);
            var attack = Attack(Enemy, execution: 201); var source = Source(attack, origin: TacticalThirdBatchOrigin.DirectProjectile);
            var replacement = new GameplayEntityId(Enemy.Index, Enemy.Generation + 1);
            var result = Play(state, Start(attack, enemy: true), TacticalThirdBatchEvent.Born(source),
                TacticalThirdBatchEvent.Depart(Order(4, Enemy), Enemy), Hit(source, Self, 5));
            Assert.That(result[3].CoinScaleMilli, Is.EqualTo(1000));
            Assert.That(state.Coin.Enemy, Is.Not.EqualTo(replacement));
            Assert.That(state.CoinPhase, Is.EqualTo(TacticalThirdBatchCoinPhase.Ended));
        }

        [Test]
        public void Coin_SelfResultKeepsOtherSegmentsAfterInitiallySelectedEnemyDies()
        {
            var state = State(face: TacticalThirdBatchCoinFace.SelfExecution); var attack = Attack(Self); var source = Source(attack);
            var result = Play(state, Start(attack), TacticalThirdBatchEvent.Born(source),
                TacticalThirdBatchEvent.Depart(Order(4, Enemy), Enemy), Hit(source, Other, 5));
            Assert.That(result[3].CoinScaleMilli, Is.EqualTo(1500));
        }

        [Test]
        public void Coin_EndHostCannotCreateNewFrozenSourceButExistingSourceRemains()
        {
            var state = State(face: TacticalThirdBatchCoinFace.SelfExecution); var attack = Attack(Self);
            Play(state, Start(attack), EndHost(3));
            var source = Source(attack, 4, frame: 20); var item = TacticalThirdBatchEvent.Born(source);
            Assert.That(state.TryOpenRound(20, 0, new[] { item.Order }, 1), Is.True);
            Assert.That(state.TryApplyNext(item, out _), Is.False);
        }

        [Test]
        public void Ack_DeadActorCannotMintExecutionProof()
        {
            var state = State(); var attack = Attack(Self);
            var item = TacticalThirdBatchEvent.Start(attack.AcceptedOrder, attack, false, false, true);
            Assert.That(state.TryOpenRound(10, 0, new[] { item.Order }, 1), Is.True);
            Assert.That(state.TryApplyNext(item, out _), Is.False);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void Ack_SamePhysicalIdentityCannotBeRegisteredByChangingFrozenOrder(bool endOriginal)
        {
            var state = State(glass: true, throne: true, face: TacticalThirdBatchCoinFace.SelfExecution);
            var original = Attack(Self);
            if (endOriginal)
                Play(state, Start(original), TacticalThirdBatchEvent.EndAttack(Order(3, Self), original));
            else
                Play(state, Start(original));
            var alias = Attack(Self, 1, round: 1);
            var item = Start(alias);
            Assert.That(state.TryOpenRound(10, 1, new[] { item.Order }, 1), Is.True);
            var glassBefore = state.GlassPhase;
            var coinBefore = state.CoinPhase;
            bool ironBefore = state.IronThroneActive;
            Assert.That(state.TryApplyNext(item, out var result), Is.False,
                "Changing AcceptedOrder cannot register the same physical ACK again.");
            Assert.That(result.GlassScaleMilli, Is.EqualTo(1000));
            Assert.That(result.CoinScaleMilli, Is.EqualTo(1000));
            Assert.That(result.GlassConsumed, Is.False);
            Assert.That(result.CoinBound, Is.False);
            Assert.That(state.RoundComplete, Is.False);
            Assert.That(state.GlassPhase, Is.EqualTo(glassBefore));
            Assert.That(state.CoinPhase, Is.EqualTo(coinBefore));
            Assert.That(state.IronThroneActive, Is.EqualTo(ironBefore));
            Assert.That(state.IronThroneActive, Is.EqualTo(!endOriginal));
            Assert.That(state.SuppressesMotion(original, original, TacticalThirdBatchMotionChannel.RootTranslation),
                Is.EqualTo(!endOriginal));
            Assert.That(state.SuppressesMotion(alias, alias, TacticalThirdBatchMotionChannel.RootTranslation), Is.False);
        }

        [Test]
        public void Ack_ReusedPhysicalInstanceWithNewRevisionCanBeRegistered()
        {
            var state = State(throne: true);
            var original = Attack(Self);
            Play(state, Start(original), TacticalThirdBatchEvent.EndAttack(Order(3, Self), original));
            var next = Attack(Self, 1, round: 1, revision: 2);
            Play(state, Start(next));
            Assert.That(state.IronThroneActive, Is.True);
            Assert.That(state.SuppressesMotion(next, next, TacticalThirdBatchMotionChannel.RootTranslation), Is.True);
            Assert.That(state.SuppressesMotion(original, original, TacticalThirdBatchMotionChannel.RootTranslation), Is.False);
        }

        [Test]
        public void Root_SamePhysicalOccurrenceCannotBeReacceptedUnderAnotherFrameOrEvent()
        {
            var state = State(glass: true, face: TacticalThirdBatchCoinFace.SelfExecution);
            var attack = Attack(Self); var source = Source(attack);
            Play(state, Start(attack), TacticalThirdBatchEvent.Born(source), Hit(source, Enemy));
            var alias = Hit(source, Enemy, 8, frame: 20);
            Assert.That(state.TryOpenRound(20, 0, new[] { alias.Order }, 1), Is.True);
            Assert.That(state.TryApplyNext(alias, out var result), Is.False);
            Assert.That(result.CoinScaleMilli, Is.EqualTo(1000));
            var duplicate = Hit(source, Enemy, 8, HitResolveKind.Duplicate, frame: 20);
            Assert.That(state.TryApplyNext(duplicate, out result), Is.True);
            Assert.That(result.CoinScaleMilli, Is.EqualTo(1000));
        }

        [Test]
        public void Root_PreviouslyBlockedOccurrenceCannotChangeToDamageForGlassRisk()
        {
            var state = State(glass: true);
            Play(state, Hit(OldEnemySource(), Self, 2, HitResolveKind.Blocked));
            var changed = Hit(OldEnemySource(), Self, 2, frame: 20);
            Assert.That(state.TryOpenRound(20, 0, new[] { changed.Order }, 1), Is.True);
            Assert.That(state.TryApplyNext(changed, out _), Is.False);
            Assert.That(state.GlassPhase, Is.EqualTo(TacticalThirdBatchGlassPhase.OutputAndRiskPending));
        }

        [Test]
        public void Coin_EnemyBindingOrderIsStableAcrossCallerPermutations()
        {
            var a = State(face: TacticalThirdBatchCoinFace.EnemyNextAttack);
            var b = State(face: TacticalThirdBatchCoinFace.EnemyNextAttack);
            var first = Attack(Enemy, 2, 201); var second = Attack(Enemy, 3, 202, instance: 2);
            Play(a, Start(first, enemy: true), Start(second, enemy: true));
            Play(b, Start(second, enemy: true), Start(first, enemy: true));
            Assert.That(a.BoundEnemyAttack.Matches(first), Is.True);
            Assert.That(b.BoundEnemyAttack.Matches(first), Is.True);
        }

        [TestCase(TacticalThirdBatchOrigin.Derived)]
        [TestCase(TacticalThirdBatchOrigin.Periodic)]
        [TestCase(TacticalThirdBatchOrigin.Area)]
        [TestCase(TacticalThirdBatchOrigin.Unknown)]
        public void Coin_DerivedAndUnknownSourcesNeverInheritBonus(TacticalThirdBatchOrigin origin)
        {
            var state = State(face: TacticalThirdBatchCoinFace.SelfExecution); var attack = Attack(Self); var source = Source(attack, origin: origin);
            var result = Play(state, Start(attack), TacticalThirdBatchEvent.Born(source), Hit(source, Enemy));
            Assert.That(result[2].CoinScaleMilli, Is.EqualTo(1000));
        }

        [Test]
        public void IronThrone_RequiresActualAckAndStopsAtFinalHostTerminal()
        {
            var state = State(throne: true); var attack = Attack(Self);
            Assert.That(state.IronThroneActive, Is.False);
            Assert.That(state.SuppressesMotion(attack, attack, TacticalThirdBatchMotionChannel.OrdinaryKnockback), Is.False);
            Play(state, Start(attack, eligible: false), EndHost(4, execution: 999));
            Assert.That(state.IronThroneActive, Is.True);
            Play(state, EndHost(1, frame: 20));
            Assert.That(state.SuppressesMotion(attack, attack, TacticalThirdBatchMotionChannel.OrdinaryKnockback), Is.False);
        }

        [TestCase(TacticalThirdBatchMotionChannel.ActiveTranslation)]
        [TestCase(TacticalThirdBatchMotionChannel.RootTranslation)]
        [TestCase(TacticalThirdBatchMotionChannel.ActiveImpulse)]
        [TestCase(TacticalThirdBatchMotionChannel.ActiveVelocityOverride)]
        [TestCase(TacticalThirdBatchMotionChannel.NewJump)]
        [TestCase(TacticalThirdBatchMotionChannel.ActiveGravitySuppression)]
        [TestCase(TacticalThirdBatchMotionChannel.OrdinaryKnockback)]
        public void IronThrone_SuppressesOnlyVerifiedActiveChannels(TacticalThirdBatchMotionChannel channel)
        {
            var state = State(throne: true); var attack = Attack(Self);
            Play(state, Start(attack));
            Assert.That(state.SuppressesMotion(attack, attack, channel), Is.True);
        }

        [TestCase(TacticalThirdBatchMotionChannel.Yaw)]
        [TestCase(TacticalThirdBatchMotionChannel.WeaponTrajectory)]
        [TestCase(TacticalThirdBatchMotionChannel.SpecialExternalForce)]
        [TestCase(TacticalThirdBatchMotionChannel.Gravity)]
        [TestCase(TacticalThirdBatchMotionChannel.ExistingAirborneVelocity)]
        [TestCase(TacticalThirdBatchMotionChannel.ExternalLaunch)]
        [TestCase(TacticalThirdBatchMotionChannel.Unknown)]
        public void IronThrone_PreservesIndependentChannels(TacticalThirdBatchMotionChannel channel)
        {
            var state = State(throne: true); var attack = Attack(Self);
            Play(state, Start(attack));
            Assert.That(state.SuppressesMotion(attack, attack, channel), Is.False);
        }

        [Test]
        public void IronThrone_RejectsNormalKnockbackButKeepsBlackHoleSpecialForce()
        {
            var state = State(throne: true); var own = Attack(Self); var enemy = Attack(Enemy, 1, 201, 0);
            Play(state, Start(own));
            Assert.That(state.SuppressesMotion(own, enemy, TacticalThirdBatchMotionChannel.OrdinaryKnockback), Is.True);
            Assert.That(state.SuppressesMotion(own, enemy, TacticalThirdBatchMotionChannel.SpecialExternalForce), Is.False);
            Assert.That(state.SuppressesMotion(Attack(Other, execution: 301), enemy, TacticalThirdBatchMotionChannel.OrdinaryKnockback), Is.False);
        }

        [Test]
        public void IronThrone_SameActionTemplateNextExecutionAndGenerationAreNotSuppressed()
        {
            var state = State(throne: true); var own = Attack(Self); var next = Attack(Self, 3, 102);
            Play(state, Start(own), Start(next));
            Assert.That(state.SuppressesMotion(next, next, TacticalThirdBatchMotionChannel.RootTranslation), Is.False);
            Assert.That(state.SuppressesMotion(next, own, TacticalThirdBatchMotionChannel.OrdinaryKnockback), Is.False);
            var revised = Attack(Self, revision: 2);
            Assert.That(state.SuppressesMotion(own, revised, TacticalThirdBatchMotionChannel.RootTranslation), Is.False);
            Assert.That(state.SuppressesMotion(revised, own, TacticalThirdBatchMotionChannel.OrdinaryKnockback), Is.False);
            var replacement = new GameplayEntityId(Self.Index, 2);
            Assert.That(state.SuppressesMotion(Attack(replacement), own, TacticalThirdBatchMotionChannel.RootTranslation), Is.False);
            Assert.That(state.SuppressesMotion(Attack(replacement), own, TacticalThirdBatchMotionChannel.OrdinaryKnockback), Is.False);
        }

        [Test]
        public void IronThrone_EndedPhysicalActionDoesNotSuppressStaleRootAndLastHostEndClearsRemaining()
        {
            var state = State(throne: true); var first = Attack(Self); var second = Attack(Self, 3, instance: 2);
            Play(state, Start(first), Start(second), TacticalThirdBatchEvent.EndAttack(Order(4, Self), first));
            Assert.That(state.SuppressesMotion(second, first, TacticalThirdBatchMotionChannel.RootTranslation), Is.False);
            Assert.That(state.SuppressesMotion(second, second, TacticalThirdBatchMotionChannel.RootTranslation), Is.True);
            Play(state, EndHost(frame: 20));
            Assert.That(state.SuppressesMotion(second, second, TacticalThirdBatchMotionChannel.RootTranslation), Is.False);
        }

        [Test]
        public void IronThrone_DoesNotSuppressDamageOrConsumeGlassOnItsOwn()
        {
            var state = State(throne: true); var own = Attack(Self);
            var result = Play(state, Start(own), Hit(OldEnemySource(), Self, 3));
            Assert.That(result[1].GlassScaleMilli, Is.EqualTo(1000));
            Assert.That(result[1].CoinScaleMilli, Is.EqualTo(1000));
            Assert.That(state.IronThroneActive, Is.True);
        }

        [Test]
        public void IronThrone_LastPhysicalTerminalEndsImmunityEvenBeforeHostEndNotification()
        {
            var state = State(throne: true); var own = Attack(Self);
            Play(state, Start(own), TacticalThirdBatchEvent.EndAttack(Order(3, Self), own));
            Assert.That(state.IronThroneActive, Is.False);
            Assert.That(state.SuppressesMotion(own, own, TacticalThirdBatchMotionChannel.OrdinaryKnockback), Is.False);
        }

        [Test]
        public void Order_SameFrameGlassRaceUsesProducerOrderRegardlessOfCallerArrayOrder()
        {
            var a = State(glass: true); var b = State(glass: true); var attack = Attack(Self); var source = Source(attack);
            var hit = Hit(source, Enemy, 5); var risk = Hit(OldEnemySource(), Self, 4);
            var ar = Play(a, Start(attack), TacticalThirdBatchEvent.Born(source), hit, risk);
            var br = Play(b, risk, hit, TacticalThirdBatchEvent.Born(source), Start(attack));
            for (int i = 0; i < ar.Length; i++) Assert.That(ar[i].GlassScaleMilli, Is.EqualTo(br[i].GlassScaleMilli));
            Assert.That(ar[2].GlassScaleMilli, Is.EqualTo(1500)); // risk order=4
            Assert.That(ar[3].GlassScaleMilli, Is.EqualTo(1500)); // output order=5
        }

        [Test]
        public void Order_OutOfOrderApplyAndDuplicateApplyCannotConsumeOrEmitAnotherDamageFactor()
        {
            var state = State(glass: true); var first = Hit(OldEnemySource(), Self, 2); var second = Hit(OldEnemySource(), Self, 3, occurrence: 2);
            Assert.That(state.TryOpenRound(10, 0, new[] { second.Order, first.Order }, 2), Is.True);
            Assert.That(state.TryApplyNext(second, out _), Is.False);
            Assert.That(state.GlassPhase, Is.EqualTo(TacticalThirdBatchGlassPhase.OutputAndRiskPending));
            Assert.That(state.TryApplyNext(first, out var decision), Is.True);
            Assert.That(decision.GlassConsumed, Is.True);
            Assert.That(state.TryApplyNext(first, out decision), Is.False);
            Assert.That(decision.GlassConsumed, Is.False);
            Assert.That(state.TryApplyNext(second, out decision), Is.True);
            Assert.That(decision.GlassScaleMilli, Is.EqualTo(1000));
        }

        [Test]
        public void Order_DuplicateScheduleAndIncompleteRoundFailWithoutPartialState()
        {
            var state = State(glass: true); var item = Hit(OldEnemySource(), Self, 2);
            Assert.That(state.TryOpenRound(10, 0, new[] { item.Order, item.Order }, 2), Is.False);
            Assert.That(state.TryOpenRound(10, 0, new[] { item.Order }, 1), Is.True);
            Assert.That(state.TryOpenRound(11, 0, Array.Empty<TacticalThirdBatchOrder>(), 0), Is.False);
            Assert.That(state.GlassPhase, Is.EqualTo(TacticalThirdBatchGlassPhase.OutputAndRiskPending));
        }

        [Test]
        public void Order_ActivationWatermarkRejectsPreCommitAndAlreadyRunningAttackRegistration()
        {
            var state = State();
            Assert.That(state.TryOpenRound(10, 0, new[] { Order(1, Self, 1) }, 1), Is.False);
            var old = Attack(Enemy, 1, 201, 0);
            var forgedLateStart = TacticalThirdBatchEvent.Start(Order(2, Enemy, 10), old, true, true, true);
            Assert.That(state.TryOpenRound(10, 0, new[] { forgedLateStart.Order }, 1), Is.True);
            Assert.That(state.TryApplyNext(forgedLateStart, out _), Is.False);
        }

        [Test]
        public void Order_CausalRoundsAreContiguousAndNewFrameStartsAtZero()
        {
            var state = State();
            Assert.That(state.TryOpenRound(10, 0, Array.Empty<TacticalThirdBatchOrder>(), 0), Is.True);
            Assert.That(state.TryOpenRound(10, 2, Array.Empty<TacticalThirdBatchOrder>(), 0), Is.False);
            Assert.That(state.TryOpenRound(10, 1, Array.Empty<TacticalThirdBatchOrder>(), 0), Is.True);
            Assert.That(state.TryOpenRound(20, 1, Array.Empty<TacticalThirdBatchOrder>(), 0), Is.False);
            Assert.That(state.TryOpenRound(20, 0, Array.Empty<TacticalThirdBatchOrder>(), 0), Is.True);
            Assert.That(state.TryOpenRound(10, 2, Array.Empty<TacticalThirdBatchOrder>(), 0), Is.False);
        }

        [Test]
        public void Order_FullTurnSettlementMustFollowAllDamageInOwnSingletonRound()
        {
            var state = State(glass: true); var settle = Settle(2, round: 0); var damage = Hit(OldEnemySource(), Self, 2);
            Assert.That(state.TryOpenRound(10, 0, new[] { settle.Order, damage.Order }, 2), Is.True);
            Assert.That(state.TryApplyNext(settle, out _), Is.False);
            Assert.That(state.GlassPhase, Is.EqualTo(TacticalThirdBatchGlassPhase.OutputAndRiskPending));
        }

        [Test]
        public void Source_NodeIdentityCannotBeBorrowedByChangedRevisionOrOrigin()
        {
            var state = State(glass: true); var attack = Attack(Self); var source = Source(attack);
            Play(state, Start(attack), TacticalThirdBatchEvent.Born(source));
            var changed = Source(Attack(Self, revision: 2));
            var item = Hit(changed, Enemy, frame: 20);
            Assert.That(state.TryOpenRound(20, 0, new[] { item.Order }, 1), Is.True);
            Assert.That(state.TryApplyNext(item, out _), Is.False);
            Assert.That(state.GlassPhase, Is.EqualTo(TacticalThirdBatchGlassPhase.OutputAndRiskPending));
        }

        [Test]
        public void Source_CannotReclassifyOldOrDuplicateBirthForNewBonus()
        {
            var state = State(face: TacticalThirdBatchCoinFace.SelfExecution); var attack = Attack(Self); var source = Source(attack);
            Play(state, Start(attack), TacticalThirdBatchEvent.Born(source));
            var forged = Source(attack, 2, origin: TacticalThirdBatchOrigin.DirectProjectile, frame: 20);
            var item = TacticalThirdBatchEvent.Born(forged);
            Assert.That(state.TryOpenRound(20, 0, new[] { item.Order }, 1), Is.True);
            Assert.That(state.TryApplyNext(item, out _), Is.False);
        }

        [Test]
        public void Clone_CoinReceiptAndRngContinuationNeverRerollExistingCommit()
        {
            var ledger = new TacticalThirdBatchCoinLedger(7, 8192);
            Assert.That(ledger.TryPublish(true, Commit(), Enemy, out var original), Is.True);
            var copy = ledger.CloneForTrustedPause();
            Assert.That(copy.TryPublish(true, Commit(), Enemy, out var repeat), Is.True);
            Assert.That(repeat.Face, Is.EqualTo(original.Face));
            Assert.That(copy.DrawCount, Is.EqualTo(1));
            Assert.That(copy.TryPublish(true, Commit(2), Enemy, out var next), Is.True);
            Assert.That(ledger.DrawCount, Is.EqualTo(1));
            Assert.That(ledger.TryPublish(true, Commit(2), Enemy, out var actualNext), Is.True);
            Assert.That(next.Face, Is.EqualTo(actualNext.Face));
            Assert.That(next.AfterRandom.State, Is.EqualTo(actualNext.AfterRandom.State));
        }

        [Test]
        public void Clone_PartialRoundAndConsumptionAreIndependentForAtomicOuterCommit()
        {
            var state = State(glass: true); var item = Hit(OldEnemySource(), Self, 2);
            Assert.That(state.TryOpenRound(10, 0, new[] { item.Order }, 1), Is.True);
            var candidate = state.CloneForTrustedPause();
            Assert.That(candidate.TryApplyNext(item, out var decision), Is.True);
            Assert.That(decision.GlassScaleMilli, Is.EqualTo(1500));
            Assert.That(candidate.GlassPhase, Is.EqualTo(TacticalThirdBatchGlassPhase.RiskPaidOutputPending));
            Assert.That(state.GlassPhase, Is.EqualTo(TacticalThirdBatchGlassPhase.OutputAndRiskPending));
            Assert.That(state.RoundComplete, Is.False);
            Assert.That(state.TryApplyNext(item, out decision), Is.True);
            Assert.That(decision.GlassScaleMilli, Is.EqualTo(1500));
        }

        [Test]
        public void Clone_FrozenProjectileAndTerminalMotorStateArePreservedWithoutAliasing()
        {
            var state = State(throne: true, face: TacticalThirdBatchCoinFace.SelfExecution);
            var attack = Attack(Self); var source = Source(attack, origin: TacticalThirdBatchOrigin.DirectProjectile);
            Play(state, Start(attack), TacticalThirdBatchEvent.Born(source), EndHost(4));
            var copy = state.CloneForTrustedPause();
            var result = Play(copy, Hit(source, Enemy, frame: 20));
            Assert.That(result[0].CoinScaleMilli, Is.EqualTo(1500));
            Assert.That(copy.IronThroneActive, Is.False);
            Play(copy, Settle(2, 20, 1));
            result = Play(state, Hit(source, Enemy, frame: 20));
            Assert.That(result[0].CoinScaleMilli, Is.EqualTo(1500));
        }

        [Test]
        public void Factors_GlassAndCoinComposeOnceBeforeArmorWithSingleTruncation()
        {
            var state = State(glass: true, face: TacticalThirdBatchCoinFace.SelfExecution);
            var attack = Attack(Self); var source = Source(attack);
            var result = Play(state, Start(attack), TacticalThirdBatchEvent.Born(source), Hit(source, Enemy));
            Assert.That(TacticalAccessoryThirdBatchRules.TryScale(3, result[2].GlassScaleMilli, result[2].CoinScaleMilli, out int damage), Is.True);
            Assert.That(damage, Is.EqualTo(6));
        }

        [TestCase(0, 1500, 1500, 0)]
        [TestCase(1, 1500, 1500, 2)]
        [TestCase(3, 1500, 1500, 6)]
        [TestCase(10, 1000, 1500, 15)]
        [TestCase(int.MaxValue, 1500, 1500, int.MaxValue)]
        [TestCase(int.MaxValue, int.MaxValue, int.MaxValue, int.MaxValue)]
        public void Factors_BoundariesSaturateAndTruncateOnlyAtEnd(int input, int glass, int coin, int expected)
        {
            Assert.That(TacticalAccessoryThirdBatchRules.TryScale(input, glass, coin, out int result), Is.True);
            Assert.That(result, Is.EqualTo(expected));
        }

        [TestCase(-1, 1500, 1500)]
        [TestCase(1, 999, 1500)]
        [TestCase(1, 1500, 999)]
        public void Factors_InvalidInputsDoNotPublishDamage(int input, int glass, int coin)
            => Assert.That(TacticalAccessoryThirdBatchRules.TryScale(input, glass, coin, out _), Is.False);
    }
}
