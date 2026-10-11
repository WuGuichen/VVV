using System;
using System.Collections.Generic;
using MxFramework.Combat.Physics;
using MxFramework.Core.Math;

namespace MxFramework.Combat.Levels
{
    public static class CombatLevelValidationCodes
    {
        public const string NullDefinition = "NullDefinition";
        public const string UnsupportedSchemaVersion = "UnsupportedSchemaVersion";
        public const string MissingLevelStableId = "MissingLevelStableId";
        public const string MissingSpawnPoint = "MissingSpawnPoint";
        public const string NullCollider = "NullCollider";
        public const string MissingStableId = "MissingStableId";
        public const string DuplicateStableId = "DuplicateStableId";
        public const string InvalidEntityId = "InvalidEntityId";
        public const string InvalidBodyId = "InvalidBodyId";
        public const string InvalidColliderId = "InvalidColliderId";
        public const string DuplicateBodyId = "DuplicateBodyId";
        public const string DuplicateColliderId = "DuplicateColliderId";
        public const string InvalidLayer = "InvalidLayer";
        public const string InvalidAabb = "InvalidAabb";
        public const string InvalidObbPosition = "InvalidObbPosition";
        public const string InvalidObbLocalCenter = "InvalidObbLocalCenter";
        public const string InvalidObbHalfExtents = "InvalidObbHalfExtents";
        public const string InvalidObbAxis = "InvalidObbAxis";
        public const string InvalidObbOrthogonality = "InvalidObbOrthogonality";
        public const string InvalidObbHandedness = "InvalidObbHandedness";
        public const string InvalidSurfaceMaterial = "InvalidSurfaceMaterial";
        public const string NullPoint = "NullPoint";
    }

    public enum CombatLevelValidationSeverity
    {
        Info = 0,
        Warning = 1,
        Error = 2
    }

    public readonly struct CombatLevelValidationIssue
    {
        public CombatLevelValidationIssue(
            CombatLevelValidationSeverity severity,
            string code,
            string path,
            string message)
        {
            Severity = severity;
            Code = code ?? string.Empty;
            Path = path ?? string.Empty;
            Message = message ?? string.Empty;
        }

        public CombatLevelValidationSeverity Severity { get; }

        public string Code { get; }

        public string Path { get; }

        public string Message { get; }
    }

    public sealed class CombatLevelValidationReport
    {
        private readonly List<CombatLevelValidationIssue> _issues = new List<CombatLevelValidationIssue>();

        public IReadOnlyList<CombatLevelValidationIssue> Issues => _issues;

        public bool HasErrors { get; private set; }

        public void Add(CombatLevelValidationIssue issue)
        {
            _issues.Add(issue);
            if (issue.Severity == CombatLevelValidationSeverity.Error)
            {
                HasErrors = true;
            }
        }

        internal void AddError(string code, string path, string message)
        {
            Add(new CombatLevelValidationIssue(
                CombatLevelValidationSeverity.Error,
                code,
                path,
                message));
        }
    }

    public static class CombatLevelValidator
    {
        public const long AxisUnitLengthSquaredToleranceRaw = 32L;
        public const long AxisOrthogonalityToleranceRaw = 32L;
        public const long AxisHandednessToleranceRaw = 64L;
        public const long IdentityAxisComponentToleranceRaw = 8L;

        public static CombatLevelValidationReport Validate(CombatLevelDefinition definition)
        {
            var report = new CombatLevelValidationReport();
            if (definition == null)
            {
                report.AddError(
                    CombatLevelValidationCodes.NullDefinition,
                    "$",
                    "Combat level definition is null.");
                return report;
            }

            if (definition.SchemaVersion != CombatLevelDefinition.CurrentSchemaVersion)
            {
                report.AddError(
                    CombatLevelValidationCodes.UnsupportedSchemaVersion,
                    "$.schemaVersion",
                    "Combat level schema version is not supported.");
            }

            if (string.IsNullOrWhiteSpace(definition.LevelStableId))
            {
                report.AddError(
                    CombatLevelValidationCodes.MissingLevelStableId,
                    "$.levelStableId",
                    "Combat level stable id is required.");
            }

            if (definition.SpawnPoints.Count == 0)
            {
                report.AddError(
                    CombatLevelValidationCodes.MissingSpawnPoint,
                    "$.spawnPoints",
                    "Combat level requires at least one spawn point.");
            }

            var stableIds = new HashSet<string>(StringComparer.Ordinal);
            var bodyIds = new HashSet<int>();
            var colliderKeys = new HashSet<string>(StringComparer.Ordinal);

            ValidateColliders(definition, report, stableIds, bodyIds, colliderKeys);
            ValidatePoints(definition.SpawnPoints, "$.spawnPoints", report, stableIds);
            ValidatePoints(definition.Checkpoints, "$.checkpoints", report, stableIds);

            return report;
        }

        private static void ValidateColliders(
            CombatLevelDefinition definition,
            CombatLevelValidationReport report,
            HashSet<string> stableIds,
            HashSet<int> bodyIds,
            HashSet<string> colliderKeys)
        {
            for (int i = 0; i < definition.AabbColliders.Count; i++)
            {
                string path = "$.aabbColliders[" + i + "]";
                CombatLevelAabbColliderDefinition collider = definition.AabbColliders[i];
                if (collider == null)
                {
                    report.AddError(
                        CombatLevelValidationCodes.NullCollider,
                        path,
                        "AABB collider definition is null.");
                    continue;
                }

                ValidateStableId(collider.StableId, path + ".stableId", report, stableIds);

                if (collider.EntityId <= 0)
                {
                    report.AddError(
                        CombatLevelValidationCodes.InvalidEntityId,
                        path + ".entityId",
                        "AABB collider entity id must be greater than zero.");
                }

                if (collider.BodyId <= 0)
                {
                    report.AddError(
                        CombatLevelValidationCodes.InvalidBodyId,
                        path + ".bodyId",
                        "AABB collider body id must be greater than zero.");
                }
                else if (!bodyIds.Add(collider.BodyId))
                {
                    report.AddError(
                        CombatLevelValidationCodes.DuplicateBodyId,
                        path + ".bodyId",
                        "AABB collider body id must be unique in level v2.");
                }

                if (collider.ColliderId <= 0)
                {
                    report.AddError(
                        CombatLevelValidationCodes.InvalidColliderId,
                        path + ".colliderId",
                        "AABB collider id must be greater than zero.");
                }
                else
                {
                    string colliderKey = collider.BodyId + ":" + collider.ColliderId;
                    if (!colliderKeys.Add(colliderKey))
                    {
                        report.AddError(
                            CombatLevelValidationCodes.DuplicateColliderId,
                            path + ".colliderId",
                            "AABB collider body/collider id pair must be unique.");
                    }
                }

                ValidateLayer(collider.Layer, path + ".layer", report);
                ValidateAabb(collider, path, report);
                ValidateSurfaceMaterial(collider.SurfaceMaterial, path + ".surfaceMaterial", report);
            }

            for (int i = 0; i < definition.ObbColliders.Count; i++)
            {
                string path = "$.obbColliders[" + i + "]";
                CombatLevelObbColliderDefinition collider = definition.ObbColliders[i];
                if (collider == null)
                {
                    report.AddError(
                        CombatLevelValidationCodes.NullCollider,
                        path,
                        "OBB collider definition is null.");
                    continue;
                }

                ValidateStableId(collider.StableId, path + ".stableId", report, stableIds);

                if (collider.EntityId <= 0)
                {
                    report.AddError(
                        CombatLevelValidationCodes.InvalidEntityId,
                        path + ".entityId",
                        "OBB collider entity id must be greater than zero.");
                }

                if (collider.BodyId <= 0)
                {
                    report.AddError(
                        CombatLevelValidationCodes.InvalidBodyId,
                        path + ".bodyId",
                        "OBB collider body id must be greater than zero.");
                }
                else if (!bodyIds.Add(collider.BodyId))
                {
                    report.AddError(
                        CombatLevelValidationCodes.DuplicateBodyId,
                        path + ".bodyId",
                        "OBB collider body id must be unique in level v2.");
                }

                if (collider.ColliderId <= 0)
                {
                    report.AddError(
                        CombatLevelValidationCodes.InvalidColliderId,
                        path + ".colliderId",
                        "OBB collider id must be greater than zero.");
                }
                else
                {
                    string colliderKey = collider.BodyId + ":" + collider.ColliderId;
                    if (!colliderKeys.Add(colliderKey))
                    {
                        report.AddError(
                            CombatLevelValidationCodes.DuplicateColliderId,
                            path + ".colliderId",
                            "OBB collider body/collider id pair must be unique.");
                    }
                }

                ValidateLayer(collider.Layer, path + ".layer", report);
                ValidateObb(collider, path, report);
                ValidateSurfaceMaterial(collider.SurfaceMaterial, path + ".surfaceMaterial", report);
            }
        }

        private static void ValidateObb(
            CombatLevelObbColliderDefinition collider,
            string path,
            CombatLevelValidationReport report)
        {
            if (collider.Position == null)
            {
                report.AddError(
                    CombatLevelValidationCodes.InvalidObbPosition,
                    path + ".position",
                    "OBB position is required.");
            }

            if (collider.LocalCenter == null)
            {
                report.AddError(
                    CombatLevelValidationCodes.InvalidObbLocalCenter,
                    path + ".localCenter",
                    "OBB local center is required.");
            }

            if (collider.HalfExtents == null
                || collider.HalfExtents.XRaw <= 0L
                || collider.HalfExtents.YRaw <= 0L
                || collider.HalfExtents.ZRaw <= 0L)
            {
                report.AddError(
                    CombatLevelValidationCodes.InvalidObbHalfExtents,
                    path + ".halfExtents",
                    "OBB half extents must be present and greater than zero on all axes.");
            }

            bool axisXAvailable = ValidateObbAxis(collider.AxisX, path + ".axisX", "X", report);
            bool axisYAvailable = ValidateObbAxis(collider.AxisY, path + ".axisY", "Y", report);
            bool axisZAvailable = ValidateObbAxis(collider.AxisZ, path + ".axisZ", "Z", report);

            if (axisXAvailable && axisYAvailable)
            {
                ValidateObbOrthogonality(
                    collider.AxisX,
                    collider.AxisY,
                    path + ".axisX",
                    "X/Y",
                    report);
            }

            if (axisXAvailable && axisZAvailable)
            {
                ValidateObbOrthogonality(
                    collider.AxisX,
                    collider.AxisZ,
                    path + ".axisX",
                    "X/Z",
                    report);
            }

            if (axisYAvailable && axisZAvailable)
            {
                ValidateObbOrthogonality(
                    collider.AxisY,
                    collider.AxisZ,
                    path + ".axisY",
                    "Y/Z",
                    report);
            }

            if (axisXAvailable && axisYAvailable && axisZAvailable)
            {
                if (!TryHandednessRaw(collider.AxisX, collider.AxisY, collider.AxisZ, out long handednessRaw))
                {
                    report.AddError(
                        CombatLevelValidationCodes.InvalidObbAxis,
                        path + ".axisZ",
                        "OBB axis raw handedness arithmetic overflowed.");
                }
                else if (!IsWithin(
                    handednessRaw,
                    Fix64.One.RawValue,
                    AxisHandednessToleranceRaw))
                {
                    report.AddError(
                        CombatLevelValidationCodes.InvalidObbHandedness,
                        path + ".axisZ",
                        "OBB axes must form a right-handed basis within the raw tolerance.");
                }
            }
        }

        private static bool ValidateObbAxis(
            CombatLevelFixedVector3 axis,
            string path,
            string axisName,
            CombatLevelValidationReport report)
        {
            if (axis == null)
            {
                report.AddError(
                    CombatLevelValidationCodes.InvalidObbAxis,
                    path,
                    "OBB axis " + axisName + " is required.");
                return false;
            }

            if (axis.XRaw == 0L && axis.YRaw == 0L && axis.ZRaw == 0L)
            {
                report.AddError(
                    CombatLevelValidationCodes.InvalidObbAxis,
                    path,
                    "OBB axis " + axisName + " cannot be zero.");
                return false;
            }

            if (!TryDotRaw(axis, axis, out long lengthSquaredRaw))
            {
                report.AddError(
                    CombatLevelValidationCodes.InvalidObbAxis,
                    path,
                    "OBB axis " + axisName + " raw arithmetic overflowed.");
                return false;
            }

            if (!IsWithin(
                    lengthSquaredRaw,
                    Fix64.One.RawValue,
                    AxisUnitLengthSquaredToleranceRaw))
            {
                report.AddError(
                    CombatLevelValidationCodes.InvalidObbAxis,
                    path,
                    "OBB axis " + axisName + " must have unit length within the raw tolerance.");
            }

            return true;
        }

        private static void ValidateObbOrthogonality(
            CombatLevelFixedVector3 left,
            CombatLevelFixedVector3 right,
            string path,
            string pairName,
            CombatLevelValidationReport report)
        {
            if (!TryDotRaw(left, right, out long dotRaw))
            {
                report.AddError(
                    CombatLevelValidationCodes.InvalidObbAxis,
                    path,
                    "OBB axes " + pairName + " raw orthogonality arithmetic overflowed.");
                return;
            }

            if (dotRaw < -AxisOrthogonalityToleranceRaw
                || dotRaw > AxisOrthogonalityToleranceRaw)
            {
                report.AddError(
                    CombatLevelValidationCodes.InvalidObbOrthogonality,
                    path,
                    "OBB axes " + pairName + " must be orthogonal within the raw tolerance.");
            }
        }

        private static bool TryHandednessRaw(
            CombatLevelFixedVector3 axisX,
            CombatLevelFixedVector3 axisY,
            CombatLevelFixedVector3 axisZ,
            out long handednessRaw)
        {
            handednessRaw = 0L;
            try
            {
                long crossX = checked(
                    MultiplyFixedRaw(axisX.YRaw, axisY.ZRaw)
                    - MultiplyFixedRaw(axisX.ZRaw, axisY.YRaw));
                long crossY = checked(
                    MultiplyFixedRaw(axisX.ZRaw, axisY.XRaw)
                    - MultiplyFixedRaw(axisX.XRaw, axisY.ZRaw));
                long crossZ = checked(
                    MultiplyFixedRaw(axisX.XRaw, axisY.YRaw)
                    - MultiplyFixedRaw(axisX.YRaw, axisY.XRaw));
                handednessRaw = checked(
                    checked(MultiplyFixedRaw(crossX, axisZ.XRaw)
                        + MultiplyFixedRaw(crossY, axisZ.YRaw))
                    + MultiplyFixedRaw(crossZ, axisZ.ZRaw));
                return true;
            }
            catch (OverflowException)
            {
                return false;
            }
        }

        private static bool TryDotRaw(
            CombatLevelFixedVector3 left,
            CombatLevelFixedVector3 right,
            out long dotRaw)
        {
            dotRaw = 0L;
            try
            {
                dotRaw = checked(
                    checked(MultiplyFixedRaw(left.XRaw, right.XRaw)
                        + MultiplyFixedRaw(left.YRaw, right.YRaw))
                    + MultiplyFixedRaw(left.ZRaw, right.ZRaw));
                return true;
            }
            catch (OverflowException)
            {
                return false;
            }
        }

        private static long MultiplyFixedRaw(long left, long right)
        {
            return checked(left * right) / Fix64.Scale;
        }

        private static bool IsWithin(long value, long expected, long tolerance)
        {
            return value >= expected - tolerance && value <= expected + tolerance;
        }

        private static void ValidatePoints(
            IReadOnlyList<CombatLevelPointDefinition> points,
            string path,
            CombatLevelValidationReport report,
            HashSet<string> stableIds)
        {
            for (int i = 0; i < points.Count; i++)
            {
                string pointPath = path + "[" + i + "]";
                CombatLevelPointDefinition point = points[i];
                if (point == null)
                {
                    report.AddError(
                        CombatLevelValidationCodes.NullPoint,
                        pointPath,
                        "Level point definition is null.");
                    continue;
                }

                ValidateStableId(point.StableId, pointPath + ".stableId", report, stableIds);
            }
        }

        private static void ValidateStableId(
            string stableId,
            string path,
            CombatLevelValidationReport report,
            HashSet<string> stableIds)
        {
            if (string.IsNullOrWhiteSpace(stableId))
            {
                report.AddError(
                    CombatLevelValidationCodes.MissingStableId,
                    path,
                    "Stable id is required.");
                return;
            }

            if (!stableIds.Add(stableId))
            {
                report.AddError(
                    CombatLevelValidationCodes.DuplicateStableId,
                    path,
                    "Stable id must be unique across level objects.");
            }
        }

        private static void ValidateLayer(int layer, string path, CombatLevelValidationReport report)
        {
            try
            {
                CombatPhysicsLayerMask.FromLayer(layer);
            }
            catch (ArgumentOutOfRangeException)
            {
                report.AddError(
                    CombatLevelValidationCodes.InvalidLayer,
                    path,
                    "Combat physics layer must be in range 0-31.");
            }
        }

        private static void ValidateAabb(
            CombatLevelAabbColliderDefinition collider,
            string path,
            CombatLevelValidationReport report)
        {
            CombatLevelFixedVector3 min = collider.LocalMin;
            CombatLevelFixedVector3 max = collider.LocalMax;
            if (min.XRaw > max.XRaw || min.YRaw > max.YRaw || min.ZRaw > max.ZRaw)
            {
                report.AddError(
                    CombatLevelValidationCodes.InvalidAabb,
                    path,
                    "AABB local min cannot be greater than local max.");
            }
        }

        private static void ValidateSurfaceMaterial(
            CombatLevelSurfaceMaterialDefinition material,
            string path,
            CombatLevelValidationReport report)
        {
            if (material == null)
            {
                report.AddError(
                    CombatLevelValidationCodes.InvalidSurfaceMaterial,
                    path,
                    "Surface material is required.");
                return;
            }

            if (!IsDefinedSurfaceKind(material.Kind)
                || material.FrictionRaw < 0L
                || material.ElasticityRaw < 0L
                || material.ElasticityRaw > Fix64.Scale
                || material.FixedBounceHeightRaw < 0L)
            {
                report.AddError(
                    CombatLevelValidationCodes.InvalidSurfaceMaterial,
                    path,
                    "Surface material kind and raw values must map to a valid CombatSurfaceMaterial.");
            }
        }

        private static bool IsDefinedSurfaceKind(CombatSurfaceMaterialKind kind)
        {
            return kind == CombatSurfaceMaterialKind.Default
                || kind == CombatSurfaceMaterialKind.HardSlick
                || kind == CombatSurfaceMaterialKind.Rough
                || kind == CombatSurfaceMaterialKind.Elastic
                || kind == CombatSurfaceMaterialKind.BouncePad;
        }
    }
}
