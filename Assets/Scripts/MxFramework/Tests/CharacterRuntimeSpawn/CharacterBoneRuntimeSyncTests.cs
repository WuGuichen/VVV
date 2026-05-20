using MxFramework.CharacterRuntimeSpawn.Unity;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace MxFramework.Tests.CharacterRuntimeSpawn
{
    public sealed class CharacterBoneRuntimeSyncTests
    {
        [Test]
        public void ApplyBindings_SyncsTargetFromBoneRelativePose()
        {
            var root = new GameObject("character");
            var bone = new GameObject("hand.R").transform;
            var socket = new GameObject("Socket_mainHand").transform;
            try
            {
                bone.SetParent(root.transform, false);
                socket.SetParent(root.transform, false);
                bone.position = new Vector3(1f, 2f, 3f);
                bone.rotation = Quaternion.Euler(0f, 90f, 0f);

                CharacterBoneRuntimeSync sync = root.AddComponent<CharacterBoneRuntimeSync>();
                ConfigureSync(sync, socket, bone);

                sync.ApplyBindings();

                Assert.That(socket.position.x, Is.EqualTo(0.75f).Within(0.0001f));
                Assert.That(socket.position.y, Is.EqualTo(2.5f).Within(0.0001f));
                Assert.That(socket.position.z, Is.EqualTo(3f).Within(0.0001f));
                Assert.That(socket.rotation.eulerAngles.y, Is.EqualTo(90f).Within(0.0001f));
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        private static void ConfigureSync(CharacterBoneRuntimeSync sync, Transform target, Transform bone)
        {
            var serialized = new SerializedObject(sync);
            SerializedProperty bindings = serialized.FindProperty("_bindings");
            bindings.arraySize = 1;
            SerializedProperty item = bindings.GetArrayElementAtIndex(0);
            item.FindPropertyRelative("_bindingId").stringValue = "socket:mainHand";
            item.FindPropertyRelative("_bonePath").stringValue = "hand.R";
            item.FindPropertyRelative("_target").objectReferenceValue = target;
            item.FindPropertyRelative("_bone").objectReferenceValue = bone;
            item.FindPropertyRelative("_localPosition").vector3Value = new Vector3(0f, 0.5f, -0.25f);
            item.FindPropertyRelative("_localRotation").quaternionValue = Quaternion.identity;
            item.FindPropertyRelative("_localScale").vector3Value = Vector3.one;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
