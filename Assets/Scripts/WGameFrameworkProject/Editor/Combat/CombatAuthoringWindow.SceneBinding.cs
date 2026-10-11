using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using MxFramework.Combat.Authoring;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace MxFramework.Combat.Editor
{
    public sealed partial class CombatAuthoringWindow
    {
        private void RegisterSerializedRefresh(VisualElement root)
        {
            if (ReferenceEquals(root.userData, SerializedRefreshRegistrationMarker))
            {
                return;
            }

            root.userData = SerializedRefreshRegistrationMarker;
            root.RegisterCallback<SerializedPropertyChangeEvent>(_ =>
            {
                EditorApplication.delayCall -= DelayedSerializedRefresh;
                EditorApplication.delayCall += DelayedSerializedRefresh;
            });
        }

        private void DelayedSerializedRefresh()
        {
            if (HasValidSerializedTarget(_actionSerialized))
            {
                _actionSerialized.Update();
            }

            if (HasValidSerializedTarget(_bindingSerialized))
            {
                _bindingSerialized.Update();
            }

            RefreshTimeline(refreshDetail: false);
            RefreshContext();
            RefreshValidation();
            _isApplyingLocalSerializedRefresh = true;
            try
            {
                CombatAuthoringSceneState.NotifyDataChanged();
            }
            finally
            {
                _observedDataRevision = CombatAuthoringSceneState.DataRevision;
                _isApplyingLocalSerializedRefresh = false;
            }
        }

        private static bool HasValidSerializedTarget(SerializedObject serialized)
        {
            return serialized != null && serialized.targetObject != null;
        }

        private void UseSelection()
        {
            if (Selection.activeObject is CombatActionAuthoringAsset action)
            {
                SetActionAsset(action);
                return;
            }

            if (Selection.activeObject is CombatSceneBindingAsset binding)
            {
                SetSceneBindingAsset(binding);
            }
        }

        private void CreateActionAsset()
        {
            string path = EditorUtility.SaveFilePanelInProject(
                "Create Combat Action Asset",
                "CombatActionAuthoringAsset",
                "asset",
                "选择 Combat Action Authoring Asset 保存路径。");
            if (string.IsNullOrEmpty(path))
            {
                return;
            }

            var asset = CreateInstance<CombatActionAuthoringAsset>();
            AssetDatabase.CreateAsset(asset, path);
            AssetDatabase.SaveAssets();
            SetActionAsset(asset);
            FocusCreatedAsset(asset);
        }

        private void CreateBindingAsset()
        {
            CombatSceneBindingAsset asset = CreateBindingAssetFromSavePanel(true);
            if (asset != null)
            {
                FocusCreatedAsset(asset);
            }
        }

        private CombatSceneBindingAsset CreateBindingAssetFromSavePanel(bool setAsCurrent)
        {
            string path = EditorUtility.SaveFilePanelInProject(
                "Create Combat Scene Binding Asset",
                "CombatSceneBindingAsset",
                "asset",
                "选择 Combat Scene Binding Asset 保存路径。");
            if (string.IsNullOrEmpty(path))
            {
                return null;
            }

            var asset = CreateInstance<CombatSceneBindingAsset>();
            AssetDatabase.CreateAsset(asset, path);
            AssetDatabase.SaveAssets();
            if (setAsCurrent)
            {
                SetSceneBindingAsset(asset);
            }

            return asset;
        }

        private void FocusCreatedAsset(UnityEngine.Object asset)
        {
            if (asset == null)
            {
                return;
            }

            Show();
            Focus();
            EditorApplication.delayCall += () =>
            {
                if (this == null)
                {
                    return;
                }

                Show();
                Focus();
                PingObject(asset);
            };
        }

        private void KeepWindowVisible()
        {
            Show();
            Focus();
            EditorApplication.delayCall += () =>
            {
                if (this == null)
                {
                    return;
                }

                Show();
                Focus();
                Repaint();
            };
        }

        private CombatSceneBindingAsset EnsureSceneBindingAsset()
        {
            if (_sceneBindingAsset != null)
            {
                return _sceneBindingAsset;
            }

            return CreateBindingAssetFromSavePanel(true);
        }

        private void GenerateBindingFromCurrentScene()
        {
            CombatSceneBindingAsset binding = EnsureSceneBindingAsset();
            if (binding == null)
            {
                return;
            }

            CollectSceneMarkerCandidates(_sceneMarkerCandidates);
            if (_sceneMarkerCandidates.Count == 0)
            {
                EditorUtility.DisplayDialog(
                    "Combat Scene Binding",
                    "当前打开场景中没有找到名字包含 Player、Enemy、Marker 或 Combat 的 Transform。",
                    "确定");
                return;
            }

            SerializedObject serialized = new SerializedObject(binding);
            serialized.Update();
            Undo.RecordObject(binding, "Generate Combat Scene Binding");

            FillBindingIdentity(serialized);
            WriteMarkers(serialized.FindProperty("markers"), _sceneMarkerCandidates);
            WriteActors(serialized.FindProperty("actors"), _sceneMarkerCandidates);

            serialized.ApplyModifiedProperties();
            EditorUtility.SetDirty(binding);
            SetSceneBindingAsset(binding);
            CombatAuthoringSceneState.NotifyDataChanged();
            SceneView.RepaintAll();
            KeepWindowVisible();
        }

        private void RelinkSelectedTransform()
        {
            TryRelinkSelectedTransform(out _);
        }

        private bool TryRelinkSelectedTransform(out string markerId)
        {
            Transform selected = Selection.activeTransform;
            if (selected == null && Selection.activeGameObject != null)
            {
                selected = Selection.activeGameObject.transform;
            }

            markerId = string.Empty;
            if (selected == null)
            {
                EditorUtility.DisplayDialog("Combat Scene Binding", "请选择一个场景 Transform 或 GameObject。", "确定");
                return false;
            }

            if (EditorUtility.IsPersistent(selected))
            {
                EditorUtility.DisplayDialog("Combat Scene Binding", "请选择当前场景中的对象，不能使用 Project 资产。", "确定");
                return false;
            }

            CombatSceneBindingAsset binding = EnsureSceneBindingAsset();
            if (binding == null)
            {
                return false;
            }

            string targetPath = GetHierarchyPath(selected);
            markerId = CreateReadableMarkerId(selected.name);
            if (string.IsNullOrEmpty(markerId))
            {
                markerId = "Marker";
            }

            SerializedObject serialized = new SerializedObject(binding);
            serialized.Update();
            Undo.RecordObject(binding, "Relink Combat Marker");

            FillBindingIdentity(serialized);
            SerializedProperty markers = serialized.FindProperty("markers");
            UpsertMarker(markers, markerId, targetPath);

            serialized.ApplyModifiedProperties();
            EditorUtility.SetDirty(binding);
            SetSceneBindingAsset(binding);
            CombatAuthoringSceneState.NotifyDataChanged();
            SceneView.RepaintAll();
            KeepWindowVisible();
            return true;
        }

        private void AddShape(string section, string propertyRoot)
        {
            if (_actionAsset == null)
            {
                EditorUtility.DisplayDialog("Combat Authoring", "请先选择或创建 Action Asset。", "确定");
                return;
            }

            if (_actionSerialized == null)
            {
                _actionSerialized = new SerializedObject(_actionAsset);
            }

            SerializedProperty shapes = _actionSerialized.FindProperty(propertyRoot);
            if (shapes == null || !shapes.isArray)
            {
                EditorUtility.DisplayDialog("Combat Authoring", "找不到数组字段：" + propertyRoot, "确定");
                return;
            }

            string markerId = EnsureDefaultMarkerIdForNewShape(section);
            if (string.IsNullOrEmpty(markerId))
            {
                EditorUtility.DisplayDialog(
                    "Combat Authoring",
                    "添加 " + section + " 需要先有可用的 Binding marker。请先选择 Scene Binding，并使用“从当前场景生成 Binding”或“使用当前选择重连”。",
                    "确定");
                SetQuickActionStatus("未添加 " + section + "：缺少可绑定的 marker。", UiColors.WarnAmber);
                return;
            }

            int currentFrame = CombatAuthoringSceneState.Frame;
            int index = shapes.arraySize;
            Undo.RecordObject(_actionAsset, "Add Combat " + section);
            shapes.arraySize++;

            SerializedProperty shape = shapes.GetArrayElementAtIndex(index);
            int trackId = GetNextShapeTrackId(shapes);
            shape.FindPropertyRelative("trackId").intValue = trackId;
            shape.FindPropertyRelative("shapeKind").enumValueIndex = (int)CombatAuthoringShapeKind.Sphere;
            SerializedProperty frameRange = shape.FindPropertyRelative("frameRange");
            frameRange.FindPropertyRelative("startFrame").intValue = currentFrame;
            frameRange.FindPropertyRelative("endFrame").intValue = currentFrame;
            shape.FindPropertyRelative("markerId").stringValue = markerId;
            shape.FindPropertyRelative("localCenter").vector3Value = Vector3.zero;
            shape.FindPropertyRelative("radiusRaw").intValue = DefaultShapeRadiusRaw;
            shape.FindPropertyRelative("heightRaw").intValue = DefaultCapsuleHeightRaw;
            shape.FindPropertyRelative("sourceOrder").intValue = index + 1;

            _actionSerialized.ApplyModifiedProperties();
            EditorUtility.SetDirty(_actionAsset);
            _actionSerialized.Update();
            CombatAuthoringSceneState.NotifyDataChanged();
            RefreshTimeline();
            SelectTimelineRow(FindTimelineRow(section, trackId));
            RefreshValidation();
            SceneView.RepaintAll();
            SetQuickActionStatus("已添加 " + section + "，绑定 Marker：" + markerId, UiColors.SuccessGreen);
        }

        private string EnsureDefaultMarkerIdForNewShape(string section)
        {
            string markerId = GetDefaultMarkerId();
            if (!string.IsNullOrEmpty(markerId))
            {
                return markerId;
            }

            if (_sceneBindingAsset == null)
            {
                return string.Empty;
            }

            CollectSceneMarkerCandidates(_sceneMarkerCandidates);
            if (_sceneMarkerCandidates.Count == 0)
            {
                return string.Empty;
            }

            SerializedObject serialized = new SerializedObject(_sceneBindingAsset);
            serialized.Update();
            Undo.RecordObject(_sceneBindingAsset, "Prepare Combat Binding Marker");

            FillBindingIdentity(serialized);
            SerializedProperty markers = serialized.FindProperty("markers");
            if (markers != null && markers.arraySize == 0)
            {
                WriteMarkers(markers, _sceneMarkerCandidates);
            }

            SerializedProperty actors = serialized.FindProperty("actors");
            if (actors != null && actors.arraySize == 0)
            {
                WriteActors(actors, _sceneMarkerCandidates);
            }

            serialized.ApplyModifiedProperties();
            EditorUtility.SetDirty(_sceneBindingAsset);
            _bindingSerialized = new SerializedObject(_sceneBindingAsset);
            CombatAuthoringSceneState.SetContext(_actionAsset, _sceneBindingAsset);
            SetQuickActionStatus("已为 " + section + " 自动补齐 Binding marker。", UiColors.SuccessGreen);
            return GetDefaultMarkerId();
        }

        private string GetDefaultMarkerId()
        {
            CombatSceneBindingAsset binding = _sceneBindingAsset;
            if (binding != null && binding.Actors != null && binding.Actors.Length > 0)
            {
                CombatActorBindingData actor = binding.Actors[0];
                if (!string.IsNullOrEmpty(actor.MarkerId) && MarkerExistsInBinding(binding, actor.MarkerId))
                {
                    return actor.MarkerId;
                }
            }

            if (binding != null && binding.Markers != null && binding.Markers.Length > 0)
            {
                CombatMarkerBindingData[] markers = binding.Markers;
                for (int i = 0; i < markers.Length; i++)
                {
                    if (!string.IsNullOrEmpty(markers[i].MarkerId))
                    {
                        return markers[i].MarkerId;
                    }
                }
            }

            return string.Empty;
        }

        private static bool MarkerExistsInBinding(CombatSceneBindingAsset binding, string markerId)
        {
            if (binding == null || binding.Markers == null || string.IsNullOrEmpty(markerId))
            {
                return false;
            }

            CombatMarkerBindingData[] markers = binding.Markers;
            for (int i = 0; i < markers.Length; i++)
            {
                if (string.Equals(markers[i].MarkerId, markerId, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        private static int GetNextShapeTrackId(SerializedProperty shapes)
        {
            int maxTrackId = 0;
            for (int i = 0; i < shapes.arraySize; i++)
            {
                SerializedProperty element = shapes.GetArrayElementAtIndex(i);
                maxTrackId = Math.Max(maxTrackId, element.FindPropertyRelative("trackId").intValue);
            }

            return maxTrackId + 1;
        }

        private int FindTimelineRow(string section, int trackId)
        {
            for (int i = 0; i < _timelineRows.Count; i++)
            {
                TimelineRow row = _timelineRows[i];
                if (row.TrackId == trackId && string.Equals(row.Section, section, StringComparison.Ordinal))
                {
                    return i;
                }
            }

            return -1;
        }

        private int FindTimelineRow(TimelineRow target)
        {
            for (int i = 0; i < _timelineRows.Count; i++)
            {
                TimelineRow row = _timelineRows[i];
                if (row.TrackId == target.TrackId
                    && string.Equals(row.Section, target.Section, StringComparison.Ordinal)
                    && string.Equals(row.PropertyPath, target.PropertyPath, StringComparison.Ordinal))
                {
                    return i;
                }
            }

            return -1;
        }

        private int FindPersistentTimelineSelection()
        {
            if (string.IsNullOrEmpty(_persistentSelectionSection)
                || string.IsNullOrEmpty(_persistentSelectionPropertyPath))
            {
                return -1;
            }

            for (int i = 0; i < _timelineRows.Count; i++)
            {
                TimelineRow row = _timelineRows[i];
                if (row.TrackId == _persistentSelectionTrackId
                    && string.Equals(row.Section, _persistentSelectionSection, StringComparison.Ordinal)
                    && string.Equals(row.PropertyPath, _persistentSelectionPropertyPath, StringComparison.Ordinal))
                {
                    return i;
                }
            }

            return -1;
        }

        private void StorePersistentTimelineSelection(TimelineRow row)
        {
            _persistentSelectionSection = row.Section ?? string.Empty;
            _persistentSelectionTrackId = row.TrackId;
            _persistentSelectionPropertyPath = row.PropertyPath ?? string.Empty;
        }

        private void ClearPersistentTimelineSelection()
        {
            _persistentSelectionSection = string.Empty;
            _persistentSelectionTrackId = -1;
            _persistentSelectionPropertyPath = string.Empty;
        }

        private static void FillBindingIdentity(SerializedObject serialized)
        {
            Scene activeScene = SceneManager.GetActiveScene();
            SerializedProperty sceneGuid = serialized.FindProperty("sceneGuid");
            if (sceneGuid != null && string.IsNullOrEmpty(sceneGuid.stringValue) && activeScene.IsValid())
            {
                sceneGuid.stringValue = string.IsNullOrEmpty(activeScene.path)
                    ? activeScene.name
                    : AssetDatabase.AssetPathToGUID(activeScene.path);
            }

            SerializedProperty profileId = serialized.FindProperty("bindingProfileId");
            if (profileId != null && string.IsNullOrEmpty(profileId.stringValue))
            {
                profileId.stringValue = string.IsNullOrEmpty(activeScene.name) ? "default" : activeScene.name;
            }
        }

        private static void WriteMarkers(SerializedProperty markers, List<SceneMarkerCandidate> candidates)
        {
            if (markers == null)
            {
                return;
            }

            markers.arraySize = candidates.Count;
            for (int i = 0; i < candidates.Count; i++)
            {
                SerializedProperty marker = markers.GetArrayElementAtIndex(i);
                marker.FindPropertyRelative("markerId").stringValue = candidates[i].MarkerId;
                marker.FindPropertyRelative("targetPath").stringValue = candidates[i].TargetPath;
                marker.FindPropertyRelative("sourceOrder").intValue = i + 1;
            }
        }

        private static void WriteActors(SerializedProperty actors, List<SceneMarkerCandidate> candidates)
        {
            if (actors == null)
            {
                return;
            }

            int[] actorIndices = GetActorCandidateIndices(candidates);
            int actorCount = actorIndices.Length;
            actors.arraySize = actorCount;
            for (int i = 0; i < actorCount; i++)
            {
                int id = i + 1;
                SceneMarkerCandidate candidate = candidates[actorIndices[i]];
                SerializedProperty actor = actors.GetArrayElementAtIndex(i);
                actor.FindPropertyRelative("entityId").intValue = id;
                actor.FindPropertyRelative("displayName").stringValue = candidate.TransformName;
                actor.FindPropertyRelative("markerId").stringValue = candidate.MarkerId;
                actor.FindPropertyRelative("bodyId").intValue = id;

                SerializedProperty colliders = actor.FindPropertyRelative("colliders");
                colliders.arraySize = 1;
                SerializedProperty collider = colliders.GetArrayElementAtIndex(0);
                collider.FindPropertyRelative("colliderId").intValue = id;
                collider.FindPropertyRelative("markerId").stringValue = candidate.MarkerId;
                collider.FindPropertyRelative("sourceOrder").intValue = id;
            }
        }

        private static int[] GetActorCandidateIndices(List<SceneMarkerCandidate> candidates)
        {
            if (candidates.Count == 0)
            {
                return Array.Empty<int>();
            }

            int playerIndex = FindFirstCandidateByScore(candidates, 0);
            int enemyIndex = FindFirstCandidateByScore(candidates, 1);
            if (playerIndex >= 0 && enemyIndex >= 0 && playerIndex != enemyIndex)
            {
                return new[] { playerIndex, enemyIndex };
            }

            if (candidates.Count == 1)
            {
                return new[] { 0 };
            }

            return new[] { 0, 1 };
        }

        private static int FindFirstCandidateByScore(List<SceneMarkerCandidate> candidates, int score)
        {
            for (int i = 0; i < candidates.Count; i++)
            {
                if (candidates[i].Score == score)
                {
                    return i;
                }
            }

            return -1;
        }

        private static void UpsertMarker(SerializedProperty markers, string markerId, string targetPath)
        {
            if (markers == null)
            {
                return;
            }

            int index = -1;
            for (int i = 0; i < markers.arraySize; i++)
            {
                SerializedProperty marker = markers.GetArrayElementAtIndex(i);
                SerializedProperty currentId = marker.FindPropertyRelative("markerId");
                if (string.Equals(currentId.stringValue, markerId, StringComparison.Ordinal))
                {
                    index = i;
                    break;
                }
            }

            if (index < 0)
            {
                index = markers.arraySize;
                markers.arraySize++;
            }

            SerializedProperty target = markers.GetArrayElementAtIndex(index);
            target.FindPropertyRelative("markerId").stringValue = markerId;
            target.FindPropertyRelative("targetPath").stringValue = targetPath;
            target.FindPropertyRelative("sourceOrder").intValue = index + 1;
        }

        private void CollectSceneMarkerCandidates(List<SceneMarkerCandidate> candidates)
        {
            candidates.Clear();
            _markerIdBuffer.Clear();

            var scenes = new List<Scene>(SceneManager.sceneCount);
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                Scene scene = SceneManager.GetSceneAt(i);
                if (scene.IsValid() && scene.isLoaded)
                {
                    scenes.Add(scene);
                }
            }

            scenes.Sort(CompareScenes);
            for (int i = 0; i < scenes.Count; i++)
            {
                CollectSceneMarkerCandidates(scenes[i], candidates);
            }

            candidates.Sort(CompareSceneMarkerCandidates);
            if (candidates.Count > MaxSceneBindingMarkers)
            {
                candidates.RemoveRange(MaxSceneBindingMarkers, candidates.Count - MaxSceneBindingMarkers);
            }

            for (int i = 0; i < candidates.Count; i++)
            {
                SceneMarkerCandidate candidate = candidates[i];
                candidate.MarkerId = MakeUniqueMarkerId(candidate.MarkerId, _markerIdBuffer);
                candidates[i] = candidate;
            }
        }

        private static void CollectSceneMarkerCandidates(Scene scene, List<SceneMarkerCandidate> candidates)
        {
            GameObject[] roots = scene.GetRootGameObjects();
            Array.Sort(roots, CompareRootGameObjects);
            for (int i = 0; i < roots.Length; i++)
            {
                CollectSceneMarkerCandidates(roots[i].transform, candidates);
            }
        }

        private static void CollectSceneMarkerCandidates(Transform current, List<SceneMarkerCandidate> candidates)
        {
            if (current == null
                || EditorUtility.IsPersistent(current)
                || (current.hideFlags & HideFlags.HideInHierarchy) != 0)
            {
                return;
            }

            string targetPath = GetHierarchyPath(current);
            int score = GetMarkerCandidateScore(current.name);
            if (score < int.MaxValue)
            {
                candidates.Add(new SceneMarkerCandidate(
                    current.name,
                    targetPath,
                    CreateReadableMarkerId(current.name),
                    score));
            }

            for (int i = 0; i < current.childCount; i++)
            {
                CollectSceneMarkerCandidates(current.GetChild(i), candidates);
            }
        }

        private static int GetMarkerCandidateScore(string name)
        {
            if (ContainsOrdinalIgnoreCase(name, "Player"))
            {
                return 0;
            }

            if (ContainsOrdinalIgnoreCase(name, "Enemy"))
            {
                return 1;
            }

            if (ContainsOrdinalIgnoreCase(name, "Marker"))
            {
                return 2;
            }

            return ContainsOrdinalIgnoreCase(name, "Combat") ? 3 : int.MaxValue;
        }

        private static bool ContainsOrdinalIgnoreCase(string text, string value)
        {
            return !string.IsNullOrEmpty(text)
                && text.IndexOf(value, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static string CreateReadableMarkerId(string name)
        {
            if (string.IsNullOrEmpty(name))
            {
                return string.Empty;
            }

            var builder = new StringBuilder(name.Length);
            bool previousWasSeparator = false;
            for (int i = 0; i < name.Length; i++)
            {
                char ch = name[i];
                if (char.IsLetterOrDigit(ch))
                {
                    builder.Append(ch);
                    previousWasSeparator = false;
                    continue;
                }

                if (!previousWasSeparator && builder.Length > 0)
                {
                    builder.Append('_');
                    previousWasSeparator = true;
                }
            }

            if (builder.Length > 0 && builder[builder.Length - 1] == '_')
            {
                builder.Length--;
            }

            return builder.ToString();
        }

        private static string MakeUniqueMarkerId(string baseId, HashSet<string> usedIds)
        {
            string stableBase = string.IsNullOrEmpty(baseId) ? "Marker" : baseId;
            string markerId = stableBase;
            int suffix = 2;
            while (!usedIds.Add(markerId))
            {
                markerId = stableBase + "_" + suffix;
                suffix++;
            }

            return markerId;
        }

        private static string GetHierarchyPath(Transform transform)
        {
            string path = transform.name;
            Transform current = transform.parent;
            while (current != null)
            {
                path = current.name + "/" + path;
                current = current.parent;
            }

            return path;
        }

        private static int CompareScenes(Scene left, Scene right)
        {
            int pathCompare = string.CompareOrdinal(left.path, right.path);
            return pathCompare != 0 ? pathCompare : string.CompareOrdinal(left.name, right.name);
        }

        private static int CompareRootGameObjects(GameObject left, GameObject right)
        {
            return string.CompareOrdinal(left == null ? string.Empty : left.name, right == null ? string.Empty : right.name);
        }

        private static int CompareSceneMarkerCandidates(SceneMarkerCandidate left, SceneMarkerCandidate right)
        {
            int scoreCompare = left.Score.CompareTo(right.Score);
            return scoreCompare != 0 ? scoreCompare : string.CompareOrdinal(left.TargetPath, right.TargetPath);
        }

        private static void PingObject(UnityEngine.Object target)
        {
            if (target == null)
            {
                return;
            }

            Selection.activeObject = target;
            EditorGUIUtility.PingObject(target);
        }

    }
}
