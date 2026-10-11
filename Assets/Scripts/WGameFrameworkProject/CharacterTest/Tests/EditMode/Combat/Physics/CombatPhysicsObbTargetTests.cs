using System.Collections.Generic;
using System;
using System.Diagnostics;
using MxFramework.Combat.Core;
using MxFramework.Combat.Physics;
using MxFramework.Core.Math;
using NUnit.Framework;

namespace MxFramework.Tests.Combat.Physics
{
    public sealed class CombatPhysicsObbTargetTests
    {
        private static readonly CombatEntityId EntityId = new CombatEntityId(71);
        private static readonly CombatBodyId BodyId = new CombatBodyId(72);
        private static readonly CombatColliderId ColliderId = new CombatColliderId(73);
        private static readonly FixVector3 AxisX = new FixVector3(Fix64.One, Fix64.Zero, Fix64.Zero);
        private static readonly FixVector3 AxisY = new FixVector3(Fix64.Zero, Fix64.One, Fix64.Zero);
        private static readonly FixVector3 AxisZ = new FixVector3(Fix64.Zero, Fix64.Zero, Fix64.One);

        [TestCase(CombatQueryKind.Ray)]
        [TestCase(CombatQueryKind.Sphere)]
        [TestCase(CombatQueryKind.Capsule)]
        [TestCase(CombatQueryKind.Aabb)]
        [TestCase(CombatQueryKind.Sector)]
        [TestCase(CombatQueryKind.Obb)]
        public void Query_AllSupportedShapesHitObbTarget(CombatQueryKind kind)
        {
            CombatPhysicsWorld world = CreateWorld();
            CombatPhysicsQuery query = CreateQuery(kind);
            var hits = new List<CombatQueryResult>();

            Assert.That(world.Query(query, hits), Is.EqualTo(1));
            Assert.That(hits[0].TargetShapeKind, Is.EqualTo(CombatPhysicsShapeKind.Obb));
            Assert.That(hits[0].TargetColliderId, Is.EqualTo(ColliderId));
        }

        [Test]
        public void ColliderBatchUpdate_InvalidatesTopologyOnce()
        {
            CombatPhysicsWorld world = CreateWorld();
            int revision = world.TopologyRevision;
            world.BeginColliderBatchUpdate();
            world.UpsertObbCollider(CreateCollider(FixVector3.Zero));
            world.UpsertObbCollider(CreateCollider(new FixVector3(Fix64.One, Fix64.Zero, Fix64.Zero)));
            Assert.That(world.TopologyRevision, Is.EqualTo(revision));
            world.EndColliderBatchUpdate();
            Assert.That(world.TopologyRevision, Is.EqualTo(revision + 1));
        }

        [Test]
        public void Query_EqualDistanceUsesDescendingTargetPriorityBeforeStableId()
        {
            var world = new CombatPhysicsWorld();
            world.UpsertBody(new CombatPhysicsBody(EntityId, BodyId, FixVector3.Zero));
            world.UpsertSphereCollider(new CombatPhysicsSphereCollider(
                BodyId,
                new CombatColliderId(10),
                10,
                FixVector3.Zero,
                Fix64.One,
                priority: 1));
            world.UpsertSphereCollider(new CombatPhysicsSphereCollider(
                BodyId,
                new CombatColliderId(20),
                10,
                FixVector3.Zero,
                Fix64.One,
                priority: 50));
            var hits = new List<CombatQueryResult>();

            world.Query(CreateQuery(CombatQueryKind.Ray), hits);

            Assert.That(hits, Has.Count.EqualTo(2));
            Assert.That(hits[0].TargetColliderId, Is.EqualTo(new CombatColliderId(20)));
            Assert.That(hits[0].TargetPriority, Is.EqualTo(50));
        }

        [Test]
        public void EightLargeCharacters_UpdatesAndOneHundredQueriesStayNoAllocAndUnderBudget()
        {
            var world = new CombatPhysicsWorld();
            for (int character = 0; character < 8; character++)
            {
                var bodyId = new CombatBodyId(100 + character);
                world.UpsertBody(new CombatPhysicsBody(
                    new CombatEntityId(100 + character),
                    bodyId,
                    new FixVector3(Fix64.FromInt(character * 20), Fix64.Zero, Fix64.Zero)));
                for (int collider = 0; collider < 31; collider++)
                    world.UpsertObbCollider(CreatePerformanceCollider(bodyId, collider, Fix64.Zero));
            }

            var hits = new List<CombatQueryResult>(64);
            CombatPhysicsQuery query = CombatPhysicsQuery.From(new CombatRayQuery(
                new CombatQueryHeader(10, CombatQueryKind.Ray, CombatEntityId.None, 1, 1, 1, CombatPhysicsLayerMask.FromLayer(10)),
                new FixVector3(-Fix64.FromInt(5), Fix64.One, Fix64.Zero),
                AxisX,
                Fix64.FromInt(10)));
            for (int i = 0; i < 8; i++) RunPerformanceFrame(world, query, hits, (i & 1) == 0 ? Fix64.Zero : Fix64.FromRatio(1, 1000));

            var elapsedTicks = new long[60];
            _ = GC.GetAllocatedBytesForCurrentThread();
            for (int frame = 0; frame < elapsedTicks.Length; frame++)
            {
                long allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
                long started = Stopwatch.GetTimestamp();
                RunPerformanceFrame(world, query, hits, (frame & 1) == 0 ? Fix64.Zero : Fix64.FromRatio(1, 1000));
                elapsedTicks[frame] = Stopwatch.GetTimestamp() - started;
                Assert.That(GC.GetAllocatedBytesForCurrentThread() - allocatedBefore, Is.Zero, "steady-state frame allocated at sample " + frame);
            }

            Array.Sort(elapsedTicks);
            double p95Milliseconds = elapsedTicks[56] * 1000.0 / Stopwatch.Frequency;
            TestContext.WriteLine("hurtbox_perf_p95_ms=" + p95Milliseconds.ToString("F4", System.Globalization.CultureInfo.InvariantCulture));
            Assert.That(p95Milliseconds, Is.LessThanOrEqualTo(2.0), "P95 update + query budget in milliseconds");
        }

        [Test]
        public void CompoundBvh_EightByThirtyTwoLeavesAndOneHundredQueriesStayNoAllocAndUnderOneMillisecond()
        {
            var world = new CombatPhysicsWorld();
            for (int character = 0; character < 8; character++)
            {
                var bodyId = new CombatBodyId(300 + character);
                world.UpsertBody(new CombatPhysicsBody(new CombatEntityId(300 + character), bodyId, new FixVector3(Fix64.FromInt(character * 20), Fix64.Zero, Fix64.Zero)));
                UpdateCompound(world, bodyId, Fix64.Zero);
            }

            var hits = new List<CombatQueryResult>(64);
            CombatPhysicsQuery query = CombatPhysicsQuery.From(new CombatRayQuery(
                new CombatQueryHeader(11, CombatQueryKind.Ray, CombatEntityId.None, 1, 1, 1, CombatPhysicsLayerMask.FromLayer(10)),
                new FixVector3(-Fix64.FromInt(5), Fix64.One, Fix64.Zero), AxisX, Fix64.FromInt(10)));
            for (int i = 0; i < 12; i++) RunCompoundPerformanceFrame(world, query, hits, (i & 1) == 0 ? Fix64.Zero : Fix64.FromRatio(1, 1000));

            var elapsedTicks = new long[80];
            _ = GC.GetAllocatedBytesForCurrentThread();
            for (int frame = 0; frame < elapsedTicks.Length; frame++)
            {
                long allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
                long started = Stopwatch.GetTimestamp();
                RunCompoundPerformanceFrame(world, query, hits, (frame & 1) == 0 ? Fix64.Zero : Fix64.FromRatio(1, 1000));
                elapsedTicks[frame] = Stopwatch.GetTimestamp() - started;
                Assert.That(GC.GetAllocatedBytesForCurrentThread() - allocatedBefore, Is.Zero, "compound steady-state frame allocated at sample " + frame);
            }

            Array.Sort(elapsedTicks);
            double p95Milliseconds = elapsedTicks[75] * 1000.0 / Stopwatch.Frequency;
            TestContext.WriteLine("compound_hurtbox_perf_p95_ms=" + p95Milliseconds.ToString("F4", System.Globalization.CultureInfo.InvariantCulture));
            Assert.That(p95Milliseconds, Is.LessThanOrEqualTo(1.0), "compound P95 update + query budget in milliseconds");
        }

        [Test]
        public void CompoundBvh_PreservesLeafIdentityPriorityAndBodyTranslation()
        {
            var world = new CombatPhysicsWorld();
            var bodyId = new CombatBodyId(401);
            world.UpsertBody(new CombatPhysicsBody(new CombatEntityId(402), bodyId, new FixVector3(Fix64.FromInt(10), Fix64.Zero, Fix64.Zero)));
            world.BeginCompoundColliderUpdate(bodyId, 2);
            world.UpsertCompoundCollider(new CombatPhysicsCompoundLeaf(new CombatColliderId(1), 10, CombatPhysicsShape.Sphere(FixVector3.Zero, Fix64.One), priority: 5));
            world.UpsertCompoundCollider(new CombatPhysicsCompoundLeaf(new CombatColliderId(2), 10, CombatPhysicsShape.Obb(FixVector3.Zero, new FixVector3(Fix64.One, Fix64.One, Fix64.One), AxisX, AxisY, AxisZ), priority: 50));
            world.EndCompoundColliderUpdate();
            var hits = new List<CombatQueryResult>();
            var header = new CombatQueryHeader(12, CombatQueryKind.Ray, CombatEntityId.None, 1, 1, 1, CombatPhysicsLayerMask.FromLayer(10));

            world.Query(CombatPhysicsQuery.From(new CombatRayQuery(header, new FixVector3(Fix64.FromInt(5), Fix64.Zero, Fix64.Zero), AxisX, Fix64.FromInt(10))), hits);

            Assert.That(hits, Has.Count.EqualTo(2));
            Assert.That(hits[0].TargetColliderId, Is.EqualTo(new CombatColliderId(2)));
            Assert.That(hits[0].TargetPriority, Is.EqualTo(50));
        }

        [Test]
        public void CompoundBvh_CopyApisExposeLeavesForSnapshotAndNetworkSerialization()
        {
            var world = new CombatPhysicsWorld();
            var bodyId = new CombatBodyId(410);
            world.UpsertBody(new CombatPhysicsBody(new CombatEntityId(411), bodyId, FixVector3.Zero));
            world.BeginCompoundColliderUpdate(bodyId, 3);
            world.UpsertCompoundCollider(new CombatPhysicsCompoundLeaf(new CombatColliderId(11), 10,
                CombatPhysicsShape.Sphere(FixVector3.Zero, Fix64.One), priority: 1));
            world.UpsertCompoundCollider(new CombatPhysicsCompoundLeaf(new CombatColliderId(12), 10,
                CombatPhysicsShape.Capsule(FixVector3.Zero, AxisY, Fix64.Half), priority: 2));
            world.UpsertCompoundCollider(new CombatPhysicsCompoundLeaf(new CombatColliderId(13), 10,
                CombatPhysicsShape.Obb(FixVector3.Zero, new FixVector3(Fix64.One, Fix64.Half, Fix64.Half), AxisX, AxisY, AxisZ), priority: 3));
            world.EndCompoundColliderUpdate();

            var spheres = new List<CombatPhysicsSphereCollider>();
            var capsules = new List<CombatPhysicsCapsuleCollider>();
            var obbs = new List<CombatPhysicsObbCollider>();
            Assert.That(world.CopySphereCollidersTo(spheres), Is.EqualTo(1));
            Assert.That(world.CopyCapsuleCollidersTo(capsules), Is.EqualTo(1));
            Assert.That(world.CopyObbCollidersTo(obbs), Is.EqualTo(1));
            Assert.That(spheres[0].ColliderId, Is.EqualTo(new CombatColliderId(11)));
            Assert.That(capsules[0].ColliderId, Is.EqualTo(new CombatColliderId(12)));
            Assert.That(obbs[0].ColliderId, Is.EqualTo(new CombatColliderId(13)));
        }

        [Test]
        public void CompoundBvh_LargeAuthoringShapesDoNotOverflowSahCost()
        {
            var world = new CombatPhysicsWorld();
            var bodyId = new CombatBodyId(420);
            world.UpsertBody(new CombatPhysicsBody(new CombatEntityId(421), bodyId, FixVector3.Zero));
            world.BeginCompoundColliderUpdate(bodyId, 18);
            for (int collider = 0; collider < 18; collider++)
            {
                var center = new FixVector3(Fix64.FromInt(collider * 10), Fix64.Zero, Fix64.Zero);
                world.UpsertCompoundCollider(new CombatPhysicsCompoundLeaf(
                    new CombatColliderId(6000 + collider),
                    10,
                    CombatPhysicsShape.Sphere(center, Fix64.FromInt(2000)),
                    priority: collider));
            }

            Assert.DoesNotThrow(() => world.EndCompoundColliderUpdate());
            Assert.That(world.CompoundLeafCount, Is.EqualTo(18));
        }

        private static void RunCompoundPerformanceFrame(CombatPhysicsWorld world, CombatPhysicsQuery query, List<CombatQueryResult> hits, Fix64 offset)
        {
            for (int character = 0; character < 8; character++) UpdateCompound(world, new CombatBodyId(300 + character), offset);
            for (int queryIndex = 0; queryIndex < 100; queryIndex++) { hits.Clear(); world.Query(query, hits); }
        }

        private static void UpdateCompound(CombatPhysicsWorld world, CombatBodyId bodyId, Fix64 offset)
        {
            world.BeginCompoundColliderUpdate(bodyId, 32);
            for (int collider = 0; collider < 32; collider++)
            {
                CombatPhysicsShape shape = (collider % 3) == 0
                    ? CombatPhysicsShape.Sphere(new FixVector3(offset, Fix64.FromRatio(collider % 6, 2), Fix64.FromRatio((collider % 5) - 2, 2)), Fix64.Half)
                    : (collider % 3) == 1
                        ? CombatPhysicsShape.Capsule(new FixVector3(offset, Fix64.FromRatio(collider % 6, 2), Fix64.Zero), new FixVector3(offset + Fix64.Half, Fix64.FromRatio(collider % 6, 2), Fix64.Zero), Fix64.FromRatio(1, 4))
                        : CombatPhysicsShape.Obb(new FixVector3(offset, Fix64.FromRatio(collider % 6, 2), Fix64.FromRatio((collider % 5) - 2, 2)), new FixVector3(Fix64.Half, Fix64.Half, Fix64.Half), AxisX, AxisY, AxisZ);
                world.UpsertCompoundCollider(new CombatPhysicsCompoundLeaf(new CombatColliderId(5000 + collider), 10, shape, priority: collider));
            }
            world.EndCompoundColliderUpdate();
        }

        private static void RunPerformanceFrame(CombatPhysicsWorld world, CombatPhysicsQuery query, List<CombatQueryResult> hits, Fix64 offset)
        {
            world.BeginColliderBatchUpdate();
            for (int character = 0; character < 8; character++)
            {
                var bodyId = new CombatBodyId(100 + character);
                for (int collider = 0; collider < 31; collider++)
                    world.UpsertObbCollider(CreatePerformanceCollider(bodyId, collider, offset));
            }
            world.EndColliderBatchUpdate();
            for (int queryIndex = 0; queryIndex < 100; queryIndex++)
            {
                hits.Clear();
                world.Query(query, hits);
            }
        }

        private static CombatPhysicsObbCollider CreatePerformanceCollider(CombatBodyId bodyId, int colliderIndex, Fix64 offset) =>
            new CombatPhysicsObbCollider(
                bodyId,
                new CombatColliderId(1000 + colliderIndex),
                10,
                new FixVector3(offset, Fix64.FromRatio(colliderIndex % 6, 2), Fix64.FromRatio((colliderIndex % 5) - 2, 2)),
                new FixVector3(Fix64.Half, Fix64.Half, Fix64.Half),
                AxisX,
                AxisY,
                AxisZ);

        private static CombatPhysicsWorld CreateWorld()
        {
            var world = new CombatPhysicsWorld();
            world.UpsertBody(new CombatPhysicsBody(EntityId, BodyId, FixVector3.Zero));
            world.UpsertObbCollider(CreateCollider(FixVector3.Zero));
            return world;
        }

        private static CombatPhysicsObbCollider CreateCollider(FixVector3 center) => new CombatPhysicsObbCollider(
            BodyId,
            ColliderId,
            10,
            center,
            new FixVector3(Fix64.One, Fix64.One, Fix64.One),
            AxisX,
            AxisY,
            AxisZ);

        private static CombatPhysicsQuery CreateQuery(CombatQueryKind kind)
        {
            var header = new CombatQueryHeader(1, kind, CombatEntityId.None, 1, 1, 1, CombatPhysicsLayerMask.FromLayer(10));
            switch (kind)
            {
                case CombatQueryKind.Ray:
                    return CombatPhysicsQuery.From(new CombatRayQuery(header, new FixVector3(-Fix64.FromInt(3), Fix64.Zero, Fix64.Zero), AxisX, Fix64.FromInt(6)));
                case CombatQueryKind.Sphere:
                    return CombatPhysicsQuery.From(new CombatSphereQuery(header, new FixVector3(Fix64.FromRatio(3, 2), Fix64.Zero, Fix64.Zero), Fix64.One));
                case CombatQueryKind.Capsule:
                    return CombatPhysicsQuery.From(new CombatCapsuleQuery(header, new FixVector3(-Fix64.FromInt(2), Fix64.Zero, Fix64.Zero), new FixVector3(Fix64.FromInt(2), Fix64.Zero, Fix64.Zero), Fix64.FromRatio(1, 4)));
                case CombatQueryKind.Aabb:
                    return CombatPhysicsQuery.From(new CombatAabbQuery(header, new FixVector3(-Fix64.Half, -Fix64.Half, -Fix64.Half), new FixVector3(Fix64.Half, Fix64.Half, Fix64.Half)));
                case CombatQueryKind.Sector:
                    return CombatPhysicsQuery.From(new CombatSectorQuery(header, new FixVector3(-Fix64.FromInt(3), Fix64.Zero, Fix64.Zero), AxisX, Fix64.FromInt(5), Fix64.Zero));
                default:
                    return CombatPhysicsQuery.From(new CombatObbQuery(header, FixVector3.Zero, new FixVector3(Fix64.Half, Fix64.Half, Fix64.Half), AxisX, AxisY, AxisZ));
            }
        }
    }
}
