using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using MxFramework.CharacterAction;
using MxFramework.CharacterRuntimeSpawn;
using MxFramework.CharacterRuntimeSpawn.Unity;
using MxFramework.Combat.Core;
using MxFramework.Combat.Physics;
using MxFramework.Core.Math;
using MxFramework.Game.CharacterTest;
using MxFramework.Gameplay;
using MxFramework.Network;
using MxFramework.Resources;
using MxFramework.Runtime;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace MxFramework.Tests.PlayMode.CharacterTest
{
    [Parallelizable(ParallelScope.None)]
    public sealed class CharacterTestLargeCharacterPresencePlayModeTests
    {
        private const double TickSeconds = 1d / 60d;
        private const int MaximumExpectedContactSpeed = 6100;

        [UnityTest]
        public IEnumerator MotionLifecycleAndRollbackMatrix_UsesFormalDragon14Presence()
        {
            yield return null;

            CharacterImportedGeometryBinding dragonBinding = LoadDragon14Geometry();
            int[] translation = RunPair(
                dragonBinding,
                sourcePreviousX: -100,
                sourceX: 0,
                targetX: 900,
                targetZ: 250);
            AssertSuccessfulBoundedContact(translation, "translation");
            Assert.Greater(translation[3], 0);

            int[] turnInPlace = RunPair(
                dragonBinding,
                sourcePreviousX: 0,
                sourceX: 0,
                targetX: 900,
                targetZ: 250,
                previousYaw: -90000,
                currentYaw: 0);
            AssertSuccessfulBoundedContact(turnInPlace, "turn-in-place");
            Assert.Greater(Magnitude(turnInPlace[3], turnInPlace[4]), 0);

            int[] opposing = RunPair(
                dragonBinding,
                sourcePreviousX: 0,
                sourceX: 0,
                targetX: 900,
                targetZ: 250,
                sourceAttemptedVelocityX: 1000,
                targetAttemptedVelocityX: -1000);
            AssertSuccessfulBoundedContact(opposing, "opposing motion");
            Assert.Greater(opposing[2], 0);

            int[] sameCenter = RunPair(
                dragonBinding,
                sourcePreviousX: 0,
                sourceX: 0,
                targetX: 0,
                targetZ: 0);
            AssertSuccessfulBoundedContact(sameCenter, "same center");

            int[] heavy = RunPair(
                dragonBinding,
                sourcePreviousX: -100,
                sourceX: 0,
                targetX: 900,
                targetZ: 250,
                sourceMassScale: 4f);
            int[] light = RunPair(
                dragonBinding,
                sourcePreviousX: -100,
                sourceX: 0,
                targetX: 900,
                targetZ: 250,
                sourceMassScale: 0.25f);
            Assert.Greater(
                Magnitude(heavy[3], heavy[4]),
                Magnitude(light[3], light[4]),
                "mass ratio must affect transfer");

            int[] immovable = RunPair(
                dragonBinding,
                sourcePreviousX: -100,
                sourceX: 0,
                targetX: 900,
                targetZ: 250,
                targetCanBePushed: false);
            Assert.Greater(immovable[2], 0);
            Assert.AreEqual(0, Magnitude(immovable[3], immovable[4]));

            int[] dead = RunPair(
                dragonBinding,
                sourcePreviousX: -100,
                sourceX: 0,
                targetX: 900,
                targetZ: 250,
                sourceDead: true);
            Assert.AreEqual((int)CharacterTestBodyPresenceContactBuildStatus.Success, dead[0]);
            Assert.AreEqual(0, dead[1]);

            CharacterTestCharacterDefinition dragon = FindDefinition("000003");
            CharacterTestCharacterDefinition legacy = FindDefinition("000001");
            var sourceModule = CreateModule(CreatePhysics("lcc-m1-t5.rollback-source"));
            var restoredModule = CreateModule(CreatePhysics("lcc-m1-t5.rollback-restored"));
            using (var sourceHost = new RuntimeHost())
            using (var restoredHost = new RuntimeHost())
            {
                Start(sourceHost, sourceModule);
                Start(restoredHost, restoredModule);
                EnqueueSpawn(sourceModule, dragon, 1, 0f, 0f);
                int sourceFrame = 0;
                Tick(sourceHost, ref sourceFrame);
                Assert.GreaterOrEqual(
                    sourceModule.CaptureBodyPresenceSyncDiagnostics().ProjectedLeafCount,
                    3);
                Assert.IsTrue(sourceModule.World.Remove(sourceModule.Entities[0].Id));
                Tick(sourceHost, ref sourceFrame);
                Assert.AreEqual(
                    0,
                    sourceModule.CaptureBodyPresenceSyncDiagnostics().ProjectedLeafCount);
                sourceModule.World.Clear();
                EnqueueSpawn(sourceModule, legacy, 2, 0.9f, 0.25f);
                EnqueueSpawn(sourceModule, dragon, 1, 0f, 0f);
                EnqueueSpawn(restoredModule, dragon, 1, 0f, 0f);
                EnqueueSpawn(restoredModule, legacy, 2, 0.9f, 0.25f);
                int restoredFrame = 0;
                Tick(sourceHost, ref sourceFrame);
                Tick(restoredHost, ref restoredFrame);

                var sourceParticipant = new CharacterTestNetworkSnapshotParticipant(sourceModule, null);
                var restoredParticipant = new CharacterTestNetworkSnapshotParticipant(restoredModule, null);
                RuntimeFrame rollbackFrame = new RuntimeFrame(sourceFrame);
                CharacterTestNetworkSnapshotDocument rollback =
                    sourceParticipant.CaptureRollbackDocument(rollbackFrame);
                NetworkSnapshotApplyResult applied = restoredParticipant.ApplyRollbackDocument(
                    rollbackFrame,
                    rollback,
                    preserveLocalPredictedEntity: false);
                Assert.IsTrue(applied.Success, applied.Message);
                Assert.AreEqual(
                    sourceParticipant.ComputeStateHash(rollbackFrame),
                    restoredParticipant.ComputeStateHash(rollbackFrame));

                restoredFrame = sourceFrame;
                Tick(sourceHost, ref sourceFrame);
                Tick(restoredHost, ref restoredFrame);
                RuntimeFrame nextFrame = new RuntimeFrame(sourceFrame);
                Assert.AreEqual(
                    sourceParticipant.ComputeStateHash(nextFrame),
                    restoredParticipant.ComputeStateHash(nextFrame),
                    "rollback + 1 tick must preserve derived Presence and contact state");
            }
        }

        [UnityTest]
        public IEnumerator StaticObstacleAndSemanticIsolationMatrix_RemainsBoundedAndLayerSafe()
        {
            yield return null;

            int[][] sweeps =
            {
                RunStaticSweep(StaticLayout.Wall),
                RunStaticSweep(StaticLayout.Corner),
                RunStaticSweep(StaticLayout.Corridor)
            };
            for (int i = 0; i < sweeps.Length; i++)
            {
                Assert.Greater(sweeps[i][0], 0, ((StaticLayout)i).ToString());
                Assert.LessOrEqual(sweeps[i][0], MaximumExpectedContactSpeed, ((StaticLayout)i).ToString());
            }
            int[] wall = sweeps[0];
            Assert.LessOrEqual(wall[1], 1660, "wall sweep must win over soft pressure");
            int[] corner = sweeps[1];
            Assert.LessOrEqual(corner[1], 1660);
            Assert.LessOrEqual(corner[3], 1660);
            int[] corridor = sweeps[2];
            Assert.That(corridor[1], Is.InRange(-290, 290));
            Assert.Greater(corridor[3], corridor[2]);

            CharacterTestCharacterPhysicsRuntime physics = CreatePhysics("lcc-m1-t5.legacy-hard-root");
            CharacterTestCharacterEntity legacySource = CreateEntity(1, 0, 0);
            CharacterTestCharacterEntity legacyTarget = CreateEntity(2, 500, 0);
            CharacterTestCharacterEntity[] legacyEntities = { legacySource, legacyTarget };
            var legacyResolver = new CharacterTestCharacterContactResolver(null);
            legacyResolver.RecordAttemptedContactVelocity(legacySource.Id.Value, 1000, 0);
            legacyResolver.ResolveCharacterContactPressure(
                legacyEntities,
                physics,
                bodyPresenceReady: false,
                bodyPresenceMotionSamples: null,
                bodyPresenceMotionSampleCount: 0,
                bodyPresenceMotionReady: false,
                runSpeedMillimetersPerSecond: 6000,
                accelerationMillimetersPerSecondSquared: 6000,
                turnSpeedMilliDegreesPerSecond: 10800000,
                tickRate: 60);
            legacyResolver.ApplyCharacterContactState(legacyEntities);
            Assert.AreEqual(
                CharacterTestBodyPresenceContactBuildStatus.SourceUnavailable,
                legacyResolver.LastBodyPresenceContactBuildStatus);
            Assert.Greater(legacyTarget.Physics.ContactVelocityXMillimetersPerSecond, 0,
                "Presence off must retain legacy Hard Root pressure");

            Assert.IsFalse(physics.MotionObstacleLayerMask.ContainsLayer(
                CharacterTestCharacterPhysicsRuntime.BodyPresenceLayer));
            Assert.IsFalse(physics.StaticObstacleLayerMask.ContainsLayer(
                CharacterTestCharacterPhysicsRuntime.BodyPresenceLayer));
            Assert.AreNotEqual(
                CharacterTestCharacterPhysicsRuntime.CharacterHurtboxLayer,
                CharacterTestCharacterPhysicsRuntime.BodyPresenceLayer);

        }

        [UnityTest]
        public IEnumerator FourLayerDiagnosticsAppendageExclusionAndNaturalAttackRegression_AreVisible()
        {
            yield return null;

            CharacterTestLargeCharacterPresenceDebugLayer originalLayers =
                CharacterTestLargeCharacterPresenceDebug.EnabledLayers;
            try
            {
                CharacterTestCharacterDefinition dragon = FindDefinition("000003");
                CharacterTestCharacterDefinition legacy = FindDefinition("000001");
                var module = CreateModule(CreatePhysics("lcc-m1-t5.diagnostics"));
                using (var host = new RuntimeHost())
                {
                    Start(host, module);
                    EnqueueSpawn(module, dragon, 1, 0f, 0f);
                    EnqueueSpawn(module, legacy, 2, 0f, -1.8f);
                    int frame = 0;
                    Tick(host, ref frame);

                    CharacterTestCharacterEntity dragonEntity = module.Entities[0];
                    CharacterTestCharacterEntity target = module.Entities[1];
                    int initialHp = module.World.GetCurrentAttribute(
                        target,
                        CharacterTestCharacterAttributeIds.Hp);
                    Assert.IsTrue(CharacterTestLargeCharacterPresenceDebug.TryCapture(
                        module,
                        dragonEntity,
                        out CharacterTestLargeCharacterPresenceDebugSnapshot snapshot));
                    Assert.AreEqual(18, snapshot.AnimatedHurtboxCount);
                    Assert.AreEqual(3, snapshot.Binding.BodyPresenceVolumes.Length);
                    for (int i = 0; i < snapshot.Binding.BodyPresenceVolumes.Length; i++)
                    {
                        string id = snapshot.Binding.BodyPresenceVolumes[i].VolumeId.ToLowerInvariant();
                        Assert.IsFalse(id.Contains("wing") || id.Contains("tail_tip")
                            || id.Contains("head_tip") || id.Contains("trace"));
                    }

                    CharacterTestLargeCharacterPresenceDebugLayer[] layers =
                    {
                        CharacterTestLargeCharacterPresenceDebugLayer.HardRoot,
                        CharacterTestLargeCharacterPresenceDebugLayer.Presence,
                        CharacterTestLargeCharacterPresenceDebugLayer.Hurtbox,
                        CharacterTestLargeCharacterPresenceDebugLayer.ActiveTrace
                    };
                    string[] labels = { "Hard Root", "Body Presence", "Animated Hurtbox", "Active Trace" };
                    for (int i = 0; i < layers.Length; i++)
                    {
                        CharacterTestLargeCharacterPresenceDebug.EnabledLayers = layers[i];
                        CharacterTestDeveloperDebugCard layerCard =
                            CharacterTestLargeCharacterPresenceDebug.CreateCard(snapshot);
                        string text = JoinCard(layerCard);
                        Assert.That(text, Does.Contain("Layer Toggles="));
                        Assert.That(text, Does.Contain(labels[i] + "=on"));
                    }

                    CharacterTestDeveloperDebugCard contactCard =
                        CharacterTestLargeCharacterPresenceDebug.CreateContactDiagnosticsCard(module);
                    string contactText = JoinCard(contactCard);
                    Assert.That(contactText, Does.Contain("Presence Sync="));
                    Assert.That(contactText, Does.Contain("Contact Build="));
                    Assert.That(contactText, Does.Contain("Pressure=iterations="));
                    Assert.That(contactText, Does.Contain("normal=("));
                    Assert.That(contactText, Does.Contain("penetration="));
                    Assert.That(contactText, Does.Contain("pressure="));
                    Assert.That(contactText, Does.Contain("surface=("));
                    Assert.That(contactText, Does.Contain("clampedMax="));
                    for (int i = 0; i < 8; i++)
                        Tick(host, ref frame);
                    Assert.AreEqual(
                        initialHp,
                        module.World.GetCurrentAttribute(target, CharacterTestCharacterAttributeIds.Hp),
                        "idle Presence must not become damage");
                    Assert.AreEqual(0, module.CombatTraceSystem.HitPointDebugFrames.Count,
                        "idle Presence must not become an Active Trace");
                }
            }
            finally
            {
                CharacterTestLargeCharacterPresenceDebug.EnabledLayers = originalLayers;
            }

            IEnumerator naturalAttackRegression =
                new CharacterTestDragon14NaturalAttackPlayModeTests()
                    .BakedSocketCapsulesDamageTrainingTargetWithPerEventDeduplication();
            while (naturalAttackRegression.MoveNext())
                yield return naturalAttackRegression.Current;
        }

        private static int[] RunPair(
            CharacterImportedGeometryBinding sourceBinding,
            int sourcePreviousX,
            int sourceX,
            int targetX,
            int targetZ,
            int previousYaw = 0,
            int currentYaw = 0,
            int sourceAttemptedVelocityX = 0,
            int targetAttemptedVelocityX = 0,
            float sourceMassScale = 1f,
            bool targetCanBePushed = true,
            bool sourceDead = false)
        {
            CharacterTestCharacterPhysicsRuntime physics = CreatePhysics("lcc-m1-t5.pair");
            CharacterTestCharacterEntity source = CreateEntity(1, sourceX, 0, currentYaw);
            CharacterTestCharacterEntity target = CreateEntity(2, targetX, targetZ);
            CharacterTestCharacterPhysicsComponent sourcePhysics = source.Physics.WithRuntimeModifiers(
                sourceMassScale, 1f, 1f, blocksCharacters: true, canBePushed: true);
            CharacterTestCharacterPhysicsComponent targetPhysics = target.Physics.WithRuntimeModifiers(
                1f, 1f, 1f, blocksCharacters: true, canBePushed: targetCanBePushed);
            source.SetPhysics(sourcePhysics);
            target.SetPhysics(targetPhysics);
            source.SetColliderPosture(CharacterTestColliderPostureComponent.FromPhysics(sourcePhysics, 0));
            target.SetColliderPosture(CharacterTestColliderPostureComponent.FromPhysics(targetPhysics, 0));
            if (sourceDead)
                SetDead(source);
            CharacterTestCharacterEntity[] entities = { source, target };
            var presence = new CharacterTestCharacterBodyPresenceSystem();
            Assert.IsTrue(presence.SyncAll(
                entities,
                physics,
                new[] { sourceBinding, CharacterImportedGeometryBinding.Empty }));
            CharacterTestBodyPresenceMotionSample[] samples =
            {
                CreateSample(source, sourcePreviousX, 0, previousYaw, sourceBinding, true),
                CreateSample(target, targetX, targetZ, 0, null, false)
            };
            var resolver = new CharacterTestCharacterContactResolver(null);
            if (sourceAttemptedVelocityX != 0)
                resolver.RecordAttemptedContactVelocity(source.Id.Value, sourceAttemptedVelocityX, 0);
            if (targetAttemptedVelocityX != 0)
                resolver.RecordAttemptedContactVelocity(target.Id.Value, targetAttemptedVelocityX, 0);
            resolver.ResolveCharacterContactPressure(
                entities,
                physics,
                bodyPresenceReady: true,
                samples,
                samples.Length,
                bodyPresenceMotionReady: true,
                runSpeedMillimetersPerSecond: 6000,
                accelerationMillimetersPerSecondSquared: 6000,
                turnSpeedMilliDegreesPerSecond: 10800000,
                tickRate: 60);
            resolver.ApplyCharacterContactState(entities);
            return new[]
            {
                (int)resolver.LastBodyPresenceContactBuildStatus,
                resolver.LastBodyPresenceConstraintCount,
                target.Physics.ContactCount,
                target.Physics.ContactVelocityXMillimetersPerSecond,
                target.Physics.ContactVelocityYMillimetersPerSecond
            };
        }

        private static int[] RunStaticSweep(StaticLayout layout)
        {
            var world = new CombatPhysicsWorld();
            RegisterStaticAabb(world, 100, 100,
                new FixVector3(-Fix64.FromInt(20), -Fix64.One, -Fix64.FromInt(20)),
                new FixVector3(Fix64.FromInt(20), Fix64.Zero, Fix64.FromInt(20)));
            int targetX;
            int targetZ;
            CharacterBodyPresenceRuntimeBinding volume;
            if (layout == StaticLayout.Corridor)
            {
                RegisterStaticAabb(world, 101, 101,
                    new FixVector3(Meters(-1.2f), Fix64.Zero, Meters(-4f)),
                    new FixVector3(Meters(-0.8f), Fix64.FromInt(3), Meters(4f)));
                RegisterStaticAabb(world, 102, 102,
                    new FixVector3(Meters(0.8f), Fix64.Zero, Meters(-4f)),
                    new FixVector3(Meters(1.2f), Fix64.FromInt(3), Meters(4f)));
                targetX = 0;
                targetZ = 0;
                volume = SphereAt("corridor", 0f, -0.5f, 1f);
            }
            else
            {
                RegisterStaticAabb(world, 101, 101,
                    new FixVector3(Meters(2f), Fix64.Zero, Meters(-4f)),
                    new FixVector3(Meters(3f), Fix64.FromInt(3), Meters(4f)));
                if (layout == StaticLayout.Corner)
                {
                    RegisterStaticAabb(world, 102, 102,
                        new FixVector3(Meters(-4f), Fix64.Zero, Meters(2f)),
                        new FixVector3(Meters(4f), Fix64.FromInt(3), Meters(3f)));
                }
                targetX = 1400;
                targetZ = layout == StaticLayout.Corner ? 1400 : 0;
                volume = SphereAt(
                    layout == StaticLayout.Corner ? "corner" : "wall",
                    1f,
                    layout == StaticLayout.Corner ? 1f : 0f,
                    1f);
            }

            var physics = new CharacterTestCharacterPhysicsRuntime(
                world,
                CombatPhysicsLayerMask.FromLayer(1),
                new FixVector3(Fix64.Half, Fix64.One, Fix64.Half),
                Fix64.FromRatio(1, 100),
                Fix64.Half,
                layout == StaticLayout.Corner || layout == StaticLayout.Corridor ? 3 : 2,
                "lcc-m1-t5.static-" + layout);
            var runtimeWorld = new CharacterTestCharacterRuntimeWorld();
            var definition = new CharacterTestCharacterDefinition(
                1, "LCC M1 T5 World Character", default(ResourceKey), 1,
                default(CharacterRuntimeAnimationBinding), CharacterTestCharacterPhysicsComponent.Default);
            CharacterTestCharacterEntity source = runtimeWorld.Spawn(
                CharacterTestCharacterSpawnRequest.FromDeterministic(
                    1, 1, 0, 0, 0, CharacterTestCharacterControlOwner.None),
                definition);
            CharacterTestCharacterEntity target = runtimeWorld.Spawn(
                CharacterTestCharacterSpawnRequest.FromDeterministic(
                    1, 1, targetX, 0, targetZ, CharacterTestCharacterControlOwner.None),
                definition);
            source.SetMotionStateDeterministic(0, 0, 0, true);
            target.SetMotionStateDeterministic(0, 0, 0, true);
            var binding = new CharacterImportedGeometryBinding(
                "mx.characterGeometryBinding.v4", "4.0", "lcc-m1-t5", null,
                Array.Empty<CharacterBodyColliderRuntimeBinding>(),
                Array.Empty<CharacterHitZoneRuntimeBinding>(),
                Array.Empty<CharacterDamageZoneRuntimeBinding>(),
                Array.Empty<CharacterSocketRuntimeBinding>(),
                Array.Empty<CharacterWeaponAttachmentRuntimeBinding>(),
                Array.Empty<CharacterWeaponTraceRuntimeBinding>(),
                bodyPresenceVolumes: new[] { volume },
                bodyPresenceRevision: 1,
                bodyPresenceContentHash: "sha256:" + new string('5', 64),
                bodyPresenceMode: CharacterBodyPresenceMode.Authored);
            var presence = new CharacterTestCharacterBodyPresenceSystem();
            Assert.IsTrue(presence.SyncAll(
                runtimeWorld.Entities,
                physics,
                new[] { binding, CharacterImportedGeometryBinding.Empty }));
            CharacterTestBodyPresenceMotionSample[] samples =
            {
                CreateSample(source, 0, layout == StaticLayout.Corridor ? -100 : 0, 0, binding, true),
                CreateSample(target, targetX, targetZ, 0, null, false)
            };
            var resolver = new CharacterTestCharacterContactResolver(null);
            resolver.ResolveCharacterContactPressure(
                runtimeWorld.Entities,
                physics,
                true,
                samples,
                samples.Length,
                true,
                6000,
                6000,
                10800000,
                60);
            resolver.ApplyCharacterContactState(runtimeWorld.Entities);
            int contactSpeed = Magnitude(
                target.Physics.ContactVelocityXMillimetersPerSecond,
                target.Physics.ContactVelocityYMillimetersPerSecond);
            int zBefore = target.Transform.ZMillimeters;
            var motion = new CharacterTestCharacterMotionSystem();
            motion.SetPhysicsRuntime(physics);
            motion.Tick(runtimeWorld, tickRate: 60);
            return new[] { contactSpeed, target.Transform.XMillimeters, zBefore, target.Transform.ZMillimeters };
        }

        private static CharacterTestCharacterRuntimeModule CreateModule(
            CharacterTestCharacterPhysicsRuntime physics)
        {
            return new CharacterTestCharacterRuntimeModule(
                null,
                NullRuntimeLogger.Instance,
                physicsRuntime: physics,
                traceRecorder: CharacterTestTraceRecorder.CreateInMemory(4096),
                createViews: false);
        }

        private static void Start(RuntimeHost host, CharacterTestCharacterRuntimeModule module)
        {
            host.RegisterModule(module);
            host.Initialize();
            host.Start();
        }

        private static void EnqueueSpawn(
            CharacterTestCharacterRuntimeModule module,
            CharacterTestCharacterDefinition definition,
            int team,
            float x,
            float z)
        {
            Assert.IsTrue(module.EnqueueSpawn(new CharacterTestCharacterSpawnRequest(
                definition.CharacterId,
                team,
                x,
                0f,
                z,
                CharacterTestCharacterControlOwner.LocalPlayer)));
        }

        private static void Tick(RuntimeHost host, ref int frame)
        {
            host.Tick(frame, TickSeconds, frame * TickSeconds);
            frame++;
        }

        private static CharacterTestCharacterDefinition FindDefinition(string packageId)
        {
            var registry = new CharacterTestCharacterDefinitionRegistry();
            for (int i = 0; i < registry.Definitions.Count; i++)
            {
                CharacterTestCharacterDefinition definition = registry.Definitions[i];
                if (definition.GeneratedPackageRoot.Replace('\\', '/').EndsWith(
                        "/" + packageId,
                        StringComparison.OrdinalIgnoreCase))
                    return definition;
            }
            Assert.Fail("Generated package was not discovered: " + packageId);
            return default;
        }

        private static CharacterImportedGeometryBinding LoadDragon14Geometry()
        {
            CharacterTestCharacterDefinition definition = FindDefinition("000003");
            CharacterImportedPackage package = CharacterImportedPackageJson.LoadFromDirectory(
                Path.GetFullPath(definition.GeneratedPackageRoot));
            Assert.NotNull(package);
            Assert.NotNull(package.Geometry);
            return package.Geometry;
        }

        private static CharacterTestCharacterPhysicsRuntime CreatePhysics(string description)
        {
            var world = new CombatPhysicsWorld();
            RegisterStaticAabb(world, 100, 100,
                new FixVector3(-Fix64.FromInt(20), -Fix64.One, -Fix64.FromInt(20)),
                new FixVector3(Fix64.FromInt(20), Fix64.Zero, Fix64.FromInt(20)));
            return new CharacterTestCharacterPhysicsRuntime(
                world,
                CombatPhysicsLayerMask.FromLayer(1),
                new FixVector3(Fix64.Half, Fix64.One, Fix64.Half),
                Fix64.FromRatio(1, 100),
                Fix64.Half,
                1,
                description);
        }

        private static CharacterTestCharacterEntity CreateEntity(
            int id,
            int xMillimeters,
            int zMillimeters,
            int yawMilliDegrees = 0)
        {
            var entity = new CharacterTestCharacterEntity(
                new CharacterTestCharacterEntityId(id),
                new CharacterTestCharacterIdentityComponent(
                    1,
                    "LCC M1 T5 Character",
                    default(ResourceKey),
                    default(CharacterRuntimeAnimationBinding)),
                new CharacterTestCharacterTeamComponent(1, 1),
                CharacterTestCharacterTransformComponent.FromDeterministic(
                    xMillimeters,
                    0,
                    zMillimeters,
                    yawMilliDegrees),
                new CharacterTestCharacterEquipmentComponent(false),
                new CharacterTestCharacterControlComponent(CharacterTestCharacterControlOwner.None),
                new GameplayEntityId(id, 1),
                CharacterTestCharacterPhysicsComponent.Default);
            entity.SetMotionStateDeterministic(0, 0, 0, true);
            return entity;
        }

        private static void SetDead(CharacterTestCharacterEntity entity)
        {
            entity.ComponentWorld.SetBattleState(entity.Id, new CharacterTestBattleStateComponent(
                CharacterTestCharacterLifeState.Dead,
                CharacterTestBattleStateDominantReason.Death,
                CharacterTestBattleGateFlags.None,
                1,
                1,
                CharacterActionId.None,
                0));
        }

        private static CharacterTestBodyPresenceMotionSample CreateSample(
            CharacterTestCharacterEntity entity,
            int previousX,
            int previousZ,
            int previousYaw,
            CharacterImportedGeometryBinding binding,
            bool rotationWitnessValid)
        {
            return new CharacterTestBodyPresenceMotionSample(
                entity.Id.Value,
                previousX,
                entity.Transform.YMillimeters,
                previousZ,
                previousYaw,
                entity.Transform.XMillimeters,
                entity.Transform.YMillimeters,
                entity.Transform.ZMillimeters,
                entity.Transform.YawMilliDegrees,
                binding,
                rotationWitnessValid);
        }

        private static CharacterBodyPresenceRuntimeBinding SphereAt(
            string id,
            float x,
            float z,
            float radius)
        {
            return new CharacterBodyPresenceRuntimeBinding(
                id,
                CharacterBodyPresenceReferenceFrame.ModelRoot,
                CharacterBodyPresenceShape.Sphere,
                new CharacterRuntimePose(
                    "ModelRoot",
                    string.Empty,
                    new CharacterRuntimeVector3(x, 0.93f, z),
                    new CharacterRuntimeQuaternion(0f, 0f, 0f, 1f),
                    new CharacterRuntimeVector3(1f, 1f, 1f)),
                radius,
                0f,
                default,
                0,
                1000,
                true);
        }

        private static void RegisterStaticAabb(
            CombatPhysicsWorld world,
            int entityId,
            int bodyId,
            FixVector3 min,
            FixVector3 max)
        {
            var id = new CombatBodyId(bodyId);
            world.UpsertBody(new CombatPhysicsBody(new CombatEntityId(entityId), id, FixVector3.Zero));
            world.UpsertAabbCollider(new CombatPhysicsAabbCollider(
                id,
                new CombatColliderId(1),
                1,
                min,
                max));
        }

        private static void AssertSuccessfulBoundedContact(int[] result, string scenario)
        {
            Assert.AreEqual((int)CharacterTestBodyPresenceContactBuildStatus.Success, result[0], scenario);
            Assert.Greater(result[1], 0, scenario);
            Assert.LessOrEqual(Magnitude(result[3], result[4]), MaximumExpectedContactSpeed, scenario);
        }

        private static string JoinCard(CharacterTestDeveloperDebugCard card)
        {
            Assert.NotNull(card);
            var lines = new List<string>(card.Lines.Count);
            for (int i = 0; i < card.Lines.Count; i++)
                lines.Add(card.Lines[i].Name + "=" + card.Lines[i].Value);
            return string.Join("\n", lines);
        }

        private static Fix64 Meters(float value) =>
            CharacterTestDeterministicUnits.MillimetersToFix64Meters(
                CharacterTestDeterministicUnits.MetersToMillimeters(value));

        private static int Magnitude(int x, int z) =>
            (int)Math.Ceiling(Math.Sqrt((double)x * x + (double)z * z));

        private enum StaticLayout
        {
            Wall,
            Corner,
            Corridor
        }

    }
}
