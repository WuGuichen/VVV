using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace MxFramework.Editor
{
    internal enum VfxSemanticValueType
    {
        Bool,
        Int,
        Float,
        Enum,
        Vector2,
        Vector3,
        Vector4,
        ColorHdr,
        Gradient,
        Curve,
        AssetReference
    }

    internal enum VfxSemanticOperation
    {
        Multiply,
        Add,
        Remap,
        Replace
    }

    internal enum VfxSemanticTimeDomain
    {
        Static,
        LayerFrame,
        EmitterNormalized,
        ParticleNormalized,
        CycleLocal
    }

    internal enum VfxRetimeMode
    {
        PlaybackWindow,
        ExtendBody,
        ExtendTail,
        RepeatBody,
        EventRemap,
        WholeSpeed,
        Fixed
    }

    internal enum VfxVisualStartMode
    {
        AutoNormalize,
        PreserveSource,
        ManualLeadIn
    }

    internal enum VfxTimeEventAnchor
    {
        StartLocked,
        EndLocked,
        BodyNormalized,
        CycleLocal
    }

    internal enum VfxMemberParticipationState
    {
        Follow,
        Override,
        Excluded,
        Unsupported,
        Unconfirmed
    }

    internal enum VfxProfileReviewState
    {
        AutoSuggested,
        Confirmed,
        Deprecated,
        Stale
    }

    internal enum VfxSemanticValidationLevel
    {
        Structural,
        Previewable,
        CandidateBakeable,
        ReviewNeededEligible
    }

    [Serializable]
    internal sealed class VfxSemanticPropertyDefinitionView
    {
        [JsonProperty("propertyId")] public string PropertyId = string.Empty;
        [JsonProperty("displayName")] public string DisplayName = string.Empty;
        [JsonProperty("group")] public string Group = string.Empty;
        [JsonProperty("valueType")] public string ValueType = nameof(VfxSemanticValueType.Float);
        [JsonProperty("unit")] public string Unit = string.Empty;
        [JsonProperty("timeDomain")] public string TimeDomain = nameof(VfxSemanticTimeDomain.Static);
        [JsonProperty("operations")] public List<string> Operations = new List<string>();
        [JsonProperty("minimum", NullValueHandling = NullValueHandling.Ignore)] public double? Minimum;
        [JsonProperty("maximum", NullValueHandling = NullValueHandling.Ignore)] public double? Maximum;
        [JsonProperty("recommendedMinimum", NullValueHandling = NullValueHandling.Ignore)] public double? RecommendedMinimum;
        [JsonProperty("recommendedMaximum", NullValueHandling = NullValueHandling.Ignore)] public double? RecommendedMaximum;
        [JsonProperty("backendCapabilities")] public List<string> BackendCapabilities = new List<string>();

        public bool Supports(VfxSemanticOperation operation)
        {
            return Operations.Contains(operation.ToString(), StringComparer.Ordinal);
        }
    }

    [Serializable]
    internal sealed class VfxSemanticPropertyRegistryView
    {
        [JsonProperty("schemaVersion")] public string SchemaVersion = "mx.vfx.property-registry.v1";
        [JsonProperty("registryId")] public string RegistryId = "mxframework.vfx.properties";
        [JsonProperty("properties")] public List<VfxSemanticPropertyDefinitionView> Properties = new List<VfxSemanticPropertyDefinitionView>();
        [JsonProperty("registryHash")] public string RegistryHash = string.Empty;

        [JsonIgnore] private Dictionary<string, VfxSemanticPropertyDefinitionView> _byId;

        public VfxSemanticPropertyDefinitionView Find(string propertyId)
        {
            _byId ??= Properties.ToDictionary(value => value.PropertyId, StringComparer.Ordinal);
            _byId.TryGetValue(propertyId ?? string.Empty, out VfxSemanticPropertyDefinitionView result);
            return result;
        }
    }

    [Serializable]
    internal sealed class VfxSemanticOverrideView
    {
        [JsonProperty("propertyId")] public string PropertyId = string.Empty;
        [JsonProperty("operation")] public string Operation = nameof(VfxSemanticOperation.Multiply);
        [JsonProperty("value")] public JToken Value { get; set; } = JValue.CreateNull();
        [JsonProperty("experimental", DefaultValueHandling = DefaultValueHandling.Ignore)] public bool Experimental;

        public float FloatValue(float fallback = 0f)
        {
            return Value != null && Value.Type != JTokenType.Null ? Value.Value<float>() : fallback;
        }

        public int IntValue(int fallback = 0)
        {
            return Value != null && Value.Type != JTokenType.Null ? Value.Value<int>() : fallback;
        }
    }

    [Serializable]
    internal sealed class VfxProfileDependencyView
    {
        [JsonProperty("profileId")] public string ProfileId = string.Empty;
        [JsonProperty("hash")] public string Hash = string.Empty;
    }

    [Serializable]
    internal sealed class VfxDraftDependencySetView
    {
        [JsonProperty("catalogHash")] public string CatalogHash = string.Empty;
        [JsonProperty("sourceIndexHash")] public string SourceIndexHash = string.Empty;
        [JsonProperty("curationHash")] public string CurationHash = string.Empty;
        [JsonProperty("propertyRegistryHash")] public string PropertyRegistryHash = string.Empty;
        [JsonProperty("profileManifestHash")] public string ProfileManifestHash = string.Empty;
    }

    [Serializable]
    internal sealed class VfxLayerSourceReferenceView
    {
        [JsonProperty("kind")] public string Kind = "Element";
        [JsonProperty("stableId")] public string StableId = string.Empty;
    }

    [Serializable]
    internal sealed class VfxLayerTransformView
    {
        [JsonProperty("position")] public List<float> Position = new List<float> { 0f, 0f, 0f };
        [JsonProperty("rotationEuler")] public List<float> RotationEuler = new List<float> { 0f, 0f, 0f };
        [JsonProperty("scale")] public List<float> Scale = new List<float> { 1f, 1f, 1f };
    }

    [Serializable]
    internal sealed class VfxTimeSegmentsView
    {
        [JsonProperty("preRollFrames")] public int PreRollFrames;
        [JsonProperty("entryFrames")] public int EntryFrames;
        [JsonProperty("bodyFrames")] public int BodyFrames = 1;
        [JsonProperty("exitFrames")] public int ExitFrames;
        [JsonProperty("cleanupFrames")] public int CleanupFrames;
        [JsonProperty("minimumBodyFrames")] public int MinimumBodyFrames = 1;
        [JsonProperty("cycleFrames")] public int CycleFrames;

        [JsonIgnore] public int ArtisticFrames => Math.Max(0, PreRollFrames) + Math.Max(0, EntryFrames) + Math.Max(0, BodyFrames) + Math.Max(0, ExitFrames);
        [JsonIgnore] public int TechnicalFrames => ArtisticFrames + Math.Max(0, CleanupFrames);
        [JsonIgnore] public int MinimumArtisticFrames => Math.Max(0, PreRollFrames) + Math.Max(0, EntryFrames) + Math.Max(1, MinimumBodyFrames) + Math.Max(0, ExitFrames);

        public VfxTimeSegmentsView Clone()
        {
            return (VfxTimeSegmentsView)MemberwiseClone();
        }
    }

    [Serializable]
    internal sealed class VfxTimeEventView
    {
        [JsonProperty("eventId")] public string EventId = string.Empty;
        [JsonProperty("anchor")] public string Anchor = nameof(VfxTimeEventAnchor.StartLocked);
        [JsonProperty("frame")] public int Frame;
        [JsonProperty("protected", DefaultValueHandling = DefaultValueHandling.Ignore)] public bool Protected;
    }

    [Serializable]
    internal sealed class VfxRetimeMapPointView
    {
        [JsonProperty("sourceFrame")] public int SourceFrame;
        [JsonProperty("targetFrame")] public int TargetFrame;
    }

    [Serializable]
    internal sealed class VfxLayerTimingView
    {
        [JsonProperty("retimeMode")] public string RetimeMode = nameof(VfxRetimeMode.ExtendBody);
        [JsonProperty("sourceSegments", NullValueHandling = NullValueHandling.Ignore)] public VfxTimeSegmentsView SourceSegments;
        [JsonProperty("targetArtisticFrames")] public int TargetArtisticFrames;
        [JsonProperty("minimumBodyFrames", DefaultValueHandling = DefaultValueHandling.Ignore)] public int MinimumBodyFrames;
        [JsonProperty("windowEndPolicy", NullValueHandling = NullValueHandling.Ignore)] public string WindowEndPolicy;
        [JsonProperty("dissipateFrames", DefaultValueHandling = DefaultValueHandling.Ignore)] public int DissipateFrames;
        [JsonProperty("protectedEvents", NullValueHandling = NullValueHandling.Ignore)] public List<string> ProtectedEvents = new List<string>();
        [JsonProperty("eventMap", NullValueHandling = NullValueHandling.Ignore)] public List<VfxRetimeMapPointView> EventMap = new List<VfxRetimeMapPointView>();
        [JsonProperty("tailPolicy", NullValueHandling = NullValueHandling.Ignore)] public string TailPolicy;
        [JsonProperty("keepTotalEmission", DefaultValueHandling = DefaultValueHandling.Ignore)] public bool KeepTotalEmission;
        [JsonProperty("allowPartialCycle", DefaultValueHandling = DefaultValueHandling.Ignore)] public bool AllowPartialCycle;
        [JsonProperty("experimentalAcknowledged", DefaultValueHandling = DefaultValueHandling.Ignore)] public bool ExperimentalAcknowledged;
    }

    [Serializable]
    internal sealed class VfxLayerVisualStartView
    {
        [JsonProperty("mode")] public string Mode = nameof(VfxVisualStartMode.AutoNormalize);
        [JsonProperty("manualLeadInFrames", DefaultValueHandling = DefaultValueHandling.Ignore)]
        public int ManualLeadInFrames;
    }

    [Serializable]
    internal sealed class VfxMemberParticipationView
    {
        [JsonProperty("memberId")] public string MemberId = string.Empty;
        [JsonProperty("state")] public string State = nameof(VfxMemberParticipationState.Follow);
        [JsonProperty("propertyStates", NullValueHandling = NullValueHandling.Ignore)]
        public Dictionary<string, string> PropertyStates { get; set; } = new Dictionary<string, string>(StringComparer.Ordinal);
    }

    [Serializable]
    internal sealed class VfxMemberSemanticOverrideView
    {
        [JsonProperty("memberId")] public string MemberId = string.Empty;
        [JsonProperty("overrides")] public List<VfxSemanticOverrideView> Overrides = new List<VfxSemanticOverrideView>();
        [JsonProperty("retimeMode", NullValueHandling = NullValueHandling.Ignore)] public string RetimeMode;
    }

    [Serializable]
    internal sealed class VfxProfileTimingView
    {
        [JsonProperty("preRollFrames")] public int PreRollFrames;
        [JsonProperty("entryFrames")] public int EntryFrames;
        [JsonProperty("bodyFrames")] public int BodyFrames = 1;
        [JsonProperty("exitFrames")] public int ExitFrames;
        [JsonProperty("cleanupFrames")] public int CleanupFrames;
        [JsonProperty("minimumBodyFrames")] public int MinimumBodyFrames = 1;
        [JsonProperty("cycleFrames")] public int CycleFrames;
        [JsonProperty("retimeMechanism")] public string RetimeMechanism = "EmitterWindow";
        [JsonProperty("tailPolicy")] public string TailPolicy = "EndLocked";
        [JsonProperty("trailContract", NullValueHandling = NullValueHandling.Ignore)]
        public VfxTrailAuthoringContractView TrailContract;

        public VfxTimeSegmentsView ToSegments()
        {
            return new VfxTimeSegmentsView
            {
                PreRollFrames = PreRollFrames,
                EntryFrames = EntryFrames,
                BodyFrames = BodyFrames,
                ExitFrames = ExitFrames,
                CleanupFrames = CleanupFrames,
                MinimumBodyFrames = MinimumBodyFrames,
                CycleFrames = CycleFrames
            };
        }
    }

    [Serializable]
    internal sealed class VfxTrailAuthoringContractView
    {
        [JsonProperty("retimePolicy")] public string RetimePolicy = "ReadOnly";
        [JsonProperty("preserveWidth")] public bool PreserveWidth = true;
        [JsonProperty("preserveColor")] public bool PreserveColor = true;
        [JsonProperty("expectedDieWithParticles")] public bool ExpectedDieWithParticles;
        [JsonProperty("sourceTrailHash")] public string SourceTrailHash = string.Empty;
    }

    [Serializable]
    internal sealed class VfxSemanticBindingView
    {
        [JsonProperty("propertyId")] public string PropertyId = string.Empty;
        [JsonProperty("adapter")] public string Adapter = string.Empty;
        [JsonProperty("target")] public string Target = string.Empty;
        [JsonProperty("operation")] public string Operation = nameof(VfxSemanticOperation.Multiply);
        [JsonProperty("timeDomain")] public string TimeDomain = nameof(VfxSemanticTimeDomain.Static);
        [JsonProperty("valueType")] public string ValueType = "Float";
        [JsonProperty("shaderProfileId", NullValueHandling = NullValueHandling.Ignore)] public string ShaderProfileId;
        [JsonProperty("keyword", NullValueHandling = NullValueHandling.Ignore)] public string Keyword;
        [JsonProperty("keywordRequiredValue", NullValueHandling = NullValueHandling.Ignore)] public float? KeywordRequiredValue;
        [JsonProperty("primaryOpacity", DefaultValueHandling = DefaultValueHandling.Ignore)] public bool PrimaryOpacity;
        [JsonProperty("responseWeight", DefaultValueHandling = DefaultValueHandling.Ignore)] public float ResponseWeight = 1f;
        [JsonProperty("recommendedMinimum", NullValueHandling = NullValueHandling.Ignore)] public float? RecommendedMinimum;
        [JsonProperty("recommendedMaximum", NullValueHandling = NullValueHandling.Ignore)] public float? RecommendedMaximum;
        [JsonProperty("readOnly", DefaultValueHandling = DefaultValueHandling.Ignore)] public bool ReadOnly;
    }

    [Serializable]
    internal sealed class VfxComponentProfileView
    {
        [JsonProperty("schemaVersion")] public string SchemaVersion = "mx.vfx.component-profile.v1";
        [JsonProperty("profileId")] public string ProfileId = string.Empty;
        [JsonProperty("sourceComponentId")] public string SourceComponentId = string.Empty;
        [JsonProperty("sourceSnapshotHash")] public string SourceSnapshotHash = string.Empty;
        [JsonProperty("status")] public string Status = nameof(VfxProfileReviewState.AutoSuggested);
        [JsonProperty("timing")] public VfxProfileTimingView Timing = new VfxProfileTimingView();
        [JsonProperty("events")] public List<VfxTimeEventView> Events = new List<VfxTimeEventView>();
        [JsonProperty("bindings")] public List<VfxSemanticBindingView> Bindings = new List<VfxSemanticBindingView>();
        [JsonProperty("readOnlyModules")] public List<string> ReadOnlyModules = new List<string>();
        [JsonProperty("blockers")] public List<string> Blockers = new List<string>();
        [JsonProperty("contentHash")] public string ContentHash = string.Empty;
    }

    [Serializable]
    internal sealed class VfxElementProfileMemberView
    {
        [JsonProperty("memberId")] public string MemberId = string.Empty;
        [JsonProperty("componentProfileId")] public string ComponentProfileId = string.Empty;
        [JsonProperty("responsibility")] public string Responsibility = "Detail";
        [JsonProperty("defaultRetimeMode")] public string DefaultRetimeMode = nameof(VfxRetimeMode.ExtendBody);
        [JsonProperty("participation")] public Dictionary<string, string> Participation { get; set; } = new Dictionary<string, string>(StringComparer.Ordinal);
        [JsonProperty("responseWeights")] public Dictionary<string, float> ResponseWeights { get; set; } = new Dictionary<string, float>(StringComparer.Ordinal);
    }

    [Serializable]
    internal sealed class VfxElementProfileView
    {
        [JsonProperty("schemaVersion")] public string SchemaVersion = "mx.vfx.element-profile.v1";
        [JsonProperty("profileId")] public string ProfileId = string.Empty;
        [JsonProperty("elementId")] public string ElementId = string.Empty;
        [JsonProperty("status")] public string Status = nameof(VfxProfileReviewState.AutoSuggested);
        [JsonProperty("members")] public List<VfxElementProfileMemberView> Members = new List<VfxElementProfileMemberView>();
        [JsonProperty("emitterScalePolicy")] public string EmitterScalePolicy = "KeepTotal";
        [JsonProperty("contentHash")] public string ContentHash = string.Empty;
    }

    [Serializable]
    internal sealed class VfxShaderPropertyBindingView
    {
        [JsonProperty("semanticPropertyId")] public string SemanticPropertyId = string.Empty;
        [JsonProperty("shaderPropertyName")] public string ShaderPropertyName = string.Empty;
        [JsonProperty("propertyType")] public string PropertyType = "Float";
        [JsonProperty("dynamicPropertyBlock")] public bool DynamicPropertyBlock;
        [JsonProperty("requiresDerivedMaterial")] public bool RequiresDerivedMaterial;
        [JsonProperty("timeDomain")] public string TimeDomain = nameof(VfxSemanticTimeDomain.Static);
        [JsonProperty("operation")] public string Operation = nameof(VfxSemanticOperation.Replace);
        [JsonProperty("keyword", NullValueHandling = NullValueHandling.Ignore)] public string Keyword;
        [JsonProperty("keywordRequiredValue", NullValueHandling = NullValueHandling.Ignore)] public float? KeywordRequiredValue;
    }

    [Serializable]
    internal sealed class VfxShaderCustomDataBindingView
    {
        [JsonProperty("stream")] public string Stream = "Custom1";
        [JsonProperty("channels")] public string Channels = "XYZW";
        [JsonProperty("semanticPropertyId")] public string SemanticPropertyId = string.Empty;
        [JsonProperty("shaderUsage")] public string ShaderUsage = string.Empty;
        [JsonProperty("editable")] public bool Editable;
        [JsonProperty("timeDomain")] public string TimeDomain = nameof(VfxSemanticTimeDomain.ParticleNormalized);
    }

    [Serializable]
    internal sealed class VfxShaderProfileView
    {
        [JsonProperty("schemaVersion")] public string SchemaVersion = "mx.vfx.shader-profile.v1";
        [JsonProperty("profileId")] public string ProfileId = string.Empty;
        [JsonProperty("shaderName")] public string ShaderName = string.Empty;
        [JsonProperty("shaderGuid")] public string ShaderGuid = string.Empty;
        [JsonProperty("shaderHash")] public string ShaderHash = string.Empty;
        [JsonProperty("status")] public string Status = nameof(VfxProfileReviewState.AutoSuggested);
        [JsonProperty("bindings")] public List<VfxShaderPropertyBindingView> Bindings = new List<VfxShaderPropertyBindingView>();
        [JsonProperty("requiredVertexStreams")] public List<string> RequiredVertexStreams = new List<string>();
        [JsonProperty("customDataBindings")] public List<VfxShaderCustomDataBindingView> CustomDataBindings = new List<VfxShaderCustomDataBindingView>();
        [JsonProperty("contentHash")] public string ContentHash = string.Empty;
    }

    [Serializable]
    internal sealed class VfxProfileManifestEntryView
    {
        [JsonProperty("profileId")] public string ProfileId = string.Empty;
        [JsonProperty("kind")] public string Kind = string.Empty;
        [JsonProperty("path")] public string Path = string.Empty;
        [JsonProperty("schemaVersion")] public string SchemaVersion = string.Empty;
        [JsonProperty("contentHash")] public string ContentHash = string.Empty;
        [JsonProperty("sourceDependencies")] public List<string> SourceDependencies = new List<string>();
        [JsonProperty("reviewState")] public string ReviewState = nameof(VfxProfileReviewState.AutoSuggested);
        [JsonProperty("backend")] public string Backend = "ParticleSystem";
    }

    [Serializable]
    internal sealed class VfxProfileManifestView
    {
        [JsonProperty("schemaVersion")] public string SchemaVersion = "mx.vfx.profile-manifest.v1";
        [JsonProperty("manifestId")] public string ManifestId = "mxframework.vfx.authoring-profiles";
        [JsonProperty("profiles")] public List<VfxProfileManifestEntryView> Profiles = new List<VfxProfileManifestEntryView>();
        [JsonProperty("manifestHash")] public string ManifestHash = string.Empty;
    }

    internal sealed class VfxProfileBatchWriteView
    {
        public object Profile;
        public string ProfileId = string.Empty;
        public string Kind = string.Empty;
        public string RelativePath = string.Empty;
        public IReadOnlyList<string> SourceDependencies = Array.Empty<string>();
        public string ReviewState = nameof(VfxProfileReviewState.AutoSuggested);
    }

    [Serializable]
    internal sealed class VfxCurveKeySnapshotView
    {
        [JsonProperty("time")] public float Time;
        [JsonProperty("value")] public float Value;
        [JsonProperty("inTangent")] public float InTangent;
        [JsonProperty("outTangent")] public float OutTangent;
        [JsonProperty("inTangentSpecial", NullValueHandling = NullValueHandling.Ignore)] public string InTangentSpecial;
        [JsonProperty("outTangentSpecial", NullValueHandling = NullValueHandling.Ignore)] public string OutTangentSpecial;
        [JsonProperty("inWeight")] public float InWeight;
        [JsonProperty("outWeight")] public float OutWeight;
        [JsonProperty("weightedMode")] public int WeightedMode;

        internal static VfxCurveKeySnapshotView Capture(Keyframe key, string path)
        {
            EnsureFinite(key.time, path + ".time");
            EnsureFinite(key.value, path + ".value");
            EnsureFinite(key.inWeight, path + ".inWeight");
            EnsureFinite(key.outWeight, path + ".outWeight");
            var result = new VfxCurveKeySnapshotView
            {
                Time = key.time,
                Value = key.value,
                InWeight = key.inWeight,
                OutWeight = key.outWeight,
                WeightedMode = (int)key.weightedMode
            };
            result.InTangent = CaptureTangent(key.inTangent, path + ".inTangent", out result.InTangentSpecial);
            result.OutTangent = CaptureTangent(key.outTangent, path + ".outTangent", out result.OutTangentSpecial);
            return result;
        }

        internal float ResolveInTangent() => ResolveTangent(InTangent, InTangentSpecial);
        internal float ResolveOutTangent() => ResolveTangent(OutTangent, OutTangentSpecial);
        internal float ScaleInTangent(float multiplier) => string.IsNullOrWhiteSpace(InTangentSpecial)
            ? InTangent * multiplier
            : ResolveInTangent();
        internal float ScaleOutTangent(float multiplier) => string.IsNullOrWhiteSpace(OutTangentSpecial)
            ? OutTangent * multiplier
            : ResolveOutTangent();

        private static float CaptureTangent(float value, string path, out string special)
        {
            if (float.IsNaN(value))
                throw new JsonSerializationException("VFXCURVE001 Invalid NaN curve tangent at " + path + ".");
            if (float.IsPositiveInfinity(value))
            {
                special = "PositiveInfinity";
                return 0f;
            }
            if (float.IsNegativeInfinity(value))
            {
                special = "NegativeInfinity";
                return 0f;
            }
            special = null;
            return value;
        }

        private static float ResolveTangent(float value, string special)
        {
            return special switch
            {
                "PositiveInfinity" => float.PositiveInfinity,
                "NegativeInfinity" => float.NegativeInfinity,
                _ => value
            };
        }

        private static void EnsureFinite(float value, string path)
        {
            if (float.IsNaN(value) || float.IsInfinity(value))
                throw new JsonSerializationException("VFXCURVE001 Invalid non-finite curve value at " + path + ".");
        }
    }

    [Serializable]
    internal sealed class VfxCurveSnapshotView
    {
        [JsonProperty("preWrapMode")] public int PreWrapMode;
        [JsonProperty("postWrapMode")] public int PostWrapMode;
        [JsonProperty("keys")] public List<VfxCurveKeySnapshotView> Keys = new List<VfxCurveKeySnapshotView>();
    }

    /// <summary>Canonical bridge between Unity curves and sparse Draft v3 semantic values.</summary>
    /// <remarks>Editor-only, AllocByDesign. Preserves tangents, weights and wrap modes.</remarks>
    internal static class VfxCurveAuthoringJson
    {
        public static JToken Capture(AnimationCurve curve)
        {
            var snapshot = new VfxCurveSnapshotView();
            if (curve != null)
            {
                snapshot.PreWrapMode = (int)curve.preWrapMode;
                snapshot.PostWrapMode = (int)curve.postWrapMode;
                Keyframe[] keys = curve.keys;
                for (int i = 0; i < keys.Length; i++)
                {
                    snapshot.Keys.Add(VfxCurveKeySnapshotView.Capture(keys[i], "curve.keys[" + i + "]"));
                }
            }
            return JObject.FromObject(snapshot, JsonSerializer.Create(VfxCompositionDraftService.JsonSettings));
        }

        public static AnimationCurve Restore(JToken token, AnimationCurve fallback = null)
        {
            if (token == null || token.Type != JTokenType.Object) return Clone(fallback);
            VfxCurveSnapshotView snapshot;
            try { snapshot = token.ToObject<VfxCurveSnapshotView>(); }
            catch (JsonException) { return Clone(fallback); }
            if (snapshot?.Keys == null || snapshot.Keys.Count == 0) return Clone(fallback);
            var keys = new Keyframe[snapshot.Keys.Count];
            for (int i = 0; i < keys.Length; i++)
            {
                VfxCurveKeySnapshotView source = snapshot.Keys[i];
                keys[i] = new Keyframe(source.Time, source.Value, source.ResolveInTangent(), source.ResolveOutTangent(), source.InWeight, source.OutWeight)
                {
                    weightedMode = (WeightedMode)source.WeightedMode
                };
            }
            return new AnimationCurve(keys)
            {
                preWrapMode = (WrapMode)snapshot.PreWrapMode,
                postWrapMode = (WrapMode)snapshot.PostWrapMode
            };
        }

        public static AnimationCurve Clone(AnimationCurve source)
        {
            if (source == null) return null;
            return new AnimationCurve(source.keys) { preWrapMode = source.preWrapMode, postWrapMode = source.postWrapMode };
        }
    }

    [Serializable]
    internal sealed class VfxMinMaxCurveSnapshotView
    {
        [JsonProperty("mode")] public int Mode;
        [JsonProperty("curveMultiplier")] public float CurveMultiplier;
        [JsonProperty("constantMin")] public float ConstantMin;
        [JsonProperty("constantMax")] public float ConstantMax;
        [JsonProperty("curveMin", NullValueHandling = NullValueHandling.Ignore)] public VfxCurveSnapshotView CurveMin;
        [JsonProperty("curveMax", NullValueHandling = NullValueHandling.Ignore)] public VfxCurveSnapshotView CurveMax;
    }

    [Serializable]
    internal sealed class VfxMinMaxCurveAuthoringView
    {
        [JsonProperty("kind")] public string Kind = "MinMaxCurve";
        [JsonProperty("mode")] public int Mode;
        [JsonProperty("curveMultiplier")] public float CurveMultiplier = 1f;
        [JsonProperty("constantMin")] public float ConstantMin;
        [JsonProperty("constantMax")] public float ConstantMax;
        [JsonProperty("curveMin", NullValueHandling = NullValueHandling.Ignore)] public VfxCurveSnapshotView CurveMin;
        [JsonProperty("curveMax", NullValueHandling = NullValueHandling.Ignore)] public VfxCurveSnapshotView CurveMax;
    }

    /// <summary>Exact Draft v3 bridge for all Unity MinMaxCurve modes.</summary>
    /// <remarks>Editor-only, AllocByDesign. TwoConstants and TwoCurves are never flattened.</remarks>
    internal static class VfxMinMaxCurveAuthoringJson
    {
        public static bool IsMinMaxCurve(JToken value)
        {
            return value is JObject obj && string.Equals(obj.Value<string>("kind"), "MinMaxCurve", StringComparison.Ordinal);
        }

        public static JToken Capture(ParticleSystem.MinMaxCurve value)
        {
            return JObject.FromObject(new VfxMinMaxCurveAuthoringView
            {
                Mode = (int)value.mode,
                CurveMultiplier = value.curveMultiplier,
                ConstantMin = value.constantMin,
                ConstantMax = value.constantMax,
                CurveMin = CaptureCurve(value.curveMin),
                CurveMax = CaptureCurve(value.curveMax)
            }, JsonSerializer.Create(VfxCompositionDraftService.JsonSettings));
        }

        public static ParticleSystem.MinMaxCurve Restore(JToken value, ParticleSystem.MinMaxCurve fallback)
        {
            if (!IsMinMaxCurve(value)) return fallback;
            VfxMinMaxCurveAuthoringView data = value.ToObject<VfxMinMaxCurveAuthoringView>(JsonSerializer.Create(VfxCompositionDraftService.JsonSettings));
            if (data == null) return fallback;
            ParticleSystemCurveMode mode = Enum.IsDefined(typeof(ParticleSystemCurveMode), data.Mode)
                ? (ParticleSystemCurveMode)data.Mode
                : fallback.mode;
            switch (mode)
            {
                case ParticleSystemCurveMode.Constant:
                    return new ParticleSystem.MinMaxCurve(data.ConstantMax);
                case ParticleSystemCurveMode.TwoConstants:
                    return new ParticleSystem.MinMaxCurve(data.ConstantMin, data.ConstantMax);
                case ParticleSystemCurveMode.Curve:
                    return new ParticleSystem.MinMaxCurve(data.CurveMultiplier, RestoreCurve(data.CurveMax) ?? AnimationCurve.Constant(0f, 1f, data.ConstantMax));
                case ParticleSystemCurveMode.TwoCurves:
                    return new ParticleSystem.MinMaxCurve(
                        data.CurveMultiplier,
                        RestoreCurve(data.CurveMin) ?? AnimationCurve.Constant(0f, 1f, data.ConstantMin),
                        RestoreCurve(data.CurveMax) ?? AnimationCurve.Constant(0f, 1f, data.ConstantMax));
                default:
                    return fallback;
            }
        }

        public static ParticleSystem.MinMaxCurve Restore(VfxMinMaxCurveSnapshotView snapshot, ParticleSystem.MinMaxCurve fallback)
        {
            if (snapshot == null) return fallback;
            JObject value = JObject.FromObject(new VfxMinMaxCurveAuthoringView
            {
                Mode = snapshot.Mode,
                CurveMultiplier = snapshot.CurveMultiplier,
                ConstantMin = snapshot.ConstantMin,
                ConstantMax = snapshot.ConstantMax,
                CurveMin = snapshot.CurveMin,
                CurveMax = snapshot.CurveMax
            }, JsonSerializer.Create(VfxCompositionDraftService.JsonSettings));
            return Restore(value, fallback);
        }

        private static VfxCurveSnapshotView CaptureCurve(AnimationCurve curve)
        {
            if (curve == null) return null;
            return VfxCurveAuthoringJson.Capture(curve).ToObject<VfxCurveSnapshotView>(JsonSerializer.Create(VfxCompositionDraftService.JsonSettings));
        }

        private static AnimationCurve RestoreCurve(VfxCurveSnapshotView curve)
        {
            return curve == null
                ? null
                : VfxCurveAuthoringJson.Restore(JObject.FromObject(curve, JsonSerializer.Create(VfxCompositionDraftService.JsonSettings)));
        }
    }

    [Serializable]
    internal sealed class VfxGradientColorKeySnapshotView
    {
        [JsonProperty("time")] public float Time;
        [JsonProperty("color")] public List<float> Color = new List<float>();
    }

    [Serializable]
    internal sealed class VfxGradientAlphaKeySnapshotView
    {
        [JsonProperty("time")] public float Time;
        [JsonProperty("alpha")] public float Alpha;
    }

    [Serializable]
    internal sealed class VfxGradientSnapshotView
    {
        [JsonProperty("mode")] public int Mode;
        [JsonProperty("colorKeys")] public List<VfxGradientColorKeySnapshotView> ColorKeys = new List<VfxGradientColorKeySnapshotView>();
        [JsonProperty("alphaKeys")] public List<VfxGradientAlphaKeySnapshotView> AlphaKeys = new List<VfxGradientAlphaKeySnapshotView>();
    }

    [Serializable]
    internal sealed class VfxMinMaxGradientSnapshotView
    {
        [JsonProperty("mode")] public int Mode;
        [JsonProperty("colorMin")] public List<float> ColorMin = new List<float>();
        [JsonProperty("colorMax")] public List<float> ColorMax = new List<float>();
        [JsonProperty("gradientMin", NullValueHandling = NullValueHandling.Ignore)] public VfxGradientSnapshotView GradientMin;
        [JsonProperty("gradientMax", NullValueHandling = NullValueHandling.Ignore)] public VfxGradientSnapshotView GradientMax;
    }

    [Serializable]
    internal sealed class VfxMinMaxGradientAuthoringView
    {
        [JsonProperty("kind")] public string Kind = "MinMaxGradient";
        [JsonProperty("mode")] public int Mode;
        [JsonProperty("colorMin")] public List<float> ColorMin = new List<float>();
        [JsonProperty("colorMax")] public List<float> ColorMax = new List<float>();
        [JsonProperty("gradientMin", NullValueHandling = NullValueHandling.Ignore)] public VfxGradientSnapshotView GradientMin;
        [JsonProperty("gradientMax", NullValueHandling = NullValueHandling.Ignore)] public VfxGradientSnapshotView GradientMax;
    }

    /// <summary>Exact Draft v3 bridge for every Unity MinMaxGradient mode.</summary>
    /// <remarks>Editor-only, AllocByDesign. Color alpha keys and both random ranges are preserved.</remarks>
    internal static class VfxMinMaxGradientAuthoringJson
    {
        public static bool IsMinMaxGradient(JToken value)
        {
            return value is JObject obj && string.Equals(obj.Value<string>("kind"), "MinMaxGradient", StringComparison.Ordinal);
        }

        public static JToken Capture(ParticleSystem.MinMaxGradient value)
        {
            return JObject.FromObject(new VfxMinMaxGradientAuthoringView
            {
                Mode = (int)value.mode,
                ColorMin = CaptureColor(value.colorMin),
                ColorMax = CaptureColor(value.colorMax),
                GradientMin = Gradient(value.gradientMin),
                GradientMax = Gradient(value.gradientMax)
            }, JsonSerializer.Create(VfxCompositionDraftService.JsonSettings));
        }

        public static ParticleSystem.MinMaxGradient Restore(JToken value, ParticleSystem.MinMaxGradient fallback)
        {
            if (!IsMinMaxGradient(value)) return fallback;
            VfxMinMaxGradientAuthoringView data = value.ToObject<VfxMinMaxGradientAuthoringView>(JsonSerializer.Create(VfxCompositionDraftService.JsonSettings));
            if (data == null) return fallback;
            ParticleSystemGradientMode mode = Enum.IsDefined(typeof(ParticleSystemGradientMode), data.Mode)
                ? (ParticleSystemGradientMode)data.Mode
                : fallback.mode;
            ParticleSystem.MinMaxGradient result;
            switch (mode)
            {
                case ParticleSystemGradientMode.Color:
                    result = new ParticleSystem.MinMaxGradient(RestoreColor(data.ColorMax, Color.white));
                    break;
                case ParticleSystemGradientMode.TwoColors:
                    result = new ParticleSystem.MinMaxGradient(RestoreColor(data.ColorMin, Color.white), RestoreColor(data.ColorMax, Color.white));
                    break;
                case ParticleSystemGradientMode.TwoGradients:
                    result = new ParticleSystem.MinMaxGradient(RestoreGradient(data.GradientMin) ?? WhiteGradient(), RestoreGradient(data.GradientMax) ?? WhiteGradient());
                    break;
                case ParticleSystemGradientMode.RandomColor:
                    result = new ParticleSystem.MinMaxGradient(RestoreGradient(data.GradientMax) ?? WhiteGradient());
                    result.mode = ParticleSystemGradientMode.RandomColor;
                    break;
                default:
                    result = new ParticleSystem.MinMaxGradient(RestoreGradient(data.GradientMax) ?? WhiteGradient());
                    break;
            }
            return result;
        }

        public static ParticleSystem.MinMaxGradient Restore(VfxMinMaxGradientSnapshotView snapshot, ParticleSystem.MinMaxGradient fallback)
        {
            if (snapshot == null) return fallback;
            JObject value = JObject.FromObject(new VfxMinMaxGradientAuthoringView
            {
                Mode = snapshot.Mode,
                ColorMin = snapshot.ColorMin,
                ColorMax = snapshot.ColorMax,
                GradientMin = snapshot.GradientMin,
                GradientMax = snapshot.GradientMax
            }, JsonSerializer.Create(VfxCompositionDraftService.JsonSettings));
            return Restore(value, fallback);
        }

        private static List<float> CaptureColor(Color value) => new List<float> { value.r, value.g, value.b, value.a };

        private static Color RestoreColor(IReadOnlyList<float> value, Color fallback)
        {
            return value != null && value.Count >= 4 ? new Color(value[0], value[1], value[2], value[3]) : fallback;
        }

        private static VfxGradientSnapshotView Gradient(Gradient value)
        {
            if (value == null) return null;
            var result = new VfxGradientSnapshotView { Mode = (int)value.mode };
            foreach (GradientColorKey key in value.colorKeys)
                result.ColorKeys.Add(new VfxGradientColorKeySnapshotView { Time = key.time, Color = CaptureColor(key.color) });
            foreach (GradientAlphaKey key in value.alphaKeys)
                result.AlphaKeys.Add(new VfxGradientAlphaKeySnapshotView { Time = key.time, Alpha = key.alpha });
            return result;
        }

        private static Gradient RestoreGradient(VfxGradientSnapshotView value)
        {
            if (value == null || value.ColorKeys.Count == 0 || value.AlphaKeys.Count == 0) return null;
            var gradient = new Gradient { mode = (GradientMode)value.Mode };
            gradient.SetKeys(
                value.ColorKeys.Select(key => new GradientColorKey(RestoreColor(key.Color, Color.white), key.Time)).ToArray(),
                value.AlphaKeys.Select(key => new GradientAlphaKey(key.Alpha, key.Time)).ToArray());
            return gradient;
        }

        private static Gradient WhiteGradient()
        {
            var value = new Gradient();
            value.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) });
            return value;
        }
    }

    [Serializable]
    internal sealed class VfxCustomDataStreamSnapshotView
    {
        [JsonProperty("stream")] public int Stream;
        [JsonProperty("mode")] public int Mode;
        [JsonProperty("color", NullValueHandling = NullValueHandling.Ignore)] public VfxMinMaxGradientSnapshotView Color;
        [JsonProperty("vector")] public List<VfxMinMaxCurveSnapshotView> Vector = new List<VfxMinMaxCurveSnapshotView>();
    }

    [Serializable]
    internal sealed class VfxParticleSystemSnapshotView
    {
        [JsonProperty("componentId")] public string ComponentId = string.Empty;
        [JsonProperty("sourcePrefabGuid")] public string SourcePrefabGuid = string.Empty;
        [JsonProperty("sourceContentHash")] public string SourceContentHash = string.Empty;
        [JsonProperty("localFileId")] public long LocalFileId;
        [JsonProperty("transformPath")] public string TransformPath = string.Empty;
        [JsonProperty("localPosition")] public List<float> LocalPosition = new List<float>();
        [JsonProperty("localEulerAngles")] public List<float> LocalEulerAngles = new List<float>();
        [JsonProperty("localScale")] public List<float> LocalScale = new List<float>();
        [JsonProperty("durationSeconds")] public float DurationSeconds;
        [JsonProperty("loop")] public bool Loop;
        [JsonProperty("prewarm")] public bool Prewarm;
        [JsonProperty("simulationSpeed")] public float SimulationSpeed;
        [JsonProperty("simulationSpace")] public int SimulationSpace;
        [JsonProperty("scalingMode")] public int ScalingMode;
        [JsonProperty("maxParticles")] public int MaxParticles;
        [JsonProperty("startDelay")] public VfxMinMaxCurveSnapshotView StartDelay;
        [JsonProperty("startLifetime")] public VfxMinMaxCurveSnapshotView StartLifetime;
        [JsonProperty("startSpeed")] public VfxMinMaxCurveSnapshotView StartSpeed;
        [JsonProperty("startSize3D")] public bool StartSize3D;
        [JsonProperty("startSize")] public VfxMinMaxCurveSnapshotView StartSize;
        [JsonProperty("startSizeX")] public VfxMinMaxCurveSnapshotView StartSizeX;
        [JsonProperty("startSizeY")] public VfxMinMaxCurveSnapshotView StartSizeY;
        [JsonProperty("startSizeZ")] public VfxMinMaxCurveSnapshotView StartSizeZ;
        [JsonProperty("startRotation3D")] public bool StartRotation3D;
        [JsonProperty("startRotation")] public VfxMinMaxCurveSnapshotView StartRotation;
        [JsonProperty("startRotationX")] public VfxMinMaxCurveSnapshotView StartRotationX;
        [JsonProperty("startRotationY")] public VfxMinMaxCurveSnapshotView StartRotationY;
        [JsonProperty("startRotationZ")] public VfxMinMaxCurveSnapshotView StartRotationZ;
        [JsonProperty("startColor")] public VfxMinMaxGradientSnapshotView StartColor;
        [JsonProperty("gravityModifier")] public VfxMinMaxCurveSnapshotView GravityModifier;
        [JsonProperty("rateOverTime")] public VfxMinMaxCurveSnapshotView RateOverTime;
        [JsonProperty("rateOverDistance")] public VfxMinMaxCurveSnapshotView RateOverDistance;
        [JsonProperty("shapeType")] public int ShapeType;
        [JsonProperty("shapeScale")] public List<float> ShapeScale = new List<float>();
        [JsonProperty("shapeRadius")] public float ShapeRadius;
        [JsonProperty("shapeAngle")] public float ShapeAngle;
        [JsonProperty("shapeArc")] public float ShapeArc;
        [JsonProperty("shapeRandomDirection")] public float ShapeRandomDirection;
        [JsonProperty("colorOverLifetime")] public VfxMinMaxGradientSnapshotView ColorOverLifetime;
        [JsonProperty("sizeOverLifetimeX")] public VfxMinMaxCurveSnapshotView SizeOverLifetimeX;
        [JsonProperty("sizeOverLifetimeY")] public VfxMinMaxCurveSnapshotView SizeOverLifetimeY;
        [JsonProperty("sizeOverLifetimeZ")] public VfxMinMaxCurveSnapshotView SizeOverLifetimeZ;
        [JsonProperty("velocityOverLifetimeX")] public VfxMinMaxCurveSnapshotView VelocityOverLifetimeX;
        [JsonProperty("velocityOverLifetimeY")] public VfxMinMaxCurveSnapshotView VelocityOverLifetimeY;
        [JsonProperty("velocityOverLifetimeZ")] public VfxMinMaxCurveSnapshotView VelocityOverLifetimeZ;
        [JsonProperty("velocitySpeedModifier")] public VfxMinMaxCurveSnapshotView VelocitySpeedModifier;
        [JsonProperty("velocitySpace")] public int VelocitySpace;
        [JsonProperty("forceOverLifetimeX")] public VfxMinMaxCurveSnapshotView ForceOverLifetimeX;
        [JsonProperty("forceOverLifetimeY")] public VfxMinMaxCurveSnapshotView ForceOverLifetimeY;
        [JsonProperty("forceOverLifetimeZ")] public VfxMinMaxCurveSnapshotView ForceOverLifetimeZ;
        [JsonProperty("forceSpace")] public int ForceSpace;
        [JsonProperty("rotationOverLifetimeX")] public VfxMinMaxCurveSnapshotView RotationOverLifetimeX;
        [JsonProperty("rotationOverLifetimeY")] public VfxMinMaxCurveSnapshotView RotationOverLifetimeY;
        [JsonProperty("rotationOverLifetimeZ")] public VfxMinMaxCurveSnapshotView RotationOverLifetimeZ;
        [JsonProperty("limitVelocity")] public VfxMinMaxCurveSnapshotView LimitVelocity;
        [JsonProperty("drag")] public VfxMinMaxCurveSnapshotView Drag;
        [JsonProperty("noiseStrength")] public VfxMinMaxCurveSnapshotView NoiseStrength;
        [JsonProperty("noiseFrequency")] public float NoiseFrequency;
        [JsonProperty("noiseScrollSpeed")] public VfxMinMaxCurveSnapshotView NoiseScrollSpeed;
        [JsonProperty("textureFrameOverTime")] public VfxMinMaxCurveSnapshotView TextureFrameOverTime;
        [JsonProperty("textureStartFrame")] public VfxMinMaxCurveSnapshotView TextureStartFrame;
        [JsonProperty("textureCycleCount")] public int TextureCycleCount;
        [JsonProperty("textureTiles")] public List<int> TextureTiles = new List<int>();
        [JsonProperty("trails", NullValueHandling = NullValueHandling.Ignore)] public VfxParticleTrailSnapshotView Trails;
        [JsonProperty("customData")] public List<VfxCustomDataStreamSnapshotView> CustomData = new List<VfxCustomDataStreamSnapshotView>();
        [JsonProperty("moduleFlags")] public List<string> ModuleFlags = new List<string>();
        [JsonProperty("rendererMode")] public int RendererMode;
        [JsonProperty("rendererAlignment")] public int RendererAlignment;
        [JsonProperty("rendererSortMode")] public int RendererSortMode;
        [JsonProperty("sortingOrder")] public int SortingOrder;
        [JsonProperty("rendererPivot")] public List<float> RendererPivot = new List<float>();
        [JsonProperty("rendererMinParticleSize")] public float RendererMinParticleSize;
        [JsonProperty("rendererMaxParticleSize")] public float RendererMaxParticleSize;
        [JsonProperty("vertexStreams")] public List<int> VertexStreams = new List<int>();
        [JsonProperty("materialGuids")] public List<string> MaterialGuids = new List<string>();
        [JsonProperty("meshGuid")] public string MeshGuid = string.Empty;
        [JsonProperty("rawEditorJson")] public string RawEditorJson = string.Empty;
        [JsonProperty("snapshotHash")] public string SnapshotHash = string.Empty;
    }

    [Serializable]
    internal sealed class VfxParticleTrailSnapshotView
    {
        [JsonProperty("enabled")] public bool Enabled;
        [JsonProperty("ratio")] public float Ratio;
        [JsonProperty("lifetime")] public VfxMinMaxCurveSnapshotView Lifetime;
        [JsonProperty("minimumVertexDistance")] public float MinimumVertexDistance;
        [JsonProperty("textureMode")] public int TextureMode;
        [JsonProperty("worldSpace")] public bool WorldSpace;
        [JsonProperty("dieWithParticles")] public bool DieWithParticles;
        [JsonProperty("sizeAffectsWidth")] public bool SizeAffectsWidth;
        [JsonProperty("sizeAffectsLifetime")] public bool SizeAffectsLifetime;
        [JsonProperty("inheritParticleColor")] public bool InheritParticleColor;
        [JsonProperty("colorOverLifetime")] public VfxMinMaxGradientSnapshotView ColorOverLifetime;
        [JsonProperty("widthOverTrail")] public VfxMinMaxCurveSnapshotView WidthOverTrail;
        [JsonProperty("generateLightingData")] public bool GenerateLightingData;
        [JsonProperty("ribbonCount")] public int RibbonCount;
        [JsonProperty("shadowBias")] public float ShadowBias;
        [JsonProperty("splitSubEmitterRibbons")] public bool SplitSubEmitterRibbons;
        [JsonProperty("attachRibbonsToTransform")] public bool AttachRibbonsToTransform;
        [JsonProperty("contentHash")] public string ContentHash = string.Empty;
    }

    [Serializable]
    internal sealed class VfxSnapshotManifestEntryView
    {
        [JsonProperty("componentId")] public string ComponentId = string.Empty;
        [JsonProperty("sourceHash")] public string SourceHash = string.Empty;
        [JsonProperty("blobHash")] public string BlobHash = string.Empty;
        [JsonProperty("schemaVersion")] public string SchemaVersion = "mx.vfx.source-snapshot.v2";
        [JsonProperty("moduleFlags")] public List<string> ModuleFlags = new List<string>();
        [JsonProperty("uncompressedBytes")] public int UncompressedBytes;
    }

    [Serializable]
    internal sealed class VfxSnapshotManifestView
    {
        [JsonProperty("schemaVersion")] public string SchemaVersion = "mx.vfx.source-snapshot-manifest.v2";
        [JsonProperty("entries")] public List<VfxSnapshotManifestEntryView> Entries = new List<VfxSnapshotManifestEntryView>();
        [JsonProperty("manifestHash")] public string ManifestHash = string.Empty;
    }

    [Serializable]
    internal sealed class VfxSemanticDiagnosticView
    {
        [JsonProperty("code")] public string Code = string.Empty;
        [JsonProperty("severity")] public string Severity = "Error";
        [JsonProperty("message")] public string Message = string.Empty;
        [JsonProperty("memberId", NullValueHandling = NullValueHandling.Ignore)] public string MemberId;
        [JsonProperty("propertyId", NullValueHandling = NullValueHandling.Ignore)] public string PropertyId;
        [JsonProperty("fixAction", NullValueHandling = NullValueHandling.Ignore)] public string FixAction;
    }

    [Serializable]
    internal sealed class VfxUnityFieldDiffView
    {
        [JsonProperty("memberId")] public string MemberId = string.Empty;
        [JsonProperty("propertyId")] public string PropertyId = string.Empty;
        [JsonProperty("adapter")] public string Adapter = string.Empty;
        [JsonProperty("unityPath")] public string UnityPath = string.Empty;
        [JsonProperty("sourceValue")] public string SourceValue = string.Empty;
        [JsonProperty("operation")] public string Operation = string.Empty;
        [JsonProperty("semanticValue")] public string SemanticValue = string.Empty;
        [JsonProperty("finalValue")] public string FinalValue = string.Empty;
        [JsonProperty("timeDomain")] public string TimeDomain = string.Empty;
    }

    [Serializable]
    internal sealed class VfxCompiledEventView
    {
        [JsonProperty("eventId")] public string EventId = string.Empty;
        [JsonProperty("sourceFrame")] public int SourceFrame;
        [JsonProperty("targetFrame")] public int TargetFrame;
        [JsonProperty("anchor")] public string Anchor = string.Empty;
    }

    [Serializable]
    internal sealed class VfxCompiledLayerView
    {
        [JsonProperty("layerId")] public string LayerId = string.Empty;
        [JsonProperty("startFrame")] public int StartFrame;
        [JsonProperty("sourceLeadInFrames")] public int SourceLeadInFrames;
        [JsonProperty("resolvedLeadInFrames")] public int ResolvedLeadInFrames;
        [JsonProperty("visualStartMode")] public string VisualStartMode = nameof(VfxVisualStartMode.AutoNormalize);
        [JsonProperty("sourceSegments")] public VfxTimeSegmentsView SourceSegments = new VfxTimeSegmentsView();
        [JsonProperty("targetSegments")] public VfxTimeSegmentsView TargetSegments = new VfxTimeSegmentsView();
        [JsonProperty("timeMap")] public List<VfxRetimeMapPointView> TimeMap = new List<VfxRetimeMapPointView>();
        [JsonProperty("events")] public List<VfxCompiledEventView> Events = new List<VfxCompiledEventView>();
        [JsonProperty("fieldDiffs")] public List<VfxUnityFieldDiffView> FieldDiffs = new List<VfxUnityFieldDiffView>();
        [JsonProperty("diagnostics")] public List<VfxSemanticDiagnosticView> Diagnostics = new List<VfxSemanticDiagnosticView>();
        [JsonProperty("experimental")] public bool Experimental;
        [JsonProperty("compileHash")] public string CompileHash = string.Empty;
        [JsonProperty("playbackWindowFrames", DefaultValueHandling = DefaultValueHandling.Ignore)] public int PlaybackWindowFrames;

        [JsonIgnore] public int ArtisticEndFrame => StartFrame + (PlaybackWindowFrames > 0 ? PlaybackWindowFrames : TargetSegments.ArtisticFrames);
        [JsonIgnore] public int TechnicalEndFrame => StartFrame + (PlaybackWindowFrames > 0 ? PlaybackWindowFrames + TargetSegments.CleanupFrames : TargetSegments.TechnicalFrames);
        [JsonIgnore] public bool Valid => Diagnostics.All(value => !string.Equals(value.Severity, "Error", StringComparison.Ordinal));
    }

    internal static class VfxCanonicalJson
    {
        public static string Serialize(object value, params string[] excludedProperties)
        {
            JToken token = value is JToken source ? source.DeepClone() : JToken.FromObject(value, JsonSerializer.Create(VfxCompositionDraftService.JsonSettings));
            if (excludedProperties != null && excludedProperties.Length > 0)
                RemoveProperties(token, new HashSet<string>(excludedProperties, StringComparer.Ordinal));
            return Sort(token, "$").ToString(Formatting.None);
        }

        public static string Hash(object value, params string[] excludedProperties)
        {
            return HashUtf8(Serialize(value, excludedProperties));
        }

        public static string HashUtf8(string value)
        {
            using SHA256 sha = SHA256.Create();
            byte[] bytes = Encoding.UTF8.GetBytes(value ?? string.Empty);
            return "sha256:" + BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", string.Empty).ToLowerInvariant();
        }

        private static void RemoveProperties(JToken token, HashSet<string> names)
        {
            if (token is JObject obj)
            {
                foreach (JProperty property in obj.Properties().ToArray())
                {
                    if (names.Contains(property.Name)) property.Remove();
                    else RemoveProperties(property.Value, names);
                }
            }
            else if (token is JArray array)
            {
                for (int i = 0; i < array.Count; i++) RemoveProperties(array[i], names);
            }
        }

        private static JToken Sort(JToken token, string path)
        {
            if (token is JObject obj)
            {
                var result = new JObject();
                foreach (JProperty property in obj.Properties().OrderBy(value => value.Name, StringComparer.Ordinal))
                    result.Add(property.Name, Sort(property.Value, path + "." + property.Name));
                return result;
            }
            if (token is JArray array)
            {
                var result = new JArray();
                for (int i = 0; i < array.Count; i++) result.Add(Sort(array[i], path + "[" + i + "]"));
                return result;
            }
            if (token is JValue number && (number.Type == JTokenType.Float || number.Type == JTokenType.Integer))
            {
                if (number.Type == JTokenType.Integer) return new JValue(number.Value<long>());
                double value = number.Value<double>();
                if (double.IsNaN(value) || double.IsInfinity(value))
                    throw new JsonSerializationException("Canonical JSON does not allow NaN or Infinity at " + path + ".");
                return new JValue(double.Parse(value.ToString("R", CultureInfo.InvariantCulture), CultureInfo.InvariantCulture));
            }
            return token.DeepClone();
        }
    }

    internal sealed class VfxSemanticPropertyRegistryService
    {
        internal const string RegistryPath = "Assets/Config/MxFramework/Vfx/AuthoringProfiles/vfx_property_registry.json";

        public VfxSemanticPropertyRegistryView Load(out string message)
        {
            if (!File.Exists(RegistryPath))
            {
                message = "VFX Property Registry 不存在：" + RegistryPath;
                return null;
            }
            try
            {
                var result = JsonConvert.DeserializeObject<VfxSemanticPropertyRegistryView>(File.ReadAllText(RegistryPath), VfxCompositionDraftService.JsonSettings);
                string expected = result?.RegistryHash ?? string.Empty;
                if (result == null)
                {
                    message = "VFX Property Registry 内容为空。";
                    return null;
                }
                result.RegistryHash = string.Empty;
                string actual = VfxCanonicalJson.Hash(result, "registryHash");
                result.RegistryHash = expected;
                if (!string.IsNullOrWhiteSpace(expected) && !string.Equals(expected, actual, StringComparison.Ordinal))
                {
                    message = "VFX Property Registry hash 不匹配。";
                    return null;
                }
                if (string.IsNullOrWhiteSpace(result.RegistryHash)) result.RegistryHash = actual;
                message = "已加载 " + result.Properties.Count + " 个受控 VFX 属性。";
                return result;
            }
            catch (Exception exception) when (exception is IOException || exception is JsonException)
            {
                message = "VFX Property Registry 读取失败：" + exception.Message;
                return null;
            }
        }
    }

    [Serializable]
    internal sealed class VfxSemanticRetimeFixtureCatalogView
    {
        [JsonProperty("schemaVersion")] public string SchemaVersion = "mx.vfx.semantic-retime-fixtures.v1";
        [JsonProperty("catalogId")] public string CatalogId = "mx.vfx.semantic-retime-fixtures";
        [JsonProperty("frameRate")] public int FrameRate = 60;
        [JsonProperty("seed")] public int Seed = 17;
        [JsonProperty("fixtures")] public List<VfxSemanticRetimeFixtureView> Fixtures = new List<VfxSemanticRetimeFixtureView>();
        [JsonProperty("catalogHash")] public string CatalogHash = string.Empty;
    }

    [Serializable]
    internal sealed class VfxSemanticRetimeFixtureView
    {
        [JsonProperty("fixtureId")] public string FixtureId = string.Empty;
        [JsonProperty("displayName")] public string DisplayName = string.Empty;
        [JsonProperty("purpose")] public string Purpose = string.Empty;
        [JsonProperty("retimeMode")] public string RetimeMode = nameof(VfxRetimeMode.ExtendBody);
        [JsonProperty("sourceSegments")] public VfxTimeSegmentsView SourceSegments = new VfxTimeSegmentsView();
        [JsonProperty("targetArtisticFrames")] public int TargetArtisticFrames;
        [JsonProperty("cycleFrames")] public int CycleFrames;
        [JsonProperty("requiredPropertyIds")] public List<string> RequiredPropertyIds = new List<string>();
        [JsonProperty("requiredAdapters")] public List<string> RequiredAdapters = new List<string>();
        [JsonProperty("invariants")] public List<string> Invariants = new List<string>();
    }

    internal sealed class VfxSemanticRetimeFixtureCatalogService
    {
        internal const string CatalogPath = "Assets/Config/MxFramework/Vfx/AuthoringProfiles/vfx_semantic_retime_fixtures.json";

        public VfxSemanticRetimeFixtureCatalogView Load(out string message)
        {
            if (!File.Exists(CatalogPath))
            {
                message = "VFX 语义重定时 Fixture 目录不存在：" + CatalogPath;
                return null;
            }
            try
            {
                VfxSemanticRetimeFixtureCatalogView result = JsonConvert.DeserializeObject<VfxSemanticRetimeFixtureCatalogView>(
                    File.ReadAllText(CatalogPath),
                    VfxCompositionDraftService.JsonSettings);
                if (result == null || result.Fixtures.Count == 0)
                {
                    message = "VFX 语义重定时 Fixture 目录为空。";
                    return null;
                }
                string expected = result.CatalogHash;
                string actual = VfxCanonicalJson.Hash(result, "catalogHash");
                if (!string.IsNullOrWhiteSpace(expected) && !string.Equals(expected, actual, StringComparison.Ordinal))
                {
                    message = "VFX 语义重定时 Fixture catalog hash 不匹配。";
                    return null;
                }
                result.CatalogHash = string.IsNullOrWhiteSpace(expected) ? actual : expected;
                message = "已加载 " + result.Fixtures.Count + " 个 VFX 语义重定时 Fixture。";
                return result;
            }
            catch (Exception exception) when (exception is IOException || exception is JsonException)
            {
                message = "VFX 语义重定时 Fixture 读取失败：" + exception.Message;
                return null;
            }
        }
    }

    internal sealed class VfxSemanticProfileStore
    {
        internal const string Root = "Assets/Config/MxFramework/Vfx/AuthoringProfiles";
        internal const string ManifestPath = Root + "/vfx_profile_manifest.json";

        public VfxProfileManifestView LoadManifest(out string message)
        {
            if (!File.Exists(ManifestPath))
            {
                message = "Profile Manifest 不存在。";
                return new VfxProfileManifestView();
            }
            try
            {
                VfxProfileManifestView result = JsonConvert.DeserializeObject<VfxProfileManifestView>(File.ReadAllText(ManifestPath), VfxCompositionDraftService.JsonSettings) ?? new VfxProfileManifestView();
                string expected = result.ManifestHash;
                string actual = VfxCanonicalJson.Hash(result, "manifestHash");
                if (!string.IsNullOrWhiteSpace(expected) && !string.Equals(expected, actual, StringComparison.Ordinal))
                {
                    message = "Profile Manifest hash 不匹配。";
                    return null;
                }
                result.ManifestHash = string.IsNullOrWhiteSpace(expected) ? actual : expected;
                message = "已加载 " + result.Profiles.Count + " 个 VFX Authoring Profile。";
                return result;
            }
            catch (Exception exception) when (exception is IOException || exception is JsonException)
            {
                message = "Profile Manifest 读取失败：" + exception.Message;
                return null;
            }
        }

        public T Load<T>(VfxProfileManifestView manifest, string profileId, out string message) where T : class
        {
            VfxProfileManifestEntryView entry = manifest?.Profiles.FirstOrDefault(value => value.ProfileId == profileId);
            if (entry == null || !File.Exists(entry.Path))
            {
                message = "Profile 不存在：" + profileId;
                return null;
            }
            try
            {
                string json = File.ReadAllText(entry.Path);
                T result = JsonConvert.DeserializeObject<T>(json, VfxCompositionDraftService.JsonSettings);
                string actual = VfxCanonicalJson.Hash(JObject.Parse(json), "contentHash");
                if (!string.IsNullOrWhiteSpace(entry.ContentHash) && !string.Equals(entry.ContentHash, actual, StringComparison.Ordinal))
                {
                    message = "Profile 已变化或被篡改：" + profileId;
                    return null;
                }
                message = "Profile 已加载。";
                return result;
            }
            catch (Exception exception) when (exception is IOException || exception is JsonException)
            {
                message = "Profile 读取失败：" + exception.Message;
                return null;
            }
        }

        public bool SaveGuarded<T>(
            T profile,
            string profileId,
            string kind,
            string relativePath,
            string expectedManifestHash,
            IReadOnlyList<string> sourceDependencies,
            string reviewState,
            out string message)
        {
            VfxProfileManifestView manifest = LoadManifest(out message);
            if (manifest == null) return false;
            if (!string.IsNullOrWhiteSpace(expectedManifestHash) && !string.Equals(expectedManifestHash, manifest.ManifestHash, StringComparison.Ordinal))
            {
                message = "Profile Manifest 已变化；请刷新影响分析后重试。";
                return false;
            }

            string path = Root + "/" + relativePath.Replace('\\', '/').TrimStart('/');
            Directory.CreateDirectory(Path.GetDirectoryName(path) ?? Root);
            JObject profileObject = JObject.FromObject(profile, JsonSerializer.Create(VfxCompositionDraftService.JsonSettings));
            profileObject["profileId"] = profileId;
            profileObject["contentHash"] = string.Empty;
            string contentHash = VfxCanonicalJson.Hash(profileObject, "contentHash");
            profileObject["contentHash"] = contentHash;
            string profileJson = profileObject.ToString(Formatting.Indented) + Environment.NewLine;

            VfxProfileManifestEntryView entry = manifest.Profiles.FirstOrDefault(value => value.ProfileId == profileId);
            if (entry == null)
            {
                entry = new VfxProfileManifestEntryView { ProfileId = profileId };
                manifest.Profiles.Add(entry);
            }
            entry.Kind = kind;
            entry.Path = path;
            entry.SchemaVersion = profileObject.Value<string>("schemaVersion") ?? string.Empty;
            entry.ContentHash = contentHash;
            entry.SourceDependencies = sourceDependencies?.OrderBy(value => value, StringComparer.Ordinal).ToList() ?? new List<string>();
            entry.ReviewState = reviewState;
            manifest.Profiles = manifest.Profiles.OrderBy(value => value.ProfileId, StringComparer.Ordinal).ToList();
            manifest.ManifestHash = string.Empty;
            manifest.ManifestHash = VfxCanonicalJson.Hash(manifest, "manifestHash");
            string manifestJson = JsonConvert.SerializeObject(manifest, Formatting.Indented) + Environment.NewLine;

            string profileTemporary = path + ".tmp";
            string manifestTemporary = ManifestPath + ".tmp";
            string profileBackup = path + ".bak";
            string manifestBackup = ManifestPath + ".bak";
            try
            {
                if (File.Exists(path)) File.Copy(path, profileBackup, true);
                if (File.Exists(ManifestPath)) File.Copy(ManifestPath, manifestBackup, true);
                File.WriteAllText(profileTemporary, profileJson, new UTF8Encoding(false));
                File.WriteAllText(manifestTemporary, manifestJson, new UTF8Encoding(false));
                File.Copy(profileTemporary, path, true);
                File.Copy(manifestTemporary, ManifestPath, true);
                File.Delete(profileTemporary);
                File.Delete(manifestTemporary);
                if (File.Exists(profileBackup)) File.Delete(profileBackup);
                if (File.Exists(manifestBackup)) File.Delete(manifestBackup);
                message = "Profile 与 Manifest 已通过 expected-hash 事务保存：" + profileId;
                return true;
            }
            catch (IOException exception)
            {
                if (File.Exists(profileBackup)) File.Copy(profileBackup, path, true);
                if (File.Exists(manifestBackup)) File.Copy(manifestBackup, ManifestPath, true);
                message = "Profile 保存失败，已回滚：" + exception.Message;
                return false;
            }
            finally
            {
                SafeDelete(profileTemporary);
                SafeDelete(manifestTemporary);
                SafeDelete(profileBackup);
                SafeDelete(manifestBackup);
            }
        }

        /// <summary>Saves every profile and the manifest as one guarded file transaction.</summary>
        /// <remarks>Editor-only, AllocByDesign. All touched bytes are restored when any write fails.</remarks>
        public bool SaveBatchGuarded(
            IReadOnlyList<VfxProfileBatchWriteView> writes,
            string expectedManifestHash,
            out VfxProfileManifestView savedManifest,
            out string message)
        {
            savedManifest = null;
            if (writes == null || writes.Count == 0)
            {
                message = "Profile 批次为空。";
                return false;
            }
            VfxProfileManifestView manifest = LoadManifest(out message);
            if (manifest == null) return false;
            if (!string.IsNullOrWhiteSpace(expectedManifestHash)
                && !string.Equals(expectedManifestHash, manifest.ManifestHash, StringComparison.Ordinal))
            {
                message = "Profile Manifest 已变化；请刷新影响分析后重试。";
                return false;
            }

            var finalBytes = new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase);
            var touched = new List<string>();
            for (int i = 0; i < writes.Count; i++)
            {
                VfxProfileBatchWriteView write = writes[i];
                if (write?.Profile == null || string.IsNullOrWhiteSpace(write.ProfileId))
                {
                    message = "Profile 批次包含空对象或空 ID。";
                    return false;
                }
                string path = Root + "/" + (write.RelativePath ?? string.Empty).Replace('\\', '/').TrimStart('/');
                JObject profileObject = JObject.FromObject(write.Profile, JsonSerializer.Create(VfxCompositionDraftService.JsonSettings));
                profileObject["profileId"] = write.ProfileId;
                profileObject["contentHash"] = string.Empty;
                string contentHash = VfxCanonicalJson.Hash(profileObject, "contentHash");
                profileObject["contentHash"] = contentHash;
                finalBytes[path] = new UTF8Encoding(false).GetBytes(profileObject.ToString(Formatting.Indented) + Environment.NewLine);
                touched.Add(path);

                VfxProfileManifestEntryView entry = manifest.Profiles.FirstOrDefault(value => value.ProfileId == write.ProfileId);
                if (entry == null)
                {
                    entry = new VfxProfileManifestEntryView { ProfileId = write.ProfileId };
                    manifest.Profiles.Add(entry);
                }
                entry.Kind = write.Kind;
                entry.Path = path;
                entry.SchemaVersion = profileObject.Value<string>("schemaVersion") ?? string.Empty;
                entry.ContentHash = contentHash;
                entry.SourceDependencies = write.SourceDependencies?.Where(value => !string.IsNullOrWhiteSpace(value)).Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal).ToList()
                    ?? new List<string>();
                entry.ReviewState = write.ReviewState;
            }
            if (touched.Distinct(StringComparer.OrdinalIgnoreCase).Count() != touched.Count)
            {
                message = "Profile 批次包含重复目标路径。";
                return false;
            }
            manifest.Profiles = manifest.Profiles.OrderBy(value => value.ProfileId, StringComparer.Ordinal).ToList();
            manifest.ManifestHash = string.Empty;
            manifest.ManifestHash = VfxCanonicalJson.Hash(manifest, "manifestHash");
            finalBytes[ManifestPath] = new UTF8Encoding(false).GetBytes(JsonConvert.SerializeObject(manifest, Formatting.Indented) + Environment.NewLine);

            string transactionId = Guid.NewGuid().ToString("N");
            string transactionRoot = "Temp/MxFramework/VfxSemantic/ProfileTransactions/" + transactionId;
            Directory.CreateDirectory(transactionRoot);
            var originals = new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase);
            var didExist = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            try
            {
                foreach (KeyValuePair<string, byte[]> pair in finalBytes)
                {
                    string directory = Path.GetDirectoryName(pair.Key);
                    if (!string.IsNullOrWhiteSpace(directory)) Directory.CreateDirectory(directory);
                    if (File.Exists(pair.Key))
                    {
                        didExist.Add(pair.Key);
                        originals[pair.Key] = File.ReadAllBytes(pair.Key);
                    }
                    string temporary = transactionRoot + "/" + VfxCanonicalJson.HashUtf8(pair.Key).Substring("sha256:".Length) + ".tmp";
                    File.WriteAllBytes(temporary, pair.Value);
                }
                foreach (KeyValuePair<string, byte[]> pair in finalBytes)
                {
                    string temporary = transactionRoot + "/" + VfxCanonicalJson.HashUtf8(pair.Key).Substring("sha256:".Length) + ".tmp";
                    File.Copy(temporary, pair.Key, true);
                }
                savedManifest = manifest;
                message = "已事务保存 " + writes.Count + " 个 Profile 与 Manifest。";
                return true;
            }
            catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException)
            {
                foreach (string path in finalBytes.Keys)
                {
                    try
                    {
                        if (didExist.Contains(path)) File.WriteAllBytes(path, originals[path]);
                        else if (File.Exists(path)) File.Delete(path);
                    }
                    catch (Exception rollbackException) when (rollbackException is IOException || rollbackException is UnauthorizedAccessException)
                    {
                        Debug.LogError("VFX Profile 回滚失败：" + path + " · " + rollbackException.Message);
                    }
                }
                message = "Profile 批次保存失败，已恢复原始字节：" + exception.Message;
                return false;
            }
            finally
            {
                try { if (Directory.Exists(transactionRoot)) Directory.Delete(transactionRoot, true); }
                catch (IOException) { }
            }
        }

        private static void SafeDelete(string path)
        {
            try { if (File.Exists(path)) File.Delete(path); }
            catch (IOException) { }
        }
    }

    internal sealed class VfxSourceSnapshotStore
    {
        internal const string SnapshotSchemaVersion = "mx.vfx.source-snapshot.v2";
        internal const string Root = "Temp/MxFramework/VfxSemantic/Snapshots";
        internal const string ManifestPath = Root + "/manifest.json";

        public VfxSnapshotManifestView LoadManifest()
        {
            if (!File.Exists(ManifestPath)) return new VfxSnapshotManifestView();
            try
            {
                return JsonConvert.DeserializeObject<VfxSnapshotManifestView>(File.ReadAllText(ManifestPath), VfxCompositionDraftService.JsonSettings) ?? new VfxSnapshotManifestView();
            }
            catch (Exception exception) when (exception is IOException || exception is JsonException)
            {
                Debug.LogWarning("VFX Snapshot Manifest 已损坏，将在需要时重建：" + exception.Message);
                return new VfxSnapshotManifestView();
            }
        }

        public bool Put(VfxParticleSystemSnapshotView snapshot, out string hash, out string message)
        {
            hash = string.Empty;
            if (snapshot == null || string.IsNullOrWhiteSpace(snapshot.ComponentId))
            {
                message = "Snapshot 必须包含 componentId。";
                return false;
            }
            Directory.CreateDirectory(Root + "/blobs");
            snapshot.SnapshotHash = string.Empty;
            string canonical = VfxCanonicalJson.Serialize(snapshot, "snapshotHash");
            hash = VfxCanonicalJson.HashUtf8(canonical);
            snapshot.SnapshotHash = hash;
            byte[] bytes = Encoding.UTF8.GetBytes(VfxCanonicalJson.Serialize(snapshot));
            string token = hash.Substring("sha256:".Length);
            string blobPath = Root + "/blobs/" + token + ".json.gz";
            if (!File.Exists(blobPath))
            {
                using FileStream stream = File.Create(blobPath);
                using var gzip = new GZipStream(stream, System.IO.Compression.CompressionLevel.Optimal);
                gzip.Write(bytes, 0, bytes.Length);
            }

            VfxSnapshotManifestView manifest = LoadManifest();
            VfxSnapshotManifestEntryView entry = manifest.Entries.FirstOrDefault(value => value.ComponentId == snapshot.ComponentId);
            if (entry == null)
            {
                entry = new VfxSnapshotManifestEntryView { ComponentId = snapshot.ComponentId };
                manifest.Entries.Add(entry);
            }
            entry.SourceHash = snapshot.SourceContentHash;
            entry.BlobHash = hash;
            entry.SchemaVersion = SnapshotSchemaVersion;
            entry.ModuleFlags = snapshot.ModuleFlags.OrderBy(value => value, StringComparer.Ordinal).ToList();
            entry.UncompressedBytes = bytes.Length;
            manifest.Entries = manifest.Entries.OrderBy(value => value.ComponentId, StringComparer.Ordinal).ToList();
            manifest.ManifestHash = string.Empty;
            manifest.ManifestHash = VfxCanonicalJson.Hash(manifest, "manifestHash");
            Directory.CreateDirectory(Root);
            File.WriteAllText(ManifestPath, JsonConvert.SerializeObject(manifest, Formatting.Indented) + Environment.NewLine, new UTF8Encoding(false));
            message = "Snapshot 已写入内容寻址缓存。";
            return true;
        }

        public VfxParticleSystemSnapshotView Get(string componentId, string expectedSourceHash, out string message)
        {
            VfxSnapshotManifestEntryView entry = LoadManifest().Entries.FirstOrDefault(value => value.ComponentId == componentId);
            if (entry == null)
            {
                message = "Snapshot 尚未生成。";
                return null;
            }
            if (!string.Equals(entry.SchemaVersion, SnapshotSchemaVersion, StringComparison.Ordinal))
            {
                message = "Snapshot schema 已过期，将按 v2 完整模块合同重建。";
                return null;
            }
            if (!string.IsNullOrWhiteSpace(expectedSourceHash) && !string.Equals(entry.SourceHash, expectedSourceHash, StringComparison.Ordinal))
            {
                message = "Snapshot source hash 已过期。";
                return null;
            }
            string path = Root + "/blobs/" + entry.BlobHash.Substring("sha256:".Length) + ".json.gz";
            if (!File.Exists(path))
            {
                message = "Snapshot blob 缺失。";
                return null;
            }
            try
            {
                using FileStream stream = File.OpenRead(path);
                using var gzip = new GZipStream(stream, CompressionMode.Decompress);
                using var reader = new StreamReader(gzip, Encoding.UTF8);
                VfxParticleSystemSnapshotView result = JsonConvert.DeserializeObject<VfxParticleSystemSnapshotView>(reader.ReadToEnd(), VfxCompositionDraftService.JsonSettings);
                message = "Snapshot 已加载。";
                return result;
            }
            catch (Exception exception) when (exception is IOException || exception is JsonException || exception is InvalidDataException)
            {
                message = "Snapshot 解压失败：" + exception.Message;
                return null;
            }
        }
    }
}
