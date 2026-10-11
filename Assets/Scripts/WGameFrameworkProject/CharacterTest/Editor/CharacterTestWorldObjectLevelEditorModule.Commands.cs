using System;
using System.Collections.Generic;
using System.IO;
using MxFramework.Config.Levels;
using MxFramework.Editor.Levels;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace MxFramework.Game.CharacterTest.Editor
{
    public sealed partial class CharacterTestWorldObjectLevelEditorModule
    {
        public void SelectRootSource()
        {
            SceneCacheTarget target = FindSceneCacheTarget();
            if (target == null || target.GameObject == null)
            {
                _status = "当前场景中未找到 CharacterTest GameManager 场景缓存目标。";
                return;
            }

            Selection.activeGameObject = target.GameObject;
            EditorGUIUtility.PingObject(target.GameObject);
            _status = "已选择：" + target.DisplayName;
        }

        private void AddSelected(LevelWorldObjectRole role, int definitionId)
        {
            EnsureLoaded();
            SceneCacheTarget target = FindSceneCacheTarget();
            if (target == null || target.Root == null)
            {
                _status = "当前场景中未找到 CharacterTest GameManager 场景缓存目标。";
                return;
            }

            CharacterTestWorldObjectLevelEditorModel.AddRowsResult result =
                CharacterTestWorldObjectLevelEditorModel.AddTransformsDetailed(
                _document,
                target.Root,
                Selection.transforms,
                role,
                definitionId);
            _status = BuildAddStatus(
                role == LevelWorldObjectRole.Movable ? "可移动物体" : "静态碰撞体",
                result);
            if (result.Added > 0)
                MarkDirty();
        }

        private void AddStaticBatchFromSelection()
        {
            EnsureLoaded();
            SceneCacheTarget target = FindSceneCacheTarget();
            if (target == null || target.Root == null)
            {
                _status = "当前场景中未找到 CharacterTest GameManager 场景缓存目标。";
                return;
            }

            CharacterTestWorldObjectLevelEditorModel.AddRowsResult result =
                CharacterTestWorldObjectLevelEditorModel.AddStaticBatchesDetailed(
                _document,
                target.Root,
                Selection.transforms,
                _selectedStaticDefinitionId,
                colliderId: 1);
            _status = BuildAddStatus("静态碰撞体批次", result);
            if (result.Added > 0)
                MarkDirty();
        }

        private void AddPointFromSelection()
        {
            EnsureLoaded();
            SceneCacheTarget target = FindSceneCacheTarget();
            if (target == null || target.Root == null)
            {
                _status = "当前场景中未找到 CharacterTest GameManager 场景缓存目标。";
                return;
            }

            CharacterTestWorldObjectLevelEditorModel.AddRowsResult result =
                CharacterTestWorldObjectLevelEditorModel.AddPointsDetailed(
                    _document,
                    target.Root,
                    Selection.transforms,
                    _selectedPointDefinitionId);
            _status = BuildStableIdAddStatus("关卡点", result);
            if (result.Added > 0)
                MarkDirty();
        }

        private void AddRegionFromSelection()
        {
            EnsureLoaded();
            SceneCacheTarget target = FindSceneCacheTarget();
            if (target == null || target.Root == null)
            {
                _status = "当前场景中未找到 CharacterTest GameManager 场景缓存目标。";
                return;
            }

            CharacterTestWorldObjectLevelEditorModel.AddRowsResult result =
                CharacterTestWorldObjectLevelEditorModel.AddRegionsDetailed(
                    _document,
                    target.Root,
                    Selection.transforms,
                    _selectedRegionDefinitionId);
            _status = BuildStableIdAddStatus("区域", result);
            if (result.Added > 0)
                MarkDirty();
        }

        private static string BuildAddStatus(
            string noun,
            CharacterTestWorldObjectLevelEditorModel.AddRowsResult result)
        {
            noun = string.IsNullOrWhiteSpace(noun) ? "条目" : noun.Trim();
            if (result.Added <= 0)
            {
                string skipped = result.SkippedTotal > 0
                    ? " 已跳过=" + result.SkippedTotal + "，重复=" + result.SkippedDuplicate + "，无效=" + result.SkippedInvalid
                    : string.Empty;
                return "未将任何选中 Transform 添加为" + noun + "。" + skipped;
            }

            string ids = result.HasAssignedIds
                ? "ID=" + FormatIdRange(result.FirstAssignedId, result.LastAssignedId)
                : string.Empty;
            string skippedText = result.SkippedTotal > 0
                ? " 已跳过=" + result.SkippedTotal + "，重复=" + result.SkippedDuplicate + "，无效=" + result.SkippedInvalid
                : string.Empty;
            return "已添加 "
                + result.Added
                + " 个"
                + noun
                + "，自动分配 "
                + (string.IsNullOrWhiteSpace(ids) ? "ID" : ids)
                + "。"
                + skippedText;
        }

        private static string BuildStableIdAddStatus(
            string noun,
            CharacterTestWorldObjectLevelEditorModel.AddRowsResult result)
        {
            noun = string.IsNullOrWhiteSpace(noun) ? "条目" : noun.Trim();
            if (result.Added <= 0)
            {
                string skipped = result.SkippedTotal > 0
                    ? " 已跳过=" + result.SkippedTotal + "，重复=" + result.SkippedDuplicate + "，无效=" + result.SkippedInvalid
                    : string.Empty;
                return "未将任何选中 Transform 添加为" + noun + "。" + skipped;
            }

            string skippedText = result.SkippedTotal > 0
                ? " 已跳过=" + result.SkippedTotal + "，重复=" + result.SkippedDuplicate + "，无效=" + result.SkippedInvalid
                : string.Empty;
            return "已添加 "
                + result.Added
                + " 个"
                + noun
                + "，并自动生成稳定 ID。"
                + skippedText;
        }

        private static string FormatIdRange(int firstId, int lastId)
        {
            return firstId == lastId ? firstId.ToString() : firstId + "-" + lastId;
        }

        private string BuildValidationBlockedStatus(string action)
        {
            action = string.IsNullOrWhiteSpace(action) ? "操作" : action.Trim();
            string detail = string.Empty;
            if (_report != null)
            {
                for (int i = 0; i < _report.Issues.Count; i++)
                {
                    LevelWorldObjectAuthoringIssue issue = _report.Issues[i];
                    if (issue.Severity != LevelWorldObjectAuthoringSeverity.Error)
                        continue;

                    detail = " 首个问题="
                        + issue.Code
                        + " "
                        + issue.Path;
                    break;
                }
            }

            return action
                + "已被校验错误阻止：错误="
                + (_report?.ErrorCount ?? 0)
                + "，警告="
                + (_report?.WarningCount ?? 0)
                + "。"
                + detail;
        }

        private void RepairTransformRefs()
        {
            EnsureLoaded();
            SceneCacheTarget target = FindSceneCacheTarget();
            if (target == null || target.Root == null)
            {
                _status = "当前场景中未找到 CharacterTest GameManager 场景缓存目标。";
                return;
            }

            int repaired = CharacterTestWorldObjectLevelEditorModel.RepairTransformRefs(_document, target.Root);
            _status = "已修复 " + repaired + " 个 Transform 引用。";
            if (repaired > 0)
                MarkDirty();
        }

        private void SelectTransform(LevelTransformReference transformRef)
        {
            if (!TryResolveTransform(transformRef, out Transform transform))
                return;

            Selection.activeTransform = transform;
            EditorGUIUtility.PingObject(transform.gameObject);
        }

        private void PingTransform(LevelTransformReference transformRef)
        {
            if (TryResolveTransform(transformRef, out Transform transform))
                EditorGUIUtility.PingObject(transform.gameObject);
        }

        private void FrameTransform(LevelTransformReference transformRef)
        {
            if (!TryResolveTransform(transformRef, out Transform transform))
                return;

            Selection.activeTransform = transform;
            EditorGUIUtility.PingObject(transform.gameObject);
            SceneView.lastActiveSceneView?.FrameSelected();
        }

        private bool TryResolveTransform(LevelTransformReference transformRef, out Transform transform)
        {
            transform = null;
            SceneCacheTarget target = FindSceneCacheTarget();
            if (target == null || target.Root == null)
            {
                _status = "当前场景中未找到 CharacterTest GameManager 场景缓存目标。";
                return false;
            }

            if (!CharacterTestWorldObjectAuthoringAdapter.TryResolveTransform(
                    target.Root,
                    transformRef,
                    out transform))
            {
                _status = "找不到 Transform 引用：" + (transformRef?.Path ?? "<无>");
                return false;
            }

            return true;
        }

        public bool TryResolveRowObject(
            MxLevelEditorContext context,
            MxLevelEditorListRow row,
            out Object target)
        {
            target = null;
            EnsureLoaded();
            if (row?.Payload is not Row payload)
                return false;

            if (!TryResolveRowTransform(payload, out Transform transform))
                return false;

            target = transform.gameObject;
            return target != null;
        }

        public bool TryResolveSelection(
            MxLevelEditorContext context,
            Object selectedObject,
            out MxLevelEditorSelectionTarget target)
        {
            target = null;
            EnsureLoaded();

            SceneCacheTarget cacheTarget = FindSceneCacheTarget();
            Transform selectedTransform = ResolveSelectionTransform(selectedObject);
            if (cacheTarget == null
                || cacheTarget.Root == null
                || selectedTransform == null
                || !IsChildOrSelf(cacheTarget.Root, selectedTransform))
            {
                return false;
            }

            Transform current = selectedTransform;
            while (current != null && IsChildOrSelf(cacheTarget.Root, current))
            {
                LevelTransformReference currentRef =
                    CharacterTestWorldObjectAuthoringAdapter.CreateTransformRef(cacheTarget.Root, current);
                string currentPath = NormalizePath(currentRef.Path);
                if (TryResolveSelectionTargetByExactPath(currentPath, out target))
                    return true;

                if (TryResolveBatchSelectionTarget(cacheTarget.Root, current, out target))
                    return true;

                if (current == cacheTarget.Root)
                    break;
                current = current.parent;
            }

            return false;
        }

        private bool TryResolveRowTransform(Row row, out Transform transform)
        {
            transform = null;
            SceneCacheTarget target = FindSceneCacheTarget();
            if (target == null || target.Root == null || row == null)
                return false;

            LevelTransformReference transformRef = null;
            switch (row.Kind)
            {
                case RowKind.Movable:
                    if (row.Index >= 0 && row.Index < _document.MovableInstances.Count)
                        transformRef = _document.MovableInstances[row.Index].TransformRef;
                    break;
                case RowKind.SingleStatic:
                    if (row.Index >= 0 && row.Index < _document.SingleStaticColliders.Count)
                        transformRef = _document.SingleStaticColliders[row.Index].TransformRef;
                    break;
                case RowKind.StaticBatch:
                    if (row.Index >= 0 && row.Index < _document.StaticColliderBatches.Count)
                        transformRef = _document.StaticColliderBatches[row.Index].ContainerTransformRef;
                    break;
                case RowKind.Point:
                    if (row.Index >= 0 && row.Index < _document.Points.Count)
                        transformRef = _document.Points[row.Index].TransformRef;
                    break;
                case RowKind.Region:
                    if (row.Index >= 0 && row.Index < _document.Regions.Count)
                        transformRef = _document.Regions[row.Index].TransformRef;
                    break;
            }

            return transformRef != null
                && CharacterTestWorldObjectAuthoringAdapter.TryResolveTransform(
                    target.Root,
                    transformRef,
                    out transform);
        }

        private bool TryResolveSelectionTargetByExactPath(
            string path,
            out MxLevelEditorSelectionTarget target)
        {
            target = null;
            if (string.IsNullOrWhiteSpace(path))
                return false;

            for (int i = 0; i < _document.MovableInstances.Count; i++)
            {
                LevelWorldObjectInstanceDefinition instance = _document.MovableInstances[i];
                if (instance != null && PathsEqual(path, ResolveTransformPath(instance.TransformRef, instance.TransformPath)))
                {
                    target = new MxLevelEditorSelectionTarget("movables", CreateInstanceRowKey(instance));
                    return true;
                }
            }

            for (int i = 0; i < _document.SingleStaticColliders.Count; i++)
            {
                LevelWorldObjectInstanceDefinition instance = _document.SingleStaticColliders[i];
                if (instance != null && PathsEqual(path, ResolveTransformPath(instance.TransformRef, instance.TransformPath)))
                {
                    target = new MxLevelEditorSelectionTarget("static.single", CreateInstanceRowKey(instance));
                    return true;
                }
            }

            for (int i = 0; i < _document.StaticColliderBatches.Count; i++)
            {
                LevelWorldObjectStaticColliderBatchDefinition batch = _document.StaticColliderBatches[i];
                if (batch != null && PathsEqual(path, ResolveTransformPath(batch.ContainerTransformRef, batch.ContainerTransformPath)))
                {
                    target = new MxLevelEditorSelectionTarget("static.batches", CreateBatchRowKey(batch));
                    return true;
                }
            }

            for (int i = 0; i < _document.Points.Count; i++)
            {
                LevelWorldObjectPointDefinition point = _document.Points[i];
                if (point != null && PathsEqual(path, ResolveTransformPath(point.TransformRef, point.TransformPath)))
                {
                    target = new MxLevelEditorSelectionTarget("points", CreatePointRowKey(point));
                    return true;
                }
            }

            for (int i = 0; i < _document.Regions.Count; i++)
            {
                LevelWorldObjectRegionDefinition region = _document.Regions[i];
                if (region != null && PathsEqual(path, ResolveTransformPath(region.TransformRef, region.TransformPath)))
                {
                    target = new MxLevelEditorSelectionTarget("regions", CreateRegionRowKey(region));
                    return true;
                }
            }

            return false;
        }

        private bool TryResolveBatchSelectionTarget(
            Transform root,
            Transform candidate,
            out MxLevelEditorSelectionTarget target)
        {
            target = null;
            if (root == null || candidate == null || candidate.parent == null)
                return false;

            LevelTransformReference parentRef =
                CharacterTestWorldObjectAuthoringAdapter.CreateTransformRef(root, candidate.parent);
            string parentPath = NormalizePath(parentRef.Path);
            for (int i = 0; i < _document.StaticColliderBatches.Count; i++)
            {
                LevelWorldObjectStaticColliderBatchDefinition batch = _document.StaticColliderBatches[i];
                if (batch == null
                    || !PathsEqual(parentPath, ResolveTransformPath(batch.ContainerTransformRef, batch.ContainerTransformPath)))
                {
                    continue;
                }

                target = new MxLevelEditorSelectionTarget("static.batches", CreateBatchRowKey(batch));
                return true;
            }

            return false;
        }

        private static Transform ResolveSelectionTransform(Object selectedObject)
        {
            switch (selectedObject)
            {
                case GameObject gameObject:
                    return gameObject.transform;
                case Component component:
                    return component.transform;
                default:
                    return null;
            }
        }

        private static bool IsChildOrSelf(Transform root, Transform candidate)
        {
            Transform current = candidate;
            while (current != null)
            {
                if (current == root)
                    return true;
                current = current.parent;
            }

            return false;
        }

        private static bool PathsEqual(string left, string right)
        {
            return string.Equals(NormalizePath(left), NormalizePath(right), StringComparison.Ordinal);
        }

        private static string NormalizePath(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim().Replace('\\', '/');
        }

    }
}
