using System.Collections.Generic;

namespace MxFramework.Authoring
{
    public sealed class AnimationAuthoringPackage
    {
        public string SchemaVersion { get; set; } = "1.0";
        public string PackageId { get; set; } = string.Empty;
        public string StableId { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string SkeletonProfileId { get; set; } = string.Empty;
        public string AvatarProfileId { get; set; } = string.Empty;
        public List<AnimationAuthoringSet> Sets { get; set; } = new List<AnimationAuthoringSet>();
        public List<AnimationAuthoringProfile> Profiles { get; set; } = new List<AnimationAuthoringProfile>();
        public List<AnimationAuthoringDiagnostic> Diagnostics { get; set; } = new List<AnimationAuthoringDiagnostic>();
        public Dictionary<string, string> Metadata { get; set; } = new Dictionary<string, string>();
    }

    public sealed class AnimationAuthoringSet
    {
        public string SetId { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string Version { get; set; } = "1.0";
        public string DefaultClipId { get; set; } = string.Empty;
        public string FallbackClipId { get; set; } = string.Empty;
        public List<AnimationLayerAuthoring> Layers { get; set; } = new List<AnimationLayerAuthoring>();
        public List<AnimationGroupAuthoring> Groups { get; set; } = new List<AnimationGroupAuthoring>();
        public List<AnimationActionBindingAuthoring> ActionBindings { get; set; } = new List<AnimationActionBindingAuthoring>();
        public AnimationCompatibilityExpectationAuthoring Compatibility { get; set; } = new AnimationCompatibilityExpectationAuthoring();
        public AnimationWarmupAuthoring Warmup { get; set; } = new AnimationWarmupAuthoring();
        public List<AnimationAuthoringDiagnostic> Diagnostics { get; set; } = new List<AnimationAuthoringDiagnostic>();
        public Dictionary<string, string> Metadata { get; set; } = new Dictionary<string, string>();
    }

    public sealed class AnimationAuthoringProfile
    {
        public string ProfileId { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string DefaultSetId { get; set; } = string.Empty;
        public string DefaultGroupId { get; set; } = string.Empty;
        public List<AnimationProfileSlotAuthoring> Slots { get; set; } = new List<AnimationProfileSlotAuthoring>();
        public AnimationCompatibilityExpectationAuthoring Compatibility { get; set; } = new AnimationCompatibilityExpectationAuthoring();
        public AnimationWarmupAuthoring Warmup { get; set; } = new AnimationWarmupAuthoring();
        public List<AnimationAuthoringDiagnostic> Diagnostics { get; set; } = new List<AnimationAuthoringDiagnostic>();
        public Dictionary<string, string> Metadata { get; set; } = new Dictionary<string, string>();
    }

    public sealed class AnimationProfileSlotAuthoring
    {
        public string SlotId { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string Purpose { get; set; } = string.Empty;
        public string SetId { get; set; } = string.Empty;
        public string GroupId { get; set; } = string.Empty;
        public string DefaultClipId { get; set; } = string.Empty;
        public string DefaultBlendId { get; set; } = string.Empty;
        public string PreloadPolicy { get; set; } = AuthoringResourcePreloadPolicies.AnimationWarmup;
        public bool Required { get; set; }
    }

    public sealed class AnimationLayerAuthoring
    {
        public string LayerId { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string Purpose { get; set; } = string.Empty;
        public float Weight { get; set; } = 1f;
        public bool Additive { get; set; }
        public string SyncLayerId { get; set; } = string.Empty;
        public string RootMotionPolicy { get; set; } = "Ignore";
        public AuthoringResourceSelectionRef AvatarMaskSelection { get; set; } = new AuthoringResourceSelectionRef();
        public List<string> Tags { get; set; } = new List<string>();
        public Dictionary<string, string> Metadata { get; set; } = new Dictionary<string, string>();
    }

    public sealed class AnimationActionBindingAuthoring
    {
        public string BindingId { get; set; } = string.Empty;
        public string ActionId { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string GroupId { get; set; } = string.Empty;
        public string ClipId { get; set; } = string.Empty;
        public string BlendId { get; set; } = string.Empty;
        public string TimelineId { get; set; } = string.Empty;
        public bool Required { get; set; }
        public List<string> Tags { get; set; } = new List<string>();
        public Dictionary<string, string> Metadata { get; set; } = new Dictionary<string, string>();
    }

    public sealed class AnimationGroupAuthoring
    {
        public string GroupId { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Usage { get; set; } = string.Empty;
        public List<AnimationClipMappingAuthoring> Clips { get; set; } = new List<AnimationClipMappingAuthoring>();
        public List<AnimationBlend1DAuthoring> Blend1D { get; set; } = new List<AnimationBlend1DAuthoring>();
        public List<AnimationBlend2DAuthoring> Blend2D { get; set; } = new List<AnimationBlend2DAuthoring>();
        public List<AnimationTimelineAuthoring> Timelines { get; set; } = new List<AnimationTimelineAuthoring>();
        public List<AnimationAuthoringDiagnostic> Diagnostics { get; set; } = new List<AnimationAuthoringDiagnostic>();
        public Dictionary<string, string> Metadata { get; set; } = new Dictionary<string, string>();
    }

    public sealed class AnimationClipMappingAuthoring
    {
        public string ClipId { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public AuthoringResourceSelectionRef SourceSelection { get; set; } = new AuthoringResourceSelectionRef();
        public string SourceSubClipId { get; set; } = string.Empty;
        public string SourceClipName { get; set; } = string.Empty;
        public string RuntimeResourceKey { get; set; } = string.Empty;
        public bool Loop { get; set; }
        public float Speed { get; set; } = 1f;
        public string RootMotionPolicy { get; set; } = "Ignore";
        public List<string> Tags { get; set; } = new List<string>();
        public List<AuthoringResourceSelectionRef> GeneratedArtifactSelections { get; set; } = new List<AuthoringResourceSelectionRef>();
        public List<AnimationAuthoringDiagnostic> Diagnostics { get; set; } = new List<AnimationAuthoringDiagnostic>();
        public Dictionary<string, string> Metadata { get; set; } = new Dictionary<string, string>();
    }

    public sealed class AnimationBlend1DAuthoring
    {
        public string BlendId { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string Parameter { get; set; } = string.Empty;
        public string DefaultClipId { get; set; } = string.Empty;
        public List<AnimationBlend1DPointAuthoring> Points { get; set; } = new List<AnimationBlend1DPointAuthoring>();
        public List<AnimationAuthoringDiagnostic> Diagnostics { get; set; } = new List<AnimationAuthoringDiagnostic>();
        public Dictionary<string, string> Metadata { get; set; } = new Dictionary<string, string>();
    }

    public sealed class AnimationBlend1DPointAuthoring
    {
        public string ClipId { get; set; } = string.Empty;
        public float Value { get; set; }
        public float Weight { get; set; } = 1f;
    }

    public sealed class AnimationBlend2DAuthoring
    {
        public string BlendId { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string XParameter { get; set; } = string.Empty;
        public string YParameter { get; set; } = string.Empty;
        public string DefaultClipId { get; set; } = string.Empty;
        public List<AnimationBlend2DPointAuthoring> Points { get; set; } = new List<AnimationBlend2DPointAuthoring>();
        public List<AnimationAuthoringDiagnostic> Diagnostics { get; set; } = new List<AnimationAuthoringDiagnostic>();
        public Dictionary<string, string> Metadata { get; set; } = new Dictionary<string, string>();
    }

    public sealed class AnimationBlend2DPointAuthoring
    {
        public string ClipId { get; set; } = string.Empty;
        public float X { get; set; }
        public float Y { get; set; }
        public float Weight { get; set; } = 1f;
    }

    public sealed class AnimationTimelineAuthoring
    {
        public string TimelineId { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string ClipId { get; set; } = string.Empty;
        public string TimeDomain { get; set; } = "Seconds";
        public List<AnimationTimelineEventAuthoring> Events { get; set; } = new List<AnimationTimelineEventAuthoring>();
        public List<AnimationAuthoringDiagnostic> Diagnostics { get; set; } = new List<AnimationAuthoringDiagnostic>();
        public Dictionary<string, string> Metadata { get; set; } = new Dictionary<string, string>();
    }

    public sealed class AnimationTimelineEventAuthoring
    {
        public string EventId { get; set; } = string.Empty;
        public string ClipId { get; set; } = string.Empty;
        public string TimeDomain { get; set; } = "Seconds";
        public float Time { get; set; }
        public string EventKind { get; set; } = string.Empty;
        public AuthoringResourceSelectionRef ResourceSelection { get; set; } = new AuthoringResourceSelectionRef();
        public string PayloadJson { get; set; } = string.Empty;
        public Dictionary<string, string> Metadata { get; set; } = new Dictionary<string, string>();
    }

    public sealed class AnimationCompatibilityExpectationAuthoring
    {
        public string CompatibilityId { get; set; } = string.Empty;
        public string SkeletonProfileId { get; set; } = string.Empty;
        public string AvatarProfileId { get; set; } = string.Empty;
        public string CoordinateConvention { get; set; } = string.Empty;
        public bool AllowRetargeting { get; set; }
        public AuthoringResourceSelectionRef CompatibilityProfileSelection { get; set; } = new AuthoringResourceSelectionRef();
        public AuthoringResourceSelectionRef AvatarMaskSelection { get; set; } = new AuthoringResourceSelectionRef();
        public List<string> RequiredBoneIds { get; set; } = new List<string>();
        public List<string> RequiredSocketIds { get; set; } = new List<string>();
        public List<AnimationAuthoringDiagnostic> Diagnostics { get; set; } = new List<AnimationAuthoringDiagnostic>();
        public Dictionary<string, string> Metadata { get; set; } = new Dictionary<string, string>();
    }

    public sealed class AnimationWarmupAuthoring
    {
        public string WarmupId { get; set; } = string.Empty;
        public string PreloadPolicy { get; set; } = AuthoringResourcePreloadPolicies.AnimationWarmup;
        public bool IncludeDefaultClip { get; set; } = true;
        public bool IncludeFallbackClip { get; set; } = true;
        public bool IncludeActionBindings { get; set; } = true;
        public bool IncludeBlendPoints { get; set; } = true;
        public List<string> RequiredClipIds { get; set; } = new List<string>();
        public List<string> RequiredBlendIds { get; set; } = new List<string>();
        public List<AuthoringResourceSelectionRef> AvatarMaskSelections { get; set; } = new List<AuthoringResourceSelectionRef>();
        public List<AuthoringResourceSelectionRef> VfxSelections { get; set; } = new List<AuthoringResourceSelectionRef>();
        public List<AuthoringResourceSelectionRef> AudioCueSelections { get; set; } = new List<AuthoringResourceSelectionRef>();
        public List<AuthoringResourceSelectionRef> GeneratedArtifactSelections { get; set; } = new List<AuthoringResourceSelectionRef>();
        public List<AuthoringResourceSelectionRef> AdditionalResourceSelections { get; set; } = new List<AuthoringResourceSelectionRef>();
        public List<AnimationAuthoringDiagnostic> Diagnostics { get; set; } = new List<AnimationAuthoringDiagnostic>();
        public Dictionary<string, string> Metadata { get; set; } = new Dictionary<string, string>();
    }

    public sealed class AnimationAuthoringDiagnostic
    {
        public CharacterAuthoringValidationSeverity Severity { get; set; }
        public CharacterAuthoringValidationGate Gate { get; set; } = CharacterAuthoringValidationGate.WarningOnly;
        public string Code { get; set; } = string.Empty;
        public string SourcePath { get; set; } = string.Empty;
        public string SourceObjectPath { get; set; } = string.Empty;
        public string Field { get; set; } = string.Empty;
        public string SetId { get; set; } = string.Empty;
        public string GroupId { get; set; } = string.Empty;
        public string ClipId { get; set; } = string.Empty;
        public string BlendId { get; set; } = string.Empty;
        public string EventId { get; set; } = string.Empty;
        public string ResourceStableId { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public string SuggestedFix { get; set; } = string.Empty;
    }
}
