using System;
using System.Collections.Generic;
using MxFramework.CharacterRuntimeSpawn;
using MxFramework.Combat.Core;
using MxFramework.Combat.Physics;
using MxFramework.Core.Math;
using MxFramework.Game.CharacterTest;
using MxFramework.Runtime;
using NUnit.Framework;
using UnityEngine;

namespace MxFramework.Tests.Game.CharacterTest
{
    public sealed partial class CharacterTestIntegrationContractTests
    {
        [TestCase(true)]
        [TestCase(false)]
        public void RuntimeCommandModule_TargetLock_CombatHandleKeepsCasterIdentity(bool explicitSet)
        {
            CharacterTestCharacterDefinition definition = GetIronVanguardDefinition();
            var module = new CharacterTestCharacterRuntimeModule(
                null,
                NullRuntimeLogger.Instance,
                definitions: new CharacterTestCharacterDefinitionRegistry(new[] { definition }),
                createViews: false,
                physicsRuntime: CreateFrameworkPhysicsRuntime(includeWall: false));
            CharacterTestCharacterEntity source = SpawnPerceptionTestEntity(
                module, teamId: 1, x: 0f, z: 0f,
                CharacterTestCharacterControlOwner.LocalPlayer);
            CharacterTestCharacterEntity target = SpawnPerceptionTestEntity(
                module, teamId: 2, x: 0f, z: 3f,
                CharacterTestCharacterControlOwner.None);
            CharacterTestCharacterEntity alternate = SpawnPerceptionTestEntity(
                module, teamId: 2, x: 3f, z: 3f,
                CharacterTestCharacterControlOwner.None);
            var commands = new CharacterTestRuntimeCommandModule(module, NullRuntimeLogger.Instance);
            try
            {
                module.Tick(new RuntimeTickContext(0, 1d / 60d, 0d, RuntimeTickStage.Simulation));
                Assert.IsTrue(module.TryGetDamageGeometrySnapshot(target.Id.Value, out var geometry),
                    "Fixture requires published Hurtboxes, not a root collider fallback.");
                Assert.IsTrue(geometry.IsValid);
                Assert.Greater(geometry.ColliderCount, 1,
                    "Fixture must expose authored compound Hurtboxes, not a compatibility Aabb.");
                var frame = new RuntimeFrame(1);
                CharacterTestPerceptionFrameCache cache = module.PerceptionFrameCache;
                Assert.IsTrue(cache.BeginCommittedConsumerPhase(frame, module, module.PhysicsRuntime));
                Assert.IsTrue(cache.TryAcquireNearestHurtbox(frame, source, target, out var baseline, out var sample));
                Assert.IsTrue(sample.HasDistance);
                Assert.AreEqual(source.Id.Value, baseline.SourceEntityId);

                RuntimeCommand request = explicitSet
                    ? CharacterTestRuntimeCommandFactory.SetTargetLock(
                        frame, CharacterTestRuntimeCommandSources.SkillPanel, true, target.Id.Value)
                    : CharacterTestRuntimeCommandFactory.ToggleTargetLock(
                        frame, CharacterTestRuntimeCommandSources.CameraInput, source.Id.Value);
                RuntimeCommandValidationResult queued = commands.Enqueue(request);
                Assert.IsTrue(queued.Success, queued.Error.Message);
                TickCommands(commands, frame);

                Assert.IsEmpty(commands.LastCommandErrors);
                Assert.IsTrue(commands.TargetLockEnabled);
                Assert.AreEqual(target.Id.Value, commands.LockedTargetEntityId);
                CharacterTestCombatTargetHandle handle = commands.LockedCombatTarget;
                Assert.IsTrue(handle.HasTarget);
                Assert.AreEqual(source.Id.Value, handle.SourceEntityId);
                Assert.AreEqual(target.Id.Value, handle.TargetEntityId);
                Assert.IsTrue(cache.TryGetLockedHurtboxDistance(frame, handle, out var lockedDistance));
                Assert.IsTrue(lockedDistance.HasDistance);

                var switchFrame = new RuntimeFrame(2);
                Assert.IsTrue(commands.Enqueue(CharacterTestRuntimeCommandFactory.ToggleTargetLockViewSwitch(
                    switchFrame, CharacterTestRuntimeCommandSources.CameraInput,
                    source.Id.Value, alternate.Id.Value, Vector2.right)).Success);
                TickCommands(commands, switchFrame);
                Assert.IsEmpty(commands.LastCommandErrors);
                Assert.AreEqual(alternate.Id.Value, commands.LockedTargetEntityId);
                CharacterTestCombatTargetHandle switched = commands.LockedCombatTarget;
                Assert.IsTrue(switched.HasTarget);
                Assert.AreEqual(source.Id.Value, switched.SourceEntityId);
                Assert.AreEqual(alternate.Id.Value, switched.TargetEntityId);
                Assert.IsTrue(cache.TryGetLockedHurtboxDistance(switchFrame, switched, out var switchedDistance));
                Assert.IsTrue(switchedDistance.HasDistance);

                var rejectFrame = new RuntimeFrame(3);
                Assert.IsTrue(commands.Enqueue(CharacterTestRuntimeCommandFactory.ToggleTargetLockViewSwitch(
                    rejectFrame, CharacterTestRuntimeCommandSources.CameraInput,
                    source.Id.Value, alternate.Id.Value, Vector2.right)).Success);
                TickCommands(commands, rejectFrame);
                Assert.AreEqual(1, commands.LastCommandErrors.Count);
                Assert.IsTrue(commands.TargetLockEnabled);
                Assert.AreEqual(alternate.Id.Value, commands.LockedTargetEntityId);
                Assert.IsTrue(switched.Equals(commands.LockedCombatTarget));

                var disableFrame = new RuntimeFrame(4);
                RuntimeCommand disable = explicitSet
                    ? CharacterTestRuntimeCommandFactory.SetTargetLock(
                        disableFrame, CharacterTestRuntimeCommandSources.SkillPanel, false, alternate.Id.Value)
                    : CharacterTestRuntimeCommandFactory.ToggleTargetLock(
                        disableFrame, CharacterTestRuntimeCommandSources.CameraInput, source.Id.Value);
                Assert.IsTrue(commands.Enqueue(disable).Success);
                TickCommands(commands, disableFrame);
                Assert.IsEmpty(commands.LastCommandErrors);
                Assert.IsFalse(commands.TargetLockEnabled);
                Assert.AreEqual(0, commands.LockedTargetEntityId);
                Assert.IsFalse(commands.LockedCombatTarget.HasTarget);

                var relockFrame = new RuntimeFrame(5);
                Assert.IsTrue(commands.Enqueue(CharacterTestRuntimeCommandFactory.SetTargetLock(
                    relockFrame, CharacterTestRuntimeCommandSources.System, true, alternate.Id.Value)).Success);
                TickCommands(commands, relockFrame);
                Assert.IsEmpty(commands.LastCommandErrors);
                Assert.AreEqual(source.Id.Value, commands.LockedCombatTarget.SourceEntityId);
                SetTargetDead(module, alternate, new RuntimeFrame(6));
                TickCommands(commands, new RuntimeFrame(6));
                Assert.IsEmpty(commands.LastCommandErrors);
                Assert.IsFalse(commands.TargetLockEnabled);
                Assert.AreEqual(0, commands.LockedTargetEntityId);
                Assert.IsFalse(commands.LockedCombatTarget.HasTarget);
            }
            finally
            {
                commands.Dispose();
                module.Dispose();
            }
        }

        [Test]
        public void PerceptionFrameCache_TargetGeometry_ReusesWithinFrameAndRefreshesNextFrame()
        {
            var module = new CharacterTestCharacterRuntimeModule(
                null,
                NullRuntimeLogger.Instance,
                createViews: false);
            CharacterTestCharacterEntity source = SpawnPerceptionTestEntity(
                module,
                teamId: 1,
                x: 0f,
                z: 0f,
                CharacterTestCharacterControlOwner.LocalPlayer);
            CharacterTestCharacterEntity target = SpawnPerceptionTestEntity(
                module,
                teamId: 2,
                x: 3f,
                z: 4f,
                CharacterTestCharacterControlOwner.None);
            CharacterTestPerceptionFrameCache cache = module.PerceptionFrameCache;

            var frame = new RuntimeFrame(10);
            cache.BeginFrame(frame, module, module.PhysicsRuntime);

            Assert.IsTrue(cache.TryGetTargetGeometry(
                frame,
                source,
                target,
                out CharacterTestTargetGeometrySample first));
            Assert.IsTrue(cache.TryGetTargetGeometry(
                frame,
                source,
                target,
                out CharacterTestTargetGeometrySample second));

            Assert.AreEqual(1, cache.TargetGeometryBuildCount);
            Assert.AreEqual(25f, first.SqrDistanceMeters, 0.0001f);
            Assert.AreEqual(first.DistanceMeters, second.DistanceMeters, 0.0001f);

            target.SetTransform(new CharacterTestCharacterTransformComponent(6f, 0f, 8f));
            Assert.IsTrue(cache.TryGetTargetGeometry(
                frame,
                source,
                target,
                out CharacterTestTargetGeometrySample sameFrame));
            Assert.AreEqual(5f, sameFrame.DistanceMeters, 0.0001f);

            var nextFrame = new RuntimeFrame(11);
            cache.BeginFrame(nextFrame, module, module.PhysicsRuntime);
            Assert.IsTrue(cache.TryGetTargetGeometry(
                nextFrame,
                source,
                target,
                out CharacterTestTargetGeometrySample refreshed));
            Assert.AreEqual(10f, refreshed.DistanceMeters, 0.0001f);
            Assert.AreEqual(1, cache.TargetGeometryBuildCount);
        }

        [Test]
        public void PerceptionFrameCache_LockedHurtboxLookupFindsMiddleAndTailNumericIds()
        {
            CharacterTestCharacterDefinition definition = GetIronVanguardDefinition();
            CharacterImportedPackage package =
                CharacterTestGeneratedPackageSource.LoadPackage(definition.GeneratedPackageRoot);
            CharacterBodyColliderRuntimeBinding[] colliders = package.Geometry.BodyColliders;
            Assert.GreaterOrEqual(colliders.Length, 3);
            CharacterBodyColliderRuntimeBinding[] originals =
                new[] { colliders[0], colliders[1], colliders[2] };
            try
            {
                colliders[0] = CloneColliderWithId(originals[0], CharacterTestAuthoredContentIds.D400_Id57);
                colliders[1] = CloneColliderWithId(originals[1], CharacterTestAuthoredContentIds.D400_Id103);
                colliders[2] = CloneColliderWithId(originals[2], CharacterTestAuthoredContentIds.D400_Id70);

                var module = new CharacterTestCharacterRuntimeModule(
                    null,
                    NullRuntimeLogger.Instance,
                    definitions: new CharacterTestCharacterDefinitionRegistry(new[] { definition }),
                    createViews: false);
                CharacterTestCharacterEntity source = SpawnPerceptionTestEntity(
                    module,
                    teamId: 1,
                    x: 0f,
                    z: 0f,
                    CharacterTestCharacterControlOwner.LocalPlayer);
                CharacterTestCharacterEntity target = SpawnPerceptionTestEntity(
                    module,
                    teamId: 2,
                    x: 0.95f,
                    z: 0f,
                    CharacterTestCharacterControlOwner.None);
                module.Tick(new RuntimeTickContext(0, 1d / 60d, 0d, RuntimeTickStage.Simulation));
                RuntimeFrame frame = new RuntimeFrame(12);
                CharacterTestPerceptionFrameCache cache = module.PerceptionFrameCache;
                cache.BeginPostMotionGeometryFrame(frame, module, module.PhysicsRuntime);
                Assert.IsTrue(module.TryGetDamageGeometrySnapshot(
                    target.Id.Value,
                    out CharacterTestDamageGeometrySnapshot geometry));

                int armIndex = FindColliderIndex(geometry, CharacterTestAuthoredContentIds.D400_Id57);
                int tailIndex = FindColliderIndex(geometry, CharacterTestAuthoredContentIds.D400_Id103);
                int torsoIndex = FindColliderIndex(geometry, CharacterTestAuthoredContentIds.D400_Id70);
                Assert.Less(armIndex, tailIndex);
                Assert.Less(tailIndex, torsoIndex);

                int[] ids =
                {
                    CharacterTestAuthoredContentIds.D400_Id57,
                    CharacterTestAuthoredContentIds.D400_Id103,
                    CharacterTestAuthoredContentIds.D400_Id70
                };
                for (int i = 0; i < ids.Length; i++)
                {
                    CharacterTestDamageGeometryColliderMetadata collider = GetCollider(geometry, ids[i]);
                    var handle = new CharacterTestCombatTargetHandle(
                        source.Id.Value,
                        target.Id.Value,
                        target.GameplayEntityId,
                        module.PhysicsRuntime.GetEntityId(target),
                        0,
                        collider.HitZoneId,
                        collider.LogicalColliderId,
                        geometry.HurtboxRevision,
                        frame,
                        string.Empty,
                        collider.HitZoneLabel,
                        collider.LogicalColliderLabel);

                    Assert.IsTrue(cache.TryGetLockedHurtboxDistance(
                        frame,
                        handle,
                        out CharacterTestLockedHurtboxDistanceSample sample),
                        "Production cache must resolve numeric collider " + ids[i]);
                    Assert.IsTrue(sample.HasDistance);
                    Assert.AreEqual(collider.CombatColliderId, sample.TargetCombatColliderId);
                }
            }
            finally
            {
                colliders[0] = originals[0];
                colliders[1] = originals[1];
                colliders[2] = originals[2];
            }
        }

        private static CharacterBodyColliderRuntimeBinding CloneColliderWithId(
            CharacterBodyColliderRuntimeBinding source,
            int colliderId)
        {
            return new CharacterBodyColliderRuntimeBinding(
                colliderId,
                source.PartId,
                source.HitZoneId,
                source.Shape,
                source.LocalPose,
                source.Size,
                source.Radius,
                source.Height,
                source.Priority,
                source.IsWeakPoint,
                source.PhysicsLayer,
                source.DamageMultiplierOverride,
                source.ImpactPowerScaleOverride,
                source.MaterialStableId,
                source.PartLabel,
                source.HitZoneLabel,
                source.ColliderLabel);
        }

        private static int FindColliderIndex(
            CharacterTestDamageGeometrySnapshot geometry,
            int logicalColliderId)
        {
            for (int i = 0; i < geometry.ColliderCount; i++)
            {
                if (geometry.GetCollider(i).LogicalColliderId == logicalColliderId)
                    return i;
            }

            Assert.Fail("Missing numeric collider " + logicalColliderId);
            return -1;
        }

        private static CharacterTestDamageGeometryColliderMetadata GetCollider(
            CharacterTestDamageGeometrySnapshot geometry,
            int logicalColliderId)
        {
            Assert.IsTrue(geometry.TryGetCollider(logicalColliderId, out CharacterTestDamageGeometryColliderMetadata collider));
            return collider;
        }

        [Test]
        public void PerceptionFrameCache_CompletedGeometry_ReusesPairAcrossCombatPlannerAndCommandPhases()
        {
            var module = new CharacterTestCharacterRuntimeModule(
                null,
                NullRuntimeLogger.Instance,
                createViews: false);
            CharacterTestCharacterEntity source = SpawnPerceptionTestEntity(
                module,
                teamId: 1,
                x: 0f,
                z: 0f,
                CharacterTestCharacterControlOwner.LocalPlayer);
            CharacterTestCharacterEntity target = SpawnPerceptionTestEntity(
                module,
                teamId: 2,
                x: 3f,
                z: 4f,
                CharacterTestCharacterControlOwner.None);
            CharacterTestPerceptionFrameCache cache = module.PerceptionFrameCache;
            var completedFrame = new RuntimeFrame(10);

            cache.BeginPostMotionGeometryFrame(completedFrame, module, module.PhysicsRuntime);
            Assert.IsTrue(cache.TryGetTargetGeometry(completedFrame, source, target, out CharacterTestTargetGeometrySample combat));
            Assert.AreEqual(completedFrame, combat.EvaluationFrame);
            Assert.AreEqual(completedFrame, combat.GeometryFrame);
            Assert.AreEqual(1, cache.CompletedPairBuildCount);
            cache.CommitCompletedGeometryFrame(completedFrame);

            target.SetTransform(new CharacterTestCharacterTransformComponent(6f, 0f, 8f));
            var evaluationFrame = new RuntimeFrame(11);
            Assert.IsTrue(cache.BeginCommittedConsumerPhase(evaluationFrame, module, module.PhysicsRuntime));
            Assert.IsTrue(cache.TryGetTargetGeometry(evaluationFrame, source, target, out CharacterTestTargetGeometrySample planner));
            Assert.AreEqual(evaluationFrame, planner.EvaluationFrame);
            Assert.AreEqual(completedFrame, planner.GeometryFrame);
            Assert.AreEqual(5f, planner.DistanceMeters, 0.0001f);
            Assert.AreEqual(1, cache.CompletedPairBuildCount);

            Assert.IsTrue(cache.BeginCommittedConsumerPhase(evaluationFrame, module, module.PhysicsRuntime));
            Assert.IsTrue(cache.TryGetTargetGeometry(evaluationFrame, source, target, out CharacterTestTargetGeometrySample commands));
            Assert.AreEqual(5f, commands.DistanceMeters, 0.0001f);
            Assert.AreEqual(1, cache.CompletedPairBuildCount);

            cache.BeginPostMotionGeometryFrame(evaluationFrame, module, module.PhysicsRuntime);
            Assert.IsTrue(cache.TryGetTargetGeometry(evaluationFrame, source, target, out CharacterTestTargetGeometrySample nextCombat));
            Assert.AreEqual(evaluationFrame, nextCombat.GeometryFrame);
            Assert.AreEqual(10f, nextCombat.DistanceMeters, 0.0001f);
            Assert.AreEqual(1, cache.CompletedPairBuildCount);
        }

        [Test]
        public void PerceptionFrameCache_CompletedGeometry_SharesUndirectedDistanceAndKeepsDirectedProjection()
        {
            var module = new CharacterTestCharacterRuntimeModule(
                null,
                NullRuntimeLogger.Instance,
                createViews: false);
            CharacterTestCharacterEntity a = SpawnPerceptionTestEntity(
                module,
                teamId: 1,
                x: 0f,
                z: 0f,
                CharacterTestCharacterControlOwner.LocalPlayer);
            CharacterTestCharacterEntity b = SpawnPerceptionTestEntity(
                module,
                teamId: 2,
                x: 3f,
                z: 4f,
                CharacterTestCharacterControlOwner.None);
            b.SetTransform(CharacterTestCharacterTransformComponent.FromDeterministic(
                b.Transform.XMillimeters,
                12000,
                b.Transform.ZMillimeters,
                b.Transform.YawMilliDegrees));
            var frame = new RuntimeFrame(20);
            CharacterTestPerceptionFrameCache cache = module.PerceptionFrameCache;
            cache.BeginPostMotionGeometryFrame(frame, module, module.PhysicsRuntime);

            Assert.IsTrue(cache.TryGetTargetGeometry(frame, a, b, out CharacterTestTargetGeometrySample forward));
            Assert.IsTrue(cache.TryGetTargetGeometry(frame, b, a, out CharacterTestTargetGeometrySample reverse));

            Assert.AreEqual(1, cache.CompletedPairBuildCount);
            Assert.AreEqual(2, cache.CompletedDirectedProjectionBuildCount);
            Assert.AreEqual(forward.SqrDistanceMeters, reverse.SqrDistanceMeters, 0.0001f);
            Assert.AreEqual(forward.DistanceMeters, reverse.DistanceMeters, 0.0001f);
            Assert.AreEqual(-forward.DeltaXMillimeters, reverse.DeltaXMillimeters);
            Assert.AreEqual(-forward.DeltaZMillimeters, reverse.DeltaZMillimeters);
            Assert.AreEqual(25000000L, forward.SqrPlanarDistanceMillimeters);
            Assert.AreEqual(169000000L, forward.SqrSpatialDistanceMillimeters);
        }

        [Test]
        public void PerceptionFrameCache_CompletedGeometry_ZeroDistanceIsAvailable()
        {
            var module = new CharacterTestCharacterRuntimeModule(
                null,
                NullRuntimeLogger.Instance,
                createViews: false);
            CharacterTestCharacterEntity source = SpawnPerceptionTestEntity(
                module,
                teamId: 1,
                x: 2f,
                z: -3f,
                CharacterTestCharacterControlOwner.LocalPlayer);
            CharacterTestCharacterEntity target = SpawnPerceptionTestEntity(
                module,
                teamId: 2,
                x: 2f,
                z: -3f,
                CharacterTestCharacterControlOwner.None);
            var frame = new RuntimeFrame(30);
            CharacterTestPerceptionFrameCache cache = module.PerceptionFrameCache;
            cache.BeginPostMotionGeometryFrame(frame, module, module.PhysicsRuntime);

            Assert.IsTrue(cache.TryGetTargetGeometry(frame, source, target, out CharacterTestTargetGeometrySample sample));
            Assert.AreEqual(0L, sample.SqrPlanarDistanceMillimeters);
            Assert.AreEqual(0f, sample.SqrDistanceMeters);
            Assert.AreEqual(0f, sample.DistanceMeters);
            Assert.AreEqual(1, cache.CompletedPairBuildCount);
        }

        [Test]
        public void PerceptionFrameCache_CompletedGeometry_PostMotionMutationRecapturesBeforeCommit()
        {
            var module = new CharacterTestCharacterRuntimeModule(
                null,
                NullRuntimeLogger.Instance,
                createViews: false);
            CharacterTestCharacterEntity source = SpawnPerceptionTestEntity(
                module,
                teamId: 1,
                x: 0f,
                z: 0f,
                CharacterTestCharacterControlOwner.LocalPlayer);
            CharacterTestCharacterEntity target = SpawnPerceptionTestEntity(
                module,
                teamId: 2,
                x: 3f,
                z: 4f,
                CharacterTestCharacterControlOwner.None);
            var frame = new RuntimeFrame(40);
            CharacterTestPerceptionFrameCache cache = module.PerceptionFrameCache;
            cache.BeginPostMotionGeometryFrame(frame, module, module.PhysicsRuntime);
            Assert.IsTrue(cache.TryGetTargetGeometry(frame, source, target, out _));
            Assert.AreEqual(1, cache.CompletedPairBuildCount);

            target.SetTransform(new CharacterTestCharacterTransformComponent(6f, 0f, 8f));
            cache.CommitCompletedGeometryFrame(frame);

            Assert.AreEqual(1, cache.CompletedPostMotionMutationCount);
            Assert.AreEqual(0, cache.CompletedPairBuildCount);
            Assert.IsTrue(cache.BeginCommittedConsumerPhase(new RuntimeFrame(41), module, module.PhysicsRuntime));
            Assert.IsTrue(cache.TryGetTargetGeometry(new RuntimeFrame(41), source, target, out CharacterTestTargetGeometrySample refreshed));
            Assert.AreEqual(10f, refreshed.DistanceMeters, 0.0001f);
            Assert.AreEqual(1, cache.CompletedPairBuildCount);
        }

        [Test]
        public void PerceptionFrameCache_CompletedGeometry_RestoreSameFrameUsesNewGenerationAndRestoredPositions()
        {
            var module = new CharacterTestCharacterRuntimeModule(
                null,
                NullRuntimeLogger.Instance,
                createViews: false);
            CharacterTestCharacterEntity source = SpawnPerceptionTestEntity(
                module,
                teamId: 1,
                x: 0f,
                z: 0f,
                CharacterTestCharacterControlOwner.LocalPlayer);
            CharacterTestCharacterEntity target = SpawnPerceptionTestEntity(
                module,
                teamId: 2,
                x: 3f,
                z: 4f,
                CharacterTestCharacterControlOwner.None);
            var frame = new RuntimeFrame(50);
            CharacterTestPerceptionFrameCache cache = module.PerceptionFrameCache;
            cache.BeginPostMotionGeometryFrame(frame, module, module.PhysicsRuntime);
            Assert.IsTrue(cache.TryGetTargetGeometry(frame, source, target, out CharacterTestTargetGeometrySample original));
            cache.CommitCompletedGeometryFrame(frame);
            long originalGeneration = original.GeometryGeneration;

            cache.ResetForAuthorityRestore(frame);
            target.SetTransform(new CharacterTestCharacterTransformComponent(6f, 0f, 8f));
            Assert.IsTrue(cache.BeginCommittedConsumerPhase(new RuntimeFrame(51), module, module.PhysicsRuntime));
            Assert.IsTrue(cache.TryGetTargetGeometry(new RuntimeFrame(51), source, target, out CharacterTestTargetGeometrySample restored));

            Assert.AreEqual(frame, restored.GeometryFrame);
            Assert.AreNotEqual(originalGeneration, restored.GeometryGeneration);
            Assert.AreEqual(10f, restored.DistanceMeters, 0.0001f);
        }

        [Test]
        public void PerceptionFrameCache_CompletedGeometry_WarmedDoubleBufferAllocatesZeroBytes()
        {
            var module = new CharacterTestCharacterRuntimeModule(
                null,
                NullRuntimeLogger.Instance,
                createViews: false);
            CharacterTestCharacterEntity source = SpawnPerceptionTestEntity(
                module,
                teamId: 1,
                x: 0f,
                z: 0f,
                CharacterTestCharacterControlOwner.LocalPlayer);
            CharacterTestCharacterEntity target = SpawnPerceptionTestEntity(
                module,
                teamId: 2,
                x: 3f,
                z: 4f,
                CharacterTestCharacterControlOwner.None);
            CharacterTestPerceptionFrameCache cache = module.PerceptionFrameCache;

            for (int frameValue = 1; frameValue <= 4; frameValue++)
            {
                var frame = new RuntimeFrame(frameValue);
                cache.BeginPostMotionGeometryFrame(frame, module, module.PhysicsRuntime);
                Assert.IsTrue(cache.TryGetTargetGeometry(frame, source, target, out _));
                cache.CommitCompletedGeometryFrame(frame);
                Assert.IsTrue(cache.BeginCommittedConsumerPhase(
                    new RuntimeFrame(frameValue + 1),
                    module,
                    module.PhysicsRuntime));
                Assert.IsTrue(cache.TryGetTargetGeometry(
                    new RuntimeFrame(frameValue + 1),
                    source,
                    target,
                    out _));
            }

            _ = GC.GetAllocatedBytesForCurrentThread();
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int frameValue = 5; frameValue < 133; frameValue++)
            {
                var frame = new RuntimeFrame(frameValue);
                cache.BeginPostMotionGeometryFrame(frame, module, module.PhysicsRuntime);
                cache.TryGetTargetGeometry(frame, source, target, out _);
                cache.CommitCompletedGeometryFrame(frame);
                var next = new RuntimeFrame(frameValue + 1);
                cache.BeginCommittedConsumerPhase(next, module, module.PhysicsRuntime);
                cache.TryGetTargetGeometry(next, source, target, out _);
            }
            long allocated = GC.GetAllocatedBytesForCurrentThread() - before;

            Assert.AreEqual(0L, allocated);
            Assert.AreEqual(1, cache.CompletedPairBuildCount);
        }

        [Test]
        public void CompletedFrameGeometry_CommitExcludesEntitiesRemovedAfterCombat()
        {
            var module = new CharacterTestCharacterRuntimeModule(
                null,
                NullRuntimeLogger.Instance,
                createViews: false);
            CharacterTestCharacterEntity source = SpawnPerceptionTestEntity(
                module,
                teamId: 1,
                x: 0f,
                z: 0f,
                CharacterTestCharacterControlOwner.LocalPlayer);
            CharacterTestCharacterEntity target = SpawnPerceptionTestEntity(
                module,
                teamId: 2,
                x: 3f,
                z: 4f,
                CharacterTestCharacterControlOwner.None);
            var frame = new RuntimeFrame(70);
            var cache = new CharacterTestCompletedFrameGeometryCache();
            cache.BeginPostMotionGeometryFrame(frame, module.Entities);
            Assert.IsTrue(cache.TryGetPairGeometry(
                source.Id.Value,
                target.Id.Value,
                out _,
                out _,
                out _,
                out _));

            cache.CommitCompletedGeometryFrame(frame, new[] { source });

            Assert.IsFalse(cache.TryGetCommittedPairGeometry(
                source.Id.Value,
                target.Id.Value,
                out _,
                out _));
        }

        [Test]
        public void RuntimeAiPlannerPerceptionCache_NearestOpposingTeamKeepsFirstSquaredDistanceTie()
        {
            var module = new CharacterTestCharacterRuntimeModule(
                null,
                NullRuntimeLogger.Instance,
                createViews: false);
            CharacterTestCharacterEntity ai = SpawnPerceptionTestEntity(
                module,
                teamId: 2,
                x: 0f,
                z: 0f,
                CharacterTestCharacterControlOwner.AiControlled);
            CharacterTestCharacterEntity firstTarget = SpawnPerceptionTestEntity(
                module,
                teamId: 1,
                x: 1f,
                z: 0f,
                CharacterTestCharacterControlOwner.None);
            SpawnPerceptionTestEntity(
                module,
                teamId: 1,
                x: -1f,
                z: 0f,
                CharacterTestCharacterControlOwner.None);
            CharacterTestRuntimeAiPlannerProfile profile =
                CharacterTestRuntimeAiPlannerProfileSource.CreateDefault();
            CharacterTestRuntimeAiPlannerCompiledProfile.TryCompile(
                profile,
                out CharacterTestRuntimeAiPlannerCompiledProfile compiled,
                out CharacterTestRuntimeAiPlannerValidationReport report);
            Assert.NotNull(compiled, report.ToText());

            var frame = new RuntimeFrame(12);
            module.PerceptionFrameCache.BeginFrame(frame, module, module.PhysicsRuntime);
            var evaluator = new CharacterTestRuntimeAiPlannerEvaluator(
                compiled,
                CharacterTestDirectAiNavigationService.Instance,
                CharacterTestRuntimeAiPlannerWorldPerceptionSource.Empty,
                module.PerceptionFrameCache);

            Assert.IsTrue(evaluator.TryEvaluate(
                ai,
                module.Entities,
                frame,
                out CharacterTestRuntimeAiPlannerDecision decision));

            Assert.AreEqual(firstTarget.Id.Value, decision.TargetEntityId);
            Assert.AreEqual(1f, decision.TargetDistance, 0.0001f);
            Assert.IsTrue(decision.HasScenePreviewGeometry);
            Assert.AreEqual(0f, decision.ScenePreviewSourceCenterX, 0.0001f);
            Assert.AreEqual(1f, decision.ScenePreviewTargetCenterX, 0.0001f);
            Assert.AreEqual(1f, decision.ScenePreviewTargetDirectionX, 0.0001f);
            Assert.AreEqual(0f, decision.ScenePreviewTargetDirectionZ, 0.0001f);
            Assert.AreEqual(profile.AttackRange, decision.ScenePreviewAttackRange, 0.0001f);

            CharacterTestRuntimeAiPlannerSnapshot snapshot = evaluator.CreateSnapshot("ok");
            Assert.AreEqual(1, snapshot.Agents.Count);
            CharacterTestRuntimeAiPlannerAgentSnapshot agentSnapshot = snapshot.Agents[0];
            Assert.IsTrue(agentSnapshot.HasScenePreviewGeometry);
            Assert.AreEqual(1f, agentSnapshot.ScenePreviewTargetCenterX, 0.0001f);
            Assert.AreEqual(decision.Move.x, agentSnapshot.ScenePreviewDecisionMoveX, 0.0001f);
        }

        [Test]
        public void RuntimeCommandModule_TargetLock_SelectsNearestAliveHostileWithinRange()
        {
            var module = new CharacterTestCharacterRuntimeModule(
                null,
                NullRuntimeLogger.Instance,
                createViews: false);
            CharacterTestCharacterEntity primary = SpawnPerceptionTestEntity(
                module,
                teamId: 1,
                x: 0f,
                z: 0f,
                CharacterTestCharacterControlOwner.LocalPlayer);
            SpawnPerceptionTestEntity(
                module,
                teamId: 1,
                x: 1f,
                z: 0f,
                CharacterTestCharacterControlOwner.None);
            CharacterTestCharacterEntity deadHostile = SpawnPerceptionTestEntity(
                module,
                teamId: 2,
                x: 2f,
                z: 0f,
                CharacterTestCharacterControlOwner.None);
            CharacterTestCharacterEntity aliveHostile = SpawnPerceptionTestEntity(
                module,
                teamId: 2,
                x: 3f,
                z: 0f,
                CharacterTestCharacterControlOwner.None);
            SetTargetDead(module, deadHostile, new RuntimeFrame(12));
            var commands = new CharacterTestRuntimeCommandModule(module, NullRuntimeLogger.Instance);
            try
            {
                commands.SetTargetLockMaxDistance(4f);
                var frame = new RuntimeFrame(13);
                RuntimeCommandValidationResult enqueue = commands.Enqueue(
                    CharacterTestRuntimeCommandFactory.ToggleTargetLock(
                        frame,
                        CharacterTestRuntimeCommandSources.CameraInput,
                        primary.Id.Value,
                        traceId: new RuntimeTraceIdentity(932021)));
                Assert.IsTrue(enqueue.Success, enqueue.Error.Message);

                TickCommands(commands, frame);

                Assert.IsTrue(commands.TargetLockEnabled);
                Assert.AreEqual(aliveHostile.Id.Value, commands.LockedTargetEntityId);
                AssertRecentEvent(
                    commands,
                    CharacterTestRuntimeEventType.TargetLockChanged,
                    CharacterTestRuntimeCommandIds.ToggleTargetLock);
            }
            finally
            {
                commands.Dispose();
            }
        }

        [Test]
        public void RuntimeCommandModule_TargetLock_RejectsWhenNoAliveHostileInRange()
        {
            var module = new CharacterTestCharacterRuntimeModule(
                null,
                NullRuntimeLogger.Instance,
                createViews: false);
            CharacterTestCharacterEntity primary = SpawnPerceptionTestEntity(
                module,
                teamId: 1,
                x: 0f,
                z: 0f,
                CharacterTestCharacterControlOwner.LocalPlayer);
            SpawnPerceptionTestEntity(
                module,
                teamId: 1,
                x: 1f,
                z: 0f,
                CharacterTestCharacterControlOwner.None);
            CharacterTestCharacterEntity deadHostile = SpawnPerceptionTestEntity(
                module,
                teamId: 2,
                x: 2f,
                z: 0f,
                CharacterTestCharacterControlOwner.None);
            SpawnPerceptionTestEntity(
                module,
                teamId: 2,
                x: 5f,
                z: 0f,
                CharacterTestCharacterControlOwner.None);
            SetTargetDead(module, deadHostile, new RuntimeFrame(20));

            var commands = new CharacterTestRuntimeCommandModule(module, NullRuntimeLogger.Instance);
            try
            {
                commands.SetTargetLockMaxDistance(2.5f);
                var frame = new RuntimeFrame(21);
                RuntimeCommandValidationResult enqueue = commands.Enqueue(
                    CharacterTestRuntimeCommandFactory.ToggleTargetLock(
                        frame,
                        CharacterTestRuntimeCommandSources.CameraInput,
                        primary.Id.Value,
                        traceId: new RuntimeTraceIdentity(956742)));
                Assert.IsTrue(enqueue.Success, enqueue.Error.Message);

                TickCommands(commands, frame);

                Assert.IsFalse(commands.TargetLockEnabled);
                Assert.AreEqual(0, commands.LockedTargetEntityId);
                AssertRecentEvent(
                    commands,
                    CharacterTestRuntimeEventType.TargetLockRejected,
                    CharacterTestRuntimeCommandIds.ToggleTargetLock);
            }
            finally
            {
                commands.Dispose();
            }
        }

        [Test]
        public void RuntimeCommandModule_SetTargetLock_ValidatesHostileAliveAndRange()
        {
            var module = new CharacterTestCharacterRuntimeModule(
                null,
                NullRuntimeLogger.Instance,
                createViews: false);
            SpawnPerceptionTestEntity(
                module,
                teamId: 1,
                x: 0f,
                z: 0f,
                CharacterTestCharacterControlOwner.LocalPlayer);
            CharacterTestCharacterEntity friendly = SpawnPerceptionTestEntity(
                module,
                teamId: 1,
                x: 1f,
                z: 0f,
                CharacterTestCharacterControlOwner.None);
            CharacterTestCharacterEntity deadHostile = SpawnPerceptionTestEntity(
                module,
                teamId: 2,
                x: 1.5f,
                z: 0f,
                CharacterTestCharacterControlOwner.None);
            CharacterTestCharacterEntity farHostile = SpawnPerceptionTestEntity(
                module,
                teamId: 2,
                x: 4f,
                z: 0f,
                CharacterTestCharacterControlOwner.None);
            CharacterTestCharacterEntity validHostile = SpawnPerceptionTestEntity(
                module,
                teamId: 2,
                x: 2f,
                z: 0f,
                CharacterTestCharacterControlOwner.None);
            SetTargetDead(module, deadHostile, new RuntimeFrame(30));

            var commands = new CharacterTestRuntimeCommandModule(module, NullRuntimeLogger.Instance);
            try
            {
                commands.SetTargetLockMaxDistance(2.5f);

                AssertSetTargetLockRejected(commands, friendly.Id.Value, new RuntimeFrame(31));
                AssertSetTargetLockRejected(commands, deadHostile.Id.Value, new RuntimeFrame(32));
                AssertSetTargetLockRejected(commands, farHostile.Id.Value, new RuntimeFrame(33));

                RuntimeCommandValidationResult accepted = commands.Enqueue(
                    CharacterTestRuntimeCommandFactory.SetTargetLock(
                        new RuntimeFrame(34),
                        CharacterTestRuntimeCommandSources.System,
                        enabled: true,
                        validHostile.Id.Value,
                        traceId: new RuntimeTraceIdentity(995173)));
                Assert.IsTrue(accepted.Success, accepted.Error.Message);
                TickCommands(commands, new RuntimeFrame(34));

                Assert.IsTrue(commands.TargetLockEnabled);
                Assert.AreEqual(validHostile.Id.Value, commands.LockedTargetEntityId);
                AssertRecentEvent(
                    commands,
                    CharacterTestRuntimeEventType.TargetLockChanged,
                    CharacterTestRuntimeCommandIds.SetTargetLock);
            }
            finally
            {
                commands.Dispose();
            }
        }

        [Test]
        public void RuntimeCommandModule_TargetLock_LegacyToggleUnlocksWhenAlreadyLocked()
        {
            var module = new CharacterTestCharacterRuntimeModule(
                null,
                NullRuntimeLogger.Instance,
                createViews: false);
            CharacterTestCharacterEntity primary = SpawnPerceptionTestEntity(
                module,
                teamId: 1,
                x: 0f,
                z: 0f,
                CharacterTestCharacterControlOwner.LocalPlayer);
            CharacterTestCharacterEntity currentTarget = SpawnPerceptionTestEntity(
                module,
                teamId: 2,
                x: 1f,
                z: 0f,
                CharacterTestCharacterControlOwner.None);

            var commands = new CharacterTestRuntimeCommandModule(module, NullRuntimeLogger.Instance);
            try
            {
                commands.SetTargetLockMaxDistance(4f);
                AssertSetTargetLockAccepted(commands, currentTarget.Id.Value, new RuntimeFrame(60));

                RuntimeCommandValidationResult accepted = commands.Enqueue(
                    CharacterTestRuntimeCommandFactory.ToggleTargetLock(
                        new RuntimeFrame(61),
                        CharacterTestRuntimeCommandSources.CameraInput,
                        primary.Id.Value,
                        traceId: new RuntimeTraceIdentity(981334)));
                Assert.IsTrue(accepted.Success, accepted.Error.Message);
                TickCommands(commands, new RuntimeFrame(61));

                Assert.IsFalse(commands.TargetLockEnabled);
                Assert.AreEqual(0, commands.LockedTargetEntityId);
                AssertRecentEvent(
                    commands,
                    CharacterTestRuntimeEventType.TargetLockChanged,
                    CharacterTestRuntimeCommandIds.ToggleTargetLock);
            }
            finally
            {
                commands.Dispose();
            }
        }

        [Test]
        public void RuntimeCommandModule_TargetLock_ViewSwitchValidatesRequestedTargetAndKeepsCurrentOnReject()
        {
            var module = new CharacterTestCharacterRuntimeModule(
                null,
                NullRuntimeLogger.Instance,
                createViews: false);
            CharacterTestCharacterEntity primary = SpawnPerceptionTestEntity(
                module,
                teamId: 1,
                x: 0f,
                z: 0f,
                CharacterTestCharacterControlOwner.LocalPlayer);
            CharacterTestCharacterEntity currentTarget = SpawnPerceptionTestEntity(
                module,
                teamId: 2,
                x: 1f,
                z: 0f,
                CharacterTestCharacterControlOwner.None);
            CharacterTestCharacterEntity validTarget = SpawnPerceptionTestEntity(
                module,
                teamId: 2,
                x: 2f,
                z: 0f,
                CharacterTestCharacterControlOwner.None);
            CharacterTestCharacterEntity friendly = SpawnPerceptionTestEntity(
                module,
                teamId: 1,
                x: 1f,
                z: 1f,
                CharacterTestCharacterControlOwner.None);
            CharacterTestCharacterEntity deadHostile = SpawnPerceptionTestEntity(
                module,
                teamId: 2,
                x: 1.5f,
                z: 1f,
                CharacterTestCharacterControlOwner.None);
            CharacterTestCharacterEntity farHostile = SpawnPerceptionTestEntity(
                module,
                teamId: 2,
                x: 5f,
                z: 0f,
                CharacterTestCharacterControlOwner.None);
            SetTargetDead(module, deadHostile, new RuntimeFrame(70));

            var commands = new CharacterTestRuntimeCommandModule(module, NullRuntimeLogger.Instance);
            try
            {
                commands.SetTargetLockMaxDistance(3f);
                AssertSetTargetLockAccepted(commands, currentTarget.Id.Value, new RuntimeFrame(71));

                AssertViewSwitchNoCandidateKeepsLockWithoutError(
                    commands,
                    primary.Id.Value,
                    requestedTargetEntityId: 0,
                    expectedLockedTargetEntityId: currentTarget.Id.Value,
                    new RuntimeFrame(72));
                AssertViewSwitchRejectedKeepsLock(
                    commands,
                    primary.Id.Value,
                    primary.Id.Value,
                    currentTarget.Id.Value,
                    new RuntimeFrame(73));
                AssertViewSwitchRejectedKeepsLock(
                    commands,
                    primary.Id.Value,
                    currentTarget.Id.Value,
                    currentTarget.Id.Value,
                    new RuntimeFrame(74));
                AssertViewSwitchRejectedKeepsLock(
                    commands,
                    primary.Id.Value,
                    friendly.Id.Value,
                    currentTarget.Id.Value,
                    new RuntimeFrame(75));
                AssertViewSwitchRejectedKeepsLock(
                    commands,
                    primary.Id.Value,
                    deadHostile.Id.Value,
                    currentTarget.Id.Value,
                    new RuntimeFrame(76));
                AssertViewSwitchRejectedKeepsLock(
                    commands,
                    primary.Id.Value,
                    farHostile.Id.Value,
                    currentTarget.Id.Value,
                    new RuntimeFrame(77));

                RuntimeCommandValidationResult accepted = commands.Enqueue(
                    CharacterTestRuntimeCommandFactory.ToggleTargetLockViewSwitch(
                        new RuntimeFrame(78),
                        CharacterTestRuntimeCommandSources.CameraInput,
                        primary.Id.Value,
                        validTarget.Id.Value,
                        Vector2.right,
                        traceId: new RuntimeTraceIdentity(932250)));
                Assert.IsTrue(accepted.Success, accepted.Error.Message);
                TickCommands(commands, new RuntimeFrame(78));

                Assert.IsTrue(commands.TargetLockEnabled);
                Assert.AreEqual(validTarget.Id.Value, commands.LockedTargetEntityId);
                AssertRecentEvent(
                    commands,
                    CharacterTestRuntimeEventType.TargetLockChanged,
                    CharacterTestRuntimeCommandIds.ToggleTargetLock);
            }
            finally
            {
                commands.Dispose();
            }
        }

        [Test]
        public void RuntimeCommandModule_TargetLock_StaleViewSwitchAfterTargetDeathClearsLockWithoutReject()
        {
            var module = new CharacterTestCharacterRuntimeModule(
                null,
                NullRuntimeLogger.Instance,
                createViews: false);
            CharacterTestCharacterEntity primary = SpawnPerceptionTestEntity(
                module,
                teamId: 1,
                x: 0f,
                z: 0f,
                CharacterTestCharacterControlOwner.LocalPlayer);
            CharacterTestCharacterEntity currentTarget = SpawnPerceptionTestEntity(
                module,
                teamId: 2,
                x: 1f,
                z: 0f,
                CharacterTestCharacterControlOwner.None);

            var commands = new CharacterTestRuntimeCommandModule(module, NullRuntimeLogger.Instance);
            try
            {
                commands.SetTargetLockMaxDistance(3f);
                AssertSetTargetLockAccepted(commands, currentTarget.Id.Value, new RuntimeFrame(79));
                SetTargetDead(module, currentTarget, new RuntimeFrame(80));

                RuntimeCommandValidationResult accepted = commands.Enqueue(
                    CharacterTestRuntimeCommandFactory.ToggleTargetLockViewSwitch(
                        new RuntimeFrame(80),
                        CharacterTestRuntimeCommandSources.CameraInput,
                        primary.Id.Value,
                        requestedTargetEntityId: 0,
                        Vector2.right,
                        traceId: new RuntimeTraceIdentity(947871)));
                Assert.IsTrue(accepted.Success, accepted.Error.Message);
                TickCommands(commands, new RuntimeFrame(80));

                Assert.IsFalse(commands.TargetLockEnabled);
                Assert.AreEqual(0, commands.LockedTargetEntityId);
                Assert.IsEmpty(commands.LastCommandErrors);
                AssertRecentEvent(
                    commands,
                    CharacterTestRuntimeEventType.TargetLockChanged,
                    CharacterTestRuntimeCommandIds.SetTargetLock);
                AssertNoRecentEvent(
                    commands,
                    CharacterTestRuntimeEventType.TargetLockRejected,
                    CharacterTestRuntimeCommandIds.ToggleTargetLock);
            }
            finally
            {
                commands.Dispose();
            }
        }

        [Test]
        public void RuntimeCommandModule_TargetLock_ViewSwitchIgnoresLineOfSightBlockage()
        {
            CharacterTestCharacterPhysicsRuntime physicsRuntime =
                CreateFrameworkPhysicsRuntime(includeWall: true);
            var module = new CharacterTestCharacterRuntimeModule(
                null,
                NullRuntimeLogger.Instance,
                physicsRuntime: physicsRuntime,
                createViews: false);
            CharacterTestCharacterEntity primary = SpawnPerceptionTestEntity(
                module,
                teamId: 1,
                x: 0f,
                z: 0f,
                CharacterTestCharacterControlOwner.LocalPlayer);
            CharacterTestCharacterEntity currentTarget = SpawnPerceptionTestEntity(
                module,
                teamId: 2,
                x: -1f,
                z: 0f,
                CharacterTestCharacterControlOwner.None);
            CharacterTestCharacterEntity blockedTarget = SpawnPerceptionTestEntity(
                module,
                teamId: 2,
                x: 4f,
                z: 0f,
                CharacterTestCharacterControlOwner.None);

            CharacterTestPerceptionFrameCache cache = module.PerceptionFrameCache;
            var settings = CharacterTestRuntimeAiPlannerPerceptionSettings.CreateDefault();
            var visibilityFrame = new RuntimeFrame(80);
            cache.BeginFrame(visibilityFrame, module, physicsRuntime);
            Assert.IsTrue(cache.TryGetVisibility(
                visibilityFrame,
                primary,
                blockedTarget,
                settings,
                out CharacterTestVisibilitySample visibility));
            Assert.IsTrue(visibility.LineOfSightBlocked);

            var commands = new CharacterTestRuntimeCommandModule(module, NullRuntimeLogger.Instance);
            try
            {
                commands.SetTargetLockMaxDistance(6f);
                AssertSetTargetLockAccepted(commands, currentTarget.Id.Value, new RuntimeFrame(81));

                RuntimeCommandValidationResult accepted = commands.Enqueue(
                    CharacterTestRuntimeCommandFactory.ToggleTargetLockViewSwitch(
                        new RuntimeFrame(82),
                        CharacterTestRuntimeCommandSources.CameraInput,
                        primary.Id.Value,
                        blockedTarget.Id.Value,
                        Vector2.right,
                        traceId: new RuntimeTraceIdentity(941702)));
                Assert.IsTrue(accepted.Success, accepted.Error.Message);
                TickCommands(commands, new RuntimeFrame(82));

                Assert.IsTrue(commands.TargetLockEnabled);
                Assert.AreEqual(blockedTarget.Id.Value, commands.LockedTargetEntityId);
            }
            finally
            {
                commands.Dispose();
            }
        }

        [TestCase("dead")]
        [TestCase("missing")]
        [TestCase("friendly")]
        [TestCase("out_of_range")]
        public void RuntimeCommandModule_TargetLock_PrunesInvalidLockedTarget(string invalidation)
        {
            var module = new CharacterTestCharacterRuntimeModule(
                null,
                NullRuntimeLogger.Instance,
                createViews: false);
            SpawnPerceptionTestEntity(
                module,
                teamId: 1,
                x: 0f,
                z: 0f,
                CharacterTestCharacterControlOwner.LocalPlayer);
            CharacterTestCharacterEntity target = SpawnPerceptionTestEntity(
                module,
                teamId: 2,
                x: 2f,
                z: 0f,
                CharacterTestCharacterControlOwner.None);
            CharacterTestCharacterEntity friendly = null;
            if (string.Equals(invalidation, "friendly", StringComparison.Ordinal))
            {
                friendly = SpawnPerceptionTestEntity(
                    module,
                    teamId: 1,
                    x: 1f,
                    z: 0f,
                    CharacterTestCharacterControlOwner.None);
            }

            var commands = new CharacterTestRuntimeCommandModule(module, NullRuntimeLogger.Instance);
            try
            {
                commands.SetTargetLockMaxDistance(3f);
                if (friendly != null)
                {
                    commands.ApplyNetworkSnapshotState(
                        commands.CameraMode,
                        true,
                        friendly.Id.Value);
                }
                else
                {
                    RuntimeCommandValidationResult accepted = commands.Enqueue(
                        CharacterTestRuntimeCommandFactory.SetTargetLock(
                            new RuntimeFrame(40),
                            CharacterTestRuntimeCommandSources.System,
                            enabled: true,
                            target.Id.Value,
                            traceId: new RuntimeTraceIdentity(934568)));
                    Assert.IsTrue(accepted.Success, accepted.Error.Message);
                    TickCommands(commands, new RuntimeFrame(40));
                }

                Assert.IsTrue(commands.TargetLockEnabled);
                int expectedLockedTarget = friendly != null ? friendly.Id.Value : target.Id.Value;
                Assert.AreEqual(expectedLockedTarget, commands.LockedTargetEntityId);

                switch (invalidation)
                {
                    case "dead":
                        SetTargetDead(module, target, new RuntimeFrame(41));
                        break;
                    case "missing":
                        Assert.IsTrue(module.World.Remove(target.Id));
                        break;
                    case "friendly":
                        break;
                    case "out_of_range":
                        target.SetTransform(new CharacterTestCharacterTransformComponent(4f, 0f, 0f));
                        break;
                    default:
                        Assert.Fail("unknown invalidation");
                        break;
                }

                TickCommands(commands, new RuntimeFrame(42));

                Assert.IsFalse(commands.TargetLockEnabled);
                Assert.AreEqual(0, commands.LockedTargetEntityId);
                AssertRecentEvent(
                    commands,
                    CharacterTestRuntimeEventType.TargetLockChanged,
                    CharacterTestRuntimeCommandIds.SetTargetLock);
            }
            finally
            {
                commands.Dispose();
            }
        }

        [Test]
        public void RuntimeCommandModule_TargetLock_KeepsBlockedLineOfSightTarget()
        {
            CharacterTestCharacterPhysicsRuntime physicsRuntime =
                CreateFrameworkPhysicsRuntime(includeWall: true);
            var module = new CharacterTestCharacterRuntimeModule(
                null,
                NullRuntimeLogger.Instance,
                physicsRuntime: physicsRuntime,
                createViews: false);
            CharacterTestCharacterEntity primary = SpawnPerceptionTestEntity(
                module,
                teamId: 1,
                x: 0f,
                z: 0f,
                CharacterTestCharacterControlOwner.LocalPlayer);
            CharacterTestCharacterEntity target = SpawnPerceptionTestEntity(
                module,
                teamId: 2,
                x: 4f,
                z: 0f,
                CharacterTestCharacterControlOwner.None);
            CharacterTestPerceptionFrameCache cache = module.PerceptionFrameCache;
            var settings = CharacterTestRuntimeAiPlannerPerceptionSettings.CreateDefault();
            var visibilityFrame = new RuntimeFrame(49);
            cache.BeginFrame(visibilityFrame, module, physicsRuntime);
            Assert.IsTrue(cache.TryGetVisibility(
                visibilityFrame,
                primary,
                target,
                settings,
                out CharacterTestVisibilitySample visibility));
            Assert.IsTrue(visibility.LineOfSightBlocked);

            var commands = new CharacterTestRuntimeCommandModule(module, NullRuntimeLogger.Instance);
            try
            {
                commands.SetTargetLockMaxDistance(6f);
                RuntimeCommandValidationResult accepted = commands.Enqueue(
                    CharacterTestRuntimeCommandFactory.SetTargetLock(
                        new RuntimeFrame(50),
                        CharacterTestRuntimeCommandSources.System,
                        enabled: true,
                        target.Id.Value,
                        traceId: new RuntimeTraceIdentity(938845)));
                Assert.IsTrue(accepted.Success, accepted.Error.Message);
                TickCommands(commands, new RuntimeFrame(50));
                Assert.IsTrue(commands.TargetLockEnabled);
                Assert.AreEqual(target.Id.Value, commands.LockedTargetEntityId);

                TickCommands(commands, new RuntimeFrame(51));

                Assert.IsTrue(commands.TargetLockEnabled);
                Assert.AreEqual(target.Id.Value, commands.LockedTargetEntityId);
            }
            finally
            {
                commands.Dispose();
            }
        }

        [Test]
        public void PerceptionFrameCache_PhysicsQueries_ReusedWithinFrame()
        {
            CharacterTestCharacterPhysicsRuntime physicsRuntime =
                CreateFrameworkPhysicsRuntime(includeWall: true);
            var module = new CharacterTestCharacterRuntimeModule(
                null,
                NullRuntimeLogger.Instance,
                physicsRuntime: physicsRuntime,
                createViews: false);
            CharacterTestCharacterEntity source = SpawnPerceptionTestEntity(
                module,
                teamId: 2,
                x: 0f,
                z: 0f,
                CharacterTestCharacterControlOwner.AiControlled);
            CharacterTestCharacterEntity target = SpawnPerceptionTestEntity(
                module,
                teamId: 1,
                x: 4f,
                z: 0f,
                CharacterTestCharacterControlOwner.None);
            CharacterTestPerceptionFrameCache cache = module.PerceptionFrameCache;
            var settings = new CharacterTestRuntimeAiPlannerPerceptionSettings(
                visionRange: 12f,
                targetMemoryFrames: CharacterTestRuntimeAiPlannerPerceptionSettings.DefaultTargetMemoryFrames,
                lineOfSightObstacleLayerMask: CharacterTestRuntimeAiPlannerPerceptionSettings.DefaultLineOfSightObstacleLayerMask,
                environmentSampleRadius: 2.5f,
                threatDistanceWeight: CharacterTestRuntimeAiPlannerPerceptionSettings.DefaultThreatDistanceWeight);
            var frame = new RuntimeFrame(14);
            cache.BeginFrame(frame, module, physicsRuntime);

            Assert.IsTrue(cache.TryGetVisibility(
                frame,
                source,
                target,
                settings,
                out CharacterTestVisibilitySample firstVisibility));
            Assert.IsTrue(cache.TryGetVisibility(
                frame,
                source,
                target,
                settings,
                out CharacterTestVisibilitySample secondVisibility));
            Assert.AreEqual(1, cache.VisibilityQueryCount);
            Assert.AreEqual(firstVisibility.LineOfSightBlocked, secondVisibility.LineOfSightBlocked);

            float[] candidateAngles = { 45f, -45f };
            Assert.IsTrue(cache.TryGetNavigation(
                source,
                target,
                attackRange: 2f,
                navigationProfileKey: 777,
                probeMinLength: 1.5f,
                probeMaxLength: 6f,
                candidateAngles,
                out CharacterTestNavigationSample firstNavigation));
            int navigationQueries = cache.NavigationQueryCount;
            Assert.Greater(navigationQueries, 0);
            Assert.IsTrue(cache.TryGetNavigation(
                source,
                target,
                attackRange: 2f,
                navigationProfileKey: 777,
                probeMinLength: 1.5f,
                probeMaxLength: 6f,
                candidateAngles,
                out CharacterTestNavigationSample secondNavigation));
            Assert.AreEqual(navigationQueries, cache.NavigationQueryCount);
            Assert.AreEqual(
                firstNavigation.Result.SelectedCandidateIndex,
                secondNavigation.Result.SelectedCandidateIndex);
            int cachedNavigationQueries = cache.NavigationQueryCount;
            Assert.IsTrue(cache.TryGetNavigation(
                source,
                target,
                attackRange: 2f,
                navigationProfileKey: 778,
                probeMinLength: 1.5f,
                probeMaxLength: 6f,
                candidateAngles,
                out _));
            Assert.Greater(cache.NavigationQueryCount, cachedNavigationQueries);

            Assert.IsTrue(cache.TryGetEnvironment(frame, source, settings, out _));
            Assert.IsTrue(cache.TryGetEnvironment(frame, source, settings, out _));
            Assert.AreEqual(1, cache.EnvironmentQueryCount);
        }

        [Test]
        public void PerceptionFrameCache_NavigationCapsuleProbeUsesClearanceAndRefreshesKey()
        {
            CharacterTestCharacterPhysicsRuntime physicsRuntime =
                CreateFrameworkPhysicsRuntime(includeWall: false);
            RegisterStaticAabb(
                physicsRuntime.World,
                entity: 130,
                body: 130,
                collider: 1,
                layer: 1,
                position: FixVector3.Zero,
                localMin: new FixVector3(ToFix64(0.28f), Fix64.Zero, ToFix64(1.8f)),
                localMax: new FixVector3(ToFix64(0.35f), Fix64.FromInt(2), ToFix64(2.2f)));
            var module = new CharacterTestCharacterRuntimeModule(
                null,
                NullRuntimeLogger.Instance,
                physicsRuntime: physicsRuntime,
                createViews: false);
            CharacterTestCharacterEntity source = SpawnPerceptionTestEntity(
                module,
                teamId: 2,
                x: 0f,
                z: 0f,
                CharacterTestCharacterControlOwner.AiControlled);
            CharacterTestCharacterEntity target = SpawnPerceptionTestEntity(
                module,
                teamId: 1,
                x: 0f,
                z: 4f,
                CharacterTestCharacterControlOwner.None);
            CharacterTestPerceptionFrameCache cache = module.PerceptionFrameCache;
            float[] noCandidateAngles = Array.Empty<float>();

            var frame = new RuntimeFrame(15);
            cache.BeginFrame(frame, module, physicsRuntime);
            Assert.IsTrue(cache.TryGetNavigation(
                source,
                target,
                attackRange: 1f,
                navigationProfileKey: 901,
                probeMinLength: 1f,
                probeMaxLength: 5f,
                noCandidateAngles,
                out CharacterTestNavigationSample blocked));

            Assert.AreEqual(CharacterTestNavigationProbeShape.Capsule, blocked.QueryShape);
            Assert.AreEqual(physicsRuntime.MotionObstacleLayerMask.Value, blocked.LayerMask);
            Assert.AreEqual(source.Physics.EffectiveRadiusMillimeters, blocked.ClearanceRadiusMillimeters);
            Assert.IsFalse(blocked.Result.DirectPathClear);
            Assert.Greater(blocked.DirectPathHitCount, 0);
            Assert.AreEqual(new CombatEntityId(130), blocked.NearestObstacleEntityId);
            Assert.AreEqual(new CombatBodyId(130), blocked.NearestObstacleBodyId);

            int afterBlockedQueries = cache.NavigationQueryCount;
            source.SetPhysics(new CharacterTestCharacterPhysicsComponent(
                capsuleRadiusMeters: 0.1f,
                capsuleHeightMeters: CharacterTestCharacterPhysicsComponent.DefaultCapsuleHeightMeters,
                capsuleCenterY: CharacterTestCharacterPhysicsComponent.DefaultCapsuleHeightMeters / 2f,
                massKg: CharacterTestCharacterPhysicsComponent.DefaultMassKg));
            Assert.IsTrue(cache.TryGetNavigation(
                source,
                target,
                attackRange: 1f,
                navigationProfileKey: 901,
                probeMinLength: 1f,
                probeMaxLength: 5f,
                noCandidateAngles,
                out CharacterTestNavigationSample clearAfterClearanceChange));

            Assert.Greater(cache.NavigationQueryCount, afterBlockedQueries);
            Assert.AreEqual(100, clearAfterClearanceChange.ClearanceRadiusMillimeters);
            Assert.IsTrue(clearAfterClearanceChange.Result.DirectPathClear);
        }

        [Test]
        public void PerceptionFrameCache_WarmedRepeatedQueries_DoNotAllocate()
        {
            CharacterTestCharacterPhysicsRuntime physicsRuntime =
                CreateFrameworkPhysicsRuntime(includeWall: false);
            var module = new CharacterTestCharacterRuntimeModule(
                null,
                NullRuntimeLogger.Instance,
                physicsRuntime: physicsRuntime,
                createViews: false);
            CharacterTestCharacterEntity source = SpawnPerceptionTestEntity(
                module,
                teamId: 2,
                x: 0f,
                z: 0f,
                CharacterTestCharacterControlOwner.AiControlled);
            CharacterTestCharacterEntity target = SpawnPerceptionTestEntity(
                module,
                teamId: 1,
                x: 3f,
                z: 4f,
                CharacterTestCharacterControlOwner.None);
            SpawnPerceptionTestEntity(
                module,
                teamId: 1,
                x: -2f,
                z: 1f,
                CharacterTestCharacterControlOwner.None);
            CharacterTestPerceptionFrameCache cache = module.PerceptionFrameCache;
            var settings = CharacterTestRuntimeAiPlannerPerceptionSettings.CreateDefault();
            float[] candidateAngles = { 45f, -45f };

            WarmPerceptionCache(cache, module, source, target, settings, new RuntimeFrame(20), candidateAngles);

            _ = GC.GetAllocatedBytesForCurrentThread();
            long before = GC.GetAllocatedBytesForCurrentThread();
            var measuredFrame = new RuntimeFrame(21);
            cache.BeginFrame(measuredFrame, module, module.PhysicsRuntime);
            for (int i = 0; i < 32; i++)
            {
                Assert.IsTrue(cache.TryGetTargetGeometry(
                    measuredFrame,
                    source,
                    target,
                    out _));
                Assert.IsTrue(cache.TryFindNearestTarget(
                    measuredFrame,
                    source,
                    CharacterTestPerceptionTargetRelationFilter.OpposingTeam,
                    float.PositiveInfinity,
                    CharacterTestPerceptionTargetTiePolicy.First,
                    out _,
                    out _));
                Assert.IsTrue(cache.TryGetEnvironment(
                    measuredFrame,
                    source,
                    settings,
                    out _));
                Assert.IsTrue(cache.TryGetNavigation(
                    source,
                    target,
                    attackRange: 2f,
                    navigationProfileKey: 779,
                    probeMinLength: 1.5f,
                    probeMaxLength: 6f,
                    candidateAngles,
                    out _));
            }

            long allocatedBytes = GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.AreEqual(0L, allocatedBytes);
        }

        private static CharacterTestCharacterEntity SpawnPerceptionTestEntity(
            CharacterTestCharacterRuntimeModule module,
            int teamId,
            float x,
            float z,
            CharacterTestCharacterControlOwner owner)
        {
            CharacterTestCharacterDefinition definition = GetIronVanguardDefinition();
            CharacterTestCharacterEntity entity = module.World.Spawn(
                new CharacterTestCharacterSpawnRequest(
                    definition.CharacterId,
                    teamId,
                    x,
                    y: 0f,
                    z,
                    owner),
                definition);
            entity.SetMotionState(0f, Vector2.zero, isGrounded: true);
            return entity;
        }

        private static void SetTargetDead(
            CharacterTestCharacterRuntimeModule module,
            CharacterTestCharacterEntity target,
            RuntimeFrame frame)
        {
            module.World.SetCurrentAttribute(
                target.GameplayEntityId,
                CharacterTestCharacterAttributeIds.Hp,
                0);
            module.World.SyncCombatStateHealth(target);
            CharacterTestBattleStateSystem.RefreshEntity(module.World, target, frame, null);
        }

        private static void TickCommands(
            CharacterTestRuntimeCommandModule commands,
            RuntimeFrame frame)
        {
            commands.Tick(new RuntimeTickContext(
                frame.Value,
                deltaTime: 1d / 60d,
                elapsedTime: 0d,
                RuntimeTickStage.Simulation));
        }

        private static void AssertSetTargetLockRejected(
            CharacterTestRuntimeCommandModule commands,
            int targetEntityId,
            RuntimeFrame frame)
        {
            RuntimeCommandValidationResult accepted = commands.Enqueue(
                CharacterTestRuntimeCommandFactory.SetTargetLock(
                    frame,
                    CharacterTestRuntimeCommandSources.System,
                    enabled: true,
                    targetEntityId,
                    traceId: new RuntimeTraceIdentity(995177)));
            Assert.IsTrue(accepted.Success, accepted.Error.Message);
            TickCommands(commands, frame);

            Assert.IsFalse(commands.TargetLockEnabled);
            Assert.AreEqual(0, commands.LockedTargetEntityId);
            AssertRecentEvent(
                commands,
                CharacterTestRuntimeEventType.TargetLockRejected,
                CharacterTestRuntimeCommandIds.SetTargetLock);
        }

        private static void AssertSetTargetLockAccepted(
            CharacterTestRuntimeCommandModule commands,
            int targetEntityId,
            RuntimeFrame frame)
        {
            RuntimeCommandValidationResult accepted = commands.Enqueue(
                CharacterTestRuntimeCommandFactory.SetTargetLock(
                    frame,
                    CharacterTestRuntimeCommandSources.System,
                    enabled: true,
                    targetEntityId,
                    traceId: new RuntimeTraceIdentity(934568)));
            Assert.IsTrue(accepted.Success, accepted.Error.Message);
            TickCommands(commands, frame);

            Assert.IsTrue(commands.TargetLockEnabled);
            Assert.AreEqual(targetEntityId, commands.LockedTargetEntityId);
        }

        private static void AssertViewSwitchRejectedKeepsLock(
            CharacterTestRuntimeCommandModule commands,
            int primaryEntityId,
            int requestedTargetEntityId,
            int expectedLockedTargetEntityId,
            RuntimeFrame frame)
        {
            RuntimeCommandValidationResult accepted = commands.Enqueue(
                CharacterTestRuntimeCommandFactory.ToggleTargetLockViewSwitch(
                    frame,
                    CharacterTestRuntimeCommandSources.CameraInput,
                    primaryEntityId,
                    requestedTargetEntityId,
                    Vector2.right,
                    traceId: new RuntimeTraceIdentity(919272)));
            Assert.IsTrue(accepted.Success, accepted.Error.Message);
            TickCommands(commands, frame);

            Assert.IsTrue(commands.TargetLockEnabled);
            Assert.AreEqual(expectedLockedTargetEntityId, commands.LockedTargetEntityId);
            AssertRecentEvent(
                commands,
                CharacterTestRuntimeEventType.TargetLockRejected,
                CharacterTestRuntimeCommandIds.ToggleTargetLock);
        }

        private static void AssertViewSwitchNoCandidateKeepsLockWithoutError(
            CharacterTestRuntimeCommandModule commands,
            int primaryEntityId,
            int requestedTargetEntityId,
            int expectedLockedTargetEntityId,
            RuntimeFrame frame)
        {
            RuntimeCommandValidationResult accepted = commands.Enqueue(
                CharacterTestRuntimeCommandFactory.ToggleTargetLockViewSwitch(
                    frame,
                    CharacterTestRuntimeCommandSources.CameraInput,
                    primaryEntityId,
                    requestedTargetEntityId,
                    Vector2.right,
                    traceId: new RuntimeTraceIdentity(964049)));
            Assert.IsTrue(accepted.Success, accepted.Error.Message);
            TickCommands(commands, frame);

            Assert.IsTrue(commands.TargetLockEnabled);
            Assert.AreEqual(expectedLockedTargetEntityId, commands.LockedTargetEntityId);
            Assert.IsEmpty(commands.LastCommandErrors);
            AssertNoRecentEvent(
                commands,
                CharacterTestRuntimeEventType.TargetLockRejected,
                CharacterTestRuntimeCommandIds.ToggleTargetLock);
        }

        private static void AssertRecentEvent(
            CharacterTestRuntimeCommandModule commands,
            CharacterTestRuntimeEventType type,
            int commandId)
        {
            IReadOnlyList<CharacterTestRuntimeEvent> events = commands.RecentEvents;
            for (int i = events.Count - 1; i >= 0; i--)
            {
                CharacterTestRuntimeEvent evt = events[i];
                if (evt.Type == type && evt.CommandId == commandId)
                    return;
            }

            Assert.Fail("Expected runtime event type " + type + " for command " + commandId + ".");
        }

        private static void AssertNoRecentEvent(
            CharacterTestRuntimeCommandModule commands,
            CharacterTestRuntimeEventType type,
            int commandId)
        {
            IReadOnlyList<CharacterTestRuntimeEvent> events = commands.RecentEvents;
            for (int i = events.Count - 1; i >= 0; i--)
            {
                CharacterTestRuntimeEvent evt = events[i];
                if (evt.Type == type && evt.CommandId == commandId)
                    Assert.Fail("Unexpected runtime event type " + type + " for command " + commandId + ".");
            }
        }

        private static void WarmPerceptionCache(
            CharacterTestPerceptionFrameCache cache,
            CharacterTestCharacterRuntimeModule module,
            CharacterTestCharacterEntity source,
            CharacterTestCharacterEntity target,
            CharacterTestRuntimeAiPlannerPerceptionSettings settings,
            RuntimeFrame frame,
            float[] candidateAngles)
        {
            cache.BeginFrame(frame, module, module.PhysicsRuntime);
            Assert.IsTrue(cache.TryGetTargetGeometry(frame, source, target, out _));
            Assert.IsTrue(cache.TryFindNearestTarget(
                frame,
                source,
                CharacterTestPerceptionTargetRelationFilter.OpposingTeam,
                float.PositiveInfinity,
                CharacterTestPerceptionTargetTiePolicy.First,
                out _,
                out _));
            Assert.IsTrue(cache.TryGetEnvironment(frame, source, settings, out _));
            Assert.IsTrue(cache.TryGetNavigation(
                source,
                target,
                attackRange: 2f,
                navigationProfileKey: 779,
                probeMinLength: 1.5f,
                probeMaxLength: 6f,
                candidateAngles,
                out _));
        }
    }
}
