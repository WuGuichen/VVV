using System;
using System.Collections.Generic;
using MxFramework.Combat.Core;
using MxFramework.Combat.Physics;
using MxFramework.Core.Math;
using NUnit.Framework;

namespace MxFramework.Tests.Combat.Physics
{
    public sealed class CombatGroundProbeTests
    {
        private const int GroundLayer = 0;
        private const int WorldStaticLayer = 1;
        private const int DynamicCharacterLayer = 8;
        private const int MovableLayer = 9;
        private const int HurtboxLayer = 10;
        private const int PresenceLayer = 11;

        private static readonly FixVector3 Down = new FixVector3(
            Fix64.Zero,
            -Fix64.One,
            Fix64.Zero);
        private static readonly FixVector3 AxisX = new FixVector3(
            Fix64.One,
            Fix64.Zero,
            Fix64.Zero);
        private static readonly FixVector3 AxisY = new FixVector3(
            Fix64.Zero,
            Fix64.One,
            Fix64.Zero);
        private static readonly FixVector3 AxisZ = new FixVector3(
            Fix64.Zero,
            Fix64.Zero,
            Fix64.One);
        private static readonly CombatPhysicsLayerMask StaticMask =
            CombatPhysicsLayerMask.FromLayer(GroundLayer).AddLayer(WorldStaticLayer);

        [Test]
        public void StopGate_StaticMaskHitsZeroAndOneButExcludesDynamicCharacterMovableHurtboxAndPresence()
        {
            var world = new CombatPhysicsWorld();
            AddFloor(world, 100, 100, GroundLayer, Fix64.Zero, CombatSurfaceMaterial.Rough);
            AddFloor(world, 101, 101, WorldStaticLayer, -Fix64.Half, CombatSurfaceMaterial.HardSlick);
            AddFloor(world, 108, 108, DynamicCharacterLayer, Fix64.FromRatio(3, 2), CombatSurfaceMaterial.Elastic);
            AddFloor(world, 109, 109, MovableLayer, Fix64.FromRatio(7, 5), CombatSurfaceMaterial.Elastic);
            AddFloor(world, 110, 110, HurtboxLayer, Fix64.FromRatio(13, 10), CombatSurfaceMaterial.Elastic);
            AddFloor(world, 111, 111, PresenceLayer, Fix64.FromRatio(6, 5), CombatSurfaceMaterial.Elastic);

            var allHits = new List<CombatQueryResult>();
            var allQuery = new CombatRayQuery(
                Header(900, CombatQueryKind.Ray, CombatPhysicsLayerMask.All),
                new FixVector3(Fix64.Zero, Fix64.FromInt(2), Fix64.Zero),
                Down,
                Fix64.FromInt(4));
            Assert.That(world.QueryRay(allQuery, allHits), Is.EqualTo(6));
            Assert.That(allHits[0].TargetBodyId, Is.EqualTo(new CombatBodyId(108)));

            CombatGroundProbeResult result = world.QueryGroundProbe(Probe(radius: Fix64.Zero));

            Assert.That(result.Status, Is.EqualTo(CombatGroundProbeStatus.Success));
            Assert.That(result.Hit.SurfaceBodyId, Is.EqualTo(new CombatBodyId(100)));
            Assert.That(result.Hit.SurfaceMaterial, Is.EqualTo(CombatSurfaceMaterial.Rough));
            Assert.That(result.Hit.SurfaceBodyId.Value, Is.Not.EqualTo(108));
            Assert.That(result.Hit.SurfaceBodyId.Value, Is.Not.EqualTo(109));
            Assert.That(result.Hit.SurfaceBodyId.Value, Is.Not.EqualTo(110));
            Assert.That(result.Hit.SurfaceBodyId.Value, Is.Not.EqualTo(111));

            var worldStaticOnly = new CombatGroundProbeQuery(
                902,
                CombatEntityId.None,
                0,
                new FixVector3(Fix64.Zero, Fix64.FromInt(2), Fix64.Zero),
                Down,
                Fix64.FromInt(4),
                Fix64.Zero,
                CombatPhysicsLayerMask.FromLayer(WorldStaticLayer),
                Fix64.FromRatio(4, 5));
            CombatGroundProbeResult worldStaticResult = world.QueryGroundProbe(worldStaticOnly);
            Assert.That(worldStaticResult.Status, Is.EqualTo(CombatGroundProbeStatus.Success));
            Assert.That(worldStaticResult.Hit.SurfaceBodyId, Is.EqualTo(new CombatBodyId(101)));
        }

        [Test]
        public void StopGate_CompoundLeafIdentityAndNonDefaultMaterialRoundTripExactly()
        {
            var world = new CombatPhysicsWorld();
            var bodyId = new CombatBodyId(200);
            var colliderId = new CombatColliderId(201);
            world.UpsertBody(new CombatPhysicsBody(new CombatEntityId(202), bodyId, FixVector3.Zero));
            world.BeginCompoundColliderUpdate(bodyId, 1);
            world.UpsertCompoundCollider(new CombatPhysicsCompoundLeaf(
                colliderId,
                WorldStaticLayer,
                CombatPhysicsShape.Obb(
                    new FixVector3(Fix64.Zero, -Fix64.Half, Fix64.Zero),
                    new FixVector3(Fix64.FromInt(4), Fix64.Half, Fix64.FromInt(4)),
                    AxisX,
                    AxisY,
                    AxisZ),
                CombatSurfaceMaterial.BouncePad,
                priority: 17));
            world.EndCompoundColliderUpdate();
            var hits = new List<CombatQueryResult>();
            var ray = new CombatRayQuery(
                Header(901, CombatQueryKind.Ray, StaticMask),
                new FixVector3(Fix64.Zero, Fix64.FromInt(2), Fix64.Zero),
                Down,
                Fix64.FromInt(4));

            Assert.That(world.QueryRay(ray, hits), Is.EqualTo(1));
            Assert.That(world.TryGetSurfaceMaterial(hits[0], out CombatSurfaceMaterial material), Is.True);
            Assert.That(material, Is.EqualTo(CombatSurfaceMaterial.BouncePad));

            CombatGroundProbeResult result = world.QueryGroundProbe(Probe(radius: Fix64.Zero));
            Assert.That(result.Status, Is.EqualTo(CombatGroundProbeStatus.Success));
            Assert.That(result.Hit.SurfaceEntityId, Is.EqualTo(new CombatEntityId(202)));
            Assert.That(result.Hit.SurfaceBodyId, Is.EqualTo(bodyId));
            Assert.That(result.Hit.SurfaceColliderId, Is.EqualTo(colliderId));
            Assert.That(result.Hit.SurfaceLeafId, Is.EqualTo(colliderId));
            Assert.That(result.Hit.SurfaceShapeKind, Is.EqualTo(CombatPhysicsShapeKind.Obb));
            Assert.That(result.Hit.SurfaceMaterial, Is.EqualTo(CombatSurfaceMaterial.BouncePad));
            Assert.That(result.Hit.SurfacePriority, Is.EqualTo(17));
        }

        [Test]
        public void FlatProbe_QuantizesDistancePointNormalAndSlopeWithoutChangingMaterialRawFields()
        {
            var world = new CombatPhysicsWorld();
            Fix64 top = Fix64.FromRatio(3333, 10000);
            var material = new CombatSurfaceMaterial(
                CombatSurfaceMaterialKind.Elastic,
                Fix64.FromRatio(17, 10),
                Fix64.FromRatio(3, 4),
                Fix64.FromRatio(1234, 1000));
            AddFloor(world, 300, 301, GroundLayer, top, material);
            var query = new CombatGroundProbeQuery(
                1,
                CombatEntityId.None,
                0,
                new FixVector3(Fix64.Zero, Fix64.FromRatio(20004, 10000), Fix64.Zero),
                Down,
                Fix64.FromInt(4),
                Fix64.Zero,
                StaticMask,
                Fix64.FromRatio(4, 5));

            CombatGroundProbeResult result = world.QueryGroundProbe(query);

            Assert.That(result.Status, Is.EqualTo(CombatGroundProbeStatus.Success));
            Assert.That(result.Hit.Distance.RawValue % 1000L, Is.Zero);
            Assert.That(result.Hit.Point.X.RawValue % 1000L, Is.Zero);
            Assert.That(result.Hit.Point.Y.RawValue % 1000L, Is.Zero);
            Assert.That(result.Hit.Point.Z.RawValue % 1000L, Is.Zero);
            Assert.That(result.Hit.Point.Y, Is.EqualTo(Fix64.FromRatio(333, 1000)));
            Assert.That(result.Hit.Normal, Is.EqualTo(AxisY));
            Assert.That(result.Hit.SlopeCosine, Is.EqualTo(Fix64.One));
            Assert.That(result.Hit.SurfaceMaterial, Is.EqualTo(material));
        }

        [Test]
        public void RadiusProbe_SelectsFirstAxialContactInsteadOfClosestWholeCapsulePair()
        {
            var world = new CombatPhysicsWorld();
            AddSphere(
                world,
                1200,
                1200,
                new FixVector3(Fix64.Zero, Fix64.Zero, Fix64.Zero),
                Fix64.Half,
                CombatSurfaceMaterial.Rough);
            AddSphere(
                world,
                1300,
                1300,
                new FixVector3(Fix64.Zero, Fix64.FromInt(2), Fix64.Zero),
                Fix64.Half,
                CombatSurfaceMaterial.HardSlick);
            var query = new CombatGroundProbeQuery(
                2,
                CombatEntityId.None,
                0,
                new FixVector3(Fix64.Zero, Fix64.FromInt(4), Fix64.Zero),
                Down,
                Fix64.FromInt(5),
                Fix64.FromRatio(1, 5),
                StaticMask,
                Fix64.FromRatio(4, 5));

            CombatGroundProbeResult result = world.QueryGroundProbe(query);

            Assert.That(result.Status, Is.EqualTo(CombatGroundProbeStatus.Success));
            Assert.That(result.Hit.SurfaceBodyId, Is.EqualTo(new CombatBodyId(1300)));
            Assert.That(result.Hit.Distance, Is.EqualTo(Fix64.FromRatio(13, 10)));
            Assert.That(result.Hit.Point, Is.EqualTo(
                new FixVector3(Fix64.Zero, Fix64.FromRatio(5, 2), Fix64.Zero)));
            Assert.That(result.Hit.Normal, Is.EqualTo(AxisY));
            Assert.That(result.Hit.SurfaceMaterial, Is.EqualTo(CombatSurfaceMaterial.HardSlick));
        }

        [Test]
        public void RadiusProbe_AnalyticCapsuleTargetReturnsAxialDistanceAndTargetSurfacePoint()
        {
            var world = new CombatPhysicsWorld();
            var bodyId = new CombatBodyId(1350);
            world.UpsertBody(new CombatPhysicsBody(new CombatEntityId(1350), bodyId, FixVector3.Zero));
            world.UpsertCapsuleCollider(new CombatPhysicsCapsuleCollider(
                bodyId,
                new CombatColliderId(1351),
                GroundLayer,
                new FixVector3(-Fix64.One, Fix64.Zero, Fix64.Zero),
                new FixVector3(Fix64.One, Fix64.Zero, Fix64.Zero),
                Fix64.Half,
                CombatSurfaceMaterial.Elastic));
            var query = new CombatGroundProbeQuery(
                3,
                CombatEntityId.None,
                0,
                new FixVector3(Fix64.Zero, Fix64.FromInt(3), Fix64.Zero),
                Down,
                Fix64.FromInt(4),
                Fix64.FromRatio(1, 5),
                StaticMask,
                Fix64.FromRatio(4, 5));

            CombatGroundProbeResult result = world.QueryGroundProbe(query);

            Assert.That(result.Status, Is.EqualTo(CombatGroundProbeStatus.Success));
            Assert.That(result.Hit.SurfaceShapeKind, Is.EqualTo(CombatPhysicsShapeKind.Capsule));
            Assert.That(result.Hit.Distance, Is.EqualTo(Fix64.FromRatio(23, 10)));
            Assert.That(result.Hit.Point, Is.EqualTo(
                new FixVector3(Fix64.Zero, Fix64.Half, Fix64.Zero)));
            Assert.That(result.Hit.Normal, Is.EqualTo(AxisY));
        }

        [Test]
        public void RadiusProbe_TangentAtDistanceLimitIsStable()
        {
            var world = new CombatPhysicsWorld();
            AddSphere(
                world,
                1370,
                1371,
                new FixVector3(Fix64.FromRatio(7, 10), Fix64.Zero, Fix64.Zero),
                Fix64.Half,
                CombatSurfaceMaterial.Rough);
            var query = new CombatGroundProbeQuery(
                4,
                CombatEntityId.None,
                0,
                new FixVector3(Fix64.Zero, Fix64.FromInt(2), Fix64.Zero),
                Down,
                Fix64.FromInt(2),
                Fix64.FromRatio(1, 5),
                StaticMask,
                Fix64.Zero);

            CombatGroundProbeResult expected = world.QueryGroundProbe(query);

            Assert.That(expected.Status, Is.EqualTo(CombatGroundProbeStatus.Success));
            Assert.That(expected.Hit.Distance, Is.EqualTo(Fix64.FromInt(2)));
            Assert.That(expected.Hit.Point, Is.EqualTo(
                new FixVector3(Fix64.FromRatio(1, 5), Fix64.Zero, Fix64.Zero)));
            Assert.That(expected.Hit.Normal, Is.EqualTo(
                new FixVector3(-Fix64.One, Fix64.Zero, Fix64.Zero)));
            for (int i = 0; i < 64; i++)
            {
                Assert.That(world.QueryGroundProbe(query), Is.EqualTo(expected));
            }
        }

        [Test]
        public void SlopedObbs_AcceptBothDirectionsAndRejectTooSteepSurface()
        {
            Fix64 sin30 = Fix64.Half;
            Fix64 cos30 = Fix64.FromRatio(866025, 1000000);
            CombatGroundProbeResult uphill = ProbeSlope(sin30, cos30, reverse: false, Fix64.FromRatio(4, 5));
            CombatGroundProbeResult downhill = ProbeSlope(sin30, cos30, reverse: true, Fix64.FromRatio(4, 5));

            Assert.That(uphill.Status, Is.EqualTo(CombatGroundProbeStatus.Success));
            Assert.That(downhill.Status, Is.EqualTo(CombatGroundProbeStatus.Success));
            Assert.That(uphill.Hit.Normal.X, Is.LessThan(Fix64.Zero));
            Assert.That(downhill.Hit.Normal.X, Is.GreaterThan(Fix64.Zero));
            Assert.That(uphill.Hit.SlopeCosine, Is.GreaterThan(Fix64.FromRatio(4, 5)));
            Assert.That(downhill.Hit.SlopeCosine, Is.EqualTo(uphill.Hit.SlopeCosine));

            Fix64 sin60 = cos30;
            Fix64 cos60 = Fix64.Half;
            CombatGroundProbeResult tooSteep = ProbeSlope(
                sin60,
                cos60,
                reverse: false,
                Fix64.FromRatio(4, 5));
            Assert.That(tooSteep.Status, Is.EqualTo(CombatGroundProbeStatus.TooSteep));
            Assert.That(tooSteep.HasHit, Is.False);
        }

        [Test]
        public void RadiusProbe_SelectsRaisedStepEdgeAndIsIndependentOfRegistrationOrder()
        {
            CombatGroundProbeResult forward = ProbeStep(reverse: false);
            CombatGroundProbeResult reverse = ProbeStep(reverse: true);

            Assert.That(forward.Status, Is.EqualTo(CombatGroundProbeStatus.Success));
            Assert.That(reverse.Status, Is.EqualTo(CombatGroundProbeStatus.Success));
            Assert.That(forward.Hit.SurfaceColliderId, Is.EqualTo(new CombatColliderId(402)));
            Assert.That(forward.Hit.Distance, Is.EqualTo(Fix64.FromRatio(13, 10)));
            Assert.That(forward.Hit.Point.Y, Is.EqualTo(Fix64.Half));
            Assert.That(reverse.Hit, Is.EqualTo(forward.Hit));
            Assert.That(
                CombatGroundProbeHitComparer.Instance.Compare(forward.Hit, reverse.Hit),
                Is.Zero);
        }

        [Test]
        public void EqualDistanceAndPriority_UseStableIdentityInsteadOfContainerOrder()
        {
            CombatGroundProbeResult forward = ProbeEqualSurfaces(reverse: false);
            CombatGroundProbeResult reverse = ProbeEqualSurfaces(reverse: true);

            Assert.That(forward.Status, Is.EqualTo(CombatGroundProbeStatus.Success));
            Assert.That(reverse.Status, Is.EqualTo(CombatGroundProbeStatus.Success));
            Assert.That(forward.Hit.SurfaceEntityId, Is.EqualTo(new CombatEntityId(501)));
            Assert.That(reverse.Hit, Is.EqualTo(forward.Hit));
        }

        [Test]
        public void Failures_AreTypedAndNeverExposeFabricatedTargets()
        {
            var empty = new CombatPhysicsWorld();
            AssertFailure(empty.QueryGroundProbe(Probe(radius: Fix64.Zero)), CombatGroundProbeStatus.NoHit);

            var invalidDirection = new CombatGroundProbeQuery(
                1,
                CombatEntityId.None,
                0,
                FixVector3.Zero,
                FixVector3.Zero,
                Fix64.One,
                Fix64.Zero,
                StaticMask,
                Fix64.Zero);
            AssertFailure(
                empty.QueryGroundProbe(invalidDirection),
                CombatGroundProbeStatus.InvalidInput);

            var outOfRange = new CombatGroundProbeQuery(
                1,
                CombatEntityId.None,
                0,
                FixVector3.Zero,
                Down,
                Fix64.FromInt(101),
                Fix64.Zero,
                StaticMask,
                Fix64.Zero);
            AssertFailure(
                empty.QueryGroundProbe(outOfRange),
                CombatGroundProbeStatus.OutOfRange);

            var maximumBounds = new CombatGroundProbeQuery(
                1,
                CombatEntityId.None,
                0,
                FixVector3.Zero,
                Down,
                Fix64.FromInt(100),
                Fix64.FromInt(100),
                StaticMask,
                Fix64.Zero);
            AssertFailure(
                empty.QueryGroundProbe(maximumBounds),
                CombatGroundProbeStatus.NoHit);

            var nonUnitDirection = new CombatGroundProbeQuery(
                1,
                CombatEntityId.None,
                0,
                FixVector3.Zero,
                new FixVector3(Fix64.Zero, -Fix64.Half, Fix64.Zero),
                Fix64.One,
                Fix64.Zero,
                StaticMask,
                Fix64.Zero);
            AssertFailure(
                empty.QueryGroundProbe(nonUnitDirection),
                CombatGroundProbeStatus.InvalidInput);

            var endpointOverflow = new CombatGroundProbeQuery(
                1,
                CombatEntityId.None,
                0,
                new FixVector3(Fix64.Zero, Fix64.FromRaw(long.MinValue + 10L), Fix64.Zero),
                Down,
                Fix64.One,
                Fix64.Zero,
                StaticMask,
                Fix64.Zero);
            AssertFailure(
                empty.QueryGroundProbe(endpointOverflow),
                CombatGroundProbeStatus.OutOfRange);

            var unsupported = new CombatPhysicsWorld();
            var bodyId = new CombatBodyId(601);
            unsupported.UpsertBody(new CombatPhysicsBody(
                new CombatEntityId(602),
                bodyId,
                new FixVector3(Fix64.Zero, Fix64.FromInt(2), Fix64.Zero)));
            unsupported.UpsertSphereCollider(new CombatPhysicsSphereCollider(
                bodyId,
                new CombatColliderId(603),
                GroundLayer,
                FixVector3.Zero,
                Fix64.One,
                CombatSurfaceMaterial.Rough));
            AssertFailure(
                unsupported.QueryGroundProbe(Probe(radius: Fix64.Zero)),
                CombatGroundProbeStatus.UnsupportedShape);
        }

        [Test]
        public void AmbiguousPrimitiveAndCompoundIdentity_FailsClosedWithoutPickingMaterial()
        {
            var world = new CombatPhysicsWorld();
            var bodyId = new CombatBodyId(700);
            var colliderId = new CombatColliderId(701);
            world.UpsertBody(new CombatPhysicsBody(new CombatEntityId(702), bodyId, FixVector3.Zero));
            world.UpsertSphereCollider(new CombatPhysicsSphereCollider(
                bodyId,
                colliderId,
                GroundLayer,
                new FixVector3(Fix64.Zero, Fix64.Zero, Fix64.Zero),
                Fix64.One,
                CombatSurfaceMaterial.Rough));
            world.BeginCompoundColliderUpdate(bodyId, 1);
            world.UpsertCompoundCollider(new CombatPhysicsCompoundLeaf(
                colliderId,
                GroundLayer,
                CombatPhysicsShape.Sphere(FixVector3.Zero, Fix64.One),
                CombatSurfaceMaterial.BouncePad));
            world.EndCompoundColliderUpdate();
            var query = new CombatGroundProbeQuery(
                1,
                CombatEntityId.None,
                0,
                new FixVector3(Fix64.Zero, Fix64.FromInt(3), Fix64.Zero),
                Down,
                Fix64.FromInt(4),
                Fix64.Zero,
                StaticMask,
                Fix64.Zero);

            CombatGroundProbeResult result = world.QueryGroundProbe(query);

            AssertFailure(result, CombatGroundProbeStatus.SurfaceMaterialUnavailable);
        }

        [Test]
        public void NonzeroRadiusSlopedObb_ReportsSurfaceGeometryAndMirrorsDeterministically()
        {
            Fix64 sin = Fix64.Half;
            Fix64 cos = Fix64.FromRatio(866025, 1000000);
            var world = new CombatPhysicsWorld();
            var mirroredWorld = new CombatPhysicsWorld();
            AddSlope(world, 800, 801, sin, cos, reverse: false);
            AddSlope(mirroredWorld, 800, 801, sin, cos, reverse: true);
            CombatGroundProbeQuery query = Probe(radius: Fix64.FromRatio(3, 20));
            CombatGroundProbeResult expected = world.QueryGroundProbe(query);
            CombatGroundProbeResult mirrored = mirroredWorld.QueryGroundProbe(query);

            Assert.That(expected.Status, Is.EqualTo(CombatGroundProbeStatus.Success));
            Assert.That(mirrored.Status, Is.EqualTo(CombatGroundProbeStatus.Success));
            Assert.That(expected.Hit.SurfaceShapeKind, Is.EqualTo(CombatPhysicsShapeKind.Obb));
            Assert.That(expected.Hit.Distance, Is.EqualTo(Fix64.FromRatio(1811, 1000)));
            Assert.That(mirrored.Hit.Distance, Is.EqualTo(expected.Hit.Distance));
            Assert.That(expected.Hit.Point, Is.EqualTo(new FixVector3(
                Fix64.FromRatio(75, 1000),
                Fix64.FromRatio(59, 1000),
                Fix64.Zero)));
            Assert.That(mirrored.Hit.Point, Is.EqualTo(new FixVector3(
                Fix64.FromRatio(-75, 1000),
                Fix64.FromRatio(59, 1000),
                Fix64.Zero)));
            Assert.That(expected.Hit.Normal, Is.EqualTo(new FixVector3(
                Fix64.FromRaw(-500012),
                Fix64.FromRaw(866019),
                Fix64.Zero)));
            Assert.That(mirrored.Hit.Normal, Is.EqualTo(new FixVector3(
                Fix64.FromRaw(500012),
                Fix64.FromRaw(866019),
                Fix64.Zero)));
            Assert.That(expected.Hit.SlopeCosine, Is.EqualTo(Fix64.FromRaw(866023)));
            Assert.That(mirrored.Hit.SlopeCosine, Is.EqualTo(expected.Hit.SlopeCosine));
            for (int i = 0; i < 512; i++)
            {
                Assert.That(world.QueryGroundProbe(query), Is.EqualTo(expected));
                Assert.That(mirroredWorld.QueryGroundProbe(query), Is.EqualTo(mirrored));
            }
        }

        [Test]
        public void FixedPointReplay_ReturnsIdenticalQuantizedResult()
        {
            var world = new CombatPhysicsWorld();
            AddSlope(world, 820, 821, Fix64.Half, Fix64.FromRatio(866025, 1000000), reverse: false);
            CombatGroundProbeQuery query = Probe(radius: Fix64.FromRatio(3, 20));
            CombatGroundProbeResult expected = world.QueryGroundProbe(query);

            Assert.That(expected.Status, Is.EqualTo(CombatGroundProbeStatus.Success));
            for (int i = 0; i < 512; i++)
            {
                Assert.That(world.QueryGroundProbe(query), Is.EqualTo(expected));
            }
        }

        [Test]
        public void WarmedGroundProbe_IsNoAlloc()
        {
            var world = new CombatPhysicsWorld();
            AddFloor(world, 900, 901, GroundLayer, Fix64.Zero, CombatSurfaceMaterial.Rough);
            CombatGroundProbeQuery query = Probe(radius: Fix64.FromRatio(1, 5));
            for (int i = 0; i < 32; i++)
            {
                world.QueryGroundProbe(query);
            }

            _ = GC.GetAllocatedBytesForCurrentThread();
            long before = GC.GetAllocatedBytesForCurrentThread();
            int checksum = 0;
            for (int i = 0; i < 4096; i++)
            {
                CombatGroundProbeResult result = world.QueryGroundProbe(query);
                checksum ^= result.Hit.SurfaceColliderId.Value;
            }

            long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.That(allocated, Is.Zero);
            Assert.That(checksum, Is.Zero);
        }

        [Test]
        public void ExistingRayAndCapsuleQueries_RemainCompatible()
        {
            var world = new CombatPhysicsWorld();
            AddFloor(world, 1000, 1001, GroundLayer, Fix64.Zero, CombatSurfaceMaterial.Rough);
            var rayHits = new List<CombatQueryResult>();
            var capsuleHits = new List<CombatQueryResult>();
            var origin = new FixVector3(Fix64.Zero, Fix64.FromInt(2), Fix64.Zero);

            int rayCount = world.QueryRay(
                new CombatRayQuery(
                    Header(100, CombatQueryKind.Ray, StaticMask),
                    origin,
                    Down,
                    Fix64.FromInt(4)),
                rayHits);
            int capsuleCount = world.QueryCapsule(
                new CombatCapsuleQuery(
                    Header(101, CombatQueryKind.Capsule, StaticMask),
                    origin,
                    origin + Down * Fix64.FromInt(4),
                    Fix64.FromRatio(1, 5)),
                capsuleHits);

            Assert.That(rayCount, Is.EqualTo(1));
            Assert.That(capsuleCount, Is.EqualTo(1));
            Assert.That(rayHits[0].TargetColliderId, Is.EqualTo(new CombatColliderId(1001)));
            Assert.That(capsuleHits[0].TargetColliderId, Is.EqualTo(new CombatColliderId(1001)));
            Assert.That(rayHits[0].Normal, Is.EqualTo(AxisY));
            Assert.That(capsuleHits[0].Normal, Is.EqualTo(AxisY));
        }

        private static CombatGroundProbeResult ProbeSlope(
            Fix64 sin,
            Fix64 cos,
            bool reverse,
            Fix64 minimumSlopeCosine)
        {
            var world = new CombatPhysicsWorld();
            AddSlope(world, 350, 351, sin, cos, reverse);
            CombatGroundProbeQuery query = new CombatGroundProbeQuery(
                1,
                CombatEntityId.None,
                0,
                new FixVector3(Fix64.Zero, Fix64.FromInt(2), Fix64.Zero),
                Down,
                Fix64.FromInt(4),
                Fix64.Zero,
                StaticMask,
                minimumSlopeCosine);
            return world.QueryGroundProbe(query);
        }

        private static CombatGroundProbeResult ProbeStep(bool reverse)
        {
            var world = new CombatPhysicsWorld();
            if (reverse)
            {
                AddStep(world, 401, 401, -Fix64.One, Fix64.Zero, Fix64.Zero);
                AddStep(world, 402, 402, Fix64.Zero, Fix64.One, Fix64.Half);
            }
            else
            {
                AddStep(world, 402, 402, Fix64.Zero, Fix64.One, Fix64.Half);
                AddStep(world, 401, 401, -Fix64.One, Fix64.Zero, Fix64.Zero);
            }

            var query = new CombatGroundProbeQuery(
                1,
                CombatEntityId.None,
                0,
                new FixVector3(Fix64.FromRatio(3, 20), Fix64.FromInt(2), Fix64.Zero),
                Down,
                Fix64.FromInt(4),
                Fix64.FromRatio(1, 5),
                StaticMask,
                Fix64.FromRatio(4, 5));
            return world.QueryGroundProbe(query);
        }

        private static CombatGroundProbeResult ProbeEqualSurfaces(bool reverse)
        {
            var world = new CombatPhysicsWorld();
            if (reverse)
            {
                AddFloor(world, 502, 502, GroundLayer, Fix64.Zero, CombatSurfaceMaterial.Rough);
                AddFloor(world, 501, 501, GroundLayer, Fix64.Zero, CombatSurfaceMaterial.Rough);
            }
            else
            {
                AddFloor(world, 501, 501, GroundLayer, Fix64.Zero, CombatSurfaceMaterial.Rough);
                AddFloor(world, 502, 502, GroundLayer, Fix64.Zero, CombatSurfaceMaterial.Rough);
            }

            return world.QueryGroundProbe(Probe(radius: Fix64.Zero));
        }

        private static CombatGroundProbeQuery Probe(Fix64 radius)
        {
            return new CombatGroundProbeQuery(
                1,
                CombatEntityId.None,
                0,
                new FixVector3(Fix64.Zero, Fix64.FromInt(2), Fix64.Zero),
                Down,
                Fix64.FromInt(4),
                radius,
                StaticMask,
                Fix64.FromRatio(4, 5));
        }

        private static void AddFloor(
            CombatPhysicsWorld world,
            int bodyValue,
            int colliderValue,
            int layer,
            Fix64 top,
            CombatSurfaceMaterial material)
        {
            var bodyId = new CombatBodyId(bodyValue);
            world.UpsertBody(new CombatPhysicsBody(new CombatEntityId(bodyValue), bodyId, FixVector3.Zero));
            world.UpsertAabbCollider(new CombatPhysicsAabbCollider(
                bodyId,
                new CombatColliderId(colliderValue),
                layer,
                new FixVector3(-Fix64.FromInt(4), top - Fix64.One, -Fix64.FromInt(4)),
                new FixVector3(Fix64.FromInt(4), top, Fix64.FromInt(4)),
                material));
        }

        private static void AddStep(
            CombatPhysicsWorld world,
            int bodyValue,
            int colliderValue,
            Fix64 minX,
            Fix64 maxX,
            Fix64 top)
        {
            var bodyId = new CombatBodyId(bodyValue);
            world.UpsertBody(new CombatPhysicsBody(new CombatEntityId(bodyValue), bodyId, FixVector3.Zero));
            world.UpsertAabbCollider(new CombatPhysicsAabbCollider(
                bodyId,
                new CombatColliderId(colliderValue),
                GroundLayer,
                new FixVector3(minX, top - Fix64.One, -Fix64.One),
                new FixVector3(maxX, top, Fix64.One),
                CombatSurfaceMaterial.Rough));
        }

        private static void AddSphere(
            CombatPhysicsWorld world,
            int bodyValue,
            int colliderValue,
            FixVector3 center,
            Fix64 radius,
            CombatSurfaceMaterial material)
        {
            var bodyId = new CombatBodyId(bodyValue);
            world.UpsertBody(new CombatPhysicsBody(new CombatEntityId(bodyValue), bodyId, FixVector3.Zero));
            world.UpsertSphereCollider(new CombatPhysicsSphereCollider(
                bodyId,
                new CombatColliderId(colliderValue),
                GroundLayer,
                center,
                radius,
                material));
        }

        private static void AddSlope(
            CombatPhysicsWorld world,
            int bodyValue,
            int colliderValue,
            Fix64 sin,
            Fix64 cos,
            bool reverse)
        {
            if (reverse) sin = -sin;
            var bodyId = new CombatBodyId(bodyValue);
            world.UpsertBody(new CombatPhysicsBody(new CombatEntityId(bodyValue), bodyId, FixVector3.Zero));
            world.UpsertObbCollider(new CombatPhysicsObbCollider(
                bodyId,
                new CombatColliderId(colliderValue),
                GroundLayer,
                new FixVector3(Fix64.Zero, -Fix64.FromRatio(1, 10), Fix64.Zero),
                new FixVector3(Fix64.FromInt(2), Fix64.FromRatio(1, 10), Fix64.FromInt(2)),
                new FixVector3(cos, sin, Fix64.Zero),
                new FixVector3(-sin, cos, Fix64.Zero),
                AxisZ,
                CombatSurfaceMaterial.Rough));
        }

        private static CombatQueryHeader Header(
            int queryId,
            CombatQueryKind kind,
            CombatPhysicsLayerMask mask)
        {
            return new CombatQueryHeader(
                queryId,
                kind,
                CombatEntityId.None,
                0,
                0,
                0,
                mask);
        }

        private static void AssertFailure(
            CombatGroundProbeResult result,
            CombatGroundProbeStatus expected)
        {
            Assert.That(result.Status, Is.EqualTo(expected));
            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.HasHit, Is.False);
        }
    }
}
