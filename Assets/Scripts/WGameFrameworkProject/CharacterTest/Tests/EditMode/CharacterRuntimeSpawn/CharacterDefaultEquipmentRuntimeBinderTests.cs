using MxFramework.CharacterRuntimeSpawn.Unity;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace MxFramework.Tests.CharacterRuntimeSpawn
{
    public sealed class CharacterDefaultEquipmentRuntimeBinderTests
    {
        [Test]
        public void InstantiateDefaultWeapons_ParentsPrefabsToConfiguredSockets()
        {
            var root = new GameObject("character");
            var sockets = new GameObject("Sockets").transform;
            var socket = new GameObject("Socket_mainHand").transform;
            var weaponPrefab = new GameObject("weapon_prefab");
            try
            {
                sockets.SetParent(root.transform, false);
                socket.SetParent(sockets, false);

                CharacterDefaultEquipmentRuntimeBinder binder = root.AddComponent<CharacterDefaultEquipmentRuntimeBinder>();
                ConfigureBinder(binder, sockets, socket, weaponPrefab);

                binder.InstantiateDefaultWeapons();

                Assert.AreEqual(1, binder.SpawnedWeapons.Count);
                GameObject spawned = binder.SpawnedWeapons[0];
                Assert.AreSame(socket, spawned.transform.parent);
                Assert.AreEqual(new Vector3(0.1f, 0.2f, 0.3f), spawned.transform.localPosition);
                Assert.AreEqual(new Vector3(1f, 2f, 3f), spawned.transform.localScale);
            }
            finally
            {
                Object.DestroyImmediate(root);
                Object.DestroyImmediate(weaponPrefab);
            }
        }

        [Test]
        public void InstantiateDefaultWeapons_PreservesPrefabRootScale()
        {
            var root = new GameObject("character");
            var sockets = new GameObject("Sockets").transform;
            var socket = new GameObject("Socket_mainHand").transform;
            var weaponPrefab = new GameObject("weapon_wrapper_prefab");
            try
            {
                sockets.SetParent(root.transform, false);
                socket.SetParent(sockets, false);
                weaponPrefab.transform.localScale = new Vector3(0.01f, 0.01f, 0.01f);

                CharacterDefaultEquipmentRuntimeBinder binder = root.AddComponent<CharacterDefaultEquipmentRuntimeBinder>();
                ConfigureBinder(binder, sockets, socket, weaponPrefab);

                binder.InstantiateDefaultWeapons();

                Assert.AreEqual(1, binder.SpawnedWeapons.Count);
                GameObject spawned = binder.SpawnedWeapons[0];
                Assert.AreEqual(new Vector3(0.01f, 0.02f, 0.03f), spawned.transform.localScale);
            }
            finally
            {
                Object.DestroyImmediate(root);
                Object.DestroyImmediate(weaponPrefab);
            }
        }

        [Test]
        public void InstantiateDefaultWeapons_ComposesPrefabRootWrapperPoseWithGripPose()
        {
            var root = new GameObject("character");
            var sockets = new GameObject("Sockets").transform;
            var socket = new GameObject("Socket_mainHand").transform;
            var weaponPrefab = new GameObject("weapon_wrapper_prefab");
            try
            {
                sockets.SetParent(root.transform, false);
                socket.SetParent(sockets, false);
                weaponPrefab.transform.localPosition = new Vector3(0f, -0.125f, 0f);
                weaponPrefab.transform.localRotation = Quaternion.Euler(0f, 90f, 0f);
                weaponPrefab.transform.localScale = new Vector3(1f, 1f, 1f);

                CharacterDefaultEquipmentRuntimeBinder binder = root.AddComponent<CharacterDefaultEquipmentRuntimeBinder>();
                ConfigureBinder(binder, sockets, socket, weaponPrefab);

                binder.InstantiateDefaultWeapons();

                Assert.AreEqual(1, binder.SpawnedWeapons.Count);
                GameObject spawned = binder.SpawnedWeapons[0];
                Assert.That(spawned.transform.localPosition.x, Is.EqualTo(0.1f).Within(0.0001f));
                Assert.That(spawned.transform.localPosition.y, Is.EqualTo(-0.05f).Within(0.0001f));
                Assert.That(spawned.transform.localPosition.z, Is.EqualTo(0.3f).Within(0.0001f));
                Assert.That(Quaternion.Angle(Quaternion.Euler(0f, 90f, 0f), spawned.transform.localRotation), Is.LessThan(0.01f));
                Assert.AreEqual(new Vector3(1f, 2f, 3f), spawned.transform.localScale);
            }
            finally
            {
                Object.DestroyImmediate(root);
                Object.DestroyImmediate(weaponPrefab);
            }
        }

        private static void ConfigureBinder(
            CharacterDefaultEquipmentRuntimeBinder binder,
            Transform socketsRoot,
            Transform socket,
            GameObject weaponPrefab)
        {
            var serialized = new SerializedObject(binder);
            serialized.FindProperty("_socketsRoot").objectReferenceValue = socketsRoot;
            SerializedProperty weapons = serialized.FindProperty("_defaultWeapons");
            weapons.arraySize = 1;
            SerializedProperty item = weapons.GetArrayElementAtIndex(0);
            item.FindPropertyRelative("_weaponId").stringValue = "weapon.iron_sword";
            item.FindPropertyRelative("_equipSlot").stringValue = "mainHand";
            item.FindPropertyRelative("_socketId").stringValue = "mainHand";
            item.FindPropertyRelative("_socketTransform").objectReferenceValue = socket;
            item.FindPropertyRelative("_prefab").objectReferenceValue = weaponPrefab;
            item.FindPropertyRelative("_localPosition").vector3Value = new Vector3(0.1f, 0.2f, 0.3f);
            item.FindPropertyRelative("_localRotation").quaternionValue = Quaternion.identity;
            item.FindPropertyRelative("_localScale").vector3Value = new Vector3(1f, 2f, 3f);
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
