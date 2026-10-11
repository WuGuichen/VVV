using System;
using System.Collections.Generic;
using MxFramework.Config.Levels;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace MxFramework.Game.CharacterTest.Editor
{
    internal sealed class CharacterTestGenesisValidationWorldScaffoldResult
    {
        public CharacterTestGenesisValidationRegionRef SceneBounds { get; set; }
        public CharacterTestGenesisValidationPointRef OriginPoint { get; set; }
        public List<CharacterTestGenesisValidationWorldStationScaffold> Stations { get; } =
            new List<CharacterTestGenesisValidationWorldStationScaffold>();
    }

    internal sealed class CharacterTestGenesisValidationWorldStationScaffold
    {
        public string TemplateId { get; set; }
        public string ZoneId { get; set; }
        public string ZoneDisplayName { get; set; }
        public string GroupId { get; set; }
        public int SortOrder { get; set; }
        public CharacterTestGenesisValidationZoneLayoutPattern LayoutPattern { get; set; }
        public CharacterTestGenesisValidationRegionRef FootprintRegion { get; set; }
        public CharacterTestGenesisValidationPointRef EntryPoint { get; set; }
        public CharacterTestGenesisValidationPointRef ApproachStart { get; set; }
        public CharacterTestGenesisValidationPointRef ResetPoint { get; set; }
        public CharacterTestGenesisValidationPointRef ObservationPoint { get; set; }
        public CharacterTestGenesisValidationPointRef ExitPoint { get; set; }
        public List<CharacterTestGenesisValidationStaticColliderRef> SampleStatics { get; } =
            new List<CharacterTestGenesisValidationStaticColliderRef>();
        public CharacterTestGenesisValidationStaticColliderRef TargetStatic { get; set; }
        public CharacterTestGenesisValidationMovableObjectRef TargetMovable { get; set; }
    }

    public sealed partial class CharacterTestWorldObjectLevelEditorModule
    {
        private const string GenesisLayoutRootName = "GenesisValidationLayout";

        internal LevelWorldObjectAuthoringDocument GetWorldObjectDocumentSnapshotForComposition()
        {
            EnsureLoaded();
            return _document.Normalize();
        }

        internal bool TryCreateGenesisValidationScaffold(
            Vector3 anchor,
            int undoGroup,
            out CharacterTestGenesisValidationWorldScaffoldResult result,
            out string failureMessage)
        {
            result = null;
            failureMessage = string.Empty;
            EnsureLoaded();
            EnsureUndoSession();
            SceneCacheTarget target = FindSceneCacheTarget();
            if (target == null || target.Root == null)
            {
                failureMessage = "当前场景中未找到 CharacterTest 世界对象根节点。";
                return false;
            }
            if (FindDirectChildByName(target.Root, GenesisLayoutRootName) != null)
            {
                failureMessage = "场景中已经存在 GenesisValidationLayout；为避免静默重复绑定，本次未创建。";
                return false;
            }
            if (!ValidateGenesisScaffoldDefinitions(out failureMessage))
                return false;

            using CharacterTestLevelEditorDocumentUndoSession.Transaction transaction =
                _undoSession.BeginJoinedTransaction("创建创世验证场景骨架", undoGroup);
            try
            {
                result = CreateGenesisScaffoldCore(target.Root, anchor);
                transaction.Commit();
            }
            catch (Exception exception)
            {
                failureMessage = "创建场景骨架失败：" + exception.Message;
                result = null;
                return false;
            }

            _sceneDirty = true;
            EditorSceneManager.MarkSceneDirty(target.Root.gameObject.scene);
            _status = "已创建并登记创世验证场景骨架；世界对象与场景尚未保存。";
            MarkDirty();
            return true;
        }

        private bool ValidateGenesisScaffoldDefinitions(out string failureMessage)
        {
            int[] ids =
            {
                CharacterTestWorldObjectDefinitionIds.PointValidationCheckpoint,
                CharacterTestWorldObjectDefinitionIds.PointValidationMarker,
                CharacterTestWorldObjectDefinitionIds.RegionTestCourse,
                CharacterTestWorldObjectDefinitionIds.StaticHardSlickSurface,
                CharacterTestWorldObjectDefinitionIds.StaticRoughSurface,
                CharacterTestWorldObjectDefinitionIds.StaticElasticSurface,
                CharacterTestWorldObjectDefinitionIds.StaticDefaultBoundary,
                CharacterTestWorldObjectDefinitionIds.MovableRoughHeavy
            };
            for (int i = 0; i < ids.Length; i++)
            {
                if (!ActiveDefinitionCatalog.TryGetDefinition(
                        ids[i],
                        out CharacterTestWorldObjectDefinition _)
                    || ActivePresentationCatalog == null
                    || !ActivePresentationCatalog.TryGet(
                        ids[i],
                        out CharacterTestWorldObjectPresentationBinding _))
                {
                    failureMessage = "场景骨架所需的 owner 定义尚未就绪：" + ids[i] + "。";
                    return false;
                }
            }
            failureMessage = string.Empty;
            return true;
        }

        private CharacterTestGenesisValidationWorldScaffoldResult CreateGenesisScaffoldCore(
            Transform worldRoot,
            Vector3 anchor)
        {
            Transform layoutRoot = CreateStructuralParent(worldRoot, GenesisLayoutRootName, anchor);
            var result = new CharacterTestGenesisValidationWorldScaffoldResult();
            result.SceneBounds = CreateRegion(
                layoutRoot,
                "SceneBounds",
                "genesis.scene.bounds",
                anchor + new Vector3(18f, 0.05f, 0f),
                new Vector3(54f, 0.1f, 14f));
            result.OriginPoint = CreatePoint(
                layoutRoot,
                "SceneOrigin",
                "genesis.scene.origin",
                CharacterTestWorldObjectDefinitionIds.PointValidationMarker,
                anchor + new Vector3(-8f, 0f, 0f));

            result.Stations.Add(CreateSurfaceComparisonScaffold(layoutRoot, anchor));
            result.Stations.Add(CreateBoundaryApproachScaffold(layoutRoot, anchor + new Vector3(18f, 0f, 0f)));
            result.Stations.Add(CreateMovableSandboxScaffold(layoutRoot, anchor + new Vector3(36f, 0f, 0f)));
            return result;
        }

        private CharacterTestGenesisValidationWorldStationScaffold CreateSurfaceComparisonScaffold(
            Transform layoutRoot,
            Vector3 center)
        {
            const string prefix = "genesis.surface";
            Transform zone = CreateStructuralParent(layoutRoot, "Zone_SurfaceComparisonLane", center);
            var result = CreateStationSpatialScaffold(
                zone,
                center,
                prefix,
                CharacterTestGenesisValidationStationTemplateIds.SurfaceComparisonLane,
                "zone.surface-comparison",
                "表面对比跑道",
                "surface-materials",
                10,
                CharacterTestGenesisValidationZoneLayoutPattern.ComparisonLanes);
            int[] definitionIds =
            {
                CharacterTestWorldObjectDefinitionIds.StaticHardSlickSurface,
                CharacterTestWorldObjectDefinitionIds.StaticRoughSurface,
                CharacterTestWorldObjectDefinitionIds.StaticElasticSurface
            };
            float[] laneZ = { -2.5f, 0f, 2.5f };
            for (int i = 0; i < definitionIds.Length; i++)
            {
                CharacterTestGenesisValidationStaticColliderRef sample = CreateStatic(
                    zone,
                    "Sample_" + (i + 1),
                    definitionIds[i],
                    center + new Vector3(2f, 0.25f, laneZ[i]),
                    new Vector3(4f, 0.5f, 2f));
                result.SampleStatics.Add(sample);
            }
            return result;
        }

        private CharacterTestGenesisValidationWorldStationScaffold CreateBoundaryApproachScaffold(
            Transform layoutRoot,
            Vector3 center)
        {
            const string prefix = "genesis.boundary";
            Transform zone = CreateStructuralParent(layoutRoot, "Zone_BoundaryApproachBay", center);
            CharacterTestGenesisValidationWorldStationScaffold result = CreateStationSpatialScaffold(
                zone,
                center,
                prefix,
                CharacterTestGenesisValidationStationTemplateIds.BoundaryApproachBay,
                "zone.boundary-approach",
                "边界接近舱",
                "boundary-forms",
                20,
                CharacterTestGenesisValidationZoneLayoutPattern.LinearApproach);
            result.TargetStatic = CreateStatic(
                zone,
                "Target_Boundary",
                CharacterTestWorldObjectDefinitionIds.StaticDefaultBoundary,
                center + new Vector3(2f, 1f, 0f),
                new Vector3(0.5f, 2f, 6f));
            return result;
        }

        private CharacterTestGenesisValidationWorldStationScaffold CreateMovableSandboxScaffold(
            Transform layoutRoot,
            Vector3 center)
        {
            const string prefix = "genesis.movable";
            Transform zone = CreateStructuralParent(layoutRoot, "Zone_MovableObjectSandbox", center);
            CharacterTestGenesisValidationWorldStationScaffold result = CreateStationSpatialScaffold(
                zone,
                center,
                prefix,
                CharacterTestGenesisValidationStationTemplateIds.MovableObjectSandbox,
                "zone.movable-sandbox",
                "可动物沙盒",
                "movable-natural-objects",
                30,
                CharacterTestGenesisValidationZoneLayoutPattern.Sandbox);
            result.TargetMovable = CreateMovable(
                zone,
                "Target_Movable",
                "genesis.movable.target",
                CharacterTestWorldObjectDefinitionIds.MovableRoughHeavy,
                center + new Vector3(2f, 0.5f, 0f),
                Vector3.one);
            return result;
        }

        private CharacterTestGenesisValidationWorldStationScaffold CreateStationSpatialScaffold(
            Transform parent,
            Vector3 center,
            string stablePrefix,
            string templateId,
            string zoneId,
            string zoneDisplayName,
            string groupId,
            int sortOrder,
            CharacterTestGenesisValidationZoneLayoutPattern layoutPattern)
        {
            return new CharacterTestGenesisValidationWorldStationScaffold
            {
                TemplateId = templateId,
                ZoneId = zoneId,
                ZoneDisplayName = zoneDisplayName,
                GroupId = groupId,
                SortOrder = sortOrder,
                LayoutPattern = layoutPattern,
                FootprintRegion = CreateRegion(
                    parent,
                    "Footprint",
                    stablePrefix + ".footprint",
                    center + new Vector3(0f, 0.05f, 0f),
                    new Vector3(14f, 0.1f, 10f)),
                EntryPoint = CreatePoint(
                    parent,
                    CharacterTestGenesisValidationSpatialRoleIds.ZoneEntryGate,
                    stablePrefix + ".entry",
                    CharacterTestWorldObjectDefinitionIds.PointValidationMarker,
                    center + new Vector3(-6f, 0f, 0f)),
                ApproachStart = CreatePoint(
                    parent,
                    CharacterTestGenesisValidationSpatialRoleIds.ApproachStart,
                    stablePrefix + ".approach-start",
                    CharacterTestWorldObjectDefinitionIds.PointValidationCheckpoint,
                    center + new Vector3(-5f, 0f, 0f)),
                ResetPoint = CreatePoint(
                    parent,
                    CharacterTestGenesisValidationSpatialRoleIds.ResetMarker,
                    stablePrefix + ".reset",
                    CharacterTestWorldObjectDefinitionIds.PointValidationCheckpoint,
                    center + new Vector3(-5f, 0f, -3.5f)),
                ObservationPoint = CreatePoint(
                    parent,
                    CharacterTestGenesisValidationSpatialRoleIds.ObservationMarker,
                    stablePrefix + ".observation",
                    CharacterTestWorldObjectDefinitionIds.PointValidationMarker,
                    center + new Vector3(0f, 0f, 4f)),
                ExitPoint = CreatePoint(
                    parent,
                    CharacterTestGenesisValidationSpatialRoleIds.ZoneExitGate,
                    stablePrefix + ".exit",
                    CharacterTestWorldObjectDefinitionIds.PointValidationMarker,
                    center + new Vector3(6f, 0f, 0f))
            };
        }

        private Transform CreateStructuralParent(Transform parent, string name, Vector3 position)
        {
            var gameObject = new GameObject(name);
            Undo.RegisterCreatedObjectUndo(gameObject, "创建创世验证场景骨架");
            gameObject.transform.SetParent(parent, worldPositionStays: true);
            gameObject.transform.position = position;
            return gameObject.transform;
        }

        private CharacterTestGenesisValidationPointRef CreatePoint(
            Transform parent,
            string name,
            string stableId,
            int definitionId,
            Vector3 position)
        {
            GameObject created = CreateOwnedSceneObject(
                parent,
                name,
                PlacementRole.Point,
                definitionId,
                position,
                Vector3.one);
            CharacterTestWorldObjectLevelEditorModel.AddRowsResult added =
                CharacterTestWorldObjectLevelEditorModel.AddPointsDetailed(
                    _document,
                    FindSceneCacheTarget().Root,
                    new[] { created.transform },
                    definitionId);
            if (added.Added != 1)
                throw new InvalidOperationException("Point owner registration failed: " + stableId);
            LevelWorldObjectPointDefinition entry = FindPointByTransform(created.transform);
            entry.StableId = EnsureUniquePointStableId(stableId);
            entry.Notes = "Genesis validation spatial role";
            return new CharacterTestGenesisValidationPointRef { StableId = entry.StableId };
        }

        private CharacterTestGenesisValidationRegionRef CreateRegion(
            Transform parent,
            string name,
            string stableId,
            Vector3 position,
            Vector3 fullExtents)
        {
            GameObject created = CreateOwnedSceneObject(
                parent,
                name,
                PlacementRole.Region,
                CharacterTestWorldObjectDefinitionIds.RegionTestCourse,
                position,
                fullExtents);
            CharacterTestWorldObjectLevelEditorModel.AddRowsResult added =
                CharacterTestWorldObjectLevelEditorModel.AddRegionsDetailed(
                    _document,
                    FindSceneCacheTarget().Root,
                    new[] { created.transform },
                    CharacterTestWorldObjectDefinitionIds.RegionTestCourse);
            if (added.Added != 1)
                throw new InvalidOperationException("Region owner registration failed: " + stableId);
            LevelWorldObjectRegionDefinition entry = FindRegionByTransform(created.transform);
            entry.StableId = EnsureUniqueRegionStableId(stableId);
            entry.Notes = "Genesis validation footprint";
            return new CharacterTestGenesisValidationRegionRef { StableId = entry.StableId };
        }

        private CharacterTestGenesisValidationStaticColliderRef CreateStatic(
            Transform parent,
            string name,
            int definitionId,
            Vector3 position,
            Vector3 fullExtents)
        {
            GameObject created = CreateOwnedSceneObject(
                parent,
                name,
                PlacementRole.Static,
                definitionId,
                position,
                fullExtents);
            CharacterTestWorldObjectLevelEditorModel.AddRowsResult added =
                CharacterTestWorldObjectLevelEditorModel.AddTransformsDetailed(
                    _document,
                    FindSceneCacheTarget().Root,
                    new[] { created.transform },
                    LevelWorldObjectRole.StaticCollider,
                    definitionId);
            if (added.Added != 1)
                throw new InvalidOperationException("Static owner registration failed: " + name);
            LevelWorldObjectInstanceDefinition entry = FindInstanceByTransform(
                _document.SingleStaticColliders,
                created.transform);
            entry.Notes = "Genesis validation fixture";
            return new CharacterTestGenesisValidationStaticColliderRef
            {
                SourceKind = CharacterTestGenesisValidationStaticSourceKind.SingleStatic,
                BodyId = entry.BodyId,
                ColliderId = entry.ColliderId > 0 ? entry.ColliderId : 1,
                ExpectedDefinitionId = entry.DefinitionId
            };
        }

        private CharacterTestGenesisValidationMovableObjectRef CreateMovable(
            Transform parent,
            string name,
            string stableId,
            int definitionId,
            Vector3 position,
            Vector3 fullExtents)
        {
            GameObject created = CreateOwnedSceneObject(
                parent,
                name,
                PlacementRole.Movable,
                definitionId,
                position,
                fullExtents);
            CharacterTestWorldObjectLevelEditorModel.AddRowsResult added =
                CharacterTestWorldObjectLevelEditorModel.AddTransformsDetailed(
                    _document,
                    FindSceneCacheTarget().Root,
                    new[] { created.transform },
                    LevelWorldObjectRole.Movable,
                    definitionId);
            if (added.Added != 1)
                throw new InvalidOperationException("Movable owner registration failed: " + name);
            LevelWorldObjectInstanceDefinition entry = FindInstanceByTransform(
                _document.MovableInstances,
                created.transform);
            entry.InstanceStableId = EnsureUniqueMovableStableId(stableId);
            entry.Notes = "Genesis validation fixture";
            return new CharacterTestGenesisValidationMovableObjectRef
            {
                InstanceStableId = entry.InstanceStableId
            };
        }

        private GameObject CreateOwnedSceneObject(
            Transform parent,
            string name,
            PlacementRole role,
            int definitionId,
            Vector3 position,
            Vector3 fullExtents)
        {
            if (!ActiveDefinitionCatalog.TryGetDefinition(
                    definitionId,
                    out CharacterTestWorldObjectDefinition definition))
            {
                throw new InvalidOperationException("Missing owner definition: " + definitionId + ".");
            }
            if (!TryResolvePlacementPresentation(
                    definitionId,
                    out CharacterTestWorldObjectPresentationBinding presentation,
                    out string reason))
                throw new InvalidOperationException(reason);
            GameObject created = CreateSceneObjectForDefinition(
                parent,
                definition,
                presentation,
                role,
                position);
            if (created == null)
                throw new InvalidOperationException("Owner object creation returned null.");
            created.name = name;
            created.transform.localScale = fullExtents;
            return created;
        }

        private LevelWorldObjectPointDefinition FindPointByTransform(Transform transform)
        {
            string path = CharacterTestWorldObjectAuthoringAdapter
                .CreateTransformRef(FindSceneCacheTarget().Root, transform).Path;
            for (int i = 0; i < _document.Points.Count; i++)
            {
                if (string.Equals(_document.Points[i].TransformRef?.Path, path, StringComparison.Ordinal))
                    return _document.Points[i];
            }
            throw new InvalidOperationException("Registered point entry cannot be resolved.");
        }

        private LevelWorldObjectRegionDefinition FindRegionByTransform(Transform transform)
        {
            string path = CharacterTestWorldObjectAuthoringAdapter
                .CreateTransformRef(FindSceneCacheTarget().Root, transform).Path;
            for (int i = 0; i < _document.Regions.Count; i++)
            {
                if (string.Equals(_document.Regions[i].TransformRef?.Path, path, StringComparison.Ordinal))
                    return _document.Regions[i];
            }
            throw new InvalidOperationException("Registered region entry cannot be resolved.");
        }

        private LevelWorldObjectInstanceDefinition FindInstanceByTransform(
            IReadOnlyList<LevelWorldObjectInstanceDefinition> instances,
            Transform transform)
        {
            string path = CharacterTestWorldObjectAuthoringAdapter
                .CreateTransformRef(FindSceneCacheTarget().Root, transform).Path;
            for (int i = 0; i < instances.Count; i++)
            {
                if (string.Equals(instances[i].TransformRef?.Path, path, StringComparison.Ordinal))
                    return instances[i];
            }
            throw new InvalidOperationException("Registered instance entry cannot be resolved.");
        }

        private string EnsureUniquePointStableId(string requested)
        {
            var used = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < _document.Points.Count; i++)
                used.Add(_document.Points[i].StableId ?? string.Empty);
            return EnsureUniqueStableId(requested, used);
        }

        private string EnsureUniqueRegionStableId(string requested)
        {
            var used = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < _document.Regions.Count; i++)
                used.Add(_document.Regions[i].StableId ?? string.Empty);
            return EnsureUniqueStableId(requested, used);
        }

        private string EnsureUniqueMovableStableId(string requested)
        {
            var used = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < _document.MovableInstances.Count; i++)
                used.Add(_document.MovableInstances[i].InstanceStableId ?? string.Empty);
            return EnsureUniqueStableId(requested, used);
        }

        private static string EnsureUniqueStableId(string requested, HashSet<string> used)
        {
            string seed = string.IsNullOrWhiteSpace(requested) ? "genesis.generated" : requested.Trim();
            if (!used.Contains(seed))
                return seed;
            int suffix = 2;
            string candidate;
            do
            {
                candidate = seed + "." + suffix++;
            }
            while (used.Contains(candidate));
            return candidate;
        }
    }
}
