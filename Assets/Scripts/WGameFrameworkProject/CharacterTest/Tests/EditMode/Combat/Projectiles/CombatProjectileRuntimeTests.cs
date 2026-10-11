using System;
using System.Collections.Generic;
using MxFramework.Combat.Core;
using MxFramework.Combat.Hit;
using MxFramework.Combat.Physics;
using MxFramework.Combat.Projectiles;
using MxFramework.Core.Math;
using MxFramework.Runtime;
using NUnit.Framework;

namespace MxFramework.Tests.Combat.Projectiles
{
    public sealed class CombatProjectileRuntimeTests
    {
        private const int ForceDamageTypeId = 940001;
        private const int PhysicalAnalysisId = 940002;
        private const int ProjectileImpactEventId = 940101;
        private const int EnvironmentImpactEventId = 940102;
        private const int EnvironmentDirectEventId = 940103;
        private const int EnvironmentAreaEventId = 940104;
        private const int PresentationTestId = 940201;
        private const int PresentationLaserId = 940202;
        [Test]
        public void BorrowedStateViewMatchesOwnedSnapshotWithoutAllocatingOrMutatingHistory()
        {
            var runtime = CreateRuntimeWithActiveProjectile(out _);
            var owned = runtime.CreateStateSnapshot();
            Assert.Greater(owned.ActiveCount, 0);
            var expected = new MxFramework.Runtime.RuntimeHashAccumulator(RuntimeHashSchemaIds.CombatProjectile, RuntimeHashSchemaIds.Version1);
            var actual = new MxFramework.Runtime.RuntimeHashAccumulator(RuntimeHashSchemaIds.CombatProjectile, RuntimeHashSchemaIds.Version1);
            MxFramework.Game.CharacterTest.CharacterTestNetworkProjectileSnapshotCodec.AddStateHash(expected, owned);
            for (int i = 0; i < 10; i++)
            {
                actual.Reset();
                MxFramework.Game.CharacterTest.CharacterTestNetworkProjectileSnapshotCodec.AddStateHash(actual, runtime.ReadStateView());
            }
            Assert.AreEqual(expected.ToHash(), actual.ToHash());
            const Unity.Profiling.ProfilerRecorderOptions options = Unity.Profiling.ProfilerRecorderOptions.StartImmediately
                | Unity.Profiling.ProfilerRecorderOptions.CollectOnlyOnCurrentThread;
            using (var positive = Unity.Profiling.ProfilerRecorder.StartNew(Unity.Profiling.ProfilerCategory.Memory, "GC.Alloc", 1024, options))
            { System.GC.KeepAlive(new byte[8192]); positive.Stop(); Assert.Greater(positive.Count, 0); }
            using (var recorder = Unity.Profiling.ProfilerRecorder.StartNew(Unity.Profiling.ProfilerCategory.Memory, "GC.Alloc", 65536, options))
            {
                for (int i = 0; i < 100; i++)
                {
                    actual.Reset();
                    MxFramework.Game.CharacterTest.CharacterTestNetworkProjectileSnapshotCodec.AddStateHash(actual, runtime.ReadStateView());
                }
                recorder.Stop(); Assert.AreEqual(0, recorder.Count);
            }
            var empty = new CombatProjectileRuntimeState(owned.SchemaVersion, owned.ModuleId, owned.DefinitionHash,
                owned.NextProjectileId, null);
            Assert.IsTrue(runtime.TryRestoreStateSnapshot(empty, out string failure), failure);
            Assert.AreEqual(0, runtime.ReadStateView().ActiveCount);
            Assert.Greater(owned.ActiveCount, 0, "Borrowed working storage must not overwrite a retained snapshot.");
        }

        [Test]
        public void SpawnRequest_DoesNotMoveUntilNextCombatFrame()
        {
            CombatProjectileSpawnRequestBuffer buffer = new CombatProjectileSpawnRequestBuffer();
            CombatProjectileRuntime runtime = CreateRuntime(buffer, new CombatPhysicsWorld(), lifetimeFrames: 10);
            buffer.Enqueue(Request(new CombatFrame(0)));

            CombatProjectileFrameEvents spawnFrame = runtime.Tick(new CombatFrame(0));
            CombatProjectileRuntimeState afterSpawn = runtime.CreateStateSnapshot();

            Assert.AreEqual(1, spawnFrame.Spawns.Count);
            Assert.AreEqual(0, spawnFrame.Impacts.Count);
            Assert.AreEqual(1, afterSpawn.ActiveCount);
            Assert.AreEqual(FixVector3.Zero, afterSpawn.ActiveProjectiles[0].Position);
            Assert.AreEqual(0, afterSpawn.ActiveProjectiles[0].AgeFrames);

            runtime.Tick(new CombatFrame(1));
            CombatProjectileRuntimeState afterMove = runtime.CreateStateSnapshot();

            Assert.AreEqual(new FixVector3(Fix64.Half, Fix64.Zero, Fix64.Zero), afterMove.ActiveProjectiles[0].Position);
            Assert.AreEqual(1, afterMove.ActiveProjectiles[0].AgeFrames);
        }

        [Test]
        public void CopyActiveSnapshotsTo_DoesNotAllocateAfterWarmup()
        {
            CombatProjectileSpawnRequestBuffer buffer = new CombatProjectileSpawnRequestBuffer();
            CombatProjectileRuntime runtime = CreateRuntime(buffer, new CombatPhysicsWorld(), lifetimeFrames: 10);
            buffer.Enqueue(Request(new CombatFrame(0)));
            runtime.Tick(new CombatFrame(0));
            runtime.Tick(new CombatFrame(1));
            CombatProjectileFrameEvents noEvents = runtime.Tick(new CombatFrame(2));
            var snapshots = new List<CombatProjectileSnapshot>(runtime.ActiveCount);

            Assert.AreSame(CombatProjectileFrameEvents.Empty, noEvents);
            _ = GC.GetAllocatedBytesForCurrentThread();
            runtime.CopyActiveSnapshotsTo(snapshots);
            Assert.AreEqual(1, snapshots.Count);

            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 64; i++)
            {
                snapshots.Clear();
                runtime.CopyActiveSnapshotsTo(snapshots);
            }

            long allocatedBytes = GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.AreEqual(0L, allocatedBytes);
        }

        [Test]
        public void Snapshot_PreservesDirectionRawValuesForRollback()
        {
            var direction = new FixVector3(
                Fix64.FromRatio(707106, 1000000),
                Fix64.FromRatio(1, 1000000),
                Fix64.FromRatio(707107, 1000000));

            var snapshot = new CombatProjectileSnapshot(
                projectileId: 1,
                spawnSequence: 0,
                definitionId: 10,
                definitionStableId: "projectile.rollback.direction",
                sourceEntityId: new CombatEntityId(1001),
                actionId: 1,
                actionInstanceId: 1,
                traceId: 1,
                localFrame: 0,
                spawnFrame: new CombatFrame(0),
                lastFrame: new CombatFrame(0),
                ageFrames: 0,
                position: FixVector3.Zero,
                direction: direction,
                acceptedImpacts: null);

            Assert.AreEqual(direction.X.RawValue, snapshot.Direction.X.RawValue);
            Assert.AreEqual(direction.Y.RawValue, snapshot.Direction.Y.RawValue);
            Assert.AreEqual(direction.Z.RawValue, snapshot.Direction.Z.RawValue);
        }

        [Test]
        public void TickWithFrameEventBufferAndActiveViews_DoesNotAllocateAfterWarmup()
        {
            CombatProjectileSpawnRequestBuffer buffer = new CombatProjectileSpawnRequestBuffer();
            CombatProjectileRuntime runtime = CreateRuntime(buffer, new CombatPhysicsWorld(), lifetimeFrames: 100);
            var events = new CombatProjectileFrameEventBuffer();
            var activeViews = new List<CombatProjectileActiveView>(1);

            buffer.Enqueue(Request(new CombatFrame(0)));
            runtime.Tick(new CombatFrame(0), events);
            runtime.Tick(new CombatFrame(1), events);
            runtime.CopyActiveViewsTo(activeViews);
            Assert.AreEqual(1, activeViews.Count);

            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 32; i++)
            {
                runtime.Tick(new CombatFrame(i + 2), events);
                Assert.AreEqual(0, events.Impacts.Count);
                Assert.AreEqual(0, events.Expirations.Count);
                activeViews.Clear();
                runtime.CopyActiveViewsTo(activeViews);
                Assert.AreEqual(1, activeViews.Count);
            }

            long allocatedBytes = GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.AreEqual(0L, allocatedBytes);
        }

        [Test]
        public void LinearMovement_ExpiresAfterConfiguredLifetime()
        {
            CombatProjectileSpawnRequestBuffer buffer = new CombatProjectileSpawnRequestBuffer();
            CombatProjectileRuntime runtime = CreateRuntime(buffer, new CombatPhysicsWorld(), lifetimeFrames: 2);
            buffer.Enqueue(Request(new CombatFrame(0)));

            runtime.Tick(new CombatFrame(0));
            runtime.Tick(new CombatFrame(1));
            CombatProjectileFrameEvents expired = runtime.Tick(new CombatFrame(2));

            Assert.AreEqual(0, runtime.ActiveCount);
            Assert.AreEqual(1, expired.Expirations.Count);
            Assert.AreEqual(2, expired.Expirations[0].Projectile.AgeFrames);
            Assert.AreEqual(new FixVector3(Fix64.One, Fix64.Zero, Fix64.Zero), expired.Expirations[0].Projectile.Position);
        }

        [Test]
        public void PhysicalEvents_DisabledByDefaultDoesNotEmit()
        {
            CombatProjectileSpawnRequestBuffer buffer = new CombatProjectileSpawnRequestBuffer();
            CombatProjectileRuntime runtime = CreatePhysicalEventRuntime(
                buffer,
                new CombatPhysicsWorld(),
                physicalEventsEnabled: false);
            buffer.Enqueue(Request(new CombatFrame(0)));

            runtime.Tick(new CombatFrame(0));
            CombatProjectileFrameEvents events = runtime.Tick(new CombatFrame(1));

            Assert.AreEqual(0, events.PhysicalEvents.Count);
        }

        [Test]
        public void PhysicalEvents_ActiveFrameUsesCollisionRadiusAndImpactPower()
        {
            CombatProjectileSpawnRequestBuffer buffer = new CombatProjectileSpawnRequestBuffer();
            CombatProjectileRuntime runtime = CreatePhysicalEventRuntime(
                buffer,
                new CombatPhysicsWorld(),
                collisionRadius: Fix64.FromRatio(3, 10),
                impactPower: 7);
            buffer.Enqueue(Request(new CombatFrame(0)));

            runtime.Tick(new CombatFrame(0));
            CombatProjectileFrameEvents events = runtime.Tick(new CombatFrame(1));

            Assert.AreEqual(1, events.PhysicalEvents.Count);
            CombatProjectilePhysicalEvent physicalEvent = events.PhysicalEvents[0];
            Assert.AreEqual(CombatProjectilePhysicalEventKind.ActiveFrame, physicalEvent.Kind);
            Assert.AreEqual(Fix64.FromRatio(3, 10), physicalEvent.Radius);
            Assert.AreEqual(7, physicalEvent.ImpactPower);
            Assert.AreEqual(new FixVector3(Fix64.Half, Fix64.Zero, Fix64.Zero), physicalEvent.Position);
        }

        [Test]
        public void PhysicalEvents_ImpactUsesHitPointCollisionRadiusAndImpactPower()
        {
            CombatPhysicsWorld world = new CombatPhysicsWorld();
            RegisterTarget(world, entity: 2, x: Fix64.Half, z: Fix64.Zero);
            CombatProjectileSpawnRequestBuffer buffer = new CombatProjectileSpawnRequestBuffer();
            CombatProjectileRuntime runtime = CreatePhysicalEventRuntime(
                buffer,
                world,
                collisionRadius: Fix64.FromRatio(4, 10),
                impactPower: 9);
            buffer.Enqueue(Request(new CombatFrame(0)));

            runtime.Tick(new CombatFrame(0));
            CombatProjectileFrameEvents events = runtime.Tick(new CombatFrame(1));

            Assert.AreEqual(1, events.Impacts.Count);
            Assert.AreEqual(1, events.PhysicalEvents.Count);
            CombatProjectilePhysicalEvent physicalEvent = events.PhysicalEvents[0];
            Assert.AreEqual(CombatProjectilePhysicalEventKind.Impact, physicalEvent.Kind);
            Assert.AreEqual(Fix64.FromRatio(4, 10), physicalEvent.Radius);
            Assert.AreEqual(9, physicalEvent.ImpactPower);
            Assert.AreEqual(events.Impacts[0].PhysicsHit.Point, physicalEvent.Position);
        }

        [Test]
        public void PhysicalEvents_AreaPulseUsesAreaRadiusAndAreaImpactPower()
        {
            CombatPhysicsWorld world = new CombatPhysicsWorld();
            RegisterTarget(world, entity: 2, x: Fix64.Half, z: Fix64.Zero);
            CombatProjectileSpawnRequestBuffer buffer = new CombatProjectileSpawnRequestBuffer();
            CombatProjectileRuntime runtime = CreatePhysicalEventRuntime(
                buffer,
                world,
                areaPulse: true,
                areaRadius: Fix64.FromRatio(3, 2),
                impactPower: 9,
                areaImpactPower: 11);
            buffer.Enqueue(Request(new CombatFrame(0)));

            runtime.Tick(new CombatFrame(0));
            CombatProjectileFrameEvents events = runtime.Tick(new CombatFrame(1));

            Assert.AreEqual(1, events.AreaPulseTriggers.Count);
            Assert.AreEqual(1, events.PhysicalEvents.Count);
            CombatProjectilePhysicalEvent physicalEvent = events.PhysicalEvents[0];
            Assert.AreEqual(CombatProjectilePhysicalEventKind.AreaPulse, physicalEvent.Kind);
            Assert.AreEqual(CombatProjectileAreaPulseTriggerKind.DirectSweep, physicalEvent.AreaPulseTriggerKind);
            Assert.AreEqual(Fix64.FromRatio(3, 2), physicalEvent.Radius);
            Assert.AreEqual(11, physicalEvent.ImpactPower);
            Assert.AreEqual(events.AreaPulseTriggers[0].Center, physicalEvent.Position);
        }

        [Test]
        public void PhysicalEvents_ExpirationAreaPulseUsesAreaRadiusAndFallbackImpactPower()
        {
            CombatProjectileSpawnRequestBuffer buffer = new CombatProjectileSpawnRequestBuffer();
            CombatProjectileRuntime runtime = CreatePhysicalEventRuntime(
                buffer,
                new CombatPhysicsWorld(),
                lifetimeFrames: 1,
                areaPulse: true,
                areaRadius: Fix64.FromRatio(5, 2),
                impactPower: 13,
                areaImpactPower: 0);
            buffer.Enqueue(Request(new CombatFrame(0)));

            runtime.Tick(new CombatFrame(0));
            CombatProjectileFrameEvents events = runtime.Tick(new CombatFrame(1));

            Assert.AreEqual(1, events.Expirations.Count);
            Assert.AreEqual(2, events.PhysicalEvents.Count);
            Assert.AreEqual(CombatProjectilePhysicalEventKind.AreaPulse, events.PhysicalEvents[0].Kind);
            Assert.AreEqual(CombatProjectilePhysicalEventKind.Expiration, events.PhysicalEvents[1].Kind);
            Assert.AreEqual(CombatProjectileAreaPulseTriggerKind.Expiration, events.PhysicalEvents[0].AreaPulseTriggerKind);
            Assert.AreEqual(Fix64.FromRatio(5, 2), events.PhysicalEvents[0].Radius);
            Assert.AreEqual(Fix64.FromRatio(5, 2), events.PhysicalEvents[1].Radius);
            Assert.AreEqual(13, events.PhysicalEvents[0].ImpactPower);
            Assert.AreEqual(13, events.PhysicalEvents[1].ImpactPower);
        }

        [Test]
        public void PhysicalEvents_ZeroRadiusOrImpactPowerDoesNotEmit()
        {
            CombatProjectileSpawnRequestBuffer zeroRadiusBuffer = new CombatProjectileSpawnRequestBuffer();
            CombatProjectileRuntime zeroRadiusRuntime = CreatePhysicalEventRuntime(
                zeroRadiusBuffer,
                new CombatPhysicsWorld(),
                collisionRadius: Fix64.Zero,
                impactPower: 7);
            zeroRadiusBuffer.Enqueue(Request(new CombatFrame(0)));
            zeroRadiusRuntime.Tick(new CombatFrame(0));

            CombatProjectileSpawnRequestBuffer zeroImpactBuffer = new CombatProjectileSpawnRequestBuffer();
            CombatProjectileRuntime zeroImpactRuntime = CreatePhysicalEventRuntime(
                zeroImpactBuffer,
                new CombatPhysicsWorld(),
                collisionRadius: Fix64.FromRatio(1, 10),
                impactPower: 0);
            zeroImpactBuffer.Enqueue(Request(new CombatFrame(0)));
            zeroImpactRuntime.Tick(new CombatFrame(0));

            Assert.AreEqual(0, zeroRadiusRuntime.Tick(new CombatFrame(1)).PhysicalEvents.Count);
            Assert.AreEqual(0, zeroImpactRuntime.Tick(new CombatFrame(1)).PhysicalEvents.Count);
        }

        [Test]
        public void SpeedProfile_RedistributesLinearPathProgressDeterministically()
        {
            AssertLinearHalfwayPosition(
                CombatProjectileSpeedProfileKind.EaseInQuadratic,
                new FixVector3(Fix64.Half, Fix64.Zero, Fix64.Zero));
            AssertLinearHalfwayPosition(
                CombatProjectileSpeedProfileKind.EaseOutQuadratic,
                new FixVector3(Fix64.FromRatio(3, 2), Fix64.Zero, Fix64.Zero));
            AssertLinearHalfwayPosition(
                CombatProjectileSpeedProfileKind.EaseInOutSmoothstep,
                new FixVector3(Fix64.One, Fix64.Zero, Fix64.Zero));
        }

        [Test]
        public void ArcToTargetMovement_SamplesParabolaAndExpiresAtTargetPoint()
        {
            CombatProjectileSpawnRequestBuffer buffer = new CombatProjectileSpawnRequestBuffer();
            CombatProjectileRuntime runtime = CreateRuntime(
                buffer,
                new CombatPhysicsWorld(),
                lifetimeFrames: 4,
                motionKind: CombatProjectileMotionKind.ArcToTarget,
                arcHeight: Fix64.FromInt(2));
            FixVector3 origin = FixVector3.Zero;
            FixVector3 targetPoint = new FixVector3(Fix64.FromInt(4), Fix64.Zero, Fix64.Zero);
            buffer.Enqueue(TargetedRequest(new CombatFrame(0), origin, targetPoint));

            runtime.Tick(new CombatFrame(0));
            Assert.AreEqual(origin, runtime.CreateStateSnapshot().ActiveProjectiles[0].Position);

            runtime.Tick(new CombatFrame(1));
            Assert.AreEqual(
                new FixVector3(Fix64.One, Fix64.FromRatio(3, 2), Fix64.Zero),
                runtime.CreateStateSnapshot().ActiveProjectiles[0].Position);

            runtime.Tick(new CombatFrame(2));
            Assert.AreEqual(
                new FixVector3(Fix64.FromInt(2), Fix64.FromInt(2), Fix64.Zero),
                runtime.CreateStateSnapshot().ActiveProjectiles[0].Position);

            runtime.Tick(new CombatFrame(3));
            CombatProjectileFrameEvents expired = runtime.Tick(new CombatFrame(4));

            Assert.AreEqual(0, runtime.ActiveCount);
            Assert.AreEqual(1, expired.Expirations.Count);
            Assert.AreEqual(targetPoint, expired.Expirations[0].Projectile.Position);
        }

        [Test]
        public void ArcToTargetMovement_UsesSpawnRequestEffectiveLifetime()
        {
            CombatProjectileSpawnRequestBuffer buffer = new CombatProjectileSpawnRequestBuffer();
            CombatProjectileRuntime runtime = CreateRuntime(
                buffer,
                new CombatPhysicsWorld(),
                lifetimeFrames: 10,
                motionKind: CombatProjectileMotionKind.ArcToTarget,
                arcHeight: Fix64.FromInt(2));
            FixVector3 targetPoint = new FixVector3(Fix64.FromInt(2), Fix64.Zero, Fix64.Zero);
            buffer.Enqueue(TargetedRequest(
                new CombatFrame(0),
                FixVector3.Zero,
                targetPoint,
                effectiveLifetimeFrames: 4));

            runtime.Tick(new CombatFrame(0));
            runtime.Tick(new CombatFrame(1));
            runtime.Tick(new CombatFrame(2));
            CombatProjectileSnapshot halfway = runtime.CreateStateSnapshot().ActiveProjectiles[0];

            Assert.AreEqual(4, halfway.EffectiveLifetimeFrames);
            Assert.AreEqual(
                new FixVector3(Fix64.One, Fix64.FromInt(2), Fix64.Zero),
                halfway.Position);

            runtime.Tick(new CombatFrame(3));
            CombatProjectileFrameEvents expired = runtime.Tick(new CombatFrame(4));

            Assert.AreEqual(0, runtime.ActiveCount);
            Assert.AreEqual(1, expired.Expirations.Count);
            Assert.AreEqual(4, expired.Expirations[0].Projectile.EffectiveLifetimeFrames);
            Assert.AreEqual(targetPoint, expired.Expirations[0].Projectile.Position);
        }

        [Test]
        public void ArcToTargetSweep_HitsTargetAlongCurve()
        {
            CombatPhysicsWorld world = new CombatPhysicsWorld();
            RegisterTarget(world, entity: 2, x: Fix64.One, y: Fix64.FromRatio(3, 2), z: Fix64.Zero);
            CombatProjectileSpawnRequestBuffer buffer = new CombatProjectileSpawnRequestBuffer();
            CombatProjectileRuntime runtime = CreateRuntime(
                buffer,
                world,
                lifetimeFrames: 4,
                motionKind: CombatProjectileMotionKind.ArcToTarget,
                arcHeight: Fix64.FromInt(2));
            buffer.Enqueue(TargetedRequest(
                new CombatFrame(0),
                FixVector3.Zero,
                new FixVector3(Fix64.FromInt(4), Fix64.Zero, Fix64.Zero)));

            runtime.Tick(new CombatFrame(0));
            CombatProjectileFrameEvents hit = runtime.Tick(new CombatFrame(1));

            Assert.AreEqual(1, hit.Impacts.Count);
            Assert.AreEqual(HitResolveKind.Damage, hit.Impacts[0].ResolveResult.Kind);
            Assert.AreEqual(2, hit.Impacts[0].ResolveResult.TargetId.Value);
            Assert.AreEqual(0, runtime.ActiveCount);
        }

        [Test]
        public void BallisticArcToTargetMovement_UsesRangeOptimalAngleAndExpiresAtTargetPoint()
        {
            CombatProjectileSpawnRequestBuffer buffer = new CombatProjectileSpawnRequestBuffer();
            CombatProjectileRuntime runtime = CreateRuntime(
                buffer,
                new CombatPhysicsWorld(),
                lifetimeFrames: 10,
                motionKind: CombatProjectileMotionKind.BallisticArcToTarget,
                gravityPerFrameSquared: Fix64.One,
                speedPerFrame: Fix64.FromInt(2));
            FixVector3 origin = FixVector3.Zero;
            FixVector3 targetPoint = new FixVector3(Fix64.FromInt(2), Fix64.Zero, Fix64.Zero);
            buffer.Enqueue(TargetedRequest(new CombatFrame(0), origin, targetPoint));

            runtime.Tick(new CombatFrame(0));
            runtime.Tick(new CombatFrame(1));
            CombatProjectileSnapshot first = runtime.CreateStateSnapshot().ActiveProjectiles[0];

            Assert.AreEqual(2, first.EffectiveLifetimeFrames);
            Assert.Greater(first.LaunchSpeedPerFrame.RawValue, Fix64.Zero.RawValue);
            Assert.Less(first.LaunchSpeedPerFrame.RawValue, Fix64.FromInt(2).RawValue);
            Assert.Greater(first.Direction.Y.RawValue, Fix64.Zero.RawValue);
            Assert.Greater(first.Position.Y.RawValue, Fix64.Zero.RawValue);

            CombatProjectileFrameEvents expired = runtime.Tick(new CombatFrame(2));

            Assert.AreEqual(0, runtime.ActiveCount);
            Assert.AreEqual(1, expired.Expirations.Count);
            Assert.AreEqual(targetPoint, expired.Expirations[0].Projectile.Position);
        }

        [Test]
        public void BallisticArcToTargetMovement_UsesSpawnRequestEffectiveLifetimeAsCap()
        {
            CombatProjectileSpawnRequestBuffer buffer = new CombatProjectileSpawnRequestBuffer();
            CombatProjectileRuntime runtime = CreateRuntime(
                buffer,
                new CombatPhysicsWorld(),
                lifetimeFrames: 20,
                motionKind: CombatProjectileMotionKind.BallisticArcToTarget,
                gravityPerFrameSquared: Fix64.One,
                speedPerFrame: Fix64.FromInt(2));
            FixVector3 targetPoint = new FixVector3(Fix64.FromInt(2), Fix64.Zero, Fix64.Zero);
            buffer.Enqueue(TargetedRequest(
                new CombatFrame(0),
                FixVector3.Zero,
                targetPoint,
                effectiveLifetimeFrames: 10));

            runtime.Tick(new CombatFrame(0));
            runtime.Tick(new CombatFrame(1));
            CombatProjectileSnapshot halfway = runtime.CreateStateSnapshot().ActiveProjectiles[0];

            Assert.AreEqual(2, halfway.EffectiveLifetimeFrames);
            Assert.Greater(halfway.Position.Y.RawValue, Fix64.Zero.RawValue);

            CombatProjectileFrameEvents expired = runtime.Tick(new CombatFrame(2));

            Assert.AreEqual(0, runtime.ActiveCount);
            Assert.AreEqual(1, expired.Expirations.Count);
            Assert.AreEqual(2, expired.Expirations[0].Projectile.EffectiveLifetimeFrames);
            Assert.AreEqual(targetPoint, expired.Expirations[0].Projectile.Position);
        }

        [Test]
        public void BallisticArcToTargetSweep_HitsTargetAlongCurve()
        {
            CombatPhysicsWorld world = new CombatPhysicsWorld();
            RegisterTarget(world, entity: 2, x: Fix64.One, y: Fix64.Half, z: Fix64.Zero);
            CombatProjectileSpawnRequestBuffer buffer = new CombatProjectileSpawnRequestBuffer();
            CombatProjectileRuntime runtime = CreateRuntime(
                buffer,
                world,
                lifetimeFrames: 10,
                motionKind: CombatProjectileMotionKind.BallisticArcToTarget,
                gravityPerFrameSquared: Fix64.One,
                speedPerFrame: Fix64.FromInt(2));
            buffer.Enqueue(TargetedRequest(
                new CombatFrame(0),
                FixVector3.Zero,
                new FixVector3(Fix64.FromInt(3), Fix64.Zero, Fix64.Zero)));

            runtime.Tick(new CombatFrame(0));
            CombatProjectileFrameEvents hit = runtime.Tick(new CombatFrame(1));

            Assert.AreEqual(1, hit.Impacts.Count);
            Assert.AreEqual(HitResolveKind.Damage, hit.Impacts[0].ResolveResult.Kind);
            Assert.AreEqual(2, hit.Impacts[0].ResolveResult.TargetId.Value);
            Assert.AreEqual(0, runtime.ActiveCount);
        }

        [Test]
        public void BallisticArcToTargetMovement_UsesMaxSpeedAtSameHeightMaxRange()
        {
            CombatProjectileSpawnRequestBuffer buffer = new CombatProjectileSpawnRequestBuffer();
            Fix64 maxSpeed = Fix64.FromRatio(3535533906L, 10000000000L);
            Fix64 gravity = Fix64.FromRatio(1, 120);
            CombatProjectileRuntime runtime = CreateRuntime(
                buffer,
                new CombatPhysicsWorld(),
                lifetimeFrames: 90,
                motionKind: CombatProjectileMotionKind.BallisticArcToTarget,
                gravityPerFrameSquared: gravity,
                speedPerFrame: maxSpeed);
            FixVector3 targetPoint = new FixVector3(Fix64.FromInt(15), Fix64.Zero, Fix64.Zero);
            buffer.Enqueue(TargetedRequest(new CombatFrame(0), FixVector3.Zero, targetPoint));

            runtime.Tick(new CombatFrame(0));
            CombatProjectileSnapshot snapshot = runtime.CreateStateSnapshot().ActiveProjectiles[0];

            Assert.AreEqual(61, snapshot.EffectiveLifetimeFrames);
            AssertFix64Approximately(maxSpeed, snapshot.LaunchSpeedPerFrame, Fix64.FromRatio(1, 100000));
            AssertFix64Approximately(snapshot.Direction.X, snapshot.Direction.Y, Fix64.FromRatio(1, 10000));
        }

        [Test]
        public void BallisticArcToTargetMovement_RecomputesLowerLaunchSpeedForCloserTarget()
        {
            CombatProjectileSpawnRequestBuffer buffer = new CombatProjectileSpawnRequestBuffer();
            Fix64 maxSpeed = Fix64.FromRatio(3535533906L, 10000000000L);
            Fix64 gravity = Fix64.FromRatio(1, 120);
            CombatProjectileRuntime runtime = CreateRuntime(
                buffer,
                new CombatPhysicsWorld(),
                lifetimeFrames: 90,
                motionKind: CombatProjectileMotionKind.BallisticArcToTarget,
                gravityPerFrameSquared: gravity,
                speedPerFrame: maxSpeed);
            buffer.Enqueue(TargetedRequest(
                new CombatFrame(0),
                FixVector3.Zero,
                new FixVector3(Fix64.FromInt(7), Fix64.Zero, Fix64.Zero)));

            runtime.Tick(new CombatFrame(0));
            CombatProjectileSnapshot snapshot = runtime.CreateStateSnapshot().ActiveProjectiles[0];

            Assert.Greater(snapshot.LaunchSpeedPerFrame.RawValue, Fix64.Zero.RawValue);
            Assert.Less(snapshot.LaunchSpeedPerFrame.RawValue, maxSpeed.RawValue);
            Assert.AreEqual(41, snapshot.EffectiveLifetimeFrames);
        }

        [Test]
        public void BallisticArcToTargetMovement_NearTargetArcStaysBelowOldHighArc()
        {
            CombatProjectileSpawnRequestBuffer buffer = new CombatProjectileSpawnRequestBuffer();
            CombatProjectileRuntime runtime = CreateRuntime(
                buffer,
                new CombatPhysicsWorld(),
                lifetimeFrames: 10,
                motionKind: CombatProjectileMotionKind.BallisticArcToTarget,
                gravityPerFrameSquared: Fix64.One,
                speedPerFrame: Fix64.FromInt(2));
            buffer.Enqueue(TargetedRequest(
                new CombatFrame(0),
                FixVector3.Zero,
                new FixVector3(Fix64.One, Fix64.Zero, Fix64.Zero)));

            runtime.Tick(new CombatFrame(0));
            runtime.Tick(new CombatFrame(1));
            CombatProjectileSnapshot snapshot = runtime.CreateStateSnapshot().ActiveProjectiles[0];

            Assert.Less(snapshot.Position.Y.RawValue, Fix64.Half.RawValue);
        }

        [Test]
        public void BallisticArcToTargetMovement_ExplicitMaxAngleReducesReach()
        {
            CombatProjectileSpawnRequestBuffer buffer = new CombatProjectileSpawnRequestBuffer();
            CombatProjectileRuntime runtime = CreateRuntime(
                buffer,
                new CombatPhysicsWorld(),
                lifetimeFrames: 10,
                motionKind: CombatProjectileMotionKind.BallisticArcToTarget,
                gravityPerFrameSquared: Fix64.One,
                speedPerFrame: Fix64.FromInt(2),
                maxLaunchAngleTangent: Fix64.Half);
            buffer.Enqueue(TargetedRequest(
                new CombatFrame(0),
                FixVector3.Zero,
                new FixVector3(Fix64.FromRatio(35, 10), Fix64.Zero, Fix64.Zero)));

            CombatProjectileFrameEvents events = runtime.Tick(new CombatFrame(0));

            Assert.AreEqual(0, runtime.ActiveCount);
            Assert.AreEqual(1, events.Rejections.Count);
        }

        [Test]
        public void BallisticArcToTargetMovement_RejectsUnreachableTarget()
        {
            CombatProjectileSpawnRequestBuffer buffer = new CombatProjectileSpawnRequestBuffer();
            CombatProjectileRuntime runtime = CreateRuntime(
                buffer,
                new CombatPhysicsWorld(),
                lifetimeFrames: 10,
                motionKind: CombatProjectileMotionKind.BallisticArcToTarget,
                gravityPerFrameSquared: Fix64.One,
                speedPerFrame: Fix64.FromInt(2));
            buffer.Enqueue(TargetedRequest(
                new CombatFrame(0),
                FixVector3.Zero,
                new FixVector3(Fix64.FromInt(5), Fix64.Zero, Fix64.Zero)));

            CombatProjectileFrameEvents events = runtime.Tick(new CombatFrame(0));

            Assert.AreEqual(0, runtime.ActiveCount);
            Assert.AreEqual(1, events.Rejections.Count);
        }

        [Test]
        public void BoomerangReturnMovement_ReturnsToOriginAtLifetime()
        {
            CombatProjectileSpawnRequestBuffer buffer = new CombatProjectileSpawnRequestBuffer();
            CombatProjectileRuntime runtime = CreateRuntime(
                buffer,
                new CombatPhysicsWorld(),
                lifetimeFrames: 4,
                motionKind: CombatProjectileMotionKind.BoomerangReturn);
            FixVector3 origin = FixVector3.Zero;
            FixVector3 targetPoint = new FixVector3(Fix64.FromInt(4), Fix64.Zero, Fix64.Zero);
            buffer.Enqueue(TargetedRequest(new CombatFrame(0), origin, targetPoint));

            runtime.Tick(new CombatFrame(0));
            runtime.Tick(new CombatFrame(1));
            Assert.AreEqual(
                new FixVector3(Fix64.FromInt(2), Fix64.Zero, Fix64.Zero),
                runtime.CreateStateSnapshot().ActiveProjectiles[0].Position);

            runtime.Tick(new CombatFrame(2));
            Assert.AreEqual(targetPoint, runtime.CreateStateSnapshot().ActiveProjectiles[0].Position);

            runtime.Tick(new CombatFrame(3));
            Assert.AreEqual(
                new FixVector3(Fix64.FromInt(2), Fix64.Zero, Fix64.Zero),
                runtime.CreateStateSnapshot().ActiveProjectiles[0].Position);

            CombatProjectileFrameEvents expired = runtime.Tick(new CombatFrame(4));
            Assert.AreEqual(0, runtime.ActiveCount);
            Assert.AreEqual(origin, expired.Expirations[0].Projectile.Position);
        }

        [Test]
        public void SwayForwardMovement_AppliesDeterministicLateralTriangleWave()
        {
            CombatProjectileSpawnRequestBuffer buffer = new CombatProjectileSpawnRequestBuffer();
            CombatProjectileRuntime runtime = CreateRuntime(
                buffer,
                new CombatPhysicsWorld(),
                lifetimeFrames: 4,
                motionKind: CombatProjectileMotionKind.SwayForward,
                lateralAmplitude: Fix64.One,
                lateralCycles: 1);
            buffer.Enqueue(TargetedRequest(
                new CombatFrame(0),
                FixVector3.Zero,
                new FixVector3(Fix64.FromInt(4), Fix64.Zero, Fix64.Zero)));

            runtime.Tick(new CombatFrame(0));
            runtime.Tick(new CombatFrame(1));
            Assert.AreEqual(
                new FixVector3(Fix64.One, Fix64.Zero, -Fix64.One),
                runtime.CreateStateSnapshot().ActiveProjectiles[0].Position);

            runtime.Tick(new CombatFrame(2));
            Assert.AreEqual(
                new FixVector3(Fix64.FromInt(2), Fix64.Zero, Fix64.Zero),
                runtime.CreateStateSnapshot().ActiveProjectiles[0].Position);

            runtime.Tick(new CombatFrame(3));
            Assert.AreEqual(
                new FixVector3(Fix64.FromInt(3), Fix64.Zero, Fix64.One),
                runtime.CreateStateSnapshot().ActiveProjectiles[0].Position);
        }

        [Test]
        public void BezierToTargetMovement_SamplesCubicCurveAndExpiresAtTargetPoint()
        {
            CombatProjectileSpawnRequestBuffer buffer = new CombatProjectileSpawnRequestBuffer();
            CombatProjectileRuntime runtime = CreateRuntime(
                buffer,
                new CombatPhysicsWorld(),
                lifetimeFrames: 4,
                motionKind: CombatProjectileMotionKind.BezierToTarget,
                bezierControlOffsetA: new FixVector3(Fix64.Zero, Fix64.FromInt(2), Fix64.Zero),
                bezierControlOffsetB: new FixVector3(Fix64.Zero, Fix64.FromInt(2), Fix64.Zero));
            FixVector3 origin = FixVector3.Zero;
            FixVector3 targetPoint = new FixVector3(Fix64.FromInt(4), Fix64.Zero, Fix64.Zero);
            buffer.Enqueue(TargetedRequest(new CombatFrame(0), origin, targetPoint));

            runtime.Tick(new CombatFrame(0));
            Assert.AreEqual(origin, runtime.CreateStateSnapshot().ActiveProjectiles[0].Position);

            runtime.Tick(new CombatFrame(1));
            runtime.Tick(new CombatFrame(2));
            Assert.AreEqual(
                new FixVector3(Fix64.FromInt(2), Fix64.FromRatio(3, 2), Fix64.Zero),
                runtime.CreateStateSnapshot().ActiveProjectiles[0].Position);

            runtime.Tick(new CombatFrame(3));
            CombatProjectileFrameEvents expired = runtime.Tick(new CombatFrame(4));

            Assert.AreEqual(0, runtime.ActiveCount);
            Assert.AreEqual(1, expired.Expirations.Count);
            Assert.AreEqual(targetPoint, expired.Expirations[0].Projectile.Position);
        }

        [Test]
        public void HomingToTargetMovement_ReorientsTowardTrackingTarget()
        {
            var tracking = new MutableTrackingTargetProvider
            {
                TargetPoint = new FixVector3(Fix64.FromInt(4), Fix64.Zero, Fix64.FromInt(4))
            };
            CombatProjectileSpawnRequestBuffer buffer = new CombatProjectileSpawnRequestBuffer();
            CombatProjectileRuntime runtime = CreateRuntime(
                buffer,
                new CombatPhysicsWorld(),
                lifetimeFrames: 10,
                motionKind: CombatProjectileMotionKind.HomingToTarget,
                trackingTargetProvider: tracking);
            buffer.Enqueue(TrackingRequest(
                new CombatFrame(0),
                FixVector3.Zero,
                new FixVector3(Fix64.FromInt(4), Fix64.Zero, Fix64.Zero),
                new CombatEntityId(2),
                effectiveLifetimeFrames: 10));

            runtime.Tick(new CombatFrame(0));
            runtime.Tick(new CombatFrame(1));
            CombatProjectileSnapshot snapshot = runtime.CreateStateSnapshot().ActiveProjectiles[0];

            Assert.IsTrue(snapshot.HasTrackingTarget);
            Assert.AreEqual(2, snapshot.TrackingTargetEntityId.Value);
            Assert.AreEqual(tracking.TargetPoint, snapshot.TargetPoint);
            Assert.Greater(snapshot.Direction.Z.RawValue, 0L);
            Assert.Greater(snapshot.Position.Z.RawValue, 0L);
        }

        [Test]
        public void HomingToTargetMovement_RespectsDeterministicTurnRate()
        {
            var tracking = new MutableTrackingTargetProvider
            {
                TargetPoint = new FixVector3(Fix64.Zero, Fix64.Zero, Fix64.FromInt(10))
            };
            CombatProjectileSpawnRequestBuffer buffer = new CombatProjectileSpawnRequestBuffer();
            CombatProjectileRuntime runtime = CreateRuntime(
                buffer,
                new CombatPhysicsWorld(),
                lifetimeFrames: 10,
                motionKind: CombatProjectileMotionKind.HomingToTarget,
                homingTurnRatePerFrame: Fix64.Half,
                trackingTargetProvider: tracking);
            buffer.Enqueue(TrackingRequest(
                new CombatFrame(0),
                FixVector3.Zero,
                new FixVector3(Fix64.FromInt(4), Fix64.Zero, Fix64.Zero),
                new CombatEntityId(2),
                effectiveLifetimeFrames: 10));

            runtime.Tick(new CombatFrame(0));
            runtime.Tick(new CombatFrame(1));
            CombatProjectileSnapshot snapshot = runtime.CreateStateSnapshot().ActiveProjectiles[0];

            Assert.Greater(snapshot.Direction.X.RawValue, 0L);
            Assert.Greater(snapshot.Direction.Z.RawValue, 0L);
            Assert.Less(snapshot.Direction.X.RawValue, Fix64.One.RawValue);
            Assert.Less(snapshot.Direction.Z.RawValue, Fix64.One.RawValue);
            Assert.Greater(snapshot.Position.X.RawValue, 0L);
            Assert.Greater(snapshot.Position.Z.RawValue, 0L);
            Assert.Less(snapshot.Position.Z.RawValue, Fix64.Half.RawValue);
        }

        [Test]
        public void HomingToTargetMovement_ContinuesForwardWhenTrackingTargetCannotResolve()
        {
            var tracking = new MutableTrackingTargetProvider
            {
                Enabled = false,
                TargetPoint = new FixVector3(Fix64.FromInt(4), Fix64.Zero, Fix64.FromInt(4))
            };
            CombatProjectileSpawnRequestBuffer buffer = new CombatProjectileSpawnRequestBuffer();
            CombatProjectileRuntime runtime = CreateRuntime(
                buffer,
                new CombatPhysicsWorld(),
                lifetimeFrames: 10,
                motionKind: CombatProjectileMotionKind.HomingToTarget,
                trackingTargetProvider: tracking);
            buffer.Enqueue(TrackingRequest(
                new CombatFrame(0),
                FixVector3.Zero,
                new FixVector3(Fix64.FromInt(4), Fix64.Zero, Fix64.Zero),
                new CombatEntityId(2),
                effectiveLifetimeFrames: 10));

            runtime.Tick(new CombatFrame(0));
            runtime.Tick(new CombatFrame(1));
            CombatProjectileSnapshot snapshot = runtime.CreateStateSnapshot().ActiveProjectiles[0];

            Assert.IsTrue(snapshot.HasTrackingTarget);
            Assert.AreEqual(new FixVector3(Fix64.One, Fix64.Zero, Fix64.Zero), snapshot.Direction);
            Assert.AreEqual(new FixVector3(Fix64.Half, Fix64.Zero, Fix64.Zero), snapshot.Position);
            Assert.AreEqual(new FixVector3(Fix64.FromInt(4), Fix64.Zero, Fix64.Zero), snapshot.TargetPoint);
        }

        [Test]
        public void CapsuleSweep_HitsNearestTargetAndConsumesProjectile()
        {
            CombatPhysicsWorld world = new CombatPhysicsWorld();
            RegisterTarget(world, entity: 2, x: Fix64.One);
            RegisterTarget(world, entity: 3, x: Fix64.FromInt(2));
            CombatProjectileSpawnRequestBuffer buffer = new CombatProjectileSpawnRequestBuffer();
            CombatProjectileRuntime runtime = CreateRuntime(buffer, world, lifetimeFrames: 10);
            buffer.Enqueue(Request(new CombatFrame(0)));

            runtime.Tick(new CombatFrame(0));
            runtime.Tick(new CombatFrame(1));
            CombatProjectileFrameEvents hit = runtime.Tick(new CombatFrame(2));

            Assert.AreEqual(1, hit.Impacts.Count);
            Assert.AreEqual(HitResolveKind.Damage, hit.Impacts[0].ResolveResult.Kind);
            Assert.AreEqual(2, hit.Impacts[0].ResolveResult.TargetId.Value);
            Assert.IsTrue(hit.Impacts[0].ConsumedProjectile);
            Assert.AreEqual(0, runtime.ActiveCount);
        }

        [Test]
        public void FriendlyAndDeadTargets_DoNotConsumeProjectile()
        {
            CombatPhysicsWorld friendlyWorld = new CombatPhysicsWorld();
            RegisterTarget(friendlyWorld, entity: 2, x: Fix64.One);
            CombatProjectileSpawnRequestBuffer friendlyBuffer = new CombatProjectileSpawnRequestBuffer();
            CombatProjectileRuntime friendlyRuntime = CreateRuntime(
                friendlyBuffer,
                friendlyWorld,
                lifetimeFrames: 10,
                teamRelationProvider: FriendlyTeams.Instance);
            friendlyBuffer.Enqueue(Request(new CombatFrame(0)));

            friendlyRuntime.Tick(new CombatFrame(0));
            friendlyRuntime.Tick(new CombatFrame(1));
            CombatProjectileFrameEvents friendlyHit = friendlyRuntime.Tick(new CombatFrame(2));

            Assert.AreEqual(1, friendlyHit.Impacts.Count);
            Assert.AreEqual(HitResolveKind.Friendly, friendlyHit.Impacts[0].ResolveResult.Kind);
            Assert.AreEqual(1, friendlyRuntime.ActiveCount);

            CombatPhysicsWorld deadWorld = new CombatPhysicsWorld();
            RegisterTarget(deadWorld, entity: 2, x: Fix64.One);
            CombatProjectileSpawnRequestBuffer deadBuffer = new CombatProjectileSpawnRequestBuffer();
            CombatProjectileRuntime deadRuntime = CreateRuntime(
                deadBuffer,
                deadWorld,
                lifetimeFrames: 10,
                targetStateResolver: new FixedTargetState(HitTargetStateFlags.None));
            deadBuffer.Enqueue(Request(new CombatFrame(0)));

            deadRuntime.Tick(new CombatFrame(0));
            deadRuntime.Tick(new CombatFrame(1));
            CombatProjectileFrameEvents deadHit = deadRuntime.Tick(new CombatFrame(2));

            Assert.AreEqual(1, deadHit.Impacts.Count);
            Assert.AreEqual(HitResolveKind.TargetDead, deadHit.Impacts[0].ResolveResult.Kind);
            Assert.AreEqual(1, deadRuntime.ActiveCount);
        }

        [Test]
        public void AreaPulseOnImpact_HitsMultipleHostilesAndConsumesProjectile()
        {
            CombatPhysicsWorld world = new CombatPhysicsWorld();
            RegisterTarget(world, entity: 2, x: Fix64.One);
            RegisterTarget(world, entity: 3, x: Fix64.One, z: Fix64.One);
            RegisterTarget(world, entity: 4, x: Fix64.FromInt(4));
            CombatProjectileSpawnRequestBuffer buffer = new CombatProjectileSpawnRequestBuffer();
            CombatProjectileRuntime runtime = CreateRuntime(
                buffer,
                world,
                lifetimeFrames: 10,
                areaPulse: true);
            buffer.Enqueue(Request(new CombatFrame(0)));

            runtime.Tick(new CombatFrame(0));
            runtime.Tick(new CombatFrame(1));
            CombatProjectileFrameEvents hit = runtime.Tick(new CombatFrame(2));

            Assert.AreEqual(2, hit.Impacts.Count);
            Assert.AreEqual(1, hit.AreaPulseTriggers.Count);
            Assert.AreEqual(CombatProjectileAreaPulseTriggerKind.DirectSweep, hit.AreaPulseTriggers[0].TriggerKind);
            Assert.AreEqual(
                new FixVector3(Fix64.FromRatio(4, 5), Fix64.Zero, Fix64.Zero),
                hit.AreaPulseTriggers[0].Center);
            Assert.IsTrue(hit.AreaPulseTriggers[0].ConsumedProjectile);
            Assert.IsTrue(hit.AreaPulseTriggers[0].HasDirectHit);
            Assert.AreEqual(HitResolveKind.Damage, hit.Impacts[0].ResolveResult.Kind);
            Assert.AreEqual(2, hit.Impacts[0].ResolveResult.TargetId.Value);
            Assert.AreEqual(HitResolveKind.Damage, hit.Impacts[1].ResolveResult.Kind);
            Assert.AreEqual(3, hit.Impacts[1].ResolveResult.TargetId.Value);
            Assert.IsTrue(hit.Impacts[0].ConsumedProjectile);
            Assert.IsTrue(hit.Impacts[1].ConsumedProjectile);
            Assert.AreEqual(0, runtime.ActiveCount);
        }

        [Test]
        public void AreaPulseOnImpact_ResolvesAreaHitsWithAreaPulseContext()
        {
            CombatPhysicsWorld world = new CombatPhysicsWorld();
            RegisterTarget(world, entity: 2, x: Fix64.One);
            RegisterTarget(world, entity: 3, x: Fix64.One, z: Fix64.One);
            CombatProjectileSpawnRequestBuffer buffer = new CombatProjectileSpawnRequestBuffer();
            var resolver = new ContextualImpactResolver();
            var runtime = new CombatProjectileRuntime(
                CreateDefinitions(lifetimeFrames: 10, areaPulse: true),
                buffer,
                world,
                resolver);
            buffer.Enqueue(Request(new CombatFrame(0)));

            runtime.Tick(new CombatFrame(0));
            runtime.Tick(new CombatFrame(1));
            CombatProjectileFrameEvents hit = runtime.Tick(new CombatFrame(2));

            Assert.AreEqual(2, hit.Impacts.Count);
            Assert.AreEqual(1, hit.AreaPulseTriggers.Count);
            Assert.AreEqual(1, resolver.DirectSweepCount);
            Assert.AreEqual(2, resolver.AreaPulseCount);
            Assert.AreEqual(30, hit.Impacts[0].Payload.Damage);
            Assert.AreEqual("area", hit.Impacts[0].Payload.StableEventId);
            Assert.AreEqual(30, hit.Impacts[1].Payload.Damage);
            Assert.AreEqual("area", hit.Impacts[1].Payload.StableEventId);
        }

        [Test]
        public void AreaPulseOnImpact_TriggersWhenDirectSweepIsBlocked()
        {
            CombatPhysicsWorld world = new CombatPhysicsWorld();
            RegisterTargetOnLayer(world, entity: 90, layer: 1, x: Fix64.One, y: Fix64.Zero, z: Fix64.Zero);
            RegisterTargetOnLayer(
                world,
                entity: 2,
                layer: 2,
                x: Fix64.One,
                y: Fix64.Zero,
                z: Fix64.Half);
            CombatProjectileSpawnRequestBuffer buffer = new CombatProjectileSpawnRequestBuffer();
            var resolver = new EnvironmentBlockedAreaPulseResolver(new CombatEntityId(90));
            var runtime = new CombatProjectileRuntime(
                CreateEnvironmentBlockedAreaPulseDefinitions(),
                buffer,
                world,
                resolver);
            buffer.Enqueue(Request(new CombatFrame(0)));

            runtime.Tick(new CombatFrame(0));
            runtime.Tick(new CombatFrame(1));
            CombatProjectileFrameEvents hit = runtime.Tick(new CombatFrame(2));

            Assert.AreEqual(1, resolver.DirectSweepCount);
            Assert.AreEqual(1, resolver.AreaPulseCount);
            Assert.AreEqual(1, hit.AreaPulseTriggers.Count);
            Assert.AreEqual(CombatProjectileAreaPulseTriggerKind.DirectSweep, hit.AreaPulseTriggers[0].TriggerKind);
            Assert.IsTrue(hit.AreaPulseTriggers[0].HasDirectHit);
            Assert.AreEqual(HitResolveKind.Blocked, hit.AreaPulseTriggers[0].DirectResolveResult.Kind);
            Assert.AreEqual(90, hit.AreaPulseTriggers[0].DirectResolveResult.TargetId.Value);
            Assert.AreEqual(1, hit.Impacts.Count);
            Assert.AreEqual(HitResolveKind.Damage, hit.Impacts[0].ResolveResult.Kind);
            Assert.AreEqual(2, hit.Impacts[0].ResolveResult.TargetId.Value);
            Assert.AreEqual(20, hit.Impacts[0].Payload.Damage);
            Assert.IsTrue(hit.Impacts[0].ConsumedProjectile);
            Assert.AreEqual(0, runtime.ActiveCount);
        }

        [Test]
        public void AreaPulseOnImpact_EmitsTriggerWhenBlockedEvenWithoutAreaTargets()
        {
            CombatPhysicsWorld world = new CombatPhysicsWorld();
            RegisterTargetOnLayer(world, entity: 90, layer: 1, x: Fix64.One, y: Fix64.Zero, z: Fix64.Zero);
            CombatProjectileSpawnRequestBuffer buffer = new CombatProjectileSpawnRequestBuffer();
            var resolver = new EnvironmentBlockedAreaPulseResolver(new CombatEntityId(90));
            var runtime = new CombatProjectileRuntime(
                CreateEnvironmentBlockedAreaPulseDefinitions(),
                buffer,
                world,
                resolver);
            buffer.Enqueue(Request(new CombatFrame(0)));

            runtime.Tick(new CombatFrame(0));
            runtime.Tick(new CombatFrame(1));
            CombatProjectileFrameEvents hit = runtime.Tick(new CombatFrame(2));

            Assert.AreEqual(1, resolver.DirectSweepCount);
            Assert.AreEqual(0, resolver.AreaPulseCount);
            Assert.AreEqual(1, hit.AreaPulseTriggers.Count);
            Assert.AreEqual(0, hit.Impacts.Count);
            Assert.AreEqual(CombatProjectileAreaPulseTriggerKind.DirectSweep, hit.AreaPulseTriggers[0].TriggerKind);
            Assert.AreEqual(
                new FixVector3(Fix64.FromRatio(4, 5), Fix64.Zero, Fix64.Zero),
                hit.AreaPulseTriggers[0].Center);
            Assert.IsTrue(hit.AreaPulseTriggers[0].ConsumedProjectile);
            Assert.IsTrue(hit.AreaPulseTriggers[0].HasDirectHit);
            Assert.AreEqual(HitResolveKind.Blocked, hit.AreaPulseTriggers[0].DirectResolveResult.Kind);
            Assert.AreEqual(0, runtime.ActiveCount);
        }

        [Test]
        public void AreaPulseOnImpact_ResultOrderIsStableAcrossPhysicsRegistrationOrder()
        {
            int[] forward = RunAreaPulseForRegisteredTargets(2, 3);
            int[] reverse = RunAreaPulseForRegisteredTargets(3, 2);

            CollectionAssert.AreEqual(new[] { 2, 3 }, forward);
            CollectionAssert.AreEqual(forward, reverse);
        }

        [Test]
        public void AreaPulseOnImpact_FiltersFriendlyAndDeadTargetsThroughHitResolve()
        {
            CombatPhysicsWorld world = new CombatPhysicsWorld();
            RegisterTarget(world, entity: 2, x: Fix64.One);
            RegisterTarget(world, entity: 3, x: Fix64.One, z: Fix64.One);
            RegisterTarget(world, entity: 4, x: Fix64.One, z: -Fix64.One);
            CombatProjectileSpawnRequestBuffer buffer = new CombatProjectileSpawnRequestBuffer();
            CombatProjectileRuntime runtime = CreateRuntime(
                buffer,
                world,
                lifetimeFrames: 10,
                areaPulse: true,
                teamRelationProvider: new SelectiveFriendlyTeams(new CombatEntityId(3)),
                targetStateResolver: new SelectiveDeadTargetState(new CombatEntityId(4)));
            buffer.Enqueue(Request(new CombatFrame(0)));

            runtime.Tick(new CombatFrame(0));
            runtime.Tick(new CombatFrame(1));
            CombatProjectileFrameEvents hit = runtime.Tick(new CombatFrame(2));

            Assert.AreEqual(3, hit.Impacts.Count);
            Assert.AreEqual(HitResolveKind.Damage, hit.Impacts[0].ResolveResult.Kind);
            Assert.AreEqual(HitResolveKind.Friendly, hit.Impacts[1].ResolveResult.Kind);
            Assert.AreEqual(HitResolveKind.TargetDead, hit.Impacts[2].ResolveResult.Kind);
            Assert.AreEqual(0, runtime.ActiveCount);
        }

        [Test]
        public void AreaPulseOnImpact_TriggersOnLifetimeExpireAtFinalPosition()
        {
            CombatPhysicsWorld world = new CombatPhysicsWorld();
            RegisterTarget(world, entity: 2, x: Fix64.FromRatio(3, 2));
            CombatProjectileSpawnRequestBuffer buffer = new CombatProjectileSpawnRequestBuffer();
            CombatProjectileRuntime runtime = CreateRuntime(
                buffer,
                world,
                lifetimeFrames: 2,
                areaPulse: true);
            buffer.Enqueue(Request(new CombatFrame(0)));

            runtime.Tick(new CombatFrame(0));
            runtime.Tick(new CombatFrame(1));
            CombatProjectileFrameEvents expired = runtime.Tick(new CombatFrame(2));

            Assert.AreEqual(1, expired.Expirations.Count);
            Assert.AreEqual(new FixVector3(Fix64.One, Fix64.Zero, Fix64.Zero), expired.Expirations[0].Projectile.Position);
            Assert.AreEqual(1, expired.AreaPulseTriggers.Count);
            Assert.AreEqual(CombatProjectileAreaPulseTriggerKind.Expiration, expired.AreaPulseTriggers[0].TriggerKind);
            Assert.AreEqual(new FixVector3(Fix64.One, Fix64.Zero, Fix64.Zero), expired.AreaPulseTriggers[0].Center);
            Assert.IsFalse(expired.AreaPulseTriggers[0].HasDirectHit);
            Assert.AreEqual(1, expired.Impacts.Count);
            Assert.AreEqual(HitResolveKind.Damage, expired.Impacts[0].ResolveResult.Kind);
            Assert.AreEqual(2, expired.Impacts[0].ResolveResult.TargetId.Value);
            Assert.AreEqual(0, runtime.ActiveCount);
        }

        [Test]
        public void PiercingDirectHit_HitsMultipleHostilesInStableOrderAndStopsAtLimit()
        {
            CombatPhysicsWorld world = new CombatPhysicsWorld();
            RegisterTarget(world, entity: 3, x: Fix64.FromRatio(9, 10));
            RegisterTarget(world, entity: 2, x: Fix64.FromRatio(3, 4));
            RegisterTarget(world, entity: 4, x: Fix64.FromRatio(11, 10));
            CombatProjectileSpawnRequestBuffer buffer = new CombatProjectileSpawnRequestBuffer();
            CombatProjectileRuntime runtime = CreateRuntime(
                buffer,
                world,
                lifetimeFrames: 10,
                piercingMaxAcceptedImpacts: 2);
            buffer.Enqueue(Request(new CombatFrame(0)));

            runtime.Tick(new CombatFrame(0));
            runtime.Tick(new CombatFrame(1));
            CombatProjectileFrameEvents hit = runtime.Tick(new CombatFrame(2));

            Assert.AreEqual(2, hit.Impacts.Count);
            Assert.AreEqual(2, hit.Impacts[0].ResolveResult.TargetId.Value);
            Assert.AreEqual(3, hit.Impacts[1].ResolveResult.TargetId.Value);
            Assert.IsFalse(hit.Impacts[0].ConsumedProjectile);
            Assert.IsTrue(hit.Impacts[1].ConsumedProjectile);
            Assert.AreEqual(1, hit.Impacts[0].Projectile.AcceptedImpactCount);
            Assert.AreEqual(2, hit.Impacts[1].Projectile.AcceptedImpactCount);
            Assert.AreEqual(0, runtime.ActiveCount);
        }

        [Test]
        public void PiercingDirectHit_DoesNotDamageSameTargetAcrossOverlappingFrames()
        {
            CombatPhysicsWorld world = new CombatPhysicsWorld();
            RegisterTarget(world, entity: 2, x: Fix64.FromRatio(105, 100));
            CombatProjectileSpawnRequestBuffer buffer = new CombatProjectileSpawnRequestBuffer();
            CombatProjectileRuntime runtime = CreateRuntime(
                buffer,
                world,
                lifetimeFrames: 10,
                piercingMaxAcceptedImpacts: 3);
            buffer.Enqueue(Request(new CombatFrame(0)));

            runtime.Tick(new CombatFrame(0));
            runtime.Tick(new CombatFrame(1));
            CombatProjectileFrameEvents firstHit = runtime.Tick(new CombatFrame(2));
            CombatProjectileFrameEvents overlappingFrame = runtime.Tick(new CombatFrame(3));

            Assert.AreEqual(1, firstHit.Impacts.Count);
            Assert.AreEqual(HitResolveKind.Damage, firstHit.Impacts[0].ResolveResult.Kind);
            Assert.IsFalse(firstHit.Impacts[0].ConsumedProjectile);
            Assert.AreEqual(1, runtime.ActiveCount);
            Assert.AreEqual(0, overlappingFrame.Impacts.Count);
            CombatProjectileRuntimeState state = runtime.CreateStateSnapshot();
            Assert.AreEqual(1, state.ActiveProjectiles[0].AcceptedImpactCount);
            Assert.AreEqual(2, state.ActiveProjectiles[0].AcceptedImpacts[0].TargetEntityId.Value);
        }

        [Test]
        public void DirectSweepProvider_ReturningNoQueriesUsesDefaultCenterCapsule()
        {
            CombatPhysicsWorld world = new CombatPhysicsWorld();
            RegisterTarget(world, entity: 2, x: Fix64.Half);
            CombatProjectileSpawnRequestBuffer buffer = new CombatProjectileSpawnRequestBuffer();
            CombatProjectileRuntime runtime = CreateRuntime(
                buffer,
                world,
                lifetimeFrames: 10,
                directSweepProvider: new TestDirectSweepProvider(FixVector3.Zero, queryCount: 0));
            buffer.Enqueue(Request(new CombatFrame(0)));

            runtime.Tick(new CombatFrame(0));
            CombatProjectileFrameEvents hit = runtime.Tick(new CombatFrame(1));

            Assert.AreEqual(1, hit.Impacts.Count);
            Assert.AreEqual(HitResolveKind.Damage, hit.Impacts[0].ResolveResult.Kind);
            Assert.AreEqual(2, hit.Impacts[0].ResolveResult.TargetId.Value);
            Assert.AreEqual(0, runtime.ActiveCount);
        }

        [Test]
        public void DirectSweepProvider_CanHitTargetOutsideCenterCapsule()
        {
            CombatPhysicsWorld world = new CombatPhysicsWorld();
            RegisterTarget(world, entity: 2, x: Fix64.Half, z: Fix64.FromRatio(85, 100));
            CombatProjectileSpawnRequestBuffer buffer = new CombatProjectileSpawnRequestBuffer();
            CombatProjectileRuntime runtime = CreateRuntime(
                buffer,
                world,
                lifetimeFrames: 10,
                directSweepProvider: new TestDirectSweepProvider(
                    new FixVector3(Fix64.Zero, Fix64.Zero, Fix64.FromRatio(85, 100)),
                    queryCount: 1));
            buffer.Enqueue(Request(new CombatFrame(0)));

            runtime.Tick(new CombatFrame(0));
            CombatProjectileFrameEvents hit = runtime.Tick(new CombatFrame(1));

            Assert.AreEqual(1, hit.Impacts.Count);
            Assert.AreEqual(HitResolveKind.Damage, hit.Impacts[0].ResolveResult.Kind);
            Assert.AreEqual(2, hit.Impacts[0].ResolveResult.TargetId.Value);
            Assert.AreEqual(0, runtime.ActiveCount);
        }

        [Test]
        public void DirectSweepProvider_SameTargetCoveredByMultipleSamplesEmitsOneImpact()
        {
            CombatPhysicsWorld world = new CombatPhysicsWorld();
            RegisterTarget(world, entity: 2, x: Fix64.Half, z: Fix64.FromRatio(85, 100));
            CombatProjectileSpawnRequestBuffer buffer = new CombatProjectileSpawnRequestBuffer();
            CombatProjectileRuntime runtime = CreateRuntime(
                buffer,
                world,
                lifetimeFrames: 10,
                piercingMaxAcceptedImpacts: 4,
                directSweepProvider: new TestDirectSweepProvider(
                    new FixVector3(Fix64.Zero, Fix64.Zero, Fix64.FromRatio(85, 100)),
                    queryCount: 2));
            buffer.Enqueue(Request(new CombatFrame(0)));

            runtime.Tick(new CombatFrame(0));
            CombatProjectileFrameEvents hit = runtime.Tick(new CombatFrame(1));

            Assert.AreEqual(1, hit.Impacts.Count);
            Assert.AreEqual(HitResolveKind.Damage, hit.Impacts[0].ResolveResult.Kind);
            Assert.AreEqual(1, hit.Impacts[0].Projectile.AcceptedImpactCount);
            Assert.IsFalse(hit.Impacts[0].ConsumedProjectile);
            Assert.AreEqual(1, runtime.ActiveCount);
        }

        [Test]
        public void DirectSweepProvider_BlockedSampleConsumesProjectile()
        {
            CombatPhysicsWorld world = new CombatPhysicsWorld();
            RegisterTarget(world, entity: 2, x: Fix64.Half, z: Fix64.FromRatio(85, 100));
            CombatProjectileSpawnRequestBuffer buffer = new CombatProjectileSpawnRequestBuffer();
            CombatProjectileRuntime runtime = CreateRuntime(
                buffer,
                world,
                lifetimeFrames: 10,
                piercingMaxAcceptedImpacts: 4,
                targetStateResolver: new FixedTargetState(HitTargetStateFlags.Alive | HitTargetStateFlags.Blocking),
                directSweepProvider: new TestDirectSweepProvider(
                    new FixVector3(Fix64.Zero, Fix64.Zero, Fix64.FromRatio(85, 100)),
                    queryCount: 1));
            buffer.Enqueue(Request(new CombatFrame(0)));

            runtime.Tick(new CombatFrame(0));
            CombatProjectileFrameEvents hit = runtime.Tick(new CombatFrame(1));

            Assert.AreEqual(1, hit.Impacts.Count);
            Assert.AreEqual(HitResolveKind.Blocked, hit.Impacts[0].ResolveResult.Kind);
            Assert.IsTrue(hit.Impacts[0].ConsumedProjectile);
            Assert.AreEqual(0, runtime.ActiveCount);
        }

        [Test]
        public void PiercingDirectHit_FriendlyAndDeadTargetsDoNotConsumeAcceptedImpactLimit()
        {
            CombatPhysicsWorld world = new CombatPhysicsWorld();
            RegisterTarget(world, entity: 2, x: Fix64.FromRatio(3, 4));
            RegisterTarget(world, entity: 3, x: Fix64.FromRatio(9, 10));
            RegisterTarget(world, entity: 4, x: Fix64.FromRatio(95, 100));
            CombatProjectileSpawnRequestBuffer buffer = new CombatProjectileSpawnRequestBuffer();
            CombatProjectileRuntime runtime = CreateRuntime(
                buffer,
                world,
                lifetimeFrames: 10,
                piercingMaxAcceptedImpacts: 1,
                teamRelationProvider: new SelectiveFriendlyTeams(new CombatEntityId(2)),
                targetStateResolver: new SelectiveDeadTargetState(new CombatEntityId(3)));
            buffer.Enqueue(Request(new CombatFrame(0)));

            runtime.Tick(new CombatFrame(0));
            runtime.Tick(new CombatFrame(1));
            CombatProjectileFrameEvents hit = runtime.Tick(new CombatFrame(2));

            Assert.AreEqual(3, hit.Impacts.Count);
            Assert.AreEqual(HitResolveKind.Friendly, hit.Impacts[0].ResolveResult.Kind);
            Assert.AreEqual(HitResolveKind.TargetDead, hit.Impacts[1].ResolveResult.Kind);
            Assert.AreEqual(HitResolveKind.Damage, hit.Impacts[2].ResolveResult.Kind);
            Assert.AreEqual(1, hit.Impacts[2].Projectile.AcceptedImpactCount);
            Assert.IsTrue(hit.Impacts[2].ConsumedProjectile);
            Assert.AreEqual(0, runtime.ActiveCount);
        }

        [Test]
        public void OneFramePiercingSweep_HitsHostilesSkipsRejectedTargetsAndExpires()
        {
            CombatPhysicsWorld world = new CombatPhysicsWorld();
            RegisterTarget(world, entity: 1, x: Fix64.FromInt(1));
            RegisterTarget(world, entity: 2, x: Fix64.FromInt(5));
            RegisterTarget(world, entity: 3, x: Fix64.FromInt(12));
            RegisterTarget(world, entity: 4, x: Fix64.FromInt(18));
            RegisterTarget(world, entity: 5, x: Fix64.FromInt(24));
            RegisterTarget(world, entity: 6, x: Fix64.FromInt(30));
            world.UpsertAabbCollider(new CombatPhysicsAabbCollider(
                new CombatBodyId(3),
                new CombatColliderId(2),
                layer: 1,
                localMin: new FixVector3(-Fix64.FromRatio(1, 10), -Fix64.FromRatio(1, 10), -Fix64.FromRatio(1, 10)),
                localMax: new FixVector3(Fix64.FromRatio(1, 10), Fix64.FromRatio(1, 10), Fix64.FromRatio(1, 10))));
            CombatProjectileSpawnRequestBuffer buffer = new CombatProjectileSpawnRequestBuffer();
            CombatProjectileRuntime runtime = CreateLaserLanceRuntime(
                buffer,
                world,
                teamRelationProvider: new SelectiveFriendlyTeams(new CombatEntityId(4)),
                targetStateResolver: new SelectiveDeadTargetState(new CombatEntityId(5)));
            buffer.Enqueue(Request(new CombatFrame(0)));

            runtime.Tick(new CombatFrame(0));
            CombatProjectileFrameEvents sweep = runtime.Tick(new CombatFrame(1));
            long hash = RuntimeHashCombiner.ComputeHash(
                RuntimeFrame.Zero,
                new IRuntimeHashContributor[] { new CombatProjectileRuntimeHashContributor(runtime) });

            var accepted = new List<int>();
            for (int i = 0; i < sweep.Impacts.Count; i++)
            {
                if (sweep.Impacts[i].ResolveResult.Kind == HitResolveKind.Damage)
                    accepted.Add(sweep.Impacts[i].ResolveResult.TargetId.Value);
            }

            CollectionAssert.AreEqual(new[] { 2, 3, 6 }, accepted);
            CollectionAssert.DoesNotContain(accepted, 1);
            Assert.That(sweep.Impacts, Has.Some.Matches<CombatProjectileImpactEvent>(
                impact => impact.ResolveResult.Kind == HitResolveKind.Friendly));
            Assert.That(sweep.Impacts, Has.Some.Matches<CombatProjectileImpactEvent>(
                impact => impact.ResolveResult.Kind == HitResolveKind.TargetDead));
            Assert.AreEqual(0, runtime.ActiveCount);
            Assert.AreEqual(
                hash,
                RuntimeHashCombiner.ComputeHash(
                    RuntimeFrame.Zero,
                    new IRuntimeHashContributor[] { new CombatProjectileRuntimeHashContributor(runtime) }));
        }

        [Test]
        public void Hash_IsStableForEquivalentStateAndChangesWhenStateChanges()
        {
            CombatProjectileRuntime first = CreateRuntimeWithActiveProjectile(out _);
            CombatProjectileRuntime second = CreateRuntimeWithActiveProjectile(out _);

            long firstHash = RuntimeHashCombiner.ComputeHash(
                RuntimeFrame.Zero,
                new IRuntimeHashContributor[] { new CombatProjectileRuntimeHashContributor(first) });
            long secondHash = RuntimeHashCombiner.ComputeHash(
                RuntimeFrame.Zero,
                new IRuntimeHashContributor[] { new CombatProjectileRuntimeHashContributor(second) });

            Assert.AreEqual(firstHash, secondHash);

            first.Tick(new CombatFrame(2));
            long changedHash = RuntimeHashCombiner.ComputeHash(
                RuntimeFrame.Zero,
                new IRuntimeHashContributor[] { new CombatProjectileRuntimeHashContributor(first) });

            Assert.AreNotEqual(firstHash, changedHash);
        }

        [Test]
        public void DefinitionHash_ChangesWhenImpactPolicyChanges()
        {
            CombatProjectileDefinitionRegistry direct = CreateDefinitions(lifetimeFrames: 10);
            CombatProjectileDefinitionRegistry area = CreateDefinitions(lifetimeFrames: 10, areaPulse: true);

            Assert.AreNotEqual(direct.ComputeStableHash(), area.ComputeStableHash());
        }

        [Test]
        public void DefinitionHash_ChangesWhenPiercePolicyChanges()
        {
            CombatProjectileDefinitionRegistry direct = CreateDefinitions(lifetimeFrames: 10);
            CombatProjectileDefinitionRegistry piercing = CreateDefinitions(
                lifetimeFrames: 10,
                piercingMaxAcceptedImpacts: 3);

            Assert.AreNotEqual(direct.ComputeStableHash(), piercing.ComputeStableHash());
        }

        [Test]
        public void DefinitionHash_ChangesWhenMotionKindChanges()
        {
            CombatProjectileDefinitionRegistry linear = CreateDefinitions(lifetimeFrames: 10);
            CombatProjectileDefinitionRegistry arc = CreateDefinitions(
                lifetimeFrames: 10,
                motionKind: CombatProjectileMotionKind.ArcToTarget,
                arcHeight: Fix64.FromInt(2));

            Assert.AreNotEqual(linear.ComputeStableHash(), arc.ComputeStableHash());
        }

        [Test]
        public void DefinitionHash_ChangesWhenSwayLateralConfigChanges()
        {
            CombatProjectileDefinitionRegistry narrow = CreateDefinitions(
                lifetimeFrames: 10,
                motionKind: CombatProjectileMotionKind.SwayForward,
                lateralAmplitude: Fix64.Half,
                lateralCycles: 1);
            CombatProjectileDefinitionRegistry wide = CreateDefinitions(
                lifetimeFrames: 10,
                motionKind: CombatProjectileMotionKind.SwayForward,
                lateralAmplitude: Fix64.One,
                lateralCycles: 2);

            Assert.AreNotEqual(narrow.ComputeStableHash(), wide.ComputeStableHash());
        }

        [Test]
        public void DefinitionHash_ChangesWhenSpeedProfileChanges()
        {
            CombatProjectileDefinitionRegistry constant = CreateDefinitions(lifetimeFrames: 10);
            CombatProjectileDefinitionRegistry easeIn = CreateDefinitions(
                lifetimeFrames: 10,
                speedProfileKind: CombatProjectileSpeedProfileKind.EaseInQuadratic);

            Assert.AreNotEqual(constant.ComputeStableHash(), easeIn.ComputeStableHash());
        }

        [Test]
        public void SaveState_RoundtripPreservesActiveProjectilesAndHash()
        {
            CombatProjectileRuntime runtime = CreateRuntimeWithActiveProjectile(out CombatProjectileDefinitionRegistry definitions);
            RuntimeSaveState saveState = new CombatProjectileRuntimeSaveStateProvider(runtime).CaptureSaveState().Value;

            CombatProjectileSpawnRequestBuffer restoredBuffer = new CombatProjectileSpawnRequestBuffer();
            CombatProjectileRuntime restored = new CombatProjectileRuntime(
                definitions,
                restoredBuffer,
                new CombatPhysicsWorld(),
                FixedImpactResolver.Instance);
            RuntimeSaveStateResult<bool> restoreResult =
                new CombatProjectileRuntimeSaveStateProvider(restored).RestoreSaveState(saveState);

            Assert.IsTrue(restoreResult.Success, restoreResult.Error.ToString());
            Assert.AreEqual(runtime.CreateStateSnapshot().ActiveCount, restored.CreateStateSnapshot().ActiveCount);

            long originalHash = RuntimeHashCombiner.ComputeHash(
                RuntimeFrame.Zero,
                new IRuntimeHashContributor[] { new CombatProjectileRuntimeHashContributor(runtime) });
            long restoredHash = RuntimeHashCombiner.ComputeHash(
                RuntimeFrame.Zero,
                new IRuntimeHashContributor[] { new CombatProjectileRuntimeHashContributor(restored) });
            Assert.AreEqual(originalHash, restoredHash);
        }

        [Test]
        public void SaveState_RoundtripPreservesOriginAndTargetPoint()
        {
            CombatProjectileSpawnRequestBuffer buffer = new CombatProjectileSpawnRequestBuffer();
            CombatProjectileDefinitionRegistry definitions = CreateDefinitions(lifetimeFrames: 10);
            var runtime = new CombatProjectileRuntime(
                definitions,
                buffer,
                new CombatPhysicsWorld(),
                FixedImpactResolver.Instance);
            FixVector3 origin = new FixVector3(Fix64.FromInt(2), Fix64.FromInt(3), Fix64.FromInt(4));
            FixVector3 targetPoint = new FixVector3(Fix64.FromInt(7), Fix64.FromInt(3), Fix64.FromInt(4));
            buffer.Enqueue(TargetedRequest(new CombatFrame(0), origin, targetPoint));
            runtime.Tick(new CombatFrame(0));

            RuntimeSaveState saveState = new CombatProjectileRuntimeSaveStateProvider(runtime).CaptureSaveState().Value;
            var restored = new CombatProjectileRuntime(
                definitions,
                new CombatProjectileSpawnRequestBuffer(),
                new CombatPhysicsWorld(),
                FixedImpactResolver.Instance);
            RuntimeSaveStateResult<bool> restoreResult =
                new CombatProjectileRuntimeSaveStateProvider(restored).RestoreSaveState(saveState);

            Assert.IsTrue(restoreResult.Success, restoreResult.Error.ToString());
            CombatProjectileSnapshot snapshot = restored.CreateStateSnapshot().ActiveProjectiles[0];
            Assert.AreEqual(origin, snapshot.Origin);
            Assert.IsTrue(snapshot.HasTargetPoint);
            Assert.AreEqual(targetPoint, snapshot.TargetPoint);
        }

        [Test]
        public void SaveState_RoundtripPreservesEffectiveLifetime()
        {
            CombatProjectileSpawnRequestBuffer buffer = new CombatProjectileSpawnRequestBuffer();
            CombatProjectileDefinitionRegistry definitions = CreateDefinitions(
                lifetimeFrames: 10,
                motionKind: CombatProjectileMotionKind.ArcToTarget,
                arcHeight: Fix64.FromInt(2));
            var runtime = new CombatProjectileRuntime(
                definitions,
                buffer,
                new CombatPhysicsWorld(),
                FixedImpactResolver.Instance);
            buffer.Enqueue(TargetedRequest(
                new CombatFrame(0),
                FixVector3.Zero,
                new FixVector3(Fix64.FromInt(2), Fix64.Zero, Fix64.Zero),
                effectiveLifetimeFrames: 4));
            runtime.Tick(new CombatFrame(0));
            RuntimeSaveState saveState = new CombatProjectileRuntimeSaveStateProvider(runtime).CaptureSaveState().Value;

            var restored = new CombatProjectileRuntime(
                definitions,
                new CombatProjectileSpawnRequestBuffer(),
                new CombatPhysicsWorld(),
                FixedImpactResolver.Instance);
            RuntimeSaveStateResult<bool> restoreResult =
                new CombatProjectileRuntimeSaveStateProvider(restored).RestoreSaveState(saveState);

            Assert.IsTrue(restoreResult.Success, restoreResult.Error.ToString());
            Assert.AreEqual(4, restored.CreateStateSnapshot().ActiveProjectiles[0].EffectiveLifetimeFrames);
        }

        [Test]
        public void SaveState_RoundtripPreservesTrackingTarget()
        {
            CombatProjectileSpawnRequestBuffer buffer = new CombatProjectileSpawnRequestBuffer();
            CombatProjectileDefinitionRegistry definitions = CreateDefinitions(
                lifetimeFrames: 10,
                motionKind: CombatProjectileMotionKind.HomingToTarget);
            var runtime = new CombatProjectileRuntime(
                definitions,
                buffer,
                new CombatPhysicsWorld(),
                FixedImpactResolver.Instance);
            FixVector3 targetPoint = new FixVector3(Fix64.FromInt(4), Fix64.Zero, Fix64.Zero);
            buffer.Enqueue(TrackingRequest(
                new CombatFrame(0),
                FixVector3.Zero,
                targetPoint,
                new CombatEntityId(2),
                effectiveLifetimeFrames: 10));
            runtime.Tick(new CombatFrame(0));
            RuntimeSaveState saveState = new CombatProjectileRuntimeSaveStateProvider(runtime).CaptureSaveState().Value;

            var restored = new CombatProjectileRuntime(
                definitions,
                new CombatProjectileSpawnRequestBuffer(),
                new CombatPhysicsWorld(),
                FixedImpactResolver.Instance);
            RuntimeSaveStateResult<bool> restoreResult =
                new CombatProjectileRuntimeSaveStateProvider(restored).RestoreSaveState(saveState);

            Assert.IsTrue(restoreResult.Success, restoreResult.Error.ToString());
            CombatProjectileSnapshot snapshot = restored.CreateStateSnapshot().ActiveProjectiles[0];
            Assert.IsTrue(snapshot.HasTrackingTarget);
            Assert.AreEqual(2, snapshot.TrackingTargetEntityId.Value);
            Assert.AreEqual(targetPoint, snapshot.TargetPoint);
            Assert.AreEqual(10, snapshot.EffectiveLifetimeFrames);
        }

        [Test]
        public void SaveState_RoundtripPreservesArcPath()
        {
            CombatProjectileSpawnRequestBuffer buffer = new CombatProjectileSpawnRequestBuffer();
            CombatProjectileDefinitionRegistry definitions = CreateDefinitions(
                lifetimeFrames: 4,
                motionKind: CombatProjectileMotionKind.ArcToTarget,
                arcHeight: Fix64.FromInt(2));
            var runtime = new CombatProjectileRuntime(
                definitions,
                buffer,
                new CombatPhysicsWorld(),
                FixedImpactResolver.Instance);
            buffer.Enqueue(TargetedRequest(
                new CombatFrame(0),
                FixVector3.Zero,
                new FixVector3(Fix64.FromInt(4), Fix64.Zero, Fix64.Zero)));
            runtime.Tick(new CombatFrame(0));
            runtime.Tick(new CombatFrame(1));
            RuntimeSaveState saveState = new CombatProjectileRuntimeSaveStateProvider(runtime).CaptureSaveState().Value;

            var restored = new CombatProjectileRuntime(
                definitions,
                new CombatProjectileSpawnRequestBuffer(),
                new CombatPhysicsWorld(),
                FixedImpactResolver.Instance);
            RuntimeSaveStateResult<bool> restoreResult =
                new CombatProjectileRuntimeSaveStateProvider(restored).RestoreSaveState(saveState);

            Assert.IsTrue(restoreResult.Success, restoreResult.Error.ToString());
            Assert.AreEqual(
                runtime.CreateStateSnapshot().ActiveProjectiles[0].LaunchSpeedPerFrame,
                restored.CreateStateSnapshot().ActiveProjectiles[0].LaunchSpeedPerFrame);
            runtime.Tick(new CombatFrame(2));
            restored.Tick(new CombatFrame(2));
            AssertFixVectorApproximately(
                runtime.CreateStateSnapshot().ActiveProjectiles[0].Position,
                restored.CreateStateSnapshot().ActiveProjectiles[0].Position,
                Fix64.FromRatio(1, 100000));
        }

        [Test]
        public void SaveState_RoundtripPreservesBallisticArcPath()
        {
            CombatProjectileSpawnRequestBuffer buffer = new CombatProjectileSpawnRequestBuffer();
            CombatProjectileDefinitionRegistry definitions = CreateDefinitions(
                lifetimeFrames: 10,
                motionKind: CombatProjectileMotionKind.BallisticArcToTarget,
                gravityPerFrameSquared: Fix64.One,
                speedPerFrame: Fix64.FromInt(2));
            var runtime = new CombatProjectileRuntime(
                definitions,
                buffer,
                new CombatPhysicsWorld(),
                FixedImpactResolver.Instance);
            buffer.Enqueue(TargetedRequest(
                new CombatFrame(0),
                FixVector3.Zero,
                new FixVector3(Fix64.FromInt(3), Fix64.Zero, Fix64.Zero)));
            runtime.Tick(new CombatFrame(0));
            runtime.Tick(new CombatFrame(1));
            RuntimeSaveState saveState = new CombatProjectileRuntimeSaveStateProvider(runtime).CaptureSaveState().Value;

            var restored = new CombatProjectileRuntime(
                definitions,
                new CombatProjectileSpawnRequestBuffer(),
                new CombatPhysicsWorld(),
                FixedImpactResolver.Instance);
            RuntimeSaveStateResult<bool> restoreResult =
                new CombatProjectileRuntimeSaveStateProvider(restored).RestoreSaveState(saveState);

            Assert.IsTrue(restoreResult.Success, restoreResult.Error.ToString());
            Assert.AreEqual(
                runtime.CreateStateSnapshot().ActiveProjectiles[0].LaunchSpeedPerFrame,
                restored.CreateStateSnapshot().ActiveProjectiles[0].LaunchSpeedPerFrame);
            runtime.Tick(new CombatFrame(2));
            restored.Tick(new CombatFrame(2));
            AssertFixVectorApproximately(
                runtime.CreateStateSnapshot().ActiveProjectiles[0].Position,
                restored.CreateStateSnapshot().ActiveProjectiles[0].Position,
                Fix64.FromRatio(1, 100000));
        }

        [Test]
        public void SaveState_RoundtripPreservesPiercingHitHistoryAndHash()
        {
            CombatPhysicsWorld world = new CombatPhysicsWorld();
            RegisterTarget(world, entity: 2, x: Fix64.FromRatio(105, 100));
            CombatProjectileSpawnRequestBuffer buffer = new CombatProjectileSpawnRequestBuffer();
            CombatProjectileDefinitionRegistry definitions = CreateDefinitions(
                lifetimeFrames: 10,
                piercingMaxAcceptedImpacts: 3);
            var runtime = new CombatProjectileRuntime(
                definitions,
                buffer,
                world,
                FixedImpactResolver.Instance);
            buffer.Enqueue(Request(new CombatFrame(0)));
            runtime.Tick(new CombatFrame(0));
            runtime.Tick(new CombatFrame(1));
            runtime.Tick(new CombatFrame(2));
            Assert.AreEqual(1, runtime.CreateStateSnapshot().ActiveProjectiles[0].AcceptedImpactCount);
            RuntimeSaveState saveState = new CombatProjectileRuntimeSaveStateProvider(runtime).CaptureSaveState().Value;

            var restored = new CombatProjectileRuntime(
                definitions,
                new CombatProjectileSpawnRequestBuffer(),
                new CombatPhysicsWorld(),
                FixedImpactResolver.Instance);
            RuntimeSaveStateResult<bool> restoreResult =
                new CombatProjectileRuntimeSaveStateProvider(restored).RestoreSaveState(saveState);

            Assert.IsTrue(restoreResult.Success, restoreResult.Error.ToString());
            CombatProjectileRuntimeState restoredState = restored.CreateStateSnapshot();
            Assert.AreEqual(1, restoredState.ActiveProjectiles[0].AcceptedImpactCount);
            Assert.AreEqual(2, restoredState.ActiveProjectiles[0].AcceptedImpacts[0].TargetEntityId.Value);

            long originalHash = RuntimeHashCombiner.ComputeHash(
                RuntimeFrame.Zero,
                new IRuntimeHashContributor[] { new CombatProjectileRuntimeHashContributor(runtime) });
            long restoredHash = RuntimeHashCombiner.ComputeHash(
                RuntimeFrame.Zero,
                new IRuntimeHashContributor[] { new CombatProjectileRuntimeHashContributor(restored) });
            Assert.AreEqual(originalHash, restoredHash);
        }

        [Test]
        public void RestoreStateSnapshot_RejectsDefinitionHashMismatch()
        {
            CombatProjectileRuntime runtime = CreateRuntimeWithActiveProjectile(out _);
            CombatProjectileRuntimeState state = runtime.CreateStateSnapshot();
            CombatProjectileRuntime mismatch = new CombatProjectileRuntime(
                CreateDefinitions(lifetimeFrames: 10, areaPulse: true),
                new CombatProjectileSpawnRequestBuffer(),
                new CombatPhysicsWorld(),
                FixedImpactResolver.Instance);

            bool restored = mismatch.TryRestoreStateSnapshot(state, out string failure);

            Assert.IsFalse(restored);
            StringAssert.Contains("definition hash", failure);
        }

        private static void AssertLinearHalfwayPosition(
            CombatProjectileSpeedProfileKind speedProfileKind,
            FixVector3 expectedPosition)
        {
            CombatProjectileSpawnRequestBuffer buffer = new CombatProjectileSpawnRequestBuffer();
            CombatProjectileRuntime runtime = CreateRuntime(
                buffer,
                new CombatPhysicsWorld(),
                lifetimeFrames: 4,
                speedProfileKind: speedProfileKind);
            buffer.Enqueue(Request(new CombatFrame(0)));

            runtime.Tick(new CombatFrame(0));
            runtime.Tick(new CombatFrame(1));
            runtime.Tick(new CombatFrame(2));

            Assert.AreEqual(expectedPosition, runtime.CreateStateSnapshot().ActiveProjectiles[0].Position);
        }

        private static void AssertFixVectorApproximately(
            FixVector3 expected,
            FixVector3 actual,
            Fix64 tolerance)
        {
            Assert.LessOrEqual(Math.Abs(expected.X.RawValue - actual.X.RawValue), tolerance.RawValue);
            Assert.LessOrEqual(Math.Abs(expected.Y.RawValue - actual.Y.RawValue), tolerance.RawValue);
            Assert.LessOrEqual(Math.Abs(expected.Z.RawValue - actual.Z.RawValue), tolerance.RawValue);
        }

        private static void AssertFix64Approximately(
            Fix64 expected,
            Fix64 actual,
            Fix64 tolerance)
        {
            Assert.LessOrEqual(Math.Abs(expected.RawValue - actual.RawValue), tolerance.RawValue);
        }

        private static int[] RunAreaPulseForRegisteredTargets(params int[] entityOrder)
        {
            CombatPhysicsWorld world = new CombatPhysicsWorld();
            for (int i = 0; i < entityOrder.Length; i++)
            {
                int entity = entityOrder[i];
                RegisterTarget(
                    world,
                    entity,
                    x: Fix64.One,
                    z: entity == 3 ? Fix64.One : Fix64.Zero);
            }

            CombatProjectileSpawnRequestBuffer buffer = new CombatProjectileSpawnRequestBuffer();
            CombatProjectileRuntime runtime = CreateRuntime(
                buffer,
                world,
                lifetimeFrames: 10,
                areaPulse: true);
            buffer.Enqueue(Request(new CombatFrame(0)));

            runtime.Tick(new CombatFrame(0));
            runtime.Tick(new CombatFrame(1));
            CombatProjectileFrameEvents hit = runtime.Tick(new CombatFrame(2));
            var targets = new List<int>();
            for (int i = 0; i < hit.Impacts.Count; i++)
            {
                if (hit.Impacts[i].ResolveResult.Kind == HitResolveKind.Damage)
                    targets.Add(hit.Impacts[i].ResolveResult.TargetId.Value);
            }

            return targets.ToArray();
        }

        private static CombatProjectileRuntime CreateRuntimeWithActiveProjectile(
            out CombatProjectileDefinitionRegistry definitions)
        {
            definitions = CreateDefinitions(lifetimeFrames: 10);
            CombatProjectileSpawnRequestBuffer buffer = new CombatProjectileSpawnRequestBuffer();
            var runtime = new CombatProjectileRuntime(
                definitions,
                buffer,
                new CombatPhysicsWorld(),
                FixedImpactResolver.Instance);
            buffer.Enqueue(Request(new CombatFrame(0)));
            runtime.Tick(new CombatFrame(0));
            runtime.Tick(new CombatFrame(1));
            return runtime;
        }

        private static CombatProjectileRuntime CreateRuntime(
            CombatProjectileSpawnRequestBuffer buffer,
            CombatPhysicsWorld world,
            int lifetimeFrames,
            bool areaPulse = false,
            int piercingMaxAcceptedImpacts = 1,
            CombatProjectileMotionKind motionKind = CombatProjectileMotionKind.Linear,
            Fix64 arcHeight = default,
            CombatProjectileSpeedProfileKind speedProfileKind = CombatProjectileSpeedProfileKind.Constant,
            Fix64 lateralAmplitude = default,
            int lateralCycles = 0,
            FixVector3 bezierControlOffsetA = default,
            FixVector3 bezierControlOffsetB = default,
            Fix64 homingTurnRatePerFrame = default,
            Fix64 gravityPerFrameSquared = default,
            Fix64 speedPerFrame = default,
            Fix64 maxLaunchAngleTangent = default,
            ITeamRelationProvider teamRelationProvider = null,
            IHitTargetStateResolver targetStateResolver = null,
            ICombatProjectileTrackingTargetProvider trackingTargetProvider = null,
            ICombatProjectileDirectSweepProvider directSweepProvider = null)
        {
            return new CombatProjectileRuntime(
                CreateDefinitions(
                    lifetimeFrames,
                    areaPulse,
                    piercingMaxAcceptedImpacts,
                    motionKind,
                    arcHeight,
                    speedProfileKind,
                    lateralAmplitude,
                    lateralCycles,
                    bezierControlOffsetA,
                    bezierControlOffsetB,
                    homingTurnRatePerFrame,
                    gravityPerFrameSquared,
                    speedPerFrame,
                    maxLaunchAngleTangent),
                buffer,
                world,
                FixedImpactResolver.Instance,
                teamRelationProvider,
                targetStateResolver,
                allowFriendlyFire: false,
                trackingTargetProvider: trackingTargetProvider,
                directSweepProvider: directSweepProvider);
        }

        private static CombatProjectileRuntime CreatePhysicalEventRuntime(
            CombatProjectileSpawnRequestBuffer buffer,
            CombatPhysicsWorld world,
            bool physicalEventsEnabled = true,
            bool areaPulse = false,
            int lifetimeFrames = 10,
            Fix64? collisionRadius = null,
            Fix64? areaRadius = null,
            int impactPower = 8,
            int areaImpactPower = 0)
        {
            Fix64 resolvedCollisionRadius = collisionRadius ?? Fix64.FromRatio(1, 10);
            Fix64 resolvedAreaRadius = areaRadius ?? Fix64.FromRatio(3, 2);
            CombatHitEventConfig hitEvent = CreatePhysicalEventHitEvent(
                "test.projectile.impact",
                impactPower);
            CombatHitEventConfig areaHitEvent = areaImpactPower > 0
                ? CreatePhysicalEventHitEvent("test.projectile.impact.area", areaImpactPower)
                : default;
            CombatProjectileImpactConfig impact = areaPulse
                ? new CombatProjectileImpactConfig(
                    PresentationTestId,
                    ProjectileImpactEventId,
                    CombatProjectileImpactPolicyKind.AreaPulseOnImpact,
                    resolvedAreaRadius,
                    CombatPhysicsLayerMask.FromLayer(1),
                    triggerOnExpire: true,
                    hitEvent: hitEvent,
                    areaHitEvent: areaHitEvent)
                : new CombatProjectileImpactConfig(hitEvent, ProjectileImpactEventId, stableEventLabel: "test.projectile.impact");

            var definitions = new CombatProjectileDefinitionRegistry(new[]
            {
                new CombatProjectileDefinition(
                    1,
                    "test.projectile",
                    new CombatProjectileMotionConfig(
                        CombatProjectileMotionKind.Linear,
                        Fix64.Half,
                        lifetimeFrames),
                    new CombatProjectileCollisionConfig(
                        resolvedCollisionRadius,
                        CombatPhysicsLayerMask.FromLayer(1),
                        stopOnFirstAcceptedImpact: true,
                        stopOnBlockedImpact: true),
                    impact,
                    physicalEventsEnabled)
            });

            return new CombatProjectileRuntime(
                definitions,
                buffer,
                world,
                FixedImpactResolver.Instance);
        }

        private static CombatHitEventConfig CreatePhysicalEventHitEvent(
            string stableEventId,
            int impactPower)
        {
            return new CombatHitEventConfig(
                CombatHitTargetTypes.HostileCharacter,
                CombatEnvironmentHitPolicy.None,
                ProjectileImpactEventId,
                maxAcceptedHits: 1,
                CombatHitSameTargetPolicy.AllowRepeat,
                new CombatHitDamageConfig(
                    ForceDamageTypeId,
                    baseDamage: 0,
                    impactPower,
                    Array.Empty<int>(),
                    PhysicalAnalysisId,
                    damageTypeLabel: "force",
                    analysisCategoryLabel: "PhysicalEventTest"));
        }

        private static CombatProjectileRuntime CreateLaserLanceRuntime(
            CombatProjectileSpawnRequestBuffer buffer,
            CombatPhysicsWorld world,
            ITeamRelationProvider teamRelationProvider = null,
            IHitTargetStateResolver targetStateResolver = null)
        {
            return new CombatProjectileRuntime(
                new CombatProjectileDefinitionRegistry(new[]
                {
                    new CombatProjectileDefinition(
                        1,
                        "test.laser_lance.projectile",
                        new CombatProjectileMotionConfig(
                            CombatProjectileMotionKind.Linear,
                            Fix64.FromInt(44),
                            lifetimeFrames: 1),
                        new CombatProjectileCollisionConfig(
                            Fix64.FromRatio(20, 100),
                            CombatPhysicsLayerMask.FromLayer(1),
                            stopOnFirstAcceptedImpact: false,
                            stopOnBlockedImpact: true,
                            maxAcceptedImpacts: 5,
                            hitSameTargetPolicy: CombatProjectileHitSameTargetPolicy.OncePerProjectile),
                        new CombatProjectileImpactConfig(
                            presentationProfileId: PresentationLaserId,
                            stableEventId: ProjectileImpactEventId,
                            presentationProfileLabel: "presentation.laser",
                            stableEventLabel: "laser.impact",
                            hitEvent: CreatePhysicalEventHitEvent("laser.impact", 8)))
                }),
                buffer,
                world,
                FixedImpactResolver.Instance,
                teamRelationProvider,
                targetStateResolver,
                allowFriendlyFire: false);
        }

        private static CombatProjectileDefinitionRegistry CreateDefinitions(
            int lifetimeFrames,
            bool areaPulse = false,
            int piercingMaxAcceptedImpacts = 1,
            CombatProjectileMotionKind motionKind = CombatProjectileMotionKind.Linear,
            Fix64 arcHeight = default,
            CombatProjectileSpeedProfileKind speedProfileKind = CombatProjectileSpeedProfileKind.Constant,
            Fix64 lateralAmplitude = default,
            int lateralCycles = 0,
            FixVector3 bezierControlOffsetA = default,
            FixVector3 bezierControlOffsetB = default,
            Fix64 homingTurnRatePerFrame = default,
            Fix64 gravityPerFrameSquared = default,
            Fix64 speedPerFrame = default,
            Fix64 maxLaunchAngleTangent = default)
        {
            Fix64 resolvedSpeedPerFrame = speedPerFrame > Fix64.Zero ? speedPerFrame : Fix64.Half;
            return new CombatProjectileDefinitionRegistry(new[]
            {
                new CombatProjectileDefinition(
                    1,
                    "test.projectile",
                    new CombatProjectileMotionConfig(
                        motionKind,
                        resolvedSpeedPerFrame,
                        lifetimeFrames,
                        arcHeight,
                        speedProfileKind,
                        lateralAmplitude,
                        lateralCycles,
                        bezierControlOffsetA,
                        bezierControlOffsetB,
                        homingTurnRatePerFrame,
                        gravityPerFrameSquared,
                        maxLaunchAngleTangent),
                    new CombatProjectileCollisionConfig(
                        Fix64.FromRatio(1, 10),
                        CombatPhysicsLayerMask.FromLayer(1),
                        stopOnFirstAcceptedImpact: piercingMaxAcceptedImpacts <= 1,
                        stopOnBlockedImpact: true,
                        maxAcceptedImpacts: piercingMaxAcceptedImpacts,
                        hitSameTargetPolicy: piercingMaxAcceptedImpacts > 1
                            ? CombatProjectileHitSameTargetPolicy.OncePerProjectile
                            : CombatProjectileHitSameTargetPolicy.AllowRepeat),
                    areaPulse
                        ? new CombatProjectileImpactConfig(
                            PresentationTestId,
                            ProjectileImpactEventId,
                            CombatProjectileImpactPolicyKind.AreaPulseOnImpact,
                            Fix64.FromRatio(3, 2),
                            CombatPhysicsLayerMask.FromLayer(1),
                            triggerOnExpire: true,
                            hitEvent: CreatePhysicalEventHitEvent("test.projectile.impact", 8))
                        : new CombatProjectileImpactConfig(
                            presentationProfileId: PresentationTestId,
                            stableEventId: ProjectileImpactEventId,
                            presentationProfileLabel: "presentation.test",
                            stableEventLabel: "test.projectile.impact",
                            hitEvent: CreatePhysicalEventHitEvent("test.projectile.impact", 8)))
            });
        }

        private static CombatProjectileDefinitionRegistry CreateEnvironmentBlockedAreaPulseDefinitions()
        {
            return new CombatProjectileDefinitionRegistry(new[]
            {
                new CombatProjectileDefinition(
                    1,
                    "test.environment_blocked_area.projectile",
                    new CombatProjectileMotionConfig(
                        CombatProjectileMotionKind.Linear,
                        Fix64.Half,
                        lifetimeFrames: 10),
                    new CombatProjectileCollisionConfig(
                        Fix64.FromRatio(1, 10),
                        CombatPhysicsLayerMask.FromLayer(1),
                        stopOnFirstAcceptedImpact: true,
                        stopOnBlockedImpact: true,
                        maxAcceptedImpacts: 1,
                        hitSameTargetPolicy: CombatProjectileHitSameTargetPolicy.AllowRepeat),
                    new CombatProjectileImpactConfig(
                        PresentationTestId,
                        EnvironmentImpactEventId,
                        CombatProjectileImpactPolicyKind.AreaPulseOnImpact,
                        Fix64.FromRatio(3, 2),
                        CombatPhysicsLayerMask.FromLayer(2),
                        triggerOnExpire: true,
                        hitEvent: new CombatHitEventConfig(
                            CombatHitTargetTypes.HostileCharacter | CombatHitTargetTypes.WorldStatic,
                            CombatEnvironmentHitPolicy.BlockedPresentation,
                            EnvironmentDirectEventId,
                            stableEventLabel: "test.environment_blocked_area.direct"),
                        areaHitEvent: new CombatHitEventConfig(
                            CombatHitTargetTypes.HostileCharacter,
                            CombatEnvironmentHitPolicy.None,
                            EnvironmentAreaEventId,
                            stableEventLabel: "test.environment_blocked_area.area"),
                        effectTimelineId: 0,
                        stableEventLabel: "test.environment_blocked_area.impact"))
            });
        }

        private static CombatProjectileSpawnRequest Request(CombatFrame frame)
        {
            return new CombatProjectileSpawnRequest(
                frame,
                definitionId: 1,
                sourceEntityId: new CombatEntityId(1),
                origin: FixVector3.Zero,
                direction: new FixVector3(Fix64.One, Fix64.Zero, Fix64.Zero),
                actionId: 10,
                actionInstanceId: 20,
                traceId: 30,
                localFrame: 6);
        }

        private static CombatProjectileSpawnRequest TargetedRequest(
            CombatFrame frame,
            FixVector3 origin,
            FixVector3 targetPoint,
            int effectiveLifetimeFrames = 0)
        {
            return new CombatProjectileSpawnRequest(
                frame,
                definitionId: 1,
                sourceEntityId: new CombatEntityId(1),
                origin: origin,
                direction: new FixVector3(Fix64.One, Fix64.Zero, Fix64.Zero),
                hasTargetPoint: true,
                targetPoint: targetPoint,
                actionId: 10,
                actionInstanceId: 20,
                traceId: 30,
                localFrame: 6,
                effectiveLifetimeFrames: effectiveLifetimeFrames);
        }

        private static CombatProjectileSpawnRequest TrackingRequest(
            CombatFrame frame,
            FixVector3 origin,
            FixVector3 targetPoint,
            CombatEntityId targetEntityId,
            int effectiveLifetimeFrames = 0)
        {
            return new CombatProjectileSpawnRequest(
                frame,
                definitionId: 1,
                sourceEntityId: new CombatEntityId(1),
                origin: origin,
                direction: new FixVector3(Fix64.One, Fix64.Zero, Fix64.Zero),
                hasTargetPoint: true,
                targetPoint: targetPoint,
                actionId: 10,
                actionInstanceId: 20,
                traceId: 30,
                localFrame: 6,
                effectiveLifetimeFrames: effectiveLifetimeFrames,
                hasTrackingTarget: true,
                trackingTargetEntityId: targetEntityId);
        }

        private static void RegisterTarget(CombatPhysicsWorld world, int entity, Fix64 x)
        {
            RegisterTarget(world, entity, x, Fix64.Zero);
        }

        private static void RegisterTarget(CombatPhysicsWorld world, int entity, Fix64 x, Fix64 z)
        {
            RegisterTarget(world, entity, x, Fix64.Zero, z);
        }

        private static void RegisterTarget(CombatPhysicsWorld world, int entity, Fix64 x, Fix64 y, Fix64 z)
        {
            RegisterTargetOnLayer(world, entity, 1, x, y, z);
        }

        private static void RegisterTargetOnLayer(
            CombatPhysicsWorld world,
            int entity,
            int layer,
            Fix64 x,
            Fix64 y,
            Fix64 z)
        {
            world.UpsertBody(new CombatPhysicsBody(
                new CombatEntityId(entity),
                new CombatBodyId(entity),
                new FixVector3(x, y, z)));
            world.UpsertAabbCollider(new CombatPhysicsAabbCollider(
                new CombatBodyId(entity),
                new CombatColliderId(1),
                layer,
                localMin: new FixVector3(-Fix64.FromRatio(1, 10), -Fix64.FromRatio(1, 10), -Fix64.FromRatio(1, 10)),
                localMax: new FixVector3(Fix64.FromRatio(1, 10), Fix64.FromRatio(1, 10), Fix64.FromRatio(1, 10))));
        }

        private sealed class MutableTrackingTargetProvider : ICombatProjectileTrackingTargetProvider
        {
            public FixVector3 TargetPoint { get; set; }
            public bool Enabled { get; set; } = true;

            public bool TryResolveTrackingTarget(
                CombatProjectileTrackingTargetQuery query,
                out FixVector3 targetPoint)
            {
                targetPoint = TargetPoint;
                return Enabled;
            }
        }

        private sealed class TestDirectSweepProvider : ICombatProjectileDirectSweepProvider
        {
            private readonly FixVector3 _offset;
            private readonly int _queryCount;

            public TestDirectSweepProvider(FixVector3 offset, int queryCount)
            {
                _offset = offset;
                _queryCount = queryCount;
            }

            public int CollectDirectSweepQueries(
                CombatFrame frame,
                CombatProjectileSnapshot projectile,
                CombatProjectileDefinition definition,
                CombatQueryHeader header,
                FixVector3 previousPosition,
                FixVector3 currentPosition,
                List<CombatCapsuleQuery> output)
            {
                for (int i = 0; i < _queryCount; i++)
                {
                    output.Add(new CombatCapsuleQuery(
                        header,
                        previousPosition + _offset,
                        currentPosition + _offset,
                        definition.Collision.Radius));
                }

                return _queryCount;
            }
        }

        private sealed class FixedImpactResolver : ICombatProjectileImpactResolver
        {
            public static readonly FixedImpactResolver Instance = new FixedImpactResolver();

            public bool TryResolveImpact(
                CombatProjectileSnapshot projectile,
                CombatQueryResult hit,
                CombatFrame frame,
                CombatProjectileImpactContext context,
                out CombatProjectileImpactPayload payload)
            {
                payload = new CombatProjectileImpactPayload(
                    damage: 10,
                    staggerFrames: 2,
                    knockback: FixVector3.Zero,
                    targetState: HitTargetStateFlags.Alive,
                    presentationProfileId: "presentation.test",
                    stableEventId: "test.projectile.impact");
                return true;
            }
        }

        private sealed class ContextualImpactResolver : ICombatProjectileImpactResolver
        {
            public int DirectSweepCount { get; private set; }
            public int AreaPulseCount { get; private set; }

            public bool TryResolveImpact(
                CombatProjectileSnapshot projectile,
                CombatQueryResult hit,
                CombatFrame frame,
                CombatProjectileImpactContext context,
                out CombatProjectileImpactPayload payload)
            {
                if (context.SourceKind == CombatProjectileImpactSourceKind.AreaPulse)
                {
                    AreaPulseCount++;
                    payload = new CombatProjectileImpactPayload(
                        damage: 30,
                        staggerFrames: 1,
                        knockback: FixVector3.Zero,
                        targetState: HitTargetStateFlags.Alive,
                        stableEventId: "area");
                    return true;
                }

                DirectSweepCount++;
                payload = new CombatProjectileImpactPayload(
                    damage: 10,
                    staggerFrames: 1,
                    knockback: FixVector3.Zero,
                    targetState: HitTargetStateFlags.Alive,
                    stableEventId: "direct");
                return true;
            }
        }

        private sealed class EnvironmentBlockedAreaPulseResolver : ICombatProjectileImpactResolver
        {
            private readonly CombatEntityId _blockedTargetId;

            public EnvironmentBlockedAreaPulseResolver(CombatEntityId blockedTargetId)
            {
                _blockedTargetId = blockedTargetId;
            }

            public int DirectSweepCount { get; private set; }

            public int AreaPulseCount { get; private set; }

            public bool TryResolveImpact(
                CombatProjectileSnapshot projectile,
                CombatQueryResult hit,
                CombatFrame frame,
                CombatProjectileImpactContext context,
                out CombatProjectileImpactPayload payload)
            {
                if (context.SourceKind == CombatProjectileImpactSourceKind.AreaPulse)
                {
                    AreaPulseCount++;
                    payload = new CombatProjectileImpactPayload(
                        damage: 20,
                        staggerFrames: 1,
                        knockback: FixVector3.Zero,
                        targetState: HitTargetStateFlags.Alive,
                        stableEventId: "test.environment_blocked_area.area");
                    return true;
                }

                DirectSweepCount++;
                HitTargetStateFlags targetState = hit.TargetEntityId.Equals(_blockedTargetId)
                    ? HitTargetStateFlags.Alive | HitTargetStateFlags.Blocking
                    : HitTargetStateFlags.Alive;
                payload = new CombatProjectileImpactPayload(
                    damage: 0,
                    staggerFrames: 0,
                    knockback: FixVector3.Zero,
                    targetState: targetState,
                    stableEventId: "test.environment_blocked_area.direct");
                return true;
            }
        }

        private sealed class FixedTargetState : IHitTargetStateResolver
        {
            private readonly HitTargetStateFlags _state;

            public FixedTargetState(HitTargetStateFlags state)
            {
                _state = state;
            }

            public HitTargetStateFlags ResolveTargetState(CombatEntityId targetId)
            {
                return _state;
            }
        }

        private sealed class SelectiveDeadTargetState : IHitTargetStateResolver
        {
            private readonly CombatEntityId _deadTargetId;

            public SelectiveDeadTargetState(CombatEntityId deadTargetId)
            {
                _deadTargetId = deadTargetId;
            }

            public HitTargetStateFlags ResolveTargetState(CombatEntityId targetId)
            {
                return targetId.Equals(_deadTargetId) ? HitTargetStateFlags.None : HitTargetStateFlags.Alive;
            }
        }

        private sealed class FriendlyTeams : ITeamRelationProvider
        {
            public static readonly FriendlyTeams Instance = new FriendlyTeams();

            public bool AreHostile(CombatEntityId a, CombatEntityId b)
            {
                return false;
            }

            public bool AreFriendly(CombatEntityId a, CombatEntityId b)
            {
                return true;
            }

            public bool IsSameTeam(CombatEntityId a, CombatEntityId b)
            {
                return true;
            }
        }

        private sealed class SelectiveFriendlyTeams : ITeamRelationProvider
        {
            private readonly CombatEntityId _friendlyTargetId;

            public SelectiveFriendlyTeams(CombatEntityId friendlyTargetId)
            {
                _friendlyTargetId = friendlyTargetId;
            }

            public bool AreHostile(CombatEntityId a, CombatEntityId b)
            {
                return !b.Equals(_friendlyTargetId);
            }

            public bool AreFriendly(CombatEntityId a, CombatEntityId b)
            {
                return b.Equals(_friendlyTargetId);
            }

            public bool IsSameTeam(CombatEntityId a, CombatEntityId b)
            {
                return b.Equals(_friendlyTargetId);
            }
        }
    }
}
