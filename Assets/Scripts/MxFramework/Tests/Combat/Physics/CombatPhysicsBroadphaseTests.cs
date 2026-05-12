using System.Collections.Generic;
using MxFramework.Combat.Core;
using MxFramework.Combat.Physics;
using MxFramework.Core.Math;
using NUnit.Framework;

namespace MxFramework.Tests.Combat.Physics
{
    public sealed class CombatPhysicsBroadphaseTests
    {
        [Test]
        public void Broadphase_ReducesCandidatesButMatchesFullScanOracle()
        {
            CombatPhysicsWorld world = CreateLineWorld(120, spacing: 8);
            var query = new CombatAabbQuery(
                Header(CombatQueryKind.Aabb),
                new FixVector3(Fix64.FromInt(78), -Fix64.One, -Fix64.One),
                new FixVector3(Fix64.FromInt(82), Fix64.One, Fix64.One));
            var oracle = new List<CombatQueryResult>();
            var broadphase = new List<CombatQueryResult>();

            world.QueryAabb(query, oracle);
            world.Query(CombatPhysicsQuery.From(query), broadphase);
            CombatPhysicsQueryDebugReport report = world.ExplainQuery(CombatPhysicsQuery.From(query));

            AssertResultsEqual(oracle, broadphase);
            Assert.Less(report.CandidateCount, world.ColliderCount);
            Assert.Less(report.PostFilterCandidateCount, world.ColliderCount);
            Assert.GreaterOrEqual(report.BroadphaseRawCandidateCount, report.BroadphaseCandidateCount);
            Assert.AreEqual(report.CandidateCount, report.BroadphaseCandidateCount);
            Assert.AreEqual(report.HitCount, broadphase.Count);
        }

        [Test]
        public void Broadphase_AllQueryShapesMatchFullScanOracle()
        {
            CombatPhysicsWorld world = CreateLineWorld(30, spacing: 3);

            AssertMatchesOracle(world, new CombatRayQuery(
                Header(CombatQueryKind.Ray),
                new FixVector3(Fix64.FromInt(-2), Fix64.Zero, Fix64.Zero),
                UnitX,
                Fix64.FromInt(18)));
            AssertMatchesOracle(world, new CombatSphereQuery(
                Header(CombatQueryKind.Sphere),
                new FixVector3(Fix64.FromInt(15), Fix64.Zero, Fix64.Zero),
                Fix64.FromInt(4)));
            AssertMatchesOracle(world, new CombatCapsuleQuery(
                Header(CombatQueryKind.Capsule),
                new FixVector3(Fix64.FromInt(4), Fix64.Zero, Fix64.Zero),
                new FixVector3(Fix64.FromInt(16), Fix64.Zero, Fix64.Zero),
                Fix64.Half));
            AssertMatchesOracle(world, new CombatAabbQuery(
                Header(CombatQueryKind.Aabb),
                new FixVector3(Fix64.FromInt(8), -Fix64.One, -Fix64.One),
                new FixVector3(Fix64.FromInt(17), Fix64.One, Fix64.One)));
            AssertMatchesOracle(world, new CombatSectorQuery(
                Header(CombatQueryKind.Sector),
                new FixVector3(Fix64.FromInt(10), Fix64.Zero, Fix64.Zero),
                UnitX,
                Fix64.FromInt(12),
                Fix64.Zero));
        }

        [Test]
        public void Broadphase_RegistrationOrderDoesNotAffectCandidatesOrHits()
        {
            CombatPhysicsWorld forward = CreateGridWorld(reverseRegistration: false);
            CombatPhysicsWorld reverse = CreateGridWorld(reverseRegistration: true);
            var query = new CombatAabbQuery(
                Header(CombatQueryKind.Aabb),
                new FixVector3(Fix64.FromInt(-2), -Fix64.One, -Fix64.One),
                new FixVector3(Fix64.FromInt(38), Fix64.One, Fix64.One));
            var forwardHits = new List<CombatQueryResult>();
            var reverseHits = new List<CombatQueryResult>();

            forward.Query(CombatPhysicsQuery.From(query), forwardHits);
            reverse.Query(CombatPhysicsQuery.From(query), reverseHits);
            CombatPhysicsQueryDebugReport forwardReport = forward.ExplainQuery(CombatPhysicsQuery.From(query));
            CombatPhysicsQueryDebugReport reverseReport = reverse.ExplainQuery(CombatPhysicsQuery.From(query));

            AssertResultsEqual(forwardHits, reverseHits);
            Assert.AreEqual(forwardReport.CandidateCount, reverseReport.CandidateCount);
            Assert.AreEqual(forwardReport.Rows.Count, reverseReport.Rows.Count);
            for (int i = 0; i < forwardReport.Rows.Count; i++)
            {
                Assert.AreEqual(forwardReport.Rows[i].EntityId, reverseReport.Rows[i].EntityId);
                Assert.AreEqual(forwardReport.Rows[i].BodyId, reverseReport.Rows[i].BodyId);
                Assert.AreEqual(forwardReport.Rows[i].ColliderId, reverseReport.Rows[i].ColliderId);
                Assert.AreEqual(forwardReport.Rows[i].Status, reverseReport.Rows[i].Status);
            }
        }

        [Test]
        public void Broadphase_QueryBatchRemainsStable()
        {
            CombatPhysicsWorld forward = CreateGridWorld(reverseRegistration: false);
            CombatPhysicsWorld reverse = CreateGridWorld(reverseRegistration: true);
            var queries = new[]
            {
                CombatPhysicsQuery.From(new CombatAabbQuery(
                    Header(CombatQueryKind.Aabb, queryId: 20, sourceOrder: 20),
                    new FixVector3(Fix64.FromInt(10), -Fix64.One, -Fix64.One),
                    new FixVector3(Fix64.FromInt(24), Fix64.One, Fix64.One))),
                CombatPhysicsQuery.From(new CombatSphereQuery(
                    Header(CombatQueryKind.Sphere, queryId: 10, sourceOrder: 10),
                    new FixVector3(Fix64.FromInt(16), Fix64.Zero, Fix64.Zero),
                    Fix64.FromInt(6))),
            };
            var forwardResults = new List<CombatPhysicsQueryBatchResult>();
            var reverseResults = new List<CombatPhysicsQueryBatchResult>();

            forward.QueryBatch(new CombatPhysicsQueryBatch(queries), forwardResults);
            reverse.QueryBatch(new CombatPhysicsQueryBatch(queries), reverseResults);

            Assert.AreEqual(2, forwardResults.Count);
            Assert.AreEqual(10, forwardResults[0].Query.Header.SourceOrder);
            AssertBatchResultsEqual(forwardResults, reverseResults);
            Assert.Less(forwardResults[0].DebugReport.CandidateCount, forward.ColliderCount);
            Assert.Less(forwardResults[1].DebugReport.CandidateCount, forward.ColliderCount);
        }

        [Test]
        public void Broadphase_ReportSeparatesRawDedupFilterAndHits()
        {
            CombatPhysicsWorld world = new CombatPhysicsWorld(new CombatPhysicsBroadphaseConfig(Fix64.One));
            RegisterBodyWithAabb(world, entity: 1, body: 1, collider: 1, layer: 1, x: 0, halfSize: 2);
            RegisterBodyWithAabb(world, entity: 2, body: 2, collider: 1, layer: 1, x: 0, halfSize: 1);
            RegisterBodyWithAabb(world, entity: 3, body: 3, collider: 1, layer: 2, x: 0, halfSize: 1);
            RegisterBodyWithAabb(world, entity: 4, body: 4, collider: 1, layer: 1, x: 20, halfSize: 1);
            var query = new CombatAabbQuery(
                Header(CombatQueryKind.Aabb),
                new FixVector3(Fix64.FromInt(-1), -Fix64.One, -Fix64.One),
                new FixVector3(Fix64.FromInt(1), Fix64.One, Fix64.One));

            CombatPhysicsQueryDebugReport report = world.ExplainQuery(CombatPhysicsQuery.From(query));

            Assert.Greater(report.BroadphaseRawCandidateCount, report.BroadphaseCandidateCount);
            Assert.AreEqual(3, report.BroadphaseCandidateCount);
            Assert.AreEqual(1, report.FilteredSourceCount);
            Assert.AreEqual(1, report.FilteredLayerCount);
            Assert.AreEqual(1, report.PostFilterCandidateCount);
            Assert.AreEqual(1, report.HitCount);
            Assert.Greater(report.BroadphaseCellCount, 1);
        }

        [Test]
        public void Broadphase_ReportProvidesReadableSummaryAndCandidateReasons()
        {
            CombatPhysicsWorld world = new CombatPhysicsWorld(new CombatPhysicsBroadphaseConfig(Fix64.One));
            RegisterBodyWithAabb(world, entity: 1, body: 1, collider: 1, layer: 1, x: 0, halfSize: 1);
            RegisterBodyWithAabb(world, entity: 2, body: 2, collider: 1, layer: 1, x: 2, halfSize: 1);
            RegisterBodyWithAabb(world, entity: 3, body: 3, collider: 1, layer: 2, x: 1, halfSize: 1);
            RegisterBodyWithAabb(world, entity: 4, body: 4, collider: 1, layer: 1, x: -2, halfSize: 1);
            RegisterBodyWithAabb(world, entity: 5, body: 5, collider: 1, layer: 1, x: 20, halfSize: 1);
            var query = new CombatSectorQuery(
                Header(CombatQueryKind.Sector),
                FixVector3.Zero,
                UnitX,
                Fix64.FromInt(3),
                Fix64.Zero);

            CombatPhysicsQueryDebugReport report = world.ExplainQuery(CombatPhysicsQuery.From(query));

            Assert.AreEqual(4, report.CandidateCount);
            Assert.Greater(report.BroadphaseRawCandidateCount, report.BroadphaseCandidateCount);
            Assert.Greater(report.BroadphaseDuplicateCandidateCount, 0);
            Assert.Greater(report.BroadphaseDeduplicationPermille, 0);
            Assert.AreEqual(1, report.FilteredSourceCount);
            Assert.AreEqual(1, report.FilteredLayerCount);
            Assert.AreEqual(2, report.FilteredCandidateCount);
            Assert.AreEqual(2, report.NarrowphaseCandidateCount);
            Assert.AreEqual(1, report.HitCount);
            Assert.AreEqual(1, report.MissCount);

            Assert.AreEqual(4, report.SummaryLines.Count);
            StringAssert.Contains("shape=Sector", report.SummaryLines[0]);
            StringAssert.Contains("raw=", report.SummaryLines[1]);
            StringAssert.Contains("dedup=", report.SummaryLines[1]);
            StringAssert.Contains("source=1", report.SummaryLines[2]);
            StringAssert.Contains("layer=1", report.SummaryLines[2]);
            StringAssert.Contains("hit=1", report.SummaryLines[3]);
            StringAssert.Contains("miss=1", report.SummaryLines[3]);

            Assert.AreEqual(CombatPhysicsQueryDebugRowStatus.FilteredSource, report.Rows[0].Status);
            Assert.AreEqual("filtered-source", report.Rows[0].StatusToken);
            Assert.AreEqual("source entity excluded", report.Rows[0].StatusReason);
            Assert.IsTrue(report.Rows[0].WasFiltered);
            Assert.IsFalse(report.Rows[0].EnteredNarrowphase);
            StringAssert.Contains("candidate[0] entity=1", report.Rows[0].Label);
            StringAssert.Contains("status=filtered-source", report.Rows[0].Label);

            Assert.AreEqual(CombatPhysicsQueryDebugRowStatus.Hit, report.Rows[1].Status);
            Assert.IsTrue(report.Rows[1].EnteredNarrowphase);
            Assert.AreEqual("narrowphase accepted candidate", report.Rows[1].StatusReason);

            Assert.AreEqual(CombatPhysicsQueryDebugRowStatus.FilteredLayer, report.Rows[2].Status);
            Assert.AreEqual("layer mask rejected candidate", report.Rows[2].StatusReason);

            Assert.AreEqual(CombatPhysicsQueryDebugRowStatus.Miss, report.Rows[3].Status);
            Assert.IsTrue(report.Rows[3].EnteredNarrowphase);
            Assert.AreEqual("narrowphase rejected candidate", report.Rows[3].StatusReason);
            StringAssert.Contains("status=miss", report.Rows[3].Label);
        }

        private static void AssertMatchesOracle(CombatPhysicsWorld world, CombatRayQuery query)
        {
            var oracle = new List<CombatQueryResult>();
            var broadphase = new List<CombatQueryResult>();
            world.QueryRay(query, oracle);
            world.Query(CombatPhysicsQuery.From(query), broadphase);
            AssertResultsEqual(oracle, broadphase);
        }

        private static void AssertMatchesOracle(CombatPhysicsWorld world, CombatSphereQuery query)
        {
            var oracle = new List<CombatQueryResult>();
            var broadphase = new List<CombatQueryResult>();
            world.QuerySphere(query, oracle);
            world.Query(CombatPhysicsQuery.From(query), broadphase);
            AssertResultsEqual(oracle, broadphase);
        }

        private static void AssertMatchesOracle(CombatPhysicsWorld world, CombatCapsuleQuery query)
        {
            var oracle = new List<CombatQueryResult>();
            var broadphase = new List<CombatQueryResult>();
            world.QueryCapsule(query, oracle);
            world.Query(CombatPhysicsQuery.From(query), broadphase);
            AssertResultsEqual(oracle, broadphase);
        }

        private static void AssertMatchesOracle(CombatPhysicsWorld world, CombatAabbQuery query)
        {
            var oracle = new List<CombatQueryResult>();
            var broadphase = new List<CombatQueryResult>();
            world.QueryAabb(query, oracle);
            world.Query(CombatPhysicsQuery.From(query), broadphase);
            AssertResultsEqual(oracle, broadphase);
        }

        private static void AssertMatchesOracle(CombatPhysicsWorld world, CombatSectorQuery query)
        {
            var oracle = new List<CombatQueryResult>();
            var broadphase = new List<CombatQueryResult>();
            world.QuerySector(query, oracle);
            world.Query(CombatPhysicsQuery.From(query), broadphase);
            AssertResultsEqual(oracle, broadphase);
        }

        private static CombatPhysicsWorld CreateLineWorld(int count, int spacing)
        {
            var world = new CombatPhysicsWorld();
            for (int i = 0; i < count; i++)
            {
                RegisterBodyWithAabb(world, entity: i + 2, body: i + 2, collider: 1, layer: 1, x: i * spacing);
            }

            return world;
        }

        private static CombatPhysicsWorld CreateGridWorld(bool reverseRegistration)
        {
            var world = new CombatPhysicsWorld();
            if (reverseRegistration)
            {
                for (int i = 19; i >= 0; i--)
                {
                    RegisterBodyWithAabb(world, entity: i + 2, body: i + 2, collider: 1, layer: 1, x: i * 2);
                }
            }
            else
            {
                for (int i = 0; i < 20; i++)
                {
                    RegisterBodyWithAabb(world, entity: i + 2, body: i + 2, collider: 1, layer: 1, x: i * 2);
                }
            }

            return world;
        }

        private static void RegisterBodyWithAabb(
            CombatPhysicsWorld world,
            int entity,
            int body,
            int collider,
            int layer,
            int x,
            int halfSize = 1)
        {
            world.UpsertBody(new CombatPhysicsBody(
                new CombatEntityId(entity),
                new CombatBodyId(body),
                new FixVector3(Fix64.FromInt(x), Fix64.Zero, Fix64.Zero)));
            world.UpsertAabbCollider(new CombatPhysicsAabbCollider(
                new CombatBodyId(body),
                new CombatColliderId(collider),
                layer,
                new FixVector3(-Fix64.FromInt(halfSize), -Fix64.Half, -Fix64.Half),
                new FixVector3(Fix64.FromInt(halfSize), Fix64.Half, Fix64.Half)));
        }

        private static void AssertBatchResultsEqual(
            IReadOnlyList<CombatPhysicsQueryBatchResult> expected,
            IReadOnlyList<CombatPhysicsQueryBatchResult> actual)
        {
            Assert.AreEqual(expected.Count, actual.Count);
            for (int i = 0; i < expected.Count; i++)
            {
                Assert.AreEqual(expected[i].Query, actual[i].Query);
                Assert.AreEqual(expected[i].HitCount, actual[i].HitCount);
                AssertResultsEqual(expected[i].Hits, actual[i].Hits);
            }
        }

        private static void AssertResultsEqual(
            IReadOnlyList<CombatQueryResult> expected,
            IReadOnlyList<CombatQueryResult> actual)
        {
            Assert.AreEqual(expected.Count, actual.Count);
            for (int i = 0; i < expected.Count; i++)
            {
                Assert.AreEqual(expected[i], actual[i]);
            }
        }

        private static FixVector3 UnitX => new FixVector3(Fix64.One, Fix64.Zero, Fix64.Zero);

        private static CombatQueryHeader Header(
            CombatQueryKind kind,
            int queryId = 1,
            int sourceOrder = 7)
        {
            return new CombatQueryHeader(
                queryId,
                kind,
                new CombatEntityId(1),
                traceId: 5,
                actionId: 6,
                sourceOrder,
                CombatPhysicsLayerMask.FromLayer(1));
        }
    }
}
