using System;
using System.Collections.Generic;
using System.Reflection;
using MxFramework.Rendering;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace MxFramework.Rendering.Editor
{
    public static class MxRenderingPipelineFeatureAssetUtility
    {
        public const string DefaultRendererAssetPath = "Assets/Config/MxFramework/Rendering/MxFrameworkUniversalRenderer.asset";

        [MenuItem("MxFramework/Rendering/Ensure Pipeline Feature")]
        public static void EnsureDefaultRendererFeatureMenu()
        {
            EnsureDefaultRendererFeature();
        }

        public static void EnsureDefaultRendererFeature()
        {
            EnsureRendererFeature(DefaultRendererAssetPath);
        }

        public static void EnsureAndValidateDefaultRendererFeatureForBatch()
        {
            EnsureDefaultRendererFeature();

            if (!ValidateDefaultRendererFeature())
                throw new InvalidOperationException("MxFrameworkUniversalRenderer does not contain exactly one MxRenderingPipelineFeature.");
        }

        public static void EnsureRendererFeature(string rendererAssetPath)
        {
            var rendererData = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(rendererAssetPath);
            if (rendererData == null)
                throw new InvalidOperationException("Universal renderer asset was not found: " + rendererAssetPath);

            RemoveExtraFeatureSubAssets(rendererData);

            MxRenderingPipelineFeature feature = FindFeatureSubAsset(rendererData);
            if (feature == null)
            {
                feature = ScriptableObject.CreateInstance<MxRenderingPipelineFeature>();
                feature.name = nameof(MxRenderingPipelineFeature);
                AssetDatabase.AddObjectToAsset(feature, rendererData);
            }

            SetOnlyRendererFeature(rendererData, feature);
            EditorUtility.SetDirty(feature);
            EditorUtility.SetDirty(rendererData);
            AssetDatabase.SaveAssets();
            AssetDatabase.ImportAsset(rendererAssetPath, ImportAssetOptions.ForceUpdate);
        }

        public static bool ValidateDefaultRendererFeature()
        {
            var rendererData = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(DefaultRendererAssetPath);
            if (rendererData == null)
                return false;

            var features = GetRendererFeatures(rendererData);
            return features.Count == 1 && features[0] is MxRenderingPipelineFeature;
        }

        private static void RemoveExtraFeatureSubAssets(UniversalRendererData rendererData)
        {
            UnityEngine.Object[] assets = AssetDatabase.LoadAllAssetsAtPath(AssetDatabase.GetAssetPath(rendererData));

            foreach (UnityEngine.Object asset in assets)
            {
                if (asset == null || asset == rendererData)
                    continue;

                if (asset is ScriptableRendererFeature && asset is not MxRenderingPipelineFeature)
                    UnityEngine.Object.DestroyImmediate(asset, true);
            }
        }

        private static MxRenderingPipelineFeature FindFeatureSubAsset(UniversalRendererData rendererData)
        {
            UnityEngine.Object[] assets = AssetDatabase.LoadAllAssetsAtPath(AssetDatabase.GetAssetPath(rendererData));

            foreach (UnityEngine.Object asset in assets)
            {
                if (asset is MxRenderingPipelineFeature feature)
                    return feature;
            }

            return null;
        }

        private static void SetOnlyRendererFeature(UniversalRendererData rendererData, MxRenderingPipelineFeature feature)
        {
            var serializedObject = new SerializedObject(rendererData);
            SerializedProperty featuresProperty = serializedObject.FindProperty("m_RendererFeatures");
            if (featuresProperty == null)
                throw new InvalidOperationException("m_RendererFeatures was not found on UniversalRendererData.");

            featuresProperty.ClearArray();
            featuresProperty.arraySize = 1;
            featuresProperty.GetArrayElementAtIndex(0).objectReferenceValue = feature;
            UpdateRendererFeatureMap(serializedObject, feature);
            serializedObject.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void UpdateRendererFeatureMap(SerializedObject serializedObject, MxRenderingPipelineFeature feature)
        {
            SerializedProperty mapProperty = serializedObject.FindProperty("m_RendererFeatureMap");
            if (mapProperty == null || !AssetDatabase.TryGetGUIDAndLocalFileIdentifier(feature, out _, out long localId))
                return;

            if (mapProperty.isArray)
            {
                mapProperty.ClearArray();
                mapProperty.arraySize = 1;
                mapProperty.GetArrayElementAtIndex(0).longValue = localId;
            }
            else
            {
                mapProperty.longValue = localId;
            }
        }

        private static IReadOnlyList<ScriptableRendererFeature> GetRendererFeatures(UniversalRendererData rendererData)
        {
            PropertyInfo property = rendererData.GetType().GetProperty(
                "rendererFeatures",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

            if (property != null && property.GetValue(rendererData) is IReadOnlyList<ScriptableRendererFeature> features)
                return features;

            var serializedObject = new SerializedObject(rendererData);
            SerializedProperty featuresProperty = serializedObject.FindProperty("m_RendererFeatures");
            var result = new List<ScriptableRendererFeature>();
            if (featuresProperty == null)
                return result;

            for (int i = 0; i < featuresProperty.arraySize; i++)
            {
                if (featuresProperty.GetArrayElementAtIndex(i).objectReferenceValue is ScriptableRendererFeature feature)
                    result.Add(feature);
            }

            return result;
        }
    }
}
