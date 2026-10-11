using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using MxFramework.CharacterAction;
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
    [Parallelizable(ParallelScope.None)]
    public sealed class CharacterTestAuthorityPosePerformanceTests
    {
        private const int CharacterCount = 8;
        private const int WarmupFrames = 300;
        private const int MeasurementFrames = 1800;
        private const int GroundLayer = 0;
        private const int GroundBodyValue = 980001;
        private const int GroundColliderValue = 980002;
        private const int ClipLocalFrame = 10;
        private const string ClipKey = "char.000003.animation.attack1a";
        private static readonly CharacterActionId ActionId = new CharacterActionId(940000);
        private const string AnimationActionKey = "000003.attack1a";
        private const string BaseSourceHash = "sha256:3333333333333333333333333333333333333333333333333333333333333333";

        [Test]
        public void EightDragon_DoubleHurtboxSyncAndFourLimbPose_Warm300Measure1800_IsZeroAllocAndCalibrated()
        {
            AcceptanceFixture fixture = CreateFixture();
            for (int frame = 1; frame <= WarmupFrames; frame++)
                Assert.IsTrue(RunFullFrame(fixture, frame));

            var firstSyncTicks = new long[MeasurementFrames];
            var poseTicks = new long[MeasurementFrames];
            var secondSyncTicks = new long[MeasurementFrames];
            var fullTicks = new long[MeasurementFrames];
            bool succeeded = true;

            _ = GC.GetAllocatedBytesForCurrentThread();
            long allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
            for (int sample = 0; sample < MeasurementFrames; sample++)
            {
                var frame = new RuntimeFrame(WarmupFrames + sample + 1L);
                long fullStart = Stopwatch.GetTimestamp();
                fixture.Hurtboxes.SyncAll(fixture.World.Entities, fixture.Physics, frame);
                long firstSyncEnd = Stopwatch.GetTimestamp();
                for (int entityIndex = 0; entityIndex < fixture.World.Entities.Count; entityIndex++)
                {
                    succeeded &= fixture.Terrain.TickForTests(
                        fixture.World.Entities[entityIndex],
                        fixture.Physics.World,
                        frame,
                        ClipKey,
                        localFrame: ClipLocalFrame,
                        loop: true);
                }
                long poseEnd = Stopwatch.GetTimestamp();
                fixture.Hurtboxes.SyncAll(fixture.World.Entities, fixture.Physics, frame);
                long fullEnd = Stopwatch.GetTimestamp();

                firstSyncTicks[sample] = firstSyncEnd - fullStart;
                poseTicks[sample] = poseEnd - firstSyncEnd;
                secondSyncTicks[sample] = fullEnd - poseEnd;
                fullTicks[sample] = fullEnd - fullStart;
            }
            long allocatedBytes = GC.GetAllocatedBytesForCurrentThread() - allocatedBefore;

            Assert.IsTrue(succeeded);
            Assert.AreEqual(0L, allocatedBytes,
                "The warmed first Sync -> four probes/IK -> second Sync path must allocate 0 B.");
            Assert.AreEqual(CharacterCount * 18, fixture.Physics.World.CompoundLeafCount);
            AssertAllDragonOutputs(fixture, expectedFailure: CharacterTestAuthorityPoseFailure.Success);
            WriteCalibration("first_sync", firstSyncTicks);
            WriteCalibration("four_probe_ik", poseTicks);
            WriteCalibration("second_sync", secondSyncTicks);
            WriteCalibration("full_chain", fullTicks);
            TestContext.Out.WriteLine(
                "lcc_m4_t5_perf characters=8 limbs_per_character=4 hurtboxes_per_character=18"
                + " warmup=300 measured=1800 allocated_bytes=" + allocatedBytes);
        }

        [Test]
        public void EightDragon_NoHitAndRecoverySteadyPaths_Measure1800_AllocateZeroBytes()
        {
            AcceptanceFixture fixture = CreateFixture();
            for (int frame = 1; frame <= WarmupFrames; frame++)
                Assert.IsTrue(RunFullFrame(fixture, frame));

            Assert.IsTrue(fixture.Physics.World.RemoveBody(new CombatBodyId(GroundBodyValue)));
            int frameValue = WarmupFrames;
            for (int i = 0; i < 16; i++)
                Assert.IsTrue(RunFullFrame(fixture, ++frameValue));
            AssertAllDragonOutputs(fixture, expectedFailure: CharacterTestAuthorityPoseFailure.NoHit);

            bool noHitSucceeded = true;
            _ = GC.GetAllocatedBytesForCurrentThread();
            long noHitBefore = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < MeasurementFrames / 2; i++)
                noHitSucceeded &= RunFullFrame(fixture, ++frameValue);
            long noHitAllocated = GC.GetAllocatedBytesForCurrentThread() - noHitBefore;

            AddGround(fixture.Physics.World);
            for (int i = 0; i < 16; i++)
                Assert.IsTrue(RunFullFrame(fixture, ++frameValue));
            AssertAllDragonOutputs(fixture, expectedFailure: CharacterTestAuthorityPoseFailure.Success);

            bool recoverySucceeded = true;
            _ = GC.GetAllocatedBytesForCurrentThread();
            long recoveryBefore = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < MeasurementFrames / 2; i++)
                recoverySucceeded &= RunFullFrame(fixture, ++frameValue);
            long recoveryAllocated = GC.GetAllocatedBytesForCurrentThread() - recoveryBefore;

            Assert.IsTrue(noHitSucceeded);
            Assert.IsTrue(recoverySucceeded);
            Assert.AreEqual(0L, noHitAllocated, "Settled no-hit path must allocate 0 B.");
            Assert.AreEqual(0L, recoveryAllocated, "Settled recovered path must allocate 0 B.");
            TestContext.Out.WriteLine(
                "lcc_m4_t5_failure_recovery characters=8 warmup=300"
                + " no_hit_measured=900 recovery_measured=900 no_hit_allocated_bytes="
                + noHitAllocated + " recovery_allocated_bytes=" + recoveryAllocated);
        }

        private static AcceptanceFixture CreateFixture()
        {
            CharacterTestCharacterDefinition dragon = FindDragonDefinition();
            CharacterImportedPackage package =
                CharacterTestGeneratedPackageSource.LoadPackage(dragon.GeneratedPackageRoot);
            Assert.NotNull(package);
            Assert.AreEqual(18, package.Geometry.BodyColliders.Length);
            CharacterAuthorityPoseRigRuntimeBinding rig = package.Geometry.AuthorityPoseRig;
            Assert.NotNull(rig);
            Assert.IsTrue(rig.IsAuthored);
            Assert.AreEqual(4, rig.Limbs.Length);
            Assert.IsTrue(CharacterTestDefaultBakedWeaponTraceSource.Instance
                .TryGetPackageSocketCatalog(dragon.GeneratedPackageRoot, out CharacterSocketBakeCatalog catalog));
            Assert.IsTrue(catalog.TryGetTrack(ClipKey, rig.Limbs[0].TipChannelId, out _));

            CharacterAuthorityPoseRigDefinition definition =
                CharacterAuthorityPoseRigDefinition.FromRuntimeBinding(
                    rig,
                    CharacterTestAuthorityPoseDefinitionSource
                        .CreateDefaultDefinition()
                        .SecondaryBindings);
            var world = new CharacterTestCharacterRuntimeWorld();
            var physicsWorld = new CombatPhysicsWorld();
            AddGround(physicsWorld);
            var physics = new CharacterTestCharacterPhysicsRuntime(
                physicsWorld,
                CombatPhysicsLayerMask.FromLayer(2),
                new FixVector3(Fix64.Half, Fix64.One, Fix64.Half),
                Fix64.FromRatio(1, 100),
                Fix64.Half,
                staticColliderCount: 1,
                sourceDescription: "lcc-m4-t5.authority-pose-performance");
            var terrain = new CharacterTestAuthorityPoseTerrainSystem(actionRuntime: null);
            var hurtboxes = new CharacterTestCharacterHurtboxSystem();
            for (int i = 0; i < CharacterCount; i++)
            {
                float x = (i - ((CharacterCount - 1) * 0.5f)) * 8f;
                CharacterTestCharacterEntity entity = world.Spawn(
                    new CharacterTestCharacterSpawnRequest(
                        dragon.CharacterId,
                        teamId: 1,
                        x: x,
                        y: 0f,
                        z: 0f,
                        controlOwner: CharacterTestCharacterControlOwner.None),
                    dragon);
                hurtboxes.PrepareEntity(entity);
                entity.SetMotionState(
                    verticalVelocity: 0f,
                    lastMoveInput: Vector2.zero,
                    isGrounded: true,
                    movementMode: CharacterMovementMode.Idle);
                entity.SetAction(CreateAction(localFrame: ClipLocalFrame));
                entity.SetAnimation(new CharacterTestCharacterAnimationComponent(
                    Vector2.zero,
                    locomotionSpeed01: 0f,
                    grounded: true,
                    airborne: false,
                    actionActive: true,
                    actionKey: AnimationActionKey));
                Assert.IsTrue(terrain.ConfigureForTests(
                    entity,
                    definition,
                    catalog,
                    baseSourceRevision: 1,
                    baseSourceContentHash: BaseSourceHash));
            }

            return new AcceptanceFixture(world, physics, terrain, hurtboxes);
        }

        private static CharacterTestCharacterActionComponent CreateAction(int localFrame)
        {
            return CharacterTestCharacterActionComponent.FromDeterministic(
                ActionId,
                state: MxFramework.Game.CharacterTest.CharacterTestActionStateId.Running,
                phase: MxFramework.Game.CharacterTest.CharacterTestActionPhaseId.Active,
                localFrame,
                lastRejectReason: default,
                lastAnimationActionKey: AnimationActionKey,
                animationActionKeyThisFrame: AnimationActionKey,
                animationTransitionFrames: 0,
                animationHoldFrames: 0,
                jumpImpulseYMillimetersPerSecond: 0);
        }

        private static bool RunFullFrame(AcceptanceFixture fixture, long frameValue)
        {
            var frame = new RuntimeFrame(frameValue);
            fixture.Hurtboxes.SyncAll(fixture.World.Entities, fixture.Physics, frame);
            bool succeeded = true;
            for (int i = 0; i < fixture.World.Entities.Count; i++)
            {
                succeeded &= fixture.Terrain.TickForTests(
                    fixture.World.Entities[i],
                    fixture.Physics.World,
                    frame,
                    ClipKey,
                    localFrame: ClipLocalFrame,
                    loop: true);
            }
            fixture.Hurtboxes.SyncAll(fixture.World.Entities, fixture.Physics, frame);
            return succeeded;
        }

        private static void AssertAllDragonOutputs(
            AcceptanceFixture fixture,
            CharacterTestAuthorityPoseFailure expectedFailure)
        {
            for (int i = 0; i < fixture.World.Entities.Count; i++)
            {
                CharacterTestCharacterEntity entity = fixture.World.Entities[i];
                Assert.IsTrue(entity.TerrainIkState.IsConfigured);
                Assert.AreEqual(4, entity.TerrainIkState.LimbCount);
                Assert.IsTrue(entity.ResolvedAuthorityPoseFrame.IsPublished);
                Assert.AreEqual(12, entity.ResolvedAuthorityPoseFrame.Count);
                for (int limb = 0; limb < entity.TerrainIkState.LimbCount; limb++)
                    Assert.AreEqual(expectedFailure, entity.TerrainIkState.GetLimb(limb).CurrentFailure);
                Assert.IsTrue(fixture.Hurtboxes.TryGetDamageGeometrySnapshot(
                    entity.Id.Value,
                    out CharacterTestDamageGeometrySnapshot snapshot));
                Assert.AreEqual(18, snapshot.ColliderCount);
            }
        }

        private static void AddGround(CombatPhysicsWorld world)
        {
            var bodyId = new CombatBodyId(GroundBodyValue);
            world.UpsertBody(new CombatPhysicsBody(
                new CombatEntityId(GroundBodyValue),
                bodyId,
                FixVector3.Zero));
            world.UpsertAabbCollider(new CombatPhysicsAabbCollider(
                bodyId,
                new CombatColliderId(GroundColliderValue),
                GroundLayer,
                new FixVector3(-Fix64.FromInt(64), -Fix64.One, -Fix64.FromInt(16)),
                new FixVector3(Fix64.FromInt(64), Fix64.FromRatio(1, 10), Fix64.FromInt(16)),
                CombatSurfaceMaterial.Rough));
        }

        private static CharacterTestCharacterDefinition FindDragonDefinition()
        {
            IReadOnlyList<CharacterTestCharacterDefinition> definitions =
                CharacterTestPlayableCharacterCatalog.Discover();
            for (int i = 0; i < definitions.Count; i++)
            {
                string root = (definitions[i].GeneratedPackageRoot ?? string.Empty)
                    .Replace('\\', '/')
                    .TrimEnd('/');
                if (root.EndsWith("/000003", StringComparison.OrdinalIgnoreCase))
                    return definitions[i];
            }

            Assert.Fail("Playable Dragon14 definition is missing.");
            return default;
        }

        private static void WriteCalibration(string stage, long[] samples)
        {
            Array.Sort(samples);
            double ticksToMicroseconds = 1_000_000d / Stopwatch.Frequency;
            double p50 = samples[(samples.Length - 1) / 2] * ticksToMicroseconds;
            int p95Index = Math.Max(0, (int)Math.Ceiling(samples.Length * 0.95d) - 1);
            double p95 = samples[p95Index] * ticksToMicroseconds;
            double max = samples[samples.Length - 1] * ticksToMicroseconds;
            TestContext.Out.WriteLine(string.Format(
                CultureInfo.InvariantCulture,
                "lcc_m4_t5_calibration stage={0} p50_us={1:F3} p95_us={2:F3} max_us={3:F3}",
                stage,
                p50,
                p95,
                max));
        }

        private sealed class AcceptanceFixture
        {
            internal AcceptanceFixture(
                CharacterTestCharacterRuntimeWorld world,
                CharacterTestCharacterPhysicsRuntime physics,
                CharacterTestAuthorityPoseTerrainSystem terrain,
                CharacterTestCharacterHurtboxSystem hurtboxes)
            {
                World = world;
                Physics = physics;
                Terrain = terrain;
                Hurtboxes = hurtboxes;
            }

            internal CharacterTestCharacterRuntimeWorld World { get; }
            internal CharacterTestCharacterPhysicsRuntime Physics { get; }
            internal CharacterTestAuthorityPoseTerrainSystem Terrain { get; }
            internal CharacterTestCharacterHurtboxSystem Hurtboxes { get; }
        }
    }
}
