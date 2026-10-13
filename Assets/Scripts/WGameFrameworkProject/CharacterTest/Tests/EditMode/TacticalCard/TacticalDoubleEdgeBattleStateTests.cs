using MxFramework.Combat.Damage;
using MxFramework.Combat.Hit;
using MxFramework.Gameplay;
using MxFramework.Runtime;
using NUnit.Framework;

namespace MxFramework.Game.CharacterTest.TacticalCard
{
    public sealed class TacticalDoubleEdgeBattleStateTests
    {
        private static readonly GameplayEntityId Actor = new GameplayEntityId(1, 3), Enemy = new GameplayEntityId(2, 4);
        private static TacticalDoubleEdgeCommitKey Key(ulong id = 1, GameplayEntityId? actor = null, ulong card = 0)
            => new TacticalDoubleEdgeCommitKey(7, id, id, actor ?? Actor, card == 0 ? id : card, 100);
        private static TacticalDoubleEdgeParameters Parameters(bool duel = true, bool shattered = true, bool field = true)
            => new TacticalDoubleEdgeParameters(duel ? 1500 : 1000, shattered ? 2000 : 1000, field,
                new TacticalBlackHolePullParameters(3000, 250, 3000, 80000), 6000, 2000);
        private static TacticalDoubleEdgeBattleState State()
        { var state = new TacticalDoubleEdgeBattleState(); state.Bind(7, 2); return state; }
        private static TacticalDoubleEdgeDamageFacts Hit(ulong turn = 2, ulong revision = 1, long frame = 101,
            GameplayEntityId? actor = null, GameplayEntityId? target = null)
            => new TacticalDoubleEdgeDamageFacts(7, turn, frame, actor ?? Actor, target ?? Enemy, true, true,
                HitResolveKind.Damage, TacticalAccessoryDamageOrigin.DirectAttack, DamageProfileSourceKind.WeaponTrace, 22, revision);
        private static void Publish(TacticalDoubleEdgeBattleState state, ulong id = 1, TacticalDoubleEdgeParameters? parameters = null,
            GameplayEntityId? actor = null, GameplayEntityId? enemy = null)
        {
            Assert.IsTrue(state.TryPrepare(Key(id, actor), 2, enemy ?? Enemy, parameters ?? Parameters(), true, true, out var prepared));
            Assert.IsTrue(prepared.IsCurrent); Assert.IsTrue(prepared.TryPublish()); Assert.IsFalse(prepared.IsCurrent);
        }
        private static TacticalBlackHoleAcceleration Pull(TacticalDoubleEdgeBattleState state, long frame = 103,
            int x = 1000, int velocityX = 0, int velocityZ = 0)
        {
            Assert.IsTrue(state.TryPull(frame, x, 0, 0, 80000, 1000, true, velocityX, velocityZ, 60, out var force)); return force;
        }
        private static long Hash(TacticalDoubleEdgeBattleState state)
        { var hash = new RuntimeHashAccumulator(1, 1); state.AddHash(ref hash); return hash.ToHash(); }

        [TestCase(0)][TestCase(1)][TestCase(2)]
        public void SharedAckAndBirthSourcesDoNotRequireShatteredFantasy(int kind)
        {
            var state = State(); Publish(state, parameters: Parameters(kind == 0, kind == 1, kind == 2));
            Assert.IsTrue(state.AcknowledgeAction(Key(), 22, 1, 101, out var source));
            Assert.IsTrue(state.FindSource(Key(), 22, 1).IsValid);
            Assert.IsTrue(state.BindTrace(99, source)); Assert.IsTrue(state.BindProjectile(12, 0, source));
            Assert.IsTrue(state.ProjectileSource(12, 0).IsValid);
            Assert.AreEqual(kind == 0 ? 3 : kind == 1 ? 4 : 2, state.ScaleDamage(2, Hit(), source));
        }

        [TestCase("stage")][TestCase("turn")][TestCase("commit")][TestCase("execution")]
        [TestCase("actor-index")][TestCase("actor-generation")][TestCase("card")][TestCase("frame")]
        [TestCase("target-index")][TestCase("target-generation")][TestCase("duel-scale")][TestCase("shattered-scale")]
        [TestCase("field-enabled")][TestCase("radius")][TestCase("stop-radius")][TestCase("acceleration")]
        [TestCase("mass")][TestCase("acceleration-cap")][TestCase("speed-cap")]
        public void EachFrozenIdentityAndParameterChangesTheStageHash(string field)
        {
            var baseline = State(); Publish(baseline);
            ulong stage = field == "stage" ? 8UL : 7UL, turn = field == "turn" ? 3UL : 2UL;
            var state = new TacticalDoubleEdgeBattleState(); state.Bind(stage, turn);
            var key = new TacticalDoubleEdgeCommitKey(stage, field == "commit" ? 2UL : 1UL,
                field == "execution" ? 2UL : 1UL, new GameplayEntityId(field == "actor-index" ? 3 : 1,
                    field == "actor-generation" ? 4 : 3), field == "card" ? 2UL : 1UL, field == "frame" ? 101 : 100);
            var p = new TacticalDoubleEdgeParameters(field == "duel-scale" ? 1501 : 1500,
                field == "shattered-scale" ? 2001 : 2000, field != "field-enabled",
                new TacticalBlackHolePullParameters(field == "radius" ? 3001 : 3000, field == "stop-radius" ? 251 : 250,
                    field == "acceleration" ? 3001 : 3000, field == "mass" ? 80001 : 80000),
                field == "acceleration-cap" ? 6001 : 6000, field == "speed-cap" ? 2001 : 2000);
            Assert.IsTrue(state.TryPrepare(key, turn, new GameplayEntityId(field == "target-index" ? 4 : 2,
                field == "target-generation" ? 5 : 4), p, true, true, out var prepared)); Assert.IsTrue(prepared.TryPublish());
            Assert.AreNotEqual(Hash(baseline), Hash(state), field);
        }

        [TestCase("ack")][TestCase("projectile")][TestCase("terminal")][TestCase("natural")][TestCase("exhaust")]
        [TestCase("revoke")][TestCase("turn")][TestCase("terminal-frame")][TestCase("x")][TestCase("y")][TestCase("z")]
        public void EachLiveMutationChangesOnlyTheTrustedClone(string field)
        {
            var state = State(); Publish(state); state.AcknowledgeAction(Key(), 22, 1, 101, out var source);
            var frozen = state.CloneTrusted(); long before = Hash(state);
            string projection = Newtonsoft.Json.JsonConvert.SerializeObject(state.CaptureProjection());
            if (field == "ack") frozen.AcknowledgeAction(Key(), 22, 2, 102, out _);
            else if (field == "projectile") Assert.IsTrue(frozen.BindProjectile(1, 0, source));
            else if (field == "revoke") frozen.RevokePair(Actor);
            else if (field == "turn") frozen.AdvanceTurn(7, 3);
            else {
                bool natural = field != "terminal";
                Assert.IsTrue(frozen.CompleteHost(Key(), natural, field == "terminal-frame" ? 104 : 103,
                    field == "x" ? 1 : 0, field == "y" ? 1 : 0, field == "z" ? 1 : 0));
                if (field == "exhaust") Assert.IsTrue(frozen.MarkExhausted(Key()));
                if (field == "terminal-frame" || field == "x" || field == "y" || field == "z" || field == "exhaust") {
                    var control = state.CloneTrusted(); control.CompleteHost(Key(), true, 103, 0, 0, 0);
                    Assert.AreNotEqual(Hash(control), Hash(frozen), field);
                }
            }
            Assert.AreEqual(before, Hash(state)); Assert.AreNotEqual(before, Hash(frozen), field);
            Assert.AreEqual(projection, Newtonsoft.Json.JsonConvert.SerializeObject(state.CaptureProjection()));
        }

        [Test]
        public void ProjectileBirthCannotRebindToAnotherAckAndProjectionIsDetached()
        {
            var state = State(); Publish(state);
            state.AcknowledgeAction(Key(), 22, 1, 101, out var first); Assert.IsTrue(state.BindProjectile(1, 0, first));
            state.AcknowledgeAction(Key(), 22, 2, 102, out var second); Assert.IsFalse(state.BindProjectile(1, 0, second));
            long birthHash = Hash(state); Assert.IsTrue(state.BindTrace(1, first)); Assert.AreNotEqual(birthHash, Hash(state));
            Assert.IsFalse(state.BindTrace(1, second), "A prior trace window cannot acquire a later ACK revision.");
            Assert.IsTrue(state.BindProjectile(2, 1, second));
            state.RevokePair(Actor); state.CompleteHost(Key(), true, 103, 50, 70, 90); state.MarkExhausted(Key());
            var clone = state.CloneTrusted(); Assert.AreEqual(Hash(state), Hash(clone));
            Assert.AreEqual(1, clone.ProjectileSource(1, 0).ActionRevision);
            Assert.AreEqual(2, clone.ProjectileSource(2, 1).ActionRevision);
            Assert.IsFalse(clone.ProjectileSource(1, 1).IsValid);
            long before = Hash(clone); var projection = clone.CaptureProjection();
            foreach (var row in projection) for (int i = 0; i < row.Length; i++) row[i]++;
            Assert.AreEqual(before, Hash(clone)); Assert.AreEqual(Hash(state), Hash(clone));
            clone.Clear(); Assert.AreEqual(1, state.ProjectileSource(1, 0).ActionRevision);
        }

        [Test]
        public void PreparationDoesNotReserveOrPublishAndChangedPreparationCannotPublish()
        {
            var state = State(); var hash = Hash(state);
            Assert.IsTrue(state.TryPrepare(Key(), 2, Enemy, Parameters(), true, true, out var prepared));
            Assert.AreEqual(0, state.OccupiedFieldSlots); Assert.AreEqual(hash, Hash(state));
            Publish(state, 2); Assert.IsFalse(prepared.IsCurrent);
            Assert.IsFalse(prepared.TryPublish());
            Assert.AreEqual(1, state.EntryCount);
        }

        [Test]
        public void FourActiveOrReservedFieldsBlockFifthBeforePaymentAndFailureReleasesOnlyItsSlot()
        {
            var state = State();
            for (ulong id = 1; id <= 4; id++) Publish(state, id);
            Assert.AreEqual(4, state.OccupiedFieldSlots);
            Assert.IsFalse(state.TryPrepare(Key(5), 2, Enemy, Parameters(), true, true, out _));
            Assert.IsTrue(state.CompleteHost(Key(), false, 102, 0, 0, 0));
            Assert.AreEqual(3, state.OccupiedFieldSlots); Publish(state, 5);
            Assert.AreEqual(4, state.OccupiedFieldSlots);
        }

        [Test]
        public void AdmissionRejectsNoRealDeckInvalidEnemyWrongTurnAndRepeatedCommit()
        {
            var state = State();
            Assert.IsFalse(state.TryPrepare(Key(), 2, Enemy, Parameters(), true, false, out _));
            Assert.IsFalse(state.TryPrepare(Key(), 2, Enemy, Parameters(), false, true, out _));
            Assert.IsFalse(state.TryPrepare(Key(), 3, Enemy, Parameters(), true, true, out _));
            Assert.IsFalse(state.TryPrepare(Key(), 2, Actor, Parameters(), true, true, out _));
            Publish(state);
            Assert.IsFalse(state.TryPrepare(Key(), 2, Enemy, Parameters(), true, true, out _));
            Assert.IsFalse(state.TryPrepare(Key(2, card: 1), 2, Enemy, Parameters(), true, true, out _));
        }

        [Test]
        public void SamePairNeverStacksAndPairDeathRevocationDoesNotRemoveTheIndependentField()
        {
            var state = State(); Publish(state); Publish(state, 2);
            Assert.AreEqual(4, state.ScaleDamage(3, Hit(), default));
            Assert.IsTrue(state.AcknowledgeAction(Key(), 22, 1, 100, out var stamp));
            Assert.AreEqual(9, state.ScaleDamage(3, Hit(), stamp));
            Assert.AreEqual(4, state.ScaleDamage(3, Hit(actor: Enemy, target: Actor), stamp));
            Assert.IsTrue(state.CompleteHost(Key(), true, 102, 0, 0, 0));
            state.RevokePair(Actor);
            Assert.AreEqual(6, state.ScaleDamage(3, Hit(frame: 104), stamp));
            Assert.AreEqual(-3000, Pull(state).X);
            state.RevokePair(Actor); Assert.AreEqual(-3000, Pull(state).X);
        }

        [Test]
        public void DelayedSourcesRemainValidAfterHostAndTurnWhileForgedOrReusedRevisionsFail()
        {
            var state = State(); Publish(state);
            Assert.IsTrue(state.AcknowledgeAction(Key(), 22, 1, 101, out var stamp));
            var before = Hash(state);
            Assert.IsTrue(state.AcknowledgeAction(Key(), 22, 1, 101, out _)); Assert.AreEqual(before, Hash(state));
            Assert.IsFalse(state.AcknowledgeAction(Key(), 22, 1, 102, out _));
            var forged = new TacticalDirectAttackStamp(Key(), 22, 2, 101);
            Assert.AreEqual(4, state.ScaleDamage(3, Hit(revision: 2), forged));
            Assert.AreEqual(4, state.ScaleDamage(3, Hit(revision: 2), stamp));
            Assert.IsFalse(state.FindSource(Key(), 22, 2).IsValid);
            Assert.IsFalse(state.FindSource(Key(2), 22, 1).IsValid);
            state.CompleteHost(Key(), true, 102, 0, 0, 0); state.AdvanceTurn(7, 3);
            Assert.AreEqual(6, state.ScaleDamage(3, Hit(turn: 3, frame: 300), stamp));
            Assert.IsFalse(state.AcknowledgeAction(Key(), 23, 2, 301, out _));
        }

        [TestCase(false)] [TestCase(true)]
        public void ExhaustObligationExistsBeforeHitsAndTerminalCannotUndoIt(bool success)
        {
            var state = State(); Publish(state);
            Assert.IsTrue(state.MustExhaust(Key())); Assert.IsFalse(state.MustExhaust(Key(2)));
            Assert.IsFalse(state.MarkExhausted(Key()));
            Assert.IsTrue(state.CompleteHost(Key(), success, 102, 0, 0, 0));
            Assert.IsTrue(state.MarkExhausted(Key())); var hash = Hash(state);
            Assert.IsTrue(state.MarkExhausted(Key())); Assert.AreEqual(hash, Hash(state));
            Assert.IsFalse(state.MarkExhausted(Key(2)));
        }

        [Test]
        public void FirstWholeHostTerminalWinsAndFreezesCenterAtThatFrame()
        {
            var state = State(); Publish(state);
            Assert.AreEqual(0, Pull(state).X);
            Assert.IsTrue(state.CompleteHost(Key(), true, 102, 0, 0, 0));
            Assert.AreEqual(0, Pull(state, frame: 102).X);
            Assert.AreEqual(-3000, Pull(state).X);
            var hash = Hash(state);
            Assert.IsTrue(state.CompleteHost(Key(), true, 150, 2000, 0, 0)); Assert.AreEqual(hash, Hash(state));
            Assert.AreEqual(-3000, Pull(state, frame: 151).X);
        }

        [Test]
        public void CancellationCannotLaterBecomeNaturalSuccess()
        {
            var state = State(); Publish(state);
            state.CompleteHost(Key(), false, 102, 0, 0, 0);
            state.CompleteHost(Key(), true, 103, 0, 0, 0);
            Assert.AreEqual(0, state.OccupiedFieldSlots); Assert.AreEqual(0, Pull(state).X);
            Assert.IsTrue(state.MustExhaust(Key()));
        }

        [Test]
        public void FieldsSurviveSourceDepartureAndExpireAtActualNextTurnBoundaryWithoutResidualForce()
        {
            var state = State(); Publish(state); state.CompleteHost(Key(), true, 102, 0, 0, 0);
            state.RevokePair(Actor); state.AdvanceTurn(7, 3);
            Assert.AreEqual(-3000, Pull(state, frame: 999999).X); Assert.AreEqual(1, state.OccupiedFieldSlots);
            state.AdvanceTurn(7, 4);
            Assert.AreEqual(0, Pull(state, frame: 1000000).X); Assert.AreEqual(0, state.OccupiedFieldSlots);
            Assert.IsFalse(state.AdvanceTurn(7, 3)); Assert.IsFalse(state.AdvanceTurn(8, 4));
        }

        [Test]
        public void MultipleFieldsSumBeforeOneAccelerationAndRadialSpeedLimit()
        {
            var state = State();
            for (ulong i = 1; i <= 4; i++) { Publish(state, i); state.CompleteHost(Key(i), true, 102, 0, 0, 0); }
            Assert.AreEqual(-6000, Pull(state).X);
            Assert.AreEqual(-600, Pull(state, velocityX: -1990, velocityZ: 8000).X);
            Assert.AreEqual(0, Pull(state, velocityX: -2000).X);
        }

        [Test]
        public void UnequalOpposingContributionsAreNotIndividuallyClampedToTheGameplayCap()
        {
            var state = State();
            var strong = new TacticalDoubleEdgeParameters(1000, 1000, true,
                new TacticalBlackHolePullParameters(3000, 250, 12000, 80000), 6000, 2000);
            var weak = new TacticalDoubleEdgeParameters(1000, 1000, true,
                new TacticalBlackHolePullParameters(3000, 250, 6000, 80000), 6000, 2000);
            Publish(state, parameters: strong); Publish(state, 2, weak);
            state.CompleteHost(Key(), true, 102, 0, 0, 0); state.CompleteHost(Key(2), true, 102, 2000, 0, 0);
            Assert.AreEqual(-6000, Pull(state).X, "12000 left minus 6000 right; clamping each first would incorrectly cancel.");
        }

        [Test]
        public void OpposingFieldsCancelAndTrustedCloneRetainsStateAfterSourceClears()
        {
            var state = State(); Publish(state); Publish(state, 2);
            state.AcknowledgeAction(Key(), 22, 1, 101, out _);
            state.CompleteHost(Key(), true, 102, 0, 0, 0); state.CompleteHost(Key(2), true, 102, 2000, 0, 0);
            Assert.AreEqual(0, Pull(state).X);
            var clone = state.CloneTrusted(); Assert.AreEqual(Hash(state), Hash(clone));
            state.Clear(); Assert.AreEqual(0, state.EntryCount); Assert.AreEqual(2, clone.EntryCount);
            Assert.AreNotEqual(Hash(state), Hash(clone)); Assert.AreEqual(2, clone.OccupiedFieldSlots);
        }

        [Test]
        public void HugeUnequalFieldContributionsRetainTheirTrueResultantSign()
        {
            var state = State();
            for (ulong id = 1; id <= 3; id++) {
                var parameters = new TacticalDoubleEdgeParameters(1000, 1000, true,
                    new TacticalBlackHolePullParameters(3000, 250, id == 1 ? 1000000000 : 300000000, 80000), 6000, 2000);
                Publish(state, id, parameters); state.CompleteHost(Key(id), true, 102, id == 1 ? 0 : 2000, 0, 0);
            }
            Assert.AreEqual(-6000, Pull(state).X, "-1e9 + 3e8 + 3e8 must stay negative before the single final cap.");
        }

        [Test]
        public void ReusedInstanceRevisionsKeepOldProjectileWitnessAndRejectEarlierTerminalOrAnotherIdentity()
        {
            var state = State(); Publish(state);
            Assert.IsTrue(state.AcknowledgeAction(Key(), 22, 1, 101, out var old));
            Assert.IsTrue(state.AcknowledgeAction(Key(), 22, 2, 103, out var current));
            Assert.AreEqual(6, state.ScaleDamage(2, Hit(frame: 104), old));
            Assert.AreEqual(6, state.ScaleDamage(2, Hit(revision: 2, frame: 104), current));
            Assert.AreEqual(3, state.ScaleDamage(2, Hit(revision: 2, frame: 104), old));
            Assert.IsFalse(state.CompleteHost(Key(), true, 102, 0, 0, 0));
            Assert.IsFalse(state.CompleteHost(Key(2), true, 104, 0, 0, 0));
            Assert.IsFalse(state.CompleteHost(Key(actor: Enemy), true, 104, 0, 0, 0));
            Assert.IsFalse(state.CompleteHost(new TacticalDoubleEdgeCommitKey(8, 1, 1, Actor, 1, 100), true, 104, 0, 0, 0));
            Assert.IsFalse(state.TryPrepare(new TacticalDoubleEdgeCommitKey(7, 3, 3, Actor, 3, long.MaxValue),
                2, Enemy, Parameters(), true, true, out _));
            Assert.IsTrue(state.CompleteHost(Key(), true, 104, 0, 0, 0)); Assert.IsTrue(state.MarkExhausted(Key()));
            Assert.IsFalse(state.TryPrepare(Key(3, card: 1), 2, Enemy, Parameters(), true, true, out _));
            Assert.IsTrue(state.TryPrepare(Key(3, card: 2), 2, Enemy, Parameters(), true, true, out _));
        }
    }
}
