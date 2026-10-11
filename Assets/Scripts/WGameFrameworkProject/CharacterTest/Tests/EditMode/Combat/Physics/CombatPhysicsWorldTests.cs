using System;
using System.Collections.Generic;
using MxFramework.Combat.Core;
using MxFramework.Combat.Physics;
using MxFramework.Core.Math;
using NUnit.Framework;

namespace MxFramework.Tests.Combat.Physics
{
    public class CombatPhysicsWorldTests
    {
        [Test]
        public void Register_RejectsInvalidBodyAndCollider()
        {
            Assert.Throws<ArgumentException>(() => new CombatPhysicsBody(
                CombatEntityId.None,
                new CombatBodyId(1),
                FixVector3.Zero));
            Assert.Throws<ArgumentException>(() => new CombatPhysicsBody(
                new CombatEntityId(1),
                CombatBodyId.None,
                FixVector3.Zero));
            Assert.Throws<ArgumentException>(() => new CombatPhysicsAabbCollider(
                new CombatBodyId(1),
                CombatColliderId.None,
                layer: 0,
                localMin: FixVector3.Zero,
                localMax: FixVector3.Zero));
            Assert.Throws<InvalidOperationException>(() => new CombatPhysicsWorld().UpsertAabbCollider(
                new CombatPhysicsAabbCollider(
                    new CombatBodyId(1),
                    new CombatColliderId(1),
                    layer: 0,
                    localMin: FixVector3.Zero,
                    localMax: FixVector3.Zero)));
            Assert.Throws<ArgumentOutOfRangeException>(() => new CombatPhysicsSphereCollider(
                new CombatBodyId(1),
                new CombatColliderId(1),
                layer: 0,
                localCenter: FixVector3.Zero,
                radius: -Fix64.One));
            Assert.Throws<ArgumentOutOfRangeException>(() => new CombatPhysicsCapsuleCollider(
                new CombatBodyId(1),
                new CombatColliderId(1),
                layer: 0,
                localPointA: FixVector3.Zero,
                localPointB: FixVector3.Zero,
                radius: -Fix64.One));
            Assert.Throws<InvalidOperationException>(() => new CombatPhysicsWorld().UpsertSphereCollider(
                new CombatPhysicsSphereCollider(
                    new CombatBodyId(1),
                    new CombatColliderId(1),
                    layer: 0,
                    localCenter: FixVector3.Zero,
                    radius: Fix64.One)));
            Assert.Throws<InvalidOperationException>(() => new CombatPhysicsWorld().UpsertCapsuleCollider(
                new CombatPhysicsCapsuleCollider(
                    new CombatBodyId(1),
                    new CombatColliderId(1),
                    layer: 0,
                    localPointA: FixVector3.Zero,
                    localPointB: FixVector3.Zero,
                    radius: Fix64.One)));
        }

        [Test]
        public void AabbCollider_FrictionOnlyConstructorCreatesDefaultSurfaceMaterial()
        {
            var collider = new CombatPhysicsAabbCollider(
                new CombatBodyId(1),
                new CombatColliderId(1),
                layer: 0,
                localMin: FixVector3.Zero,
                localMax: new FixVector3(Fix64.One, Fix64.One, Fix64.One),
                surfaceFriction: Fix64.FromInt(17));

            Assert.AreEqual(CombatSurfaceMaterialKind.Default, collider.SurfaceMaterialKind);
            Assert.AreEqual("default", collider.SurfaceMaterialStableId);
            Assert.AreEqual(Fix64.FromInt(17), collider.SurfaceFriction);
            Assert.AreEqual(Fix64.Zero, collider.SurfaceElasticity);
            Assert.AreEqual(Fix64.Zero, collider.SurfaceFixedBounceHeight);
            Assert.AreEqual(collider.SurfaceFriction, collider.SurfaceMaterial.Friction);
        }

        [Test]
        public void AabbCollider_SurfaceMaterialCarriesKindFrictionElasticityAndFixedBounceHeight()
        {
            var material = new CombatSurfaceMaterial(
                CombatSurfaceMaterialKind.BouncePad,
                Fix64.FromInt(12),
                Fix64.Half,
                Fix64.FromInt(4));
            var collider = new CombatPhysicsAabbCollider(
                new CombatBodyId(1),
                new CombatColliderId(1),
                layer: 0,
                localMin: FixVector3.Zero,
                localMax: new FixVector3(Fix64.One, Fix64.One, Fix64.One),
                surfaceMaterial: material);

            Assert.AreEqual(material, collider.SurfaceMaterial);
            Assert.AreEqual(CombatSurfaceMaterialKind.BouncePad, collider.SurfaceMaterialKind);
            Assert.AreEqual("bounce_pad", collider.SurfaceMaterialStableId);
            Assert.AreEqual(Fix64.FromInt(12), collider.SurfaceFriction);
            Assert.AreEqual(Fix64.Half, collider.SurfaceElasticity);
            Assert.AreEqual(Fix64.FromInt(4), collider.SurfaceFixedBounceHeight);
        }

        [Test]
        public void SurfaceMaterial_RejectsNegativeFrictionOutOfRangeElasticityAndNegativeFixedBounceHeight()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new CombatSurfaceMaterial(
                CombatSurfaceMaterialKind.Default,
                -Fix64.One,
                Fix64.Zero));
            Assert.Throws<ArgumentOutOfRangeException>(() => new CombatSurfaceMaterial(
                CombatSurfaceMaterialKind.Default,
                Fix64.Zero,
                -Fix64.One));
            Assert.Throws<ArgumentOutOfRangeException>(() => new CombatSurfaceMaterial(
                CombatSurfaceMaterialKind.Default,
                Fix64.Zero,
                Fix64.One + Fix64.One));
            Assert.Throws<ArgumentOutOfRangeException>(() => new CombatSurfaceMaterial(
                CombatSurfaceMaterialKind.BouncePad,
                Fix64.Zero,
                Fix64.Zero,
                -Fix64.One));
        }

        [Test]
        public void SphereAndCapsuleColliders_CarryMaterialAndWorldBounds()
        {
            var bodyPosition = new FixVector3(Fix64.FromInt(10), Fix64.FromInt(2), Fix64.Zero);
            var sphere = new CombatPhysicsSphereCollider(
                new CombatBodyId(2),
                new CombatColliderId(1),
                layer: 1,
                localCenter: new FixVector3(Fix64.One, Fix64.Zero, Fix64.Zero),
                radius: Fix64.Half,
                surfaceMaterial: CombatSurfaceMaterial.Elastic);
            var capsule = new CombatPhysicsCapsuleCollider(
                new CombatBodyId(2),
                new CombatColliderId(2),
                layer: 1,
                localPointA: new FixVector3(Fix64.Zero, -Fix64.One, Fix64.Zero),
                localPointB: new FixVector3(Fix64.Zero, Fix64.One, Fix64.Zero),
                radius: Fix64.Half,
                surfaceMaterial: CombatSurfaceMaterial.Rough);

            Assert.AreEqual(CombatSurfaceMaterialKind.Elastic, sphere.SurfaceMaterialKind);
            Assert.AreEqual("elastic", sphere.SurfaceMaterialStableId);
            Assert.AreEqual(CombatSurfaceMaterial.Elastic, sphere.SurfaceMaterial);
            Assert.AreEqual(new FixVector3(Fix64.FromInt(11), Fix64.FromInt(2), Fix64.Zero), sphere.GetWorldCenter(bodyPosition));
            Assert.AreEqual(new FixVector3(Fix64.FromRatio(21, 2), Fix64.FromRatio(3, 2), -Fix64.Half), sphere.GetWorldMin(bodyPosition));
            Assert.AreEqual(new FixVector3(Fix64.FromRatio(23, 2), Fix64.FromRatio(5, 2), Fix64.Half), sphere.GetWorldMax(bodyPosition));
            Assert.AreEqual(CombatSurfaceMaterialKind.Rough, capsule.SurfaceMaterialKind);
            Assert.AreEqual("rough", capsule.SurfaceMaterialStableId);
            Assert.AreEqual(CombatSurfaceMaterial.Rough, capsule.SurfaceMaterial);
            Assert.AreEqual(new FixVector3(Fix64.FromInt(10), Fix64.One, Fix64.Zero), capsule.GetWorldPointA(bodyPosition));
            Assert.AreEqual(new FixVector3(Fix64.FromInt(10), Fix64.FromInt(3), Fix64.Zero), capsule.GetWorldPointB(bodyPosition));
            Assert.AreEqual(new FixVector3(Fix64.FromRatio(19, 2), Fix64.Half, -Fix64.Half), capsule.GetWorldMin(bodyPosition));
            Assert.AreEqual(new FixVector3(Fix64.FromRatio(21, 2), Fix64.FromRatio(7, 2), Fix64.Half), capsule.GetWorldMax(bodyPosition));
        }

        [Test]
        public void SphereAndCapsuleRegistry_UpsertsRemovesCopiesAndAggregatesStats()
        {
            var world = new CombatPhysicsWorld();
            world.UpsertBody(new CombatPhysicsBody(new CombatEntityId(4), new CombatBodyId(4), FixVector3.Zero));
            world.UpsertBody(new CombatPhysicsBody(new CombatEntityId(2), new CombatBodyId(2), FixVector3.Zero));
            var sphereA = new CombatPhysicsSphereCollider(
                new CombatBodyId(4),
                new CombatColliderId(2),
                layer: 1,
                localCenter: FixVector3.Zero,
                radius: Fix64.Half);
            var sphereB = new CombatPhysicsSphereCollider(
                new CombatBodyId(2),
                new CombatColliderId(3),
                layer: 1,
                localCenter: FixVector3.Zero,
                radius: Fix64.Half);
            var sphereC = new CombatPhysicsSphereCollider(
                new CombatBodyId(2),
                new CombatColliderId(1),
                layer: 1,
                localCenter: FixVector3.Zero,
                radius: Fix64.Half);
            var capsuleA = new CombatPhysicsCapsuleCollider(
                new CombatBodyId(4),
                new CombatColliderId(4),
                layer: 1,
                localPointA: -UnitY,
                localPointB: UnitY,
                radius: Fix64.Half);
            var capsuleB = new CombatPhysicsCapsuleCollider(
                new CombatBodyId(2),
                new CombatColliderId(5),
                layer: 1,
                localPointA: -UnitY,
                localPointB: UnitY,
                radius: Fix64.Half);

            world.UpsertSphereCollider(sphereA);
            world.UpsertSphereCollider(sphereB);
            world.UpsertSphereCollider(sphereC);
            world.UpsertCapsuleCollider(capsuleA);
            world.UpsertCapsuleCollider(capsuleB);
            var spheres = new List<CombatPhysicsSphereCollider>
            {
                new CombatPhysicsSphereCollider(new CombatBodyId(99), new CombatColliderId(99), 1, FixVector3.Zero, Fix64.Half),
            };
            var capsules = new List<CombatPhysicsCapsuleCollider>
            {
                new CombatPhysicsCapsuleCollider(new CombatBodyId(99), new CombatColliderId(99), 1, -UnitY, UnitY, Fix64.Half),
            };

            Assert.AreEqual(5, world.ColliderCount);
            Assert.AreEqual(new CombatPhysicsWorldStats(world.Revision, 2, 5), world.CreateStats());
            Assert.IsTrue(world.TryGetSphereCollider(new CombatBodyId(2), new CombatColliderId(1), out CombatPhysicsSphereCollider foundSphere));
            Assert.AreEqual(sphereC, foundSphere);
            Assert.IsTrue(world.TryGetCapsuleCollider(new CombatBodyId(4), new CombatColliderId(4), out CombatPhysicsCapsuleCollider foundCapsule));
            Assert.AreEqual(capsuleA, foundCapsule);
            Assert.AreEqual(3, world.CopySphereCollidersTo(spheres));
            Assert.AreEqual(2, world.CopyCapsuleCollidersTo(capsules));
            Assert.AreEqual(99, spheres[0].BodyId.Value);
            Assert.AreEqual(2, spheres[1].BodyId.Value);
            Assert.AreEqual(1, spheres[1].ColliderId.Value);
            Assert.AreEqual(2, spheres[2].BodyId.Value);
            Assert.AreEqual(3, spheres[2].ColliderId.Value);
            Assert.AreEqual(4, spheres[3].BodyId.Value);
            Assert.AreEqual(2, spheres[3].ColliderId.Value);
            Assert.AreEqual(99, capsules[0].BodyId.Value);
            Assert.AreEqual(2, capsules[1].BodyId.Value);
            Assert.AreEqual(5, capsules[1].ColliderId.Value);
            Assert.AreEqual(4, capsules[2].BodyId.Value);
            Assert.AreEqual(4, capsules[2].ColliderId.Value);

            Assert.IsTrue(world.RemoveCollider(new CombatBodyId(2), new CombatColliderId(3)));
            Assert.AreEqual(4, world.ColliderCount);
            Assert.IsFalse(world.TryGetSphereCollider(new CombatBodyId(2), new CombatColliderId(3), out _));
        }

        [Test]
        public void ColliderRegistry_EnforcesUniqueBodyColliderAcrossShapes()
        {
            var world = new CombatPhysicsWorld();
            var bodyId = new CombatBodyId(2);
            var colliderId = new CombatColliderId(1);
            world.UpsertBody(new CombatPhysicsBody(new CombatEntityId(2), bodyId, FixVector3.Zero));

            world.UpsertAabbCollider(new CombatPhysicsAabbCollider(
                bodyId,
                colliderId,
                layer: 1,
                localMin: FixVector3.Zero,
                localMax: FixVector3.Zero));
            Assert.AreEqual(1, world.ColliderCount);
            Assert.IsTrue(world.TryGetAabbCollider(bodyId, colliderId, out _));

            world.UpsertSphereCollider(new CombatPhysicsSphereCollider(
                bodyId,
                colliderId,
                layer: 1,
                localCenter: FixVector3.Zero,
                radius: Fix64.Half));
            Assert.AreEqual(1, world.ColliderCount);
            Assert.IsFalse(world.TryGetAabbCollider(bodyId, colliderId, out _));
            Assert.IsTrue(world.TryGetSphereCollider(bodyId, colliderId, out _));

            world.UpsertCapsuleCollider(new CombatPhysicsCapsuleCollider(
                bodyId,
                colliderId,
                layer: 1,
                localPointA: -UnitY,
                localPointB: UnitY,
                radius: Fix64.Half));
            Assert.AreEqual(1, world.ColliderCount);
            Assert.IsFalse(world.TryGetSphereCollider(bodyId, colliderId, out _));
            Assert.IsTrue(world.TryGetCapsuleCollider(bodyId, colliderId, out _));

            Assert.IsTrue(world.RemoveCollider(bodyId, colliderId));
            Assert.AreEqual(0, world.ColliderCount);
            Assert.IsFalse(world.TryGetCapsuleCollider(bodyId, colliderId, out _));
        }

        [Test]
        public void BodyLookup_RemainsValidAfterRemovingMiddleBody()
        {
            var world = new CombatPhysicsWorld();
            RegisterBodyWithAabb(world, entity: 1, body: 1, collider: 1, layer: 1, x: 0);
            RegisterBodyWithAabb(world, entity: 2, body: 2, collider: 1, layer: 1, x: 2);
            RegisterBodyWithSphereAt(
                world,
                entity: 3,
                body: 3,
                collider: 1,
                layer: 1,
                x: Fix64.FromInt(3),
                radius: Fix64.Half);

            Assert.IsTrue(world.RemoveBody(new CombatBodyId(2)));
            Assert.IsFalse(world.TryGetBody(new CombatBodyId(2), out _));
            Assert.IsFalse(world.TryGetAabbCollider(new CombatBodyId(2), new CombatColliderId(1), out _));
            Assert.IsTrue(world.SetBodyPosition(new CombatBodyId(3), new FixVector3(Fix64.FromInt(5), Fix64.Zero, Fix64.Zero)));

            Assert.IsTrue(world.TryGetBody(new CombatBodyId(3), out CombatPhysicsBody body));
            Assert.AreEqual(new FixVector3(Fix64.FromInt(5), Fix64.Zero, Fix64.Zero), body.Position);

            var query = new CombatAabbQuery(
                Header(CombatQueryKind.Aabb, CombatPhysicsLayerMask.FromLayer(1)),
                new FixVector3(Fix64.FromInt(4), -Fix64.One, -Fix64.One),
                new FixVector3(Fix64.FromInt(6), Fix64.One, Fix64.One));
            var hits = new List<CombatQueryResult>();

            Assert.AreEqual(1, world.QueryAabb(query, hits));
            Assert.AreEqual(3, hits[0].TargetEntityId.Value);
            Assert.AreEqual(CombatPhysicsShapeKind.Sphere, hits[0].TargetShapeKind);
        }

        [Test]
        public void ColliderLookup_RebuildsWhenRemovingAndReplacingShapes()
        {
            var world = new CombatPhysicsWorld();
            RegisterBodyWithSphereAt(
                world,
                entity: 1,
                body: 1,
                collider: 1,
                layer: 1,
                x: Fix64.Zero,
                radius: Fix64.Half);
            RegisterBodyWithSphereAt(
                world,
                entity: 2,
                body: 2,
                collider: 1,
                layer: 1,
                x: Fix64.FromInt(2),
                radius: Fix64.Half);
            RegisterBodyWithSphereAt(
                world,
                entity: 3,
                body: 3,
                collider: 1,
                layer: 1,
                x: Fix64.FromInt(4),
                radius: Fix64.Half);

            world.UpsertAabbCollider(new CombatPhysicsAabbCollider(
                new CombatBodyId(2),
                new CombatColliderId(1),
                layer: 1,
                localMin: new FixVector3(-Fix64.One, -Fix64.One, -Fix64.One),
                localMax: new FixVector3(Fix64.One, Fix64.One, Fix64.One)));

            Assert.IsFalse(world.TryGetSphereCollider(new CombatBodyId(2), new CombatColliderId(1), out _));
            Assert.IsTrue(world.TryGetAabbCollider(new CombatBodyId(2), new CombatColliderId(1), out _));
            Assert.IsTrue(world.TryGetSphereCollider(new CombatBodyId(3), new CombatColliderId(1), out _));

            Assert.IsTrue(world.RemoveCollider(new CombatBodyId(1), new CombatColliderId(1)));
            Assert.IsTrue(world.TryGetSphereCollider(new CombatBodyId(3), new CombatColliderId(1), out _));

            world.UpsertCapsuleCollider(new CombatPhysicsCapsuleCollider(
                new CombatBodyId(3),
                new CombatColliderId(1),
                layer: 1,
                localPointA: -UnitY,
                localPointB: UnitY,
                radius: Fix64.Half));

            Assert.IsFalse(world.TryGetSphereCollider(new CombatBodyId(3), new CombatColliderId(1), out _));
            Assert.IsTrue(world.TryGetCapsuleCollider(new CombatBodyId(3), new CombatColliderId(1), out _));
        }

        [Test]
        public void QueryAabb_FiltersLayerAndSourceEntity()
        {
            CombatPhysicsWorld world = CreateWorld();
            var results = new List<CombatQueryResult>();
            var query = new CombatAabbQuery(
                Header(CombatQueryKind.Aabb, CombatPhysicsLayerMask.FromLayer(1)),
                new FixVector3(Fix64.FromInt(-2), Fix64.FromInt(-2), Fix64.FromInt(-2)),
                new FixVector3(Fix64.FromInt(4), Fix64.FromInt(2), Fix64.FromInt(2)));

            int count = world.QueryAabb(query, results);

            Assert.AreEqual(1, count);
            Assert.AreEqual(1, results.Count);
            Assert.AreEqual(2, results[0].TargetEntityId.Value);
            Assert.AreEqual(2, results[0].TargetBodyId.Value);
            Assert.AreEqual(1, results[0].TargetColliderId.Value);
            Assert.AreEqual(Fix64.Zero, results[0].Distance);
        }

        [Test]
        public void QueryAabb_ResultOrderDoesNotDependOnRegistrationOrder()
        {
            CombatPhysicsWorld first = new CombatPhysicsWorld();
            RegisterBodyWithAabb(first, entity: 4, body: 4, collider: 4, layer: 1, x: 1);
            RegisterBodyWithAabb(first, entity: 2, body: 2, collider: 2, layer: 1, x: 1);

            CombatPhysicsWorld second = new CombatPhysicsWorld();
            RegisterBodyWithAabb(second, entity: 2, body: 2, collider: 2, layer: 1, x: 1);
            RegisterBodyWithAabb(second, entity: 4, body: 4, collider: 4, layer: 1, x: 1);

            var query = new CombatAabbQuery(
                Header(CombatQueryKind.Aabb, CombatPhysicsLayerMask.FromLayer(1)),
                new FixVector3(Fix64.Zero, Fix64.FromInt(-2), Fix64.FromInt(-2)),
                new FixVector3(Fix64.FromInt(2), Fix64.FromInt(2), Fix64.FromInt(2)));
            var firstResults = new List<CombatQueryResult>();
            var secondResults = new List<CombatQueryResult>();

            first.QueryAabb(query, firstResults);
            second.QueryAabb(query, secondResults);

            Assert.AreEqual(firstResults.Count, secondResults.Count);
            Assert.AreEqual(2, firstResults[0].TargetEntityId.Value);
            Assert.AreEqual(firstResults[0], secondResults[0]);
            Assert.AreEqual(firstResults[1], secondResults[1]);
        }

        [Test]
        public void QueryRay_ReturnsNearestHitPointAndNormal()
        {
            CombatPhysicsWorld world = new CombatPhysicsWorld();
            RegisterBodyWithAabb(world, entity: 2, body: 2, collider: 1, layer: 1, x: 4);
            RegisterBodyWithAabb(world, entity: 3, body: 3, collider: 1, layer: 1, x: 2);
            var query = new CombatRayQuery(
                Header(CombatQueryKind.Ray, CombatPhysicsLayerMask.FromLayer(1)),
                FixVector3.Zero,
                UnitX,
                Fix64.FromInt(10));
            var results = new List<CombatQueryResult>();

            int count = world.QueryRay(query, results);

            Assert.AreEqual(2, count);
            Assert.AreEqual(3, results[0].TargetEntityId.Value);
            Assert.AreEqual(Fix64.FromRatio(3, 2), results[0].Distance);
            Assert.AreEqual(new FixVector3(Fix64.FromRatio(3, 2), Fix64.Zero, Fix64.Zero), results[0].Point);
            Assert.AreEqual(new FixVector3(-Fix64.One, Fix64.Zero, Fix64.Zero), results[0].Normal);
            Assert.AreEqual(2, results[1].TargetEntityId.Value);
            Assert.AreEqual(Fix64.FromRatio(7, 2), results[1].Distance);
        }

        [Test]
        public void QuerySphere_UsesClosestPointOnAabb()
        {
            CombatPhysicsWorld world = new CombatPhysicsWorld();
            RegisterBodyWithAabb(world, entity: 2, body: 2, collider: 1, layer: 1, x: 2);
            var query = new CombatSphereQuery(
                Header(CombatQueryKind.Sphere, CombatPhysicsLayerMask.FromLayer(1)),
                FixVector3.Zero,
                Fix64.FromInt(2));
            var results = new List<CombatQueryResult>();

            int count = world.QuerySphere(query, results);

            Assert.AreEqual(1, count);
            Assert.AreEqual(2, results[0].TargetEntityId.Value);
            Assert.AreEqual(Fix64.FromRatio(3, 2), results[0].Distance);
            Assert.AreEqual(new FixVector3(Fix64.FromRatio(3, 2), Fix64.Zero, Fix64.Zero), results[0].Point);
        }

        [Test]
        public void QueryCapsule_UsesWeaponSegmentAndRadius()
        {
            CombatPhysicsWorld world = new CombatPhysicsWorld();
            RegisterBodyWithAabb(world, entity: 2, body: 2, collider: 1, layer: 1, x: 3);
            var query = new CombatCapsuleQuery(
                Header(CombatQueryKind.Capsule, CombatPhysicsLayerMask.FromLayer(1)),
                FixVector3.Zero,
                new FixVector3(Fix64.FromInt(4), Fix64.Zero, Fix64.Zero),
                Fix64.Half);
            var results = new List<CombatQueryResult>();

            int count = world.QueryCapsule(query, results);

            Assert.AreEqual(1, count);
            Assert.AreEqual(2, results[0].TargetEntityId.Value);
            Assert.AreEqual(Fix64.FromInt(2), results[0].Distance);
        }

        [Test]
        public void QueryCapsule_LongCameraSegmentAgainstLargeGroundAabb_DoesNotOverflow()
        {
            CombatPhysicsWorld world = new CombatPhysicsWorld();
            var bodyId = new CombatBodyId(2);
            world.UpsertBody(new CombatPhysicsBody(
                new CombatEntityId(2),
                bodyId,
                FixVector3.Zero));
            world.UpsertAabbCollider(new CombatPhysicsAabbCollider(
                bodyId,
                new CombatColliderId(1),
                layer: 1,
                new FixVector3(Fix64.FromInt(-120), Fix64.FromRatio(-1, 20), Fix64.FromInt(-120)),
                new FixVector3(Fix64.FromInt(120), Fix64.Zero, Fix64.FromInt(120))));
            var query = new CombatCapsuleQuery(
                Header(CombatQueryKind.Capsule, CombatPhysicsLayerMask.FromLayer(1)),
                new FixVector3(Fix64.Zero, Fix64.FromInt(3), Fix64.Zero),
                new FixVector3(Fix64.FromInt(-20), Fix64.FromInt(12), Fix64.FromInt(-18)),
                Fix64.FromRatio(11, 20));
            var results = new List<CombatQueryResult>();

            int count = world.QueryCapsule(query, results);

            Assert.AreEqual(0, count);
        }

        [Test]
        public void QuerySector_FiltersByFacingDot()
        {
            CombatPhysicsWorld world = new CombatPhysicsWorld();
            RegisterBodyWithAabb(world, entity: 2, body: 2, collider: 1, layer: 1, x: 2);
            RegisterBodyWithAabb(world, entity: 3, body: 3, collider: 1, layer: 1, x: -2);
            var query = new CombatSectorQuery(
                Header(CombatQueryKind.Sector, CombatPhysicsLayerMask.FromLayer(1)),
                FixVector3.Zero,
                UnitX,
                Fix64.FromInt(4),
                Fix64.Zero);
            var results = new List<CombatQueryResult>();

            int count = world.QuerySector(query, results);

            Assert.AreEqual(1, count);
            Assert.AreEqual(2, results[0].TargetEntityId.Value);
        }

        [Test]
        public void QueriesHitSphereAndCapsuleTargetsWithShapeKind()
        {
            CombatPhysicsWorld world = CreateSphereCapsuleWorld();

            AssertQueryHitsSphereAndCapsule(world, CombatPhysicsQuery.From(new CombatRayQuery(
                Header(CombatQueryKind.Ray, CombatPhysicsLayerMask.FromLayer(1)),
                FixVector3.Zero,
                UnitX,
                Fix64.FromInt(6))));
            AssertQueryHitsSphereAndCapsule(world, CombatPhysicsQuery.From(new CombatSphereQuery(
                Header(CombatQueryKind.Sphere, CombatPhysicsLayerMask.FromLayer(1)),
                new FixVector3(Fix64.FromInt(3), Fix64.Zero, Fix64.Zero),
                Fix64.FromInt(2))));
            AssertQueryHitsSphereAndCapsule(world, CombatPhysicsQuery.From(new CombatCapsuleQuery(
                Header(CombatQueryKind.Capsule, CombatPhysicsLayerMask.FromLayer(1)),
                new FixVector3(Fix64.One, Fix64.Zero, Fix64.Zero),
                new FixVector3(Fix64.FromInt(5), Fix64.Zero, Fix64.Zero),
                Fix64.Half)));
            AssertQueryHitsSphereAndCapsule(world, CombatPhysicsQuery.From(new CombatAabbQuery(
                Header(CombatQueryKind.Aabb, CombatPhysicsLayerMask.FromLayer(1)),
                new FixVector3(Fix64.One, -Fix64.One, -Fix64.One),
                new FixVector3(Fix64.FromInt(5), Fix64.One, Fix64.One))));
            AssertQueryHitsSphereAndCapsule(world, CombatPhysicsQuery.From(new CombatSectorQuery(
                Header(CombatQueryKind.Sector, CombatPhysicsLayerMask.FromLayer(1)),
                FixVector3.Zero,
                UnitX,
                Fix64.FromInt(6),
                Fix64.Zero)));
            AssertQueryHitsSphereAndCapsule(world, CombatPhysicsQuery.From(new CombatObbQuery(
                Header(CombatQueryKind.Obb, CombatPhysicsLayerMask.FromLayer(1)),
                new FixVector3(Fix64.FromInt(3), Fix64.Zero, Fix64.Zero),
                new FixVector3(Fix64.FromInt(3), Fix64.One, Fix64.One),
                UnitX,
                UnitY,
                UnitZ)));
        }

        [Test]
        public void SpecializedQueriesAndUnifiedQueryIncludeSphereAndCapsuleTargets()
        {
            CombatPhysicsWorld world = CreateSphereCapsuleWorld();
            var ray = new CombatRayQuery(
                Header(CombatQueryKind.Ray, CombatPhysicsLayerMask.FromLayer(1)),
                FixVector3.Zero,
                UnitX,
                Fix64.FromInt(6));
            var sphere = new CombatSphereQuery(
                Header(CombatQueryKind.Sphere, CombatPhysicsLayerMask.FromLayer(1)),
                new FixVector3(Fix64.FromInt(3), Fix64.Zero, Fix64.Zero),
                Fix64.FromInt(2));
            var capsule = new CombatCapsuleQuery(
                Header(CombatQueryKind.Capsule, CombatPhysicsLayerMask.FromLayer(1)),
                new FixVector3(Fix64.One, Fix64.Zero, Fix64.Zero),
                new FixVector3(Fix64.FromInt(5), Fix64.Zero, Fix64.Zero),
                Fix64.Half);
            var aabb = new CombatAabbQuery(
                Header(CombatQueryKind.Aabb, CombatPhysicsLayerMask.FromLayer(1)),
                new FixVector3(Fix64.One, -Fix64.One, -Fix64.One),
                new FixVector3(Fix64.FromInt(5), Fix64.One, Fix64.One));
            var sector = new CombatSectorQuery(
                Header(CombatQueryKind.Sector, CombatPhysicsLayerMask.FromLayer(1)),
                FixVector3.Zero,
                UnitX,
                Fix64.FromInt(6),
                Fix64.Zero);

            AssertSpecializedMatchesUnified(world, ray);
            AssertSpecializedMatchesUnified(world, sphere);
            AssertSpecializedMatchesUnified(world, capsule);
            AssertSpecializedMatchesUnified(world, aabb);
            AssertSpecializedMatchesUnified(world, sector);
        }

        [Test]
        public void SphereAndCapsuleTargets_RespectLayerAndSourceFilters()
        {
            var world = new CombatPhysicsWorld();
            RegisterBodyWithSphereAt(world, entity: 1, body: 1, collider: 1, layer: 1, x: Fix64.FromInt(2), radius: Fix64.Half);
            RegisterBodyWithSphereAt(world, entity: 2, body: 2, collider: 1, layer: 2, x: Fix64.FromInt(3), radius: Fix64.Half);
            RegisterBodyWithCapsuleAt(world, entity: 3, body: 3, collider: 1, layer: 1, x: Fix64.FromInt(4), radius: Fix64.Half);
            var query = CombatPhysicsQuery.From(new CombatAabbQuery(
                Header(CombatQueryKind.Aabb, CombatPhysicsLayerMask.FromLayer(1)),
                new FixVector3(Fix64.One, -Fix64.One, -Fix64.One),
                new FixVector3(Fix64.FromInt(5), Fix64.One, Fix64.One)));
            var includeSourceQuery = CombatPhysicsQuery.From(
                new CombatAabbQuery(
                    Header(CombatQueryKind.Aabb, CombatPhysicsLayerMask.FromLayer(1)),
                    new FixVector3(Fix64.One, -Fix64.One, -Fix64.One),
                    new FixVector3(Fix64.FromInt(5), Fix64.One, Fix64.One)),
                new CombatPhysicsQueryFilter(includeSourceEntity: true));
            var hits = new List<CombatQueryResult>();

            Assert.AreEqual(1, world.Query(query, hits));
            Assert.AreEqual(3, hits[0].TargetEntityId.Value);
            Assert.AreEqual(CombatPhysicsShapeKind.Capsule, hits[0].TargetShapeKind);

            hits.Clear();
            Assert.AreEqual(2, world.Query(includeSourceQuery, hits));
            AssertHasHit(hits, entity: 1, CombatPhysicsShapeKind.Sphere);
            AssertHasHit(hits, entity: 3, CombatPhysicsShapeKind.Capsule);
            AssertNoHit(hits, entity: 2);
        }

        [Test]
        public void UnifiedQuery_DispatchesToExistingShapeQueries()
        {
            CombatPhysicsWorld world = CreateWorld();

            AssertUnifiedMatchesOld(world, new CombatRayQuery(
                Header(CombatQueryKind.Ray, CombatPhysicsLayerMask.FromLayer(1)),
                FixVector3.Zero,
                UnitX,
                Fix64.FromInt(4)));
            AssertUnifiedMatchesOld(world, new CombatSphereQuery(
                Header(CombatQueryKind.Sphere, CombatPhysicsLayerMask.FromLayer(1)),
                FixVector3.Zero,
                Fix64.FromInt(3)));
            AssertUnifiedMatchesOld(world, new CombatCapsuleQuery(
                Header(CombatQueryKind.Capsule, CombatPhysicsLayerMask.FromLayer(1)),
                FixVector3.Zero,
                new FixVector3(Fix64.FromInt(3), Fix64.Zero, Fix64.Zero),
                Fix64.Half));
            AssertUnifiedMatchesOld(world, new CombatAabbQuery(
                Header(CombatQueryKind.Aabb, CombatPhysicsLayerMask.FromLayer(1)),
                new FixVector3(Fix64.FromInt(-2), Fix64.FromInt(-2), Fix64.FromInt(-2)),
                new FixVector3(Fix64.FromInt(4), Fix64.FromInt(2), Fix64.FromInt(2))));
            AssertUnifiedMatchesOld(world, new CombatSectorQuery(
                Header(CombatQueryKind.Sector, CombatPhysicsLayerMask.FromLayer(1)),
                FixVector3.Zero,
                UnitX,
                Fix64.FromInt(4),
                Fix64.Zero));
        }

        [Test]
        public void UnifiedQuery_FilterCanIncludeSourceEntity()
        {
            CombatPhysicsWorld world = CreateWorld();
            var specific = new CombatAabbQuery(
                Header(CombatQueryKind.Aabb, CombatPhysicsLayerMask.FromLayer(1)),
                new FixVector3(Fix64.FromInt(-2), Fix64.FromInt(-2), Fix64.FromInt(-2)),
                new FixVector3(Fix64.FromInt(4), Fix64.FromInt(2), Fix64.FromInt(2)));
            CombatPhysicsQuery query = CombatPhysicsQuery.From(
                specific,
                new CombatPhysicsQueryFilter(includeSourceEntity: true));
            var results = new List<CombatQueryResult>();

            int count = world.Query(query, results);

            Assert.AreEqual(2, count);
            Assert.AreEqual(1, results[0].TargetEntityId.Value);
            Assert.AreEqual(2, results[1].TargetEntityId.Value);
        }

        [Test]
        public void Query_AppendsSortedHitsWithoutResortingExistingBuffer()
        {
            var world = new CombatPhysicsWorld();
            RegisterBodyWithAabb(world, entity: 3, body: 3, collider: 1, layer: 1, x: 1);
            RegisterBodyWithAabb(world, entity: 2, body: 2, collider: 1, layer: 1, x: 2);
            CombatQueryHeader header = Header(CombatQueryKind.Aabb, CombatPhysicsLayerMask.FromLayer(1));
            var query = CombatPhysicsQuery.From(new CombatAabbQuery(
                header,
                new FixVector3(Fix64.Zero, -Fix64.One, -Fix64.One),
                new FixVector3(Fix64.FromInt(3), Fix64.One, Fix64.One)));
            var results = new List<CombatQueryResult>
            {
                new CombatQueryResult(
                    header,
                    new CombatEntityId(99),
                    new CombatBodyId(99),
                    new CombatColliderId(99),
                    Fix64.FromInt(99),
                    FixVector3.Zero,
                    UnitX)
            };

            int count = world.Query(query, results);

            Assert.AreEqual(2, count);
            Assert.AreEqual(3, results.Count);
            Assert.AreEqual(99, results[0].TargetEntityId.Value);
            Assert.AreEqual(2, results[1].TargetEntityId.Value);
            Assert.AreEqual(3, results[2].TargetEntityId.Value);
        }

        [Test]
        public void QueryBatch_SortsQueriesAndHitsDeterministically()
        {
            CombatPhysicsWorld first = new CombatPhysicsWorld();
            RegisterBodyWithAabb(first, entity: 4, body: 4, collider: 4, layer: 1, x: 1);
            RegisterBodyWithAabb(first, entity: 2, body: 2, collider: 2, layer: 1, x: 1);

            CombatPhysicsWorld second = new CombatPhysicsWorld();
            RegisterBodyWithAabb(second, entity: 2, body: 2, collider: 2, layer: 1, x: 1);
            RegisterBodyWithAabb(second, entity: 4, body: 4, collider: 4, layer: 1, x: 1);

            var queries = new List<CombatPhysicsQuery>
            {
                CombatPhysicsQuery.From(new CombatRayQuery(
                    Header(CombatQueryKind.Ray, CombatPhysicsLayerMask.FromLayer(1), queryId: 20, sourceOrder: 20),
                    FixVector3.Zero,
                    UnitX,
                    Fix64.FromInt(4))),
                CombatPhysicsQuery.From(new CombatAabbQuery(
                    Header(CombatQueryKind.Aabb, CombatPhysicsLayerMask.FromLayer(1), queryId: 10, sourceOrder: 10),
                    new FixVector3(Fix64.Zero, Fix64.FromInt(-2), Fix64.FromInt(-2)),
                    new FixVector3(Fix64.FromInt(2), Fix64.FromInt(2), Fix64.FromInt(2)))),
            };
            var firstResults = new List<CombatPhysicsQueryBatchResult>();
            var secondResults = new List<CombatPhysicsQueryBatchResult>();

            first.QueryBatch(new CombatPhysicsQueryBatch(queries), firstResults);
            second.QueryBatch(new CombatPhysicsQueryBatch(queries), secondResults);

            Assert.AreEqual(2, firstResults.Count);
            Assert.AreEqual(10, firstResults[0].Query.Header.SourceOrder);
            Assert.AreEqual(20, firstResults[1].Query.Header.SourceOrder);
            AssertBatchResultsEqual(firstResults, secondResults);
            Assert.AreEqual(2, firstResults[0].Hits.Count);
            Assert.AreEqual(2, firstResults[0].Hits[0].TargetEntityId.Value);
            Assert.AreEqual(4, firstResults[0].Hits[1].TargetEntityId.Value);
        }

        [Test]
        public void QueryBatch_AppendsSortedResultsWithoutResortingExistingBuffer()
        {
            CombatPhysicsWorld world = new CombatPhysicsWorld();
            RegisterBodyWithAabb(world, entity: 4, body: 4, collider: 4, layer: 1, x: 1);
            RegisterBodyWithAabb(world, entity: 2, body: 2, collider: 2, layer: 1, x: 1);
            CombatPhysicsQuery sentinelQuery = CombatPhysicsQuery.From(new CombatAabbQuery(
                Header(CombatQueryKind.Aabb, CombatPhysicsLayerMask.FromLayer(1), queryId: 99, sourceOrder: 99),
                new FixVector3(Fix64.FromInt(10), Fix64.Zero, Fix64.Zero),
                new FixVector3(Fix64.FromInt(11), Fix64.One, Fix64.One)));
            var queries = new List<CombatPhysicsQuery>
            {
                CombatPhysicsQuery.From(new CombatRayQuery(
                    Header(CombatQueryKind.Ray, CombatPhysicsLayerMask.FromLayer(1), queryId: 20, sourceOrder: 20),
                    FixVector3.Zero,
                    UnitX,
                    Fix64.FromInt(4))),
                CombatPhysicsQuery.From(new CombatAabbQuery(
                    Header(CombatQueryKind.Aabb, CombatPhysicsLayerMask.FromLayer(1), queryId: 10, sourceOrder: 10),
                    new FixVector3(Fix64.Zero, Fix64.FromInt(-2), Fix64.FromInt(-2)),
                    new FixVector3(Fix64.FromInt(2), Fix64.FromInt(2), Fix64.FromInt(2)))),
            };
            var results = new List<CombatPhysicsQueryBatchResult>
            {
                new CombatPhysicsQueryBatchResult(
                    sentinelQuery,
                    99,
                    Array.Empty<CombatQueryResult>(),
                    world.ExplainQuery(sentinelQuery))
            };

            int count = world.QueryBatch(new CombatPhysicsQueryBatch(queries), results);

            Assert.AreEqual(2, count);
            Assert.AreEqual(3, results.Count);
            Assert.AreEqual(99, results[0].Query.Header.SourceOrder);
            Assert.AreEqual(10, results[1].Query.Header.SourceOrder);
            Assert.AreEqual(20, results[2].Query.Header.SourceOrder);
        }

        [Test]
        public void QueryBatch_EmptyHitsShareEmptySnapshot()
        {
            CombatPhysicsWorld world = new CombatPhysicsWorld();
            var queries = new List<CombatPhysicsQuery>
            {
                CombatPhysicsQuery.From(new CombatRayQuery(
                    Header(CombatQueryKind.Ray, CombatPhysicsLayerMask.FromLayer(1), queryId: 20, sourceOrder: 20),
                    FixVector3.Zero,
                    UnitX,
                    Fix64.FromInt(4))),
                CombatPhysicsQuery.From(new CombatAabbQuery(
                    Header(CombatQueryKind.Aabb, CombatPhysicsLayerMask.FromLayer(1), queryId: 10, sourceOrder: 10),
                    new FixVector3(Fix64.Zero, Fix64.FromInt(-2), Fix64.FromInt(-2)),
                    new FixVector3(Fix64.FromInt(2), Fix64.FromInt(2), Fix64.FromInt(2)))),
            };
            var results = new List<CombatPhysicsQueryBatchResult>();

            world.QueryBatch(new CombatPhysicsQueryBatch(queries), results);

            Assert.AreEqual(2, results.Count);
            Assert.AreEqual(0, results[0].HitCount);
            Assert.AreEqual(0, results[1].HitCount);
            Assert.AreSame(results[0].Hits, results[1].Hits);
        }

        [Test]
        public void ExplainQuery_ReportsCandidatesFiltersAndHits()
        {
            CombatPhysicsWorld world = CreateWorld();
            var specific = new CombatAabbQuery(
                Header(CombatQueryKind.Aabb, CombatPhysicsLayerMask.FromLayer(1)),
                new FixVector3(Fix64.FromInt(-2), Fix64.FromInt(-2), Fix64.FromInt(-2)),
                new FixVector3(Fix64.FromInt(4), Fix64.FromInt(2), Fix64.FromInt(2)));

            CombatPhysicsQueryDebugReport report = world.ExplainQuery(CombatPhysicsQuery.From(specific));

            Assert.AreEqual(CombatPhysicsShapeKind.Aabb, report.ShapeKind);
            Assert.AreEqual(3, report.CandidateCount);
            Assert.AreEqual(1, report.FilteredSourceCount);
            Assert.AreEqual(1, report.FilteredLayerCount);
            Assert.AreEqual(1, report.HitCount);
            Assert.AreEqual(3, report.Rows.Count);
            Assert.AreEqual(CombatPhysicsQueryDebugRowStatus.FilteredSource, report.Rows[0].Status);
            Assert.AreEqual(CombatPhysicsQueryDebugRowStatus.Hit, report.Rows[1].Status);
            Assert.AreEqual(CombatPhysicsQueryDebugRowStatus.FilteredLayer, report.Rows[2].Status);
        }

        [Test]
        public void QueryObb_AxisAlignedMatchesEquivalentAabbOracle()
        {
            CombatPhysicsWorld world = CreateWorld();
            var aabbQuery = new CombatAabbQuery(
                Header(CombatQueryKind.Aabb, CombatPhysicsLayerMask.FromLayer(1)),
                new FixVector3(Fix64.FromInt(-2), Fix64.FromInt(-2), Fix64.FromInt(-2)),
                new FixVector3(Fix64.FromInt(4), Fix64.FromInt(2), Fix64.FromInt(2)));
            var obbQuery = new CombatObbQuery(
                Header(CombatQueryKind.Obb, CombatPhysicsLayerMask.FromLayer(1)),
                new FixVector3(Fix64.One, Fix64.Zero, Fix64.Zero),
                new FixVector3(Fix64.FromInt(3), Fix64.FromInt(2), Fix64.FromInt(2)),
                UnitX,
                UnitY,
                UnitZ);
            var aabbHits = new List<CombatQueryResult>();
            var obbHits = new List<CombatQueryResult>();

            world.QueryAabb(aabbQuery, aabbHits);
            int count = world.Query(CombatPhysicsQuery.From(obbQuery), obbHits);
            CombatPhysicsQueryDebugReport report = world.ExplainQuery(CombatPhysicsQuery.From(obbQuery));

            Assert.AreEqual(aabbHits.Count, count);
            Assert.AreEqual(aabbHits.Count, obbHits.Count);
            for (int i = 0; i < aabbHits.Count; i++)
            {
                Assert.AreEqual(aabbHits[i].TargetEntityId, obbHits[i].TargetEntityId);
                Assert.AreEqual(aabbHits[i].TargetBodyId, obbHits[i].TargetBodyId);
                Assert.AreEqual(aabbHits[i].TargetColliderId, obbHits[i].TargetColliderId);
            }

            Assert.IsFalse(report.IsUnsupported);
            Assert.AreEqual(count, report.HitCount);
        }

        [Test]
        public void QueryObb_CoversRotatedBoundaryContainmentSeparationAndThinBoxes()
        {
            CombatPhysicsWorld world = new CombatPhysicsWorld();
            RegisterBodyWithAabbAt(world, entity: 2, body: 2, collider: 1, layer: 1, x: Fix64.One, y: Fix64.One, z: Fix64.Zero, halfSize: Fix64.Half);
            RegisterBodyWithAabbAt(world, entity: 3, body: 3, collider: 1, layer: 1, x: Fix64.FromInt(5), y: Fix64.FromInt(5), z: Fix64.Zero, halfSize: Fix64.Half);
            RegisterBodyWithAabbAt(world, entity: 4, body: 4, collider: 1, layer: 1, x: Fix64.FromRatio(3, 2), y: Fix64.Zero, z: Fix64.Zero, halfSize: Fix64.Half);
            RegisterBodyWithAabbAt(world, entity: 5, body: 5, collider: 1, layer: 1, x: Fix64.Zero, y: Fix64.Zero, z: Fix64.Zero, halfSize: Fix64.Half);
            var rotated = CombatPhysicsQuery.From(new CombatObbQuery(
                Header(CombatQueryKind.Obb, CombatPhysicsLayerMask.FromLayer(1), queryId: 1),
                FixVector3.Zero,
                new FixVector3(Fix64.FromInt(2), Fix64.Half, Fix64.One),
                new FixVector3(Fix64.One, Fix64.One, Fix64.Zero),
                new FixVector3(-Fix64.One, Fix64.One, Fix64.Zero),
                UnitZ));
            var boundary = CombatPhysicsQuery.From(new CombatObbQuery(
                Header(CombatQueryKind.Obb, CombatPhysicsLayerMask.FromLayer(1), queryId: 2),
                FixVector3.Zero,
                new FixVector3(Fix64.One, Fix64.One, Fix64.One),
                UnitX,
                UnitY,
                UnitZ));
            var thin = CombatPhysicsQuery.From(new CombatObbQuery(
                Header(CombatQueryKind.Obb, CombatPhysicsLayerMask.FromLayer(1), queryId: 3),
                FixVector3.Zero,
                new FixVector3(Fix64.FromInt(2), Fix64.Zero, Fix64.One),
                UnitX,
                UnitY,
                UnitZ));
            var rotatedHits = new List<CombatQueryResult>();
            var boundaryHits = new List<CombatQueryResult>();
            var thinHits = new List<CombatQueryResult>();

            world.Query(rotated, rotatedHits);
            world.Query(boundary, boundaryHits);
            world.Query(thin, thinHits);

            AssertHasHit(rotatedHits, entity: 2);
            AssertHasHit(rotatedHits, entity: 5);
            AssertNoHit(rotatedHits, entity: 3);
            AssertHasHit(boundaryHits, entity: 4);
            AssertHasHit(boundaryHits, entity: 5);
            AssertHasHit(thinHits, entity: 5);
        }

        [Test]
        public void QueryObb_OrderAndBatchRemainStable()
        {
            CombatPhysicsWorld first = new CombatPhysicsWorld();
            RegisterBodyWithAabb(first, entity: 4, body: 4, collider: 4, layer: 1, x: 1);
            RegisterBodyWithAabb(first, entity: 2, body: 2, collider: 2, layer: 1, x: 1);

            CombatPhysicsWorld second = new CombatPhysicsWorld();
            RegisterBodyWithAabb(second, entity: 2, body: 2, collider: 2, layer: 1, x: 1);
            RegisterBodyWithAabb(second, entity: 4, body: 4, collider: 4, layer: 1, x: 1);

            CombatPhysicsQuery obb = CombatPhysicsQuery.From(new CombatObbQuery(
                Header(CombatQueryKind.Obb, CombatPhysicsLayerMask.FromLayer(1), queryId: 30, sourceOrder: 30),
                new FixVector3(Fix64.One, Fix64.Zero, Fix64.Zero),
                new FixVector3(Fix64.FromInt(2), Fix64.One, Fix64.One),
                UnitX,
                UnitY,
                UnitZ));
            CombatPhysicsQuery ray = CombatPhysicsQuery.From(new CombatRayQuery(
                Header(CombatQueryKind.Ray, CombatPhysicsLayerMask.FromLayer(1), queryId: 10, sourceOrder: 10),
                FixVector3.Zero,
                UnitX,
                Fix64.FromInt(4)));
            var firstHits = new List<CombatQueryResult>();
            var secondHits = new List<CombatQueryResult>();
            var firstBatch = new List<CombatPhysicsQueryBatchResult>();
            var secondBatch = new List<CombatPhysicsQueryBatchResult>();

            first.Query(obb, firstHits);
            second.Query(obb, secondHits);
            first.QueryBatch(new CombatPhysicsQueryBatch(new[] { obb, ray }), firstBatch);
            second.QueryBatch(new CombatPhysicsQueryBatch(new[] { obb, ray }), secondBatch);

            AssertResultsEqual(firstHits, secondHits);
            Assert.AreEqual(2, firstHits.Count);
            Assert.AreEqual(2, firstHits[0].TargetEntityId.Value);
            Assert.AreEqual(4, firstHits[1].TargetEntityId.Value);
            AssertBatchResultsEqual(firstBatch, secondBatch);
            Assert.AreEqual(10, firstBatch[0].Query.Header.SourceOrder);
            Assert.AreEqual(30, firstBatch[1].Query.Header.SourceOrder);
            Assert.IsFalse(firstBatch[1].IsUnsupported);
            Assert.AreEqual(2, firstBatch[1].HitCount);
        }

        [Test]
        public void ExplainQuery_ReportsObbCandidatesFiltersAndHits()
        {
            CombatPhysicsWorld world = CreateWorld();
            var query = CombatPhysicsQuery.From(new CombatObbQuery(
                Header(CombatQueryKind.Obb, CombatPhysicsLayerMask.FromLayer(1)),
                    new FixVector3(Fix64.One, Fix64.Zero, Fix64.Zero),
                    new FixVector3(Fix64.One, Fix64.One, Fix64.One),
                    UnitX,
                    UnitY,
                    UnitZ));

            CombatPhysicsQueryDebugReport report = world.ExplainQuery(query);

            Assert.IsFalse(report.IsUnsupported);
            Assert.AreEqual(CombatPhysicsShapeKind.Obb, report.ShapeKind);
            Assert.AreEqual(3, report.CandidateCount);
            Assert.AreEqual(1, report.FilteredSourceCount);
            Assert.AreEqual(1, report.FilteredLayerCount);
            Assert.AreEqual(1, report.HitCount);
            Assert.AreEqual(CombatPhysicsQueryDebugRowStatus.FilteredSource, report.Rows[0].Status);
            Assert.AreEqual(CombatPhysicsQueryDebugRowStatus.Hit, report.Rows[1].Status);
            Assert.AreEqual(CombatPhysicsQueryDebugRowStatus.FilteredLayer, report.Rows[2].Status);
        }

        [Test]
        public void UpsertBody_UpdatesColliderWorldPosition()
        {
            CombatPhysicsWorld world = new CombatPhysicsWorld();
            RegisterBodyWithAabb(world, entity: 2, body: 2, collider: 1, layer: 1, x: 10);
            var query = new CombatRayQuery(
                Header(CombatQueryKind.Ray, CombatPhysicsLayerMask.FromLayer(1)),
                FixVector3.Zero,
                UnitX,
                Fix64.FromInt(3));
            var results = new List<CombatQueryResult>();

            Assert.AreEqual(0, world.QueryRay(query, results));

            world.UpsertBody(new CombatPhysicsBody(
                new CombatEntityId(2),
                new CombatBodyId(2),
                new FixVector3(Fix64.FromInt(2), Fix64.Zero, Fix64.Zero)));

            Assert.AreEqual(1, world.QueryRay(query, results));
            Assert.AreEqual(Fix64.FromRatio(3, 2), results[0].Distance);
        }

        [Test]
        public void UpsertBody_MovingBodyChangesUnifiedQueryHits()
        {
            CombatPhysicsWorld world = new CombatPhysicsWorld();
            RegisterBodyWithAabb(world, entity: 2, body: 2, collider: 1, layer: 1, x: 2);
            CombatPhysicsQuery query = CombatPhysicsQuery.From(new CombatAabbQuery(
                Header(CombatQueryKind.Aabb, CombatPhysicsLayerMask.FromLayer(1)),
                new FixVector3(Fix64.FromInt(1), -Fix64.One, -Fix64.One),
                new FixVector3(Fix64.FromInt(3), Fix64.One, Fix64.One)));
            var hits = new List<CombatQueryResult>();

            Assert.AreEqual(1, world.Query(query, hits));
            Assert.AreEqual(2, hits[0].TargetEntityId.Value);

            world.UpsertBody(new CombatPhysicsBody(
                new CombatEntityId(2),
                new CombatBodyId(2),
                new FixVector3(Fix64.FromInt(10), Fix64.Zero, Fix64.Zero)));

            hits.Clear();
            Assert.AreEqual(0, world.Query(query, hits));
            Assert.AreEqual(0, world.ExplainQuery(query).HitCount);

            world.UpsertBody(new CombatPhysicsBody(
                new CombatEntityId(2),
                new CombatBodyId(2),
                new FixVector3(Fix64.FromInt(2), Fix64.Zero, Fix64.Zero)));

            hits.Clear();
            Assert.AreEqual(1, world.Query(query, hits));
            Assert.AreEqual(2, hits[0].TargetBodyId.Value);
        }

        [Test]
        public void RemoveBody_CascadesColliderRemoval()
        {
            CombatPhysicsWorld world = new CombatPhysicsWorld();
            world.UpsertBody(new CombatPhysicsBody(new CombatEntityId(2), new CombatBodyId(2), FixVector3.Zero));
            world.UpsertAabbCollider(new CombatPhysicsAabbCollider(
                new CombatBodyId(2),
                new CombatColliderId(1),
                layer: 1,
                localMin: new FixVector3(-Fix64.Half, -Fix64.Half, -Fix64.Half),
                localMax: new FixVector3(Fix64.Half, Fix64.Half, Fix64.Half)));
            world.UpsertAabbCollider(new CombatPhysicsAabbCollider(
                new CombatBodyId(2),
                new CombatColliderId(2),
                layer: 1,
                localMin: new FixVector3(Fix64.One, -Fix64.Half, -Fix64.Half),
                localMax: new FixVector3(Fix64.FromInt(2), Fix64.Half, Fix64.Half)));
            RegisterBodyWithAabb(world, entity: 3, body: 3, collider: 1, layer: 1, x: 4);
            CombatPhysicsQuery query = CombatPhysicsQuery.From(new CombatAabbQuery(
                Header(CombatQueryKind.Aabb, CombatPhysicsLayerMask.FromLayer(1)),
                new FixVector3(Fix64.FromInt(-1), -Fix64.One, -Fix64.One),
                new FixVector3(Fix64.FromInt(5), Fix64.One, Fix64.One)));

            Assert.AreEqual(2, world.BodyCount);
            Assert.AreEqual(3, world.ColliderCount);

            Assert.IsTrue(world.RemoveBody(new CombatBodyId(2)));

            var hits = new List<CombatQueryResult>();
            Assert.AreEqual(1, world.BodyCount);
            Assert.AreEqual(1, world.ColliderCount);
            Assert.AreEqual(1, world.Query(query, hits));
            Assert.AreEqual(3, hits[0].TargetEntityId.Value);
            Assert.IsFalse(world.RemoveBody(new CombatBodyId(2)));
        }

        [Test]
        public void RemoveCollider_RemovesOnlyRequestedCollider()
        {
            CombatPhysicsWorld world = new CombatPhysicsWorld();
            world.UpsertBody(new CombatPhysicsBody(new CombatEntityId(2), new CombatBodyId(2), FixVector3.Zero));
            world.UpsertAabbCollider(new CombatPhysicsAabbCollider(
                new CombatBodyId(2),
                new CombatColliderId(1),
                layer: 1,
                localMin: new FixVector3(-Fix64.Half, -Fix64.Half, -Fix64.Half),
                localMax: new FixVector3(Fix64.Half, Fix64.Half, Fix64.Half)));
            world.UpsertAabbCollider(new CombatPhysicsAabbCollider(
                new CombatBodyId(2),
                new CombatColliderId(2),
                layer: 1,
                localMin: new FixVector3(Fix64.FromInt(3), -Fix64.Half, -Fix64.Half),
                localMax: new FixVector3(Fix64.FromInt(4), Fix64.Half, Fix64.Half)));
            CombatPhysicsQuery query = CombatPhysicsQuery.From(new CombatAabbQuery(
                Header(CombatQueryKind.Aabb, CombatPhysicsLayerMask.FromLayer(1)),
                new FixVector3(Fix64.FromInt(-1), -Fix64.One, -Fix64.One),
                new FixVector3(Fix64.FromInt(5), Fix64.One, Fix64.One)));
            var hits = new List<CombatQueryResult>();

            Assert.AreEqual(2, world.Query(query, hits));
            Assert.AreEqual(1, hits[0].TargetColliderId.Value);
            Assert.AreEqual(2, hits[1].TargetColliderId.Value);

            Assert.IsTrue(world.RemoveCollider(new CombatBodyId(2), new CombatColliderId(1)));

            hits.Clear();
            Assert.AreEqual(1, world.ColliderCount);
            Assert.AreEqual(1, world.Query(query, hits));
            Assert.AreEqual(2, hits[0].TargetColliderId.Value);
            Assert.IsFalse(world.RemoveCollider(new CombatBodyId(2), new CombatColliderId(1)));
        }

        [Test]
        public void Clear_RemovesBodiesCollidersAndDebugCandidates()
        {
            CombatPhysicsWorld world = CreateWorld();
            CombatPhysicsQuery query = CombatPhysicsQuery.From(new CombatAabbQuery(
                Header(CombatQueryKind.Aabb, CombatPhysicsLayerMask.All),
                new FixVector3(Fix64.FromInt(-2), -Fix64.One, -Fix64.One),
                new FixVector3(Fix64.FromInt(4), Fix64.One, Fix64.One)));

            world.Clear();

            var hits = new List<CombatQueryResult>();
            CombatPhysicsQueryDebugReport report = world.ExplainQuery(query);
            Assert.AreEqual(0, world.BodyCount);
            Assert.AreEqual(0, world.ColliderCount);
            Assert.AreEqual(0, world.Query(query, hits));
            Assert.AreEqual(0, report.CandidateCount);
            Assert.AreEqual(0, report.HitCount);
        }

        [Test]
        public void UpsertAabbCollider_ReplacesExistingCollider()
        {
            CombatPhysicsWorld world = new CombatPhysicsWorld();
            RegisterBodyWithAabb(world, entity: 2, body: 2, collider: 1, layer: 1, x: 0);
            CombatPhysicsQuery layerOneQuery = CombatPhysicsQuery.From(new CombatAabbQuery(
                Header(CombatQueryKind.Aabb, CombatPhysicsLayerMask.FromLayer(1)),
                new FixVector3(-Fix64.One, -Fix64.One, -Fix64.One),
                new FixVector3(Fix64.One, Fix64.One, Fix64.One)));
            CombatPhysicsQuery layerTwoQuery = CombatPhysicsQuery.From(new CombatAabbQuery(
                Header(CombatQueryKind.Aabb, CombatPhysicsLayerMask.FromLayer(2)),
                new FixVector3(Fix64.FromInt(3), -Fix64.One, -Fix64.One),
                new FixVector3(Fix64.FromInt(5), Fix64.One, Fix64.One)));
            var hits = new List<CombatQueryResult>();

            Assert.AreEqual(1, world.Query(layerOneQuery, hits));

            world.UpsertAabbCollider(new CombatPhysicsAabbCollider(
                new CombatBodyId(2),
                new CombatColliderId(1),
                layer: 2,
                localMin: new FixVector3(Fix64.FromInt(3), -Fix64.Half, -Fix64.Half),
                localMax: new FixVector3(Fix64.FromInt(4), Fix64.Half, Fix64.Half)));

            hits.Clear();
            Assert.AreEqual(1, world.BodyCount);
            Assert.AreEqual(1, world.ColliderCount);
            Assert.AreEqual(0, world.Query(layerOneQuery, hits));

            hits.Clear();
            Assert.AreEqual(1, world.Query(layerTwoQuery, hits));
            Assert.AreEqual(1, hits[0].TargetColliderId.Value);
        }

        [Test]
        public void QueryBatch_ResultSnapshotRemainsStableAfterWorldMutation()
        {
            CombatPhysicsWorld world = new CombatPhysicsWorld();
            RegisterBodyWithAabb(world, entity: 4, body: 4, collider: 1, layer: 1, x: 1);
            RegisterBodyWithAabb(world, entity: 2, body: 2, collider: 1, layer: 1, x: 1);
            var query = CombatPhysicsQuery.From(new CombatAabbQuery(
                Header(CombatQueryKind.Aabb, CombatPhysicsLayerMask.FromLayer(1)),
                new FixVector3(Fix64.Zero, -Fix64.One, -Fix64.One),
                new FixVector3(Fix64.FromInt(2), Fix64.One, Fix64.One)));
            var batchResults = new List<CombatPhysicsQueryBatchResult>();

            world.QueryBatch(new CombatPhysicsQueryBatch(new[] { query }), batchResults);
            world.Clear();

            Assert.AreEqual(1, batchResults.Count);
            Assert.AreEqual(2, batchResults[0].HitCount);
            Assert.AreEqual(2, batchResults[0].Hits[0].TargetEntityId.Value);
            Assert.AreEqual(4, batchResults[0].Hits[1].TargetEntityId.Value);
            Assert.AreEqual(2, batchResults[0].DebugReport.HitCount);

            var freshHits = new List<CombatQueryResult>();
            Assert.AreEqual(0, world.Query(query, freshHits));
        }

        [Test]
        public void RuntimeLifecycleApi_TracksRevisionLookupMovementAndStats()
        {
            var world = new CombatPhysicsWorld();
            var bodyId = new CombatBodyId(2);
            var colliderId = new CombatColliderId(1);
            var body = new CombatPhysicsBody(
                new CombatEntityId(2),
                bodyId,
                FixVector3.Zero);
            var collider = new CombatPhysicsAabbCollider(
                bodyId,
                colliderId,
                layer: 1,
                localMin: new FixVector3(-Fix64.Half, -Fix64.Half, -Fix64.Half),
                localMax: new FixVector3(Fix64.Half, Fix64.Half, Fix64.Half));

            Assert.AreEqual(0, world.Revision);
            Assert.AreEqual(new CombatPhysicsWorldStats(0, 0, 0), world.CreateStats());

            world.UpsertBody(body);
            Assert.AreEqual(1, world.Revision);
            Assert.IsTrue(world.TryGetBody(bodyId, out CombatPhysicsBody foundBody));
            Assert.AreEqual(body, foundBody);

            world.UpsertBody(body);
            Assert.AreEqual(1, world.Revision);

            world.UpsertAabbCollider(collider);
            Assert.AreEqual(2, world.Revision);
            Assert.IsTrue(world.TryGetAabbCollider(bodyId, colliderId, out CombatPhysicsAabbCollider foundCollider));
            Assert.AreEqual(collider, foundCollider);
            Assert.AreEqual(new CombatPhysicsWorldStats(2, 1, 1), world.CreateStats());

            FixVector3 position = new FixVector3(Fix64.FromInt(3), Fix64.Zero, Fix64.Zero);
            Assert.IsTrue(world.SetBodyPosition(bodyId, position));
            Assert.AreEqual(3, world.Revision);
            Assert.IsTrue(world.TryGetBody(bodyId, out foundBody));
            Assert.AreEqual(position, foundBody.Position);

            Assert.IsTrue(world.SetBodyPosition(bodyId, position));
            Assert.AreEqual(3, world.Revision);

            Assert.IsTrue(world.MoveBody(bodyId, new FixVector3(Fix64.One, Fix64.Zero, Fix64.Zero)));
            Assert.AreEqual(4, world.Revision);
            Assert.IsTrue(world.TryGetBody(bodyId, out foundBody));
            Assert.AreEqual(new FixVector3(Fix64.FromInt(4), Fix64.Zero, Fix64.Zero), foundBody.Position);

            Assert.IsFalse(world.MoveBody(new CombatBodyId(404), FixVector3.Zero));
            Assert.AreEqual(4, world.Revision);

            Assert.IsTrue(world.RemoveCollider(bodyId, colliderId));
            Assert.AreEqual(5, world.Revision);
            Assert.AreEqual(new CombatPhysicsWorldStats(5, 1, 0), world.CreateStats());

            world.Clear();
            Assert.AreEqual(6, world.Revision);
            Assert.AreEqual(new CombatPhysicsWorldStats(6, 0, 0), world.CreateStats());

            world.Clear();
            Assert.AreEqual(6, world.Revision);
        }

        [Test]
        public void RuntimeLifecycleApi_CopiesBodiesAndCollidersInStableOrder()
        {
            var world = new CombatPhysicsWorld();
            world.UpsertBody(new CombatPhysicsBody(new CombatEntityId(4), new CombatBodyId(4), FixVector3.Zero));
            world.UpsertBody(new CombatPhysicsBody(new CombatEntityId(2), new CombatBodyId(2), FixVector3.Zero));
            world.UpsertAabbCollider(new CombatPhysicsAabbCollider(
                new CombatBodyId(4),
                new CombatColliderId(2),
                layer: 1,
                localMin: FixVector3.Zero,
                localMax: FixVector3.Zero));
            world.UpsertAabbCollider(new CombatPhysicsAabbCollider(
                new CombatBodyId(2),
                new CombatColliderId(3),
                layer: 1,
                localMin: FixVector3.Zero,
                localMax: FixVector3.Zero));
            world.UpsertAabbCollider(new CombatPhysicsAabbCollider(
                new CombatBodyId(2),
                new CombatColliderId(1),
                layer: 1,
                localMin: FixVector3.Zero,
                localMax: FixVector3.Zero));
            world.UpsertAabbCollider(new CombatPhysicsAabbCollider(
                new CombatBodyId(4),
                new CombatColliderId(1),
                layer: 1,
                localMin: FixVector3.Zero,
                localMax: FixVector3.Zero));
            var bodies = new List<CombatPhysicsBody>
            {
                new CombatPhysicsBody(new CombatEntityId(99), new CombatBodyId(99), FixVector3.Zero),
            };
            var colliders = new List<CombatPhysicsAabbCollider>
            {
                new CombatPhysicsAabbCollider(
                    new CombatBodyId(99),
                    new CombatColliderId(99),
                    layer: 1,
                    localMin: FixVector3.Zero,
                    localMax: FixVector3.Zero),
            };

            Assert.AreEqual(2, world.CopyBodiesTo(bodies));
            Assert.AreEqual(4, world.CopyAabbCollidersTo(colliders));

            Assert.AreEqual(99, bodies[0].BodyId.Value);
            Assert.AreEqual(2, bodies[1].BodyId.Value);
            Assert.AreEqual(4, bodies[2].BodyId.Value);
            Assert.AreEqual(99, colliders[0].BodyId.Value);
            Assert.AreEqual(2, colliders[1].BodyId.Value);
            Assert.AreEqual(1, colliders[1].ColliderId.Value);
            Assert.AreEqual(2, colliders[2].BodyId.Value);
            Assert.AreEqual(3, colliders[2].ColliderId.Value);
            Assert.AreEqual(4, colliders[3].BodyId.Value);
            Assert.AreEqual(1, colliders[3].ColliderId.Value);
            Assert.AreEqual(4, colliders[4].BodyId.Value);
            Assert.AreEqual(2, colliders[4].ColliderId.Value);
        }

        [Test]
        public void RuntimeLifecycleApi_TopologyRevisionChangesForTopologyButNotBodyPosition()
        {
            var world = new CombatPhysicsWorld();
            Assert.AreEqual(0, world.TopologyRevision);

            RegisterBodyWithAabb(world, entity: 1, body: 1, collider: 1, layer: 1, x: 0);
            int registeredRevision = world.TopologyRevision;
            Assert.Greater(registeredRevision, 0);

            Assert.IsTrue(world.SetBodyPosition(
                new CombatBodyId(1),
                new FixVector3(Fix64.FromInt(2), Fix64.Zero, Fix64.Zero)));
            Assert.AreEqual(registeredRevision, world.TopologyRevision);

            world.UpsertAabbCollider(new CombatPhysicsAabbCollider(
                new CombatBodyId(1),
                new CombatColliderId(1),
                layer: 1,
                localMin: FixVector3.Zero,
                localMax: new FixVector3(Fix64.FromInt(2), Fix64.One, Fix64.One)));
            Assert.Greater(world.TopologyRevision, registeredRevision);
        }

        private static CombatPhysicsWorld CreateWorld()
        {
            CombatPhysicsWorld world = new CombatPhysicsWorld();
            RegisterBodyWithAabb(world, entity: 1, body: 1, collider: 1, layer: 1, x: 0);
            RegisterBodyWithAabb(world, entity: 2, body: 2, collider: 1, layer: 1, x: 2);
            RegisterBodyWithAabb(world, entity: 3, body: 3, collider: 1, layer: 2, x: 2);
            return world;
        }

        private static CombatPhysicsWorld CreateSphereCapsuleWorld()
        {
            CombatPhysicsWorld world = new CombatPhysicsWorld();
            RegisterBodyWithSphereAt(world, entity: 2, body: 2, collider: 10, layer: 1, x: Fix64.FromInt(2), radius: Fix64.Half);
            RegisterBodyWithCapsuleAt(world, entity: 3, body: 3, collider: 20, layer: 1, x: Fix64.FromInt(4), radius: Fix64.Half);
            return world;
        }

        private static void AssertUnifiedMatchesOld(CombatPhysicsWorld world, CombatRayQuery query)
        {
            var oldResults = new List<CombatQueryResult>();
            var unifiedResults = new List<CombatQueryResult>();

            int oldCount = world.QueryRay(query, oldResults);
            int unifiedCount = world.Query(CombatPhysicsQuery.From(query), unifiedResults);

            Assert.AreEqual(oldCount, unifiedCount);
            AssertResultsEqual(oldResults, unifiedResults);
        }

        private static void AssertUnifiedMatchesOld(CombatPhysicsWorld world, CombatSphereQuery query)
        {
            var oldResults = new List<CombatQueryResult>();
            var unifiedResults = new List<CombatQueryResult>();

            int oldCount = world.QuerySphere(query, oldResults);
            int unifiedCount = world.Query(CombatPhysicsQuery.From(query), unifiedResults);

            Assert.AreEqual(oldCount, unifiedCount);
            AssertResultsEqual(oldResults, unifiedResults);
        }

        private static void AssertUnifiedMatchesOld(CombatPhysicsWorld world, CombatCapsuleQuery query)
        {
            var oldResults = new List<CombatQueryResult>();
            var unifiedResults = new List<CombatQueryResult>();

            int oldCount = world.QueryCapsule(query, oldResults);
            int unifiedCount = world.Query(CombatPhysicsQuery.From(query), unifiedResults);

            Assert.AreEqual(oldCount, unifiedCount);
            AssertResultsEqual(oldResults, unifiedResults);
        }

        private static void AssertUnifiedMatchesOld(CombatPhysicsWorld world, CombatAabbQuery query)
        {
            var oldResults = new List<CombatQueryResult>();
            var unifiedResults = new List<CombatQueryResult>();

            int oldCount = world.QueryAabb(query, oldResults);
            int unifiedCount = world.Query(CombatPhysicsQuery.From(query), unifiedResults);

            Assert.AreEqual(oldCount, unifiedCount);
            AssertResultsEqual(oldResults, unifiedResults);
        }

        private static void AssertUnifiedMatchesOld(CombatPhysicsWorld world, CombatSectorQuery query)
        {
            var oldResults = new List<CombatQueryResult>();
            var unifiedResults = new List<CombatQueryResult>();

            int oldCount = world.QuerySector(query, oldResults);
            int unifiedCount = world.Query(CombatPhysicsQuery.From(query), unifiedResults);

            Assert.AreEqual(oldCount, unifiedCount);
            AssertResultsEqual(oldResults, unifiedResults);
        }

        private static void AssertSpecializedMatchesUnified(CombatPhysicsWorld world, CombatRayQuery query)
        {
            var specialized = new List<CombatQueryResult>();
            var unified = new List<CombatQueryResult>();

            int specializedCount = world.QueryRay(query, specialized);
            int unifiedCount = world.Query(CombatPhysicsQuery.From(query), unified);

            Assert.AreEqual(specializedCount, unifiedCount);
            AssertResultsEqual(specialized, unified);
            AssertHasHit(specialized, entity: 2, CombatPhysicsShapeKind.Sphere);
            AssertHasHit(specialized, entity: 3, CombatPhysicsShapeKind.Capsule);
        }

        private static void AssertSpecializedMatchesUnified(CombatPhysicsWorld world, CombatSphereQuery query)
        {
            var specialized = new List<CombatQueryResult>();
            var unified = new List<CombatQueryResult>();

            int specializedCount = world.QuerySphere(query, specialized);
            int unifiedCount = world.Query(CombatPhysicsQuery.From(query), unified);

            Assert.AreEqual(specializedCount, unifiedCount);
            AssertResultsEqual(specialized, unified);
            AssertHasHit(specialized, entity: 2, CombatPhysicsShapeKind.Sphere);
            AssertHasHit(specialized, entity: 3, CombatPhysicsShapeKind.Capsule);
        }

        private static void AssertSpecializedMatchesUnified(CombatPhysicsWorld world, CombatCapsuleQuery query)
        {
            var specialized = new List<CombatQueryResult>();
            var unified = new List<CombatQueryResult>();

            int specializedCount = world.QueryCapsule(query, specialized);
            int unifiedCount = world.Query(CombatPhysicsQuery.From(query), unified);

            Assert.AreEqual(specializedCount, unifiedCount);
            AssertResultsEqual(specialized, unified);
            AssertHasHit(specialized, entity: 2, CombatPhysicsShapeKind.Sphere);
            AssertHasHit(specialized, entity: 3, CombatPhysicsShapeKind.Capsule);
        }

        private static void AssertSpecializedMatchesUnified(CombatPhysicsWorld world, CombatAabbQuery query)
        {
            var specialized = new List<CombatQueryResult>();
            var unified = new List<CombatQueryResult>();

            int specializedCount = world.QueryAabb(query, specialized);
            int unifiedCount = world.Query(CombatPhysicsQuery.From(query), unified);

            Assert.AreEqual(specializedCount, unifiedCount);
            AssertResultsEqual(specialized, unified);
            AssertHasHit(specialized, entity: 2, CombatPhysicsShapeKind.Sphere);
            AssertHasHit(specialized, entity: 3, CombatPhysicsShapeKind.Capsule);
        }

        private static void AssertSpecializedMatchesUnified(CombatPhysicsWorld world, CombatSectorQuery query)
        {
            var specialized = new List<CombatQueryResult>();
            var unified = new List<CombatQueryResult>();

            int specializedCount = world.QuerySector(query, specialized);
            int unifiedCount = world.Query(CombatPhysicsQuery.From(query), unified);

            Assert.AreEqual(specializedCount, unifiedCount);
            AssertResultsEqual(specialized, unified);
            AssertHasHit(specialized, entity: 2, CombatPhysicsShapeKind.Sphere);
            AssertHasHit(specialized, entity: 3, CombatPhysicsShapeKind.Capsule);
        }

        private static void AssertQueryHitsSphereAndCapsule(CombatPhysicsWorld world, CombatPhysicsQuery query)
        {
            var hits = new List<CombatQueryResult>();

            int count = world.Query(query, hits);

            Assert.AreEqual(2, count);
            AssertHasHit(hits, entity: 2, CombatPhysicsShapeKind.Sphere);
            AssertHasHit(hits, entity: 3, CombatPhysicsShapeKind.Capsule);
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
                Assert.AreEqual(expected[i].DebugReport.HitCount, actual[i].DebugReport.HitCount);
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

        private static void AssertHasHit(IReadOnlyList<CombatQueryResult> hits, int entity)
        {
            for (int i = 0; i < hits.Count; i++)
            {
                if (hits[i].TargetEntityId.Value == entity)
                {
                    return;
                }
            }

            Assert.Fail("Expected query to hit entity " + entity + ".");
        }

        private static void AssertHasHit(IReadOnlyList<CombatQueryResult> hits, int entity, CombatPhysicsShapeKind targetShapeKind)
        {
            for (int i = 0; i < hits.Count; i++)
            {
                if (hits[i].TargetEntityId.Value == entity)
                {
                    Assert.AreEqual(targetShapeKind, hits[i].TargetShapeKind);
                    return;
                }
            }

            Assert.Fail("Expected query to hit entity " + entity + " with target shape " + targetShapeKind + ".");
        }

        private static void AssertNoHit(IReadOnlyList<CombatQueryResult> hits, int entity)
        {
            for (int i = 0; i < hits.Count; i++)
            {
                if (hits[i].TargetEntityId.Value == entity)
                {
                    Assert.Fail("Expected query to miss entity " + entity + ".");
                }
            }
        }

        private static void RegisterBodyWithAabb(
            CombatPhysicsWorld world,
            int entity,
            int body,
            int collider,
            int layer,
            int x)
        {
            world.UpsertBody(new CombatPhysicsBody(
                new CombatEntityId(entity),
                new CombatBodyId(body),
                new FixVector3(Fix64.FromInt(x), Fix64.Zero, Fix64.Zero)));
            world.UpsertAabbCollider(new CombatPhysicsAabbCollider(
                new CombatBodyId(body),
                new CombatColliderId(collider),
                layer,
                new FixVector3(-Fix64.Half, -Fix64.Half, -Fix64.Half),
                new FixVector3(Fix64.Half, Fix64.Half, Fix64.Half)));
        }

        private static void RegisterBodyWithAabbAt(
            CombatPhysicsWorld world,
            int entity,
            int body,
            int collider,
            int layer,
            Fix64 x,
            Fix64 y,
            Fix64 z,
            Fix64 halfSize)
        {
            world.UpsertBody(new CombatPhysicsBody(
                new CombatEntityId(entity),
                new CombatBodyId(body),
                new FixVector3(x, y, z)));
            world.UpsertAabbCollider(new CombatPhysicsAabbCollider(
                new CombatBodyId(body),
                new CombatColliderId(collider),
                layer,
                new FixVector3(-halfSize, -halfSize, -halfSize),
                new FixVector3(halfSize, halfSize, halfSize)));
        }

        private static void RegisterBodyWithSphereAt(
            CombatPhysicsWorld world,
            int entity,
            int body,
            int collider,
            int layer,
            Fix64 x,
            Fix64 radius)
        {
            world.UpsertBody(new CombatPhysicsBody(
                new CombatEntityId(entity),
                new CombatBodyId(body),
                new FixVector3(x, Fix64.Zero, Fix64.Zero)));
            world.UpsertSphereCollider(new CombatPhysicsSphereCollider(
                new CombatBodyId(body),
                new CombatColliderId(collider),
                layer,
                FixVector3.Zero,
                radius,
                CombatSurfaceMaterial.HardSlick));
        }

        private static void RegisterBodyWithCapsuleAt(
            CombatPhysicsWorld world,
            int entity,
            int body,
            int collider,
            int layer,
            Fix64 x,
            Fix64 radius)
        {
            world.UpsertBody(new CombatPhysicsBody(
                new CombatEntityId(entity),
                new CombatBodyId(body),
                new FixVector3(x, Fix64.Zero, Fix64.Zero)));
            world.UpsertCapsuleCollider(new CombatPhysicsCapsuleCollider(
                new CombatBodyId(body),
                new CombatColliderId(collider),
                layer,
                -UnitY,
                UnitY,
                radius,
                CombatSurfaceMaterial.Rough));
        }

        private static FixVector3 UnitX => new FixVector3(Fix64.One, Fix64.Zero, Fix64.Zero);

        private static FixVector3 UnitY => new FixVector3(Fix64.Zero, Fix64.One, Fix64.Zero);

        private static FixVector3 UnitZ => new FixVector3(Fix64.Zero, Fix64.Zero, Fix64.One);

        private static CombatQueryHeader Header(
            CombatQueryKind kind,
            CombatPhysicsLayerMask layerMask,
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
                layerMask);
        }
    }
}
