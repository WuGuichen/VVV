using System;
using System.Collections.Generic;
using MxFramework.Animation;
using MxFramework.CharacterRuntimeSpawn.Unity;
using MxFramework.CharacterAction;
using MxFramework.Combat.Animation;
using MxFramework.Combat.Core;
using MxFramework.Combat.Physics;
using MxFramework.Core.Math;
using MxFramework.Gameplay;
using MxFramework.Game.CharacterTest;
using MxFramework.Resources;
using MxFramework.Runtime;
using NUnit.Framework;

namespace MxFramework.Tests.Game.CharacterTest
{
    public sealed class CharacterTestCharacterDistanceM0Tests
    {
        [Test]
        public void AttackEvaluationSnapshot_IsCurrentOnlyForExactAuthorityVersions()
        {
            CombatPhysicsWorld world = new CombatPhysicsWorld();
            CharacterTestCharacterEntity source = CreateEntity(1, 0f, 0f, 0f, 0f);
            CharacterTestCharacterEntity target = CreateEntity(2, 3f, 0f, 0f, 0f);
            RuntimeFrame frame = new RuntimeFrame(42);
            CharacterTestDamageGeometrySnapshot geometry = CreateGeometry(
                target.Id.Value,
                new CombatEntityId(CharacterTestCharacterPhysicsRuntime.DynamicBodyIdStart + target.Id.Value),
                frame,
                world.Revision,
                hurtboxRevision: 7,
                reverseInsertion: false);

            Assert.IsTrue(CharacterTestAttackEvaluationSnapshot.TryCapture(
                frame,
                world,
                source,
                target,
                geometry,
                sourcePoseRevision: 11,
                sourceFacingRevision: 13,
                resolvedActionRevision: 17,
                out CharacterTestAttackEvaluationSnapshot snapshot));

            Assert.IsTrue(snapshot.IsCurrent(
                frame,
                world,
                source,
                target,
                geometry,
                sourcePoseRevision: 11,
                sourceFacingRevision: 13,
                resolvedActionRevision: 17));

            Assert.IsFalse(snapshot.IsCurrent(
                frame.Next(),
                world,
                source,
                target,
                geometry,
                sourcePoseRevision: 11,
                sourceFacingRevision: 13,
                resolvedActionRevision: 17));
            Assert.IsFalse(snapshot.IsCurrent(
                frame,
                world,
                source,
                target,
                geometry,
                sourcePoseRevision: 12,
                sourceFacingRevision: 13,
                resolvedActionRevision: 17));
            Assert.IsFalse(snapshot.IsCurrent(
                frame,
                world,
                source,
                target,
                geometry,
                sourcePoseRevision: 11,
                sourceFacingRevision: 14,
                resolvedActionRevision: 17));
            Assert.IsFalse(snapshot.IsCurrent(
                frame,
                world,
                source,
                target,
                geometry,
                sourcePoseRevision: 11,
                sourceFacingRevision: 13,
                resolvedActionRevision: 18));

            source.SetTransform(new CharacterTestCharacterTransformComponent(0.25f, 0f, 0f, 0f));
            Assert.IsFalse(snapshot.IsCurrent(
                frame,
                world,
                source,
                target,
                geometry,
                sourcePoseRevision: 11,
                sourceFacingRevision: 13,
                resolvedActionRevision: 17));

            CharacterTestDamageGeometrySnapshot invalidGeometry =
                new CharacterTestDamageGeometrySnapshot(initialColliderCapacity: 1);
            Assert.IsFalse(CharacterTestAttackEvaluationSnapshot.TryCapture(
                frame,
                world,
                source,
                target,
                invalidGeometry,
                sourcePoseRevision: 11,
                sourceFacingRevision: 13,
                resolvedActionRevision: 17,
                out _));
        }

        [Test]
        public void AttackEvaluationSnapshot_RejectsCombatPhysicsRevisionChanges()
        {
            CombatPhysicsWorld world = new CombatPhysicsWorld();
            CharacterTestCharacterEntity source = CreateEntity(1, 0f, 0f, 0f, 0f);
            CharacterTestCharacterEntity target = CreateEntity(2, 3f, 0f, 0f, 0f);
            RuntimeFrame frame = new RuntimeFrame(9);
            CharacterTestDamageGeometrySnapshot geometry = CreateGeometry(
                target.Id.Value,
                new CombatEntityId(CharacterTestCharacterPhysicsRuntime.DynamicBodyIdStart + target.Id.Value),
                frame,
                world.Revision,
                hurtboxRevision: 1,
                reverseInsertion: false);

            Assert.IsTrue(CharacterTestAttackEvaluationSnapshot.TryCapture(
                frame,
                world,
                source,
                target,
                geometry,
                sourcePoseRevision: 1,
                sourceFacingRevision: 1,
                resolvedActionRevision: 1,
                out CharacterTestAttackEvaluationSnapshot snapshot));

            world.UpsertBody(new CombatPhysicsBody(
                new CombatEntityId(9001),
                new CombatBodyId(9001),
                FixVector3.Zero));

            Assert.Greater(world.Revision, snapshot.CombatPhysicsRevision);
            Assert.IsFalse(snapshot.IsCurrent(
                frame,
                world,
                source,
                target,
                geometry,
                sourcePoseRevision: 1,
                sourceFacingRevision: 1,
                resolvedActionRevision: 1));
        }

        [Test]
        public void AttackEvaluationSnapshot_RejectsTargetGeometryFrameHurtboxAndWorldRevisionChanges()
        {
            CombatPhysicsWorld world = new CombatPhysicsWorld();
            CharacterTestCharacterEntity source = CreateEntity(1, 0f, 0f, 0f, 0f);
            CharacterTestCharacterEntity target = CreateEntity(2, 3f, 0f, 0f, 0f);
            RuntimeFrame frame = new RuntimeFrame(14);
            CharacterTestDamageGeometrySnapshot geometry = CreateGeometry(
                target.Id.Value,
                new CombatEntityId(CharacterTestCharacterPhysicsRuntime.DynamicBodyIdStart + target.Id.Value),
                frame,
                world.Revision,
                hurtboxRevision: 4,
                reverseInsertion: false);

            Assert.IsTrue(CharacterTestAttackEvaluationSnapshot.TryCapture(
                frame,
                world,
                source,
                target,
                geometry,
                sourcePoseRevision: 2,
                sourceFacingRevision: 3,
                resolvedActionRevision: 5,
                out CharacterTestAttackEvaluationSnapshot snapshot));

            geometry.Commit(frame.Next(), world.Revision, snapshot.TargetHurtboxRevision);
            Assert.IsFalse(snapshot.IsCurrent(
                frame,
                world,
                source,
                target,
                geometry,
                sourcePoseRevision: 2,
                sourceFacingRevision: 3,
                resolvedActionRevision: 5));

            geometry.Commit(frame, world.Revision, snapshot.TargetHurtboxRevision + 1);
            Assert.IsFalse(snapshot.IsCurrent(
                frame,
                world,
                source,
                target,
                geometry,
                sourcePoseRevision: 2,
                sourceFacingRevision: 3,
                resolvedActionRevision: 5));

            geometry.Commit(frame, world.Revision + 1, snapshot.TargetHurtboxRevision);
            Assert.IsFalse(snapshot.IsCurrent(
                frame,
                world,
                source,
                target,
                geometry,
                sourcePoseRevision: 2,
                sourceFacingRevision: 3,
                resolvedActionRevision: 5));
        }

        [Test]
        public void AttackEvaluationSnapshot_RejectsDestroyedAndReusedGameplayIdentity()
        {
            CombatPhysicsWorld world = new CombatPhysicsWorld();
            CharacterTestCharacterEntity source = CreateEntity(1, 0f, 0f, 0f, 0f);
            CharacterTestCharacterEntity target = CreateEntity(2, 3f, 0f, 0f, 0f);
            RuntimeFrame frame = new RuntimeFrame(18);
            CharacterTestDamageGeometrySnapshot geometry = CreateGeometry(
                target.Id.Value,
                target.GameplayEntityId,
                new CombatEntityId(CharacterTestCharacterPhysicsRuntime.DynamicBodyIdStart + target.Id.Value),
                frame,
                world.Revision,
                hurtboxRevision: 2,
                reverseInsertion: false);

            Assert.IsTrue(CharacterTestAttackEvaluationSnapshot.TryCapture(
                frame,
                world,
                source,
                target,
                geometry,
                sourcePoseRevision: 1,
                sourceFacingRevision: 1,
                resolvedActionRevision: 1,
                out CharacterTestAttackEvaluationSnapshot snapshot));

            target.SetPresence(target.Presence.WithDestroyed(frame.Value));
            Assert.IsFalse(snapshot.IsCurrent(
                frame,
                world,
                source,
                target,
                geometry,
                sourcePoseRevision: 1,
                sourceFacingRevision: 1,
                resolvedActionRevision: 1));

            CharacterTestCharacterEntity reusedTarget = CreateEntity(2, 3f, 0f, 0f, 0f, gameplayGeneration: 2);
            Assert.IsFalse(snapshot.IsCurrent(
                frame,
                world,
                source,
                reusedTarget,
                geometry,
                sourcePoseRevision: 1,
                sourceFacingRevision: 1,
                resolvedActionRevision: 1));
        }

        [Test]
        public void DamageGeometrySnapshot_SortsStableMetadataAndComputesConservativeAggregateBounds()
        {
            RuntimeFrame frame = new RuntimeFrame(12);
            CharacterTestDamageGeometrySnapshot ordered = CreateGeometry(
                entityId: 2,
                combatEntityId: new CombatEntityId(20002),
                frame,
                worldRevision: 5,
                hurtboxRevision: 8,
                reverseInsertion: false);
            CharacterTestDamageGeometrySnapshot shuffled = CreateGeometry(
                entityId: 2,
                combatEntityId: new CombatEntityId(20002),
                frame,
                worldRevision: 5,
                hurtboxRevision: 8,
                reverseInsertion: true);

            Assert.IsTrue(ordered.IsValid);
            Assert.AreEqual(3, ordered.ColliderCount);
            AssertVectorEqual(V(-2, -1, -1), ordered.WorldMin);
            AssertVectorEqual(V(4, 4, 1), ordered.WorldMax);
            AssertVectorEqual(ordered.WorldMin, shuffled.WorldMin);
            AssertVectorEqual(ordered.WorldMax, shuffled.WorldMax);

            for (int i = 0; i < ordered.ColliderCount; i++)
            {
                CharacterTestDamageGeometryColliderMetadata left = ordered.GetCollider(i);
                CharacterTestDamageGeometryColliderMetadata right = shuffled.GetCollider(i);
                Assert.AreEqual(left.LogicalColliderId, right.LogicalColliderId);
                Assert.AreEqual(left.CombatColliderId, right.CombatColliderId);
                Assert.AreEqual(left.PartId, right.PartId);
                Assert.AreEqual(left.HitZoneId, right.HitZoneId);
                Assert.AreEqual(left.Priority, right.Priority);
                Assert.AreEqual(left.LocalShape, right.LocalShape);
                AssertVectorEqual(left.WorldMin, right.WorldMin);
                AssertVectorEqual(left.WorldMax, right.WorldMax);
            }

            Assert.AreEqual("arm", ordered.GetCollider(0).LogicalColliderLabel);
            Assert.AreEqual("tail", ordered.GetCollider(1).LogicalColliderLabel);
            Assert.AreEqual("torso", ordered.GetCollider(2).LogicalColliderLabel);
            Assert.IsTrue(ordered.TryGetCollider(
                new CombatColliderId(1003),
                out CharacterTestDamageGeometryColliderMetadata tailByCombatId));
            Assert.AreEqual("tail", tailByCombatId.LogicalColliderLabel);
        }

        [Test]
        public void LargeTargetNearTail_IsRetainedByAggregateBoundsDespiteFarRootDistance()
        {
            CharacterTestDamageGeometrySnapshot targetGeometry = CreateLargeTargetGeometry();
            CharacterTestDirectHitTraceBounds sourceBounds = default(CharacterTestDirectHitTraceBounds);
            sourceBounds.IncludeCapsule(V(0, 0, 0), V(2, 0, 0), F(1));

            Fix64 rootDistanceSquared = F(100) * F(100);
            Assert.IsTrue(rootDistanceSquared > F(20) * F(20));
            Assert.IsTrue(BoundsIntersect(
                sourceBounds.Min,
                sourceBounds.Max,
                targetGeometry.WorldMin,
                targetGeometry.WorldMax));

            Assert.IsTrue(targetGeometry.TryGetCollider(
                CharacterTestAuthoredContentIds.D400_Id103,
                out CharacterTestDamageGeometryColliderMetadata tail));
            Assert.IsTrue(tail.WorldMin.X <= sourceBounds.Max.X);
            Assert.IsTrue(tail.WorldMax.X >= sourceBounds.Min.X);
        }

        [Test]
        public void WarmedSnapshotGeometryAndBroadphaseQueries_DoNotAllocate()
        {
            CombatPhysicsWorld world = new CombatPhysicsWorld();
            world.UpsertBody(new CombatPhysicsBody(
                new CombatEntityId(20002),
                new CombatBodyId(50002),
                V(3, 0, 0)));
            world.UpsertAabbCollider(new CombatPhysicsAabbCollider(
                new CombatBodyId(50002),
                new CombatColliderId(1000),
                layer: 10,
                localMin: V(-1, -1, -1),
                localMax: V(1, 1, 1)));

            CharacterTestCharacterEntity source = CreateEntity(1, 0f, 0f, 0f, 0f);
            CharacterTestCharacterEntity target = CreateEntity(2, 3f, 0f, 0f, 0f);
            RuntimeFrame frame = new RuntimeFrame(20);
            CharacterTestDamageGeometrySnapshot geometry = CreateGeometry(
                target.Id.Value,
                new CombatEntityId(CharacterTestCharacterPhysicsRuntime.DynamicBodyIdStart + target.Id.Value),
                frame,
                world.Revision,
                hurtboxRevision: 3,
                reverseInsertion: false);
            Assert.IsTrue(CharacterTestAttackEvaluationSnapshot.TryCapture(
                frame,
                world,
                source,
                target,
                geometry,
                sourcePoseRevision: 4,
                sourceFacingRevision: 5,
                resolvedActionRevision: 6,
                out CharacterTestAttackEvaluationSnapshot snapshot));

            CombatQueryHeader header = new CombatQueryHeader(
                queryId: 1,
                kind: CombatQueryKind.Capsule,
                sourceEntityId: new CombatEntityId(20001),
                traceId: 7,
                actionId: 8,
                sourceOrder: 0,
                layerMask: CombatPhysicsLayerMask.FromLayer(10));
            CombatPhysicsQuery query = CombatPhysicsQuery.From(new CombatCapsuleQuery(
                header,
                V(0, 0, 0),
                V(4, 0, 0),
                F(1)));
            var hits = new List<CombatQueryResult>(4);

            for (int i = 0; i < 8; i++)
            {
                hits.Clear();
                world.Query(query, hits);
                Assert.IsTrue(snapshot.IsCurrent(
                    frame,
                    world,
                    source,
                    target,
                    geometry,
                    sourcePoseRevision: 4,
                    sourceFacingRevision: 5,
                    resolvedActionRevision: 6));
                CharacterTestDamageGeometryColliderMetadata metadata = geometry.GetCollider(0);
                Assert.IsTrue(metadata.CombatColliderId.Value > 0);
            }

            _ = GC.GetAllocatedBytesForCurrentThread();
            long before = GC.GetAllocatedBytesForCurrentThread();
            bool observed = true;
            for (int i = 0; i < 64; i++)
            {
                hits.Clear();
                world.Query(query, hits);
                observed &= snapshot.IsCurrent(
                    frame,
                    world,
                    source,
                    target,
                    geometry,
                    sourcePoseRevision: 4,
                    sourceFacingRevision: 5,
                    resolvedActionRevision: 6);
                for (int j = 0; j < geometry.ColliderCount; j++)
                    observed &= geometry.GetCollider(j).CombatColliderId.Value > 0;
            }

            long allocatedBytes = GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.IsTrue(observed);
            Assert.AreEqual(0L, allocatedBytes);
        }

        [Test]
        public void DirectHitTracePlanSampler_WeaponSamplesHaveStableOrderAndIds()
        {
            WeaponTraceFrame frame = new WeaponTraceFrame(
                traceId: 23,
                rootPrev: V(0, 1, 0),
                tipPrev: V(2, 1, 0),
                rootNow: V(0, 1, 1),
                tipNow: V(4, 1, 1),
                radius: F(1) / F(2),
                targetMask: CombatPhysicsLayerMask.FromLayer(10),
                proximalExtension: F(1),
                proximalRadius: F(1) / F(2));
            CombatEntityId sourceEntityId = new CombatEntityId(20001);
            var firstQueries = new List<CombatCapsuleQuery>(16);
            var firstSamples = new List<CharacterTestDirectHitTracePlanSample>(16);
            var firstBounds = default(CharacterTestDirectHitTraceBounds);
            int firstQueryId = 3;
            int firstSampleOrder = 0;
            CharacterTestDirectHitTracePlanSampler.AppendWeaponTraceSamples(
                frame,
                sourceEntityId,
                actionId: 8,
                hitWindowId: 11,
                localFrame: 5,
                useTipSweepCapsules: true,
                maxTipDistancePerSubstep: F(1),
                maxSubsteps: 8,
                ref firstQueryId,
                ref firstSampleOrder,
                firstQueries,
                firstSamples,
                ref firstBounds);

            var secondQueries = new List<CombatCapsuleQuery>(16);
            var secondSamples = new List<CharacterTestDirectHitTracePlanSample>(16);
            var secondBounds = default(CharacterTestDirectHitTraceBounds);
            int secondQueryId = 101;
            int secondSampleOrder = 0;
            CharacterTestDirectHitTracePlanSampler.AppendWeaponTraceSamples(
                frame,
                sourceEntityId,
                actionId: 8,
                hitWindowId: 11,
                localFrame: 5,
                useTipSweepCapsules: true,
                maxTipDistancePerSubstep: F(1),
                maxSubsteps: 8,
                ref secondQueryId,
                ref secondSampleOrder,
                secondQueries,
                secondSamples,
                ref secondBounds);

            Assert.AreEqual(firstSamples.Count, secondSamples.Count);
            Assert.Greater(firstSamples.Count, 1);
            for (int i = 0; i < firstSamples.Count; i++)
            {
                CharacterTestDirectHitTracePlanSample left = firstSamples[i];
                CharacterTestDirectHitTracePlanSample right = secondSamples[i];
                Assert.AreEqual(left.Kind, right.Kind);
                Assert.AreEqual(left.LocalFrame, right.LocalFrame);
                Assert.AreEqual(left.SampleOrder, right.SampleOrder);
                Assert.AreEqual(left.QueryOffset, right.QueryOffset);
                Assert.AreEqual(left.ActionId, right.ActionId);
                Assert.AreEqual(left.TraceId, right.TraceId);
                Assert.AreEqual(left.ResolvePriority, right.ResolvePriority);
                Assert.AreEqual(left.StableSampleId, right.StableSampleId);
                Assert.AreEqual(left.Query.Header.Kind, right.Query.Header.Kind);
                Assert.AreEqual(left.Query.Header.SourceEntityId, right.Query.Header.SourceEntityId);
                Assert.AreEqual(left.Query.Header.TraceId, right.Query.Header.TraceId);
                Assert.AreEqual(left.Query.Header.ActionId, right.Query.Header.ActionId);
                Assert.AreEqual(left.Query.Header.SourceOrder, right.Query.Header.SourceOrder);
                AssertVectorEqual(left.Query.PointA, right.Query.PointA);
                AssertVectorEqual(left.Query.PointB, right.Query.PointB);
                AssertFix64Equal(left.Query.Radius, right.Query.Radius);
            }

            Assert.IsTrue(firstBounds.HasBounds);
            AssertVectorEqual(firstBounds.Min, secondBounds.Min);
            AssertVectorEqual(firstBounds.Max, secondBounds.Max);
        }

        [Test]
        public void DirectHitTracePlanSampler_BuildsManualCasterAndBakedSocketCapsulesWithFixedShape()
        {
            CharacterTestDirectHitTraceSourcePose manualPose =
                new CharacterTestDirectHitTraceSourcePose(1, V(10, 0, 20), 0);
            CharacterActionHitVolumeConfig manual = CharacterActionHitVolumeConfig.Capsule(
                0f, 0f, 0f,
                2f, 0f, 0f,
                0.5f);
            Assert.IsTrue(CharacterTestDirectHitTracePlanSampler.TryBuildManualCapsule(
                manualPose,
                manual,
                out FixVector3 manualPointA,
                out FixVector3 manualPointB,
                out Fix64 manualRadius));
            AssertVectorEqual(V(10, 0, 20), manualPointA);
            AssertVectorEqual(V(12, 0, 20), manualPointB);
            AssertFix64Equal(F(1) / F(2), manualRadius);

            CharacterTestDirectHitTraceSourcePose casterPose =
                new CharacterTestDirectHitTraceSourcePose(2, V(0, 0, 0), 0);
            CharacterActionHitVolumeConfig caster = CharacterActionHitVolumeConfig.CasterBody(
                bodyOffsetX: 0.5f,
                bodyOffsetY: 0.25f,
                bodyOffsetZ: 0f,
                bodyRadiusScale: 0.5f,
                bodyRadiusAdd: 0.25f,
                bodyHeightScale: 0.5f,
                bodyHeightAdd: 0.5f);
            Assert.IsTrue(CharacterTestDirectHitTracePlanSampler.TryBuildCasterBodyCapsule(
                casterPose,
                VF(F(10), F(1), F(20)),
                VF(F(1), F(2), F(1)),
                caster,
                out FixVector3 casterCenter,
                out FixVector3 casterPointA,
                out FixVector3 casterPointB,
                out Fix64 casterRadius,
                out Fix64 casterSegmentHalf));
            AssertVectorEqual(VF(F(21) / F(2), F(5) / F(4), F(20)), casterCenter);
            AssertVectorEqual(VF(F(21) / F(2), F(3) / F(4), F(20)), casterPointA);
            AssertVectorEqual(VF(F(21) / F(2), F(7) / F(4), F(20)), casterPointB);
            AssertFix64Equal(F(3) / F(4), casterRadius);
            AssertFix64Equal(F(1) / F(2), casterSegmentHalf);

            CharacterTestDirectHitTraceSourcePose bakedPose =
                new CharacterTestDirectHitTraceSourcePose(3, V(10, 0, 20), 0);
            Assert.IsTrue(CharacterTestDirectHitTracePlanSampler.TryBuildBakedSocketCapsule(
                bakedPose,
                new MxAnimationBakedVector3(1000000, 0, 0),
                new MxAnimationBakedVector3(3000000, 0, 0),
                authoredRadius: 0.25f,
                out FixVector3 bakedPointA,
                out FixVector3 bakedPointB,
                out Fix64 bakedRadius));
            AssertVectorEqual(V(11, 0, 20), bakedPointA);
            AssertVectorEqual(V(13, 0, 20), bakedPointB);
            AssertFix64Equal(F(1) / F(4), bakedRadius);
        }

        [Test]
        public void DirectHitTracePlanSampler_AccumulatesContinuousActiveFramesWithRootMotion()
        {
            CharacterActionHitVolumeConfig manual = CharacterActionHitVolumeConfig.Capsule(
                0f, 0f, 0f,
                1f, 0f, 0f,
                0.25f);
            CharacterTestDirectHitTraceSourcePose firstPose =
                new CharacterTestDirectHitTraceSourcePose(1, V(0, 0, 0), 0);
            CharacterTestDirectHitTraceSourcePose secondPose =
                new CharacterTestDirectHitTraceSourcePose(2, V(1, 0, 0), 0);
            var samples = new List<CharacterTestDirectHitTracePlanSample>(2);
            var bounds = default(CharacterTestDirectHitTraceBounds);
            int sampleOrder = 0;
            AppendManualFrameSample(firstPose, manual, ref sampleOrder, samples, ref bounds);
            AppendManualFrameSample(secondPose, manual, ref sampleOrder, samples, ref bounds);

            Assert.AreEqual(2, samples.Count);
            Assert.AreEqual(2, sampleOrder);
            Assert.AreEqual(1, samples[0].LocalFrame);
            Assert.AreEqual(2, samples[1].LocalFrame);
            Assert.AreEqual(0, samples[0].SampleOrder);
            Assert.AreEqual(1, samples[1].SampleOrder);
            Assert.AreNotEqual(samples[0].StableSampleId, samples[1].StableSampleId);
            AssertVectorEqual(V(0, 0, 0), samples[0].Query.PointA);
            AssertVectorEqual(V(1, 0, 0), samples[0].Query.PointB);
            AssertVectorEqual(V(1, 0, 0), samples[1].Query.PointA);
            AssertVectorEqual(V(2, 0, 0), samples[1].Query.PointB);
            AssertFix64Equal(F(1) / F(4), samples[0].Query.Radius);
            AssertFix64Equal(F(1) / F(4), samples[1].Query.Radius);
            AssertVectorEqual(VF(-F(1) / F(4), -F(1) / F(4), -F(1) / F(4)), bounds.Min);
            AssertVectorEqual(VF(F(9) / F(4), F(1) / F(4), F(1) / F(4)), bounds.Max);
        }

        [Test]
        public void DirectHitTracePlanSampler_HitVolumeKindsPreserveShapeAndBounds()
        {
            CharacterTestDirectHitTraceSampleKind[] kinds =
            {
                CharacterTestDirectHitTraceSampleKind.ManualCapsule,
                CharacterTestDirectHitTraceSampleKind.CasterBody,
                CharacterTestDirectHitTraceSampleKind.BakedSocketCapsule
            };
            for (int i = 0; i < kinds.Length; i++)
            {
                var samples = new List<CharacterTestDirectHitTracePlanSample>(1);
                var bounds = default(CharacterTestDirectHitTraceBounds);
                int sampleOrder = 0;
                Assert.IsTrue(CharacterTestDirectHitTracePlanSampler.TryAppendHitVolumeSample(
                    kinds[i],
                    new CombatEntityId(20001),
                    actionId: 9,
                    traceId: 31,
                    hitWindowId: 12,
                    localFrame: 7,
                    sourceOrder: 2,
                    queryOffset: i,
                    targetMask: CombatPhysicsLayerMask.FromLayer(10),
                    pointA: V(1, 2, 3),
                    pointB: V(4, 5, 6),
                    radius: F(1) / F(2),
                    ref sampleOrder,
                    samples,
                    ref bounds,
                    out CharacterTestDirectHitTracePlanSample sample));

                Assert.AreEqual(1, samples.Count);
                Assert.AreEqual(kinds[i], sample.Kind);
                Assert.AreEqual(7, sample.LocalFrame);
                Assert.AreEqual(0, sample.SampleOrder);
                Assert.AreEqual(i, sample.QueryOffset);
                Assert.AreEqual(9, sample.ActionId);
                Assert.AreEqual(31, sample.TraceId);
                Assert.AreEqual(12, sample.HitWindowId);
                Assert.AreEqual(CombatQueryKind.Capsule, sample.Query.Header.Kind);
                AssertVectorEqual(V(1, 2, 3), sample.Query.PointA);
                AssertVectorEqual(V(4, 5, 6), sample.Query.PointB);
                AssertFix64Equal(F(1) / F(2), sample.Query.Radius);
                Assert.IsTrue(bounds.HasBounds);
                AssertBoundsContains(bounds, sample.Query.PointA, sample.Query.PointB, sample.Query.Radius);
            }
        }

        [Test]
        public void DirectHitTracePlanSampler_SourcePoseTransformPreservesRootMotionAndLocalFrame()
        {
            var pose = new CharacterTestDirectHitTraceSourcePose(
                localFrame: 17,
                position: V(10, 0, 20),
                yawDegrees: 90f);
            FixVector3 transformed = CharacterTestDirectHitTracePlanSampler.TransformLocal(pose, V(1, 0, 0));

            AssertVectorEqual(V(10, 0, 19), transformed);

            var samples = new List<CharacterTestDirectHitTracePlanSample>(1);
            var bounds = default(CharacterTestDirectHitTraceBounds);
            int sampleOrder = 0;
            Assert.IsTrue(CharacterTestDirectHitTracePlanSampler.TryAppendHitVolumeSample(
                CharacterTestDirectHitTraceSampleKind.BakedSocketCapsule,
                new CombatEntityId(20001),
                actionId: 4,
                traceId: 5,
                hitWindowId: 6,
                localFrame: pose.LocalFrame,
                sourceOrder: 0,
                queryOffset: 0,
                targetMask: CombatPhysicsLayerMask.FromLayer(10),
                pointA: transformed,
                pointB: transformed + V(0, 1, 0),
                radius: F(1) / F(4),
                ref sampleOrder,
                samples,
                ref bounds,
                out CharacterTestDirectHitTracePlanSample sample));
            Assert.AreEqual(pose.LocalFrame, sample.LocalFrame);
            AssertVectorEqual(transformed, sample.Query.PointA);
        }

        [Test]
        public void DirectHitTracePlanSampler_SourceBoundsContainEveryProducedCapsule()
        {
            WeaponTraceFrame weapon = new WeaponTraceFrame(
                traceId: 17,
                rootPrev: V(-2, 0, 0),
                tipPrev: V(2, 0, 0),
                rootNow: V(-1, 0, 1),
                tipNow: V(3, 0, 1),
                radius: F(1) / F(2),
                targetMask: CombatPhysicsLayerMask.FromLayer(10),
                proximalExtension: F(1),
                proximalRadius: F(1) / F(4));
            var queries = new List<CombatCapsuleQuery>(16);
            var samples = new List<CharacterTestDirectHitTracePlanSample>(16);
            var bounds = default(CharacterTestDirectHitTraceBounds);
            int queryId = 1;
            int sampleOrder = 0;
            CharacterTestDirectHitTracePlanSampler.AppendWeaponTraceSamples(
                weapon,
                new CombatEntityId(20001),
                actionId: 3,
                hitWindowId: 4,
                localFrame: 8,
                useTipSweepCapsules: true,
                maxTipDistancePerSubstep: F(1),
                maxSubsteps: 8,
                ref queryId,
                ref sampleOrder,
                queries,
                samples,
                ref bounds);

            Assert.IsTrue(CharacterTestDirectHitTracePlanSampler.TryAppendHitVolumeSample(
                CharacterTestDirectHitTraceSampleKind.ManualCapsule,
                new CombatEntityId(20001),
                actionId: 3,
                traceId: 18,
                hitWindowId: 4,
                localFrame: 8,
                sourceOrder: 2,
                queryOffset: 9,
                targetMask: CombatPhysicsLayerMask.FromLayer(10),
                pointA: V(-3, 0, -1),
                pointB: V(3, 0, 2),
                radius: F(1) / F(2),
                ref sampleOrder,
                samples,
                ref bounds,
                out _));

            Assert.IsTrue(bounds.HasBounds);
            for (int i = 0; i < samples.Count; i++)
            {
                CharacterTestDirectHitTracePlanSample sample = samples[i];
                AssertBoundsContains(bounds, sample.Query.PointA, sample.Query.PointB, sample.Query.Radius);
            }
        }

        [Test]
        public void DirectHitTracePreviewPlan_MatchesActiveFrameSamplerForAllKindsAndStableOrder()
        {
            RuntimeFrame runtimeFrame = new RuntimeFrame(30);
            int combatPhysicsRevision = 17;
            CombatEntityId sourceEntityId = new CombatEntityId(20001);
            int resolvedActionRevision = 41;
            CreatePreviewInputs(
                out List<CharacterTestDirectHitTraceSourcePoseOffset> poses,
                out List<CharacterTestDirectHitTracePreviewWeaponFrame> weaponFrames,
                out List<CharacterTestDirectHitTracePreviewCapsuleFrame> capsuleFrames);

            var queryBuffer = new List<CombatCapsuleQuery>(64);
            var output = new CharacterTestDirectHitTracePlanBuffer();
            Assert.IsTrue(CharacterTestDirectHitTracePlanSampler.TryBuildPreviewPlan(
                runtimeFrame,
                combatPhysicsRevision,
                sourceEntityId,
                resolvedActionRevision,
                CreatePreviewPoseIdentity(),
                poses,
                weaponFrames,
                capsuleFrames,
                useTipSweepCapsules: false,
                maxTipDistancePerSubstep: F(1),
                maxSubsteps: 8,
                queryBuffer,
                output,
                out CharacterTestDirectHitTracePlanView previewPlan));

            var expectedQueries = new List<CombatCapsuleQuery>(64);
            var expectedSamples = new List<CharacterTestDirectHitTracePlanSample>(64);
            var expectedBounds = default(CharacterTestDirectHitTraceBounds);
            AppendExpectedPreviewSamples(
                poses,
                weaponFrames,
                capsuleFrames,
                sourceEntityId,
                resolvedActionRevision,
                expectedQueries,
                expectedSamples,
                ref expectedBounds);

            Assert.IsTrue(previewPlan.IsCurrent(
                runtimeFrame,
                combatPhysicsRevision,
                sourceEntityId,
                resolvedActionRevision));
            Assert.AreEqual(expectedSamples.Count, previewPlan.Count);
            Assert.IsTrue(previewPlan.TryGetBounds(out CharacterTestDirectHitTraceBounds previewBounds));
            AssertBoundsEqual(expectedBounds, previewBounds);

            bool sawWeapon = false;
            bool sawManual = false;
            bool sawCaster = false;
            bool sawBaked = false;
            for (int i = 0; i < expectedSamples.Count; i++)
            {
                Assert.IsTrue(previewPlan.TryGetSample(i, out CharacterTestDirectHitTracePlanSample actual));
                AssertPlanSampleEqual(expectedSamples[i], actual);
                sawWeapon |= actual.Kind == CharacterTestDirectHitTraceSampleKind.WeaponTraceBlade;
                sawManual |= actual.Kind == CharacterTestDirectHitTraceSampleKind.ManualCapsule;
                sawCaster |= actual.Kind == CharacterTestDirectHitTraceSampleKind.CasterBody;
                sawBaked |= actual.Kind == CharacterTestDirectHitTraceSampleKind.BakedSocketCapsule;
            }

            Assert.IsTrue(sawWeapon);
            Assert.IsTrue(sawManual);
            Assert.IsTrue(sawCaster);
            Assert.IsTrue(sawBaked);
            Assert.IsTrue(HasSampleAtFrame(previewPlan, 11));
            Assert.IsTrue(HasRootMotionDisplacement(previewPlan, 10, 11));
        }

        [Test]
        public void DirectHitTracePreviewPlan_PoseIdentityIsTheOnlyWorldPoseAuthority()
        {
            RuntimeFrame runtimeFrame = new RuntimeFrame(30);
            const int combatPhysicsRevision = 17;
            var sourceEntityId = new CombatEntityId(20001);
            const int resolvedActionRevision = 41;
            CreatePreviewInputs(
                out List<CharacterTestDirectHitTraceSourcePoseOffset> poseOffsets,
                out List<CharacterTestDirectHitTracePreviewWeaponFrame> weaponFrames,
                out List<CharacterTestDirectHitTracePreviewCapsuleFrame> capsuleFrames);
            var identity = new CharacterTestDirectHitTracePlanPoseIdentity(
                CharacterTestDirectHitTracePlanPoseKind.CurrentPose,
                V(10, 0, 0),
                sourceYawMilliDegrees: 90000,
                sourcePoseRevision: 5,
                sourceFacingRevision: 6);
            var output = new CharacterTestDirectHitTracePlanBuffer();

            Assert.IsTrue(CharacterTestDirectHitTracePlanSampler.TryBuildPreviewPlan(
                runtimeFrame,
                combatPhysicsRevision,
                sourceEntityId,
                resolvedActionRevision,
                identity,
                poseOffsets,
                weaponFrames,
                capsuleFrames,
                useTipSweepCapsules: false,
                maxTipDistancePerSubstep: F(1),
                maxSubsteps: 8,
                new List<CombatCapsuleQuery>(64),
                output,
                out CharacterTestDirectHitTracePlanView plan));

            bool foundFrameTenManual = false;
            bool foundFrameElevenManual = false;
            for (int i = 0; i < plan.Count; i++)
            {
                Assert.IsTrue(plan.TryGetSample(i, out CharacterTestDirectHitTracePlanSample sample));
                if (sample.Kind != CharacterTestDirectHitTraceSampleKind.ManualCapsule)
                    continue;
                if (sample.LocalFrame == 10)
                {
                    AssertVectorEqual(V(10, 0, 0), sample.Query.PointA);
                    foundFrameTenManual = true;
                }
                else if (sample.LocalFrame == 11)
                {
                    // The +X local root-motion offset is rotated by the sealed
                    // 90-degree base identity, so it becomes -Z world motion.
                    AssertVectorEqual(V(10, 0, -1), sample.Query.PointA);
                    foundFrameElevenManual = true;
                }
            }

            Assert.IsTrue(foundFrameTenManual);
            Assert.IsTrue(foundFrameElevenManual);
        }

        [Test]
        public void DirectHitTracePlanView_RejectsFramePhysicsSourceActionSealAndGenerationChanges()
        {
            RuntimeFrame runtimeFrame = new RuntimeFrame(30);
            int combatPhysicsRevision = 17;
            CombatEntityId sourceEntityId = new CombatEntityId(20001);
            int resolvedActionRevision = 41;
            CreatePreviewInputs(
                out List<CharacterTestDirectHitTraceSourcePoseOffset> poses,
                out List<CharacterTestDirectHitTracePreviewWeaponFrame> weaponFrames,
                out List<CharacterTestDirectHitTracePreviewCapsuleFrame> capsuleFrames);
            var queryBuffer = new List<CombatCapsuleQuery>(64);
            var output = new CharacterTestDirectHitTracePlanBuffer();
            Assert.IsTrue(CharacterTestDirectHitTracePlanSampler.TryBuildPreviewPlan(
                runtimeFrame,
                combatPhysicsRevision,
                sourceEntityId,
                resolvedActionRevision,
                CreatePreviewPoseIdentity(),
                poses,
                weaponFrames,
                capsuleFrames,
                useTipSweepCapsules: false,
                maxTipDistancePerSubstep: F(1),
                maxSubsteps: 8,
                queryBuffer,
                output,
                out CharacterTestDirectHitTracePlanView oldPlan));
            CharacterTestDirectHitTracePlanCollectionView oldCollection = output.CreateView();
            int initialGeneration = output.Generation;

            Assert.IsTrue(oldPlan.IsCurrent(
                runtimeFrame,
                combatPhysicsRevision,
                sourceEntityId,
                resolvedActionRevision));
            Assert.IsTrue(oldCollection.IsCurrent(runtimeFrame, combatPhysicsRevision));
            Assert.Greater(oldPlan.Count, 0);
            Assert.AreEqual(1, oldCollection.Count);
            Assert.IsTrue(oldPlan.TryGetSample(0, out _));
            Assert.IsTrue(oldPlan.TryGetBounds(out _));

            Assert.IsTrue(oldPlan.IsStale(
                runtimeFrame.Next(),
                combatPhysicsRevision,
                sourceEntityId,
                resolvedActionRevision));
            Assert.IsTrue(oldPlan.IsStale(
                runtimeFrame,
                combatPhysicsRevision + 1,
                sourceEntityId,
                resolvedActionRevision));
            Assert.IsTrue(oldPlan.IsStale(
                runtimeFrame,
                combatPhysicsRevision,
                new CombatEntityId(sourceEntityId.Value + 1),
                resolvedActionRevision));
            Assert.IsTrue(oldPlan.IsStale(
                runtimeFrame,
                combatPhysicsRevision,
                sourceEntityId,
                resolvedActionRevision + 1));
            Assert.IsTrue(oldCollection.IsStale(runtimeFrame.Next(), combatPhysicsRevision));
            Assert.IsTrue(oldCollection.IsStale(runtimeFrame, combatPhysicsRevision + 1));

            Assert.IsFalse(output.Seal(runtimeFrame, combatPhysicsRevision + 1));
            Assert.IsFalse(output.Seal(runtimeFrame, combatPhysicsRevision));
            Assert.AreEqual(initialGeneration, output.Generation);
            Assert.Greater(oldPlan.Count, 0);
            Assert.IsTrue(oldPlan.TryGetSample(0, out _));
            Assert.IsTrue(oldPlan.TryGetBounds(out _));
            Assert.AreEqual(1, oldCollection.Count);
            Assert.IsTrue(oldCollection.TryGetPlan(0, out _));
            Assert.IsFalse(output.UpdatePlan(0, 0, default));
            Assert.IsNull(output.GetPlanSamplesBuffer(0));
            Assert.AreEqual(-1, output.GetOrBeginPlan(
                sourceEntityId,
                resolvedActionRevision,
                CreatePreviewPoseIdentity()));

            output.Reset(runtimeFrame.Next(), combatPhysicsRevision + 2);
            Assert.Greater(output.Generation, initialGeneration);
            Assert.AreEqual(0, oldPlan.Count);
            Assert.IsFalse(oldPlan.TryGetSample(0, out _));
            Assert.AreEqual(0, oldCollection.Count);
            Assert.IsFalse(oldCollection.TryGetPlan(0, out _));
        }

        [Test]
        public void DirectHitTracePlanBuffer_SealCopiesSnapshotAndRejectsLeakedListMutation()
        {
            RuntimeFrame runtimeFrame = new RuntimeFrame(31);
            const int combatPhysicsRevision = 18;
            CombatEntityId sourceEntityId = new CombatEntityId(20001);
            var output = new CharacterTestDirectHitTracePlanBuffer();
            output.Reset(runtimeFrame, combatPhysicsRevision);
            int planIndex = output.GetOrBeginPlan(
                sourceEntityId,
                resolvedActionRevision: 42,
                CreatePreviewPoseIdentity());
            List<CharacterTestDirectHitTracePlanSample> leakedBuildList =
                output.GetPlanSamplesBuffer(planIndex);
            var bounds = default(CharacterTestDirectHitTraceBounds);
            int sampleOrder = 0;
            Assert.IsTrue(CharacterTestDirectHitTracePlanSampler.TryAppendHitVolumeSample(
                CharacterTestDirectHitTraceSampleKind.ManualCapsule,
                sourceEntityId,
                actionId: 7,
                traceId: 501,
                hitWindowId: 42,
                localFrame: 3,
                sourceOrder: 0,
                queryOffset: 0,
                CombatPhysicsLayerMask.FromLayer(10),
                V(0, 0, 0),
                V(1, 0, 0),
                F(1) / F(4),
                ref sampleOrder,
                leakedBuildList,
                ref bounds,
                out _));
            Assert.IsTrue(output.UpdatePlan(planIndex, leakedBuildList.Count, bounds));

            CharacterTestDirectHitTracePlanCollectionView unsealedView = output.CreateView();
            Assert.AreEqual(0, unsealedView.Count);
            Assert.IsFalse(unsealedView.TryGetPlan(0, out _));

            Assert.IsTrue(output.Seal(runtimeFrame, combatPhysicsRevision));
            int sealedGeneration = output.Generation;
            CharacterTestDirectHitTracePlanCollectionView sealedView = output.CreateView();
            Assert.IsTrue(sealedView.TryGetPlan(0, out CharacterTestDirectHitTracePlanView plan));
            Assert.AreEqual(1, plan.Count);
            Assert.IsTrue(plan.TryGetSample(0, out CharacterTestDirectHitTracePlanSample beforeMutation));

            leakedBuildList.Clear();
            Assert.AreEqual(1, plan.Count);
            Assert.IsTrue(plan.TryGetSample(0, out CharacterTestDirectHitTracePlanSample afterMutation));
            Assert.AreEqual(beforeMutation.StableSampleId, afterMutation.StableSampleId);
            Assert.IsFalse(output.UpdatePlan(planIndex, 0, default));
            Assert.IsFalse(output.Seal(runtimeFrame, combatPhysicsRevision));
            Assert.AreEqual(sealedGeneration, output.Generation);
        }

        [Test]
        public void DirectHitTracePlanBuffer_KeepsActionSamplesAndBoundsIndependent()
        {
            RuntimeFrame runtimeFrame = new RuntimeFrame(7);
            var output = new CharacterTestDirectHitTracePlanBuffer();
            output.Reset(runtimeFrame, combatPhysicsRevision: 3);
            CombatEntityId sourceEntityId = new CombatEntityId(20001);
            CombatPhysicsLayerMask mask = CombatPhysicsLayerMask.FromLayer(10);

            int firstPlanIndex = output.GetOrBeginPlan(
                sourceEntityId,
                resolvedActionRevision: 101,
                CreatePreviewPoseIdentity());
            List<CharacterTestDirectHitTracePlanSample> firstSamples =
                output.GetPlanSamplesBuffer(firstPlanIndex);
            var firstBounds = default(CharacterTestDirectHitTraceBounds);
            int firstOrder = 0;
            Assert.IsTrue(CharacterTestDirectHitTracePlanSampler.TryAppendHitVolumeSample(
                CharacterTestDirectHitTraceSampleKind.ManualCapsule,
                sourceEntityId,
                actionId: 101,
                traceId: 1001,
                hitWindowId: 201,
                localFrame: 3,
                sourceOrder: 0,
                queryOffset: 0,
                mask,
                V(-1, 0, 0),
                V(1, 0, 0),
                F(1) / F(4),
                ref firstOrder,
                firstSamples,
                ref firstBounds,
                out _));
            output.UpdatePlan(firstPlanIndex, firstSamples.Count, firstBounds);

            int secondPlanIndex = output.GetOrBeginPlan(
                sourceEntityId,
                resolvedActionRevision: 202,
                CreatePreviewPoseIdentity());
            List<CharacterTestDirectHitTracePlanSample> secondSamples =
                output.GetPlanSamplesBuffer(secondPlanIndex);
            var secondBounds = default(CharacterTestDirectHitTraceBounds);
            int secondOrder = 0;
            Assert.IsTrue(CharacterTestDirectHitTracePlanSampler.TryAppendHitVolumeSample(
                CharacterTestDirectHitTraceSampleKind.BakedSocketCapsule,
                sourceEntityId,
                actionId: 202,
                traceId: 2002,
                hitWindowId: 302,
                localFrame: 4,
                sourceOrder: 1,
                queryOffset: 1,
                mask,
                V(9, 0, 0),
                V(11, 0, 0),
                F(1) / F(2),
                ref secondOrder,
                secondSamples,
                ref secondBounds,
                out _));
            output.UpdatePlan(secondPlanIndex, secondSamples.Count, secondBounds);
            Assert.IsTrue(output.Seal(runtimeFrame, combatPhysicsRevision: 3));

            CharacterTestDirectHitTracePlanCollectionView collection = output.CreateView();
            Assert.AreEqual(2, collection.Count);
            Assert.IsTrue(collection.TryGetPlan(0, out CharacterTestDirectHitTracePlanView firstPlan));
            Assert.IsTrue(collection.TryGetPlan(1, out CharacterTestDirectHitTracePlanView secondPlan));
            Assert.IsTrue(firstPlan.IsCurrent(runtimeFrame, 3, sourceEntityId, 101));
            Assert.IsTrue(secondPlan.IsCurrent(runtimeFrame, 3, sourceEntityId, 202));
            Assert.AreEqual(1, firstPlan.Count);
            Assert.AreEqual(1, secondPlan.Count);
            Assert.IsTrue(firstPlan.TryGetSample(0, out CharacterTestDirectHitTracePlanSample firstSample));
            Assert.IsTrue(secondPlan.TryGetSample(0, out CharacterTestDirectHitTracePlanSample secondSample));
            Assert.AreEqual(101, firstSample.ActionId);
            Assert.AreEqual(202, secondSample.ActionId);
            Assert.AreEqual(1001, firstSample.TraceId);
            Assert.AreEqual(2002, secondSample.TraceId);
            AssertVectorEqual(V(-1, 0, 0), firstSample.Query.PointA);
            AssertVectorEqual(V(9, 0, 0), secondSample.Query.PointA);
            Assert.IsTrue(firstPlan.TryGetBounds(out CharacterTestDirectHitTraceBounds firstViewBounds));
            Assert.IsTrue(secondPlan.TryGetBounds(out CharacterTestDirectHitTraceBounds secondViewBounds));
            AssertBoundsEqual(firstBounds, firstViewBounds);
            AssertBoundsEqual(secondBounds, secondViewBounds);
            Assert.IsTrue(firstViewBounds.Max.X < secondViewBounds.Min.X);

            Assert.IsTrue(firstPlan.TryGetSample(0, out CharacterTestDirectHitTracePlanSample firstSampleAgain));
            Assert.AreEqual(firstSample.StableSampleId, firstSampleAgain.StableSampleId);
            Assert.AreEqual(firstSample.Query.Header.QueryId, firstSampleAgain.Query.Header.QueryId);
        }

        [Test]
        public void DirectHitTracePlanBuffer_UpdatePlanRetainsWeaponAndCapsuleSamplesInOneAction()
        {
            RuntimeFrame runtimeFrame = new RuntimeFrame(8);
            CombatEntityId sourceEntityId = new CombatEntityId(20001);
            var output = new CharacterTestDirectHitTracePlanBuffer();
            output.Reset(runtimeFrame, combatPhysicsRevision: 4);
            int planIndex = output.GetOrBeginPlan(
                sourceEntityId,
                resolvedActionRevision: 303,
                CreatePreviewPoseIdentity());
            List<CharacterTestDirectHitTracePlanSample> samples = output.GetPlanSamplesBuffer(planIndex);
            var bounds = default(CharacterTestDirectHitTraceBounds);
            var queryBuffer = new List<CombatCapsuleQuery>(4);
            int queryId = 0;
            int sampleOrder = 0;

            CharacterTestDirectHitTracePlanSampler.AppendWeaponTraceSamples(
                new WeaponTraceFrame(
                    traceId: 3031,
                    rootPrev: V(0, 1, 0),
                    tipPrev: V(1, 1, 0),
                    rootNow: V(0, 1, 1),
                    tipNow: V(2, 1, 1),
                    radius: F(1) / F(4),
                    targetMask: CombatPhysicsLayerMask.FromLayer(10),
                    proximalExtension: Fix64.Zero,
                    proximalRadius: Fix64.Zero),
                sourceEntityId,
                actionId: 303,
                hitWindowId: 404,
                localFrame: 6,
                traceIndex: 0,
                useTipSweepCapsules: false,
                maxTipDistancePerSubstep: F(1),
                maxSubsteps: 8,
                ref queryId,
                ref sampleOrder,
                queryBuffer,
                samples,
                ref bounds);
            int weaponSampleCount = samples.Count;
            Assert.AreEqual(1, weaponSampleCount);

            Assert.IsTrue(CharacterTestDirectHitTracePlanSampler.TryAppendHitVolumeSample(
                CharacterTestDirectHitTraceSampleKind.ManualCapsule,
                sourceEntityId,
                actionId: 303,
                traceId: 3032,
                hitWindowId: 404,
                localFrame: 7,
                sourceOrder: 1,
                queryOffset: 2,
                targetMask: CombatPhysicsLayerMask.FromLayer(10),
                pointA: V(4, 0, 0),
                pointB: V(5, 0, 0),
                radius: F(1) / F(4),
                ref sampleOrder,
                samples,
                ref bounds,
                out _));
            int totalSampleCount = samples.Count;
            Assert.AreEqual(weaponSampleCount + 1, totalSampleCount);
            output.UpdatePlan(planIndex, totalSampleCount, bounds);
            Assert.IsTrue(output.Seal(runtimeFrame, combatPhysicsRevision: 4));

            CharacterTestDirectHitTracePlanCollectionView collection = output.CreateView();
            Assert.IsTrue(collection.TryGetPlan(0, out CharacterTestDirectHitTracePlanView plan));
            Assert.AreEqual(totalSampleCount, plan.Count);
            Assert.IsTrue(plan.TryGetSample(0, out CharacterTestDirectHitTracePlanSample weaponSample));
            Assert.IsTrue(plan.TryGetSample(weaponSampleCount, out CharacterTestDirectHitTracePlanSample capsuleSample));
            Assert.AreEqual(CharacterTestDirectHitTraceSampleKind.WeaponTraceBlade, weaponSample.Kind);
            Assert.AreEqual(CharacterTestDirectHitTraceSampleKind.ManualCapsule, capsuleSample.Kind);
            Assert.AreEqual(3031, weaponSample.TraceId);
            Assert.AreEqual(3032, capsuleSample.TraceId);
            AssertVectorEqual(V(0, 1, 1), weaponSample.Query.PointA);
            AssertVectorEqual(V(4, 0, 0), capsuleSample.Query.PointA);
            Assert.IsTrue(plan.TryGetBounds(out CharacterTestDirectHitTraceBounds viewBounds));
            AssertBoundsContains(viewBounds, weaponSample.Query.PointA, weaponSample.Query.PointB, weaponSample.Query.Radius);
            AssertBoundsContains(viewBounds, capsuleSample.Query.PointA, capsuleSample.Query.PointB, capsuleSample.Query.Radius);
        }

        [Test]
        public void DirectHitTracePreviewPlan_WarmedBuildLookupAndBoundsDoNotAllocate()
        {
            RuntimeFrame runtimeFrame = new RuntimeFrame(30);
            int combatPhysicsRevision = 17;
            CombatEntityId sourceEntityId = new CombatEntityId(20001);
            int resolvedActionRevision = 41;
            CreatePreviewInputs(
                out List<CharacterTestDirectHitTraceSourcePoseOffset> poses,
                out List<CharacterTestDirectHitTracePreviewWeaponFrame> weaponFrames,
                out List<CharacterTestDirectHitTracePreviewCapsuleFrame> capsuleFrames);
            var queryBuffer = new List<CombatCapsuleQuery>(64);
            var output = new CharacterTestDirectHitTracePlanBuffer();

            for (int i = 0; i < 8; i++)
            {
                Assert.IsTrue(CharacterTestDirectHitTracePlanSampler.TryBuildPreviewPlan(
                    runtimeFrame,
                    combatPhysicsRevision,
                    sourceEntityId,
                    resolvedActionRevision,
                    CreatePreviewPoseIdentity(),
                    poses,
                    weaponFrames,
                    capsuleFrames,
                    useTipSweepCapsules: false,
                    maxTipDistancePerSubstep: F(1),
                    maxSubsteps: 8,
                    queryBuffer,
                    output,
                    out CharacterTestDirectHitTracePlanView warmedPlan));
                CharacterTestDirectHitTracePlanCollectionView warmedCollection = output.CreateView();
                Assert.IsTrue(warmedCollection.TryGetPlan(0, out CharacterTestDirectHitTracePlanView lookedUpPlan));
                Assert.IsTrue(lookedUpPlan.TryGetSample(0, out _));
                Assert.IsTrue(lookedUpPlan.TryGetBounds(out _));
            }

            _ = GC.GetAllocatedBytesForCurrentThread();
            long before = GC.GetAllocatedBytesForCurrentThread();
            bool observed = true;
            for (int i = 0; i < 64; i++)
            {
                observed &= CharacterTestDirectHitTracePlanSampler.TryBuildPreviewPlan(
                    runtimeFrame,
                    combatPhysicsRevision,
                    sourceEntityId,
                    resolvedActionRevision,
                    CreatePreviewPoseIdentity(),
                    poses,
                    weaponFrames,
                    capsuleFrames,
                    useTipSweepCapsules: false,
                    maxTipDistancePerSubstep: F(1),
                    maxSubsteps: 8,
                    queryBuffer,
                    output,
                    out CharacterTestDirectHitTracePlanView plan);
                CharacterTestDirectHitTracePlanCollectionView collection = output.CreateView();
                observed &= collection.IsCurrent(runtimeFrame, combatPhysicsRevision);
                observed &= collection.TryGetPlan(0, out CharacterTestDirectHitTracePlanView lookedUpPlan);
                observed &= lookedUpPlan.IsCurrent(
                    runtimeFrame,
                    combatPhysicsRevision,
                    sourceEntityId,
                    resolvedActionRevision);
                observed &= lookedUpPlan.TryGetSample(0, out _);
                observed &= lookedUpPlan.TryGetBounds(out _);
                observed &= plan.Count > 0;
            }

            long allocatedBytes = GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.IsTrue(observed);
            Assert.AreEqual(0L, allocatedBytes);
        }

        [Test]
        public void DirectHitTracePreviewPlan_RejectsHeterogeneousActionSamples()
        {
            var runtimeFrame = new RuntimeFrame(30);
            const int combatPhysicsRevision = 17;
            var sourceEntityId = new CombatEntityId(20001);
            const int resolvedActionRevision = 41;
            CreatePreviewInputs(
                out List<CharacterTestDirectHitTraceSourcePoseOffset> poses,
                out List<CharacterTestDirectHitTracePreviewWeaponFrame> weaponFrames,
                out List<CharacterTestDirectHitTracePreviewCapsuleFrame> capsuleFrames);
            CharacterTestDirectHitTracePreviewCapsuleFrame first = capsuleFrames[0];
            capsuleFrames[0] = new CharacterTestDirectHitTracePreviewCapsuleFrame(
                first.Kind,
                first.LocalFrame,
                first.SourceOrder,
                first.QueryOffset,
                first.ActionId + 1,
                first.TraceId,
                first.HitWindowId,
                first.TargetMask,
                first.PointALocal,
                first.PointBLocal,
                first.Radius);

            AssertPreviewRejected(
                runtimeFrame,
                combatPhysicsRevision,
                sourceEntityId,
                resolvedActionRevision,
                poses,
                weaponFrames,
                capsuleFrames);
        }

        [Test]
        public void DirectHitTracePreviewPlan_RejectsMalformedMissingGapUnorderedAndUnknownInputs()
        {
            RuntimeFrame runtimeFrame = new RuntimeFrame(30);
            int combatPhysicsRevision = 17;
            CombatEntityId sourceEntityId = new CombatEntityId(20001);
            int resolvedActionRevision = 41;

            CreatePreviewInputs(
                out List<CharacterTestDirectHitTraceSourcePoseOffset> poses,
                out List<CharacterTestDirectHitTracePreviewWeaponFrame> weaponFrames,
                out List<CharacterTestDirectHitTracePreviewCapsuleFrame> capsuleFrames);
            AssertPreviewRejected(
                runtimeFrame,
                combatPhysicsRevision,
                sourceEntityId,
                resolvedActionRevision,
                new List<CharacterTestDirectHitTraceSourcePoseOffset>(),
                weaponFrames,
                capsuleFrames);
            var gapPoses = new List<CharacterTestDirectHitTraceSourcePoseOffset>
            {
                poses[0],
                new CharacterTestDirectHitTraceSourcePoseOffset(12, V(2, 0, 0), 0)
            };
            var gapWeapons = new List<CharacterTestDirectHitTracePreviewWeaponFrame>
            {
                weaponFrames[0]
            };
            var gapCapsules = new List<CharacterTestDirectHitTracePreviewCapsuleFrame>
            {
                new CharacterTestDirectHitTracePreviewCapsuleFrame(
                    kind: CharacterTestDirectHitTraceSampleKind.ManualCapsule,
                    localFrame: 12,
                    sourceOrder: 0,
                    queryOffset: 0,
                    actionId: 7,
                    traceId: 221,
                    hitWindowId: 53,
                    targetMask: CombatPhysicsLayerMask.FromLayer(10),
                    pointALocal: V(0, 0, 0),
                    pointBLocal: V(0, 1, 0),
                    radius: F(1) / F(4))
            };
            AssertPreviewRejected(
                runtimeFrame,
                combatPhysicsRevision,
                sourceEntityId,
                resolvedActionRevision,
                gapPoses,
                gapWeapons,
                gapCapsules);
            AssertPreviewRejected(
                runtimeFrame,
                combatPhysicsRevision,
                sourceEntityId,
                resolvedActionRevision,
                new List<CharacterTestDirectHitTraceSourcePoseOffset> { poses[1], poses[0] },
                weaponFrames,
                capsuleFrames);

            var missingFrameWeapon = new List<CharacterTestDirectHitTracePreviewWeaponFrame>(weaponFrames)
            {
                new CharacterTestDirectHitTracePreviewWeaponFrame(
                    localFrame: 12,
                    traceIndex: 2,
                    traceId: 103,
                    actionId: 7,
                    rootPrevLocal: V(0, 1, 0),
                    tipPrevLocal: V(1, 1, 0),
                    rootNowLocal: V(0, 1, 1),
                    tipNowLocal: V(2, 1, 1),
                    radius: F(1) / F(4),
                    targetMask: CombatPhysicsLayerMask.FromLayer(10),
                    proximalExtension: F(1) / F(2),
                    proximalRadius: F(1) / F(8))
            };
            AssertPreviewRejected(
                runtimeFrame,
                combatPhysicsRevision,
                sourceEntityId,
                resolvedActionRevision,
                poses,
                missingFrameWeapon,
                capsuleFrames);

            var malformedWeapon = new List<CharacterTestDirectHitTracePreviewWeaponFrame>
            {
                new CharacterTestDirectHitTracePreviewWeaponFrame(
                    localFrame: 10,
                    traceIndex: 0,
                    traceId: 101,
                    actionId: 0,
                    rootPrevLocal: V(0, 1, 0),
                    tipPrevLocal: V(1, 1, 0),
                    rootNowLocal: V(0, 1, 1),
                    tipNowLocal: V(2, 1, 1),
                    radius: F(1) / F(4),
                    targetMask: CombatPhysicsLayerMask.FromLayer(10),
                    proximalExtension: F(1) / F(2),
                    proximalRadius: F(1) / F(8))
            };
            AssertPreviewRejected(
                runtimeFrame,
                combatPhysicsRevision,
                sourceEntityId,
                resolvedActionRevision,
                poses,
                malformedWeapon,
                capsuleFrames);

            var unknownKindCapsule = new List<CharacterTestDirectHitTracePreviewCapsuleFrame>(capsuleFrames);
            unknownKindCapsule[0] = new CharacterTestDirectHitTracePreviewCapsuleFrame(
                (CharacterTestDirectHitTraceSampleKind)999,
                capsuleFrames[0].LocalFrame,
                capsuleFrames[0].SourceOrder,
                capsuleFrames[0].QueryOffset,
                capsuleFrames[0].ActionId,
                capsuleFrames[0].TraceId,
                capsuleFrames[0].HitWindowId,
                capsuleFrames[0].TargetMask,
                capsuleFrames[0].PointALocal,
                capsuleFrames[0].PointBLocal,
                capsuleFrames[0].Radius);
            AssertPreviewRejected(
                runtimeFrame,
                combatPhysicsRevision,
                sourceEntityId,
                resolvedActionRevision,
                poses,
                weaponFrames,
                unknownKindCapsule);

            var missingFrameCapsule = new List<CharacterTestDirectHitTracePreviewCapsuleFrame>(capsuleFrames);
            missingFrameCapsule[0] = new CharacterTestDirectHitTracePreviewCapsuleFrame(
                kind: capsuleFrames[0].Kind,
                localFrame: 12,
                sourceOrder: capsuleFrames[0].SourceOrder,
                queryOffset: capsuleFrames[0].QueryOffset,
                actionId: capsuleFrames[0].ActionId,
                traceId: capsuleFrames[0].TraceId,
                hitWindowId: capsuleFrames[0].HitWindowId,
                targetMask: capsuleFrames[0].TargetMask,
                pointALocal: capsuleFrames[0].PointALocal,
                pointBLocal: capsuleFrames[0].PointBLocal,
                radius: capsuleFrames[0].Radius);
            AssertPreviewRejected(
                runtimeFrame,
                combatPhysicsRevision,
                sourceEntityId,
                resolvedActionRevision,
                poses,
                weaponFrames,
                missingFrameCapsule);

            AssertPreviewRejected(
                runtimeFrame,
                combatPhysicsRevision,
                sourceEntityId,
                resolvedActionRevision,
                poses,
                null,
                capsuleFrames);
        }

        [Test]
        public void CharacterTestCharacterHurtboxSystem_RemovedEntityInvalidatesSnapshotAndRemovesBody()
        {
            var world = new CombatPhysicsWorld();
            var physics = new CharacterTestCharacterPhysicsRuntime(
                world,
                CombatPhysicsLayerMask.FromLayer(0),
                V(1, 1, 1),
                F(1) / F(100),
                F(1),
                staticColliderCount: 0,
                sourceDescription: "CD-M0 test");
            var hurtboxes = new CharacterTestCharacterHurtboxSystem();
            CharacterTestCharacterEntity entity = CreateEntity(3, 2f, 0f, 0f, 0f);
            var entities = new List<CharacterTestCharacterEntity> { entity };

            hurtboxes.SyncAll(entities, physics, new RuntimeFrame(1));
            Assert.IsTrue(hurtboxes.TryGetDamageGeometrySnapshot(
                entity.Id.Value,
                out CharacterTestDamageGeometrySnapshot oldSnapshot));
            Assert.IsTrue(oldSnapshot.IsValid);
            CombatBodyId bodyId = physics.GetHurtboxBodyId(entity);
            Assert.IsTrue(world.TryGetBody(bodyId, out _));

            hurtboxes.SyncAll(Array.Empty<CharacterTestCharacterEntity>(), physics, new RuntimeFrame(2));
            Assert.IsFalse(oldSnapshot.IsValid);
            Assert.IsFalse(hurtboxes.TryGetDamageGeometrySnapshot(entity.Id.Value, out _));
            Assert.IsFalse(world.TryGetBody(bodyId, out _));
        }

        [Test]
        public void DirectHitTracePreviewPlan_AllowsDeclaredIdleGapButRejectsMissingActiveTraceOrTrajectory()
        {
            var poses=new List<CharacterTestDirectHitTraceSourcePoseOffset>{
                new(10,FixVector3.Zero,0),new(11,FixVector3.Zero,0),new(12,FixVector3.Zero,0)};
            var weapons=new List<CharacterTestDirectHitTracePreviewWeaponFrame>();
            var capsules=new List<CharacterTestDirectHitTracePreviewCapsuleFrame>();
            foreach(int frame in new[]{10,12})capsules.Add(new CharacterTestDirectHitTracePreviewCapsuleFrame(
                CharacterTestDirectHitTraceSampleKind.ManualCapsule,frame,0,0,7,frame+200,41,
                CombatPhysicsLayerMask.FromLayer(1),V(0,0,0),V(0,2,0),F(1)/F(4)));
            var queries=new List<CombatCapsuleQuery>();var buffer=new CharacterTestDirectHitTracePlanBuffer();
            Assert.IsTrue(CharacterTestDirectHitTracePlanSampler.TryBuildPreviewPlan(new RuntimeFrame(30),17,
                new CombatEntityId(20001),41,CreatePreviewPoseIdentity(),poses,weapons,capsules,false,F(1),8,
                queries,buffer,out var plan,requiredActiveFrames:new[]{10,12}));
            for(int i=0;i<plan.Count;i++){Assert.IsTrue(plan.TryGetSample(i,out var sample));Assert.AreNotEqual(11,sample.LocalFrame);}
            Assert.IsFalse(CharacterTestDirectHitTracePlanSampler.TryBuildPreviewPlan(new RuntimeFrame(30),17,
                new CombatEntityId(20001),41,CreatePreviewPoseIdentity(),poses,weapons,capsules,false,F(1),8,
                queries,buffer,out _,requiredActiveFrames:new[]{10,11,12}),"An active frame cannot be silently omitted.");
            capsules.RemoveAt(1);
            Assert.IsFalse(CharacterTestDirectHitTracePlanSampler.TryBuildPreviewPlan(new RuntimeFrame(30),17,
                new CombatEntityId(20001),41,CreatePreviewPoseIdentity(),poses,weapons,capsules,false,F(1),8,
                queries,buffer,out _,requiredActiveFrames:new[]{10,12}));
            poses.RemoveAt(1);
            Assert.IsFalse(CharacterTestDirectHitTracePlanSampler.TryBuildPreviewPlan(new RuntimeFrame(30),17,
                new CombatEntityId(20001),41,CreatePreviewPoseIdentity(),poses,weapons,capsules,false,F(1),8,
                queries,buffer,out _,requiredActiveFrames:new[]{10}),"The nominal trajectory remains complete across idle frames.");
        }

        private static void CreatePreviewInputs(
            out List<CharacterTestDirectHitTraceSourcePoseOffset> poses,
            out List<CharacterTestDirectHitTracePreviewWeaponFrame> weaponFrames,
            out List<CharacterTestDirectHitTracePreviewCapsuleFrame> capsuleFrames)
        {
            CombatPhysicsLayerMask targetMask = CombatPhysicsLayerMask.FromLayer(10);
            poses = new List<CharacterTestDirectHitTraceSourcePoseOffset>
            {
                new CharacterTestDirectHitTraceSourcePoseOffset(10, V(0, 0, 0), 0),
                new CharacterTestDirectHitTraceSourcePoseOffset(11, V(1, 0, 0), 90000)
            };
            weaponFrames = new List<CharacterTestDirectHitTracePreviewWeaponFrame>
            {
                new CharacterTestDirectHitTracePreviewWeaponFrame(
                    localFrame: 10,
                    traceIndex: 0,
                    traceId: 101,
                    actionId: 7,
                    rootPrevLocal: V(0, 1, 0),
                    tipPrevLocal: V(1, 1, 0),
                    rootNowLocal: V(0, 1, 1),
                    tipNowLocal: V(2, 1, 1),
                    radius: F(1) / F(4),
                    targetMask: targetMask,
                    proximalExtension: F(1) / F(2),
                    proximalRadius: F(1) / F(8)),
                new CharacterTestDirectHitTracePreviewWeaponFrame(
                    localFrame: 11,
                    traceIndex: 1,
                    traceId: 102,
                    actionId: 7,
                    rootPrevLocal: V(0, 1, 0),
                    tipPrevLocal: V(1, 1, 0),
                    rootNowLocal: V(0, 1, 1),
                    tipNowLocal: V(3, 1, 1),
                    radius: F(1) / F(4),
                    targetMask: targetMask,
                    proximalExtension: F(1) / F(2),
                    proximalRadius: F(1) / F(8))
            };
            capsuleFrames = new List<CharacterTestDirectHitTracePreviewCapsuleFrame>
            {
                new CharacterTestDirectHitTracePreviewCapsuleFrame(
                    CharacterTestDirectHitTraceSampleKind.ManualCapsule,
                    localFrame: 10,
                    sourceOrder: 0,
                    queryOffset: 0,
                    actionId: 7,
                    traceId: 201,
                    hitWindowId: 41,
                    targetMask: targetMask,
                    pointALocal: V(0, 0, 0),
                    pointBLocal: V(0, 2, 0),
                    radius: F(1) / F(4)),
                new CharacterTestDirectHitTracePreviewCapsuleFrame(
                    CharacterTestDirectHitTraceSampleKind.CasterBody,
                    localFrame: 10,
                    sourceOrder: 1,
                    queryOffset: 1,
                    actionId: 7,
                    traceId: 202,
                    hitWindowId: 41,
                    targetMask: targetMask,
                    pointALocal: V(-1, 0, 0),
                    pointBLocal: V(1, 0, 0),
                    radius: F(1) / F(3)),
                new CharacterTestDirectHitTracePreviewCapsuleFrame(
                    CharacterTestDirectHitTraceSampleKind.BakedSocketCapsule,
                    localFrame: 10,
                    sourceOrder: 2,
                    queryOffset: 2,
                    actionId: 7,
                    traceId: 203,
                    hitWindowId: 41,
                    targetMask: targetMask,
                    pointALocal: V(0, 0, 1),
                    pointBLocal: V(0, 1, 1),
                    radius: F(1) / F(5)),
                new CharacterTestDirectHitTracePreviewCapsuleFrame(
                    CharacterTestDirectHitTraceSampleKind.ManualCapsule,
                    localFrame: 11,
                    sourceOrder: 0,
                    queryOffset: 0,
                    actionId: 7,
                    traceId: 211,
                    hitWindowId: 41,
                    targetMask: targetMask,
                    pointALocal: V(0, 0, 0),
                    pointBLocal: V(0, 2, 0),
                    radius: F(1) / F(4)),
                new CharacterTestDirectHitTracePreviewCapsuleFrame(
                    CharacterTestDirectHitTraceSampleKind.CasterBody,
                    localFrame: 11,
                    sourceOrder: 1,
                    queryOffset: 1,
                    actionId: 7,
                    traceId: 212,
                    hitWindowId: 41,
                    targetMask: targetMask,
                    pointALocal: V(-1, 0, 0),
                    pointBLocal: V(1, 0, 0),
                    radius: F(1) / F(3)),
                new CharacterTestDirectHitTracePreviewCapsuleFrame(
                    CharacterTestDirectHitTraceSampleKind.BakedSocketCapsule,
                    localFrame: 11,
                    sourceOrder: 2,
                    queryOffset: 2,
                    actionId: 7,
                    traceId: 213,
                    hitWindowId: 41,
                    targetMask: targetMask,
                    pointALocal: V(0, 0, 1),
                    pointBLocal: V(0, 1, 1),
                    radius: F(1) / F(5))
            };
        }

        private static void AppendExpectedPreviewSamples(
            List<CharacterTestDirectHitTraceSourcePoseOffset> poses,
            List<CharacterTestDirectHitTracePreviewWeaponFrame> weaponFrames,
            List<CharacterTestDirectHitTracePreviewCapsuleFrame> capsuleFrames,
            CombatEntityId sourceEntityId,
            int resolvedActionRevision,
            List<CombatCapsuleQuery> queryBuffer,
            List<CharacterTestDirectHitTracePlanSample> samples,
            ref CharacterTestDirectHitTraceBounds bounds)
        {
            int queryId = 0;
            int sampleOrder = 0;
            for (int poseIndex = 0; poseIndex < poses.Count; poseIndex++)
            {
                CharacterTestDirectHitTraceSourcePose pose =
                    poses[poseIndex].Resolve(CreatePreviewPoseIdentity());
                for (int weaponIndex = 0; weaponIndex < weaponFrames.Count; weaponIndex++)
                {
                    CharacterTestDirectHitTracePreviewWeaponFrame weapon = weaponFrames[weaponIndex];
                    if (weapon.LocalFrame != pose.LocalFrame)
                        continue;

                    CharacterTestDirectHitTracePlanSampler.AppendWeaponTraceSamples(
                        weapon.Resolve(pose),
                        sourceEntityId,
                        weapon.ActionId,
                        resolvedActionRevision,
                        weapon.LocalFrame,
                        weapon.TraceIndex,
                        useTipSweepCapsules: false,
                        maxTipDistancePerSubstep: F(1),
                        maxSubsteps: 8,
                        ref queryId,
                        ref sampleOrder,
                        queryBuffer,
                        samples,
                        ref bounds);
                }

                for (int capsuleIndex = 0; capsuleIndex < capsuleFrames.Count; capsuleIndex++)
                {
                    CharacterTestDirectHitTracePreviewCapsuleFrame capsule = capsuleFrames[capsuleIndex];
                    if (capsule.LocalFrame != pose.LocalFrame)
                        continue;

                    Assert.IsTrue(CharacterTestDirectHitTracePlanSampler.TryAppendHitVolumeSample(
                        capsule.Kind,
                        sourceEntityId,
                        capsule.ActionId,
                        capsule.TraceId,
                        capsule.HitWindowId,
                        capsule.LocalFrame,
                        capsule.SourceOrder,
                        capsule.QueryOffset,
                        capsule.TargetMask,
                        pose.TransformLocal(capsule.PointALocal),
                        pose.TransformLocal(capsule.PointBLocal),
                        capsule.Radius,
                        ref sampleOrder,
                        samples,
                        ref bounds,
                        out _));
                }
            }
        }

        private static void AssertPreviewRejected(
            RuntimeFrame runtimeFrame,
            int combatPhysicsRevision,
            CombatEntityId sourceEntityId,
            int resolvedActionRevision,
            IReadOnlyList<CharacterTestDirectHitTraceSourcePoseOffset> poses,
            IReadOnlyList<CharacterTestDirectHitTracePreviewWeaponFrame> weaponFrames,
            IReadOnlyList<CharacterTestDirectHitTracePreviewCapsuleFrame> capsuleFrames)
        {
            var output = new CharacterTestDirectHitTracePlanBuffer();
            var queryBuffer = new List<CombatCapsuleQuery>(64);
            Assert.IsFalse(CharacterTestDirectHitTracePlanSampler.TryBuildPreviewPlan(
                runtimeFrame,
                combatPhysicsRevision,
                sourceEntityId,
                resolvedActionRevision,
                CreatePreviewPoseIdentity(),
                poses,
                weaponFrames,
                capsuleFrames,
                useTipSweepCapsules: false,
                maxTipDistancePerSubstep: F(1),
                maxSubsteps: 8,
                queryBuffer,
                output,
                out CharacterTestDirectHitTracePlanView plan));
            Assert.AreEqual(0, output.PlanCount);
            Assert.AreEqual(0, plan.Count);
            Assert.IsFalse(plan.TryGetSample(0, out _));
            Assert.IsFalse(plan.TryGetBounds(out _));
        }

        private static void AssertPlanSampleEqual(
            CharacterTestDirectHitTracePlanSample expected,
            CharacterTestDirectHitTracePlanSample actual)
        {
            Assert.AreEqual(expected.Kind, actual.Kind);
            Assert.AreEqual(expected.LocalFrame, actual.LocalFrame);
            Assert.AreEqual(expected.SampleOrder, actual.SampleOrder);
            Assert.AreEqual(expected.QueryOffset, actual.QueryOffset);
            Assert.AreEqual(expected.HitWindowId, actual.HitWindowId);
            Assert.AreEqual(expected.ActionId, actual.ActionId);
            Assert.AreEqual(expected.TraceId, actual.TraceId);
            Assert.AreEqual(expected.ResolvePriority, actual.ResolvePriority);
            Assert.AreEqual(expected.StableSampleId, actual.StableSampleId);
            Assert.AreEqual(expected.Query.Header.QueryId, actual.Query.Header.QueryId);
            Assert.AreEqual(expected.Query.Header.Kind, actual.Query.Header.Kind);
            Assert.AreEqual(expected.Query.Header.SourceEntityId.Value, actual.Query.Header.SourceEntityId.Value);
            Assert.AreEqual(expected.Query.Header.TraceId, actual.Query.Header.TraceId);
            Assert.AreEqual(expected.Query.Header.ActionId, actual.Query.Header.ActionId);
            Assert.AreEqual(expected.Query.Header.SourceOrder, actual.Query.Header.SourceOrder);
            Assert.AreEqual(expected.Query.Header.LayerMask.Value, actual.Query.Header.LayerMask.Value);
            AssertVectorEqual(expected.Query.PointA, actual.Query.PointA);
            AssertVectorEqual(expected.Query.PointB, actual.Query.PointB);
            AssertFix64Equal(expected.Query.Radius, actual.Query.Radius);
        }

        private static CharacterTestDirectHitTracePlanPoseIdentity CreatePreviewPoseIdentity()
        {
            return new CharacterTestDirectHitTracePlanPoseIdentity(
                CharacterTestDirectHitTracePlanPoseKind.CurrentPose,
                V(0, 0, 0),
                sourceYawMilliDegrees: 0,
                sourcePoseRevision: 5,
                sourceFacingRevision: 6);
        }

        private static void AssertBoundsEqual(
            CharacterTestDirectHitTraceBounds expected,
            CharacterTestDirectHitTraceBounds actual)
        {
            Assert.AreEqual(expected.HasBounds, actual.HasBounds);
            if (!expected.HasBounds)
                return;

            AssertVectorEqual(expected.Min, actual.Min);
            AssertVectorEqual(expected.Max, actual.Max);
        }

        private static bool HasSampleAtFrame(
            CharacterTestDirectHitTracePlanView plan,
            int localFrame)
        {
            for (int i = 0; i < plan.Count; i++)
            {
                if (plan.TryGetSample(i, out CharacterTestDirectHitTracePlanSample sample)
                    && sample.LocalFrame == localFrame)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool HasRootMotionDisplacement(
            CharacterTestDirectHitTracePlanView plan,
            int firstFrame,
            int secondFrame)
        {
            bool foundFirst = false;
            bool foundSecond = false;
            FixVector3 first = FixVector3.Zero;
            FixVector3 second = FixVector3.Zero;
            for (int i = 0; i < plan.Count; i++)
            {
                if (!plan.TryGetSample(i, out CharacterTestDirectHitTracePlanSample sample)
                    || sample.Kind != CharacterTestDirectHitTraceSampleKind.WeaponTraceBlade)
                {
                    continue;
                }

                if (sample.LocalFrame == firstFrame && !foundFirst)
                {
                    first = sample.Query.PointA;
                    foundFirst = true;
                }
                else if (sample.LocalFrame == secondFrame && !foundSecond)
                {
                    second = sample.Query.PointA;
                    foundSecond = true;
                }
            }

            return foundFirst
                && foundSecond
                && !(first.X == second.X && first.Y == second.Y && first.Z == second.Z);
        }

        private static CharacterTestCharacterEntity CreateEntity(
            int id,
            float x,
            float y,
            float z,
            float yawDegrees,
            int gameplayGeneration = 1)
        {
            return new CharacterTestCharacterEntity(
                new CharacterTestCharacterEntityId(id),
                new CharacterTestCharacterIdentityComponent(
                    characterId: 1,
                    displayName: "CD-M0 Test Character",
                    prefabKey: default(ResourceKey),
                    animationBinding: default(CharacterRuntimeAnimationBinding)),
                new CharacterTestCharacterTeamComponent(teamId: 1, defaultTeamId: 1),
                new CharacterTestCharacterTransformComponent(x, y, z, yawDegrees),
                new CharacterTestCharacterEquipmentComponent(useDefaultLoadout: false),
                new CharacterTestCharacterControlComponent(CharacterTestCharacterControlOwner.None),
                new GameplayEntityId(id, gameplayGeneration),
                CharacterTestCharacterPhysicsComponent.Default);
        }

        private static CharacterTestDamageGeometrySnapshot CreateGeometry(
            int entityId,
            CombatEntityId combatEntityId,
            RuntimeFrame frame,
            int worldRevision,
            int hurtboxRevision,
            bool reverseInsertion)
        {
            return CreateGeometry(
                entityId,
                new GameplayEntityId(entityId, 1),
                combatEntityId,
                frame,
                worldRevision,
                hurtboxRevision,
                reverseInsertion);
        }

        private static CharacterTestDamageGeometrySnapshot CreateGeometry(
            int entityId,
            GameplayEntityId gameplayEntityId,
            CombatEntityId combatEntityId,
            RuntimeFrame frame,
            int worldRevision,
            int hurtboxRevision,
            bool reverseInsertion)
        {
            var snapshot = new CharacterTestDamageGeometrySnapshot(initialColliderCapacity: 3);
            snapshot.BeginWrite(entityId, gameplayEntityId, combatEntityId);
            if (reverseInsertion)
            {
                AddCollider(snapshot, "torso", 1002, "body", "center", 2, V(-1, -1, -1), V(2, 4, 1));
                AddCollider(snapshot, "arm", 1001, "arm.r", "upper", 1, V(-2, 0, -1), V(0, 3, 0));
                AddCollider(snapshot, "tail", 1003, "tail", "rear", 3, V(2, 0, -1), V(4, 2, 1));
            }
            else
            {
                AddCollider(snapshot, "tail", 1003, "tail", "rear", 3, V(2, 0, -1), V(4, 2, 1));
                AddCollider(snapshot, "arm", 1001, "arm.r", "upper", 1, V(-2, 0, -1), V(0, 3, 0));
                AddCollider(snapshot, "torso", 1002, "body", "center", 2, V(-1, -1, -1), V(2, 4, 1));
            }
            snapshot.SortCollidersByStableLogicalId();
            snapshot.Commit(frame, worldRevision, hurtboxRevision);
            return snapshot;
        }

        private static CharacterTestDamageGeometrySnapshot CreateLargeTargetGeometry()
        {
            var snapshot = new CharacterTestDamageGeometrySnapshot(initialColliderCapacity: 2);
            snapshot.BeginWrite(2, new GameplayEntityId(2, 1), new CombatEntityId(20002));
            AddCollider(snapshot, "root", 1001, "body", "center", 1, V(98, -1, -1), V(102, 1, 1));
            AddCollider(snapshot, "tail", 1002, "tail", "rear", 2, V(2, 0, -1), V(4, 2, 1));
            snapshot.SortCollidersByStableLogicalId();
            snapshot.Commit(new RuntimeFrame(1), worldRevision: 1, hurtboxRevision: 1);
            return snapshot;
        }

        private static void AppendManualFrameSample(
            CharacterTestDirectHitTraceSourcePose sourcePose,
            CharacterActionHitVolumeConfig hitVolume,
            ref int sampleOrder,
            List<CharacterTestDirectHitTracePlanSample> samples,
            ref CharacterTestDirectHitTraceBounds bounds)
        {
            Assert.IsTrue(CharacterTestDirectHitTracePlanSampler.TryBuildManualCapsule(
                sourcePose,
                hitVolume,
                out FixVector3 pointA,
                out FixVector3 pointB,
                out Fix64 radius));
            Assert.IsTrue(CharacterTestDirectHitTracePlanSampler.TryAppendHitVolumeSample(
                CharacterTestDirectHitTraceSampleKind.ManualCapsule,
                new CombatEntityId(20001),
                actionId: 7,
                traceId: 71,
                hitWindowId: 9,
                localFrame: sourcePose.LocalFrame,
                sourceOrder: 0,
                queryOffset: sourcePose.LocalFrame,
                targetMask: CombatPhysicsLayerMask.FromLayer(10),
                pointA,
                pointB,
                radius,
                ref sampleOrder,
                samples,
                ref bounds,
                out _));
        }

        private static void AddCollider(
            CharacterTestDamageGeometrySnapshot snapshot,
            string logicalColliderId,
            int combatColliderId,
            string partId,
            string hitZoneId,
            int priority,
            FixVector3 worldMin,
            FixVector3 worldMax)
        {
            snapshot.AddCollider(
                ResolveLogicalColliderId(logicalColliderId),
                logicalColliderId,
                new CombatColliderId(combatColliderId),
                ResolvePartId(partId),
                partId,
                ResolveHitZoneId(hitZoneId),
                hitZoneId,
                priority,
                CharacterTestCharacterPhysicsRuntime.CharacterHurtboxLayer,
                CombatPhysicsShape.Aabb(worldMin, worldMax),
                worldMin,
                worldMax);
        }

        private static int ResolveLogicalColliderId(string label)
        {
            switch (label)
            {
                case "arm": return CharacterTestAuthoredContentIds.D400_Id57;
                case "tail": return CharacterTestAuthoredContentIds.D400_Id103;
                case "torso": return CharacterTestAuthoredContentIds.D400_Id70;
                case "root": return CharacterTestAuthoredContentIds.D400_Id1;
                default: return CharacterTestAuthoredContentIds.D400_Id36;
            }
        }

        private static int ResolvePartId(string label)
        {
            switch (label)
            {
                case "upper": return CharacterTestAuthoredContentIds.D400_Id58;
                case "rear": return CharacterTestAuthoredContentIds.D400_Id103;
                default: return CharacterTestAuthoredContentIds.D400_Id34;
            }
        }

        private static int ResolveHitZoneId(string label)
        {
            switch (label)
            {
                case "arm.r": return CharacterTestAuthoredContentIds.D400_Id60;
                case "rear": return CharacterTestAuthoredContentIds.D400_Id103;
                default: return CharacterTestAuthoredContentIds.D400_Id34;
            }
        }

        private static bool BoundsIntersect(
            FixVector3 leftMin,
            FixVector3 leftMax,
            FixVector3 rightMin,
            FixVector3 rightMax)
        {
            return leftMin.X <= rightMax.X
                && leftMax.X >= rightMin.X
                && leftMin.Y <= rightMax.Y
                && leftMax.Y >= rightMin.Y
                && leftMin.Z <= rightMax.Z
                && leftMax.Z >= rightMin.Z;
        }

        private static void AssertBoundsContains(
            CharacterTestDirectHitTraceBounds bounds,
            FixVector3 pointA,
            FixVector3 pointB,
            Fix64 radius)
        {
            FixVector3 min = new FixVector3(
                Min(pointA.X, pointB.X) - radius,
                Min(pointA.Y, pointB.Y) - radius,
                Min(pointA.Z, pointB.Z) - radius);
            FixVector3 max = new FixVector3(
                Max(pointA.X, pointB.X) + radius,
                Max(pointA.Y, pointB.Y) + radius,
                Max(pointA.Z, pointB.Z) + radius);
            Assert.IsTrue(bounds.Min.X <= min.X);
            Assert.IsTrue(bounds.Min.Y <= min.Y);
            Assert.IsTrue(bounds.Min.Z <= min.Z);
            Assert.IsTrue(bounds.Max.X >= max.X);
            Assert.IsTrue(bounds.Max.Y >= max.Y);
            Assert.IsTrue(bounds.Max.Z >= max.Z);
        }

        private static Fix64 Min(Fix64 left, Fix64 right)
        {
            return left < right ? left : right;
        }

        private static Fix64 Max(Fix64 left, Fix64 right)
        {
            return left > right ? left : right;
        }

        private static FixVector3 V(int x, int y, int z)
        {
            return new FixVector3(F(x), F(y), F(z));
        }

        private static FixVector3 VF(Fix64 x, Fix64 y, Fix64 z)
        {
            return new FixVector3(x, y, z);
        }

        private static Fix64 F(int value)
        {
            return Fix64.FromInt(value);
        }

        private static void AssertVectorEqual(FixVector3 expected, FixVector3 actual)
        {
            Assert.IsTrue(expected.X == actual.X);
            Assert.IsTrue(expected.Y == actual.Y);
            Assert.IsTrue(expected.Z == actual.Z);
        }

        private static void AssertFix64Equal(Fix64 expected, Fix64 actual)
        {
            Assert.IsTrue(expected == actual);
        }
    }
}
