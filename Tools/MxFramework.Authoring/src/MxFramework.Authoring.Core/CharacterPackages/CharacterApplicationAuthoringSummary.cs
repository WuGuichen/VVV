using System.Collections.Generic;

namespace MxFramework.Authoring
{
    public sealed class CharacterApplicationCompileBoundary
    {
        public List<string> AuthoringOnly { get; set; } = new List<string>();
        public List<string> RuntimeConfigCandidates { get; set; } = new List<string>();
    }

    public sealed class CharacterApplicationAuthoringSummary
    {
        public string SchemaVersion { get; set; } = "1.0";
        public string AuthoringNote { get; set; } = string.Empty;
        public string CharacterStableId { get; set; } = string.Empty;
        public string BodyProfileStableId { get; set; } = string.Empty;
        public string AttributeProfileStableId { get; set; } = string.Empty;
        public string EquipmentSchemaStableId { get; set; } = string.Empty;
        public List<string> Loadouts { get; set; } = new List<string>();
        public List<string> ResourceKeys { get; set; } = new List<string>();
        public List<CharacterAnimationGroupAuthoringSummary> AnimationGroups { get; set; } = new List<CharacterAnimationGroupAuthoringSummary>();
        public List<CharacterAnimationProfileAuthoringSummary> AnimationProfiles { get; set; } = new List<CharacterAnimationProfileAuthoringSummary>();
        public CharacterApplicationCompileBoundary CompileBoundary { get; set; } = new CharacterApplicationCompileBoundary();
    }

    public sealed class CharacterAnimationGroupAuthoringSummary
    {
        public string GroupId { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string SourceResourceKey { get; set; } = string.Empty;
        public string AnimationPolicy { get; set; } = "splitByClipNames";
        public string DefaultBlendSpaceId { get; set; } = string.Empty;
        public List<CharacterAnimationClipAuthoringSummary> Clips { get; set; } = new List<CharacterAnimationClipAuthoringSummary>();
        public List<CharacterAnimationBlendSpaceAuthoringSummary> BlendSpaces { get; set; } = new List<CharacterAnimationBlendSpaceAuthoringSummary>();
    }

    public sealed class CharacterAnimationClipAuthoringSummary
    {
        public string ClipId { get; set; } = string.Empty;
        public string SourceClipName { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public bool Loop { get; set; }
        public string RootMotionPolicy { get; set; } = "Ignore";
        public float Speed { get; set; } = 1f;
    }

    public sealed class CharacterAnimationBlendSpaceAuthoringSummary
    {
        public string BlendSpaceId { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string XParameter { get; set; } = "moveX";
        public string YParameter { get; set; } = "moveY";
        public string DefaultClipId { get; set; } = string.Empty;
        public List<CharacterAnimationBlendSpaceClipAuthoringSummary> Clips { get; set; } = new List<CharacterAnimationBlendSpaceClipAuthoringSummary>();
    }

    public sealed class CharacterAnimationBlendSpaceClipAuthoringSummary
    {
        public string ClipId { get; set; } = string.Empty;
        public float X { get; set; }
        public float Y { get; set; }
    }

    public sealed class CharacterAnimationProfileAuthoringSummary
    {
        public string ProfileId { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public List<CharacterAnimationSlotAuthoringSummary> Slots { get; set; } = new List<CharacterAnimationSlotAuthoringSummary>();
    }

    public sealed class CharacterAnimationSlotAuthoringSummary
    {
        public string SlotId { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string Purpose { get; set; } = string.Empty;
        public string AnimationGroupId { get; set; } = string.Empty;
        public string ResourceKey { get; set; } = string.Empty;
        public string PreloadPolicy { get; set; } = AuthoringResourcePreloadPolicies.AnimationWarmup;
        public bool Required { get; set; }
        public AuthoringResourceSelectionRef ResourceSelection { get; set; } = new AuthoringResourceSelectionRef();
    }
}
