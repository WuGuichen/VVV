using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using MxFramework.Animation;
using MxFramework.CharacterAction;
using MxFramework.CharacterRuntimeSpawn;
using MxFramework.Combat.Core;
using MxFramework.Combat.Physics;
using MxFramework.Core.Math;
using MxFramework.Game.CharacterTest;
using MxFramework.Runtime;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace MxFramework.Tests.PlayMode.CharacterTest
{
    [Parallelizable(ParallelScope.None)]
    public sealed class CharacterTestDragon14AuthorityTerrainPosePlayModeTests
    {
        private const int GroundLayer = 0;
        private const int ClipLocalFrame = 10;
        private const string ClipKey = "char.000003.animation.attack1a";
        private static readonly CharacterActionId ActionId = new CharacterActionId(940000);
        private const string AnimationActionKey = "000003.attack1a";
        private const string BaseSourceHash = "lcc-m4-t5-000003-playable-base";
        private const int LeftForeColliderId = 133;
        private const int TailPartStateId = 43;

        [UnityTest]
        public IEnumerator FormalDragon14_TerrainMotionAndFailureMatrix_IsDeterministic()
        {
            yield return null;
            DragonAssets assets = LoadDragonAssets();

            DragonFixture flat = CreateFixture(assets, CreateFlatWorld());
            Tick(flat, 1, 2);
            AssertAllFailures(flat, CharacterTestAuthorityPoseFailure.Success);

            DragonFixture uphill = CreateFixture(
                assets,
                CreateProbeAlignedWorld(
                    assets,
                    slopeSinMilli: 2588,
                    slopeCosineMilli: 9659,
                    farReach: false));
            Tick(uphill, 10, 11);
            AssertAllFailures(uphill, CharacterTestAuthorityPoseFailure.Success);

            DragonFixture downhill = CreateFixture(
                assets,
                CreateProbeAlignedWorld(
                    assets,
                    slopeSinMilli: -2588,
                    slopeCosineMilli: 9659,
                    farReach: false));
            Tick(downhill, 20, 21);
            AssertAllFailures(downhill, CharacterTestAuthorityPoseFailure.Success);

            DragonFixture step = CreateFixture(assets, CreateStepWorld());
            Tick(step, 30, 31);
            AssertAllFailures(step, CharacterTestAuthorityPoseFailure.Success);
            AssertDistinctTargetHeights(step, minimumDistinctCount: 2);

            DragonFixture uneven = CreateFixture(assets, CreateUnevenWorld());
            Tick(uneven, 40, 41);
            AssertAllFailures(uneven, CharacterTestAuthorityPoseFailure.Success);
            AssertDistinctTargetHeights(uneven, minimumDistinctCount: 4);

            DragonFixture noHit = CreateFixture(assets, new CombatPhysicsWorld());
            Tick(noHit, 50, 51);
            AssertAllFailures(noHit, CharacterTestAuthorityPoseFailure.NoHit);

            DragonFixture tooSteep = CreateFixture(
                assets,
                CreateProbeAlignedWorld(
                    assets,
                    slopeSinMilli: 9165,
                    slopeCosineMilli: 4000,
                    farReach: false));
            Tick(tooSteep, 60, 61);
            AssertContainsFailure(tooSteep, CharacterTestAuthorityPoseFailure.TooSteep);

            DragonFixture outOfReach = CreateFixture(
                assets,
                CreateProbeAlignedWorld(
                    assets,
                    slopeSinMilli: 0,
                    slopeCosineMilli: 10000,
                    farReach: true));
            Tick(outOfReach, 70, 71);
            AssertContainsFailure(outOfReach, CharacterTestAuthorityPoseFailure.OutOfReach);

            CharacterTestTerrainIkLimbState[] beforeMove = CopyLimbStates(flat);
            CharacterTestCharacterTransformComponent beforeTransform = flat.Entity.Transform;
            var movedTransform = new CharacterTestCharacterTransformComponent(3f, 0f, 2f, 90f);
            flat.Entity.SetTransform(movedTransform);
            flat.Entity.SetMotionState(
                verticalVelocity: 0f,
                lastMoveInput: Vector2.up,
                isGrounded: true,
                movementMode: CharacterMovementMode.Run);
            Tick(flat, 80, 81);
            AssertAllFailures(flat, CharacterTestAuthorityPoseFailure.Success);
            for (int i = 0; i < flat.Entity.TerrainIkState.LimbCount; i++)
            {
                FixVector3 local = CharacterTestAuthorityPoseTerrainMath.WorldPointToModel(
                    beforeMove[i].LastValidTargetWorld,
                    beforeTransform);
                FixVector3 expected = CharacterTestAuthorityPoseTerrainMath.ModelPointToWorld(
                    local,
                    movedTransform);
                AssertVectorWithinRawTolerance(
                    expected,
                    flat.Entity.TerrainIkState.GetLimb(i).LastValidTargetWorld,
                    tolerance: 32L);
            }

            TestContext.Out.WriteLine(
                "lcc_m4_t5_000003_matrix flat=success uphill=success downhill=success"
                + " step=success uneven=success no_hit=NoHit too_steep=TooSteep"
                + " out_of_reach=OutOfReach move_turn=success limbs=4 channels=12");
        }

        [UnityTest]
        public IEnumerator FormalDragon14_EligibilityAndTailSeverance_FailClosedWithoutInventedLimbMetadata()
        {
            yield return null;
            DragonAssets assets = LoadDragonAssets();
            for (int i = 0; i < assets.Rig.Limbs.Length; i++)
            {
                Assert.AreEqual(0, assets.Rig.Limbs[i].DisabledActionTags.Length);
                Assert.IsTrue(assets.Rig.Limbs[i].PartStateId == 0);
            }

            DragonFixture flying = CreateFixture(assets, CreateFlatWorld());
            flying.Entity.SetMotionState(
                verticalVelocity: 0f,
                lastMoveInput: Vector2.zero,
                isGrounded: false,
                movementMode: CharacterMovementMode.Airborne);
            Tick(flying, 100);
            AssertAllFailures(flying, CharacterTestAuthorityPoseFailure.Flying);

            DragonFixture knockedBack = CreateFixture(assets, CreateFlatWorld());
            knockedBack.Entity.SetSpecialHitState(CharacterTestSpecialHitStateComponent.Launched(
                frame: 101,
                sourceEntityId: 9,
                traceId: 10,
                launchVelocityYMillimetersPerSecond: 6000,
                horizontalImpulseMillimetersPerSecond: 2000));
            Tick(knockedBack, 102);
            AssertAllFailures(knockedBack, CharacterTestAuthorityPoseFailure.KnockedBack);

            DragonFixture dead = CreateFixture(assets, CreateFlatWorld());
            dead.Entity.BeginDeath(frame: 103, sourceEntityId: 9, traceId: 11);
            Tick(dead, 104);
            AssertAllFailures(dead, CharacterTestAuthorityPoseFailure.Dead);

            DragonFixture severed = CreateFixture(assets, CreateFlatWorld());
            Tick(severed, 110, 111);
            var physicsRuntime = new CharacterTestCharacterPhysicsRuntime(
                severed.Physics,
                CombatPhysicsLayerMask.FromLayer(2),
                new FixVector3(Fix64.Half, Fix64.One, Fix64.Half),
                Fix64.FromRatio(1, 100),
                Fix64.Half,
                staticColliderCount: 1,
                sourceDescription: "lcc-m4-t5.000003-severance");
            var hurtboxes = new CharacterTestCharacterHurtboxSystem();
            for (int i = 0; i < severed.World.Entities.Count; i++) hurtboxes.PrepareEntity(severed.World.Entities[i]);
            hurtboxes.SyncAll(severed.World.Entities, physicsRuntime, new RuntimeFrame(111));
            SeverPart(severed.Entity, TailPartStateId, breakFrame: 112, severFrame: 113);
            Tick(severed, 114);
            hurtboxes.SyncAll(severed.World.Entities, physicsRuntime, new RuntimeFrame(114));
            AssertAllFailures(severed, CharacterTestAuthorityPoseFailure.Success);
            Assert.IsTrue(hurtboxes.TryGetDamageGeometrySnapshot(
                severed.Entity.Id.Value,
                out CharacterTestDamageGeometrySnapshot snapshot));
            Assert.AreEqual(15, snapshot.ColliderCount);

            TestContext.Out.WriteLine(
                "lcc_m4_t5_000003_eligibility flying=Flying knocked_back=KnockedBack dead=Dead"
                + " tail_severed_hurtboxes=15 formal_disabled_action_tags=0"
                + " formal_limb_part_state_ids=0 generic_action_and_limb_severed_contract=separate");
        }

        [UnityTest]
        public IEnumerator TerrainActiveDragon14_ViewHurtboxAndClawSocketsShareFrame_AndCaptureGameView()
        {
            yield return null;
            DragonAssets assets = LoadDragonAssets();
            DragonFixture fixture = CreateFixture(assets, CreateUnevenWorld());
            var physicsRuntime = new CharacterTestCharacterPhysicsRuntime(
                fixture.Physics,
                CombatPhysicsLayerMask.FromLayer(2),
                new FixVector3(Fix64.Half, Fix64.One, Fix64.Half),
                Fix64.FromRatio(1, 100),
                Fix64.Half,
                staticColliderCount: 4,
                sourceDescription: "lcc-m4-t5.000003-game-view");
            var hurtboxes = new CharacterTestCharacterHurtboxSystem();
            for (int i = 0; i < fixture.World.Entities.Count; i++) hurtboxes.PrepareEntity(fixture.World.Entities[i]);
            hurtboxes.SyncAll(fixture.World.Entities, physicsRuntime, new RuntimeFrame(120));
            Assert.IsTrue(hurtboxes.TryGetDamageGeometrySnapshot(
                fixture.Entity.Id.Value,
                out CharacterTestDamageGeometrySnapshot baseline));
            Assert.IsTrue(baseline.TryGetCollider(
                CharacterTestAuthoredContentIds.D400_Id133,
                out CharacterTestDamageGeometryColliderMetadata baselineCollider));

            Tick(fixture, 121, 122);
            AssertAllFailures(fixture, CharacterTestAuthorityPoseFailure.Success);
            hurtboxes.SyncAll(fixture.World.Entities, physicsRuntime, new RuntimeFrame(122));
            Assert.IsTrue(hurtboxes.TryGetDamageGeometrySnapshot(
                fixture.Entity.Id.Value,
                out CharacterTestDamageGeometrySnapshot resolved));
            Assert.IsTrue(resolved.TryGetCollider(
                CharacterTestAuthoredContentIds.D400_Id133,
                out CharacterTestDamageGeometryColliderMetadata resolvedCollider));
            Assert.IsTrue(CharacterTestAuthorityPoseConsumerMath.TryResolveAttachmentDelta(
                fixture.Entity,
                assets.Rig,
                assets.Catalog,
                CharacterAuthorityPoseRigAttachmentKind.Collider,
                LeftForeColliderId,
                ClipKey,
                localFrame: ClipLocalFrame,
                loop: true,
                new RuntimeFrame(122),
                out CharacterTestAuthorityPoseAttachmentDelta hurtboxDelta));
            AssertVectorWithinRawTolerance(
                QuantizeMillimeters(hurtboxDelta.TransformPoint(baselineCollider.LocalShape.PointA)),
                resolvedCollider.LocalShape.PointA,
                tolerance: 16L);
            AssertVectorWithinRawTolerance(
                QuantizeMillimeters(hurtboxDelta.TransformPoint(baselineCollider.LocalShape.PointB)),
                resolvedCollider.LocalShape.PointB,
                tolerance: 16L);

            string resourcePath = "MxFrameworkGenerated/CharacterPackages/000003/prefabs/000003_character_preview";
            GameObject prefab = UnityEngine.Resources.Load<GameObject>(resourcePath);
            Assert.NotNull(prefab, resourcePath);
            GameObject instance = UnityEngine.Object.Instantiate(prefab);
            CharacterTestRuntimeCharacterView view = null;
            var visuals = new List<GameObject>();
            var materials = new List<Material>();
            var disabledCameras = new List<Camera>();
            Camera evidenceCamera = null;
            Light evidenceLight = null;
            try
            {
                instance.name = "LCC-M4-T5 Dragon14 Terrain Authority";
                instance.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                Animator animator = instance.GetComponentInChildren<Animator>(true);
                if (animator != null)
                    animator.enabled = false;
                view = new CharacterTestRuntimeCharacterView(
                    fixture.Entity.Id,
                    instance,
                    null,
                    null,
                    null,
                    boneSync: null,
                    proceduralBoneRig: null,
                    authorityPoseRig: assets.Rig);
                view.SyncAnimation(fixture.Entity.Animation, new RuntimeFrame(122), presentationPaused: true);
                view.SyncAuthorityPose(
                    fixture.Entity.AuthorityPose,
                    fixture.Entity.TerrainIkState,
                    fixture.Entity.ResolvedAuthorityPoseFrame,
                    new RuntimeFrame(122));
                view.TickPresentation(0f, new RuntimeFrame(122));

                Assert.AreEqual(12, fixture.Entity.ResolvedAuthorityPoseFrame.Count);
                Assert.NotNull(animator);
                for (int i = 0; i < assets.Rig.Channels.Length; i++)
                {
                    CharacterAuthorityPoseChannelRuntimeBinding channel = assets.Rig.Channels[i];
                    Assert.IsTrue(fixture.Entity.ResolvedAuthorityPoseFrame.TryGetSample(
                        channel.ChannelId,
                        out CharacterTestResolvedAuthorityPoseSample sample));
                    Transform bone = animator.transform.Find(channel.BonePath);
                    Assert.NotNull(bone, channel.BonePath);
                    Vector3 expected = instance.transform.position
                        + instance.transform.rotation * ToVector3(sample.Position);
                    Assert.That(Vector3.Distance(expected, bone.position), Is.LessThan(0.0001f), "Channel " + channel.ChannelId);
                }

                Material probeMaterial = CreateMaterial(new Color(0.1f, 1f, 0.2f), materials);
                Material channelMaterial = CreateMaterial(new Color(0f, 0.9f, 1f), materials);
                Material hurtboxMaterial = CreateMaterial(new Color(1f, 0.15f, 0.12f), materials);
                Material socketMaterial = CreateMaterial(new Color(1f, 0.85f, 0.05f), materials);
                Material groundMaterial = CreateMaterial(new Color(0.2f, 0.28f, 0.36f), materials);
                AddUnevenVisualGround(visuals, groundMaterial);

                for (int i = 0; i < fixture.Entity.TerrainIkState.LimbCount; i++)
                {
                    CharacterTestTerrainIkLimbState limb = fixture.Entity.TerrainIkState.GetLimb(i);
                    AddMarker(visuals, "probe-target-" + i, ToVector3(limb.LastValidTargetWorld), 0.16f, probeMaterial);
                    Assert.IsTrue(fixture.Entity.ResolvedAuthorityPoseFrame.TryGetSample(
                        assets.Rig.Limbs[i].TipChannelId,
                        out CharacterTestResolvedAuthorityPoseSample tip));
                    FixVector3 tipWorld = CharacterTestAuthorityPoseTerrainMath.ModelPointToWorld(
                        tip.Position,
                        fixture.Entity.Transform);
                    AddMarker(visuals, "resolved-tip-" + i, ToVector3(tipWorld), 0.12f, channelMaterial);
                    TestContext.Out.WriteLine(
                        "lcc_m4_t5_same_frame limb=" + assets.Rig.Limbs[i].ProbeId
                        + " frame=122 revision=" + fixture.Entity.ResolvedAuthorityPoseFrame.Revision
                        + " target_raw=" + FormatRaw(limb.LastValidTargetWorld)
                        + " tip_raw=" + FormatRaw(tipWorld));
                }

                FixVector3 hurtboxCenter = (resolvedCollider.WorldMin + resolvedCollider.WorldMax) * Fix64.Half;
                AddMarker(visuals, "resolved-hurtbox", ToVector3(hurtboxCenter), 0.22f, hurtboxMaterial);
                AddSocketMarkers(fixture, assets, visuals, socketMaterial);
                TestContext.Out.WriteLine(
                    "lcc_m4_t5_same_frame consumer=hurtbox id=" + LeftForeColliderId
                    + " frame=" + resolved.Frame.Value
                    + " center_raw=" + FormatRaw(hurtboxCenter)
                    + " contact_points=" + assets.Package.Configs.BodyPartContactPoints.Length);

                for (int i = 0; i < Camera.allCamerasCount; i++)
                {
                    Camera camera = Camera.allCameras[i];
                    if (camera != null && camera.enabled)
                    {
                        camera.enabled = false;
                        disabledCameras.Add(camera);
                    }
                }
                GameObject cameraObject = new GameObject("LCC-M4-T5 Evidence Camera");
                visuals.Add(cameraObject);
                evidenceCamera = cameraObject.AddComponent<Camera>();
                evidenceCamera.clearFlags = CameraClearFlags.SolidColor;
                evidenceCamera.backgroundColor = new Color(0.055f, 0.075f, 0.11f);
                evidenceCamera.fieldOfView = 42f;
                evidenceCamera.transform.position = new Vector3(7.3f, 4.1f, -8.7f);
                evidenceCamera.transform.rotation = Quaternion.LookRotation(
                    new Vector3(0f, 1.2f, 0.35f) - evidenceCamera.transform.position,
                    Vector3.up);
                evidenceCamera.enabled = true;

                GameObject lightObject = new GameObject("LCC-M4-T5 Evidence Light");
                visuals.Add(lightObject);
                evidenceLight = lightObject.AddComponent<Light>();
                evidenceLight.type = LightType.Directional;
                evidenceLight.intensity = 1.4f;
                evidenceLight.transform.rotation = Quaternion.Euler(42f, -32f, 0f);

                yield return null;
                view.TickPresentation(0f, new RuntimeFrame(122));
                yield return new WaitForEndOfFrame();
                string screenshotPath = GetScreenshotPath();
                Directory.CreateDirectory(Path.GetDirectoryName(screenshotPath));
                if (File.Exists(screenshotPath))
                    File.Delete(screenshotPath);
                ScreenCapture.CaptureScreenshot(screenshotPath, 1);
                for (int i = 0; i < 8 && !File.Exists(screenshotPath); i++)
                    yield return new WaitForEndOfFrame();
                Assert.IsTrue(File.Exists(screenshotPath), screenshotPath);
                Assert.Greater(new FileInfo(screenshotPath).Length, 4096L);
                TestContext.Out.WriteLine("lcc_m4_t5_game_view_screenshot=" + screenshotPath);
            }
            finally
            {
                if (evidenceCamera != null)
                    evidenceCamera.enabled = false;
                if (evidenceLight != null)
                    evidenceLight.enabled = false;
                for (int i = 0; i < disabledCameras.Count; i++)
                {
                    if (disabledCameras[i] != null)
                        disabledCameras[i].enabled = true;
                }
                if (view != null)
                    view.Release();
                if (instance != null)
                    UnityEngine.Object.Destroy(instance);
                for (int i = 0; i < visuals.Count; i++)
                {
                    if (visuals[i] != null)
                        UnityEngine.Object.Destroy(visuals[i]);
                }
                for (int i = 0; i < materials.Count; i++)
                {
                    if (materials[i] != null)
                        UnityEngine.Object.Destroy(materials[i]);
                }
            }
            yield return null;
        }

        private static DragonAssets LoadDragonAssets()
        {
            CharacterTestCharacterDefinition definition = FindDragonDefinition();
            CharacterImportedPackage package =
                CharacterTestGeneratedPackageSource.LoadPackage(definition.GeneratedPackageRoot);
            Assert.NotNull(package);
            Assert.AreEqual(18, package.Geometry.BodyColliders.Length);
            CharacterAuthorityPoseRigRuntimeBinding rig = package.Geometry.AuthorityPoseRig;
            Assert.NotNull(rig);
            Assert.IsTrue(rig.IsAuthored);
            Assert.AreEqual(4, rig.Limbs.Length);
            Assert.AreEqual(12, rig.Channels.Length);
            Assert.IsTrue(CharacterTestDefaultBakedWeaponTraceSource.Instance
                .TryGetPackageSocketCatalog(definition.GeneratedPackageRoot, out CharacterSocketBakeCatalog catalog));
            CharacterAuthorityPoseRigDefinition poseDefinition =
                CharacterAuthorityPoseRigDefinition.FromRuntimeBinding(
                    rig,
                    CharacterTestAuthorityPoseDefinitionSource
                        .CreateDefaultDefinition()
                        .SecondaryBindings);
            return new DragonAssets(definition, package, rig, poseDefinition, catalog);
        }

        private static DragonFixture CreateFixture(DragonAssets assets, CombatPhysicsWorld physics)
        {
            var world = new CharacterTestCharacterRuntimeWorld();
            CharacterTestCharacterEntity entity = world.Spawn(
                new CharacterTestCharacterSpawnRequest(
                    characterId: assets.Character.CharacterId,
                    teamId: 1,
                    x: 0f,
                    y: 0f,
                    z: 0f,
                    controlOwner: CharacterTestCharacterControlOwner.None),
                assets.Character);
            entity.SetMotionState(
                verticalVelocity: 0f,
                lastMoveInput: Vector2.zero,
                isGrounded: true,
                movementMode: CharacterMovementMode.Idle);
            entity.SetAction(CharacterTestCharacterActionComponent.FromDeterministic(
                ActionId,
                state: MxFramework.Game.CharacterTest.CharacterTestActionStateId.Running,
                phase: MxFramework.Game.CharacterTest.CharacterTestActionPhaseId.Active,
                localFrame: ClipLocalFrame,
                lastRejectReason: default,
                lastAnimationActionKey: AnimationActionKey,
                animationActionKeyThisFrame: AnimationActionKey,
                animationTransitionFrames: 0,
                animationHoldFrames: 0,
                jumpImpulseYMillimetersPerSecond: 0));
            entity.SetAnimation(new CharacterTestCharacterAnimationComponent(
                Vector2.zero,
                locomotionSpeed01: 0f,
                grounded: true,
                airborne: false,
                actionActive: true,
                actionKey: AnimationActionKey));
            var terrain = new CharacterTestAuthorityPoseTerrainSystem(actionRuntime: null);
            Assert.IsTrue(terrain.ConfigureForTests(
                entity,
                assets.PoseDefinition,
                assets.Catalog,
                baseSourceRevision: 1,
                baseSourceContentHash: BaseSourceHash));
            return new DragonFixture(world, entity, terrain, physics);
        }

        private static void Tick(DragonFixture fixture, params long[] frames)
        {
            for (int i = 0; i < frames.Length; i++)
            {
                Assert.IsTrue(fixture.Terrain.TickForTests(
                    fixture.Entity,
                    fixture.Physics,
                    new RuntimeFrame(frames[i]),
                    ClipKey,
                    localFrame: ClipLocalFrame,
                    loop: true));
            }
        }

        private static CombatPhysicsWorld CreateFlatWorld()
        {
            var world = new CombatPhysicsWorld();
            AddAabb(world, 81001, 82001, -16, 16, -16, 16, Fix64.Zero);
            return world;
        }

        private static CombatPhysicsWorld CreateStepWorld()
        {
            var world = new CombatPhysicsWorld();
            AddAabb(world, 81010, 82010, -4, 0, -4, 4, Fix64.Zero);
            AddAabb(world, 81011, 82011, 0, 4, -4, 4, Fix64.FromRatio(16, 100));
            return world;
        }

        private static CombatPhysicsWorld CreateUnevenWorld()
        {
            var world = new CombatPhysicsWorld();
            AddAabb(world, 81020, 82020, -4, 0, -4, 0, Fix64.Zero);
            AddAabb(world, 81021, 82021, 0, 4, -4, 0, Fix64.FromRatio(8, 100));
            AddAabb(world, 81022, 82022, -4, 0, 0, 4, Fix64.FromRatio(14, 100));
            AddAabb(world, 81023, 82023, 0, 4, 0, 4, Fix64.FromRatio(20, 100));
            return world;
        }

        private static CombatPhysicsWorld CreateProbeAlignedWorld(
            DragonAssets assets,
            int slopeSinMilli,
            int slopeCosineMilli,
            bool farReach)
        {
            var world = new CombatPhysicsWorld();
            Fix64 cos = Fix64.FromRatio(slopeCosineMilli, 10000);
            Fix64 sin = Fix64.FromRatio(slopeSinMilli, 10000);
            for (int i = 0; i < assets.PoseDefinition.GroundProbes.Count; i++)
            {
                CharacterAuthorityGroundProbeDefinition probe = assets.PoseDefinition.GroundProbes[i];
                ReadBaseTip(assets, probe.TipChannelId, out FixVector3 tip, out CharacterTestResolvedAuthorityPoseBasis basis);
                FixVector3 axis = CharacterTestAuthorityPoseTerrainMath.TransformByBasis(
                    basis,
                    probe.TipLocalAxis);
                Assert.IsTrue(axis.TryNormalize(out axis));
                Fix64 distance = probe.MaxDownDistance
                    * (farReach ? Fix64.FromRatio(17, 20) : Fix64.FromRatio(2, 5));
                FixVector3 point = tip + (axis * distance);
                var bodyId = new CombatBodyId(81100 + i);
                world.UpsertBody(new CombatPhysicsBody(
                    new CombatEntityId(bodyId.Value),
                    bodyId,
                    FixVector3.Zero));
                FixVector3 axisX = farReach
                    ? new FixVector3(Fix64.One, Fix64.Zero, Fix64.Zero)
                    : new FixVector3(cos, sin, Fix64.Zero);
                FixVector3 axisY = farReach
                    ? new FixVector3(Fix64.Zero, Fix64.One, Fix64.Zero)
                    : new FixVector3(-sin, cos, Fix64.Zero);
                world.UpsertObbCollider(new CombatPhysicsObbCollider(
                    bodyId,
                    new CombatColliderId(82100 + i),
                    GroundLayer,
                    point - (axisY * Fix64.FromRatio(3, 100)),
                    new FixVector3(
                        Fix64.FromRatio(45, 100),
                        Fix64.FromRatio(3, 100),
                        Fix64.FromRatio(45, 100)),
                    axisX,
                    axisY,
                    new FixVector3(Fix64.Zero, Fix64.Zero, Fix64.One),
                    CombatSurfaceMaterial.Rough));
            }
            return world;
        }

        private static void AddAabb(
            CombatPhysicsWorld world,
            int bodyValue,
            int colliderValue,
            int minX,
            int maxX,
            int minZ,
            int maxZ,
            Fix64 top)
        {
            var bodyId = new CombatBodyId(bodyValue);
            world.UpsertBody(new CombatPhysicsBody(
                new CombatEntityId(bodyValue),
                bodyId,
                FixVector3.Zero));
            world.UpsertAabbCollider(new CombatPhysicsAabbCollider(
                bodyId,
                new CombatColliderId(colliderValue),
                GroundLayer,
                new FixVector3(Fix64.FromInt(minX), -Fix64.One, Fix64.FromInt(minZ)),
                new FixVector3(Fix64.FromInt(maxX), top, Fix64.FromInt(maxZ)),
                CombatSurfaceMaterial.Rough));
        }

        private static void ReadBaseTip(
            DragonAssets assets,
            int channelId,
            out FixVector3 position,
            out CharacterTestResolvedAuthorityPoseBasis basis)
        {
            Assert.IsTrue(assets.Catalog.TryGetTrack(ClipKey, channelId, out CharacterSocketBakeTrack track));
            Assert.IsTrue(track.TryGetFrame(ClipLocalFrame, out MxAnimationBakedSocketFrame frame));
            Assert.IsTrue(track.TryGetBasis(ClipLocalFrame, out CharacterSocketBakeBasisFrame bakedBasis));
            position = ToFixVector3(frame.Position, track.QuantizationScale);
            basis = new CharacterTestResolvedAuthorityPoseBasis(
                Normalize(ToFixVector3(bakedBasis.Right, track.BasisQuantizationScale)),
                Normalize(ToFixVector3(bakedBasis.Up, track.BasisQuantizationScale)),
                Normalize(ToFixVector3(bakedBasis.Forward, track.BasisQuantizationScale)));
        }

        private static void AddSocketMarkers(
            DragonFixture fixture,
            DragonAssets assets,
            List<GameObject> visuals,
            Material material)
        {
            int[] socketIds = { 37, 38 };
            for (int i = 0; i < socketIds.Length; i++)
            {
                int socketId = socketIds[i];
                Assert.IsTrue(assets.Catalog.TryGetTrack(ClipKey, socketId, out CharacterSocketBakeTrack track));
                Assert.IsTrue(track.TryGetFrame(ClipLocalFrame, out MxAnimationBakedSocketFrame frame));
                FixVector3 basePoint = ToFixVector3(frame.Position, track.QuantizationScale);
                Assert.IsTrue(CharacterTestAuthorityPoseConsumerMath.TryResolveAttachmentDelta(
                    fixture.Entity,
                    assets.Rig,
                    assets.Catalog,
                    CharacterAuthorityPoseRigAttachmentKind.Socket,
                    socketId,
                    ClipKey,
                    localFrame: ClipLocalFrame,
                    loop: true,
                    new RuntimeFrame(122),
                    out CharacterTestAuthorityPoseAttachmentDelta delta));
                FixVector3 resolvedModel = delta.TransformPoint(basePoint);
                FixVector3 resolvedWorld = CharacterTestAuthorityPoseTerrainMath.ModelPointToWorld(
                    resolvedModel,
                    fixture.Entity.Transform);
                AddMarker(visuals, "resolved-socket-" + socketId, ToVector3(resolvedWorld), 0.1f, material);
                TestContext.Out.WriteLine(
                    "lcc_m4_t5_same_frame consumer=socket id=" + socketId
                    + " frame=122 point_raw=" + FormatRaw(resolvedWorld));
            }
        }

        private static void AddUnevenVisualGround(List<GameObject> visuals, Material material)
        {
            AddVisualBox(visuals, "ground-left-hind", new Vector3(-2f, -0.08f, -2f), new Vector3(4f, 0.16f, 4f), material);
            AddVisualBox(visuals, "ground-right-hind", new Vector3(2f, 0f, -2f), new Vector3(4f, 0.16f, 4f), material);
            AddVisualBox(visuals, "ground-left-fore", new Vector3(-2f, 0.06f, 2f), new Vector3(4f, 0.16f, 4f), material);
            AddVisualBox(visuals, "ground-right-fore", new Vector3(2f, 0.12f, 2f), new Vector3(4f, 0.16f, 4f), material);
        }

        private static void AddVisualBox(
            List<GameObject> visuals,
            string name,
            Vector3 position,
            Vector3 scale,
            Material material)
        {
            GameObject box = GameObject.CreatePrimitive(PrimitiveType.Cube);
            box.name = name;
            box.transform.position = position;
            box.transform.localScale = scale;
            RemoveUnityCollider(box);
            box.GetComponent<Renderer>().sharedMaterial = material;
            visuals.Add(box);
        }

        private static void AddMarker(
            List<GameObject> visuals,
            string name,
            Vector3 position,
            float scale,
            Material material)
        {
            GameObject marker = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            marker.name = name;
            marker.transform.position = position;
            marker.transform.localScale = Vector3.one * scale;
            RemoveUnityCollider(marker);
            marker.GetComponent<Renderer>().sharedMaterial = material;
            visuals.Add(marker);
        }

        private static void RemoveUnityCollider(GameObject instance)
        {
            Collider collider = instance.GetComponent<Collider>();
            if (collider != null)
                UnityEngine.Object.DestroyImmediate(collider);
        }

        private static Material CreateMaterial(Color color, List<Material> materials)
        {
            Shader shader = Shader.Find("WGame/PropStylizedLit");
            Assert.NotNull(shader);
            var material = new Material(shader);
            material.SetColor("_Color", color);
            material.SetFloat("_WgAlbedoStrength", 1f);
            material.SetFloat("_WgBronzeTrimAmount", 0f);
            material.SetFloat("_WgInteractableGlowAmount", 0f);
            materials.Add(material);
            return material;
        }

        private static void AssertAllFailures(
            DragonFixture fixture,
            CharacterTestAuthorityPoseFailure expected)
        {
            Assert.AreEqual(4, fixture.Entity.TerrainIkState.LimbCount);
            for (int i = 0; i < fixture.Entity.TerrainIkState.LimbCount; i++)
                Assert.AreEqual(expected, fixture.Entity.TerrainIkState.GetLimb(i).CurrentFailure, "limb=" + i);
            Assert.AreEqual(12, fixture.Entity.ResolvedAuthorityPoseFrame.Count);
        }

        private static void AssertContainsFailure(
            DragonFixture fixture,
            CharacterTestAuthorityPoseFailure expected)
        {
            int count = 0;
            for (int i = 0; i < fixture.Entity.TerrainIkState.LimbCount; i++)
            {
                if (fixture.Entity.TerrainIkState.GetLimb(i).CurrentFailure == expected)
                    count++;
            }
            Assert.Greater(count, 0, expected.ToString());
            TestContext.Out.WriteLine("lcc_m4_t5_typed_failure value=" + expected + " limbs=" + count);
        }

        private static void AssertDistinctTargetHeights(DragonFixture fixture, int minimumDistinctCount)
        {
            var heights = new HashSet<long>();
            for (int i = 0; i < fixture.Entity.TerrainIkState.LimbCount; i++)
                heights.Add(fixture.Entity.TerrainIkState.GetLimb(i).LastValidTargetWorld.Y.RawValue);
            Assert.GreaterOrEqual(heights.Count, minimumDistinctCount);
        }

        private static CharacterTestTerrainIkLimbState[] CopyLimbStates(DragonFixture fixture)
        {
            var states = new CharacterTestTerrainIkLimbState[fixture.Entity.TerrainIkState.LimbCount];
            for (int i = 0; i < states.Length; i++)
                states[i] = fixture.Entity.TerrainIkState.GetLimb(i);
            return states;
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

        private static void SeverPart(
            CharacterTestCharacterEntity entity,
            int partStateId,
            long breakFrame,
            long severFrame)
        {
            CharacterTestBodyPartStateComponent state = entity.BodyPartState;
            Assert.IsTrue(state.TryResolveDefinitionIndexByPartStateId(partStateId, out int index));
            Assert.IsTrue(state.TryGetDefinition(index, out CharacterTestBodyPartStateDefinition definition));
            Assert.IsTrue(state.TryBeginFrame(breakFrame, out _));
            Assert.AreEqual(
                CharacterTestBodyPartStateStageResult.Staged,
                state.StageAcceptedDamage(new CharacterTestBodyPartAcceptedDamageFact(
                    breakFrame,
                    sourceEntityId: 77,
                    targetEntityId: entity.Id.Value,
                    actionInstanceId: 1,
                    policyEventId: 1,
                    traceId: 1,
                    localFrame: 1,
                    definitionIndex: index,
                    finalDamage: int.MaxValue)));
            Assert.IsTrue(state.CommitFrame().Succeeded);
            Assert.IsTrue(state.TryBeginFrame(severFrame, out _));
            Assert.AreEqual(
                CharacterTestBodyPartStateStageResult.Staged,
                state.StageSever(new CharacterTestBodyPartSeverFact(
                    severFrame,
                    sourceEntityId: 77,
                    targetEntityId: entity.Id.Value,
                    definitionIndex: index,
                    severTag: definition.SeverRequiredTag,
                    correlationId: severFrame)));
            Assert.IsTrue(state.CommitFrame().Succeeded);
        }

        private static FixVector3 ToFixVector3(MxAnimationBakedVector3 value, int scale)
        {
            Assert.Greater(scale, 0);
            return new FixVector3(
                Fix64.FromRatio(value.X, scale),
                Fix64.FromRatio(value.Y, scale),
                Fix64.FromRatio(value.Z, scale));
        }

        private static FixVector3 Normalize(FixVector3 value)
        {
            Assert.IsTrue(value.TryNormalize(out FixVector3 normalized));
            return normalized;
        }

        private static FixVector3 QuantizeMillimeters(FixVector3 value)
        {
            return new FixVector3(
                QuantizeMillimeters(value.X),
                QuantizeMillimeters(value.Y),
                QuantizeMillimeters(value.Z));
        }

        private static Fix64 QuantizeMillimeters(Fix64 value)
        {
            long millimeters = value.RawValue * 1000L / Fix64.Scale;
            return Fix64.FromRaw(millimeters * Fix64.Scale / 1000L);
        }

        private static Vector3 ToVector3(FixVector3 value)
        {
            return new Vector3(
                (float)((double)value.X.RawValue / Fix64.Scale),
                (float)((double)value.Y.RawValue / Fix64.Scale),
                (float)((double)value.Z.RawValue / Fix64.Scale));
        }

        private static string FormatRaw(FixVector3 value)
        {
            return "(" + value.X.RawValue + "," + value.Y.RawValue + "," + value.Z.RawValue + ")";
        }

        private static void AssertVectorWithinRawTolerance(
            FixVector3 expected,
            FixVector3 actual,
            long tolerance)
        {
            Assert.That(actual.X.RawValue, Is.EqualTo(expected.X.RawValue).Within(tolerance), "x");
            Assert.That(actual.Y.RawValue, Is.EqualTo(expected.Y.RawValue).Within(tolerance), "y");
            Assert.That(actual.Z.RawValue, Is.EqualTo(expected.Z.RawValue).Within(tolerance), "z");
        }

        private static string GetScreenshotPath()
        {
            string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            return Path.Combine(
                projectRoot,
                "Temp",
                "CodexEvidence",
                "LCC-M4-T5",
                "LCC-M4-T5-Dragon14-Terrain-GameView.png");
        }

        private sealed class DragonAssets
        {
            internal DragonAssets(
                CharacterTestCharacterDefinition character,
                CharacterImportedPackage package,
                CharacterAuthorityPoseRigRuntimeBinding rig,
                CharacterAuthorityPoseRigDefinition poseDefinition,
                CharacterSocketBakeCatalog catalog)
            {
                Character = character;
                Package = package;
                Rig = rig;
                PoseDefinition = poseDefinition;
                Catalog = catalog;
            }

            internal CharacterTestCharacterDefinition Character { get; }
            internal CharacterImportedPackage Package { get; }
            internal CharacterAuthorityPoseRigRuntimeBinding Rig { get; }
            internal CharacterAuthorityPoseRigDefinition PoseDefinition { get; }
            internal CharacterSocketBakeCatalog Catalog { get; }
        }

        private sealed class DragonFixture
        {
            internal DragonFixture(
                CharacterTestCharacterRuntimeWorld world,
                CharacterTestCharacterEntity entity,
                CharacterTestAuthorityPoseTerrainSystem terrain,
                CombatPhysicsWorld physics)
            {
                World = world;
                Entity = entity;
                Terrain = terrain;
                Physics = physics;
            }

            internal CharacterTestCharacterRuntimeWorld World { get; }
            internal CharacterTestCharacterEntity Entity { get; }
            internal CharacterTestAuthorityPoseTerrainSystem Terrain { get; }
            internal CombatPhysicsWorld Physics { get; }
        }
    }
}
