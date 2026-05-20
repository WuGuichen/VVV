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

        private static void SetResource(SerializedProperty property, string id, string address, Object asset)
        {
            property.FindPropertyRelative("_id").stringValue = id;
            property.FindPropertyRelative("_typeId").stringValue = ResourceTypeIds.GameObject;
            property.FindPropertyRelative("_variant").stringValue = "default";
            property.FindPropertyRelative("_packageId").stringValue = "test_package";
            property.FindPropertyRelative("_address").stringValue = address;
            property.FindPropertyRelative("_asset").objectReferenceValue = asset;
        }
    }
}
