using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MxFramework.CharacterRuntimeSpawn;
using MxFramework.CharacterRuntimeSpawn.Unity;
using MxFramework.Game.CharacterTest;
using MxFramework.Resources;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace MxFramework.Tests.CharacterImport
{
    public sealed class CharacterGenericImportIntegrationTests
    {
        private const string Root = "Assets/MxFrameworkGenerated/CharacterPackages/000003";
        private const string BodyPath = Root + "/resources/models/000003_basemesh2.fbx";
        private const string PrefabPath = Root + "/prefabs/000003_character_preview.prefab";
        private const string RuntimePrefabPath = Root + "/runtime_resources/Resources/MxFrameworkGenerated/CharacterPackages/000003/prefabs/000003_character_preview.prefab";
        private const string ReceiptPath = Root + "/package_cache/unity_editor_import_receipt.json";
        private const string SocketBakePath = Root + "/generated/character_socket_bake_catalog.json";
        private const string SocketBakePayloadPath = Root + "/generated/character_socket_bake_catalog.bytes";
        private const string ActionSetPath = "Assets/MxFrameworkGenerated/CharacterActions/ActionSets/000003/character_action_sets.json";
        private const string ActionsPath = "Assets/MxFrameworkGenerated/CharacterActions/Libraries/000003/character_actions.json";

        [Test]
        public void Dragon14_GenericImport_IsUnityReady()
        {
            if (!File.Exists(ReceiptPath))
                Assert.Ignore("Dragon14 generated package is not present.");

            ModelImporter bodyImporter = AssetImporter.GetAtPath(BodyPath) as ModelImporter;
            Assert.IsNotNull(bodyImporter);
            Assert.AreEqual(ModelImporterAnimationType.Generic, bodyImporter.animationType);
            Assert.AreEqual(ModelImporterAvatarSetup.CreateFromThisModel, bodyImporter.avatarSetup);
            Avatar bodyAvatar = AssetDatabase.LoadAllAssetsAtPath(BodyPath).OfType<Avatar>().FirstOrDefault();
            Assert.IsNotNull(bodyAvatar);
            Assert.IsTrue(bodyAvatar.isValid);
            Assert.IsFalse(bodyAvatar.isHuman);

            AnimationClip idleClip = AssertAnimation(
                Root + "/resources/animations/anim000003_idle1.fbx",
                Root + "/runtime_assets/animation_clips/char.000003.animation.idle1.anim",
                bodyAvatar,
                "Dragon14_Idle1",
                loopPose: false);
            AnimationClip walkClip = AssertAnimation(
                Root + "/resources/animations/anim000003_walkf.fbx",
                Root + "/runtime_assets/animation_clips/char.000003.animation.walk_f.anim",
                bodyAvatar,
                "Dragon14_WalkF",
                loopPose: false);
            AnimationClip runClip = AssertAnimation(
                Root + "/resources/animations/anim000003_run1.fbx",
                Root + "/runtime_assets/animation_clips/char.000003.animation.run1.anim",
                bodyAvatar,
                "Dragon14_Run1",
                loopPose: true);
            AnimationClip attack1a = AssertAnimation(
                Root + "/resources/animations/anim000003_attack1a.fbx",
                Root + "/runtime_assets/animation_clips/char.000003.animation.attack1a.anim",
                bodyAvatar,
                "Dragon14_Attack1a",
                loopPose: false,
                loopTime: false);
            AnimationClip attack2a = AssertAnimation(
                Root + "/resources/animations/anim000003_attack2a.fbx",
                Root + "/runtime_assets/animation_clips/char.000003.animation.attack2a.anim",
                bodyAvatar,
                "Dragon14_Attack2a",
                loopPose: false,
                loopTime: false);
            AnimationClip attack6a = AssertAnimation(
                Root + "/resources/animations/anim000003_attack6a.fbx",
                Root + "/runtime_assets/animation_clips/char.000003.animation.attack6a.anim",
                bodyAvatar,
                "Dragon14_Attack6a",
                loopPose: false,
                loopTime: false);
            AnimationClip attack13a = AssertAnimation(
                Root + "/resources/animations/anim000003_attack13a.fbx",
                Root + "/runtime_assets/animation_clips/char.000003.animation.attack13a.anim",
                bodyAvatar,
                "Dragon14_Attack13a",
                loopPose: false,
                loopTime: false);
            AnimationClip attack30b = AssertAnimation(
                Root + "/resources/animations/anim000003_attack30b.fbx",
                Root + "/runtime_assets/animation_clips/char.000003.animation.attack30b.anim",
                bodyAvatar,
                "Dragon14_Attack30b",
                loopPose: false,
                loopTime: false);

            JObject unityCatalog = JObject.Parse(File.ReadAllText(Root + "/config/unity_resource_catalog.json"));
            JObject[] animationResources = (unityCatalog["entries"] as JArray)
                .OfType<JObject>()
                .Where(item => (string)item["type"] == "AnimationClip")
                .ToArray();
            Assert.AreEqual(8, animationResources.Length);
            Assert.IsTrue(animationResources.All(item => ((string)item["address"]).EndsWith(".anim", StringComparison.OrdinalIgnoreCase)));
            Assert.IsTrue(animationResources.All(item => (string)item["sourceFormat"] == "anim"));
            Assert.IsTrue(animationResources.All(item => (string)item["importStatus"] == "Extracted"));
            Assert.IsTrue(animationResources.All(item => (string)item["unityMainObjectType"] == "AnimationClip"));
            Assert.IsTrue(animationResources.All(item => ((string)item["providerData"]?["sourceFbxAssetPath"]).EndsWith(".fbx", StringComparison.OrdinalIgnoreCase)));

            AssertTexture(
                Root + "/resources/textures/t_000003_body_skin2_basemap.png",
                TextureImporterType.Default,
                srgb: true);
            AssertTexture(
                Root + "/resources/textures/t_000003_body_skin2_maskmap.png",
                TextureImporterType.Default,
                srgb: false);
            AssertTexture(
                Root + "/resources/textures/t_000003_body_skin2_normal.png",
                TextureImporterType.NormalMap,
                srgb: false);
            AssertTexture(
                Root + "/resources/textures/t_000003_eye_albedo2.png",
                TextureImporterType.Default,
                srgb: true);

            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            GameObject runtimePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(RuntimePrefabPath);
            Assert.IsNotNull(prefab);
            Assert.IsNotNull(runtimePrefab);
            AssertPreviewSwitches(prefab, idleClip, walkClip, runClip);
            AssertPreviewSwitches(prefab, attack1a, attack2a, attack6a, attack13a, attack30b);
            AssertStableRootCameraFocus(prefab, idleClip, attack1a, attack13a);
            AssertStableRootCameraFocus(runtimePrefab, idleClip, attack1a, attack13a);
            Animator animator = prefab.GetComponentInChildren<Animator>(includeInactive: true);
            Assert.IsNotNull(animator);
            Assert.AreSame(bodyAvatar, animator.avatar);
            Assert.IsTrue(animator.avatar.isValid);
            Assert.IsFalse(animator.avatar.isHuman);

            Material body = AssetDatabase.LoadAssetAtPath<Material>(Root + "/materials/000003_Body.mat");
            Material eye = AssetDatabase.LoadAssetAtPath<Material>(Root + "/materials/000003_Eye.mat");
            Assert.IsNotNull(body);
            Assert.IsNotNull(eye);
            Assert.AreSame(
                AssetDatabase.LoadAssetAtPath<Texture2D>(Root + "/resources/textures/t_000003_body_skin2_basemap.png"),
                body.GetTexture("_BaseMap"));
            Assert.AreSame(
                AssetDatabase.LoadAssetAtPath<Texture2D>(Root + "/resources/textures/t_000003_body_skin2_normal.png"),
                body.GetTexture("_WgNormalTex"));
            Assert.AreSame(
                AssetDatabase.LoadAssetAtPath<Texture2D>(Root + "/resources/textures/t_000003_body_skin2_maskmap.png"),
                body.GetTexture("_WgMaskTex"));
            Assert.AreSame(
                AssetDatabase.LoadAssetAtPath<Texture2D>(Root + "/resources/textures/t_000003_eye_albedo2.png"),
                eye.GetTexture("_BaseMap"));

            int bodySlots = 0;
            int eyeSlots = 0;
            SkinnedMeshRenderer[] renderers = prefab.GetComponentsInChildren<SkinnedMeshRenderer>(includeInactive: true);
            Assert.Greater(renderers.Length, 0);
            for (int rendererIndex = 0; rendererIndex < renderers.Length; rendererIndex++)
            {
                Material[] materials = renderers[rendererIndex].sharedMaterials;
                for (int materialIndex = 0; materialIndex < materials.Length; materialIndex++)
                {
                    Material material = materials[materialIndex];
                    Assert.IsTrue(
                        material == body || material == eye,
                        renderers[rendererIndex].name + "[" + materialIndex + "] was not matched to Body or Eye.");
                    if (material == body)
                        bodySlots++;
                    else
                        eyeSlots++;
                }
            }
            Assert.Greater(bodySlots, 0);
            Assert.Greater(eyeSlots, 0);
            Transform[] prefabTransforms = prefab.GetComponentsInChildren<Transform>(includeInactive: true);
            string[] requiredSockets =
            {
                "Socket_leftClawStart", "Socket_leftClawEnd",
                "Socket_rightClawStart", "Socket_rightClawEnd",
                "Socket_biteStart", "Socket_biteEnd",
                "Socket_tailSweepStart", "Socket_tailSweepEnd", "Socket_mouthVfx"
            };
            for (int i = 0; i < requiredSockets.Length; i++)
                Assert.IsTrue(prefabTransforms.Any(item => item.name == requiredSockets[i]), requiredSockets[i]);

            JObject receipt = JObject.Parse(File.ReadAllText(ReceiptPath));
            Assert.AreEqual("mx.characterUnityEditorImportReceipt.v3", (string)receipt["format"]);
            Assert.AreEqual("3.0", (string)receipt["schemaVersion"]);
            Assert.IsTrue((bool)receipt["ready"]);
            Assert.AreEqual("Generic", (string)receipt["avatar"]?["rigType"]);
            Assert.AreEqual("ValidGeneric", (string)receipt["avatar"]?["rigStatus"]);
            Assert.AreEqual(PrefabPath, (string)receipt["prefab"]?["prefabPath"]);
            Assert.AreEqual(RuntimePrefabPath, (string)receipt["prefab"]?["runtimePrefabPath"]);
            JArray receiptMaterials = receipt["materials"] as JArray;
            Assert.IsNotNull(receiptMaterials);
            Assert.AreEqual(2, receiptMaterials.Count);
            CollectionAssert.AreEquivalent(
                new[] { "Body", "Eye" },
                receiptMaterials.Select(item => (string)item["sourceMaterialName"]).ToArray());
            Assert.AreEqual(bodySlots, receiptMaterials.First(item => (string)item["sourceMaterialName"] == "Body")["assignedSlotCount"].Value<int>());
            Assert.AreEqual(eyeSlots, receiptMaterials.First(item => (string)item["sourceMaterialName"] == "Eye")["assignedSlotCount"].Value<int>());
            Assert.AreEqual("Ready", (string)receipt["weaponTraceBake"]?["status"]);
            Assert.AreEqual(0, (int)receipt["weaponTraceBake"]?["requestCount"]);
            Assert.AreEqual(0, (int)receipt["weaponTraceBake"]?["artifactCount"]);
            Assert.AreEqual("Ready", (string)receipt["socketBake"]?["status"]);
            Assert.AreEqual(8, (int)receipt["socketBake"]?["requestCount"]);
            Assert.AreEqual(8, (int)receipt["socketBake"]?["clipCount"]);
            Assert.AreEqual(106, (int)receipt["socketBake"]?["socketTrackCount"]);
            Assert.AreEqual(14274, (int)receipt["socketBake"]?["checkedFrameCount"]);
            Assert.AreEqual(0, (int)receipt["socketBake"]?["issueCount"]);
            Assert.IsTrue(File.Exists(SocketBakePath));
            Assert.IsTrue(File.Exists(SocketBakePayloadPath));
            Assert.AreEqual("mx.character.socketBakeCatalog.v3", (string)receipt["socketBake"]?["format"]);
            Assert.AreEqual("generated/character_socket_bake_catalog.bytes", (string)receipt["socketBake"]?["binaryPath"]);
            Assert.IsTrue((long)receipt["socketBake"]?["binarySize"] > 0);
            Assert.IsTrue(new FileInfo(SocketBakePayloadPath).Length <= 10875774 / 4);
        }

        [Test]
        public void Dragon14_LocomotionRuntimeResourceKeys_LoadStandaloneAnimationClips()
        {
            if (!File.Exists(ReceiptPath))
                Assert.Ignore("Dragon14 generated package is not present.");

            CharacterImportedPackage package = CharacterImportedPackageJson.LoadFromDirectory(Path.GetFullPath(Root));
            bool bindingCreated = CharacterRuntimeAnimationResources.TryCreateBinding(
                package,
                out CharacterRuntimeAnimationBinding binding,
                out CharacterRuntimeSpawnResult spawnResult);
            Assert.IsTrue(bindingCreated, spawnResult != null ? spawnResult.Status.ToString() : "Spawn result was null.");

            ResourceCatalogEntry[] entries = CharacterRuntimeAnimationResources.CreateCatalogEntries(
                    binding,
                    CharacterTestEditorAnimationAssetProvider.Id)
                .ToArray();
            Assert.AreEqual(8, entries.Length);
            Assert.IsTrue(entries.All(entry => entry.Address.EndsWith(".anim", StringComparison.OrdinalIgnoreCase)));
            Assert.IsFalse(entries.Any(entry => entry.Address.EndsWith(".fbx", StringComparison.OrdinalIgnoreCase)));

            var manager = new ResourceManager();
            manager.RegisterProvider(new CharacterTestEditorAnimationAssetProvider());
            manager.AddCatalog(new ResourceCatalog("000003.animation.runtime.test", "000003", entries));
            var handles = new List<ResourceHandle<AnimationClip>>();
            try
            {
                foreach (string resourceId in new[]
                {
                    "char.000003.animation.idle1",
                    "char.000003.animation.walk_f",
                    "char.000003.animation.run1"
                })
                {
                    var key = new ResourceKey(resourceId, ResourceTypeIds.AnimationClip, packageId: "000003");
                    ResourceLoadResult<ResourceHandle<AnimationClip>> load = manager.Load<AnimationClip>(key);
                    Assert.IsTrue(load.Success, resourceId + ": " + load.Error.Message);
                    Assert.IsNotNull(load.Value.Value, resourceId);
                    handles.Add(load.Value);
                }
            }
            finally
            {
                for (int i = 0; i < handles.Count; i++)
                    manager.Release(handles[i]);
            }
        }

        [Test]
        public void Dragon14_NaturalAttackContent_HasExpectedSequencesDamageAndBodyGeometry()
        {
            if (!File.Exists(ActionSetPath) || !File.Exists(ActionsPath))
                Assert.Ignore("Dragon14 generated CharacterAction content is not present.");

            JObject actionSetRoot = JObject.Parse(File.ReadAllText(ActionSetPath));
            JObject actionSet = (actionSetRoot["actionSets"] as JArray)?.Single() as JObject;
            Assert.IsNotNull(actionSet);
            Assert.AreEqual("000003.unarmed", (string)actionSet["stableId"]);
            Assert.AreEqual("Dragon14 Natural Attacks", (string)actionSet["displayName"]);
            JArray sequences = actionSet["commandSequences"] as JArray;
            CollectionAssert.AreEqual(
                new[] { 940000, 940001, 940002 },
                sequences.Single(item => (string)item["intentId"] == "LightAttack")["actionIds"].ToObject<int[]>());
            CollectionAssert.AreEqual(
                new[] { 940003 },
                sequences.Single(item => (string)item["intentId"] == "AttackSecondary")["actionIds"].ToObject<int[]>());
            CollectionAssert.AreEqual(
                new[] { 940004 },
                sequences.Single(item => (string)item["intentId"] == "Skill1")["actionIds"].ToObject<int[]>());

            JArray actions = JObject.Parse(File.ReadAllText(ActionsPath))["actions"] as JArray;
            JObject attack1 = AssertAction(actions, 940000, 120, 1);
            JObject attack2 = AssertAction(actions, 940001, 120, 1);
            AssertPrimaryComboCancel(attack1);
            AssertPrimaryComboCancel(attack2);
            JObject attack6 = AssertAction(actions, 940002, 194, 2);
            JObject attack13 = AssertAction(actions, 940003, 168, 0);
            JObject attack30 = AssertAction(actions, 940004, 210, 1);
            JArray attack6Events = attack6["combatTrack"]?["events"] as JArray;
            CollectionAssert.AreEqual(
                new[] { "000003.attack6.bite", "000003.attack6.tail_sweep" },
                attack6Events.Select(item => (string)item["stableEventId"]).ToArray());
            Assert.IsTrue(attack6Events.All(item => (string)item["hitEvent"]?["hitSameTargetPolicy"] == "OncePerEvent"));
            Assert.AreEqual("OncePerEvent", (string)attack30["combatTrack"]?["events"]?[0]?["hitEvent"]?["hitSameTargetPolicy"]);
            Assert.AreEqual(JTokenType.Null, attack13["combatTrack"]?.Type);
            Assert.AreEqual(JTokenType.Null, attack13["gameplayTrack"]?.Type);
            Assert.AreEqual(1, (attack13["animationTrack"]?["events"] as JArray)?.Count);

            JObject body = JObject.Parse(File.ReadAllText("Authoring/CharacterPackages/character-000003/geometry/body_geometry.json"));
            Assert.AreEqual(4.2f, (float)body["defaultCapsule"]?["height"], 0.0001f);
            Assert.AreEqual(1.3f, (float)body["defaultCapsule"]?["radius"], 0.0001f);
            Assert.AreEqual(2.1f, (float)body["defaultCapsule"]?["center"]?["y"], 0.0001f);
            Assert.AreEqual(0.5f, (float)body["defaultCapsule"]?["center"]?["z"], 0.0001f);
            JObject sockets = JObject.Parse(File.ReadAllText("Authoring/CharacterPackages/character-000003/geometry/sockets.json"));
            Assert.AreEqual(12, (sockets["sockets"] as JArray)?.Count);
            JObject mouthVfx = (sockets["sockets"] as JArray)?.Single(item => (string)item["socketId"] == "mouthVfx") as JObject;
            StringAssert.EndsWith("Dragon15_tongue3", (string)mouthVfx?["bonePath"]);
        }

        private static JObject AssertAction(JArray actions, int id, int durationFrames, int combatEventCount)
        {
            JObject action = actions.Single(item => (int)item["actionId"] == id) as JObject;
            Assert.IsNotNull(action);
            Assert.AreEqual(durationFrames, (int)action["durationFrames"]);
            JObject combatTrack = action["combatTrack"] as JObject;
            JArray combatEvents = combatTrack?["events"] as JArray;
            Assert.AreEqual(combatEventCount, combatEvents?.Count ?? 0);
            for (int i = 0; combatEvents != null && i < combatEvents.Count; i++)
                Assert.AreEqual("BakedSocketCapsule", (string)combatEvents[i]["hitVolume"]?["sourceKind"]);
            return action;
        }

        private static void AssertPrimaryComboCancel(JObject action)
        {
            JArray cancelRules = action["cancelRules"] as JArray;
            Assert.AreEqual(1, cancelRules?.Count ?? 0);
            Assert.AreEqual(108, (int)cancelRules[0]["startFrame"]);
            Assert.AreEqual(119, (int)cancelRules[0]["endFrame"]);
            Assert.AreEqual(4, (int)cancelRules[0]["requestTypeMask"]);
            Assert.AreEqual(1, (int)cancelRules[0]["actionSlotMask"]);
        }

        private static AnimationClip AssertAnimation(
            string sourceFbxPath,
            string runtimeClipPath,
            Avatar bodyAvatar,
            string clipName,
            bool loopPose,
            bool loopTime = true)
        {
            ModelImporter importer = AssetImporter.GetAtPath(sourceFbxPath) as ModelImporter;
            Assert.IsNotNull(importer, sourceFbxPath);
            Assert.AreEqual(ModelImporterAnimationType.Generic, importer.animationType, sourceFbxPath);
            Assert.AreEqual(ModelImporterAvatarSetup.CopyFromOther, importer.avatarSetup, sourceFbxPath);
            Assert.AreSame(bodyAvatar, importer.sourceAvatar, sourceFbxPath);
            Assert.AreEqual(1, importer.clipAnimations.Length, sourceFbxPath);
            ModelImporterClipAnimation clip = importer.clipAnimations[0];
            Assert.AreEqual("Unreal Take", clip.takeName, sourceFbxPath);
            Assert.AreEqual(clipName, clip.name, sourceFbxPath);
            Assert.AreEqual(loopTime, clip.loopTime, sourceFbxPath);
            Assert.AreEqual(loopPose, clip.loopPose, sourceFbxPath);
            Assert.IsTrue(clip.lockRootRotation, sourceFbxPath);
            Assert.IsTrue(clip.lockRootHeightY, sourceFbxPath);
            Assert.IsTrue(clip.lockRootPositionXZ, sourceFbxPath);

            Assert.AreEqual(typeof(AnimationClip), AssetDatabase.GetMainAssetTypeAtPath(runtimeClipPath), runtimeClipPath);
            AnimationClip animationClip = AssetDatabase.LoadAssetAtPath<AnimationClip>(runtimeClipPath);
            Assert.IsNotNull(animationClip, runtimeClipPath + " was not materialized as a standalone AnimationClip.");
            Assert.AreEqual(Path.GetFileNameWithoutExtension(runtimeClipPath), animationClip.name, runtimeClipPath);
            AnimationClipSettings settings = AnimationUtility.GetAnimationClipSettings(animationClip);
            Assert.AreEqual(loopTime, settings.loopTime, runtimeClipPath);
            Assert.AreEqual(loopPose, settings.loopBlend, runtimeClipPath);
            return animationClip;
        }

        private static void AssertPreviewSwitches(GameObject prefab, params AnimationClip[] clips)
        {
            GameObject instance = UnityEngine.Object.Instantiate(prefab);
            try
            {
                Animator animator = instance.GetComponentInChildren<Animator>(includeInactive: true);
                Assert.IsNotNull(animator);
                var poseFingerprints = new double[clips.Length];
                for (int clipIndex = 0; clipIndex < clips.Length; clipIndex++)
                {
                    AnimationClip clip = clips[clipIndex];
                    clip.SampleAnimation(animator.gameObject, clip.length * 0.37f);
                    Transform[] transforms = animator.gameObject.GetComponentsInChildren<Transform>(includeInactive: true);
                    double fingerprint = 0d;
                    for (int transformIndex = 0; transformIndex < transforms.Length; transformIndex++)
                    {
                        Vector3 position = transforms[transformIndex].localPosition;
                        Quaternion rotation = transforms[transformIndex].localRotation;
                        fingerprint += (transformIndex + 1) * (
                            (position.x * 1.1d) +
                            (position.y * 1.3d) +
                            (position.z * 1.7d) +
                            (rotation.x * 1.9d) +
                            (rotation.y * 2.3d) +
                            (rotation.z * 2.9d) +
                            (rotation.w * 3.1d));
                    }

                    poseFingerprints[clipIndex] = Math.Round(fingerprint, 6);
                }

                Assert.AreEqual(
                    clips.Length,
                    poseFingerprints.Distinct().Count(),
                    "Each Dragon14 preview clip must produce a distinct sampled skeleton pose.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(instance);
            }
        }

        private static void AssertStableRootCameraFocus(GameObject prefab, params AnimationClip[] clips)
        {
            GameObject instance = UnityEngine.Object.Instantiate(prefab);
            try
            {
                CharacterAbstractBoneRuntimeMap map =
                    instance.GetComponent<CharacterAbstractBoneRuntimeMap>();
                Assert.IsNotNull(map, prefab.name);
                Assert.IsTrue(
                    map.TryResolve("ext.000003.camera_focus", out Transform cameraFocus),
                    prefab.name);
                Assert.IsNotNull(cameraFocus, prefab.name);

                Transform bodyModel = instance.GetComponentsInChildren<Transform>(includeInactive: true)
                    .FirstOrDefault(item => item.name == "BodyModel");
                Assert.IsNotNull(bodyModel, prefab.name);
                Animator animator = instance.GetComponentInChildren<Animator>(includeInactive: true);
                Assert.IsNotNull(animator, prefab.name);

                Vector3 expected = new Vector3(0f, 3.72f, 0f);
                Assert.That(
                    Vector3.Distance(bodyModel.InverseTransformPoint(cameraFocus.position), expected),
                    Is.LessThanOrEqualTo(0.01f),
                    prefab.name);

                for (int clipIndex = 0; clipIndex < clips.Length; clipIndex++)
                {
                    AnimationClip clip = clips[clipIndex];
                    clip.SampleAnimation(animator.gameObject, clip.length * 0.37f);
                    Assert.That(
                        Vector3.Distance(bodyModel.InverseTransformPoint(cameraFocus.position), expected),
                        Is.LessThanOrEqualTo(0.01f),
                        prefab.name + " moved cameraFocus while sampling " + clip.name + ".");
                }

                instance.transform.rotation = Quaternion.Euler(0f, 137f, 0f);
                Assert.That(
                    Vector3.Distance(bodyModel.InverseTransformPoint(cameraFocus.position), expected),
                    Is.LessThanOrEqualTo(0.01f),
                    prefab.name + " changed cameraFocus during root yaw.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(instance);
            }
        }

        private static void AssertTexture(string path, TextureImporterType type, bool srgb)
        {
            TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
            Assert.IsNotNull(importer, path);
            Assert.AreEqual(type, importer.textureType, path);
            Assert.AreEqual(srgb, importer.sRGBTexture, path);
        }
    }
}
