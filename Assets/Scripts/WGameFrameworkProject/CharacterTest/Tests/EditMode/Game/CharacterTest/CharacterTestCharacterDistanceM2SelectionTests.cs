using System;
using System.Collections.Generic;
using MxFramework.CharacterRuntimeSpawn;
using MxFramework.CharacterRuntimeSpawn.Unity;
using MxFramework.Combat.Core;
using MxFramework.Combat.Physics;
using MxFramework.Core.Math;
using MxFramework.Game.CharacterTest;
using MxFramework.Gameplay;
using MxFramework.Resources;
using MxFramework.Runtime;
using NUnit.Framework;

namespace MxFramework.Tests.Game.CharacterTest
{
    public sealed class CharacterTestCharacterDistanceM2SelectionTests
    {
        [Test]
        public void InitialSelection_UsesLevelTickPriorityAndStableIds()
        {
            Fixture fixture = CreateFixture();
            fixture.Evaluator.Set(601, CharacterTestAttackOpportunityLevel.CurrentPose, 5, CharacterTestAuthoredContentIds.D400_Id133);
            fixture.Evaluator.Set(602, CharacterTestAttackOpportunityLevel.CurrentPose, 5, CharacterTestAuthoredContentIds.D400_Id134);
            CharacterTestDamageZoneSelectionResult result = fixture.Select(default);
            Assert.AreEqual(602, result.Zone.ZoneId, "higher authored priority should win after equal level/tick.");
            Assert.AreEqual(CharacterTestDamageZoneSwitchReason.InitialSelection, result.SwitchReason);

            fixture.Evaluator.Set(601, CharacterTestAttackOpportunityLevel.CurrentPose, 4, CharacterTestAuthoredContentIds.D400_Id133);
            result = fixture.Select(default);
            Assert.AreEqual(601, result.Zone.ZoneId, "earlier tick should outrank authored priority.");
        }

        [TestCase(0)]
        [TestCase(1)]
        public void PreviousLock_ImprovementBelowTwoTicksIsKept(int improvement)
        {
            Fixture fixture = CreateFixture();
            fixture.Evaluator.Set(601, CharacterTestAttackOpportunityLevel.CurrentPose, 6, CharacterTestAuthoredContentIds.D400_Id133);
            fixture.Evaluator.Set(602, CharacterTestAttackOpportunityLevel.CurrentPose, 8, CharacterTestAuthoredContentIds.D400_Id134);
            CharacterTestDamageZoneLock previous = fixture.Select(default).ZoneLock;
            fixture.Evaluator.Set(602, CharacterTestAttackOpportunityLevel.CurrentPose, 6 - improvement, CharacterTestAuthoredContentIds.D400_Id134);
            CharacterTestDamageZoneSelectionResult result = fixture.Select(previous);
            Assert.AreEqual(601, result.Zone.ZoneId);
            Assert.AreEqual(CharacterTestDamageZoneSwitchReason.PreviousLockKept, result.SwitchReason);
        }

        [TestCase(2)]
        [TestCase(3)]
        public void PreviousLock_ImprovementAtLeastTwoTicksSwitches(int improvement)
        {
            Fixture fixture = CreateFixture();
            fixture.Evaluator.Set(601, CharacterTestAttackOpportunityLevel.CurrentPose, 8, CharacterTestAuthoredContentIds.D400_Id133);
            fixture.Evaluator.Set(602, CharacterTestAttackOpportunityLevel.CurrentPose, 10, CharacterTestAuthoredContentIds.D400_Id134);
            CharacterTestDamageZoneLock previous = fixture.Select(default).ZoneLock;
            fixture.Evaluator.Set(602, CharacterTestAttackOpportunityLevel.CurrentPose, 8 - improvement, CharacterTestAuthoredContentIds.D400_Id134);
            CharacterTestDamageZoneSelectionResult result = fixture.Select(previous);
            Assert.AreEqual(602, result.Zone.ZoneId);
            Assert.AreEqual(CharacterTestDamageZoneSwitchReason.EarliestHitImprovedByThreshold, result.SwitchReason);
        }

        [Test]
        public void PreviousLock_HigherOpportunityLevelSwitchesWithoutTickThreshold()
        {
            Fixture fixture = CreateFixture();
            fixture.Evaluator.Set(601, CharacterTestAttackOpportunityLevel.TurnInPlace, 3, CharacterTestAuthoredContentIds.D400_Id133);
            fixture.Evaluator.Set(602, CharacterTestAttackOpportunityLevel.RequiresTranslation, -1, CharacterTestAuthoredContentIds.D400_Id134);
            CharacterTestDamageZoneLock previous = fixture.Select(default).ZoneLock;
            fixture.Evaluator.Set(602, CharacterTestAttackOpportunityLevel.CurrentPose, 99, CharacterTestAuthoredContentIds.D400_Id134);
            CharacterTestDamageZoneSelectionResult result = fixture.Select(previous);
            Assert.AreEqual(602, result.Zone.ZoneId);
            Assert.AreEqual(CharacterTestDamageZoneSwitchReason.OpportunityLevelImproved, result.SwitchReason);
        }

        [Test]
        public void PreviousLock_StaleWitnessInvalidatesAndUnavailableDoesNotParticipate()
        {
            Fixture fixture = CreateFixture();
            fixture.Evaluator.Set(601, CharacterTestAttackOpportunityLevel.CurrentPose, 4, CharacterTestAuthoredContentIds.D400_Id133);
            fixture.Evaluator.Set(602, CharacterTestAttackOpportunityLevel.CurrentPose, 5, CharacterTestAuthoredContentIds.D400_Id134);
            CharacterTestDamageZoneLock previous = fixture.Select(default).ZoneLock;
            fixture.Evaluator.SetUnavailable(601);
            CharacterTestDamageZoneSelectionResult result = fixture.Select(previous);
            Assert.AreEqual(602, result.Zone.ZoneId);
            Assert.AreEqual(CharacterTestDamageZoneSwitchReason.PreviousLockInvalidated, result.SwitchReason);
        }

        [Test]
        public void PreviousLock_PoseAndWorldRevisionRefreshDoesNotBreakHysteresis()
        {
            Fixture fixture = CreateFixture();
            fixture.Evaluator.Set(601, CharacterTestAttackOpportunityLevel.CurrentPose, 6, CharacterTestAuthoredContentIds.D400_Id133);
            fixture.Evaluator.Set(602, CharacterTestAttackOpportunityLevel.CurrentPose, 7, CharacterTestAuthoredContentIds.D400_Id134);
            CharacterTestDamageZoneLock captured = fixture.Select(default).ZoneLock;
            var priorFrameWitness = new CharacterTestDamageZoneLock(
                captured.TargetGameplayEntityId,
                captured.TargetCombatEntityId,
                captured.ResolvedActionRevision,
                captured.ZoneId,
                captured.HitZoneId,
                captured.LogicalHurtboxId,
                captured.OpportunityLevel,
                captured.EarliestHitTick,
                captured.BindingRevision,
                captured.HurtboxRevision - 1,
                captured.WorldRevision - 1,
                captured.ZoneRevision,
                RuntimeFrame.Zero,
                captured.SwitchReason);
            fixture.Evaluator.Set(602, CharacterTestAttackOpportunityLevel.CurrentPose, 5, CharacterTestAuthoredContentIds.D400_Id134);
            CharacterTestDamageZoneSelectionResult result = fixture.Select(priorFrameWitness);
            Assert.AreEqual(601, result.Zone.ZoneId);
            Assert.AreEqual(CharacterTestDamageZoneSwitchReason.PreviousLockKept, result.SwitchReason);
            Assert.AreEqual(fixture.ZoneSnapshot.HurtboxRevision, result.ZoneLock.HurtboxRevision);
            Assert.AreEqual(fixture.ZoneSnapshot.WorldRevision, result.ZoneLock.WorldRevision);
        }

        [Test]
        public void Selector_StaleZoneSnapshotFailsClosed()
        {
            Fixture fixture = CreateFixture();
            fixture.Evaluator.Set(601, CharacterTestAttackOpportunityLevel.CurrentPose, 4, CharacterTestAuthoredContentIds.D400_Id133);
            fixture.ZoneSnapshot.SealWorldRevision(fixture.Query.CurrentCompletedFrame, fixture.Query.CurrentCombatPhysicsRevision + 1);
            CharacterTestDamageZoneSelectionResult result = fixture.Select(default);
            Assert.AreEqual(CharacterTestDamageZoneSelectionStatus.StaleSnapshot, result.Status);
        }

        [Test]
        public void Selector_WarmedPathAllocatesZeroBytes()
        {
            Fixture fixture = CreateFixture();
            fixture.Evaluator.Set(601, CharacterTestAttackOpportunityLevel.CurrentPose, 4, CharacterTestAuthoredContentIds.D400_Id133);
            fixture.Evaluator.Set(602, CharacterTestAttackOpportunityLevel.CurrentPose, 5, CharacterTestAuthoredContentIds.D400_Id134);
            CharacterTestDamageZoneLock previous = fixture.Select(default).ZoneLock;
            _ = GC.GetAllocatedBytesForCurrentThread();
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 32; i++)
                previous = fixture.Select(previous).ZoneLock;
            Assert.AreEqual(0L, GC.GetAllocatedBytesForCurrentThread() - before);
        }

        [Test]
        public void EvaluateZone_EvaluatesOnlyRequestedZoneWithoutCrossZoneHysteresis()
        {
            Fixture fixture = CreateFixture();
            fixture.Evaluator.Set(601, CharacterTestAttackOpportunityLevel.TurnInPlace, 8, CharacterTestAuthoredContentIds.D400_Id133);
            fixture.Evaluator.Set(602, CharacterTestAttackOpportunityLevel.CurrentPose, 1, CharacterTestAuthoredContentIds.D400_Id134);

            CharacterTestDamageZoneSelectionResult result = fixture.EvaluateZone(601);

            Assert.AreEqual(CharacterTestDamageZoneSelectionStatus.Valid, result.Status);
            Assert.AreEqual(601, result.Zone.ZoneId);
            Assert.AreEqual(CharacterTestAuthoredContentIds.D400_Id133, result.Opportunity.SelectedHurtboxId);
            Assert.AreEqual(CharacterTestAttackOpportunityLevel.TurnInPlace, result.Opportunity.Level);
            Assert.AreEqual(601, result.ZoneLock.ZoneId);
        }

        private static Fixture CreateFixture()
        {
            var world = new CombatPhysicsWorld();
            RuntimeFrame frame = new RuntimeFrame(11);
            CharacterTestCharacterEntity source = CreateEntity(1);
            CharacterTestCharacterEntity target = CreateEntity(2);
            var geometry = new CharacterTestDamageGeometrySnapshot(2);
            geometry.BeginWrite(2, target.GameplayEntityId, new CombatEntityId(CharacterTestCharacterPhysicsRuntime.DynamicBodyIdStart + 2));
            AddCollider(geometry, "hurt.alpha", 1, "hit.alpha");
            AddCollider(geometry, "hurt.beta", 2, "hit.beta");
            geometry.SortCollidersByStableLogicalId();
            geometry.Commit(frame, world.Revision, 7);
            var zones = new CharacterTestDamageZoneSnapshot();
            zones.Build(geometry, new[]
            {
                new CharacterDamageZoneRuntimeBinding(601, "Alpha", new[] { CharacterTestAuthoredContentIds.D400_Id55 }, true, true, false, 1, "zone.alpha"),
                new CharacterDamageZoneRuntimeBinding(602, "Beta", new[] { CharacterTestAuthoredContentIds.D400_Id56 }, true, false, true, 9, "zone.beta")
            });
            Assert.IsTrue(CharacterTestAttackEvaluationSnapshot.TryCapture(
                frame, world, source, target, geometry, 3, 5, 13, out CharacterTestAttackEvaluationSnapshot snapshot));
            var query = new CharacterTestAttackOpportunityQuery(
                snapshot, geometry, world, source, target,
                default, false, default, false,
                frame, world.Revision, 3, 5, geometry.HurtboxRevision, 13,
                null, null);
            var evaluator = new FakeEvaluator();
            return new Fixture(query, zones, evaluator);
        }

        private static CharacterTestCharacterEntity CreateEntity(int id)
        {
            return new CharacterTestCharacterEntity(
                new CharacterTestCharacterEntityId(id),
                new CharacterTestCharacterIdentityComponent(1, "M2", default(ResourceKey), default(CharacterRuntimeAnimationBinding)),
                new CharacterTestCharacterTeamComponent(1, 1),
                new CharacterTestCharacterTransformComponent(id == 1 ? 0f : 3f, 0f, 0f, 0f),
                new CharacterTestCharacterEquipmentComponent(false),
                new CharacterTestCharacterControlComponent(CharacterTestCharacterControlOwner.None),
                new GameplayEntityId(id, 1),
                CharacterTestCharacterPhysicsComponent.Default);
        }

        private static void AddCollider(CharacterTestDamageGeometrySnapshot geometry, string id, int colliderId, string hitZoneId)
        {
            geometry.AddCollider(
                ResolveLogicalColliderId(id), id, new CombatColliderId(colliderId),
                CharacterTestAuthoredContentIds.D400_Id34, hitZoneId,
                ResolveHitZoneId(hitZoneId), hitZoneId, 0,
                CombatPhysicsShape.Aabb(FixVector3.Zero, FixVector3.Zero),
                FixVector3.Zero, new FixVector3(Fix64.One, Fix64.One, Fix64.One));
        }

        private static int ResolveLogicalColliderId(string label)
        {
            return label == "hurt.alpha"
                ? CharacterTestAuthoredContentIds.D400_Id133
                : CharacterTestAuthoredContentIds.D400_Id134;
        }

        private static int ResolveHitZoneId(string label)
        {
            return label == "hit.alpha"
                ? CharacterTestAuthoredContentIds.D400_Id55
                : CharacterTestAuthoredContentIds.D400_Id56;
        }

        private sealed class Fixture
        {
            private readonly CharacterTestDamageZoneSelector _selector;

            internal Fixture(CharacterTestAttackOpportunityQuery query, CharacterTestDamageZoneSnapshot zones, FakeEvaluator evaluator)
            {
                Query = query;
                ZoneSnapshot = zones;
                Evaluator = evaluator;
                _selector = new CharacterTestDamageZoneSelector(evaluator);
            }

            internal CharacterTestAttackOpportunityQuery Query { get; }
            internal CharacterTestDamageZoneSnapshot ZoneSnapshot { get; }
            internal FakeEvaluator Evaluator { get; }
            internal CharacterTestDamageZoneSelectionResult Select(CharacterTestDamageZoneLock previous) =>
                _selector.Select(Query, ZoneSnapshot, previous);
            internal CharacterTestDamageZoneSelectionResult EvaluateZone(int zoneId) =>
                _selector.EvaluateZone(Query, ZoneSnapshot, zoneId);
        }

        private sealed class FakeEvaluator : ICharacterTestAttackOpportunityEvaluator
        {
            private readonly Dictionary<int, CharacterTestAttackOpportunityResult> _results =
                new Dictionary<int, CharacterTestAttackOpportunityResult>();

            internal void Set(int zoneId, CharacterTestAttackOpportunityLevel level, int tick, int hurtboxId)
            {
                _results[zoneId] = Result(zoneId, level, tick, hurtboxId, CharacterTestAttackOpportunityStatus.Valid);
            }

            internal void SetUnavailable(int zoneId)
            {
                _results[zoneId] = Result(zoneId, CharacterTestAttackOpportunityLevel.NoOpportunity, -1, 0, CharacterTestAttackOpportunityStatus.Unavailable);
            }

            public CharacterTestAttackOpportunityResult Evaluate(CharacterTestAttackOpportunityQuery query, int requiredYawDeltaMilliDegrees = 0)
            {
                return _results.TryGetValue(query.RequiredDamageZoneId, out CharacterTestAttackOpportunityResult result)
                    ? result
                    : Result(query.RequiredDamageZoneId, CharacterTestAttackOpportunityLevel.NoOpportunity, -1, 0, CharacterTestAttackOpportunityStatus.Unavailable);
            }

            private static CharacterTestAttackOpportunityResult Result(
                int zoneId,
                CharacterTestAttackOpportunityLevel level,
                int tick,
                int hurtboxId,
                CharacterTestAttackOpportunityStatus status)
            {
                return new CharacterTestAttackOpportunityResult(
                    status, CharacterTestAttackEvaluationMode.CurrentPose, level,
                    true, true, true,
                    level == CharacterTestAttackOpportunityLevel.CurrentPose,
                    level >= CharacterTestAttackOpportunityLevel.TurnInPlace,
                    level == CharacterTestAttackOpportunityLevel.RequiresTranslation,
                    0, tick, 0, 1, hurtboxId,
                    hurtboxId == CharacterTestAuthoredContentIds.D400_Id133 ? CharacterTestAuthoredContentIds.D400_Id55 : CharacterTestAuthoredContentIds.D400_Id56,
                    zoneId, FixVector3.Zero, FixVector3.Zero,
                    CharacterTestAttackOpportunityFailureReason.None,
                    CharacterTestAttackOpportunityFailureFlags.None);
            }
        }
    }
}
