using System;
using System.Collections.Generic;
using MxFramework.Config.Levels;
using MxFramework.Editor.Levels;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace MxFramework.Game.CharacterTest.Editor
{
    public sealed partial class CharacterTestWorldObjectLevelEditorModule
    {
        public SelectionValidationSummary BuildSelectionValidationSummaryForTests(
            MxLevelEditorListRow selectedRow,
            Object selectedObject)
        {
            EnsureLoaded();
            return BuildSelectionValidationSummary(selectedRow?.Payload as Row, selectedObject);
        }

        private SelectionValidationSummary BuildSelectionValidationSummary(
            Row selectedRow,
            Object selectedObject)
        {
            EnsureLoaded();
            if (selectedRow != null)
                return BuildRegisteredSelectionSummary(selectedRow, selectedObject);

            Transform selectedTransform = ResolveSelectionTransform(selectedObject);
            if (selectedObject == null)
                return SelectionValidationSummary.Empty("未选择场景对象。");
            if (selectedTransform == null)
                return SelectionValidationSummary.Empty("当前选择不是场景 Transform。");

            SceneCacheTarget target = FindSceneCacheTarget();
            if (target == null || target.Root == null)
                return SelectionValidationSummary.Empty("未找到 CharacterTest GameManager 场景缓存目标。");
            if (!IsChildOrSelf(target.Root, selectedTransform))
            {
                return SelectionValidationSummary.Empty(
                    "当前选择不在 " + target.DisplayName + " 内。");
            }

            if (TryResolveSelection(_activeContext, selectedObject, out MxLevelEditorSelectionTarget rowTarget)
                && TryFindRowByKey(rowTarget.RowKey, out Row resolvedRow))
            {
                return BuildRegisteredSelectionSummary(resolvedRow, selectedObject);
            }

            LevelTransformReference transformRef =
                CharacterTestWorldObjectAuthoringAdapter.CreateTransformRef(target.Root, selectedTransform);
            string path = NormalizePath(transformRef.Path);
            return new SelectionValidationSummary(
                isRegistered: false,
                isAddableSelection: true,
                isBatchChild: false,
                title: "未登记",
                status: "未登记",
                role: "未指定",
                definition: "无",
                group: CharacterTestWorldObjectAuthoringAdapter.ResolveGroup(path),
                transformPath: path,
                transformStatus: "可解析",
                runtimeCache: "添加前不会进入 CharacterTest 运行时缓存。",
                shape: "无",
                material: "无",
                potency: "无",
                issueSeverity: string.Empty,
                issueText: string.Empty,
                bodyIdPreview: string.Empty,
                colliderIdPreview: string.Empty,
                rowKey: string.Empty);
        }

        private SelectionValidationSummary BuildRegisteredSelectionSummary(
            Row row,
            Object selectedObject)
        {
            if (row == null)
                return SelectionValidationSummary.Empty("未选择条目。");

            bool transformResolved = TryResolveRowTransform(row, out Transform rowTransform);
            TryGetRowDefinition(row, out CharacterTestWorldObjectDefinition definition);
            bool enabled = IsRowEnabled(row);
            bool roleMatches = definition.DefinitionId > 0 && DefinitionMatchesRow(row, definition);
            TryGetRowIssueSummary(row, out string severity, out string issueText);
            TryResolveBatchChild(row, selectedObject, out Transform batchChild, out int childIndex);
            string runtimeCache = BuildRuntimeCacheSummary(row, enabled, transformResolved, roleMatches);
            string bodyIdPreview = BuildBodyIdPreview(row, childIndex);
            string colliderIdPreview = BuildColliderIdPreview(row);

            return new SelectionValidationSummary(
                isRegistered: true,
                isAddableSelection: false,
                isBatchChild: batchChild != null,
                title: batchChild == null ? row.KindLabel : row.KindLabel + "子对象",
                status: BuildParticipationStatus(enabled, transformResolved, roleMatches, severity),
                role: row.KindLabel,
                definition: definition.DefinitionId > 0
                    ? CharacterTestLevelEditorChineseText.DefinitionLabel(definition) + " (" + definition.StableId + ")"
                    : "缺少对象定义",
                group: row.Group,
                transformPath: row.Detail,
                transformStatus: transformResolved ? "可解析" : "缺少 Transform",
                runtimeCache: runtimeCache,
                shape: definition.DefinitionId > 0
                    ? CharacterTestLevelEditorChineseText.Shape(definition.Shape.ShapeKind)
                        + " "
                        + definition.Shape.HalfExtentXMillimeters
                        + "/"
                        + definition.Shape.HalfExtentYMillimeters
                        + "/"
                        + definition.Shape.HalfExtentZMillimeters
                        + "mm"
                    : "无",
                material: definition.DefinitionId > 0
                    ? CharacterTestLevelEditorChineseText.Material(definition.Material.Kind)
                    : "无",
                potency: definition.DefinitionId > 0
                    ? "D/P/S="
                        + definition.Potency.BasePotency.Motion
                        + "/"
                        + definition.Potency.BasePotency.Cohesion
                        + "/"
                        + definition.Potency.BasePotency.Scatter
                    : "无",
                issueSeverity: severity,
                issueText: issueText,
                bodyIdPreview: bodyIdPreview,
                colliderIdPreview: colliderIdPreview,
                rowKey: row.Key);
        }

        private bool TryFindRowByKey(string rowKey, out Row row)
        {
            row = null;
            if (string.IsNullOrWhiteSpace(rowKey))
                return false;

            RebuildRows();
            for (int i = 0; i < _rows.Count; i++)
            {
                if (string.Equals(_rows[i].Key, rowKey, StringComparison.Ordinal))
                {
                    row = _rows[i];
                    return true;
                }
            }

            return false;
        }

        private bool TryGetRowDefinition(Row row, out CharacterTestWorldObjectDefinition definition)
        {
            definition = default;
            int definitionId = GetRowDefinitionId(row);
            return definitionId > 0
                && CharacterTestWorldObjectSceneSource.BuiltInCatalog.TryGetDefinition(
                    definitionId,
                    out definition);
        }

        private int GetRowDefinitionId(Row row)
        {
            if (row == null)
                return 0;

            switch (row.Kind)
            {
                case RowKind.Movable:
                    return row.Index >= 0 && row.Index < _document.MovableInstances.Count
                        ? _document.MovableInstances[row.Index].DefinitionId
                        : 0;
                case RowKind.SingleStatic:
                    return row.Index >= 0 && row.Index < _document.SingleStaticColliders.Count
                        ? _document.SingleStaticColliders[row.Index].DefinitionId
                        : 0;
                case RowKind.StaticBatch:
                    return row.Index >= 0 && row.Index < _document.StaticColliderBatches.Count
                        ? _document.StaticColliderBatches[row.Index].DefinitionId
                        : 0;
                case RowKind.Point:
                    return row.Index >= 0 && row.Index < _document.Points.Count
                        ? _document.Points[row.Index].DefinitionId
                        : 0;
                case RowKind.Region:
                    return row.Index >= 0 && row.Index < _document.Regions.Count
                        ? _document.Regions[row.Index].DefinitionId
                        : 0;
                default:
                    return 0;
            }
        }

        private bool IsRowEnabled(Row row)
        {
            if (row == null)
                return false;

            switch (row.Kind)
            {
                case RowKind.Movable:
                    return row.Index >= 0
                        && row.Index < _document.MovableInstances.Count
                        && _document.MovableInstances[row.Index].Enabled;
                case RowKind.SingleStatic:
                    return row.Index >= 0
                        && row.Index < _document.SingleStaticColliders.Count
                        && _document.SingleStaticColliders[row.Index].Enabled;
                case RowKind.StaticBatch:
                    return row.Index >= 0
                        && row.Index < _document.StaticColliderBatches.Count
                        && _document.StaticColliderBatches[row.Index].Enabled;
                case RowKind.Point:
                    return row.Index >= 0
                        && row.Index < _document.Points.Count
                        && _document.Points[row.Index].Enabled;
                case RowKind.Region:
                    return row.Index >= 0
                        && row.Index < _document.Regions.Count
                        && _document.Regions[row.Index].Enabled;
                default:
                    return false;
            }
        }

        private static bool DefinitionMatchesRow(Row row, CharacterTestWorldObjectDefinition definition)
        {
            if (row == null || definition.DefinitionId <= 0)
                return false;

            switch (row.Kind)
            {
                case RowKind.Movable:
                    return definition.CanSpawnMovableNaturalObject;
                case RowKind.SingleStatic:
                case RowKind.StaticBatch:
                    return definition.CanSpawnStaticCollider;
                case RowKind.Point:
                    return definition.CanSpawnPoint;
                case RowKind.Region:
                    return definition.CanSpawnRegion;
                default:
                    return false;
            }
        }

        private string BuildRuntimeCacheSummary(
            Row row,
            bool enabled,
            bool transformResolved,
            bool roleMatches)
        {
            if (!enabled)
                return "已禁用；应用时会跳过，不写入运行时缓存。";
            if (!transformResolved)
                return "已阻止；Transform 引用无法解析。";
            if (!roleMatches)
                return "已阻止；对象定义与当前类型不匹配。";

            switch (row.Kind)
            {
                case RowKind.Movable:
                    return "生成 1 个可移动物体出生配置和视图绑定。";
                case RowKind.SingleStatic:
                    return "生成 1 个静态碰撞体缓存条目。";
                case RowKind.StaticBatch:
                    if (!TryResolveRowTransform(row, out Transform container))
                        return "已阻止；静态批次容器无法解析。";
                    return "为 "
                        + CountActiveDirectChildren(container)
                        + " 个启用的直接子 Transform 生成静态碰撞体缓存条目。";
                case RowKind.Point:
                    return "生成 1 个关卡点或出生点缓存条目。";
                case RowKind.Region:
                    return "生成 1 个关卡区域缓存条目。";
                default:
                    return "不会生成运行时缓存。";
            }
        }

        private static int CountActiveDirectChildren(Transform container)
        {
            if (container == null)
                return 0;

            int count = 0;
            for (int i = 0; i < container.childCount; i++)
            {
                Transform child = container.GetChild(i);
                if (child != null && child.gameObject.activeSelf)
                    count++;
            }

            return count;
        }

        private string BuildBodyIdPreview(Row row, int batchChildIndex)
        {
            if (row == null)
                return string.Empty;

            switch (row.Kind)
            {
                case RowKind.SingleStatic:
                    return row.Index >= 0 && row.Index < _document.SingleStaticColliders.Count
                        ? _document.SingleStaticColliders[row.Index].BodyId.ToString()
                        : string.Empty;
                case RowKind.StaticBatch:
                    if (row.Index < 0 || row.Index >= _document.StaticColliderBatches.Count)
                        return string.Empty;
                    LevelWorldObjectStaticColliderBatchDefinition batch =
                        _document.StaticColliderBatches[row.Index];
                    return batchChildIndex >= 0
                        ? (batch.BodyIdStart + batchChildIndex).ToString()
                        : batch.BodyIdStart + "+子对象索引";
                default:
                    return string.Empty;
            }
        }

        private string BuildColliderIdPreview(Row row)
        {
            if (row == null)
                return string.Empty;

            switch (row.Kind)
            {
                case RowKind.SingleStatic:
                    return row.Index >= 0 && row.Index < _document.SingleStaticColliders.Count
                        ? (_document.SingleStaticColliders[row.Index].ColliderId > 0
                            ? _document.SingleStaticColliders[row.Index].ColliderId
                            : 1).ToString()
                        : string.Empty;
                case RowKind.StaticBatch:
                    return row.Index >= 0 && row.Index < _document.StaticColliderBatches.Count
                        ? (_document.StaticColliderBatches[row.Index].ColliderId > 0
                            ? _document.StaticColliderBatches[row.Index].ColliderId
                            : 1).ToString()
                        : string.Empty;
                default:
                    return string.Empty;
            }
        }

        private static string BuildParticipationStatus(
            bool enabled,
            bool transformResolved,
            bool roleMatches,
            string issueSeverity)
        {
            if (!enabled)
                return "已禁用";
            if (!transformResolved || !roleMatches || string.Equals(issueSeverity, "Error", StringComparison.Ordinal))
                return "已阻止";
            if (string.Equals(issueSeverity, "Warning", StringComparison.Ordinal))
                return "警告";
            return "运行时就绪";
        }

        private bool TryResolveBatchChild(
            Row row,
            Object selectedObject,
            out Transform batchChild,
            out int childIndex)
        {
            batchChild = null;
            childIndex = -1;
            if (row == null || row.Kind != RowKind.StaticBatch)
                return false;
            if (!TryResolveRowTransform(row, out Transform container))
                return false;

            Transform selectedTransform = ResolveSelectionTransform(selectedObject);
            if (selectedTransform == null || selectedTransform.parent != container)
                return false;

            batchChild = selectedTransform;
            childIndex = selectedTransform.GetSiblingIndex();
            return true;
        }

        private bool TryGetRowIssueSummary(
            Row row,
            out string severity,
            out string text)
        {
            severity = string.Empty;
            text = string.Empty;
            if (_report == null || row == null)
                return false;

            string rowPath = ResolveRowPath(row);
            if (string.IsNullOrWhiteSpace(rowPath))
                return false;

            LevelWorldObjectAuthoringIssue best = null;
            for (int i = 0; i < _report.Issues.Count; i++)
            {
                LevelWorldObjectAuthoringIssue issue = _report.Issues[i];
                if (issue == null
                    || !IssuePathBelongsToRow(issue.Path, rowPath))
                {
                    continue;
                }

                if (best == null || issue.Severity > best.Severity)
                    best = issue;
            }

            if (best == null)
                return false;

            severity = best.Severity.ToString();
            text = CharacterTestLevelEditorChineseText.DiagnosticText(best);
            return true;
        }

        private static bool IssuePathBelongsToRow(string issuePath, string rowPath)
        {
            if (string.IsNullOrWhiteSpace(issuePath) || string.IsNullOrWhiteSpace(rowPath))
                return false;

            return string.Equals(issuePath, rowPath, StringComparison.Ordinal)
                || issuePath.StartsWith(rowPath + ".", StringComparison.Ordinal);
        }

        private static bool IsRowErrorSeverity(string severity)
        {
            return string.Equals(severity, "Error", StringComparison.Ordinal);
        }

        private static bool IsRowWarningSeverity(string severity)
        {
            return string.Equals(severity, "Warning", StringComparison.Ordinal);
        }

        private string ResolveRowPath(Row row)
        {
            if (row == null || row.Index < 0)
                return string.Empty;

            switch (row.Kind)
            {
                case RowKind.Movable:
                    return "movableInstances[" + row.Index + "]";
                case RowKind.SingleStatic:
                    return "singleStaticColliders[" + row.Index + "]";
                case RowKind.StaticBatch:
                    return "staticColliderBatches[" + row.Index + "]";
                case RowKind.Point:
                    return "points[" + row.Index + "]";
                case RowKind.Region:
                    return "regions[" + row.Index + "]";
                default:
                    return string.Empty;
            }
        }

        public sealed class SelectionValidationSummary
        {
            public SelectionValidationSummary(
                bool isRegistered,
                bool isAddableSelection,
                bool isBatchChild,
                string title,
                string status,
                string role,
                string definition,
                string group,
                string transformPath,
                string transformStatus,
                string runtimeCache,
                string shape,
                string material,
                string potency,
                string issueSeverity,
                string issueText,
                string bodyIdPreview,
                string colliderIdPreview,
                string rowKey)
            {
                IsRegistered = isRegistered;
                IsAddableSelection = isAddableSelection;
                IsBatchChild = isBatchChild;
                Title = title ?? string.Empty;
                Status = status ?? string.Empty;
                Role = role ?? string.Empty;
                Definition = definition ?? string.Empty;
                Group = group ?? string.Empty;
                TransformPath = transformPath ?? string.Empty;
                TransformStatus = transformStatus ?? string.Empty;
                RuntimeCache = runtimeCache ?? string.Empty;
                Shape = shape ?? string.Empty;
                Material = material ?? string.Empty;
                Potency = potency ?? string.Empty;
                IssueSeverity = issueSeverity ?? string.Empty;
                IssueText = issueText ?? string.Empty;
                BodyIdPreview = bodyIdPreview ?? string.Empty;
                ColliderIdPreview = colliderIdPreview ?? string.Empty;
                RowKey = rowKey ?? string.Empty;
            }

            public bool IsRegistered { get; }

            public bool IsAddableSelection { get; }

            public bool IsBatchChild { get; }

            public string Title { get; }

            public string Status { get; }

            public string Role { get; }

            public string Definition { get; }

            public string Group { get; }

            public string TransformPath { get; }

            public string TransformStatus { get; }

            public string RuntimeCache { get; }

            public string Shape { get; }

            public string Material { get; }

            public string Potency { get; }

            public string IssueSeverity { get; }

            public string IssueText { get; }

            public string BodyIdPreview { get; }

            public string ColliderIdPreview { get; }

            public string RowKey { get; }

            public static SelectionValidationSummary Empty(string status)
            {
                return new SelectionValidationSummary(
                    false,
                    false,
                    false,
                    "选中对象",
                    status,
                    string.Empty,
                    string.Empty,
                    string.Empty,
                    string.Empty,
                    string.Empty,
                    "不会生成运行时缓存。",
                    string.Empty,
                    string.Empty,
                    string.Empty,
                    string.Empty,
                    string.Empty,
                    string.Empty,
                    string.Empty,
                    string.Empty);
            }
        }
    }
}
