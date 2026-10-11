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

    public static class CharacterTestWorldObjectLevelEditorModel
    {
        private const int DefaultStaticBodyIdStart = 10000;
        private const int DefaultStaticColliderId = 1;

        public static int AddTransforms(
            LevelWorldObjectAuthoringDocument document,
            CharacterTestWorldObjectSceneSource source,
            IEnumerable<Transform> transforms,
            LevelWorldObjectRole role,
            int definitionId)
        {
            return AddTransformsDetailed(
                document,
                source == null ? null : source.transform,
                transforms,
                role,
                definitionId).Added;
        }

        public static AddRowsResult AddTransformsDetailed(
            LevelWorldObjectAuthoringDocument document,
            CharacterTestWorldObjectSceneSource source,
            IEnumerable<Transform> transforms,
            LevelWorldObjectRole role,
            int definitionId)
        {
            return AddTransformsDetailed(
                document,
                source == null ? null : source.transform,
                transforms,
                role,
                definitionId);
        }

        public static AddRowsResult AddTransformsDetailed(
            LevelWorldObjectAuthoringDocument document,
            Transform root,
            IEnumerable<Transform> transforms,
            LevelWorldObjectRole role,
            int definitionId)
        {
            if (document == null)
                throw new ArgumentNullException(nameof(document));
            if (root == null || transforms == null)
                return AddRowsResult.Empty;

            document = EnsureCollections(document);

            List<LevelWorldObjectInstanceDefinition> target = role == LevelWorldObjectRole.Movable
                ? document.MovableInstances
                : document.SingleStaticColliders;
            var existingPaths = CollectInstancePaths(document);

            int added = 0;
            int skippedInvalid = 0;
            int skippedDuplicate = 0;
            int firstAssignedId = 0;
            int lastAssignedId = 0;
            int nextMovableId = ResolveNextMovableId(document.MovableInstances);
            foreach (Transform transform in transforms)
            {
                if (transform == null || !IsChildOrSelf(root, transform))
                {
                    skippedInvalid++;
                    continue;
                }

                LevelTransformReference transformRef =
                    CharacterTestWorldObjectAuthoringAdapter.CreateTransformRef(root, transform);
                if (!existingPaths.Add(transformRef.Path))
                {
                    skippedDuplicate++;
                    continue;
                }

                var instance = new LevelWorldObjectInstanceDefinition
                {
                    Enabled = true,
                    Role = role,
                    DefinitionId = Math.Max(0, definitionId),
                    TransformRef = transformRef,
                    TransformPath = transformRef.Path,
                    ScaleSource = LevelWorldObjectScaleSource.SceneTransform,
                    Group = CharacterTestWorldObjectAuthoringAdapter.ResolveGroup(transformRef.Path)
                };
                if (role == LevelWorldObjectRole.Movable)
                    instance.NumericInstanceId = nextMovableId++;
                else
                {
                    instance.BodyId = ResolveNextStaticBodyId(document, root);
                    instance.ColliderId = DefaultStaticColliderId;
                }

                AppendAssignedId(
                    role == LevelWorldObjectRole.Movable
                        ? instance.NumericInstanceId
                        : instance.BodyId,
                    ref firstAssignedId,
                    ref lastAssignedId);
                target.Add(instance);
                added++;
            }

            SortDocument(document);
            return new AddRowsResult(
                added,
                skippedInvalid,
                skippedDuplicate,
                firstAssignedId,
                lastAssignedId,
                role == LevelWorldObjectRole.Movable ? "numeric ids" : "body ids");
        }

        public static int AddStaticBatches(
            LevelWorldObjectAuthoringDocument document,
            CharacterTestWorldObjectSceneSource source,
            IEnumerable<Transform> containers,
            int definitionId,
            int colliderId)
        {
            return AddStaticBatchesDetailed(
                document,
                source == null ? null : source.transform,
                containers,
                definitionId,
                colliderId).Added;
        }

        public static AddRowsResult AddStaticBatchesDetailed(
            LevelWorldObjectAuthoringDocument document,
            CharacterTestWorldObjectSceneSource source,
            IEnumerable<Transform> containers,
            int definitionId,
            int colliderId)
        {
            return AddStaticBatchesDetailed(
                document,
                source == null ? null : source.transform,
                containers,
                definitionId,
                colliderId);
        }

        public static AddRowsResult AddStaticBatchesDetailed(
            LevelWorldObjectAuthoringDocument document,
            Transform root,
            IEnumerable<Transform> containers,
            int definitionId,
            int colliderId)
        {
            if (document == null)
                throw new ArgumentNullException(nameof(document));
            if (root == null || containers == null)
                return AddRowsResult.Empty;

            document = EnsureCollections(document);
            var existingPaths = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < document.StaticColliderBatches.Count; i++)
            {
                LevelWorldObjectStaticColliderBatchDefinition batch = document.StaticColliderBatches[i];
                if (batch != null && batch.ContainerTransformRef != null)
                    existingPaths.Add(batch.ContainerTransformRef.Path);
            }

            int added = 0;
            int skippedInvalid = 0;
            int skippedDuplicate = 0;
            int firstAssignedId = 0;
            int lastAssignedId = 0;
            int nextBodyId = ResolveNextStaticBodyId(document, root);
            foreach (Transform transform in containers)
            {
                if (transform == null || !IsChildOrSelf(root, transform))
                {
                    skippedInvalid++;
                    continue;
                }

                LevelTransformReference transformRef =
                    CharacterTestWorldObjectAuthoringAdapter.CreateTransformRef(root, transform);
                if (!existingPaths.Add(transformRef.Path))
                {
                    skippedDuplicate++;
                    continue;
                }

                int bodyIdStart = nextBodyId;
                int span = Math.Max(1, transform.childCount);
                document.StaticColliderBatches.Add(new LevelWorldObjectStaticColliderBatchDefinition
                {
                    Enabled = true,
                    DefinitionId = Math.Max(0, definitionId),
                    ContainerTransformRef = transformRef,
                    ContainerTransformPath = transformRef.Path,
                    ScaleSource = LevelWorldObjectScaleSource.SceneTransform,
                    BodyIdStart = bodyIdStart,
                    ColliderId = colliderId > 0 ? colliderId : DefaultStaticColliderId,
                    Group = CharacterTestWorldObjectAuthoringAdapter.ResolveGroup(transformRef.Path)
                });
                AppendAssignedRange(bodyIdStart, bodyIdStart + span - 1, ref firstAssignedId, ref lastAssignedId);
                nextBodyId += span;
                added++;
            }

            SortDocument(document);
            return new AddRowsResult(
                added,
                skippedInvalid,
                skippedDuplicate,
                firstAssignedId,
                lastAssignedId,
                "body ids");
        }

        public static AddRowsResult AddPointsDetailed(
            LevelWorldObjectAuthoringDocument document,
            CharacterTestWorldObjectSceneSource source,
            IEnumerable<Transform> transforms,
            int definitionId)
        {
            return AddPointsDetailed(
                document,
                source == null ? null : source.transform,
                transforms,
                definitionId);
        }

        public static AddRowsResult AddPointsDetailed(
            LevelWorldObjectAuthoringDocument document,
            Transform root,
            IEnumerable<Transform> transforms,
            int definitionId)
        {
            if (document == null)
                throw new ArgumentNullException(nameof(document));
            if (root == null || transforms == null)
                return AddRowsResult.Empty;

            document = EnsureCollections(document);
            var existingPaths = CollectPointPaths(document.Points);
            var existingStableIds = CollectPointStableIds(document.Points);
            int added = 0;
            int skippedInvalid = 0;
            int skippedDuplicate = 0;
            foreach (Transform transform in transforms)
            {
                if (transform == null || !IsChildOrSelf(root, transform))
                {
                    skippedInvalid++;
                    continue;
                }

                LevelTransformReference transformRef =
                    CharacterTestWorldObjectAuthoringAdapter.CreateTransformRef(root, transform);
                if (!existingPaths.Add(transformRef.Path))
                {
                    skippedDuplicate++;
                    continue;
                }

                document.Points.Add(new LevelWorldObjectPointDefinition
                {
                    Enabled = true,
                    DefinitionId = Math.Max(0, definitionId),
                    StableId = ResolveUniqueStableId("point", definitionId, transformRef.Path, existingStableIds),
                    PointKind = ResolvePointKind(definitionId),
                    TransformRef = transformRef,
                    TransformPath = transformRef.Path,
                    Group = CharacterTestWorldObjectAuthoringAdapter.ResolveGroup(transformRef.Path)
                });
                added++;
            }

            SortDocument(document);
            return new AddRowsResult(added, skippedInvalid, skippedDuplicate, 0, 0, "stable ids");
        }

        public static AddRowsResult AddRegionsDetailed(
            LevelWorldObjectAuthoringDocument document,
            CharacterTestWorldObjectSceneSource source,
            IEnumerable<Transform> transforms,
            int definitionId)
        {
            return AddRegionsDetailed(
                document,
                source == null ? null : source.transform,
                transforms,
                definitionId);
        }

        public static AddRowsResult AddRegionsDetailed(
            LevelWorldObjectAuthoringDocument document,
            Transform root,
            IEnumerable<Transform> transforms,
            int definitionId)
        {
            if (document == null)
                throw new ArgumentNullException(nameof(document));
            if (root == null || transforms == null)
                return AddRowsResult.Empty;

            document = EnsureCollections(document);
            var existingPaths = CollectRegionPaths(document.Regions);
            var existingStableIds = CollectRegionStableIds(document.Regions);
            int added = 0;
            int skippedInvalid = 0;
            int skippedDuplicate = 0;
            foreach (Transform transform in transforms)
            {
                if (transform == null || !IsChildOrSelf(root, transform))
                {
                    skippedInvalid++;
                    continue;
                }

                LevelTransformReference transformRef =
                    CharacterTestWorldObjectAuthoringAdapter.CreateTransformRef(root, transform);
                if (!existingPaths.Add(transformRef.Path))
                {
                    skippedDuplicate++;
                    continue;
                }

                document.Regions.Add(new LevelWorldObjectRegionDefinition
                {
                    Enabled = true,
                    DefinitionId = Math.Max(0, definitionId),
                    StableId = ResolveUniqueStableId("region", definitionId, transformRef.Path, existingStableIds),
                    RegionKind = ResolveRegionKind(definitionId),
                    TransformRef = transformRef,
                    TransformPath = transformRef.Path,
                    ScaleSource = LevelWorldObjectScaleSource.SceneTransform,
                    Group = CharacterTestWorldObjectAuthoringAdapter.ResolveGroup(transformRef.Path)
                });
                added++;
            }

            SortDocument(document);
            return new AddRowsResult(added, skippedInvalid, skippedDuplicate, 0, 0, "stable ids");
        }

        public static int RepairTransformRefs(
            LevelWorldObjectAuthoringDocument document,
            CharacterTestWorldObjectSceneSource source)
        {
            return RepairTransformRefs(document, source == null ? null : source.transform);
        }

        public static int RepairTransformRefs(
            LevelWorldObjectAuthoringDocument document,
            Transform root)
        {
            if (document == null || root == null)
                return 0;

            LevelWorldObjectAuthoringDocument normalized = document.Normalize();
            int repaired = 0;
            repaired += RepairInstances(normalized.MovableInstances, root);
            repaired += RepairInstances(normalized.SingleStaticColliders, root);
            for (int i = 0; i < normalized.StaticColliderBatches.Count; i++)
            {
                LevelWorldObjectStaticColliderBatchDefinition batch = normalized.StaticColliderBatches[i];
                if (batch == null)
                    continue;
                if (CharacterTestWorldObjectAuthoringAdapter.TryResolveTransform(
                        root,
                        batch.ContainerTransformRef,
                        out Transform transform))
                {
                    batch.ContainerTransformRef =
                        CharacterTestWorldObjectAuthoringAdapter.CreateTransformRef(root, transform);
                    batch.ContainerTransformPath = batch.ContainerTransformRef.Path;
                    repaired++;
                }
            }
            repaired += RepairPoints(normalized.Points, root);
            repaired += RepairRegions(normalized.Regions, root);

            CopyDocument(normalized, document);
            return repaired;
        }

        public static void SortDocument(LevelWorldObjectAuthoringDocument document)
        {
            if (document == null)
                return;

            CopyDocument(document.Normalize(), document);
        }

        private static int RepairInstances(
            IReadOnlyList<LevelWorldObjectInstanceDefinition> instances,
            Transform root)
        {
            int repaired = 0;
            if (instances == null)
                return repaired;

            for (int i = 0; i < instances.Count; i++)
            {
                LevelWorldObjectInstanceDefinition instance = instances[i];
                if (instance == null)
                    continue;
                if (!CharacterTestWorldObjectAuthoringAdapter.TryResolveTransform(
                        root,
                        instance.TransformRef,
                        out Transform transform))
                    continue;

                instance.TransformRef =
                    CharacterTestWorldObjectAuthoringAdapter.CreateTransformRef(root, transform);
                instance.TransformPath = instance.TransformRef.Path;
                repaired++;
            }

            return repaired;
        }

        private static int RepairPoints(
            IReadOnlyList<LevelWorldObjectPointDefinition> points,
            Transform root)
        {
            int repaired = 0;
            if (points == null)
                return repaired;

            for (int i = 0; i < points.Count; i++)
            {
                LevelWorldObjectPointDefinition point = points[i];
                if (point == null)
                    continue;
                if (!CharacterTestWorldObjectAuthoringAdapter.TryResolveTransform(
                        root,
                        point.TransformRef,
                        out Transform transform))
                    continue;

                point.TransformRef =
                    CharacterTestWorldObjectAuthoringAdapter.CreateTransformRef(root, transform);
                point.TransformPath = point.TransformRef.Path;
                repaired++;
            }

            return repaired;
        }

        private static int RepairRegions(
            IReadOnlyList<LevelWorldObjectRegionDefinition> regions,
            Transform root)
        {
            int repaired = 0;
            if (regions == null)
                return repaired;

            for (int i = 0; i < regions.Count; i++)
            {
                LevelWorldObjectRegionDefinition region = regions[i];
                if (region == null)
                    continue;
                if (!CharacterTestWorldObjectAuthoringAdapter.TryResolveTransform(
                        root,
                        region.TransformRef,
                        out Transform transform))
                    continue;

                region.TransformRef =
                    CharacterTestWorldObjectAuthoringAdapter.CreateTransformRef(root, transform);
                region.TransformPath = region.TransformRef.Path;
                repaired++;
            }

            return repaired;
        }

        private static HashSet<string> CollectInstancePaths(LevelWorldObjectAuthoringDocument document)
        {
            var paths = new HashSet<string>(StringComparer.Ordinal);
            AddInstancePaths(document.MovableInstances, paths);
            AddInstancePaths(document.SingleStaticColliders, paths);
            return paths;
        }

        private static void AddInstancePaths(
            IReadOnlyList<LevelWorldObjectInstanceDefinition> instances,
            HashSet<string> paths)
        {
            if (instances == null)
                return;

            for (int i = 0; i < instances.Count; i++)
            {
                LevelWorldObjectInstanceDefinition instance = instances[i];
                string path = instance?.TransformRef?.Path ?? instance?.TransformPath;
                if (!string.IsNullOrWhiteSpace(path))
                    paths.Add(path.Trim().Replace('\\', '/'));
            }
        }

        private static HashSet<string> CollectPointPaths(IReadOnlyList<LevelWorldObjectPointDefinition> points)
        {
            var paths = new HashSet<string>(StringComparer.Ordinal);
            if (points == null)
                return paths;

            for (int i = 0; i < points.Count; i++)
            {
                string path = points[i]?.TransformRef?.Path ?? points[i]?.TransformPath;
                if (!string.IsNullOrWhiteSpace(path))
                    paths.Add(path.Trim().Replace('\\', '/'));
            }

            return paths;
        }

        private static HashSet<string> CollectRegionPaths(IReadOnlyList<LevelWorldObjectRegionDefinition> regions)
        {
            var paths = new HashSet<string>(StringComparer.Ordinal);
            if (regions == null)
                return paths;

            for (int i = 0; i < regions.Count; i++)
            {
                string path = regions[i]?.TransformRef?.Path ?? regions[i]?.TransformPath;
                if (!string.IsNullOrWhiteSpace(path))
                    paths.Add(path.Trim().Replace('\\', '/'));
            }

            return paths;
        }

        private static HashSet<string> CollectPointStableIds(IReadOnlyList<LevelWorldObjectPointDefinition> points)
        {
            var stableIds = new HashSet<string>(StringComparer.Ordinal);
            if (points == null)
                return stableIds;

            for (int i = 0; i < points.Count; i++)
            {
                string stableId = points[i]?.StableId;
                if (!string.IsNullOrWhiteSpace(stableId))
                    stableIds.Add(stableId.Trim());
            }

            return stableIds;
        }

        private static HashSet<string> CollectRegionStableIds(IReadOnlyList<LevelWorldObjectRegionDefinition> regions)
        {
            var stableIds = new HashSet<string>(StringComparer.Ordinal);
            if (regions == null)
                return stableIds;

            for (int i = 0; i < regions.Count; i++)
            {
                string stableId = regions[i]?.StableId;
                if (!string.IsNullOrWhiteSpace(stableId))
                    stableIds.Add(stableId.Trim());
            }

            return stableIds;
        }

        private static string ResolveUniqueStableId(
            string fallbackPrefix,
            int definitionId,
            string transformPath,
            HashSet<string> existingStableIds)
        {
            string baseId = string.Empty;
            if (CharacterTestWorldObjectSceneSource.BuiltInCatalog.TryGetDefinition(
                    definitionId,
                    out CharacterTestWorldObjectDefinition definition))
            {
                baseId = definition.StableId;
            }

            if (string.IsNullOrWhiteSpace(baseId))
                baseId = fallbackPrefix + "." + SanitizeStableSegment(transformPath);

            string candidate = baseId.Trim();
            int suffix = 2;
            while (existingStableIds != null && !existingStableIds.Add(candidate))
                candidate = baseId + "." + suffix++;
            return candidate;
        }

        private static LevelWorldObjectPointKind ResolvePointKind(int definitionId)
        {
            if (CharacterTestWorldObjectSceneSource.BuiltInCatalog.TryGetDefinition(
                    definitionId,
                    out CharacterTestWorldObjectDefinition definition)
                && definition.CanSpawnPoint)
            {
                return definition.Point.PointKind;
            }

            return LevelWorldObjectPointKind.Unknown;
        }

        private static LevelWorldObjectRegionKind ResolveRegionKind(int definitionId)
        {
            if (CharacterTestWorldObjectSceneSource.BuiltInCatalog.TryGetDefinition(
                    definitionId,
                    out CharacterTestWorldObjectDefinition definition)
                && definition.CanSpawnRegion)
            {
                return definition.Region.RegionKind;
            }

            return LevelWorldObjectRegionKind.Unknown;
        }

        private static string SanitizeStableSegment(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return "item";

            char[] chars = value.Trim().ToLowerInvariant().ToCharArray();
            for (int i = 0; i < chars.Length; i++)
            {
                char c = chars[i];
                bool keep = (c >= 'a' && c <= 'z') || (c >= '0' && c <= '9');
                chars[i] = keep ? c : '_';
            }

            return new string(chars).Trim('_');
        }

        private static int ResolveNextMovableId(IReadOnlyList<LevelWorldObjectInstanceDefinition> instances)
        {
            int next = 1;
            if (instances == null)
                return next;

            for (int i = 0; i < instances.Count; i++)
            {
                LevelWorldObjectInstanceDefinition instance = instances[i];
                if (instance != null)
                    next = Math.Max(next, instance.NumericInstanceId + 1);
            }

            return next;
        }

        private static void AppendAssignedId(int id, ref int firstAssignedId, ref int lastAssignedId)
        {
            if (id <= 0)
                return;

            AppendAssignedRange(id, id, ref firstAssignedId, ref lastAssignedId);
        }

        private static void AppendAssignedRange(
            int firstId,
            int lastId,
            ref int firstAssignedId,
            ref int lastAssignedId)
        {
            if (firstId <= 0 || lastId <= 0)
                return;

            if (lastId < firstId)
            {
                int swap = firstId;
                firstId = lastId;
                lastId = swap;
            }

            firstAssignedId = firstAssignedId <= 0 ? firstId : Math.Min(firstAssignedId, firstId);
            lastAssignedId = Math.Max(lastAssignedId, lastId);
        }

        private static int ResolveNextStaticBodyId(
            LevelWorldObjectAuthoringDocument document,
            Transform root)
        {
            int next = DefaultStaticBodyIdStart;
            if (document == null)
                return next;

            if (document.SingleStaticColliders != null)
            {
                for (int i = 0; i < document.SingleStaticColliders.Count; i++)
                {
                    LevelWorldObjectInstanceDefinition instance = document.SingleStaticColliders[i];
                    if (instance != null && instance.BodyId > 0)
                        next = Math.Max(next, instance.BodyId + 1);
                }
            }

            if (document.StaticColliderBatches != null)
            {
                for (int i = 0; i < document.StaticColliderBatches.Count; i++)
                {
                    LevelWorldObjectStaticColliderBatchDefinition batch = document.StaticColliderBatches[i];
                    if (batch == null || batch.BodyIdStart <= 0)
                        continue;

                    next = Math.Max(next, batch.BodyIdStart + ResolveStaticBatchBodyIdSpan(root, batch));
                }
            }

            return next;
        }

        private static int ResolveStaticBatchBodyIdSpan(
            Transform root,
            LevelWorldObjectStaticColliderBatchDefinition batch)
        {
            if (root != null
                && batch != null
                && CharacterTestWorldObjectAuthoringAdapter.TryResolveTransform(
                    root,
                    batch.ContainerTransformRef,
                    out Transform container)
                && container != null)
            {
                return Math.Max(1, container.childCount);
            }

            return 1;
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

        private static LevelWorldObjectAuthoringDocument EnsureCollections(
            LevelWorldObjectAuthoringDocument document)
        {
            document.MovableInstances ??= new List<LevelWorldObjectInstanceDefinition>();
            document.SingleStaticColliders ??= new List<LevelWorldObjectInstanceDefinition>();
            document.StaticColliderBatches ??= new List<LevelWorldObjectStaticColliderBatchDefinition>();
            document.Points ??= new List<LevelWorldObjectPointDefinition>();
            document.Regions ??= new List<LevelWorldObjectRegionDefinition>();
            return document;
        }

        private static void CopyDocument(
            LevelWorldObjectAuthoringDocument source,
            LevelWorldObjectAuthoringDocument target)
        {
            target.Format = source.Format;
            target.SchemaVersion = source.SchemaVersion;
            target.LevelId = source.LevelId;
            target.ScenePath = source.ScenePath;
            target.RootPath = source.RootPath;
            target.Instances = source.Instances;
            target.MovableInstances = source.MovableInstances;
            target.SingleStaticColliders = source.SingleStaticColliders;
            target.StaticColliderBatches = source.StaticColliderBatches;
            target.Points = source.Points;
            target.Regions = source.Regions;
        }

        public readonly struct AddRowsResult
        {
            public AddRowsResult(
                int added,
                int skippedInvalid,
                int skippedDuplicate,
                int firstAssignedId,
                int lastAssignedId,
                string assignedIdLabel)
            {
                Added = Math.Max(0, added);
                SkippedInvalid = Math.Max(0, skippedInvalid);
                SkippedDuplicate = Math.Max(0, skippedDuplicate);
                FirstAssignedId = Math.Max(0, firstAssignedId);
                LastAssignedId = Math.Max(0, lastAssignedId);
                AssignedIdLabel = string.IsNullOrWhiteSpace(assignedIdLabel)
                    ? "ids"
                    : assignedIdLabel.Trim();
            }

            public static AddRowsResult Empty { get; } =
                new AddRowsResult(0, 0, 0, 0, 0, "ids");

            public int Added { get; }

            public int SkippedInvalid { get; }

            public int SkippedDuplicate { get; }

            public int SkippedTotal => SkippedInvalid + SkippedDuplicate;

            public int FirstAssignedId { get; }

            public int LastAssignedId { get; }

            public string AssignedIdLabel { get; }

            public bool HasAssignedIds => FirstAssignedId > 0 && LastAssignedId > 0;
        }
    }
}
