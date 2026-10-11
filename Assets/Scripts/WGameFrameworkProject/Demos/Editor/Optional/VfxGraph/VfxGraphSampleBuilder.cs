using System;
using System.IO;
using System.Reflection;
using MxFramework.Vfx.Unity.VfxGraph;
using UnityEditor;
using UnityEngine;
using UnityEngine.VFX;

namespace MxFramework.Vfx.Unity.VfxGraph.Editor
{
    /// <summary>Creates the reviewed-technical VFX Graph sample through official Unity Editor asset APIs.</summary>
    public static class VfxGraphSampleBuilder
    {
        public const string GraphPath = "Assets/VFX/MxFramework/Samples/VfxGraph/Graphs/ImpactBurst.vfx";
        public const string PrefabPath = "Assets/VFX/MxFramework/Samples/Fireball/Prefabs/Fireball_BurstImpact.prefab";

        [MenuItem("MxFramework/VFX/Create VFX Graph Impact Sample")]
        public static void Create()
        {
            EnsureFolder("Assets/VFX/MxFramework/Samples/VfxGraph/Graphs");
            if (AssetDatabase.LoadAssetAtPath<VisualEffectAsset>(GraphPath) == null)
                CreateGraphFromPackageTemplate();
            VisualEffectAsset graph = AssetDatabase.LoadAssetAtPath<VisualEffectAsset>(GraphPath);
            if (graph == null)
                throw new InvalidOperationException("VFX Graph sample asset could not be imported: " + GraphPath);

            GameObject root = PrefabUtility.LoadPrefabContents(PrefabPath);
            try
            {
                Transform child = root.transform.Find("VFXGraph_ImpactBurst");
                GameObject graphObject = child != null
                    ? child.gameObject
                    : new GameObject("VFXGraph_ImpactBurst", typeof(VisualEffect));
                graphObject.transform.SetParent(root.transform, false);
                VisualEffect visualEffect = graphObject.GetComponent<VisualEffect>();
                if (visualEffect == null)
                    visualEffect = ObjectFactory.AddComponent<VisualEffect>(graphObject);
                if (visualEffect == null)
                    throw new InvalidOperationException("VisualEffect could not be attached to the sample prefab child.");
                visualEffect.visualEffectAsset = graph;
                UnityVfxGraphController controller = root.GetComponent<UnityVfxGraphController>() ?? root.AddComponent<UnityVfxGraphController>();
                var serialized = new SerializedObject(controller);
                serialized.FindProperty("_suppressParticleSystemFallbackWhenAvailable").boolValue = true;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("VFX Graph impact sample created. graph=" + GraphPath + " prefab=" + PrefabPath);
        }

        private static void CreateGraphFromPackageTemplate()
        {
            UnityEditor.PackageManager.PackageInfo package = UnityEditor.PackageManager.PackageInfo.FindForPackageName("com.unity.visualeffectgraph");
            if (package == null)
                throw new InvalidOperationException("com.unity.visualeffectgraph is not installed.");
            string templatePath = Path.Combine(package.resolvedPath, "Editor", "Templates", "Simple_Burst.vfx");
            if (!File.Exists(templatePath))
                throw new FileNotFoundException("VFX Graph Simple Burst template is missing.", templatePath);
            Type utility = Type.GetType("UnityEditor.VisualEffectAssetEditorUtility, Unity.VisualEffectGraph.Editor", throwOnError: true);
            MethodInfo createTemplate = utility.GetMethod("CreateTemplateAsset", BindingFlags.Public | BindingFlags.Static);
            if (createTemplate == null)
                throw new MissingMethodException(utility.FullName, "CreateTemplateAsset");
            createTemplate.Invoke(null, new object[] { GraphPath, templatePath });
        }

        private static void EnsureFolder(string path)
        {
            string[] parts = path.Split('/');
            string current = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                string next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(current, parts[i]);
                current = next;
            }
        }
    }
}
