using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using MxFramework.CharacterRuntimeSpawn;
using MxFramework.Core.Math;
using UnityEngine;

namespace MxFramework.Game.CharacterTest
{
    [Flags]
    internal enum CharacterTestLargeCharacterPresenceDebugLayer
    {
        None = 0,
        HardRoot = 1 << 0,
        Presence = 1 << 1,
        Hurtbox = 1 << 2,
        ActiveTrace = 1 << 3,
        All = HardRoot | Presence | Hurtbox | ActiveTrace
    }

    /// <summary>Read-only CharacterTest projection of the three large-character geometry layers.</summary>
    /// <remarks>DebugProbe. Does not participate in simulation, contact, damage, or networking.</remarks>
    internal sealed class CharacterTestLargeCharacterPresenceDebugSnapshot
    {
        internal CharacterTestLargeCharacterPresenceDebugSnapshot(
            int entityId,
            string characterStableId,
            CharacterImportedGeometryBinding binding,
            int hardRootRadiusMillimeters,
            int hardRootHeightMillimeters,
            int hardRootCenterYMillimeters,
            int animatedHurtboxCount,
            int animatedHurtboxRevision)
        {
            EntityId = entityId;
            CharacterStableId = characterStableId ?? string.Empty;
            Binding = binding ?? CharacterImportedGeometryBinding.Empty;
            HardRootRadiusMillimeters = hardRootRadiusMillimeters;
            HardRootHeightMillimeters = hardRootHeightMillimeters;
            HardRootCenterYMillimeters = hardRootCenterYMillimeters;
            AnimatedHurtboxCount = Math.Max(0, animatedHurtboxCount);
            AnimatedHurtboxRevision = Math.Max(0, animatedHurtboxRevision);
        }

        internal int EntityId { get; }
        internal string CharacterStableId { get; }
        internal CharacterImportedGeometryBinding Binding { get; }
        internal int HardRootRadiusMillimeters { get; }
        internal int HardRootHeightMillimeters { get; }
        internal int HardRootCenterYMillimeters { get; }
        internal int AnimatedHurtboxCount { get; }
        internal int AnimatedHurtboxRevision { get; }
        internal bool GameplayAffecting
        {
            get
            {
                if (Binding.BodyPresenceMode != CharacterBodyPresenceMode.Authored
                    || Binding.BodyPresenceVolumes == null)
                {
                    return false;
                }

                for (int i = 0; i < Binding.BodyPresenceVolumes.Length; i++)
                {
                    CharacterBodyPresenceRuntimeBinding volume = Binding.BodyPresenceVolumes[i];
                    if (volume != null && volume.Enabled)
                        return true;
                }

                return false;
            }
        }
        internal bool HasUnconsumedAuthorCenterXZ =>
            !Binding.RuntimeConsumesAuthorCapsuleCenterXZ
            && (Math.Abs(Binding.AuthorCapsuleCenterX) > 0.0001f
                || Math.Abs(Binding.AuthorCapsuleCenterZ) > 0.0001f);
    }

    internal static class CharacterTestLargeCharacterPresenceDebug
    {
        internal const string CenterDiagnosticCode =
            "CHARPRES013_ROOT_CAPSULE_CENTER_XZ_NOT_CONSUMED";
        internal const string ExcludedGeometry =
            "wing membranes, tail tip, head tip, attack Trace";
        internal const string HardRootLabel = "Hard Root";
        internal const string BodyPresenceLabel = "Body Presence";
        internal const string AnimatedHurtboxLabel = "Animated Hurtbox";
        internal const string ActiveTraceLabel = "Active Trace";

        internal static CharacterTestLargeCharacterPresenceDebugLayer EnabledLayers { get; set; } =
            CharacterTestLargeCharacterPresenceDebugLayer.All;

        private static readonly Color HardRootColor = new Color(1f, 0.72f, 0.08f, 0.95f);
        private static readonly Color BodyPresenceColor = new Color(0.05f, 0.9f, 1f, 0.95f);
        private static readonly Color AnimatedHurtboxColor = new Color(1f, 0.18f, 0.72f, 0.8f);

        /// <summary>Captures imported binding facts and current read-only runtime revisions.</summary>
        /// <remarks>LowFreqAlloc DebugProbe.</remarks>
        internal static bool TryCapture(
            CharacterTestCharacterRuntimeModule runtime,
            CharacterTestCharacterEntity entity,
            out CharacterTestLargeCharacterPresenceDebugSnapshot snapshot)
        {
            snapshot = null;
            if (runtime == null || entity == null)
                return false;

            if (!runtime.TryGetRuntimeGeometryBinding(entity.Id.Value, out CharacterImportedGeometryBinding binding)
                || binding == null)
            {
                binding = CharacterTestGeneratedPackageSource.LoadPackageForEntity(entity)?.Geometry;
            }

            if (binding == null
                || binding.BodyPresenceMode != CharacterBodyPresenceMode.Authored)
            {
                return false;
            }

            int hurtboxCount = 0;
            if (runtime.TryGetDamageGeometrySnapshot(
                    entity.Id.Value,
                    out CharacterTestDamageGeometrySnapshot damageGeometry)
                && damageGeometry != null
                && damageGeometry.IsValid)
            {
                hurtboxCount = damageGeometry.ColliderCount;
            }

            CharacterTestColliderPostureComponent hardRoot = entity.ColliderPosture;
            snapshot = Project(
                entity.Id.Value,
                entity.Identity.CharacterStableId,
                binding,
                hardRoot.RadiusMillimeters,
                hardRoot.HeightMillimeters,
                hardRoot.CenterYMillimeters,
                hurtboxCount,
                runtime.GetHurtboxRevision(entity.Id.Value));
            return true;
        }

        /// <summary>Projects immutable values for focused tests and developer UI.</summary>
        /// <remarks>LowFreqAlloc DebugProbe.</remarks>
        internal static CharacterTestLargeCharacterPresenceDebugSnapshot Project(
            int entityId,
            string characterStableId,
            CharacterImportedGeometryBinding binding,
            int hardRootRadiusMillimeters,
            int hardRootHeightMillimeters,
            int hardRootCenterYMillimeters,
            int animatedHurtboxCount,
            int animatedHurtboxRevision)
        {
            return new CharacterTestLargeCharacterPresenceDebugSnapshot(
                entityId,
                characterStableId,
                binding,
                hardRootRadiusMillimeters,
                hardRootHeightMillimeters,
                hardRootCenterYMillimeters,
                animatedHurtboxCount,
                animatedHurtboxRevision);
        }

        /// <summary>Builds the developer-card projection without mutating gameplay state.</summary>
        /// <remarks>LowFreqAlloc DebugProbe.</remarks>
        internal static CharacterTestDeveloperDebugCard CreateCard(
            CharacterTestLargeCharacterPresenceDebugSnapshot snapshot)
        {
            if (snapshot == null)
                return null;

            CharacterImportedGeometryBinding binding = snapshot.Binding;
            var lines = new List<CharacterTestDeveloperDebugLine>
            {
                CharacterTestDeveloperDebugLine.StatusChip(
                    "Binding",
                    binding.Format + "/" + binding.SchemaVersion
                        + " " + binding.BodyPresenceMode),
                CharacterTestDeveloperDebugLine.StatusChip(
                    HardRootLabel,
                    "r=" + Meters(snapshot.HardRootRadiusMillimeters)
                        + " h=" + Meters(snapshot.HardRootHeightMillimeters)
                        + " centerY=" + Meters(snapshot.HardRootCenterYMillimeters)),
                CharacterTestDeveloperDebugLine.StatusChip(
                    BodyPresenceLabel,
                    binding.BodyPresenceVolumes.Length
                        + " volumes / rev " + binding.BodyPresenceRevision,
                    binding.BodyPresenceVolumes.Length == 0),
                CharacterTestDeveloperDebugLine.StatusChip(
                    AnimatedHurtboxLabel,
                    snapshot.AnimatedHurtboxCount
                        + " shapes / rev " + snapshot.AnimatedHurtboxRevision,
                    snapshot.AnimatedHurtboxCount == 0),
                CharacterTestDeveloperDebugLine.Row(
                    "Layer Colors",
                    "Hard Root=amber | Body Presence=cyan | Animated Hurtbox=magenta"),
                CharacterTestDeveloperDebugLine.Row(
                    "Layer Toggles",
                    FormatLayerToggles(EnabledLayers)),
                CharacterTestDeveloperDebugLine.Row(
                    "Presence IDs",
                    JoinVolumeIds(binding.BodyPresenceVolumes)),
                CharacterTestDeveloperDebugLine.Row(
                    "Presence Hash",
                    binding.BodyPresenceContentHash),
                CharacterTestDeveloperDebugLine.Row(
                    "Center X/Z",
                    "source=(" + Float(binding.AuthorCapsuleCenterX)
                        + ", " + Float(binding.AuthorCapsuleCenterZ)
                        + ") runtime-consumed=(" + Float(binding.RuntimeConsumedCapsuleCenterX)
                        + ", " + Float(binding.RuntimeConsumedCapsuleCenterZ) + ")",
                    snapshot.HasUnconsumedAuthorCenterXZ),
                CharacterTestDeveloperDebugLine.Row(
                    "Center Diagnostic",
                    snapshot.HasUnconsumedAuthorCenterXZ
                        ? CenterDiagnosticCode + " (source X/Z not consumed)"
                        : "none",
                    snapshot.HasUnconsumedAuthorCenterXZ),
                CharacterTestDeveloperDebugLine.Row("Excluded", ExcludedGeometry),
                CharacterTestDeveloperDebugLine.Row(
                    ActiveTraceLabel,
                    "existing CharacterTest attack Trace; separate from Presence"),
                CharacterTestDeveloperDebugLine.StatusChip(
                    "Gameplay",
                    snapshot.GameplayAffecting ? "affecting" : "debug-only / unchanged",
                    false),
                CharacterTestDeveloperDebugLine.Row(
                    "Projection",
                    "read-only diagnostics; no authority writes")
            };

            return CharacterTestDeveloperDebugCard.Entity(
                "Presence Layers · Entity #" + snapshot.EntityId,
                lines);
        }

        /// <summary>Projects current fixed-buffer counters and constraints into low-frequency strings.</summary>
        /// <remarks>LowFreqAlloc DebugProbe. The authority buffers are read directly and never copied.</remarks>
        internal static CharacterTestDeveloperDebugCard CreateContactDiagnosticsCard(
            CharacterTestCharacterRuntimeModule runtime)
        {
            if (runtime == null)
                return null;

            CharacterTestBodyPresenceContactDiagnostics contact =
                runtime.CaptureBodyPresenceContactDiagnostics();
            CharacterTestBodyPresenceSyncDiagnostics sync =
                runtime.CaptureBodyPresenceSyncDiagnostics();
            List<CharacterTestDeveloperDebugLine> lines = CreateContactDiagnosticsLines(contact, sync);

            for (int i = 0; i < contact.ConstraintCount; i++)
            {
                if (!runtime.TryGetBodyPresenceConstraintDiagnostic(
                        i,
                        out CharacterTestBodyPresenceConstraintDiagnostic constraint))
                {
                    lines.Add(CharacterTestDeveloperDebugLine.Row(
                        "Constraint[" + i.ToString(CultureInfo.InvariantCulture) + "]",
                        "unavailable",
                        true));
                    break;
                }

                lines.Add(CreateConstraintLine(i, in constraint));
            }

            return CharacterTestDeveloperDebugCard.Entity(
                "Presence Contact · Fixed Buffers",
                lines);
        }

        /// <summary>Task-specific summary projection used by focused debug tests.</summary>
        /// <remarks>LowFreqAlloc DebugProbe.</remarks>
        internal static CharacterTestDeveloperDebugCard CreateContactDiagnosticsCard(
            CharacterTestBodyPresenceContactDiagnostics contact,
            CharacterTestBodyPresenceSyncDiagnostics sync)
        {
            return CharacterTestDeveloperDebugCard.Entity(
                "Presence Contact · Fixed Buffers",
                CreateContactDiagnosticsLines(contact, sync));
        }

        private static List<CharacterTestDeveloperDebugLine> CreateContactDiagnosticsLines(
            CharacterTestBodyPresenceContactDiagnostics contact,
            CharacterTestBodyPresenceSyncDiagnostics sync)
        {
            return new List<CharacterTestDeveloperDebugLine>
            {
                CharacterTestDeveloperDebugLine.Row(
                    "Layer Toggles",
                    FormatLayerToggles(EnabledLayers)),
                CharacterTestDeveloperDebugLine.StatusChip(
                    "Presence Sync",
                    (sync.LastSyncSucceeded ? "Success" : "Unavailable")
                        + " projections=" + Count(sync.ProjectionCount, sync.ProjectionCapacity)
                        + " leaves=" + Count(sync.ProjectedLeafCount, sync.ProjectedLeafCapacity)
                        + " registered=" + sync.RegisteredEntityCount.ToString(CultureInfo.InvariantCulture),
                    !sync.LastSyncSucceeded),
                CharacterTestDeveloperDebugLine.StatusChip(
                    "Contact Build",
                    ((int)contact.Status).ToString(CultureInfo.InvariantCulture)
                        + "/" + contact.Status
                        + " raw=" + Count(contact.RawCandidateCount, contact.RawCandidateCapacity)
                        + " pairs=" + Count(contact.PairCount, contact.PairCapacity)
                        + " constraints=" + Count(contact.ConstraintCount, contact.ConstraintCapacity),
                    contact.HasFailure),
                CharacterTestDeveloperDebugLine.Row(
                    "Pressure",
                    "iterations=" + contact.PressureIterationCount.ToString(CultureInfo.InvariantCulture)
                        + " clamps=" + contact.SurfaceClampCount.ToString(CultureInfo.InvariantCulture)),
                CharacterTestDeveloperDebugLine.Row(
                    "Failure",
                    contact.HasFailure
                        ? ((int)contact.Status).ToString(CultureInfo.InvariantCulture)
                            + "/" + contact.Status
                        : "none",
                    contact.HasFailure)
            };
        }

        /// <summary>Draws the three layers through the existing CharacterTest physics-debug seam.</summary>
        /// <remarks>DebugProbe. Editor/Game View gizmos only.</remarks>
        internal static void DrawGizmos(
            CharacterTestCharacterRuntimeModule runtime,
            IReadOnlyList<CharacterTestCharacterEntity> entities)
        {
            DrawGizmos(runtime, entities, EnabledLayers);
        }

        /// <summary>Draws independently maskable large-character geometry layers.</summary>
        /// <remarks>DebugProbe. Editor/Game View gizmos only.</remarks>
        internal static void DrawGizmos(
            CharacterTestCharacterRuntimeModule runtime,
            IReadOnlyList<CharacterTestCharacterEntity> entities,
            CharacterTestLargeCharacterPresenceDebugLayer layers)
        {
            if (runtime == null || entities == null)
                return;

            Color previousColor = Gizmos.color;
            Matrix4x4 previousMatrix = Gizmos.matrix;
            try
            {
                for (int i = 0; i < entities.Count; i++)
                {
                    CharacterTestCharacterEntity entity = entities[i];
                    if (!TryCapture(runtime, entity, out CharacterTestLargeCharacterPresenceDebugSnapshot snapshot))
                        continue;

                    if (IsLayerEnabled(layers, CharacterTestLargeCharacterPresenceDebugLayer.HardRoot))
                        DrawHardRoot(entity, snapshot);
                    if (IsLayerEnabled(layers, CharacterTestLargeCharacterPresenceDebugLayer.Presence))
                        DrawPresence(entity, snapshot.Binding.BodyPresenceVolumes);
                    if (IsLayerEnabled(layers, CharacterTestLargeCharacterPresenceDebugLayer.Hurtbox))
                        DrawAnimatedHurtboxBounds(runtime, entity.Id.Value);
                }

                if (IsLayerEnabled(layers, CharacterTestLargeCharacterPresenceDebugLayer.Presence))
                    DrawPresenceContactNormals(runtime);
            }
            finally
            {
                Gizmos.color = previousColor;
                Gizmos.matrix = previousMatrix;
            }
        }

        internal static bool IsLayerEnabled(
            CharacterTestLargeCharacterPresenceDebugLayer layers,
            CharacterTestLargeCharacterPresenceDebugLayer layer)
        {
            return (layers & layer) == layer;
        }

        private static void DrawPresenceContactNormals(CharacterTestCharacterRuntimeModule runtime)
        {
            CharacterTestBodyPresenceContactDiagnostics contact =
                runtime.CaptureBodyPresenceContactDiagnostics();
            if (contact.Status != CharacterTestBodyPresenceContactBuildStatus.Success)
                return;

            Gizmos.color = BodyPresenceColor;
            Gizmos.matrix = Matrix4x4.identity;
            for (int i = 0; i < contact.ConstraintCount; i++)
            {
                if (!runtime.TryGetBodyPresenceConstraintDiagnostic(
                        i,
                        out CharacterTestBodyPresenceConstraintDiagnostic constraint))
                {
                    return;
                }

                Vector3 point = new Vector3(
                    Fix64RawToFloat(constraint.ContactPointXFix64Raw),
                    Fix64RawToFloat(constraint.ContactPointYFix64Raw),
                    Fix64RawToFloat(constraint.ContactPointZFix64Raw));
                Vector3 normal = new Vector3(
                    Fix64RawToFloat(constraint.NormalXFix64Raw),
                    Fix64RawToFloat(constraint.NormalYFix64Raw),
                    Fix64RawToFloat(constraint.NormalZFix64Raw));
                if (normal.sqrMagnitude <= 0.000001f)
                    continue;
                normal.Normalize();
                float penetration = Math.Max(
                    0f,
                    Fix64RawToFloat(constraint.PenetrationFix64Raw));
                Gizmos.DrawLine(point, point + normal * Mathf.Max(0.1f, penetration));
            }
        }

        private static CharacterTestDeveloperDebugLine CreateConstraintLine(
            int index,
            in CharacterTestBodyPresenceConstraintDiagnostic constraint)
        {
            string value = "owner=" + constraint.PresenceEntityId.ToString(CultureInfo.InvariantCulture)
                + " target=" + constraint.TargetEntityId.ToString(CultureInfo.InvariantCulture)
                + " volume=" + constraint.VolumeOrdinal.ToString(CultureInfo.InvariantCulture)
                + " normal=(" + Fix64Raw(constraint.NormalXFix64Raw)
                + "," + Fix64Raw(constraint.NormalYFix64Raw)
                + "," + Fix64Raw(constraint.NormalZFix64Raw) + ")"
                + " penetration=" + Fix64Raw(constraint.PenetrationFix64Raw) + "m"
                + " pressure=" + constraint.PressureScaleMilli.ToString(CultureInfo.InvariantCulture)
                + " bias=" + constraint.PenetrationBiasMillimetersPerSecond.ToString(CultureInfo.InvariantCulture)
                + "mm/s"
                + " surface=(" + constraint.SurfaceVelocityXMillimetersPerSecond.ToString(CultureInfo.InvariantCulture)
                + "," + constraint.SurfaceVelocityZMillimetersPerSecond.ToString(CultureInfo.InvariantCulture)
                + ")mm/s"
                + " clampedMax=" + constraint.MaximumComposedSpeedMillimetersPerSecond.ToString(CultureInfo.InvariantCulture)
                + " status=" + constraint.SeparationStatus;
            return CharacterTestDeveloperDebugLine.Row(
                "Constraint[" + index.ToString(CultureInfo.InvariantCulture) + "]",
                value);
        }

        private static string FormatLayerToggles(
            CharacterTestLargeCharacterPresenceDebugLayer layers)
        {
            return HardRootLabel + "=" + Toggle(layers, CharacterTestLargeCharacterPresenceDebugLayer.HardRoot)
                + " | " + BodyPresenceLabel + "=" + Toggle(layers, CharacterTestLargeCharacterPresenceDebugLayer.Presence)
                + " | " + AnimatedHurtboxLabel + "=" + Toggle(layers, CharacterTestLargeCharacterPresenceDebugLayer.Hurtbox)
                + " | " + ActiveTraceLabel + "=" + Toggle(layers, CharacterTestLargeCharacterPresenceDebugLayer.ActiveTrace);
        }

        private static string Toggle(
            CharacterTestLargeCharacterPresenceDebugLayer layers,
            CharacterTestLargeCharacterPresenceDebugLayer layer)
        {
            return IsLayerEnabled(layers, layer) ? "on" : "off";
        }

        private static string Count(int value, int capacity)
        {
            return value.ToString(CultureInfo.InvariantCulture)
                + "/" + capacity.ToString(CultureInfo.InvariantCulture);
        }

        private static string Fix64Raw(long raw)
        {
            return (raw / (double)Fix64.Scale).ToString("0.######", CultureInfo.InvariantCulture);
        }

        private static float Fix64RawToFloat(long raw)
        {
            return raw / (float)Fix64.Scale;
        }

        private static void DrawHardRoot(
            CharacterTestCharacterEntity entity,
            CharacterTestLargeCharacterPresenceDebugSnapshot snapshot)
        {
            Gizmos.color = HardRootColor;
            Gizmos.matrix = Matrix4x4.identity;
            Vector3 root = ToVector3(entity.Transform);
            Vector3 center = root + Vector3.up * MetersValue(snapshot.HardRootCenterYMillimeters);
            DrawWireCapsule(
                center,
                Quaternion.identity,
                MetersValue(snapshot.HardRootRadiusMillimeters),
                MetersValue(snapshot.HardRootHeightMillimeters));
        }

        private static void DrawPresence(
            CharacterTestCharacterEntity entity,
            IReadOnlyList<CharacterBodyPresenceRuntimeBinding> volumes)
        {
            if (volumes == null)
                return;

            Vector3 root = ToVector3(entity.Transform);
            Quaternion rootRotation = Quaternion.Euler(0f, entity.Transform.YawDegrees, 0f);
            Gizmos.color = BodyPresenceColor;
            for (int i = 0; i < volumes.Count; i++)
            {
                CharacterBodyPresenceRuntimeBinding volume = volumes[i];
                if (volume == null || !volume.Enabled)
                    continue;

                Vector3 localPosition = ToVector3(volume.LocalPose.Position);
                Quaternion localRotation = ToQuaternion(volume.LocalPose.Rotation);
                Vector3 center = root + rootRotation * localPosition;
                Quaternion rotation = rootRotation * localRotation;
                float skin = Math.Max(0, volume.SkinMillimeters) / 1000f;
                switch (volume.Shape)
                {
                    case CharacterBodyPresenceShape.Sphere:
                        Gizmos.matrix = Matrix4x4.TRS(center, rotation, Vector3.one);
                        Gizmos.DrawWireSphere(Vector3.zero, volume.Radius + skin);
                        break;
                    case CharacterBodyPresenceShape.Capsule:
                        DrawWireCapsule(
                            center,
                            rotation,
                            volume.Radius + skin,
                            volume.Height + skin * 2f);
                        break;
                    case CharacterBodyPresenceShape.Box:
                        Gizmos.matrix = Matrix4x4.TRS(center, rotation, Vector3.one);
                        Gizmos.DrawWireCube(
                            Vector3.zero,
                            ToVector3(volume.Size) + Vector3.one * (skin * 2f));
                        break;
                }
            }
        }

        private static void DrawAnimatedHurtboxBounds(
            CharacterTestCharacterRuntimeModule runtime,
            int entityId)
        {
            if (!runtime.TryGetDamageGeometrySnapshot(
                    entityId,
                    out CharacterTestDamageGeometrySnapshot snapshot)
                || snapshot == null
                || !snapshot.IsValid)
            {
                return;
            }

            Gizmos.color = AnimatedHurtboxColor;
            Gizmos.matrix = Matrix4x4.identity;
            for (int i = 0; i < snapshot.ColliderCount; i++)
            {
                CharacterTestDamageGeometryColliderMetadata collider = snapshot.GetCollider(i);
                Vector3 min = ToVector3(collider.WorldMin);
                Vector3 max = ToVector3(collider.WorldMax);
                Gizmos.DrawWireCube((min + max) * 0.5f, max - min);
            }
        }

        private static void DrawWireCapsule(
            Vector3 center,
            Quaternion rotation,
            float radius,
            float height)
        {
            radius = Mathf.Max(0.001f, radius);
            height = Mathf.Max(radius * 2f, height);
            float halfSegment = Mathf.Max(0f, height * 0.5f - radius);
            Gizmos.matrix = Matrix4x4.TRS(center, rotation, Vector3.one);
            Vector3 top = Vector3.up * halfSegment;
            Vector3 bottom = Vector3.down * halfSegment;
            Gizmos.DrawWireSphere(top, radius);
            Gizmos.DrawWireSphere(bottom, radius);
            Gizmos.DrawLine(top + Vector3.right * radius, bottom + Vector3.right * radius);
            Gizmos.DrawLine(top - Vector3.right * radius, bottom - Vector3.right * radius);
            Gizmos.DrawLine(top + Vector3.forward * radius, bottom + Vector3.forward * radius);
            Gizmos.DrawLine(top - Vector3.forward * radius, bottom - Vector3.forward * radius);
        }

        private static string JoinVolumeIds(IReadOnlyList<CharacterBodyPresenceRuntimeBinding> volumes)
        {
            if (volumes == null || volumes.Count == 0)
                return "none";

            var builder = new StringBuilder();
            for (int i = 0; i < volumes.Count; i++)
            {
                if (i > 0)
                    builder.Append(", ");
                builder.Append(volumes[i]?.VolumeId ?? "missing");
            }
            return builder.ToString();
        }

        private static string Meters(int millimeters) =>
            MetersValue(millimeters).ToString("F3", CultureInfo.InvariantCulture) + "m";

        private static float MetersValue(int millimeters) =>
            CharacterTestDeterministicUnits.MillimetersToMeters(millimeters);

        private static string Float(float value) =>
            value.ToString("0.###", CultureInfo.InvariantCulture);

        private static Vector3 ToVector3(CharacterTestCharacterTransformComponent value) =>
            new Vector3(value.X, value.Y, value.Z);

        private static Vector3 ToVector3(CharacterRuntimeVector3 value) =>
            new Vector3(value.X, value.Y, value.Z);

        private static Vector3 ToVector3(FixVector3 value) =>
            new Vector3(ToFloat(value.X), ToFloat(value.Y), ToFloat(value.Z));

        private static Quaternion ToQuaternion(CharacterRuntimeQuaternion value) =>
            new Quaternion(value.X, value.Y, value.Z, value.W);

        private static float ToFloat(Fix64 value) =>
            value.RawValue / (float)Fix64.Scale;
    }
}
