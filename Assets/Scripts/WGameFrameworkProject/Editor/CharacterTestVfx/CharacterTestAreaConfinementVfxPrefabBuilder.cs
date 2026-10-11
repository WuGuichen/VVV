using MxFramework.Game.CharacterTest;
using System.IO;
using System;
using UnityEditor;
using UnityEngine;

namespace MxFramework.Editor
{
    public static class CharacterTestAreaConfinementVfxPrefabBuilder
    {
        private const string MenuPath = "MxFramework/CharacterTest/VFX/Rebuild/Area Confinement Prefab";
        private const string RootPath = "Assets/VFX/MxFramework/Samples/AreaConfinement";
        private const string MaterialRootPath = RootPath + "/Materials";
        private const string PrefabRootPath = RootPath + "/Prefabs";
        private const string ZonePrefabPath = PrefabRootPath + "/AreaConfinement_Zone.prefab";
        private const string SceneProfilePath =
            "Assets/Config/MxFramework/CharacterTest/CharacterTestSceneProfile.asset";
        private const string ViewTypeName =
            "MxFramework.Game.CharacterTest.CharacterTestAreaConfinementVfxView, MxFramework.Game.CharacterTest";

        [MenuItem(MenuPath)]
        public static void RebuildPrefab()
        {
            EnsureFolder("Assets/VFX", "MxFramework");
            EnsureFolder("Assets/VFX/MxFramework", "Samples");
            EnsureFolder("Assets/VFX/MxFramework/Samples", "AreaConfinement");
            EnsureFolder(RootPath, "Materials");
            EnsureFolder(RootPath, "Prefabs");

            Material lineMaterial = LoadOrCreateLineMaterial(
                MaterialRootPath + "/AreaConfinement_Line.mat");

            BuildZonePrefab(lineMaterial);
            AssignSceneProfilePrefab();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        private static void BuildZonePrefab(Material lineMaterial)
        {
            var root = new GameObject("AreaConfinement_Zone");
            root.transform.position = Vector3.zero;
            root.transform.rotation = Quaternion.identity;
            root.transform.localScale = Vector3.one;

            Type viewType = Type.GetType(ViewTypeName);
            if (viewType == null)
            {
                Debug.LogError("Missing CharacterTest Area Confinement VFX view type: " + ViewTypeName);
                UnityEngine.Object.DestroyImmediate(root);
                return;
            }

            Component view = root.AddComponent(viewType);
            var serialized = new SerializedObject(view);
            serialized.FindProperty("_lineMaterialOverride").objectReferenceValue = lineMaterial;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            viewType.GetMethod("RebuildVisuals")?.Invoke(view, null);
            EditorUtility.SetDirty(view);

            PrefabUtility.SaveAsPrefabAsset(root, ZonePrefabPath);
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
            SerializedProperty property = serialized.FindProperty("_areaConfinementZoneVfxPrefab");
            if (property == null)
                return;

            property.objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameObject>(ZonePrefabPath);
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
