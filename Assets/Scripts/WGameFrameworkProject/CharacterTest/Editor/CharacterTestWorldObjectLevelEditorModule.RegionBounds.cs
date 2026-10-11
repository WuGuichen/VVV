using System;
using MxFramework.Config.Levels;
using MxFramework.Editor.Levels;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace MxFramework.Game.CharacterTest.Editor
{
    public sealed partial class CharacterTestWorldObjectLevelEditorModule
    {
        private const float MinimumRegionBoundsSize = 0.02f;
        private static readonly Vector3[] AabbFaceVertices = new Vector3[4];
        private readonly BoxBoundsHandle _regionBoundsHandle = new BoxBoundsHandle();
        private string _regionBoundsHandleRowKey = string.Empty;
        private bool _regionBoundsGestureActive;

        private void DrawSelectedRegionBoundsHandle(
            MxLevelEditorSceneViewContext context,
            SceneView sceneView)
        {
            if (!TryResolveSelectedEditableRegion(
                    context,
                    out Row row,
                    out _,
                    out _,
                    out Vector3 center,
                    out Vector3 size))
            {
                CancelRegionBoundsGesture();
                return;
            }

            Event current = Event.current;
            string rowKey = context.SelectedRow.Key;
            if (!string.Equals(
                    _regionBoundsHandleRowKey,
                    rowKey,
                    StringComparison.Ordinal))
            {
                CancelRegionBoundsGesture();
                _regionBoundsHandleRowKey = rowKey;
            }

            if (_regionBoundsGestureActive
                && current != null
                && current.rawType == EventType.KeyDown
                && current.keyCode == KeyCode.Escape)
            {
                GUIUtility.hotControl = 0;
                CancelRegionBoundsGesture();
                current.Use();
                sceneView?.Repaint();
                return;
            }

            if (!_regionBoundsGestureActive)
            {
                _regionBoundsHandle.center = center;
                _regionBoundsHandle.size = size;
            }

            Color handleColor = new Color(1f, 0.78f, 0.18f, 1f);
            _regionBoundsHandle.handleColor = handleColor;
            _regionBoundsHandle.wireframeColor =
                new Color(handleColor.r, handleColor.g, handleColor.b, 0.78f);

            if (_regionBoundsGestureActive)
            {
                DrawAabbFill(
                    _regionBoundsHandle.center,
                    ClampRegionBoundsSize(_regionBoundsHandle.size) * 0.5f,
                    new Color(handleColor.r, handleColor.g, handleColor.b, 0.16f));
            }

            EditorGUI.BeginChangeCheck();
            _regionBoundsHandle.DrawHandle();
            if (EditorGUI.EndChangeCheck())
            {
                _regionBoundsGestureActive = true;
                _regionBoundsHandle.size = ClampRegionBoundsSize(
                    _regionBoundsHandle.size);
                sceneView?.Repaint();
            }

            if (!_regionBoundsGestureActive
                || current == null
                || current.rawType != EventType.MouseUp)
            {
                return;
            }

            Vector3 candidateCenter = _regionBoundsHandle.center;
            Vector3 candidateSize = ClampRegionBoundsSize(
                _regionBoundsHandle.size);
            _regionBoundsGestureActive = false;
            if (!TryApplyRegionBounds(
                    context,
                    row,
                    candidateCenter,
                    candidateSize,
                    out string failureMessage))
            {
                _status = failureMessage;
            }

            sceneView?.Repaint();
        }

        internal bool TryApplySelectedRegionBoundsForTests(
            MxLevelEditorSceneViewContext context,
            Vector3 center,
            Vector3 size,
            out string failureMessage)
        {
            if (!TryResolveSelectedEditableRegion(
                    context,
                    out Row row,
                    out _,
                    out _,
                    out _,
                    out _))
            {
                failureMessage = "当前没有可在 Scene 中调整的 AABB 区域。";
                return false;
            }

            return TryApplyRegionBounds(
                context,
                row,
                center,
                size,
                out failureMessage);
        }

        private bool TryApplyRegionBounds(
            MxLevelEditorSceneViewContext context,
            Row row,
            Vector3 center,
            Vector3 size,
            out string failureMessage)
        {
            failureMessage = string.Empty;
            if (row == null
                || row.Kind != RowKind.Region
                || _document?.Regions == null
                || row.Index < 0
                || row.Index >= _document.Regions.Count
                || !TryResolveRowTransform(row, out Transform transform)
                || transform == null)
            {
                failureMessage = "区域或场景 Transform 已失效，无法调整范围。";
                return false;
            }

            if (!IsFinite(center)
                || !IsFinite(size)
                || size.x < MinimumRegionBoundsSize
                || size.y < MinimumRegionBoundsSize
                || size.z < MinimumRegionBoundsSize)
            {
                failureMessage = "区域尺寸必须是有限且大于零的三维数值。";
                return false;
            }

            if (!TryResolveLocalScaleForWorldSize(
                    transform,
                    size,
                    out Vector3 localScale))
            {
                failureMessage = "区域父节点缩放无效，无法换算 Scene 范围。";
                return false;
            }

            LevelWorldObjectRegionDefinition region =
                _document.Regions[row.Index];
            bool switchedToSceneTransform =
                region.ScaleSource != LevelWorldObjectScaleSource.SceneTransform;
            const string operationName = "在 Scene 中调整 CharacterTest 区域范围";
            EnsureUndoSession();
            using (CharacterTestLevelEditorDocumentUndoSession.Transaction transaction =
                   _undoSession.BeginTransaction(operationName))
            {
                Undo.RecordObject(transform, operationName);
                transform.position = center;
                transform.localScale = localScale;
                region.ScaleSource = LevelWorldObjectScaleSource.SceneTransform;
                EditorUtility.SetDirty(transform);
                EditorSceneManager.MarkSceneDirty(transform.gameObject.scene);
                NotifySceneTransformChanged(transform);
                if (switchedToSceneTransform)
                    MarkDirty();
                else
                    QueueValidation();
                transaction.Commit();
            }

            _status = switchedToSceneTransform
                ? "已调整区域范围，并将尺寸来源切换为场景 Transform。"
                : "已在 Scene 中调整区域范围。";
            RefreshInspector();
            RefreshDiagnostics();
            SceneView.RepaintAll();
            context?.EditorContext?.RequestRefresh?.Invoke();
            return true;
        }

        private bool TryResolveSelectedEditableRegion(
            MxLevelEditorSceneViewContext context,
            out Row row,
            out LevelWorldObjectRegionDefinition region,
            out Transform transform,
            out Vector3 center,
            out Vector3 size)
        {
            row = context?.SelectedRow?.Payload as Row;
            region = null;
            transform = null;
            center = Vector3.zero;
            size = Vector3.zero;
            if (row == null
                || row.Kind != RowKind.Region
                || _document?.Regions == null
                || row.Index < 0
                || row.Index >= _document.Regions.Count)
            {
                return false;
            }

            region = _document.Regions[row.Index];
            if (region == null
                || !TryResolveRowTransform(row, out transform)
                || transform == null
                || !IsFinite(transform.rotation)
                || Quaternion.Angle(
                    transform.rotation,
                    Quaternion.identity) > 0.01f
                || !TryGetOverlayDefinition(
                    region.DefinitionId,
                    out CharacterTestWorldObjectDefinition definition)
                || definition.Shape.ShapeKind
                    != CharacterTestMovableNaturalObjectShapeKind.Aabb)
            {
                return false;
            }

            center = transform.position;
            size = ResolveOverlayHalfExtents(
                definition,
                transform,
                region.ScaleSource) * 2f;
            return IsFinite(center)
                && IsFinite(size)
                && size.x >= MinimumRegionBoundsSize
                && size.y >= MinimumRegionBoundsSize
                && size.z >= MinimumRegionBoundsSize;
        }

        private void CancelRegionBoundsGesture()
        {
            if (_regionBoundsGestureActive && GUIUtility.hotControl != 0)
                GUIUtility.hotControl = 0;
            _regionBoundsGestureActive = false;
            _regionBoundsHandleRowKey = string.Empty;
        }

        private bool ShouldSuppressCommittedRegionOverlay(
            SceneOverlayItem item)
        {
            return item?.IsRegion == true
                && item.IsSelected
                && IsRegionBoundsPreviewActiveFor(item.RowKey);
        }

        private bool IsRegionBoundsPreviewActiveFor(string rowKey)
        {
            return _regionBoundsGestureActive
                && !string.IsNullOrEmpty(rowKey)
                && string.Equals(
                    _regionBoundsHandleRowKey,
                    rowKey,
                    StringComparison.Ordinal);
        }

        private static Vector3 ClampRegionBoundsSize(Vector3 size)
        {
            return new Vector3(
                Mathf.Max(MinimumRegionBoundsSize, Mathf.Abs(size.x)),
                Mathf.Max(MinimumRegionBoundsSize, Mathf.Abs(size.y)),
                Mathf.Max(MinimumRegionBoundsSize, Mathf.Abs(size.z)));
        }

        private static bool TryResolveLocalScaleForWorldSize(
            Transform transform,
            Vector3 worldSize,
            out Vector3 localScale)
        {
            localScale = Vector3.zero;
            if (transform == null)
                return false;

            Vector3 parentScale = transform.parent == null
                ? Vector3.one
                : transform.parent.lossyScale;
            float parentX = Mathf.Abs(parentScale.x);
            float parentY = Mathf.Abs(parentScale.y);
            float parentZ = Mathf.Abs(parentScale.z);
            if (!IsFinite(parentX)
                || !IsFinite(parentY)
                || !IsFinite(parentZ)
                || parentX < 0.0001f
                || parentY < 0.0001f
                || parentZ < 0.0001f)
            {
                return false;
            }

            localScale = new Vector3(
                worldSize.x / parentX,
                worldSize.y / parentY,
                worldSize.z / parentZ);
            return IsFinite(localScale)
                && localScale.x > 0f
                && localScale.y > 0f
                && localScale.z > 0f;
        }

        private static bool IsFinite(Vector3 value)
        {
            return IsFinite(value.x)
                && IsFinite(value.y)
                && IsFinite(value.z);
        }

        private static void DrawAabbFill(
            Vector3 center,
            Vector3 halfExtents,
            Color fillColor)
        {
            if (Event.current == null
                || Event.current.type != EventType.Repaint)
            {
                return;
            }

            Vector3 min = center - halfExtents;
            Vector3 max = center + halfExtents;
            Vector3 p000 = new Vector3(min.x, min.y, min.z);
            Vector3 p001 = new Vector3(min.x, min.y, max.z);
            Vector3 p010 = new Vector3(min.x, max.y, min.z);
            Vector3 p011 = new Vector3(min.x, max.y, max.z);
            Vector3 p100 = new Vector3(max.x, min.y, min.z);
            Vector3 p101 = new Vector3(max.x, min.y, max.z);
            Vector3 p110 = new Vector3(max.x, max.y, min.z);
            Vector3 p111 = new Vector3(max.x, max.y, max.z);

            Color previousColor = Handles.color;
            CompareFunction previousZTest = Handles.zTest;
            Handles.color = fillColor;
            Handles.zTest = CompareFunction.LessEqual;
            DrawAabbFace(p000, p001, p011, p010);
            DrawAabbFace(p100, p110, p111, p101);
            DrawAabbFace(p000, p100, p101, p001);
            DrawAabbFace(p010, p011, p111, p110);
            DrawAabbFace(p000, p010, p110, p100);
            DrawAabbFace(p001, p101, p111, p011);
            Handles.zTest = previousZTest;
            Handles.color = previousColor;
        }

        private static void DrawAabbFace(
            Vector3 a,
            Vector3 b,
            Vector3 c,
            Vector3 d)
        {
            AabbFaceVertices[0] = a;
            AabbFaceVertices[1] = b;
            AabbFaceVertices[2] = c;
            AabbFaceVertices[3] = d;
            Handles.DrawAAConvexPolygon(AabbFaceVertices);
        }
    }
}
