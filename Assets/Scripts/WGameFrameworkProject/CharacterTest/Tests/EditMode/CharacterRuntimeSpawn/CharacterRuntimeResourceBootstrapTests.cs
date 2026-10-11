using System.Collections.Generic;
using System.IO;
using MxFramework.Animation;
using MxFramework.Animation.Unity;
using MxFramework.CharacterApplication;
using MxFramework.CharacterControl;
using MxFramework.CharacterRuntimeSpawn;
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
                ConfigureSlimCharacterTemplate(characterTemplate);

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
        public void Compose_CreatesMissingSocketMarkerForRuntimeBoneSync()
        {
            var characterTemplate = new GameObject("character_template");
            try
            {
                ConfigureSlimCharacterTemplate(characterTemplate);
                Transform modelRoot = characterTemplate.transform.Find("ModelRoot");
                Transform footBone = new GameObject("LeftFoot").transform;
                footBone.SetParent(modelRoot, false);
                footBone.localPosition = new Vector3(0.2f, 0.4f, 0.1f);
                footBone.localRotation = Quaternion.Euler(0f, 25f, 0f);

                CharacterImportedPackage package = CreateRuntimePackage(new[]
                {
                    new CharacterSocketRuntimeBinding(
                        MxFramework.Game.CharacterTest.CharacterTestAuthoredContentIds.D400_Id50,
                        "foot_l",
                        "bone.foot.L",
                        string.Empty,
                        new CharacterRuntimePose(
                            "Bone",
                            "bone.foot.L",
                            new CharacterRuntimeVector3(0f, -0.02f, 0.04f),
                            new CharacterRuntimeQuaternion(0f, 0f, 0f, 1f),
                            new CharacterRuntimeVector3(1f, 1f, 1f)),
                        "Gameplay")
                });
                CharacterRuntimeSpawnResult spawnResult = CharacterRuntimeSpawnResolver.Resolve(package, default);
                Assert.IsTrue(spawnResult.IsSuccess);

                CharacterRuntimePrefabCompositionResult composition = CharacterRuntimePrefabComposer.Compose(
                    characterTemplate,
                    new CharacterRuntimePrefabCompositionOptions(package, spawnResult)
                    {
                        InstallDefaultEquipmentBinder = false,
                        InstallInputMotionController = false,
                        EnableInputMotion = false,
                        UseExternalRuntimeTick = true
                    });

                Assert.IsTrue(composition.Success, string.Join(" | ", composition.Issues));
                Assert.NotNull(composition.BoneSync);
                Transform socketsRoot = characterTemplate.transform.Find("Sockets");
                Assert.NotNull(socketsRoot);
                Transform footSocket = socketsRoot.Find("Socket_foot.L");
                Assert.NotNull(footSocket);
                AssertHasBinding(composition.BoneSync, "socket:foot.L");

                footBone.position = new Vector3(0.5f, 0.9f, -0.25f);
                footBone.rotation = Quaternion.Euler(0f, 60f, 0f);
                composition.BoneSync.ApplyBindings();

                Vector3 expected = footBone.position + (footBone.rotation * new Vector3(0f, -0.02f, 0.04f));
                Assert.Less(Vector3.Distance(expected, footSocket.position), 0.0001f);
            }
            finally
            {
                Object.DestroyImmediate(characterTemplate);
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
        public void AnimationResources_CreateCatalogEntries_ReusesParsedClipRegistryData()
        {
            string registryPath = Path.Combine(
                Path.GetTempPath(),
                "mx-animation-clip-registry-" + System.Guid.NewGuid().ToString("N") + ".json");
            File.WriteAllText(registryPath, AnimationClipRegistryWithDuplicateJson);
            try
            {
                var binding = new CharacterRuntimeAnimationBinding(
                    "animation-set.json",
                    registryPath,
                    new CharacterRuntimeAnimationProfileBinding(
                        "profile.test",
                        CharacterRuntimeAnimationProfileResolver.LocomotionSlotId,
                        "test_set",
                        "group.locomotion",
                        "locomotion",
                        CharacterAnimationBlendKind.Blend2D,
                        "test_package"));

                var first = new List<ResourceCatalogEntry>(
                    CharacterRuntimeAnimationResources.CreateCatalogEntries(binding, "memory"));
                var second = new List<ResourceCatalogEntry>(
                    CharacterRuntimeAnimationResources.CreateCatalogEntries(binding, "memory"));

                Assert.AreEqual(2, first.Count);
                Assert.AreEqual(2, second.Count);
                Assert.AreEqual(first[0].Id, second[0].Id);
                Assert.AreEqual("char.test.anim.idle", first[0].Id);
                Assert.AreEqual("Assets/Test/Idle.anim", first[0].Address);
                Assert.AreEqual("hash-idle", first[0].Hash);
                Assert.AreEqual("test_package", first[0].PackageId);
                Assert.IsTrue(CharacterRuntimeAnimationResources.TryGetCatalogEntryClipId(first[0], out string clipId));
                Assert.AreEqual("idle", clipId);
                Assert.IsTrue(CharacterRuntimeAnimationResources.TryGetCatalogEntryUnityAssetPath(first[0], out string assetPath));
                Assert.AreEqual("Assets/Test/Idle.anim", assetPath);
                Assert.AreEqual("char.test.anim.run", first[1].Id);
            }
            finally
            {
                if (File.Exists(registryPath))
                    File.Delete(registryPath);
            }
        }

        [Test]
        public void AnimationResources_CreateCatalogEntries_ResolvesPackageFbxSourceThroughImportedUnityCatalog()
        {
            string configDirectory = Path.Combine(
                Path.GetTempPath(),
                "mx-animation-runtime-catalog-" + System.Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(configDirectory);
            string registryPath = Path.Combine(configDirectory, "animation_clip_registry.json");
            string unityCatalogPath = Path.Combine(configDirectory, "unity_resource_catalog.json");
            File.WriteAllText(registryPath, AnimationClipRegistryWithPackageResourceJson);
            File.WriteAllText(unityCatalogPath, UnityResourceCatalogWithExtractedAnimationJson);
            try
            {
                var binding = new CharacterRuntimeAnimationBinding(
                    "animation-set.json",
                    registryPath,
                    new CharacterRuntimeAnimationProfileBinding(
                        "profile.test",
                        CharacterRuntimeAnimationProfileResolver.LocomotionSlotId,
                        "test_set",
                        "group.locomotion",
                        "locomotion",
                        CharacterAnimationBlendKind.Blend1D,
                        "test_package"));

                var entries = new List<ResourceCatalogEntry>(
                    CharacterRuntimeAnimationResources.CreateCatalogEntries(binding, "memory"));

                Assert.AreEqual(1, entries.Count, "FBX runtime addresses must fail closed while the extracted .anim remains loadable.");
                Assert.AreEqual("char.test.anim.idle", entries[0].Id);
                Assert.AreEqual("Assets/Generated/Test/Idle.anim", entries[0].Address);
                Assert.AreEqual("test_package", entries[0].PackageId);
                Assert.IsTrue(CharacterRuntimeAnimationResources.TryGetCatalogEntryUnityAssetPath(entries[0], out string assetPath));
                Assert.AreEqual("Assets/Generated/Test/Idle.anim", assetPath);
                StringAssert.EndsWith(".anim", assetPath.ToLowerInvariant());
            }
            finally
            {
                if (Directory.Exists(configDirectory))
                    Directory.Delete(configDirectory, true);
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
                characterTemplate.AddComponent<Animator>();
                sockets.SetParent(characterTemplate.transform, false);
                socket.SetParent(sockets, false);
                ConfigureSlimCharacterTemplate(characterTemplate);

                CharacterRuntimeResourceBootstrap bootstrap = bootstrapObject.AddComponent<CharacterRuntimeResourceBootstrap>();
                ConfigureBootstrap(bootstrap, characterTemplate, weaponTemplate);
                ConfigureAnimationWarmup(bootstrap, idleClip, setDefinition, clipRegistry);

                Assert.IsTrue(bootstrap.LoadCharacter());

                Assert.NotNull(bootstrap.AnimationWarmupResult);
                Assert.IsTrue(bootstrap.AnimationWarmupResult.Success, FormatWarmupIssues(bootstrap.AnimationWarmupResult));
                CollectionAssert.Contains(
                    bootstrap.AnimationWarmupResult.RequiredKeys,
                    new ResourceKey("char.test.anim.idle", ResourceTypeIds.AnimationClip, string.Empty, "test_package"));
                CharacterRuntimeLocomotionBlendController locomotion =
                    bootstrap.CharacterInstance.GetComponent<CharacterRuntimeLocomotionBlendController>();
                Assert.NotNull(locomotion);
                Assert.IsTrue(locomotion.HasAnimationBackend);
                Assert.AreEqual(CharacterAnimationBlendKind.Blend1D, locomotion.ActiveBlendKind);
                Assert.GreaterOrEqual(bootstrap.ResourceManager.CreateDebugSnapshot().LoadedCount, 2);
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
                ConfigureSlimCharacterTemplate(characterTemplate);

                CharacterRuntimeResourceBootstrap bootstrap = bootstrapObject.AddComponent<CharacterRuntimeResourceBootstrap>();
                ConfigureBootstrap(bootstrap, characterTemplate, weaponTemplate);
                ConfigureAnimationPlaybackWarmup(bootstrap, idleClip, walkClip, runClip, attackClip, setDefinition, clipRegistry);

                Assert.IsTrue(bootstrap.LoadCharacter());
                Assert.IsTrue(bootstrap.AnimationWarmupResult.Success, FormatWarmupIssues(bootstrap.AnimationWarmupResult));
                Assert.AreEqual(4, bootstrap.AnimationWarmupResult.RequiredKeys.Count);
                Assert.GreaterOrEqual(bootstrap.ResourceManager.CreateDebugSnapshot().LoadedCount, 5);

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
                Assert.GreaterOrEqual(bootstrap.ResourceManager.CreateDebugSnapshot().LoadedCount, 5);
                Assert.GreaterOrEqual(bootstrap.ResourceManager.CreateDebugSnapshot().TotalRefCount, 11);

                bootstrap.WarmupAnimationResources();
                Assert.IsTrue(bootstrap.AnimationWarmupResult.Success, FormatWarmupIssues(bootstrap.AnimationWarmupResult));
                Assert.GreaterOrEqual(bootstrap.ResourceManager.CreateDebugSnapshot().LoadedCount, 5);
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

        private static void ConfigureSlimCharacterTemplate(GameObject characterTemplate)
        {
            if (characterTemplate.transform.Find("ModelRoot") == null)
                new GameObject("ModelRoot").transform.SetParent(characterTemplate.transform, false);
            if (characterTemplate.transform.Find("Sockets") == null)
                new GameObject("Sockets").transform.SetParent(characterTemplate.transform, false);
            if (characterTemplate.transform.Find("AuthoringColliders") == null)
                new GameObject("AuthoringColliders").transform.SetParent(characterTemplate.transform, false);
            if (characterTemplate.transform.Find("Diagnostics") == null)
                new GameObject("Diagnostics").transform.SetParent(characterTemplate.transform, false);

            CharacterRuntimePrefabDescriptor descriptor =
                characterTemplate.GetComponent<CharacterRuntimePrefabDescriptor>()
                ?? characterTemplate.AddComponent<CharacterRuntimePrefabDescriptor>();
            descriptor.Configure("test_package", "char.test.prefab.character_preview", "default");
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
            SetResource(resources.GetArrayElementAtIndex(1), "char.test_package.prefab.weapon.mainhand.weapon_iron_sword", "test/weapon", weaponTemplate);
            serialized.ApplyModifiedPropertiesWithoutUndo();
            bootstrap.ConfigureImportedPackageForRuntime(CreateRuntimePackage());
        }

        private static CharacterImportedPackage CreateRuntimePackage(
            CharacterSocketRuntimeBinding[] sockets = null)
        {
            var characterId = new CharacterConfigId(1001);
            var attributeProfileId = new CharacterAttributeProfileId(2001);
            var bodyProfileId = new CharacterBodyProfileId(3001);
            var bodyPartId = new CharacterBodyPartConfigId(3002);
            var equipmentSchemaId = new EquipmentSchemaId(4001);
            var loadoutId = new EquipmentLoadoutId(5001);
            var stateId = new EquipmentStateId(6001);
            var presentationId = new CharacterPresentationProfileId(7001);
            var spawnProfileId = new SpawnProfileId(8001);
            var combatActionSetId = new CombatActionSetId(9001);

            var configs = new CharacterImportedConfigSet(
                new[]
                {
                    new CharacterConfig(
                        characterId,
                        "test.character",
                        default,
                        default,
                        attributeProfileId,
                        bodyProfileId,
                        equipmentSchemaId,
                        loadoutId,
                        default,
                        presentationId,
                        CharacterControllerKind.HumanInput,
                        string.Empty,
                        System.Array.Empty<string>())
                },
                new[]
                {
                    new CharacterAttributeProfileConfig(
                        attributeProfileId,
                        "test.attributes",
                        System.Array.Empty<CharacterAttributeEntry>())
                },
                new[]
                {
                    new CharacterBodyProfileConfig(
                        bodyProfileId,
                        "test.body",
                        CharacterBodyKind.Humanoid,
                        "test.parts",
                        string.Empty,
                        string.Empty,
                        System.Array.Empty<CharacterSocketEntry>(),
                        System.Array.Empty<CharacterHitZoneBindingEntry>())
                },
                new[]
                {
                    new CharacterBodyPartConfig(
                        bodyPartId,
                        "test.body.root",
                        "test.parts",
                        1,
                        0,
                        CharacterBodyPartKind.Root,
                        string.Empty,
                        0,
                        0,
                        1f,
                        1f,
                        1f,
                        1f,
                        false)
                },
                System.Array.Empty<CharacterBodyPartContactPointConfig>(),
                new[]
                {
                    new EquipmentSchemaConfig(
                        equipmentSchemaId,
                        "test.schema",
                        System.Array.Empty<EquipmentSlotEntry>(),
                        System.Array.Empty<EquipmentExclusiveGroupEntry>(),
                        new[] { stateId })
                },
                new[]
                {
                    new EquipmentLoadoutConfig(
                        loadoutId,
                        "test.loadout",
                        equipmentSchemaId,
                        System.Array.Empty<EquipmentLoadoutSlotEntry>())
                },
                new[]
                {
                    new EquipmentStateConfig(
                        stateId,
                        "test.state",
                        equipmentSchemaId,
                        0,
                        System.Array.Empty<EquipmentMatchRule>(),
                        default,
                        combatActionSetId,
                        "profile.test",
                        System.Array.Empty<string>())
                },
                System.Array.Empty<WeaponConfig>(),
                System.Array.Empty<AbilityLoadoutConfig>(),
                new[]
                {
                    new CombatActionSetConfig(
                        combatActionSetId,
                        "test.actions",
                        new[]
                        {
                            new CombatActionEntry(
                                "primary",
                                new CharacterCombatActionId(910001),
                                string.Empty,
                                "test.attack")
                        })
                },
                new[]
                {
                    new CharacterPresentationProfileConfig(
                        presentationId,
                        "test.presentation",
                        "profile.test",
                        System.Array.Empty<CharacterResourceKeyEntry>(),
                        System.Array.Empty<string>())
                },
                new[]
                {
                    new SpawnProfileConfig(
                        spawnProfileId,
                        "test.spawn",
                        characterId,
                        "player",
                        CharacterControllerKind.HumanInput,
                        loadoutId,
                        default,
                        string.Empty,
                        "test character")
                });

            var geometry = new CharacterImportedGeometryBinding(
                "test_package",
                null,
                System.Array.Empty<CharacterBodyColliderRuntimeBinding>(),
                System.Array.Empty<CharacterHitZoneRuntimeBinding>(),
                sockets ?? System.Array.Empty<CharacterSocketRuntimeBinding>(),
                new[]
                {
                    new CharacterWeaponAttachmentRuntimeBinding(
                        1,
                        1,
                        1,
                        new CharacterRuntimePose(
                            string.Empty,
                            string.Empty,
                            default,
                            default,
                            new CharacterRuntimeVector3(1f, 1f, 1f)),
                        "weapon.iron_sword.preview",
                        0)
                },
                System.Array.Empty<CharacterWeaponTraceRuntimeBinding>());
            var report = new CharacterUnityImportRuntimeReport(
                "test_package",
                "test.package",
                "Ready",
                canWriteToUnityProject: true,
                canSpawnAfterImport: true,
                string.Empty,
                string.Empty,
                "source",
                "config",
                "geometry",
                "resource",
                "write",
                conflictCount: 0,
                errorCount: 0);
            var animationSetDefinition = new CharacterAnimationSetDefinition(
                "mx.animationSetDefinition.v1",
                "1.0",
                "animation.test",
                "animation.test",
                "Test Animation",
                string.Empty,
                string.Empty,
                new[]
                {
                    new CharacterAnimationSet(
                        "set.base",
                        "Base",
                        "1.0",
                        "idle",
                        "idle",
                        new[]
                        {
                            new CharacterAnimationGroup(
                                "locomotion",
                                "Locomotion",
                                string.Empty,
                                new[]
                                {
                                    new CharacterAnimationClipReference(
                                        "idle",
                                        "Idle",
                                        "char.test.anim.idle",
                                        string.Empty,
                                        string.Empty,
                                        true,
                                        1f,
                                        string.Empty)
                                },
                                new[]
                                {
                                    new CharacterAnimationBlendReference(
                                        "locomotion",
                                        CharacterAnimationBlendKind.Blend1D,
                                        "idle",
                                        new[] { "idle" })
                                })
                        },
                        System.Array.Empty<CharacterAnimationActionBinding>())
                },
                new[]
                {
                    new CharacterAnimationProfile(
                        "profile.test",
                        "Test Profile",
                        "set.base",
                        "locomotion",
                        new[]
                        {
                            new CharacterAnimationProfileSlot(
                                CharacterRuntimeAnimationProfileResolver.LocomotionSlotId,
                                "Locomotion",
                                string.Empty,
                                "set.base",
                                "locomotion",
                                "idle",
                                "locomotion",
                                string.Empty,
                                true)
                        })
                });
            var animationClipKey = new ResourceKey(
                "char.test.anim.idle",
                ResourceTypeIds.AnimationClip,
                string.Empty,
                "test_package");
            var animationCatalog = new ResourceCatalog(
                "test.animations",
                "test_package",
                new[]
                {
                    new ResourceCatalogEntry(
                        animationClipKey.Id,
                        animationClipKey.TypeId,
                        "memory",
                        "test/idle",
                        packageId: "test_package")
                });
            var animationArtifacts = new CharacterCompiledAnimationArtifacts(
                animationSetDefinition,
                new CharacterAnimationClipRegistry(
                    "mx.animationClipRegistry.v1",
                    "1.0",
                    "animation.test",
                    new[]
                    {
                        new CharacterAnimationClipRegistryEntry(
                            "set.base",
                            "locomotion",
                            "idle",
                            "Idle",
                            string.Empty,
                            string.Empty,
                            animationClipKey.Id)
                    }),
                new CharacterAnimationResourcePlan(
                    "mx.animationResourcePlan.v1",
                    "1.0",
                    "animation.test",
                    "animation.test",
                    "plan"),
                System.Array.Empty<CharacterRuntimeSpawnIssue>(),
                new MxAnimationStaticMappingProvider(new[]
                {
                    new MxAnimationSetDefinition(
                        "set.base",
                        1,
                        animationClipKey,
                        animationClipKey,
                        blend1DDefinitions: new[]
                        {
                            new MxAnimationBlend1DDefinition(
                                "locomotion",
                                "locomotion.speed",
                                MxAnimationLayerId.Base,
                                new[] { new MxAnimationBlend1DPoint(0, animationClipKey) })
                        })
                }),
                new MxAnimationClipRegistry(
                    1,
                    animationCatalog.CatalogId,
                    string.Empty,
                    new[] { new MxAnimationClipRegistryEntry(animationClipKey) }));
            return new CharacterImportedPackage(
                string.Empty,
                "test_package",
                configs,
                geometry,
                new CharacterImportedResourceMapping("test_package", System.Array.Empty<CharacterImportedResourceMappingEntry>()),
                new ResourceCatalog("test.unity", "test_package", System.Array.Empty<ResourceCatalogEntry>()),
                report,
                animationCatalog,
                compiledAnimationArtifacts: animationArtifacts);
        }

        private static void AssertHasBinding(CharacterBoneRuntimeSync boneSync, string bindingId)
        {
            IReadOnlyList<CharacterBoneRuntimeSyncBinding> bindings = boneSync.Bindings;
            for (int i = 0; i < bindings.Count; i++)
            {
                CharacterBoneRuntimeSyncBinding binding = bindings[i];
                if (binding != null && binding.BindingId == bindingId)
                    return;
            }

            Assert.Fail("Missing bone runtime sync binding: " + bindingId);
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
          ],
          ""blend1D"": [
            {
              ""blendId"": ""locomotion"",
              ""parameter"": ""locomotion.speed"",
              ""defaultClipId"": ""idle"",
              ""points"": [
                { ""value"": 0, ""clipId"": ""idle"" }
              ]
            }
          ]
        }
      ],
      ""actionBindings"": []
    }
  ],
  ""profiles"": [
    {
      ""profileId"": ""profile.test"",
      ""slots"": [
        {
          ""slotId"": ""locomotion"",
          ""setId"": ""set.base"",
          ""groupId"": ""locomotion"",
          ""defaultClipId"": ""idle"",
          ""defaultBlendId"": ""locomotion"",
          ""required"": true
        }
      ]
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

        private const string AnimationClipRegistryWithDuplicateJson = @"{
  ""format"": ""mx.animationClipRegistry.v1"",
  ""schemaVersion"": ""1.0"",
  ""packageId"": ""animation.test"",
  ""clips"": [
    {
      ""setId"": ""set.base"",
      ""groupId"": ""locomotion"",
      ""clipId"": ""idle"",
      ""runtimeResourceKey"": ""char.test.anim.idle"",
      ""sourceClipName"": ""Idle"",
      ""hash"": ""hash-idle"",
      ""sourceSelection"": {
        ""unityAssetPath"": ""Assets/Test/Idle.anim""
      }
    },
    {
      ""setId"": ""set.base"",
      ""groupId"": ""locomotion"",
      ""clipId"": ""idle_duplicate"",
      ""runtimeResourceKey"": ""char.test.anim.idle"",
      ""sourceClipName"": ""Idle Duplicate"",
      ""hash"": ""hash-idle-duplicate"",
      ""sourceSelection"": {
        ""unityAssetPath"": ""Assets/Test/IdleDuplicate.anim""
      }
    },
    {
      ""setId"": ""set.base"",
      ""groupId"": ""locomotion"",
      ""clipId"": ""run"",
      ""runtimeResourceKey"": ""char.test.anim.run"",
      ""sourceClipName"": ""Run"",
      ""hash"": ""hash-run"",
      ""sourceSelection"": {
        ""unityAssetPath"": ""Assets/Test/Run.anim""
      }
    }
  ]
}";

        private const string AnimationClipRegistryWithPackageResourceJson = @"{
  ""format"": ""mx.animationClipRegistry.v1"",
  ""schemaVersion"": ""1.0"",
  ""packageId"": ""animation.test"",
  ""clips"": [
    {
      ""setId"": ""set.base"",
      ""groupId"": ""locomotion"",
      ""clipId"": ""idle"",
      ""runtimeResourceKey"": ""char.test.anim.idle"",
      ""sourceClipName"": ""Idle"",
      ""sourceSelection"": {
        ""bindingKind"": ""PackageResource"",
        ""packageResourceKey"": ""char.test.anim.idle""
      }
    },
    {
      ""setId"": ""set.base"",
      ""groupId"": ""locomotion"",
      ""clipId"": ""invalid"",
      ""runtimeResourceKey"": ""char.test.anim.invalid"",
      ""sourceClipName"": ""Invalid"",
      ""sourceSelection"": {
        ""bindingKind"": ""PackageResource"",
        ""packageResourceKey"": ""char.test.anim.invalid""
      }
    }
  ]
}";

        private const string UnityResourceCatalogWithExtractedAnimationJson = @"{
  ""format"": ""mx.characterUnityResourceCatalog.v1"",
  ""entries"": [
    {
      ""id"": ""char.test.anim.idle"",
      ""type"": ""AnimationClip"",
      ""address"": ""Assets/Generated/Test/Idle.anim"",
      ""providerData"": {
        ""assetPath"": ""Assets/Generated/Test/Idle.anim"",
        ""sourceFbxAssetPath"": ""Assets/Generated/Test/Idle.fbx""
      }
    },
    {
      ""id"": ""char.test.anim.invalid"",
      ""type"": ""AnimationClip"",
      ""address"": ""Assets/Generated/Test/Invalid.fbx"",
      ""providerData"": {
        ""assetPath"": ""Assets/Generated/Test/Invalid.fbx""
      }
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
              ""defaultClipId"": ""idle"",
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
  ],
  ""profiles"": [
    {
      ""profileId"": ""profile.test"",
      ""slots"": [
        {
          ""slotId"": ""locomotion"",
          ""setId"": ""set.base"",
          ""groupId"": ""locomotion"",
          ""defaultClipId"": ""idle"",
          ""defaultBlendId"": ""locomotion"",
          ""required"": true
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
