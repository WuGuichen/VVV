using MxFramework.Game.CharacterTest;
using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace MxFramework.Editor
{
    public static class CharacterTestPhysicalShieldVfxPrefabBuilder
    {
        private const string MenuPath = "MxFramework/CharacterTest/VFX/Rebuild/Physical Shield Prefab";
        private const string RootPath = "Assets/VFX/MxFramework/Samples/PhysicalShield";
        private const string MaterialRootPath = RootPath + "/Materials";
        private const string PrefabRootPath = RootPath + "/Prefabs";
        private const string ShieldPrefabPath = PrefabRootPath + "/PhysicalShield.prefab";
        private const string SceneProfilePath =
            "Assets/Config/MxFramework/CharacterTest/CharacterTestSceneProfile.asset";
        private const string ViewTypeName =
            "MxFramework.Game.CharacterTest.CharacterTestPhysicalShieldVfxView, MxFramework.Game.CharacterTest";

        [MenuItem(MenuPath)]
        public static void RebuildPrefab()
        {
            EnsureFolder("Assets/VFX", "MxFramework");
            EnsureFolder("Assets/VFX/MxFramework", "Samples");
            EnsureFolder("Assets/VFX/MxFramework/Samples", "PhysicalShield");
            EnsureFolder(RootPath, "Materials");
            EnsureFolder(RootPath, "Prefabs");

            Material lineMaterial = LoadOrCreateLineMaterial(
                MaterialRootPath + "/PhysicalShield_Line.mat");

            BuildShieldPrefab(lineMaterial);
            AssignSceneProfilePrefab();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        private static void BuildShieldPrefab(Material lineMaterial)
        {
            var root = new GameObject("PhysicalShield");
            root.transform.position = Vector3.zero;
            root.transform.rotation = Quaternion.identity;
            root.transform.localScale = Vector3.one;

            Type viewType = Type.GetType(ViewTypeName);
            if (viewType == null)
            {
                Debug.LogError("Missing CharacterTest Physical Shield VFX view type: " + ViewTypeName);
                UnityEngine.Object.DestroyImmediate(root);
                return;
            }

            Component view = root.AddComponent(viewType);
            var serialized = new SerializedObject(view);
            serialized.FindProperty("_lineMaterialOverride").objectReferenceValue = lineMaterial;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            viewType.GetMethod("RebuildVisuals")?.Invoke(view, null);
            EditorUtility.SetDirty(view);

            PrefabUtility.SaveAsPrefabAsset(root, ShieldPrefabPath);
            UnityEngine.Object.DestroyImmediate(root);
        }

        private static Material LoadOrCreateLineMaterial(string path)
        {
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = CharacterTestUnlitMaterials.CreateVfx(Path.GetFileNameWithoutExtension(path), Color.white);
                AssetDatabase.CreateAsset(material, path);
            }
            else CharacterTestUnlitMaterials.ConfigureVfx(material, Color.white);
            EditorUtility.SetDirty(material);
            return material;
        }

        private static void AssignSceneProfilePrefab()
        {
            ScriptableObject profile = AssetDatabase.LoadAssetAtPath<ScriptableObject>(SceneProfilePath);
            if (profile == null)
                return;

            var serialized = new SerializedObject(profile);
            SerializedProperty property = serialized.FindProperty("_physicalShieldVfxPrefab");
            if (property == null)
                return;

            property.objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameObject>(ShieldPrefabPath);
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(profile);
        }

        private static void EnsureFolder(string parent, string child)
        {
            string path = parent + "/" + child;
            if (!AssetDatabase.IsValidFolder(path))
                AssetDatabase.CreateFolder(parent, child);
        }
    }
}
