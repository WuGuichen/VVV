using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MxFramework.Game.CharacterTest.Editor
{
    internal static class CharacterTestVegetationAuthoring
    {
        internal const string GrassPrefabPath = "Assets/Art/WGame/Environment/Vegetation/Generated/BasicGrassClump.prefab";
        internal const string GroupName = "Vegetation";

        internal static void ValidateExclusion(Transform vegetationRoot, Scene scene)
        {
            var game = CharacterTestSceneRenderingLevelEditorModule.FindUniqueOwner(scene);
            Transform staticRoot = game?.SceneRendering?.StaticRoot;
            if (staticRoot != null && (vegetationRoot == staticRoot || vegetationRoot.IsChildOf(staticRoot)))
                throw new InvalidOperationException("植被数据根必须独立于静态光照/遮挡采集根。");
            var sources = game?.SceneRendering?.StaticSourceRenderers;
            if (sources != null)
                foreach (var renderer in sources)
                    if (renderer != null && (renderer.transform == vegetationRoot || renderer.transform.IsChildOf(vegetationRoot)))
                        throw new InvalidOperationException("请先从显式 GI 静态源中移除草渲染器；本工具不会改动光照配置。");
        }

        internal static Transform FindGround(Scene scene)
        {
            foreach (var root in scene.GetRootGameObjects())
                foreach (var t in root.GetComponentsInChildren<Transform>(true))
                    if (t.name == "SurroundingEarth" || t.name == "ExteriorTerrain") return t;
            return null;
        }

        internal static bool RemoveType(CharacterTestVegetationSource owner, int typeId)
        {
            foreach (var instance in owner.Instances)
                if (instance.TypeId == typeId) throw new InvalidOperationException("该类型仍有草实例引用，请先擦除对应实例。");
            var type = owner.EditableTypes.Find(item => item.Id == typeId);
            if (type == null) return false;
            Undo.RegisterCompleteObjectUndo(owner.Data, "删除植被类型"); owner.EditableTypes.Remove(type);
            EditorUtility.SetDirty(owner.Data); EditorSceneManager.MarkSceneDirty(owner.Scene); owner.InvalidatePresentation(); return true;
        }

        internal static int Adjust(CharacterTestVegetationSource owner, HashSet<int> selected, float scaleMultiplier, float yawDelta)
        {
            if (selected == null || selected.Count == 0) return 0;
            if (!float.IsFinite(scaleMultiplier) || scaleMultiplier <= 0 || !float.IsFinite(yawDelta))
                throw new ArgumentException("批量调整需要有限旋转和正缩放。");
            Undo.IncrementCurrentGroup(); int group = Undo.GetCurrentGroup(); Undo.SetCurrentGroupName("批量调整植被");
            Undo.RegisterCompleteObjectUndo(owner.UndoTarget, "批量调整植被"); int count = 0;
            for (int i = 0; i < owner.EditableInstances.Count; i++)
            {
                var item = owner.EditableInstances[i]; if (!selected.Contains(item.Id)) continue;
                item.Scale *= scaleMultiplier;
                item.Rotation = Quaternion.Euler(0, yawDelta, 0) * item.Rotation;
                owner.EditableInstances[i] = item; count++;
            }
            if (count == 0) Undo.RevertAllDownToGroup(group);
            else
            {
                EditorUtility.SetDirty(owner.UndoTarget); EditorSceneManager.MarkSceneDirty(owner.gameObject.scene);
                owner.InvalidatePresentation(); Undo.CollapseUndoOperations(group);
            }
            return count;
        }
    }
}
