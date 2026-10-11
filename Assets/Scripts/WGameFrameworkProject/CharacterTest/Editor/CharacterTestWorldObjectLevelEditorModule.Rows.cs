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
        public IReadOnlyList<MxLevelEditorTreeNode> BuildTree(MxLevelEditorContext context)
        {
            _activeContext = context;
            EnsureLoaded();
            var nodes = new List<MxLevelEditorTreeNode>
            {
                new MxLevelEditorTreeNode("document", "全部"),
                new MxLevelEditorTreeNode(
                    "definitions",
                    "对象定义",
                    0,
                    Count(_definitionDocument?.Definitions)),
                new MxLevelEditorTreeNode("movables", "可移动物体", 0, Count(_document.MovableInstances)),
                new MxLevelEditorTreeNode("static", "静态碰撞体", 0, Count(_document.SingleStaticColliders) + Count(_document.StaticColliderBatches)),
                new MxLevelEditorTreeNode("static.single", "单个静态碰撞体", 1, Count(_document.SingleStaticColliders)),
                new MxLevelEditorTreeNode("static.batches", "静态碰撞体批次", 1, Count(_document.StaticColliderBatches)),
                new MxLevelEditorTreeNode("points", "关卡点", 0, Count(_document.Points)),
                new MxLevelEditorTreeNode("regions", "区域", 0, Count(_document.Regions))
            };

            var groups = new SortedSet<string>(StringComparer.Ordinal);
            AddGroups(_document.MovableInstances, groups);
            AddGroups(_document.SingleStaticColliders, groups);
            AddBatchGroups(groups);
            AddPointGroups(groups);
            AddRegionGroups(groups);
            foreach (string group in groups)
                nodes.Add(new MxLevelEditorTreeNode("group:" + group, group, 0, CountRowsForGroup(group)));
            return nodes;
        }

        public IReadOnlyList<MxLevelEditorListRow> BuildRows(
            MxLevelEditorContext context,
            string selectedTreeNodeId)
        {
            _activeContext = context;
            _activeTreeNodeId = string.IsNullOrWhiteSpace(selectedTreeNodeId)
                ? "document"
                : selectedTreeNodeId;
            EnsureLoaded();
            if (string.Equals(_activeTreeNodeId, "definitions", StringComparison.Ordinal))
                return BuildDefinitionRows();
            RebuildRows();
            var rows = new List<MxLevelEditorListRow>(_rows.Count);
            for (int i = 0; i < _rows.Count; i++)
            {
                Row row = _rows[i];
                if (row == null || !RowMatchesTree(row, selectedTreeNodeId))
                    continue;

                rows.Add(new MxLevelEditorListRow(
                    row.Key,
                    row.KindLabel,
                    row.Group,
                    row.Definition,
                    row.Id,
                    row.Detail,
                    row.Status,
                    row.SearchText,
                    row));
            }

            return rows;
        }


        private VisualElement MakeRow()
        {
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Column;
            row.style.justifyContent = Justify.Center;
            row.style.paddingLeft = 6;
            row.Add(new Label { name = "title" });
            row.Add(new Label { name = "detail" });
            return row;
        }

        private void BindRow(VisualElement element, int index)
        {
            Row row = index >= 0 && index < _rows.Count ? _rows[index] : null;
            element.Q<Label>("title").text = row == null ? string.Empty : row.Title;
            element.Q<Label>("detail").text = row == null ? string.Empty : row.Detail;
        }

        private void OnSelectionChanged(IEnumerable<object> selected)
        {
            _selectedRow = null;
            foreach (object item in selected)
            {
                _selectedRow = item as Row;
                break;
            }

            RefreshInspector();
        }

        private void RebuildRows()
        {
            _rows.Clear();
            AddRows(_document.MovableInstances, RowKind.Movable, "可移动物体");
            AddRows(_document.SingleStaticColliders, RowKind.SingleStatic, "静态碰撞体");
            AddBatchRows();
            AddPointRows();
            AddRegionRows();
        }

        private void AddRows(
            IReadOnlyList<LevelWorldObjectInstanceDefinition> instances,
            RowKind kind,
            string label)
        {
            if (instances == null)
                return;

            for (int i = 0; i < instances.Count; i++)
            {
                LevelWorldObjectInstanceDefinition instance = instances[i];
                if (instance == null)
                    continue;

                _rows.Add(new Row(
                    kind,
                    i,
                    CreateInstanceRowKey(instance),
                    label + " | " + instance.Group + " | 定义=" + instance.DefinitionId,
                    instance.TransformRef?.Path ?? string.Empty,
                    instance.Group,
                    ResolveDefinitionLabel(instance.DefinitionId),
                    kind == RowKind.Movable
                        ? instance.NumericInstanceId.ToString()
                        : instance.BodyId + ":" + (instance.ColliderId > 0 ? instance.ColliderId : 1),
                    instance.Enabled ? string.Empty : "disabled",
                    CreateInstanceSearchText(instance)));
            }
        }

        private void AddBatchRows()
        {
            if (_document.StaticColliderBatches == null)
                return;

            for (int i = 0; i < _document.StaticColliderBatches.Count; i++)
            {
                LevelWorldObjectStaticColliderBatchDefinition batch = _document.StaticColliderBatches[i];
                if (batch == null)
                    continue;

                _rows.Add(new Row(
                    RowKind.StaticBatch,
                    i,
                    CreateBatchRowKey(batch),
                    "静态碰撞体批次 | " + batch.Group + " | 定义=" + batch.DefinitionId + " 起始刚体 ID=" + batch.BodyIdStart,
                    batch.ContainerTransformRef?.Path ?? string.Empty,
                    batch.Group,
                    ResolveDefinitionLabel(batch.DefinitionId),
                    batch.BodyIdStart + "+",
                    batch.Enabled ? string.Empty : "disabled",
                    CreateBatchSearchText(batch)));
            }
        }

        private void AddPointRows()
        {
            if (_document.Points == null)
                return;

            for (int i = 0; i < _document.Points.Count; i++)
            {
                LevelWorldObjectPointDefinition point = _document.Points[i];
                if (point == null)
                    continue;

                _rows.Add(new Row(
                    RowKind.Point,
                    i,
                    CreatePointRowKey(point),
                    "关卡点 | " + CharacterTestLevelEditorChineseText.PointKind(point.PointKind) + " | 定义=" + point.DefinitionId,
                    point.TransformRef?.Path ?? string.Empty,
                    point.Group,
                    ResolveDefinitionLabel(point.DefinitionId),
                    point.StableId,
                    point.Enabled ? string.Empty : "disabled",
                    CreatePointSearchText(point)));
            }
        }

        private void AddRegionRows()
        {
            if (_document.Regions == null)
                return;

            for (int i = 0; i < _document.Regions.Count; i++)
            {
                LevelWorldObjectRegionDefinition region = _document.Regions[i];
                if (region == null)
                    continue;

                _rows.Add(new Row(
                    RowKind.Region,
                    i,
                    CreateRegionRowKey(region),
                    "区域 | " + CharacterTestLevelEditorChineseText.RegionKind(region.RegionKind) + " | 定义=" + region.DefinitionId,
                    region.TransformRef?.Path ?? string.Empty,
                    region.Group,
                    ResolveDefinitionLabel(region.DefinitionId),
                    region.StableId,
                    region.Enabled ? string.Empty : "disabled",
                    CreateRegionSearchText(region)));
            }
        }

        private static int Count<T>(IReadOnlyList<T> values)
        {
            return values == null ? 0 : values.Count;
        }

        private static void AddGroups(
            IReadOnlyList<LevelWorldObjectInstanceDefinition> instances,
            SortedSet<string> groups)
        {
            if (instances == null || groups == null)
                return;

            for (int i = 0; i < instances.Count; i++)
            {
                string group = instances[i]?.Group;
                if (!string.IsNullOrWhiteSpace(group))
                    groups.Add(group.Trim());
            }
        }

        private void AddBatchGroups(SortedSet<string> groups)
        {
            if (_document?.StaticColliderBatches == null || groups == null)
                return;

            for (int i = 0; i < _document.StaticColliderBatches.Count; i++)
            {
                string group = _document.StaticColliderBatches[i]?.Group;
                if (!string.IsNullOrWhiteSpace(group))
                    groups.Add(group.Trim());
            }
        }

        private void AddPointGroups(SortedSet<string> groups)
        {
            if (_document?.Points == null || groups == null)
                return;

            for (int i = 0; i < _document.Points.Count; i++)
            {
                string group = _document.Points[i]?.Group;
                if (!string.IsNullOrWhiteSpace(group))
                    groups.Add(group.Trim());
            }
        }

        private void AddRegionGroups(SortedSet<string> groups)
        {
            if (_document?.Regions == null || groups == null)
                return;

            for (int i = 0; i < _document.Regions.Count; i++)
            {
                string group = _document.Regions[i]?.Group;
                if (!string.IsNullOrWhiteSpace(group))
                    groups.Add(group.Trim());
            }
        }

        private int CountRowsForGroup(string group)
        {
            if (string.IsNullOrWhiteSpace(group))
                return 0;

            RebuildRows();
            int count = 0;
            for (int i = 0; i < _rows.Count; i++)
            {
                if (string.Equals(_rows[i].Group, group, StringComparison.Ordinal))
                    count++;
            }

            return count;
        }

        private int CountRowsForTree(string selectedTreeNodeId)
        {
            RebuildRows();
            int count = 0;
            for (int i = 0; i < _rows.Count; i++)
            {
                if (RowMatchesTree(_rows[i], selectedTreeNodeId))
                    count++;
            }

            return count;
        }

        private string ResolveTreeLabel(string selectedTreeNodeId)
        {
            if (string.IsNullOrWhiteSpace(selectedTreeNodeId))
                return "全部";

            switch (selectedTreeNodeId)
            {
                case "document":
                    return "全部";
                case "movables":
                    return "可移动物体";
                case "static":
                    return "静态碰撞体";
                case "static.single":
                    return "单个静态碰撞体";
                case "static.batches":
                    return "静态碰撞体批次";
                case "points":
                    return "关卡点";
                case "regions":
                    return "区域";
                default:
                    if (selectedTreeNodeId.StartsWith("group:", StringComparison.Ordinal))
                        return selectedTreeNodeId.Substring("group:".Length);
                    return selectedTreeNodeId;
            }
        }

        private static bool RowMatchesTree(Row row, string selectedTreeNodeId)
        {
            if (row == null || string.IsNullOrWhiteSpace(selectedTreeNodeId))
                return true;

            switch (selectedTreeNodeId)
            {
                case "document":
                    return true;
                case "movables":
                    return row.Kind == RowKind.Movable;
                case "static":
                    return row.Kind == RowKind.SingleStatic || row.Kind == RowKind.StaticBatch;
                case "static.single":
                    return row.Kind == RowKind.SingleStatic;
                case "static.batches":
                    return row.Kind == RowKind.StaticBatch;
                case "points":
                    return row.Kind == RowKind.Point;
                case "regions":
                    return row.Kind == RowKind.Region;
                default:
                    if (selectedTreeNodeId.StartsWith("group:", StringComparison.Ordinal))
                    {
                        string group = selectedTreeNodeId.Substring("group:".Length);
                        return string.Equals(row.Group, group, StringComparison.Ordinal);
                    }

                    return true;
            }
        }

        private string ResolveDefinitionLabel(int definitionId)
        {
            if (ActiveDefinitionCatalog.TryGetDefinition(
                    definitionId,
                    out CharacterTestWorldObjectDefinition definition))
            {
                return CharacterTestLevelEditorChineseText.DefinitionLabel(definition);
            }

            return definitionId.ToString();
        }

        private static string CreateInstanceRowKey(LevelWorldObjectInstanceDefinition instance)
        {
            if (instance == null)
                return string.Empty;

            string prefix = instance.Role == LevelWorldObjectRole.Movable ? "movable" : "static";
            string id = instance.Role == LevelWorldObjectRole.Movable
                ? instance.NumericInstanceId.ToString()
                : instance.BodyId + ":" + (instance.ColliderId > 0 ? instance.ColliderId : 1);
            return prefix + ":" + id + ":" + ResolveTransformPath(instance.TransformRef, instance.TransformPath);
        }

        private static string CreateBatchRowKey(LevelWorldObjectStaticColliderBatchDefinition batch)
        {
            if (batch == null)
                return string.Empty;

            return "batch:"
                + batch.BodyIdStart
                + ":"
                + (batch.ColliderId > 0 ? batch.ColliderId : 1)
                + ":"
                + ResolveTransformPath(batch.ContainerTransformRef, batch.ContainerTransformPath);
        }

        private static string CreatePointRowKey(LevelWorldObjectPointDefinition point)
        {
            if (point == null)
                return string.Empty;

            return "point:" + point.StableId + ":" + ResolveTransformPath(point.TransformRef, point.TransformPath);
        }

        private static string CreateRegionRowKey(LevelWorldObjectRegionDefinition region)
        {
            if (region == null)
                return string.Empty;

            return "region:" + region.StableId + ":" + ResolveTransformPath(region.TransformRef, region.TransformPath);
        }

        private string CreateInstanceSearchText(LevelWorldObjectInstanceDefinition instance)
        {
            if (instance == null)
                return string.Empty;

            return CreateSearchText(
                instance.DefinitionId,
                instance.TransformRef,
                instance.TransformPath,
                instance.Group,
                instance.Notes,
                instance.Role.ToString(),
                instance.NumericInstanceId.ToString(),
                instance.InstanceStableId,
                instance.BodyId.ToString(),
                instance.ColliderId.ToString(),
                instance.ScaleSource.ToString());
        }

        private string CreateBatchSearchText(LevelWorldObjectStaticColliderBatchDefinition batch)
        {
            if (batch == null)
                return string.Empty;

            return CreateSearchText(
                batch.DefinitionId,
                batch.ContainerTransformRef,
                batch.ContainerTransformPath,
                batch.Group,
                batch.Notes,
                "Batch",
                batch.BodyIdStart.ToString(),
                batch.ColliderId.ToString(),
                batch.ScaleSource.ToString());
        }

        private string CreatePointSearchText(LevelWorldObjectPointDefinition point)
        {
            if (point == null)
                return string.Empty;

            return CreateSearchText(
                point.DefinitionId,
                point.TransformRef,
                point.TransformPath,
                point.Group,
                point.Notes,
                "Point",
                point.StableId,
                point.PointKind.ToString());
        }

        private string CreateRegionSearchText(LevelWorldObjectRegionDefinition region)
        {
            if (region == null)
                return string.Empty;

            return CreateSearchText(
                region.DefinitionId,
                region.TransformRef,
                region.TransformPath,
                region.Group,
                region.Notes,
                "Region",
                region.StableId,
                region.RegionKind.ToString(),
                region.ScaleSource.ToString());
        }

        private string CreateSearchText(
            int definitionId,
            LevelTransformReference transformRef,
            string transformPath,
            string group,
            string notes,
            params string[] extra)
        {
            string text = definitionId
                + " "
                + ResolveDefinitionLabel(definitionId)
                + " "
                + ResolveDefinitionStableId(definitionId)
                + " "
                + ResolveTransformPath(transformRef, transformPath)
                + " "
                + (transformRef?.GlobalObjectId ?? string.Empty)
                + " "
                + (group ?? string.Empty)
                + " "
                + (notes ?? string.Empty);
            if (extra != null)
            {
                for (int i = 0; i < extra.Length; i++)
                    text += " " + (extra[i] ?? string.Empty);
            }

            return text;
        }

        private string ResolveDefinitionStableId(int definitionId)
        {
            if (ActiveDefinitionCatalog.TryGetDefinition(
                    definitionId,
                    out CharacterTestWorldObjectDefinition definition))
            {
                return definition.StableId;
            }

            return string.Empty;
        }

        private static string ResolveTransformPath(
            LevelTransformReference transformRef,
            string legacyPath)
        {
            string path = transformRef?.Path;
            if (string.IsNullOrWhiteSpace(path))
                path = legacyPath;
            return string.IsNullOrWhiteSpace(path) ? string.Empty : path.Trim().Replace('\\', '/');
        }

        private sealed class Row
        {
            public Row(
                RowKind kind,
                int index,
                string key,
                string title,
                string detail,
                string group,
                string definition,
                string id,
                string status,
                string searchText)
            {
                Kind = kind;
                Index = index;
                Key = string.IsNullOrWhiteSpace(key) ? string.Empty : key.Trim();
                Title = title ?? string.Empty;
                Detail = detail ?? string.Empty;
                Group = group ?? string.Empty;
                Definition = definition ?? string.Empty;
                Id = id ?? string.Empty;
                Status = status ?? string.Empty;
                SearchText = searchText ?? string.Empty;
            }

            public RowKind Kind { get; }

            public int Index { get; }

            public string Key { get; }

            public string Title { get; }

            public string Detail { get; }

            public string Group { get; }

            public string Definition { get; }

            public string Id { get; }

            public string Status { get; }

            public string SearchText { get; }

            public string KindLabel
            {
                get
                {
                    switch (Kind)
                    {
                        case RowKind.Movable:
                            return "可移动物体";
                        case RowKind.SingleStatic:
                            return "静态碰撞体";
                        case RowKind.StaticBatch:
                            return "静态碰撞体批次";
                        case RowKind.Point:
                            return "关卡点";
                        case RowKind.Region:
                            return "区域";
                        default:
                            return Kind.ToString();
                    }
                }
            }
        }

        private enum RowKind
        {
            Movable,
            SingleStatic,
            StaticBatch,
            Point,
            Region
        }

    }
}
