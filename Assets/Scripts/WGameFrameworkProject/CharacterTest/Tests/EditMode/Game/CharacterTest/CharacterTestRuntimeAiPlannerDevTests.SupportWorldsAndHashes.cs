using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using MxFramework.Animation;
using MxFramework.Audio;
using MxFramework.CharacterAction;
using MxFramework.CharacterRuntimeSpawn.Unity;
using MxFramework.Combat.Core;
using MxFramework.Combat.GameplayBridge;
using MxFramework.Combat.Physics;
using MxFramework.Combat.Projectiles;
using MxFramework.Combat.Shields;
using MxFramework.Core.Math;
using MxFramework.Game.CharacterTest;
using MxFramework.Gameplay;
using MxFramework.Network;
using MxFramework.Resources;
using MxFramework.Runtime;
using MxFramework.UI;
using MxFramework.UI.FairyGui;
using MxFramework.Vfx;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace MxFramework.Tests.Game.CharacterTest
{
    public sealed partial class CharacterTestRuntimeAiPlannerDevTests
    {

        private static void AssertAreaConfinementReplayMatches(
            FrameSyncAreaConfinementFrameRecord expected,
            FrameSyncAreaConfinementFrameRecord actual)
        {
            Assert.AreEqual(
                expected.AreaConfinementState.PendingCount,
                actual.AreaConfinementState.PendingCount,
                "Area Confinement pending channel count should replay exactly.");
            Assert.AreEqual(
                expected.AreaConfinementState.ActiveZoneCount,
                actual.AreaConfinementState.ActiveZoneCount,
                "Area Confinement active zone count should replay exactly.");
            Assert.AreEqual(
                expected.AreaConfinementHash,
                actual.AreaConfinementHash,
                "Area Confinement channel/source state hash should replay exactly.");

            AssertAuthorityReplayMatches(expected.Sample, actual.Sample);
        }

        private static void AssertAuthorityReplayMatches(
            CharacterTestRuntimeAiLabFrameSyncSample expected,
            CharacterTestRuntimeAiLabFrameSyncSample actual)
        {
            var expectedAuthority = new CharacterTestRuntimeAiLabAuthoritySample(
                new RuntimeFrame(expected.RuntimeFrame),
                expected.StateHash);
            var actualAuthority = new CharacterTestRuntimeAiLabAuthoritySample(
                new RuntimeFrame(actual.RuntimeFrame),
                actual.StateHash);
            CharacterTestRuntimeAiLabFrameSyncReport report =
                CharacterTestRuntimeAiLabAuthorityProbe.Compare(expectedAuthority, actualAuthority);

            Assert.IsTrue(report.Passed, report.SummaryText);
        }

        private static void AssertAreaConfinementTargetRooted(FrameSyncAreaConfinementFrameRecord record)
        {
            Assert.IsTrue(
                TryFindFrameSyncCharacterSample(
                    record.Sample.Characters,
                    record.TargetEntityId,
                    out CharacterTestRuntimeAiLabFrameSyncCharacterSample target),
                "Area Confinement target sample should be present.");
            Assert.Greater(
                target.BuffCount,
                0,
                "Area Confinement target should have Rooted buff in the frame sync sample.");
            Assert.Greater(
                target.StatusCount,
                0,
                "Area Confinement target should project AreaConfined status in the frame sync sample.");
        }

        private static int FindFirstProjectileRecordWithActiveCount(
            IReadOnlyList<FrameSyncProjectileFrameRecord> records,
            int minimumActiveCount)
        {
            for (int i = 0; i < records.Count; i++)
            {
                if (records[i].Sample.Projectiles.Count >= minimumActiveCount)
                    return i;
            }

            return -1;
        }

        private static int FindMeteorBarrageRunnerContinuationCheckpoint(
            IReadOnlyList<FrameSyncMeteorBarrageRunnerFrameRecord> records,
            out int expectedIndex)
        {
            expectedIndex = -1;
            if (records == null)
                return -1;

            for (int i = 0; i < records.Count - 1; i++)
            {
                int activeCount = records[i].ProjectileState != null
                    ? records[i].ProjectileState.ActiveCount
                    : 0;
                if (activeCount <= 0
                    || activeCount >= CharacterTestFireballProjectileAdapter.MeteorBarrageProjectileCount
                    || records[i].Sample.Characters.Count == 0
                    || !records[i].Sample.Characters[0].HasRunner)
                {
                    continue;
                }

                for (int j = i + 1; j < records.Count; j++)
                {
                    int futureActiveCount = records[j].ProjectileState != null
                        ? records[j].ProjectileState.ActiveCount
                        : 0;
                    if (futureActiveCount > activeCount)
                    {
                        expectedIndex = j;
                        return i;
                    }
                }
            }

            return -1;
        }

        private static FrameSyncProjectileReplayResult RunFrameSyncProjectileAcceptedImpactReplay(int maxFrames)
        {
            CharacterTestCharacterRuntimeWorld world = CreateFrameSyncProjectilePierceWorld(
                out CharacterTestCharacterEntity attacker,
                out CharacterTestCharacterEntity firstTarget,
                out CharacterTestCharacterEntity secondTarget);
            CharacterTestCharacterActionRuntime actionRuntime = CharacterTestCharacterActionConfigSource.Load();
            CharacterTestCharacterPhysicsRuntime physicsRuntime = CreateFrameSyncPhysicsRuntime();
            var motion = new CharacterTestCharacterMotionSystem();
            motion.SetMovementProfile(CreateFrameSyncMovementRuntime());
            motion.SetPhysicsRuntime(physicsRuntime);
            var combat = new CharacterTestCharacterCombatTraceSystem();
            combat.SetPhysicsRuntime(physicsRuntime);

            motion.Tick(world, 1f / 60f, RuntimeFrame.Zero);
            Assert.IsTrue(actionRuntime.TryStartIntent(
                attacker,
                CharacterTestCharacterActionConfigSource.FlameLanceIntentId,
                RuntimeFrame.Zero));

            var records = new List<FrameSyncProjectileFrameRecord>(Math.Max(1, maxFrames));
            int firstActiveRecordIndex = -1;
            int firstAcceptedImpactRecordIndex = -1;
            bool targetsPlacedOnProjectileLine = false;
            int maxAcceptedImpactCount = 0;
            string lastProjectileDiagnostic = string.Empty;
            for (int i = 0; i < maxFrames; i++)
            {
                RuntimeFrame frame = new RuntimeFrame(i);
                actionRuntime.TickEntity(attacker);
                motion.Tick(world, 1f / 60f, frame);
                combat.Tick(world, actionRuntime, frame);
                CombatProjectileRuntimeState projectileState = combat.CreateProjectileStateSnapshot();
                if (!targetsPlacedOnProjectileLine
                    && projectileState != null
                    && projectileState.ActiveCount > 0)
                {
                    CombatProjectileSnapshot activeProjectile = projectileState.ActiveProjectiles[0];
                    PlaceFrameSyncTargetOnProjectileLine(
                        physicsRuntime,
                        firstTarget,
                        activeProjectile,
                        Fix64.FromInt(2));
                    PlaceFrameSyncTargetOnProjectileLine(
                        physicsRuntime,
                        secondTarget,
                        activeProjectile,
                        Fix64.FromInt(4));
                    targetsPlacedOnProjectileLine = true;
                }
                CharacterTestRuntimeAiLabFrameSyncSample sample =
                    CharacterTestRuntimeAiLabFrameSyncProbe.Capture(
                        frame,
                        stateHash: 0L,
                        entities: null,
                        actionRuntime: null,
                        projectileState: projectileState);
                records.Add(new FrameSyncProjectileFrameRecord(i, projectileState, sample));

                if (projectileState == null || projectileState.ActiveCount <= 0)
                    continue;

                CombatProjectileSnapshot projectile = projectileState.ActiveProjectiles[0];
                Assert.AreEqual(CharacterTestFireballProjectileAdapter.FlameLanceStableId, projectile.DefinitionStableId);
                maxAcceptedImpactCount = Math.Max(maxAcceptedImpactCount, projectile.AcceptedImpactCount);
                lastProjectileDiagnostic = "frame=" + i
                    + " position=" + projectile.Position
                    + " direction=" + projectile.Direction
                    + " target1=" + firstTarget.Transform.X + "," + firstTarget.Transform.Y + "," + firstTarget.Transform.Z
                    + " target2=" + secondTarget.Transform.X + "," + secondTarget.Transform.Y + "," + secondTarget.Transform.Z;
                if (firstActiveRecordIndex < 0)
                    firstActiveRecordIndex = records.Count - 1;
                if (firstAcceptedImpactRecordIndex < 0 && projectile.AcceptedImpactCount >= 2)
                    firstAcceptedImpactRecordIndex = records.Count - 1;
            }

            if (firstAcceptedImpactRecordIndex < 0)
            {
                Assert.Fail(
                    "Flame Lance accepted no piercing impacts. maxAccepted="
                    + maxAcceptedImpactCount
                    + " "
                    + lastProjectileDiagnostic);
            }

            return new FrameSyncProjectileReplayResult(
                firstActiveRecordIndex,
                records,
                firstAcceptedImpactRecordIndex);
        }

        private static CharacterTestRuntimeAiLabFrameSyncSample ReplayFrameSyncProjectileAcceptedImpactFromCheckpoint(
            CombatProjectileRuntimeState checkpointState,
            int checkpointFrame,
            int targetFrame)
        {
            CharacterTestCharacterRuntimeWorld world = CreateFrameSyncProjectilePierceWorld(
                out _,
                out _,
                out _);
            CharacterTestCharacterActionRuntime actionRuntime = CharacterTestCharacterActionConfigSource.Load();
            CharacterTestCharacterPhysicsRuntime physicsRuntime = CreateFrameSyncPhysicsRuntime();
            var motion = new CharacterTestCharacterMotionSystem();
            motion.SetMovementProfile(CreateFrameSyncMovementRuntime());
            motion.SetPhysicsRuntime(physicsRuntime);
            var combat = new CharacterTestCharacterCombatTraceSystem();
            combat.SetPhysicsRuntime(physicsRuntime);

            motion.Tick(world, 1f / 60f, new RuntimeFrame(checkpointFrame));
            Assert.IsTrue(
                combat.TryApplyProjectileStateSnapshot(checkpointState, out string failure),
                failure);

            CombatProjectileRuntimeState projectileState = combat.CreateProjectileStateSnapshot();
            for (int frameValue = checkpointFrame + 1; frameValue <= targetFrame; frameValue++)
            {
                RuntimeFrame frame = new RuntimeFrame(frameValue);
                motion.Tick(world, 1f / 60f, frame);
                combat.Tick(world, actionRuntime, frame);
                projectileState = combat.CreateProjectileStateSnapshot();
            }

            return CharacterTestRuntimeAiLabFrameSyncProbe.Capture(
                new RuntimeFrame(targetFrame),
                stateHash: 0L,
                entities: null,
                actionRuntime: null,
                projectileState: projectileState);
        }

        private static CharacterTestCharacterRuntimeWorld CreateFrameSyncProjectileWorld(
            out CharacterTestCharacterEntity attacker)
        {
            return CreateFrameSyncProjectileWorld(out attacker, out _, includeTarget: false);
        }

        private static CharacterTestCharacterRuntimeWorld CreateFrameSyncProjectileWorld(
            out CharacterTestCharacterEntity attacker,
            out CharacterTestCharacterEntity target,
            bool includeTarget)
        {
            var registry = new CharacterTestCharacterDefinitionRegistry();
            Assert.IsTrue(
                registry.TryGet(
                    CharacterTestCharacterDefinitionRegistry.IronVanguardCharacterId,
                    out CharacterTestCharacterDefinition definition),
                "Iron Vanguard definition must be available for frame sync projectile replay tests.");
            var world = new CharacterTestCharacterRuntimeWorld();
            attacker = world.Spawn(
                new CharacterTestCharacterSpawnRequest(
                    definition.CharacterId,
                    definition.DefaultTeamId,
                    0f,
                    0f,
                    0f,
                    CharacterTestCharacterControlOwner.LocalPlayer),
                definition);
            attacker.SetMotionState(0f, Vector2.zero, isGrounded: true);
            target = null;
            if (includeTarget)
            {
                target = world.Spawn(
                    new CharacterTestCharacterSpawnRequest(
                        definition.CharacterId,
                        teamId: 2,
                        x: 8f,
                        y: 0f,
                        z: 0f,
                        CharacterTestCharacterControlOwner.None),
                    definition);
                target.SetMotionState(0f, Vector2.zero, isGrounded: true);
            }

            return world;
        }

        private static CharacterTestCharacterRuntimeWorld CreateFrameSyncHookChainWorld(
            out CharacterTestCharacterEntity attacker,
            out CharacterTestCharacterEntity target)
        {
            CharacterTestCharacterRuntimeWorld world = CreateFrameSyncProjectileWorld(
                out attacker,
                out target,
                includeTarget: true);
            target.SetTransform(new CharacterTestCharacterTransformComponent(0f, 0f, 7f, 180f));
            target.SetMotionState(0f, Vector2.zero, isGrounded: true);
            return world;
        }

        private static CharacterTestCharacterRuntimeWorld CreateFrameSyncArcMineWorld(
            out CharacterTestCharacterEntity attacker,
            out CharacterTestCharacterEntity target)
        {
            CharacterTestCharacterRuntimeWorld world = CreateFrameSyncProjectileWorld(
                out attacker,
                out target,
                includeTarget: true);
            target.SetTransform(new CharacterTestCharacterTransformComponent(0f, 0f, 5f, 180f));
            target.SetMotionState(0f, Vector2.zero, isGrounded: true);
            return world;
        }

        private static CharacterTestCharacterRuntimeWorld CreateFrameSyncRuneTurretWorld(
            out CharacterTestCharacterEntity attacker,
            out CharacterTestCharacterEntity target)
        {
            CharacterTestCharacterRuntimeWorld world = CreateFrameSyncProjectileWorld(
                out attacker,
                out target,
                includeTarget: true);
            target.SetTransform(new CharacterTestCharacterTransformComponent(0f, 0f, 7f, 180f));
            target.SetMotionState(0f, Vector2.zero, isGrounded: true);
            return world;
        }

        private static CharacterTestCharacterRuntimeWorld CreateFrameSyncTornadoWorld(
            out CharacterTestCharacterEntity attacker,
            out CharacterTestCharacterEntity target)
        {
            CharacterTestCharacterRuntimeWorld world = CreateFrameSyncProjectileWorld(
                out attacker,
                out target,
                includeTarget: true);
            target.SetTransform(new CharacterTestCharacterTransformComponent(0f, 0f, 8f, 180f));
            target.SetMotionState(0f, Vector2.zero, isGrounded: true);
            return world;
        }

        private static CharacterTestCharacterRuntimeWorld CreateFrameSyncLaserPathWorld(
            out CharacterTestCharacterEntity attacker,
            out CharacterTestCharacterEntity target)
        {
            CharacterTestCharacterRuntimeWorld world = CreateFrameSyncProjectileWorld(
                out attacker,
                out target,
                includeTarget: true);
            target.SetTransform(new CharacterTestCharacterTransformComponent(0f, 0f, 8f, 180f));
            target.SetMotionState(0f, Vector2.zero, isGrounded: true);
            return world;
        }

        private static CharacterTestCharacterRuntimeWorld CreateFrameSyncChainLightningWorld(
            out CharacterTestCharacterEntity attacker,
            out CharacterTestCharacterEntity lockedTarget)
        {
            var registry = new CharacterTestCharacterDefinitionRegistry();
            Assert.IsTrue(
                registry.TryGet(
                    CharacterTestCharacterDefinitionRegistry.IronVanguardCharacterId,
                    out CharacterTestCharacterDefinition definition),
                "Iron Vanguard definition must be available for frame sync Chain Lightning tests.");

            var world = new CharacterTestCharacterRuntimeWorld();
            attacker = world.Spawn(
                new CharacterTestCharacterSpawnRequest(
                    definition.CharacterId,
                    definition.DefaultTeamId,
                    0f,
                    0f,
                    0f,
                    CharacterTestCharacterControlOwner.LocalPlayer),
                definition);
            attacker.SetMotionState(0f, Vector2.zero, isGrounded: true);

            CharacterTestCharacterEntity closerForwardTarget = world.Spawn(
                new CharacterTestCharacterSpawnRequest(
                    definition.CharacterId,
                    teamId: 2,
                    x: 0f,
                    y: 0f,
                    z: 6f,
                    CharacterTestCharacterControlOwner.None),
                definition);
            lockedTarget = world.Spawn(
                new CharacterTestCharacterSpawnRequest(
                    definition.CharacterId,
                    teamId: 2,
                    x: 1f,
                    y: 0f,
                    z: 8f,
                    CharacterTestCharacterControlOwner.None),
                definition);
            CharacterTestCharacterEntity thirdTarget = world.Spawn(
                new CharacterTestCharacterSpawnRequest(
                    definition.CharacterId,
                    teamId: 2,
                    x: 1f,
                    y: 0f,
                    z: 12.5f,
                    CharacterTestCharacterControlOwner.None),
                definition);
            CharacterTestCharacterEntity fourthTarget = world.Spawn(
                new CharacterTestCharacterSpawnRequest(
                    definition.CharacterId,
                    teamId: 2,
                    x: -1f,
                    y: 0f,
                    z: 16f,
                    CharacterTestCharacterControlOwner.None),
                definition);
            CharacterTestCharacterEntity cappedTarget = world.Spawn(
                new CharacterTestCharacterSpawnRequest(
                    definition.CharacterId,
                    teamId: 2,
                    x: -1f,
                    y: 0f,
                    z: 19.5f,
                    CharacterTestCharacterControlOwner.None),
                definition);
            closerForwardTarget.SetMotionState(0f, Vector2.zero, isGrounded: true);
            lockedTarget.SetMotionState(0f, Vector2.zero, isGrounded: true);
            thirdTarget.SetMotionState(0f, Vector2.zero, isGrounded: true);
            fourthTarget.SetMotionState(0f, Vector2.zero, isGrounded: true);
            cappedTarget.SetMotionState(0f, Vector2.zero, isGrounded: true);
            return world;
        }

        private static CharacterTestCharacterRuntimeWorld CreateFrameSyncProjectilePierceWorld(
            out CharacterTestCharacterEntity attacker,
            out CharacterTestCharacterEntity firstTarget,
            out CharacterTestCharacterEntity secondTarget)
        {
            var registry = new CharacterTestCharacterDefinitionRegistry();
            Assert.IsTrue(
                registry.TryGet(
                    CharacterTestCharacterDefinitionRegistry.IronVanguardCharacterId,
                    out CharacterTestCharacterDefinition definition),
                "Iron Vanguard definition must be available for frame sync projectile replay tests.");
            var world = new CharacterTestCharacterRuntimeWorld();
            attacker = world.Spawn(
                new CharacterTestCharacterSpawnRequest(
                    definition.CharacterId,
                    definition.DefaultTeamId,
                    0f,
                    0f,
                    0f,
                    CharacterTestCharacterControlOwner.LocalPlayer),
                definition);
            firstTarget = world.Spawn(
                new CharacterTestCharacterSpawnRequest(
                    definition.CharacterId,
                    teamId: 2,
                    x: 0f,
                    y: 0f,
                    z: 0.95f,
                    CharacterTestCharacterControlOwner.None),
                definition);
            secondTarget = world.Spawn(
                new CharacterTestCharacterSpawnRequest(
                    definition.CharacterId,
                    teamId: 2,
                    x: 0f,
                    y: 0f,
                    z: 0.95f,
                    CharacterTestCharacterControlOwner.None),
                definition);
            attacker.SetMotionState(0f, Vector2.zero, isGrounded: true);
            firstTarget.SetMotionState(0f, Vector2.zero, isGrounded: true);
            secondTarget.SetMotionState(0f, Vector2.zero, isGrounded: true);
            return world;
        }

        private static CharacterTestCharacterRuntimeWorld CreateFrameSyncCombatWorld(
            out CharacterTestCharacterEntity attacker,
            out CharacterTestCharacterEntity target)
        {
            var registry = new CharacterTestCharacterDefinitionRegistry();
            Assert.IsTrue(
                registry.TryGet(
                    CharacterTestCharacterDefinitionRegistry.IronVanguardCharacterId,
                    out CharacterTestCharacterDefinition definition),
                "Iron Vanguard definition must be available for frame sync combat replay tests.");
            var world = new CharacterTestCharacterRuntimeWorld();
            attacker = world.Spawn(
                new CharacterTestCharacterSpawnRequest(
                    definition.CharacterId,
                    definition.DefaultTeamId,
                    0f,
                    0f,
                    0f,
                    CharacterTestCharacterControlOwner.LocalPlayer),
                definition);
            target = world.Spawn(
                new CharacterTestCharacterSpawnRequest(
                    definition.CharacterId,
                    teamId: 2,
                    x: 0f,
                    y: 0f,
                    z: 0.95f,
                    CharacterTestCharacterControlOwner.None),
                definition);
            attacker.SetMotionState(0f, Vector2.zero, isGrounded: true);
            target.SetMotionState(0f, Vector2.zero, isGrounded: true);
            return world;
        }

        private static CharacterTestCharacterRuntimeWorld CreateFrameSyncAreaConfinementWorld(
            out CharacterTestCharacterEntity attacker,
            out CharacterTestCharacterEntity target)
        {
            CharacterTestCharacterRuntimeWorld world = CreateFrameSyncProjectileWorld(
                out attacker,
                out target,
                includeTarget: true);
            target.SetTransform(new CharacterTestCharacterTransformComponent(0f, 0f, 4f, 180f));
            target.SetMotionState(0f, Vector2.zero, isGrounded: true);
            return world;
        }

        private static CharacterTestCharacterRuntimeWorld CreateFrameSyncPoisonBottlePoolWorld(
            out CharacterTestCharacterEntity attacker,
            out CharacterTestCharacterEntity target)
        {
            CharacterTestCharacterRuntimeWorld world = CreateFrameSyncProjectileWorld(
                out attacker,
                out target,
                includeTarget: true);
            target.SetTransform(new CharacterTestCharacterTransformComponent(0f, 0f, 6f, 180f));
            target.SetMotionState(0f, Vector2.zero, isGrounded: true);
            return world;
        }

        private static CharacterTestCharacterMovementProfileRuntime CreateFrameSyncMovementRuntime()
        {
            return new CharacterTestCharacterMovementProfileRuntime(
                new CharacterMovementProfileConfig(
                    CharacterTestCharacterMovementProfileSource.DefaultProfileId,
                    CharacterMovementMode.Run,
                    walkSpeed: 2f,
                    runSpeed: 4f,
                    acceleration: 100f,
                    deceleration: 100f,
                    turnSpeed: 720f,
                    groundFriction: 0f,
                    airControl: 0.35f,
                    gravity: -30f,
                    jumpImpulse: 8f,
                    slopeLimitDegrees: 45f,
                    locomotionBlendId: "locomotion.move"),
                maxFallSpeed: -50f,
                jumpBufferSeconds: 0.12f,
                coyoteTimeSeconds: 0.1f,
                jumpReleaseVelocityMultiplier: 0.45f,
                apexGravityMultiplier: 0.75f,
                fallGravityMultiplier: 1.35f,
                usedFallback: false,
                sourceDescription: "runtime-ai-lab.frame-sync.test");
        }

        private static CharacterTestCharacterPhysicsRuntime CreateFrameSyncPhysicsRuntime()
        {
            var physicsWorld = new CombatPhysicsWorld();
            RegisterFrameSyncStaticAabb(
                physicsWorld,
                entity: 100,
                body: 100,
                collider: 1,
                layer: 1,
                position: FixVector3.Zero,
                localMin: new FixVector3(-Fix64.FromInt(20), -Fix64.One, -Fix64.FromInt(20)),
                localMax: new FixVector3(Fix64.FromInt(20), Fix64.Zero, Fix64.FromInt(20)));
            return new CharacterTestCharacterPhysicsRuntime(
                physicsWorld,
                CombatPhysicsLayerMask.FromLayer(1),
                new FixVector3(Fix64.Half, Fix64.One, Fix64.Half),
                Fix64.FromRatio(1, 100),
                Fix64.Zero,
                staticColliderCount: 1,
                sourceDescription: "runtime-ai-lab.frame-sync.test");
        }

        private static void RegisterFrameSyncStaticAabb(
            CombatPhysicsWorld world,
            int entity,
            int body,
            int collider,
            int layer,
            FixVector3 position,
            FixVector3 localMin,
            FixVector3 localMax)
        {
            var bodyId = new CombatBodyId(body);
            world.UpsertBody(new CombatPhysicsBody(new CombatEntityId(entity), bodyId, position));
            world.UpsertAabbCollider(new CombatPhysicsAabbCollider(
                bodyId,
                new CombatColliderId(collider),
                layer,
                localMin,
                localMax));
        }

        private static void PlaceFrameSyncTargetOnProjectileLine(
            CharacterTestCharacterPhysicsRuntime physicsRuntime,
            CharacterTestCharacterEntity target,
            CombatProjectileSnapshot projectile,
            Fix64 distance)
        {
            FixVector3 center = projectile.Origin + projectile.Direction * distance;
            Fix64 transformY = center.Y - physicsRuntime.GetCharacterCenterOffsetY(target);
            target.SetTransform(new CharacterTestCharacterTransformComponent(
                ToFrameSyncFloat(center.X),
                ToFrameSyncFloat(transformY),
                ToFrameSyncFloat(center.Z),
                0f));
            SyncFrameSyncCharacterCombatCollider(physicsRuntime, target);
        }

        private static void SyncFrameSyncCharacterCombatCollider(
            CharacterTestCharacterPhysicsRuntime physicsRuntime,
            CharacterTestCharacterEntity target)
        {
            CombatBodyId bodyId = physicsRuntime.GetBodyId(target);
            CombatEntityId entityId = physicsRuntime.GetEntityId(target);
            FixVector3 halfExtents = physicsRuntime.GetCharacterHalfExtents(target);
            FixVector3 centerPosition = new FixVector3(
                ToFrameSyncFix64(target.Transform.X),
                ToFrameSyncFix64(target.Transform.Y) + physicsRuntime.GetCharacterCenterOffsetY(target),
                ToFrameSyncFix64(target.Transform.Z));
            physicsRuntime.World.UpsertBody(new CombatPhysicsBody(entityId, bodyId, centerPosition));
            physicsRuntime.World.UpsertAabbCollider(new CombatPhysicsAabbCollider(
                bodyId,
                new CombatColliderId(CharacterTestCharacterPhysicsRuntime.DynamicCharacterColliderId),
                CharacterTestCharacterPhysicsRuntime.DynamicCharacterLayer,
                new FixVector3(-halfExtents.X, -halfExtents.Y, -halfExtents.Z),
                halfExtents));
        }

        private static long ComputeHookPullStateHash(CharacterTestHookPullRuntimeState state)
        {
            var accumulator = new RuntimeHashAccumulator(RuntimeHashSchemaIds.NetworkSnapshot, RuntimeHashSchemaIds.Version1);
            CharacterTestNetworkHookPullSnapshotCodec.AddStateHash(accumulator, state);
            return accumulator.ToHash();
        }

        private static long ComputeArcMineStateHash(CharacterTestArcMineRuntimeState state)
        {
            var accumulator = new RuntimeHashAccumulator(RuntimeHashSchemaIds.NetworkSnapshot, RuntimeHashSchemaIds.Version1);
            CharacterTestNetworkArcMineSnapshotCodec.AddStateHash(accumulator, state);
            return accumulator.ToHash();
        }

        private static long ComputeRuneTurretStateHash(CharacterTestRuneTurretRuntimeState state)
        {
            var accumulator = new RuntimeHashAccumulator(RuntimeHashSchemaIds.NetworkSnapshot, RuntimeHashSchemaIds.Version1);
            CharacterTestNetworkRuneTurretSnapshotCodec.AddStateHash(accumulator, state);
            return accumulator.ToHash();
        }

        private static long ComputeAreaConfinementStateHash(CharacterTestAreaConfinementRuntimeState state)
        {
            var accumulator = new RuntimeHashAccumulator(RuntimeHashSchemaIds.NetworkSnapshot, RuntimeHashSchemaIds.Version1);
            CharacterTestNetworkAreaConfinementSnapshotCodec.AddStateHash(accumulator, state);
            return accumulator.ToHash();
        }

        private static long ComputePoisonBottlePoolStateHash(CharacterTestMechanicSourceRuntimeState state)
        {
            var accumulator = new RuntimeHashAccumulator(RuntimeHashSchemaIds.NetworkSnapshot, RuntimeHashSchemaIds.Version1);
            CharacterTestNetworkPoisonBottlePoolSnapshotCodec.AddStateHash(accumulator, state);
            return accumulator.ToHash();
        }

        private static long ComputePhysicalShieldStateHash(CharacterTestPhysicalShieldRuntimeState state)
        {
            var accumulator = new RuntimeHashAccumulator(RuntimeHashSchemaIds.NetworkSnapshot, RuntimeHashSchemaIds.Version1);
            CharacterTestNetworkPhysicalShieldSnapshotCodec.AddStateHash(accumulator, state);
            return accumulator.ToHash();
        }

        private static long ComputeInteractionLastFrameHash(CharacterTestInteractionHistory history)
        {
            IReadOnlyList<CharacterTestInteractionEvent> events = history != null
                ? history.LastFrameEvents
                : null;
            int count = events != null ? events.Count : 0;
            var accumulator = new RuntimeHashAccumulator(RuntimeHashSchemaIds.NetworkSnapshot, RuntimeHashSchemaIds.Version1);
            accumulator.BeginArray(1, count);
            for (int i = 0; i < count; i++)
            {
                accumulator.BeginElement(i);
                events[i].Contribute(accumulator, 1);
                accumulator.EndElement();
            }
            accumulator.EndArray();

            return accumulator.ToHash();
        }

        private static float ToFrameSyncFloat(Fix64 value)
        {
            return (float)value.RawValue / Fix64.Scale;
        }

        private static Fix64 ToFrameSyncFix64(float value)
        {
            return Fix64.FromRaw((long)Math.Round(value * Fix64.Scale));
        }
    }
}
