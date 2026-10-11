using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MagicaCloth2;
using MxFramework.CharacterCloth.Magica.Unity.Editor;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace MxFramework.Tests.CharacterCloth.Magica.Editor
{
    public sealed class MagicaCharacterClothAuthoringEditorTests
    {
        [Test]
        public void Serializer_RoundTripIsStableAndPreservesUnknownFields()
        {
            const string json = "{\"schemaVersion\":\"1.0\",\"backend\":\"MagicaCloth2\",\"enabled\":true,\"futureRoot\":{\"value\":7},\"cloths\":[{\"id\":\"hair\",\"clothType\":\"BoneCloth\",\"rootBonePaths\":[\"root/hair\"],\"connectionMode\":\"Line\",\"presetAssetPath\":\"Assets/MagicaCloth2/Res/Preset/MC2_Preset_Tail.json\",\"presetSha256\":\"sha256:84399b8b65a155eadda37d373a27d89e268dfd987dbdb8f4cef21a194cf39dcf\",\"futureCloth\":true,\"colliders\":[{\"id\":\"head\",\"type\":\"Sphere\",\"parentBonePath\":\"root/head\",\"localPosition\":{\"x\":0,\"y\":0,\"z\":0},\"localRotationEuler\":{\"x\":0,\"y\":0,\"z\":0},\"radius\":0.1,\"endRadius\":0,\"length\":0,\"direction\":\"Y\",\"futureCollider\":\"kept\"}]}]}";
            MagicaCharacterClothAuthoringDocument document = MagicaCharacterClothAuthoringSerializer.Deserialize(json);
            string first = MagicaCharacterClothAuthoringSerializer.Serialize(document);
            string second = MagicaCharacterClothAuthoringSerializer.Serialize(
                MagicaCharacterClothAuthoringSerializer.Deserialize(first));
            Assert.AreEqual(first, second);
            JObject root = JObject.Parse(first);
            Assert.AreEqual(7, (int)root["futureRoot"]["value"]);
            Assert.IsTrue((bool)root["cloths"][0]["futureCloth"]);
            Assert.AreEqual("kept", (string)root["cloths"][0]["colliders"][0]["futureCollider"]);
        }

        [Test]
        public void Serializer_RoundTripsTypedParameterOverrides()
        {
            MagicaCharacterClothAuthoringDocument document = CreateValidDocument();
            document.schemaVersion = "1.1";
            document.cloths[0].parameterOverrides = CreateCompleteParameterOverrides();

            string json = MagicaCharacterClothAuthoringSerializer.Serialize(document);
            MagicaCharacterClothAuthoringDocument roundTrip =
                MagicaCharacterClothAuthoringSerializer.Deserialize(json);

            Assert.AreEqual("1.1", roundTrip.schemaVersion);
            Assert.AreEqual(0.2f, roundTrip.cloths[0].parameterOverrides.damping.value, 0.0001f);
            Assert.IsFalse(roundTrip.cloths[0].parameterOverrides.movementSpeedLimit.use);
            Assert.AreEqual(45f, roundTrip.cloths[0].parameterOverrides.angleLimit.value, 0.0001f);
            Assert.AreEqual(0.15f, roundTrip.cloths[0].parameterOverrides.springPower.value, 0.0001f);
        }

        [Test]
        public void UnitySerialization_PreservesNullForUnselectedParameterOverrides()
        {
            MagicaCharacterClothAuthoringSession source =
                ScriptableObject.CreateInstance<MagicaCharacterClothAuthoringSession>();
            MagicaCharacterClothAuthoringSession copy =
                ScriptableObject.CreateInstance<MagicaCharacterClothAuthoringSession>();
            try
            {
                source.document = CreateValidDocument();
                source.document.schemaVersion = "1.1";
                source.document.cloths[0].parameterOverrides =
                    new MagicaCharacterClothParameterOverrides
                    {
                        damping = new MagicaCharacterClothFloatOverride(0.2f),
                    };

                EditorUtility.CopySerialized(source, copy);

                MagicaCharacterClothParameterOverrides value =
                    copy.document.cloths[0].parameterOverrides;
                Assert.IsNotNull(value);
                Assert.AreEqual(0.2f, value.damping.value, 0.0001f);
                Assert.IsNull(value.gravity);
                Assert.IsNull(value.gravityDirection);
                Assert.IsNull(value.surfaceThickness);
                Assert.IsNull(value.springPower);
                Assert.IsNull(value.windInfluence);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(source);
                UnityEngine.Object.DestroyImmediate(copy);
            }
        }

        [Test]
        public void BoneSceneOverlay_CollectsActualSkinnedMeshBones()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/MxFrameworkGenerated/CharacterPackages/000001/prefabs/000001_character_preview.prefab");
            Assert.IsNotNull(prefab);
            Transform body = Array.Find(
                prefab.GetComponentsInChildren<Transform>(true),
                transform => transform.name == "BodyModel");
            Assert.IsNotNull(body);

            Transform[] bones = MagicaCharacterClothBoneSceneOverlay.CollectSkeletonBones(body);
            MagicaCharacterClothAuthoringDocument document =
                MagicaCharacterClothAuthoringRepository.Load(
                    Path.GetFullPath("Authoring/CharacterPackages/character-000001"))
                .Document;
            Transform[] clothBones = MagicaCharacterClothBoneSceneOverlay
                .CollectClothBones(body, document);

            Assert.Greater(bones.Length, 10);
            Assert.IsNotNull(Array.Find(bones, transform => transform.name == "head.x"));
            Assert.IsNotNull(Array.Find(bones, transform => transform.name == "cape.l_01"));
            Assert.IsNull(Array.Find(bones, transform => transform.name.StartsWith("MagicaCollider_", StringComparison.Ordinal)));
            Assert.IsNull(Array.Find(bones, transform => transform.name.StartsWith("MagicaCloth_", StringComparison.Ordinal)));
            Assert.Greater(clothBones.Length, document.cloths[0].rootBonePaths.Length);
            Assert.IsNotNull(Array.Find(clothBones, transform => transform.name == "cape.l_01"));
            Assert.IsNotNull(Array.Find(clothBones, transform => transform.name == "cape.l_02"));
        }

        [Test]
        public void CreateClothFromSceneBone_ClonesTemplateAndReplacesRoot()
        {
            MagicaCharacterClothAuthoringEntry template =
                MagicaCharacterClothAuthoringRepository.Load(
                    Path.GetFullPath("Authoring/CharacterPackages/character-000001"))
                .Document.cloths[0];
            template.parameterOverrides = new MagicaCharacterClothParameterOverrides
            {
                damping = new MagicaCharacterClothFloatOverride(0.25f),
            };
            const string selectedPath = "root/spine/head/new_hair_00";

            MagicaCharacterClothAuthoringEntry created =
                MagicaCharacterClothAuthoringWindow.CreateClothFromSceneBone(
                    template,
                    selectedPath,
                    new List<string> { template.id, "new_hair_00" });

            Assert.AreEqual("new_hair_00_2", created.id);
            CollectionAssert.AreEqual(new[] { selectedPath }, created.rootBonePaths);
            Assert.AreEqual(template.presetAssetPath, created.presetAssetPath);
            Assert.AreEqual(template.presetSha256, created.presetSha256);
            Assert.IsFalse(ReferenceEquals(template.colliders, created.colliders));
            Assert.IsFalse(ReferenceEquals(template.parameterOverrides, created.parameterOverrides));
            Assert.AreEqual(0.25f, created.parameterOverrides.damping.value, 0.0001f);
        }

        [Test]
        public void ModelInitializer_GroupsEligibleCustomChainsByBodyRegion()
        {
            var body = new GameObject("BodyModel");
            var model = new GameObject("000004_lod0");
            var rig = new GameObject("rig");
            var pelvis = new GameObject("pelvis.x");
            var customRoot = new GameObject("pelvis.x.001");
            var customChild = new GameObject("pelvis.x.002");
            var breastRoot = new GameObject("breast_01.l");
            var breastChild = new GameObject("breast_02.l");
            var singleBone = new GameObject("head.x.001");
            var zeroRoot = new GameObject("pelvis.x.008");
            var zeroChild = new GameObject("pelvis.x.009");
            var thighLeft = new GameObject("thigh.l");
            var lowerLegLeft = new GameObject("lower_leg.l");
            var thighRight = new GameObject("thigh.r");
            var lowerLegRight = new GameObject("lower_leg.r");
            var rendererObject = new GameObject("body_mesh");
            var mesh = new Mesh();
            try
            {
                model.transform.SetParent(body.transform);
                rig.transform.SetParent(model.transform);
                pelvis.transform.SetParent(rig.transform);
                customRoot.transform.SetParent(pelvis.transform);
                customChild.transform.SetParent(customRoot.transform);
                breastRoot.transform.SetParent(pelvis.transform);
                breastChild.transform.SetParent(breastRoot.transform);
                singleBone.transform.SetParent(pelvis.transform);
                zeroRoot.transform.SetParent(pelvis.transform);
                zeroChild.transform.SetParent(zeroRoot.transform);
                thighLeft.transform.SetParent(pelvis.transform);
                lowerLegLeft.transform.SetParent(thighLeft.transform);
                thighRight.transform.SetParent(pelvis.transform);
                lowerLegRight.transform.SetParent(thighRight.transform);
                thighLeft.transform.localPosition = new Vector3(-0.1f, -0.1f, 0f);
                lowerLegLeft.transform.localPosition = new Vector3(0f, -0.4f, 0f);
                thighRight.transform.localPosition = new Vector3(0.1f, -0.1f, 0f);
                lowerLegRight.transform.localPosition = new Vector3(0f, -0.4f, 0f);
                rendererObject.transform.SetParent(model.transform);

                mesh.vertices = new[]
                {
                    Vector3.zero,
                    Vector3.right,
                    Vector3.up,
                };
                mesh.boneWeights = new[]
                {
                    new BoneWeight { boneIndex0 = 0, weight0 = 1f },
                    new BoneWeight { boneIndex0 = 2, weight0 = 1f },
                    new BoneWeight { boneIndex0 = 4, weight0 = 1f },
                };
                var renderer = rendererObject.AddComponent<SkinnedMeshRenderer>();
                renderer.sharedMesh = mesh;
                renderer.rootBone = pelvis.transform;
                renderer.bones = new[]
                {
                    pelvis.transform,
                    customRoot.transform,
                    customChild.transform,
                    breastRoot.transform,
                    breastChild.transform,
                    singleBone.transform,
                    zeroRoot.transform,
                    zeroChild.transform,
                    thighLeft.transform,
                    lowerLegLeft.transform,
                    thighRight.transform,
                    lowerLegRight.transform,
                };

                MagicaCharacterClothModelInitializationResult result =
                    MagicaCharacterClothModelInitializer.Create(body.transform);

                Assert.IsTrue(result.Succeeded, result.Message);
                Assert.AreEqual(1, result.Cloths.Length);
                Assert.AreEqual("lower_garment", result.Cloths[0].id);
                CollectionAssert.AreEqual(
                    new[] { "000004_lod0/rig/pelvis.x/pelvis.x.001" },
                    result.Cloths[0].rootBonePaths);
                Assert.AreEqual("Assets/MagicaCloth2/Res/Preset/MC2_Preset_Cape.json", result.Cloths[0].presetAssetPath);
                Assert.IsTrue(result.Cloths[0].presetSha256.StartsWith("sha256:", StringComparison.Ordinal));
                Assert.AreEqual(0.14f, result.Cloths[0].parameterOverrides.damping.value);
                Assert.AreEqual(0.55f, result.Cloths[0].parameterOverrides.worldInertia.value);
                Assert.AreEqual(0.70f, result.Cloths[0].parameterOverrides.localInertia.value);
                Assert.AreEqual(0.55f, result.Cloths[0].parameterOverrides.movementInertiaSmoothing.value);
                Assert.IsTrue(result.Cloths[0].parameterOverrides.movementSpeedLimit.use);
                Assert.AreEqual(3.5f, result.Cloths[0].parameterOverrides.movementSpeedLimit.value);
                Assert.IsTrue(result.Cloths[0].parameterOverrides.rotationSpeedLimit.use);
                Assert.AreEqual(540f, result.Cloths[0].parameterOverrides.rotationSpeedLimit.value);
                Assert.IsTrue(result.Cloths[0].parameterOverrides.particleSpeedLimit.use);
                Assert.AreEqual(3.5f, result.Cloths[0].parameterOverrides.particleSpeedLimit.value);
                Assert.AreEqual(2, result.Cloths[0].colliders.Length);
                Assert.AreEqual("thigh_l", result.Cloths[0].colliders[0].id);
                Assert.AreEqual("Capsule", result.Cloths[0].colliders[0].type);
                Assert.Greater(result.Cloths[0].colliders[0].radius, 0f);
                Assert.AreEqual("thigh_r", result.Cloths[0].colliders[1].id);
                Assert.AreEqual("Capsule", result.Cloths[0].colliders[1].type);
                Assert.AreEqual(2, result.CreatedColliderCount);
                Assert.AreEqual(1, result.ExcludedBreastChainCount);
                Assert.AreEqual(1, result.ExcludedSingleBoneChainCount);
                Assert.AreEqual(1, result.ExcludedZeroWeightChainCount);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(mesh);
                UnityEngine.Object.DestroyImmediate(body);
            }
        }

        [Test]
        public void ModelInitializer_UpperSideClothCreatesTorsoAndMatchingArmOnly()
        {
            var body = new GameObject("BodyModel");
            try
            {
                var model = new GameObject("000004_lod0");
                var rig = new GameObject("rig");
                var root = new GameObject("root.x");
                var pelvis = new GameObject("pelvis.x");
                var spine01 = new GameObject("spine_01.x");
                var spine02 = new GameObject("spine_02.x");
                var upperChest = new GameObject("upper_chest.x");
                var neck = new GameObject("neck.x");
                var shoulderLeft = new GameObject("shoulder.l");
                var upperArmLeft = new GameObject("upper_arm.l");
                var forearmLeft = new GameObject("forearm.l");
                var shoulderRight = new GameObject("shoulder.r");
                var upperArmRight = new GameObject("upper_arm.r");
                var forearmRight = new GameObject("forearm.r");
                var accessoryRoot = new GameObject("upper_chest.x.005");

                model.transform.SetParent(body.transform);
                rig.transform.SetParent(model.transform);
                root.transform.SetParent(rig.transform);
                pelvis.transform.SetParent(root.transform);
                spine01.transform.SetParent(pelvis.transform);
                spine02.transform.SetParent(spine01.transform);
                upperChest.transform.SetParent(spine02.transform);
                neck.transform.SetParent(upperChest.transform);
                shoulderLeft.transform.SetParent(upperChest.transform);
                upperArmLeft.transform.SetParent(shoulderLeft.transform);
                forearmLeft.transform.SetParent(upperArmLeft.transform);
                shoulderRight.transform.SetParent(upperChest.transform);
                upperArmRight.transform.SetParent(shoulderRight.transform);
                forearmRight.transform.SetParent(upperArmRight.transform);
                accessoryRoot.transform.SetParent(shoulderLeft.transform);

                pelvis.transform.localPosition = new Vector3(0f, 0.8f, 0f);
                spine01.transform.localPosition = new Vector3(0f, 0.2f, 0f);
                spine02.transform.localPosition = new Vector3(0f, 0.2f, 0f);
                upperChest.transform.localPosition = new Vector3(0f, 0.2f, 0f);
                neck.transform.localPosition = new Vector3(0f, 0.25f, 0f);
                shoulderLeft.transform.localPosition = new Vector3(-0.15f, 0.1f, 0f);
                upperArmLeft.transform.localPosition = new Vector3(-0.2f, 0f, 0f);
                forearmLeft.transform.localPosition = new Vector3(-0.35f, 0f, 0f);
                shoulderRight.transform.localPosition = new Vector3(0.15f, 0.1f, 0f);
                upperArmRight.transform.localPosition = new Vector3(0.2f, 0f, 0f);
                forearmRight.transform.localPosition = new Vector3(0.35f, 0f, 0f);

                var cloth = new MagicaCharacterClothAuthoringEntry
                {
                    id = "left_upper_accessory",
                    rootBonePaths = new[]
                    {
                        "000004_lod0/rig/root.x/pelvis.x/spine_01.x/spine_02.x/upper_chest.x/shoulder.l/upper_chest.x.005",
                    },
                };

                MagicaCharacterClothAuthoringCollider[] colliders =
                    MagicaCharacterClothModelInitializer.CreateCollidersForCloth(body.transform, cloth);

                Assert.AreEqual(2, colliders.Length);
                Assert.AreEqual("torso", colliders[0].id);
                Assert.AreEqual("Capsule", colliders[0].type);
                Assert.AreEqual("upper_arm_l", colliders[1].id);
                Assert.AreEqual("Capsule", colliders[1].type);
                Assert.IsFalse(Array.Exists(colliders, value => value.id == "upper_arm_r"));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(body);
            }
        }

        [Test]
        public void PrefabPostprocessor_RequiresFullResetForMissingBonePaths()
        {
            var body = new GameObject("BodyModel");
            try
            {
                MagicaCharacterClothAuthoringDocument document = CreateValidDocument();
                document.cloths[0].rootBonePaths = new[] { "missing/root" };

                MagicaCharacterClothValidationResult validation =
                    MagicaCharacterClothAuthoringValidator.Validate(document, body.transform);

                Assert.IsTrue(validation.HasErrors);
                Assert.IsTrue(MagicaCharacterClothPrefabPostprocessor.RequiresFullModelReset(validation));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(body);
            }
        }

        [Test]
        public void MotionPreviewCleanup_RemovesOnlyOwnedOrphanedEnvironment()
        {
            var owned = new GameObject("[Cloth Motion Preview]")
            {
                layer = 31,
                hideFlags = HideFlags.DontSaveInEditor | HideFlags.DontSaveInBuild,
            };
            var unrelated = new GameObject("[Cloth Motion Preview]");
            try
            {
                int removed = MagicaCharacterClothMotionPreviewController
                    .CleanupOrphanedPreviewEnvironments();

                Assert.AreEqual(1, removed);
                Assert.IsTrue(owned == null);
                Assert.IsTrue(unrelated != null);
            }
            finally
            {
                if (owned != null) UnityEngine.Object.DestroyImmediate(owned);
                if (unrelated != null) UnityEngine.Object.DestroyImmediate(unrelated);
            }
        }

        [Test]
        public void Normalization_RepairsLegacyInlineSerializationPollution()
        {
            MagicaCharacterClothAuthoringDocument document = CreateValidDocument();
            document.schemaVersion = "1.1";
            var polluted = new MagicaCharacterClothParameterOverrides();
            foreach (System.Reflection.FieldInfo field in typeof(MagicaCharacterClothParameterOverrides)
                         .GetFields(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public))
            {
                object value = field.FieldType == typeof(MagicaCharacterClothFloatOverride)
                    ? new MagicaCharacterClothFloatOverride(0f)
                    : field.FieldType == typeof(MagicaCharacterClothLimitOverride)
                        ? new MagicaCharacterClothLimitOverride(true, 0f)
                        : field.FieldType == typeof(MagicaCharacterClothAuthoringVector)
                            ? new MagicaCharacterClothAuthoringVector()
                            : null;
                field.SetValue(polluted, value);
            }
            document.cloths[0].parameterOverrides = polluted;

            int repaired = MagicaCharacterClothAuthoringNormalization
                .RepairUnityInlineSerializationPollution(document);

            Assert.AreEqual(1, repaired);
            Assert.IsNull(document.cloths[0].parameterOverrides);
        }

        [Test]
        public void Repository_SaveUsesExpectedHashAndAtomicAcceptedHash()
        {
            string root = Path.GetFullPath("Temp/MxFrameworkTests/CharacterClothAuthoring/" + Guid.NewGuid().ToString("N"));
            string configDirectory = Path.Combine(root, "config");
            Directory.CreateDirectory(configDirectory);
            string path = Path.Combine(configDirectory, "character_cloth_magica.json");
            MagicaCharacterClothAuthoringDocument document = CreateValidDocument();
            string original = MagicaCharacterClothAuthoringSerializer.Serialize(document);
            File.WriteAllText(path, original);
            try
            {
                MagicaCharacterClothAuthoringSaveResult conflict = MagicaCharacterClothAuthoringRepository.Save(root, document, "sha256:bad");
                Assert.AreEqual(MagicaCharacterClothSaveStatus.Conflict, conflict.Status);
                Assert.AreEqual(original, File.ReadAllText(path));

                string expected = MagicaCharacterClothAuthoringRepository.ComputeTextHash(original);
                document.cloths[0].colliders[0].radius = 0.14f;
                document.cloths[0].colliders[0].localPosition = new MagicaCharacterClothAuthoringVector(1.751502e-14f, 0.05f, 5.587935e-09f);
                MagicaCharacterClothAuthoringSaveResult saved = MagicaCharacterClothAuthoringRepository.Save(root, document, expected);
                Assert.IsTrue(saved.Succeeded, saved.Message);
                Assert.AreEqual(saved.AcceptedHash, MagicaCharacterClothAuthoringRepository.ComputeFileHash(path));
                MagicaCharacterClothAuthoringCollider loadedCollider = MagicaCharacterClothAuthoringRepository.Load(root).Document.cloths[0].colliders[0];
                Assert.AreEqual(0.14f, loadedCollider.radius, 0.0001f);
                Assert.AreEqual(0f, loadedCollider.localPosition.x);
                Assert.AreEqual(0.05f, loadedCollider.localPosition.y);
                Assert.AreEqual(0f, loadedCollider.localPosition.z);
            }
            finally
            {
                if (Directory.Exists(root)) Directory.Delete(root, true);
            }
        }

        [Test]
        public void Repository_FirstSaveCreatesConfigAndDeclaresItInManifest()
        {
            string root = Path.GetFullPath("Temp/MxFrameworkTests/CharacterClothAuthoring/" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            string manifestPath = Path.Combine(root, "manifest.json");
            File.WriteAllText(manifestPath, "{\n  \"packageId\": \"cloth_test\",\n  \"configFiles\": []\n}\n");
            try
            {
                MagicaCharacterClothAuthoringSaveResult saved =
                    MagicaCharacterClothAuthoringRepository.Save(root, new MagicaCharacterClothAuthoringDocument(), string.Empty);

                Assert.IsTrue(saved.Succeeded, saved.Message);
                Assert.IsTrue(File.Exists(Path.Combine(root, "config", "character_cloth_magica.json")));
                Newtonsoft.Json.Linq.JObject manifest =
                    Newtonsoft.Json.Linq.JObject.Parse(File.ReadAllText(manifestPath));
                CollectionAssert.Contains(
                    manifest["configFiles"]?.Values<string>().ToArray(),
                    MagicaCharacterClothAuthoringRepository.ConfigRelativePath);
            }
            finally
            {
                if (Directory.Exists(root)) Directory.Delete(root, true);
            }
        }

        [Test]
        public void Normalization_ClearsFloatNoiseAndPreservesMeaningfulOffsets()
        {
            MagicaCharacterClothAuthoringDocument document = CreateValidDocument();
            MagicaCharacterClothAuthoringCollider collider = document.cloths[0].colliders[0];
            collider.localPosition = new MagicaCharacterClothAuthoringVector(1.751502e-14f, 0.05f, 5.587935e-09f);
            collider.localRotationEuler = new MagicaCharacterClothAuthoringVector(360f - 0.00001f, -0.00001f, 12f);

            MagicaCharacterClothAuthoringNormalization.Normalize(document);

            Assert.AreEqual(0f, collider.localPosition.x);
            Assert.AreEqual(0.05f, collider.localPosition.y);
            Assert.AreEqual(0f, collider.localPosition.z);
            Assert.AreEqual(0f, collider.localRotationEuler.x);
            Assert.AreEqual(0f, collider.localRotationEuler.y);
            Assert.AreEqual(12f, collider.localRotationEuler.z);
        }

        [Test]
        public void CapsuleGeometry_MatchesMagicaLengthSemantics()
        {
            Assert.AreEqual(0f, MagicaCharacterClothCapsuleGeometry.StartCenterOffset(0.25f, 0.13f));
            Assert.AreEqual(0f, MagicaCharacterClothCapsuleGeometry.EndCenterOffset(0.25f, 0.13f));
            Assert.IsTrue(MagicaCharacterClothCapsuleGeometry.HasCollapsedEnd(0.25f, 0.13f, 0.13f));
            Assert.AreEqual(0.26f, MagicaCharacterClothCapsuleGeometry.MinimumSeparatedLength(0.13f, 0.13f), 0.00001f);

            Assert.AreEqual(0.05f, MagicaCharacterClothCapsuleGeometry.StartCenterOffset(0.36f, 0.13f), 0.00001f);
            Assert.AreEqual(0.08f, MagicaCharacterClothCapsuleGeometry.EndCenterOffset(0.36f, 0.10f), 0.00001f);
            Assert.IsFalse(MagicaCharacterClothCapsuleGeometry.HasCollapsedEnd(0.36f, 0.13f, 0.10f));
        }

        [Test]
        public void MotionPreviewCatalog_ResolvesRegisteredStandaloneClips()
        {
            MagicaCharacterClothMotionPreviewCatalogResult result =
                MagicaCharacterClothMotionPreviewCatalog.Load("000001");

            Assert.IsTrue(result.Succeeded, result.Error);
            Assert.GreaterOrEqual(result.Clips.Count, 9);
            for (int i = 0; i < result.Clips.Count; i++)
            {
                Assert.IsNotNull(result.Clips[i].Clip);
                StringAssert.EndsWith(".anim", result.Clips[i].AssetPath.ToLowerInvariant());
                StringAssert.StartsWith("art.character.", result.Clips[i].RuntimeResourceKey);
            }
        }

        [Test]
        public void MotionPreviewClipMemory_ResolvesStableIdAndFallsBackOnlyWhenMissing()
        {
            var clips = new List<MagicaCharacterClothMotionPreviewClip>
            {
                new MagicaCharacterClothMotionPreviewClip
                {
                    ClipId = "locomotion.idle",
                    RuntimeResourceKey = "art.character.demo.animation.idle",
                    AssetPath = "Assets/Demo/idle.anim",
                },
                new MagicaCharacterClothMotionPreviewClip
                {
                    ClipId = "combat.idle",
                    RuntimeResourceKey = "art.character.demo.animation.combat_idle",
                    AssetPath = "Assets/Demo/combat_idle.anim",
                },
            };

            Assert.AreEqual(
                1,
                MagicaCharacterClothMotionPreviewController.ResolveRememberedClipIndex(
                    clips,
                    "combat.idle"));

            clips.Reverse();
            Assert.AreEqual(
                0,
                MagicaCharacterClothMotionPreviewController.ResolveRememberedClipIndex(
                    clips,
                    "combat.idle"));
            Assert.AreEqual(
                0,
                MagicaCharacterClothMotionPreviewController.ResolveRememberedClipIndex(
                    clips,
                    "removed.clip"));
        }

        [Test]
        public void MotionPreviewPresets_StayBoundedAndExposeTurns()
        {
            MagicaCharacterClothMotionPreviewPose forward =
                MagicaCharacterClothMotionPreviewController.EvaluateMotion(
                    MagicaCharacterClothMotionPreset.ForwardBackward,
                    0.75f);
            MagicaCharacterClothMotionPreviewPose turn =
                MagicaCharacterClothMotionPreviewController.EvaluateMotion(
                    MagicaCharacterClothMotionPreset.Turn180,
                    1f);
            MagicaCharacterClothMotionPreviewPose startStop =
                MagicaCharacterClothMotionPreviewController.EvaluateMotion(
                    MagicaCharacterClothMotionPreset.StartStop,
                    1.5f);
            MagicaCharacterClothMotionPreviewPose adjustableDistance =
                MagicaCharacterClothMotionPreviewController.EvaluateMotion(
                    MagicaCharacterClothMotionPreset.ForwardBackward,
                    1f,
                    1.5f,
                    4f,
                    1f,
                    90f);
            MagicaCharacterClothMotionPreviewPose slowTurn =
                MagicaCharacterClothMotionPreviewController.EvaluateMotion(
                    MagicaCharacterClothMotionPreset.Turn180,
                    0.25f,
                    0.8f,
                    4f,
                    1f,
                    30f);
            MagicaCharacterClothMotionPreviewPose fastTurn =
                MagicaCharacterClothMotionPreviewController.EvaluateMotion(
                    MagicaCharacterClothMotionPreset.Turn180,
                    0.25f,
                    0.8f,
                    4f,
                    1f,
                    180f);

            Assert.LessOrEqual(Mathf.Abs(forward.Position.z), 0.8f);
            Assert.Greater(Mathf.Abs(turn.Yaw), 45f);
            Assert.AreEqual(0.8f, startStop.Position.z, 0.0001f);
            Assert.AreEqual(1.5f, adjustableDistance.Position.z, 0.0001f);
            Assert.Greater(Mathf.Abs(fastTurn.Yaw), Mathf.Abs(slowTurn.Yaw));
        }

        [UnityTest]
        public System.Collections.IEnumerator MotionPreviewController_CreatesInteractivePlayModeEnvironment()
        {
            Assert.IsTrue(
                MagicaCharacterClothMotionPreviewController.PrepareRequest("000001", out string error),
                error);

            yield return new EnterPlayMode();
            float deadline = Time.realtimeSinceStartup + 8f;
            while (!MagicaCharacterClothMotionPreviewController.IsActive && Time.realtimeSinceStartup < deadline)
                yield return null;

            Assert.IsTrue(MagicaCharacterClothMotionPreviewController.IsActive,
                MagicaCharacterClothMotionPreviewController.Status);
            Assert.GreaterOrEqual(MagicaCharacterClothMotionPreviewController.ClipLabels.Length, 9);
            Assert.AreEqual(1, MagicaCharacterClothMotionPreviewController.ActiveClothCount);
            Assert.AreEqual(5, MagicaCharacterClothMotionPreviewController.ActiveColliderCount);
            Assert.IsTrue(MagicaCharacterClothMotionPreviewController.HasActiveCamera);

            MagicaCharacterClothMotionPreviewController.SetSelectedClip(2);
            MagicaCharacterClothMotionPreviewController.SetMotionPreset(MagicaCharacterClothMotionPreset.ZigZag);
            MagicaCharacterClothMotionPreviewController.SetCameraMode(
                MagicaCharacterClothMotionCameraMode.FixedWorld);
            MagicaCharacterClothMotionPreviewController.SetShowColliders(true);
            MagicaCharacterClothMotionPreviewController.SetShowMotionGuides(true);
            MagicaCharacterClothMotionPreviewController.SetPlaybackSpeed(0.5f);
            MagicaCharacterClothMotionPreviewController.SetMotionDistance(1.2f);
            MagicaCharacterClothMotionPreviewController.SetMotionPeriod(5f);
            MagicaCharacterClothMotionPreviewController.SetStopDuration(0.75f);
            MagicaCharacterClothMotionPreviewController.SetTurnSpeed(150f);
            for (int i = 0; i < 8; i++) yield return null;

            Assert.AreEqual(2, MagicaCharacterClothMotionPreviewController.SelectedClipIndex);
            Assert.AreEqual(MagicaCharacterClothMotionPreset.ZigZag,
                MagicaCharacterClothMotionPreviewController.MotionPreset);
            Assert.AreEqual(MagicaCharacterClothMotionCameraMode.FixedWorld,
                MagicaCharacterClothMotionPreviewController.CameraMode);
            Assert.IsTrue(MagicaCharacterClothMotionPreviewController.ShowColliders);
            Assert.IsTrue(MagicaCharacterClothMotionPreviewController.ShowMotionGuides);
            Assert.IsTrue(MagicaCharacterClothMotionPreviewController.HasMotionGuides);
            Assert.AreEqual(1.2f, MagicaCharacterClothMotionPreviewController.MotionDistance, 0.0001f);
            Assert.AreEqual(5f, MagicaCharacterClothMotionPreviewController.MotionPeriod, 0.0001f);
            Assert.AreEqual(0.75f, MagicaCharacterClothMotionPreviewController.StopDuration, 0.0001f);
            Assert.AreEqual(150f, MagicaCharacterClothMotionPreviewController.TurnSpeed, 0.0001f);
            Assert.Greater(MagicaCharacterClothMotionPreviewController.MotionPosition.sqrMagnitude, 0f);
            Assert.Greater(MagicaCharacterClothMotionPreviewController.NormalizedTime, 0f);

            Assert.IsFalse(MagicaCharacterClothMotionPreviewController.PreviewWindEnabled);
            MagicaCharacterClothMotionPreviewController.SetPreviewWindStrength(7f);
            MagicaCharacterClothMotionPreviewController.SetPreviewWindYaw(45f);
            MagicaCharacterClothMotionPreviewController.SetPreviewWindTurbulence(0.75f);
            MagicaCharacterClothMotionPreviewController.SetPreviewWindEnabled(true);
            yield return null;
            Assert.IsTrue(MagicaCharacterClothMotionPreviewController.HasActivePreviewWind);
            MagicaWindZone previewWind = Array.Find(
                UnityEngine.Object.FindObjectsByType<MagicaWindZone>(FindObjectsInactive.Include),
                wind => wind.name == "Cloth Motion Preview Wind");
            Assert.IsNotNull(previewWind);
            Assert.AreEqual(MagicaWindZone.Mode.GlobalDirection, previewWind.mode);
            Assert.AreEqual(7f, previewWind.main, 0.0001f);
            Assert.AreEqual(45f, previewWind.directionAngleY, 0.0001f);
            Assert.AreEqual(0.75f, previewWind.turbulence, 0.0001f);

            MagicaCharacterClothMotionPreviewController.TriggerStressPulse();
            Assert.IsTrue(MagicaCharacterClothMotionPreviewController.IsStressPulseActive);
            Assert.AreEqual(MagicaCharacterClothMotionPreset.StartStop,
                MagicaCharacterClothMotionPreviewController.MotionPreset);
            deadline = Time.realtimeSinceStartup + 3f;
            while (MagicaCharacterClothMotionPreviewController.MotionPreset
                    != MagicaCharacterClothMotionPreset.Turn180
                && Time.realtimeSinceStartup < deadline)
                yield return null;
            Assert.AreEqual(MagicaCharacterClothMotionPreset.Turn180,
                MagicaCharacterClothMotionPreviewController.MotionPreset);
            deadline = Time.realtimeSinceStartup + 3f;
            while (MagicaCharacterClothMotionPreviewController.IsStressPulseActive
                && Time.realtimeSinceStartup < deadline)
                yield return null;
            Assert.IsFalse(MagicaCharacterClothMotionPreviewController.IsStressPulseActive);
            Assert.AreEqual(MagicaCharacterClothMotionPreset.ZigZag,
                MagicaCharacterClothMotionPreviewController.MotionPreset);
            Assert.AreEqual(1.2f, MagicaCharacterClothMotionPreviewController.MotionDistance, 0.0001f);
            Assert.AreEqual(5f, MagicaCharacterClothMotionPreviewController.MotionPeriod, 0.0001f);

            MagicaCloth originalCloth =
                MagicaCharacterClothMotionPreviewController.ActiveBodyModel.GetComponentInChildren<MagicaCloth>(true);
            Assert.IsNotNull(originalCloth);
            MagicaCharacterClothAuthoringDocument liveDocument =
                MagicaCharacterClothAuthoringRepository.Load(
                    Path.GetFullPath("Authoring/CharacterPackages/character-000001")).Document;
            liveDocument.schemaVersion = "1.1";
            liveDocument.cloths[0].parameterOverrides = CreateCompleteParameterOverrides();
            liveDocument.cloths[0].colliders[0].radius = 0.18f;
            int revision = MagicaCharacterClothMotionPreviewController.HotApplyRevision;
            MagicaCharacterClothMotionPreviewController.RequestHotApply(liveDocument);
            deadline = Time.realtimeSinceStartup + 8f;
            while (MagicaCharacterClothMotionPreviewController.HotApplyRevision == revision
                && Time.realtimeSinceStartup < deadline)
                yield return null;

            Assert.Greater(MagicaCharacterClothMotionPreviewController.HotApplyRevision, revision,
                MagicaCharacterClothMotionPreviewController.HotApplyStatus);
            MagicaCloth rebuiltCloth =
                MagicaCharacterClothMotionPreviewController.ActiveBodyModel.GetComponentInChildren<MagicaCloth>(true);
            Assert.IsNotNull(rebuiltCloth);
            Assert.IsFalse(ReferenceEquals(originalCloth, rebuiltCloth));
            Assert.AreEqual(6f, rebuiltCloth.SerializeData.gravity, 0.0001f);
            Vector3 expectedGravityDirection = new Vector3(0.2f, -0.9f, 0.1f).normalized;
            Assert.AreEqual(expectedGravityDirection.x, rebuiltCloth.SerializeData.gravityDirection.x, 0.0001f);
            Assert.AreEqual(expectedGravityDirection.y, rebuiltCloth.SerializeData.gravityDirection.y, 0.0001f);
            Assert.AreEqual(expectedGravityDirection.z, rebuiltCloth.SerializeData.gravityDirection.z, 0.0001f);
            Assert.AreEqual(0.2f, rebuiltCloth.SerializeData.damping.value, 0.0001f);
            Assert.IsFalse(rebuiltCloth.SerializeData.damping.useCurve);
            Assert.AreEqual(0.85f, rebuiltCloth.SerializeData.blendWeight, 0.0001f);
            Assert.AreEqual(0.35f, rebuiltCloth.SerializeData.inertiaConstraint.worldInertia, 0.0001f);
            Assert.AreEqual(0.65f, rebuiltCloth.SerializeData.inertiaConstraint.localInertia, 0.0001f);
            Assert.AreEqual(0.55f, rebuiltCloth.SerializeData.inertiaConstraint.movementInertiaSmoothing, 0.0001f);
            Assert.IsFalse(rebuiltCloth.SerializeData.inertiaConstraint.movementSpeedLimit.use);
            Assert.AreEqual(4f, rebuiltCloth.SerializeData.inertiaConstraint.movementSpeedLimit.value, 0.0001f);
            Assert.IsTrue(rebuiltCloth.SerializeData.inertiaConstraint.rotationSpeedLimit.use);
            Assert.AreEqual(500f, rebuiltCloth.SerializeData.inertiaConstraint.rotationSpeedLimit.value, 0.0001f);
            Assert.AreEqual(3f, rebuiltCloth.SerializeData.inertiaConstraint.particleSpeedLimit.value, 0.0001f);
            Assert.AreEqual(0.75f, rebuiltCloth.SerializeData.inertiaConstraint.teleportDistance, 0.0001f);
            Assert.AreEqual(120f, rebuiltCloth.SerializeData.inertiaConstraint.teleportRotation, 0.0001f);
            Assert.AreEqual(0.8f, rebuiltCloth.SerializeData.distanceConstraint.stiffness.value, 0.0001f);
            Assert.IsTrue(rebuiltCloth.SerializeData.angleRestorationConstraint.useAngleRestoration);
            Assert.AreEqual(0.25f, rebuiltCloth.SerializeData.angleRestorationConstraint.stiffness.value, 0.0001f);
            Assert.AreEqual(0.6f, rebuiltCloth.SerializeData.angleRestorationConstraint.velocityAttenuation, 0.0001f);
            Assert.IsTrue(rebuiltCloth.SerializeData.angleLimitConstraint.useAngleLimit);
            Assert.AreEqual(45f, rebuiltCloth.SerializeData.angleLimitConstraint.limitAngle.value, 0.0001f);
            Assert.IsTrue(rebuiltCloth.SerializeData.motionConstraint.useMaxDistance);
            Assert.AreEqual(0.4f, rebuiltCloth.SerializeData.motionConstraint.maxDistance.value, 0.0001f);
            Assert.AreEqual(0.2f, rebuiltCloth.SerializeData.colliderCollisionConstraint.friction, 0.0001f);
            Assert.AreEqual(0.08f, rebuiltCloth.SerializeData.colliderCollisionConstraint.limitDistance.value, 0.0001f);
            Assert.AreEqual(0.015f, rebuiltCloth.SerializeData.selfCollisionConstraint.surfaceThickness.value, 0.0001f);
            Assert.AreEqual(1.2f, rebuiltCloth.SerializeData.wind.influence, 0.0001f);
            Assert.AreEqual(1.4f, rebuiltCloth.SerializeData.wind.frequency, 0.0001f);
            Assert.AreEqual(0.8f, rebuiltCloth.SerializeData.wind.turbulence, 0.0001f);
            Assert.IsTrue(rebuiltCloth.SerializeData.springConstraint.useSpring);
            Assert.AreEqual(0.15f, rebuiltCloth.SerializeData.springConstraint.springPower, 0.0001f);
            Assert.AreEqual(0.12f, rebuiltCloth.SerializeData.springConstraint.limitDistance, 0.0001f);
            Assert.IsTrue(MagicaCharacterClothMotionPreviewController.TryGetActiveClothData(
                liveDocument.cloths[0].id,
                out ClothSerializeData runtimeData));
            Assert.AreSame(rebuiltCloth.SerializeData, runtimeData);
            MagicaSphereCollider rebuiltFirstCollider = Array.Find(
                MagicaCharacterClothMotionPreviewController.ActiveBodyModel.GetComponentsInChildren<MagicaSphereCollider>(true),
                collider => collider.name.Contains(liveDocument.cloths[0].colliders[0].id));
            Assert.IsNotNull(rebuiltFirstCollider);
            Assert.AreEqual(0.18f, rebuiltFirstCollider.GetSize().x, 0.0001f);

            MagicaCharacterClothAuthoringEntry liveCloth = liveDocument.cloths[0];
            MagicaCharacterClothAuthoringCollider[] expanded = liveCloth.colliders;
            Array.Resize(ref expanded, expanded.Length + 1);
            expanded[expanded.Length - 1] = new MagicaCharacterClothAuthoringCollider
            {
                id = "neck.live",
                type = "Sphere",
                parentBonePath = liveCloth.colliders[0].parentBonePath,
                localPosition = new MagicaCharacterClothAuthoringVector(0f, -0.12f, 0f),
                radius = 0.07f,
                direction = "Y",
            };
            liveCloth.colliders = expanded;
            revision = MagicaCharacterClothMotionPreviewController.HotApplyRevision;
            MagicaCharacterClothMotionPreviewController.RequestHotApply(liveDocument);
            deadline = Time.realtimeSinceStartup + 8f;
            while (MagicaCharacterClothMotionPreviewController.HotApplyRevision == revision
                && Time.realtimeSinceStartup < deadline)
                yield return null;
            Assert.Greater(MagicaCharacterClothMotionPreviewController.HotApplyRevision, revision,
                MagicaCharacterClothMotionPreviewController.HotApplyStatus);
            Assert.AreEqual(6, MagicaCharacterClothMotionPreviewController.ActiveColliderCount);

            MagicaCharacterClothAuthoringDocument invalidDocument = liveDocument.Clone();
            invalidDocument.cloths[0].parameterOverrides.damping.value = 2f;
            revision = MagicaCharacterClothMotionPreviewController.HotApplyRevision;
            MagicaCharacterClothMotionPreviewController.RequestHotApply(invalidDocument);
            deadline = Time.realtimeSinceStartup + 3f;
            while (MagicaCharacterClothMotionPreviewController.IsHotApplyPending
                && Time.realtimeSinceStartup < deadline)
                yield return null;
            Assert.AreEqual(revision, MagicaCharacterClothMotionPreviewController.HotApplyRevision);
            StringAssert.Contains("热应用已阻断", MagicaCharacterClothMotionPreviewController.HotApplyStatus);
            Assert.AreEqual(6, MagicaCharacterClothMotionPreviewController.ActiveColliderCount);

            yield return new ExitPlayMode();
        }

        [Test]
        public void NumericCharacterDocument_ValidatesAgainstGeneratedBodyModel()
        {
            string packagePath = Path.GetFullPath("Authoring/CharacterPackages/character-000001");
            MagicaCharacterClothAuthoringLoadResult load = MagicaCharacterClothAuthoringRepository.Load(packagePath);
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/MxFrameworkGenerated/CharacterPackages/000001/prefabs/000001_character_preview.prefab");
            Assert.IsNotNull(prefab);
            Transform body = Array.Find(prefab.GetComponentsInChildren<Transform>(true), x => x.name == "BodyModel");
            Assert.IsNotNull(body);
            MagicaCharacterClothValidationResult validation = MagicaCharacterClothAuthoringValidator.Validate(load.Document, body);
            Assert.AreEqual(0, validation.ErrorCount, validation.Summary);
        }

        private static MagicaCharacterClothAuthoringDocument CreateValidDocument()
        {
            return new MagicaCharacterClothAuthoringDocument
            {
                cloths = new[]
                {
                    new MagicaCharacterClothAuthoringEntry
                    {
                        id = "hair",
                        rootBonePaths = new[] { "root/hair" },
                        presetAssetPath = "Assets/MagicaCloth2/Res/Preset/MC2_Preset_Tail.json",
                        presetSha256 = "sha256:84399b8b65a155eadda37d373a27d89e268dfd987dbdb8f4cef21a194cf39dcf",
                        colliders = new[]
                        {
                            new MagicaCharacterClothAuthoringCollider
                            {
                                id = "head", type = "Sphere", parentBonePath = "root/head", radius = 0.1f,
                            }
                        }
                    }
                }
            };
        }

        private static MagicaCharacterClothParameterOverrides CreateCompleteParameterOverrides()
        {
            return new MagicaCharacterClothParameterOverrides
            {
                gravity = new MagicaCharacterClothFloatOverride(6f),
                gravityDirection = new MagicaCharacterClothAuthoringVector(0.2f, -0.9f, 0.1f),
                damping = new MagicaCharacterClothFloatOverride(0.2f),
                blendWeight = new MagicaCharacterClothFloatOverride(0.85f),
                worldInertia = new MagicaCharacterClothFloatOverride(0.35f),
                localInertia = new MagicaCharacterClothFloatOverride(0.65f),
                movementInertiaSmoothing = new MagicaCharacterClothFloatOverride(0.55f),
                movementSpeedLimit = new MagicaCharacterClothLimitOverride(false, 4f),
                rotationSpeedLimit = new MagicaCharacterClothLimitOverride(true, 500f),
                particleSpeedLimit = new MagicaCharacterClothLimitOverride(true, 3f),
                teleportDistance = new MagicaCharacterClothFloatOverride(0.75f),
                teleportRotation = new MagicaCharacterClothFloatOverride(120f),
                distanceStiffness = new MagicaCharacterClothFloatOverride(0.8f),
                angleRestorationStiffness = new MagicaCharacterClothLimitOverride(true, 0.25f),
                velocityAttenuation = new MagicaCharacterClothFloatOverride(0.6f),
                angleLimit = new MagicaCharacterClothLimitOverride(true, 45f),
                maxDistance = new MagicaCharacterClothLimitOverride(true, 0.4f),
                collisionFriction = new MagicaCharacterClothFloatOverride(0.2f),
                collisionLimitDistance = new MagicaCharacterClothFloatOverride(0.08f),
                surfaceThickness = new MagicaCharacterClothFloatOverride(0.015f),
                windInfluence = new MagicaCharacterClothFloatOverride(1.2f),
                windFrequency = new MagicaCharacterClothFloatOverride(1.4f),
                windTurbulence = new MagicaCharacterClothFloatOverride(0.8f),
                springPower = new MagicaCharacterClothLimitOverride(true, 0.15f),
                springLimitDistance = new MagicaCharacterClothFloatOverride(0.12f),
            };
        }
    }
}
