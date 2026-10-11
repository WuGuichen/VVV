using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace MxFramework.CharacterCloth.Magica.Unity.Editor
{
    internal sealed class MagicaCharacterClothPreviewSession
    {
        internal string PrefabPath { get; private set; } = string.Empty;
        internal GameObject Root { get; private set; }
        internal Transform BodyModel { get; private set; }
        private int _initialDirtinessClearFrames;

        internal bool Open(string packageId)
        {
            if (string.IsNullOrWhiteSpace(packageId)) return false;
            PrefabPath = "Assets/MxFrameworkGenerated/CharacterPackages/" + packageId
                + "/prefabs/" + packageId + "_character_preview.prefab";
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (prefab == null) { Clear(); return false; }
            PrefabStage stage = PrefabStageUtility.GetCurrentPrefabStage();
            if (stage == null || !string.Equals(stage.assetPath, PrefabPath, StringComparison.OrdinalIgnoreCase))
            {
                AssetDatabase.OpenAsset(prefab);
                stage = PrefabStageUtility.GetCurrentPrefabStage();
            }
            if (stage == null || !string.Equals(stage.assetPath, PrefabPath, StringComparison.OrdinalIgnoreCase))
            {
                Clear();
                return false;
            }
            Root = stage.prefabContentsRoot;
            BodyModel = FindByName(Root != null ? Root.transform : null, "BodyModel");
            // Magica components can mark a freshly opened generated prefab stage dirty from
            // Editor-only initialization. This authoring surface never saves the generated prefab.
            stage.ClearDirtiness();
            _initialDirtinessClearFrames = 8;
            EditorApplication.update -= ClearInitialDirtiness;
            EditorApplication.update += ClearInitialDirtiness;
            SceneView.RepaintAll();
            return Root != null && BodyModel != null;
        }

        internal bool Refresh()
        {
            PrefabStage stage = PrefabStageUtility.GetCurrentPrefabStage();
            if (stage == null || !string.Equals(stage.assetPath, PrefabPath, StringComparison.OrdinalIgnoreCase))
                return false;
            Root = stage.prefabContentsRoot;
            BodyModel = FindByName(Root != null ? Root.transform : null, "BodyModel");
            return Root != null && BodyModel != null;
        }

        internal void Focus(Transform target)
        {
            if (target == null || SceneView.lastActiveSceneView == null) return;
            float size = HandleUtility.GetHandleSize(target.position) * 0.35f;
            SceneView.lastActiveSceneView.Frame(new Bounds(target.position, Vector3.one * size), false);
        }

        internal void Clear()
        {
            EditorApplication.update -= ClearInitialDirtiness;
            _initialDirtinessClearFrames = 0;
            PrefabPath = string.Empty;
            Root = null;
            BodyModel = null;
        }

        internal void CloseGeneratedPrefabStage()
        {
            PrefabStage stage = PrefabStageUtility.GetCurrentPrefabStage();
            if (stage != null && string.Equals(stage.assetPath, PrefabPath, StringComparison.OrdinalIgnoreCase))
            {
                stage.ClearDirtiness();
                StageUtility.GoToMainStage();
            }
            Clear();
        }

        private static Transform FindByName(Transform root, string name)
        {
            if (root == null) return null;
            Transform[] all = root.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < all.Length; i++)
                if (string.Equals(all[i].name, name, StringComparison.Ordinal)) return all[i];
            return null;
        }

        private void ClearInitialDirtiness()
        {
            if (_initialDirtinessClearFrames-- <= 0)
            {
                EditorApplication.update -= ClearInitialDirtiness;
                return;
            }
            PrefabStage stage = PrefabStageUtility.GetCurrentPrefabStage();
            if (stage == null || !string.Equals(stage.assetPath, PrefabPath, StringComparison.OrdinalIgnoreCase))
            {
                EditorApplication.update -= ClearInitialDirtiness;
                _initialDirtinessClearFrames = 0;
                return;
            }
            stage.ClearDirtiness();
        }
    }
}
