using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using MxFramework.CharacterRuntimeSpawn;
using MxFramework.CharacterRuntimeSpawn.Unity;
using MxFramework.Combat.Core;
using MxFramework.Combat.Physics;
using MxFramework.Core.Math;
using MxFramework.Game.CharacterTest;
using MxFramework.Runtime;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace MxFramework.Tests.PlayMode.CharacterTest
{
    [Parallelizable(ParallelScope.None)]
    public sealed class CharacterTestDragon14AnimatedAppendageSoftContactPlayModeTests
    {
        private const double TickSeconds = 1d / 60d;
        private const int MaximumExpectedContactSpeed = 6100;

        [UnityTest]
        public IEnumerator FormalDragon14_HeadTailMidAndTailTipProduceBoundedSoftContactWithoutDamage()
        {
            Scene scene = EditorSceneManager.LoadSceneInPlayMode(
                "Assets/Scenes/CharacterTestScene.unity",
                new LoadSceneParameters(LoadSceneMode.Single));
            yield return null;
            Assert.That(
                scene.path.Replace('\\', '/'),
                Does.EndWith("/CharacterTestScene.unity"));
            GameManager sceneManager = UnityEngine.Object.FindAnyObjectByType<GameManager>();
            Assert.NotNull(sceneManager, "Formal CharacterTestScene GameManager is missing.");
            CharacterTestMainMenuCommandResult closeMenu =
                sceneManager.RuntimeContext.MainMenuCoordinator.CloseMainMenu();
            Assert.IsTrue(closeMenu.Success, closeMenu.Message);
            yield return null;

            CharacterTestCharacterDefinition dragonDefinition = FindDefinition("000003");
            CharacterTestCharacterDefinition playerDefinition = FindDefinition("000001");
            CharacterImportedPackage package = CharacterImportedPackageJson.LoadFromDirectory(
                Path.GetFullPath(dragonDefinition.GeneratedPackageRoot));
            Assert.NotNull(package?.Geometry);
            Assert.AreEqual("mx.characterGeometryBinding.v8", package.Geometry.Format);
            Assert.AreEqual(3, package.Geometry.AnimatedHurtboxSources.Length);

            var physics = CreatePhysics("lcc-m5-t3.formal-playable");
            CharacterTestTraceRecorder traceRecorder = CharacterTestTraceRecorder.CreateInMemory(4096);
            var module = new CharacterTestCharacterRuntimeModule(
                null,
                NullRuntimeLogger.Instance,
                physicsRuntime: physics,
                traceRecorder: traceRecorder,
                createViews: false);
            using (var host = new RuntimeHost())
            {
                host.RegisterModule(module);
                host.Initialize();
                host.Start();
                EnqueueSpawn(module, dragonDefinition, team: 1, x: 0f, z: 0f);
                EnqueueSpawn(module, playerDefinition, team: 2, x: 12f, z: 12f);
                int frame = 0;
                Tick(host, ref frame);
                Tick(host, ref frame);

                Assert.AreEqual(2, module.Entities.Count);
                CharacterTestCharacterEntity dragon = module.Entities[0];
                CharacterTestCharacterEntity player = module.Entities[1];
                Assert.AreEqual(dragonDefinition.CharacterId, dragon.Identity.CharacterId);
                Assert.AreEqual(playerDefinition.CharacterId, player.Identity.CharacterId);
                Assert.IsTrue(module.TryGetDamageGeometrySnapshot(
                    dragon.Id.Value,
                    out CharacterTestDamageGeometrySnapshot snapshot));

                int[] colliderIds =
                {
                    CharacterTestAuthoredContentIds.D400_Id150,
                    CharacterTestAuthoredContentIds.D400_Id148,
                    CharacterTestAuthoredContentIds.D400_Id149
                };
                int baselineHp = module.World.GetCurrentAttribute(
                    player,
                    CharacterTestCharacterAttributeIds.Hp);
                var markerObjects = new List<GameObject>();
                CharacterTestRuntimeCharacterView dragonView = null;
                CharacterTestRuntimeCharacterView playerView = null;
                Camera evidenceCamera = null;
                Light evidenceLight = null;
                var disabledCameras = new List<Camera>();
                try
                {
                    dragonView = CreateFormalView(dragon, "000003", "LCC-M5-T3 Formal Dragon14");
                    playerView = CreateFormalView(player, "000001", "LCC-M5-T3 Formal Player");
                    ConfigureEvidenceCamera(disabledCameras, out evidenceCamera, out evidenceLight);
                    int combatHitCountBefore = CountCombatHits(traceRecorder);
                    for (int i = 0; i < colliderIds.Length; i++)
                    {
                        int colliderId = colliderIds[i];
                        Assert.IsTrue(snapshot.TryGetCollider(
                            colliderId,
                            out CharacterTestDamageGeometryColliderMetadata metadata),
                            colliderId.ToString());
                        int x = ToMillimeters((metadata.WorldMin.X + metadata.WorldMax.X) * Fix64.Half) + 100;
                        int y = Math.Max(
                            0,
                            ToMillimeters((metadata.WorldMin.Y + metadata.WorldMax.Y) * Fix64.Half)
                                - player.Physics.CapsuleCenterYMillimeters);
                        int z = ToMillimeters((metadata.WorldMin.Z + metadata.WorldMax.Z) * Fix64.Half);
                        MoveIdle(player, x, y, z);
                        Tick(host, ref frame);

                        int speed = Magnitude(
                            player.Physics.ContactVelocityXMillimetersPerSecond,
                            player.Physics.ContactVelocityYMillimetersPerSecond);
                        string contactEvidence = colliderId
                            + " targetMm=(" + x + "," + y + "," + z + ")"
                            + " count=" + player.Physics.ContactCount
                            + " speed=" + speed
                            + " shape=" + metadata.LocalShape.Kind
                            + " boundsRaw=" + metadata.WorldMin + ".." + metadata.WorldMax;
                        Assert.Greater(player.Physics.ContactCount, 0, contactEvidence);
                        Assert.Greater(speed, 0, contactEvidence);
                        Assert.LessOrEqual(speed, MaximumExpectedContactSpeed, colliderId.ToString());
                        Assert.AreEqual(
                            baselineHp,
                            module.World.GetCurrentAttribute(
                                player,
                                CharacterTestCharacterAttributeIds.Hp),
                            colliderId + " ordinary contact must not cause damage");

                        CharacterTestBodyPresenceContactDiagnostics presence =
                            module.CaptureBodyPresenceContactDiagnostics();
                        Assert.AreEqual(
                            0,
                            presence.ConstraintCount,
                            colliderId + " evidence must come from M5 rather than core Presence");

                        int beforeX = player.Transform.XMillimeters;
                        int beforeY = player.Transform.YMillimeters;
                        int beforeZ = player.Transform.ZMillimeters;
                        int beforeYaw = player.Transform.YawMilliDegrees;
                        Tick(host, ref frame);
                        int displacement = Magnitude3(
                            player.Transform.XMillimeters - beforeX,
                            player.Transform.YMillimeters - beforeY,
                            player.Transform.ZMillimeters - beforeZ);
                        Assert.Greater(displacement, 0, colliderId + " next tick must consume M5 velocity");
                        Assert.LessOrEqual(displacement, 120, colliderId + " next-tick slide must stay bounded");
                        Assert.AreEqual(
                            beforeYaw,
                            player.Transform.YawMilliDegrees,
                            colliderId + " external contact velocity must not change facing");
                        Tick(host, ref frame);
                        Assert.AreEqual(
                            beforeYaw,
                            player.Transform.YawMilliDegrees,
                            colliderId + " persisted external velocity must not become facing on a later tick");
                        Assert.AreEqual(
                            baselineHp,
                            module.World.GetCurrentAttribute(player, CharacterTestCharacterAttributeIds.Hp),
                            colliderId + " multi-tick contact must not cause damage");
                        TestContext.Out.WriteLine(
                            "lcc_m5_t3 contact=" + colliderId
                            + " frame=" + frame
                            + " speed_mmps=" + speed
                            + " count=" + player.Physics.ContactCount
                            + " next_tick_displacement_mm=" + displacement);

                        AddMarker(markerObjects, colliderId.ToString(), metadata, i);
                        if (i + 1 < colliderIds.Length)
                        {
                            MoveIdle(player, 12000, 0, 12000);
                            Tick(host, ref frame);
                            Assert.AreEqual(0, player.Physics.ContactCount, colliderId + " ended");
                        }
                    }

                    Assert.AreEqual(combatHitCountBefore, CountCombatHits(traceRecorder),
                        "Ordinary appendage contact must not emit CombatHit traces.");
                    SyncView(dragonView, dragon);
                    SyncView(playerView, player);
                    Assert.That(dragonView.Instance.activeInHierarchy, Is.True);
                    Assert.That(playerView.Instance.activeInHierarchy, Is.True);
                    yield return null;
                    yield return CaptureGameView();
                }
                finally
                {
                    if (dragonView != null)
                        dragonView.Release();
                    if (playerView != null)
                        playerView.Release();
                    for (int i = 0; i < markerObjects.Count; i++)
                        UnityEngine.Object.DestroyImmediate(markerObjects[i]);
                    if (evidenceCamera != null)
                        UnityEngine.Object.DestroyImmediate(evidenceCamera.gameObject);
                    if (evidenceLight != null)
                        UnityEngine.Object.DestroyImmediate(evidenceLight.gameObject);
                    for (int i = 0; i < disabledCameras.Count; i++)
                    {
                        if (disabledCameras[i] != null)
                            disabledCameras[i].enabled = true;
                    }
                }
            }
        }

        [UnityTest]
        public IEnumerator FormalDragon14_MovingTurningHighSpeedWallCornerAndSeveredStayBounded()
        {
            yield return null;
            CharacterTestCharacterDefinition dragonDefinition = FindDefinition("000003");
            CharacterTestCharacterDefinition playerDefinition = FindDefinition("000001");
            CharacterImportedPackage package = CharacterImportedPackageJson.LoadFromDirectory(
                Path.GetFullPath(dragonDefinition.GeneratedPackageRoot));
            Assert.AreEqual("mx.characterGeometryBinding.v8", package.Geometry.Format);
            Assert.AreEqual(3, package.Geometry.AnimatedHurtboxSources.Length);

            CharacterTestCharacterPhysicsRuntime physics = CreatePhysics("lcc-m5-t3.motion-safety");
            var module = new CharacterTestCharacterRuntimeModule(
                null,
                NullRuntimeLogger.Instance,
                physicsRuntime: physics,
                traceRecorder: CharacterTestTraceRecorder.CreateInMemory(2048),
                createViews: false);
            using (var host = new RuntimeHost())
            {
                host.RegisterModule(module);
                host.Initialize();
                host.Start();
                EnqueueSpawn(module, dragonDefinition, 1, 0f, 0f);
                EnqueueSpawn(module, playerDefinition, 2, 12f, 12f);
                int frame = 0;
                Tick(host, ref frame);
                Tick(host, ref frame);
                CharacterTestCharacterEntity dragon = module.Entities[0];
                CharacterTestCharacterEntity player = module.Entities[1];

                Assert.IsTrue(module.TryGetDamageGeometrySnapshot(
                    dragon.Id.Value,
                    out CharacterTestDamageGeometrySnapshot geometry));
                Assert.IsTrue(geometry.TryGetCollider(
                    CharacterTestAuthoredContentIds.D400_Id150,
                    out CharacterTestDamageGeometryColliderMetadata head));
                PlaceAtCollider(player, head, deltaXMillimeters: 150);
                dragon.SetTransform(CharacterTestCharacterTransformComponent.FromDeterministic(
                    50,
                    0,
                    0,
                    0));
                player.SetTransform(CharacterTestCharacterTransformComponent.FromDeterministic(
                    player.Transform.XMillimeters + 50,
                    player.Transform.YMillimeters,
                    player.Transform.ZMillimeters,
                    0));
                Tick(host, ref frame);
                AssertBoundedM5Contact(module, player, "moving head");
                AssertNextTickMovesBoundedly(host, ref frame, player, "moving head");

                MoveIdle(player, 12000, 0, 12000);
                Tick(host, ref frame);
                Assert.IsTrue(module.TryGetDamageGeometrySnapshot(dragon.Id.Value, out geometry));
                Assert.IsTrue(geometry.TryGetCollider(CharacterTestAuthoredContentIds.D400_Id150, out head));
                PlaceAtCollider(player, head, deltaXMillimeters: 100);
                dragon.SetTransform(CharacterTestCharacterTransformComponent.FromDeterministic(
                    dragon.Transform.XMillimeters,
                    dragon.Transform.YMillimeters,
                    dragon.Transform.ZMillimeters,
                    10000));
                Tick(host, ref frame);
                AssertBoundedM5Contact(module, player, "turning head");
                AssertNextTickMovesBoundedly(host, ref frame, player, "turning head");

                MoveIdle(player, 12000, 0, 12000);
                Tick(host, ref frame);
                Assert.IsTrue(module.TryGetDamageGeometrySnapshot(dragon.Id.Value, out geometry));
                Assert.IsTrue(geometry.TryGetCollider(
                    CharacterTestAuthoredContentIds.D400_Id149,
                    out CharacterTestDamageGeometryColliderMetadata tailTip));
                PlaceAtCollider(player, tailTip, deltaXMillimeters: 100);
                dragon.SetTransform(CharacterTestCharacterTransformComponent.FromDeterministic(
                    dragon.Transform.XMillimeters + 1000,
                    dragon.Transform.YMillimeters,
                    dragon.Transform.ZMillimeters,
                    dragon.Transform.YawMilliDegrees));
                player.SetTransform(CharacterTestCharacterTransformComponent.FromDeterministic(
                    player.Transform.XMillimeters + 1000,
                    player.Transform.YMillimeters,
                    player.Transform.ZMillimeters));
                Tick(host, ref frame);
                AssertBoundedM5Contact(module, player, "high-speed tail-tip clamp");

                int wallStartX = player.Transform.XMillimeters;
                int wallStartZ = player.Transform.ZMillimeters;
                int vx = player.Physics.ContactVelocityXMillimetersPerSecond;
                int vz = player.Physics.ContactVelocityYMillimetersPerSecond;
                int radius = player.Physics.CapsuleRadiusMillimeters;
                int xSign = vx < 0 ? -1 : 1;
                int zSign = vz < 0 ? -1 : 1;
                int wallX = wallStartX + xSign * (radius + 20);
                int wallZ = wallStartZ + zSign * (radius + 20);
                AddAxisWall(physics.World, bodyId: 201, colliderId: 201, wallX, wallStartZ, alongX: true, xSign);
                AddAxisWall(physics.World, bodyId: 202, colliderId: 202, wallStartX, wallZ, alongX: false, zSign);
                Tick(host, ref frame);
                if (xSign > 0)
                    Assert.LessOrEqual(player.Transform.XMillimeters, wallX - radius + 1, "x wall sweep");
                else
                    Assert.GreaterOrEqual(player.Transform.XMillimeters, wallX + radius - 1, "x wall sweep");
                if (zSign > 0)
                    Assert.LessOrEqual(player.Transform.ZMillimeters, wallZ - radius + 1, "z corner sweep");
                else
                    Assert.GreaterOrEqual(player.Transform.ZMillimeters, wallZ + radius - 1, "z corner sweep");
                TestContext.Out.WriteLine(
                    "lcc_m5_t3 wall_corner start=(" + wallStartX + "," + wallStartZ + ")"
                    + " resolved=(" + player.Transform.XMillimeters + "," + player.Transform.ZMillimeters + ")"
                    + " wall=(" + wallX + "," + wallZ + ") radius=" + radius);
                physics.World.RemoveBody(new CombatBodyId(201));
                physics.World.RemoveBody(new CombatBodyId(202));

                dragon.SetTransform(CharacterTestCharacterTransformComponent.FromDeterministic(0, 0, 0));
                MoveIdle(player, 12000, 0, 12000);
                Tick(host, ref frame);
                Assert.IsTrue(module.TryGetDamageGeometrySnapshot(dragon.Id.Value, out geometry));
                Assert.IsTrue(geometry.TryGetCollider(CharacterTestAuthoredContentIds.D400_Id149, out tailTip));
                int oldTailX = ToMillimeters((tailTip.WorldMin.X + tailTip.WorldMax.X) * Fix64.Half) + 100;
                int oldTailY = Math.Max(
                    0,
                    ToMillimeters((tailTip.WorldMin.Y + tailTip.WorldMax.Y) * Fix64.Half)
                        - player.Physics.CapsuleCenterYMillimeters);
                int oldTailZ = ToMillimeters((tailTip.WorldMin.Z + tailTip.WorldMax.Z) * Fix64.Half);
                SeverPart(dragon, CharacterTestAuthoredContentIds.D400_Id43, frame, frame + 1);
                Tick(host, ref frame);
                Tick(host, ref frame);
                MoveIdle(player, oldTailX, oldTailY, oldTailZ);
                Tick(host, ref frame);
                Assert.AreEqual(0, player.Physics.ContactCount, "Severed tail must not retain M5 contact");

                Assert.IsTrue(module.TryGetDamageGeometrySnapshot(dragon.Id.Value, out geometry));
                Assert.IsFalse(geometry.TryGetCollider(CharacterTestAuthoredContentIds.D400_Id149, out _));
                Assert.IsTrue(geometry.TryGetCollider(CharacterTestAuthoredContentIds.D400_Id150, out head));
                PlaceAtCollider(player, head, deltaXMillimeters: 100);
                Tick(host, ref frame);
                AssertBoundedM5Contact(module, player, "head after Severed tail");
                TestContext.Out.WriteLine(
                    "lcc_m5_t3 severed tail_contact=0 head_contact="
                    + player.Physics.ContactCount + " frame=" + frame);
            }
        }

        private static IEnumerator CaptureGameView()
        {
            string path = GetGameViewPath();
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            if (File.Exists(path))
                File.Delete(path);
            ScreenCapture.CaptureScreenshot(path);
            yield return new WaitForEndOfFrame();
            yield return null;
            Assert.IsTrue(File.Exists(path), "Formal Game View screenshot was not written: " + path);
            TestContext.Out.WriteLine("lcc_m5_t3_game_view=" + path);
        }

        private static void AddMarker(
            List<GameObject> objects,
            string colliderId,
            CharacterTestDamageGeometryColliderMetadata metadata,
            int ordinal)
        {
            Vector3 center = new Vector3(
                ToMeters((metadata.WorldMin.X + metadata.WorldMax.X) * Fix64.Half),
                ToMeters((metadata.WorldMin.Y + metadata.WorldMax.Y) * Fix64.Half),
                ToMeters((metadata.WorldMin.Z + metadata.WorldMax.Z) * Fix64.Half));
            GameObject marker = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            marker.name = "LCC-M5-T3 tests-only marker " + colliderId;
            marker.transform.position = center;
            marker.transform.localScale = Vector3.one * (0.22f + ordinal * 0.03f);
            Collider collider = marker.GetComponent<Collider>();
            if (collider != null)
                UnityEngine.Object.DestroyImmediate(collider);
            objects.Add(marker);
        }

        private static CharacterTestRuntimeCharacterView CreateFormalView(
            CharacterTestCharacterEntity entity,
            string packageId,
            string name)
        {
            string resourcePath = "MxFrameworkGenerated/CharacterPackages/"
                + packageId + "/prefabs/" + packageId + "_character_preview";
            GameObject prefab = UnityEngine.Resources.Load<GameObject>(resourcePath);
            Assert.NotNull(prefab, resourcePath);
            GameObject instance = UnityEngine.Object.Instantiate(prefab);
            instance.name = name;
            Animator animator = instance.GetComponentInChildren<Animator>(true);
            if (animator != null)
                animator.enabled = false;
            var view = new CharacterTestRuntimeCharacterView(
                entity.Id,
                instance,
                null,
                null,
                null);
            SyncView(view, entity);
            return view;
        }

        private static void SyncView(
            CharacterTestRuntimeCharacterView view,
            CharacterTestCharacterEntity entity)
        {
            view.SyncTransform(
                new Vector3(entity.Transform.X, entity.Transform.Y, entity.Transform.Z),
                Quaternion.Euler(0f, entity.Transform.YawDegrees, 0f));
        }

        private static void ConfigureEvidenceCamera(
            List<Camera> disabled,
            out Camera evidenceCamera,
            out Light evidenceLight)
        {
            Camera[] cameras = UnityEngine.Object.FindObjectsByType<Camera>(
                FindObjectsInactive.Include);
            for (int i = 0; i < cameras.Length; i++)
            {
                if (cameras[i].enabled)
                {
                    cameras[i].enabled = false;
                    disabled.Add(cameras[i]);
                }
            }

            var cameraObject = new GameObject("LCC-M5-T3 tests-only evidence camera");
            evidenceCamera = cameraObject.AddComponent<Camera>();
            evidenceCamera.clearFlags = CameraClearFlags.SolidColor;
            evidenceCamera.backgroundColor = new Color(0.08f, 0.1f, 0.13f, 1f);
            evidenceCamera.fieldOfView = 48f;
            cameraObject.transform.position = new Vector3(8f, 5.5f, -9f);
            cameraObject.transform.LookAt(new Vector3(0f, 1.5f, 0f));

            var lightObject = new GameObject("LCC-M5-T3 tests-only evidence light");
            evidenceLight = lightObject.AddComponent<Light>();
            evidenceLight.type = LightType.Directional;
            evidenceLight.intensity = 1.4f;
            lightObject.transform.rotation = Quaternion.Euler(45f, -35f, 0f);
        }

        private static int CountCombatHits(CharacterTestTraceRecorder recorder)
        {
            int count = 0;
            for (int i = 0; i < recorder.RecentEvents.Count; i++)
            {
                if (recorder.RecentEvents[i].Kind == CharacterTestTraceEventKind.CombatHit)
                    count++;
            }
            return count;
        }

        private static CharacterTestCharacterPhysicsRuntime CreatePhysics(string description)
        {
            var world = new CombatPhysicsWorld();
            var groundBody = new CombatBodyId(100);
            world.UpsertBody(new CombatPhysicsBody(new CombatEntityId(100), groundBody, FixVector3.Zero));
            world.UpsertAabbCollider(new CombatPhysicsAabbCollider(
                groundBody,
                new CombatColliderId(100),
                1,
                new FixVector3(-Fix64.FromInt(20), -Fix64.One, -Fix64.FromInt(20)),
                new FixVector3(Fix64.FromInt(20), Fix64.Zero, Fix64.FromInt(20))));
            return new CharacterTestCharacterPhysicsRuntime(
                world,
                CombatPhysicsLayerMask.FromLayer(1),
                new FixVector3(Fix64.Half, Fix64.One, Fix64.Half),
                Fix64.FromRatio(1, 100),
                Fix64.Half,
                staticColliderCount: 1,
                sourceDescription: description);
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
                CharacterTestCharacterControlOwner.None)));
        }

        private static void Tick(RuntimeHost host, ref int frame)
        {
            host.Tick(frame, TickSeconds, frame * TickSeconds);
            frame++;
        }

        private static void MoveIdle(CharacterTestCharacterEntity entity, int x, int y, int z)
        {
            entity.SetTransform(CharacterTestCharacterTransformComponent.FromDeterministic(x, y, z));
            entity.SetPhysics(entity.Physics.WithContactState(Vector2.zero, Vector2.zero, 0));
            entity.SetMotionStateDeterministic(0, 0, 0, true);
        }

        private static void PlaceAtCollider(
            CharacterTestCharacterEntity player,
            CharacterTestDamageGeometryColliderMetadata collider,
            int deltaXMillimeters)
        {
            int x = ToMillimeters((collider.WorldMin.X + collider.WorldMax.X) * Fix64.Half)
                + deltaXMillimeters;
            int y = Math.Max(
                0,
                ToMillimeters((collider.WorldMin.Y + collider.WorldMax.Y) * Fix64.Half)
                    - player.Physics.CapsuleCenterYMillimeters);
            int z = ToMillimeters((collider.WorldMin.Z + collider.WorldMax.Z) * Fix64.Half);
            MoveIdle(player, x, y, z);
        }

        private static void AssertBoundedM5Contact(
            CharacterTestCharacterRuntimeModule module,
            CharacterTestCharacterEntity player,
            string scenario)
        {
            int speed = Magnitude(
                player.Physics.ContactVelocityXMillimetersPerSecond,
                player.Physics.ContactVelocityYMillimetersPerSecond);
            Assert.Greater(player.Physics.ContactCount, 0, scenario);
            Assert.Greater(speed, 0, scenario);
            Assert.LessOrEqual(speed, MaximumExpectedContactSpeed, scenario);
            Assert.AreEqual(0, module.CaptureBodyPresenceContactDiagnostics().ConstraintCount, scenario);
            TestContext.Out.WriteLine(
                "lcc_m5_t3 scenario=" + scenario
                + " speed_mmps=" + speed
                + " count=" + player.Physics.ContactCount);
        }

        private static void AssertNextTickMovesBoundedly(
            RuntimeHost host,
            ref int frame,
            CharacterTestCharacterEntity player,
            string scenario)
        {
            int beforeX = player.Transform.XMillimeters;
            int beforeY = player.Transform.YMillimeters;
            int beforeZ = player.Transform.ZMillimeters;
            int beforeYaw = player.Transform.YawMilliDegrees;
            Tick(host, ref frame);
            int displacement = Magnitude3(
                player.Transform.XMillimeters - beforeX,
                player.Transform.YMillimeters - beforeY,
                player.Transform.ZMillimeters - beforeZ);
            Assert.Greater(displacement, 0, scenario);
            Assert.LessOrEqual(displacement, 120, scenario);
            Assert.AreEqual(
                beforeYaw,
                player.Transform.YawMilliDegrees,
                scenario + " external contact velocity must not change facing");
            Tick(host, ref frame);
            Assert.AreEqual(
                beforeYaw,
                player.Transform.YawMilliDegrees,
                scenario + " persisted external velocity must not become facing on a later tick");
            TestContext.Out.WriteLine(
                "lcc_m5_t3 scenario=" + scenario
                + " next_tick_displacement_mm=" + displacement);
        }

        private static void AddAxisWall(
            CombatPhysicsWorld world,
            int bodyId,
            int colliderId,
            int xMillimeters,
            int zMillimeters,
            bool alongX,
            int sign)
        {
            Fix64 x = CharacterTestDeterministicUnits.MillimetersToFix64Meters(xMillimeters);
            Fix64 z = CharacterTestDeterministicUnits.MillimetersToFix64Meters(zMillimeters);
            Fix64 thickness = Fix64.FromRatio(1, 10);
            Fix64 reach = Fix64.FromInt(4);
            FixVector3 min;
            FixVector3 max;
            if (alongX)
            {
                min = new FixVector3(sign > 0 ? x : x - thickness, Fix64.Zero, z - reach);
                max = new FixVector3(sign > 0 ? x + thickness : x, Fix64.FromInt(3), z + reach);
            }
            else
            {
                min = new FixVector3(x - reach, Fix64.Zero, sign > 0 ? z : z - thickness);
                max = new FixVector3(x + reach, Fix64.FromInt(3), sign > 0 ? z + thickness : z);
            }
            var id = new CombatBodyId(bodyId);
            world.UpsertBody(new CombatPhysicsBody(new CombatEntityId(bodyId), id, FixVector3.Zero));
            world.UpsertAabbCollider(new CombatPhysicsAabbCollider(
                id,
                new CombatColliderId(colliderId),
                1,
                min,
                max));
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
                    77,
                    entity.Id.Value,
                    1,
                    1,
                    1,
                    1,
                    index,
                    int.MaxValue)));
            Assert.IsTrue(state.CommitFrame().Succeeded);
            Assert.IsTrue(state.TryBeginFrame(severFrame, out _));
            Assert.AreEqual(
                CharacterTestBodyPartStateStageResult.Staged,
                state.StageSever(new CharacterTestBodyPartSeverFact(
                    severFrame,
                    77,
                    entity.Id.Value,
                    index,
                    definition.SeverRequiredTag,
                    severFrame)));
            Assert.IsTrue(state.CommitFrame().Succeeded);
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

        private static int Magnitude(int x, int z) =>
            (int)Math.Ceiling(Math.Sqrt((double)x * x + (double)z * z));

        private static int Magnitude3(int x, int y, int z) =>
            (int)Math.Ceiling(Math.Sqrt((double)x * x + (double)y * y + (double)z * z));

        private static int ToMillimeters(Fix64 value) =>
            checked((int)(value.RawValue * 1000L / Fix64.Scale));

        private static float ToMeters(Fix64 value) =>
            (float)((double)value.RawValue / Fix64.Scale);

        private static string GetGameViewPath()
        {
            string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            return Path.Combine(
                projectRoot,
                "Docs",
                "Evidence",
                "LCC",
                "LCC-M5-T3",
                "LCC-M5-T3-Dragon14-Appendage-GameView.png");
        }
    }
}
