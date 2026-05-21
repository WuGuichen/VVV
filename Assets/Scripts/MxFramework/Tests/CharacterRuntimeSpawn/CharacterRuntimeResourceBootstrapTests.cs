using MxFramework.Animation;
using MxFramework.Animation.Unity;
using MxFramework.CharacterControl;
using MxFramework.CharacterRuntimeSpawn.Unity;
using MxFramework.Resources;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace MxFramework.Tests.CharacterRuntimeSpawn
{
    public sealed class CharacterRuntimeResourceBootstrapTests
    {
        [Test]
        public void LoadCharacter_LoadsCharacterAndDefaultWeaponThroughResourceManager()
        {
            var bootstrapObject = new GameObject("bootstrap");
            var characterTemplate = new GameObject("character_template");
            var sockets = new GameObject("Sockets").transform;
            var socket = new GameObject("Socket_mainHand").transform;
            var weaponTemplate = new GameObject("weapon_template");
            try
            {
                sockets.SetParent(characterTemplate.transform, false);
                socket.SetParent(sockets, false);
                CharacterRuntimeControllerBinding controllerBinding = characterTemplate.AddComponent<CharacterRuntimeControllerBinding>();
                ConfigureControllerBinding(controllerBinding);
                characterTemplate.AddComponent<CharacterRuntimeInputMotionController>().EnableInputMotion = false;
                CharacterDefaultEquipmentRuntimeBinder binder = characterTemplate.AddComponent<CharacterDefaultEquipmentRuntimeBinder>();
                ConfigureBinder(binder, sockets, socket);

                CharacterRuntimeResourceBootstrap bootstrap = bootstrapObject.AddComponent<CharacterRuntimeResourceBootstrap>();
                ConfigureBootstrap(bootstrap, characterTemplate, weaponTemplate);

                Assert.IsTrue(bootstrap.LoadCharacter());

                CharacterDefaultEquipmentRuntimeBinder runtimeBinder =
                    bootstrap.CharacterInstance.GetComponent<CharacterDefaultEquipmentRuntimeBinder>();
                Assert.NotNull(runtimeBinder);
                Assert.AreEqual(1, runtimeBinder.SpawnedWeapons.Count);
                Assert.AreEqual("DefaultWeapon_mainHand_weapon.iron_sword", runtimeBinder.SpawnedWeapons[0].name);
                Assert.AreSame(runtimeBinder.SpawnedWeapons[0].transform.parent, runtimeBinder.DefaultWeapons[0].SocketTransform);

                CharacterRuntimeControllerBinding runtimeController =
                    bootstrap.CharacterInstance.GetComponent<CharacterRuntimeControllerBinding>();
                Assert.NotNull(runtimeController);
                Assert.IsTrue(runtimeController.IsInitialized);
                Assert.AreEqual(CharacterControlState.Locomotion, runtimeController.StateMachine.CurrentState);
                Assert.AreEqual(1001, runtimeController.StateMachine.Entity.StableId);
                Assert.NotNull(bootstrap.CharacterInstance.GetComponent<CharacterRuntimeInputMotionController>());
            }
            finally
            {
                Object.DestroyImmediate(bootstrapObject);
                Object.DestroyImmediate(characterTemplate);
                Object.DestroyImmediate(weaponTemplate);
            }
        }

        [Test]
        public void EnsureResourceManager_AllowsResourcesProviderEntriesWithoutSceneAssetReferences()
        {
            var bootstrapObject = new GameObject("bootstrap");
            try
            {
                CharacterRuntimeResourceBootstrap bootstrap = bootstrapObject.AddComponent<CharacterRuntimeResourceBootstrap>();
                var serialized = new SerializedObject(bootstrap);
                serialized.FindProperty("_catalogId").stringValue = "character.runtime.resources.test";
                serialized.FindProperty("_packageId").stringValue = "test_package";
                SerializedProperty resources = serialized.FindProperty("_resources");
                resources.arraySize = 1;
                SerializedProperty resource = resources.GetArrayElementAtIndex(0);
                resource.FindPropertyRelative("_id").stringValue = "char.test.prefab.character_preview";
                resource.FindPropertyRelative("_typeId").stringValue = ResourceTypeIds.GameObject;
                resource.FindPropertyRelative("_providerId").stringValue = "resources";
                resource.FindPropertyRelative("_variant").stringValue = "default";
                resource.FindPropertyRelative("_packageId").stringValue = "test_package";
                resource.FindPropertyRelative("_address").stringValue = "MxFrameworkGenerated/CharacterPackages/test/prefabs/character";
                resource.FindPropertyRelative("_asset").objectReferenceValue = null;
                serialized.ApplyModifiedPropertiesWithoutUndo();

                bootstrap.EnsureResourceManager();

                ResourceDebugSnapshot snapshot = bootstrap.ResourceManager.CreateDebugSnapshot();
                Assert.AreEqual(1, snapshot.ProviderCount);
                Assert.AreEqual(1, snapshot.CatalogCount);
                Assert.AreEqual(1, snapshot.EntryCount);
            }
            finally
            {
                Object.DestroyImmediate(bootstrapObject);
            }
        }

        [Test]
        public void EnsureResourceManager_UsesMemoryProviderWhenSceneAssetReferenceIsProvided()
        {
            var bootstrapObject = new GameObject("bootstrap");
            var characterTemplate = new GameObject("character_template");
            var weaponTemplate = new GameObject("weapon_template");
            try
            {
                CharacterRuntimeResourceBootstrap bootstrap = bootstrapObject.AddComponent<CharacterRuntimeResourceBootstrap>();
                ConfigureBootstrap(bootstrap, characterTemplate, weaponTemplate);

                bootstrap.EnsureResourceManager();

                ResourceDebugSnapshot snapshot = bootstrap.ResourceManager.CreateDebugSnapshot();
                Assert.AreEqual(1, snapshot.ProviderCount);
                Assert.AreEqual(2, snapshot.EntryCount);
            }
            finally
            {
                Object.DestroyImmediate(bootstrapObject);
                Object.DestroyImmediate(characterTemplate);
                Object.DestroyImmediate(weaponTemplate);
            }
        }

        [Test]
        public void LoadCharacter_WarmsCompiledAnimationResourcesBeforeSpawn()
        {
            var bootstrapObject = new GameObject("bootstrap");
            var characterTemplate = new GameObject("character_template");
            var sockets = new GameObject("Sockets").transform;
            var socket = new GameObject("Socket_mainHand").transform;
            var weaponTemplate = new GameObject("weapon_template");
            var idleClip = new AnimationClip();
            var setDefinition = new TextAsset(AnimationSetDefinitionJson);
            var clipRegistry = new TextAsset(AnimationClipRegistryJson);
            try
            {
                sockets.SetParent(characterTemplate.transform, false);
                socket.SetParent(sockets, false);
                CharacterRuntimeControllerBinding controllerBinding = characterTemplate.AddComponent<CharacterRuntimeControllerBinding>();
                ConfigureControllerBinding(controllerBinding);
                characterTemplate.AddComponent<CharacterRuntimeInputMotionController>().EnableInputMotion = false;
                CharacterDefaultEquipmentRuntimeBinder binder = characterTemplate.AddComponent<CharacterDefaultEquipmentRuntimeBinder>();
                ConfigureBinder(binder, sockets, socket);

                CharacterRuntimeResourceBootstrap bootstrap = bootstrapObject.AddComponent<CharacterRuntimeResourceBootstrap>();
                ConfigureBootstrap(bootstrap, characterTemplate, weaponTemplate);
                ConfigureAnimationWarmup(bootstrap, idleClip, setDefinition, clipRegistry);

                Assert.IsTrue(bootstrap.LoadCharacter());

                Assert.NotNull(bootstrap.AnimationWarmupResult);
                Assert.IsTrue(bootstrap.AnimationWarmupResult.Success, FormatWarmupIssues(bootstrap.AnimationWarmupResult));
                CollectionAssert.Contains(
                    bootstrap.AnimationWarmupResult.RequiredKeys,
                    new ResourceKey("char.test.anim.idle", ResourceTypeIds.AnimationClip, string.Empty, "test_package"));
                Assert.GreaterOrEqual(bootstrap.ResourceManager.CreateDebugSnapshot().LoadedCount, 3);
            }
            finally
            {
                Object.DestroyImmediate(bootstrapObject);
                Object.DestroyImmediate(characterTemplate);
                Object.DestroyImmediate(weaponTemplate);
                Object.DestroyImmediate(idleClip);
                Object.DestroyImmediate(setDefinition);
                Object.DestroyImmediate(clipRegistry);
            }
        }

        [Test]
        public void LoadCharacter_WarmedRuntimeCatalogClipsCanBindAndPlayThroughUnityPlayables()
        {
            var bootstrapObject = new GameObject("bootstrap");
            var characterTemplate = new GameObject("character_template");
            var sockets = new GameObject("Sockets").transform;
            var socket = new GameObject("Socket_mainHand").transform;
            var weaponTemplate = new GameObject("weapon_template");
            var idleClip = new AnimationClip { name = "Idle" };
            var walkClip = new AnimationClip { name = "Walk" };
            var runClip = new AnimationClip { name = "Run" };
            var attackClip = new AnimationClip { name = "Attack" };
            var setDefinition = new TextAsset(AnimationPlaybackSetDefinitionJson);
            var clipRegistry = new TextAsset(AnimationPlaybackClipRegistryJson);
            UnityPlayablesAnimationBackend backend = null;
            try
            {
                characterTemplate.AddComponent<Animator>();
                sockets.SetParent(characterTemplate.transform, false);
                socket.SetParent(sockets, false);
                CharacterRuntimeControllerBinding controllerBinding = characterTemplate.AddComponent<CharacterRuntimeControllerBinding>();
                ConfigureControllerBinding(controllerBinding);
                characterTemplate.AddComponent<CharacterRuntimeInputMotionController>().EnableInputMotion = false;
                CharacterDefaultEquipmentRuntimeBinder binder = characterTemplate.AddComponent<CharacterDefaultEquipmentRuntimeBinder>();
                ConfigureBinder(binder, sockets, socket);

                CharacterRuntimeResourceBootstrap bootstrap = bootstrapObject.AddComponent<CharacterRuntimeResourceBootstrap>();
                ConfigureBootstrap(bootstrap, characterTemplate, weaponTemplate);
                ConfigureAnimationPlaybackWarmup(bootstrap, idleClip, walkClip, runClip, attackClip, setDefinition, clipRegistry);

                Assert.IsTrue(bootstrap.LoadCharacter());
                Assert.IsTrue(bootstrap.AnimationWarmupResult.Success, FormatWarmupIssues(bootstrap.AnimationWarmupResult));
                Assert.AreEqual(4, bootstrap.AnimationWarmupResult.RequiredKeys.Count);
                Assert.AreEqual(6, bootstrap.ResourceManager.CreateDebugSnapshot().LoadedCount);

                MxAnimationSetDefinition definition =
                    MxAnimationCompiledArtifactJson.LoadSetDefinitions(setDefinition.text, "test_package")[0];
                backend = new UnityPlayablesAnimationBackend(
                    bootstrap.CharacterInstance.GetComponent<Animator>(),
                    bootstrap.ResourceManager,
                    definition,
                    "actor.test");

                MxAnimationBackendResult blend = backend.SetBlend1D(new MxAnimationBlend1DRequest
                {
                    BlendId = "locomotion",
                    Parameter = new MxAnimationQuantizedParameter("locomotion.speed", 750),
                    CorrelationId = "speed:750"
                });
                Assert.IsTrue(blend.Success, blend.Message);

                MxAnimationBackendResult action = backend.Play(new MxAnimationPlayRequest
                {
                    ClipKey = new ResourceKey("char.test.anim.attack", ResourceTypeIds.AnimationClip, string.Empty, "test_package"),
                    LayerId = new MxAnimationLayerId("upper_body"),
                    CorrelationId = "action:attack"
                });
                Assert.IsTrue(action.Success, action.Message);

                MxAnimationDiagnosticSnapshot snapshot = backend.CreateSnapshot();
                MxAnimationLayerDiagnostic baseLayer = FindLayer(snapshot, MxAnimationLayerId.Base);
                MxAnimationLayerDiagnostic upperLayer = FindLayer(snapshot, new MxAnimationLayerId("upper_body"));
                Assert.AreEqual("locomotion", baseLayer.Blend1DId);
                Assert.AreEqual(2, baseLayer.ActivePlayableCount);
                Assert.AreEqual(new ResourceKey("char.test.anim.attack", ResourceTypeIds.AnimationClip, string.Empty, "test_package"), upperLayer.CurrentClipKey);
                Assert.AreEqual(6, bootstrap.ResourceManager.CreateDebugSnapshot().LoadedCount);
                Assert.GreaterOrEqual(bootstrap.ResourceManager.CreateDebugSnapshot().TotalRefCount, 11);

                bootstrap.WarmupAnimationResources();
                Assert.IsTrue(bootstrap.AnimationWarmupResult.Success, FormatWarmupIssues(bootstrap.AnimationWarmupResult));
                Assert.AreEqual(6, bootstrap.ResourceManager.CreateDebugSnapshot().LoadedCount);
            }
            finally
            {
                backend?.Release();
                Object.DestroyImmediate(bootstrapObject);
                Object.DestroyImmediate(characterTemplate);
                Object.DestroyImmediate(weaponTemplate);
                Object.DestroyImmediate(idleClip);
                Object.DestroyImmediate(walkClip);
                Object.DestroyImmediate(runClip);
                Object.DestroyImmediate(attackClip);
                Object.DestroyImmediate(setDefinition);
                Object.DestroyImmediate(clipRegistry);
            }
        }

        private static void ConfigureBinder(CharacterDefaultEquipmentRuntimeBinder binder, Transform socketsRoot, Transform socket)
        {
            var serialized = new SerializedObject(binder);
            serialized.FindProperty("_socketsRoot").objectReferenceValue = socketsRoot;
            serialized.FindProperty("_instantiateDefaultWeaponsOnAwake").boolValue = false;
            SerializedProperty weapons = serialized.FindProperty("_defaultWeapons");
            weapons.arraySize = 1;
            SerializedProperty item = weapons.GetArrayElementAtIndex(0);
            item.FindPropertyRelative("_weaponId").stringValue = "weapon.iron_sword";
            item.FindPropertyRelative("_equipSlot").stringValue = "mainHand";
            item.FindPropertyRelative("_socketId").stringValue = "mainHand";
            item.FindPropertyRelative("_resourceId").stringValue = "char.test.prefab.weapon.mainhand.weapon_iron_sword";
            item.FindPropertyRelative("_resourceTypeId").stringValue = ResourceTypeIds.GameObject;
            item.FindPropertyRelative("_resourceVariant").stringValue = "default";
            item.FindPropertyRelative("_resourcePackageId").stringValue = "test_package";
            item.FindPropertyRelative("_socketTransform").objectReferenceValue = socket;
            item.FindPropertyRelative("_prefab").objectReferenceValue = null;
            item.FindPropertyRelative("_localPosition").vector3Value = Vector3.zero;
            item.FindPropertyRelative("_localRotation").quaternionValue = Quaternion.identity;
            item.FindPropertyRelative("_localScale").vector3Value = Vector3.one;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void ConfigureControllerBinding(CharacterRuntimeControllerBinding binding)
        {
            var serialized = new SerializedObject(binding);
            serialized.FindProperty("_stableCharacterId").intValue = 1001;
            serialized.FindProperty("_gameplayEntityIndex").intValue = 1001;
            serialized.FindProperty("_gameplayEntityGeneration").intValue = 1;
            serialized.FindProperty("_combatEntityId").intValue = 1001;
            serialized.FindProperty("_combatBodyId").intValue = 1001;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void ConfigureBootstrap(
            CharacterRuntimeResourceBootstrap bootstrap,
            GameObject characterTemplate,
            GameObject weaponTemplate)
        {
            var serialized = new SerializedObject(bootstrap);
            serialized.FindProperty("_catalogId").stringValue = "character.runtime.test";
            serialized.FindProperty("_packageId").stringValue = "test_package";
            serialized.FindProperty("_characterResourceId").stringValue = "char.test.prefab.character_preview";
            serialized.FindProperty("_characterResourceVariant").stringValue = "default";
            serialized.FindProperty("_loadOnStart").boolValue = false;

            SerializedProperty resources = serialized.FindProperty("_resources");
            resources.arraySize = 2;
            SetResource(resources.GetArrayElementAtIndex(0), "char.test.prefab.character_preview", "test/character", characterTemplate);
            SetResource(resources.GetArrayElementAtIndex(1), "char.test.prefab.weapon.mainhand.weapon_iron_sword", "test/weapon", weaponTemplate);
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void ConfigureAnimationWarmup(
            CharacterRuntimeResourceBootstrap bootstrap,
            AnimationClip idleClip,
            TextAsset setDefinition,
            TextAsset clipRegistry)
        {
            var serialized = new SerializedObject(bootstrap);
            serialized.FindProperty("_warmupAnimationOnLoad").boolValue = true;
            serialized.FindProperty("_animationSetDefinitionJson").objectReferenceValue = setDefinition;
            serialized.FindProperty("_animationClipRegistryJson").objectReferenceValue = clipRegistry;
            serialized.FindProperty("_animationSetId").stringValue = "set.base";

            SerializedProperty resources = serialized.FindProperty("_resources");
            resources.arraySize = 3;
            SetResource(
                resources.GetArrayElementAtIndex(2),
                "char.test.anim.idle",
                ResourceTypeIds.AnimationClip,
                string.Empty,
                "test/idle",
                idleClip);
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void ConfigureAnimationPlaybackWarmup(
            CharacterRuntimeResourceBootstrap bootstrap,
            AnimationClip idleClip,
            AnimationClip walkClip,
            AnimationClip runClip,
            AnimationClip attackClip,
            TextAsset setDefinition,
            TextAsset clipRegistry)
        {
            var serialized = new SerializedObject(bootstrap);
            serialized.FindProperty("_warmupAnimationOnLoad").boolValue = true;
            serialized.FindProperty("_animationSetDefinitionJson").objectReferenceValue = setDefinition;
            serialized.FindProperty("_animationClipRegistryJson").objectReferenceValue = clipRegistry;
            serialized.FindProperty("_animationSetId").stringValue = "set.base";

            SerializedProperty resources = serialized.FindProperty("_resources");
            resources.arraySize = 6;
            SetResource(
                resources.GetArrayElementAtIndex(2),
                "char.test.anim.idle",
                ResourceTypeIds.AnimationClip,
                string.Empty,
                "test/animations/idle",
                idleClip);
            SetResource(
                resources.GetArrayElementAtIndex(3),
                "char.test.anim.walk",
                ResourceTypeIds.AnimationClip,
                string.Empty,
                "test/animations/walk",
                walkClip);
            SetResource(
                resources.GetArrayElementAtIndex(4),
                "char.test.anim.run",
                ResourceTypeIds.AnimationClip,
                string.Empty,
                "test/animations/run",
                runClip);
            SetResource(
                resources.GetArrayElementAtIndex(5),
                "char.test.anim.attack",
                ResourceTypeIds.AnimationClip,
                string.Empty,
                "test/animations/attack",
                attackClip);
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetResource(SerializedProperty property, string id, string address, Object asset)
        {
            SetResource(property, id, ResourceTypeIds.GameObject, "default", address, asset);
        }

        private static void SetResource(SerializedProperty property, string id, string typeId, string variant, string address, Object asset)
        {
            property.FindPropertyRelative("_id").stringValue = id;
            property.FindPropertyRelative("_typeId").stringValue = typeId;
            property.FindPropertyRelative("_variant").stringValue = variant;
            property.FindPropertyRelative("_packageId").stringValue = "test_package";
            property.FindPropertyRelative("_address").stringValue = address;
            property.FindPropertyRelative("_asset").objectReferenceValue = asset;
        }

        private static string FormatWarmupIssues(MxFramework.Animation.MxAnimationWarmupResult result)
        {
            if (result == null)
                return "missing";
            var messages = new System.Collections.Generic.List<string>();
            for (int i = 0; i < result.Issues.Count; i++)
                messages.Add(result.Issues[i].Code + ":" + result.Issues[i].Message);
            return string.Join("; ", messages);
        }

        private static MxAnimationLayerDiagnostic FindLayer(MxAnimationDiagnosticSnapshot snapshot, MxAnimationLayerId layerId)
        {
            for (int i = 0; i < snapshot.LayerStates.Count; i++)
            {
                if (snapshot.LayerStates[i].LayerId == layerId)
                    return snapshot.LayerStates[i];
            }

            Assert.Fail("Expected layer diagnostic for " + layerId + ".");
            return null;
        }

        private const string AnimationSetDefinitionJson = @"{
  ""format"": ""mx.animationSetDefinition.v1"",
  ""schemaVersion"": ""1.0"",
  ""packageId"": ""animation.test"",
  ""sets"": [
    {
      ""setId"": ""set.base"",
      ""version"": ""1.0"",
      ""defaultClipId"": ""idle"",
      ""fallbackClipId"": ""idle"",
      ""groups"": [
        {
          ""groupId"": ""locomotion"",
          ""clips"": [
            {
              ""clipId"": ""idle"",
              ""runtimeResourceKey"": ""char.test.anim.idle"",
              ""loop"": true,
              ""speed"": 1
            }
          ]
        }
      ],
      ""actionBindings"": []
    }
  ]
}";

        private const string AnimationClipRegistryJson = @"{
  ""format"": ""mx.animationClipRegistry.v1"",
  ""schemaVersion"": ""1.0"",
  ""packageId"": ""animation.test"",
  ""clips"": [
    {
      ""setId"": ""set.base"",
      ""groupId"": ""locomotion"",
      ""clipId"": ""idle"",
      ""runtimeResourceKey"": ""char.test.anim.idle""
    }
  ]
}";

        private const string AnimationPlaybackSetDefinitionJson = @"{
  ""format"": ""mx.animationSetDefinition.v1"",
  ""schemaVersion"": ""1.0"",
  ""packageId"": ""animation.test"",
  ""sets"": [
    {
      ""setId"": ""set.base"",
      ""version"": ""1.0"",
      ""defaultClipId"": ""idle"",
      ""fallbackClipId"": ""idle"",
      ""groups"": [
        {
          ""groupId"": ""locomotion"",
          ""clips"": [
            {
              ""clipId"": ""idle"",
              ""runtimeResourceKey"": ""char.test.anim.idle"",
              ""loop"": true,
              ""speed"": 1
            },
            {
              ""clipId"": ""walk"",
              ""runtimeResourceKey"": ""char.test.anim.walk"",
              ""loop"": true,
              ""speed"": 1
            },
            {
              ""clipId"": ""run"",
              ""runtimeResourceKey"": ""char.test.anim.run"",
              ""loop"": true,
              ""speed"": 1
            },
            {
              ""clipId"": ""attack"",
              ""runtimeResourceKey"": ""char.test.anim.attack"",
              ""loop"": false,
              ""speed"": 1
            }
          ],
          ""blend1D"": [
            {
              ""blendId"": ""locomotion"",
              ""parameter"": ""locomotion.speed"",
              ""points"": [
                { ""value"": 0, ""clipId"": ""idle"" },
                { ""value"": 0.5, ""clipId"": ""walk"" },
                { ""value"": 1, ""clipId"": ""run"" }
              ]
            }
          ]
        }
      ],
      ""layers"": [
        {
          ""layerId"": ""base"",
          ""weight"": 1
        },
        {
          ""layerId"": ""upper_body"",
          ""purpose"": ""humanoid.upper"",
          ""weight"": 1
        }
      ],
      ""actionBindings"": [
        {
          ""bindingId"": ""attack"",
          ""actionId"": ""action:attack"",
          ""clipId"": ""attack"",
          ""loop"": false,
          ""speed"": 1
        }
      ]
    }
  ]
}";

        private const string AnimationPlaybackClipRegistryJson = @"{
  ""format"": ""mx.animationClipRegistry.v1"",
  ""schemaVersion"": ""1.0"",
  ""packageId"": ""animation.test"",
  ""clips"": [
    {
      ""setId"": ""set.base"",
      ""groupId"": ""locomotion"",
      ""clipId"": ""idle"",
      ""runtimeResourceKey"": ""char.test.anim.idle""
    },
    {
      ""setId"": ""set.base"",
      ""groupId"": ""locomotion"",
      ""clipId"": ""walk"",
      ""runtimeResourceKey"": ""char.test.anim.walk""
    },
    {
      ""setId"": ""set.base"",
      ""groupId"": ""locomotion"",
      ""clipId"": ""run"",
      ""runtimeResourceKey"": ""char.test.anim.run""
    },
    {
      ""setId"": ""set.base"",
      ""groupId"": ""locomotion"",
      ""clipId"": ""attack"",
      ""runtimeResourceKey"": ""char.test.anim.attack""
    }
  ]
}";
    }
}
