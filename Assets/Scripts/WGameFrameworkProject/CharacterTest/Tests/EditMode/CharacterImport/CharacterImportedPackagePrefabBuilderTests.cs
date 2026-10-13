using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using MxFramework.Animation;
using MxFramework.Animation.Unity;
using MxFramework.CharacterApplication;
using MxFramework.CharacterAction;
using MxFramework.CharacterRuntimeSpawn;
using MxFramework.CharacterRuntimeSpawn.Unity;
using MxFramework.Editor;
using MxFramework.Editor.CharacterAction;
using MxFramework.Editor.CharacterImport;
using MxFramework.Resources;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MxFramework.Tests.CharacterImport
{
    public class CharacterImportedPackagePrefabBuilderTests
    {
        [TestCase("displayName", false)]
        [TestCase("damage", false)]
        [TestCase("control", false)]
        [TestCase("hitWindow", true)]
        [TestCase("animationTiming", true)]
        public void ActionSamplingProjection_TracksOnlyPoseSamplingChanges(string edit, bool changesSampling)
        {
            const string root = "Assets/MxFrameworkGenerated/CharacterPackages/000001";
            CharacterImportedPackage package = CharacterImportedPackageJson.LoadFromDirectory(root);
            string sets = File.ReadAllText("Assets/MxFrameworkGenerated/CharacterActions/ActionSets/000001/character_action_sets.json");
            JObject actions = JObject.Parse(File.ReadAllText("Assets/MxFrameworkGenerated/CharacterActions/Libraries/Common/character_actions.json"));
            string before = SamplingProjectionForTest(package, sets, actions.ToString());
            JObject action = ((JArray)actions["actions"]).OfType<JObject>().Single(item => item["actionId"].Value<int>() == 301);
            JObject hit = ((JArray)action["combatTrack"]["events"]).OfType<JObject>().First(item => (string)item["kind"] == "HitTraceWindow");
            if (edit == "displayName") action["displayName"] = "Renamed action";
            else if (edit == "damage") hit["hitEvent"]["damage"]["baseDamage"] = 999;
            else if (edit == "hitWindow") hit["endFrame"] = hit["endFrame"].Value<int>() + 1;
            else if (edit == "animationTiming") action["animationTrack"]["events"][0]["playbackSpeed"] = 1.25;
            else
                foreach (JObject motion in ((JArray)action["motionTrack"]["events"]).OfType<JObject>())
                    if ((string)motion["kind"] == "SetControlScale")
                    { motion["movementControlScale"] = 0.5; motion["rotationControlScale"] = 0.25; }
            string after = SamplingProjectionForTest(package, sets, actions.ToString());
            Assert.AreEqual(changesSampling, before != after);
        }

        private static string SamplingProjectionForTest(CharacterImportedPackage package, string sets, string actions)
        {
            CharacterActionJsonImportResult imported = CharacterActionJsonImporter.Import(sets, actions);
            Assert.IsTrue(imported.Success, string.Join(" | ", imported.Diagnostics.Select(issue => issue.Code)));
            var registry = new CharacterActionConfigRegistry(imported.ActionSets, imported.Actions);
            var requests = (CharacterWeaponTraceBakeRequest[])typeof(CharacterImportedPackagePrefabBuilder)
                .GetMethod("CreateWeaponTraceBakeRequests", BindingFlags.Static | BindingFlags.NonPublic)
                .Invoke(null, new object[] { package, registry });
            return CharacterImportedPackagePrefabBuilder.CreateActionSamplingProjection(requests, registry);
        }

        [Test]
        public void IncrementalWeaponBake_PreservesWindowEndpointsAcrossSliceBoundaries()
        {
            var package = CharacterImportedPackageJson.LoadFromDirectory("Assets/MxFrameworkGenerated/CharacterPackages/000001");
            object[] registryArgs = { package, null, null };
            var flags = BindingFlags.Static | BindingFlags.NonPublic;
            Assert.IsTrue((bool)typeof(CharacterImportedPackagePrefabBuilder)
                .GetMethod("TryLoadCharacterActionRegistryForPackage", flags).Invoke(null, registryArgs));
            var requests = (CharacterWeaponTraceBakeRequest[])typeof(CharacterImportedPackagePrefabBuilder)
                .GetMethod("CreateWeaponTraceBakeRequests", flags).Invoke(null, new[] { (object)package, registryArgs[1] });
            var request = requests.First(item => item.ActionId.Value == 301);
            var state = new CharacterImportedPackagePrefabBuilder.WeaponTraceSamplingState(package, new[] { request }, false, false);
            var referenceState = new CharacterImportedPackagePrefabBuilder.WeaponTraceSamplingState(package, new[] { request }, false, false);
            using (var reference = new CharacterImportedPackagePrefabBuilder.WeaponTraceBakeJob(referenceState))
            using (var sliced = new CharacterImportedPackagePrefabBuilder.WeaponTraceBakeJob(state))
            {
                while (reference.Step(10000, 10000)) { }
                while (sliced.Step(7, 10000)) { }
                Assert.IsTrue(reference.Report.Success);
                Assert.IsTrue(sliced.Report.Success);
                var expected = CharacterWeaponTraceBakeStorage.SelectWeaponWindows(reference.WeaponCatalog, new[] { request });
                var actual = CharacterWeaponTraceBakeStorage.SelectWeaponWindows(sliced.WeaponCatalog, new[] { request });
                CollectionAssert.AreEqual(CharacterWeaponTraceBakeStorage.Encode(package.PackageId, expected, false),
                    CharacterWeaponTraceBakeStorage.Encode(package.PackageId, actual, false));
                Assert.IsTrue(actual.TryGetFrame(request.ClipKey, request.TraceId, request.ActiveStartFrame, out _));
                Assert.IsTrue(actual.TryGetFrame(request.ClipKey, request.TraceId, request.ActiveEndFrame, out _));
            }
        }

        [Test]
        public void IncrementalContactBake_MatchesSynchronousBytesAndBoundsEachStep()
        {
            const string root = "Assets/MxFrameworkGenerated/CharacterPackages/000003";
            CharacterImportedPackage package = CharacterImportedPackageJson.LoadFromDirectory(root);
            object[] arguments = { package, Array.Empty<CharacterWeaponTraceBakeRequest>(), null, null };
            typeof(CharacterImportedPackagePrefabBuilder).GetMethod("BuildSampledWeaponTraceBakeCatalog",
                BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, arguments);
            var expected = (CharacterWeaponTraceBakeCatalog)arguments[2];
            byte[] expectedBytes = CharacterWeaponTraceBakeStorage.Encode(package.PackageId, expected, true);
            int steps = 0;
            using (var job = CharacterImportedPackagePrefabBuilder.CreateWeaponTraceBakeJob(root, false))
            {
                while (!job.IsComplete)
                {
                    int before = job.ProcessedWorkUnits;
                    job.Step(32, 10000);
                    Assert.LessOrEqual(job.ProcessedWorkUnits - before, 32);
                    Assert.Less(++steps, 10000);
                }
                Assert.IsTrue(job.Report.Success, string.Join(" | ", job.Report.Issues.Select(issue => issue.Code)));
                Assert.Greater(steps, 1);
                Assert.AreEqual(0, job.WeaponCatalog.Count);
                CollectionAssert.AreEqual(expectedBytes,
                    CharacterWeaponTraceBakeStorage.Encode(package.PackageId, job.ContactCatalog, true));
            }
        }

        [Test]
        public void IncrementalContactBake_CancelReleasesPreviewAndCannotPublish()
        {
            int before = EditorSceneManager.previewSceneCount;
            var job = CharacterImportedPackagePrefabBuilder.CreateWeaponTraceBakeJob(
                "Assets/MxFrameworkGenerated/CharacterPackages/000003", false);
            try
            {
                for (int i = 0; i < 256 && EditorSceneManager.previewSceneCount == before; i++)
                    job.Step(1, 10000);
                Assert.Greater(EditorSceneManager.previewSceneCount, before);
                Assert.IsFalse(job.IsComplete);
                Assert.Throws<InvalidOperationException>(() => job.Publish());
                job.Dispose();
                Assert.AreEqual(before, EditorSceneManager.previewSceneCount);
                Assert.Throws<ObjectDisposedException>(() => job.Publish());
            }
            finally { job.Dispose(); }
        }

        [Test]
        public void IncrementalBake_MissingClipFailsBeforeOpeningPreviewOrSampling()
        {
            var package = CharacterImportedPackageJson.LoadFromDirectory("Assets/MxFrameworkGenerated/CharacterPackages/000003");
            var request = new CharacterWeaponTraceBakeRequest(new CharacterActionId(1), "test.missing.clip", 1, 0, 1, 1);
            var state = new CharacterImportedPackagePrefabBuilder.WeaponTraceSamplingState(package, new[] { request }, false);
            int previews = EditorSceneManager.previewSceneCount;
            using (var job = new CharacterImportedPackagePrefabBuilder.WeaponTraceBakeJob(state))
            {
                while (job.Step(16, 10000)) { }
                Assert.IsFalse(job.Report.Success);
                Assert.AreEqual(0, job.Report.ArtifactCount);
                Assert.AreEqual(previews, EditorSceneManager.previewSceneCount);
                Assert.Throws<InvalidOperationException>(() => job.Publish());
            }
        }

        [Test]
        public void BakeInputDependencies_IncludeFallbackAnimationAndImportSettings()
        {
            string[] paths = CharacterImportedPackagePrefabBuilder.CollectWeaponTraceBakeInputAssetPaths(
                "Assets/MxFrameworkGenerated/CharacterPackages/000001");
            const string fallback = "Assets/Art/MxFramework/Samples/Characters/Skeleton/AnimationClips/katana_atk1.anim";
            CollectionAssert.Contains(paths, fallback);
            CollectionAssert.Contains(paths, fallback + ".meta");
            CollectionAssert.Contains(paths, "Assets/MxFrameworkGenerated/CharacterActions/Libraries/Common/character_actions.json");
            CollectionAssert.Contains(paths, "Assets/MxFrameworkGenerated/CharacterActions/Profiles/000001/character_action_profile.json");
            CollectionAssert.Contains(paths, "Assets/MxFrameworkGenerated/CharacterPackages/000001/config/animation_clip_registry.json");
        }

        [Test]
        public void ContactMotionBake_UnarmedPackageSamplesAndRoundTripsWithoutWeaponTrace()
        {
            const string packageRoot = "Assets/MxFrameworkGenerated/CharacterPackages/000003";
            if (!Directory.Exists(packageRoot))
                Assert.Ignore("The unarmed character package is not present.");

            CharacterImportedPackage package = CharacterImportedPackageJson.LoadFromDirectory(packageRoot);
            Assert.IsEmpty(package.Geometry.WeaponTraces);
            Assert.IsTrue(package.Configs.BodyPartContactPoints.Any(
                point => point.MotionMode == CharacterBodyPartContactMotionMode.BakedSocket));
            var requests = CharacterImportedPackagePrefabBuilder.CreateBodyPartContactMotionBakeRequests(package);
            Assert.Greater(requests.Length, 0);
            int socketId = package.Configs.BodyPartContactPoints
                .Where(point => point.MotionMode == CharacterBodyPartContactMotionMode.BakedSocket
                    && point.MotionSocketId > 0)
                .Min(point => point.MotionSocketId);
            Assert.IsTrue(requests.All(request => request.GroupSocketId == socketId));

            object[] arguments = { package, Array.Empty<CharacterWeaponTraceBakeRequest>(), null, null };
            var weapons = (CharacterWeaponTraceBakeCatalog)typeof(CharacterImportedPackagePrefabBuilder)
                .GetMethod("BuildSampledWeaponTraceBakeCatalog", BindingFlags.Static | BindingFlags.NonPublic)
                .Invoke(null, arguments);
            var contacts = (CharacterWeaponTraceBakeCatalog)arguments[2];
            var report = (CharacterWeaponTraceBakeBuildReport)arguments[3];
            Assert.IsTrue(report.Success, string.Join(" | ", report.Issues.Select(issue => issue.Code + ":" + issue.Message)));
            Assert.AreEqual(0, weapons.Count);
            Assert.AreEqual(requests.Length, contacts.Count);
            Assert.AreEqual(requests.Length, report.RequestCount);

            byte[] binary = CharacterWeaponTraceBakeStorage.Encode(package.PackageId, contacts, true);
            CharacterWeaponTraceBakeCatalog restored = CharacterWeaponTraceBakeStorage.Decode(
                binary, package.PackageId, true, out CharacterWeaponTraceBakeBuildReport loadReport);
            Assert.IsTrue(loadReport.Success);
            Assert.IsTrue(restored.TryGetSocketFrame(requests[0].ClipKey, socketId, socketId, 1, out _));
            Assert.IsTrue(restored.EnumerateArtifacts().All(pair => pair.Value.WeaponTraceFrames.Count == 0));
        }

        [TestCase("000001")]
        [TestCase("000004")]
        [TestCase("000008")]
        public void WeaponTracePoseSampler_MatchesRuntimeHumanoidPoseWithoutRootMotion(string packageId)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/MxFrameworkGenerated/CharacterPackages/" + packageId + "/prefabs/" + packageId + "_character_preview.prefab");
            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(
                "Assets/Art/MxFramework/Samples/Characters/Skeleton/AnimationClips/katana_atk1.anim");
            Assert.NotNull(prefab);
            Assert.NotNull(clip);
            Scene preview = EditorSceneManager.NewPreviewScene();
            GameObject sampled = null;
            GameObject reference = null;
            UnityPlayablesAnimationBackend backend = null;
            try
            {
                sampled = (GameObject)PrefabUtility.InstantiatePrefab(prefab, preview);
                reference = (GameObject)PrefabUtility.InstantiatePrefab(prefab, preview);
                Animator sampledAnimator = sampled.GetComponentInChildren<Animator>(true);
                Animator runtimeAnimator = reference.GetComponentInChildren<Animator>(true);
                runtimeAnimator.applyRootMotion = false;
                runtimeAnimator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                var key = new ResourceKey("test.weapon.rootmotion", ResourceTypeIds.AnimationClip);
                var manager = new ResourceManager();
                manager.RegisterProvider(new MemoryResourceProvider().Register("clip", clip));
                manager.AddCatalog(new ResourceCatalog("weapon.bake.test", string.Empty, new[] {
                    new ResourceCatalogEntry(key.Id, key.TypeId, "memory", "clip") }));
                backend = new UnityPlayablesAnimationBackend(runtimeAnimator, manager,
                    new MxAnimationSetDefinition("weapon.bake.test", 1, default, default));
                Assert.IsTrue(backend.Play(new MxAnimationPlayRequest { ClipKey = key }).Success);
                backend.Tick(0f);
                using (var sampler = new CharacterImportedPackagePrefabBuilder.WeaponTracePoseSampler(sampled, clip))
                {
                    foreach (int frame in new[] { 0, 12, 24, 40, 60, 24 })
                    {
                        sampler.Sample(frame / 60f);
                        Assert.IsTrue(backend.TrySynchronizeCurrentClipTime(MxAnimationLayerId.Base, key, frame / 60d));
                        backend.Tick(1f / 60f);
                        Transform actual = sampledAnimator.GetBoneTransform(HumanBodyBones.RightHand);
                        Transform expected = runtimeAnimator.GetBoneTransform(HumanBodyBones.RightHand);
                        Assert.Less(Vector3.Distance(actual.position, expected.position), 0.0001f,
                            packageId + " frame " + frame + " retains extracted root translation in the baked hand pose.");
                        Assert.Less(Quaternion.Angle(actual.rotation, expected.rotation), 0.01f);
                    }
                }
            }
            finally
            {
                backend?.Release();
                if (sampled != null) UnityEngine.Object.DestroyImmediate(sampled);
                if (reference != null) UnityEngine.Object.DestroyImmediate(reference);
                EditorSceneManager.ClosePreviewScene(preview);
            }
        }

        [Test]
        public void CodexFlowHurtboxBake_LocomotionTracksExcludeAccumulatedRootMotion()
        {
            const string generatedRoot =
                "Assets/MxFrameworkGenerated/CharacterPackages/codex_flow_test/generated/";
            TextAsset index = AssetDatabase.LoadAssetAtPath<TextAsset>(
                generatedRoot + "character_hurtbox_bake_catalog.json");
            TextAsset binary = AssetDatabase.LoadAssetAtPath<TextAsset>(
                generatedRoot + "character_hurtbox_bake_catalog.bytes");
            Assert.NotNull(index);
            Assert.NotNull(binary);

            CharacterHurtboxBakeCatalog catalog =
                CharacterHurtboxBakeCatalogBinary.Load(
                    index.text,
                    binary.bytes,
                    out string[] issues);
            Assert.IsEmpty(issues);

            const long maximumPlanarOffsetMillimeters = 1500L;
            int checkedPoseCount = 0;
            foreach (KeyValuePair<CharacterHurtboxBakeKey, CharacterHurtboxBakeTrack> pair
                     in catalog.EnumerateTracks())
            {
                string clipKey = pair.Key.ClipKey ?? string.Empty;
                if (clipKey.IndexOf("standing_walk_", StringComparison.Ordinal) < 0
                    && clipKey.IndexOf("standing_run_", StringComparison.Ordinal) < 0)
                {
                    continue;
                }

                IReadOnlyList<CharacterHurtboxBakeRun> runs = pair.Value.Runs;
                for (int runIndex = 0; runIndex < runs.Count; runIndex++)
                {
                    CharacterHurtboxBakePose pose = runs[runIndex].Pose;
                    AssertPlanarHurtboxPointIsBodyLocal(
                        pair.Key,
                        pose.PointA,
                        maximumPlanarOffsetMillimeters);
                    if (pose.ShapeKind == CharacterHurtboxBakeShapeKind.Capsule)
                    {
                        AssertPlanarHurtboxPointIsBodyLocal(
                            pair.Key,
                            pose.PointB,
                            maximumPlanarOffsetMillimeters);
                    }
                    checkedPoseCount++;
                }
            }

            Assert.Greater(checkedPoseCount, 0);
        }

        private static void AssertPlanarHurtboxPointIsBodyLocal(
            CharacterHurtboxBakeKey key,
            CharacterHurtboxBakeVector3 point,
            long maximumPlanarOffsetMillimeters)
        {
            Assert.LessOrEqual(
                Math.Abs(point.X),
                maximumPlanarOffsetMillimeters,
                key.ClipKey + "/" + key.ColliderId + " retained accumulated RootT.x.");
            Assert.LessOrEqual(
                Math.Abs(point.Z),
                maximumPlanarOffsetMillimeters,
                key.ClipKey + "/" + key.ColliderId + " retained accumulated RootT.z.");
        }

        [Test]
        public void AbstractBoneMap_ResolvesLocatorAndModelRootSocketsThroughOneRuntimeInterface()
        {
            var root = new GameObject("abstract-bone-test-root");
            try
            {
                Transform body = new GameObject("BodyModel").transform;
                body.SetParent(root.transform, false);
                Transform locators = new GameObject("locators").transform;
                locators.SetParent(body, false);
                Transform top = new GameObject("top_vfx").transform;
                top.SetParent(locators, false);
                Transform socketsRoot = new GameObject("Sockets").transform;
                socketsRoot.SetParent(root.transform, false);

                var geometry = new CharacterImportedGeometryBinding(
                    "test_package",
                    null,
                    Array.Empty<CharacterBodyColliderRuntimeBinding>(),
                    Array.Empty<CharacterHitZoneRuntimeBinding>(),
                    new[]
                    {
                        new CharacterSocketRuntimeBinding(
                            1, CharacterAbstractBoneIds.Head, "body", string.Empty,
                            "locators/top_vfx", CharacterRuntimePose.Identity, "Vfx", "topVfx"),
                        new CharacterSocketRuntimeBinding(
                            2, CharacterAbstractBoneIds.BodyCenter, string.Empty, string.Empty,
                            string.Empty, new CharacterRuntimePose(
                                "ModelRoot",
                                string.Empty,
                                new CharacterRuntimeVector3(0f, 3.72f, 0f),
                                new CharacterRuntimeQuaternion(0f, 0f, 0f, 1f),
                                new CharacterRuntimeVector3(1f, 1f, 1f)),
                            "Gameplay")
                    },
                    Array.Empty<CharacterWeaponAttachmentRuntimeBinding>(),
                    Array.Empty<CharacterWeaponTraceRuntimeBinding>());
                var package = new CharacterImportedPackage(
                    string.Empty,
                    "test_package",
                    null,
                    geometry,
                    null,
                    null,
                    null);

                Type bindingType = BuilderType.GetNestedType(
                    "TransformBoneBindingInfo",
                    BindingFlags.NonPublic);
                Assert.NotNull(bindingType);
                object boneBindings = Activator.CreateInstance(
                    typeof(List<>).MakeGenericType(bindingType));
                var resolvedSocketIds = new HashSet<string>(StringComparer.Ordinal);
                MethodInfo createSockets = BuilderType.GetMethod(
                    "CreateSockets",
                    BindingFlags.NonPublic | BindingFlags.Static);
                Assert.NotNull(createSockets);
                object sockets = createSockets.Invoke(
                    null,
                    new[] { package, socketsRoot, body, boneBindings, resolvedSocketIds });

                MethodInfo addMap = BuilderType.GetMethod(
                    "AddAbstractBoneRuntimeMap",
                    BindingFlags.NonPublic | BindingFlags.Static);
                Assert.NotNull(addMap);
                addMap.Invoke(null, new[] { root, package, sockets, resolvedSocketIds });

                CharacterAbstractBoneRuntimeMap map =
                    root.GetComponent<CharacterAbstractBoneRuntimeMap>();
                Assert.NotNull(map);
                Assert.AreEqual(2, map.Bindings.Count);
                Assert.IsTrue(map.TryResolve(CharacterAbstractBoneIds.Head, out Transform head));
                Assert.IsTrue(map.TryResolve(CharacterAbstractBoneIds.BodyCenter, out Transform center));
                Assert.NotNull(head);
                Assert.NotNull(center);
                Assert.That(Vector3.Distance(center.position, new Vector3(0f, 3.72f, 0f)), Is.LessThan(0.0001f));
                Vector3 stablePosition = center.position;
                top.localPosition = new Vector3(1f, 4f, -2f);
                top.localRotation = Quaternion.Euler(25f, 80f, -15f);
                Assert.That(Vector3.Distance(center.position, stablePosition), Is.LessThan(0.0001f),
                    "ModelRoot camera focus must not inherit animated head or locator motion.");
                CollectionAssert.AreEquivalent(
                    new[] { "topVfx", "bodyCenter" },
                    resolvedSocketIds);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void BoneResolver_MapsHumanoidTemplateAliasesToAutoRigProUniversalNames()
        {
            var root = new GameObject("arp-bone-resolver-root");
            try
            {
                string[] autoRigProBones =
                {
                    "head.x", "spine_02.x",
                    "arm_stretch.l", "forearm_stretch.l", "hand.l",
                    "arm_stretch.r", "forearm_stretch.r", "hand.r",
                    "thigh_stretch.l", "leg_stretch.l", "foot.l",
                    "thigh_stretch.r", "leg_stretch.r", "foot.r"
                };
                for (int index = 0; index < autoRigProBones.Length; index++)
                {
                    Transform bone = new GameObject(autoRigProBones[index]).transform;
                    bone.SetParent(root.transform, false);
                }

                MethodInfo find = BuilderType.GetMethod(
                    "FindBodyChild",
                    BindingFlags.NonPublic | BindingFlags.Static);
                Assert.NotNull(find);
                var expected = new Dictionary<string, string>
                {
                    ["bone.head"] = "head.x",
                    ["bone.spine"] = "spine_02.x",
                    ["bone.upper_arm.L"] = "arm_stretch.l",
                    ["bone.forearm.L"] = "forearm_stretch.l",
                    ["bone.hand.L"] = "hand.l",
                    ["bone.upper_arm.R"] = "arm_stretch.r",
                    ["bone.forearm.R"] = "forearm_stretch.r",
                    ["bone.hand.R"] = "hand.r",
                    ["bone.thigh.L"] = "thigh_stretch.l",
                    ["bone.shin.L"] = "leg_stretch.l",
                    ["bone.foot.L"] = "foot.l",
                    ["bone.thigh.R"] = "thigh_stretch.r",
                    ["bone.shin.R"] = "leg_stretch.r",
                    ["bone.foot.R"] = "foot.r"
                };
                foreach (KeyValuePair<string, string> pair in expected)
                {
                    Transform resolved = find.Invoke(
                        null,
                        new object[] { root.transform, pair.Key, string.Empty }) as Transform;
                    Assert.NotNull(resolved, pair.Key);
                    Assert.AreEqual(pair.Value, resolved.name, pair.Key);
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void BoneResolver_MapsShinAliasesToWGameLowerLegNames()
        {
            var root = new GameObject("wgame-lower-leg-resolver-root");
            try
            {
                Transform left = new GameObject("lower_leg.l").transform;
                left.SetParent(root.transform, false);
                Transform right = new GameObject("lower_leg.r").transform;
                right.SetParent(root.transform, false);

                MethodInfo find = BuilderType.GetMethod(
                    "FindBodyChild",
                    BindingFlags.NonPublic | BindingFlags.Static);
                Assert.NotNull(find);

                Transform resolvedLeft = find.Invoke(
                    null,
                    new object[] { root.transform, "bone.shin.L", "shin_l" }) as Transform;
                Transform resolvedRight = find.Invoke(
                    null,
                    new object[] { root.transform, "bone.shin.R", "shin_r" }) as Transform;

                Assert.AreEqual(left, resolvedLeft);
                Assert.AreEqual(right, resolvedRight);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void RuntimeWeaponPose_ComposesCharacterSlotBeforeSingleWeaponPose()
        {
            Quaternion slotRotation = Quaternion.Euler(0f, 90f, 0f);
            Quaternion weaponRotation = Quaternion.Euler(15f, 0f, 0f);
            var slotPose = new CharacterRuntimePose(
                "Socket", "mainHand",
                new CharacterRuntimeVector3(1f, 2f, 3f),
                new CharacterRuntimeQuaternion(slotRotation.x, slotRotation.y, slotRotation.z, slotRotation.w),
                new CharacterRuntimeVector3(2f, 1f, 0.5f));
            var weaponPose = new CharacterRuntimePose(
                "ModelRoot", string.Empty,
                new CharacterRuntimeVector3(0.25f, 0.5f, 1f),
                new CharacterRuntimeQuaternion(weaponRotation.x, weaponRotation.y, weaponRotation.z, weaponRotation.w),
                new CharacterRuntimeVector3(0.5f, 3f, 4f));
            var binding = new CharacterWeaponAttachmentRuntimeBinding(
                1, 1, 1, slotPose, slotPose, weaponPose,
                string.Empty, MxFramework.Game.CharacterTest.CharacterTestAuthoredContentIds.D403_Id554678986, "weapon.sword.model", "common", "resourceMapping",
                0, 0, 0.05f, "CapsuleSweep", "weapon.attachment.test.mainhand.sword",
                traceLabel: "trace.sword", equipmentSlotLabel: "mainHand", attachSocketLabel: "mainHand", weaponLabel: "sword");

            MethodInfo compose = typeof(CharacterRuntimePrefabComposer).GetMethod(
                "ComposeWeaponPose", BindingFlags.NonPublic | BindingFlags.Static);
            Assert.NotNull(compose);
            object[] arguments = { binding, null, null, null };
            compose.Invoke(null, arguments);

            Vector3 actualPosition = (Vector3)arguments[1];
            Quaternion actualRotation = (Quaternion)arguments[2];
            Vector3 actualScale = (Vector3)arguments[3];
            Vector3 expectedPosition = new Vector3(1f, 2f, 3f) +
                slotRotation * Vector3.Scale(new Vector3(2f, 1f, 0.5f), new Vector3(0.25f, 0.5f, 1f));
            Assert.That(Vector3.Distance(actualPosition, expectedPosition), Is.LessThan(0.0001f));
            Assert.That(Quaternion.Angle(actualRotation, slotRotation * weaponRotation), Is.LessThan(0.001f));
            Assert.That(Vector3.Distance(actualScale, new Vector3(1f, 3f, 2f)), Is.LessThan(0.0001f));
        }

        private const string TempRoot = "Assets/MxFrameworkGenerated/CharacterPackages/__prefab_builder_tests";
        private const string ConfigRoot = TempRoot + "/config";
        private const string AssetRoot = TempRoot + "/resources/models";
        private const string TextureRoot = TempRoot + "/resources/textures";
        private const string ActionProfileRoot = "Assets/MxFrameworkGenerated/CharacterActions/Profiles/test_package";
        private const string ActionSetRoot = "Assets/MxFrameworkGenerated/CharacterActions/ActionSets/test_package";
        private const string ActionProfilePath = ActionProfileRoot + "/character_action_profile.json";
        private const string ActionSetPath = ActionSetRoot + "/character_action_sets.json";
        private const string ReactionProfilePath = ActionSetRoot + "/character_reaction_profiles.json";
        private const string CommonActionLibraryPath = "Assets/MxFrameworkGenerated/CharacterActions/Libraries/Common/character_actions.json";
        private const string GlobalProfilePath = "Assets/Config/MxFramework/ResourceProfiles/global_resource_mapping_profile.v2.json";
        private const string TemporaryGlobalProfilePath = ConfigRoot + "/global_resource_mapping_profile.v2.json";
        private static readonly Type BuilderType = typeof(CharacterImportedPackagePrefabBuilder);

        [TearDown]
        public void TearDown()
        {
            RemoveGeneratedTestPackageActions();
            AssetDatabase.DeleteAsset(TempRoot + "/runtime_resources");
            AssetDatabase.DeleteAsset(TempRoot + "/prefabs");
            AssetDatabase.DeleteAsset(TempRoot + "/materials");
            AssetDatabase.DeleteAsset(TempRoot);
            AssetDatabase.DeleteAsset(ActionProfileRoot);
            AssetDatabase.DeleteAsset(ActionSetRoot);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        [Test]
        public void PrefabConfigAggregateHash_ChangesWhenOptionalConfigChanges()
        {
            EnsureTempRoot();
            Directory.CreateDirectory(ConfigRoot);
            string path = ConfigRoot + "/optional_prefab_postprocessor.json";
            File.WriteAllText(path, "{\"value\":1}\n");
            string first = CharacterImportedPackagePrefabBuilder
                .ComputePrefabConfigAggregateHash(TempRoot);

            File.WriteAllText(path, "{\"value\":2}\n");
            string second = CharacterImportedPackagePrefabBuilder
                .ComputePrefabConfigAggregateHash(TempRoot);

            Assert.That(first, Does.StartWith("sha256:"));
            Assert.That(second, Does.StartWith("sha256:"));
            Assert.AreNotEqual(first, second);
        }

        [Test]
        public void ReadResourcePreviewInfos_UsesUnityCatalogAssetAndKeepsMappingOnlyForWrapperPose()
        {
            EnsureTempRoot();
            WriteMapping("char.test.model.body", "characterModel", "Assets/ShouldNotBeConsumed/mapping_only.glb", scaleX: 2f);
            string modelPath = CreatePrefabAsset(AssetRoot + "/catalog_model.prefab");
            CharacterImportedPackage package = CreatePackage(CreateCatalogEntry(
                "char.test.model.body",
                ResourceTypeIds.GameObject,
                modelPath,
                AssetDatabase.AssetPathToGUID(modelPath),
                "Imported",
                "characterModel",
                "character.model"));

            IDictionary resources = InvokeReadResourcePreviewInfos(package);
            object resource = resources["char.test.model.body"];

            Assert.IsNotNull(resource);
            Assert.AreEqual(modelPath, ReadProperty<string>(resource, "AssetPath"));
            Assert.IsTrue(ReadProperty<bool>(resource, "IsImportReady"));
            Assert.AreEqual(2f, ReadProperty<CharacterRuntimePose>(resource, "WrapperPose").Scale.X);
            Assert.AreNotEqual("Assets/ShouldNotBeConsumed/mapping_only.glb", ReadProperty<string>(resource, "AssetPath"));
        }

        [Test]
        public void ReadResourcePreviewInfos_ReportsNotReadyCatalogEntryForPlaceholderDiagnostics()
        {
            EnsureTempRoot();
            WriteMapping("char.test.model.body", "characterModel", "Assets/ShouldNotBeConsumed/mapping_only.glb", scaleX: 1f);
            CharacterImportedPackage package = CreatePackage(CreateCatalogEntry(
                "char.test.model.body",
                ResourceTypeIds.GameObject,
                "Assets/MissingImportedModel.glb",
                "missing-guid",
                "UnityMissing",
                "characterModel",
                "character.model"));

            IDictionary resources = InvokeReadResourcePreviewInfos(package);
            object resource = resources["char.test.model.body"];

            Assert.IsNotNull(resource);
            Assert.IsFalse(ReadProperty<bool>(resource, "IsImportReady"));
            StringAssert.Contains("UnityMissing", ReadProperty<string>(resource, "Diagnostic"));
            StringAssert.Contains("Assets/MissingImportedModel.glb", ReadProperty<string>(resource, "Diagnostic"));
        }

        [Test]
        public void ReadResourcePreviewInfos_TreatsLoadablePendingUnityImportAsReady()
        {
            EnsureTempRoot();
            string modelPath = CreatePrefabAsset(AssetRoot + "/pending_model.prefab");
            CharacterImportedPackage package = CreatePackage(CreateCatalogEntry(
                "char.test.model.body",
                ResourceTypeIds.GameObject,
                modelPath,
                AssetDatabase.AssetPathToGUID(modelPath),
                "PendingUnityImport",
                "characterModel",
                "character.model"));

            IDictionary resources = InvokeReadResourcePreviewInfos(package);
            object resource = resources["char.test.model.body"];

            Assert.IsNotNull(resource);
            Assert.IsTrue(ReadProperty<bool>(resource, "IsImportReady"));
            Assert.AreEqual("Imported", ReadProperty<string>(resource, "ImportStatus"));
            Assert.AreEqual(string.Empty, ReadProperty<string>(resource, "Diagnostic"));
        }

        [Test]
        public void ReadResourcePreviewInfos_ResolvesPublicWeaponFromGlobalProfile()
        {
            EnsureTempRoot();
            string weaponPath = CreatePrefabAsset(AssetRoot + "/common_weapon.prefab");
            string weaponGuid = AssetDatabase.AssetPathToGUID(weaponPath);
            string profileJson = GlobalWeaponProfileJson(weaponPath);

            WithTemporaryGlobalProfile(profileJson, () =>
            {
                CharacterImportedPackage package = CreatePackage();
                IDictionary resources = InvokeReadResourcePreviewInfos(package);

                Assert.IsTrue(resources.Contains("weapon.common_weapon_1.model"));
                object resource = resources["weapon.common_weapon_1.model"];
                Assert.AreEqual("weapon.common_weapon_1.model", ReadProperty<string>(resource, "Key"));
                Assert.AreEqual("weaponModel", ReadProperty<string>(resource, "Usage"));
                Assert.AreEqual(weaponPath, ReadProperty<string>(resource, "AssetPath"));
                Assert.AreEqual(weaponGuid, ReadProperty<string>(resource, "AssetGuid"));
                Assert.IsTrue(ReadProperty<bool>(resource, "IsImportReady"));
                Assert.AreEqual("Imported", ReadProperty<string>(resource, "ImportStatus"));
                Assert.AreEqual(1f, ReadProperty<CharacterRuntimePose>(resource, "WrapperPose").Scale.X);

                object resolved = InvokeFindResource(resources, "weaponModel", "weapon.common_weapon_1.model");
                Assert.IsNotNull(resolved);
                Assert.AreEqual("weapon.common_weapon_1.model", ReadProperty<string>(resolved, "Key"));
            });
        }

        [Test]
        public void FindResource_DoesNotFallbackWhenExplicitWeaponResourceIsMissing()
        {
            EnsureTempRoot();
            string modelPath = CreatePrefabAsset(AssetRoot + "/weapon_model.prefab");
            CharacterImportedPackage package = CreatePackage(CreateCatalogEntry(
                "char.test.weapon.sword.model",
                ResourceTypeIds.GameObject,
                modelPath,
                AssetDatabase.AssetPathToGUID(modelPath),
                "Imported",
                "weaponModel",
                "weapon.model"));
            IDictionary resources = InvokeReadResourcePreviewInfos(package);

            object missing = InvokeFindResource(resources, "weaponModel", "char.test.weapon.missing.model");
            object fallback = InvokeFindResource(resources, "weaponModel", string.Empty);

            Assert.IsNull(missing);
            Assert.IsNotNull(fallback);
            Assert.AreEqual("char.test.weapon.sword.model", ReadProperty<string>(fallback, "Key"));
        }

        [Test]
        public void ShouldMountWeaponAttachment_RequiresBoundResourceAndGeneratedPrefab()
        {
            var emptyPose = new CharacterRuntimePose(string.Empty, string.Empty, default, default, new CharacterRuntimeVector3(1f, 1f, 1f));
            var removed = new CharacterWeaponAttachmentRuntimeBinding(
                1,
                1,
                1,
                emptyPose,
                string.Empty,
                0);
            var bound = new CharacterWeaponAttachmentRuntimeBinding(
                1,
                1,
                1,
                emptyPose,
                "char.test.weapon.model",
                0);
            var prefabs = new Dictionary<string, GameObject>(StringComparer.Ordinal);
            GameObject prefab = new GameObject("WeaponPrefab");
            try
            {
                Assert.IsFalse(InvokeShouldMountWeaponAttachment(removed, prefabs));
                Assert.IsFalse(InvokeShouldMountWeaponAttachment(bound, prefabs));

                prefabs["mainHand::weapon.test"] = prefab;

                Assert.IsFalse(InvokeShouldMountWeaponAttachment(removed, prefabs));
                Assert.IsTrue(InvokeShouldMountWeaponAttachment(bound, prefabs));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(prefab);
            }
        }

        [Test]
        public void SynchronizeCharacterActionProfile_CreatesDiscoverableProfileAndActionSet()
        {
            CharacterImportedPackage package = CreatePackage();

            InvokeSynchronizeCharacterActionProfile(package);

            AssertGeneratedCharacterActionSources("char.test_package");
        }

        [Test]
        public void SynchronizeCharacterActionProfile_Schema8PreservesCompilerOwnedProjections()
        {
            EnsureFolderPath(TempRoot + "/package_cache");
            EnsureFolderPath(ActionProfileRoot);
            EnsureFolderPath(ActionSetRoot);
            File.WriteAllText(
                TempRoot + "/package_cache/character_application.json",
                "{\n  \"schemaVersion\": \"8.0\"\n}\n");
            const string profileJson =
                "{\n  \"schemaVersion\": \"0.3.0\",\n  \"profiles\": [{\n"
                + "    \"stableId\": \"test_package.default\",\n"
                + "    \"actionSetIds\": [\"test_package.sword_empty\"]\n  }]\n}\n";
            const string actionSetJson =
                "{\n  \"schemaVersion\": \"0.3.0\",\n  \"actionSets\": [{\n"
                + "    \"stableId\": \"test_package.sword_empty\",\n"
                + "    \"reactionProfileId\": \"reaction.test_package.sword_empty\"\n  }]\n}\n";
            const string reactionProfileJson =
                "{\n  \"schemaVersion\": \"0.3.0\",\n  \"reactionProfiles\": [{\n"
                + "    \"stableId\": \"reaction.test_package.sword_empty\",\n"
                + "    \"rules\": []\n  }]\n}\n";
            File.WriteAllText(ActionProfilePath, profileJson);
            File.WriteAllText(ActionSetPath, actionSetJson);
            File.WriteAllText(ReactionProfilePath, reactionProfileJson);
            CharacterImportedPackage package = CreatePackage();

            InvokeSynchronizeCharacterActionProfile(package);

            Assert.AreEqual(profileJson, File.ReadAllText(ActionProfilePath));
            Assert.AreEqual(actionSetJson, File.ReadAllText(ActionSetPath));
            Assert.AreEqual(reactionProfileJson, File.ReadAllText(ReactionProfilePath));
        }

        [Test]
        public void SynchronizeCharacterActionProfile_GeneratesReactionActionAnimationFromCachedApplicationConfig()
        {
            EnsureFolderPath(TempRoot + "/package_cache");
            WriteReactionApplicationConfig("art.test.reaction.cohesion_break");
            CharacterImportedPackage package = CreatePackage();

            InvokeSynchronizeCharacterActionProfile(package);

            CharacterActionJsonDto reactionAction = FindGeneratedCommonAction("test_package_cohesion_break_react");
            Assert.IsNotNull(reactionAction);
            Assert.AreEqual(CharacterActionCategory.Reaction.ToString(), reactionAction.Category);
            Assert.AreEqual(18, reactionAction.DurationFrames);
            Assert.AreEqual(1, reactionAction.AnimationTrack.Events.Length);
            Assert.AreEqual("art.test.reaction.cohesion_break", reactionAction.AnimationTrack.Events[0].AnimationClipResourceKey);
            Assert.AreEqual(1, reactionAction.MotionTrack.Events.Length);
            Assert.AreEqual(0, reactionAction.MotionTrack.Events[0].Frame);
            Assert.AreEqual(17, reactionAction.MotionTrack.Events[0].EndFrame);
            Assert.AreEqual("test_package_cohesion_break_react.control_lock", reactionAction.MotionTrack.Events[0].StableEventId);
        }

        [Test]
        public void SynchronizeCharacterActionProfile_GeneratesDefaultDeathAnimationWhenNoDeathSlotConfigured()
        {
            CharacterImportedPackage package = CreatePackage();

            InvokeSynchronizeCharacterActionProfile(package);

            CharacterActionJsonDto deathAction = FindGeneratedCommonAction("test_package_death_react");
            Assert.IsNotNull(deathAction);
            Assert.AreEqual(CharacterActionCategory.Reaction.ToString(), deathAction.Category);
            Assert.AreEqual(60, deathAction.DurationFrames);
            Assert.AreEqual(1, deathAction.AnimationTrack.Events.Length);
            Assert.AreEqual(2, deathAction.MotionTrack.Events.Length);
            Assert.AreEqual(59, deathAction.MotionTrack.Events[0].EndFrame);
            Assert.AreEqual(CharacterActionTrackEventKind.SetCollisionProfile.ToString(), deathAction.MotionTrack.Events[1].Kind);
            Assert.AreEqual(24, deathAction.MotionTrack.Events[1].Frame);
            Assert.AreEqual(59, deathAction.MotionTrack.Events[1].EndFrame);
            Assert.AreEqual("corpse", deathAction.MotionTrack.Events[1].CollisionProfileId);
            Assert.AreEqual("anim.reaction.death", deathAction.AnimationTrack.Events[0].AnimationActionKey);
            Assert.AreEqual(
                "art.character.skeleton.animation.damage_ko_mid_2",
                deathAction.AnimationTrack.Events[0].AnimationClipResourceKey);
        }

        [Test]
        public void SynchronizeCharacterActionProfile_RespectsExplicitEmptyDeathAnimation()
        {
            EnsureFolderPath(TempRoot + "/package_cache");
            WriteDeathReactionApplicationConfig(string.Empty);
            CharacterImportedPackage package = CreatePackage();

            InvokeSynchronizeCharacterActionProfile(package);

            CharacterActionJsonDto deathAction = FindGeneratedCommonAction("test_package_death_react");
            Assert.IsNotNull(deathAction);
            Assert.AreEqual(CharacterActionCategory.Reaction.ToString(), deathAction.Category);
            Assert.AreEqual(0, deathAction.AnimationTrack.Events.Length);
        }

        [Test]
        public void BuildPrefab_GeneratesCleanCharacterPrefabAndDefaultWeaponResourceMetadata()
        {
            EnsureTempRoot();
            string bodyPath = CreatePrefabAsset(AssetRoot + "/body.prefab");
            string katanaPath = CreatePrefabAsset(AssetRoot + "/katana.prefab");
            string shieldPath = CreatePrefabAsset(AssetRoot + "/kite_shield.prefab");
            WriteRuntimeEquipmentPackageFiles(bodyPath, katanaPath, shieldPath);

            string prefabPath = CharacterImportedPackagePrefabBuilder.BuildPrefab(TempRoot, selectPrefab: false);

            Assert.IsFalse(string.IsNullOrWhiteSpace(prefabPath));
            GameObject characterPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            Assert.IsNotNull(characterPrefab);
            AssertGeneratedCharacterActionSources("test.character");
            AssertNoForbiddenWeaponDescendants(characterPrefab);
            AssertSlimRuntimePrefab(characterPrefab, "test_package");

            Assert.IsNotNull(AssetDatabase.LoadAssetAtPath<GameObject>(
                TempRoot + "/prefabs/weapons/test_package_mainHand_weapon_katana.prefab"));
            Assert.IsNotNull(AssetDatabase.LoadAssetAtPath<GameObject>(
                TempRoot + "/prefabs/weapons/test_package_offHand_weapon_kite_shield.prefab"));

            CharacterImportedPackage package = CharacterImportedPackageJson.LoadFromDirectory(Path.GetFullPath(TempRoot));
            GameObject instance = null;
            CharacterDefaultEquipmentRuntimeBinder binder = null;
            try
            {
                ResourceManager resourceManager = CreateGeneratedPackageWeaponResourceManager(package);
                CharacterRuntimePrefabCompositionResult composition = ComposePrefabForTest(
                    characterPrefab,
                    package,
                    resourceManager,
                    instantiateDefaultWeapons: true,
                    out instance);
                Assert.IsTrue(composition.Success, string.Join(" | ", composition.Issues));
                Assert.IsNull(instance.GetComponent<CharacterRuntimeInputMotionController>());
                Assert.IsNotNull(composition.ControllerBinding);
                Assert.IsNotNull(composition.LocomotionController);
                Assert.IsNotNull(composition.BoneSync);
                binder = composition.DefaultEquipmentBinder;
                Assert.IsNotNull(binder);
                Assert.AreEqual(2, binder.DefaultWeapons.Count);
                Assert.AreEqual(2, binder.SpawnedWeapons.Count);

                for (int i = 0; i < binder.SpawnedWeapons.Count; i++)
                    Assert.IsTrue(binder.SpawnedWeapons[i].transform.parent.name.StartsWith("Socket_", StringComparison.Ordinal));

                CollectionAssert.AreEquivalent(
                    new[]
                    {
                        "char.test_package.prefab.weapon.mainhand.weapon_katana",
                        "char.test_package.prefab.weapon.offhand.weapon_kite_shield"
                    },
                    ReadDefaultWeaponResourceIds(binder));

                for (int i = 0; i < binder.DefaultWeapons.Count; i++)
                {
                    CharacterDefaultWeaponPrefabBinding weapon = binder.DefaultWeapons[i];
                    Assert.AreEqual(ResourceTypeIds.GameObject, weapon.ResourceTypeId);
                    Assert.AreEqual("test_package", weapon.ResourcePackageId);
                    Assert.IsNull(weapon.Prefab);
                }
            }
            finally
            {
                if (binder != null)
                    binder.ClearSpawnedWeapons();
                if (instance != null)
                    UnityEngine.Object.DestroyImmediate(instance);
            }
        }

        [Test]
        public void BuildPrefab_BindsBodyAlbedoThroughGeneratedMaterialTemplate()
        {
            EnsureTempRoot();
            string bodyPath = CreatePrefabAsset(AssetRoot + "/body.prefab");
            string katanaPath = CreatePrefabAsset(AssetRoot + "/katana.prefab");
            string shieldPath = CreatePrefabAsset(AssetRoot + "/kite_shield.prefab");
            string publicWeaponPath = CreatePrefabAsset(AssetRoot + "/common_weapon.prefab");
            string publicShieldPath = CreatePrefabAsset(AssetRoot + "/common_kite_shield.prefab");
            string texturePath = CreateTextureAsset(TextureRoot + "/body_albedo.png");
            WriteRuntimeEquipmentPackageFiles(bodyPath, katanaPath, shieldPath, texturePath);
            WriteGeometryBindingWithPublicDefaultWeapons();

            WithTemporaryGlobalProfile(GlobalWeaponProfileJson(publicWeaponPath, publicShieldPath), () =>
            {
                CharacterImportedPackagePrefabBuildResult result = CharacterImportedPackagePrefabBuilder.BuildPrefabWithReport(
                    TempRoot,
                    selectPrefab: false);

                Assert.AreEqual(
                    "Built",
                    result.Status,
                    "Prefab build failed. message=" + result.Message + " issues=" + string.Join(" | ", result.Issues ?? Array.Empty<string>()));
                Assert.AreEqual("bodyMaterialBound", result.BodyMaterialBindingStatus);
                Assert.AreEqual("wgame.character_traditional_lit", result.BodyMaterialTemplateId);
                Assert.AreEqual("char.test.texture.body_albedo", result.BodyMaterialTextureResourceKey);
                Assert.Greater(result.BodyMaterialRendererCount, 0);

                GameObject characterPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(result.PrefabPath);
                Assert.IsNotNull(characterPrefab);
                Material material = AssetDatabase.LoadAssetAtPath<Material>(TempRoot + "/materials/test_package_Body.mat");
                Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);
                Assert.IsNotNull(material);
                Assert.IsNotNull(texture);
                Assert.AreEqual("WGame/CharacterTraditionalLit", material.shader.name);
                Assert.AreSame(texture, material.GetTexture("_BaseMap"));
                Assert.IsFalse(material.HasProperty("_WgAlbedoStrength"));
                AssertBodyModelRenderersUseMaterial(characterPrefab, material);
                AssertSlimRuntimePrefab(characterPrefab, "test_package");
            });
        }

        [Test]
        public void PaletteMaterialBinding_V2ProfileIsRejectedForFaceDetailTextureMigration()
        {
            EnsureTempRoot();
            EnsureFolderPath(ConfigRoot);
            File.WriteAllText(
                ConfigRoot + "/character_palette_binding.json",
                "{\"format\":\"mx.characterPaletteBinding.v2\",\"schemaVersion\":\"2.0\",\"enabled\":true,\"atlasResourceKey\":\"wgame.character.palette.shared\",\"profileRow\":3,\"columnCount\":32,\"rowCount\":128,\"materialClassSource\":\"paletteAlpha\",\"weaponPolicy\":\"useCharacterPaletteProfile\",\"faceDetailEnabled\":true,\"faceDetailAtlasResourceKey\":\"wgame.character.face_detail.shared\",\"facePreset\":2,\"facePresetCount\":16,\"faceDetailMode\":\"directColorRgba\"}");
            AssetDatabase.ImportAsset(ConfigRoot + "/character_palette_binding.json", ImportAssetOptions.ForceUpdate);

            GameObject bodyModel = GameObject.CreatePrimitive(PrimitiveType.Cube);
            try
            {
                Type resourcePreviewType = BuilderType.GetNestedType("ResourcePreviewInfo", BindingFlags.NonPublic);
                Assert.IsNotNull(resourcePreviewType);
                Type dictionaryType = typeof(Dictionary<,>).MakeGenericType(typeof(string), resourcePreviewType);
                object resources = Activator.CreateInstance(dictionaryType);
                MethodInfo method = BuilderType.GetMethod(
                    "TryApplyPaletteBodyMaterialBinding",
                    BindingFlags.NonPublic | BindingFlags.Static);
                Assert.IsNotNull(method);
                var package = new CharacterImportedPackage(TempRoot, "test_package", null, null, null, null, null);
                object[] arguments = { package, resources, TempRoot, bodyModel.transform, false };

                TargetInvocationException exception = Assert.Throws<TargetInvocationException>(() => method.Invoke(null, arguments));
                Assert.IsTrue((bool)arguments[4]);
                Assert.IsInstanceOf<InvalidOperationException>(exception.InnerException);
                StringAssert.Contains("FaceDetailTextureMigrationRequired", exception.InnerException.Message);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(bodyModel);
            }
        }

        [Test]
        public void PaletteMaterialBinding_V4ColorProfileBindsValidatedUsageV2()
        {
            EnsureTempRoot();
            EnsureFolderPath(ConfigRoot);
            EnsureFolderPath(AssetRoot);
            const string MeshPath = AssetRoot + "/palette_contract_mesh.asset";
            File.WriteAllText(
                ConfigRoot + "/character_palette_binding.json",
                "{\"format\":\"mx.characterPaletteBinding.v5\",\"schemaVersion\":\"5.0\",\"enabled\":true,\"colorAtlasResourceKey\":\"wgame.character.color_palette.shared\",\"addressingMode\":\"colorUvSurfaceProfileVertexAlpha\",\"atlasWidth\":256,\"atlasHeight\":256,\"surfaceProfileSource\":\"paletteDataAlpha16\",\"surfaceProfileCount\":16,\"faceDetailEnabled\":false,\"cullMode\":\"Back\"}");

            GameObject bodyModel = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Mesh paletteMesh = UnityEngine.Object.Instantiate(bodyModel.GetComponent<MeshFilter>().sharedMesh);
            paletteMesh.name = "PaletteContractMesh";
            var paletteUv = new Vector2[paletteMesh.vertexCount];
            var colors = new Color32[paletteMesh.vertexCount];
            for (int vertexIndex = 0; vertexIndex < paletteMesh.vertexCount; vertexIndex++)
            {
                paletteUv[vertexIndex] = new Vector2(0.5f / 256f, 1.5f / 256f);
                colors[vertexIndex] = new Color32(255, 128, 0, 8);
            }
            paletteMesh.uv2 = paletteUv;
            paletteMesh.colors32 = colors;
            AssetDatabase.CreateAsset(paletteMesh, MeshPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.ImportAsset(MeshPath, ImportAssetOptions.ForceSynchronousImport);
            paletteMesh = AssetDatabase.LoadAssetAtPath<Mesh>(MeshPath);
            bodyModel.GetComponent<MeshFilter>().sharedMesh = paletteMesh;

            WGameSurfacePaletteCatalogDocument catalog = WGameSurfacePaletteCatalog.LoadAndValidate(out string catalogIssue);
            Assert.NotNull(catalog, catalogIssue);
            string modelHash = WGameSurfacePaletteCatalog.ComputeSha256(File.ReadAllBytes(Path.GetFullPath(MeshPath)));
            int faceCount = 0;
            for (int subMesh = 0; subMesh < paletteMesh.subMeshCount; subMesh++)
                faceCount += paletteMesh.GetIndices(subMesh).Length / 3;
            File.WriteAllText(
                ConfigRoot + "/character_palette_usage.json",
                "{\"format\":\"mx.characterPaletteUsage.v2\",\"schemaVersion\":\"2.0\",\"packageId\":\"test_package\",\"catalogHash\":\""
                    + catalog.CatalogHash
                    + "\",\"modelHash\":\""
                    + modelHash
                    + "\",\"paletteRepaintRequired\":false,\"invalidFaceCount\":0,\"tokenUsages\":[{\"tokenId\":\"wgame.surface.v3.r00.g08.b00.skin_soft\",\"logicalToken\":4096,\"colorIndex\":256,\"surfaceProfileId\":\"skin_soft\",\"faceCount\":"
                    + faceCount
                    + ",\"source\":\"character\"}]}" );
            AssetDatabase.ImportAsset(ConfigRoot + "/character_palette_binding.json", ImportAssetOptions.ForceUpdate);
            AssetDatabase.ImportAsset(ConfigRoot + "/character_palette_usage.json", ImportAssetOptions.ForceUpdate);

            try
            {
                Type resourcePreviewType = BuilderType.GetNestedType("ResourcePreviewInfo", BindingFlags.NonPublic);
                Type dictionaryType = typeof(Dictionary<,>).MakeGenericType(typeof(string), resourcePreviewType);
                object resources = Activator.CreateInstance(dictionaryType);
                MethodInfo method = BuilderType.GetMethod(
                    "TryApplyPaletteBodyMaterialBinding",
                    BindingFlags.NonPublic | BindingFlags.Static);
                var package = new CharacterImportedPackage(TempRoot, "test_package", null, null, null, null, null);
                object[] arguments = { package, resources, TempRoot, bodyModel.transform, false };

                object status = method.Invoke(null, arguments);
                Assert.IsTrue((bool)arguments[4]);
                Assert.AreEqual("bodyMaterialBound", ReadProperty<string>(status, "Status"));
                Material material = AssetDatabase.LoadAssetAtPath<Material>(ReadProperty<string>(status, "MaterialPath"));
                Assert.NotNull(material);
                Assert.IsFalse(material.HasProperty("_WgPaletteRow"));
                Assert.AreEqual(new Vector4(256f, 256f, 1f / 256f, 1f / 256f), material.GetVector("_WgColorPaletteParams"));
                Assert.AreSame(
                    AssetDatabase.LoadAssetAtPath<Texture2D>(WGameCharacterPaletteAssetGenerator.TexturePath),
                    material.GetTexture("_WgColorPaletteTex"));
                Assert.AreSame(material, bodyModel.GetComponent<Renderer>().sharedMaterial);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(bodyModel);
            }
        }

        [Test]
        public void BuildPrefab_MaterialDescriptorPathMatchesV2FinalAssetContract()
        {
            EnsureTempRoot();
            string bodyPath = CreatePrefabAsset(AssetRoot + "/body.prefab");
            string katanaPath = CreatePrefabAsset(AssetRoot + "/katana.prefab");
            string shieldPath = CreatePrefabAsset(AssetRoot + "/kite_shield.prefab");
            string texturePath = CreateTextureAsset(TextureRoot + "/body_albedo.png");
            AssignNamedSourceMaterial(bodyPath, "Source Body");
            WriteMaterialDescriptorPackageFiles(bodyPath, katanaPath, shieldPath, texturePath);
            string descriptorPath = WritePackageMaterialDescriptor(
                "Body",
                "Source Body.mat",
                "char.test.texture.body_albedo",
                "AlphaClip",
                0.42f,
                2450);
            AddMaterialDescriptorToUnityCatalog(
                "char.test.material.body",
                descriptorPath);

            CharacterImportedPackage package = CharacterImportedPackageJson.LoadFromDirectory(Path.GetFullPath(TempRoot));
            GameObject bodyPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(bodyPath);
            GameObject instance = PrefabUtility.InstantiatePrefab(bodyPrefab) as GameObject;
            try
            {
                Assert.IsNotNull(instance);
                MethodInfo bind = BuilderType.GetMethod(
                    "ApplyPackageMaterialDescriptorBindings",
                    BindingFlags.NonPublic | BindingFlags.Static);
                Assert.IsNotNull(bind);
                object status = bind.Invoke(null, new object[] { package, TempRoot, instance.transform });
                Assert.IsNotNull(status);
                PropertyInfo statusProperty = status.GetType().GetProperty(
                    "Status",
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                PropertyInfo materialPathProperty = status.GetType().GetProperty(
                    "MaterialPath",
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                Assert.IsNotNull(statusProperty);
                Assert.IsNotNull(materialPathProperty);
                Assert.AreEqual("bodyMaterialBound", statusProperty.GetValue(status));
                const string expectedMaterialPath = TempRoot + "/materials/test_package_Body.mat";
                Assert.AreEqual(expectedMaterialPath, materialPathProperty.GetValue(status));
                Assert.IsNull(
                    AssetDatabase.LoadAssetAtPath<Material>(TempRoot + "/materials/test_package_Source Body.mat"),
                    "The source material name is for renderer matching only and must not control the generated asset path.");

                Material material = AssetDatabase.LoadAssetAtPath<Material>(expectedMaterialPath);
                Assert.IsNotNull(material);
                Assert.IsTrue(material.IsKeywordEnabled("_ALPHATEST_ON"));
                Assert.AreEqual(1f, material.GetFloat("_AlphaClip"));
                Assert.AreEqual(0.42f, material.GetFloat("_Cutoff"), 0.0001f);
                Assert.AreEqual("TransparentCutout", material.GetTag("RenderType", searchFallbacks: false));
                Assert.AreEqual(2450, material.renderQueue);
                Renderer[] renderers = instance.GetComponentsInChildren<Renderer>(includeInactive: true);
                Assert.Greater(renderers.Length, 0);
                for (int rendererIndex = 0; rendererIndex < renderers.Length; rendererIndex++)
                {
                    Material[] materials = renderers[rendererIndex].sharedMaterials;
                    for (int materialIndex = 0; materialIndex < materials.Length; materialIndex++)
                        Assert.AreEqual(material, materials[materialIndex]);
                }
                string guid = AssetDatabase.AssetPathToGUID(expectedMaterialPath);
                byte[] meta = File.ReadAllBytes(expectedMaterialPath + ".meta");
                GameObject repeatedInstance = PrefabUtility.InstantiatePrefab(bodyPrefab) as GameObject;
                try
                {
                    object repeatedStatus = bind.Invoke(null, new object[] { package, TempRoot, repeatedInstance.transform });
                    Assert.AreEqual("bodyMaterialBound", statusProperty.GetValue(repeatedStatus));
                }
                finally
                {
                    if (repeatedInstance != null)
                        UnityEngine.Object.DestroyImmediate(repeatedInstance);
                }
                AssetDatabase.SaveAssets();
                Assert.AreEqual(guid, AssetDatabase.AssetPathToGUID(expectedMaterialPath));
                CollectionAssert.AreEqual(meta, File.ReadAllBytes(expectedMaterialPath + ".meta"));
                Assert.AreEqual(material, AssetDatabase.LoadAssetAtPath<Material>(expectedMaterialPath));
            }
            finally
            {
                if (instance != null)
                    UnityEngine.Object.DestroyImmediate(instance);
            }
        }

        [TestCase("Shared", "Shared")]
        [TestCase("Shared", "shared")]
        [TestCase("Shared.Detail", "Shared_Detail")]
        public void MaterialTargetCollision_BlocksBeforeWritingExistingAssets(string firstName, string secondName)
        {
            EnsureTempRoot();
            string bodyPath = CreatePrefabAsset(AssetRoot + "/body.prefab");
            string katanaPath = CreatePrefabAsset(AssetRoot + "/katana.prefab");
            string shieldPath = CreatePrefabAsset(AssetRoot + "/kite_shield.prefab");
            string texturePath = CreateTextureAsset(TextureRoot + "/body_albedo.png");
            AssignNamedSourceMaterial(bodyPath, "Source Body");
            WriteMaterialDescriptorPackageFiles(bodyPath, katanaPath, shieldPath, texturePath);
            string firstPath = WritePackageMaterialDescriptor("a/" + firstName, "Source Body.mat",
                "char.test.texture.body_albedo", tint: Color.red);
            AddMaterialDescriptorToUnityCatalog("char.test.material.probe_a", firstPath);
            var package = CharacterImportedPackageJson.LoadFromDirectory(Path.GetFullPath(TempRoot));
            GameObject instance = PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(bodyPath)) as GameObject;
            MethodInfo bind = BuilderType.GetMethod("ApplyPackageMaterialDescriptorBindings", BindingFlags.NonPublic | BindingFlags.Static);
            try
            {
                Assert.IsNotNull(instance);
                object baseline = bind.Invoke(null, new object[] { package, TempRoot, instance.transform });
                Type statusType = baseline.GetType();
                Assert.AreEqual("bodyMaterialBound", statusType.GetProperty("Status").GetValue(baseline));
                string materialPath = (string)statusType.GetProperty("MaterialPath").GetValue(baseline);
                Material material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
                Assert.AreEqual(Color.red, material.GetColor("_Color"));
                Assert.IsFalse(material.IsKeywordEnabled("_ALPHATEST_ON"));
                string guid = AssetDatabase.AssetPathToGUID(materialPath);

                var hair = new GameObject("Hair");
                hair.transform.SetParent(instance.transform, false);
                var sourceHair = new Material(material.shader) { name = "Source Hair" };
                AssetDatabase.CreateAsset(sourceHair, AssetRoot + "/source_hair.mat");
                hair.AddComponent<MeshRenderer>().sharedMaterial = sourceHair;
                Renderer[] renderers = instance.GetComponentsInChildren<Renderer>();
                Material[][] rendererMaterials = renderers.Select(r => r.sharedMaterials).ToArray();
                string secondPath = WritePackageMaterialDescriptor("b/" + secondName, "Source Hair.mat",
                    "char.test.texture.body_albedo", "AlphaClip", 0.17f, 2450, Color.blue);
                AddMaterialDescriptorToUnityCatalog("char.test.material.probe_b", secondPath);
                AssetDatabase.SaveAssets();

                string previousMessage = null;
                for (int attempt = 0; attempt < 2; attempt++)
                {
                    if (attempt == 1)
                    {
                        string catalogPath = ConfigRoot + "/unity_resource_catalog.json";
                        JObject catalog = JObject.Parse(File.ReadAllText(catalogPath));
                        catalog["entries"] = new JArray(((JArray)catalog["entries"]).Reverse());
                        File.WriteAllText(catalogPath, catalog.ToString(Formatting.Indented));
                        AssetDatabase.ImportAsset(catalogPath, ImportAssetOptions.ForceUpdate);
                    }
                    package = CharacterImportedPackageJson.LoadFromDirectory(Path.GetFullPath(TempRoot));
                    var bytesBefore = Directory.GetFiles(TempRoot, "*", SearchOption.AllDirectories)
                        .ToDictionary(path => path, File.ReadAllBytes);
                    object blocked = bind.Invoke(null, new object[] { package, TempRoot, instance.transform });
                    Assert.AreEqual(Color.red, material.GetColor("_Color"), "Collision preflight must run before modifying an existing material.");
                    Assert.AreEqual("packageMaterialDescriptorInvalid", statusType.GetProperty("Status").GetValue(blocked));
                    string message = (string)statusType.GetProperty("Message").GetValue(blocked);
                    StringAssert.Contains("CharacterGeneratedMaterialTargetCollision", message);
                    StringAssert.Contains("char.test.material.probe_a", message);
                    StringAssert.Contains("char.test.material.probe_b", message);
                    if (previousMessage != null)
                        Assert.AreEqual(previousMessage, message);
                    previousMessage = message;

                    CharacterImportedPackagePrefabBuildResult report = CharacterImportedPackagePrefabBuilder.BuildPrefabWithReport(TempRoot, false);
                    Assert.AreEqual("Failed", report.Status);
                    StringAssert.Contains("CharacterGeneratedMaterialTargetCollision", report.Message);
                    UnityEngine.TestTools.LogAssert.Expect(LogType.Error, "MxFramework Character Preview: " + message);
                    Assert.IsEmpty(CharacterImportedPackagePrefabBuilder.BuildPrefab(TempRoot, false));
                    AssetDatabase.SaveAssets();
                    string[] filesAfter = Directory.GetFiles(TempRoot, "*", SearchOption.AllDirectories);
                    CollectionAssert.AreEquivalent(bytesBefore.Keys, filesAfter, "Preflight must create no prefab, material or cache output.");
                    foreach (string file in filesAfter)
                        CollectionAssert.AreEqual(bytesBefore[file], File.ReadAllBytes(file), file);
                    Assert.AreEqual(guid, AssetDatabase.AssetPathToGUID(materialPath));
                    Assert.AreEqual(Color.red, material.GetColor("_Color"));
                    Assert.IsFalse(material.IsKeywordEnabled("_ALPHATEST_ON"));
                    for (int i = 0; i < renderers.Length; i++)
                        CollectionAssert.AreEqual(rendererMaterials[i], renderers[i].sharedMaterials);
                }
            }
            finally
            {
                if (instance != null)
                    UnityEngine.Object.DestroyImmediate(instance);
            }
        }

        [Test]
        public void BuildPrefab_PublicWeaponRefsUseGlobalResourceKeysWithoutGeneratedPackageWeaponPrefabs()
        {
            EnsureTempRoot();
            string bodyPath = CreatePrefabAsset(AssetRoot + "/body.prefab");
            string katanaPath = CreatePrefabAsset(AssetRoot + "/katana.prefab");
            string shieldPath = CreatePrefabAsset(AssetRoot + "/kite_shield.prefab");
            string publicWeaponPath = CreatePrefabAsset(AssetRoot + "/common_weapon.prefab");
            string publicShieldPath = CreatePrefabAsset(AssetRoot + "/common_kite_shield.prefab");
            WriteRuntimeEquipmentPackageFiles(bodyPath, katanaPath, shieldPath);
            WriteGeometryBindingWithPublicDefaultWeapons();
            CharacterImportedPackage package = CharacterImportedPackageJson.LoadFromDirectory(Path.GetFullPath(TempRoot));
            CharacterWeaponAttachmentRuntimeBinding parsedMainHand = FindAttachment(package.Geometry.WeaponAttachments, "mainHand");
            Assert.IsNotNull(parsedMainHand);
            Assert.AreEqual("weapon.common_weapon_1.model", parsedMainHand.RuntimeResourceKey);
            CharacterWeaponAttachmentRuntimeBinding parsedOffHand = FindAttachment(package.Geometry.WeaponAttachments, "offHand");
            Assert.IsNotNull(parsedOffHand);
            Assert.AreEqual("weapon.common_kite_shield.model", parsedOffHand.RuntimeResourceKey);
            CharacterRuntimeSpawnResult spawnResult = CharacterRuntimeSpawnResolver.Resolve(package, default);
            Assert.IsTrue(
                spawnResult.IsSuccess,
                "Runtime spawn resolve failed before prefab build: " + FormatSpawnIssues(spawnResult));
            const string staleMainHandPrefabPath = TempRoot + "/prefabs/weapons/test_package_mainHand_weapon_katana.prefab";
            const string staleOffHandPrefabPath = TempRoot + "/prefabs/weapons/test_package_offHand_weapon_kite_shield.prefab";
            AssetDatabase.DeleteAsset(staleMainHandPrefabPath);
            AssetDatabase.DeleteAsset(staleOffHandPrefabPath);
            Assert.IsNull(
                AssetDatabase.LoadAssetAtPath<GameObject>(staleMainHandPrefabPath),
                "Test setup should start without a stale package-local mainHand prefab.");
            Assert.IsNull(
                AssetDatabase.LoadAssetAtPath<GameObject>(staleOffHandPrefabPath),
                "Test setup should start without a stale package-local offHand prefab.");

            WithTemporaryGlobalProfile(GlobalWeaponProfileJson(publicWeaponPath, publicShieldPath), () =>
            {
                CharacterImportedPackagePrefabBuildResult result = CharacterImportedPackagePrefabBuilder.BuildPrefabWithReport(
                    TempRoot,
                    selectPrefab: false);
                string prefabPath = result.PrefabPath;

                Assert.IsFalse(
                    string.IsNullOrWhiteSpace(prefabPath),
                    "Prefab build failed. status=" + result.Status + " message=" + result.Message
                    + " issues=" + string.Join(" | ", result.Issues ?? Array.Empty<string>()));
                GameObject characterPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
                Assert.IsNotNull(characterPrefab);
                AssertSlimRuntimePrefab(characterPrefab, "test_package");

                GameObject instance = null;
                CharacterDefaultEquipmentRuntimeBinder binder = null;
                try
                {
                    CharacterRuntimePrefabCompositionResult composition = ComposePrefabForTest(
                        characterPrefab,
                        package,
                        resourceManager: null,
                        instantiateDefaultWeapons: false,
                        out instance);
                    Assert.IsTrue(composition.Success, string.Join(" | ", composition.Issues));
                    binder = composition.DefaultEquipmentBinder;
                    Assert.IsNotNull(binder);
                    Assert.AreEqual(2, binder.DefaultWeapons.Count);
                    AssertDefaultWeaponBinding(
                        binder,
                        "mainHand",
                        "weapon.common_weapon_1.model",
                        string.Empty,
                        "common");
                    AssertDefaultWeaponBinding(
                        binder,
                        "offHand",
                        "weapon.common_kite_shield.model",
                        string.Empty,
                        "common");
                }
                finally
                {
                    if (binder != null)
                        binder.ClearSpawnedWeapons();
                    if (instance != null)
                        UnityEngine.Object.DestroyImmediate(instance);
                }

                Assert.IsNull(
                    AssetDatabase.LoadAssetAtPath<GameObject>(staleMainHandPrefabPath),
                    "Public mainHand weaponRef must not generate or keep a package-local weapon prefab.");
                Assert.IsNull(
                    AssetDatabase.LoadAssetAtPath<GameObject>(staleOffHandPrefabPath),
                    "Public offHand weaponRef must not generate or keep a package-local weapon prefab.");
            });
        }

        [Test]
        public void BuildPrefab_CodexFlowTestUsesPublicDefaultWeaponResources()
        {
            const string packageRoot = "Assets/MxFrameworkGenerated/CharacterPackages/codex_flow_test";
            if (!Directory.Exists(packageRoot))
                Assert.Ignore("codex_flow_test generated package is not present.");

            InvokeCommonWeaponWrapperPrefabSync();
            CharacterImportedPackagePrefabBuildResult result = CharacterImportedPackagePrefabBuilder.BuildPrefabWithReport(
                packageRoot,
                selectPrefab: false);

            Assert.AreEqual(
                "Built",
                result.Status,
                "Prefab build failed. message=" + result.Message + " issues=" + string.Join(" | ", result.Issues ?? Array.Empty<string>()));
            Assert.IsFalse(string.IsNullOrWhiteSpace(result.PrefabPath));

            GameObject characterPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(result.PrefabPath);
            Assert.IsNotNull(characterPrefab);
            AssertSlimRuntimePrefab(characterPrefab, "codex_flow_test");
            AssertGeneratedBodyMaterialBinding(
                characterPrefab,
                result,
                packageRoot,
                "codex_flow_test",
                expectBinding: true);
            CollectionAssert.IsSubsetOf(
                new[] { "weapon.common_weapon_1.model" },
                result.DefaultWeaponResourceIds);

            CharacterImportedPackage package = CharacterImportedPackageJson.LoadFromDirectory(packageRoot);
            GameObject instance = null;
            CharacterDefaultEquipmentRuntimeBinder binder = null;
            try
            {
                CharacterRuntimePrefabCompositionResult composition = ComposePrefabForTest(
                    characterPrefab,
                    package,
                    resourceManager: null,
                    instantiateDefaultWeapons: false,
                    out instance);
                Assert.IsTrue(composition.Success, string.Join(" | ", composition.Issues));
                binder = composition.DefaultEquipmentBinder;
                Assert.IsNotNull(binder);
                AssertDefaultWeaponBinding(
                    binder,
                    "mainHand",
                    "weapon.common_weapon_1.model",
                    string.Empty,
                    "common");
                Assert.IsNull(FindDefaultWeapon(binder, "offHand"),
                    "The current codex_flow_test setup leaves offHand empty and must not create a default binding.");
            }
            finally
            {
                if (binder != null)
                    binder.ClearSpawnedWeapons();
                if (instance != null)
                    UnityEngine.Object.DestroyImmediate(instance);
            }
            Assert.IsNull(AssetDatabase.LoadAssetAtPath<GameObject>(
                packageRoot + "/prefabs/weapons/codex_flow_test_mainHand_weapon_main_hand_katana.prefab"));
            Assert.IsNull(AssetDatabase.LoadAssetAtPath<GameObject>(
                packageRoot + "/runtime_resources/Resources/MxFrameworkGenerated/CharacterPackages/codex_flow_test/prefabs/weapons/codex_flow_test_mainHand_weapon_main_hand_katana.prefab"));
            Assert.IsNull(AssetDatabase.LoadAssetAtPath<GameObject>(
                packageRoot + "/prefabs/weapons/codex_flow_test_offHand_weapon_off_hand_kite_shield.prefab"));
            Assert.IsNull(AssetDatabase.LoadAssetAtPath<GameObject>(
                packageRoot + "/runtime_resources/Resources/MxFrameworkGenerated/CharacterPackages/codex_flow_test/prefabs/weapons/codex_flow_test_offHand_weapon_off_hand_kite_shield.prefab"));
            Assert.IsNotNull(AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/MxFrameworkGenerated/Common/Weapons/common_weapon_1/prefabs/common_weapon_1_weapon.prefab"));
            AssertGeneratedCommonWeaponMaterial("common_weapon_1", "weaponMaterialBound");
        }

        [Test]
        public void BuildPrefab_GeneratedPackagesUseAuthoringColliderMarkersWithoutUnityColliders()
        {
            string[] packageRoots =
            {
                "Assets/MxFrameworkGenerated/CharacterPackages/codex_flow_test",
                "Assets/MxFrameworkGenerated/CharacterPackages/codex_flow_test",
                "Assets/MxFrameworkGenerated/CharacterPackages/codex_flow_test",
            };

            InvokeCommonWeaponWrapperPrefabSync();
            foreach (string packageRoot in packageRoots)
            {
                Assert.IsTrue(Directory.Exists(packageRoot), packageRoot + " generated package is not present.");

                CharacterImportedPackagePrefabBuildResult result = CharacterImportedPackagePrefabBuilder.BuildPrefabWithReport(
                    packageRoot,
                    selectPrefab: false);
                Assert.AreEqual(
                    "Built",
                    result.Status,
                    "Prefab build failed for " + packageRoot + ". message=" + result.Message + " issues=" + string.Join(" | ", result.Issues ?? Array.Empty<string>()));
                Assert.IsFalse(string.IsNullOrWhiteSpace(result.PrefabPath));

                CharacterImportedPackage package = CharacterImportedPackageJson.LoadFromDirectory(packageRoot);
                GameObject characterPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(result.PrefabPath);
                Assert.IsNotNull(characterPrefab, result.PrefabPath);
                AssertSlimRuntimePrefab(characterPrefab, package.PackageId);
                AssertGeneratedContactPointMarkers(characterPrefab, package);
                AssertGeneratedBodyMaterialBinding(
                    characterPrefab,
                    result,
                    packageRoot,
                    package.PackageId,
                    AssetDatabase.LoadAssetAtPath<Texture2D>(packageRoot + "/resources/textures/body_albedo.png") != null);
            }
        }

        [Test]
        public void BuildPrefab_CodexFlowTestWritesSampledWeaponTraceBakeCatalog()
        {
            const string packageRoot = "Assets/MxFrameworkGenerated/CharacterPackages/codex_flow_test";
            if (!Directory.Exists(packageRoot))
                Assert.Ignore("codex_flow_test generated package is not present.");

            CharacterWeaponTraceBakeBuildReport report =
                CharacterImportedPackagePrefabBuilder.GenerateWeaponTraceBakeCatalog(packageRoot);

            Assert.IsTrue(
                report.Success,
                "Weapon trace bake failed: " + string.Join(
                    " | ",
                    report.Issues.Select(issue => issue.Code + ":" + issue.SourcePath + ":" + issue.SourceObjectPath + ":" + issue.Message)));
            Assert.GreaterOrEqual(report.ArtifactCount, 3);

            string catalogPath = packageRoot + "/" + CharacterWeaponTraceBakeCatalogJson.DefaultRelativePath;
            Assert.IsTrue(File.Exists(catalogPath), "Sampled weapon trace bake catalog should be generated.");
            JObject root = LoadWeaponBakeDiagnosticJson(packageRoot);
            JArray artifacts = root["artifacts"] as JArray;
            Assert.NotNull(artifacts);
            Assert.GreaterOrEqual(artifacts.Count, 3);
            JObject first = artifacts[0] as JObject;
            Assert.NotNull(first);
            StringAssert.Contains(
                "sampledWeaponLocators",
                first["profile"]?["importSettingsFingerprint"]?.Value<string>() ?? string.Empty);
            JArray frames = first["frames"] as JArray;
            Assert.NotNull(frames);
            JObject activeFrame = frames.FirstOrDefault(token => token["localFrame"]?.Value<int>() == 23) as JObject;
            Assert.NotNull(activeFrame);
            JArray rootNow = activeFrame["rootNow"] as JArray;
            JArray tipNow = activeFrame["tipNow"] as JArray;
            Assert.NotNull(rootNow);
            Assert.NotNull(tipNow);
            Assert.AreNotEqual(0L, rootNow[1].Value<long>(), "Sampled root should be character-local, not socket-local zero.");
            Assert.AreNotEqual(rootNow[1].Value<long>(), tipNow[1].Value<long>());

            JObject socketArtifact = artifacts
                .OfType<JObject>()
                .FirstOrDefault(artifact => (artifact["socketFrames"] as JArray)?.Any(
                    frame => string.Equals(frame["socketId"]?.Value<string>(), "foot.L", StringComparison.Ordinal)) == true);
            Assert.NotNull(socketArtifact, "BakedSocket contact point motion should emit foot socket frames.");
            JArray socketFrames = socketArtifact["socketFrames"] as JArray;
            Assert.NotNull(socketFrames);
            Assert.IsTrue(
                socketFrames.Any(frame => string.Equals(frame["socketId"]?.Value<string>(), "foot.R", StringComparison.Ordinal)),
                "Both foot contact sockets should be baked.");
            Assert.IsTrue(
                socketFrames
                    .OfType<JObject>()
                    .Where(frame => string.Equals(frame["socketId"]?.Value<string>(), "foot.L", StringComparison.Ordinal))
                    .All(frame => IsAnimatedContactSocketPath(frame["socketPath"]?.Value<string>())),
                "Foot contact socket frames must be sampled from animated bone or humanoid clip curves, not static authoring markers.");
            AssertSocketFramesVaryOnYAxis(artifacts, "art.character.skeleton.animation.standing_walk_forward", "foot.L");
            AssertSocketFramesVaryOnYAxis(artifacts, "art.character.skeleton.animation.standing_walk_forward", "foot.R");
            AssertSocketFramesVaryOnYAxis(artifacts, "art.character.skeleton.animation.standing_run_forward", "foot.L");
            AssertSocketFramesVaryOnYAxis(artifacts, "art.character.skeleton.animation.standing_run_forward", "foot.R");
            CharacterWeaponTraceBakeCatalog loadedCatalog = CharacterWeaponTraceBakeStorage.Load(
                packageRoot, CharacterImportedPackageJson.LoadFromDirectory(packageRoot).PackageId, true,
                out CharacterWeaponTraceBakeBuildReport loadReport);
            Assert.IsTrue(loadReport.Success, string.Join(", ", loadReport.Issues.Select(issue => issue.Code)));
            Assert.IsTrue(
                loadedCatalog.TryGetSocketFrame(
                    socketArtifact["clipKey"]?.Value<string>(),
                    socketArtifact["traceId"]?.Value<int>() ?? 0,
                    MxFramework.Game.CharacterTest.CharacterTestAuthoredContentIds.D400_Id50,
                    0,
                    out MxAnimationBakedSocketFrame footFrame),
                "Baked foot socket frame should survive JSON load.");
            Assert.AreEqual(MxFramework.Game.CharacterTest.CharacterTestAuthoredContentIds.D400_Id50, footFrame.SocketId);
        }

        [Test]
        public void BuildPrefab_CodexFlowTestPublicWeaponTraceBakeUsesSpawnedWrapper()
        {
            const string packageRoot = "Assets/MxFrameworkGenerated/CharacterPackages/codex_flow_test";
            if (!Directory.Exists(packageRoot))
                Assert.Ignore("codex_flow_test generated package is not present.");

            CharacterWeaponTraceBakeBuildReport report =
                CharacterImportedPackagePrefabBuilder.GenerateWeaponTraceBakeCatalog(packageRoot);

            Assert.IsTrue(
                report.Success,
                "Weapon trace bake failed: " + string.Join(
                    " | ",
                    report.Issues.Select(issue => issue.Code + ":" + issue.SourcePath + ":" + issue.SourceObjectPath + ":" + issue.Message)));
            Assert.GreaterOrEqual(report.ArtifactCount, 1);

            string catalogPath = packageRoot + "/" + CharacterWeaponTraceBakeCatalogJson.DefaultRelativePath;
            Assert.IsTrue(File.Exists(catalogPath), "Default CharacterTest package should have a sampled weapon trace bake catalog.");
            JObject root = LoadWeaponBakeDiagnosticJson(packageRoot);
            JArray artifacts = root["artifacts"] as JArray;
            Assert.NotNull(artifacts);
            Assert.GreaterOrEqual(artifacts.Count, 1);
            JObject first = artifacts[0] as JObject;
            Assert.NotNull(first);
            JArray frames = first["frames"] as JArray;
            Assert.NotNull(frames);
            AssertTraceFramesVary(frames, "Public weapon trace bake should follow animated sockets.");
            JObject activeFrame = frames.FirstOrDefault(token => token["localFrame"]?.Value<int>() == 23) as JObject;
            Assert.NotNull(activeFrame);
            JArray rootNow = activeFrame["rootNow"] as JArray;
            JArray tipNow = activeFrame["tipNow"] as JArray;
            Assert.NotNull(rootNow);
            Assert.NotNull(tipNow);
            Assert.AreNotEqual(
                -46439L,
                rootNow[2].Value<long>(),
                "Public weapon trace bake should sample the spawned wrapper prefab, not the old socket fallback root.");
            Assert.AreNotEqual(
                1024593L,
                tipNow[2].Value<long>(),
                "Public weapon trace bake should sample the spawned wrapper prefab, not the old socket fallback tip.");
            Assert.AreNotEqual(rootNow[2].Value<long>(), tipNow[2].Value<long>());

            CharacterWeaponTraceBakeCatalog catalog = CharacterWeaponTraceBakeStorage.Load(
                packageRoot, CharacterImportedPackageJson.LoadFromDirectory(packageRoot).PackageId, false,
                out CharacterWeaponTraceBakeBuildReport loadReport);
            Assert.IsTrue(loadReport.Success, string.Join(", ", loadReport.Issues.Select(issue => issue.Code)));
            Assert.IsTrue(
                catalog.TryGetFrame(
                    "art.character.skeleton.animation.katana_atk1",
                    MxFramework.Game.CharacterTest.CharacterTestAuthoredContentIds.D403_Id554678986,
                    23,
                    out MxAnimationBakedWeaponTraceFrame bakedFrame),
                "katana_atk1 frame 23 should be present in the generated weapon trace bake catalog.");
            AssertWeaponTraceFramesPresent(
                catalog,
                "art.character.skeleton.animation.katana_atk1",
                MxFramework.Game.CharacterTest.CharacterTestAuthoredContentIds.D403_Id554678986,
                23,
                27);
            AssertWeaponTraceFramesPresent(
                catalog,
                "art.character.skeleton.animation.katana_atk2",
                MxFramework.Game.CharacterTest.CharacterTestAuthoredContentIds.D403_Id554678986,
                26,
                30);

            CharacterImportedPackage package = CharacterImportedPackageJson.LoadFromDirectory(packageRoot);
            CharacterWeaponTraceRuntimeBinding trace = package.Geometry.WeaponTraces.Single(
                candidate => candidate.TraceId == MxFramework.Game.CharacterTest.CharacterTestAuthoredContentIds.D403_Id554678986);
            AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(
                "Assets/Art/MxFramework/Samples/Characters/Skeleton/AnimationClips/katana_atk1.anim");
            Assert.NotNull(clip);
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                CharacterRuntimePrefabResourceCatalog.GetGeneratedCharacterPrefabPath(packageRoot, package.PackageId));
            Assert.NotNull(prefab);

            GameObject instance = PrefabUtility.InstantiatePrefab(prefab) as GameObject;
            bool startedAnimationMode = false;
            CharacterDefaultEquipmentRuntimeBinder binder = null;
            try
            {
                Assert.NotNull(instance);
                instance.hideFlags = HideFlags.HideAndDontSave;
                instance.SetActive(true);

                ResourceManager resourceManager = CreateCodexFlowTestPublicWeaponResourceManager();
                CharacterRuntimeSpawnResult spawnResult = CharacterRuntimeSpawnResolver.Resolve(package, default);
                Assert.IsTrue(spawnResult.IsSuccess, FormatSpawnIssues(spawnResult));
                CharacterRuntimePrefabCompositionResult composition = CharacterRuntimePrefabComposer.Compose(
                    instance,
                    new CharacterRuntimePrefabCompositionOptions(package, spawnResult)
                    {
                        ResourceManager = resourceManager,
                        InstantiateDefaultWeapons = false,
                        InstallInputMotionController = false,
                        EnableInputMotion = false,
                        UseExternalRuntimeTick = true
                    });
                Assert.IsTrue(composition.Success, string.Join(" | ", composition.Issues));
                binder = composition.DefaultEquipmentBinder;
                Assert.NotNull(binder);
                binder.ConfigureResourceManager(resourceManager, instantiateDefaultWeapons: false);
                binder.ClearSpawnedWeapons();
                binder.InstantiateDefaultWeapons(resourceManager);
                InvokeEnsureSpawnedWeaponTraceLocators(package, binder);
                Assert.GreaterOrEqual(binder.SpawnedWeapons.Count, 1);

                if (!AnimationMode.InAnimationMode())
                {
                    AnimationMode.StartAnimationMode();
                    startedAnimationMode = true;
                }

                InvokeSampleAnimationClipForWeaponTraceBake(instance, clip, 23f / 60f);
                CharacterBoneRuntimeSync boneSync = composition.BoneSync;
                Assert.NotNull(boneSync);
                boneSync.ApplyBindings();

                GameObject weapon = binder.SpawnedWeapons.FirstOrDefault(
                    spawned => spawned != null && spawned.name.Contains("mainHand"));
                Assert.NotNull(weapon);
                Transform rootLocator = FindDescendant(weapon.transform, "blade_root");
                Transform tipLocator = FindDescendant(weapon.transform, "blade_tip");
                Assert.NotNull(rootLocator);
                Assert.NotNull(tipLocator);
                Vector3 expectedRoot = instance.transform.InverseTransformPoint(rootLocator.position);
                Vector3 expectedTip = instance.transform.InverseTransformPoint(tipLocator.position);

                AssertBakedVectorApproximately(expectedRoot, bakedFrame.RootNow, "rootNow");
                AssertBakedVectorApproximately(expectedTip, bakedFrame.TipNow, "tipNow");
            }
            finally
            {
                if (startedAnimationMode)
                    AnimationMode.StopAnimationMode();
                if (binder != null)
                    binder.ClearSpawnedWeapons();
                if (instance != null)
                    UnityEngine.Object.DestroyImmediate(instance);
            }
        }

        [Test]
        public void WeaponTraceBakeRegistry_MergesPackageAndCommonActionLibraries()
        {
            const string packageRoot = "Assets/MxFrameworkGenerated/CharacterPackages/codex_flow_test";
            if (!Directory.Exists(packageRoot))
                Assert.Ignore("codex_flow_test generated package is not present.");

            CharacterImportedPackage package = CharacterImportedPackageJson.LoadFromDirectory(packageRoot);
            MethodInfo method = BuilderType.GetMethod(
                "TryLoadCharacterActionRegistryForPackage",
                BindingFlags.NonPublic | BindingFlags.Static);
            Assert.NotNull(method);
            object[] arguments = { package, null, null };

            bool success = (bool)method.Invoke(null, arguments);

            Assert.IsTrue(success, arguments[2] as string);
            CharacterActionConfigRegistry registry = arguments[1] as CharacterActionConfigRegistry;
            Assert.NotNull(registry);
            Assert.IsTrue(registry.TryGetAction(new MxFramework.CharacterAction.CharacterActionId(930100), out _));
            Assert.IsTrue(registry.TryGetAction(new MxFramework.CharacterAction.CharacterActionId(301), out _));
            Assert.IsTrue(registry.TryGetAction(new MxFramework.CharacterAction.CharacterActionId(324), out _));
        }

        private static JObject LoadWeaponBakeDiagnosticJson(string packageRoot)
        {
            var package = CharacterImportedPackageJson.LoadFromDirectory(packageRoot);
            var weapons = CharacterWeaponTraceBakeStorage.Load(packageRoot, package.PackageId, false, out var weaponReport);
            var contacts = CharacterWeaponTraceBakeStorage.Load(packageRoot, package.PackageId, true, out var contactReport);
            Assert.IsTrue(weaponReport.Success); Assert.IsTrue(contactReport.Success);
            var combined = new CharacterWeaponTraceBakeCatalog();
            var requests = new List<CharacterWeaponTraceBakeRequest>();
            foreach (var pair in weapons.EnumerateArtifacts())
            {
                contacts.TryGetArtifact(pair.Key.ClipKey, pair.Key.TraceId, out var contact);
                combined.RegisterArtifact(pair.Key.ClipKey, pair.Key.TraceId,
                    new MxAnimationBakeArtifact(pair.Value.Profile, pair.Value.WeaponTraceFrames, socketFrames: contact?.SocketFrames));
                requests.Add(new CharacterWeaponTraceBakeRequest(CharacterActionId.None, pair.Key.ClipKey, pair.Key.TraceId, 0, 0, 0));
            }
            foreach (var pair in contacts.EnumerateArtifacts())
            {
                if (combined.TryGetArtifact(pair.Key.ClipKey, pair.Key.TraceId, out _)) continue;
                combined.RegisterArtifact(pair.Key.ClipKey, pair.Key.TraceId, pair.Value);
                requests.Add(new CharacterWeaponTraceBakeRequest(CharacterActionId.None, pair.Key.ClipKey, pair.Key.TraceId, 0, 0, 0));
            }
            return JObject.Parse(CharacterWeaponTraceBakeCatalogJson.Serialize(package, requests, combined));
        }

        private static void AssertWeaponTraceFramesPresent(
            CharacterWeaponTraceBakeCatalog catalog,
            string clipKey,
            int traceId,
            int startFrame,
            int endFrame)
        {
            for (int frame = startFrame; frame <= endFrame; frame++)
            {
                Assert.IsTrue(
                    catalog.TryGetFrame(clipKey, traceId, frame, out _),
                    clipKey + " must contain weapon trace frame " + frame + ".");
            }
        }

        [Test]
        public void CreateModelWrapper_PreservesImportedModelRootScale()
        {
            EnsureTempRoot();
            string bodyPath = CreatePrefabAsset(AssetRoot + "/body_scaled.prefab", new Vector3(0.01f, 0.01f, 0.01f));
            object resource = CreateResourcePreviewInfo("char.test.model.body", bodyPath);
            var parent = new GameObject("ModelRoot");
            try
            {
                Transform bodyModel = InvokeCreateModelWrapper("BodyModel", resource, parent.transform);
                Assert.IsNotNull(bodyModel);
                Assert.Greater(bodyModel.childCount, 0);

                Transform importedRoot = bodyModel.GetChild(0);
                Assert.That(importedRoot.localScale.x, Is.EqualTo(0.01f).Within(0.0001f));
                Assert.That(importedRoot.localScale.y, Is.EqualTo(0.01f).Within(0.0001f));
                Assert.That(importedRoot.localScale.z, Is.EqualTo(0.01f).Within(0.0001f));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(parent);
            }
        }

        [Test]
        public void CreateModelWrapper_CompensatesLargeImportedRootScaleForSubUnitWrapper()
        {
            EnsureTempRoot();
            string weaponPath = CreatePrefabAsset(AssetRoot + "/weapon_unit_scaled.prefab", new Vector3(100f, 100f, 100f));
            object resource = CreateResourcePreviewInfo(
                "weapon.common_weapon_1.model",
                weaponPath,
                "weaponModel",
                new Vector3(0.01f, 0.01f, 0.01f));
            var parent = new GameObject("WeaponRoot");
            try
            {
                Transform wrapper = InvokeCreateModelWrapper("Model", resource, parent.transform);
                Assert.IsNotNull(wrapper);
                Assert.That(wrapper.localScale.x, Is.EqualTo(1f).Within(0.0001f));
                Assert.That(wrapper.localScale.y, Is.EqualTo(1f).Within(0.0001f));
                Assert.That(wrapper.localScale.z, Is.EqualTo(1f).Within(0.0001f));
                Assert.Greater(wrapper.childCount, 0);

                Transform importedRoot = wrapper.GetChild(0);
                Assert.That(importedRoot.localScale.x, Is.EqualTo(100f).Within(0.0001f));
                Assert.That(importedRoot.localScale.y, Is.EqualTo(100f).Within(0.0001f));
                Assert.That(importedRoot.localScale.z, Is.EqualTo(100f).Within(0.0001f));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(parent);
            }
        }

        [Test]
        public void CreateModelWrapper_DoesNotDoubleApplyBakedPublicWeaponWrapper()
        {
            EnsureTempRoot();
            string weaponPath = CreatePrefabAsset(AssetRoot + "/weapon_wrapper_prefab.prefab", new Vector3(1f, 1f, 1f));
            object resource = CreateResourcePreviewInfo(
                "weapon.common_weapon_1.model",
                weaponPath,
                "weaponModel",
                new Vector3(0.01f, 0.01f, 0.01f),
                wrapperPoseBakedIntoAsset: true);
            var parent = new GameObject("WeaponRoot");
            try
            {
                Transform wrapper = InvokeCreateModelWrapper("Model", resource, parent.transform);
                Assert.IsNotNull(wrapper);
                Assert.That(wrapper.localScale.x, Is.EqualTo(1f).Within(0.0001f));
                Assert.That(wrapper.localScale.y, Is.EqualTo(1f).Within(0.0001f));
                Assert.That(wrapper.localScale.z, Is.EqualTo(1f).Within(0.0001f));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(parent);
            }
        }

        [Test]
        public void ShouldPreserveNestedPrefabInstance_OnlyForPrefabAssets()
        {
            Assert.IsTrue(InvokeShouldPreserveNestedPrefabInstance("Assets/Characters/body.prefab"));
            Assert.IsTrue(InvokeShouldPreserveNestedPrefabInstance("Assets/Characters/BODY.PREFAB"));
            Assert.IsFalse(InvokeShouldPreserveNestedPrefabInstance("Assets/Characters/body.glb"));
            Assert.IsFalse(InvokeShouldPreserveNestedPrefabInstance("Assets/Characters/body.gltf"));
            Assert.IsFalse(InvokeShouldPreserveNestedPrefabInstance("Assets/Characters/body.fbx"));
        }

        [Test]
        public void FindHumanoidAvatar_DoesNotFallbackToUnrelatedProjectAvatar()
        {
            EnsureTempRoot();

            object avatar = InvokeFindHumanoidAvatar(AssetRoot + "/missing_body.fbx", TempRoot);

            Assert.IsNull(avatar);
        }

        [Test]
        public void CharacterBoneRuntimeSync_TreatsLocalPositionAsMetersWhenBoneHasImportScale()
        {
            var root = new GameObject("RuntimeSyncScaleTest");
            try
            {
                Transform bone = new GameObject("bone.hand.R").transform;
                bone.SetParent(root.transform, false);
                bone.localPosition = new Vector3(0.5f, 1.4f, -0.05f);
                bone.localRotation = Quaternion.Euler(0f, 90f, 0f);
                bone.localScale = new Vector3(100f, 100f, 100f);

                Transform socket = new GameObject("Socket_mainHand").transform;
                socket.SetParent(root.transform, false);

                CharacterBoneRuntimeSync sync = root.AddComponent<CharacterBoneRuntimeSync>();
                var serialized = new SerializedObject(sync);
                SerializedProperty bindings = serialized.FindProperty("_bindings");
                bindings.arraySize = 1;
                SerializedProperty binding = bindings.GetArrayElementAtIndex(0);
                binding.FindPropertyRelative("_bindingId").stringValue = "socket:mainHand";
                binding.FindPropertyRelative("_bonePath").stringValue = "bone.hand.R";
                binding.FindPropertyRelative("_target").objectReferenceValue = socket;
                binding.FindPropertyRelative("_bone").objectReferenceValue = bone;
                binding.FindPropertyRelative("_localPosition").vector3Value = new Vector3(0f, 0.06f, 0f);
                binding.FindPropertyRelative("_localRotation").quaternionValue = Quaternion.identity;
                binding.FindPropertyRelative("_localScale").vector3Value = Vector3.one;
                serialized.ApplyModifiedPropertiesWithoutUndo();

                sync.ApplyBindings();

                Vector3 expectedSocketPosition = bone.position + (bone.rotation * new Vector3(0f, 0.06f, 0f));
                AssertVectorApproximately(expectedSocketPosition, socket.position);
                Assert.Less(Vector3.Distance(socket.position, bone.position), 0.1f);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void AddAnimationRuntimeResources_IncludesDirectionalRunClipsFromRegistry()
        {
            EnsureTempRoot();
            EnsureFolderPath(ConfigRoot);
            File.WriteAllText(
                ConfigRoot + "/animation_clip_registry.json",
                "{\n"
                + "  \"format\": \"mx.animationClipRegistry.v1\",\n"
                + "  \"schemaVersion\": \"1.0\",\n"
                + "  \"packageId\": \"animation.codex_flow_test\",\n"
                + "  \"clips\": [\n"
                + "    {\n"
                + "      \"setId\": \"set.base\",\n"
                + "      \"groupId\": \"group.locomotion\",\n"
                + "      \"clipId\": \"run.r\",\n"
                + "      \"sourceClipName\": \"standing_run_right\",\n"
                + "      \"runtimeResourceKey\": \"art.character.skeleton.animation.standing_run_right\"\n"
                + "    },\n"
                + "    {\n"
                + "      \"setId\": \"set.base\",\n"
                + "      \"groupId\": \"group.locomotion\",\n"
                + "      \"clipId\": \"run.l\",\n"
                + "      \"sourceClipName\": \"standing_run_left\",\n"
                + "      \"runtimeResourceKey\": \"art.character.skeleton.animation.standing_run_left\"\n"
                + "    },\n"
                + "    {\n"
                + "      \"setId\": \"set.base\",\n"
                + "      \"groupId\": \"group.locomotion\",\n"
                + "      \"clipId\": \"run.b\",\n"
                + "      \"sourceClipName\": \"standing_run_back\",\n"
                + "      \"runtimeResourceKey\": \"art.character.skeleton.animation.standing_run_back\"\n"
                + "    }\n"
                + "  ]\n"
                + "}\n");
            AssetDatabase.ImportAsset(ConfigRoot + "/animation_clip_registry.json", ImportAssetOptions.ForceUpdate);

            MethodInfo method = BuilderType.GetMethod("AddAnimationRuntimeResources", BindingFlags.NonPublic | BindingFlags.Static);
            Assert.IsNotNull(method);

            Type entryType = BuilderType.GetNestedType("RuntimeResourceBootstrapEntry", BindingFlags.NonPublic);
            Assert.IsNotNull(entryType);
            IList runtimeResources = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(entryType));
            var runtimeResourceIds = new HashSet<string>(StringComparer.Ordinal);

            const string sharedPackageId = "shared.animation.package";
            var sharedEntries = new[]
            {
                new ResourceCatalogEntry(
                    "art.character.skeleton.animation.standing_run_right",
                    ResourceTypeIds.AnimationClip,
                    "memory",
                    "standing_run_right",
                    packageId: sharedPackageId),
                new ResourceCatalogEntry(
                    "art.character.skeleton.animation.standing_run_left",
                    ResourceTypeIds.AnimationClip,
                    "memory",
                    "standing_run_left",
                    packageId: sharedPackageId),
                new ResourceCatalogEntry(
                    "art.character.skeleton.animation.standing_run_back",
                    ResourceTypeIds.AnimationClip,
                    "memory",
                    "standing_run_back",
                    packageId: sharedPackageId)
            };
            var sharedCatalog = new ResourceCatalog(
                "character.runtime.shared-animation-test",
                "test_package",
                sharedEntries);
            var package = new CharacterImportedPackage(
                Path.GetFullPath(TempRoot),
                "test_package",
                CharacterImportedConfigSet.Empty,
                CharacterImportedGeometryBinding.Empty,
                CharacterImportedResourceMapping.Empty,
                sharedCatalog,
                null,
                sharedCatalog);

            method.Invoke(null, new object[] { TempRoot, package, runtimeResources, runtimeResourceIds });

            CollectionAssert.AreEquivalent(
                new[]
                {
                    "art.character.skeleton.animation.standing_run_right",
                    "art.character.skeleton.animation.standing_run_left",
                    "art.character.skeleton.animation.standing_run_back"
                },
                ReadEntryIds(runtimeResources));
            CollectionAssert.AreEqual(
                new[] { sharedPackageId, sharedPackageId, sharedPackageId },
                ReadEntryPackageIds(runtimeResources));
        }

        [Test]
        public void ConfigureRuntimeResourceBootstrap_UsesImportedPackageRootForArtifactPathsAndAddresses()
        {
            EnsureTempRoot();
            string prefabPath = CreatePrefabAsset(TempRoot + "/prefabs/test_package_character_preview.prefab");
            string clipPath = CreateAnimationClipAsset(AssetRoot + "/standing_run_right.anim", "standing_run_right");
            WriteAnimationSetDefinition();
            WriteAnimationClipRegistry(new AnimationRegistryEntry(
                "set.base",
                "group.locomotion",
                "run.r",
                "standing_run_right",
                "art.character.skeleton.animation.standing_run_right",
                clipPath));

            CharacterImportedPackage package = CreatePackage();
            var owner = new GameObject("bootstrap");
            try
            {
                CharacterRuntimeResourceBootstrap bootstrap = owner.AddComponent<CharacterRuntimeResourceBootstrap>();
                InvokeConfigureRuntimeResourceBootstrap(bootstrap, TempRoot, package, prefabPath);

                var serialized = new SerializedObject(bootstrap);
                Assert.AreEqual(ConfigRoot + "/animation_set_definition.json", serialized.FindProperty("_animationSetDefinitionJsonPath").stringValue);
                Assert.AreEqual(ConfigRoot + "/animation_clip_registry.json", serialized.FindProperty("_animationClipRegistryPath").stringValue);
                Assert.IsNotEmpty(serialized.FindProperty("_animationSetDefinitionContentHash").stringValue);
                Assert.IsNotEmpty(serialized.FindProperty("_animationClipRegistryContentHash").stringValue);

                SerializedProperty resources = serialized.FindProperty("_resources");
                CollectionAssert.AreEquivalent(
                    new[]
                    {
                        "char.test_package.prefab.character_preview",
                        "art.character.skeleton.animation.standing_run_right"
                    },
                    ReadSerializedResourceIds(resources));
                Assert.AreEqual(
                    "MxFrameworkGenerated/CharacterPackages/test_package/prefabs/test_package_character_preview",
                    FindSerializedResourceAddress(resources, "char.test_package.prefab.character_preview"));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(owner);
            }
        }

        [Test]
        public void ConfigureRuntimeResourceBootstrap_ReplacesStaleRegistryDerivedClipSnapshot()
        {
            EnsureTempRoot();
            string prefabPath = CreatePrefabAsset(TempRoot + "/prefabs/test_package_character_preview.prefab");
            string rightClipPath = CreateAnimationClipAsset(AssetRoot + "/standing_run_right.anim", "standing_run_right");
            string leftClipPath = CreateAnimationClipAsset(AssetRoot + "/standing_run_left.anim", "standing_run_left");
            WriteAnimationSetDefinition();
            WriteAnimationClipRegistry(new AnimationRegistryEntry(
                "set.base",
                "group.locomotion",
                "run.r",
                "standing_run_right",
                "art.character.skeleton.animation.standing_run_right",
                rightClipPath));

            CharacterImportedPackage package = CreatePackage();
            var owner = new GameObject("bootstrap");
            try
            {
                CharacterRuntimeResourceBootstrap bootstrap = owner.AddComponent<CharacterRuntimeResourceBootstrap>();
                InvokeConfigureRuntimeResourceBootstrap(bootstrap, TempRoot, package, prefabPath);

                WriteAnimationClipRegistry(
                    new AnimationRegistryEntry(
                        "set.base",
                        "group.locomotion",
                        "run.r",
                        "standing_run_right",
                        "art.character.skeleton.animation.standing_run_right",
                        rightClipPath),
                    new AnimationRegistryEntry(
                        "set.base",
                        "group.locomotion",
                        "run.l",
                        "standing_run_left",
                        "art.character.skeleton.animation.standing_run_left",
                        leftClipPath));
                InvokeConfigureRuntimeResourceBootstrap(bootstrap, TempRoot, package, prefabPath);

                SerializedProperty resources = new SerializedObject(bootstrap).FindProperty("_resources");
                CollectionAssert.AreEquivalent(
                    new[]
                    {
                        "char.test_package.prefab.character_preview",
                        "art.character.skeleton.animation.standing_run_right",
                        "art.character.skeleton.animation.standing_run_left"
                    },
                    ReadSerializedResourceIds(resources));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(owner);
            }
        }

        [Test]
        public void SynchronizeRuntimeScene_KeepsAlreadyOpenSceneLoaded()
        {
            EnsureTempRoot();
            string prefabPath = CreatePrefabAsset(TempRoot + "/prefabs/test_package_character_preview.prefab");
            string clipPath = CreateAnimationClipAsset(AssetRoot + "/standing_run_right.anim", "standing_run_right");
            WriteAnimationSetDefinition();
            WriteAnimationClipRegistry(new AnimationRegistryEntry(
                "set.base",
                "group.locomotion",
                "run.r",
                "standing_run_right",
                "art.character.skeleton.animation.standing_run_right",
                clipPath));

            CharacterImportedPackage package = CreatePackage();
            string scenePath = TempRoot + "/runtime_entry_sync_test.unity";
            string temporaryPreviousScenePath = TempRoot + "/runtime_entry_sync_previous_scene.unity";
            bool savedTemporaryPreviousScene = false;
            Scene previousActiveScene = SceneManager.GetActiveScene();
            if (previousActiveScene.IsValid()
                && previousActiveScene.isLoaded
                && string.IsNullOrWhiteSpace(previousActiveScene.path))
            {
                Assert.IsTrue(EditorSceneManager.SaveScene(previousActiveScene, temporaryPreviousScenePath));
                savedTemporaryPreviousScene = true;
                previousActiveScene = SceneManager.GetSceneByPath(temporaryPreviousScenePath);
            }

            Scene openScene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            try
            {
                Assert.IsTrue(EditorSceneManager.SaveScene(openScene, scenePath));
                if (previousActiveScene.IsValid() && previousActiveScene.isLoaded)
                    EditorSceneManager.SetActiveScene(previousActiveScene);

                Scene sceneBeforeSync = SceneManager.GetSceneByPath(scenePath);
                Assert.IsTrue(sceneBeforeSync.IsValid());
                Assert.IsTrue(sceneBeforeSync.isLoaded);

                InvokeSynchronizeRuntimeScene(TempRoot, package, prefabPath, scenePath, ensureCalibrationRunner: false);

                Scene sceneAfterSync = SceneManager.GetSceneByPath(scenePath);
                Assert.IsTrue(sceneAfterSync.IsValid());
                Assert.IsTrue(sceneAfterSync.isLoaded);
            }
            finally
            {
                if (!savedTemporaryPreviousScene && previousActiveScene.IsValid() && previousActiveScene.isLoaded)
                    EditorSceneManager.SetActiveScene(previousActiveScene);

                Scene sceneToClose = SceneManager.GetSceneByPath(scenePath);
                if (sceneToClose.IsValid() && sceneToClose.isLoaded)
                    EditorSceneManager.CloseScene(sceneToClose, true);

                if (savedTemporaryPreviousScene)
                    EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            }
        }

        private static IDictionary InvokeReadResourcePreviewInfos(CharacterImportedPackage package)
        {
            MethodInfo method = BuilderType.GetMethod("ReadResourcePreviewInfos", BindingFlags.NonPublic | BindingFlags.Static);
            Assert.IsNotNull(method);
            return (IDictionary)method.Invoke(null, new object[] { package, TempRoot });
        }

        private static object InvokeFindResource(IDictionary resources, string usage, string resourceKey)
        {
            MethodInfo method = BuilderType.GetMethod("FindResource", BindingFlags.NonPublic | BindingFlags.Static);
            Assert.IsNotNull(method);
            return method.Invoke(null, new object[] { resources, usage, resourceKey });
        }

        private static void InvokeSynchronizeCharacterActionProfile(CharacterImportedPackage package)
        {
            MethodInfo method = BuilderType.GetMethod("SynchronizeCharacterActionProfile", BindingFlags.NonPublic | BindingFlags.Static);
            Assert.IsNotNull(method);
            method.Invoke(null, new object[] { package });
            AssetDatabase.Refresh();
        }

        private static bool InvokeShouldMountWeaponAttachment(
            CharacterWeaponAttachmentRuntimeBinding attachment,
            Dictionary<string, GameObject> weaponPrefabs)
        {
            MethodInfo method = BuilderType.GetMethod("ShouldMountWeaponAttachment", BindingFlags.NonPublic | BindingFlags.Static);
            Assert.IsNotNull(method);
            return (bool)method.Invoke(null, new object[] { attachment, weaponPrefabs });
        }

        private static Transform InvokeCreateModelWrapper(string name, object resource, Transform parent)
        {
            MethodInfo method = BuilderType.GetMethod("CreateModelWrapper", BindingFlags.NonPublic | BindingFlags.Static);
            Assert.IsNotNull(method);
            return (Transform)method.Invoke(null, new[] { name, resource, parent });
        }

        private static void InvokeCommonWeaponWrapperPrefabSync()
        {
            Type builder = Type.GetType("MxFramework.Editor.CommonWeaponWrapperPrefabBuilder, MxFramework.Editor");
            Assert.IsNotNull(builder);
            MethodInfo method = builder.GetMethod(
                "SynchronizeFromGlobalProfile",
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
            Assert.IsNotNull(method);
            method.Invoke(null, new object[]
            {
                File.Exists(TemporaryGlobalProfilePath)
                    ? TemporaryGlobalProfilePath
                    : GlobalProfilePath
            });
        }

        private static void InvokeEnsureSpawnedWeaponTraceLocators(
            CharacterImportedPackage package,
            CharacterDefaultEquipmentRuntimeBinder binder)
        {
            MethodInfo method = BuilderType.GetMethod(
                "EnsureSpawnedWeaponTraceLocators",
                BindingFlags.NonPublic | BindingFlags.Static);
            Assert.IsNotNull(method);
            method.Invoke(null, new object[] { package, binder });
        }

        private static void InvokeSampleAnimationClipForWeaponTraceBake(
            GameObject instance,
            AnimationClip clip,
            float time)
        {
            MethodInfo method = BuilderType.GetMethod(
                "SampleAnimationClipForWeaponTraceBake",
                BindingFlags.NonPublic | BindingFlags.Static);
            Assert.IsNotNull(method);
            method.Invoke(null, new object[] { instance, clip, time });
        }

        private static bool InvokeShouldPreserveNestedPrefabInstance(string assetPath)
        {
            MethodInfo method = BuilderType.GetMethod("ShouldPreserveNestedPrefabInstance", BindingFlags.NonPublic | BindingFlags.Static);
            Assert.IsNotNull(method);
            return (bool)method.Invoke(null, new object[] { assetPath });
        }

        private static object InvokeFindHumanoidAvatar(string modelAssetPath, string importedPackageRoot)
        {
            MethodInfo method = BuilderType.GetMethod("FindHumanoidAvatar", BindingFlags.NonPublic | BindingFlags.Static);
            Assert.IsNotNull(method);
            return method.Invoke(null, new object[] { modelAssetPath, importedPackageRoot });
        }

        private static object CreateResourcePreviewInfo(string resourceKey, string assetPath)
        {
            return CreateResourcePreviewInfo(resourceKey, assetPath, "characterModel", Vector3.one);
        }

        private static object CreateResourcePreviewInfo(
            string resourceKey,
            string assetPath,
            string usage,
            Vector3 wrapperScale,
            bool wrapperPoseBakedIntoAsset = false)
        {
            Type resourceType = BuilderType.GetNestedType("ResourcePreviewInfo", BindingFlags.NonPublic);
            Assert.IsNotNull(resourceType);
            var wrapperPose = new CharacterRuntimePose(
                string.Empty,
                string.Empty,
                default,
                default,
                new CharacterRuntimeVector3(wrapperScale.x, wrapperScale.y, wrapperScale.z));
            return Activator.CreateInstance(
                resourceType,
                resourceKey,
                usage,
                assetPath,
                AssetDatabase.AssetPathToGUID(assetPath),
                "Imported",
                true,
                string.Empty,
                string.Empty,
                string.Empty,
                wrapperPose,
                wrapperPoseBakedIntoAsset,
                string.Empty);
        }

        private static void InvokeConfigureRuntimeResourceBootstrap(
            CharacterRuntimeResourceBootstrap bootstrap,
            string importedPackageRoot,
            CharacterImportedPackage package,
            string characterPrefabPath)
        {
            MethodInfo method = BuilderType.GetMethod("ConfigureRuntimeResourceBootstrap", BindingFlags.NonPublic | BindingFlags.Static);
            Assert.IsNotNull(method);
            method.Invoke(null, new object[] { bootstrap, importedPackageRoot, package, characterPrefabPath });
        }

        private static void InvokeSynchronizeRuntimeScene(
            string importedPackageRoot,
            CharacterImportedPackage package,
            string prefabPath,
            string scenePath,
            bool ensureCalibrationRunner)
        {
            MethodInfo method = BuilderType.GetMethod("SynchronizeRuntimeScene", BindingFlags.NonPublic | BindingFlags.Static);
            Assert.IsNotNull(method);
            method.Invoke(null, new object[] { importedPackageRoot, package, prefabPath, scenePath, ensureCalibrationRunner });
        }

        private static T ReadProperty<T>(object obj, string property)
        {
            PropertyInfo info = obj.GetType().GetProperty(property, BindingFlags.Public | BindingFlags.Instance);
            Assert.IsNotNull(info);
            return (T)info.GetValue(obj);
        }

        private static string[] ReadEntryIds(IList entries)
        {
            var ids = new List<string>();
            foreach (object entry in entries)
                ids.Add(ReadProperty<string>(entry, "Id"));
            return ids.ToArray();
        }

        private static string[] ReadEntryPackageIds(IList entries)
        {
            var packageIds = new List<string>();
            foreach (object entry in entries)
                packageIds.Add(ReadProperty<string>(entry, "PackageId"));
            return packageIds.ToArray();
        }

        private static string[] ReadSerializedResourceIds(SerializedProperty resources)
        {
            var ids = new List<string>();
            for (int i = 0; i < resources.arraySize; i++)
                ids.Add(resources.GetArrayElementAtIndex(i).FindPropertyRelative("_id").stringValue);
            return ids.ToArray();
        }

        private static string FindSerializedResourceAddress(SerializedProperty resources, string id)
        {
            for (int i = 0; i < resources.arraySize; i++)
            {
                SerializedProperty entry = resources.GetArrayElementAtIndex(i);
                if (!string.Equals(entry.FindPropertyRelative("_id").stringValue, id, StringComparison.Ordinal))
                    continue;

                return entry.FindPropertyRelative("_address").stringValue;
            }

            return string.Empty;
        }

        private static string[] ReadDefaultWeaponResourceIds(CharacterDefaultEquipmentRuntimeBinder binder)
        {
            var ids = new List<string>();
            for (int i = 0; binder != null && i < binder.DefaultWeapons.Count; i++)
                ids.Add(binder.DefaultWeapons[i].ResourceId);
            return ids.ToArray();
        }

        private static CharacterRuntimePrefabCompositionResult ComposePrefabForTest(
            GameObject prefab,
            CharacterImportedPackage package,
            ResourceManager resourceManager,
            bool instantiateDefaultWeapons,
            out GameObject instance)
        {
            instance = PrefabUtility.InstantiatePrefab(prefab) as GameObject;
            if (instance == null)
                instance = UnityEngine.Object.Instantiate(prefab);
            Assert.NotNull(instance);
            instance.hideFlags = HideFlags.HideAndDontSave;
            instance.SetActive(true);

            CharacterRuntimeSpawnResult spawnResult = CharacterRuntimeSpawnResolver.Resolve(package, default);
            Assert.IsTrue(spawnResult.IsSuccess, FormatSpawnIssues(spawnResult));
            return CharacterRuntimePrefabComposer.Compose(
                instance,
                new CharacterRuntimePrefabCompositionOptions(package, spawnResult)
                {
                    ResourceManager = resourceManager,
                    InstantiateDefaultWeapons = instantiateDefaultWeapons,
                    InstallInputMotionController = false,
                    EnableInputMotion = false,
                    UseExternalRuntimeTick = true
                });
        }

        private static ResourceManager CreateGeneratedPackageWeaponResourceManager(CharacterImportedPackage package)
        {
            var manager = new ResourceManager();
            manager.RegisterProvider(new MxFramework.Editor.EditorAssetDatabaseResourceProvider());
            var entries = new List<ResourceCatalogEntry>();
            CharacterWeaponAttachmentRuntimeBinding[] attachments = package?.Geometry?.WeaponAttachments
                ?? Array.Empty<CharacterWeaponAttachmentRuntimeBinding>();
            for (int i = 0; i < attachments.Length; i++)
            {
                CharacterWeaponAttachmentRuntimeBinding attachment = attachments[i];
                if (attachment == null || CharacterRuntimePrefabResourceCatalog.HasRuntimeWeaponResource(attachment))
                    continue;

                string assetPath = ToProjectRelativePath(CharacterRuntimePrefabResourceCatalog.GetGeneratedWeaponPrefabPath(
                    package.RootPath,
                    package.PackageId,
                    attachment));
                if (!File.Exists(assetPath))
                    continue;

                string resourceId = CharacterRuntimePrefabResourceCatalog.GetDefaultWeaponResourceId(package, attachment);
                entries.Add(new ResourceCatalogEntry(
                    resourceId,
                    ResourceTypeIds.GameObject,
                    MxFramework.Editor.EditorAssetDatabaseResourceProvider.Id,
                    assetPath,
                    CharacterRuntimePrefabResourceCatalog.GetDefaultWeaponResourceVariant(attachment),
                    CharacterRuntimePrefabResourceCatalog.GetDefaultWeaponResourcePackageId(package, attachment),
                    providerData: new Dictionary<string, string>
                    {
                        { MxFramework.Editor.EditorAssetDatabaseResourceProvider.AssetPathProviderDataKey, assetPath }
                    }));
            }

            manager.AddCatalog(new ResourceCatalog(
                "character.import.tests.generatedWeapons." + (package?.PackageId ?? string.Empty),
                package?.PackageId ?? string.Empty,
                entries));
            manager.ValidateCatalogs();
            return manager;
        }

        private static string ToProjectRelativePath(string path)
        {
            string full = Path.GetFullPath(path).Replace('\\', '/');
            string project = Path.GetFullPath(".").Replace('\\', '/').TrimEnd('/') + "/";
            return full.StartsWith(project, StringComparison.OrdinalIgnoreCase)
                ? full.Substring(project.Length)
                : (path ?? string.Empty).Replace('\\', '/');
        }

        private static string FormatSpawnIssues(CharacterRuntimeSpawnResult result)
        {
            if (result == null || result.Issues == null || result.Issues.Length == 0)
                return string.Empty;

            var messages = new List<string>(result.Issues.Length);
            for (int i = 0; i < result.Issues.Length; i++)
                messages.Add(result.Issues[i].Code + ":" + result.Issues[i].Message);
            return string.Join(" | ", messages);
        }

        private static CharacterWeaponAttachmentRuntimeBinding FindAttachment(
            CharacterWeaponAttachmentRuntimeBinding[] attachments,
            string equipSlot)
        {
            for (int i = 0; attachments != null && i < attachments.Length; i++)
            {
                CharacterWeaponAttachmentRuntimeBinding attachment = attachments[i];
                if (attachment != null && attachment.EquipSlot == (equipSlot == "mainHand" ? 1 : 2))
                    return attachment;
            }

            return null;
        }

        private static CharacterDefaultWeaponPrefabBinding FindDefaultWeapon(
            CharacterDefaultEquipmentRuntimeBinder binder,
            string equipSlot)
        {
            for (int i = 0; binder != null && i < binder.DefaultWeapons.Count; i++)
            {
                CharacterDefaultWeaponPrefabBinding weapon = binder.DefaultWeapons[i];
                if (weapon != null && weapon.EquipSlot == (equipSlot == "mainHand" ? 1 : 2))
                    return weapon;
            }

            return null;
        }

        private static void AssertDefaultWeaponBinding(
            CharacterDefaultEquipmentRuntimeBinder binder,
            string equipSlot,
            string resourceId,
            string resourceVariant,
            string resourcePackageId)
        {
            CharacterDefaultWeaponPrefabBinding binding = FindDefaultWeapon(binder, equipSlot);
            Assert.IsNotNull(binding, "Missing default weapon binding for " + equipSlot + ".");
            Assert.AreEqual(resourceId, binding.ResourceId);
            Assert.AreEqual(resourceVariant, binding.ResourceVariant);
            Assert.AreEqual(resourcePackageId, binding.ResourcePackageId);
        }

        private static void AssertNoForbiddenWeaponDescendants(GameObject prefab)
        {
            var names = new List<string>();
            Transform[] descendants = prefab.GetComponentsInChildren<Transform>(includeInactive: true);
            for (int i = 0; i < descendants.Length; i++)
            {
                if (descendants[i] == prefab.transform)
                    continue;

                string name = descendants[i].name;
                names.Add(name);
                Assert.IsFalse(name.StartsWith("mainHand_weapon.", StringComparison.Ordinal), "Forbidden descendant: " + name);
                Assert.IsFalse(name.StartsWith("offHand_weapon.", StringComparison.Ordinal), "Forbidden descendant: " + name);
                Assert.IsFalse(name.StartsWith("DefaultWeapon_", StringComparison.Ordinal), "Forbidden descendant: " + name);
                Assert.IsFalse(name.StartsWith("Model_weapon_", StringComparison.Ordinal), "Forbidden descendant: " + name);
                Assert.AreNotEqual("katana", name, "Forbidden descendant: " + name);
                Assert.AreNotEqual("kite_shield", name, "Forbidden descendant: " + name);
            }
        }

        private static void AssertGeneratedCharacterActionSources(string characterStableId)
        {
            TextAsset profileAsset = AssetDatabase.LoadAssetAtPath<TextAsset>(ActionProfilePath);
            TextAsset actionSetAsset = AssetDatabase.LoadAssetAtPath<TextAsset>(ActionSetPath);
            TextAsset reactionProfileAsset = AssetDatabase.LoadAssetAtPath<TextAsset>(ReactionProfilePath);
            Assert.IsNotNull(profileAsset);
            Assert.IsNotNull(actionSetAsset);
            Assert.IsNotNull(reactionProfileAsset);

            CharacterActionProfilesJsonDocument profileDocument =
                JsonConvert.DeserializeObject<CharacterActionProfilesJsonDocument>(
                    profileAsset.text,
                    CharacterActionAuthoringJsonService.JsonSettings);
            CharacterActionSetsJsonDocument actionSetDocument =
                JsonConvert.DeserializeObject<CharacterActionSetsJsonDocument>(
                    actionSetAsset.text,
                    CharacterActionAuthoringJsonService.JsonSettings);
            CharacterReactionProfilesJsonDocument reactionProfileDocument =
                JsonConvert.DeserializeObject<CharacterReactionProfilesJsonDocument>(
                    reactionProfileAsset.text,
                    CharacterActionAuthoringJsonService.JsonSettings);

            Assert.AreEqual(CharacterActionJsonImporter.CurrentProjectionSchemaVersion, profileDocument.SchemaVersion);
            Assert.AreEqual(CharacterActionJsonImporter.CurrentProjectionSchemaVersion, actionSetDocument.SchemaVersion);
            Assert.AreEqual(CharacterActionJsonImporter.CurrentProjectionSchemaVersion, reactionProfileDocument.SchemaVersion);
            Assert.AreEqual(1, profileDocument.Profiles.Length);
            Assert.AreEqual("test_package.default", profileDocument.Profiles[0].StableId);
            CollectionAssert.Contains(profileDocument.Profiles[0].ActionSetIds, "test_package.default");
            CollectionAssert.Contains(profileDocument.Profiles[0].ActionLibraryIds, "Common");

            Assert.AreEqual(1, actionSetDocument.ActionSets.Length);
            CharacterActionSetJsonDto actionSet = actionSetDocument.ActionSets[0];
            Assert.AreEqual("test_package.default", actionSet.StableId);
            Assert.AreEqual(characterStableId, actionSet.CharacterStableId);
            Assert.AreEqual("reaction.test_package.default", actionSet.ReactionProfileId);
            Assert.IsNotEmpty(actionSet.CommandBindings);
            Assert.IsEmpty(actionSet.AbilityBindings);

            Assert.AreEqual(1, reactionProfileDocument.ReactionProfiles.Length);
            CharacterReactionProfileJsonDto reactionProfile = reactionProfileDocument.ReactionProfiles[0];
            Assert.AreEqual("reaction.test_package.default", reactionProfile.StableId);
            Assert.Greater(reactionProfile.DefaultActionId, 0);
            Assert.IsTrue(reactionProfile.Rules.Any(rule =>
                rule.ActionId > 0 &&
                rule.Trigger == CharacterReactionRuleTrigger.CohesionBreak.ToString() &&
                rule.CurrentScatterPressureBand == "Broken"));
            Assert.IsTrue(reactionProfile.Rules.Any(rule =>
                rule.ActionId > 0 &&
                rule.Trigger == CharacterReactionRuleTrigger.Hit.ToString() &&
                rule.CurrentScatterPressureBand == "Broken" &&
                rule.ReactionGroupId == 10 &&
                rule.MaxImpactForce == 59));
            Assert.IsTrue(reactionProfile.Rules.Any(rule =>
                rule.ActionId > 0 &&
                rule.Trigger == CharacterReactionRuleTrigger.Hit.ToString() &&
                rule.CurrentScatterPressureBand == "Broken" &&
                rule.ReactionGroupId == 10 &&
                rule.MinImpactForce == 60));

            CharacterActionJsonDto reactionAction = FindGeneratedCommonAction("test_package_cohesion_break_react");
            Assert.IsNotNull(reactionAction);
            Assert.AreEqual(CharacterActionCategory.Reaction.ToString(), reactionAction.Category);
            Assert.AreEqual(18, reactionAction.DurationFrames);
            CollectionAssert.Contains(reactionAction.Tags, "reaction");
            CollectionAssert.Contains(reactionAction.Tags, "cohesion_break");
            CharacterActionJsonDto midReactionAction = FindGeneratedCommonAction("test_package_hit_mid_react");
            Assert.IsNotNull(midReactionAction);
            Assert.AreEqual(CharacterActionCategory.Reaction.ToString(), midReactionAction.Category);
            Assert.AreEqual(12, midReactionAction.DurationFrames);
        }

        private static CharacterActionJsonDto FindGeneratedCommonAction(string stableId)
        {
            if (!File.Exists(CommonActionLibraryPath))
                return null;

            CharacterActionsJsonDocument document =
                JsonConvert.DeserializeObject<CharacterActionsJsonDocument>(
                    File.ReadAllText(CommonActionLibraryPath),
                    CharacterActionAuthoringJsonService.JsonSettings);
            return document?.Actions?.FirstOrDefault(action =>
                action != null && string.Equals(action.DisplayName, stableId, StringComparison.Ordinal));
        }

        private static void RemoveGeneratedTestPackageActions()
        {
            if (!File.Exists(CommonActionLibraryPath))
                return;

            CharacterActionsJsonDocument document =
                JsonConvert.DeserializeObject<CharacterActionsJsonDocument>(
                    File.ReadAllText(CommonActionLibraryPath),
                    CharacterActionAuthoringJsonService.JsonSettings);
            if (document?.Actions == null)
                return;

            CharacterActionJsonDto[] filtered = document.Actions
                .Where(action => action == null
                    || !action.DisplayName.StartsWith("test_package_", StringComparison.Ordinal))
                .ToArray();
            if (filtered.Length == document.Actions.Length)
                return;

            document.Actions = filtered;
            File.WriteAllText(CommonActionLibraryPath, JsonConvert.SerializeObject(document, CharacterActionAuthoringJsonService.JsonSettings));
            AssetDatabase.ImportAsset(CommonActionLibraryPath, ImportAssetOptions.ForceUpdate);
        }

        private static void AssertSlimRuntimePrefab(GameObject prefab, string packageId)
        {
            Assert.IsNotNull(FindDescendant(prefab.transform, "ModelRoot"));
            Assert.IsNotNull(FindDescendant(prefab.transform, "Sockets"));
            Transform authoringColliders = FindDescendant(prefab.transform, "AuthoringColliders");
            Assert.IsNotNull(authoringColliders);
            Assert.AreEqual(0, authoringColliders.GetComponentsInChildren<Collider>(includeInactive: true).Length);
            Assert.IsNotNull(FindDescendant(prefab.transform, "Diagnostics"));
            Assert.IsNull(FindDescendant(prefab.transform, "PreviewWeapons"));

            CharacterRuntimePrefabDescriptor[] descriptors = prefab.GetComponents<CharacterRuntimePrefabDescriptor>();
            Assert.AreEqual(1, descriptors.Length);
            Assert.AreEqual(CharacterRuntimePrefabDescriptor.CurrentSchemaVersion, descriptors[0].SchemaVersion);
            Assert.AreEqual(packageId, descriptors[0].PackageId);
            Assert.AreEqual(CharacterRuntimePrefabResourceCatalog.GetCharacterPrefabResourceId(packageId), descriptors[0].CharacterResourceId);
            Assert.AreEqual("default", descriptors[0].ResourceVariant);

            Assert.IsNull(prefab.GetComponent<CharacterDefaultEquipmentRuntimeBinder>());
            Assert.IsNull(prefab.GetComponent<CharacterRuntimeControllerBinding>());
            Assert.IsNull(prefab.GetComponent<CharacterRuntimeInputMotionController>());
            Assert.IsNull(prefab.GetComponent<CharacterRuntimeLocomotionBlendController>());
            Assert.IsNull(prefab.GetComponent<CharacterBoneRuntimeSync>());
        }

        private static void AssertGeneratedContactPointMarkers(GameObject prefab, CharacterImportedPackage package)
        {
            CharacterBodyPartContactPointConfig[] contactPoints =
                package?.Configs?.BodyPartContactPoints ?? Array.Empty<CharacterBodyPartContactPointConfig>();
            for (int i = 0; i < contactPoints.Length; i++)
            {
                CharacterBodyPartContactPointConfig contactPoint = contactPoints[i];
                Transform marker = FindDescendant(prefab.transform, CreateContactPointMarkerNameForTest(contactPoint));
                Assert.IsNotNull(marker, contactPoint.StableId);

                if (contactPoint.MotionMode == CharacterBodyPartContactMotionMode.BakedSocket
                    && contactPoint.MotionSocketId > 0)
                {
                    Transform socket = FindDescendant(prefab.transform, "Socket_" + contactPoint.MotionSocketId);
                    Assert.IsNotNull(socket, contactPoint.StableId);
                    Assert.AreSame(socket, marker.parent, contactPoint.StableId);
                }

                AssertVectorApproximately(
                    new Vector3(
                        contactPoint.LocalOffsetXMillimeters / 1000f,
                        contactPoint.LocalOffsetYMillimeters / 1000f,
                        contactPoint.LocalOffsetZMillimeters / 1000f),
                    marker.localPosition);

                Assert.IsNull(marker.Find("Marker"), contactPoint.StableId);
                Assert.AreEqual(0, marker.GetComponentsInChildren<Renderer>(includeInactive: true).Length, contactPoint.StableId);
            }
        }

        private static string CreateContactPointMarkerNameForTest(CharacterBodyPartContactPointConfig contactPoint)
        {
            string source = contactPoint == null ? string.Empty : contactPoint.StableId;
            if (string.IsNullOrWhiteSpace(source) && contactPoint != null && contactPoint.ContactPointId > 0)
                source = "id." + contactPoint.ContactPointId;
            if (string.IsNullOrWhiteSpace(source))
                source = "unnamed";

            var builder = new System.Text.StringBuilder(source.Length + 13);
            builder.Append("ContactPoint_");
            for (int i = 0; i < source.Length; i++)
            {
                char c = source[i];
                builder.Append(char.IsLetterOrDigit(c) || c == '_' || c == '-' || c == '.' ? c : '_');
            }

            return builder.ToString();
        }

        private static void AssertGeneratedBodyMaterialBinding(
            GameObject prefab,
            CharacterImportedPackagePrefabBuildResult result,
            string packageRoot,
            string packageId,
            bool expectBinding)
        {
            Assert.IsNotNull(prefab);
            Assert.IsNotNull(result);

            if (!expectBinding)
            {
                Assert.AreEqual("noBodyAlbedo", result.BodyMaterialBindingStatus);
                return;
            }

            Assert.AreEqual("bodyMaterialBound", result.BodyMaterialBindingStatus, string.Join(" | ", result.Issues ?? Array.Empty<string>()));
            Assert.AreEqual("wgame.character_traditional_lit", result.BodyMaterialTemplateId);
            Assert.AreEqual("char." + packageId + ".texture.body_albedo", result.BodyMaterialTextureResourceKey);
            Assert.Greater(result.BodyMaterialRendererCount, 0);

            Material material = AssetDatabase.LoadAssetAtPath<Material>(result.BodyMaterialPath);
            Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(packageRoot + "/resources/textures/body_albedo.png");
            Assert.IsNotNull(material, result.BodyMaterialPath);
            Assert.IsNotNull(texture, packageRoot + "/resources/textures/body_albedo.png");
            Assert.AreEqual("WGame/CharacterTraditionalLit", material.shader.name);
            Assert.AreSame(texture, material.GetTexture("_BaseMap"));
            Assert.IsFalse(material.HasProperty("_WgAlbedoStrength"));
            AssertBodyModelRenderersUseMaterial(prefab, material);
        }

        private static void AssertBodyModelRenderersUseMaterial(GameObject prefab, Material material)
        {
            Transform bodyModel = FindDescendant(prefab.transform, "BodyModel");
            Assert.IsNotNull(bodyModel);
            Renderer[] renderers = bodyModel.GetComponentsInChildren<Renderer>(includeInactive: true);
            Assert.Greater(renderers.Length, 0);
            for (int rendererIndex = 0; rendererIndex < renderers.Length; rendererIndex++)
            {
                Material[] materials = renderers[rendererIndex].sharedMaterials;
                Assert.Greater(materials.Length, 0, renderers[rendererIndex].name);
                for (int materialIndex = 0; materialIndex < materials.Length; materialIndex++)
                    Assert.AreSame(material, materials[materialIndex], renderers[rendererIndex].name + "[" + materialIndex + "]");
            }
        }

        private static void AssertVectorApproximately(Vector3 expected, Vector3 actual, float tolerance = 0.0001f)
        {
            Assert.That(actual.x, Is.EqualTo(expected.x).Within(tolerance), "x");
            Assert.That(actual.y, Is.EqualTo(expected.y).Within(tolerance), "y");
            Assert.That(actual.z, Is.EqualTo(expected.z).Within(tolerance), "z");
        }

        private static void AssertBakedVectorApproximately(
            Vector3 expected,
            MxAnimationBakedVector3 actual,
            string label)
        {
            const long tolerance = 2;
            long scale = CharacterWeaponTraceBakeArtifactBuilder.DefaultQuantizationScale;
            Assert.That(actual.X, Is.EqualTo((long)Math.Round(expected.x * scale)).Within(tolerance), label + ".x");
            Assert.That(actual.Y, Is.EqualTo((long)Math.Round(expected.y * scale)).Within(tolerance), label + ".y");
            Assert.That(actual.Z, Is.EqualTo((long)Math.Round(expected.z * scale)).Within(tolerance), label + ".z");
        }

        private static void AssertTraceFramesVary(JArray frames, string message)
        {
            var uniqueEndpoints = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; frames != null && i < frames.Count; i++)
            {
                JObject frame = frames[i] as JObject;
                if (frame == null)
                    continue;

                uniqueEndpoints.Add(
                    FormatFrameVector(frame["rootNow"] as JArray)
                    + "|"
                    + FormatFrameVector(frame["tipNow"] as JArray));
            }

            Assert.Greater(uniqueEndpoints.Count, 1, message);
        }

        private static void AssertSocketFramesVaryOnYAxis(JArray artifacts, string clipKey, string socketId)
        {
            JObject artifact = artifacts
                .OfType<JObject>()
                .FirstOrDefault(candidate => string.Equals(candidate["clipKey"]?.Value<string>(), clipKey, StringComparison.Ordinal)
                    && (candidate["socketFrames"] as JArray)?.Any(frame => string.Equals(frame["socketId"]?.Value<string>(), socketId, StringComparison.Ordinal)) == true);
            Assert.NotNull(artifact, "Expected socket bake artifact for clip=" + clipKey + " socket=" + socketId);

            long min = long.MaxValue;
            long max = long.MinValue;
            foreach (JObject frame in (artifact["socketFrames"] as JArray)?.OfType<JObject>() ?? Enumerable.Empty<JObject>())
            {
                if (!string.Equals(frame["socketId"]?.Value<string>(), socketId, StringComparison.Ordinal))
                    continue;

                JArray position = frame["position"] as JArray;
                Assert.NotNull(position, "Socket frame should include position for " + socketId);
                long y = position[1].Value<long>();
                min = Math.Min(min, y);
                max = Math.Max(max, y);
                Assert.IsTrue(IsAnimatedContactSocketPath(frame["socketPath"]?.Value<string>()), frame["socketPath"]?.Value<string>() ?? string.Empty);
            }

            Assert.Greater(max - min, 0L, "Baked socket Y trajectory should vary for clip=" + clipKey + " socket=" + socketId);
        }

        private static bool IsAnimatedContactSocketPath(string socketPath)
        {
            return (socketPath ?? string.Empty).StartsWith("AnimatedBone:", StringComparison.Ordinal)
                || (socketPath ?? string.Empty).StartsWith("HumanoidCurve:", StringComparison.Ordinal);
        }

        private static string FormatFrameVector(JArray value)
        {
            if (value == null || value.Count < 3)
                return string.Empty;
            return value[0].Value<long>()
                + ","
                + value[1].Value<long>()
                + ","
                + value[2].Value<long>();
        }

        private static ResourceManager CreateCodexFlowTestPublicWeaponResourceManager()
        {
            var manager = new ResourceManager();
            manager.RegisterProvider(new MxFramework.Editor.EditorAssetDatabaseResourceProvider());
            var entries = new List<ResourceCatalogEntry>();
            AddCodexFlowTestPublicWeaponEntry(
                entries,
                "weapon.common_weapon_1.model",
                "Assets/MxFrameworkGenerated/Common/Weapons/common_weapon_1/prefabs/common_weapon_1_weapon.prefab");
            AddCodexFlowTestPublicWeaponEntry(
                entries,
                "weapon.common_kite_shield.model",
                "Assets/MxFrameworkGenerated/Common/Weapons/common_kite_shield/prefabs/common_kite_shield_weapon.prefab");
            manager.AddCatalog(new ResourceCatalog(
                "character.import.tests.codexFlow.publicWeapons",
                "common",
                entries));
            manager.ValidateCatalogs();
            return manager;
        }

        private static void AddCodexFlowTestPublicWeaponEntry(
            List<ResourceCatalogEntry> entries,
            string resourceId,
            string assetPath)
        {
            if (entries == null || string.IsNullOrWhiteSpace(resourceId) || !File.Exists(assetPath))
                return;

            entries.Add(new ResourceCatalogEntry(
                resourceId,
                ResourceTypeIds.GameObject,
                MxFramework.Editor.EditorAssetDatabaseResourceProvider.Id,
                assetPath,
                string.Empty,
                "common",
                providerData: new Dictionary<string, string>
                {
                    { MxFramework.Editor.EditorAssetDatabaseResourceProvider.AssetPathProviderDataKey, assetPath }
                }));
        }

        private static void AssertGeneratedCommonWeaponMaterial(string weaponId, string expectedStatus)
        {
            string weaponRoot = "Assets/MxFrameworkGenerated/Common/Weapons/" + weaponId;
            string prefabPath = weaponRoot + "/prefabs/" + weaponId + "_weapon.prefab";
            string materialPath = weaponRoot + "/materials/" + weaponId + "_Weapon.mat";
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            Material material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            Assert.IsNotNull(prefab, prefabPath);
            Assert.IsNotNull(material, materialPath);
            Assert.IsNotNull(material.shader, materialPath);
            Assert.AreEqual("WGame/PropStylizedLit", material.shader.name);
            Assert.IsTrue(material.HasProperty("_BaseMap"), materialPath + " should expose _BaseMap.");
            Assert.IsTrue(material.HasProperty("_WgAlbedoStrength"), materialPath + " should expose _WgAlbedoStrength.");
            if (string.Equals(expectedStatus, "weaponMaterialBound", StringComparison.Ordinal))
                Assert.AreEqual(1f, material.GetFloat("_WgAlbedoStrength"));
            else
                Assert.AreEqual(0f, material.GetFloat("_WgAlbedoStrength"));

            Renderer[] renderers = prefab.GetComponentsInChildren<Renderer>(true);
            Assert.Greater(renderers.Length, 0, prefabPath + " should contain renderers.");
            for (int rendererIndex = 0; rendererIndex < renderers.Length; rendererIndex++)
            {
                Material[] materials = renderers[rendererIndex].sharedMaterials;
                Assert.IsNotNull(materials, renderers[rendererIndex].name);
                Assert.Greater(materials.Length, 0, renderers[rendererIndex].name);
                for (int materialIndex = 0; materialIndex < materials.Length; materialIndex++)
                    Assert.AreSame(material, materials[materialIndex], renderers[rendererIndex].name + "[" + materialIndex + "]");
            }

            if (string.Equals(weaponId, "common_weapon_1", StringComparison.Ordinal))
            {
                Assert.IsNotNull(material.GetTexture("_BaseMap"), materialPath + " should bind the extracted embedded albedo texture.");
            }
            else if (string.Equals(weaponId, "common_kite_shield", StringComparison.Ordinal))
            {
                Color tint = material.GetColor("_Color");
                Assert.Less(tint.r, 0.5f, materialPath + " should preserve a non-white GLB base color tint.");
                Assert.Less(tint.g, 0.5f, materialPath + " should preserve a non-white GLB base color tint.");
                Assert.Less(tint.b, 0.6f, materialPath + " should preserve a non-white GLB base color tint.");
            }
        }

        private static JObject FindGlobalProfileEntry(JObject profile, string resourceKey)
        {
            foreach (JObject entry in (profile?["entries"] as JArray)?.OfType<JObject>() ?? Enumerable.Empty<JObject>())
            {
                if (string.Equals(entry["resourceKey"]?["id"]?.Value<string>(), resourceKey, StringComparison.Ordinal))
                    return entry;
            }

            return null;
        }

        private static Transform FindDescendant(Transform root, string name)
        {
            if (root == null)
                return null;
            if (string.Equals(root.name, name, StringComparison.Ordinal))
                return root;

            for (int i = 0; i < root.childCount; i++)
            {
                Transform result = FindDescendant(root.GetChild(i), name);
                if (result != null)
                    return result;
            }

            return null;
        }

        private static CharacterImportedPackage CreatePackage(params ResourceCatalogEntry[] entries)
        {
            return new CharacterImportedPackage(
                Path.GetFullPath(TempRoot),
                "test_package",
                CharacterImportedConfigSet.Empty,
                CharacterImportedGeometryBinding.Empty,
                CharacterImportedResourceMapping.Empty,
                new ResourceCatalog("character.package.test", "test_package", entries),
                null);
        }

        private static ResourceCatalogEntry CreateCatalogEntry(
            string id,
            string typeId,
            string assetPath,
            string assetGuid,
            string importStatus,
            string usage,
            string label)
        {
            return new ResourceCatalogEntry(
                id,
                typeId,
                "memory",
                assetPath,
                "default",
                "test_package",
                Array.Empty<ResourceKey>(),
                new[] { label },
                "sha256:test",
                0,
                false,
                new Dictionary<string, string>
                {
                    { "packageResourceKey", id },
                    { "unityAssetPath", assetPath },
                    { "unityAssetGuid", assetGuid },
                    { "importStatus", importStatus },
                    { "usage", usage }
                });
        }

        private static void WriteMaterialDescriptorPackageFiles(string bodyPath, string katanaPath, string shieldPath, string texturePath)
        {
            EnsureFolderPath(ConfigRoot);
            EnsureFolderPath(TempRoot + "/package_cache");
            File.WriteAllText(ConfigRoot + "/character_config_patch.json",
                "{\"format\":\"mx.characterApplicationConfigPatchBundle.v1\",\"runtimeCharacterId\":1,\"patch\":{\"entries\":[]}}");
            File.WriteAllText(ConfigRoot + "/geometry_binding.json",
                "{\"format\":\"mx.characterGeometryBinding.v8\",\"schemaVersion\":\"8.0\",\"packageId\":\"test_package\",\"features\":0}");
            File.WriteAllText(ConfigRoot + "/resource_catalog_mapping.json",
                "{\"format\":\"mx.characterResourceMapping.v1\",\"packageId\":\"test_package\",\"entries\":[]}");
            WriteUnityResourceCatalog(bodyPath, katanaPath, shieldPath, texturePath);
            WriteImportReport();
            AssetDatabase.ImportAsset(ConfigRoot, ImportAssetOptions.ImportRecursive | ImportAssetOptions.ForceUpdate);
            AssetDatabase.ImportAsset(TempRoot + "/package_cache", ImportAssetOptions.ImportRecursive | ImportAssetOptions.ForceUpdate);
        }

        private static void WriteRuntimeEquipmentPackageFiles(string bodyPath, string katanaPath, string shieldPath)
        {
            WriteRuntimeEquipmentPackageFiles(bodyPath, katanaPath, shieldPath, string.Empty);
        }

        private static void WriteRuntimeEquipmentPackageFiles(string bodyPath, string katanaPath, string shieldPath, string bodyAlbedoPath)
        {
            EnsureFolderPath(ConfigRoot);
            EnsureFolderPath(TempRoot + "/package_cache");
            WriteCharacterConfigPatch();
            WriteGeometryBinding();
            WriteRuntimeResourceMapping(bodyPath, katanaPath, shieldPath);
            WriteUnityResourceCatalog(bodyPath, katanaPath, shieldPath, bodyAlbedoPath);
            WriteImportReport();
            AssetDatabase.ImportAsset(ConfigRoot, ImportAssetOptions.ImportRecursive | ImportAssetOptions.ForceUpdate);
            AssetDatabase.ImportAsset(TempRoot + "/package_cache", ImportAssetOptions.ImportRecursive | ImportAssetOptions.ForceUpdate);
        }

        private static void AssignNamedSourceMaterial(string prefabPath, string materialName)
        {
            string materialPath = TempRoot + "/resources/materials/source/" + materialName + ".mat";
            EnsureFolderPath(Path.GetDirectoryName(materialPath)?.Replace('\\', '/'));
            Shader shader = Shader.Find("WGame/PropStylizedLit");
            Assert.IsNotNull(shader);
            var sourceMaterial = new Material(shader) { name = materialName };
            AssetDatabase.CreateAsset(sourceMaterial, materialPath);
            AssetDatabase.ImportAsset(materialPath, ImportAssetOptions.ForceUpdate);

            GameObject root = PrefabUtility.LoadPrefabContents(prefabPath);
            try
            {
                Renderer renderer = root.GetComponentInChildren<Renderer>(includeInactive: true);
                Assert.IsNotNull(renderer);
                renderer.sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
                PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
            AssetDatabase.ImportAsset(prefabPath, ImportAssetOptions.ForceUpdate);
        }

        private static string WritePackageMaterialDescriptor(
            string descriptorId,
            string sourceAssetFileName,
            string albedoTextureResourceKey,
            string surfaceMode = "Opaque",
            float alphaCutoff = 0.5f,
            int renderQueue = 2000,
            Color? tint = null)
        {
            string descriptorPath = TempRoot + "/resources/materials/descriptors/" + descriptorId + ".material.json";
            EnsureFolderPath(Path.GetDirectoryName(descriptorPath)?.Replace('\\', '/'));
            var descriptor = new JObject
            {
                ["sourceAssetPath"] = "Assets/SourceMaterials/" + sourceAssetFileName,
                ["surfaceMode"] = surfaceMode,
                ["alphaCutoff"] = alphaCutoff,
                ["renderQueue"] = renderQueue,
                ["textures"] = new JArray
                {
                    new JObject
                    {
                        ["property"] = "_MainTex",
                        ["resourceKey"] = albedoTextureResourceKey,
                        ["scale"] = new JObject { ["x"] = 1f, ["y"] = 1f },
                        ["offset"] = new JObject { ["x"] = 0f, ["y"] = 0f }
                    }
                }
            };
            if (tint.HasValue)
                descriptor["colors"] = new JArray(new JObject
                {
                    ["name"] = "_Color",
                    ["value"] = new JObject { ["r"] = tint.Value.r, ["g"] = tint.Value.g, ["b"] = tint.Value.b, ["a"] = tint.Value.a }
                });
            File.WriteAllText(descriptorPath, descriptor.ToString(Formatting.Indented));
            AssetDatabase.ImportAsset(descriptorPath, ImportAssetOptions.ForceUpdate);
            return descriptorPath;
        }

        private static void AddMaterialDescriptorToUnityCatalog(string resourceKey, string descriptorPath)
        {
            string catalogPath = ConfigRoot + "/unity_resource_catalog.json";
            JObject catalog = JObject.Parse(File.ReadAllText(catalogPath));
            var entries = catalog["entries"] as JArray;
            Assert.IsNotNull(entries);
            entries.Add(new JObject
            {
                ["id"] = resourceKey,
                ["type"] = ResourceTypeIds.Material,
                ["provider"] = "memory",
                ["address"] = descriptorPath,
                ["variant"] = "default",
                ["packageId"] = "test_package",
                ["dependencies"] = new JArray(),
                ["labels"] = new JArray("character.material"),
                ["contentHash"] = "sha256:test",
                ["sizeBytes"] = 0,
                ["preload"] = false,
                ["providerData"] = new JObject
                {
                    ["packageResourceKey"] = resourceKey,
                    ["unityAssetPath"] = descriptorPath,
                    ["unityAssetGuid"] = AssetDatabase.AssetPathToGUID(descriptorPath),
                    ["importStatus"] = "Imported",
                    ["usage"] = "material"
                }
            });
            File.WriteAllText(catalogPath, catalog.ToString(Formatting.Indented));
            AssetDatabase.ImportAsset(catalogPath, ImportAssetOptions.ForceUpdate);
        }

        private static void WriteCharacterConfigPatch()
        {
            File.WriteAllText(
                ConfigRoot + "/character_config_patch.json",
                "{\n"
                + "  \"format\": \"mx.characterApplicationConfigPatchBundle.v1\",\n"
                + "  \"patch\": {\n"
                + "    \"entries\": [\n"
                + "      { \"source\": \"CharacterConfig\", \"fields\": { \"CharacterId\": 1, \"StableId\": \"test.character\", \"NameText\": \"test.name\", \"DescriptionText\": \"test.desc\", \"AttributeProfileId\": 1, \"BodyProfileId\": 1, \"EquipmentSchemaId\": 1, \"DefaultLoadoutId\": 1, \"BaseAbilityLoadoutId\": 1, \"PresentationProfileId\": 1, \"DefaultControllerKind\": \"None\", \"DefaultControllerProfileId\": \"\", \"DefaultSpawnTags\": [] } },\n"
                + "      { \"source\": \"CharacterAttributeProfileConfig\", \"fields\": { \"AttributeProfileId\": 1, \"StableId\": \"test.attributes\", \"Attributes\": [] } },\n"
                + "      { \"source\": \"CharacterBodyProfileConfig\", \"fields\": { \"BodyProfileId\": 1, \"StableId\": \"test.body\", \"BodyKind\": \"Custom\", \"PartSetId\": \"test_parts\", \"DefaultMotionProfileId\": \"\", \"DefaultPhysicsProfileId\": \"\", \"Sockets\": [], \"HitZoneBindings\": [] } },\n"
                + "      { \"source\": \"CharacterBodyPartConfig\", \"fields\": { \"BodyPartConfigId\": 1, \"StableId\": \"test.body.root\", \"PartSetId\": \"test_parts\", \"PartId\": \"body\", \"ParentPartId\": \"\", \"PartKind\": \"Custom\", \"LocatorId\": \"\", \"HitZoneId\": \"body\", \"ReactionGroupId\": \"\", \"DamageMultiplier\": 1, \"ImpulseScale\": 1, \"StaggerScale\": 1, \"ImpactPowerScale\": 1, \"IsCritical\": false } },\n"
                + "      { \"source\": \"EquipmentSchemaConfig\", \"fields\": { \"EquipmentSchemaId\": 1, \"StableId\": \"test.schema\", \"Slots\": [ { \"SlotId\": \"mainHand\", \"Kind\": \"Custom\", \"DisplayName\": \"Main Hand\", \"AllowedWeaponCategories\": [], \"RequiredTags\": [] }, { \"SlotId\": \"offHand\", \"Kind\": \"Custom\", \"DisplayName\": \"Off Hand\", \"AllowedWeaponCategories\": [], \"RequiredTags\": [] } ], \"ExclusiveGroups\": [], \"AllowedStateIds\": [1] } },\n"
                + "      { \"source\": \"EquipmentLoadoutConfig\", \"fields\": { \"LoadoutId\": 1, \"StableId\": \"test.loadout\", \"EquipmentSchemaId\": 1, \"Slots\": [ { \"SlotId\": \"mainHand\", \"WeaponId\": 101, \"WeaponInstanceStableId\": \"default.katana\" }, { \"SlotId\": \"offHand\", \"WeaponId\": 102, \"WeaponInstanceStableId\": \"default.kite_shield\" } ] } },\n"
                + "      { \"source\": \"EquipmentStateConfig\", \"fields\": { \"StateId\": 1, \"StableId\": \"test.equipment.state\", \"EquipmentSchemaId\": 1, \"Priority\": 1, \"MatchRules\": [], \"GrantedAbilityLoadoutId\": 0, \"CombatActionSetId\": 1, \"AnimationProfileId\": \"\", \"Tags\": [] } },\n"
                + "      { \"source\": \"WeaponConfig\", \"fields\": { \"WeaponId\": 101, \"StableId\": \"weapon.katana\", \"Category\": \"Custom\", \"Tags\": [], \"OccupiesSlots\": [\"mainHand\"], \"GrantedAbilityLoadoutId\": 0, \"WeaponPresentationProfileId\": \"\", \"WeaponCombatProfileId\": \"\", \"WeaponResourceProfileId\": \"\", \"ResourceKeys\": [ { \"Id\": \"char.test.weapon.katana.model\", \"TypeId\": \"" + ResourceTypeIds.GameObject + "\", \"UsageKind\": \"WeaponModel\", \"Variant\": \"default\", \"PackageId\": \"test_package\", \"PreloadGroupId\": \"test_package\" } ], \"TraceBindings\": [], \"BaseModifiers\": [] } },\n"
                + "      { \"source\": \"WeaponConfig\", \"fields\": { \"WeaponId\": 102, \"StableId\": \"weapon.kite_shield\", \"Category\": \"Custom\", \"Tags\": [], \"OccupiesSlots\": [\"offHand\"], \"GrantedAbilityLoadoutId\": 0, \"WeaponPresentationProfileId\": \"\", \"WeaponCombatProfileId\": \"\", \"WeaponResourceProfileId\": \"\", \"ResourceKeys\": [ { \"Id\": \"char.test.weapon.kite_shield.model\", \"TypeId\": \"" + ResourceTypeIds.GameObject + "\", \"UsageKind\": \"WeaponModel\", \"Variant\": \"default\", \"PackageId\": \"test_package\", \"PreloadGroupId\": \"test_package\" } ], \"TraceBindings\": [], \"BaseModifiers\": [] } },\n"
                + "      { \"source\": \"AbilityLoadoutConfig\", \"fields\": { \"LoadoutId\": 1, \"StableId\": \"test.base.abilities\", \"AbilityIds\": [], \"RemoveAbilityIds\": [], \"SlotBindings\": [] } },\n"
                + "      { \"source\": \"CombatActionSetConfig\", \"fields\": { \"ActionSetId\": 1, \"StableId\": \"test.actions\", \"Actions\": [ { \"ActionKey\": \"idle\", \"CombatActionId\": 1, \"TraceProfileIdOverride\": \"\", \"AnimationActionKey\": \"idle\" } ] } },\n"
                + "      { \"source\": \"CharacterPresentationProfileConfig\", \"fields\": { \"PresentationProfileId\": 1, \"StableId\": \"test.presentation\", \"DefaultAnimationProfileId\": \"\", \"ResourceKeys\": [], \"PresentationTags\": [] } },\n"
                + "      { \"source\": \"SpawnProfileConfig\", \"fields\": { \"SpawnProfileId\": 1, \"StableId\": \"test.spawn\", \"CharacterId\": 1, \"TeamId\": \"test\", \"ControllerKind\": \"None\", \"EquipmentLoadoutId\": 1, \"SpawnPose\": { \"AnchorId\": \"\", \"X\": 0, \"Y\": 0, \"Z\": 0, \"YawDegrees\": 0 }, \"RuntimeAiPlannerProfileId\": \"\", \"DebugName\": \"Test Character\" } }\n"
                + "    ]\n"
                + "  }\n"
                + "}\n");
        }

        private static void WriteGeometryBinding()
        {
            File.WriteAllText(
                ConfigRoot + "/geometry_binding.json",
                "{\n"
                + "  \"format\": \"mx.characterGeometryBinding.v1\",\n"
                + "  \"packageId\": \"test_package\",\n"
                + "  \"bodyProfile\": { \"profileId\": \"test.body\", \"bodyKind\": \"Custom\", \"bodyScale\": 1, \"heightMeters\": 2, \"radiusMeters\": 0.35, \"massKg\": 80, \"defaultCapsule\": { \"height\": 2, \"radius\": 0.35, \"center\": { \"x\": 0, \"y\": 1, \"z\": 0 } } },\n"
                + "  \"bodyColliders\": [],\n"
                + "  \"hitZoneBindings\": [],\n"
                + "  \"sockets\": [\n"
                + "    { \"socketId\": \"mainHand\", \"abstractBoneId\": \"right_hand\", \"parentPartId\": \"body\", \"bonePath\": \"\", \"locatorPath\": \"\", \"localPose\": " + PoseJson() + ", \"usage\": \"weapon\" },\n"
                + "    { \"socketId\": \"offHand\", \"abstractBoneId\": \"left_hand\", \"parentPartId\": \"body\", \"bonePath\": \"\", \"locatorPath\": \"\", \"localPose\": " + PoseJson() + ", \"usage\": \"weapon\" },\n"
                + "    { \"socketId\": \"headAnchor\", \"abstractBoneId\": \"head\", \"parentPartId\": \"body\", \"bonePath\": \"\", \"locatorPath\": \"\", \"localPose\": " + PoseJson() + ", \"usage\": \"vfx\" }\n"
                + "  ],\n"
                + "  \"weaponAttachments\": [\n"
                + "    { \"weaponId\": \"weapon.katana\", \"equipSlot\": \"mainHand\", \"attachSocketId\": \"mainHand\", \"localGripPose\": " + PoseJson() + ", \"previewResourceKey\": \"char.test.weapon.katana.model\", \"traceId\": \"trace.katana\" },\n"
                + "    { \"weaponId\": \"weapon.kite_shield\", \"equipSlot\": \"offHand\", \"attachSocketId\": \"offHand\", \"localGripPose\": " + PoseJson() + ", \"previewResourceKey\": \"char.test.weapon.kite_shield.model\", \"traceId\": \"trace.kite_shield\" }\n"
                + "  ],\n"
                + "  \"weaponTraces\": []\n"
                + "}\n");
        }

        private static void WriteGeometryBindingWithPublicDefaultWeapons()
        {
            File.WriteAllText(
                ConfigRoot + "/geometry_binding.json",
                "{\n"
                + "  \"format\": \"mx.characterGeometryBinding.v1\",\n"
                + "  \"packageId\": \"test_package\",\n"
                + "  \"bodyProfile\": { \"profileId\": \"test.body\", \"bodyKind\": \"Custom\", \"bodyScale\": 1, \"heightMeters\": 2, \"radiusMeters\": 0.35, \"massKg\": 80, \"defaultCapsule\": { \"height\": 2, \"radius\": 0.35, \"center\": { \"x\": 0, \"y\": 1, \"z\": 0 } } },\n"
                + "  \"bodyColliders\": [],\n"
                + "  \"hitZoneBindings\": [],\n"
                + "  \"sockets\": [\n"
                + "    { \"socketId\": \"mainHand\", \"abstractBoneId\": \"right_hand\", \"parentPartId\": \"body\", \"bonePath\": \"\", \"locatorPath\": \"\", \"localPose\": " + PoseJson() + ", \"usage\": \"weapon\" },\n"
                + "    { \"socketId\": \"offHand\", \"abstractBoneId\": \"left_hand\", \"parentPartId\": \"body\", \"bonePath\": \"\", \"locatorPath\": \"\", \"localPose\": " + PoseJson() + ", \"usage\": \"weapon\" },\n"
                + "    { \"socketId\": \"headAnchor\", \"abstractBoneId\": \"head\", \"parentPartId\": \"body\", \"bonePath\": \"\", \"locatorPath\": \"\", \"localPose\": " + PoseJson() + ", \"usage\": \"vfx\" }\n"
                + "  ],\n"
                + "  \"weaponAttachments\": [\n"
                + "    { \"weaponId\": \"weapon.katana\", \"equipSlot\": \"mainHand\", \"attachSocketId\": \"mainHand\", \"localGripPose\": " + PoseJson() + ", \"weaponRef\": { \"sourceProviderId\": \"resourceMapping\", \"runtimeResourceKey\": \"weapon.common_weapon_1.model\" }, \"traceId\": \"trace.katana\" },\n"
                + "    { \"weaponId\": \"weapon.kite_shield\", \"equipSlot\": \"offHand\", \"attachSocketId\": \"offHand\", \"localGripPose\": " + PoseJson() + ", \"weaponRef\": { \"sourceProviderId\": \"resourceMapping\", \"runtimeResourceKey\": \"weapon.common_kite_shield.model\" }, \"traceId\": \"trace.kite_shield\" }\n"
                + "  ],\n"
                + "  \"weaponTraces\": []\n"
                + "}\n");
            AssetDatabase.ImportAsset(ConfigRoot + "/geometry_binding.json", ImportAssetOptions.ForceUpdate);
        }

        private static void WriteRuntimeResourceMapping(string bodyPath, string katanaPath, string shieldPath)
        {
            File.WriteAllText(
                ConfigRoot + "/resource_catalog_mapping.json",
                "{\n"
                + "  \"format\": \"mx.characterResourceMapping.v1\",\n"
                + "  \"packageId\": \"test_package\",\n"
                + "  \"entries\": [\n"
                + ResourceMappingEntry("char.test.model.body", "characterModel", bodyPath) + ",\n"
                + ResourceMappingEntry("char.test.weapon.katana.model", "weaponModel", katanaPath) + ",\n"
                + ResourceMappingEntry("char.test.weapon.kite_shield.model", "weaponModel", shieldPath) + "\n"
                + "  ]\n"
                + "}\n");
        }

        private static void WriteUnityResourceCatalog(string bodyPath, string katanaPath, string shieldPath)
        {
            WriteUnityResourceCatalog(bodyPath, katanaPath, shieldPath, string.Empty);
        }

        private static void WriteUnityResourceCatalog(string bodyPath, string katanaPath, string shieldPath, string bodyAlbedoPath)
        {
            var entries = new List<string>
            {
                UnityCatalogEntry("char.test.model.body", "characterModel", "character.model", bodyPath),
                UnityCatalogEntry("char.test.weapon.katana.model", "weaponModel", "weapon.model", katanaPath),
                UnityCatalogEntry("char.test.weapon.kite_shield.model", "weaponModel", "weapon.model", shieldPath)
            };
            if (!string.IsNullOrWhiteSpace(bodyAlbedoPath))
                entries.Add(UnityCatalogEntry("char.test.texture.body_albedo", "texture", "character.texture", bodyAlbedoPath, ResourceTypeIds.Texture2D));

            File.WriteAllText(
                ConfigRoot + "/unity_resource_catalog.json",
                "{\n"
                + "  \"schemaVersion\": 1,\n"
                + "  \"catalogId\": \"character.package.test_package.unity\",\n"
                + "  \"packageId\": \"test_package\",\n"
                + "  \"entries\": [\n"
                + string.Join(",\n", entries) + "\n"
                + "  ]\n"
                + "}\n");
        }

        private static void WriteImportReport()
        {
            File.WriteAllText(
                TempRoot + "/package_cache/import_report.json",
                "{\n"
                + "  \"format\": \"mx.characterUnityImportReport.v1\",\n"
                + "  \"packageId\": \"test_package\",\n"
                + "  \"packageStableId\": \"test_package\",\n"
                + "  \"status\": \"Imported\",\n"
                + "  \"canWriteToUnityProject\": true,\n"
                + "  \"canSpawnAfterImport\": true,\n"
                + "  \"targetRootPath\": \"" + TempRoot + "\",\n"
                + "  \"reportPath\": \"" + TempRoot + "/package_cache/import_report.json\",\n"
                + "  \"sourcePackageHash\": \"sha256:test-source\",\n"
                + "  \"generatedConfigHash\": \"sha256:test-config\",\n"
                + "  \"geometryBindingHash\": \"sha256:test-geometry\",\n"
                + "  \"resourceMappingHash\": \"sha256:test-resources\",\n"
                + "  \"writePlanHash\": \"sha256:test-write\",\n"
                + "  \"conflictCount\": 0,\n"
                + "  \"errorCount\": 0\n"
                + "}\n");
        }

        private static void WriteReactionApplicationConfig(string animationResourceKey)
        {
            File.WriteAllText(
                TempRoot + "/package_cache/character_application.json",
                "{\n"
                + "  \"characterStableId\": \"char.test_package\",\n"
                + "  \"reactionProfiles\": [\n"
                + "    {\n"
                + "      \"profileId\": \"reaction_profile.default\",\n"
                + "      \"displayName\": \"Default reaction profile\",\n"
                + "      \"defaultSlotId\": \"cohesionBreak\",\n"
                + "      \"slots\": [\n"
                + "        {\n"
                + "          \"slotId\": \"cohesionBreak\",\n"
                + "          \"displayName\": \"Cohesion Break\",\n"
                + "          \"trigger\": \"CohesionBreak\",\n"
                + "          \"pressureBand\": \"Broken\",\n"
                + "          \"actionId\": \"test_package_cohesion_break_react\",\n"
                + "          \"durationFrames\": 18,\n"
                + "          \"priority\": 100,\n"
                + "          \"resourceKey\": \"" + animationResourceKey + "\",\n"
                + "          \"resourceSelection\": {\n"
                + "            \"runtimeResourceKey\": \"" + animationResourceKey + "\"\n"
                + "          }\n"
                + "        }\n"
                + "      ]\n"
                + "    }\n"
                + "  ]\n"
                + "}\n");
        }

        private static void WriteDeathReactionApplicationConfig(string animationResourceKey)
        {
            File.WriteAllText(
                TempRoot + "/package_cache/character_application.json",
                "{\n"
                + "  \"characterStableId\": \"char.test_package\",\n"
                + "  \"reactionProfiles\": [\n"
                + "    {\n"
                + "      \"profileId\": \"reaction_profile.default\",\n"
                + "      \"displayName\": \"Default reaction profile\",\n"
                + "      \"defaultSlotId\": \"cohesionBreak\",\n"
                + "      \"slots\": [\n"
                + "        {\n"
                + "          \"slotId\": \"death\",\n"
                + "          \"displayName\": \"Death\",\n"
                + "          \"trigger\": \"Death\",\n"
                + "          \"actionId\": \"test_package_death_react\",\n"
                + "          \"durationFrames\": 60,\n"
                + "          \"priority\": 200,\n"
                + "          \"resourceKey\": \"" + animationResourceKey + "\",\n"
                + "          \"resourceSelection\": {\n"
                + "            \"runtimeResourceKey\": \"" + animationResourceKey + "\"\n"
                + "          }\n"
                + "        }\n"
                + "      ]\n"
                + "    }\n"
                + "  ]\n"
                + "}\n");
        }

        private static string ResourceMappingEntry(string key, string usage, string path)
        {
            return "    { \"packageResourceKey\": \"" + key + "\", \"projectResourceKey\": \"" + key
                + "\", \"stableId\": \"" + key + "\", \"typeId\": \"" + ResourceTypeIds.GameObject
                + "\", \"usage\": \"" + usage + "\", \"importTargetPath\": \"" + path
                + "\", \"modelWrapperPose\": " + PoseJson()
                + ", \"importHash\": \"sha256:test\", \"dependencyHash\": \"sha256:test\" }";
        }

        private static string UnityCatalogEntry(string key, string usage, string label, string path)
        {
            return UnityCatalogEntry(key, usage, label, path, ResourceTypeIds.GameObject);
        }

        private static string UnityCatalogEntry(string key, string usage, string label, string path, string typeId)
        {
            return "    { \"id\": \"" + key + "\", \"type\": \"" + typeId
                + "\", \"provider\": \"memory\", \"address\": \"" + path
                + "\", \"variant\": \"default\", \"packageId\": \"test_package\", \"dependencies\": [], \"labels\": [\""
                + label + "\"], \"contentHash\": \"sha256:test\", \"sizeBytes\": 0, \"preload\": false, \"providerData\": { \"packageResourceKey\": \""
                + key + "\", \"unityAssetPath\": \"" + path + "\", \"unityAssetGuid\": \""
                + AssetDatabase.AssetPathToGUID(path) + "\", \"importStatus\": \"Imported\", \"usage\": \""
                + usage + "\" } }";
        }

        private static string PoseJson()
        {
            return "{ \"position\": { \"x\": 0, \"y\": 0, \"z\": 0 }, \"rotation\": { \"x\": 0, \"y\": 0, \"z\": 0, \"w\": 1 }, \"scale\": { \"x\": 1, \"y\": 1, \"z\": 1 } }";
        }

        private static string GlobalWeaponProfileJson(string weaponPath)
        {
            return GlobalWeaponProfileJson(weaponPath, null);
        }

        private static string GlobalWeaponProfileJson(string weaponPath, string shieldPath)
        {
            var entries = new List<object>
            {
                GlobalWeaponProfileEntry("weapon.common_weapon_1.model", weaponPath)
            };
            if (!string.IsNullOrWhiteSpace(shieldPath))
                entries.Add(GlobalWeaponProfileEntry("weapon.common_kite_shield.model", shieldPath));

            return JsonConvert.SerializeObject(new
            {
                schema = "mx.resource-mapping-profile.v2",
                profileId = "test.global",
                catalogId = "test.global",
                entries,
                bundleRules = Array.Empty<object>(),
                catalogMounts = Array.Empty<object>(),
                preloadGroups = Array.Empty<object>(),
                metadata = new Dictionary<string, string>()
            }, Formatting.Indented);
        }

        private static object GlobalWeaponProfileEntry(string resourceKey, string assetPath)
        {
            string assetGuid = AssetDatabase.AssetPathToGUID(assetPath);
            return new
            {
                producerReceipt = new
                {
                    producerId = "resource-manager.manual",
                    producerContractVersion = "2",
                    sourceRecordFingerprint = "sha256:0000000000000000000000000000000000000000000000000000000000000000"
                },
                authoringRecordId = "test:" + resourceKey,
                resourceKey = new
                {
                    id = resourceKey,
                    type = ResourceTypeIds.GameObject,
                    typeId = ResourceTypeIds.GameObject,
                    variant = string.Empty,
                    packageId = "common"
                },
                sourceLocator = new
                {
                    kind = "unityAsset",
                    cachedPath = assetPath,
                    unityGuid = assetGuid,
                    generatedAssetId = string.Empty,
                    domainSourceId = "test",
                    packageRelativePath = string.Empty,
                    contentHash = string.Empty,
                    providerData = new Dictionary<string, string>()
                },
                labels = new[] { "domain.weapon", "public.common", "usage.weaponModel" },
                providerData = new Dictionary<string, string>
                {
                    { "usage", "weaponModel" },
                    { "modelWrapperPose", PoseJson() }
                },
                deliveryIntent = "internal",
                overrideIntent = "forbid",
                dependencies = Array.Empty<object>(),
                preloadGroups = Array.Empty<string>()
            };
        }

        private static string CreatePrefabAsset(string path)
        {
            return CreatePrefabAsset(path, Vector3.one);
        }

        private static string CreatePrefabAsset(string path, Vector3 localScale)
        {
            EnsureFolderPath(Path.GetDirectoryName(path)?.Replace('\\', '/'));
            GameObject root = GameObject.CreatePrimitive(PrimitiveType.Cube);
            try
            {
                root.name = Path.GetFileNameWithoutExtension(path);
                root.transform.localScale = localScale;
                GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
                Assert.IsNotNull(prefab);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }

            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            return path;
        }

        private static string CreateTextureAsset(string path)
        {
            EnsureFolderPath(Path.GetDirectoryName(path)?.Replace('\\', '/'));
            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            try
            {
                texture.SetPixels(new[]
                {
                    new Color(0.8f, 0.7f, 0.6f, 1f),
                    new Color(0.7f, 0.6f, 0.5f, 1f),
                    new Color(0.6f, 0.5f, 0.4f, 1f),
                    new Color(0.5f, 0.4f, 0.3f, 1f)
                });
                texture.Apply();
                File.WriteAllBytes(path, texture.EncodeToPNG());
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(texture);
            }

            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            Assert.IsNotNull(AssetDatabase.LoadAssetAtPath<Texture2D>(path));
            return path;
        }

        private static string CreateAnimationClipAsset(string path, string clipName)
        {
            EnsureFolderPath(Path.GetDirectoryName(path)?.Replace('\\', '/'));
            var clip = new AnimationClip { name = clipName };
            AssetDatabase.CreateAsset(clip, path);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            return path;
        }

        private static void WriteAnimationSetDefinition()
        {
            EnsureFolderPath(ConfigRoot);
            File.WriteAllText(
                ConfigRoot + "/animation_set_definition.json",
                "{\n"
                + "  \"format\": \"mx.animationSetDefinition.v1\",\n"
                + "  \"schemaVersion\": \"1.0\",\n"
                + "  \"packageId\": \"test_package\",\n"
                + "  \"sets\": [\n"
                + "    {\n"
                + "      \"setId\": \"set.base\",\n"
                + "      \"layers\": [],\n"
                + "      \"blend1DDefinitions\": [],\n"
                + "      \"blend2DDefinitions\": [],\n"
                + "      \"blendTrees\": [],\n"
                + "      \"states\": [],\n"
                + "      \"transitions\": [],\n"
                + "      \"actionBindings\": [],\n"
                + "      \"warmup\": [],\n"
                + "      \"timelineEvents\": []\n"
                + "    }\n"
                + "  ]\n"
                + "}\n");
            AssetDatabase.ImportAsset(ConfigRoot + "/animation_set_definition.json", ImportAssetOptions.ForceUpdate);
        }

        private static void WriteAnimationClipRegistry(params AnimationRegistryEntry[] entries)
        {
            EnsureFolderPath(ConfigRoot);
            var lines = new List<string>
            {
                "{",
                "  \"format\": \"mx.animationClipRegistry.v1\",",
                "  \"schemaVersion\": \"1.0\",",
                "  \"packageId\": \"test_package\",",
                "  \"clips\": ["
            };

            for (int i = 0; i < entries.Length; i++)
            {
                AnimationRegistryEntry entry = entries[i];
                string suffix = i == entries.Length - 1 ? string.Empty : ",";
                lines.Add("    {");
                lines.Add("      \"setId\": \"" + entry.SetId + "\",");
                lines.Add("      \"groupId\": \"" + entry.GroupId + "\",");
                lines.Add("      \"clipId\": \"" + entry.ClipId + "\",");
                lines.Add("      \"sourceClipName\": \"" + entry.SourceClipName + "\",");
                lines.Add("      \"runtimeResourceKey\": \"" + entry.RuntimeResourceKey + "\",");
                lines.Add("      \"sourceSelection\": {");
                lines.Add("        \"unityAssetPath\": \"" + entry.UnityAssetPath + "\"");
                lines.Add("      }");
                lines.Add("    }" + suffix);
            }

            lines.Add("  ]");
            lines.Add("}");
            File.WriteAllText(ConfigRoot + "/animation_clip_registry.json", string.Join("\n", lines) + "\n");
            AssetDatabase.ImportAsset(ConfigRoot + "/animation_clip_registry.json", ImportAssetOptions.ForceUpdate);
        }

        private static void WriteMapping(string resourceKey, string usage, string importTargetPath, float scaleX)
        {
            EnsureFolderPath(ConfigRoot);
            string json = "{\n"
                + "  \"entries\": [\n"
                + "    {\n"
                + "      \"packageResourceKey\": \"" + resourceKey + "\",\n"
                + "      \"usage\": \"" + usage + "\",\n"
                + "      \"importTargetPath\": \"" + importTargetPath + "\",\n"
                + "      \"modelWrapperPose\": {\n"
                + "        \"position\": { \"x\": 0, \"y\": 0, \"z\": 0 },\n"
                + "        \"rotation\": { \"x\": 0, \"y\": 0, \"z\": 0, \"w\": 1 },\n"
                + "        \"scale\": { \"x\": " + scaleX.ToString(System.Globalization.CultureInfo.InvariantCulture) + ", \"y\": 1, \"z\": 1 }\n"
                + "      }\n"
                + "    }\n"
                + "  ]\n"
                + "}\n";
            File.WriteAllText(ConfigRoot + "/resource_catalog_mapping.json", json);
            AssetDatabase.ImportAsset(ConfigRoot + "/resource_catalog_mapping.json", ImportAssetOptions.ForceUpdate);
        }

        private static void EnsureTempRoot()
        {
            EnsureFolderPath(ConfigRoot);
            EnsureFolderPath(AssetRoot);
        }

        private static void WithTemporaryGlobalProfile(string json, Action action)
        {
            EnsureTempRoot();
            File.WriteAllText(TemporaryGlobalProfilePath, json);
            AssetDatabase.ImportAsset(TemporaryGlobalProfilePath, ImportAssetOptions.ForceUpdate);
            CharacterImportedPackagePrefabBuilder.SetResourceMappingProfilePathOverrideForTests(
                TemporaryGlobalProfilePath);

            try
            {
                action();
            }
            finally
            {
                CharacterImportedPackagePrefabBuilder.SetResourceMappingProfilePathOverrideForTests(string.Empty);
                AssetDatabase.DeleteAsset(TemporaryGlobalProfilePath);
                AssetDatabase.Refresh();
            }
        }

        private static void EnsureFolderPath(string path)
        {
            if (string.IsNullOrWhiteSpace(path) || AssetDatabase.IsValidFolder(path))
                return;

            string[] segments = path.Split('/');
            string current = segments[0];
            for (int i = 1; i < segments.Length; i++)
            {
                string next = current + "/" + segments[i];
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(current, segments[i]);
                current = next;
            }
        }

        private readonly struct AnimationRegistryEntry
        {
            public AnimationRegistryEntry(
                string setId,
                string groupId,
                string clipId,
                string sourceClipName,
                string runtimeResourceKey,
                string unityAssetPath)
            {
                SetId = setId;
                GroupId = groupId;
                ClipId = clipId;
                SourceClipName = sourceClipName;
                RuntimeResourceKey = runtimeResourceKey;
                UnityAssetPath = unityAssetPath;
            }

            public string SetId { get; }
            public string GroupId { get; }
            public string ClipId { get; }
            public string SourceClipName { get; }
            public string RuntimeResourceKey { get; }
            public string UnityAssetPath { get; }
        }
    }
}
