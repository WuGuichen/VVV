using System;
using System.Collections.Generic;
using MxFramework.Config.Levels;
using MxFramework.Editor.Levels;
using UnityEditor;
using UnityEngine;

namespace MxFramework.Game.CharacterTest.Editor
{
    public sealed partial class CharacterTestWorldObjectLevelEditorModule
    {
        private const float SceneOverlayPointSize = 0.18f;
        private static GUIStyle _sceneOverlayLabelStyle;

        public bool SupportsSceneViewEditing => true;

        public void DrawSceneView(MxLevelEditorSceneViewContext context, SceneView sceneView)
        {
            IReadOnlyList<SceneOverlayItem> items = BuildSceneOverlayItemsForTests(context);
            for (int i = 0; i < items.Count; i++)
            {
                if (ShouldSuppressCommittedRegionOverlay(items[i]))
                    continue;

                DrawSceneOverlayItem(items[i]);
            }

            if (!IsRegionBoundsPreviewActiveFor(context?.SelectedRow?.Key)
                && TryBuildSelectedTriggerAabbForTests(
                    context,
                    out CompiledTriggerAabbPreview preview))
            {
                DrawCompiledTriggerAabb(preview);
            }

            DrawSelectedRegionBoundsHandle(context, sceneView);
        }

        public IReadOnlyList<SceneOverlayItem> BuildSceneOverlayItemsForTests(
            MxLevelEditorSceneViewContext context)
        {
            EnsureLoaded();
            var items = new List<SceneOverlayItem>();
            IReadOnlyList<MxLevelEditorListRow> rows = context?.ScopeRows
                ?? Array.Empty<MxLevelEditorListRow>();
            string selectedKey = context?.SelectedRow?.Key ?? string.Empty;

            for (int i = 0; i < rows.Count; i++)
            {
                if (rows[i]?.Payload is not Row row)
                    continue;

                AddSceneOverlayItemsForRow(row, rows[i], selectedKey, items);
            }

            return items;
        }

        internal bool TryBuildSelectedTriggerAabbForTests(
            MxLevelEditorSceneViewContext context,
            out CompiledTriggerAabbPreview preview)
        {
            preview = null;
            EnsureLoaded();
            if (context?.SelectedRow?.Payload is not Row row
                || row.Kind != RowKind.Region
                || _document?.Regions == null
                || row.Index < 0
                || row.Index >= _document.Regions.Count)
            {
                return false;
            }

            LevelWorldObjectRegionDefinition region =
                _document.Regions[row.Index];
            if (region == null
                || !region.Enabled
                || region.RegionKind
                    != LevelWorldObjectRegionKind.TriggerRegion
                || _triggerCompileResult?.Succeeded != true
                || _triggerDocument?.FindEntriesForRegion(
                        region.StableId).Count <= 0
                || !TryResolveRowTransform(row, out Transform transform)
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

            Vector3 halfExtents = ResolveOverlayHalfExtents(
                definition,
                transform,
                region.ScaleSource);
            if (!IsFinitePositive(halfExtents.x)
                || !IsFinitePositive(halfExtents.y)
                || !IsFinitePositive(halfExtents.z))
            {
                return false;
            }

            preview = new CompiledTriggerAabbPreview(
                context.SelectedRow.Key,
                transform.position,
                halfExtents,
                "玩法触发器 · 编译 AABB");
            return true;
        }

        private void AddSceneOverlayItemsForRow(
            Row row,
            MxLevelEditorListRow listRow,
            string selectedKey,
            List<SceneOverlayItem> items)
        {
            if (row == null || listRow == null || items == null)
                return;

            switch (row.Kind)
            {
                case RowKind.Movable:
                    AddMovableOverlay(row, listRow, selectedKey, items);
                    break;
                case RowKind.SingleStatic:
                    AddSingleStaticOverlay(row, listRow, selectedKey, items);
                    break;
                case RowKind.StaticBatch:
                    AddBatchOverlay(row, listRow, selectedKey, items);
                    break;
                case RowKind.Point:
                    AddPointOverlay(row, listRow, selectedKey, items);
                    break;
                case RowKind.Region:
                    AddRegionOverlay(row, listRow, selectedKey, items);
                    break;
            }
        }

        private void AddMovableOverlay(
            Row row,
            MxLevelEditorListRow listRow,
            string selectedKey,
            List<SceneOverlayItem> items)
        {
            if (_document.MovableInstances == null
                || row.Index < 0
                || row.Index >= _document.MovableInstances.Count)
            {
                return;
            }

            LevelWorldObjectInstanceDefinition instance = _document.MovableInstances[row.Index];
            if (instance == null
                || !TryResolveRowTransform(row, out Transform transform)
                || !TryGetOverlayDefinition(instance.DefinitionId, out CharacterTestWorldObjectDefinition definition))
            {
                return;
            }

            TryGetRowIssueSummary(row, out string issueSeverity, out string issueText);
            items.Add(CreateOverlayItem(
                listRow,
                transform,
                definition,
                instance.ScaleSource,
                instance.Enabled,
                string.Equals(listRow.Key, selectedKey, StringComparison.Ordinal),
                isPoint: false,
                issueSeverity,
                issueText));
        }

        private void AddSingleStaticOverlay(
            Row row,
            MxLevelEditorListRow listRow,
            string selectedKey,
            List<SceneOverlayItem> items)
        {
            if (_document.SingleStaticColliders == null
                || row.Index < 0
                || row.Index >= _document.SingleStaticColliders.Count)
            {
                return;
            }

            LevelWorldObjectInstanceDefinition instance = _document.SingleStaticColliders[row.Index];
            if (instance == null
                || !TryResolveRowTransform(row, out Transform transform)
                || !TryGetOverlayDefinition(instance.DefinitionId, out CharacterTestWorldObjectDefinition definition))
            {
                return;
            }

            TryGetRowIssueSummary(row, out string issueSeverity, out string issueText);
            items.Add(CreateOverlayItem(
                listRow,
                transform,
                definition,
                instance.ScaleSource,
                instance.Enabled,
                string.Equals(listRow.Key, selectedKey, StringComparison.Ordinal),
                isPoint: false,
                issueSeverity,
                issueText));
        }

        private void AddBatchOverlay(
            Row row,
            MxLevelEditorListRow listRow,
            string selectedKey,
            List<SceneOverlayItem> items)
        {
            if (_document.StaticColliderBatches == null
                || row.Index < 0
                || row.Index >= _document.StaticColliderBatches.Count)
            {
                return;
            }

            LevelWorldObjectStaticColliderBatchDefinition batch = _document.StaticColliderBatches[row.Index];
            if (batch == null
                || !TryResolveRowTransform(row, out Transform container)
                || !TryGetOverlayDefinition(batch.DefinitionId, out CharacterTestWorldObjectDefinition definition))
            {
                return;
            }

            bool isSelected = string.Equals(listRow.Key, selectedKey, StringComparison.Ordinal);
            TryGetRowIssueSummary(row, out string issueSeverity, out string issueText);
            for (int childIndex = 0; childIndex < container.childCount; childIndex++)
            {
                Transform child = container.GetChild(childIndex);
                if (child == null)
                    continue;

                items.Add(CreateOverlayItem(
                    listRow,
                    child,
                    definition,
                    batch.ScaleSource,
                    batch.Enabled && child.gameObject.activeSelf,
                    isSelected,
                    isPoint: false,
                    issueSeverity,
                    issueText));
            }
        }

        private void AddPointOverlay(
            Row row,
            MxLevelEditorListRow listRow,
            string selectedKey,
            List<SceneOverlayItem> items)
        {
            if (_document.Points == null
                || row.Index < 0
                || row.Index >= _document.Points.Count)
            {
                return;
            }

            LevelWorldObjectPointDefinition point = _document.Points[row.Index];
            if (point == null
                || !TryResolveRowTransform(row, out Transform transform)
                || !TryGetOverlayDefinition(point.DefinitionId, out CharacterTestWorldObjectDefinition definition))
            {
                return;
            }

            TryGetRowIssueSummary(row, out string issueSeverity, out string issueText);
            items.Add(CreateOverlayItem(
                listRow,
                transform,
                definition,
                LevelWorldObjectScaleSource.Definition,
                point.Enabled,
                string.Equals(listRow.Key, selectedKey, StringComparison.Ordinal),
                isPoint: true,
                issueSeverity,
                issueText));
        }

        private void AddRegionOverlay(
            Row row,
            MxLevelEditorListRow listRow,
            string selectedKey,
            List<SceneOverlayItem> items)
        {
            if (_document.Regions == null
                || row.Index < 0
                || row.Index >= _document.Regions.Count)
            {
                return;
            }

            LevelWorldObjectRegionDefinition region = _document.Regions[row.Index];
            if (region == null
                || !TryResolveRowTransform(row, out Transform transform)
                || !TryGetOverlayDefinition(region.DefinitionId, out CharacterTestWorldObjectDefinition definition))
            {
                return;
            }

            TryGetRowIssueSummary(row, out string issueSeverity, out string issueText);
            items.Add(CreateOverlayItem(
                listRow,
                transform,
                definition,
                region.ScaleSource,
                region.Enabled,
                string.Equals(listRow.Key, selectedKey, StringComparison.Ordinal),
                isPoint: false,
                issueSeverity,
                issueText,
                isRegion: true));
        }

        private static SceneOverlayItem CreateOverlayItem(
            MxLevelEditorListRow listRow,
            Transform transform,
            CharacterTestWorldObjectDefinition definition,
            LevelWorldObjectScaleSource scaleSource,
            bool isEnabled,
            bool isSelected,
            bool isPoint,
            string issueSeverity,
            string issueText,
            bool isRegion = false)
        {
            Vector3 halfExtents = isPoint
                ? Vector3.one * SceneOverlayPointSize
                : ResolveOverlayHalfExtents(definition, transform, scaleSource);
            string label = listRow.Kind
                + " "
                + listRow.Id
                + " | "
                + CharacterTestLevelEditorChineseText.DefinitionName(
                    definition.DefinitionId,
                    definition.DisplayName);
            return new SceneOverlayItem(
                listRow.Key,
                listRow.Kind,
                label,
                transform,
                definition.Shape.ShapeKind,
                transform == null ? Vector3.zero : transform.position,
                halfExtents,
                isSelected,
                isPoint,
                isEnabled,
                issueSeverity,
                issueText,
                isRegion);
        }

        private static bool TryGetOverlayDefinition(
            int definitionId,
            out CharacterTestWorldObjectDefinition definition)
        {
            return CharacterTestWorldObjectSceneSource.BuiltInCatalog.TryGetDefinition(
                definitionId,
                out definition);
        }

        private static Vector3 ResolveOverlayHalfExtents(
            CharacterTestWorldObjectDefinition definition,
            Transform transform,
            LevelWorldObjectScaleSource scaleSource)
        {
            if (scaleSource == LevelWorldObjectScaleSource.Definition)
            {
                return new Vector3(
                    CharacterTestDeterministicUnits.MillimetersToMeters(
                        definition.Shape.HalfExtentXMillimeters),
                    CharacterTestDeterministicUnits.MillimetersToMeters(
                        definition.Shape.HalfExtentYMillimeters),
                    CharacterTestDeterministicUnits.MillimetersToMeters(
                        definition.Shape.HalfExtentZMillimeters));
            }

            Vector3 scale = transform == null ? Vector3.one : transform.lossyScale;
            return new Vector3(
                Mathf.Max(0.01f, Mathf.Abs(scale.x) * 0.5f),
                Mathf.Max(0.01f, Mathf.Abs(scale.y) * 0.5f),
                Mathf.Max(0.01f, Mathf.Abs(scale.z) * 0.5f));
        }

        private static void DrawSceneOverlayItem(SceneOverlayItem item)
        {
            if (item == null || item.Transform == null)
                return;

            Color previousColor = Handles.color;
            Handles.color = ResolveSceneOverlayColor(item);

            if (item.IsPoint)
                DrawPointOverlay(item);
            else
                DrawShapeOverlay(item);

            if (item.IsSelected)
            {
                Vector3 labelOffset = Vector3.up * Mathf.Max(0.35f, item.HalfExtents.y + 0.2f);
                string label = string.IsNullOrWhiteSpace(item.IssueText)
                    ? item.Label
                    : item.Label + "\n" + item.IssueText;
                Handles.Label(item.Center + labelOffset, label, GetSceneOverlayLabelStyle());
            }

            Handles.color = previousColor;
        }

        private static void DrawCompiledTriggerAabb(
            CompiledTriggerAabbPreview preview)
        {
            if (preview == null)
                return;

            Color previousColor = Handles.color;
            Color lineColor = new Color(0.2f, 1f, 0.78f, 1f);
            DrawAabbFill(
                preview.Center,
                preview.HalfExtents,
                new Color(lineColor.r, lineColor.g, lineColor.b, 0.08f));
            Handles.color = lineColor;
            Handles.DrawWireCube(
                preview.Center,
                preview.HalfExtents * 2f);
            Handles.Label(
                preview.Center
                    + Vector3.up
                    * Mathf.Max(0.45f, preview.HalfExtents.y + 0.35f),
                preview.Label,
                GetSceneOverlayLabelStyle());
            Handles.color = previousColor;
        }

        private static Color ResolveSceneOverlayColor(SceneOverlayItem item)
        {
            if (item == null)
                return new Color(0.25f, 0.75f, 1f, 0.45f);
            if (IsRowErrorSeverity(item.IssueSeverity))
                return item.IsSelected
                    ? new Color(1f, 0.22f, 0.16f, 1f)
                    : new Color(1f, 0.22f, 0.16f, 0.55f);
            if (IsRowWarningSeverity(item.IssueSeverity))
                return item.IsSelected
                    ? new Color(1f, 0.8f, 0.2f, 1f)
                    : new Color(1f, 0.8f, 0.2f, 0.55f);
            if (!item.IsEnabled)
                return new Color(0.55f, 0.55f, 0.55f, 0.35f);
            return item.IsSelected
                ? new Color(1f, 0.78f, 0.18f, 1f)
                : new Color(0.25f, 0.75f, 1f, 0.45f);
        }

        private static void DrawShapeOverlay(SceneOverlayItem item)
        {
            switch (item.ShapeKind)
            {
                case CharacterTestMovableNaturalObjectShapeKind.Sphere:
                    DrawSphereOverlay(item.Center, item.HalfExtents);
                    break;
                case CharacterTestMovableNaturalObjectShapeKind.Capsule:
                    DrawCapsuleOverlay(item);
                    break;
                default:
                    if (item.IsRegion)
                    {
                        Color lineColor = Handles.color;
                        float alpha = item.IsSelected ? 0.12f : 0.035f;
                        DrawAabbFill(
                            item.Center,
                            item.HalfExtents,
                            new Color(lineColor.r, lineColor.g, lineColor.b, alpha));
                        Handles.color = lineColor;
                    }
                    Handles.DrawWireCube(item.Center, item.HalfExtents * 2f);
                    break;
            }
        }

        private static void DrawPointOverlay(SceneOverlayItem item)
        {
            float size = HandleUtility.GetHandleSize(item.Center) * 0.12f;
            Vector3 right = Vector3.right * size;
            Vector3 up = Vector3.up * size;
            Vector3 forward = Vector3.forward * size;
            Handles.DrawLine(item.Center - right, item.Center + right);
            Handles.DrawLine(item.Center - up, item.Center + up);
            Handles.DrawLine(item.Center - forward, item.Center + forward);
            Handles.DrawWireDisc(item.Center, Vector3.up, size);
        }

        private static void DrawSphereOverlay(Vector3 center, Vector3 halfExtents)
        {
            float radius = Mathf.Max(0.01f, Mathf.Max(halfExtents.x, Mathf.Max(halfExtents.y, halfExtents.z)));
            Handles.DrawWireDisc(center, Vector3.up, radius);
            Handles.DrawWireDisc(center, Vector3.right, radius);
            Handles.DrawWireDisc(center, Vector3.forward, radius);
        }

        private static void DrawCapsuleOverlay(SceneOverlayItem item)
        {
            float radius = Mathf.Max(0.01f, Mathf.Max(item.HalfExtents.x, item.HalfExtents.z));
            float halfHeight = Mathf.Max(radius, item.HalfExtents.y);
            Vector3 axis = item.Transform == null ? Vector3.up : item.Transform.rotation * Vector3.up;
            if (axis.sqrMagnitude < 0.0001f)
                axis = Vector3.up;
            axis.Normalize();

            float segmentHalfLength = Mathf.Max(0f, halfHeight - radius);
            Vector3 pointA = item.Center - axis * segmentHalfLength;
            Vector3 pointB = item.Center + axis * segmentHalfLength;
            Vector3 right = Vector3.Cross(axis, Vector3.forward);
            if (right.sqrMagnitude < 0.0001f)
                right = Vector3.Cross(axis, Vector3.right);
            right.Normalize();
            Vector3 forward = Vector3.Cross(right, axis).normalized;

            Handles.DrawWireDisc(pointA, axis, radius);
            Handles.DrawWireDisc(pointB, axis, radius);
            Handles.DrawLine(pointA + right * radius, pointB + right * radius);
            Handles.DrawLine(pointA - right * radius, pointB - right * radius);
            Handles.DrawLine(pointA + forward * radius, pointB + forward * radius);
            Handles.DrawLine(pointA - forward * radius, pointB - forward * radius);
        }

        private static GUIStyle GetSceneOverlayLabelStyle()
        {
            if (_sceneOverlayLabelStyle != null)
                return _sceneOverlayLabelStyle;

            _sceneOverlayLabelStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                normal =
                {
                    textColor = new Color(1f, 0.86f, 0.35f, 1f)
                }
            };
            return _sceneOverlayLabelStyle;
        }

        public sealed class SceneOverlayItem
        {
            public SceneOverlayItem(
                string rowKey,
                string kind,
                string label,
                Transform transform,
                CharacterTestMovableNaturalObjectShapeKind shapeKind,
                Vector3 center,
                Vector3 halfExtents,
                bool isSelected,
                bool isPoint,
                bool isEnabled,
                string issueSeverity,
                string issueText)
                : this(
                    rowKey,
                    kind,
                    label,
                    transform,
                    shapeKind,
                    center,
                    halfExtents,
                    isSelected,
                    isPoint,
                    isEnabled,
                    issueSeverity,
                    issueText,
                    isRegion: false)
            {
            }

            internal SceneOverlayItem(
                string rowKey,
                string kind,
                string label,
                Transform transform,
                CharacterTestMovableNaturalObjectShapeKind shapeKind,
                Vector3 center,
                Vector3 halfExtents,
                bool isSelected,
                bool isPoint,
                bool isEnabled,
                string issueSeverity,
                string issueText,
                bool isRegion)
            {
                RowKey = rowKey ?? string.Empty;
                Kind = kind ?? string.Empty;
                Label = label ?? string.Empty;
                Transform = transform;
                ShapeKind = shapeKind;
                Center = center;
                HalfExtents = halfExtents;
                IsSelected = isSelected;
                IsPoint = isPoint;
                IsEnabled = isEnabled;
                IssueSeverity = issueSeverity ?? string.Empty;
                IssueText = issueText ?? string.Empty;
                IsRegion = isRegion;
            }

            public string RowKey { get; }

            public string Kind { get; }

            public string Label { get; }

            public Transform Transform { get; }

            public CharacterTestMovableNaturalObjectShapeKind ShapeKind { get; }

            public Vector3 Center { get; }

            public Vector3 HalfExtents { get; }

            public bool IsSelected { get; }

            public bool IsPoint { get; }

            public bool IsEnabled { get; }

            internal bool IsRegion { get; }

            public string IssueSeverity { get; }

            public string IssueText { get; }
        }

        internal sealed class CompiledTriggerAabbPreview
        {
            internal CompiledTriggerAabbPreview(
                string rowKey,
                Vector3 center,
                Vector3 halfExtents,
                string label)
            {
                RowKey = rowKey ?? string.Empty;
                Center = center;
                HalfExtents = halfExtents;
                Label = label ?? string.Empty;
            }

            internal string RowKey { get; }

            internal Vector3 Center { get; }

            internal Vector3 HalfExtents { get; }

            internal string Label { get; }
        }
    }
}
