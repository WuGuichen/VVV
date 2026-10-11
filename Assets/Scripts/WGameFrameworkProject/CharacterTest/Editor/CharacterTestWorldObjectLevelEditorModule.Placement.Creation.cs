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
        private PlacementCreateResult CreatePlacedObject(
            MxLevelEditorPlacementContext context,
            Vector3 position)
        {
            EnsureLoaded();
            if (!TryResolvePlacementDefinition(out CharacterTestWorldObjectDefinition definition, out string reason))
                return PlacementCreateResult.Failed(reason);
            if (!TryResolvePlacementPresentation(
                    definition.DefinitionId,
                    out CharacterTestWorldObjectPresentationBinding presentation,
                    out reason))
            {
                return PlacementCreateResult.Failed(reason);
            }

            SceneCacheTarget target = FindSceneCacheTarget();
            if (target == null || target.Root == null)
                return PlacementCreateResult.Failed("当前场景中未找到 CharacterTest GameManager 场景缓存目标。");

            Transform parent = ResolvePlacementParent(context, target.Root);
            if (parent == null)
                parent = target.Root;

            GameObject created = CreateSceneObjectForDefinition(
                parent,
                definition,
                presentation,
                _placementRole,
                position);
            if (created == null)
                return PlacementCreateResult.Failed(
                    "无法为"
                    + CharacterTestLevelEditorChineseText.DefinitionName(
                        definition.DefinitionId,
                        definition.DisplayName)
                    + "创建场景对象。");

            CharacterTestWorldObjectLevelEditorModel.AddRowsResult addResult =
                AddPlacedTransformToDocument(created.transform, target.Root, definition.DefinitionId);
            if (addResult.Added <= 0)
            {
                Undo.DestroyObjectImmediate(created);
                string skipped = addResult.SkippedTotal > 0
                    ? "，已跳过=" + addResult.SkippedTotal
                    : string.Empty;
                return PlacementCreateResult.Failed("放置操作未创建制作条目。" + skipped);
            }

            string status = BuildPlacementSuccessStatus(definition, addResult);
            _status = status;
            MarkDirty();

            Selection.activeGameObject = created;
            EditorGUIUtility.PingObject(created);
            string treeNodeId = GetPlacementTreeNodeId(_placementRole);
            string rowKey = string.Empty;
            if (TryResolveSelection(context?.EditorContext, created, out MxLevelEditorSelectionTarget selectionTarget)
                && selectionTarget != null)
            {
                treeNodeId = selectionTarget.TreeNodeId;
                rowKey = selectionTarget.RowKey;
                context?.EditorContext?.RevealRow(selectionTarget.TreeNodeId, selectionTarget.RowKey);
            }

            return new PlacementCreateResult(
                true,
                status,
                created,
                treeNodeId,
                rowKey,
                addResult.Added,
                addResult.FirstAssignedId,
                addResult.LastAssignedId);
        }

        private CharacterTestWorldObjectLevelEditorModel.AddRowsResult AddPlacedTransformToDocument(
            Transform transform,
            Transform root,
            int definitionId)
        {
            switch (_placementRole)
            {
                case PlacementRole.Movable:
                    return CharacterTestWorldObjectLevelEditorModel.AddTransformsDetailed(
                        _document,
                        root,
                        new[] { transform },
                        LevelWorldObjectRole.Movable,
                        definitionId);
                case PlacementRole.Static:
                    return CharacterTestWorldObjectLevelEditorModel.AddTransformsDetailed(
                        _document,
                        root,
                        new[] { transform },
                        LevelWorldObjectRole.StaticCollider,
                        definitionId);
                case PlacementRole.Point:
                    return CharacterTestWorldObjectLevelEditorModel.AddPointsDetailed(
                        _document,
                        root,
                        new[] { transform },
                        definitionId);
                case PlacementRole.Region:
                    return CharacterTestWorldObjectLevelEditorModel.AddRegionsDetailed(
                        _document,
                        root,
                        new[] { transform },
                        definitionId);
                default:
                    return CharacterTestWorldObjectLevelEditorModel.AddRowsResult.Empty;
            }
        }

        private GameObject CreateSceneObjectForDefinition(
            Transform parent,
            CharacterTestWorldObjectDefinition definition,
            CharacterTestWorldObjectPresentationBinding presentation,
            PlacementRole role,
            Vector3 position)
        {
            GameObject created;
            if (presentation.Kind == CharacterTestWorldObjectPresentationKind.PrefabResource)
            {
                if (!ContentProjection.TryResolvePrefab(
                        presentation.PrefabResource,
                        out GameObject prefab,
                        out _))
                {
                    return null;
                }

                created = PrefabUtility.InstantiatePrefab(prefab, parent) as GameObject;
                if (created == null)
                    return null;
            }
            else if (presentation.Kind == CharacterTestWorldObjectPresentationKind.PrimitiveAuthoring
                     && (role == PlacementRole.Point || role == PlacementRole.Region))
            {
                created = new GameObject();
            }
            else if (presentation.Kind == CharacterTestWorldObjectPresentationKind.PrimitiveAuthoring)
            {
                created = GameObject.CreatePrimitive(ToPrimitiveType(presentation.PrimitiveKind));
                RemoveAuthoringPrimitiveColliders(created);
            }
            else
            {
                return null;
            }

            Undo.RegisterCreatedObjectUndo(created, "放置 CharacterTest 世界对象");
            created.name = CreateUniqueChildName(parent, role, definition);
            if (created.transform.parent != parent)
                created.transform.SetParent(parent, worldPositionStays: true);
            created.transform.position = position;
            if (presentation.Kind == CharacterTestWorldObjectPresentationKind.PrimitiveAuthoring)
            {
                created.transform.rotation = Quaternion.identity;
                created.transform.localScale = role == PlacementRole.Point
                    ? Vector3.one
                    : ResolveDefinitionFullExtents(definition);
            }
            return created;
        }

        private static void RemoveAuthoringPrimitiveColliders(GameObject gameObject)
        {
            if (gameObject == null)
                return;

            Collider[] colliders = gameObject.GetComponents<Collider>();
            for (int i = 0; i < colliders.Length; i++)
            {
                if (colliders[i] != null)
                    Object.DestroyImmediate(colliders[i]);
            }
        }

        private Transform ResolvePlacementParent(
            MxLevelEditorPlacementContext context,
            Transform root)
        {
            if (root == null)
                return null;

            if (context?.SelectedRow?.Payload is Row selectedRow
                && TryResolveRowTransform(selectedRow, out Transform selectedTransform)
                && selectedTransform != null)
            {
                return selectedRow.Kind == RowKind.StaticBatch
                    ? selectedTransform
                    : selectedTransform.parent ?? root;
            }

            string treeNodeId = context?.SelectedTreeNodeId ?? _activeTreeNodeId;
            if (!string.IsNullOrWhiteSpace(treeNodeId)
                && treeNodeId.StartsWith("group:", StringComparison.Ordinal))
            {
                string group = treeNodeId.Substring("group:".Length);
                Transform groupRoot = FindDirectChildByName(root, group);
                if (groupRoot != null)
                    return groupRoot;
            }

            return root;
        }

        private static Transform FindDirectChildByName(Transform root, string childName)
        {
            if (root == null || string.IsNullOrWhiteSpace(childName))
                return null;

            string normalizedName = childName.Trim();
            for (int i = 0; i < root.childCount; i++)
            {
                Transform child = root.GetChild(i);
                if (child != null && string.Equals(child.name, normalizedName, StringComparison.Ordinal))
                    return child;
            }

            return null;
        }

        private bool TryResolvePlacementDefinition(
            out CharacterTestWorldObjectDefinition definition,
            out string reason)
        {
            int definitionId = GetPlacementDefinitionId(_placementRole);
            if (!ActiveDefinitionCatalog.TryGetDefinition(definitionId, out definition))
            {
                reason = "未找到放置定义：" + definitionId + "。";
                return false;
            }

            if (!DefinitionMatchesPlacementRole(definition))
            {
                reason = "对象定义 "
                    + definition.DefinitionId
                    + " 不支持"
                    + PlacementRoleLabel(_placementRole)
                    + "放置。";
                return false;
            }

            reason = string.Empty;
            return true;
        }

        private bool TryResolvePlacementPresentation(
            int definitionId,
            out CharacterTestWorldObjectPresentationBinding presentation,
            out string reason)
        {
            presentation = default;
            if (ActivePresentationCatalog == null
                || !ActivePresentationCatalog.TryGet(definitionId, out presentation))
            {
                reason = "未找到对象定义的场景表现配置：" + definitionId;
                return false;
            }

            if (presentation.Kind == CharacterTestWorldObjectPresentationKind.PrefabResource)
            {
                if (!ContentProjection.TryResolvePrefab(
                        presentation.PrefabResource,
                        out _,
                        out reason))
                {
                    return false;
                }
            }
            else if (presentation.Kind != CharacterTestWorldObjectPresentationKind.PrimitiveAuthoring)
            {
                reason = "对象定义未显式选择 Prefab 或基础几何体：" + definitionId;
                return false;
            }

            reason = string.Empty;
            return true;
        }

        private bool DefinitionMatchesPlacementRole(CharacterTestWorldObjectDefinition definition)
        {
            switch (_placementRole)
            {
                case PlacementRole.Movable:
                    return definition.CanSpawnMovableNaturalObject;
                case PlacementRole.Static:
                    return definition.CanSpawnStaticCollider;
                case PlacementRole.Point:
                    return definition.CanSpawnPoint;
                case PlacementRole.Region:
                    return definition.CanSpawnRegion;
                default:
                    return false;
            }
        }

        private int GetPlacementDefinitionId(PlacementRole role)
        {
            switch (role)
            {
                case PlacementRole.Static:
                    return _selectedStaticDefinitionId;
                case PlacementRole.Point:
                    return _selectedPointDefinitionId;
                case PlacementRole.Region:
                    return _selectedRegionDefinitionId;
                default:
                    return _selectedMovableDefinitionId;
            }
        }

        private void SetPlacementDefinitionId(PlacementRole role, int definitionId)
        {
            switch (role)
            {
                case PlacementRole.Static:
                    _selectedStaticDefinitionId = definitionId;
                    break;
                case PlacementRole.Point:
                    _selectedPointDefinitionId = definitionId;
                    break;
                case PlacementRole.Region:
                    _selectedRegionDefinitionId = definitionId;
                    break;
                default:
                    _selectedMovableDefinitionId = definitionId;
                    break;
            }
        }

        private static string GetPlacementTreeNodeId(PlacementRole role)
        {
            switch (role)
            {
                case PlacementRole.Static:
                    return "static.single";
                case PlacementRole.Point:
                    return "points";
                case PlacementRole.Region:
                    return "regions";
                default:
                    return "movables";
            }
        }

        private static PrimitiveType ToPrimitiveType(CharacterTestWorldObjectPrimitiveKind primitiveKind)
        {
            switch (primitiveKind)
            {
                case CharacterTestWorldObjectPrimitiveKind.Sphere:
                    return PrimitiveType.Sphere;
                case CharacterTestWorldObjectPrimitiveKind.Capsule:
                    return PrimitiveType.Capsule;
                default:
                    return PrimitiveType.Cube;
            }
        }

        private static Vector3 ResolveDefinitionHalfExtents(CharacterTestWorldObjectDefinition definition)
        {
            return new Vector3(
                CharacterTestDeterministicUnits.MillimetersToMeters(definition.Shape.HalfExtentXMillimeters),
                CharacterTestDeterministicUnits.MillimetersToMeters(definition.Shape.HalfExtentYMillimeters),
                CharacterTestDeterministicUnits.MillimetersToMeters(definition.Shape.HalfExtentZMillimeters));
        }

        private static Vector3 ResolveDefinitionFullExtents(CharacterTestWorldObjectDefinition definition)
        {
            return ResolveDefinitionHalfExtents(definition) * 2f;
        }

        private static string CreateUniqueChildName(
            Transform parent,
            PlacementRole role,
            CharacterTestWorldObjectDefinition definition)
        {
            string prefix = "LevelObject_" + role + "_" + SanitizeObjectName(definition.DisplayName);
            if (parent == null)
                return prefix;

            var existing = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < parent.childCount; i++)
            {
                Transform child = parent.GetChild(i);
                if (child != null)
                    existing.Add(child.name);
            }

            string candidate = prefix;
            int suffix = 2;
            while (existing.Contains(candidate))
                candidate = prefix + "_" + suffix++;
            return candidate;
        }

        private static string SanitizeObjectName(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return "Object";

            char[] chars = value.Trim().ToCharArray();
            for (int i = 0; i < chars.Length; i++)
            {
                char c = chars[i];
                bool keep = (c >= 'a' && c <= 'z')
                    || (c >= 'A' && c <= 'Z')
                    || (c >= '0' && c <= '9');
                chars[i] = keep ? c : '_';
            }

            string sanitized = new string(chars).Trim('_');
            return string.IsNullOrWhiteSpace(sanitized) ? "Object" : sanitized;
        }

        private static bool TryResolvePlacementPosition(Vector2 guiPosition, out Vector3 position)
        {
            return CharacterTestLevelEditorPlacementUtility.TryResolveSurfacePosition(
                guiPosition,
                out position,
                out _);
        }

    }
}
