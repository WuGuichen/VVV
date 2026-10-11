using System.Collections.Generic;
using MxFramework.Combat.Core;
using MxFramework.Combat.Hit;
using MxFramework.Combat.Physics;
using MxFramework.Combat.Shields;
using MxFramework.Core.Math;
using MxFramework.Runtime;
using NUnit.Framework;

namespace MxFramework.Tests.Combat.Shields
{
    public sealed class CombatShieldRuntimeTests
    {
        [Test]
        public void Impact_BreaksShieldRemovesColliderAndEmitsEvents()
        {
            CombatPhysicsWorld world = new CombatPhysicsWorld();
            RegisterShieldCollider(world);
            CombatShieldRuntime runtime = new CombatShieldRuntime(world);
            runtime.UpsertShield(CreateShield(baseCohesion: 100, breakLine: 100));

            bool applied = runtime.TryApplyImpact(
                Impact(delta: new CombatShieldPotencyTriplet(180, 0, 30)),
                out CombatShieldBlockEvent blockEvent);

            Assert.IsTrue(applied);
            Assert.IsTrue(blockEvent.TriggeredBreak);
            Assert.AreEqual(1, runtime.BlockEvents.Count);
            Assert.AreEqual(1, runtime.PotencyChangedEvents.Count);
            Assert.AreEqual(1, runtime.BreakEvents.Count);
            Assert.IsTrue(runtime.TryGetShield(ShieldEntity, out CombatShieldPotencyComponent shield));
            Assert.IsTrue(shield.IsBroken);
            Assert.AreEqual(CombatShieldPressureBand.Broken, shield.CurrentBand);
            Assert.IsFalse(world.TryGetAabbCollider(ShieldBody, ShieldCollider, out _));
        }

        [Test]
        public void Tick_RecoversPressureAfterDelayButBrokenShieldDoesNotRecover()
        {
            CombatShieldRuntime runtime = new CombatShieldRuntime();
            runtime.UpsertShield(CreateShield(baseCohesion: 0, breakLine: 100, recoveryRate: 10, recoveryDelayFrames: 1));
            runtime.TryApplyImpact(
                Impact(delta: new CombatShieldPotencyTriplet(50, 0, 20), frame: new CombatFrame(1)),
                out _);
            Assert.IsTrue(runtime.TryGetShield(ShieldEntity, out CombatShieldPotencyComponent afterImpact));
            Assert.AreEqual(70, afterImpact.ShieldPressure);

            runtime.Tick(new CombatFrame(2));
            Assert.IsTrue(runtime.TryGetShield(ShieldEntity, out CombatShieldPotencyComponent delayed));
            Assert.AreEqual(70, delayed.ShieldPressure);

            runtime.Tick(new CombatFrame(3));
            Assert.IsTrue(runtime.TryGetShield(ShieldEntity, out CombatShieldPotencyComponent recovered));
            Assert.AreEqual(60, recovered.ShieldPressure);
            Assert.AreEqual(1, runtime.PotencyChangedEvents.Count);

            runtime.TryApplyImpact(
                Impact(delta: new CombatShieldPotencyTriplet(80, 0, 0), frame: new CombatFrame(4)),
                out _);
            Assert.IsTrue(runtime.TryGetShield(ShieldEntity, out CombatShieldPotencyComponent broken));
            Assert.IsTrue(broken.IsBroken);

            runtime.Tick(new CombatFrame(10));
            Assert.IsTrue(runtime.TryGetShield(ShieldEntity, out CombatShieldPotencyComponent stillBroken));
            Assert.IsTrue(stillBroken.IsBroken);
            Assert.AreEqual(broken.ShieldPressure, stillBroken.ShieldPressure);
        }

        [Test]
        public void OcclusionFilter_HidesOwnerBehindActiveShield()
        {
            CombatShieldRuntime runtime = new CombatShieldRuntime();
            runtime.UpsertShield(CreateShield(baseCohesion: 100, breakLine: 100));
            var filter = new CombatShieldOcclusionFilter();
            var candidates = new[]
            {
                Candidate(target: ShieldEntity.Value, body: ShieldBody.Value, collider: ShieldCollider.Value, distanceRaw: 100000),
                Candidate(target: OwnerEntity.Value, body: OwnerBody.Value, collider: OwnerCollider.Value, distanceRaw: 200000),
                Candidate(target: 30, body: 30, collider: 1, distanceRaw: 300000),
            };
            var visible = new List<HitCandidate>();
            var shieldHits = new List<CombatShieldOcclusionHit>();

            int visibleCount = filter.FilterWeaponTraceCandidates(candidates, runtime, visible, shieldHits);

            Assert.AreEqual(1, visibleCount);
            Assert.AreEqual(1, visible.Count);
            Assert.AreEqual(30, visible[0].TargetId.Value);
            Assert.AreEqual(1, shieldHits.Count);
            Assert.AreEqual(ShieldEntity, shieldHits[0].Shield.ShieldEntityId);
        }

        [Test]
        public void OcclusionFilter_BrokenShieldDoesNotHideOwnerOnNextTrace()
        {
            CombatShieldRuntime runtime = new CombatShieldRuntime();
            runtime.UpsertShield(CreateShield(baseCohesion: 0, breakLine: 100));
            runtime.TryApplyImpact(
                Impact(delta: new CombatShieldPotencyTriplet(100, 0, 0)),
                out _);
            var filter = new CombatShieldOcclusionFilter();
            var candidates = new[]
            {
                Candidate(target: OwnerEntity.Value, body: OwnerBody.Value, collider: OwnerCollider.Value, distanceRaw: 200000),
            };
            var visible = new List<HitCandidate>();
            var shieldHits = new List<CombatShieldOcclusionHit>();

            int visibleCount = filter.FilterWeaponTraceCandidates(candidates, runtime, visible, shieldHits);

            Assert.AreEqual(1, visibleCount);
            Assert.AreEqual(OwnerEntity, visible[0].TargetId);
            Assert.AreEqual(0, shieldHits.Count);
        }

        [Test]
        public void OcclusionFilter_FindsNearestProjectileShieldHit()
        {
            CombatShieldRuntime runtime = new CombatShieldRuntime();
            runtime.UpsertShield(CreateShield(ShieldEntity.Value, OwnerEntity.Value, ShieldBody.Value, ShieldCollider.Value, 100, 100));
            runtime.UpsertShield(CreateShield(11, OwnerEntity.Value, 11, 1, 100, 100));
            var filter = new CombatShieldOcclusionFilter();
            var queryResults = new[]
            {
                QueryHit(target: ShieldEntity.Value, body: ShieldBody.Value, collider: ShieldCollider.Value, distanceRaw: 300000),
                QueryHit(target: 11, body: 11, collider: 1, distanceRaw: 100000),
                QueryHit(target: OwnerEntity.Value, body: OwnerBody.Value, collider: OwnerCollider.Value, distanceRaw: 50000),
            };

            bool found = filter.TryFindNearestShieldHit(queryResults, runtime, out CombatShieldQueryHit shieldHit);

            Assert.IsTrue(found);
            Assert.AreEqual(11, shieldHit.Shield.ShieldEntityId.Value);
            Assert.AreEqual(100000, shieldHit.QueryResult.Distance.RawValue);
        }

        [Test]
        public void SaveState_RoundTripsShieldStateAndRuntimeHash()
        {
            CombatShieldRuntime runtime = new CombatShieldRuntime();
            runtime.UpsertShield(CreateShield(baseCohesion: 50, breakLine: 100, recoveryRate: 2, recoveryDelayFrames: 3));
            long beforeHash = RuntimeHashCombiner.ComputeHash(
                RuntimeFrame.Zero,
                new IRuntimeHashContributor[] { new CombatShieldRuntimeHashContributor(runtime) });

            runtime.TryApplyImpact(
                Impact(delta: new CombatShieldPotencyTriplet(40, 0, 20), frame: new CombatFrame(7)),
                out _);
            long impactedHash = RuntimeHashCombiner.ComputeHash(
                RuntimeFrame.Zero,
                new IRuntimeHashContributor[] { new CombatShieldRuntimeHashContributor(runtime) });
            RuntimeSaveStateResult<RuntimeSaveState> captured =
                new CombatShieldRuntimeSaveStateProvider(runtime).CaptureSaveState();

            CombatShieldRuntime restored = new CombatShieldRuntime();
            RuntimeSaveStateResult<bool> restoredResult =
                new CombatShieldRuntimeSaveStateProvider(restored).RestoreSaveState(captured.Value);
            long restoredHash = RuntimeHashCombiner.ComputeHash(
                RuntimeFrame.Zero,
                new IRuntimeHashContributor[] { new CombatShieldRuntimeHashContributor(restored) });

            Assert.IsTrue(captured.Success, captured.Error.ToString());
            Assert.IsTrue(restoredResult.Success, restoredResult.Error.ToString());
            Assert.AreNotEqual(beforeHash, impactedHash);
            Assert.AreEqual(impactedHash, restoredHash);
        }

        [Test]
        public void RestoreRuntimeState_KeepsUnbrokenColliderAndRemovesBrokenCollider()
        {
            CombatPhysicsWorld world = new CombatPhysicsWorld();
            RegisterShieldCollider(world);
            CombatShieldRuntime runtime = new CombatShieldRuntime(world);
            runtime.UpsertShield(CreateShield(baseCohesion: 100, breakLine: 100));
            CombatShieldRuntimeState unbrokenState = runtime.CreateStateSnapshot();

            runtime.RestoreRuntimeState(unbrokenState);

            Assert.IsTrue(world.TryGetAabbCollider(ShieldBody, ShieldCollider, out _));

            runtime.TryApplyImpact(
                Impact(delta: new CombatShieldPotencyTriplet(220, 0, 0)),
                out _);
            CombatShieldRuntimeState brokenState = runtime.CreateStateSnapshot();
            RegisterShieldCollider(world);

            runtime.RestoreRuntimeState(brokenState);

            Assert.IsFalse(world.TryGetAabbCollider(ShieldBody, ShieldCollider, out _));
        }

        private static readonly CombatEntityId ShieldEntity = new CombatEntityId(10);
        private static readonly CombatEntityId OwnerEntity = new CombatEntityId(20);
        private static readonly CombatBodyId ShieldBody = new CombatBodyId(10);
        private static readonly CombatColliderId ShieldCollider = new CombatColliderId(1);
        private static readonly CombatBodyId OwnerBody = new CombatBodyId(20);
        private static readonly CombatColliderId OwnerCollider = new CombatColliderId(1);

        private static CombatShieldPotencyComponent CreateShield(
            int baseCohesion,
            int breakLine,
            int recoveryRate = 0,
            int recoveryDelayFrames = 0)
        {
            return CreateShield(
                ShieldEntity.Value,
                OwnerEntity.Value,
                ShieldBody.Value,
                ShieldCollider.Value,
                baseCohesion,
                breakLine,
                recoveryRate,
                recoveryDelayFrames);
        }

        private static CombatShieldPotencyComponent CreateShield(
            int shieldEntity,
            int ownerEntity,
            int body,
            int collider,
            int baseCohesion,
            int breakLine,
            int recoveryRate = 0,
            int recoveryDelayFrames = 0)
        {
            return new CombatShieldPotencyComponent(
                new CombatEntityId(shieldEntity),
                new CombatEntityId(ownerEntity),
                new CombatBodyId(body),
                new CombatColliderId(collider),
                new CombatShieldPotencyTriplet(0, baseCohesion, 0),
                breakLine,
                recoveryRate,
                recoveryDelayFrames);
        }

        private static CombatShieldImpactRequest Impact(
            CombatShieldPotencyTriplet delta,
            CombatFrame frame = default)
        {
            if (frame.Equals(default(CombatFrame)))
                frame = new CombatFrame(5);

            return new CombatShieldImpactRequest(
                ShieldEntity,
                new CombatEntityId(99),
                CombatShieldImpactSourceKind.ProjectileImpact,
                frame,
                actionId: 1001,
                actionInstanceId: 2001,
                traceId: 7,
                damage: 10,
                impactPower: 15,
                delta,
                FixVector3.Zero,
                new FixVector3(-Fix64.One, Fix64.Zero, Fix64.Zero));
        }

        private static HitCandidate Candidate(
            int target,
            int body,
            int collider,
            int distanceRaw)
        {
            CombatQueryResult hit = QueryHit(target, body, collider, distanceRaw);
            return new HitCandidate(
                new CombatEntityId(99),
                new CombatEntityId(target),
                actionId: 1001,
                actionInstanceId: 2001,
                traceId: 7,
                frame: new CombatFrame(12),
                physicsHit: hit,
                damage: 10,
                staggerFrames: 0,
                knockback: FixVector3.Zero,
                targetState: HitTargetStateFlags.Alive);
        }

        private static CombatQueryResult QueryHit(
            int target,
            int body,
            int collider,
            int distanceRaw)
        {
            var query = new CombatQueryHeader(
                queryId: 1,
                kind: CombatQueryKind.Capsule,
                sourceEntityId: new CombatEntityId(99),
                traceId: 7,
                actionId: 1001,
                sourceOrder: 0,
                layerMask: CombatPhysicsLayerMask.All);

            return new CombatQueryResult(
                query,
                new CombatEntityId(target),
                new CombatBodyId(body),
                new CombatColliderId(collider),
                Fix64.FromRaw(distanceRaw),
                FixVector3.Zero,
                new FixVector3(-Fix64.One, Fix64.Zero, Fix64.Zero));
        }

        private static void RegisterShieldCollider(CombatPhysicsWorld world)
        {
            world.UpsertBody(new CombatPhysicsBody(ShieldEntity, ShieldBody, FixVector3.Zero));
            world.UpsertAabbCollider(new CombatPhysicsAabbCollider(
                ShieldBody,
                ShieldCollider,
                layer: 0,
                localMin: new FixVector3(-Fix64.Half, Fix64.Zero, -Fix64.Half),
                localMax: new FixVector3(Fix64.Half, Fix64.FromInt(2), Fix64.Half)));
        }
    }
}
