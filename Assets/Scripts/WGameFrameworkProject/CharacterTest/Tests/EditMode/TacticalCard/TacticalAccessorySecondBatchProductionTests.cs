using System;
using MxFramework.Combat.Hit;
using MxFramework.Combat.Core;
using MxFramework.Combat.Projectiles;
using MxFramework.Core.Math;
using MxFramework.Gameplay;
using MxFramework.Runtime;
using NUnit.Framework;

namespace MxFramework.Game.CharacterTest.TacticalCard
{
    public sealed class TacticalAccessorySecondBatchProductionTests
    {
        private static TacticalDoubleEdgeBattleState Stage()
        { var stage = new TacticalDoubleEdgeBattleState(); stage.Bind(7, 2); return stage; }
        private static TacticalDirectAttackStamp Publish(TacticalDoubleEdgeBattleState stage, int actor, bool punch = false)
        {
            var key = new TacticalDoubleEdgeCommitKey(7, (ulong)actor, (ulong)actor, new GameplayEntityId(actor, 3), (ulong)actor, 100);
            var neutral = new TacticalDoubleEdgeParameters(1000, 1000, false, default, 0, 0);
            Assert.IsTrue(stage.TryPrepare(key, 2, default, neutral,
                new TacticalSecondBatchParameters(false, true, punch, 0, punch ? 2000 : 0), false, true, out var prepared));
            Assert.IsTrue(prepared.TryPublish());
            Assert.IsTrue(stage.AcknowledgeAction(key, 22, 1, 100, out var root));
            return root;
        }
        private static TacticalSecondBatchHit Hit(TacticalSecondBatchSource source, ulong evt, long frame = 101)
            => new TacticalSecondBatchHit(source, source.CreateHitKey(frame, evt, new GameplayEntityId(9, 2), evt),
                source.Producer, HitResolveKind.Damage, true, true);
        private static long Hash(TacticalDoubleEdgeBattleState stage)
        { var hash = new RuntimeHashAccumulator(1, 1); stage.AddHash(ref hash); return hash.ToHash(); }

        [Test]
        public void SecondOnlyPaymentPublishesStageAndAcknowledgedBirth()
        {
            var stage = Stage(); var root = Publish(stage, 1);
            Assert.IsTrue(stage.HasSecondBatch); Assert.IsTrue(stage.SecondBatchRoot(root).IsValid);
            Assert.IsTrue(stage.BindTrace(81, root, 101));
            var trace = stage.SecondBatchProducer(root, 2, 81, 0);
            Assert.AreEqual(101, trace.CreatedFrame); Assert.AreEqual(stage.SecondBatchRoot(root).Node, trace.Parent);
            Assert.IsTrue(stage.CompleteHost(root.Commit, true, 101, 0, 0, 0));
            Assert.IsTrue(stage.SecondBatchProducer(root, 2, 81, 0).Matches(trace));
            var output = new TacticalSecondBatchHitDecision[1];
            Assert.IsTrue(stage.TryResolveSecondBatchRound(102, 0, new[] { Hit(trace, 1, 102) }, 1, output));
            Assert.IsTrue(output[0].NonLethalHp); Assert.IsTrue(output[0].AttemptOrdinaryInterrupt);
        }

        [Test]
        public void FailedSecondLeafLeavesFirstLeafAndOutputUnchanged()
        {
            var stage = Stage(); var first = Publish(stage, 1, true); var second = Publish(stage, 2);
            var a = Hit(stage.SecondBatchRoot(first), 10);
            var b = Hit(stage.SecondBatchRoot(second), 20, 102);
            var decisions = new[] { new TacticalSecondBatchHitDecision(true, false, 4321, false, true), default };
            long before = Hash(stage);
            Assert.IsFalse(stage.TryResolveSecondBatchRound(101, 0, new[] { a, b }, 2, decisions));
            Assert.AreEqual(before, Hash(stage)); Assert.AreEqual(4321, decisions[0].DamageScaleMilli);
            Assert.IsFalse(stage.SuppressesAttack(first));
        }

        [Test]
        public void ParentEvent100ChildEvent1CompletesSameFrameAndTrustedRestore()
        {
            var stage = Stage(); var root = Publish(stage, 1); var parent = stage.SecondBatchRoot(root);
            var hit = Hit(parent, 100); var output = new TacticalSecondBatchHitDecision[1];
            Assert.IsTrue(stage.TryResolveSecondBatchRound(101, 0, new[] { hit }, 1, output));
            Assert.IsTrue(stage.TryRegisterSecondBatchBirth(parent, 900, parent.Producer, 101, hit.Key, out var child));
            var copy = stage.CloneTrusted(); Assert.AreEqual(Hash(stage), Hash(copy));
            var derived = Hit(child, 1);
            Assert.AreEqual(1, derived.Key.CausalRound);
            Assert.IsTrue(stage.TryResolveSecondBatchRound(101, 1, new[] { derived }, 1, output));
            Assert.IsTrue(copy.TryResolveSecondBatchRound(101, 1, new[] { derived }, 1, output));
            Assert.AreEqual(Hash(stage), Hash(copy)); Assert.IsTrue(output[0].NonLethalHp);
            Assert.IsFalse(output[0].AttemptOrdinaryInterrupt);
            CollectionAssert.AreEqual(stage.CaptureProjection(), copy.CaptureProjection());
        }

        [Test]
        public void CompleteRoundSortingDoesNotUseCallerOrderOrCollidingLocalNode()
        {
            var stage = Stage(); var a = Publish(stage, 1, true); var b = Publish(stage, 2, true);
            var first = Hit(stage.SecondBatchRoot(a), 1); var later = Hit(stage.SecondBatchRoot(a), 2);
            var other = Hit(stage.SecondBatchRoot(b), 1); var copy = stage.CloneTrusted();
            var output = new TacticalSecondBatchHitDecision[3]; var reversed = new TacticalSecondBatchHitDecision[3];
            Assert.IsTrue(stage.TryResolveSecondBatchRound(101, 0, new[] { later, other, first }, 3, output));
            Assert.IsTrue(copy.TryResolveSecondBatchRound(101, 0, new[] { first, other, later }, 3, reversed));
            Assert.IsTrue(output[0].SuppressAttackJudgment); Assert.AreEqual(2000, output[1].DamageScaleMilli);
            Assert.AreEqual(2000, output[2].DamageScaleMilli); Assert.AreEqual(Hash(stage), Hash(copy));
        }

        [Test]
        public void IdleLeavesCloseEmptyRoundsWithoutInventingHitReceipts()
        {
            var stage = Stage(); Publish(stage, 1); Publish(stage, 2);
            Assert.IsTrue(stage.TryResolveSecondBatchRound(101, 0, Array.Empty<TacticalSecondBatchHit>(), 0,
                Array.Empty<TacticalSecondBatchHitDecision>()));
            long before = Hash(stage);
            Assert.IsFalse(stage.TryResolveSecondBatchRound(102, 1, Array.Empty<TacticalSecondBatchHit>(), 0,
                Array.Empty<TacticalSecondBatchHitDecision>()));
            Assert.AreEqual(before, Hash(stage));
            Assert.IsTrue(stage.TryResolveSecondBatchRound(102, 0, Array.Empty<TacticalSecondBatchHit>(), 0,
                Array.Empty<TacticalSecondBatchHitDecision>()));
        }

        [Test]
        public void HistoricalReplayAcrossIdleLeavesDoesNotInvalidatePreparedPayment()
        {
            var stage = Stage(); var first = Publish(stage, 1); Publish(stage, 2);
            var hit = Hit(stage.SecondBatchRoot(first), 100); var output = new TacticalSecondBatchHitDecision[1];
            Assert.IsTrue(stage.TryResolveSecondBatchRound(101, 0, new[] { hit }, 1, output));
            Assert.IsTrue(stage.TryResolveSecondBatchRound(102, 0, Array.Empty<TacticalSecondBatchHit>(), 0, output));
            var key = new TacticalDoubleEdgeCommitKey(7, 3, 3, new GameplayEntityId(3, 3), 3, 102);
            Assert.IsTrue(stage.TryPrepare(key, 2, default, default, new TacticalSecondBatchParameters(false, true, false, 0, 0),
                false, true, out var prepared));
            long before = Hash(stage);
            Assert.IsTrue(stage.TryResolveSecondBatchRound(101, 0, new[] { hit }, 1, output));
            Assert.IsTrue(output[0].IsReplay); Assert.AreEqual(before, Hash(stage));
            Assert.IsTrue(prepared.IsCurrent); Assert.IsTrue(prepared.TryPublish());
        }

        [Test]
        public void InvalidFirstBatchParametersCannotRideAValidSecondBatchPayment()
        {
            var stage = Stage(); var key = new TacticalDoubleEdgeCommitKey(7, 1, 1, new GameplayEntityId(1, 1), 1, 100);
            Assert.IsFalse(stage.TryPrepare(key, 2, default, new TacticalDoubleEdgeParameters(999, 1000, false, default, 0, 0),
                new TacticalSecondBatchParameters(false, true, false, 0, 0), false, true, out _));
            Assert.AreEqual(0, stage.EntryCount);
        }

        [Test]
        public void BuffRefreshRetainsOathAndNewApplicationUsesNewBirthEvenAtSameFrame()
        {
            var stage = Stage(); var root = Publish(stage, 1); var parent = stage.SecondBatchRoot(root);
            var hit = Hit(parent, 100); var decisions = new TacticalSecondBatchHitDecision[1];
            Assert.IsTrue(stage.TryResolveSecondBatchRound(101, 0, new[] { hit }, 1, decisions));
            var buff = new GameplayComponentBuffEntry(71, 1, 101, 120);
            Assert.IsTrue(stage.BindBuffApplication(hit.Key.Target, buff, false, 101, 10001, root, parent, hit.Key, true));
            var first = stage.FindBuffLineage(hit.Key.Target, buff);
            Assert.AreEqual(1, first.Binding.Source.BirthRound);
            Assert.IsFalse(stage.AdvanceTurn(7, 3), "The successor birth must close before turn settlement.");
            var refreshed = new GameplayComponentBuffEntry(71, 1, 101, 150);
            Assert.IsTrue(stage.BindBuffApplication(hit.Key.Target, refreshed, true, 101, 0, default, default));
            Assert.IsTrue(first.Binding.Source.Matches(stage.FindBuffLineage(hit.Key.Target, refreshed).Binding.Source));
            Assert.IsTrue(stage.TryResolveSecondBatchRound(101, 1, Array.Empty<TacticalSecondBatchHit>(), 0, decisions));
            var dot = Hit(first.Binding.Source, 1, 102);
            Assert.IsTrue(stage.TryResolveSecondBatchRound(102, 0, new[] { dot }, 1, decisions));
            Assert.IsTrue(decisions[0].NonLethalHp); Assert.IsFalse(decisions[0].AttemptOrdinaryInterrupt);
            Assert.IsTrue(stage.BindBuffApplication(hit.Key.Target, buff, false, 103, 10001, root, parent));
            var second = stage.FindBuffLineage(hit.Key.Target, buff);
            Assert.AreNotEqual(first.Binding.Source.Node, second.Binding.Source.Node);
            Assert.IsFalse(stage.FindBuffLineage(new GameplayEntityId(hit.Key.Target.Index, 99), buff).Binding.Root.IsValid);
            Assert.AreEqual(Hash(stage), Hash(stage.CloneTrusted()));
        }

        [Test]
        public void OnePunchTombstoneSurvivesHostEndAndDoesNotAuthorizeAnotherRoot()
        {
            var stage = Stage(); var root = Publish(stage, 1, true); var source = stage.SecondBatchRoot(root);
            var output = new TacticalSecondBatchHitDecision[1]; var hit = Hit(source, 1);
            Assert.IsTrue(stage.TryResolveSecondBatchRound(101, 0, new[] { hit }, 1, output));
            Assert.IsTrue(stage.AcknowledgeAction(root.Commit, 23, 2, 102, out var animation));
            Assert.IsFalse(stage.SecondBatchRoot(animation).IsValid); Assert.IsTrue(stage.SuppressesAttack(animation));
            Assert.IsTrue(stage.CompleteHost(root.Commit, true, 102, 0, 0, 0));
            Assert.IsTrue(stage.CloneTrusted().SuppressesAttack(root));
            var next = Publish(stage, 2, true);
            Assert.IsFalse(stage.SuppressesAttack(next));
        }

        [Test]
        public void DelayedAfterHitProjectileRetainsExactParentThroughTrustedRestore()
        {
            var stage = Stage(); var root = Publish(stage, 1); var parent = stage.SecondBatchRoot(root);
            var hit = Hit(parent, 100); var output = new TacticalSecondBatchHitDecision[1];
            Assert.IsTrue(stage.TryResolveSecondBatchRound(101, 0, new[] { hit }, 1, output));
            var direction = new FixVector3(Fix64.One, Fix64.Zero, Fix64.Zero);
            var request = new CombatProjectileSpawnRequest(new CombatFrame(102), 9, new CombatEntityId(10001),
                FixVector3.Zero, direction, 10, 22, 3, 0);
            Assert.IsTrue(stage.RememberProjectileRequest(request, root, parent, 101, false, hit.Key));
            long before = Hash(stage);
            Assert.IsTrue(stage.RememberProjectileRequest(request, root, parent, 101, false, hit.Key));
            Assert.AreEqual(before, Hash(stage));
            var copy = stage.CloneTrusted(); Assert.AreEqual(before, Hash(copy));
            Assert.IsTrue(copy.TryResolveSecondBatchRound(101, 1, Array.Empty<TacticalSecondBatchHit>(), 0, output));
            var snapshot = new CombatProjectileSnapshot(7, 1, 9, "test", request.SourceEntityId, 10, 22, 3, 0,
                new CombatFrame(102), new CombatFrame(102), 0, FixVector3.Zero, direction);
            Assert.IsTrue(copy.CompleteProjectileRequest(new CombatProjectileSpawnEvent(snapshot, request)));
            var child = copy.SecondBatchProducer(root, 1, 7, 1);
            Assert.IsTrue(child.IsValid); Assert.AreEqual(0, child.BirthRound); Assert.AreEqual(102, child.CreatedFrame);
            Assert.IsFalse(copy.IsDirectProjectile(7, 1)); Assert.IsFalse(copy.ProjectileSource(7, 2).IsValid);
            Assert.IsTrue(copy.TryResolveSecondBatchRound(102, 0, new[] { Hit(child, 1, 102) }, 1, output));
            Assert.IsTrue(output[0].NonLethalHp);
        }

        [Test]
        public void FrozenProjectileActorKeepsValuesAcrossMutationRemovalAndIdentityReuse()
        {
            var stage = Stage(); Publish(stage, 1);
            var actor = new GameplayEntityId(5, 3); var attributes = new[] { new GameplayAttributeValue(10, 7, 23) };
            var birth = new TacticalDoubleEdgeBattleState.ProjectileActor(7, 1, 10005, 2, actor, attributes);
            Assert.IsTrue(stage.FreezeProjectileActor(birth));
            attributes[0] = new GameplayAttributeValue(10, 999, 999);
            Assert.AreEqual(23, stage.FindProjectileActor(7, 1).ReadAttribute(10));
            var copy = stage.CloneTrusted(); Assert.AreEqual(Hash(stage), Hash(copy));
            Assert.IsNull(copy.FindProjectileActor(7, 2));
            Assert.AreEqual(actor, copy.FindProjectileActor(7, 1).Actor);
            Assert.IsTrue(copy.FreezeProjectileActor(new TacticalDoubleEdgeBattleState.ProjectileActor(7, 2, 10005, 9,
                new GameplayEntityId(5, 4), attributes)));
            Assert.AreEqual(2, copy.FindProjectileActor(7, 1).Team);
            Assert.AreEqual(9, copy.FindProjectileActor(7, 2).Team); Assert.AreNotEqual(Hash(stage), Hash(copy));
        }

        [Test]
        public void DelayedBirthKeepsQueuedActorThroughRemovalAndTrustedClone()
        {
            var stage = Stage(); var root = Publish(stage, 1);
            var request = new CombatProjectileSpawnRequest(new CombatFrame(102), 9, new CombatEntityId(10001),
                FixVector3.Zero, new FixVector3(Fix64.One, Fix64.Zero, Fix64.Zero), 10, 22, 3, 0);
            var values = new[] { new GameplayAttributeValue(10, 7, 23) };
            var actor = new TacticalDoubleEdgeBattleState.ProjectileActor(0, 0, 10001, 2, root.Commit.Actor, values);
            long before = Hash(stage); Assert.IsTrue(stage.RememberProjectileActor(request, actor)); Assert.AreNotEqual(before, Hash(stage));
            before = Hash(stage); Assert.IsTrue(stage.RememberProjectileActor(request, actor)); Assert.AreEqual(before, Hash(stage));
            var copy = stage.CloneTrusted(); Assert.AreEqual(before, Hash(copy));
            values[0] = new GameplayAttributeValue(10, 999, 999);
            var snapshot = new CombatProjectileSnapshot(7, 1, 9, "test", request.SourceEntityId, 10, 22, 3, 0,
                new CombatFrame(102), new CombatFrame(102), 0, FixVector3.Zero, request.Direction);
            Assert.IsTrue(copy.CompletePendingProjectileActor(new CombatProjectileSpawnEvent(snapshot, request)));
            Assert.AreEqual(root.Commit.Actor, copy.FindProjectileActor(7, 1).Actor);
            Assert.AreEqual(23, copy.FindProjectileActor(7, 1).ReadAttribute(10)); Assert.AreEqual(2, copy.FindProjectileActor(7, 1).Team);
            Assert.IsNull(stage.FindProjectileActor(7, 1)); Assert.AreNotEqual(before, Hash(copy));
            Assert.IsFalse(copy.CompletePendingProjectileActor(new CombatProjectileSpawnEvent(snapshot, request)));
            Assert.AreEqual(Hash(copy), Hash(copy.CloneTrusted()));
        }

        [Test]
        public void BoundStageHashesPendingBirthEvenBeforeItsFirstAccessoryCommit()
        {
            var stage = Stage(); Assert.IsFalse(stage.HasState); Assert.IsNull(stage.CaptureProjection());
            var request = new CombatProjectileSpawnRequest(new CombatFrame(102), 9, new CombatEntityId(10001),
                FixVector3.Zero, new FixVector3(Fix64.One, Fix64.Zero, Fix64.Zero), 10, 22, 3, 0);
            var actor = new TacticalDoubleEdgeBattleState.ProjectileActor(0, 0, 10001, 2, new GameplayEntityId(1, 3),
                new[] { new GameplayAttributeValue(10, 7, 23) });
            Assert.IsTrue(stage.RememberProjectileActor(request, actor)); Assert.IsTrue(stage.HasState);
            Assert.IsNotNull(stage.CaptureProjection()); Assert.AreEqual(Hash(stage), Hash(stage.CloneTrusted()));
            Assert.IsTrue(stage.CloneTrusted().HasState); Assert.AreEqual(0, stage.EntryCount);
        }

        [Test]
        public void SourceReplayDoesNotInvalidatePreparedPayment()
        {
            var stage = Stage(); var root = Publish(stage, 1); var parent = stage.SecondBatchRoot(root);
            Assert.IsTrue(stage.TryRegisterSecondBatchBirth(parent, 900, parent.Producer, 101, default, out var child));
            var key = new TacticalDoubleEdgeCommitKey(7, 2, 2, new GameplayEntityId(2, 3), 2, 101);
            Assert.IsTrue(stage.TryPrepare(key, 2, default, default, new TacticalSecondBatchParameters(false, true, false, 0, 0),
                false, true, out var prepared));
            Assert.IsTrue(stage.TryRegisterSecondBatchBirth(parent, 900, parent.Producer, 101, default, out var repeated));
            Assert.IsTrue(child.Matches(repeated)); Assert.IsTrue(prepared.IsCurrent);
        }

        [Test]
        public void TimelineAllocatorParticipatesInTrustedCloneHashAndProjection()
        {
            var stage = Stage(); Publish(stage, 1); var copy = stage.CloneTrusted(); long before = Hash(stage);
            stage.RecordTimelineIdentity(9);
            Assert.AreNotEqual(before, Hash(stage)); Assert.AreEqual(1, copy.TimelineNextInstanceId);
            Assert.AreEqual(9, stage.CloneTrusted().TimelineNextInstanceId);
            Assert.Throws<InvalidOperationException>(() => stage.RecordTimelineIdentity(8));
        }
    }
}
