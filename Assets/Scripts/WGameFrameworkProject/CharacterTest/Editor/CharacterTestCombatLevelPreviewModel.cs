using System;
using System.Collections.Generic;
using MxFramework.Combat.Levels;
using MxFramework.Combat.Physics;
using UnityEngine;

namespace MxFramework.Game.CharacterTest.Editor
{
    public static class CharacterTestCombatLevelPreviewDiagnosticCodes
    {
        public const string MissingAabbMarker = "MissingAabbMarker";
        public const string InvalidMarkerLayer = "InvalidMarkerLayer";
        public const string InvalidMarkerExtents = "InvalidMarkerExtents";
    }

    public sealed class CharacterTestCombatLevelPreviewModel
    {
        private const float FixedRawScale = 1000000f;

        private CharacterTestCombatLevelPreviewModel(
            CombatLevelDefinition definition,
            string contentHash,
            IReadOnlyList<CharacterTestCombatLevelPreviewCollider> colliders,
            IReadOnlyList<CharacterTestCombatLevelPreviewIssue> issues)
        {
            Definition = definition;
            ContentHash = contentHash ?? string.Empty;
            Colliders = CopyList(colliders);
            Issues = CopyList(issues);
            LevelStableId = definition == null ? string.Empty : definition.LevelStableId;
            DisplayName = definition == null ? string.Empty : definition.DisplayName;
            SpawnPointCount = definition == null ? 0 : definition.SpawnPoints.Count;
            DefaultSpawnPosition = ResolveDefaultSpawnPosition(definition);
            CountIssues(Issues, out int warningCount, out int errorCount);
            WarningCount = warningCount;
            ErrorCount = errorCount;
        }

        public CombatLevelDefinition Definition { get; }

        public string LevelStableId { get; }

        public string DisplayName { get; }

        public string ContentHash { get; }

        public IReadOnlyList<CharacterTestCombatLevelPreviewCollider> Colliders { get; }

        public IReadOnlyList<CharacterTestCombatLevelPreviewIssue> Issues { get; }

        public int ColliderCount => Colliders.Count;

        public int SpawnPointCount { get; }

        public Vector3 DefaultSpawnPosition { get; }

        public int WarningCount { get; }

        public int ErrorCount { get; }

        public bool HasErrors => ErrorCount > 0;

        public bool HasColliderIssue(CharacterTestCombatLevelPreviewCollider collider)
        {
            if (collider == null)
            {
                return false;
            }

            for (int i = 0; i < Issues.Count; i++)
            {
                CharacterTestCombatLevelPreviewIssue issue = Issues[i];
                if (issue.Severity == CombatLevelValidationSeverity.Info)
                {
                    continue;
                }

                if (IssueMatchesCollider(issue, collider))
                {
                    return true;
                }
            }

            return false;
        }

        public static CharacterTestCombatLevelPreviewModel Build(
            CharacterTestCombatPhysicsSceneBridge bridge)
        {
            if (bridge == null)
            {
                throw new ArgumentNullException(nameof(bridge));
            }

            CharacterTestCombatLevelSceneAdapterResult result =
                CharacterTestCombatLevelSceneAdapter.BuildLevel(bridge);
            return Build(result, bridge);
        }

        public static CharacterTestCombatLevelPreviewModel Build(
            Transform root,
            CharacterTestWorldObjectSceneCache cache)
        {
            return Build(CharacterTestCombatLevelSceneAdapter.BuildLevel(root, cache), null);
        }

        public static CharacterTestCombatLevelPreviewModel Build(
            CharacterTestCombatLevelSceneAdapterResult result)
        {
            return Build(result, null);
        }

        private static CharacterTestCombatLevelPreviewModel Build(
            CharacterTestCombatLevelSceneAdapterResult result,
            CharacterTestCombatPhysicsSceneBridge bridge)
        {
            if (result == null)
                throw new ArgumentNullException(nameof(result));
            var issues = new List<CharacterTestCombatLevelPreviewIssue>();
            AddReportIssues(issues, "adapter", result.AdapterReport);
            AddReportIssues(issues, "definition", result.DefinitionReport);
            if (bridge != null)
                AddMarkerIssues(issues, bridge);
            if (result.StaticColliderCount == 0)
            {
                issues.Add(new CharacterTestCombatLevelPreviewIssue(
                    CombatLevelValidationSeverity.Warning,
                    CharacterTestCombatLevelPreviewDiagnosticCodes.MissingAabbMarker,
                    bridge == null ? result.Definition?.LevelStableId ?? string.Empty : bridge.name,
                    "CharacterTest combat level preview has no enabled static colliders.",
                    "editor"));
            }

            List<CharacterTestCombatLevelPreviewCollider> colliders =
                BuildColliders(result.Definition);
            colliders.Sort(CharacterTestCombatLevelPreviewColliderComparer.Instance);
            return new CharacterTestCombatLevelPreviewModel(
                result.Definition,
                result.ContentHash.ToString(),
                colliders,
                issues);
        }

        public string CreateJsonPreview()
        {
            return Definition == null ? string.Empty : CombatLevelJson.SaveToJson(Definition);
        }

        public static string ResolveMarkerStableId(
            CharacterTestCombatPhysicsSceneBridge bridge,
            CharacterTestCombatPhysicsAabbMarker marker)
        {
            if (marker == null)
            {
                return string.Empty;
            }

            string transformPath = ResolveTransformPath(
                bridge == null ? null : bridge.transform,
                marker.transform);
            return ResolveMarkerStableId(transformPath, marker);
        }

        public static string ResolveStaticColliderStableId(
            CharacterTestCombatPhysicsSceneBridge bridge,
            CharacterTestWorldObjectStaticColliderSpec spec)
        {
            return ResolveStaticColliderStableId(bridge == null ? null : bridge.transform, spec);
        }

        public static string ResolveStaticColliderStableId(
            Transform root,
            CharacterTestWorldObjectStaticColliderSpec spec)
        {
            string transformPath = ResolveTransformPath(root, spec.Transform);
            return ResolveStaticColliderStableId(transformPath, spec);
        }

        private static List<CharacterTestCombatLevelPreviewCollider> BuildColliders(
            CombatLevelDefinition definition)
        {
            var colliders = new List<CharacterTestCombatLevelPreviewCollider>();
            if (definition == null)
            {
                return colliders;
            }

            for (int i = 0; i < definition.AabbColliders.Count; i++)
            {
                CombatLevelAabbColliderDefinition collider = definition.AabbColliders[i];
                if (collider == null)
                {
                    continue;
                }

                Vector3 position = ToVector3(collider.Position);
                Vector3 localMin = ToVector3(collider.LocalMin);
                Vector3 localMax = ToVector3(collider.LocalMax);
                CombatSurfaceMaterial material = collider.SurfaceMaterial.ToSurfaceMaterial();
                colliders.Add(new CharacterTestCombatLevelPreviewCollider(
                    collider.StableId,
                    collider.EntityId,
                    collider.BodyId,
                    collider.ColliderId,
                    collider.Layer,
                    position,
                    localMin,
                    localMax,
                    position + localMin,
                    position + localMax,
                    material.Kind,
                    material.StableId,
                    ToFloat(collider.SurfaceMaterial.FrictionRaw),
                    ToFloat(collider.SurfaceMaterial.ElasticityRaw),
                    ToFloat(collider.SurfaceMaterial.FixedBounceHeightRaw)));
            }

            for (int i = 0; i < definition.ObbColliders.Count; i++)
            {
                CombatLevelObbColliderDefinition collider = definition.ObbColliders[i];
                if (collider == null)
                    continue;
                CombatSurfaceMaterial material = collider.SurfaceMaterial.ToSurfaceMaterial();
                colliders.Add(CharacterTestCombatLevelPreviewCollider.CreateObb(
                    collider.StableId,
                    collider.EntityId,
                    collider.BodyId,
                    collider.ColliderId,
                    collider.Layer,
                    ToVector3(collider.Position) + ToVector3(collider.LocalCenter),
                    ToVector3(collider.HalfExtents),
                    ToVector3(collider.AxisX),
                    ToVector3(collider.AxisY),
                    ToVector3(collider.AxisZ),
                    material.Kind,
                    material.StableId,
                    ToFloat(collider.SurfaceMaterial.FrictionRaw),
                    ToFloat(collider.SurfaceMaterial.ElasticityRaw),
                    ToFloat(collider.SurfaceMaterial.FixedBounceHeightRaw)));
            }

            return colliders;
        }

        private static void AddReportIssues(
            List<CharacterTestCombatLevelPreviewIssue> issues,
            string source,
            CombatLevelValidationReport report)
        {
            if (report == null)
            {
                return;
            }

            for (int i = 0; i < report.Issues.Count; i++)
            {
                CombatLevelValidationIssue issue = report.Issues[i];
                issues.Add(new CharacterTestCombatLevelPreviewIssue(
                    issue.Severity,
                    issue.Code,
                    issue.Path,
                    issue.Message,
                    source));
            }
        }

        private static void AddMarkerIssues(
            List<CharacterTestCombatLevelPreviewIssue> issues,
            CharacterTestCombatPhysicsSceneBridge bridge)
        {
            var markers = new List<CharacterTestCombatPhysicsAabbMarker>();
            bridge.GetComponentsInChildren(includeInactive: true, markers);
            for (int i = 0; i < markers.Count; i++)
            {
                CharacterTestCombatPhysicsAabbMarker marker = markers[i];
                if (marker == null || !marker.IsEnabled)
                {
                    continue;
                }

                string transformPath = ResolveTransformPath(bridge.transform, marker.transform);
                string stableId = ResolveMarkerStableId(transformPath, marker);
                string path = stableId + " " + transformPath;
                if (marker.CombatLayer < 0 || marker.CombatLayer > 31)
                {
                    issues.Add(new CharacterTestCombatLevelPreviewIssue(
                        CombatLevelValidationSeverity.Warning,
                        CharacterTestCombatLevelPreviewDiagnosticCodes.InvalidMarkerLayer,
                        path,
                        "Marker combat layer is outside 0-31 and will be clamped by the v0 adapter.",
                        "editor"));
                }

                Vector3 scale = marker.transform.lossyScale;
                if (Mathf.Abs(scale.x) <= 0.0001f
                    || Mathf.Abs(scale.y) <= 0.0001f
                    || Mathf.Abs(scale.z) <= 0.0001f)
                {
                    issues.Add(new CharacterTestCombatLevelPreviewIssue(
                        CombatLevelValidationSeverity.Warning,
                        CharacterTestCombatLevelPreviewDiagnosticCodes.InvalidMarkerExtents,
                        path,
                        "Marker has a zero-size axis; the runtime adapter will clamp half extents to 0.01.",
                        "editor"));
                }
            }
        }

        private static string ResolveTransformPath(Transform root, Transform target)
        {
            if (target == null)
            {
                return "missing";
            }

            var segments = new List<string>();
            Transform current = target;
            while (current != null && current != root)
            {
                segments.Add(current.name + "[" + current.GetSiblingIndex() + "]");
                current = current.parent;
            }

            if (segments.Count == 0)
            {
                return target.name + "[" + target.GetSiblingIndex() + "]";
            }

            segments.Reverse();
            return string.Join("/", segments.ToArray());
        }

        private static string ResolveMarkerStableId(
            string transformPath,
            CharacterTestCombatPhysicsAabbMarker marker)
        {
            if (marker.BodyId > 0 && marker.ColliderId > 0)
            {
                return "body." + marker.BodyId + ".collider." + marker.ColliderId;
            }

            return "marker." + SanitizeStableSegment(transformPath);
        }

        private static string ResolveStaticColliderStableId(
            string transformPath,
            CharacterTestWorldObjectStaticColliderSpec spec)
        {
            int colliderId = spec.ColliderId > 0 ? spec.ColliderId : 1;
            if (spec.HasExplicitBodyId && colliderId > 0)
            {
                return "body." + spec.BodyId + ".collider." + colliderId;
            }

            return "marker." + SanitizeStableSegment(transformPath);
        }

        private static string SanitizeStableSegment(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            var chars = new char[value.Length];
            int count = 0;
            bool lastWasSeparator = false;
            for (int i = 0; i < value.Length; i++)
            {
                char c = value[i];
                bool isAsciiLetter = (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z');
                bool isDigit = c >= '0' && c <= '9';
                if (isAsciiLetter || isDigit)
                {
                    chars[count++] = char.ToLowerInvariant(c);
                    lastWasSeparator = false;
                }
                else if (!lastWasSeparator)
                {
                    chars[count++] = '_';
                    lastWasSeparator = true;
                }
            }

            while (count > 0 && chars[count - 1] == '_')
            {
                count--;
            }

            return count == 0 ? string.Empty : new string(chars, 0, count);
        }

        private static Vector3 ResolveDefaultSpawnPosition(CombatLevelDefinition definition)
        {
            if (definition == null || definition.SpawnPoints.Count == 0)
            {
                return Vector3.zero;
            }

            return ToVector3(definition.SpawnPoints[0].Position);
        }

        private static Vector3 ToVector3(CombatLevelFixedVector3 value)
        {
            if (value == null)
            {
                return Vector3.zero;
            }

            return new Vector3(
                ToFloat(value.XRaw),
                ToFloat(value.YRaw),
                ToFloat(value.ZRaw));
        }

        private static float ToFloat(long raw)
        {
            return raw / FixedRawScale;
        }

        private static List<T> CopyList<T>(IReadOnlyList<T> source)
        {
            if (source == null || source.Count == 0)
            {
                return new List<T>();
            }

            return new List<T>(source);
        }

        private static void CountIssues(
            IReadOnlyList<CharacterTestCombatLevelPreviewIssue> issues,
            out int warningCount,
            out int errorCount)
        {
            warningCount = 0;
            errorCount = 0;
            for (int i = 0; i < issues.Count; i++)
            {
                if (issues[i].Severity == CombatLevelValidationSeverity.Error)
                {
                    errorCount++;
                }
                else if (issues[i].Severity == CombatLevelValidationSeverity.Warning)
                {
                    warningCount++;
                }
            }
        }

        private static bool IssueMatchesCollider(
            CharacterTestCombatLevelPreviewIssue issue,
            CharacterTestCombatLevelPreviewCollider collider)
        {
            if (issue == null || collider == null)
            {
                return false;
            }

            string bodyText = collider.BodyId.ToString();
            string colliderText = collider.ColliderId.ToString();
            return issue.Path.Contains(collider.StableId)
                || (issue.Path.Contains(bodyText) && issue.Path.Contains(colliderText))
                || issue.Message.Contains(collider.StableId);
        }
    }

    public sealed class CharacterTestCombatLevelPreviewCollider
    {
        public CharacterTestCombatLevelPreviewCollider(
            string stableId,
            int entityId,
            int bodyId,
            int colliderId,
            int layer,
            Vector3 position,
            Vector3 localMin,
            Vector3 localMax,
            Vector3 worldMin,
            Vector3 worldMax,
            CombatSurfaceMaterialKind surfaceMaterialKind,
            string surfaceMaterialStableId,
            float surfaceFriction,
            float surfaceElasticity,
            float surfaceFixedBounceHeight)
        {
            StableId = stableId ?? string.Empty;
            EntityId = entityId;
            BodyId = bodyId;
            ColliderId = colliderId;
            Layer = layer;
            Position = position;
            LocalMin = localMin;
            LocalMax = localMax;
            WorldMin = worldMin;
            WorldMax = worldMax;
            SurfaceMaterialKind = surfaceMaterialKind;
            SurfaceMaterialStableId = surfaceMaterialStableId ?? string.Empty;
            SurfaceFriction = surfaceFriction;
            SurfaceElasticity = surfaceElasticity;
            SurfaceFixedBounceHeight = surfaceFixedBounceHeight;
            ShapeKind = CombatPhysicsShapeKind.Aabb;
            Center = (worldMin + worldMax) * 0.5f;
            HalfExtents = (worldMax - worldMin) * 0.5f;
            AxisX = Vector3.right;
            AxisY = Vector3.up;
            AxisZ = Vector3.forward;
        }

        public static CharacterTestCombatLevelPreviewCollider CreateObb(
            string stableId, int entityId, int bodyId, int colliderId, int layer,
            Vector3 center, Vector3 halfExtents, Vector3 axisX, Vector3 axisY, Vector3 axisZ,
            CombatSurfaceMaterialKind materialKind, string materialStableId,
            float friction, float elasticity, float bounceHeight)
        {
            Vector3 extent = Abs(axisX) * halfExtents.x + Abs(axisY) * halfExtents.y + Abs(axisZ) * halfExtents.z;
            var value = new CharacterTestCombatLevelPreviewCollider(
                stableId, entityId, bodyId, colliderId, layer,
                center, -halfExtents, halfExtents, center - extent, center + extent,
                materialKind, materialStableId, friction, elasticity, bounceHeight);
            value.ShapeKind = CombatPhysicsShapeKind.Obb;
            value.Center = center;
            value.HalfExtents = halfExtents;
            value.AxisX = axisX;
            value.AxisY = axisY;
            value.AxisZ = axisZ;
            return value;
        }

        public string StableId { get; }
        public int EntityId { get; }
        public int BodyId { get; }
        public int ColliderId { get; }
        public int Layer { get; }
        public Vector3 Position { get; }
        public Vector3 LocalMin { get; }
        public Vector3 LocalMax { get; }
        public Vector3 WorldMin { get; }
        public Vector3 WorldMax { get; }
        public CombatSurfaceMaterialKind SurfaceMaterialKind { get; }
        public string SurfaceMaterialStableId { get; }
        public float SurfaceFriction { get; }
        public float SurfaceElasticity { get; }
        public float SurfaceFixedBounceHeight { get; }
        public CombatPhysicsShapeKind ShapeKind { get; private set; }
        public Vector3 Center { get; private set; }
        public Vector3 HalfExtents { get; private set; }
        public Vector3 AxisX { get; private set; }
        public Vector3 AxisY { get; private set; }
        public Vector3 AxisZ { get; private set; }

        public void CopyWorldCornersTo(Vector3[] corners)
        {
            if (corners == null || corners.Length < 8)
                throw new ArgumentException("Eight preview corners are required.", nameof(corners));
            int index = 0;
            for (int x = -1; x <= 1; x += 2)
            for (int y = -1; y <= 1; y += 2)
            for (int z = -1; z <= 1; z += 2)
                corners[index++] = Center + AxisX * (HalfExtents.x * x) + AxisY * (HalfExtents.y * y) + AxisZ * (HalfExtents.z * z);
        }

        private static Vector3 Abs(Vector3 value) =>
            new Vector3(Mathf.Abs(value.x), Mathf.Abs(value.y), Mathf.Abs(value.z));
    }

    public sealed class CharacterTestCombatLevelPreviewIssue
    {
        public CharacterTestCombatLevelPreviewIssue(
            CombatLevelValidationSeverity severity,
            string code,
            string path,
            string message,
            string source)
        {
            Severity = severity;
            Code = code ?? string.Empty;
            Path = path ?? string.Empty;
            Message = message ?? string.Empty;
            Source = source ?? string.Empty;
        }

        public CombatLevelValidationSeverity Severity { get; }
        public string Code { get; }
        public string Path { get; }
        public string Message { get; }
        public string Source { get; }
    }

    internal sealed class CharacterTestCombatLevelPreviewColliderComparer :
        IComparer<CharacterTestCombatLevelPreviewCollider>
    {
        public static readonly CharacterTestCombatLevelPreviewColliderComparer Instance =
            new CharacterTestCombatLevelPreviewColliderComparer();

        private CharacterTestCombatLevelPreviewColliderComparer()
        {
        }

        public int Compare(
            CharacterTestCombatLevelPreviewCollider left,
            CharacterTestCombatLevelPreviewCollider right)
        {
            int compare = string.CompareOrdinal(left.StableId, right.StableId);
            if (compare != 0)
            {
                return compare;
            }

            compare = left.BodyId.CompareTo(right.BodyId);
            if (compare != 0)
            {
                return compare;
            }

            return left.ColliderId.CompareTo(right.ColliderId);
        }
    }
}
