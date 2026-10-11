using System;
using System.Collections.Generic;
using MxFramework.Config.Levels;
using MxFramework.Editor.Levels;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace MxFramework.Game.CharacterTest.Editor
{
    public sealed partial class CharacterTestWorldObjectLevelEditorModule
    {
        public enum PlacementRole
        {
            Movable = 0,
            Static = 1,
            Point = 2,
            Region = 3
        }

        private bool _placementActive;
        private PlacementRole _placementRole = PlacementRole.Movable;

        public bool SupportsScenePlacement => true;

        public bool IsScenePlacementActive => _placementActive;

        public void BuildPlacementPanel(
            MxLevelEditorPlacementContext context,
            MxLevelEditorPlacementPanel panel)
        {
            EnsureLoaded();

            var placementRoles = new List<PlacementRole>
            {
                PlacementRole.Movable,
                PlacementRole.Static,
                PlacementRole.Point,
                PlacementRole.Region
            };
            var roleLabels = new List<string>(placementRoles.Count);
            for (int i = 0; i < placementRoles.Count; i++)
                roleLabels.Add(PlacementRoleLabel(placementRoles[i]));
            var roleField = new PopupField<string>(
                "类型",
                roleLabels,
                Mathf.Clamp(placementRoles.IndexOf(_placementRole), 0, placementRoles.Count - 1))
            {
                name = "character-test-placement-role",
                tooltip = "保存值仍为原 PlacementRole 枚举。"
            };
            roleField.AddToClassList("mx-level-editor__placement-field");

            var definitionHost = new VisualElement
            {
                name = "character-test-placement-definition-host"
            };

            void RebuildDefinitionField()
            {
                definitionHost.Clear();
                definitionHost.Add(CreatePlacementDefinitionPopup());
            }

            roleField.RegisterValueChangedCallback(evt =>
            {
                int selected = Mathf.Clamp(roleLabels.IndexOf(evt.newValue), 0, placementRoles.Count - 1);
                _placementRole = placementRoles[selected];
                _status = "放置类型已设为：" + PlacementRoleLabel(_placementRole) + "。";
                RebuildDefinitionField();
                SceneView.RepaintAll();
            });
            panel.FieldsHost.Add(roleField);
            panel.FieldsHost.Add(definitionHost);
            RebuildDefinitionField();

            panel.AddIconToggle(
                "character-test-placement-grid-snap",
                "网格吸附：仅吸附世界坐标 X/Z；步长读取 Unity Grid and Snap 设置："
                    + CharacterTestLevelEditorPlacementUtility.FormatGridStep(),
                "网",
                EditorGUIUtility.IconContent("Grid.BoxTool")?.image as Texture2D,
                CharacterTestLevelEditorPlacementUtility.GridSnapEnabled,
                enabled =>
                {
                    CharacterTestLevelEditorPlacementUtility.GridSnapEnabled = enabled;
                    _status = enabled
                    ? "已开启网格吸附，步长="
                        + CharacterTestLevelEditorPlacementUtility.FormatGridStep()
                        + "。"
                    : "已关闭网格吸附。";
                    SceneView.RepaintAll();
                });

            Button placement = CreateSmallButton("开始放置", () =>
            {
                context?.ClosePlacementSurface();
                BeginCreatePlacement(context?.EditorContext);
            });
            placement.name = "character-test-placement-toggle";
            placement.AddToClassList("mx-level-editor__placement-action");
            placement.SetEnabled(!_placementActive);
            panel.ActionsHost.Add(placement);
        }

        public void DrawScenePlacement(
            MxLevelEditorPlacementContext context,
            SceneView sceneView)
        {
            if (!_placementActive
                || sceneView == null
                || Event.current == null
                || Event.current.type != EventType.Repaint)
            {
                return;
            }

            if (!TryResolveCurrentPlacement(
                    Event.current.mousePosition,
                    out _,
                    out PlacementPreviewItem preview))
                return;

            DrawPlacementPreview(preview);
        }

        public void HandleScenePlacement(
            MxLevelEditorPlacementContext context,
            SceneView sceneView)
        {
            if (!_placementActive || Event.current == null)
                return;

            Event current = Event.current;
            if (current.type == EventType.Layout)
            {
                HandleUtility.AddDefaultControl(GUIUtility.GetControlID(FocusType.Passive));
                return;
            }

            if (current.type == EventType.KeyDown && current.keyCode == KeyCode.Escape)
            {
                CancelScenePlacement(context?.EditorContext);
                current.Use();
                return;
            }

            if (current.type == EventType.MouseMove || current.type == EventType.MouseDrag)
            {
                sceneView?.Repaint();
                return;
            }

            if (current.type != EventType.MouseDown || current.button != 0 || current.alt)
                return;

            if (!TryResolveCurrentPlacement(
                    current.mousePosition,
                    out Vector3 position,
                    out _))
                return;

            PlacementCreateResult result = CommitCurrentPlacement(context, position);
            _status = result.Status;
            if (result.Success)
            {
                _placementActive = false;
                ResetPlacementOperation();
                context?.EditorContext?.RequestRefresh?.Invoke();
            }
            current.Use();
            sceneView?.Repaint();
        }

        public void CancelScenePlacement(MxLevelEditorContext context)
        {
            _placementActive = false;
            ResetPlacementOperation();
            _status = "已取消场景放置。";
            SceneView.RepaintAll();
        }

        public PlacementPreviewItem BuildPlacementPreviewForTests(Vector3 position)
        {
            if (!TryResolvePlacementDefinition(out CharacterTestWorldObjectDefinition definition, out string reason))
            {
                _status = reason;
                return null;
            }

            Vector3 halfExtents = ResolveDefinitionHalfExtents(definition);
            GameObject previewObject = null;
            if (TryResolvePlacementPresentation(
                    definition.DefinitionId,
                    out CharacterTestWorldObjectPresentationBinding presentation,
                    out _)
                && presentation.Kind == CharacterTestWorldObjectPresentationKind.PrefabResource)
            {
                ContentProjection.TryResolvePrefab(
                    presentation.PrefabResource,
                    out previewObject,
                    out _);
            }
            return new PlacementPreviewItem(
                _placementRole,
                definition.DefinitionId,
                CharacterTestLevelEditorChineseText.DefinitionName(
                    definition.DefinitionId,
                    definition.DisplayName),
                definition.Shape.ShapeKind,
                position,
                halfExtents,
                previewObject);
        }

        public void ConfigurePlacementForTests(
            PlacementRole role,
            int definitionId,
            bool active)
        {
            _placementRole = role;
            SetPlacementDefinitionId(role, definitionId);
            _placementActive = active;
        }

        public PlacementCreateResult PlaceObjectForTests(
            PlacementRole role,
            int definitionId,
            Vector3 position)
        {
            _placementRole = role;
            SetPlacementDefinitionId(role, definitionId);
            _placementActive = true;
            EnsureUndoSession();
            using (CharacterTestLevelEditorDocumentUndoSession.Transaction transaction =
                   _undoSession.BeginTransaction("放置 CharacterTest 世界对象"))
            {
                PlacementCreateResult result = CreatePlacedObject(null, position);
                transaction.Commit();
                return result;
            }
        }

        private VisualElement CreatePlacementDefinitionPopup()
        {
            PopupField<string> popup = CreateDefinitionPopup(
                "定义",
                GetPlacementDefinitionId(_placementRole),
                DefinitionMatchesPlacementRole,
                value =>
                {
                    SetPlacementDefinitionId(_placementRole, value);
                    _status = "放置定义已设为：" + value + "。";
                    SceneView.RepaintAll();
                },
                "character-test-placement-definition");
            popup.AddToClassList("mx-level-editor__placement-field");
            return popup;
        }

        private void DrawPlacementPreview(PlacementPreviewItem preview)
        {
            if (preview == null)
                return;

            GameObject previewObject = _placementOperation == PlacementOperation.Create
                ? preview.PreviewObject
                : _placementSourceObject;
            bool drewActualObject = previewObject != null
                && CharacterTestLevelEditorMeshPreview.DrawGameObjectAtPosition(
                    previewObject,
                    preview.Position);
            if (!drewActualObject
                && preview.Role != PlacementRole.Point
                && preview.Role != PlacementRole.Region)
            {
                CharacterTestLevelEditorMeshPreview.DrawPrimitive(
                    preview.ShapeKind,
                    preview.Position,
                    preview.HalfExtents * 2f);
            }

            Color previousColor = Handles.color;
            Handles.color = new Color(0.25f, 1f, 0.55f, 0.9f);
            var overlay = new SceneOverlayItem(
                "placement-preview",
                PlacementRoleLabel(preview.Role),
                "放置 " + preview.DefinitionId + " | " + preview.DisplayName,
                null,
                preview.ShapeKind,
                preview.Position,
                preview.HalfExtents,
                true,
                preview.Role == PlacementRole.Point,
                true,
                string.Empty,
                string.Empty,
                isRegion: preview.Role == PlacementRole.Region);
            if (preview.Role == PlacementRole.Point)
                DrawPointOverlay(overlay);
            else
                DrawShapeOverlay(overlay);
            Handles.Label(
                preview.Position + Vector3.up * Mathf.Max(0.35f, preview.HalfExtents.y + 0.2f),
                overlay.Label,
                GetSceneOverlayLabelStyle());
            Handles.color = previousColor;
        }

        private static string BuildPlacementSuccessStatus(
            CharacterTestWorldObjectDefinition definition,
            CharacterTestWorldObjectLevelEditorModel.AddRowsResult result)
        {
            string ids = result.HasAssignedIds
                ? "，ID=" + (result.FirstAssignedId == result.LastAssignedId
                    ? result.FirstAssignedId.ToString()
                    : result.FirstAssignedId + "-" + result.LastAssignedId)
                : string.Empty;
            return "已放置 "
                + definition.DefinitionId
                + " "
                + CharacterTestLevelEditorChineseText.DefinitionName(
                    definition.DefinitionId,
                    definition.DisplayName)
                + "，并添加 "
                + result.Added
                + " 个制作条目。"
                + ids;
        }

        internal static string PlacementRoleLabel(PlacementRole role)
        {
            switch (role)
            {
                case PlacementRole.Movable:
                    return "可移动物体";
                case PlacementRole.Static:
                    return "静态碰撞体";
                case PlacementRole.Point:
                    return "关卡点";
                case PlacementRole.Region:
                    return "区域";
                default:
                    return role.ToString();
            }
        }

        public sealed class PlacementPreviewItem
        {
            public PlacementPreviewItem(
                PlacementRole role,
                int definitionId,
                string displayName,
                CharacterTestMovableNaturalObjectShapeKind shapeKind,
                Vector3 position,
                Vector3 halfExtents,
                GameObject previewObject = null)
            {
                Role = role;
                DefinitionId = definitionId;
                DisplayName = displayName ?? string.Empty;
                ShapeKind = shapeKind;
                Position = position;
                HalfExtents = halfExtents;
                PreviewObject = previewObject;
            }

            public PlacementRole Role { get; }

            public int DefinitionId { get; }

            public string DisplayName { get; }

            public CharacterTestMovableNaturalObjectShapeKind ShapeKind { get; }

            public Vector3 Position { get; }

            public Vector3 HalfExtents { get; }

            public GameObject PreviewObject { get; }
        }

        public sealed class PlacementCreateResult
        {
            public PlacementCreateResult(
                bool success,
                string status,
                GameObject gameObject,
                string treeNodeId,
                string rowKey,
                int addedRows,
                int firstAssignedId,
                int lastAssignedId)
            {
                Success = success;
                Status = status ?? string.Empty;
                GameObject = gameObject;
                TreeNodeId = treeNodeId ?? string.Empty;
                RowKey = rowKey ?? string.Empty;
                AddedRows = Math.Max(0, addedRows);
                FirstAssignedId = Math.Max(0, firstAssignedId);
                LastAssignedId = Math.Max(0, lastAssignedId);
            }

            public bool Success { get; }

            public string Status { get; }

            public GameObject GameObject { get; }

            public string TreeNodeId { get; }

            public string RowKey { get; }

            public int AddedRows { get; }

            public int FirstAssignedId { get; }

            public int LastAssignedId { get; }

            public static PlacementCreateResult Failed(string status)
            {
                return new PlacementCreateResult(false, status, null, string.Empty, string.Empty, 0, 0, 0);
            }
        }
    }
}
