using System;
using System.Collections.Generic;
using MxFramework.Combat.Core;
using MxFramework.Combat.Physics;
using MxFramework.Core.Math;
using NUnit.Framework;

namespace MxFramework.Tests.Combat.Physics
{
    public sealed class CombatShapeSeparationTests
    {
        private const int Layer = 9;
        private static readonly FixVector3 AxisX = new FixVector3(Fix64.One, Fix64.Zero, Fix64.Zero);
        private static readonly FixVector3 AxisY = new FixVector3(Fix64.Zero, Fix64.One, Fix64.Zero);
        private static readonly FixVector3 AxisZ = new FixVector3(Fix64.Zero, Fix64.Zero, Fix64.One);

        [Test]
        public void CapsuleSphere_ReportsSeparatedTouchingOverlapAndStableCoincidentFallback()
        {
            CombatCapsuleQuery source = Query(FixVector3.Zero, FixVector3.Zero, Fix64.One);

            CombatShapeSeparationResult separated = Separate(
                source,
                CombatPhysicsShape.Sphere(new FixVector3(Fix64.FromInt(3), Fix64.Zero, Fix64.Zero), Fix64.One));
            CombatShapeSeparationResult touching = Separate(
                source,
                CombatPhysicsShape.Sphere(new FixVector3(Fix64.FromInt(2), Fix64.Zero, Fix64.Zero), Fix64.One));
            CombatShapeSeparationResult overlapping = Separate(
                source,
                CombatPhysicsShape.Sphere(new FixVector3(Fix64.FromRatio(3, 2), Fix64.Zero, Fix64.Zero), Fix64.One));
            CombatShapeSeparationResult coincident = Separate(
                source,
                CombatPhysicsShape.Sphere(FixVector3.Zero, Fix64.One));

            Assert.That(separated.Status, Is.EqualTo(CombatShapeSeparationStatus.Separated));
            Assert.That(separated.HasContact, Is.False);
            Assert.That(touching.Status, Is.EqualTo(CombatShapeSeparationStatus.Touching));
            Assert.That(touching.Normal, Is.EqualTo(-AxisX));
            Assert.That(touching.ContactPoint, Is.EqualTo(new FixVector3(Fix64.One, Fix64.Zero, Fix64.Zero)));
            Assert.That(overlapping.Status, Is.EqualTo(CombatShapeSeparationStatus.Overlapping));
            Assert.That(overlapping.Penetration, Is.EqualTo(Fix64.Half));
            Assert.That(coincident.Normal, Is.EqualTo(AxisX));
            Assert.That(coincident.Penetration, Is.EqualTo(Fix64.FromInt(2)));
            AssertNormalized(touching.Normal);
            AssertNormalized(coincident.Normal);
        }

        [Test]
        public void CapsuleAabb_CoversBoundaryInsideZeroLengthAndNonOverlap()
        {
            CombatPhysicsShape box = CombatPhysicsShape.Aabb(
                new FixVector3(-Fix64.One, -Fix64.One, -Fix64.One),
                new FixVector3(Fix64.One, Fix64.One, Fix64.One));

            CombatShapeSeparationResult touching = Separate(
                Query(new FixVector3(Fix64.FromInt(2), Fix64.Zero, Fix64.Zero), new FixVector3(Fix64.FromInt(2), Fix64.Zero, Fix64.Zero), Fix64.One),
                box);
            CombatShapeSeparationResult inside = Separate(
                Query(FixVector3.Zero, FixVector3.Zero, Fix64.FromRatio(1, 4)),
                box);
            CombatShapeSeparationResult boundaryPoint = Separate(
                Query(new FixVector3(Fix64.One, Fix64.Zero, Fix64.Zero), new FixVector3(Fix64.One, Fix64.Zero, Fix64.Zero), Fix64.Zero),
                box);
            CombatShapeSeparationResult separated = Separate(
                Query(new FixVector3(Fix64.FromInt(3), Fix64.Zero, Fix64.Zero), new FixVector3(Fix64.FromInt(3), Fix64.Zero, Fix64.Zero), Fix64.One),
                box);

            Assert.That(touching.Status, Is.EqualTo(CombatShapeSeparationStatus.Touching));
            Assert.That(touching.Normal, Is.EqualTo(AxisX));
            Assert.That(touching.ContactPoint, Is.EqualTo(new FixVector3(Fix64.One, Fix64.Zero, Fix64.Zero)));
            Assert.That(inside.Status, Is.EqualTo(CombatShapeSeparationStatus.Overlapping));
            Assert.That(inside.Normal, Is.EqualTo(AxisX));
            Assert.That(inside.Penetration, Is.EqualTo(Fix64.FromRatio(5, 4)));
            Assert.That(inside.ContactPoint, Is.EqualTo(new FixVector3(Fix64.One, Fix64.Zero, Fix64.Zero)));
            Assert.That(boundaryPoint.Status, Is.EqualTo(CombatShapeSeparationStatus.Touching));
            Assert.That(separated.Status, Is.EqualTo(CombatShapeSeparationStatus.Separated));
        }

        [Test]
        public void CapsuleCapsule_CoversParallelCoincidentAndTouchingAxes()
        {
            FixVector3 bottom = new FixVector3(Fix64.Zero, -Fix64.One, Fix64.Zero);
            FixVector3 top = new FixVector3(Fix64.Zero, Fix64.One, Fix64.Zero);
            CombatCapsuleQuery source = Query(bottom, top, Fix64.One);

            CombatShapeSeparationResult coincident = Separate(
                source,
                CombatPhysicsShape.Capsule(bottom, top, Fix64.One));
            CombatShapeSeparationResult touching = Separate(
                source,
                CombatPhysicsShape.Capsule(
                    bottom + new FixVector3(Fix64.FromInt(2), Fix64.Zero, Fix64.Zero),
                    top + new FixVector3(Fix64.FromInt(2), Fix64.Zero, Fix64.Zero),
                    Fix64.One));
            CombatShapeSeparationResult separated = Separate(
                source,
                CombatPhysicsShape.Capsule(
                    bottom + new FixVector3(Fix64.FromInt(3), Fix64.Zero, Fix64.Zero),
                    top + new FixVector3(Fix64.FromInt(3), Fix64.Zero, Fix64.Zero),
                    Fix64.One));

            Assert.That(coincident.Status, Is.EqualTo(CombatShapeSeparationStatus.Overlapping));
            Assert.That(coincident.Normal, Is.EqualTo(AxisX));
            Assert.That(coincident.Penetration, Is.EqualTo(Fix64.FromInt(2)));
            Assert.That(touching.Status, Is.EqualTo(CombatShapeSeparationStatus.Touching));
            Assert.That(touching.Normal, Is.EqualTo(-AxisX));
            Assert.That(separated.Status, Is.EqualTo(CombatShapeSeparationStatus.Separated));
        }

        [Test]
        public void CapsuleCapsule_NearlyParallelEndpointMinimumReportsOverlap()
        {
            CombatCapsuleQuery source = Query(
                FixVector3.Zero,
                new FixVector3(Fix64.FromInt(20), Fix64.Zero, Fix64.Zero),
                Fix64.FromRatio(1, 10));
            CombatPhysicsShape target = CombatPhysicsShape.Capsule(
                new FixVector3(Fix64.Zero, Fix64.FromRatio(21, 100), Fix64.Zero),
                new FixVector3(Fix64.FromInt(20), Fix64.FromRatio(15, 100), Fix64.Zero),
                Fix64.FromRatio(1, 10));

            CombatShapeSeparationResult result = Separate(source, target);

            Assert.That(result.Status, Is.EqualTo(CombatShapeSeparationStatus.Overlapping));
            Assert.That(result.Penetration.RawValue, Is.EqualTo(Fix64.FromRatio(5, 100).RawValue).Within(2));
            Assert.That(result.Normal, Is.EqualTo(-AxisY));
        }

        [Test]
        public void CapsuleCapsule_CollinearOffsetUsesPerpendicularThatActuallySeparates()
        {
            CombatCapsuleQuery source = Query(
                FixVector3.Zero,
                new FixVector3(Fix64.FromInt(10), Fix64.Zero, Fix64.Zero),
                Fix64.One);
            CombatPhysicsShape target = CombatPhysicsShape.Capsule(
                new FixVector3(Fix64.One, Fix64.Zero, Fix64.Zero),
                new FixVector3(Fix64.FromInt(11), Fix64.Zero, Fix64.Zero),
                Fix64.One);

            CombatShapeSeparationResult result = Separate(source, target);
            CombatCapsuleQuery movedSource = Query(
                source.PointA + result.Normal * result.Penetration,
                source.PointB + result.Normal * result.Penetration,
                source.Radius);
            CombatShapeSeparationResult afterMove = Separate(movedSource, target);

            Assert.That(result.Status, Is.EqualTo(CombatShapeSeparationStatus.Overlapping));
            Assert.That(result.Normal, Is.EqualTo(AxisY));
            Assert.That(result.Penetration, Is.EqualTo(Fix64.FromInt(2)));
            Assert.That(afterMove.Status, Is.EqualTo(CombatShapeSeparationStatus.Touching));
        }

        [Test]
        public void CapsuleObb_TransformsContactAndRejectsSkewOrOverflow()
        {
            Fix64 diagonal = Fix64.FromRatio(707107, 1000000);
            var rotatedX = new FixVector3(diagonal, Fix64.Zero, diagonal);
            var rotatedZ = new FixVector3(-diagonal, Fix64.Zero, diagonal);
            CombatPhysicsShape rotated = CombatPhysicsShape.Obb(
                FixVector3.Zero,
                new FixVector3(Fix64.One, Fix64.One, Fix64.One),
                rotatedX,
                AxisY,
                rotatedZ);
            FixVector3 sourceCenter = rotated.AxisX * Fix64.FromRatio(3, 2);
            CombatShapeSeparationResult touching = Separate(
                Query(sourceCenter, sourceCenter, Fix64.Half),
                rotated);
            CombatShapeSeparationResult overlapping = Separate(
                Query(rotated.AxisX * Fix64.FromRatio(5, 4), rotated.AxisX * Fix64.FromRatio(5, 4), Fix64.Half),
                rotated);
            CombatShapeSeparationResult separated = Separate(
                Query(rotated.AxisX * Fix64.FromInt(2), rotated.AxisX * Fix64.FromInt(2), Fix64.Half),
                rotated);

            CombatPhysicsShape skew = CombatPhysicsShape.Obb(
                FixVector3.Zero,
                new FixVector3(Fix64.One, Fix64.One, Fix64.One),
                AxisX,
                AxisX,
                AxisZ);
            CombatShapeSeparationResult invalidBasis = Separate(Query(FixVector3.Zero, FixVector3.Zero, Fix64.One), skew);
            CombatShapeSeparationResult overflow = Separate(
                Query(FixVector3.Zero, FixVector3.Zero, Fix64.One),
                CombatPhysicsShape.Sphere(
                    new FixVector3(Fix64.FromRaw(long.MinValue), Fix64.Zero, Fix64.Zero),
                    Fix64.One));
            CombatShapeSeparationResult unsupported = Separate(
                Query(FixVector3.Zero, FixVector3.Zero, Fix64.One),
                CombatPhysicsShape.Ray(FixVector3.Zero, AxisX, Fix64.One));

            Assert.That(touching.Status, Is.EqualTo(CombatShapeSeparationStatus.Touching));
            Assert.That(touching.Normal.X.RawValue, Is.EqualTo(rotated.AxisX.X.RawValue).Within(2));
            Assert.That(touching.Normal.Z.RawValue, Is.EqualTo(rotated.AxisX.Z.RawValue).Within(2));
            AssertNormalized(touching.Normal);
            Assert.That(overlapping.Status, Is.EqualTo(CombatShapeSeparationStatus.Overlapping));
            Assert.That(overlapping.Penetration.RawValue, Is.EqualTo(Fix64.FromRatio(1, 4).RawValue).Within(4));
            Assert.That(separated.Status, Is.EqualTo(CombatShapeSeparationStatus.Separated));
            Assert.That(invalidBasis.Status, Is.EqualTo(CombatShapeSeparationStatus.InvalidShape));
            Assert.That(invalidBasis.HasContact, Is.False);
            Assert.That(overflow.Status, Is.EqualTo(CombatShapeSeparationStatus.InvalidShape));
            Assert.That(unsupported.Status, Is.EqualTo(CombatShapeSeparationStatus.UnsupportedShape));
            for (int i = 0; i < 128; i++)
                Assert.That(Separate(Query(sourceCenter, sourceCenter, Fix64.Half), rotated), Is.EqualTo(touching));
        }

        [Test]
        public void BulkQuery_IsStableAcrossInsertionAndDuplicateLeafOrder()
        {
            CombatPhysicsWorld forward = CreatePermutationWorld(reverse: false);
            CombatPhysicsWorld reverse = CreatePermutationWorld(reverse: true);
            CombatCapsuleQuery source = Query(FixVector3.Zero, FixVector3.Zero, Fix64.One);
            var forwardResults = new List<CombatShapeSeparationResult>(4);
            var reverseResults = new List<CombatShapeSeparationResult>(4);

            Assert.That(forward.QueryCapsuleSeparations(source, forwardResults), Is.EqualTo(3));
            Assert.That(reverse.QueryCapsuleSeparations(source, reverseResults), Is.EqualTo(3));
            CollectionAssert.AreEqual(forwardResults, reverseResults);
            Assert.That(forwardResults[1].TargetCompoundLeafId, Is.EqualTo(new CombatColliderId(301)));
            Assert.That(forwardResults[2].TargetCompoundLeafId, Is.EqualTo(new CombatColliderId(301)));
        }

        [Test]
        public void BulkQuery_Warmed4096IterationsAllocateZeroBytes_AndLegacyQueryStillHits()
        {
            CombatPhysicsWorld world = CreatePerformanceWorld();
            CombatCapsuleQuery source = Query(FixVector3.Zero, FixVector3.Zero, Fix64.One);
            var separations = new List<CombatShapeSeparationResult>(8);
            var legacyHits = new List<CombatQueryResult>(8);

            for (int i = 0; i < 16; i++)
            {
                separations.Clear();
                world.QueryCapsuleSeparations(source, separations);
            }

            _ = GC.GetAllocatedBytesForCurrentThread();
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 4096; i++)
            {
                separations.Clear();
                world.QueryCapsuleSeparations(source, separations);
            }
            long allocated = GC.GetAllocatedBytesForCurrentThread() - before;

            Assert.That(allocated, Is.Zero, "4096 warmed caller-owned separation queries must allocate 0 B.");
            Assert.That(separations, Has.Count.EqualTo(5));
            Assert.That(world.QueryCapsule(source, legacyHits), Is.EqualTo(5));
            Assert.That(legacyHits, Has.Count.EqualTo(5));
        }

        private static CombatShapeSeparationResult Separate(
            CombatCapsuleQuery source,
            CombatPhysicsShape target)
        {
            return CombatPhysicsWorld.CalculateCapsuleSeparation(
                source,
                new CombatEntityId(10),
                new CombatBodyId(20),
                new CombatColliderId(30),
                target);
        }

        private static CombatCapsuleQuery Query(FixVector3 pointA, FixVector3 pointB, Fix64 radius)
        {
            return new CombatCapsuleQuery(
                new CombatQueryHeader(
                    queryId: 1,
                    kind: CombatQueryKind.Capsule,
                    sourceEntityId: CombatEntityId.None,
                    traceId: 2,
                    actionId: 3,
                    sourceOrder: 4,
                    layerMask: CombatPhysicsLayerMask.FromLayer(Layer)),
                pointA,
                pointB,
                radius);
        }

        private static CombatPhysicsWorld CreatePermutationWorld(bool reverse)
        {
            var world = new CombatPhysicsWorld();
            var flatBody = new CombatPhysicsBody(new CombatEntityId(20), new CombatBodyId(20), FixVector3.Zero);
            var compoundBody = new CombatPhysicsBody(new CombatEntityId(30), new CombatBodyId(30), FixVector3.Zero);
            if (reverse)
            {
                world.UpsertBody(compoundBody);
                world.UpsertBody(flatBody);
            }
            else
            {
                world.UpsertBody(flatBody);
                world.UpsertBody(compoundBody);
            }

            world.UpsertAabbCollider(new CombatPhysicsAabbCollider(
                flatBody.BodyId,
                new CombatColliderId(201),
                Layer,
                new FixVector3(-Fix64.One, -Fix64.One, -Fix64.One),
                new FixVector3(Fix64.One, Fix64.One, Fix64.One)));

            CombatPhysicsCompoundLeaf sphere = new CombatPhysicsCompoundLeaf(
                new CombatColliderId(301),
                Layer,
                CombatPhysicsShape.Sphere(FixVector3.Zero, Fix64.One));
            CombatPhysicsCompoundLeaf capsule = new CombatPhysicsCompoundLeaf(
                new CombatColliderId(301),
                Layer,
                CombatPhysicsShape.Capsule(-AxisY, AxisY, Fix64.Half));
            world.BeginCompoundColliderUpdate(compoundBody.BodyId, 2);
            world.UpsertCompoundCollider(reverse ? capsule : sphere);
            world.UpsertCompoundCollider(reverse ? sphere : capsule);
            world.EndCompoundColliderUpdate();
            return world;
        }

        private static CombatPhysicsWorld CreatePerformanceWorld()
        {
            var world = new CombatPhysicsWorld();
            world.UpsertBody(new CombatPhysicsBody(new CombatEntityId(100), new CombatBodyId(100), FixVector3.Zero));
            world.UpsertAabbCollider(new CombatPhysicsAabbCollider(
                new CombatBodyId(100), new CombatColliderId(1), Layer,
                -AxisX, AxisX));
            world.UpsertSphereCollider(new CombatPhysicsSphereCollider(
                new CombatBodyId(100), new CombatColliderId(2), Layer,
                FixVector3.Zero, Fix64.One));
            world.UpsertCapsuleCollider(new CombatPhysicsCapsuleCollider(
                new CombatBodyId(100), new CombatColliderId(3), Layer,
                -AxisY, AxisY, Fix64.One));
            world.UpsertObbCollider(new CombatPhysicsObbCollider(
                new CombatBodyId(100), new CombatColliderId(4), Layer,
                FixVector3.Zero, new FixVector3(Fix64.One, Fix64.One, Fix64.One),
                AxisX, AxisY, AxisZ));

            world.UpsertBody(new CombatPhysicsBody(new CombatEntityId(200), new CombatBodyId(200), FixVector3.Zero));
            world.BeginCompoundColliderUpdate(new CombatBodyId(200), 1);
            world.UpsertCompoundCollider(new CombatPhysicsCompoundLeaf(
                new CombatColliderId(5), Layer,
                CombatPhysicsShape.Sphere(FixVector3.Zero, Fix64.One)));
            world.EndCompoundColliderUpdate();
            return world;
        }

        private static void AssertNormalized(FixVector3 normal)
        {
            long error = Math.Abs(normal.LengthSquared().RawValue - Fix64.One.RawValue);
            Assert.That(error, Is.LessThanOrEqualTo(4));
        }
    }
}
