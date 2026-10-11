using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace MxFramework.CharacterCloth.Magica.Unity.Editor
{
    public enum MagicaCharacterClothDiagnosticSeverity { Warning, Error }

    public sealed class MagicaCharacterClothDiagnostic
    {
        public string Code { get; internal set; } = string.Empty;
        public MagicaCharacterClothDiagnosticSeverity Severity { get; internal set; }
        public string ClothId { get; internal set; } = string.Empty;
        public string ColliderId { get; internal set; } = string.Empty;
        public string FieldPath { get; internal set; } = string.Empty;
        public string Message { get; internal set; } = string.Empty;
    }

    public sealed class MagicaCharacterClothValidationResult
    {
        internal readonly List<MagicaCharacterClothDiagnostic> diagnostics = new List<MagicaCharacterClothDiagnostic>();
        public IReadOnlyList<MagicaCharacterClothDiagnostic> Diagnostics => diagnostics;
        public bool HasErrors => diagnostics.Exists(x => x.Severity == MagicaCharacterClothDiagnosticSeverity.Error);
        public int ErrorCount => diagnostics.FindAll(x => x.Severity == MagicaCharacterClothDiagnosticSeverity.Error).Count;
        public int WarningCount => diagnostics.Count - ErrorCount;
        public string Summary => $"Character Cloth validation: {ErrorCount} error(s), {WarningCount} warning(s).";
    }

    public static class MagicaCharacterClothAuthoringValidator
    {
        public static MagicaCharacterClothValidationResult Validate(
            MagicaCharacterClothAuthoringDocument document,
            Transform bodyModel)
        {
            var result = new MagicaCharacterClothValidationResult();
            if (document == null)
            {
                Add(result, "MAGCLOTH001", MagicaCharacterClothDiagnosticSeverity.Error, "", "", "", "Document is missing.");
                return result;
            }
            bool schema10 = string.Equals(document.schemaVersion, "1.0", StringComparison.Ordinal);
            bool schema11 = string.Equals(document.schemaVersion, "1.1", StringComparison.Ordinal);
            if ((!schema10 && !schema11) ||
                !string.Equals(document.backend, "MagicaCloth2", StringComparison.Ordinal))
                Add(result, "MAGCLOTH001", MagicaCharacterClothDiagnosticSeverity.Error, "", "", "schemaVersion/backend", "Schema must be 1.0 or 1.1 and backend must be MagicaCloth2.");

            MagicaCharacterClothAuthoringEntry[] cloths = document.cloths ?? Array.Empty<MagicaCharacterClothAuthoringEntry>();
            var clothIds = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < cloths.Length; i++)
            {
                MagicaCharacterClothAuthoringEntry cloth = cloths[i];
                string clothPath = $"cloths[{i}]";
                if (cloth == null || string.IsNullOrWhiteSpace(cloth.id) || !clothIds.Add(cloth.id))
                {
                    Add(result, "MAGCLOTH002", MagicaCharacterClothDiagnosticSeverity.Error, cloth?.id, "", clothPath + ".id", "Cloth id is required and must be unique.");
                    continue;
                }
                if (!string.Equals(cloth.clothType, "BoneCloth", StringComparison.Ordinal))
                    Add(result, "MAGCLOTH003", MagicaCharacterClothDiagnosticSeverity.Error, cloth.id, "", clothPath + ".clothType", "Only BoneCloth is supported.");
                string[] roots = cloth.rootBonePaths ?? Array.Empty<string>();
                if (roots.Length == 0)
                    Add(result, "MAGCLOTH004", MagicaCharacterClothDiagnosticSeverity.Error, cloth.id, "", clothPath + ".rootBonePaths", "At least one root bone is required.");
                for (int rootIndex = 0; rootIndex < roots.Length; rootIndex++)
                    ValidateBone(result, bodyModel, roots[rootIndex], cloth.id, "", clothPath + $".rootBonePaths[{rootIndex}]");
                ValidatePreset(result, cloth, clothPath);
                if (cloth.parameterOverrides != null)
                {
                    if (!schema11)
                        Add(result, "MAGCLOTH016", MagicaCharacterClothDiagnosticSeverity.Error, cloth.id, "", clothPath + ".parameterOverrides", "parameterOverrides requires schemaVersion 1.1.");
                    ValidateParameterOverrides(result, cloth, clothPath + ".parameterOverrides");
                }

                MagicaCharacterClothAuthoringCollider[] colliders = cloth.colliders ?? Array.Empty<MagicaCharacterClothAuthoringCollider>();
                var colliderIds = new HashSet<string>(StringComparer.Ordinal);
                for (int colliderIndex = 0; colliderIndex < colliders.Length; colliderIndex++)
                {
                    MagicaCharacterClothAuthoringCollider collider = colliders[colliderIndex];
                    string path = clothPath + $".colliders[{colliderIndex}]";
                    if (collider == null || string.IsNullOrWhiteSpace(collider.id) || !colliderIds.Add(collider.id))
                    {
                        Add(result, "MAGCLOTH005", MagicaCharacterClothDiagnosticSeverity.Error, cloth.id, collider?.id, path + ".id", "Collider id is required and must be unique within the cloth.");
                        continue;
                    }
                    ValidateBone(result, bodyModel, collider.parentBonePath, cloth.id, collider.id, path + ".parentBonePath");
                    bool sphere = string.Equals(collider.type, "Sphere", StringComparison.Ordinal);
                    bool capsule = string.Equals(collider.type, "Capsule", StringComparison.Ordinal);
                    if (!sphere && !capsule)
                        Add(result, "MAGCLOTH007", MagicaCharacterClothDiagnosticSeverity.Error, cloth.id, collider.id, path + ".type", "Collider type must be Sphere or Capsule.");
                    if (!FinitePositive(collider.radius) || (capsule && (!FinitePositive(collider.length) || (!string.IsNullOrWhiteSpace(collider.direction) && collider.direction != "X" && collider.direction != "Y" && collider.direction != "Z"))))
                        Add(result, capsule && collider.direction != "X" && collider.direction != "Y" && collider.direction != "Z" ? "MAGCLOTH008" : "MAGCLOTH007", MagicaCharacterClothDiagnosticSeverity.Error, cloth.id, collider.id, path, "Collider dimensions/direction are invalid.");
                    if (!Finite(collider.localPosition) || !Finite(collider.localRotationEuler))
                        Add(result, "MAGCLOTH007", MagicaCharacterClothDiagnosticSeverity.Error, cloth.id, collider.id, path, "Collider transform contains a non-finite value.");
                }
                if (colliders.Length == 1 && string.Equals(colliders[0]?.id, "head", StringComparison.Ordinal))
                    Add(result, "MAGCLOTH015", MagicaCharacterClothDiagnosticSeverity.Warning, cloth.id, "", clothPath + ".colliders", "Only a head collider is configured; neck/back penetration is likely.");
            }
            return result;
        }

        internal static Transform FindTransform(Transform bodyModel, string configuredPath, out bool ambiguous)
        {
            ambiguous = false;
            if (bodyModel == null || string.IsNullOrWhiteSpace(configuredPath)) return null;
            string normalized = NormalizePath(configuredPath);
            Transform match = null;
            Transform[] transforms = bodyModel.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < transforms.Length; i++)
            {
                string candidatePath = NormalizePath(AnimationUtility.CalculateTransformPath(transforms[i], bodyModel));
                bool matches = normalized.IndexOf('/') >= 0
                    ? string.Equals(candidatePath, normalized, StringComparison.Ordinal)
                    : string.Equals(transforms[i].name, normalized, StringComparison.Ordinal);
                if (!matches) continue;
                if (match != null) { ambiguous = true; return null; }
                match = transforms[i];
            }
            return match;
        }

        private static void ValidateBone(MagicaCharacterClothValidationResult result, Transform bodyModel, string path, string clothId, string colliderId, string fieldPath)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                Add(result, colliderId.Length > 0 ? "MAGCLOTH006" : "MAGCLOTH004", MagicaCharacterClothDiagnosticSeverity.Error, clothId, colliderId, fieldPath, "Bone path is required.");
                return;
            }
            if (bodyModel == null) return;
            Transform bone = FindTransform(bodyModel, path, out bool ambiguous);
            if (bone == null)
                Add(result, colliderId.Length > 0 ? "MAGCLOTH006" : "MAGCLOTH004", MagicaCharacterClothDiagnosticSeverity.Error, clothId, colliderId, fieldPath, ambiguous ? "Bone path is ambiguous." : "Bone path was not found in BodyModel.");
            else if (!UniformScale(bone.lossyScale))
                Add(result, "MAGCLOTH010", MagicaCharacterClothDiagnosticSeverity.Error, clothId, colliderId, fieldPath, "Bone world scale must be finite, positive, and uniform.");
        }

        private static void ValidatePreset(MagicaCharacterClothValidationResult result, MagicaCharacterClothAuthoringEntry cloth, string fieldPath)
        {
            if (string.IsNullOrWhiteSpace(cloth.presetAssetPath) || string.IsNullOrWhiteSpace(cloth.presetSha256))
            {
                Add(result, "MAGCLOTH009", MagicaCharacterClothDiagnosticSeverity.Error, cloth.id, "", fieldPath + ".presetAssetPath", "Preset path and SHA-256 are required.");
                return;
            }
            string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            string full = Path.GetFullPath(Path.Combine(projectRoot, cloth.presetAssetPath));
            if (!full.StartsWith(projectRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) || !File.Exists(full))
                Add(result, "MAGCLOTH009", MagicaCharacterClothDiagnosticSeverity.Error, cloth.id, "", fieldPath + ".presetAssetPath", "Preset path is missing or outside the project.");
            else if (!string.Equals(MagicaCharacterClothAuthoringRepository.NormalizeHash(cloth.presetSha256), MagicaCharacterClothAuthoringRepository.ComputeFileHash(full), StringComparison.Ordinal))
                Add(result, "MAGCLOTH009", MagicaCharacterClothDiagnosticSeverity.Error, cloth.id, "", fieldPath + ".presetSha256", "Preset SHA-256 does not match the current file.");
        }

        private static void ValidateParameterOverrides(
            MagicaCharacterClothValidationResult result,
            MagicaCharacterClothAuthoringEntry cloth,
            string path)
        {
            MagicaCharacterClothParameterOverrides value = cloth.parameterOverrides;
            if (value == null) return;
            ValidateFloat(result, cloth.id, path + ".gravity", value.gravity, MagicaCharacterClothParameterRanges.GravityMin, MagicaCharacterClothParameterRanges.GravityMax);
            if (value.gravityDirection != null)
            {
                MagicaCharacterClothAuthoringVector direction = value.gravityDirection;
                float lengthSquared = direction.x * direction.x + direction.y * direction.y + direction.z * direction.z;
                if (!Finite(direction) || !Finite(lengthSquared) || lengthSquared < 0.000001f)
                    Add(result, "MAGCLOTH016", MagicaCharacterClothDiagnosticSeverity.Error, cloth.id, "", path + ".gravityDirection", "Gravity direction must be finite and non-zero.");
            }
            ValidateFloat(result, cloth.id, path + ".damping", value.damping, 0f, 1f);
            ValidateFloat(result, cloth.id, path + ".blendWeight", value.blendWeight, 0f, 1f);

            ValidateFloat(result, cloth.id, path + ".worldInertia", value.worldInertia, 0f, 1f);
            ValidateFloat(result, cloth.id, path + ".localInertia", value.localInertia, 0f, 1f);
            ValidateFloat(result, cloth.id, path + ".movementInertiaSmoothing", value.movementInertiaSmoothing, 0f, 1f);
            ValidateLimit(result, cloth.id, path + ".movementSpeedLimit", value.movementSpeedLimit, 0f, MagicaCharacterClothParameterRanges.MovementSpeedMax);
            ValidateLimit(result, cloth.id, path + ".rotationSpeedLimit", value.rotationSpeedLimit, 0f, MagicaCharacterClothParameterRanges.RotationSpeedMax);
            ValidateLimit(result, cloth.id, path + ".particleSpeedLimit", value.particleSpeedLimit, 0f, MagicaCharacterClothParameterRanges.ParticleSpeedMax);
            ValidateFloat(result, cloth.id, path + ".teleportDistance", value.teleportDistance, 0f, MagicaCharacterClothParameterRanges.TeleportDistanceMax);
            ValidateFloat(result, cloth.id, path + ".teleportRotation", value.teleportRotation, 0f, MagicaCharacterClothParameterRanges.TeleportRotationMax);

            ValidateFloat(result, cloth.id, path + ".distanceStiffness", value.distanceStiffness, 0f, 1f);
            ValidateLimit(result, cloth.id, path + ".angleRestorationStiffness", value.angleRestorationStiffness, 0f, 1f);
            ValidateFloat(result, cloth.id, path + ".velocityAttenuation", value.velocityAttenuation, 0f, 1f);
            ValidateLimit(result, cloth.id, path + ".angleLimit", value.angleLimit, 0f, MagicaCharacterClothParameterRanges.AngleMax);
            ValidateLimit(result, cloth.id, path + ".maxDistance", value.maxDistance, 0f, MagicaCharacterClothParameterRanges.MaxDistanceMax);

            ValidateFloat(result, cloth.id, path + ".collisionFriction", value.collisionFriction, 0f, MagicaCharacterClothParameterRanges.CollisionFrictionMax);
            ValidateFloat(result, cloth.id, path + ".collisionLimitDistance", value.collisionLimitDistance, 0f, MagicaCharacterClothParameterRanges.CollisionLimitDistanceMax);
            ValidateFloat(result, cloth.id, path + ".surfaceThickness", value.surfaceThickness, MagicaCharacterClothParameterRanges.SurfaceThicknessMin, MagicaCharacterClothParameterRanges.SurfaceThicknessMax);

            ValidateFloat(result, cloth.id, path + ".windInfluence", value.windInfluence, 0f, MagicaCharacterClothParameterRanges.WindMax);
            ValidateFloat(result, cloth.id, path + ".windFrequency", value.windFrequency, 0f, MagicaCharacterClothParameterRanges.WindMax);
            ValidateFloat(result, cloth.id, path + ".windTurbulence", value.windTurbulence, 0f, MagicaCharacterClothParameterRanges.WindMax);

            ValidateLimit(result, cloth.id, path + ".springPower", value.springPower, MagicaCharacterClothParameterRanges.SpringPowerMin, MagicaCharacterClothParameterRanges.SpringPowerMax);
            ValidateFloat(result, cloth.id, path + ".springLimitDistance", value.springLimitDistance, 0f, MagicaCharacterClothParameterRanges.SpringLimitDistanceMax);
        }

        private static void ValidateFloat(
            MagicaCharacterClothValidationResult result,
            string clothId,
            string path,
            MagicaCharacterClothFloatOverride value,
            float min,
            float max)
        {
            if (value != null && (!Finite(value.value) || value.value < min || value.value > max))
                Add(result, "MAGCLOTH016", MagicaCharacterClothDiagnosticSeverity.Error, clothId, "", path, $"Cloth parameter must be finite and within [{min}, {max}].");
        }

        private static void ValidateLimit(
            MagicaCharacterClothValidationResult result,
            string clothId,
            string path,
            MagicaCharacterClothLimitOverride value,
            float min,
            float max)
        {
            if (value != null && (!Finite(value.value) || value.value < min || value.value > max))
                Add(result, "MAGCLOTH016", MagicaCharacterClothDiagnosticSeverity.Error, clothId, "", path, $"Cloth limit parameter must be finite and within [{min}, {max}].");
        }

        private static bool Finite(MagicaCharacterClothAuthoringVector value) => value != null && Finite(value.x) && Finite(value.y) && Finite(value.z);
        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        private static bool FinitePositive(float value) => Finite(value) && value > 0f;
        private static bool UniformScale(Vector3 scale) => FinitePositive(Mathf.Abs(scale.x)) && FinitePositive(Mathf.Abs(scale.y)) && FinitePositive(Mathf.Abs(scale.z)) && Mathf.Abs(Mathf.Abs(scale.x) - Mathf.Abs(scale.y)) < 0.0001f && Mathf.Abs(Mathf.Abs(scale.x) - Mathf.Abs(scale.z)) < 0.0001f;
        private static string NormalizePath(string path) => (path ?? string.Empty).Replace('\\', '/').Trim().Trim('/');
        private static void Add(MagicaCharacterClothValidationResult result, string code, MagicaCharacterClothDiagnosticSeverity severity, string clothId, string colliderId, string fieldPath, string message) => result.diagnostics.Add(new MagicaCharacterClothDiagnostic { Code = code, Severity = severity, ClothId = clothId ?? string.Empty, ColliderId = colliderId ?? string.Empty, FieldPath = fieldPath ?? string.Empty, Message = message ?? string.Empty });
    }
}
