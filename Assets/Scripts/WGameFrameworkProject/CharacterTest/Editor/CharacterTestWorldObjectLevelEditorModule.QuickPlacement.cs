using System;
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
        private enum PlacementOperation
        {
            Create,
            Duplicate,
            Reposition
        }

        private PlacementOperation _placementOperation;
        private Row _placementSourceRow;
        private GameObject _placementSourceObject;

        private void BeginCreatePlacement(MxLevelEditorContext context)
        {
            _placementOperation = PlacementOperation.Create;
            _placementSourceRow = null;
            _placementSourceObject = null;
            _placementActive = true;
            _status = "放置中：左键确认，Esc 取消。";
            context?.RequestRefresh?.Invoke();
            SceneView.RepaintAll();
        }

        private void BeginDuplicatePlacement(Row row)
        {
            if (!TryBeginObjectPlacement(row, PlacementOperation.Duplicate, "复制并放置"))
                return;

            _status = "复制并放置：左键确认副本位置，Esc 取消。";
        }

        private void BeginRepositionPlacement(Row row)
        {
            if (!TryBeginObjectPlacement(row, PlacementOperation.Reposition, "重新放置"))
                return;

            _status = "重新放置：左键确认新位置，Esc 取消。";
        }

        private bool TryBeginObjectPlacement(
            Row row,
            PlacementOperation operation,
            string operationLabel)
        {
            if (row == null || row.Kind == RowKind.StaticBatch)
            {
                _status = "静态碰撞体批次暂不支持" + operationLabel + "。";
                return false;
            }

            if (!TryResolveRowTransform(row, out Transform transform) || transform == null)
            {
                _status = "无法解析当前登记对象的 Transform，不能" + operationLabel + "。";
                return false;
            }

            _placementOperation = operation;
            _placementSourceRow = row;
            _placementSourceObject = transform.gameObject;
            _placementActive = true;
            _activeContext?.RequestRefresh?.Invoke();
            SceneView.RepaintAll();
            return true;
        }

        private bool TryResolveCurrentPlacement(
            Vector2 guiPosition,
            out Vector3 pivotPosition,
            out PlacementPreviewItem preview)
        {
            pivotPosition = default;
            preview = null;
            if (!CharacterTestLevelEditorPlacementUtility.TryResolveSurfacePosition(
                    guiPosition,
                    out Vector3 surfacePosition,
                    out _))
            {
                return false;
            }

            if (_placementOperation == PlacementOperation.Create)
            {
                if (!TryResolvePlacementDefinition(
                        out CharacterTestWorldObjectDefinition definition,
                        out _))
                {
                    return false;
                }

                float bottomOffset = _placementRole == PlacementRole.Point
                    ? 0f
                    : ResolveDefinitionHalfExtents(definition).y;
                pivotPosition = CharacterTestLevelEditorPlacementUtility.ResolvePivotPosition(
                    null,
                    surfacePosition,
                    bottomOffset);
                preview = BuildPlacementPreviewForTests(pivotPosition);
                return preview != null;
            }

            if (_placementSourceRow == null
                || _placementSourceObject == null
                || !TryGetRowDefinition(
                    _placementSourceRow,
                    out CharacterTestWorldObjectDefinition sourceDefinition))
            {
                return false;
            }

            pivotPosition = CharacterTestLevelEditorPlacementUtility.ResolvePivotPosition(
                _placementSourceObject,
                surfacePosition,
                ResolveDefinitionHalfExtents(sourceDefinition).y);
            preview = new PlacementPreviewItem(
                ToPlacementRole(_placementSourceRow.Kind),
                sourceDefinition.DefinitionId,
                sourceDefinition.DisplayName,
                sourceDefinition.Shape.ShapeKind,
                pivotPosition,
                ResolveDefinitionHalfExtents(sourceDefinition));
            return true;
        }

        private PlacementCreateResult CommitCurrentPlacement(
            MxLevelEditorPlacementContext context,
            Vector3 pivotPosition)
        {
            switch (_placementOperation)
            {
                case PlacementOperation.Duplicate:
                    return DuplicateRegisteredObject(context, pivotPosition);
                case PlacementOperation.Reposition:
                    return RepositionRegisteredObject(context, pivotPosition);
                default:
                    EnsureUndoSession();
                    using (CharacterTestLevelEditorDocumentUndoSession.Transaction transaction =
                           _undoSession.BeginTransaction("放置 CharacterTest 世界对象"))
                    {
                        PlacementCreateResult createResult =
                            CreatePlacedObject(context, pivotPosition);
                        transaction.Commit();
                        return createResult;
                    }
            }
        }

        private PlacementCreateResult DuplicateRegisteredObject(
            MxLevelEditorPlacementContext context,
            Vector3 pivotPosition)
        {
            if (_placementSourceRow == null || _placementSourceObject == null)
                return PlacementCreateResult.Failed("没有可复制的登记对象。");

            EnsureUndoSession();
            using (CharacterTestLevelEditorDocumentUndoSession.Transaction transaction =
                   _undoSession.BeginTransaction("复制并放置 CharacterTest 世界对象"))
            {
                GameObject duplicate = Object.Instantiate(_placementSourceObject);
                Undo.RegisterCreatedObjectUndo(duplicate, "复制并放置 CharacterTest 世界对象");
                duplicate.name = CreateUniqueSiblingName(
                    _placementSourceObject.transform.parent,
                    _placementSourceObject.name + " Copy");
                duplicate.transform.SetParent(
                    _placementSourceObject.transform.parent,
                    worldPositionStays: true);
                duplicate.transform.position = pivotPosition;
                duplicate.transform.rotation = _placementSourceObject.transform.rotation;
                duplicate.transform.localScale = _placementSourceObject.transform.localScale;

                SceneCacheTarget target = FindSceneCacheTarget();
                if (target == null || target.Root == null)
                    return PlacementCreateResult.Failed("当前场景中未找到 CharacterTest 场景根节点。");

                CharacterTestWorldObjectLevelEditorModel.AddRowsResult addResult =
                    AddDuplicateTransformToDocument(
                        _placementSourceRow,
                        duplicate.transform,
                        target.Root);
                if (addResult.Added <= 0)
                    return PlacementCreateResult.Failed("无法为副本创建新的制作登记。");

                MarkDirty();
                transaction.Commit();
                EditorUtility.SetDirty(duplicate);
                EditorSceneManager.MarkSceneDirty(duplicate.scene);
                Selection.activeGameObject = duplicate;
                EditorGUIUtility.PingObject(duplicate);
                if (TryResolveSelection(
                        context?.EditorContext,
                        duplicate,
                        out MxLevelEditorSelectionTarget selectionTarget))
                {
                    context?.EditorContext?.RevealRow(
                        selectionTarget.TreeNodeId,
                        selectionTarget.RowKey);
                }

                return new PlacementCreateResult(
                    true,
                    "已复制并登记：" + duplicate.name + "。",
                    duplicate,
                    GetPlacementTreeNodeId(ToPlacementRole(_placementSourceRow.Kind)),
                    selectionTarget?.RowKey ?? string.Empty,
                    addResult.Added,
                    addResult.FirstAssignedId,
                    addResult.LastAssignedId);
            }
        }

        private PlacementCreateResult RepositionRegisteredObject(
            MxLevelEditorPlacementContext context,
            Vector3 pivotPosition)
        {
            if (_placementSourceRow == null || _placementSourceObject == null)
                return PlacementCreateResult.Failed("没有可重新放置的登记对象。");

            EnsureUndoSession();
            using (CharacterTestLevelEditorDocumentUndoSession.Transaction transaction =
                   _undoSession.BeginTransaction("重新放置 CharacterTest 世界对象"))
            {
                Undo.RecordObject(
                    _placementSourceObject.transform,
                    "重新放置 CharacterTest 世界对象");
                _placementSourceObject.transform.position = pivotPosition;
                EditorUtility.SetDirty(_placementSourceObject.transform);
                EditorSceneManager.MarkSceneDirty(_placementSourceObject.scene);
                transaction.Commit();
                Selection.activeGameObject = _placementSourceObject;
                return new PlacementCreateResult(
                    true,
                    "已重新放置：" + _placementSourceObject.name + "。",
                    _placementSourceObject,
                    GetPlacementTreeNodeId(ToPlacementRole(_placementSourceRow.Kind)),
                    _placementSourceRow.Key,
                    0,
                    0,
                    0);
            }
        }

        private CharacterTestWorldObjectLevelEditorModel.AddRowsResult AddDuplicateTransformToDocument(
            Row sourceRow,
            Transform duplicate,
            Transform root)
        {
            int definitionId = GetRowDefinitionId(sourceRow);
            CharacterTestWorldObjectLevelEditorModel.AddRowsResult result;
            switch (sourceRow.Kind)
            {
                case RowKind.Movable:
                    result = CharacterTestWorldObjectLevelEditorModel.AddTransformsDetailed(
                        _document,
                        root,
                        new[] { duplicate },
                        LevelWorldObjectRole.Movable,
                        definitionId);
                    CopyInstanceProperties(sourceRow, duplicate, root, isMovable: true);
                    return result;
                case RowKind.SingleStatic:
                    result = CharacterTestWorldObjectLevelEditorModel.AddTransformsDetailed(
                        _document,
                        root,
                        new[] { duplicate },
                        LevelWorldObjectRole.StaticCollider,
                        definitionId);
                    CopyInstanceProperties(sourceRow, duplicate, root, isMovable: false);
                    return result;
                case RowKind.Point:
                    result = CharacterTestWorldObjectLevelEditorModel.AddPointsDetailed(
                        _document,
                        root,
                        new[] { duplicate },
                        definitionId);
                    CopyPointProperties(sourceRow, duplicate, root);
                    return result;
                case RowKind.Region:
                    result = CharacterTestWorldObjectLevelEditorModel.AddRegionsDetailed(
                        _document,
                        root,
                        new[] { duplicate },
                        definitionId);
                    CopyRegionProperties(sourceRow, duplicate, root);
                    return result;
                default:
                    return CharacterTestWorldObjectLevelEditorModel.AddRowsResult.Empty;
            }
        }

        private void CopyInstanceProperties(
            Row sourceRow,
            Transform duplicate,
            Transform root,
            bool isMovable)
        {
            LevelWorldObjectInstanceDefinition source = isMovable
                ? _document.MovableInstances[sourceRow.Index]
                : _document.SingleStaticColliders[sourceRow.Index];
            LevelWorldObjectInstanceDefinition target = FindInstanceByTransform(
                isMovable ? _document.MovableInstances : _document.SingleStaticColliders,
                root,
                duplicate);
            if (source == null || target == null)
                return;

            target.Enabled = source.Enabled;
            target.DefinitionId = source.DefinitionId;
            target.ScaleSource = source.ScaleSource;
            target.ColliderId = source.ColliderId;
            target.Group = source.Group;
            target.Notes = source.Notes;
        }

        private void CopyPointProperties(Row sourceRow, Transform duplicate, Transform root)
        {
            LevelWorldObjectPointDefinition source = _document.Points[sourceRow.Index];
            string path = CharacterTestWorldObjectAuthoringAdapter.CreateTransformRef(root, duplicate).Path;
            LevelWorldObjectPointDefinition target = _document.Points.Find(
                item => item != null
                    && item.TransformRef != null
                    && string.Equals(item.TransformRef.Path, path, StringComparison.Ordinal));
            if (source == null || target == null)
                return;

            target.Enabled = source.Enabled;
            target.DefinitionId = source.DefinitionId;
            target.PointKind = source.PointKind;
            target.Group = source.Group;
            target.Notes = source.Notes;
        }

        private void CopyRegionProperties(Row sourceRow, Transform duplicate, Transform root)
        {
            LevelWorldObjectRegionDefinition source = _document.Regions[sourceRow.Index];
            string path = CharacterTestWorldObjectAuthoringAdapter.CreateTransformRef(root, duplicate).Path;
            LevelWorldObjectRegionDefinition target = _document.Regions.Find(
                item => item != null
                    && item.TransformRef != null
                    && string.Equals(item.TransformRef.Path, path, StringComparison.Ordinal));
            if (source == null || target == null)
                return;

            target.Enabled = source.Enabled;
            target.DefinitionId = source.DefinitionId;
            target.RegionKind = source.RegionKind;
            target.ScaleSource = source.ScaleSource;
            target.Group = source.Group;
            target.Notes = source.Notes;
        }

        private static LevelWorldObjectInstanceDefinition FindInstanceByTransform(
            System.Collections.Generic.List<LevelWorldObjectInstanceDefinition> instances,
            Transform root,
            Transform transform)
        {
            string path = CharacterTestWorldObjectAuthoringAdapter.CreateTransformRef(root, transform).Path;
            return instances.Find(
                item => item != null
                    && item.TransformRef != null
                    && string.Equals(item.TransformRef.Path, path, StringComparison.Ordinal));
        }

        private static PlacementRole ToPlacementRole(RowKind kind)
        {
            switch (kind)
            {
                case RowKind.SingleStatic:
                    return PlacementRole.Static;
                case RowKind.Point:
                    return PlacementRole.Point;
                case RowKind.Region:
                    return PlacementRole.Region;
                default:
                    return PlacementRole.Movable;
            }
        }

        private static string CreateUniqueSiblingName(Transform parent, string baseName)
        {
            string normalized = string.IsNullOrWhiteSpace(baseName) ? "Level Object Copy" : baseName.Trim();
            if (parent == null || parent.Find(normalized) == null)
                return normalized;

            int suffix = 2;
            while (parent.Find(normalized + " " + suffix) != null)
                suffix++;
            return normalized + " " + suffix;
        }

        private void ResetPlacementOperation()
        {
            _placementOperation = PlacementOperation.Create;
            _placementSourceRow = null;
            _placementSourceObject = null;
        }

        internal PlacementCreateResult DuplicateRowForTests(
            MxLevelEditorListRow row,
            Vector3 position)
        {
            if (row?.Payload is not Row payload
                || !TryBeginObjectPlacement(
                    payload,
                    PlacementOperation.Duplicate,
                    "复制并放置"))
            {
                return PlacementCreateResult.Failed(_status);
            }

            PlacementCreateResult result = DuplicateRegisteredObject(null, position);
            _placementActive = false;
            ResetPlacementOperation();
            return result;
        }

        internal PlacementCreateResult RepositionRowForTests(
            MxLevelEditorListRow row,
            Vector3 position)
        {
            if (row?.Payload is not Row payload
                || !TryBeginObjectPlacement(
                    payload,
                    PlacementOperation.Reposition,
                    "重新放置"))
            {
                return PlacementCreateResult.Failed(_status);
            }

            PlacementCreateResult result = RepositionRegisteredObject(null, position);
            _placementActive = false;
            ResetPlacementOperation();
            return result;
        }

    }
}
