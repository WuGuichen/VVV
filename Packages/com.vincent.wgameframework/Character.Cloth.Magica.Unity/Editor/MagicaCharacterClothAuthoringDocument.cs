using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace MxFramework.CharacterCloth.Magica.Unity.Editor
{
    [Serializable]
    public sealed class MagicaCharacterClothAuthoringDocument
    {
        [JsonProperty("schemaVersion", Order = 1)] public string schemaVersion = "1.1";
        [JsonProperty("backend", Order = 2)] public string backend = "MagicaCloth2";
        [JsonProperty("enabled", Order = 3)] public bool enabled = true;
        [JsonProperty("cloths", Order = 4)] public MagicaCharacterClothAuthoringEntry[] cloths = Array.Empty<MagicaCharacterClothAuthoringEntry>();
        [NonSerialized, JsonExtensionData] public IDictionary<string, JToken> extensionData;

        public MagicaCharacterClothAuthoringDocument Clone() =>
            MagicaCharacterClothAuthoringSerializer.Deserialize(
                MagicaCharacterClothAuthoringSerializer.Serialize(this));
    }

    [Serializable]
    public sealed class MagicaCharacterClothAuthoringEntry
    {
        [JsonProperty("id", Order = 1)] public string id = string.Empty;
        [JsonProperty("clothType", Order = 2)] public string clothType = "BoneCloth";
        [JsonProperty("rootBonePaths", Order = 3)] public string[] rootBonePaths = Array.Empty<string>();
        [JsonProperty("connectionMode", Order = 4)] public string connectionMode = "Line";
        [JsonProperty("presetAssetPath", Order = 5)] public string presetAssetPath = string.Empty;
        [JsonProperty("presetSha256", Order = 6)] public string presetSha256 = string.Empty;
        [JsonProperty("colliders", Order = 7)] public MagicaCharacterClothAuthoringCollider[] colliders = Array.Empty<MagicaCharacterClothAuthoringCollider>();
        [SerializeReference, JsonProperty("parameterOverrides", Order = 8, NullValueHandling = NullValueHandling.Ignore)]
        public MagicaCharacterClothParameterOverrides parameterOverrides;
        [NonSerialized, JsonExtensionData] public IDictionary<string, JToken> extensionData;
    }

    [Serializable]
    public sealed class MagicaCharacterClothParameterOverrides
    {
        [SerializeReference, JsonProperty("gravity", Order = 1, NullValueHandling = NullValueHandling.Ignore)] public MagicaCharacterClothFloatOverride gravity;
        [SerializeReference, JsonProperty("gravityDirection", Order = 2, NullValueHandling = NullValueHandling.Ignore)] public MagicaCharacterClothAuthoringVector gravityDirection;
        [SerializeReference, JsonProperty("damping", Order = 3, NullValueHandling = NullValueHandling.Ignore)] public MagicaCharacterClothFloatOverride damping;
        [SerializeReference, JsonProperty("blendWeight", Order = 4, NullValueHandling = NullValueHandling.Ignore)] public MagicaCharacterClothFloatOverride blendWeight;

        [SerializeReference, JsonProperty("worldInertia", Order = 5, NullValueHandling = NullValueHandling.Ignore)] public MagicaCharacterClothFloatOverride worldInertia;
        [SerializeReference, JsonProperty("localInertia", Order = 6, NullValueHandling = NullValueHandling.Ignore)] public MagicaCharacterClothFloatOverride localInertia;
        [SerializeReference, JsonProperty("movementInertiaSmoothing", Order = 7, NullValueHandling = NullValueHandling.Ignore)] public MagicaCharacterClothFloatOverride movementInertiaSmoothing;
        [SerializeReference, JsonProperty("movementSpeedLimit", Order = 8, NullValueHandling = NullValueHandling.Ignore)] public MagicaCharacterClothLimitOverride movementSpeedLimit;
        [SerializeReference, JsonProperty("rotationSpeedLimit", Order = 9, NullValueHandling = NullValueHandling.Ignore)] public MagicaCharacterClothLimitOverride rotationSpeedLimit;
        [SerializeReference, JsonProperty("particleSpeedLimit", Order = 10, NullValueHandling = NullValueHandling.Ignore)] public MagicaCharacterClothLimitOverride particleSpeedLimit;
        [SerializeReference, JsonProperty("teleportDistance", Order = 11, NullValueHandling = NullValueHandling.Ignore)] public MagicaCharacterClothFloatOverride teleportDistance;
        [SerializeReference, JsonProperty("teleportRotation", Order = 12, NullValueHandling = NullValueHandling.Ignore)] public MagicaCharacterClothFloatOverride teleportRotation;

        [SerializeReference, JsonProperty("distanceStiffness", Order = 13, NullValueHandling = NullValueHandling.Ignore)] public MagicaCharacterClothFloatOverride distanceStiffness;
        [SerializeReference, JsonProperty("angleRestorationStiffness", Order = 14, NullValueHandling = NullValueHandling.Ignore)] public MagicaCharacterClothLimitOverride angleRestorationStiffness;
        [SerializeReference, JsonProperty("velocityAttenuation", Order = 15, NullValueHandling = NullValueHandling.Ignore)] public MagicaCharacterClothFloatOverride velocityAttenuation;
        [SerializeReference, JsonProperty("angleLimit", Order = 16, NullValueHandling = NullValueHandling.Ignore)] public MagicaCharacterClothLimitOverride angleLimit;
        [SerializeReference, JsonProperty("maxDistance", Order = 17, NullValueHandling = NullValueHandling.Ignore)] public MagicaCharacterClothLimitOverride maxDistance;

        [SerializeReference, JsonProperty("collisionFriction", Order = 18, NullValueHandling = NullValueHandling.Ignore)] public MagicaCharacterClothFloatOverride collisionFriction;
        [SerializeReference, JsonProperty("collisionLimitDistance", Order = 19, NullValueHandling = NullValueHandling.Ignore)] public MagicaCharacterClothFloatOverride collisionLimitDistance;
        [SerializeReference, JsonProperty("surfaceThickness", Order = 20, NullValueHandling = NullValueHandling.Ignore)] public MagicaCharacterClothFloatOverride surfaceThickness;

        [SerializeReference, JsonProperty("windInfluence", Order = 21, NullValueHandling = NullValueHandling.Ignore)] public MagicaCharacterClothFloatOverride windInfluence;
        [SerializeReference, JsonProperty("windFrequency", Order = 22, NullValueHandling = NullValueHandling.Ignore)] public MagicaCharacterClothFloatOverride windFrequency;
        [SerializeReference, JsonProperty("windTurbulence", Order = 23, NullValueHandling = NullValueHandling.Ignore)] public MagicaCharacterClothFloatOverride windTurbulence;

        [SerializeReference, JsonProperty("springPower", Order = 24, NullValueHandling = NullValueHandling.Ignore)] public MagicaCharacterClothLimitOverride springPower;
        [SerializeReference, JsonProperty("springLimitDistance", Order = 25, NullValueHandling = NullValueHandling.Ignore)] public MagicaCharacterClothFloatOverride springLimitDistance;

        public bool IsEmpty() =>
            gravity == null && gravityDirection == null && damping == null && blendWeight == null
            && worldInertia == null && localInertia == null && movementInertiaSmoothing == null
            && movementSpeedLimit == null && rotationSpeedLimit == null && particleSpeedLimit == null
            && teleportDistance == null && teleportRotation == null
            && distanceStiffness == null && angleRestorationStiffness == null && velocityAttenuation == null
            && angleLimit == null && maxDistance == null
            && collisionFriction == null && collisionLimitDistance == null && surfaceThickness == null
            && windInfluence == null && windFrequency == null && windTurbulence == null
            && springPower == null && springLimitDistance == null;
    }

    [Serializable]
    public sealed class MagicaCharacterClothFloatOverride
    {
        [JsonProperty("value", Order = 1)] public float value;

        public MagicaCharacterClothFloatOverride() { }

        public MagicaCharacterClothFloatOverride(float value) => this.value = value;
    }

    [Serializable]
    public sealed class MagicaCharacterClothLimitOverride
    {
        [JsonProperty("use", Order = 1)] public bool use = true;
        [JsonProperty("value", Order = 2)] public float value;

        public MagicaCharacterClothLimitOverride() { }

        public MagicaCharacterClothLimitOverride(bool use, float value)
        {
            this.use = use;
            this.value = value;
        }
    }

    [Serializable]
    public sealed class MagicaCharacterClothAuthoringCollider
    {
        [JsonProperty("id", Order = 1)] public string id = string.Empty;
        [JsonProperty("type", Order = 2)] public string type = "Sphere";
        [JsonProperty("parentBonePath", Order = 3)] public string parentBonePath = string.Empty;
        [JsonProperty("localPosition", Order = 4)] public MagicaCharacterClothAuthoringVector localPosition = new MagicaCharacterClothAuthoringVector();
        [JsonProperty("localRotationEuler", Order = 5)] public MagicaCharacterClothAuthoringVector localRotationEuler = new MagicaCharacterClothAuthoringVector();
        [JsonProperty("radius", Order = 6)] public float radius = 0.1f;
        [JsonProperty("endRadius", Order = 7)] public float endRadius;
        [JsonProperty("length", Order = 8)] public float length;
        [JsonProperty("direction", Order = 9)] public string direction = "Y";
        [NonSerialized, JsonExtensionData] public IDictionary<string, JToken> extensionData;
    }

    [Serializable]
    public sealed class MagicaCharacterClothAuthoringVector
    {
        [JsonProperty("x", Order = 1)] public float x;
        [JsonProperty("y", Order = 2)] public float y;
        [JsonProperty("z", Order = 3)] public float z;

        public MagicaCharacterClothAuthoringVector() { }

        public MagicaCharacterClothAuthoringVector(float x, float y, float z)
        {
            this.x = x;
            this.y = y;
            this.z = z;
        }
    }

    public static class MagicaCharacterClothAuthoringNormalization
    {
        public const float PositionEpsilon = 0.000001f;
        public const float RotationEpsilon = 0.0001f;

        public static void Normalize(MagicaCharacterClothAuthoringDocument document)
        {
            MagicaCharacterClothAuthoringEntry[] cloths = document?.cloths ?? Array.Empty<MagicaCharacterClothAuthoringEntry>();
            for (int i = 0; i < cloths.Length; i++)
            {
                MagicaCharacterClothAuthoringCollider[] colliders = cloths[i]?.colliders ?? Array.Empty<MagicaCharacterClothAuthoringCollider>();
                for (int j = 0; j < colliders.Length; j++)
                {
                    MagicaCharacterClothAuthoringCollider collider = colliders[j];
                    if (collider == null) continue;
                    NormalizePosition(collider.localPosition);
                    NormalizeRotation(collider.localRotationEuler);
                }
                if (cloths[i]?.parameterOverrides?.IsEmpty() == true)
                    cloths[i].parameterOverrides = null;
            }
        }

        public static int RepairUnityInlineSerializationPollution(
            MagicaCharacterClothAuthoringDocument document)
        {
            int repaired = 0;
            MagicaCharacterClothAuthoringEntry[] cloths =
                document?.cloths ?? Array.Empty<MagicaCharacterClothAuthoringEntry>();
            for (int i = 0; i < cloths.Length; i++)
            {
                MagicaCharacterClothParameterOverrides value = cloths[i]?.parameterOverrides;
                if (!LooksLikeInlineSerializationPollution(value)) continue;

                ClearZero(ref value.gravity);
                if (value.gravityDirection != null
                    && value.gravityDirection.x == 0f
                    && value.gravityDirection.y == 0f
                    && value.gravityDirection.z == 0f)
                    value.gravityDirection = null;
                ClearZero(ref value.damping);
                ClearZero(ref value.blendWeight);
                ClearZero(ref value.worldInertia);
                ClearZero(ref value.localInertia);
                ClearZero(ref value.movementInertiaSmoothing);
                ClearZero(ref value.movementSpeedLimit);
                ClearZero(ref value.rotationSpeedLimit);
                ClearZero(ref value.particleSpeedLimit);
                ClearZero(ref value.teleportDistance);
                ClearZero(ref value.teleportRotation);
                ClearZero(ref value.distanceStiffness);
                ClearZero(ref value.angleRestorationStiffness);
                ClearZero(ref value.velocityAttenuation);
                ClearZero(ref value.angleLimit);
                ClearZero(ref value.maxDistance);
                ClearZero(ref value.collisionFriction);
                ClearZero(ref value.collisionLimitDistance);
                ClearZero(ref value.surfaceThickness);
                ClearZero(ref value.windInfluence);
                ClearZero(ref value.windFrequency);
                ClearZero(ref value.windTurbulence);
                ClearZero(ref value.springPower);
                ClearZero(ref value.springLimitDistance);
                if (value.IsEmpty()) cloths[i].parameterOverrides = null;
                repaired++;
            }
            return repaired;
        }

        private static bool LooksLikeInlineSerializationPollution(
            MagicaCharacterClothParameterOverrides value)
        {
            if (value == null || CountNonNull(value) < 20) return false;
            bool zeroDirection = value.gravityDirection != null
                && value.gravityDirection.x == 0f
                && value.gravityDirection.y == 0f
                && value.gravityDirection.z == 0f;
            bool invalidThickness = value.surfaceThickness != null
                && value.surfaceThickness.value == 0f;
            bool invalidSpring = value.springPower != null
                && value.springPower.value == 0f;
            return zeroDirection && invalidThickness && invalidSpring;
        }

        private static int CountNonNull(MagicaCharacterClothParameterOverrides value)
        {
            int count = 0;
            if (value.gravity != null) count++;
            if (value.gravityDirection != null) count++;
            if (value.damping != null) count++;
            if (value.blendWeight != null) count++;
            if (value.worldInertia != null) count++;
            if (value.localInertia != null) count++;
            if (value.movementInertiaSmoothing != null) count++;
            if (value.movementSpeedLimit != null) count++;
            if (value.rotationSpeedLimit != null) count++;
            if (value.particleSpeedLimit != null) count++;
            if (value.teleportDistance != null) count++;
            if (value.teleportRotation != null) count++;
            if (value.distanceStiffness != null) count++;
            if (value.angleRestorationStiffness != null) count++;
            if (value.velocityAttenuation != null) count++;
            if (value.angleLimit != null) count++;
            if (value.maxDistance != null) count++;
            if (value.collisionFriction != null) count++;
            if (value.collisionLimitDistance != null) count++;
            if (value.surfaceThickness != null) count++;
            if (value.windInfluence != null) count++;
            if (value.windFrequency != null) count++;
            if (value.windTurbulence != null) count++;
            if (value.springPower != null) count++;
            if (value.springLimitDistance != null) count++;
            return count;
        }

        private static void ClearZero(ref MagicaCharacterClothFloatOverride value)
        {
            if (value != null && value.value == 0f) value = null;
        }

        private static void ClearZero(ref MagicaCharacterClothLimitOverride value)
        {
            if (value != null && value.value == 0f) value = null;
        }

        public static void NormalizePosition(MagicaCharacterClothAuthoringVector value) =>
            NormalizeVector(value, PositionEpsilon, false);

        public static void NormalizeRotation(MagicaCharacterClothAuthoringVector value) =>
            NormalizeVector(value, RotationEpsilon, true);

        public static float NormalizePosition(float value) => NormalizeNearZero(value, PositionEpsilon);

        public static float NormalizeRotation(float value)
        {
            float normalized = NormalizeNearZero(value, RotationEpsilon);
            return Math.Abs(Math.Abs(normalized) - 360f) < RotationEpsilon ? 0f : normalized;
        }

        private static void NormalizeVector(MagicaCharacterClothAuthoringVector value, float epsilon, bool rotation)
        {
            if (value == null) return;
            value.x = rotation ? NormalizeRotation(value.x) : NormalizeNearZero(value.x, epsilon);
            value.y = rotation ? NormalizeRotation(value.y) : NormalizeNearZero(value.y, epsilon);
            value.z = rotation ? NormalizeRotation(value.z) : NormalizeNearZero(value.z, epsilon);
        }

        private static float NormalizeNearZero(float value, float epsilon) =>
            Math.Abs(value) < epsilon ? 0f : value;
    }

    public static class MagicaCharacterClothCapsuleGeometry
    {
        public static float StartCenterOffset(float length, float startRadius) =>
            Math.Max(length * 0.5f - startRadius, 0f);

        public static float EndCenterOffset(float length, float endRadius) =>
            Math.Max(length * 0.5f - endRadius, 0f);

        public static float MinimumSeparatedLength(float startRadius, float endRadius) =>
            Math.Max(startRadius, endRadius) * 2f;

        public static bool HasCollapsedEnd(float length, float startRadius, float endRadius) =>
            StartCenterOffset(length, startRadius) <= 0f || EndCenterOffset(length, endRadius) <= 0f;
    }

    public static class MagicaCharacterClothAuthoringSerializer
    {
        private static readonly JsonSerializerSettings Settings = new JsonSerializerSettings
        {
            Culture = System.Globalization.CultureInfo.InvariantCulture,
            FloatFormatHandling = FloatFormatHandling.DefaultValue,
            FloatParseHandling = FloatParseHandling.Double,
            NullValueHandling = NullValueHandling.Include,
            ObjectCreationHandling = ObjectCreationHandling.Replace,
        };

        public static MagicaCharacterClothAuthoringDocument Deserialize(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
                throw new JsonException("Character Cloth authoring JSON is empty.");
            return JsonConvert.DeserializeObject<MagicaCharacterClothAuthoringDocument>(json, Settings)
                ?? throw new JsonException("Character Cloth authoring JSON produced no document.");
        }

        public static string Serialize(MagicaCharacterClothAuthoringDocument document)
        {
            if (document == null)
                throw new ArgumentNullException(nameof(document));
            string json = JsonConvert.SerializeObject(document, Formatting.Indented, Settings)
                .Replace("\r\n", "\n");
            return json.EndsWith("\n", StringComparison.Ordinal) ? json : json + "\n";
        }
    }
}
