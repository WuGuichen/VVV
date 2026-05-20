using System;
using System.Collections.Generic;
using MxFramework.Combat.Core;
using MxFramework.Combat.Physics;
using MxFramework.Core.Math;
using NUnit.Framework;

namespace MxFramework.Tests.Combat.Physics
{
    public class CombatQueryContractTests
    {
        [Test]
        public void Headers_RejectNegativeIds()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => Header(CombatQueryKind.Ray, queryId: -1));
            Assert.Throws<ArgumentOutOfRangeException>(() => new CombatQueryHeader(
                1,
                CombatQueryKind.Ray,
                new CombatEntityId(1),
                -1,
                1,
                1,
                CombatPhysicsLayerMask.All));
        }

        [Test]
        public void LayerMask_UsesExplicitLayerBits()
        {
            CombatPhysicsLayerMask mask = CombatPhysicsLayerMask.FromLayer(2).AddLayer(5);

            Assert.IsTrue(mask.ContainsLayer(2));
            Assert.IsTrue(mask.ContainsLayer(5));
            Assert.IsFalse(mask.ContainsLayer(3));
            Assert.Throws<ArgumentOutOfRangeException>(() => mask.ContainsLayer(32));
        }

        [Test]
        public void Queries_ValidateKindAndShape()
        {
            Assert.Throws<ArgumentException>(() => new CombatRayQuery(
                Header(CombatQueryKind.Sphere),
                FixVector3.Zero,
                UnitX,
                Fix64.One));
            Assert.Throws<ArgumentException>(() => new CombatRayQuery(
                Header(CombatQueryKind.Ray),
                FixVector3.Zero,
                FixVector3.Zero,
                Fix64.One));
            Assert.Throws<ArgumentOutOfRangeException>(() => new CombatSphereQuery(
                Header(CombatQueryKind.Sphere),
                FixVector3.Zero,
                -Fix64.One));
            Assert.Throws<ArgumentException>(() => new CombatAabbQuery(
                Header(CombatQueryKind.Aabb),
                new FixVector3(Fix64.One, Fix64.Zero, Fix64.Zero),
                FixVector3.Zero));
            Assert.Throws<ArgumentException>(() => new CombatSectorQuery(
                Header(CombatQueryKind.Sector),
                FixVector3.Zero,
                FixVector3.Zero,
                Fix64.One,
                Fix64.Zero));
            Assert.Throws<ArgumentOutOfRangeException>(() => new CombatSectorQuery(
                Header(CombatQueryKind.Sector),
                FixVector3.Zero,
                UnitX,
                Fix64.One,
                Fix64.FromInt(2)));
        }

        [Test]
        public void PhysicsShape_ValidatesShapeFields()
        {
            Assert.Throws<ArgumentException>(() => CombatPhysicsShape.Ray(FixVector3.Zero, FixVector3.Zero, Fix64.One));
            Assert.Throws<ArgumentOutOfRangeException>(() => CombatPhysicsShape.Sphere(FixVector3.Zero, -Fix64.One));
            Assert.Throws<ArgumentException>(() => CombatPhysicsShape.Aabb(UnitX, FixVector3.Zero));
            Assert.Throws<ArgumentOutOfRangeException>(() => CombatPhysicsShape.Sector(FixVector3.Zero, UnitX, Fix64.One, Fix64.FromInt(2)));
            Assert.Throws<ArgumentOutOfRangeException>(() => CombatPhysicsShape.Obb(
                FixVector3.Zero,
                new FixVector3(-Fix64.One, Fix64.One, Fix64.One),
                UnitX,
                UnitY,
                UnitZ));
            Assert.Throws<ArgumentException>(() => CombatPhysicsShape.Obb(
                FixVector3.Zero,
                new FixVector3(Fix64.One, Fix64.One, Fix64.One),
                FixVector3.Zero,
                UnitY,
                UnitZ));
            Assert.Throws<ArgumentException>(() => new CombatObbQuery(
                Header(CombatQueryKind.Aabb),
                FixVector3.Zero,
                new FixVector3(Fix64.One, Fix64.One, Fix64.One),
                UnitX,
                UnitY,
                UnitZ));
        }

        [Test]
        public void UnifiedQuery_RequiresHeaderAndShapeKindMatch()
        {
            Assert.Throws<ArgumentException>(() => new CombatPhysicsQuery(
                Header(CombatQueryKind.Ray),
                CombatPhysicsShape.Sphere(FixVector3.Zero, Fix64.One)));

            var sphere = new CombatSphereQuery(
                Header(CombatQueryKind.Sphere),
                FixVector3.Zero,
                Fix64.One);

            CombatPhysicsQuery unified = CombatPhysicsQuery.From(sphere);

            Assert.AreEqual(CombatPhysicsShapeKind.Sphere, unified.Shape.Kind);
            Assert.AreEqual(sphere.Header, unified.Header);
            Assert.AreEqual(sphere.Center, unified.ToSphereQuery().Center);
            Assert.AreEqual(sphere.Radius, unified.ToSphereQuery().Radius);
        }

        [Test]
        public void ObbQuery_NormalizesAxesAndRoundtripsThroughUnifiedQuery()
        {
            var query = new CombatObbQuery(
                Header(CombatQueryKind.Obb),
                new FixVector3(Fix64.FromInt(2), Fix64.FromInt(3), Fix64.FromInt(4)),
                new FixVector3(Fix64.FromInt(3), Fix64.FromInt(2), Fix64.One),
                new FixVector3(Fix64.FromInt(2), Fix64.Zero, Fix64.Zero),
                new FixVector3(Fix64.Zero, Fix64.FromInt(3), Fix64.Zero),
                new FixVector3(Fix64.Zero, Fix64.Zero, Fix64.FromInt(4)));

            CombatPhysicsQuery unified = CombatPhysicsQuery.From(query);
            CombatObbQuery roundtrip = unified.ToObbQuery();

            Assert.AreEqual(CombatPhysicsShapeKind.Obb, unified.Shape.Kind);
            Assert.AreEqual(query.Header, unified.Header);
            Assert.AreEqual(UnitX, query.AxisX);
            Assert.AreEqual(UnitY, query.AxisY);
            Assert.AreEqual(UnitZ, query.AxisZ);
            Assert.AreEqual(query.Center, roundtrip.Center);
            Assert.AreEqual(query.HalfExtents, roundtrip.HalfExtents);
            Assert.AreEqual(UnitX, roundtrip.AxisX);
            Assert.AreEqual(UnitY, roundtrip.AxisY);
            Assert.AreEqual(UnitZ, roundtrip.AxisZ);
        }

        [Test]
        public void QueryFilter_ReservesOwnerAndTeamContractWithoutGameplayDependency()
        {
            var filter = new CombatPhysicsQueryFilter(
                includeSourceEntity: true,
                ownerId: 10,
                teamId: 2,
                targetOwnerMask: 4,
                targetTeamMask: 8);

            Assert.IsTrue(filter.IncludeSourceEntity);
            Assert.AreEqual(10, filter.OwnerId);
            Assert.AreEqual(2, filter.TeamId);
            Assert.AreEqual(4, filter.TargetOwnerMask);
            Assert.AreEqual(8, filter.TargetTeamMask);
            Assert.Throws<ArgumentOutOfRangeException>(() => new CombatPhysicsQueryFilter(false, ownerId: -1));
            Assert.Throws<ArgumentOutOfRangeException>(() => new CombatPhysicsQueryFilter(false, teamId: -1));
        }

        [Test]
        public void ResultSort_IsDeterministicAcrossDistanceAndTargetIds()
        {
            CombatQueryHeader query = Header(CombatQueryKind.Ray);
            var results = new List<CombatQueryResult>
            {
                Result(query, distanceRaw: 2000000, entity: 2, body: 1, collider: 1),
                Result(query, distanceRaw: 1000000, entity: 3, body: 1, collider: 1),
                Result(query, distanceRaw: 1000000, entity: 1, body: 4, collider: 1),
                Result(query, distanceRaw: 1000000, entity: 1, body: 2, collider: 3),
                Result(query, distanceRaw: 1000000, entity: 1, body: 2, collider: 2),
            };

            new CombatPhysicsWorld().SortResults(results);

            Assert.AreEqual(1, results[0].TargetEntityId.Value);
            Assert.AreEqual(2, results[0].TargetBodyId.Value);
            Assert.AreEqual(2, results[0].TargetColliderId.Value);
            Assert.AreEqual(1, results[1].TargetEntityId.Value);
            Assert.AreEqual(2, results[1].TargetBodyId.Value);
            Assert.AreEqual(3, results[1].TargetColliderId.Value);
            Assert.AreEqual(1, results[2].TargetEntityId.Value);
            Assert.AreEqual(4, results[2].TargetBodyId.Value);
            Assert.AreEqual(3, results[3].TargetEntityId.Value);
            Assert.AreEqual(2, results[4].TargetEntityId.Value);
        }

        [Test]
        public void ResultSortKey_UsesDistanceAsPrimary()
        {
            CombatQueryHeader query = Header(CombatQueryKind.Ray);
            CombatQueryResult result = Result(query, distanceRaw: 123456, entity: 7, body: 8, collider: 9);

            CombatSortKey sortKey = result.ToSortKey();

            Assert.AreEqual(123456, sortKey.Primary);
            Assert.AreEqual(7, sortKey.EntityId.Value);
            Assert.AreEqual(8, sortKey.BodyId.Value);
            Assert.AreEqual(9, sortKey.ColliderId.Value);
            Assert.AreEqual(query.TraceId, sortKey.TraceId);
            Assert.AreEqual(query.ActionId, sortKey.ActionId);
            Assert.AreEqual(query.SourceOrder, sortKey.SourceOrder);
        }

        private static FixVector3 UnitX => new FixVector3(Fix64.One, Fix64.Zero, Fix64.Zero);

        private static FixVector3 UnitY => new FixVector3(Fix64.Zero, Fix64.One, Fix64.Zero);

        private static FixVector3 UnitZ => new FixVector3(Fix64.Zero, Fix64.Zero, Fix64.One);

        private static CombatQueryHeader Header(CombatQueryKind kind, int queryId = 1)
        {
            return new CombatQueryHeader(
                queryId,
                kind,
                new CombatEntityId(100),
                traceId: 5,
                actionId: 6,
                sourceOrder: 7,
                CombatPhysicsLayerMask.All);
        }

        private static CombatQueryResult Result(
            CombatQueryHeader query,
            int distanceRaw,
            int entity,
            int body,
            int collider)
        {
            return new CombatQueryResult(
                query,
                new CombatEntityId(entity),
                new CombatBodyId(body),
                new CombatColliderId(collider),
                Fix64.FromRaw(distanceRaw),
                FixVector3.Zero,
                UnitX);
        }
    }
}
