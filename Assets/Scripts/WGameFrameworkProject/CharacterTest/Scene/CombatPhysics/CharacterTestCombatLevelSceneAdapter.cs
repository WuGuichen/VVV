using System;
using System.Collections.Generic;
using MxFramework.Combat.Core;
using MxFramework.Combat.Levels;
using MxFramework.Combat.Physics;
using MxFramework.Core.Math;
using UnityEngine;

namespace MxFramework.Game.CharacterTest
{
    public sealed class CharacterTestCombatLevelSceneAdapterResult
    {
        public CharacterTestCombatLevelSceneAdapterResult(
            CombatLevelDefinition definition,
            CombatLevelValidationReport adapterReport,
            CombatLevelValidationReport definitionReport)
        {
            Definition = definition;
            AdapterReport = adapterReport ?? new CombatLevelValidationReport();
            DefinitionReport = definitionReport ?? new CombatLevelValidationReport();
        }

        public CombatLevelDefinition Definition { get; }

        public CombatLevelValidationReport AdapterReport { get; }

        public CombatLevelValidationReport DefinitionReport { get; }

        public int AabbColliderCount => Definition == null ? 0 : Definition.AabbColliders.Count;

        public int ObbColliderCount => Definition == null ? 0 : Definition.ObbColliders.Count;

        public int StaticColliderCount => AabbColliderCount + ObbColliderCount;

        public bool HasErrors => AdapterReport.HasErrors || DefinitionReport.HasErrors;

        public CombatHash ContentHash => Definition == null
            ? CombatHash.Empty
            : CombatLevelHash.ComputeContentHash(Definition);
    }

    public static class CharacterTestCombatLevelSceneAdapter
    {
        public const string UnsupportedRotationCode = "UnsupportedRotation";
        public const string InvalidTransformNonFiniteCode = "InvalidTransformNonFinite";
        public const string InvalidTransformScaleCode = "InvalidTransformScale";
        public const string InvalidTransformReflectionCode = "InvalidTransformReflection";
        public const string InvalidTransformShearCode = "InvalidTransformShear";
        public const string InvalidQuantizedObbAxesCode = "InvalidQuantizedObbAxes";
        public const string InvalidStaticBoxHalfExtentsCode = "InvalidStaticBoxHalfExtents";

        private const string DefaultLevelStableIdPrefix = "character-test.level.";
        private const float TransformEpsilon = 0.00001f;

        public static CharacterTestCombatLevelSceneAdapterResult BuildLevel(
            CharacterTestCombatPhysicsSceneBridge bridge)
        {
            if (bridge == null)
            {
                throw new ArgumentNullException(nameof(bridge));
            }

            CharacterTestWorldObjectSceneSource worldObjectSource =
                CharacterTestWorldObjectSceneSource.FindFor(bridge.transform);
            if (worldObjectSource != null && worldObjectSource.StaticColliderInstanceCount > 0)
            {
                CharacterTestWorldObjectStaticColliderSpec[] specs =
                    worldObjectSource.CollectStaticColliderSpecs(out string[] diagnostics);
                return BuildLevel(bridge.transform, specs, diagnostics, worldObjectSource);
            }

            var markers = new List<CharacterTestCombatPhysicsAabbMarker>();
            bridge.GetComponentsInChildren(includeInactive: true, markers);
            return BuildLevel(bridge.transform, markers);
        }

        public static CharacterTestCombatLevelSceneAdapterResult BuildLevel(
            Transform root,
            IReadOnlyList<CharacterTestCombatPhysicsAabbMarker> markers)
        {
            if (root == null)
            {
                throw new ArgumentNullException(nameof(root));
            }

            CombatLevelValidationReport adapterReport = new CombatLevelValidationReport();
            List<AabbEntry> entries = CollectEntries(root, markers, adapterReport);
            return BuildLevel(root, entries, adapterReport);
        }

        public static CharacterTestCombatLevelSceneAdapterResult BuildLevel(
            Transform root,
            IReadOnlyList<CharacterTestWorldObjectStaticColliderSpec> staticColliderSpecs)
        {
            return BuildLevel(root, staticColliderSpecs, Array.Empty<string>());
        }

        public static CharacterTestCombatLevelSceneAdapterResult BuildLevel(
            Transform root,
            CharacterTestWorldObjectSceneCache cache)
        {
            if (cache == null)
                return BuildLevel(root, Array.Empty<CharacterTestWorldObjectStaticColliderSpec>());

            CharacterTestWorldObjectStaticColliderSpec[] specs =
                cache.CollectStaticColliderSpecs(out string[] diagnostics);
            return BuildLevel(root, specs, diagnostics, cache);
        }

        private static CharacterTestCombatLevelSceneAdapterResult BuildLevel(
            Transform root,
            IReadOnlyList<CharacterTestWorldObjectStaticColliderSpec> staticColliderSpecs,
            IReadOnlyList<string> sourceDiagnostics)
        {
            return BuildLevel(
                root,
                staticColliderSpecs,
                sourceDiagnostics,
                (CharacterTestWorldObjectSceneCache)null);
        }

        private static CharacterTestCombatLevelSceneAdapterResult BuildLevel(
            Transform root,
            IReadOnlyList<CharacterTestWorldObjectStaticColliderSpec> staticColliderSpecs,
            IReadOnlyList<string> sourceDiagnostics,
            CharacterTestWorldObjectSceneCache cache)
        {
            if (root == null)
            {
                throw new ArgumentNullException(nameof(root));
            }

            CombatLevelValidationReport adapterReport = new CombatLevelValidationReport();
            if (sourceDiagnostics != null)
            {
                for (int i = 0; i < sourceDiagnostics.Count; i++)
                {
                    if (!string.IsNullOrWhiteSpace(sourceDiagnostics[i]))
                    {
                        adapterReport.Add(new CombatLevelValidationIssue(
                            CombatLevelValidationSeverity.Warning,
                            "WorldObjectSceneCache",
                            root.name,
                            sourceDiagnostics[i]));
                    }
                }
            }

            List<AabbEntry> entries = CollectEntries(root, staticColliderSpecs, adapterReport);
            return BuildLevel(root, entries, adapterReport, cache);
        }

        private static CharacterTestCombatLevelSceneAdapterResult BuildLevel(
            Transform root,
            IReadOnlyList<CharacterTestWorldObjectStaticColliderSpec> staticColliderSpecs,
            IReadOnlyList<string> sourceDiagnostics,
            CharacterTestWorldObjectSceneSource worldObjectSource)
        {
            if (root == null)
            {
                throw new ArgumentNullException(nameof(root));
            }

            CombatLevelValidationReport adapterReport = new CombatLevelValidationReport();
            if (sourceDiagnostics != null)
            {
                for (int i = 0; i < sourceDiagnostics.Count; i++)
                {
                    if (!string.IsNullOrWhiteSpace(sourceDiagnostics[i]))
                    {
                        adapterReport.Add(new CombatLevelValidationIssue(
                            CombatLevelValidationSeverity.Warning,
                            "WorldObjectSceneSource",
                            root.name,
                            sourceDiagnostics[i]));
                    }
                }
            }

            List<AabbEntry> entries = CollectEntries(root, staticColliderSpecs, adapterReport);
            return BuildLevel(root, entries, adapterReport, worldObjectSource);
        }

        private static CharacterTestCombatLevelSceneAdapterResult BuildLevel(
            Transform root,
            List<AabbEntry> entries,
            CombatLevelValidationReport adapterReport)
        {
            return BuildLevel(
                root,
                entries,
                adapterReport,
                (IReadOnlyList<CharacterTestWorldObjectSceneSource.LevelPointInstance>)null);
        }

        private static CharacterTestCombatLevelSceneAdapterResult BuildLevel(
            Transform root,
            List<AabbEntry> entries,
            CombatLevelValidationReport adapterReport,
            CharacterTestWorldObjectSceneSource worldObjectSource)
        {
            IReadOnlyList<CharacterTestWorldObjectSceneSource.LevelPointInstance> pointInstances =
                worldObjectSource == null ? null : worldObjectSource.PointInstances;
            return BuildLevel(root, entries, adapterReport, pointInstances);
        }

        private static CharacterTestCombatLevelSceneAdapterResult BuildLevel(
            Transform root,
            List<AabbEntry> entries,
            CombatLevelValidationReport adapterReport,
            CharacterTestWorldObjectSceneCache cache)
        {
            IReadOnlyList<CharacterTestWorldObjectSceneSource.LevelPointInstance> pointInstances =
                cache == null ? null : cache.PointInstances;
            return BuildLevel(root, entries, adapterReport, pointInstances);
        }

        private static CharacterTestCombatLevelSceneAdapterResult BuildLevel(
            Transform root,
            List<AabbEntry> entries,
            CombatLevelValidationReport adapterReport,
            IReadOnlyList<CharacterTestWorldObjectSceneSource.LevelPointInstance> pointInstances)
        {
            entries.Sort(AabbEntryComparer.Instance);
            AssignGeneratedBodyIds(entries);

            var aabbColliders = new List<CombatLevelAabbColliderDefinition>(entries.Count);
            var obbColliders = new List<CombatLevelObbColliderDefinition>(entries.Count);
            for (int i = 0; i < entries.Count; i++)
            {
                AabbEntry entry = entries[i];
                if (entry.IsObb)
                {
                    obbColliders.Add(new CombatLevelObbColliderDefinition(
                        entry.StableId,
                        entry.BodyId,
                        entry.BodyId,
                        entry.ColliderId,
                        entry.Layer,
                        CombatLevelFixedVector3.FromFixVector3(entry.Position),
                        CombatLevelFixedVector3.Zero,
                        CombatLevelFixedVector3.FromFixVector3(entry.HalfExtents),
                        CombatLevelFixedVector3.FromFixVector3(entry.AxisX),
                        CombatLevelFixedVector3.FromFixVector3(entry.AxisY),
                        CombatLevelFixedVector3.FromFixVector3(entry.AxisZ),
                        CombatLevelSurfaceMaterialDefinition.FromSurfaceMaterial(entry.SurfaceMaterial)));
                }
                else
                {
                    aabbColliders.Add(new CombatLevelAabbColliderDefinition(
                        entry.StableId,
                        entry.BodyId,
                        entry.BodyId,
                        entry.ColliderId,
                        entry.Layer,
                        CombatLevelFixedVector3.FromFixVector3(entry.Position),
                        CombatLevelFixedVector3.FromFixVector3(-entry.HalfExtents),
                        CombatLevelFixedVector3.FromFixVector3(entry.HalfExtents),
                        CombatLevelSurfaceMaterialDefinition.FromSurfaceMaterial(entry.SurfaceMaterial)));
                }
            }

            CombatLevelPointDefinition[] spawnPoints =
                CreateSpawnPoints(root, pointInstances, adapterReport);
            var definition = new CombatLevelDefinition(
                CombatLevelDefinition.CurrentSchemaVersion,
                CreateLevelStableId(root),
                root.name,
                aabbColliders,
                obbColliders,
                spawnPoints,
                new CombatLevelPointDefinition[0]);

            return new CharacterTestCombatLevelSceneAdapterResult(
                definition,
                adapterReport,
                CombatLevelValidator.Validate(definition));
        }

        private static CombatLevelPointDefinition[] CreateSpawnPoints(
            Transform root,
            IReadOnlyList<CharacterTestWorldObjectSceneSource.LevelPointInstance> instances,
            CombatLevelValidationReport adapterReport)
        {
            if (instances == null || instances.Count == 0)
            {
                return new[]
                {
                    new CombatLevelPointDefinition("spawn.default", ToLevelVector(root.position))
                };
            }

            var points = new List<CombatLevelPointDefinition>();
            for (int i = 0; i < instances.Count; i++)
            {
                CharacterTestWorldObjectSceneSource.LevelPointInstance instance = instances[i];
                if (instance == null || !instance.IsEnabled || instance.Transform == null)
                    continue;

                if (!CharacterTestWorldObjectSceneSource.BuiltInCatalog.TryGetDefinition(
                        instance.DefinitionId,
                        out CharacterTestWorldObjectDefinition definition)
                    || !definition.CanSpawnPoint)
                {
                    adapterReport.Add(new CombatLevelValidationIssue(
                        CombatLevelValidationSeverity.Warning,
                        "WorldObjectPointDefinition",
                        instance.StableId,
                        "CharacterTest level point definition is missing or is not a point."));
                    continue;
                }

                string stableId = string.IsNullOrWhiteSpace(instance.StableId)
                    ? definition.StableId
                    : instance.StableId;
                points.Add(new CombatLevelPointDefinition(
                    stableId,
                    ToLevelVector(instance.Transform.position)));
            }

            if (points.Count == 0)
            {
                adapterReport.Add(new CombatLevelValidationIssue(
                    CombatLevelValidationSeverity.Error,
                    "MissingLevelSpawnPoints",
                    root.name,
                    "CharacterTest world object source has point cache enabled but produced no spawn points."));
            }

            points.Sort((left, right) => string.CompareOrdinal(left.StableId, right.StableId));
            return points.ToArray();
        }

        private static List<AabbEntry> CollectEntries(
            Transform root,
            IReadOnlyList<CharacterTestCombatPhysicsAabbMarker> markers,
            CombatLevelValidationReport adapterReport)
        {
            var entries = new List<AabbEntry>();
            if (markers == null)
            {
                return entries;
            }

            for (int i = 0; i < markers.Count; i++)
            {
                CharacterTestCombatPhysicsAabbMarker marker = markers[i];
                if (marker == null || !marker.IsEnabled)
                {
                    continue;
                }

                int colliderId = marker.ColliderId > 0 ? marker.ColliderId : 1;
                bool hasExplicitIdentity = marker.BodyId > 0 && marker.ColliderId > 0;
                string stableId = hasExplicitIdentity
                    ? "body." + marker.BodyId + ".collider." + colliderId
                    : "marker." + SanitizeStableSegment(ResolveTransformPath(root, marker.transform));
                if (!TryCompileBox(
                        marker.transform,
                        CharacterTestWorldObjectInstanceScaleSource.SceneTransform,
                        0,
                        0,
                        0,
                        stableId,
                        0,
                        string.Empty,
                        marker.BodyId,
                        colliderId,
                        adapterReport,
                        out CompiledBox compiled))
                {
                    continue;
                }
                entries.Add(new AabbEntry(
                    stableId,
                    marker.BodyId > 0,
                    marker.BodyId,
                    colliderId,
                    Mathf.Clamp(marker.CombatLayer, 0, 31),
                    compiled.Position,
                    compiled.HalfExtents,
                    compiled.AxisX,
                    compiled.AxisY,
                    compiled.AxisZ,
                    compiled.IsObb,
                    marker.SurfaceMaterial));
            }

            return entries;
        }

        private static List<AabbEntry> CollectEntries(
            Transform root,
            IReadOnlyList<CharacterTestWorldObjectStaticColliderSpec> specs,
            CombatLevelValidationReport adapterReport)
        {
            var entries = new List<AabbEntry>();
            if (specs == null)
            {
                return entries;
            }

            for (int i = 0; i < specs.Count; i++)
            {
                CharacterTestWorldObjectStaticColliderSpec spec = specs[i];
                if (spec.ShapeKind != CharacterTestMovableNaturalObjectShapeKind.Aabb)
                    continue;
                if (spec.Transform == null)
                    continue;

                int colliderId = spec.ColliderId > 0 ? spec.ColliderId : 1;
                bool hasExplicitIdentity = spec.HasExplicitBodyId && colliderId > 0;
                string stableId = hasExplicitIdentity
                    ? "body." + spec.BodyId + ".collider." + colliderId
                    : "marker." + SanitizeStableSegment(ResolveTransformPath(root, spec.Transform));
                if (!TryCompileBox(
                        spec.Transform,
                        spec.ScaleSource,
                        spec.DefinitionHalfExtentXMillimeters,
                        spec.DefinitionHalfExtentYMillimeters,
                        spec.DefinitionHalfExtentZMillimeters,
                        stableId,
                        spec.DefinitionId,
                        spec.DefinitionStableId,
                        spec.BodyId,
                        colliderId,
                        adapterReport,
                        out CompiledBox compiled))
                {
                    continue;
                }
                entries.Add(new AabbEntry(
                    stableId,
                    spec.HasExplicitBodyId,
                    spec.BodyId,
                    colliderId,
                    spec.Layer,
                    compiled.Position,
                    compiled.HalfExtents,
                    compiled.AxisX,
                    compiled.AxisY,
                    compiled.AxisZ,
                    compiled.IsObb,
                    spec.SurfaceMaterial));
            }

            return entries;
        }

        private static void AssignGeneratedBodyIds(List<AabbEntry> entries)
        {
            var reserved = new HashSet<int>();
            for (int i = 0; i < entries.Count; i++)
            {
                if (entries[i].HasExplicitBodyId)
                {
                    reserved.Add(entries[i].BodyId);
                }
            }

            int nextBodyId = CharacterTestCharacterPhysicsRuntime.StaticBodyIdStart;
            for (int i = 0; i < entries.Count; i++)
            {
                AabbEntry entry = entries[i];
                if (entry.HasExplicitBodyId)
                {
                    continue;
                }

                while (reserved.Contains(nextBodyId))
                {
                    nextBodyId++;
                }

                entry.BodyId = nextBodyId;
                reserved.Add(nextBodyId);
                nextBodyId++;
            }
        }

        private static bool TryCompileBox(
            Transform transform,
            CharacterTestWorldObjectInstanceScaleSource scaleSource,
            int definitionHalfExtentXMillimeters,
            int definitionHalfExtentYMillimeters,
            int definitionHalfExtentZMillimeters,
            string stableId,
            int definitionId,
            string definitionStableId,
            int bodyId,
            int colliderId,
            CombatLevelValidationReport report,
            out CompiledBox compiled)
        {
            compiled = default;
            Transform current = transform;
            while (current != null)
            {
                Vector3 localScale = current.localScale;
                if (!IsFinite(localScale))
                    return Fail(InvalidTransformNonFiniteCode, "local-scale-non-finite", out compiled);
                if (localScale.x <= 0f || localScale.y <= 0f || localScale.z <= 0f
                    || localScale.x < TransformEpsilon
                    || localScale.y < TransformEpsilon
                    || localScale.z < TransformEpsilon)
                {
                    return Fail(InvalidTransformScaleCode, "local-scale-non-positive-or-near-zero", out compiled);
                }
                current = current.parent;
            }

            Vector3 position = transform.position;
            Matrix4x4 matrix = transform.localToWorldMatrix;
            Vector3 c0 = matrix.GetColumn(0);
            Vector3 c1 = matrix.GetColumn(1);
            Vector3 c2 = matrix.GetColumn(2);
            if (!TryValidateTransformBasis(
                    position,
                    c0,
                    c1,
                    c2,
                    out float s0,
                    out float s1,
                    out float s2,
                    out Vector3 u0,
                    out Vector3 u1,
                    out Vector3 u2,
                    out string basisErrorCode,
                    out string basisErrorReason))
            {
                return Fail(basisErrorCode, basisErrorReason, out compiled);
            }

            if (!TryQuantizeVector(u0, out FixVector3 axisX)
                || !TryQuantizeVector(u1, out FixVector3 axisY)
                || !TryQuantizeVector(u2, out FixVector3 axisZ))
            {
                return Fail(InvalidTransformNonFiniteCode, "quantization-range", out compiled);
            }
            if (!IsValidQuantizedBasis(axisX, axisY, axisZ))
                return Fail(InvalidQuantizedObbAxesCode, "quantized-basis-invalid", out compiled);

            Vector3 halfExtentsFloat = scaleSource == CharacterTestWorldObjectInstanceScaleSource.Definition
                ? new Vector3(
                    CharacterTestDeterministicUnits.MillimetersToMeters(definitionHalfExtentXMillimeters),
                    CharacterTestDeterministicUnits.MillimetersToMeters(definitionHalfExtentYMillimeters),
                    CharacterTestDeterministicUnits.MillimetersToMeters(definitionHalfExtentZMillimeters))
                : new Vector3(s0 * 0.5f, s1 * 0.5f, s2 * 0.5f);
            if (!IsFinite(halfExtentsFloat))
                return Fail(InvalidTransformNonFiniteCode, "half-extents-non-finite", out compiled);
            if (!TryQuantizeVector(halfExtentsFloat, out FixVector3 halfExtents))
                return Fail(InvalidTransformNonFiniteCode, "quantization-range", out compiled);
            if (halfExtents.X <= Fix64.Zero || halfExtents.Y <= Fix64.Zero || halfExtents.Z <= Fix64.Zero)
                return Fail(InvalidStaticBoxHalfExtentsCode, "half-extents-not-positive", out compiled);

            if (!TryQuantizeVector(position, out FixVector3 quantizedPosition))
                return Fail(InvalidTransformNonFiniteCode, "quantization-range", out compiled);

            compiled = new CompiledBox(
                quantizedPosition,
                halfExtents,
                axisX,
                axisY,
                axisZ,
                !IsIdentityBasis(axisX, axisY, axisZ));
            return true;

            bool Fail(string code, string reason, out CompiledBox failed)
            {
                failed = default;
                string transformPath = CharacterTestWorldObjectSceneSource.ResolveTransformPath(null, transform);
                report.Add(new CombatLevelValidationIssue(
                    CombatLevelValidationSeverity.Error,
                    code,
                    "$.staticColliderSpecs['" + EscapeJsonPathStableId(stableId) + "'].transform",
                    "transformPath=" + transformPath
                    + "; definitionId=" + definitionId
                    + "; definitionStableId=" + (definitionStableId ?? string.Empty)
                    + "; bodyId=" + bodyId
                    + "; colliderId=" + colliderId
                    + "; reason=" + reason));
                return false;
            }
        }

        private static bool IsValidQuantizedBasis(FixVector3 x, FixVector3 y, FixVector3 z)
        {
            if (x.IsZero || y.IsZero || z.IsZero)
                return false;
            if (!Within(x.Dot(x).RawValue, Fix64.One.RawValue, CombatLevelValidator.AxisUnitLengthSquaredToleranceRaw)
                || !Within(y.Dot(y).RawValue, Fix64.One.RawValue, CombatLevelValidator.AxisUnitLengthSquaredToleranceRaw)
                || !Within(z.Dot(z).RawValue, Fix64.One.RawValue, CombatLevelValidator.AxisUnitLengthSquaredToleranceRaw))
            {
                return false;
            }
            if (Math.Abs(x.Dot(y).RawValue) > CombatLevelValidator.AxisOrthogonalityToleranceRaw
                || Math.Abs(x.Dot(z).RawValue) > CombatLevelValidator.AxisOrthogonalityToleranceRaw
                || Math.Abs(y.Dot(z).RawValue) > CombatLevelValidator.AxisOrthogonalityToleranceRaw)
            {
                return false;
            }
            FixVector3 cross = new FixVector3(
                x.Y * y.Z - x.Z * y.Y,
                x.Z * y.X - x.X * y.Z,
                x.X * y.Y - x.Y * y.X);
            return Within(
                cross.Dot(z).RawValue,
                Fix64.One.RawValue,
                CombatLevelValidator.AxisHandednessToleranceRaw);
        }

        internal static bool TryQuantizeVectorForTests(Vector3 value, out FixVector3 quantized) =>
            TryQuantizeVector(value, out quantized);

        internal static string ValidateQuantizedBasisForTests(
            Vector3 axisX,
            Vector3 axisY,
            Vector3 axisZ)
        {
            if (!TryQuantizeVector(axisX, out FixVector3 quantizedX)
                || !TryQuantizeVector(axisY, out FixVector3 quantizedY)
                || !TryQuantizeVector(axisZ, out FixVector3 quantizedZ))
            {
                return InvalidTransformNonFiniteCode;
            }
            return IsValidQuantizedBasis(quantizedX, quantizedY, quantizedZ)
                ? string.Empty
                : InvalidQuantizedObbAxesCode;
        }

        private static bool TryQuantizeVector(Vector3 value, out FixVector3 quantized)
        {
            quantized = FixVector3.Zero;
            if (!TryQuantizeComponent(value.x, out Fix64 x)
                || !TryQuantizeComponent(value.y, out Fix64 y)
                || !TryQuantizeComponent(value.z, out Fix64 z))
            {
                return false;
            }
            quantized = new FixVector3(x, y, z);
            return true;
        }

        private static bool TryQuantizeComponent(float value, out Fix64 quantized)
        {
            quantized = Fix64.Zero;
            double scaled = (double)value * Fix64.Scale;
            if (double.IsNaN(scaled) || double.IsInfinity(scaled))
                return false;
            double rounded = Math.Round(scaled);
            const double LongMinInclusive = -9223372036854775808d;
            const double LongMaxExclusive = 9223372036854775808d;
            if (double.IsNaN(rounded)
                || double.IsInfinity(rounded)
                || rounded < LongMinInclusive
                || rounded >= LongMaxExclusive)
            {
                return false;
            }
            quantized = Fix64.FromRaw(checked((long)rounded));
            return true;
        }

        internal static string ValidateTransformBasisForTests(
            Vector3 position,
            Vector3 column0,
            Vector3 column1,
            Vector3 column2)
        {
            TryValidateTransformBasis(
                position,
                column0,
                column1,
                column2,
                out _,
                out _,
                out _,
                out _,
                out _,
                out _,
                out string errorCode,
                out _);
            return errorCode ?? string.Empty;
        }

        private static bool TryValidateTransformBasis(
            Vector3 position,
            Vector3 c0,
            Vector3 c1,
            Vector3 c2,
            out float s0,
            out float s1,
            out float s2,
            out Vector3 u0,
            out Vector3 u1,
            out Vector3 u2,
            out string errorCode,
            out string errorReason)
        {
            s0 = s1 = s2 = 0f;
            u0 = u1 = u2 = Vector3.zero;
            errorCode = string.Empty;
            errorReason = string.Empty;
            if (!IsFinite(position) || !IsFinite(c0) || !IsFinite(c1) || !IsFinite(c2))
                return SetBasisError(InvalidTransformNonFiniteCode, "position-or-matrix-non-finite", out errorCode, out errorReason);

            s0 = c0.magnitude;
            s1 = c1.magnitude;
            s2 = c2.magnitude;
            if (!IsFinite(s0) || !IsFinite(s1) || !IsFinite(s2))
                return SetBasisError(InvalidTransformNonFiniteCode, "basis-length-non-finite", out errorCode, out errorReason);
            if (s0 < TransformEpsilon || s1 < TransformEpsilon || s2 < TransformEpsilon)
                return SetBasisError(InvalidTransformScaleCode, "basis-column-near-zero", out errorCode, out errorReason);

            float determinant = Vector3.Dot(Vector3.Cross(c0, c1), c2);
            if (!IsFinite(determinant))
                return SetBasisError(InvalidTransformNonFiniteCode, "determinant-non-finite", out errorCode, out errorReason);
            if (determinant <= 0f)
                return SetBasisError(InvalidTransformReflectionCode, "determinant-non-positive", out errorCode, out errorReason);

            u0 = c0 / s0;
            u1 = c1 / s1;
            u2 = c2 / s2;
            float dot01 = Vector3.Dot(u0, u1);
            float dot02 = Vector3.Dot(u0, u2);
            float dot12 = Vector3.Dot(u1, u2);
            if (!IsFinite(u0) || !IsFinite(u1) || !IsFinite(u2)
                || !IsFinite(dot01) || !IsFinite(dot02) || !IsFinite(dot12))
            {
                return SetBasisError(InvalidTransformNonFiniteCode, "normalized-basis-non-finite", out errorCode, out errorReason);
            }
            if (Mathf.Abs(dot01) > TransformEpsilon
                || Mathf.Abs(dot02) > TransformEpsilon
                || Mathf.Abs(dot12) > TransformEpsilon)
            {
                return SetBasisError(InvalidTransformShearCode, "basis-not-orthogonal", out errorCode, out errorReason);
            }
            return true;
        }

        private static bool SetBasisError(
            string code,
            string reason,
            out string errorCode,
            out string errorReason)
        {
            errorCode = code;
            errorReason = reason;
            return false;
        }

        private static bool IsIdentityBasis(FixVector3 x, FixVector3 y, FixVector3 z)
        {
            FixVector3 worldX = new FixVector3(Fix64.One, Fix64.Zero, Fix64.Zero);
            FixVector3 worldY = new FixVector3(Fix64.Zero, Fix64.One, Fix64.Zero);
            FixVector3 worldZ = new FixVector3(Fix64.Zero, Fix64.Zero, Fix64.One);
            return ComponentsWithin(x, worldX) && ComponentsWithin(y, worldY) && ComponentsWithin(z, worldZ);
        }

        private static bool ComponentsWithin(FixVector3 value, FixVector3 expected)
        {
            long tolerance = CombatLevelValidator.IdentityAxisComponentToleranceRaw;
            return Within(value.X.RawValue, expected.X.RawValue, tolerance)
                && Within(value.Y.RawValue, expected.Y.RawValue, tolerance)
                && Within(value.Z.RawValue, expected.Z.RawValue, tolerance);
        }

        private static bool Within(long value, long expected, long tolerance) =>
            value >= expected - tolerance && value <= expected + tolerance;

        private static bool IsFinite(Vector3 value) =>
            IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z);

        private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);

        private static string EscapeJsonPathStableId(string value) =>
            (value ?? string.Empty).Replace("\\", "\\\\").Replace("'", "\\'");

        private static CombatLevelFixedVector3 ToLevelVector(Vector3 value)
        {
            return CombatLevelFixedVector3.FromFixVector3(CharacterTestCombatPhysicsSceneBridge.ToFixVector3(value));
        }

        private static string CreateLevelStableId(Transform root)
        {
            string segment = SanitizeStableSegment(root.name);
            return DefaultLevelStableIdPrefix + (segment.Length == 0 ? "root" : segment);
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

        private static bool IsAxisAligned(Quaternion rotation)
        {
            return Quaternion.Angle(rotation, Quaternion.identity) <= 0.01f;
        }

        private readonly struct CompiledBox
        {
            public CompiledBox(
                FixVector3 position,
                FixVector3 halfExtents,
                FixVector3 axisX,
                FixVector3 axisY,
                FixVector3 axisZ,
                bool isObb)
            {
                Position = position;
                HalfExtents = halfExtents;
                AxisX = axisX;
                AxisY = axisY;
                AxisZ = axisZ;
                IsObb = isObb;
            }

            public FixVector3 Position { get; }
            public FixVector3 HalfExtents { get; }
            public FixVector3 AxisX { get; }
            public FixVector3 AxisY { get; }
            public FixVector3 AxisZ { get; }
            public bool IsObb { get; }
        }

        private sealed class AabbEntry
        {
            public AabbEntry(
                string stableId,
                bool hasExplicitBodyId,
                int bodyId,
                int colliderId,
                int layer,
                FixVector3 position,
                FixVector3 halfExtents,
                FixVector3 axisX,
                FixVector3 axisY,
                FixVector3 axisZ,
                bool isObb,
                CombatSurfaceMaterial surfaceMaterial)
            {
                StableId = stableId ?? string.Empty;
                HasExplicitBodyId = hasExplicitBodyId;
                BodyId = bodyId;
                ColliderId = colliderId;
                Layer = layer;
                Position = position;
                HalfExtents = halfExtents;
                AxisX = axisX;
                AxisY = axisY;
                AxisZ = axisZ;
                IsObb = isObb;
                SurfaceMaterial = surfaceMaterial;
            }

            public string StableId { get; }
            public bool HasExplicitBodyId { get; }
            public int BodyId { get; set; }
            public int ColliderId { get; }
            public int Layer { get; }
            public FixVector3 Position { get; }
            public FixVector3 HalfExtents { get; }
            public FixVector3 AxisX { get; }
            public FixVector3 AxisY { get; }
            public FixVector3 AxisZ { get; }
            public bool IsObb { get; }
            public CombatSurfaceMaterial SurfaceMaterial { get; }
        }

        private sealed class AabbEntryComparer : IComparer<AabbEntry>
        {
            public static readonly AabbEntryComparer Instance = new AabbEntryComparer();

            private AabbEntryComparer()
            {
            }

            public int Compare(AabbEntry left, AabbEntry right)
            {
                return string.CompareOrdinal(left.StableId, right.StableId);
            }
        }
    }
}
